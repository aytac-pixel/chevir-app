// Requires homuler/MediaPipeUnityPlugin (Tasks API, written against the v0.16 API surface). The assembly
// defines CEVIR_MEDIAPIPE by itself when the package com.github.homuler.mediapipe is installed (asmdef
// version define); without the package this assembly is skipped, so Core still compiles.
using System;
using System.Collections;
using Cevir.Core.LanguagePacks;
using Cevir.Core.Recognition;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.FaceLandmarker;
using Mediapipe.Tasks.Vision.HandLandmarker;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif
using MpImage = Mediapipe.Image;
using MpImageFormat = Mediapipe.ImageFormat;

namespace Cevir.Integrations.MediaPipe
{
    /// <summary>
    /// On-device landmark provider: front camera → MediaPipe HandLandmarker (manual signs, 21 points per hand)
    /// and FaceLandmarker (non-manual: 478 points + blendshapes + head pose), both in LIVE_STREAM mode.
    /// Video never leaves the device; only landmark values reach the rest of the app.
    ///
    /// Threading: MediaPipe invokes result callbacks on its own worker thread. Results are copied into
    /// back buffers under a lock and published on the main thread in Update. A frame is published when a
    /// hand result arrives; the face data in it is the latest face result (at most one inference step older).
    /// </summary>
    public sealed class MediaPipeLandmarkProvider : MonoBehaviour, ILandmarkProvider, IPackAwareLandmarkProvider, ICameraPreviewSource
    {
        [Header("Models (.task files renamed to .bytes)")]
        [SerializeField] private TextAsset handLandmarkerModel;
        [SerializeField] private TextAsset faceLandmarkerModel;

        [Tooltip("Uses the active pack's model overrides when it has them.")]
        [SerializeField] private bool allowPackOverrides = true;

        [Header("Camera")]
        [SerializeField] private int requestedWidth = 640;
        [SerializeField] private int requestedHeight = 480;
        [SerializeField] private int requestedFps = 30;

        [Tooltip("Frames per second sent to MediaPipe. 15+ is the plan's target on mid-range phones.")]
        [SerializeField, Range(5, 30)] private int inferenceFps = 20;

        [Tooltip("GPU needs extra GL context setup in the plugin on Android; CPU works everywhere and is fast enough at 640x480.")]
        [SerializeField] private bool useGpuDelegate;

        [Header("Detection")]
        [SerializeField, Range(1, 2)] private int maxHands = 2;
        [SerializeField, Range(0f, 1f)] private float minHandDetectionConfidence = 0.5f;
        [SerializeField, Range(0f, 1f)] private float minHandPresenceConfidence = 0.5f;
        [SerializeField, Range(0f, 1f)] private float minTrackingConfidence = 0.5f;
        [SerializeField] private bool enableFace = true;

        [Header("Orientation")]
        [Tooltip("Unity textures are bottom-up; MediaPipe expects top-down. Flipping y fixes landmarks.")]
        [SerializeField] private bool flipVertically = true;

        [Tooltip("Extra Left/Right swap for devices whose front camera already delivers a mirrored image. Verify per device.")]
        [SerializeField] private bool invertHandedness;

        private WebCamTexture _camera;
        private Texture2D _cpuTexture;
        private Color32[] _pixels;

        private HandLandmarker _hands;
        private FaceLandmarker _face;

        private readonly object _lock = new object();
        private readonly LandmarkFrame _back = new LandmarkFrame();
        private readonly LandmarkFrame _front = new LandmarkFrame();
        private bool _handsDirty;

        private float _nextInferenceTime;
        private long _lastSentTimestamp;
        private readonly System.Diagnostics.Stopwatch _clock = new System.Diagnostics.Stopwatch();

        public bool IsRunning { get; private set; }
        public WebCamTexture CameraTexture => _camera;

        private bool _frontFacing;

        public Texture PreviewTexture => _camera;
        public int PreviewRotation => _camera != null ? _camera.videoRotationAngle : 0;
        public bool PreviewVerticallyFlipped => _camera != null && _camera.videoVerticallyMirrored;
        public bool PreviewMirrored => _frontFacing;
        public bool CameraDenied { get; private set; }

        public event Action<LandmarkFrame> FrameReady;

        public void ApplyPack(ILanguagePack pack)
        {
            if (!allowPackOverrides || pack == null) return;
            if (pack.HandLandmarkerModelOverride != null) handLandmarkerModel = pack.HandLandmarkerModelOverride;
            if (pack.FaceLandmarkerModelOverride != null) faceLandmarkerModel = pack.FaceLandmarkerModelOverride;
        }

