using System.Collections.Generic;
using Cevir.Core.Recognition;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>
    /// Draws the tracked hand skeletons over the live camera, mirrored like the preview, so learners see that their
    /// hands are being followed. Landmarks are in the upright image; the mapping uses the preview's cover-fit size.
    /// Check alignment per device (rotation / mirroring), like the rest of the camera path.
    /// </summary>
    public sealed class LandmarkOverlayUI : MonoBehaviour
    {
        private static readonly int[] Bones =
        {
            0, 1, 1, 2, 2, 3, 3, 4, 0, 5, 5, 6, 6, 7, 7, 8, 5, 9, 9, 10, 10, 11, 11, 12,
            9, 13, 13, 14, 14, 15, 15, 16, 13, 17, 0, 17, 17, 18, 18, 19, 19, 20
        };

        [SerializeField] private CameraPreviewUI preview;
        [SerializeField] private Sprite dotSprite;
        [SerializeField] private Color dominantColor = new Color32(0x1E, 0xB9, 0xF0, 0xFF);
        [SerializeField] private Color otherColor = new Color(1f, 1f, 1f, 0.9f);
        [SerializeField] private float dotSize = 16f;
        [SerializeField] private float boneWidth = 5f;

        private readonly List<Image> _dots = new List<Image>();
        private readonly List<Image> _bones = new List<Image>();
        private ILandmarkProvider _provider;
        private readonly LandmarkFrame _latest = new LandmarkFrame();
        private bool _dirty;

        public HandSide DominantHand { get; set; } = HandSide.Right;

        public void Bind(ILandmarkProvider provider)
        {
            if (_provider != null) _provider.FrameReady -= OnFrame;
            _provider = provider;
            if (_provider != null && isActiveAndEnabled) _provider.FrameReady += OnFrame;
        }

        private void OnEnable()
        {
            if (_provider != null) _provider.FrameReady += OnFrame;
        }

        private void OnDisable()
        {
            if (_provider != null) _provider.FrameReady -= OnFrame;
            Hide(0, 0);
        }

        private void OnFrame(LandmarkFrame frame)
        {
            _latest.CopyFrom(frame);
            _dirty = true;
        }

        private void LateUpdate()
        {
            if (!_dirty) return;
            _dirty = false;

            Vector2 size = preview != null ? preview.UprightSize : ((RectTransform)transform).rect.size;
            if (size.x < 1f || size.y < 1f) size = ((RectTransform)transform).rect.size;
            bool mirrored = preview == null || preview.Source == null || preview.Source.PreviewMirrored;

            int dots = 0, bones = 0;
            DrawHand(_latest.RightHand, DominantHand == HandSide.Right, size, mirrored, ref dots, ref bones);
            DrawHand(_latest.LeftHand, DominantHand == HandSide.Left, size, mirrored, ref dots, ref bones);
            Hide(dots, bones);
        }

        private void DrawHand(HandLandmarks hand, bool dominant, Vector2 size, bool mirrored, ref int dots, ref int bones)
        {
            if (!hand.IsPresent) return;
            Color color = dominant ? dominantColor : otherColor;

            for (int b = 0; b < Bones.Length; b += 2)
            {
                Vector2 a = Map(hand.Points[Bones[b]], size, mirrored);
                Vector2 c = Map(hand.Points[Bones[b + 1]], size, mirrored);
                Image bone = Get(_bones, bones++, "Bone");
                bone.color = new Color(color.r, color.g, color.b, color.a * 0.75f);
                RectTransform rt = bone.rectTransform;
                rt.anchoredPosition = (a + c) * 0.5f;
                rt.sizeDelta = new Vector2(Vector2.Distance(a, c), boneWidth);
                rt.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(c.y - a.y, c.x - a.x) * Mathf.Rad2Deg);
            }

            for (int i = 0; i < HandLandmarks.Count; i++)
            {
                Image dot = Get(_dots, dots++, "Dot");
                dot.sprite = dotSprite;
                dot.color = color;
                dot.rectTransform.anchoredPosition = Map(hand.Points[i], size, mirrored);
                float s = i == HandLandmarks.Wrist ? dotSize * 1.4f : dotSize;
                dot.rectTransform.sizeDelta = new Vector2(s, s);
            }
        }

        private static Vector2 Map(Vector3 p, Vector2 size, bool mirrored)
        {
            float x = mirrored ? 1f - p.x : p.x;
            return new Vector2((x - 0.5f) * size.x, (0.5f - p.y) * size.y);
        }

        private Image Get(List<Image> pool, int index, string name)
        {
            if (index < pool.Count)
            {
                pool[index].gameObject.SetActive(true);
                return pool[index];
            }

            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            if (name == "Bone") go.transform.SetAsFirstSibling();
            pool.Add(image);
            return image;
        }

        private void Hide(int dotsUsed, int bonesUsed)
        {
            for (int i = dotsUsed; i < _dots.Count; i++) _dots[i].gameObject.SetActive(false);
            for (int i = bonesUsed; i < _bones.Count; i++) _bones[i].gameObject.SetActive(false);
        }
    }
}
