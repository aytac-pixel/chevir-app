using System.Collections.Generic;
using Cevir.Core.Curriculum;
using Cevir.Core.Game;
using Cevir.Core.Localization;
using Cevir.Core.Stories;
using TMPro;
using UnityEngine;

namespace Cevir.Core.UI
{
    /// <summary>
    /// Stories (practice mode list): header with completed / total, the unlock banner and one card per story.
    /// Unlocked and completed stories open the first-person story screen.
    /// </summary>
    public sealed class StoriesScreenUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI levelLabel;
        [SerializeField] private TextMeshProUGUI countLabel;
        [SerializeField] private RectTransform list;
        [SerializeField] private StoryCardUI cardPrefab;
        [SerializeField] private CevirTheme theme;

        private readonly List<StoryCardUI> _cards = new List<StoryCardUI>();
        private GameManager _game;

        private void OnEnable()
        {
            _game = GameManager.Instance;
            Loc.LanguageChanged += OnLanguageChanged;
            if (_game != null) _game.PathChanged += OnPathChanged;
            Refresh();
        }

        private void OnDisable()
        {
            Loc.LanguageChanged -= OnLanguageChanged;
            if (_game != null) _game.PathChanged -= OnPathChanged;
        }

        private void OnLanguageChanged(string language) => Refresh();
        private void OnPathChanged(IReadOnlyList<PathNode> path) => Refresh();

        private void Refresh()
        {
            if (_game == null) return;
            IReadOnlyList<StoryEntry> stories = _game.Stories;

            int completed = 0;
            foreach (StoryEntry entry in stories) if (entry.State == StoryState.Completed) completed++;
            LevelPathSummary level = _game.CurrentLevel;
            if (levelLabel != null) levelLabel.text = string.Format(UIStrings.StoriesLevel, level != null ? level.Level.Title : "A1");
            if (countLabel != null) countLabel.text = completed + " / " + stories.Count;

            while (_cards.Count < stories.Count) _cards.Add(Instantiate(cardPrefab, list));
            for (int i = 0; i < _cards.Count; i++)
            {
                bool visible = i < stories.Count;
                _cards[i].gameObject.SetActive(visible);
                if (visible) _cards[i].Bind(stories[i], theme, OnCardTapped);
            }
        }

        private void OnCardTapped(StoryEntry entry)
        {
            if (entry.State == StoryState.Locked)
            {
                Toast(string.Format(UIStrings.StoryLockedToast, UnitNumber(entry.Story.UnlockUnit)));
                return;
            }

            if (!entry.Story.IsPlayable)
            {
                Toast(UIStrings.ComingSoon);
                return;
            }

            if (_game.StartStory(entry.Story) == null) Toast(UIStrings.ComingSoon);
        }

        private int UnitNumber(UnitData unit)
        {
            if (unit == null) return 1;
            foreach (LevelPathSummary level in _game.UnitPath)
            {
                foreach (UnitPathEntry entry in level.Units)
                {
                    if (entry.Unit == unit) return entry.Number;
                }
            }

            return 1;
        }

        private static void Toast(string message)
        {
            if (CevirUIRoot.Instance != null) CevirUIRoot.Instance.ShowToast(message);
        }
    }
}
