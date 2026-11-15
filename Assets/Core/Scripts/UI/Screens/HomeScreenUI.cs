using System.Collections.Generic;
using Cevir.Core.Curriculum;
using Cevir.Core.Game;
using Cevir.Core.Gamification;
using Cevir.Core.LanguagePacks;
using Cevir.Core.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>
    /// Home (Learn): top bar with the language badge and streak / hearts / XP, the level header with unit progress,
    /// and the winding path of unit nodes joined by dotted lines. Tapping a unit opens its preview sheet; START
    /// plays the unit's next lesson.
    /// </summary>
    public sealed class HomeScreenUI : MonoBehaviour
    {
        [Header("Top bar")]
        [SerializeField] private TextMeshProUGUI badgeLabel;
        [SerializeField] private TextMeshProUGUI languageLabel;
        [SerializeField] private TextMeshProUGUI levelBadgeLabel;
        [SerializeField] private Image streakIcon;
        [SerializeField] private TextMeshProUGUI streakLabel;
        [SerializeField] private TextMeshProUGUI heartsLabel;
        [SerializeField] private TextMeshProUGUI xpLabel;

        [Header("Level header")]
        [SerializeField] private TextMeshProUGUI levelTitle;
        [SerializeField] private TextMeshProUGUI unitsLabel;
        [SerializeField] private Image levelProgressFill;

        [Header("Path")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform content;
        [SerializeField] private RectTransform pathLayer;
        [SerializeField] private UnitNodeUI nodePrefab;
        [SerializeField] private CevirTheme theme;
        [SerializeField] private float headerHeight = 300f;
        [SerializeField] private float unitSpacing = 330f;
        [SerializeField] private float bottomPadding = 260f;
        [SerializeField] private float dotSpacing = 34f;
        [SerializeField] private float dotSize = 22f;
        [SerializeField] private float tileClearance = 115f;

        [Tooltip("Horizontal offset of each unit from the centre, repeated along the path (matches the design).")]
        [SerializeField] private float[] winding = { -40f, 240f, 0f, -270f, -90f, 260f, 30f, -260f, -60f, 240f };

        [Header("Sheet")]
        [SerializeField] private BottomSheetUI sheet;

        private readonly List<UnitNodeUI> _nodes = new List<UnitNodeUI>();
        private readonly List<Image> _dots = new List<Image>();
        private GameManager _game;
        private bool _scrollPending = true;
        private RectTransform _focus;
        private float _contentHeight;

        private void OnEnable()
        {
            _game = GameManager.Instance;
            if (_game == null) return;

            _game.PathChanged += OnPathChanged;
            _game.Hearts.HeartsChanged += OnHeartsChanged;
            _game.Xp.XpGained += OnXpGained;
            _game.Streak.StreakChanged += OnStreakChanged;
            _game.LanguagePackChanged += OnPackChanged;
            Loc.LanguageChanged += OnLanguageChanged;

            if (sheet != null) sheet.Hide();
            _scrollPending = true;
            Refresh();
        }

        private void OnDisable()
        {
            Loc.LanguageChanged -= OnLanguageChanged;
            if (_game == null) return;
            _game.PathChanged -= OnPathChanged;
            _game.Hearts.HeartsChanged -= OnHeartsChanged;
            _game.Xp.XpGained -= OnXpGained;
            _game.Streak.StreakChanged -= OnStreakChanged;
            _game.LanguagePackChanged -= OnPackChanged;
        }

        private void OnPathChanged(IReadOnlyList<PathNode> path) => Refresh();
        private void OnHeartsChanged(int hearts) => RefreshStats();
        private void OnXpGained(XpState state, int amount) => RefreshStats();
        private void OnStreakChanged(StreakState state) => RefreshStats();
        private void OnPackChanged(ILanguagePack pack) => Refresh();
        private void OnLanguageChanged(string language) => Refresh();

        private void Refresh()
        {
            RefreshStats();
            RefreshPath();
        }

        private void RefreshStats()
        {
            if (_game == null) return;
            ILanguagePack pack = _game.ActivePack;
            if (badgeLabel != null) badgeLabel.text = pack != null ? pack.Manifest.Badge : "—";
            if (languageLabel != null) languageLabel.text = pack != null ? pack.Manifest.LocalizedName.Replace(" Sign ", "\nSign ").Replace(" İşarə ", "\nİşarə ") : string.Empty;
            if (streakLabel != null) streakLabel.text = _game.Streak.State.current.ToString();
            if (streakIcon != null)
            {
                // Grey while there is no flame; amber when yesterday was missed and today is the last chance.
                bool burning = _game.Streak.State.current > 0;
                bool atRisk = _game.Streak.IsAtRisk(_game.LocalNow());
                Color color = !burning ? UIPalette.TextMuted : atRisk ? UIPalette.Amber : UIPalette.Streak;
                streakIcon.color = color;
                if (streakLabel != null) streakLabel.color = color;
            }

            LevelPathSummary current = _game.CurrentLevel;
            if (levelBadgeLabel != null) levelBadgeLabel.text = current != null ? current.Level.Title : "A1";
            if (heartsLabel != null) heartsLabel.text = _game.Hearts.Hearts.ToString();
            if (xpLabel != null) xpLabel.text = _game.Xp.Total.ToString();
        }

        // ------------------------------------------------------------------ path

        private void RefreshPath()
        {
            if (_game == null || content == null) return;
            LevelPathSummary level = _game.CurrentLevel;
            int count = level != null ? level.Units.Count : 0;

            if (levelTitle != null) levelTitle.text = level != null ? string.Format(UIStrings.LevelTitle, level.Level.Title) : string.Empty;
            if (unitsLabel != null) unitsLabel.text = string.Format(UIStrings.UnitsProgress, level != null ? level.CompletedUnits : 0, count);
            if (levelProgressFill != null)
            {
                // A sliver stays visible at zero, like the design.
                float progress = count > 0 ? level.CompletedUnits / (float)count : 0f;
                levelProgressFill.fillAmount = Mathf.Max(0.035f, progress);
            }

            while (_nodes.Count < count)
            {
                UnitNodeUI node = Instantiate(nodePrefab, content);
                _nodes.Add(node);
            }

            float height = headerHeight + count * unitSpacing + bottomPadding;
            content.sizeDelta = new Vector2(content.sizeDelta.x, height);

            var centers = new List<Vector2>();
            UnitNodeUI active = null, lastOpen = null;
            for (int i = 0; i < _nodes.Count; i++)
            {
                UnitNodeUI node = _nodes[i];
                bool visible = i < count;
                node.gameObject.SetActive(visible);
                if (!visible) continue;

                UnitPathEntry entry = level.Units[i];
                node.Bind(entry, theme, OnUnitTapped);
                var rt = (RectTransform)node.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                Vector2 center = new Vector2(winding.Length > 0 ? winding[i % winding.Length] : 0f,
                    -(headerHeight + unitSpacing * (i + 0.62f)));
                rt.anchoredPosition = center;
                centers.Add(center);
                if (entry.State == NodeState.Active && active == null) active = node;
                if (entry.State != NodeState.Locked) lastOpen = node;
            }

            LayoutDots(centers);
            UnitNodeUI focus = active != null ? active : lastOpen;
            _focus = focus != null ? (RectTransform)focus.transform : null;
            _contentHeight = height;
        }

        private void LateUpdate()
        {
            // After the first layout pass, so the viewport has its real height.
            if (!_scrollPending || _focus == null || scrollRect == null || scrollRect.viewport == null) return;
            if (scrollRect.viewport.rect.height <= 1f) return;
            _scrollPending = false;
            ScrollTo(_focus, _contentHeight);
        }

        private void LayoutDots(List<Vector2> centers)
        {
            RectTransform layer = pathLayer != null ? pathLayer : content;
            int used = 0;
            for (int i = 0; i + 1 < centers.Count; i++)
            {
                Vector2 a = centers[i];
                Vector2 b = centers[i + 1];
                float pull = (a.y - b.y) * 0.55f;
                Vector2 c1 = a + new Vector2(0f, -pull);
                Vector2 c2 = b + new Vector2(0f, pull);

                // Sample the curve finely, then drop dots at even arc-length steps outside the tiles.
                Vector2 previous = a;
                float carried = 0f;
                const int samples = 80;
                for (int s = 1; s <= samples; s++)
                {
                    Vector2 p = Bezier(a, c1, c2, b, s / (float)samples);
                    carried += Vector2.Distance(previous, p);
                    previous = p;
                    if (carried < dotSpacing) continue;
                    carried = 0f;
                    if (Vector2.Distance(p, a) < tileClearance || Vector2.Distance(p, b) < tileClearance) continue;
                    PlaceDot(layer, used++, p);
                }
            }

            for (int i = used; i < _dots.Count; i++) _dots[i].gameObject.SetActive(false);
        }

        private void PlaceDot(RectTransform layer, int index, Vector2 position)
        {
            Image dot;
            if (index < _dots.Count)
            {
                dot = _dots[index];
            }
            else
            {
                var go = new GameObject("Dot", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(layer, false);
                go.transform.SetAsFirstSibling(); // behind the nodes
                dot = go.GetComponent<Image>();
                dot.sprite = theme != null ? theme.circle : null;
                dot.color = UIPalette.PathDot;
                dot.raycastTarget = false;
                _dots.Add(dot);
            }

            dot.gameObject.SetActive(true);
            RectTransform rt = dot.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(dotSize, dotSize);
            rt.anchoredPosition = position;
        }

        private static Vector2 Bezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float u = 1f - t;
            return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
        }

        private void ScrollTo(RectTransform target, float contentHeight)
        {
            if (scrollRect == null || scrollRect.viewport == null) return;
            float viewport = scrollRect.viewport.rect.height;
            float scrollable = contentHeight - viewport;
            if (scrollable <= 0f) return;
            float y = -target.anchoredPosition.y - viewport * 0.45f;
            scrollRect.verticalNormalizedPosition = 1f - Mathf.Clamp01(y / scrollable);
        }

        // ------------------------------------------------------------------ taps

        private void OnUnitTapped(UnitPathEntry entry)
        {
            if (entry.State == NodeState.Locked || entry.NextNode == null)
            {
                Toast(UIStrings.Locked);
                return;
            }

            string reason;
            if (!_game.CanStart(entry.NextNode, out reason) && reason == "no_hearts")
            {
                ShowNoHearts();
                return;
            }

            string kicker = string.Format(UIStrings.UnitKicker, entry.Number);
            string title = entry.Unit != null ? entry.Unit.Title : entry.Level.Title;
            string body;
            if (entry.IsCheckpoint && entry.NextNode.Kind == PathNodeKind.GateTest)
            {
                body = string.Format(UIStrings.CheckpointBody, entry.Level.Title, Mathf.RoundToInt(entry.Level.GateTest.passRatio * 100f));
            }
            else if (entry.State == NodeState.Completed)
            {
                body = UIStrings.UnitDone;
            }
            else
            {
                int lessonNumber = Mathf.Min(entry.LessonsCompleted + 1, entry.LessonsTotal);
                LessonData lesson = entry.NextNode.Lesson;
                body = string.Format(UIStrings.UnitLessons, lessonNumber, entry.LessonsTotal) +
                       (lesson != null ? ": " + lesson.Title : string.Empty);
            }

            string primary = entry.State == NodeState.Completed ? UIStrings.PracticeAgain : UIStrings.Start;
            PathNode node = entry.NextNode;
            if (sheet != null) sheet.Show(kicker, title, body, UIPalette.Primary, primary, () => StartNode(node), UIStrings.Close, null);
            else StartNode(node);
        }

        private void ShowNoHearts()
        {
            PathNode practice = FindPracticeNode();
            if (sheet == null) return;
            sheet.Show(null, UIStrings.OutOfHeartsTitle, string.Format(UIStrings.NoHeartsToStart, UIStrings.Duration(_game.Hearts.TimeToNextHeart())), UIPalette.Heart,
                practice != null ? UIStrings.Practice : null, () => StartNode(practice), UIStrings.Close, null);
        }

        /// <summary>Practice lessons cost no hearts, so they are the way back in.</summary>
        private PathNode FindPracticeNode()
        {
            PathNode fallback = null;
            foreach (PathNode node in _game.Path)
            {
                if (node.Kind != PathNodeKind.Lesson || node.State == NodeState.Locked) continue;
                if (node.Lesson.LessonType != LessonType.Practice) continue;
                if (node.State == NodeState.Completed) fallback = node;
                else return node;
            }

            return fallback;
        }

        private void StartNode(PathNode node)
        {
            if (node == null || _game == null) return;
            if (_game.StartNode(node) == null)
            {
                Toast(_game.LastStartFailure == "no_content" ? UIStrings.ComingSoon : string.Format(UIStrings.NoHeartsToStart, UIStrings.Duration(_game.Hearts.TimeToNextHeart())));
            }
        }

        private static void Toast(string message)
        {
            if (CevirUIRoot.Instance != null) CevirUIRoot.Instance.ShowToast(message);
        }
    }
}
