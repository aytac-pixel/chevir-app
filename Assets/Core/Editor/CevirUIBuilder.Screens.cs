using System.Collections.Generic;
using Cevir.Core.Social;
using Cevir.Core.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cevir.Core.Editor
{
    /// <summary>Home, Stories, League and Profile screens, the tab bar and their prefabs.</summary>
    public static partial class CevirUIBuilder
    {
        // ================================================================== tab bar

        private static TabBarUI BuildTabBar(Transform parent)
        {
            RectTransform bar = Rect("TabBar", parent);
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.anchoredPosition = Vector2.zero;
            bar.sizeDelta = new Vector2(0f, TabBarHeight);
            Image background = bar.gameObject.AddComponent<Image>();
            background.color = Color.white;
            Image line = Img(bar, "TopLine", null, UIPalette.Ink);
            Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(4000f, 6f));
            Layout(line.gameObject).ignoreLayout = true;

            HorizontalLayoutGroup row = Row(bar.gameObject, 0f, TextAnchor.UpperCenter);
            row.padding = new RectOffset(28, 28, 24, 0);
            row.spacing = 24f;
            row.childForceExpandWidth = true;

            TabBarUI ui = bar.gameObject.AddComponent<TabBarUI>();
            var so = new UnityEditor.SerializedObject(ui);
            UnityEditor.SerializedProperty tabs = so.FindProperty("tabs");
            string[][] specs =
            {
                new[] { "home", "Lessons", "Dərs" }, new[] { "book", "Stories", "Hekayə" },
                new[] { "trophy", "League", "Liqa" }, new[] { "user", "Profile", "Profil" }
            };

            tabs.arraySize = specs.Length;
            for (int i = 0; i < specs.Length; i++)
            {
                RectTransform tab = Rect("Tab_" + specs[i][1], bar);
                Layout(tab.gameObject, preferredHeight: 160f, flexibleWidth: 1f);
                Image hit = tab.gameObject.AddComponent<Image>();
                hit.color = new Color(1f, 1f, 1f, 0f);
                Button button = tab.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;

                // Selected tab: blue square with a dark border behind a white icon and label (pixel design).
                Image selected = Img(tab, "Selected", Sprite("UI", "navsel_gradient"), Color.white, true);
                Place(selected.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(210f, 150f));
                Image icon = Img(tab, "Icon", IconSprite(specs[i][0]), UIPalette.Ink);
                Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -54f), new Vector2(64f, 64f));
                TextMeshProUGUI label = Label(tab, "Label", specs[i][1], specs[i][2], 28f, UIPalette.Ink, TextAlignmentOptions.Center, true);
                Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(240f, 40f));

                UnityEditor.SerializedProperty element = tabs.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("tab").enumValueIndex = i;
                element.FindPropertyRelative("button").objectReferenceValue = button;
                element.FindPropertyRelative("selectedBackground").objectReferenceValue = selected;
                element.FindPropertyRelative("icon").objectReferenceValue = icon;
                element.FindPropertyRelative("label").objectReferenceValue = label;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return ui;
        }

        private static ToastUI BuildToast(Transform parent)
        {
            RectTransform toast = Rect("Toast", parent);
            Place(toast, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, TabBarHeight + 40f), new Vector2(940f, 120f));
            Image bg = toast.gameObject.AddComponent<Image>();
            bg.sprite = Sprite("UI", "pill");
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = 1f;
            bg.color = new Color(0.12f, 0.16f, 0.23f, 0.94f);
            bg.raycastTarget = false;
            CanvasGroup group = toast.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            TextMeshProUGUI label = Text(toast, "Label", "", 32f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(label.rectTransform, 40f, 40f, 10f, 10f);
            label.enableWordWrapping = true;
            ToastUI ui = toast.gameObject.AddComponent<ToastUI>();
            Set(ui, "group", group);
            Set(ui, "label", label);
            return ui;
        }

        /// <summary>Dimmed backdrop with a glass card at the bottom; <paramref name="extra"/> receives optional form content.</summary>
        private static BottomSheetUI BuildSheet(Transform parent, string name, out RectTransform extra)
        {
            RectTransform root = Rect(name, parent);
            Stretch(root);
            Layout(root.gameObject).ignoreLayout = true;
            Button backdrop = Rect("Backdrop", root).gameObject.AddComponent<Button>();
            Stretch((RectTransform)backdrop.transform);
            Image dim = backdrop.gameObject.AddComponent<Image>();
            dim.color = new Color(0.07f, 0.13f, 0.24f, 0.45f);
            backdrop.targetGraphic = dim;
            backdrop.transition = Selectable.Transition.None;

            Image cardImage = Glass(root, "Card", 48, Color.white);
            RectTransform card = cardImage.rectTransform;
            Place(card, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1010f, 400f));
            cardImage.raycastTarget = true;
            Column(card.gameObject, 52, 22);
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Image accent = Img(card, "Accent", Sprite("UI", "pill"), UIPalette.Primary, true);
            Layout(accent.gameObject, preferredHeight: 12f);
            TextMeshProUGUI kicker = Text(card, "Kicker", "UNIT 1", 28f, UIPalette.Cyan, TextAlignmentOptions.Center, true);
            TextMeshProUGUI title = Text(card, "Title", "", 52f, UIPalette.TextPrimary, TextAlignmentOptions.Center, true);
            TextMeshProUGUI body = Text(card, "Body", "", 34f, UIPalette.TextSecondary, TextAlignmentOptions.Center, false);

            extra = Rect("Extra", card);
            Column(extra.gameObject, 0, 20);
            extra.gameObject.SetActive(false);

            TextMeshProUGUI primaryLabel;
            Button primary = GradientButton(card, "PrimaryButton", "START", null, 130f, null, out primaryLabel);
            TextMeshProUGUI secondaryLabel;
            Button secondary = MakeButton(card, "SecondaryButton", "CLOSE", new Color(1f, 1f, 1f, 0f), UIPalette.TextSecondary, 90f, out secondaryLabel);
            secondaryLabel.fontSize = 32f;

            BottomSheetUI sheet = root.gameObject.AddComponent<BottomSheetUI>();
            Set(sheet, "card", card);
            Set(sheet, "accent", accent);
            Set(sheet, "kicker", kicker);
            Set(sheet, "title", title);
            Set(sheet, "body", body);
            Set(sheet, "primaryButton", primary);
            Set(sheet, "primaryLabel", primaryLabel);
            Set(sheet, "secondaryButton", secondary);
            Set(sheet, "secondaryLabel", secondaryLabel);
            Set(sheet, "backdrop", backdrop);
            Set(sheet, "extraContent", extra.gameObject);
            root.gameObject.SetActive(false);
            return sheet;
        }

        // ================================================================== home

        private static HomeScreenUI BuildHomeScreen(Transform parent, UnitNodeUI nodePrefab, CevirTheme theme)
        {
            RectTransform screen = Rect("HomeScreen", parent);
            Stretch(screen);
            HomeScreenUI ui = screen.gameObject.AddComponent<HomeScreenUI>();

            // Top bar: language badge on the left, then level (A1), streak, hearts and XP spread evenly over the rest,
            // so the row fills any phone width instead of bunching up on one side.
            const float topBar = 170f;
            RectTransform bar = Rect("TopBar", screen);
            AnchorTop(bar, topBar);
            Image barBg = bar.gameObject.AddComponent<Image>();
            barBg.color = UIPalette.Background;
            HorizontalLayoutGroup barRow = Row(bar.gameObject, 12f, TextAnchor.MiddleCenter);
            barRow.padding = new RectOffset(60, 60, 0, 0);

            // Language badge: flag + "AZ" in a white box with a dark border.
            Image badge = Img(bar, "Badge", Panel(24), Color.white, true);
            Layout(badge.gameObject, preferredWidth: 150f, preferredHeight: 90f).minWidth = 150f;
            HorizontalLayoutGroup badgeRow = Row(badge.gameObject, 8f, TextAnchor.MiddleCenter);
            badgeRow.padding = new RectOffset(14, 14, 0, 0);
            Image flag = Img(badge.transform, "Flag", Sprite("UI", "flag_az"), Color.white);
            Layout(flag.gameObject, preferredWidth: 48f, preferredHeight: 33f);
            TextMeshProUGUI badgeLabel = Text(badge.transform, "Code", "AZ", 38f, UIPalette.Ink, TextAlignmentOptions.Center, true);
            badgeLabel.enableWordWrapping = false;
            Layout(badgeLabel.gameObject, preferredWidth: 56f, preferredHeight: 60f);

            // Level pill ("A1": the level the learner is on now).
            RectTransform levelCell = StatCell(bar, "Level");
            Image levelPill = Img(levelCell, "Pill", Sprite("UI", "white"), UIPalette.CyanLight, false);
            levelPill.preserveAspect = false;
            Layout(levelPill.gameObject, preferredWidth: 100f, preferredHeight: 72f).minWidth = 100f;
            TextMeshProUGUI levelBadge = Text(levelPill.transform, "Value", "A1", 38f, UIPalette.Primary, TextAlignmentOptions.Center, true);
            levelBadge.enableWordWrapping = false;
            Stretch(levelBadge.rectTransform);

            Image streakIcon;
            TextMeshProUGUI streak = StatChip(bar, "Streak", "flame", UIPalette.Primary, "0", out streakIcon);
            Image ignoredIcon;
            TextMeshProUGUI hearts = StatChip(bar, "Hearts", "heart", UIPalette.Heart, "10", out ignoredIcon);
            TextMeshProUGUI xp = StatChip(bar, "Xp", "zap", UIPalette.Primary, "0", out ignoredIcon);
            // Scrollable path with the level header at its top.
            RectTransform scroll = Rect("PathScroll", screen);
            Stretch(scroll, 0f, 0f, topBar, 0f);
            scroll.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
            ScrollRect scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.scrollSensitivity = 30f;
            RectTransform viewport = Rect("Viewport", scroll);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
            RectTransform content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, 3600f);
            scrollRect.viewport = viewport;
            scrollRect.content = content;

            RectTransform pathLayer = Rect("PathDots", content);
            Stretch(pathLayer);

            RectTransform header = Rect("LevelHeader", content);
            Place(header, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(960f, 200f));
            TextMeshProUGUI kicker = Label(header, "Kicker", "LEARNING PATH", "ÖYRƏNMƏ YOLU", 30f, UIPalette.Primary);
            Place(kicker.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(600f, 40f));
            kicker.characterSpacing = 4f;
            TextMeshProUGUI levelTitle = Text(header, "Title", "A1 Level", 66f, UIPalette.TextPrimary, TextAlignmentOptions.BottomLeft, true);
            Place(levelTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -40f), new Vector2(640f, 90f));
            TextMeshProUGUI units = Text(header, "Units", "0 / 10 units", 30f, UIPalette.TextSecondary, TextAlignmentOptions.BottomRight, true);
            Place(units.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -40f), new Vector2(340f, 80f));
            // Thin bar with a dark border; the blue fill sits inside the border.
            Image track = Img(header, "ProgressTrack", Sprite("UI", "pill"), Color.white, true);
            Place(track.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(960f, 30f));
            Image fill = Img(track.transform, "Fill", Sprite("UI", "white"), UIPalette.Primary, false);
            fill.preserveAspect = false;
            Stretch(fill.rectTransform, 6f, 6f, 6f, 6f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0.04f;

            RectTransform extra;
            BottomSheetUI sheet = BuildSheet(screen, "UnitSheet", out extra);

            Set(ui, "badgeLabel", badgeLabel);
            Set(ui, "levelBadgeLabel", levelBadge);
            Set(ui, "streakIcon", streakIcon);
            Set(ui, "streakLabel", streak);
            Set(ui, "heartsLabel", hearts);
            Set(ui, "xpLabel", xp);
            Set(ui, "levelTitle", levelTitle);
            Set(ui, "unitsLabel", units);
            Set(ui, "levelProgressFill", fill);
            Set(ui, "scrollRect", scrollRect);
            Set(ui, "content", content);
            Set(ui, "pathLayer", pathLayer);
            Set(ui, "nodePrefab", nodePrefab);
            Set(ui, "theme", theme);
            Set(ui, "sheet", sheet);
            return ui;
        }

        /// <summary>An equal share of the top bar, content centred in it.</summary>
        private static RectTransform StatCell(Transform parent, string name)
        {
            RectTransform cell = Rect(name, parent);
            HorizontalLayoutGroup row = Row(cell.gameObject, 8f, TextAnchor.MiddleCenter);
            row.childForceExpandWidth = false;
            Layout(cell.gameObject, preferredHeight: 90f, flexibleWidth: 1f);
            cell.GetComponent<LayoutElement>().minWidth = 100f;
            return cell;
        }

        private static TextMeshProUGUI StatChip(Transform parent, string name, string icon, Color color, string value, out Image iconImage)
        {
            RectTransform chip = StatCell(parent, name);
            iconImage = Icon(chip, "Icon", icon, 48f, color);
            TextMeshProUGUI label = Text(chip, "Value", value, 40f, UIPalette.Ink, TextAlignmentOptions.MidlineLeft, true);
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            Layout(label.gameObject, preferredHeight: 56f);
            return label;
        }

        private static UnitNodeUI BuildUnitNodePrefab(CevirTheme theme)
        {
            var root = new GameObject("UnitNode", typeof(RectTransform));
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(240f, 240f);

            // Hit area covers the tile and the START pill under it.
            Image hit = root.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            Button button = root.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;

            Image glow = Img(rt, "Glow", theme.glow, new Color(0.12f, 0.73f, 0.94f, 0.75f));
            Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(380f, 380f));
            glow.gameObject.AddComponent<UIPulse>().Configure(0.08f, 0.35f, 1.8f);

            // Pixel style has no rim; it stays (invisible) for the node script.
            Image rim = Img(rt, "Rim", theme.panel, new Color(1f, 1f, 1f, 0f), true);
            Place(rim.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(164f, 164f));

            // Hard shadow: a dark square offset down and to the right of the tile.
            Image edge = Img(rt, "Edge", theme.panel, UIPalette.Ink, true);
            Place(edge.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(10f, -10f), new Vector2(164f, 164f));
            Image face = Img(rt, "Face", theme.tileGradient, Color.white, false);
            face.preserveAspect = false;
            face.type = Image.Type.Simple;
            Place(face.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(164f, 164f));
            Image icon = Img(rt, "Icon", theme.iconBook, Color.white);
            Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80f, 80f));

            // Caption above: "UNIT 2" and the title (up to two lines), growing upward.
            RectTransform caption = Rect("Caption", rt);
            Place(caption, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0f, 112f), new Vector2(500f, 100f));
            VerticalLayoutGroup captionColumn = Column(caption.gameObject, 0, 2f, TextAnchor.LowerCenter);
            captionColumn.padding = new RectOffset(0, 0, 0, 0);
            caption.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            TextMeshProUGUI kicker = Text(caption, "Kicker", "UNIT 1", 26f, UIPalette.TextMuted, TextAlignmentOptions.Center, true);
            kicker.characterSpacing = 4f;
            TextMeshProUGUI title = Text(caption, "Title", "Unit", 36f, UIPalette.TextPrimary, TextAlignmentOptions.Center, true);
            title.lineSpacing = -10f;

            // START pill under the active tile.
            Image pill = Img(rt, "StartPill", theme.buttonGradient, Color.white, true);
            Place(pill.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(290f, 72f));
            TextMeshProUGUI startLabel = Text(pill.transform, "Label", "START", 34f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(startLabel.rectTransform);
            pill.gameObject.AddComponent<UIPulse>().Configure(0.04f, 0f, 1.8f);

            TextMeshProUGUI entrance = Text(rt, "Entrance", "ENTRANCE", 30f, UIPalette.Primary, TextAlignmentOptions.Center, true);
            Place(entrance.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(400f, 50f));
            entrance.characterSpacing = 4f;

            UnitNodeUI node = root.AddComponent<UnitNodeUI>();
            Set(node, "button", button);
            Set(node, "kicker", kicker);
            Set(node, "title", title);
            Set(node, "glow", glow);
            Set(node, "rim", rim);
            Set(node, "edge", edge);
            Set(node, "face", face);
            Set(node, "icon", icon);
            Set(node, "startPill", pill.gameObject);
            Set(node, "startLabel", startLabel);
            Set(node, "entranceLabel", entrance);
            return SavePrefab(root, "UnitNode").GetComponent<UnitNodeUI>();
        }

        // ================================================================== stories

        private static StoriesScreenUI BuildStoriesScreen(Transform parent, StoryCardUI cardPrefab, CevirTheme theme)
        {
            RectTransform screen = Rect("StoriesScreen", parent);
            Stretch(screen);
            StoriesScreenUI ui = screen.gameObject.AddComponent<StoriesScreenUI>();

            ScrollRect scrollRect;
            RectTransform content = ScrollColumn(screen, "Scroll", new RectOffset(60, 60, 70, 80), 26f, out scrollRect);

            TextMeshProUGUI kicker = Label(content, "Kicker", "STORIES", "HEKAYƏLƏR", 30f, UIPalette.Primary);
            kicker.characterSpacing = 4f;
            Label(content, "Title", "Sign Language Stories", "İşarə Dili Hekayələri", 60f, UIPalette.TextPrimary);
            RectTransform levelRow = Rect("LevelRow", content);
            Row(levelRow.gameObject, 22f);
            TextMeshProUGUI level = Text(levelRow, "Level", "A1 Level", 34f, UIPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, true);
            level.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            TextMeshProUGUI count = Text(levelRow, "Count", "0 / 10", 34f, UIPalette.Primary, TextAlignmentOptions.MidlineLeft, true);
            count.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Blue banner with a yellow star and white text.
            Image banner = Img(content, "Banner", Sprite("UI", "button_gradient"), Color.white, true);
            HardShadow(banner.gameObject);
            HorizontalLayoutGroup bannerRow = Row(banner.gameObject, 30f);
            bannerRow.padding = new RectOffset(34, 34, 34, 34);
            Image star = Icon(banner.transform, "StarIcon", "star", 80f, UIPalette.Gold);
            TextMeshProUGUI bannerText = Label(banner.transform, "Text", UIStrings.StoriesBanner, null, 34f, Color.white, TextAlignmentOptions.MidlineLeft, false);
            bannerText.gameObject.AddComponent<LocalizedLabel>().Set(
                "Complete lessons on the Home screen\n<b>to unlock new stories!</b>",
                "Əsas ekranda dərsləri tamamla\n<b>və yeni hekayələr aç!</b>");
            Layout(bannerText.gameObject, flexibleWidth: 1f);

            RectTransform list = Rect("List", content);
            Column(list.gameObject, 0, 26f);
            list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Set(ui, "levelLabel", level);
            Set(ui, "countLabel", count);
            Set(ui, "list", list);
            Set(ui, "cardPrefab", cardPrefab);
            Set(ui, "theme", theme);
            return ui;
        }

        private static StoryCardUI BuildStoryCardPrefab()
        {
            var root = new GameObject("StoryCard", typeof(RectTransform));
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(984f, 176f);
            Image background = root.AddComponent<Image>();
            background.sprite = Panel(36);
            background.type = Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = 1f;
            HardShadow(root);
            Button button = root.AddComponent<Button>();
            button.targetGraphic = background;
            Layout(root, preferredHeight: 176f, minHeight: 176f);

            HorizontalLayoutGroup row = Row(root, 30f);
            row.padding = new RectOffset(32, 32, 0, 0);

            Image numberBox = Img(rt, "NumberBox", Panel(24), Color.white, true);
            Layout(numberBox.gameObject, preferredWidth: 90f, preferredHeight: 90f);
            TextMeshProUGUI number = Text(numberBox.transform, "Number", "1", 40f, UIPalette.Ink, TextAlignmentOptions.Center, true);
            Stretch(number.rectTransform);

            RectTransform texts = Rect("Texts", rt);
            Column(texts.gameObject, 0, 6f, TextAnchor.MiddleLeft);
            Layout(texts.gameObject, flexibleWidth: 1f);
            TextMeshProUGUI title = Text(texts, "Title", "Hello, my name is...", 38f, UIPalette.TextPrimary, TextAlignmentOptions.MidlineLeft, true);
            title.enableWordWrapping = false;
            title.overflowMode = TextOverflowModes.Ellipsis;
            TextMeshProUGUI status = Text(texts, "Status", "LOCKED", 26f, UIPalette.TextMuted, TextAlignmentOptions.MidlineLeft, true);
            status.characterSpacing = 4f;

            Image badge = Img(rt, "Badge", Panel(24), new Color(1f, 1f, 1f, 0f), true);
            Layout(badge.gameObject, preferredWidth: 88f, preferredHeight: 88f);
            Image badgeIcon = Img(badge.transform, "Icon", IconSprite("lock"), UIPalette.LockedIcon);
            Place(badgeIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48f, 48f));

            StoryCardUI card = root.AddComponent<StoryCardUI>();
            Set(card, "button", button);
            Set(card, "background", background);
            Set(card, "numberBox", numberBox);
            Set(card, "numberLabel", number);
            Set(card, "titleLabel", title);
            Set(card, "statusLabel", status);
            Set(card, "badge", badge);
            Set(card, "badgeIcon", badgeIcon);
            return SavePrefab(root, "StoryCard").GetComponent<StoryCardUI>();
        }

        // ================================================================== league

        private static LeagueScreenUI BuildLeagueScreen(Transform parent, LeagueRowUI rowPrefab, CevirTheme theme)
        {
            RectTransform screen = Rect("LeagueScreen", parent);
            Stretch(screen);
            LeagueScreenUI ui = screen.gameObject.AddComponent<LeagueScreenUI>();

            ScrollRect scrollRect;
            RectTransform content = ScrollColumn(screen, "Scroll", new RectOffset(60, 60, 70, 80), 26f, out scrollRect);

            // Weekly ranking card: kicker, cup in a square, league name and the time left, all centred.
            Image header = Glass(content, "Header", 48, Color.white);
            VerticalLayoutGroup headerColumn = Column(header.gameObject, 44, 22f, TextAnchor.UpperCenter);
            headerColumn.childControlWidth = true;
            TextMeshProUGUI kicker = Label(header.transform, "Kicker", "WEEKLY RANKING", "HƏFTƏLİK REYTİNQ", 28f, new Color32(0xB0, 0x5A, 0x1E, 0xFF),
                TextAlignmentOptions.Center);
            kicker.characterSpacing = 4f;
            RectTransform cupRow = Rect("CupRow", header.transform);
            Row(cupRow.gameObject, 0f, TextAnchor.MiddleCenter);
            Image cupBackdrop = Img(cupRow, "CupBackdrop", Panel(24), UIPalette.AmberLight, true);
            Layout(cupBackdrop.gameObject, preferredWidth: 170f, preferredHeight: 170f);
            Image cup = Img(cupBackdrop.transform, "Cup", Sprite("UI", "league_cup"), LeagueTiers.Color(0));
            Place(cup.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f));
            TextMeshProUGUI name = Text(header.transform, "LeagueName", "Bronze League", 66f, UIPalette.TextPrimary, TextAlignmentOptions.Center, true);
            RectTransform pillRow = Rect("PillRow", header.transform);
            Row(pillRow.gameObject, 0f, TextAnchor.MiddleCenter);
            Image timePill = Img(pillRow, "TimePill", Sprite("UI", "pill"), UIPalette.AmberLight, true);
            HorizontalLayoutGroup pillLayout = Row(timePill.gameObject, 0f, TextAnchor.MiddleCenter);
            pillLayout.padding = new RectOffset(30, 30, 12, 12);
            TextMeshProUGUI time = Text(timePill.transform, "Label", "2 days left", 28f, new Color32(0x8A, 0x5A, 0x00, 0xFF), TextAlignmentOptions.Center, true);
            time.enableWordWrapping = false;

            TextMeshProUGUI promote = NoteRow(content, "PromoteNote", "arrow_up", UIPalette.Primary);
            TextMeshProUGUI rankingTitle = Label(content, "RankingTitle", "RANKING", "SIRALAMA", 28f, UIPalette.Ink);
            rankingTitle.characterSpacing = 4f;
            RectTransform list = Rect("Rows", content);
            Column(list.gameObject, 0, 18f);
            list.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(0, 10, 0, 10); // room for the hard shadow
            list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            TextMeshProUGUI demote = NoteRow(content, "DemoteNote", "arrow_down", UIPalette.Red);

            // Encouragement card under the ranking.
            Image goal = Glass(content, "GoalCard", 36, UIPalette.Surface);
            Column(goal.gameObject, 40, 12f);
            Label(goal.transform, "Title", "First step to the top!", "Zirvəyə ilk addım!", 46f, UIPalette.Primary);
            Label(goal.transform, "Body", "Finish lessons, earn XP and keep your place in the league.",
                "Dərsləri tamamla, XP topla və liqada yerini qoru.", 32f, UIPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, false);

            Set(ui, "tierCup", cup);
            Set(ui, "tierCupBackdrop", cupBackdrop);
            Set(ui, "leagueName", name);
            Set(ui, "timeLeft", time);
            Set(ui, "promoteNote", promote);
            Set(ui, "demoteNote", demote);
            Set(ui, "list", list);
            Set(ui, "rowPrefab", rowPrefab);
            Set(ui, "theme", theme);
            return ui;
        }

        private static TextMeshProUGUI NoteRow(Transform parent, string name, string icon, Color color)
        {
            // Light blue strip with an arrow and the note.
            RectTransform row = Rect(name, parent);
            Image strip = row.gameObject.AddComponent<Image>();
            strip.sprite = Sprite("UI", "white");
            strip.color = UIPalette.CyanLight;
            strip.raycastTarget = false;
            HorizontalLayoutGroup layout = Row(row.gameObject, 18f);
            layout.padding = new RectOffset(26, 26, 22, 22);
            Icon(row, "Icon", icon, 40f, color);
            TextMeshProUGUI label = Text(row, "Label", "", 30f, UIPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, false);
            Layout(label.gameObject, flexibleWidth: 1f);
            return label;
        }

        private static LeagueRowUI BuildLeagueRowPrefab()
        {
            var root = new GameObject("LeagueRow", typeof(RectTransform));
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(1000f, 140f);
            Layout(root, preferredHeight: 140f, minHeight: 140f);

            // The learner's row gets the hard pixel shadow (a dark copy offset down-right).
            Image glow = Img(rt, "YouGlow", Sprite("UI", "shadow_r48"), UIPalette.Ink, true);
            Stretch(glow.rectTransform, 8f, -8f, 8f, -8f);
            Layout(glow.gameObject).ignoreLayout = true;

            Image background = Img(rt, "Background", Panel(36), new Color(1f, 1f, 1f, 0f), true);
            Stretch(background.rectTransform);
            Layout(background.gameObject).ignoreLayout = true;

            HorizontalLayoutGroup row = Row(root, 26f);
            row.padding = new RectOffset(30, 36, 0, 0);

            RectTransform rank = Rect("Rank", rt);
            Layout(rank.gameObject, preferredWidth: 64f, preferredHeight: 80f);
            TextMeshProUGUI rankLabel = Text(rank, "Number", "4", 44f, UIPalette.TextSecondary, TextAlignmentOptions.Center, true);
            Stretch(rankLabel.rectTransform);
            Image cup = Img(rank, "Cup", IconSprite("trophy"), UIPalette.Gold);
            Place(cup.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(52f, 52f));

            RectTransform avatarRoot = Rect("Avatar", rt);
            Layout(avatarRoot.gameObject, preferredWidth: 100f, preferredHeight: 100f);
            Image ring = Img(avatarRoot, "Ring", Sprite("UI", "circle"), Color.white);
            Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 110f));
            Image avatar = Img(avatarRoot, "Circle", Sprite("UI", "circle"), UIPalette.Primary);
            Place(avatar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(94f, 94f));
            TextMeshProUGUI initials = Text(avatar.transform, "Initials", "LM", 32f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(initials.rectTransform);

            TextMeshProUGUI nameLabel = Text(rt, "Name", "Leyla M.", 38f, UIPalette.TextPrimary, TextAlignmentOptions.MidlineLeft, true);
            nameLabel.enableWordWrapping = false;
            Layout(nameLabel.gameObject, flexibleWidth: 1f);
            Image arrow = Img(rt, "Zone", IconSprite("arrow_up"), UIPalette.Primary);
            Layout(arrow.gameObject, preferredWidth: 40f, preferredHeight: 40f);
            TextMeshProUGUI xp = Text(rt, "Xp", "520", 38f, UIPalette.TextPrimary, TextAlignmentOptions.MidlineRight, true);
            xp.enableWordWrapping = false;
            Layout(xp.gameObject, preferredWidth: 100f);
            TextMeshProUGUI unit = Text(rt, "XpUnit", "XP", 26f, UIPalette.TextMuted, TextAlignmentOptions.MidlineLeft, true);
            Layout(unit.gameObject, preferredWidth: 46f);

            LeagueRowUI ui = root.AddComponent<LeagueRowUI>();
            Set(ui, "background", background);
            Set(ui, "youGlow", glow);
            Set(ui, "rankLabel", rankLabel);
            Set(ui, "rankCup", cup);
            Set(ui, "avatar", avatar);
            Set(ui, "avatarRing", ring);
            Set(ui, "initialsLabel", initials);
            Set(ui, "nameLabel", nameLabel);
            Set(ui, "zoneArrow", arrow);
            Set(ui, "xpLabel", xp);
            Set(ui, "xpUnitLabel", unit);
            return SavePrefab(root, "LeagueRow").GetComponent<LeagueRowUI>();
        }

        // ================================================================== profile

        private static ProfileScreenUI BuildProfileScreen(Transform parent, CevirTheme theme)
        {
            RectTransform screen = Rect("ProfileScreen", parent);
            Stretch(screen);
            ProfileScreenUI ui = screen.gameObject.AddComponent<ProfileScreenUI>();

            ScrollRect scrollRect;
            RectTransform content = ScrollColumn(screen, "Scroll", new RectOffset(60, 60, 60, 80), 34f, out scrollRect);

            // Header card.
            Image header = Img(content, "Header", Sprite("UI", "profile_header"), Color.white);
            header.preserveAspect = false;
            Layout(header.gameObject, preferredHeight: 330f);
            HardShadow(header.gameObject);
            // Square avatar: white frame, blue face, white person icon (or the photo, cut to the square).
            Image avatarRing = Img(header.transform, "AvatarRing", Sprite("UI", "outline_r36"), Color.white, true);
            Place(avatarRing.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(46f, 0f), new Vector2(220f, 220f));
            Image avatar = Img(avatarRing.transform, "Avatar", Sprite("UI", "white"), UIPalette.Primary);
            avatar.preserveAspect = false;
            Place(avatar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(208f, 208f));
            Image avatarIcon = Img(avatar.transform, "Icon", IconSprite("user"), Color.white);
            Place(avatarIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f));
            // Profile photo, cut to the circle by the avatar's mask.
            avatar.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            RectTransform photoRt = Rect("Photo", avatar.transform);
            Stretch(photoRt);
            RawImage photo = photoRt.gameObject.AddComponent<RawImage>();
            photo.raycastTarget = false;
            photoRt.gameObject.SetActive(false);

            TextMeshProUGUI name = Text(header.transform, "Name", "User", 64f, Color.white, TextAlignmentOptions.BottomLeft, true);
            Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(310f, 0f), new Vector2(330f, 90f));
            name.enableWordWrapping = false;
            name.overflowMode = TextOverflowModes.Ellipsis;
            TextMeshProUGUI joined = Text(header.transform, "Joined", "Joined October 2026", 28f, new Color(1f, 1f, 1f, 0.88f), TextAlignmentOptions.TopLeft, true);
            Place(joined.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(310f, -6f), new Vector2(360f, 50f));

            RectTransform edit = Rect("EditButton", header.transform);
            Place(edit, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-36f, 10f), new Vector2(270f, 96f));
            Image editBg = edit.gameObject.AddComponent<Image>();
            editBg.sprite = Panel(24);
            editBg.type = Image.Type.Sliced;
            editBg.pixelsPerUnitMultiplier = 1f;
            editBg.color = Color.white;
            Button editButton = edit.gameObject.AddComponent<Button>();
            editButton.targetGraphic = editBg;
            Row(edit.gameObject, 12f, TextAnchor.MiddleCenter);
            Icon(edit, "Icon", "pencil", 36f, UIPalette.Primary);
            Label(edit, "Label", "Edit profile", "Profili dəyiş", 30f, UIPalette.Primary, TextAlignmentOptions.MidlineLeft, true);

            // This week's XP · place in the league · league badge.
            Image metrics = Glass(content, "Metrics", 36, UIPalette.Glass);
            Layout(metrics.gameObject, preferredHeight: 190f);
            HorizontalLayoutGroup metricsRow = Row(metrics.gameObject, 0f, TextAnchor.MiddleCenter);
            metricsRow.childForceExpandWidth = true;
            TextMeshProUGUI weeklyXp = Metric(metrics.transform, "WeeklyXp", "XP this week", "Bu həftə XP", true);
            TextMeshProUGUI leagueRank = Metric(metrics.transform, "LeagueRank", "League place", "Liqada yer", true);
            RectTransform badgeCell = Rect("League", metrics.transform);
            Column(badgeCell.gameObject, 0, 10f, TextAnchor.MiddleCenter).childControlWidth = false;
            Layout(badgeCell.gameObject, flexibleWidth: 1f);
            Image badgeBackdrop = Img(badgeCell, "Backdrop", Sprite("UI", "white"), new Color(1f, 1f, 1f, 0f), false);
            Layout(badgeBackdrop.gameObject, preferredWidth: 76f, preferredHeight: 76f);
            Image badgeIcon = Img(badgeBackdrop.transform, "Icon", IconSprite("trophy"), LeagueTiers.Color(0));
            Place(badgeIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46f, 46f));
            TextMeshProUGUI badgeLabel = Text(badgeCell, "Label", "Bronze", 27f, UIPalette.TextSecondary, TextAlignmentOptions.Center, true);
            Layout(badgeLabel.gameObject, preferredWidth: 260f);

            // Statistics grid.
            Label(content, "StatisticsTitle", "Statistics", "Statistika", 44f, UIPalette.TextPrimary);
            RectTransform grid = Rect("StatsGrid", content);
            GridLayoutGroup gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(488f, 210f);
            gridLayout.spacing = new Vector2(24f, 24f);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 2;
            grid.gameObject.AddComponent<GridAutoFit>();
            Layout(grid.gameObject, preferredHeight: 444f);
            TextMeshProUGUI streak = StatCard(grid, "Streak", "flame", "Day Streak", "Günlük seriya", "0");
            TextMeshProUGUI xp = StatCard(grid, "Xp", "zap", "Total XP", "Ümumi XP", "0");
            TextMeshProUGUI level = StatCard(grid, "Level", "crown", "Level", "Səviyyə", "A1");
            TextMeshProUGUI signs = StatCard(grid, "Signs", "hand", "Signs Learned", "Öyrənilən işarə", "0");

            // Notifications: daily practice reminders.
            Label(content, "NotificationsTitle", "Notifications", "Bildirişlər", 44f, UIPalette.TextPrimary);
            Image notifications = Glass(content, "NotificationsCard", 36, UIPalette.Glass);
            HorizontalLayoutGroup notificationsRow = Row(notifications.gameObject, 24f);
            notificationsRow.padding = new RectOffset(30, 30, 28, 28);
            Image bellTile = Img(notifications.transform, "IconTile", Sprite("UI", "white"), new Color(1f, 1f, 1f, 0f), false);
            Layout(bellTile.gameObject, preferredWidth: 80f, preferredHeight: 104f).minWidth = 80f;
            Image bell = Img(bellTile.transform, "Icon", IconSprite("bell"), UIPalette.Primary);
            Place(bell.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(58f, 58f));
            RectTransform notificationTexts = Rect("Texts", notifications.transform);
            Column(notificationTexts.gameObject, 0, 4f, TextAnchor.MiddleLeft);
            Layout(notificationTexts.gameObject, flexibleWidth: 1f);
            Label(notificationTexts, "Title", "Practice reminders", "Məşq xatırlatmaları", 34f, UIPalette.TextPrimary);
            TextMeshProUGUI notificationsStatus = Text(notificationTexts, "Status", "", 27f, UIPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, false);
            TextMeshProUGUI notificationsLabel;
            Button notificationsButton = MakeButton(notifications.transform, "SetupButton", "AYARLA", UIPalette.CyanLight, UIPalette.Primary, 88f, out notificationsLabel);
            notificationsLabel.fontSize = 30f;
            Layout(notificationsButton.gameObject, preferredWidth: 200f).minWidth = 180f;

            // Invite.
            Label(content, "FriendsTitle", "Friends", "Dostlar", 44f, UIPalette.TextPrimary);
            Image friends = Glass(content, "FriendsCard", 48, Color.white);
            Column(friends.gameObject, 44, 18f);

            Label(friends.transform, "Title", "Learning together is more fun!", "Birlikdə öyrənmək daha maraqlıdır!", 36f, UIPalette.TextPrimary);
            Label(friends.transform, "Body", "Invite friends and share your streak.", "Dostlarını dəvət et və seriyanı paylaş.", 28f, UIPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, false);
            RectTransform actions = Rect("Actions", friends.transform);
            Row(actions.gameObject, 20f);
            Layout(actions.gameObject, preferredHeight: 124f);
            TextMeshProUGUI inviteLabel;
            Button invite = GradientButton(actions, "InviteButton", "Invite friends", "Dostları dəvət et", 124f, "users", out inviteLabel);
            Layout(invite.gameObject, flexibleWidth: 1f);
            Button instagram = IconButton(actions, "Instagram", "instagram", 116f, new Color32(0xC1, 0x35, 0x84, 0xFF), Color.white, false);
            Button facebook = IconButton(actions, "Facebook", "facebook", 116f, new Color32(0x18, 0x77, 0xF2, 0xFF), Color.white, false);

            // Account: e-mail and sign out (shown only when signed in to the server).
            Image account = Glass(content, "AccountCard", 36, UIPalette.Glass);
            Column(account.gameObject, 40, 14f);
            Label(account.transform, "Title", "Account", "Hesab", 34f, UIPalette.TextPrimary);
            TextMeshProUGUI email = Text(account.transform, "Email", "", 30f, UIPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, false);
            email.enableWordWrapping = false;
            email.overflowMode = TextOverflowModes.Ellipsis;
            TextMeshProUGUI settingsLabel;
            Button settings = MakeButton(account.transform, "SettingsButton", "HESAB AYARLARI", UIPalette.CyanLight, UIPalette.Primary, 100f, out settingsLabel);
            settingsLabel.fontSize = 32f;
            TextMeshProUGUI signOutLabel;
            Button signOut = MakeButton(account.transform, "SignOutButton", "SIGN OUT", UIPalette.RedLight, UIPalette.RedDark, 100f, out signOutLabel);
            signOutLabel.fontSize = 32f;
            signOutLabel.gameObject.AddComponent<LocalizedLabel>().Set("SIGN OUT", "HESABDAN ÇIX");
            account.gameObject.SetActive(false);

            // Edit profile sheet: name and signing hand (the app is in Azerbaijani; more languages come later).
            RectTransform form;
            BottomSheetUI sheet = BuildSheet(screen, "EditProfileSheet", out form);
            TMP_InputField first = InputField(form, "FirstName", "Name", "Ad");
            TMP_InputField last = InputField(form, "LastName", "Surname", "Soyad");
            Label(form, "HandTitle", "Signing hand", "İşarə əli", 30f, UIPalette.TextSecondary);
            RectTransform hands = Rect("Hands", form);
            Row(hands.gameObject, 16f).childForceExpandWidth = true;
            Button right = ToggleButton(hands, "Right", "Right", "Sağ");
            Button left = ToggleButton(hands, "Left", "Left", "Sol");

            Set(ui, "avatarPhoto", photo);
            Set(ui, "avatarIcon", avatarIcon.gameObject);
            Set(ui, "notificationsStatus", notificationsStatus);
            Set(ui, "notificationsButton", notificationsButton);
            Set(ui, "settingsButton", settings);
            Set(ui, "nameLabel", name);
            Set(ui, "joinedLabel", joined);
            Set(ui, "editButton", editButton);
            Set(ui, "weeklyXpValue", weeklyXp);
            Set(ui, "leagueRankValue", leagueRank);
            Set(ui, "leagueBadgeIcon", badgeIcon);
            Set(ui, "leagueBadgeBackdrop", badgeBackdrop);
            Set(ui, "leagueBadgeLabel", badgeLabel);
            Set(ui, "streakValue", streak);
            Set(ui, "xpValue", xp);
            Set(ui, "levelValue", level);
            Set(ui, "signsValue", signs);
            Set(ui, "inviteButton", invite);
            Set(ui, "instagramButton", instagram);
            Set(ui, "facebookButton", facebook);
            Set(ui, "sheet", sheet);
            Set(ui, "firstNameField", first);
            Set(ui, "lastNameField", last);
            Set(ui, "rightHandButton", right);
            Set(ui, "leftHandButton", left);
            Set(ui, "accountCard", account.gameObject);
            Set(ui, "emailLabel", email);
            Set(ui, "signOutButton", signOut);
            return ui;
        }

        /// <summary>
        /// Sign in / create account over the whole canvas (last child, so it covers the tab bar too). The component sits on
        /// an always-active layer and switches its "Panel" child, so it hears sign-out while hidden.
        /// </summary>
        private static AuthScreenUI BuildAuthScreen(Transform canvas)
        {
            RectTransform layer = Rect("AuthLayer", canvas);
            Stretch(layer);
            AuthScreenUI ui = layer.gameObject.AddComponent<AuthScreenUI>();

            RectTransform panel = Rect("Panel", layer);
            Stretch(panel);
            Image background = AddImage(panel, Sprite("UI", "app_background"), Color.white);
            background.preserveAspect = false;
            background.raycastTarget = true;

            RectTransform safe = Rect("SafeArea", panel);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            ScrollRect scrollRect;
            RectTransform content = ScrollColumn(safe, "Scroll", new RectOffset(70, 70, 150, 120), 26f, out scrollRect);
            content.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperCenter;

            Image logo = Img(content, "Logo", Sprite("Brand", "cevir_logo"), Color.white);
            Layout(logo.gameObject, preferredWidth: 300f, preferredHeight: 300f);
            logo.GetComponent<LayoutElement>().flexibleWidth = 0f;
            TextMeshProUGUI brand = Text(content, "Brand", "Chevir", 96f, UIPalette.PrimaryDark, TextAlignmentOptions.Center, true);
            Layout(brand.gameObject, preferredHeight: 120f);
            TextMeshProUGUI tagline = Label(content, "Tagline", "Learn Azerbaijani Sign Language", "Azərbaycan İşarə Dilini öyrən", 36f,
                UIPalette.TextSecondary, TextAlignmentOptions.Center, false);
            Layout(tagline.gameObject, preferredHeight: 60f);

            Image card = Glass(content, "Card", 48, Color.white);
            Column(card.gameObject, 52, 24f);
            TextMeshProUGUI title = Text(card.transform, "Title", "Create an account", 52f, UIPalette.TextPrimary, TextAlignmentOptions.Center, true);
            Layout(title.gameObject, preferredHeight: 80f);
            TMP_InputField name = InputField(card.transform, "Name", "Name and surname", "Ad və soyad");
            name.characterLimit = 40;
            TMP_InputField email = InputField(card.transform, "Email", "E-mail", "E-poçt");
            TMP_InputField code = InputField(card.transform, "Code", "6-digit code from the e-mail", "E-poçta gələn 6 rəqəmli kod");
            code.gameObject.SetActive(false);
            TMP_InputField password = InputField(card.transform, "Password", "Password", "Şifrə");
            TextMeshProUGUI message = Text(card.transform, "Message", "", 30f, UIPalette.Red, TextAlignmentOptions.Center, false);
            message.gameObject.SetActive(false);
            TextMeshProUGUI primaryLabel;
            Button primary = GradientButton(card.transform, "PrimaryButton", "CREATE ACCOUNT", null, 130f, null, out primaryLabel);
            TextMeshProUGUI forgotLabel;
            Button forgot = MakeButton(card.transform, "ForgotButton", "Forgot password?", new Color(1f, 1f, 1f, 0f), UIPalette.Primary, 80f, out forgotLabel);
            forgotLabel.fontSize = 30f;
            forgotLabel.gameObject.AddComponent<LocalizedLabel>().Set("Forgot password?", "Şifrəni unutmusan?");
            TextMeshProUGUI switchLabel;
            Button switchMode = MakeButton(content, "SwitchButton", "", new Color(1f, 1f, 1f, 0f), UIPalette.PrimaryDark, 90f, out switchLabel);
            switchLabel.fontSize = 32f;

            Set(ui, "root", panel.gameObject);
            Set(ui, "titleLabel", title);
            Set(ui, "nameRow", name.gameObject);
            Set(ui, "nameField", name);
            Set(ui, "emailField", email);
            Set(ui, "passwordField", password);
            Set(ui, "codeField", code);
            Set(ui, "primaryButton", primary);
            Set(ui, "primaryLabel", primaryLabel);
            Set(ui, "switchButton", switchMode);
            Set(ui, "switchLabel", switchLabel);
            Set(ui, "forgotButton", forgot);
            Set(ui, "messageLabel", message);
            return ui;
        }

        private static TextMeshProUGUI Metric(Transform parent, string name, string en, string az, bool divider)
        {
            RectTransform cell = Rect(name, parent);
            Column(cell.gameObject, 0, 6f, TextAnchor.MiddleCenter);
            Layout(cell.gameObject, flexibleWidth: 1f);
            TextMeshProUGUI value = Text(cell, "Value", "0", 50f, UIPalette.TextPrimary, TextAlignmentOptions.Center, true);
            Label(cell, "Label", en, az, 27f, UIPalette.TextSecondary, TextAlignmentOptions.Center, true);
            if (divider)
            {
                Image line = Img(cell, "Divider", null, UIPalette.Border);
                Place(line.rectTransform, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3f, 110f));
                Layout(line.gameObject).ignoreLayout = true;
            }

            return value;
        }

        private static TextMeshProUGUI StatCard(Transform parent, string name, string icon, string en, string az, string value)
        {
            Image card = Glass(parent, name, 36, UIPalette.Glass);
            HorizontalLayoutGroup row = Row(card.gameObject, 26f);
            row.padding = new RectOffset(30, 20, 0, 0);
            Image tile = Img(card.transform, "IconTile", Sprite("UI", "white"), new Color(1f, 1f, 1f, 0f), false);
            Layout(tile.gameObject, preferredWidth: 80f, preferredHeight: 110f);
            Image glyph = Img(tile.transform, "Icon", IconSprite(icon), UIPalette.Primary);
            Place(glyph.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(62f, 62f));
            RectTransform texts = Rect("Texts", card.transform);
            Column(texts.gameObject, 0, 2f, TextAnchor.MiddleLeft);
            Layout(texts.gameObject, flexibleWidth: 1f);
            TextMeshProUGUI valueLabel = Text(texts, "Value", value, 54f, UIPalette.TextPrimary, TextAlignmentOptions.MidlineLeft, true);
            Label(texts, "Label", en, az, 26f, UIPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, true);
            return valueLabel;
        }
    }
}
