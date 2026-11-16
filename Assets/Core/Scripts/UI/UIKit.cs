using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>
    /// Small runtime UI factory for panels that build their own content (account settings, reminders). Uses the app
    /// font and the theme's rounded sprites, so the panels look like the screens generated in the editor. Every row
    /// stretches to the panel width, so the panels fit any phone width.
    /// </summary>
    public sealed class UIKit
    {
        private readonly TMP_FontAsset _font;
        private readonly CevirTheme _theme;

        public UIKit(TMP_FontAsset font, CevirTheme theme)
        {
            _font = font != null ? font : TMP_Settings.defaultFontAsset;
            _theme = theme;
        }

        public CevirTheme Theme => _theme;

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : 5;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static LayoutElement Layout(GameObject go, float preferredWidth = -1f, float preferredHeight = -1f,
            float flexibleWidth = -1f, float flexibleHeight = -1f, float minHeight = -1f, float minWidth = -1f)
        {
            LayoutElement element = go.GetComponent<LayoutElement>();
            if (element == null) element = go.AddComponent<LayoutElement>();
            if (preferredWidth >= 0f) element.preferredWidth = preferredWidth;
            if (preferredHeight >= 0f) element.preferredHeight = preferredHeight;
            if (flexibleWidth >= 0f) element.flexibleWidth = flexibleWidth;
            if (flexibleHeight >= 0f) element.flexibleHeight = flexibleHeight;
            if (minHeight >= 0f) element.minHeight = minHeight;
            if (minWidth >= 0f) element.minWidth = minWidth;
            return element;
        }

        public static VerticalLayoutGroup Column(GameObject go, int padding, float spacing, TextAnchor align = TextAnchor.UpperLeft)
        {
            VerticalLayoutGroup column = go.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(padding, padding, padding, padding);
            column.spacing = spacing;
            column.childAlignment = align;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            return column;
        }

        public static HorizontalLayoutGroup Row(GameObject go, float spacing, TextAnchor align = TextAnchor.MiddleLeft)
        {
            HorizontalLayoutGroup row = go.AddComponent<HorizontalLayoutGroup>();
            row.spacing = spacing;
            row.childAlignment = align;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            return row;
        }

        public Image Image(Transform parent, string name, Sprite sprite, Color color, bool sliced)
        {
            RectTransform rt = Rect(name, parent);
            Image image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            if (sliced && sprite != null)
            {
                image.type = UnityEngine.UI.Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 1f;
            }
            else if (sprite != null)
            {
                image.preserveAspect = true;
            }

            return image;
        }

        /// <summary>White square card (dark border in the sprite) with a hard pixel shadow and a padded column inside.</summary>
        public RectTransform Card(Transform parent, string name, int padding = 44, float spacing = 22f)
        {
            Image card = Image(parent, name, _theme != null ? _theme.panel : null, Color.white, true);
            Shadow shadow = card.gameObject.AddComponent<Shadow>();
            shadow.effectColor = UIPalette.Ink;
            shadow.effectDistance = UIPalette.ShadowOffset;
            Column(card.gameObject, padding, spacing);
            return card.rectTransform;
        }

        public TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft, bool bold = true)
        {
            RectTransform rt = Rect(name, parent);
            TextMeshProUGUI label = rt.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = _font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.enableWordWrapping = true;
            label.raycastTarget = false;
            return label;
        }

        public Image Icon(Transform parent, string name, Sprite sprite, float size, Color color)
        {
            Image image = Image(parent, name, sprite, color, false);
            image.rectTransform.sizeDelta = new Vector2(size, size);
            Layout(image.gameObject, preferredWidth: size, preferredHeight: size, minWidth: size);
            return image;
        }

        /// <summary>Rounded pill button. <paramref name="gradient"/> uses the blue gradient of the main buttons.</summary>
        public Button Button(Transform parent, string name, string text, Color fill, Color textColor, float height, bool gradient,
            out TextMeshProUGUI label, Action onClick = null)
        {
            RectTransform rt = Rect(name, parent);
            Image image = rt.gameObject.AddComponent<Image>();
            image.sprite = _theme != null ? (gradient ? _theme.buttonGradient : _theme.pill) : null;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = gradient ? Color.white : fill;
            Button button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.6f);
            button.colors = colors;
            Layout(rt.gameObject, preferredHeight: height, minHeight: height);

            label = Text(rt, "Label", text, Mathf.Min(36f, height * 0.34f), textColor, TextAlignmentOptions.Center, true);
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            Stretch(label.rectTransform, 28f, 28f, 6f, 6f);
            if (onClick != null) button.onClick.AddListener(() => onClick());
            return button;
        }

        public Button Button(Transform parent, string name, string text, Color fill, Color textColor, float height, bool gradient,
            Action onClick = null)
        {
            TextMeshProUGUI ignored;
            return Button(parent, name, text, fill, textColor, height, gradient, out ignored, onClick);
        }

        /// <summary>Round icon button (back, +, −).</summary>
        public Button IconButton(Transform parent, string name, Sprite icon, string fallbackText, float size, Color iconColor, Color fill,
            Action onClick)
        {
            RectTransform rt = Rect(name, parent);
            rt.sizeDelta = new Vector2(size, size);
            Image image = rt.gameObject.AddComponent<Image>();
            image.sprite = _theme != null ? _theme.circle : null;
            image.color = fill;
            Button button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Layout(rt.gameObject, preferredWidth: size, preferredHeight: size, minHeight: size, minWidth: size);
            if (icon != null)
            {
                Image glyph = Image(rt, "Icon", icon, iconColor, false);
                glyph.rectTransform.anchorMin = glyph.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                glyph.rectTransform.sizeDelta = new Vector2(size * 0.5f, size * 0.5f);
            }
            else
            {
                TextMeshProUGUI label = Text(rt, "Label", fallbackText, size * 0.5f, iconColor, TextAlignmentOptions.Center, true);
                Stretch(label.rectTransform);
            }

            if (onClick != null) button.onClick.AddListener(() => onClick());
            return button;
        }

        public TMP_InputField Input(Transform parent, string name, string placeholder, TMP_InputField.ContentType type, int limit = 60)
        {
            GameObject go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            go.name = name;
            go.transform.SetParent(parent, false);
            SetLayer(go, parent.gameObject.layer);
            Layout(go, preferredHeight: 116f, minHeight: 116f);

            Image background = go.GetComponent<Image>();
            background.sprite = _theme != null ? _theme.panel : null;
            background.type = UnityEngine.UI.Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = 1.4f;
            background.color = UIPalette.Surface;

            TMP_InputField field = go.GetComponent<TMP_InputField>();
            field.fontAsset = _font;
            field.pointSize = 38f;
            field.characterLimit = limit;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.contentType = type;
            if (field.textViewport != null) Stretch(field.textViewport, 32f, 32f, 12f, 12f);
            if (field.textComponent != null)
            {
                field.textComponent.font = _font;
                field.textComponent.color = UIPalette.TextPrimary;
                field.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
            }

            var hint = field.placeholder as TextMeshProUGUI;
            if (hint != null)
            {
                hint.font = _font;
                hint.text = placeholder;
                hint.color = UIPalette.TextMuted;
                hint.alignment = TextAlignmentOptions.MidlineLeft;
            }

            return field;
        }

        /// <summary>Full-screen vertical scroll view; returns the padded content column.</summary>
        public RectTransform Scroll(Transform parent, RectOffset padding, float spacing)
        {
            RectTransform scroll = Rect("Scroll", parent);
            Stretch(scroll);
            scroll.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
            ScrollRect scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.scrollSensitivity = 30f;

            RectTransform viewport = Rect("Viewport", scroll);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0f);

            RectTransform content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            VerticalLayoutGroup column = Column(content.gameObject, 0, spacing);
            column.padding = padding;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport;
            scrollRect.content = content;
            return content;
        }

        /// <summary>Colours a toggle-style button as selected / not selected.</summary>
        public static void Select(Button button, bool on)
        {
            if (button == null) return;
            var image = button.targetGraphic as Image;
            if (image != null) image.color = on ? UIPalette.Primary : UIPalette.Surface;
            foreach (TextMeshProUGUI label in button.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                label.color = on ? Color.white : UIPalette.TextSecondary;
            }
        }

        private static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayer(child.gameObject, layer);
        }
    }
}
