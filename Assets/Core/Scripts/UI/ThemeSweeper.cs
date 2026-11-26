using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>
    /// Night mode. Screens are built (and coloured from code) with the light <see cref="UIPalette"/>; while the night
    /// theme is on, this pass maps every known light colour on every canvas to its night colour right before the frame
    /// is drawn, and maps them back once when the day theme returns. Text, icons, flat fills, pixel panels and hard
    /// shadows each have their own table, because the same light colour means different things on each (white text on
    /// a blue button stays white, a white card turns dark navy).
    /// Pixel panels swap to a night sprite (light border, grey fill) so cards keep a visible pixel edge on the dark
    /// background: the fill shows the tint times the grey, the border shows the tint itself.
    /// Photos, story art, gradients and the logo are never touched.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class ThemeSweeper : MonoBehaviour
    {
        /// <summary>Grey of the night panel sprites' fill (Tools/ui_art/make_pixel_sprites.py, DARK_FILL).</summary>
        private const float NightPanelGrey = 107f / 255f;

        private const int RefreshEveryFrames = 12;

        private enum Kind
        {
            None,
            Text,
            Icon,
            Fill,
            Panel,
            NightPanel,
            Outline,
            AppBackground
        }

        private sealed class Table
        {
            public readonly Dictionary<int, Color32> ToNight = new Dictionary<int, Color32>();
            public readonly Dictionary<int, Color32> ToDay = new Dictionary<int, Color32>();

            public void Add(Color day, Color32 night)
            {
                int dayKey = Key(day);
                int nightKey = Key(night);
                if (!ToNight.ContainsKey(dayKey)) ToNight[dayKey] = night;
                if (!ToDay.ContainsKey(nightKey)) ToDay[nightKey] = day;
            }
        }

        private static Table _text, _icon, _fill, _panel, _panelPlain, _outline, _appBackground, _shadow;

        [SerializeField] private CevirTheme theme;

        private readonly Dictionary<string, Sprite> _nightByDay = new Dictionary<string, Sprite>();
        private readonly Dictionary<string, Sprite> _dayByNight = new Dictionary<string, Sprite>();
        private readonly List<Graphic> _graphics = new List<Graphic>();
        private readonly List<Sprite> _knownSprites = new List<Sprite>(); // sprite each kind below was worked out for
        private readonly List<Kind> _kinds = new List<Kind>();
        private readonly List<Graphic> _scratch = new List<Graphic>();
        private readonly List<Shadow> _shadows = new List<Shadow>();
        private readonly List<Shadow> _shadowScratch = new List<Shadow>();
        private int _framesToRefresh;
        private int _dayPasses;

        public static ThemeSweeper Instance { get; private set; }

        public void Configure(CevirTheme source)
        {
            theme = source;
            BuildSpriteMaps();
        }

        private void Awake()
        {
            Instance = this;
            BuildTables();
            BuildSpriteMaps();
            ThemeManager.Changed += OnThemeChanged;
        }

        private void OnDestroy()
        {
            ThemeManager.Changed -= OnThemeChanged;
            if (Instance == this) Instance = null;
        }

        private void OnThemeChanged()
        {
            _framesToRefresh = 0;
            _dayPasses = ThemeManager.IsDark ? 0 : 2;
        }

        private void LateUpdate()
        {
            bool night = ThemeManager.IsDark;
            if (!night && _dayPasses <= 0) return;

            if (--_framesToRefresh <= 0)
            {
                _framesToRefresh = RefreshEveryFrames;
                Collect();
            }

            Sweep(night);
            if (!night) _dayPasses--;
        }

        /// <summary>Applies the current theme to everything now (after building new UI, for example).</summary>
        public void SweepNow()
        {
            Collect();
            Sweep(ThemeManager.IsDark);
        }

        // ------------------------------------------------------------------ sweep

        private void Collect()
        {
            _graphics.Clear();
            _knownSprites.Clear();
            _kinds.Clear();
            _shadows.Clear();
            Canvas[] canvases = FindObjectsOfType<Canvas>(true);
            foreach (Canvas canvas in canvases)
            {
                if (canvas == null || !canvas.isRootCanvas) continue;
                canvas.GetComponentsInChildren(true, _scratch);
                for (int i = 0; i < _scratch.Count; i++)
                {
                    Graphic graphic = _scratch[i];
                    var image = graphic as Image;
                    _graphics.Add(graphic);
                    _knownSprites.Add(image != null ? image.sprite : null);
                    _kinds.Add(KindOf(graphic));
                }
                canvas.GetComponentsInChildren(true, _shadowScratch);
                _shadows.AddRange(_shadowScratch);
            }
        }

        private void Sweep(bool night)
        {
            for (int i = 0; i < _graphics.Count; i++)
            {
                Graphic graphic = _graphics[i];
                if (graphic == null) continue;
                var image = graphic as Image;
                if (image != null && image.sprite != _knownSprites[i])
                {
                    // The sprite changed since the last look (screens swap tile and panel sprites): classify again.
                    _knownSprites[i] = image.sprite;
                    _kinds[i] = KindOf(graphic);
                }

                // A day/night panel swap changes the sprite, so the next pass classifies it again.
                Apply(graphic, _kinds[i], night);
            }

            for (int i = 0; i < _shadows.Count; i++)
            {
                Shadow shadow = _shadows[i];
                if (shadow == null || shadow is Outline) continue;
                Color32 mapped;
                if (TryMap(_shadow, shadow.effectColor, night, out mapped)) shadow.effectColor = WithAlpha(mapped, shadow.effectColor.a);
            }
        }

        private void Apply(Graphic graphic, Kind kind, bool night)
        {
            if (kind == Kind.None) return;

            var image = graphic as Image;
            Color32 mapped;
            switch (kind)
            {
                case Kind.Panel:
                    if (night)
                    {
                        Sprite nightSprite;
                        if (TryMap(_panel, graphic.color, true, out mapped) && _nightByDay.TryGetValue(image.sprite.name, out nightSprite))
                        {
                            image.sprite = nightSprite;
                            graphic.color = WithAlpha(mapped, graphic.color.a);
                        }
                        else if (TryMap(_panelPlain, graphic.color, true, out mapped))
                        {
                            graphic.color = WithAlpha(mapped, graphic.color.a);
                        }
                    }
                    else if (TryMap(_panelPlain, graphic.color, false, out mapped))
                    {
                        graphic.color = WithAlpha(mapped, graphic.color.a);
                    }

                    return;
                case Kind.NightPanel:
                    if (!night)
                    {
                        Sprite daySprite;
                        if (_dayByNight.TryGetValue(image.sprite.name, out daySprite)) image.sprite = daySprite;
                        if (TryMap(_panel, graphic.color, false, out mapped)) graphic.color = WithAlpha(mapped, graphic.color.a);
                    }
                    else if (TryMap(_panel, graphic.color, true, out mapped))
                    {
                        // Code set a day colour again on a panel that already has the night sprite.
                        graphic.color = WithAlpha(mapped, graphic.color.a);
                    }

                    return;
            }

            Table table = kind == Kind.Text ? _text
                : kind == Kind.Icon ? _icon
                : kind == Kind.Outline ? _outline
                : kind == Kind.AppBackground ? _appBackground
                : _fill;
            if (TryMap(table, graphic.color, night, out mapped)) graphic.color = WithAlpha(mapped, graphic.color.a);
        }

        private Kind KindOf(Graphic graphic)
        {
            if (graphic is TMP_Text) return Kind.Text;
            var image = graphic as Image;
            if (image == null) return Kind.None; // RawImage: camera, video, photos

            Sprite sprite = image.sprite;
            if (sprite == null) return Kind.Fill;
            string name = sprite.name;
            if (_dayByNight.ContainsKey(name)) return Kind.NightPanel;
            if (_nightByDay.ContainsKey(name)) return Kind.Panel;
            if (name.StartsWith("icon_") || name.StartsWith("unit_")) return Kind.Icon;
            if (name.StartsWith("outline_")) return Kind.Outline;
            if (name == "app_background") return Kind.AppBackground;
            if (name == "white" || name == "circle" || name == "bubble_tail" || name == "UISprite" || name == "Background" ||
                name.StartsWith("shadow_"))
                return Kind.Fill;
            return Kind.None; // art, photos, gradients, flags, logo
        }

        private static bool TryMap(Table table, Color color, bool night, out Color32 mapped)
        {
            return (night ? table.ToNight : table.ToDay).TryGetValue(Key(color), out mapped);
        }

        private static Color WithAlpha(Color32 color, float alpha)
        {
            Color c = color;
            c.a = alpha;
            return c;
        }

        private static int Key(Color color)
        {
            Color32 c = color;
            return (c.r << 16) | (c.g << 8) | c.b;
        }

        // ------------------------------------------------------------------ sprites

        private void BuildSpriteMaps()
        {
            _nightByDay.Clear();
            _dayByNight.Clear();
            if (theme == null || theme.daySprites == null || theme.nightSprites == null) return;
            for (int i = 0; i < theme.daySprites.Length && i < theme.nightSprites.Length; i++)
            {
                Sprite day = theme.daySprites[i];
                Sprite night = theme.nightSprites[i];
                if (day == null || night == null) continue;
                _nightByDay[day.name] = night;
                _dayByNight[night.name] = day;
            }
        }

        // ------------------------------------------------------------------ colours

        private static Color32 Hex(int rgb)
        {
            return new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
        }

        /// <summary>Tint that makes a night panel's grey fill show <paramref name="fill"/> (its border shows the tint).</summary>
        private static Color32 PanelTint(int fill)
        {
            Color32 c = Hex(fill);
            return new Color32(
                (byte)Mathf.Min(255, Mathf.RoundToInt(c.r / NightPanelGrey)),
                (byte)Mathf.Min(255, Mathf.RoundToInt(c.g / NightPanelGrey)),
                (byte)Mathf.Min(255, Mathf.RoundToInt(c.b / NightPanelGrey)), 255);
        }

        private static void BuildTables()
        {
            if (_text != null) return;

            _text = new Table();
            _text.Add(UIPalette.TextPrimary, Hex(UIPalette.NightText)); // also Ink
            _text.Add(UIPalette.TextSecondary, Hex(0xAEBBD3));
            _text.Add(UIPalette.TextMuted, Hex(0x7F8FAD));
            _text.Add(UIPalette.Primary, Hex(UIPalette.NightPrimaryText));
            _text.Add(UIPalette.PrimaryDark, Hex(0x8AB0FF));
            _text.Add(UIPalette.Cyan, Hex(0x74A2FF));
            _text.Add(UIPalette.GreenDark, Hex(0x4CD493));
            _text.Add(UIPalette.Green, Hex(0x45CF8A));
            _text.Add(UIPalette.RedDark, Hex(0xFF7B86));
            _text.Add(UIPalette.Red, Hex(0xFF6B77));
            _text.Add(UIPalette.AmberDark, Hex(0xFFC54A));
            _text.Add(UIPalette.LockedIcon, Hex(0x7E90B2));

            _icon = new Table();
            _icon.Add(UIPalette.TextPrimary, Hex(UIPalette.NightText));
            _icon.Add(UIPalette.TextSecondary, Hex(0xAEBBD3));
            _icon.Add(UIPalette.TextMuted, Hex(0x7F8FAD));
            _icon.Add(UIPalette.Primary, Hex(UIPalette.NightPrimaryText));
            _icon.Add(UIPalette.Cyan, Hex(0x74A2FF));
            _icon.Add(UIPalette.LockedIcon, Hex(0x7E90B2));
            _icon.Add(UIPalette.GreenDark, Hex(0x4CD493));
            _icon.Add(UIPalette.RedDark, Hex(0xFF7B86));

            _fill = new Table();
            _fill.Add(Color.white, Hex(0x172239));
            _fill.Add(UIPalette.Background, Hex(UIPalette.NightBackground));
            _fill.Add(UIPalette.Surface, Hex(0x1B2640)); // also LockedFill
            _fill.Add(UIPalette.CyanLight, Hex(0x1F3463));
            _fill.Add(UIPalette.Border, Hex(0x2A3858));
            _fill.Add(UIPalette.PathDot, Hex(0x3A4E78));
            _fill.Add(UIPalette.GreenLight, Hex(0x143D2B));
            _fill.Add(UIPalette.RedLight, Hex(0x47202A));
            _fill.Add(UIPalette.AmberLight, Hex(0x463813));
            _fill.Add(UIPalette.DemoteRow, Hex(0x3C1E26));
            _fill.Add(UIPalette.Ink, Hex(0x2C3E66)); // divider lines stay visible on navy

            _panel = new Table();
            _panel.Add(Color.white, PanelTint(0x172239));
            _panel.Add(UIPalette.Background, PanelTint(UIPalette.NightBackground));
            _panel.Add(UIPalette.Surface, PanelTint(0x1B2640));
            _panel.Add(UIPalette.CyanLight, PanelTint(0x1F3463));
            _panel.Add(UIPalette.GreenLight, PanelTint(0x143D2B));
            _panel.Add(UIPalette.RedLight, PanelTint(0x47202A));
            _panel.Add(UIPalette.AmberLight, PanelTint(0x463813));
            _panel.Add(UIPalette.DemoteRow, PanelTint(0x3C1E26));

            // Panels that keep their day sprite: the dark hard-shadow squares.
            _panelPlain = new Table();
            _panelPlain.Add(UIPalette.Ink, Hex(0x03060D));

            _outline = new Table();
            _outline.Add(UIPalette.Ink, Hex(0x4A68A8));

            _appBackground = new Table();
            Color32 bg = Hex(UIPalette.NightBackground);
            Color32 texture = UIPalette.Background; // app_background.png is this flat colour
            _appBackground.Add(Color.white, new Color32(
                (byte)Mathf.Min(255, bg.r * 255 / Mathf.Max(1, (int)texture.r)),
                (byte)Mathf.Min(255, bg.g * 255 / Mathf.Max(1, (int)texture.g)),
                (byte)Mathf.Min(255, bg.b * 255 / Mathf.Max(1, (int)texture.b)), 255));

            _shadow = new Table();
            _shadow.Add(UIPalette.Ink, Hex(0x000000));
        }
    }
}