        public void StartCapture()
        {
            if (IsRunning || _starting) return;
            _starting = true;
            StartCoroutine(StartGuarded());
        }

        private bool _starting;

        private IEnumerator StartGuarded()
        {
            yield return StartRoutine();
            _starting = false;
        }

        public void StopCapture()
        {
            IsRunning = false;
            _starting = false;
            StopAllCoroutines();
            if (_camera != null)
            {
                _camera.Stop();
                Destroy(_camera);
                _camera = null;
            }

            CloseLandmarkers();
        }

        private IEnumerator StartRoutine()
        {
            CameraDenied = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                bool answered = false;
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ => answered = true;
                callbacks.PermissionDenied += _ => answered = true;
                callbacks.PermissionDeniedAndDontAskAgain += _ => answered = true;
                Permission.RequestUserPermission(Permission.Camera, callbacks);
                while (!answered) yield return null;
            }

            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                CameraDenied = true;
                Debug.LogWarning("[Çevir] Camera permission denied.");
                yield break;
            }
#else
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
                if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
                {
                    CameraDenied = true;
                    Debug.LogWarning("[Çevir] Camera permission denied.");
                    yield break;
                }
            }
#endif

            string deviceName = null;
            foreach (WebCamDevice device in WebCamTexture.devices)
            {
                if (device.isFrontFacing)
                {
                    deviceName = device.name;
                    _frontFacing = true;
                    break;
                }
            }

            if (deviceName == null && WebCamTexture.devices.Length > 0) deviceName = WebCamTexture.devices[0].name;
            if (deviceName == null)
            {
                Debug.LogError("[Çevir] No camera found.");
                yield break;
            }

            _camera = new WebCamTexture(deviceName, requestedWidth, requestedHeight, requestedFps);
            _camera.Play();

            // Mobile cameras report 16x16 until the first real frame arrives.
            float timeout = Time.realtimeSinceStartup + 5f;
            while (_camera.width <= 16 && Time.realtimeSinceStartup < timeout) yield return null;
            if (_camera.width <= 16)
            {
                Debug.LogError("[Çevir] Camera did not start.");
                yield break;
            }

            _cpuTexture = new Texture2D(_camera.width, _camera.height, TextureFormat.RGBA32, false);
            _pixels = new Color32[_camera.width * _camera.height];

            CreateLandmarkers();
            _clock.Restart();
            _lastSentTimestamp = -1;
            IsRunning = true;
        }

        private void CreateLandmarkers()
        {
            if (handLandmarkerModel == null)
            {
                Debug.LogError("[Çevir] Hand landmarker model is not assigned.");
                return;
            }

            BaseOptions.Delegate delegateType = useGpuDelegate && SystemInfo.supportsComputeShaders
                ? BaseOptions.Delegate.GPU
                : BaseOptions.Delegate.CPU;

            var handOptions = new HandLandmarkerOptions(
                new BaseOptions(delegateType, modelAssetBuffer: handLandmarkerModel.bytes),
                runningMode: RunningMode.LIVE_STREAM,
                numHands: maxHands,
                minHandDetectionConfidence: minHandDetectionConfidence,
                minHandPresenceConfidence: minHandPresenceConfidence,
                minTrackingConfidence: minTrackingConfidence,
                resultCallback: OnHandResult);
            _hands = HandLandmarker.CreateFromOptions(handOptions);

            if (enableFace && faceLandmarkerModel != null)
            {
                var faceOptions = new FaceLandmarkerOptions(
                    new BaseOptions(delegateType, modelAssetBuffer: faceLandmarkerModel.bytes),
                    runningMode: RunningMode.LIVE_STREAM,
                    numFaces: 1,
                    outputFaceBlendshapes: true,
                    outputFaceTransformationMatrixes: true,
                    resultCallback: OnFaceResult);
                _face = FaceLandmarker.CreateFromOptions(faceOptions);
            }
        }

        private void CloseLandmarkers()
        {
            if (_hands != null)
            {
                _hands.Close();
                _hands = null;
            }

            if (_face != null)
            {
                _face.Close();
                _face = null;
            }
        }

        private void Update()
        {
            if (!IsRunning || _camera == null || _hands == null) return;

            if (_camera.didUpdateThisFrame && Time.unscaledTime >= _nextInferenceTime)
            {
                _nextInferenceTime = Time.unscaledTime + 1f / inferenceFps;
                SendFrame();
            }

            PublishIfReady();
        }

        private void SendFrame()
        {
            long timestamp = _clock.ElapsedMilliseconds;
            if (timestamp <= _lastSentTimestamp) return; // MediaPipe requires strictly increasing timestamps.
            _lastSentTimestamp = timestamp;

            _camera.GetPixels32(_pixels);
            _cpuTexture.SetPixels32(_pixels);

            // Rotation so the landmarker sees an upright face on phones held in portrait.
            var processing = new ImageProcessingOptions(rotationDegrees: NormalizeRotation(-_camera.videoRotationAngle));

            using (var image = new MpImage(MpImageFormat.Types.Format.Srgba, _cpuTexture.width, _cpuTexture.height,
                       _cpuTexture.width * 4, _cpuTexture.GetPixelData<byte>(0)))
            {
                _hands.DetectAsync(image, timestamp, processing);
                if (_face != null) _face.DetectAsync(image, timestamp, processing);
            }

            lock (_lock)
            {
                bool rotated = (_camera.videoRotationAngle % 180) != 0;
                _back.ImageAspect = rotated ? _camera.height / (float)_camera.width : _camera.width / (float)_camera.height;
            }
        }

        private static int NormalizeRotation(int degrees)
        {
            degrees %= 360;
            if (degrees < 0) degrees += 360;
            return degrees / 90 * 90;
        }

        // ---------------------------------------------------------------- callbacks (MediaPipe thread)

        private void OnHandResult(HandLandmarkerResult result, MpImage image, long timestampMillisec)
        {
            lock (_lock)
            {
                _back.LeftHand.Clear();
                _back.RightHand.Clear();

                if (result.handLandmarks != null)
                {
                    for (int h = 0; h < result.handLandmarks.Count; h++)
                    {
                        string label = "Right";
                        float score = 0f;
                        if (result.handedness != null && h < result.handedness.Count && result.handedness[h].categories.Count > 0)
                        {
                            label = result.handedness[h].categories[0].categoryName;
                            score = result.handedness[h].categories[0].score;
                        }

                        // MediaPipe labels hands assuming a mirrored (selfie) image. The raw front-camera image
                        // is not mirrored, so its labels come out swapped; a vertical flip is itself a reflection
                        // and cancels that. Net: labels are right when flipping, swapped otherwise.
                        bool isLeft = label == "Left";
                        if (!flipVertically) isLeft = !isLeft;
                        if (invertHandedness) isLeft = !isLeft;

                        HandLandmarks target = isLeft ? _back.LeftHand : _back.RightHand;
                        if (target.IsPresent && target.Confidence >= score) continue;

                        var points = result.handLandmarks[h].landmarks;
                        for (int i = 0; i < HandLandmarks.Count && i < points.Count; i++)
                        {
                            NormalizedLandmark p = points[i];
                            target.Points[i] = new Vector3(p.x, flipVertically ? 1f - p.y : p.y, p.z);
                        }

                        target.IsPresent = true;
                        target.Confidence = score;
                    }
                }

                _back.TimestampSeconds = timestampMillisec / 1000.0;
                _handsDirty = true;
            }
        }

        private void OnFaceResult(FaceLandmarkerResult result, MpImage image, long timestampMillisec)
        {
            lock (_lock)
            {
                FaceLandmarks face = _back.Face;
                face.Clear();
                if (result.faceLandmarks == null || result.faceLandmarks.Count == 0) return;

                var points = result.faceLandmarks[0].landmarks;
                for (int i = 0; i < FaceLandmarks.Count && i < points.Count; i++)
                {
                    NormalizedLandmark p = points[i];
                    face.Points[i] = new Vector3(p.x, flipVertically ? 1f - p.y : p.y, p.z);
                }

                face.IsPresent = points.Count >= 468;

                if (result.faceBlendshapes != null && result.faceBlendshapes.Count > 0)
                {
                    FaceBlendshapes shapes = default;
                    foreach (Category category in result.faceBlendshapes[0].categories)
                    {
                        shapes.Set(category.categoryName, category.score);
                    }

                    face.Blendshapes = shapes;
                    face.HasBlendshapes = true;
                }

                if (result.facialTransformationMatrixes != null && result.facialTransformationMatrixes.Count > 0)
                {
                    Matrix4x4 m = result.facialTransformationMatrixes[0];
                    Vector3 euler = m.rotation.eulerAngles;
                    face.HeadEuler = flipVertically ? new Vector3(-euler.x, euler.y, -euler.z) : euler;
                    face.HasHeadPose = true;
                }
            }
        }

        // ---------------------------------------------------------------- main thread

        private void PublishIfReady()
        {
            lock (_lock)
            {
                if (!_handsDirty) return;
                _handsDirty = false;
                _front.CopyFrom(_back);
            }

            if (FrameReady != null) FrameReady(_front);
        }

        private void OnDestroy()
        {
            StopCapture();
            if (_cpuTexture != null) Destroy(_cpuTexture);
        }
    }
}
