using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Cevir.Core.Game;
using Cevir.Core.LanguagePacks;
using Cevir.Core.Lesson;
using Cevir.Core.Recognition;
using UnityEngine;

namespace Cevir.Core.Cloud
{
    /// <summary>
    /// Saves the learner's camera attempts for the dataset, only while they allowed it
    /// («Məlumatlarımla Chevir-i inkişaf etdirməyə kömək et», PlayerProgress.dataConsent) and are signed in.
    /// For every scored attempt it stores one row in table sign_samples: the MediaPipe hand, pose and face landmarks of
    /// every frame (JSON) with the target sign, the model's score and the verdict, plus a short low-resolution video of
    /// the last seconds (Motion-JPEG .avi) in the private storage bucket "sign-samples".
    /// Nothing is kept on the phone: an attempt that cannot be sent (offline) is dropped.
    /// </summary>
    public sealed class SignSampleCollector : MonoBehaviour
    {
        public const string Bucket = "sign-samples";
        private const string Table = "sign_samples";

        // Landmarks: at most this many seconds per attempt (the newest are kept).
        private const float MaxLandmarkSeconds = 10f;
        private const int MaxLandmarkFrames = 320;

        // Video: last few seconds, small and few frames so each clip is about 100-200 KB.
        private const float VideoSeconds = 3f;
        private const float VideoFps = 8f;
        private const int VideoWidth = 144;
        private const int JpegQuality = 50;

        private const int MaxQueued = 3;

        /// <summary>Face mesh points kept per frame (brows, eyes, nose, lips, jaw outline): enough for non-manual signals.</summary>
        private static readonly int[] FacePoints =
        {
            1, 4, 6, 10, 13, 14, 17, 21, 33, 46, 52, 55, 61, 65, 70, 78, 105, 107, 133, 145, 152, 159, 172, 197, 234, 251,
            263, 276, 282, 285, 291, 295, 300, 308, 334, 336, 362, 374, 386, 397, 454, 468, 473
        };

        private CloudService _cloud;
        private GameManager _game;
        private SignPracticeController _practice;
        private ILandmarkProvider _provider;
        private ICameraFrameSource _frames;
        private float _nextLookup;

        private bool _recording;
        private float _lastAspect = 0.75f;
        private double _attemptStart;
        private int _handFrames;
        private readonly List<string> _landmarks = new List<string>();
        private readonly StringBuilder _sb = new StringBuilder(4096);

        private struct VideoFrame
        {
            public double Time;
            public byte[] Jpeg;
        }

        private readonly List<VideoFrame> _video = new List<VideoFrame>();
        private double _nextVideoFrame;
        private Texture2D _small;
        private Color32[] _smallPixels;
        private int _videoW, _videoH;

        private sealed class Sample
        {
            public string RowJson; // without video_path
            public byte[] Video;
        }

        private readonly Queue<Sample> _queue = new Queue<Sample>();
        private bool _sending;

        public static SignSampleCollector Attach(CloudService cloud, GameManager game)
        {
            SignSampleCollector collector = cloud.GetComponent<SignSampleCollector>();
            if (collector == null) collector = cloud.gameObject.AddComponent<SignSampleCollector>();
            collector._cloud = cloud;
            collector._game = game;
            return collector;
        }

        /// <summary>True while attempts are saved: signed in and the learner allowed it.</summary>
        public bool Collecting =>
            _cloud != null && _cloud.State == CloudState.SignedIn && _game != null && _game.Progress != null && _game.Progress.dataConsent;

        private void Update()
        {
            if (_practice == null && Time.unscaledTime >= _nextLookup)
            {
                _nextLookup = Time.unscaledTime + 1f;
                SignPracticeController practice = FindObjectOfType<SignPracticeController>(true);
                if (practice != null) Hook(practice);
            }

            // The lesson screen may swap the provider; follow it.
            if (_practice != null && _practice.Provider != _provider) HookProvider(_practice.Provider);
        }

        private void OnDestroy()
        {
            if (_practice != null)
            {
                _practice.AttemptStarted -= OnAttemptStarted;
                _practice.AttemptScored -= OnAttemptScored;
            }

            HookProvider(null);
            if (_small != null) Destroy(_small);
        }

        private void Hook(SignPracticeController practice)
        {
            _practice = practice;
            _practice.AttemptStarted += OnAttemptStarted;
            _practice.AttemptScored += OnAttemptScored;
            HookProvider(practice.Provider);
        }

        private void HookProvider(ILandmarkProvider provider)
        {
            if (_provider != null) _provider.FrameReady -= OnLandmarks;
            if (_frames != null) _frames.UprightFrame -= OnPicture;
            _provider = provider;
            _frames = provider as ICameraFrameSource;
            if (_provider != null) _provider.FrameReady += OnLandmarks;
            if (_frames != null) _frames.UprightFrame += OnPicture;
        }

        // ------------------------------------------------------------------ recording

        private void OnAttemptStarted()
        {
            _recording = Collecting;
            _landmarks.Clear();
            _video.Clear();
            _handFrames = 0;
            _attemptStart = Time.realtimeSinceStartupAsDouble;
            _nextVideoFrame = 0;
        }

