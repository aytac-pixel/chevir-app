using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    public enum MainTab
    {
        Home,
        Stories,
        League,
        Profile
    }

    /// <summary>The persistent bottom navigation: Home, Stories, League, Profile.</summary>
    public sealed class TabBarUI : MonoBehaviour
    {
        [Serializable]
        public sealed class TabView
        {
            public MainTab tab;
            public Button button;
            public Image selectedBackground;
            public Image icon;
            public TextMeshProUGUI label;
        }

        [SerializeField] private List<TabView> tabs = new List<TabView>();
        [SerializeField] private Color selectedLabel = Color.white; // on the blue selected square
        [SerializeField] private Color idle = new Color32(0x0A, 0x19, 0x31, 0xFF);

        public event Action<MainTab> TabSelected;

        private void Awake()
        {
            foreach (TabView view in tabs)
            {
                MainTab tab = view.tab;
                if (view.button != null) view.button.onClick.AddListener(() => { if (TabSelected != null) TabSelected(tab); });
            }
        }

        public void Show(MainTab selected)
        {
            foreach (TabView view in tabs)
            {
                bool on = view.tab == selected;
                if (view.selectedBackground != null) view.selectedBackground.enabled = on;
                if (view.icon != null) view.icon.color = on ? Color.white : idle;
                if (view.label != null)
                {
                    view.label.color = on ? selectedLabel : idle;
                    view.label.fontStyle = FontStyles.Bold;
                }
            }
        }
    }
}
