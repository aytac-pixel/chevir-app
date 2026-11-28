# Chevir I/O: texniki bələdçi (developers)

Technical guide for developers: project layout, setup, recognition and the server. The overview is in the [README](../README.md); the step-by-step install guide in Azerbaijani is [QURASDIRMA.md](../QURASDIRMA.md).

## Layout

```
Assets/
  Core/                         language-agnostic app (assembly Cevir.Core)
    Scripts/
      Curriculum/               LevelData, UnitData, LessonData (ScriptableObjects)
      LanguagePacks/            ILanguagePack, LanguagePackDefinition, registry, SignDefinition,
                                SignReference, SignDictionary, NonManualRule, manifest
      Recognition/              landmark types, SignFeatureExtractor, SignMatcher, config, feedback text
      Gamification/             HeartSystem, StreakSystem, XpSystem, GamificationConfig
      Game/                     GameManager, PlayerProgress, ProgressStore, CurriculumProgression,
                                UnitPath (units for the Home path), ReviewScheduler
      Lesson/                   LessonPlanBuilder, LessonSession, SignPracticeController
      Stories/                  StoryData, StorySession, FingerspellingTask, StoryProgression
      Social/                   League (ranking model, offline view, local settlement), invite link
      Cloud/                    Supabase client (accounts, database), CloudService (sync), CloudLeagueService
      Localization/             Loc (Azerbaijani, English and Russian interface; Translations word list from Tools/i18n), LocalizedString
      Audio/                    AudioManager
      UI/                       CevirUIRoot (router), AuthScreenUI, TabBarUI, BottomSheetUI, ToastUI, LessonScreenUI,
                                FeedbackOverlayUI, CameraPreviewUI, LocalizedLabel, UIStrings/UIPalette
        Screens/                HomeScreenUI + UnitNodeUI, StoriesScreenUI + StoryCardUI,
                                LeagueScreenUI + LeagueRowUI, ProfileScreenUI
        Story/                  StoryScreenUI, NpcActorUI, SpeechBubbleUI, SignClipPanelUI,
                                FingerspellTilesUI, LandmarkOverlayUI, CelebrationUI
    Editor/                     CevirUIBuilder (+ .Kit, .Screens, .Story), sprite import rules,
                                asset helpers, sign reference importer
    Tests/EditMode/             matcher, gamification, progression, story, spelling, league tests
    Sprites/                    UI (pixel panels with dark borders, from Tools/ui_art/make_pixel_sprites.py), Icons (Lucide, pixelated), Story (10 pixel scenes, girl, hands), Units (unit icons), Decor (home-screen day/night decorations) from Tools/ui_art/make_pixel_art.py, Brand (logo)
    Brand/                      app icon (from the logo)
    Resources/                  CevirCloudConfig (server URL + public key, created by setup)
    Fonts/                      Nunito SemiBold/ExtraBold, covers Azerbaijani and Cyrillic (+ DejaVu Sans fallback)
    Data/  Prefabs/  Audio/  Scenes/
  Integrations/MediaPipe/       MediaPipeLandmarkProvider + bundled hand/face models (compiles when the plugin is installed)
  Setup/Editor/                 one-click project setup and build menu / CI entry points
  LanguagePacks/
    AzSLD/                      Scripts/AzSLDLanguagePack.cs, Editor/bootstrapper,
                                Curriculum/, Signs/{Dictionary,References,Clips}, NonManualRules/, Models/
Packages/manifest.json          Unity packages (the MediaPipe plugin is added by Tools/setup)
Tools/setup/                    MediaPipe plugin installer (bash + PowerShell)
Tools/landmark_extraction/      Python: dataset videos / pack media -> raw landmark JSON
Tools/references/azsld/         landmark JSON extracted from the AzSLD lesson media (imported by the pack bootstrapper)
Tools/supabase/schema.sql       database tables, access rules and league functions
.github/workflows/              cloud APK build (game-ci)
```

Dependency direction: packs and the MediaPipe bridge reference Core; Core references neither. Adding a language never changes Core.


## Setup

Short version (step-by-step guide in Azerbaijani: `QURASDIRMA.md`):

