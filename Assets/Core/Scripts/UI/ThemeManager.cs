using System;
using UnityEngine;

namespace Cevir.Core.UI
{
    /// <summary>
    /// Day (light) or night (dark) colours. The choice is saved on this phone; <see cref="ThemeSweeper"/> recolours
    /// every screen when it changes, so screens keep using the light <see cref="UIPalette"/> colours in code.
    /// </summary>
    public static class ThemeManager
    {
        private const string PrefsKey = "chevir.theme.dark";

        private static bool? _dark;

        /// <summary>Raised after the theme changes.</summary>
        public static event Action Changed;

        public static bool IsDark
        {
            get
            {
                if (!_dark.HasValue) _dark = PlayerPrefs.GetInt(PrefsKey, 0) == 1;
                return _dark.Value;
            }
        }

        public static void SetDark(bool dark)
        {
            if (IsDark == dark) return;
            _dark = dark;
            PlayerPrefs.SetInt(PrefsKey, dark ? 1 : 0);
            PlayerPrefs.Save();
            if (Changed != null) Changed();
        }

        public static void Toggle()
        {
            SetDark(!IsDark);
        }
    }
}
