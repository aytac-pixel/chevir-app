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

        public ICameraPreviewSource Source => _source;

        /// <summary>Upright, cover-fitted size of the picture (larger than the frame when cropped).</summary>
        public Vector2 UprightSize { get; private set; }

        public void SetSource(ICameraPreviewSource source)
        {
            _source = source;
        }

        private void LateUpdate()
        {
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
    }
}
