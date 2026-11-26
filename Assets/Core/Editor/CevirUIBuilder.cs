using System;
using System.Collections.Generic;
using Cevir.Core.Audio;
using Cevir.Core.Game;
using Cevir.Core.Gamification;
using Cevir.Core.LanguagePacks;
using Cevir.Core.Lesson;
using Cevir.Core.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;
using Object = UnityEngine.Object;

namespace Cevir.Core.Editor
{
    /// <summary>
    /// Generates the whole Chevir UI in the open scene: systems (GameManager, AudioManager, camera provider,
    /// SignPracticeController), EventSystem, a portrait Canvas with the four tab screens (Home, Stories, League,
    /// Profile) and the bottom tab bar, the lesson screen with its feedback overlay, the first-person story screen,
    /// and the prefabs the screens instantiate (unit node, story card, league row, letter tile). Every serialized
    /// reference is wired, so the scene runs without manual setup. Re-running replaces the generated UI.
    /// Sprites come from Assets/Core/Sprites (see CevirSpriteImportRules); replace art there.
    /// </summary>
    public static partial class CevirUIBuilder
    {
        private const string UiRootName = "Chevir UI";
        private const string SystemsName = "Chevir Systems";
        private const string PrefabFolder = "Assets/Core/Prefabs/UI";
        private const string DataFolder = "Assets/Core/Data";
        private const string FontFolder = "Assets/Core/Fonts";
        private const string MediaPipeProviderType = "Cevir.Integrations.MediaPipe.MediaPipeLandmarkProvider, Cevir.Integrations.MediaPipe";

        private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);

        private static Sprite _rounded;
        private static Sprite _circle;
        private static TMP_FontAsset _font;
        private static readonly List<string> Problems = new List<string>();

        [MenuItem("Chevir/UI/Generate Complete UI Hierarchy")]
        private static void GenerateMenu()
        {
            Generate(true);
        }

        /// <summary>Builds the UI into the open scene. Non-interactive mode (setup, CI) never shows dialogs.</summary>
        public static bool Generate(bool interactive)
        {
            Problems.Clear();
            if (!LoadResources(interactive)) return false;

            GameObject existing = GameObject.Find(UiRootName);
            if (existing != null)
            {
                if (interactive && !EditorUtility.DisplayDialog("Chevir UI", "Replace the existing generated UI in this scene?", "Replace", "Cancel")) return false;
                Undo.DestroyObjectImmediate(existing);
            }

            CevirAssetUtility.EnsureFolder(PrefabFolder);
            CevirTheme theme = BuildTheme();
            UnitNodeUI unitPrefab = BuildUnitNodePrefab(theme);
            StoryCardUI storyCardPrefab = BuildStoryCardPrefab();
            LeagueRowUI leagueRowPrefab = BuildLeagueRowPrefab();

            SignPracticeController practice = EnsureSystems();
            EnsureEventSystem();

            GameObject canvasGo = BuildCanvas();
            Transform safe = canvasGo.transform.Find("SafeArea");

            RectTransform screens = Rect("Screens", safe);
            Stretch(screens, 0f, 0f, 0f, TabBarHeight);
            HomeScreenUI home = BuildHomeScreen(screens, unitPrefab, theme);
            StoriesScreenUI stories = BuildStoriesScreen(screens, storyCardPrefab, theme);
            LeagueScreenUI league = BuildLeagueScreen(screens, leagueRowPrefab, theme);
            ProfileScreenUI profile = BuildProfileScreen(screens, theme);
            TabBarUI tabBar = BuildTabBar(safe);

            LessonScreenUI lesson = BuildLessonScreen(safe, practice);
            StoryScreenUI story = BuildStoryScreen(safe, practice, theme);
            ToastUI toast = BuildToast(safe);
            BuildRuntimePanel<AccountSettingsUI>(canvasGo.transform, "AccountSettingsLayer", theme);
            BuildRuntimePanel<ReminderSettingsUI>(canvasGo.transform, "ReminderSettingsLayer", theme);
            BuildAuthScreen(canvasGo.transform);
            BuildRuntimePanel<NameSignPickerUI>(canvasGo.transform, "NameSignPickerLayer", theme); // above sign-in: used at sign-up
            BuildRuntimePanel<DataConsentUI>(canvasGo.transform, "DataConsentLayer", theme); // permission dialog, asked at sign-up
            toast.transform.SetParent(canvasGo.transform, false); // above the full-screen panels, so their messages show
            toast.transform.SetAsLastSibling();
            HardenShadows(canvasGo);

            CevirUIRoot root = canvasGo.AddComponent<CevirUIRoot>();
            Set(root, "tabBar", tabBar);
            Set(root, "theme", theme);
            Set(root, "homeScreen", home.gameObject);
            Set(root, "storiesScreen", stories.gameObject);
            Set(root, "leagueScreen", league.gameObject);
            Set(root, "profileScreen", profile.gameObject);
            Set(root, "lessonScreen", lesson);
            Set(root, "storyScreen", story);
            Set(root, "toast", toast);
            stories.gameObject.SetActive(false);
            league.gameObject.SetActive(false);
            profile.gameObject.SetActive(false);
            lesson.gameObject.SetActive(false);
            story.gameObject.SetActive(false);

            Undo.RegisterCreatedObjectUndo(canvasGo, "Generate Chevir UI");
            EditorSceneManager.MarkSceneDirty(canvasGo.scene);
            Selection.activeGameObject = canvasGo;
            AssetDatabase.SaveAssets();

            if (Problems.Count == 0)
            {
                Debug.Log("[Chevir] UI generated and wired. Press Play to see the Home screen.");
            }
            else
            {
                Debug.LogWarning("[Chevir] UI generated with notes:\n- " + string.Join("\n- ", Problems.ToArray()));
            }

            return true;
        }