        private void OnLandmarks(LandmarkFrame frame)
        {
            if (!_recording) return;
            double t = Time.realtimeSinceStartupAsDouble - _attemptStart;
            if (frame.AnyHandPresent) _handFrames++;
            if (frame.ImageAspect > 0f) _lastAspect = frame.ImageAspect;

            _sb.Length = 0;
            _sb.Append("{\"t\":").Append(F(t));
            AppendHand(",\"l\":", frame.LeftHand);
            AppendHand(",\"r\":", frame.RightHand);
            if (frame.Pose.IsPresent)
            {
                _sb.Append(",\"p\":");
                AppendPoints(frame.Pose.Points, null, PoseLandmarks.Count);
            }

            FaceLandmarks face = frame.Face;
            if (face.IsPresent)
            {
                _sb.Append(",\"f\":");
                AppendPoints(face.Points, FacePoints, FacePoints.Length);
                if (face.HasBlendshapes)
                {
                    FaceBlendshapes b = face.Blendshapes;
                    _sb.Append(",\"b\":[").Append(F(b.BrowInnerUp)).Append(',').Append(F(b.BrowOuterUpLeft)).Append(',')
                        .Append(F(b.BrowOuterUpRight)).Append(',').Append(F(b.BrowDownLeft)).Append(',').Append(F(b.BrowDownRight))
                        .Append(',').Append(F(b.JawOpen)).Append(',').Append(F(b.MouthFunnel)).Append(',').Append(F(b.MouthPucker))
                        .Append(',').Append(F(b.CheekPuff)).Append(']');
                }

                if (face.HasHeadPose)
                {
                    _sb.Append(",\"h\":[").Append(F1(face.HeadEuler.x)).Append(',').Append(F1(face.HeadEuler.y)).Append(',')
                        .Append(F1(face.HeadEuler.z)).Append(']');
                }
            }

            _sb.Append('}');
            _landmarks.Add(_sb.ToString());
            if (_landmarks.Count > MaxLandmarkFrames) _landmarks.RemoveAt(0);
        }

        private void AppendHand(string key, HandLandmarks hand)
        {
            if (!hand.IsPresent) return;
            _sb.Append(key);
            AppendPoints(hand.Points, null, HandLandmarks.Count);
        }

        private void AppendPoints(Vector3[] points, int[] subset, int count)
        {
            _sb.Append('[');
            for (int i = 0; i < count; i++)
            {
                Vector3 p = points[subset != null ? subset[i] : i];
                if (i > 0) _sb.Append(',');
                _sb.Append(F(p.x)).Append(',').Append(F(p.y)).Append(',').Append(F(p.z));
            }

            _sb.Append(']');
        }

        private void OnPicture(Color32[] pixels, int width, int height)
        {
            if (!_recording || width <= 0 || height <= 0) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < _nextVideoFrame) return;
            _nextVideoFrame = now + 1.0 / VideoFps;

            int w = Mathf.Min(VideoWidth, width) & ~1;
            int h = Mathf.RoundToInt(w * height / (float)width) & ~1;
            if (w <= 0 || h <= 0) return;
            if (_small == null || _videoW != w || _videoH != h)
            {
                if (_small != null) Destroy(_small);
                _small = new Texture2D(w, h, TextureFormat.RGB24, false);
                _smallPixels = new Color32[w * h];
                _videoW = w;
                _videoH = h;
                _video.Clear(); // a clip keeps one size
            }

            // Nearest-neighbour downscale; the source rows are top-down, a texture's are bottom-up.
            for (int y = 0; y < h; y++)
            {
                int sourceRow = (int)((y + 0.5f) * height / h) * width;
                int targetRow = (h - 1 - y) * w;
                for (int x = 0; x < w; x++)
                {
                    _smallPixels[targetRow + x] = pixels[sourceRow + (int)((x + 0.5f) * width / w)];
                }
            }

            _small.SetPixels32(_smallPixels);
            _small.Apply(false);
            _video.Add(new VideoFrame { Time = now, Jpeg = _small.EncodeToJPG(JpegQuality) });
            while (_video.Count > 0 && now - _video[0].Time > VideoSeconds) _video.RemoveAt(0);
        }

        private void OnAttemptScored(MatchResult result)
        {
            if (!_recording) return;
            _recording = false;
            if (!Collecting || _handFrames < 3 || _landmarks.Count == 0) return; // nothing signed: not useful

            double duration = Time.realtimeSinceStartupAsDouble - _attemptStart;
            byte[] video = null;
            if (_video.Count >= 4)
            {
                var jpegs = new List<byte[]>(_video.Count);
                foreach (VideoFrame frame in _video) jpegs.Add(frame.Jpeg);
                double span = _video[_video.Count - 1].Time - _video[0].Time;
                float fps = span > 0.1 ? (float)((_video.Count - 1) / span) : VideoFps;
                video = MjpegAvi.Write(jpegs, _videoW, _videoH, fps);
            }

            _video.Clear();
            string row = BuildRow(result, (float)duration);
            _landmarks.Clear();

            if (_queue.Count >= MaxQueued) _queue.Dequeue(); // slow connection: keep the newest
            _queue.Enqueue(new Sample { RowJson = row, Video = video });
            SendNext();
        }

