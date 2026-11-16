using Cevir.Core.Recognition;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>
    /// Shows the live camera in a RawImage, upright and mirrored like a selfie view, cropped to fill its frame.
    /// Only the picture is displayed; nothing is recorded or sent anywhere.
    /// </summary>
    public sealed class CameraPreviewUI : MonoBehaviour
    {
        [SerializeField] private RawImage image;
        [SerializeField] private RectTransform frame;
        [SerializeField] private GameObject placeholder;
        [SerializeField] private TMPro.TextMeshProUGUI placeholderLabel;

        private ICameraPreviewSource _source;
        private TMPro.TextMeshProUGUI _diagnostics;

        public ICameraPreviewSource Source => _source;

        /// <summary>Upright, cover-fitted size of the picture (larger than the frame when cropped).</summary>
        public Vector2 UprightSize { get; private set; }

        public void SetSource(ICameraPreviewSource source)
        {
            _source = source;
        }

        private void LateUpdate()
        {
            UpdateDiagnostics();
            Texture texture = _source != null ? _source.PreviewTexture : null;
            bool ready = texture != null && texture.width > 16;
            if (image != null) image.enabled = ready;
            if (placeholder != null) placeholder.SetActive(!ready);
            if (placeholderLabel != null && !ready)
            {
                placeholderLabel.text = _source == null ? UIStrings.CameraMissing : (_source.CameraDenied ? UIStrings.CameraDenied : UIStrings.CameraStarting);
            }

            if (!ready || image == null) return;

            if (image.texture != texture) image.texture = texture;

            int rotation = _source.PreviewRotation;
            bool sideways = rotation % 180 != 0;
            image.rectTransform.localEulerAngles = new Vector3(0f, 0f, -rotation);

            // Flip vertically when the camera delivers bottom-up rows. The selfie mirror must be left-right on the
            // screen; when the picture is turned sideways, the screen's left-right is the texture's up-down.
            bool flipU = !sideways && _source.PreviewMirrored;
            bool flipV = _source.PreviewVerticallyFlipped ^ (sideways && _source.PreviewMirrored);
            float uvW = flipU ? -1f : 1f;
            float uvH = flipV ? -1f : 1f;
            image.uvRect = new Rect(uvW < 0 ? 1f : 0f, uvH < 0 ? 1f : 0f, uvW, uvH);

            // Cover-fit: scale the (possibly rotated) texture so it fills the frame with no bars.
            RectTransform container = frame != null ? frame : (RectTransform)image.rectTransform.parent;
            float frameW = container.rect.width;
            float frameH = container.rect.height;
            float texW = sideways ? texture.height : texture.width;
            float texH = sideways ? texture.width : texture.height;
            float scale = Mathf.Max(frameW / texW, frameH / texH);
            float w = texW * scale;
            float h = texH * scale;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            image.rectTransform.sizeDelta = sideways ? new Vector2(h, w) : new Vector2(w, h);
            UprightSize = new Vector2(w, h);
        }

        /// <summary>Small status line at the bottom of the camera (test builds): shows whether the recognizer runs.</summary>
        private void UpdateDiagnostics()
        {
            var source = _source as IRecognitionDiagnostics;
            if (source == null)
            {
                if (_diagnostics != null) _diagnostics.transform.parent.gameObject.SetActive(false);
                return;
            }

            if (_diagnostics == null)
            {
                RectTransform parent = frame != null ? frame : (RectTransform)transform;
                var go = new GameObject("Diagnostics", typeof(RectTransform));
                go.transform.SetParent(parent, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, 8f);
                rt.sizeDelta = new Vector2(-24f, 90f);
                var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
                bg.transform.SetParent(rt, false);
                var bgRt = (RectTransform)bg.transform;
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
                Image bgImage = bg.GetComponent<Image>();
                bgImage.color = new Color(0f, 0f, 0f, 0.55f);
                bgImage.raycastTarget = false;
                var textGo = new GameObject("Text", typeof(RectTransform));
                textGo.transform.SetParent(rt, false);
                _diagnostics = textGo.AddComponent<TMPro.TextMeshProUGUI>();
                var textRt = _diagnostics.rectTransform;
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.offsetMin = new Vector2(12f, 4f);
                textRt.offsetMax = new Vector2(-12f, -4f);
                if (placeholderLabel != null) _diagnostics.font = placeholderLabel.font;
                _diagnostics.fontSize = 22f;
                _diagnostics.enableWordWrapping = true;
                _diagnostics.color = Color.white;
                _diagnostics.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
                _diagnostics.raycastTarget = false;
                rt.SetAsLastSibling();
            }

            _diagnostics.transform.parent.gameObject.SetActive(true);
            _diagnostics.text = source.DiagnosticText;
        }
    }
}