        // ================================================================== resources

        private static bool LoadResources(bool interactive)
        {
            CevirSpriteImportRules.EnsureAll();
            _rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            _circle = Sprite("UI", "circle") ?? AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            // A project font in Assets/Core/Fonts wins: the TMP default (LiberationSans) has no Ə/ə glyph.
            // Galmuri11 (the design's pixel font) first, then any other font asset there.
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontFolder + "/Galmuri11 SDF.asset");
            string[] fonts = _font == null ? AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { FontFolder }) : new string[0];
            foreach (string guid in fonts)
            {
                _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (_font != null) break;
            }

            if (_font == null)
            {
                _font = TMP_Settings.defaultFontAsset;
                Problems.Add("No TMP font in " + FontFolder + ". Using the TMP default, which lacks 'Ə/ə'. " +
                             "Create a dynamic TMP font asset from a font with Azerbaijani glyphs (e.g. Noto Sans) there and re-run.");
            }

            if (_font == null)
            {
                const string message = "TextMeshPro resources are missing. Run Window > TextMeshPro > Import TMP Essential Resources, then try again.";
                if (interactive) EditorUtility.DisplayDialog("Chevir UI", message, "OK");
                else Debug.LogError("[Chevir] " + message);
                return false;
            }

            return true;
        }

        // ================================================================== systems

        private static SignPracticeController EnsureSystems()
        {
            GameObject systems = GameObject.Find(SystemsName);
            if (systems == null)
            {
                systems = new GameObject(SystemsName);
                Undo.RegisterCreatedObjectUndo(systems, "Create Chevir systems");
            }

            GameManager game = Object.FindFirstObjectByType<GameManager>();
            if (game == null) game = systems.AddComponent<GameManager>();
            if (GetRef(game, "registry") == null) Set(game, "registry", FindAsset<LanguagePackRegistry>(),
                "No LanguagePackRegistry asset found. Run Chevir > AzSLD > Create Or Update Pack Skeleton.");
            if (GetRef(game, "gamificationConfig") == null) Set(game, "gamificationConfig", FindAsset<GamificationConfig>());

            if (Object.FindFirstObjectByType<AudioManager>() == null) systems.AddComponent<AudioManager>();

            MonoBehaviour provider = null;
            Type providerType = Type.GetType(MediaPipeProviderType);
            if (providerType != null)
            {
                provider = Object.FindFirstObjectByType(providerType) as MonoBehaviour;
                if (provider == null) provider = (MonoBehaviour)systems.AddComponent(providerType);
                Set(provider, "handLandmarkerModel", FindTextAsset("hand_landmarker"), "hand_landmarker(.bytes) model not found; assign it on the provider.");
                Set(provider, "faceLandmarkerModel", FindTextAsset("face_landmarker"), "face_landmarker(.bytes) model not found; assign it on the provider.");
                Set(provider, "poseLandmarkerModel", FindTextAsset("pose_landmarker_lite"), "pose_landmarker_lite(.bytes) model not found; assign it on the provider.");
            }
            else
            {
                Problems.Add("MediaPipe bridge not compiled (install the plugin with Tools/setup). " +
                             "Then re-run, or assign a landmark provider on SignPracticeController.");
            }

            SignPracticeController practice = Object.FindFirstObjectByType<SignPracticeController>();
            if (practice == null) practice = systems.AddComponent<SignPracticeController>();
            if (provider != null) Set(practice, "landmarkProvider", provider);
            return practice;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            Type module = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (module != null) go.AddComponent(module);
            else go.AddComponent<StandaloneInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
        }

        private static GameObject BuildCanvas()
        {
            var go = new GameObject(UiRootName, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f; // portrait phones: layouts are designed for a 1080-unit width
            go.AddComponent<AdaptiveCanvasScaler>(); // blends towards the height on short / wide screens
            go.AddComponent<GraphicRaycaster>();

            // Soft sky background behind everything, including the notch area outside the safe area.
            RectTransform backdrop = Rect("Background", go.transform);
            Stretch(backdrop);
            Image backdropImage = AddImage(backdrop, Sprite("UI", "app_background"), Color.white);
            backdropImage.raycastTarget = false;

            RectTransform safe = Rect("SafeArea", go.transform);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            return go;
        }

        /// <summary>
        /// Full-screen panel that builds its own content at runtime (account settings, reminders): an always-active
        /// layer with the component, given the app font, theme, background and icons.
        /// </summary>
        private static T BuildRuntimePanel<T>(Transform canvas, string name, CevirTheme theme) where T : MonoBehaviour
        {
            RectTransform layer = Rect(name, canvas);
            Stretch(layer);
            T ui = layer.gameObject.AddComponent<T>();
            Set(ui, "font", _font);
            Set(ui, "theme", theme);
            Set(ui, "background", Sprite("UI", "app_background"));
            Set(ui, "iconBack", IconSprite("chevron_left"));
            Set(ui, "iconBell", IconSprite("bell"));
            if (typeof(T) == typeof(AccountSettingsUI)) Set(ui, "iconUser", IconSprite("user"));
            if (typeof(T) == typeof(DataConsentUI)) Set(ui, "iconCamera", IconSprite("camera"));

            return ui;
        }

        // ================================================================== lesson screen

        private static LessonScreenUI BuildLessonScreen(Transform parent, SignPracticeController practice)
        {
            RectTransform screen = Rect("LessonScreen", parent);
            Stretch(screen);
            AddImage(screen, null, UIPalette.Background);
            LessonScreenUI ui = screen.gameObject.AddComponent<LessonScreenUI>();

            VerticalLayoutGroup column = screen.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(40, 40, 30, 40);
            column.spacing = 24f;
            column.childAlignment = TextAnchor.UpperCenter;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            // HUD: close · progress bar · hearts · XP.
            RectTransform hud = Rect("HUD", screen);
            Layout(hud.gameObject, preferredHeight: 90f);
            HorizontalLayoutGroup hudLayout = hud.gameObject.AddComponent<HorizontalLayoutGroup>();
            hudLayout.spacing = 24f;
            hudLayout.childAlignment = TextAnchor.MiddleCenter;
            hudLayout.childControlWidth = true;
            hudLayout.childControlHeight = true;
            hudLayout.childForceExpandWidth = false;
            hudLayout.childForceExpandHeight = false;

            TextMeshProUGUI closeLabel;
            Button close = MakeButton(hud, "CloseButton", "X", Color.white, UIPalette.TextSecondary, 90f, out closeLabel);
            Layout(close.gameObject, preferredWidth: 90f, preferredHeight: 90f);

            RectTransform bar = Rect("ProgressBar", hud);
            Layout(bar.gameObject, flexibleWidth: 1f, preferredHeight: 36f);
            AddImage(bar, _rounded, UIPalette.Border, true).raycastTarget = false;
            RectTransform fillRt = Rect("Fill", bar);
            Stretch(fillRt);
            Image fill = AddImage(fillRt, _rounded, UIPalette.Green, false);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            fill.raycastTarget = false;

            RectTransform heartIcon;
            TextMeshProUGUI hearts = Chip(hud, "Hearts", UIPalette.Heart, "5", out heartIcon);
            TextMeshProUGUI xp = Chip(hud, "Xp", UIPalette.Xp, "0", out _);

            // Prompt.
            TextMeshProUGUI prompt = Text(screen, "Prompt", UIStrings.SignPrompt, 44f, UIPalette.TextPrimary, TextAlignmentOptions.MidlineLeft, true);
            TextMeshProUGUI gloss = Text(screen, "Gloss", "A", 80f, UIPalette.Blue, TextAlignmentOptions.Center, true);

            // Stage: reference clip and live camera share the flexible middle area.
            RectTransform stage = Rect("Stage", screen);
            Layout(stage.gameObject, flexibleHeight: 1f, minHeight: 500f);

            RectTransform media = Rect("MediaPanel", stage);
            Stretch(media);
            AddImage(media, _rounded, UIPalette.Surface, true).raycastTarget = false;
            RectTransform videoRt = Rect("Video", media);
            Stretch(videoRt, 16f, 16f, 16f, 16f);
            RawImage video = videoRt.gameObject.AddComponent<RawImage>();
            video.raycastTarget = false;
            AspectRatioFitter videoFit = videoRt.gameObject.AddComponent<AspectRatioFitter>();
            videoFit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            videoFit.aspectRatio = 3f / 4f; // media spec: 720×960 portrait
            VideoPlayer player = media.gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.audioOutputMode = VideoAudioOutputMode.None;

            RectTransform thumbRt = Rect("Thumbnail", media);
            Stretch(thumbRt, 40f, 40f, 40f, 40f);
            Image thumb = AddImage(thumbRt, null, Color.white);
            thumb.preserveAspect = true;
            thumb.raycastTarget = false;
            thumbRt.gameObject.SetActive(false);

            // Playback speed: 1x / 0.75x / 0.5x, bottom-right of the video.
            RectTransform speedRow = Rect("SpeedRow", media);
            speedRow.anchorMin = speedRow.anchorMax = new Vector2(1f, 0f);
            speedRow.pivot = new Vector2(1f, 0f);
            speedRow.anchoredPosition = new Vector2(-24f, 24f);
            speedRow.sizeDelta = new Vector2(420f, 72f);
            HorizontalLayoutGroup speedLayout = speedRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            speedLayout.spacing = 10f;
            speedLayout.childAlignment = TextAnchor.MiddleRight;
            speedLayout.childControlWidth = true;
            speedLayout.childControlHeight = true;
            speedLayout.childForceExpandWidth = true;
            speedLayout.childForceExpandHeight = true;
            var speedButtons = new List<Object>();
            foreach (float value in LessonScreenUI.PlaybackSpeeds)
            {
                TextMeshProUGUI speedLabel;
                Button speed = MakeButton(speedRow, "Speed_" + UIStrings.Speed(value), UIStrings.Speed(value), Color.white, UIPalette.Primary, 72f,
                    out speedLabel);
                speedLabel.fontSize = 32f;
                Stretch(speedLabel.rectTransform, 6f, 6f, 6f, 6f);
                speedButtons.Add(speed);
            }

            RectTransform cameraPanel = Rect("CameraPanel", stage);
            Stretch(cameraPanel);
            AddImage(cameraPanel, _rounded, new Color(0.12f, 0.12f, 0.12f, 1f), true).raycastTarget = false;
            RectTransform cameraFrame = Rect("Frame", cameraPanel);
            Stretch(cameraFrame, 12f, 12f, 12f, 12f);
            cameraFrame.gameObject.AddComponent<RectMask2D>();
            RectTransform previewRt = Rect("Preview", cameraFrame);
            RawImage preview = previewRt.gameObject.AddComponent<RawImage>();
            preview.raycastTarget = false;
            TextMeshProUGUI placeholder = Label(cameraFrame, "Placeholder", "Starting camera...", "Kamera açılır...", 34f, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.Center, false);
            Stretch(placeholder.rectTransform);
            CameraPreviewUI cameraPreview = cameraPanel.gameObject.AddComponent<CameraPreviewUI>();
            Set(cameraPreview, "image", preview);
            Set(cameraPreview, "frame", cameraFrame);
            Set(cameraPreview, "placeholder", placeholder.gameObject);
            Set(cameraPreview, "placeholderLabel", placeholder);
            RectTransform landmarksRt = Rect("Landmarks", cameraFrame);
            Stretch(landmarksRt);
            LandmarkOverlayUI landmarks = landmarksRt.gameObject.AddComponent<LandmarkOverlayUI>();
            Set(landmarks, "preview", cameraPreview);
            Set(landmarks, "dotSprite", Sprite("UI", "circle"));
            cameraPanel.gameObject.SetActive(false);

            // Live recognition status ("Əlini kameraya göstər", "Barmaq bucağını düzəlt", ...).
            TextMeshProUGUI status = Text(screen, "Status", UIStrings.PressToStart, 36f, UIPalette.TextSecondary, TextAlignmentOptions.Center, false);

            // Choices for "İşarəni Tap".
            RectTransform choices = Rect("Choices", screen);
            VerticalLayoutGroup choiceLayout = choices.gameObject.AddComponent<VerticalLayoutGroup>();
            choiceLayout.spacing = 16f;
            choiceLayout.childControlWidth = true;
            choiceLayout.childControlHeight = true;
            choiceLayout.childForceExpandWidth = true;
            choiceLayout.childForceExpandHeight = false;
            ContentSizeFitter choiceFit = choices.gameObject.AddComponent<ContentSizeFitter>();
            choiceFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var choiceButtons = new List<Object>();
            for (int i = 0; i < 4; i++)
            {
                TextMeshProUGUI choiceLabel;
                Button choice = MakeButton(choices, "Choice" + (i + 1), "Cavab " + (i + 1), Color.white, UIPalette.TextPrimary, 110f, out choiceLabel);
                Outline outline = choice.gameObject.AddComponent<Outline>();
                outline.effectColor = UIPalette.Border;
                outline.effectDistance = new Vector2(3f, -3f);
                choiceButtons.Add(choice);
            }

            choices.gameObject.SetActive(false);

            TextMeshProUGUI primaryLabel;
            Button primary = MakeButton(screen, "PrimaryButton", UIStrings.Start, UIPalette.Blue, Color.white, 130f, out primaryLabel);

            // Overlay sits on top and ignores the column layout.
            FeedbackOverlayUI overlay = BuildFeedbackOverlay(screen, heartIcon);

            Set(ui, "practice", practice);
            Set(ui, "overlay", overlay);
            Set(ui, "cameraPreview", cameraPreview);
            Set(ui, "landmarkOverlay", landmarks);
            Set(ui, "closeButton", close);
            Set(ui, "progressFill", fill);
            Set(ui, "heartsLabel", hearts);
            Set(ui, "heartIcon", heartIcon);
            Set(ui, "xpLabel", xp);
            Set(ui, "promptLabel", prompt);
            Set(ui, "glossLabel", gloss);
            Set(ui, "statusLabel", status);
            Set(ui, "mediaPanel", media.gameObject);
            Set(ui, "videoImage", video);
            Set(ui, "videoPlayer", player);
            Set(ui, "thumbnailImage", thumb);
            Set(ui, "speedRow", speedRow.gameObject);
            SetList(ui, "speedButtons", speedButtons);
            Set(ui, "cameraPanel", cameraPanel.gameObject);
            Set(ui, "choicesPanel", choices.gameObject);
            SetList(ui, "choiceButtons", choiceButtons);
            Set(ui, "primaryButton", primary);
            Set(ui, "primaryLabel", primaryLabel);
            return ui;
        }

        private static FeedbackOverlayUI BuildFeedbackOverlay(Transform parent, RectTransform heartIcon)
        {
            RectTransform root = Rect("FeedbackOverlay", parent);
            Stretch(root);
            Layout(root.gameObject).ignoreLayout = true;
            FeedbackOverlayUI overlay = root.gameObject.AddComponent<FeedbackOverlayUI>();

            // Bottom sheet.
            RectTransform sheet = Rect("Sheet", root);
            sheet.anchorMin = new Vector2(0f, 0f);
            sheet.anchorMax = new Vector2(1f, 0f);
            sheet.pivot = new Vector2(0.5f, 0f);
            sheet.anchoredPosition = Vector2.zero;
            Image sheetBg = AddImage(sheet, null, UIPalette.GreenLight);
            VerticalLayoutGroup sheetLayout = sheet.gameObject.AddComponent<VerticalLayoutGroup>();
            sheetLayout.padding = new RectOffset(48, 48, 40, 56);
            sheetLayout.spacing = 20f;
            sheetLayout.childControlWidth = true;
            sheetLayout.childControlHeight = true;
            sheetLayout.childForceExpandWidth = true;
            sheetLayout.childForceExpandHeight = false;
            ContentSizeFitter sheetFit = sheet.gameObject.AddComponent<ContentSizeFitter>();
            sheetFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TextMeshProUGUI sheetTitle = Text(sheet, "Title", UIStrings.CorrectTitle, 48f, UIPalette.GreenDark, TextAlignmentOptions.MidlineLeft, true);
            TextMeshProUGUI sheetMessage = Text(sheet, "Message", "Düzgün!", 36f, UIPalette.GreenDark, TextAlignmentOptions.MidlineLeft, false);
            TextMeshProUGUI sheetPrimaryLabel;
            Button sheetPrimary = MakeButton(sheet, "PrimaryButton", UIStrings.Continue, UIPalette.Green, Color.white, 120f, out sheetPrimaryLabel);
            TextMeshProUGUI sheetSecondaryLabel;
            Button sheetSecondary = MakeButton(sheet, "SecondaryButton", UIStrings.Close, Color.white, UIPalette.TextSecondary, 90f, out sheetSecondaryLabel);
            sheet.gameObject.SetActive(false);

            // Modal: results / out of hearts.
            RectTransform modal = Rect("Modal", root);
            Stretch(modal);
            AddImage(modal, null, new Color(1f, 1f, 1f, 0.97f));
            RectTransform card = Card(modal, "Card", new Vector2(0.5f, 0.5f), 900f, 56f);
            TextMeshProUGUI modalTitle = Text(card, "Title", UIStrings.LessonComplete, 60f, UIPalette.Green, TextAlignmentOptions.Center, true);
            TextMeshProUGUI modalBody = Text(card, "Body", "", 36f, UIPalette.TextSecondary, TextAlignmentOptions.Center, false);
            TextMeshProUGUI modalStats = Text(card, "Stats", "+10 XP", 40f, UIPalette.TextPrimary, TextAlignmentOptions.Center, true);
            TextMeshProUGUI modalPrimaryLabel;
            Button modalPrimary = MakeButton(card, "PrimaryButton", UIStrings.Continue, UIPalette.Green, Color.white, 120f, out modalPrimaryLabel);
            TextMeshProUGUI modalSecondaryLabel;
            Button modalSecondary = MakeButton(card, "SecondaryButton", UIStrings.BackToMap, Color.white, UIPalette.TextSecondary, 90f, out modalSecondaryLabel);
            modal.gameObject.SetActive(false);

            // "-1" that floats up from the hearts counter.
            TextMeshProUGUI heartLoss = Text(root, "HeartLoss", "-1", 56f, UIPalette.Red, TextAlignmentOptions.Center, true);
            RectTransform lossRt = heartLoss.rectTransform;
            lossRt.anchorMin = lossRt.anchorMax = new Vector2(1f, 1f);
            lossRt.pivot = new Vector2(0.5f, 0.5f);
            lossRt.anchoredPosition = new Vector2(-260f, -150f);
            lossRt.sizeDelta = new Vector2(160f, 80f);
            heartLoss.gameObject.SetActive(false);

            Set(overlay, "sheet", sheet);
            Set(overlay, "sheetBackground", sheetBg);
            Set(overlay, "sheetTitle", sheetTitle);
            Set(overlay, "sheetMessage", sheetMessage);
            Set(overlay, "sheetPrimaryButton", sheetPrimary);
            Set(overlay, "sheetPrimaryLabel", sheetPrimaryLabel);
            Set(overlay, "sheetSecondaryButton", sheetSecondary);
            Set(overlay, "sheetSecondaryLabel", sheetSecondaryLabel);
            Set(overlay, "modalRoot", modal.gameObject);
            Set(overlay, "modalCard", card);
            Set(overlay, "modalTitle", modalTitle);
            Set(overlay, "modalBody", modalBody);
            Set(overlay, "modalStats", modalStats);
            Set(overlay, "modalPrimaryButton", modalPrimary);
            Set(overlay, "modalPrimaryLabel", modalPrimaryLabel);
            Set(overlay, "modalSecondaryButton", modalSecondary);
            Set(overlay, "modalSecondaryLabel", modalSecondaryLabel);
            Set(overlay, "heartIcon", heartIcon);
            Set(overlay, "heartLossLabel", heartLoss);
            return overlay;
        }

        // ================================================================== prefabs

        private static GameObject SavePrefab(GameObject root, string name)
        {
            SetLayerRecursively(root, LayerMask.NameToLayer("UI"));
            HardenShadows(root);
            string path = PrefabFolder + "/" + name + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ================================================================== building blocks

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rt, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        private static void AnchorTop(RectTransform rt, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, height);
        }

        private static void Center(RectTransform rt, float size, float yOffset)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, yOffset + 10f);
            rt.sizeDelta = new Vector2(size, size);
        }

        private static Image AddImage(Component target, Sprite sprite, Color color, bool sliced = false)
        {
            Image image = target.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            if (sliced && sprite != null)
            {
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 0.35f; // larger corner radius at 1080p
            }

            return image;
        }

        private static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions alignment, bool bold)
        {
            RectTransform rt = Rect(name, parent);
            TextMeshProUGUI label = rt.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = _font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.raycastTarget = false;
            return label;
        }

        private static Button MakeButton(Transform parent, string name, string text, Color fill, Color textColor, float height,
            out TextMeshProUGUI label)
        {
            RectTransform rt = Rect(name, parent);
            rt.sizeDelta = new Vector2(400f, height);
            Image image = AddImage(rt, Panel(24), fill, true);
            image.pixelsPerUnitMultiplier = 1f; // square panel with the 6-unit dark border
            Button button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.97f, 0.97f, 0.97f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.6f);
            button.colors = colors;
            Layout(rt.gameObject, preferredHeight: height, minHeight: height);

            label = Text(rt, "Label", text, 38f, textColor, TextAlignmentOptions.Center, true);
            Stretch(label.rectTransform, 24f, 24f, 8f, 8f);
            return button;
        }

        /// <summary>Icon + number pill used for streak / XP / hearts.</summary>
        private static TextMeshProUGUI Chip(Transform parent, string name, Color color, string value, out RectTransform icon)
        {
            RectTransform chip = Rect(name, parent);
            HorizontalLayoutGroup row = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 10f;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            Layout(chip.gameObject, preferredWidth: 140f);

            icon = Rect("Icon", chip);
            AddImage(icon, _circle, color).raycastTarget = false;
            Layout(icon.gameObject, preferredWidth: 48f, preferredHeight: 48f);

            TextMeshProUGUI label = Text(chip, "Value", value, 38f, color, TextAlignmentOptions.MidlineLeft, true);
            Layout(label.gameObject, preferredWidth: 80f);
            return label;
        }

        /// <summary>White rounded card with a vertical layout that sizes to its content.</summary>
        private static RectTransform Card(Transform parent, string name, Vector2 anchor, float width, float padding)
        {
            RectTransform card = Rect(name, parent);
            card.anchorMin = card.anchorMax = anchor;
            card.pivot = anchor;
            card.anchoredPosition = anchor.y < 0.01f ? new Vector2(0f, 40f) : Vector2.zero;
            card.sizeDelta = new Vector2(width, 400f);
            Image cardImage = AddImage(card, Panel(36), Color.white, true);
            cardImage.pixelsPerUnitMultiplier = 1f;
            HardShadow(card.gameObject);
            VerticalLayoutGroup layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            int p = Mathf.RoundToInt(padding);
            layout.padding = new RectOffset(p, p, p, p);
            layout.spacing = 24f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fit = card.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return card;
        }

        private static LayoutElement Layout(GameObject go, float preferredWidth = -1f, float preferredHeight = -1f,
            float flexibleWidth = -1f, float flexibleHeight = -1f, float minHeight = -1f)
        {
            LayoutElement element = go.GetComponent<LayoutElement>();
            if (element == null) element = go.AddComponent<LayoutElement>();
            if (preferredWidth >= 0f) element.preferredWidth = preferredWidth;
            if (preferredHeight >= 0f) element.preferredHeight = preferredHeight;
            if (flexibleWidth >= 0f) element.flexibleWidth = flexibleWidth;
            if (flexibleHeight >= 0f) element.flexibleHeight = flexibleHeight;
            if (minHeight >= 0f) element.minHeight = minHeight;
            return element;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
        }

        // ================================================================== wiring

        /// <summary>Assigns a private [SerializeField] by name. Reports, rather than hides, a missing field or value.</summary>
        private static void Set(Object target, string field, Object value, string missingValueNote = null)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
            {
                Problems.Add(target.GetType().Name + " has no serialized field '" + field + "'.");
                return;
            }

            if (value == null)
            {
                if (missingValueNote != null) Problems.Add(missingValueNote);
                return;
            }

            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetList(Object target, string field, IList<Object> values)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            if (property == null || !property.isArray)
            {
                Problems.Add(target.GetType().Name + " has no serialized list '" + field + "'.");
                return;
            }

            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Object GetRef(Object target, string field)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            return property != null ? property.objectReferenceValue : null;
        }

        private static T FindAsset<T>() where T : Object
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) return asset;
            }

            return null;
        }

        private static TextAsset FindTextAsset(string nameContains)
        {
            foreach (string guid in AssetDatabase.FindAssets(nameContains + " t:TextAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) return asset;
            }

            return null;
        }
    }
}