        private string BuildRow(MatchResult result, float duration)
        {
            SignDefinition target = _practice != null ? _practice.CurrentTarget : null;
            string context = string.Empty, contextKind = string.Empty;
            if (_game.CurrentStory != null && _game.CurrentStory.Story != null)
            {
                context = _game.CurrentStory.Story.Id;
                contextKind = "story";
            }
            else if (_game.CurrentSession != null && _game.CurrentSession.Lesson != null)
            {
                context = _game.CurrentSession.Lesson.Id;
                contextKind = _game.CurrentSession.Kind.ToString().ToLowerInvariant();
            }

            var sb = new StringBuilder(64 * 1024);
            sb.Append('{');
            Str(sb, "user_id", _cloud.UserId).Append(',');
            Str(sb, "sign_id", target != null ? target.SignId : result.SignId).Append(',');
            Str(sb, "gloss", target != null ? target.Gloss : null).Append(',');
            Str(sb, "sign_kind", target != null ? target.Kind.ToString() : null).Append(',');
            Str(sb, "pack", _game.ActivePack != null ? _game.ActivePack.LanguageCode : null).Append(',');
            Str(sb, "context", context).Append(',');
            Str(sb, "context_kind", contextKind).Append(',');
            Str(sb, "judge", _practice != null ? _practice.LastJudge : null).Append(',');
            sb.Append("\"score\":").Append(F(Mathf.Clamp(result.ClassifierProbability >= 0f ? result.ClassifierProbability : result.ManualScore, 0f, 1f))).Append(',');
            sb.Append("\"correct\":").Append(result.IsCorrect ? "true" : "false").Append(',');
            Str(sb, "feedback", result.Feedback.ToString()).Append(',');
            sb.Append("\"duration_s\":").Append(F(duration)).Append(',');
            sb.Append("\"frame_count\":").Append(_landmarks.Count.ToString(CultureInfo.InvariantCulture)).Append(',');
            Str(sb, "app_version", Application.version).Append(',');
            Str(sb, "device_model", SystemInfo.deviceModel).Append(',');
            Str(sb, "os", SystemInfo.operatingSystem).Append(',');
            sb.Append("\"left_handed\":").Append(_game.Progress.leftHanded ? "true" : "false").Append(',');

            // landmarks: v1 = image-normalised x, y (top-down, not mirrored) and z; hands 21 points, pose 33,
            // face = the mesh points listed in face_idx; b = blendshapes, h = head pitch/yaw/roll in degrees.
            sb.Append("\"landmarks\":{\"v\":1,\"aspect\":").Append(F(_lastAspect)).Append(",\"face_idx\":[");
            for (int i = 0; i < FacePoints.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(FacePoints[i].ToString(CultureInfo.InvariantCulture));
            }

            sb.Append("],\"blendshapes\":[\"browInnerUp\",\"browOuterUpLeft\",\"browOuterUpRight\",\"browDownLeft\",\"browDownRight\"," +
                      "\"jawOpen\",\"mouthFunnel\",\"mouthPucker\",\"cheekPuff\"],\"frames\":[");
            for (int i = 0; i < _landmarks.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(_landmarks[i]);
            }

            sb.Append("]}");
            return sb.ToString(); // the closing brace is added with the video path
        }

        // ------------------------------------------------------------------ upload

        private void SendNext()
        {
            if (_sending || _queue.Count == 0) return;
            SupabaseClient client = _cloud != null ? _cloud.Client : null;
            if (client == null || !client.HasSession)
            {
                _queue.Clear();
                return;
            }

            _sending = true;
            Sample sample = _queue.Dequeue();
            string userId = client.UserId;

            Action<string> insert = path =>
            {
                string json = sample.RowJson + ",\"video_path\":" + (path != null ? Quote(path) : "null") + "}";
                client.Insert(Table, json, r =>
                {
                    if (!r.Ok && path != null) client.DeleteFile(Bucket, path, ignored => { });
                    _sending = false;
                    SendNext();
                });
            };

            if (sample.Video == null)
            {
                insert(null);
                return;
            }

            string file = userId + "/" + DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture) + "/" + Guid.NewGuid().ToString("N") + ".avi";
            client.UploadFile(Bucket, file, sample.Video, "video/x-msvideo", false, r => insert(r.Ok ? file : null));
        }

        // ------------------------------------------------------------------ JSON helpers

        private static string F(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return "0";
            return Math.Round(value, 4).ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static string F1(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return "0";
            return Math.Round(value, 1).ToString("0.#", CultureInfo.InvariantCulture);
        }

        private static StringBuilder Str(StringBuilder sb, string key, string value)
        {
            return sb.Append('"').Append(key).Append("\":").Append(value != null ? Quote(value) : "null");
        }

        private static string Quote(string value)
        {
            var sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }

            return sb.Append('"').ToString();
        }
    }
}
