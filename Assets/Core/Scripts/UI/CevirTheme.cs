using UnityEngine;

namespace Cevir.Core.UI
{
    /// <summary>
    /// Sprites the screens swap at runtime (node states, tab icons, tier cup). Generated and filled by
    /// <c>Chevir > UI > Generate Complete UI Hierarchy</c> from Assets/Core/Sprites; replace art there.
    /// </summary>
    [CreateAssetMenu(menuName = "Chevir/UI Theme", fileName = "CevirTheme")]
    public sealed class CevirTheme : ScriptableObject
    {
        [Header("Shapes")]
        public Sprite panel;
        public Sprite pill;
        public Sprite circle;
        public Sprite ring;
        public Sprite glow;
        public Sprite tileGradient;
        public Sprite tileActiveGradient;
        public Sprite buttonGradient;

        [Header("Icons")]
        public Sprite iconBook;
        public Sprite iconHand;
        public Sprite iconLock;
        public Sprite iconTrophy;
        public Sprite iconCheck;
        public Sprite iconPlay;
        public Sprite iconArrowUp;
        public Sprite iconArrowDown;
        public Sprite iconStar;

        [Header("Night theme")]
        [Tooltip("Pixel panels with the dark border; same order as Night Sprites.")]
        public Sprite[] daySprites;
        [Tooltip("Night versions: light border, grey fill (ThemeSweeper tints them).")]
        public Sprite[] nightSprites;

        [Header("Home decor (pixel art)")]
        public Sprite[] dayDecor;
        public Sprite[] nightDecor;

        [Header("Home sky (pixel art)")]
        [Tooltip("Full-screen Learn page backgrounds in HomeSky.Mood order: morning, noon, evening, night, rain, " +
                 "rain at night, snow, snow at night.")]
        public Sprite[] homeSkies;
    }
}