1. Open this folder with Unity 2022.3 LTS and the Android (or iOS) build modules installed.
2. Install the MediaPipe Unity Plugin: `Tools/setup/install_mediapipe_plugin.ps1` (Windows) or `bash Tools/setup/install_mediapipe_plugin.sh`. The bridge assembly turns itself on when the package `com.github.homuler.mediapipe` is present, so there is no define to add. The hand and face models are already in `Assets/Integrations/MediaPipe/Models/`.
3. **Chevir > Setup > 1. Configure Mobile Project (one click)**: imports TMP resources, creates the Nunito TMP fonts (SemiBold + ExtraBold for bold text, DejaVu Sans as fallback; both cover Ə/ə and Cyrillic), builds the AzSLD pack, creates `Assets/Core/Resources/CevirCloudConfig.asset`, generates `Assets/Scenes/Main.unity` with the full UI, sets build settings and Android/iOS player settings (portrait, IL2CPP ARM64, min API 24, GLES3, camera usage text, internet permission, app icon from the logo, bundle id `com.chevirapp.cevir`).
4. Database: create a Supabase project, run `Tools/supabase/schema.sql` in its SQL Editor, and paste the project URL and anon key into `CevirCloudConfig` (details in `QURASDIRMA.md`). Without it the app runs on the phone only, with no accounts or league ranking.
5. **Chevir > Build > Android APK** (writes `Builds/Android/Cevir.apk`), **Android App Bundle** or **iOS Xcode Project**.
6. CI: `.github/workflows/build-android.yml` builds the APK with game-ci from a fresh clone (`Cevir.Setup.Editor.CevirBuild.CiBuildAndroid` runs the setup, then the build). It runs on every push to `main` and publishes the APK to the `demo` release (always the latest: `releases/latest/download/ChevirIO.apk`). It needs `UNITY_LICENSE`, `UNITY_EMAIL` and `UNITY_PASSWORD` secrets.


## Generated UI

**Chevir > UI > Generate Complete UI Hierarchy** (also run by the one-click setup) builds and wires the scene; re-running replaces the generated canvas:
- **Chevir Systems:** `GameManager`, `AudioManager`, `MediaPipeLandmarkProvider` (if the bridge is compiled) and `SignPracticeController`.
- **Canvas:** 1080×1920 reference size inside a `SafeAreaFitter`. `CevirUIRoot` routes between the four tab screens, the lesson screen and the story screen; Android back goes to Home, then quits.
- **Screens** as described above, plus the lesson screen and `FeedbackOverlayUI` from the first version.
- **Prefabs** in `Assets/Core/Prefabs/UI/` (UnitNode, StoryCard, LeagueRow) and a `CevirTheme` asset in `Assets/Core/Data/` holding the theme sprites.

Sprites in `Assets/Core/Sprites` are imported as UI sprites automatically; a `__b<N>` suffix in a file name sets its 9-slice border.


## Sign media (pictures and videos)

Copy files into `Assets/LanguagePacks/AzSLD/Media/` using the checklist layout (`A1/U01_Elifba/herf_a.png`) and run **Chevir > AzSLD > Import Media** (setup runs it too). `SignMediaImporter` (Core/Editor) matches file names to signs through the pack's `AzSLDLanguagePack.MediaSlug`: `<slug>.png/.jpg` → thumbnail, `<slug>.mp4` → reference clip, `<slug>_yavas.mp4` → slow clip. Pictures import as sprites, videos are transcoded to H.264 without audio. Lessons show the video when there is one and the picture otherwise. Media spec: 720×960 portrait, videos 2–5 s silent, pictures under 300 KB.

Unit 1 is letters only (A–Ə, F–I, İ–M, N–S, Ş–Z, then practice); facial expressions and fingerspelled words were removed on 2026-11-13. Re-running setup on an older project rewrites Unit 1 and Story 1 and deletes the old facial-expression signs.


## Reference data pipeline

1. Build `index.csv` (`video_path,sign_id,signer_id[,dominant_hand]`) from the AzSLD annotations.
2. `python Tools/landmark_extraction/extract_landmarks.py --index index.csv --out refs/ --hand-model hand_landmarker.task --face-model face_landmarker.task`
3. In Unity: **Chevir > Import Sign References From Folder...**, pick `refs/` and the pack's `SignDictionary`.

