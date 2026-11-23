using Cevir.Core.Lesson;
using Cevir.Core.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Cevir.Core.Editor
{
    /// <summary>The first-person story screen: street scene, NPC, speech bubble, sign clips and the camera HUD.</summary>
    public static partial class CevirUIBuilder
    {
        private static StoryScreenUI BuildStoryScreen(Transform parent, SignPracticeController practice, CevirTheme theme)
        {
            RectTransform screen = Rect("StoryScreen", parent);
            Stretch(screen);
            screen.gameObject.AddComponent<Image>().color = new Color32(0xCF, 0xEB, 0xFB, 0xFF);
            StoryScreenUI ui = screen.gameObject.AddComponent<StoryScreenUI>();
            AudioSource voice = screen.gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;

            // ---------------------------------------------------------------- scene (zooms while walking)
            RectTransform sceneRoot = Rect("Scene", screen);
            Stretch(sceneRoot);
            Image background = Img(sceneRoot, "Background", Sprite("Story", "story_street_bg"), Color.white);
            background.preserveAspect = false;
            AspectRatioFitter backgroundFit = background.gameObject.AddComponent<AspectRatioFitter>();
            backgroundFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            backgroundFit.aspectRatio = 1080f / 1920f;

            // Pivot at the feet so the NPC grows toward the camera while walking in.
            RectTransform npcRt = Rect("Npc", sceneRoot);
            Place(npcRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 330f), new Vector2(490f, 1225f));
            Image npcBody = Img(npcRt, "Body", Sprite("Story", "npc_idle"), Color.white);
            Stretch(npcBody.rectTransform);
            NpcActorUI npc = npcRt.gameObject.AddComponent<NpcActorUI>();
            Set(npc, "body", npcBody);

            // The learner's own hands holding the bag straps, outside the zoom so they stay put.
            RectTransform foreground = Rect("Foreground", screen);
            foreground.anchorMin = new Vector2(0f, 0f);
            foreground.anchorMax = new Vector2(1f, 0f);
            foreground.pivot = new Vector2(0.5f, 0f);
            foreground.anchoredPosition = Vector2.zero;
            foreground.sizeDelta = new Vector2(0f, 620f);
            Image foregroundImage = Img(foreground, "Hands", Sprite("Story", "pov_hands_bag"), Color.white);
            foregroundImage.preserveAspect = false;
            Stretch(foregroundImage.rectTransform);

            // Invisible full-screen tap target for "tap to continue"; the bubble's replay button sits above it.
            RectTransform continueRt = Rect("ContinueArea", screen);
            Stretch(continueRt);
            Image continueHit = continueRt.gameObject.AddComponent<Image>();
            continueHit.color = new Color(1f, 1f, 1f, 0f);
            Button continueArea = continueRt.gameObject.AddComponent<Button>();
            continueArea.targetGraphic = continueHit;
            continueArea.transition = Selectable.Transition.None;

            // ---------------------------------------------------------------- top HUD
            RectTransform hud = Rect("Hud", screen);
            AnchorTop(hud, 150f);
            HorizontalLayoutGroup hudRow = Row(hud.gameObject, 26f);
            hudRow.padding = new RectOffset(36, 36, 30, 20);
            Button close = IconButton(hud, "CloseButton", "close", 96f, UIPalette.TextSecondary, UIPalette.Glass, true);
            RectTransform hudTexts = Rect("Texts", hud);
            Column(hudTexts.gameObject, 0, 10f, TextAnchor.MiddleLeft);
            Layout(hudTexts.gameObject, flexibleWidth: 1f);
            TextMeshProUGUI title = Text(hudTexts, "Title", "Hello, my name is...", 34f, UIPalette.TextPrimary, TextAlignmentOptions.MidlineLeft, true);
            title.enableWordWrapping = false;
            title.overflowMode = TextOverflowModes.Ellipsis;
            Image track = Img(hudTexts, "ProgressTrack", Sprite("UI", "pill"), new Color(1f, 1f, 1f, 0.75f), true);
            Layout(track.gameObject, preferredHeight: 26f, minHeight: 26f);
            Image fill = Img(track.transform, "Fill", Sprite("UI", "pill"), UIPalette.Primary);
            Stretch(fill.rectTransform);
            fill.preserveAspect = false;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 0f;

            // ---------------------------------------------------------------- dialogue
            SpeechBubbleUI bubble = BuildSpeechBubble(screen);
            SignClipPanelUI signPanel = BuildSignClipPanel(screen);

            Image hint = Glass(screen, "ContinueHint", 36, UIPalette.Glass);
            Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(520f, 96f));
            HorizontalLayoutGroup hintRow = Row(hint.gameObject, 14f, TextAnchor.MiddleCenter);
            hintRow.padding = new RectOffset(30, 30, 0, 0);
            Label(hint.transform, "Label", "Tap to continue", "Davam etmək üçün toxun", 32f, UIPalette.Primary, TextAlignmentOptions.Center);
            Image chevron = Icon(hint.transform, "Chevron", "chevron_left", 36f, UIPalette.Primary);
            chevron.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f);
            hint.gameObject.AddComponent<UIPulse>().Configure(0.04f, 0.25f, 1.6f);

            TextMeshProUGUI respondLabel;
            Button respond = GradientButton(screen, "RespondButton", "Respond in Sign Language", "İşarə dili ilə cavab ver", 150f, "hand", out respondLabel);
            Place((RectTransform)respond.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(880f, 150f));
            respond.gameObject.AddComponent<UIPulse>().Configure(0.04f, 0f, 1.4f);

            // ---------------------------------------------------------------- camera HUD
            RectTransform hudRoot = Rect("CameraHud", screen);
            Stretch(hudRoot);
            CanvasGroup cameraHud = hudRoot.gameObject.AddComponent<CanvasGroup>();
            Image dim = hudRoot.gameObject.AddComponent<Image>();
            dim.color = new Color(0.04f, 0.16f, 0.32f, 0.35f);

            Image panel = Glass(hudRoot, "Panel", 48, new Color(1f, 1f, 1f, 0.96f));
            RectTransform panelRt = panel.rectTransform;
            panelRt.anchorMin = new Vector2(0f, 0f);
            panelRt.anchorMax = new Vector2(1f, 0f);
            panelRt.pivot = new Vector2(0.5f, 0f);
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta = new Vector2(0f, 1560f);
            panel.raycastTarget = true;
            VerticalLayoutGroup panelColumn = Column(panel.gameObject, 40, 22f, TextAnchor.UpperCenter);
            panelColumn.padding = new RectOffset(40, 40, 36, 60);

            RectTransform header = Rect("Header", panelRt);
            Row(header.gameObject, 20f);
            Layout(header.gameObject, preferredHeight: 96f, minHeight: 96f);
            Icon(header, "HandIcon", "hand", 52f, UIPalette.Primary);
            TextMeshProUGUI prompt = Text(header, "Prompt", "Hello! What is your name and surname?", 36f, UIPalette.TextPrimary, TextAlignmentOptions.MidlineLeft, true);
            Layout(prompt.gameObject, flexibleWidth: 1f);
            Button closeCamera = IconButton(header, "CloseCameraButton", "close", 88f, UIPalette.TextSecondary, UIPalette.Surface, true);

            CameraPreviewUI cameraPreview;
            LandmarkOverlayUI landmarks;
            TextMeshProUGUI letter;
            Image letterThumb;
            Image tip;
            TextMeshProUGUI tipText;
            BuildStoryCamera(panelRt, out cameraPreview, out landmarks, out letter, out letterThumb, out tip, out tipText);

            FingerspellTilesUI tiles = BuildLetterTiles(panelRt);

            TextMeshProUGUI selfCheck = Text(panelRt, "SelfCheckNote", UIStrings.LetterSelfCheck, 27f, UIPalette.TextMuted, TextAlignmentOptions.Center, false);
            selfCheck.gameObject.SetActive(false);

            RectTransform footer = Rect("Footer", panelRt);
            HorizontalLayoutGroup footerRow = Row(footer.gameObject, 20f, TextAnchor.MiddleCenter);
            footerRow.childForceExpandWidth = true;
            Layout(footer.gameObject, preferredHeight: 96f, minHeight: 96f);
            Button skipLetter = ToggleButton(footer, "SkipLetterButton", "Skip letter", "Hərfi keç");
            skipLetter.gameObject.SetActive(false);

            hudRoot.gameObject.SetActive(false);

            // ---------------------------------------------------------------- name card, celebration, results
            TMP_InputField firstName, lastName;
            TextMeshProUGUI nameTitle, nameBody;
            Button nameContinue;
            GameObject nameCard = BuildNameCard(screen, out firstName, out lastName, out nameTitle, out nameBody, out nameContinue);
            CelebrationUI celebration = BuildCelebration(screen);
            FeedbackOverlayUI overlay = BuildFeedbackOverlay(screen, null);

            Set(ui, "practice", practice);
            Set(ui, "cameraPreview", cameraPreview);
            Set(ui, "landmarkOverlay", landmarks);
            Set(ui, "voice", voice);
            Set(ui, "overlay", overlay);
            Set(ui, "sceneRoot", sceneRoot);
            Set(ui, "background", background);
            Set(ui, "npc", npc);
            Set(ui, "foreground", foreground);
            Set(ui, "foregroundImage", foregroundImage);
            Set(ui, "closeButton", close);
            Set(ui, "progressFill", fill);
            Set(ui, "titleLabel", title);
            Set(ui, "bubble", bubble);
            Set(ui, "signPanel", signPanel);
            Set(ui, "continueArea", continueArea);
            Set(ui, "continueHint", hint.gameObject);
            Set(ui, "respondButton", respond);
            Set(ui, "cameraHud", cameraHud);
            Set(ui, "cameraHudPanel", panelRt);
            Set(ui, "promptLabel", prompt);
            Set(ui, "tiles", tiles);
            Set(ui, "letterLabel", letter);
            Set(ui, "letterThumbnail", letterThumb);
            Set(ui, "tipBackground", tip);
            Set(ui, "tipLabel", tipText);
            Set(ui, "selfCheckNote", selfCheck);
            Set(ui, "skipLetterButton", skipLetter);
            Set(ui, "closeCameraButton", closeCamera);
            Set(ui, "nameCard", nameCard);
            Set(ui, "firstNameField", firstName);
            Set(ui, "lastNameField", lastName);
            Set(ui, "nameCardTitle", nameTitle);
            Set(ui, "nameCardBody", nameBody);
            Set(ui, "nameContinueButton", nameContinue);
            Set(ui, "celebration", celebration);

            hint.gameObject.SetActive(false);
            respond.gameObject.SetActive(false);
            return ui;
        }

        private static SpeechBubbleUI BuildSpeechBubble(Transform parent)
        {
            Image card = Glass(parent, "SpeechBubble", 48, new Color(1f, 1f, 1f, 0.97f));
            RectTransform rt = card.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(48f, 0f);
            rt.offsetMax = new Vector2(-48f, 0f);
            rt.anchoredPosition = new Vector2(0f, -180f);
            CanvasGroup group = card.gameObject.AddComponent<CanvasGroup>();
            VerticalLayoutGroup column = Column(card.gameObject, 40, 12f);
            column.padding = new RectOffset(44, 130, 34, 40);
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TextMeshProUGUI speaker = Text(rt, "Speaker", "Aysel", 30f, UIPalette.Cyan, TextAlignmentOptions.MidlineLeft, true);
            speaker.characterSpacing = 4f;
            TextMeshProUGUI text = Text(rt, "Text", "Hello! What is your name and surname?", 46f, UIPalette.TextPrimary, TextAlignmentOptions.TopLeft, true);

            Button replay = IconButton(rt, "ReplayButton", "volume", 84f, Color.white, UIPalette.Primary, true);
            Layout(replay.gameObject).ignoreLayout = true;
            Place((RectTransform)replay.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -28f), new Vector2(84f, 84f));

            // Tail pointing down at the NPC.
            Image tail = Img(rt, "Tail", Sprite("UI", "bubble_tail"), new Color(1f, 1f, 1f, 0.97f));
            Layout(tail.gameObject).ignoreLayout = true;
            Place(tail.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-40f, 4f), new Vector2(70f, 48f));

            SpeechBubbleUI bubble = card.gameObject.AddComponent<SpeechBubbleUI>();
            Set(bubble, "group", group);
            Set(bubble, "speakerLabel", speaker);
            Set(bubble, "textLabel", text);
            Set(bubble, "replayButton", replay);
            card.gameObject.SetActive(false);
            return bubble;
        }

        private static SignClipPanelUI BuildSignClipPanel(Transform parent)
        {
            Image card = Glass(parent, "SignClipPanel", 36, new Color(1f, 1f, 1f, 0.94f));
            Place(card.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-32f, 40f), new Vector2(330f, 0f));
            Column(card.gameObject, 18, 12f, TextAnchor.UpperCenter);
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform frame = Rect("VideoFrame", card.transform);
            Layout(frame.gameObject, preferredHeight: 392f, minHeight: 392f);
            AddImage(frame, Panel(24), new Color(0.1f, 0.14f, 0.2f, 1f), true).raycastTarget = false;
            RectTransform videoRt = Rect("Video", frame);
            Stretch(videoRt, 6f, 6f, 6f, 6f);
            RawImage video = videoRt.gameObject.AddComponent<RawImage>();
            video.raycastTarget = false;
            AspectRatioFitter fit = videoRt.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 3f / 4f; // sign videos are portrait
            VideoPlayer player = frame.gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.audioOutputMode = VideoAudioOutputMode.None;

            TextMeshProUGUI gloss = Text(card.transform, "Gloss", "SALAM · SƏN · AD", 28f, UIPalette.Primary, TextAlignmentOptions.Center, true);

            SignClipPanelUI ui = card.gameObject.AddComponent<SignClipPanelUI>();
            Set(ui, "player", player);
            Set(ui, "videoImage", video);
            Set(ui, "videoFrame", frame.gameObject);
            Set(ui, "glossLabel", gloss);
            card.gameObject.SetActive(false);
            return ui;
        }

        private static void BuildStoryCamera(RectTransform parent, out CameraPreviewUI cameraPreview, out LandmarkOverlayUI landmarks,
            out TextMeshProUGUI letter, out Image letterThumb, out Image tip, out TextMeshProUGUI tipText)
        {
            RectTransform cameraPanel = Rect("CameraPanel", parent);
            Layout(cameraPanel.gameObject, preferredHeight: 860f, minHeight: 600f, flexibleHeight: 1f);
            AddImage(cameraPanel, Panel(36), new Color(0.08f, 0.12f, 0.18f, 1f), true).raycastTarget = false;
            RectTransform frame = Rect("Frame", cameraPanel);
            Stretch(frame, 8f, 8f, 8f, 8f);
            frame.gameObject.AddComponent<RectMask2D>();
            RectTransform previewRt = Rect("Preview", frame);
            RawImage preview = previewRt.gameObject.AddComponent<RawImage>();
            preview.raycastTarget = false;
            TextMeshProUGUI placeholder = Text(frame, "Placeholder", UIStrings.CameraStarting, 34f, new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.Center, false);
            Stretch(placeholder.rectTransform, 40f, 40f, 0f, 0f);

            cameraPreview = cameraPanel.gameObject.AddComponent<CameraPreviewUI>();
            Set(cameraPreview, "image", preview);
            Set(cameraPreview, "frame", frame);
            Set(cameraPreview, "placeholder", placeholder.gameObject);
            Set(cameraPreview, "placeholderLabel", placeholder);

            // Hand skeleton drawn from the frame's centre, matching the cover-fitted preview.
            RectTransform overlayRt = Rect("Landmarks", frame);
            Stretch(overlayRt);
            landmarks = overlayRt.gameObject.AddComponent<LandmarkOverlayUI>();
            Set(landmarks, "preview", cameraPreview);
            Set(landmarks, "dotSprite", Sprite("UI", "circle"));

            // Current letter: big glyph plus the reference handshape thumbnail when there is one.
            Image badge = Glass(frame, "LetterBadge", 36, new Color(1f, 1f, 1f, 0.92f));
            Place(badge.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -22f), new Vector2(0f, 150f));
            HorizontalLayoutGroup badgeRow = Row(badge.gameObject, 14f, TextAnchor.MiddleCenter);
            badgeRow.padding = new RectOffset(24, 24, 14, 14);
            badge.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            letterThumb = Img(badge.transform, "Thumbnail", null, Color.white);
            Layout(letterThumb.gameObject, preferredWidth: 120f, preferredHeight: 120f);
            letterThumb.gameObject.SetActive(false);
            letter = Text(badge.transform, "Letter", "A", 84f, UIPalette.Primary, TextAlignmentOptions.Center, true);
            Layout(letter.gameObject, preferredWidth: 110f);

            // Live tip pill ("Adjust your finger angle", "Show your hands clearly to the camera").
            RectTransform tipRt = Rect("Tip", frame);
            Place(tipRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(0f, 84f));
            tip = tipRt.gameObject.AddComponent<Image>();
            tip.sprite = Sprite("UI", "pill");
            tip.type = Image.Type.Sliced;
            tip.pixelsPerUnitMultiplier = 1f;
            tip.color = UIPalette.Primary;
            tip.raycastTarget = false;
            HorizontalLayoutGroup tipRow = Row(tipRt.gameObject, 12f, TextAnchor.MiddleCenter);
            tipRow.padding = new RectOffset(36, 36, 16, 16);
            ContentSizeFitter tipFit = tipRt.gameObject.AddComponent<ContentSizeFitter>();
            tipFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            tipFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Icon(tipRt, "Icon", "lightbulb", 38f, Color.white);
            tipText = Text(tipRt, "Label", "Show your hands clearly to the camera", 32f, Color.white, TextAlignmentOptions.Center, true);
            tipText.enableWordWrapping = false;
            tipRt.gameObject.SetActive(false);
        }

        private static FingerspellTilesUI BuildLetterTiles(RectTransform parent)
        {
            RectTransform rows = Rect("LetterTiles", parent);
            Column(rows.gameObject, 0, 14f, TextAnchor.UpperCenter).childForceExpandWidth = false;
            rows.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            FingerspellTilesUI tiles = rows.gameObject.AddComponent<FingerspellTilesUI>();

            // Templates live disabled inside the panel; FingerspellTilesUI clones them into rows.
            RectTransform templates = Rect("Templates", parent);
            Layout(templates.gameObject).ignoreLayout = true;

            RectTransform rowTemplate = Rect("WordRow", templates);
            HorizontalLayoutGroup row = Row(rowTemplate.gameObject, 10f, TextAnchor.MiddleCenter);
            row.childForceExpandWidth = false;
            ContentSizeFitter rowFit = rowTemplate.gameObject.AddComponent<ContentSizeFitter>();
            rowFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            rowFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform tileRt = Rect("LetterTile", templates);
            tileRt.sizeDelta = new Vector2(76f, 92f);
            Layout(tileRt.gameObject, preferredWidth: 76f, preferredHeight: 92f);
            Image tileBg = tileRt.gameObject.AddComponent<Image>();
            tileBg.sprite = Panel(24);
            tileBg.type = Image.Type.Sliced;
            tileBg.pixelsPerUnitMultiplier = 1f;
            tileBg.raycastTarget = false;
            Image tileLine = Img(tileRt, "Outline", Sprite("UI", "outline_r36"), UIPalette.GlassBorder, true);
            tileLine.pixelsPerUnitMultiplier = 1.5f; // thinner corners on small tiles
            Stretch(tileLine.rectTransform);
            TextMeshProUGUI tileLabel = Text(tileRt, "Letter", "A", 44f, UIPalette.TextPrimary, TextAlignmentOptions.Center, true);
            Stretch(tileLabel.rectTransform);
            LetterTileUI tile = tileRt.gameObject.AddComponent<LetterTileUI>();
            Set(tile, "background", tileBg);
            Set(tile, "outline", tileLine);
            Set(tile, "label", tileLabel);

            rowTemplate.gameObject.SetActive(false);
            tileRt.gameObject.SetActive(false);

            Set(tiles, "rows", rows);
            Set(tiles, "rowPrefab", rowTemplate);
            Set(tiles, "tilePrefab", tile);
            return tiles;
        }

        private static GameObject BuildNameCard(Transform parent, out TMP_InputField firstName, out TMP_InputField lastName,
            out TextMeshProUGUI title, out TextMeshProUGUI body, out Button next)
        {
            RectTransform root = Rect("NameCard", parent);
            Stretch(root);
            root.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.16f, 0.32f, 0.45f);

            Image card = Glass(root, "Card", 48, new Color(1f, 1f, 1f, 0.98f));
            Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(940f, 0f));
            Column(card.gameObject, 52, 24f);
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            title = Label(card.transform, "Title", "What's your name?", "Adın nədir?", 52f, UIPalette.TextPrimary);
            body = Label(card.transform, "Body", "So Aysel knows what to call you.", "Aysel səni necə çağıracağını bilsin.",
                32f, UIPalette.TextSecondary, TextAlignmentOptions.TopLeft, false);
            firstName = InputField(card.transform, "FirstName", "Name", "Ad");
            lastName = InputField(card.transform, "LastName", "Surname", "Soyad");
            TextMeshProUGUI nextLabel;
            next = GradientButton(card.transform, "ContinueButton", "CONTINUE", "DAVAM ET", 130f, null, out nextLabel);

            root.gameObject.SetActive(false);
            return root.gameObject;
        }

        private static CelebrationUI BuildCelebration(Transform parent)
        {
            RectTransform root = Rect("Celebration", parent);
            Stretch(root);

            Image badge = Img(root, "Badge", Sprite("UI", "circle"), UIPalette.Green);
            Place(badge.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(260f, 260f));
            Image glow = Img(badge.transform, "Glow", Sprite("UI", "glow"), new Color(0.17f, 0.71f, 0.45f, 0.45f));
            Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 520f));
            glow.transform.SetAsFirstSibling();
            Image check = Img(badge.transform, "Check", IconSprite("check"), Color.white);
            Place(check.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, 140f));

            TextMeshProUGUI message = Text(root, "Message", "Correct!", 60f, Color.white, TextAlignmentOptions.Center, true);
            Place(message.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 30f), new Vector2(900f, 120f));
            Outline outline = message.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.06f, 0.37f, 0.2f, 0.6f);
            outline.effectDistance = new Vector2(2f, -2f);

            CelebrationUI ui = root.gameObject.AddComponent<CelebrationUI>();
            Set(ui, "badge", badge.rectTransform);
            Set(ui, "message", message);
            Set(ui, "confettiSprite", Sprite("UI", "panel_r24"));
            root.gameObject.SetActive(false);
            return ui;
        }
    }
}
