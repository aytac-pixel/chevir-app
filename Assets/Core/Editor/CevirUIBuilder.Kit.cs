using System.Collections.Generic;
using Cevir.Core.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.Editor
{
    /// <summary>Building blocks for the glass theme: sprites from Assets/Core/Sprites, glass cards, icons, labels.</summary>
    public static partial class CevirUIBuilder
    {
        private const string SpriteFolder = "Assets/Core/Sprites";
        private const float TabBarHeight = 200f;

        private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();

        /// <summary>Loads Assets/Core/Sprites/&lt;folder&gt;/&lt;name&gt;.png, ignoring a "__b&lt;N&gt;" border suffix.</summary>
        private static Sprite Sprite(string folder, string name)
        {
            string key = folder + "/" + name;
            Sprite cached;
            if (SpriteCache.TryGetValue(key, out cached) && cached != null) return cached;

            string directory = SpriteFolder + "/" + folder;
            foreach (string guid in AssetDatabase.FindAssets(name, new[] { directory }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                if (file != name && !file.StartsWith(name + "__")) continue;
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) continue;
                SpriteCache[key] = sprite;
                return sprite;
            }

            Problems.Add("Sprite not found: " + directory + "/" + name + ".png");
            return null;
        }

        private static Sprite IconSprite(string name) => Sprite("Icons", "icon_" + name);
        private static Sprite Panel(int radius) => Sprite("UI", "panel_r" + radius);

        private static CevirTheme BuildTheme()
        {
            SpriteCache.Clear();
            CevirTheme theme = CevirAssetUtility.LoadOrCreate<CevirTheme>(DataFolder + "/CevirTheme.asset");
            theme.panel = Panel(48);
            theme.pill = Sprite("UI", "pill");
            theme.circle = Sprite("UI", "circle");
            theme.ring = Sprite("UI", "ring");
            theme.glow = Sprite("UI", "glow");
            theme.tileGradient = Sprite("UI", "tile_gradient");
            theme.tileActiveGradient = Sprite("UI", "tile_active_gradient");
            theme.buttonGradient = Sprite("UI", "button_gradient");
            theme.iconBook = IconSprite("book");
            theme.iconHand = IconSprite("hand");
            theme.iconLock = IconSprite("lock");
            theme.iconTrophy = IconSprite("trophy");
            theme.iconCheck = IconSprite("check");
            theme.iconPlay = IconSprite("play");
            theme.iconArrowUp = IconSprite("arrow_up");
            theme.iconArrowDown = IconSprite("arrow_down");
            theme.iconStar = IconSprite("star");
            EditorUtility.SetDirty(theme);
            return theme;
        }

        /// <summary>Image on a new child, with 1:1 sliced borders for the generated sprites.</summary>
        private static Image Img(Transform parent, string name, Sprite sprite, Color color, bool sliced = false)
        {
            RectTransform rt = Rect(name, parent);
            Image image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            if (sliced && sprite != null)
            {
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 1f;
            }
            else if (sprite != null)
            {
                image.preserveAspect = true;
            }

            return image;
        }

        private static Image Icon(Transform parent, string name, string icon, float size, Color color)
        {
            Image image = Img(parent, name, IconSprite(icon), color);
            image.rectTransform.sizeDelta = new Vector2(size, size);
            Layout(image.gameObject, preferredWidth: size, preferredHeight: size);
            return image;
        }

        private static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Label that follows the interface language. Pass az = null for text set from code.</summary>
        private static TextMeshProUGUI Label(Transform parent, string name, string en, string az, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft, bool bold = true)
        {
            TextMeshProUGUI label = Text(parent, name, en, size, color, alignment, bold);
            if (az != null) label.gameObject.AddComponent<LocalizedLabel>().Set(en, az);
            return label;
        }

        /// <summary>Glass card: translucent white rounded panel, a light border and a soft drop shadow.</summary>
        private static Image Glass(Transform parent, string name, int radius, Color fill)
        {
            RectTransform rt = Rect(name, parent);
            Image image = rt.gameObject.AddComponent<Image>();
            image.sprite = Panel(radius);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = fill;
            image.raycastTarget = false;

            Shadow shadow = rt.gameObject.AddComponent<Shadow>();
            shadow.effectColor = UIPalette.Shadow;
            shadow.effectDistance = new Vector2(0f, -8f);

            Image border = Img(rt, "Border", Sprite("UI", "outline_r" + (radius >= 48 ? 48 : 36)), UIPalette.GlassBorder, true);
            Stretch(border.rectTransform);
            Layout(border.gameObject).ignoreLayout = true;
            return image;
        }

        /// <summary>Blue gradient pill button with an optional icon before the label.</summary>
        private static Button GradientButton(Transform parent, string name, string en, string az, float height, string icon,
            out TextMeshProUGUI label)
        {
            RectTransform rt = Rect(name, parent);
            Image image = rt.gameObject.AddComponent<Image>();
            image.sprite = Sprite("UI", "button_gradient");
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            Shadow shadow = rt.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.06f, 0.37f, 0.82f, 0.35f);
            shadow.effectDistance = new Vector2(0f, -8f);
            Button button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Layout(rt.gameObject, preferredHeight: height, minHeight: height);

            HorizontalLayoutGroup row = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.spacing = 18f;
            row.padding = new RectOffset(30, 30, 0, 0);
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            if (!string.IsNullOrEmpty(icon)) Icon(rt, "Icon", icon, height * 0.36f, Color.white);
            label = Label(rt, "Label", en, az, Mathf.Min(38f, height * 0.32f), Color.white, TextAlignmentOptions.Center, true);
            return button;
        }

        /// <summary>Square glass icon button (close, social links, replay).</summary>
        private static Button IconButton(Transform parent, string name, string icon, float size, Color iconColor, Color fill, bool round)
        {
            RectTransform rt = Rect(name, parent);
            rt.sizeDelta = new Vector2(size, size);
            Image image = rt.gameObject.AddComponent<Image>();
            image.sprite = round ? Sprite("UI", "circle") : Panel(36);
            image.type = round ? Image.Type.Simple : Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = fill;
            Shadow shadow = rt.gameObject.AddComponent<Shadow>();
            shadow.effectColor = UIPalette.Shadow;
            shadow.effectDistance = new Vector2(0f, -6f);
            Button button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Layout(rt.gameObject, preferredWidth: size, preferredHeight: size, minHeight: size);
            Image glyph = Img(rt, "Icon", IconSprite(icon), iconColor);
            Place(glyph.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.48f, size * 0.48f));
            return button;
        }

        private static VerticalLayoutGroup Column(GameObject go, int padding, float spacing, TextAnchor align = TextAnchor.UpperLeft)
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

        private static HorizontalLayoutGroup Row(GameObject go, float spacing, TextAnchor align = TextAnchor.MiddleLeft)
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

        /// <summary>Vertical scroll view whose content is a padded column sized to its children.</summary>
        private static RectTransform ScrollColumn(Transform parent, string name, RectOffset padding, float spacing, out ScrollRect scrollRect)
        {
            RectTransform scroll = Rect(name, parent);
            Stretch(scroll);
            Image hit = scroll.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
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
            ContentSizeFitter fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport;
            scrollRect.content = content;
            return content;
        }

        private static TMP_InputField InputField(Transform parent, string name, string placeholderEn, string placeholderAz)
        {
            GameObject go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            go.name = name;
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            SetLayerRecursively(go, LayerMask.NameToLayer("UI"));
            Layout(go, preferredHeight: 120f, minHeight: 120f);

            Image background = go.GetComponent<Image>();
            background.sprite = Panel(36);
            background.type = Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = 1f;
            background.color = UIPalette.Surface;

            TMP_InputField field = go.GetComponent<TMP_InputField>();
            field.fontAsset = _font;
            field.pointSize = 40f;
            field.characterLimit = 30;
            field.lineType = TMP_InputField.LineType.SingleLine;
            if (field.textViewport != null) Stretch(field.textViewport, 32f, 32f, 14f, 14f);
            if (field.textComponent != null)
            {
                field.textComponent.font = _font;
                field.textComponent.color = UIPalette.TextPrimary;
                field.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
            }

            var placeholder = field.placeholder as TextMeshProUGUI;
            if (placeholder != null)
            {
                placeholder.font = _font;
                placeholder.color = UIPalette.TextMuted;
                placeholder.alignment = TextAlignmentOptions.MidlineLeft;
                placeholder.gameObject.AddComponent<LocalizedLabel>().Set(placeholderEn, placeholderAz);
            }

            return field;
        }

        private static Button ToggleButton(Transform parent, string name, string en, string az)
        {
            TextMeshProUGUI label;
            Button button = MakeButton(parent, name, en, UIPalette.Surface, UIPalette.TextSecondary, 96f, out label);
            Image image = button.targetGraphic as Image;
            if (image != null)
            {
                image.sprite = Sprite("UI", "pill");
                image.pixelsPerUnitMultiplier = 1f;
            }

            label.fontSize = 34f;
            label.gameObject.AddComponent<LocalizedLabel>().Set(en, az);
            Layout(button.gameObject, flexibleWidth: 1f);
            return button;
        }
    }
}
