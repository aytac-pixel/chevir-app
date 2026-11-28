using Cevir.Core.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>
    /// A full-screen page opened from the Profile tab (Statistics, Notifications, Friends): background, back button,
    /// title and a scrolling column. The content is built at runtime in the interface language and built again when the
    /// language changes.
    /// </summary>
    public abstract class ProfilePanelBase : MonoBehaviour
    {
        [SerializeField] protected TMP_FontAsset font;
        [SerializeField] protected CevirTheme theme;
        [SerializeField] protected Sprite background;
        [SerializeField] protected Sprite iconBack;
        [SerializeField] protected Sprite iconBell;

        private GameObject _panel;

        public bool IsOpen => _panel != null && _panel.activeSelf;

        protected abstract string Title { get; }

        /// <summary>Fills the scrolling column under the title.</summary>
        protected abstract void BuildContent(UIKit kit, RectTransform content);

        /// <summary>Puts current values into the built content (called on open and while open when data changes).</summary>
        protected abstract void Render();

        protected virtual void Awake()
        {
            Build();
            _panel.SetActive(false);
            Loc.LanguageChanged += Rebuild;
        }

        protected virtual void OnDestroy()
        {
            Loc.LanguageChanged -= Rebuild;
        }

        public void Open()
        {
            _panel.SetActive(true);
            Render();
        }

        public void Close()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        private void Rebuild(string language)
        {
            bool open = IsOpen;
            if (_panel != null) Destroy(_panel);
            Build();
            _panel.SetActive(open);
            if (open) Render();
        }

        private void Build()
        {
            var kit = new UIKit(font, theme);
            RectTransform panel = UIKit.Rect("Panel", transform);
            UIKit.Stretch(panel);
            Image bg = panel.gameObject.AddComponent<Image>();
            bg.sprite = background;
            bg.color = background != null ? Color.white : UIPalette.Background;
            bg.raycastTarget = true;
            _panel = panel.gameObject;
            HomeSkyUI.Create(panel, theme); // the same sky as the Learn page (time of day and weather)

            RectTransform safe = UIKit.Rect("SafeArea", panel);
            UIKit.Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            RectTransform content = kit.Scroll(safe, new RectOffset(60, 60, 40, 100), 30f);

            // Title bar on a white card, so it reads on every sky.
            RectTransform top = kit.Card(content, "Top", 0, 0f);
            DestroyImmediate(top.GetComponent<VerticalLayoutGroup>());
            HorizontalLayoutGroup topRow = UIKit.Row(top.gameObject, 20f);
            topRow.padding = new RectOffset(20, 20, 16, 16);
            UIKit.Layout(top.gameObject, preferredHeight: 128f);
            kit.IconButton(top, "Back", iconBack, "‹", 96f, UIPalette.TextPrimary, Color.white, Close);
            TextMeshProUGUI heading = kit.Text(top, "Heading", Title, 46f, UIPalette.TextPrimary);
            UIKit.Layout(heading.gameObject, flexibleWidth: 1f);

            BuildContent(kit, content);
        }

        // ------------------------------------------------------------------ shared pieces

        /// <summary>Square tile with an icon, used at the start of a row.</summary>
        protected Image IconTile(UIKit kit, Transform parent, Sprite icon, Color tint, Color fill, float size = 96f)
        {
            Image tile = kit.Image(parent, "IconTile", theme != null ? theme.panel : null, fill, true);
            UIKit.Layout(tile.gameObject, preferredWidth: size, preferredHeight: size, minWidth: size);
            Image glyph = kit.Image(tile.transform, "Icon", icon, tint, false);
            glyph.rectTransform.anchorMin = glyph.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            glyph.rectTransform.sizeDelta = new Vector2(size * 0.56f, size * 0.56f);
            return tile;
        }

        /// <summary>A two-button on/off choice; returns the two buttons (on first).</summary>
        protected Button[] OnOff(UIKit kit, Transform parent, System.Action<bool> changed)
        {
            RectTransform row = UIKit.Rect("Choice", parent);
            UIKit.Row(row.gameObject, 16f).childForceExpandWidth = true;
            Button on = kit.Button(row, "On", Loc.Pick("ON", "AÇIQ", "ВКЛ"), UIPalette.Surface, UIPalette.TextSecondary, 92f, false, () => changed(true));
            UIKit.Layout(on.gameObject, flexibleWidth: 1f);
            Button off = kit.Button(row, "Off", Loc.Pick("OFF", "BAĞLI", "ВЫКЛ"), UIPalette.Surface, UIPalette.TextSecondary, 92f, false, () => changed(false));
            UIKit.Layout(off.gameObject, flexibleWidth: 1f);
            return new[] { on, off };
        }
    }
}
