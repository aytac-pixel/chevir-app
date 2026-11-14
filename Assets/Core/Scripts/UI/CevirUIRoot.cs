using Cevir.Core.Game;
using UnityEngine;

namespace Cevir.Core.UI
{
    /// <summary>
    /// Navigation: the four tab screens with the bottom bar while on the main state, and full-screen lesson or story
    /// screens while one runs (<see cref="GameManager.State"/>). Android back returns to Home, then leaves the app.
    /// </summary>
    public sealed class CevirUIRoot : MonoBehaviour
    {
        [Header("Tabs")]
        [SerializeField] private TabBarUI tabBar;
        [SerializeField] private GameObject homeScreen;
        [SerializeField] private GameObject storiesScreen;
        [SerializeField] private GameObject leagueScreen;
        [SerializeField] private GameObject profileScreen;

        [Header("Full screen")]
        [SerializeField] private LessonScreenUI lessonScreen;
        [SerializeField] private StoryScreenUI storyScreen;

        [Header("Shared")]
        [SerializeField] private ToastUI toast;

        private GameManager _game;

        public static CevirUIRoot Instance { get; private set; }
        public MainTab CurrentTab { get; private set; }

        private void Awake()
        {
            Instance = this;
            if (tabBar != null) tabBar.TabSelected += ShowTab;
        }

        private void Start()
        {
            _game = GameManager.Instance;
            if (_game == null)
            {
                Debug.LogError("[Çevir] CevirUIRoot needs a GameManager in the scene.");
                return;
            }

            _game.StateChanged += Apply;
            Apply(_game.State);
        }

        private void OnDestroy()
        {
            if (_game != null) _game.StateChanged -= Apply;
            if (tabBar != null) tabBar.TabSelected -= ShowTab;
            if (Instance == this) Instance = null;
        }

        public void ShowTab(MainTab tab)
        {
            CurrentTab = tab;
            if (_game != null && _game.State != GameState.Map) return;
            Apply(GameState.Map);
        }

        public void ShowToast(string message)
        {
            if (toast != null) toast.Show(message);
        }

        private void Apply(GameState state)
        {
            bool main = state == GameState.Map || state == GameState.Boot;
            bool inLesson = state == GameState.Lesson || state == GameState.Results;
            bool inStory = state == GameState.Story;

            SetActive(homeScreen, main && CurrentTab == MainTab.Home);
            SetActive(storiesScreen, main && CurrentTab == MainTab.Stories);
            SetActive(leagueScreen, main && CurrentTab == MainTab.League);
            SetActive(profileScreen, main && CurrentTab == MainTab.Profile);
            if (tabBar != null)
            {
                tabBar.gameObject.SetActive(main);
                tabBar.Show(CurrentTab);
            }

            if (lessonScreen != null) lessonScreen.gameObject.SetActive(inLesson);
            if (storyScreen != null) storyScreen.gameObject.SetActive(inStory);
        }

        private void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (!Input.GetKeyDown(KeyCode.Escape) || _game == null || _game.State != GameState.Map) return;
            if (CurrentTab != MainTab.Home) ShowTab(MainTab.Home);
            else Application.Quit();
#endif
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active) go.SetActive(active);
        }
    }
}