The importer runs the app's own `SignFeatureExtractor`, so reference and live features come from the same code.


## How recognition decides

`SignFeatureExtractor` turns each frame into: 26 handshape values per hand (15 joint angles, 4 spread angles, 4 thumb-to-fingertip distances, palm normal), the wrist position relative to the nose in face widths, and non-manual values (brow raise/furrow, mouth open/round, cheek puff, head roll/pitch/yaw from blendshapes and the face matrix, minus the learner's neutral-face baseline). Left-handed learners are mirrored into the reference space.

`SignMatcher` compares an attempt with each of the sign's reference recordings and keeps the best one:
- **Handshape**: DTW for dynamic signs; for static signs (most letters), the best-held quarter of frames against the reference's mean shape.
- **Trajectory**: DTW on the start-relative wrist path, plus a check that the net movement points the same way.
- **Location**: mean hand position relative to the face.
- An optional pack classifier (`ISignClassifier`) is blended in.
- **Non-manual**: the peak (85th percentile) of each required feature against the rule. Rules are only enforced once a linguist marks them `approved`.

The decision layer then picks one message from the plan's table: Düzgün / Barmaq bucağını düzəlt / Hərəkəti yuxarıdan başla / Mimikanı əlavə et / Əlini kameraya göstər, plus an "unsure" state for borderline scores. A good average can't pass a sign whose handshape or movement clearly failed.

Heart rule: tracking problems and "unsure" never count against the learner. Soft feedback costs a heart only on the 3rd failed attempt, and then the exercise is retried at the end. A wrong quiz answer costs a heart. Practice lessons and gate tests never cost hearts, and practice lessons restore them.


## Adding another sign language

Subclass `LanguagePackDefinition` (as `AzSLDLanguagePack` does), give it a manifest, then fill the same folders with curriculum, signs, references, rules and an optional model, and register it. Each language's rules must come from its own linguists and sign language community.


## Server (Supabase)

`Tools/supabase/schema.sql` creates these tables, all with row level security:
- `profiles`: name shown on the league table, tier, total XP, streak (readable by signed-in users, writable by the owner; the tier can only change through `settle_league`).
- `progress`: the app's whole save (JSON) per user, private to its owner.
- `weekly_xp`: XP per ISO week and tier, the source of the ranking.
- `league_settlements`: which weeks were settled, with rank and group size.
- `sign_samples`: camera attempts for the dataset, only from learners who allowed it (`profiles.data_consent`, asked at sign-up, changeable in account settings; turning it off deletes their samples). Each row has the target sign, lesson / story, judge, score, verdict, device and the MediaPipe landmarks of every frame (`landmarks` jsonb, format described in the schema). A 3 s, 144 px wide, 8 fps Motion-JPEG `.avi` of the attempt goes to the private bucket `sign-samples` (`video_path`). Code: `Cloud/SignSampleCollector.cs`, `Cloud/MjpegAvi.cs`, `UI/DataConsentUI.cs`.

- `period_xp`: XP per month and year (monthly and yearly rankings).
- `private_profiles`: phone number, readable only by its owner.
- `league_devices`: per-phone tokens for the 07:00 league message (Android background check, `Assets/Plugins/Android/LeagueWatch*.java`).

Functions: `league_standings(week, tier)`, `period_standings(period)`, `league_place_for_device(token)` and `settle_league(current_week)`. The app talks to Supabase over HTTPS (`SupabaseClient`, no SDK). Progress is saved on the phone first and uploaded 3 s after each change; on sign-in the newer copy wins.


## Licences of bundled assets

Nunito font: SIL Open Font License (`Assets/Core/Fonts/Nunito-OFL.txt`). DejaVu Sans: free licence (`DejaVu-LICENSE.txt`). Icons: Lucide, ISC licence (`Assets/Core/Sprites/Icons/LUCIDE-LICENSE.txt`); Instagram glyph from Simple Icons (CC0). Story art and voice clips were made for this project.

