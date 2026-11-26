using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>
    /// Small pixel-art things beside the learning path, so the home screen is lively without a busy background
    /// picture: clouds, a kite, a bird and sparkles by day; stars, a moon and a comet by night. Each sits in the empty
    /// half of the row (the side away from the unit tile), drawn faintly behind the path, and floats a little.
    /// </summary>
    public sealed class HomeDecorUI : MonoBehaviour
    {
        private const float SideOffset = 330f;
        private const float DayAlpha = 0.9f;
        private const float NightAlpha = 0.95f;

        private sealed class Item
        {
            public Image Image;
            public Vector2 Home;
            public float Phase;
            public float Bob;
            public float Drift;
            public int Slot;
        }

        private readonly List<Item> _items = new List<Item>();
        private RectTransform _layer;
        private CevirTheme _theme;
        private bool _night;

        public void Configure(RectTransform layer, CevirTheme theme)
        {
            _layer = layer;
            _theme = theme;
        }

        private void OnEnable()
        {
            ThemeManager.Changed += ApplySprites;
        }

        private void OnDisable()
        {
            ThemeManager.Changed -= ApplySprites;
        }

        /// <summary>Places one decoration between each pair of units, on the side away from the tiles.</summary>
        public void Layout(IList<Vector2> centers, float contentHeight)
        {
            if (_layer == null || _theme == null) return;
            int used = 0;
            for (int i = 0; i < centers.Count; i++)
            {
                Vector2 a = centers[i];
                Vector2 b = i + 1 < centers.Count ? centers[i + 1] : a + new Vector2(0f, -300f);

                // The free side is opposite the tile; for centred tiles alternate sides.
                float side = Mathf.Abs(a.x) < 60f ? (i % 2 == 0 ? 1f : -1f) : -Mathf.Sign(a.x);
                float x = side * SideOffset + (Hash(i, 3) - 0.5f) * 90f;
                float y = Mathf.Lerp(a.y, b.y, 0.35f + Hash(i, 7) * 0.3f);
                Item item = GetItem(used++);
                item.Home = new Vector2(Mathf.Clamp(x, -420f, 420f), y);
                item.Slot = i;
                item.Phase = Hash(i, 11) * Mathf.PI * 2f;
                item.Bob = 6f + Hash(i, 13) * 6f;
                item.Drift = 8f + Hash(i, 17) * 16f;
                item.Image.gameObject.SetActive(true);
            }

            // One extra above the header, so the very top is not empty either.
            if (centers.Count > 0)
            {
                Item top = GetItem(used++);
                top.Home = new Vector2(330f, centers[0].y + 330f);
                top.Slot = 101;
                top.Phase = 1.3f;
                top.Bob = 8f;
                top.Drift = 12f;
                top.Image.gameObject.SetActive(true);
            }

            for (int i = used; i < _items.Count; i++) _items[i].Image.gameObject.SetActive(false);
            ApplySprites();
        }

        private Item GetItem(int index)
        {
            if (index < _items.Count) return _items[index];
            var go = new GameObject("Decor", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_layer, false);
            go.transform.SetAsFirstSibling(); // behind the dots and tiles
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            var item = new Item { Image = image };
            _items.Add(item);
            return item;
        }

        private void ApplySprites()
        {
            if (_theme == null) return;
            _night = ThemeManager.IsDark;
            Sprite[] set = _night ? _theme.nightDecor : _theme.dayDecor;
            foreach (Item item in _items)
            {
                Sprite sprite = set != null && set.Length > 0 ? set[(item.Slot * 7 + 3) % set.Length] : null;
                item.Image.sprite = sprite;
                item.Image.enabled = sprite != null;
                if (sprite == null) continue;
                // Pixel art is stored small; show it at a whole-number scale so every pixel stays square.
                const float scale = 5f;
                item.Image.rectTransform.sizeDelta = new Vector2(sprite.rect.width * scale, sprite.rect.height * scale);
                item.Image.color = new Color(1f, 1f, 1f, _night ? NightAlpha : DayAlpha);
            }
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            foreach (Item item in _items)
            {
                if (!item.Image.gameObject.activeSelf) continue;
                // Slow float; clouds by day also drift sideways, stars by night twinkle instead.
                float bob = Mathf.Sin(t * 0.9f + item.Phase) * item.Bob;
                float drift = _night ? 0f : Mathf.Sin(t * 0.25f + item.Phase) * item.Drift;
                item.Image.rectTransform.anchoredPosition = item.Home + new Vector2(Mathf.Round(drift / 5f) * 5f, Mathf.Round(bob / 5f) * 5f);
                if (_night)
                {
                    Color c = item.Image.color;
                    c.a = NightAlpha * (0.7f + 0.3f * (0.5f + 0.5f * Mathf.Sin(t * 1.7f + item.Phase * 2f)));
                    item.Image.color = c;
                }
            }
        }

        /// <summary>Stable pseudo-random 0..1 per slot, so decorations stay put between visits.</summary>
        private static float Hash(int index, int salt)
        {
            uint h = (uint)(index * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFF) / 65535f;
        }
    }
}
