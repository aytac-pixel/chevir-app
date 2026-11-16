using System.Collections.Generic;
using Cevir.Core.Game;
using Cevir.Core.Gamification;
using Cevir.Core.Localization;
using Cevir.Core.Social;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>
    /// League: weekly ranking header (tier cup, league name, time left), the promotion note, the group's rows
    /// with promotion / demotion zones and the learner highlighted, and the demotion note.
    /// </summary>
    public sealed class LeagueScreenUI : MonoBehaviour
    {
        [SerializeField] private Image tierCup;
        [SerializeField] private Image tierCupBackdrop;
        [SerializeField] private TextMeshProUGUI leagueName;
        [SerializeField] private TextMeshProUGUI timeLeft;
        [SerializeField] private TextMeshProUGUI promoteNote;
        [SerializeField] private TextMeshProUGUI demoteNote;
        [SerializeField] private RectTransform list;
        [SerializeField] private LeagueRowUI rowPrefab;
        [SerializeField] private CevirTheme theme;

        private readonly List<LeagueRowUI> _rows = new List<LeagueRowUI>();
        private GameManager _game;

        private void OnEnable()
        {
            _game = GameManager.Instance;
            Loc.LanguageChanged += OnLanguageChanged;
            if (_game != null)
            {
                _game.Xp.XpGained += OnXpGained;
                _game.ProfileChanged += Refresh;
            }

            Refresh();
            if (_game != null) _game.RequestLeagueRefresh();
        }

        private void OnDisable()
        {
            Loc.LanguageChanged -= OnLanguageChanged;
            if (_game == null) return;
            _game.Xp.XpGained -= OnXpGained;
            _game.ProfileChanged -= Refresh;
        }

        private void OnLanguageChanged(string language) => Refresh();
        private void OnXpGained(XpState state, int amount) => Refresh();

        private void Refresh()
        {
            if (_game == null) return;
            LeagueSnapshot snapshot = _game.GetLeagueSnapshot();
            Color tierColor = LeagueTiers.Color(snapshot.Tier);

            if (tierCup != null) tierCup.color = tierColor;
            // Opaque tint, so the square's dark border stays solid.
            if (tierCupBackdrop != null) tierCupBackdrop.color = Color.Lerp(Color.white, tierColor, 0.22f);
            if (leagueName != null) leagueName.text = LeagueTiers.LeagueName(snapshot.Tier);
            if (timeLeft != null) timeLeft.text = LocalLeagueService.FormatTimeLeft(snapshot.TimeLeft);
            // Real ranking only: offline, or alone in the league, say so instead of inventing rivals.
            string top = snapshot.Offline ? UIStrings.LeagueOffline
                : snapshot.PromoteCount > 0 ? string.Format(UIStrings.TopAdvance, snapshot.PromoteCount) : UIStrings.LeagueAlone;
            if (promoteNote != null) promoteNote.text = top;
            if (demoteNote != null)
            {
                demoteNote.text = string.Format(UIStrings.BottomDemoted, snapshot.DemoteCount);
                // The label sits in its note row (icon + text): hide the whole row.
                demoteNote.transform.parent.gameObject.SetActive(!snapshot.Offline && snapshot.DemoteCount > 0);
            }

            while (_rows.Count < snapshot.Entries.Count) _rows.Add(Instantiate(rowPrefab, list));
            for (int i = 0; i < _rows.Count; i++)
            {
                bool visible = i < snapshot.Entries.Count;
                _rows[i].gameObject.SetActive(visible);
                if (visible) _rows[i].Bind(snapshot.Entries[i], theme);
            }
        }
    }
}
