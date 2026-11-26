# Chevir

2D, Duolingo-style app for learning Azerbaijani Sign Language (AİD / AzSLD). The interface is in Azerbaijani; all strings go through `Loc` so more interface languages can be added later. Recognition runs fully on the phone (MediaPipe hand + face landmarks); video never leaves the device. Built from the project plan *"Chevir — Azərbaycan İşarə Dili Öyrənmə Tətbiqi: Layihə Planı"*. All numbers (units, XP, thresholds) are the plan's proposals and are meant to be tuned.

Target: Unity 2022.3 LTS (pinned to 2022.3.45f1 in ProjectSettings/ProjectVersion.txt; Unity 6 should also work), Android + iOS, TextMeshPro, Unity Test Framework.

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
    Fonts/                      Galmuri11 pixel font, covers Azerbaijani and Cyrillic (+ DejaVu Sans fallback)
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
3. **Chevir > Setup > 1. Configure Mobile Project (one click)**: imports TMP resources, creates the Galmuri11 pixel TMP font (DejaVu Sans as fallback; both cover Ə/ə and Cyrillic), builds the AzSLD pack, creates `Assets/Core/Resources/CevirCloudConfig.asset`, generates `Assets/Scenes/Main.unity` with the full UI, sets build settings and Android/iOS player settings (portrait, IL2CPP ARM64, min API 24, GLES3, camera usage text, internet permission, app icon from the logo, bundle id `com.chevirapp.cevir`).
4. Database: create a Supabase project, run `Tools/supabase/schema.sql` in its SQL Editor, and paste the project URL and anon key into `CevirCloudConfig` (details in `QURASDIRMA.md`). Without it the app runs on the phone only, with no accounts or league ranking.
5. **Chevir > Build > Android APK** (writes `Builds/Android/Cevir.apk`), **Android App Bundle** or **iOS Xcode Project**.
6. CI: `.github/workflows/build-android.yml` builds the APK with game-ci from a fresh clone (`Cevir.Setup.Editor.CevirBuild.CiBuildAndroid` runs the setup, then the build). It runs on every push to `main` and publishes the APK to the `demo` release (always the latest: `releases/latest/download/Cevir.apk`). It needs `UNITY_LICENSE`, `UNITY_EMAIL` and `UNITY_PASSWORD` secrets.

Signs without reference landmark data are checked only for a clearly visible hand (the screen says so). Real handshape scoring takes over once references are imported (see "Reference data pipeline"). References for every lesson video and letter picture are already extracted into `Tools/references/azsld/` (one recording per sign, by `Tools/landmark_extraction/extract_pack_media.py`) and are imported automatically by *Chevir > AzSLD > Create Or Update Pack Skeleton*. Re-run the script after adding media. One recording per sign is a starting point: more signers per sign and thresholds tuned on real phones will make the check fairer. Lessons without any signs show "Tezliklə" instead of completing for free.

## Screens

Light glass theme in the logo's blues (#006EF0 → #0026C4), bottom tab bar **Dərs, Hekayə, Liqa, Profil**. With a server configured, the app opens on a sign-in / create-account screen (name, e-mail, password); progress then lives in the account.

**Hearts:** 10 at most. A wrong sign or quiz answer costs one; one heart comes back every 2 hours (the clock is saved, so it runs while the app is closed). Practice lessons cost none and also give hearts back.

**Lesson flow (Unit 1 letters):** the letter is shown (picture / video) → **ANLADIM** → the front camera opens and waits for the hand → correct: next letter; wrong: −1 heart and the same letter again; out of hearts: the lesson ends. Later units add a recognition quiz and spaced review after the letter/word pairs.

- **Home:** the pack badge ("AZ") and name, streak, hearts and XP; the level header ("A1 Level", "0/10 units", progress bar); and a winding path of the level's units. The active unit glows with a START pill; locked units are grey; the checkpoint unit has a trophy and ENTRANCE. Tapping a unit opens a sheet with its lessons and a START button (or a free practice lesson when hearts are out).
- **Stories:** "Sign Language Stories, A1 Level x/10", the unlock banner and 10 story cards (locked, NEW · START, COMPLETED, COMING SOON). A story unlocks when its unit on Home is finished. When a lesson finishes a unit, the results pop-up offers the new story right away.
- **League:** tier name ("Bürünc Liqası"), time left in the week and the real ranking of every learner in the same tier this week (top 30, by weekly XP). Up to 3 promote and 2 demote, never more than a third of the group, so someone alone in a league stays put. Weeks are settled on the server (`settle_league`). Offline, only your own row is shown with a note.
- **Profile:** avatar initials, name, join month, Edit profile (name, surname, signing hand), this week's XP, league place, tier badge, stats (streak, total XP, level, signs learned), invite (system share sheet on Android, clipboard elsewhere) with Instagram and Facebook links, and the account card (e-mail, sign out).

## Story 1: "Hello, my name is..."

A first-person scene: the street, the learner's own hands holding bag straps at the bottom, and Aysel walking up. She waves, speaks (voice clip + typing speech bubble) and signs "Hello! What is your name?" (gloss strip and reference clips when imported). "Respond in Sign Language" asks for the name once if the profile has none, then opens the front camera inside the scene:

- the mirrored camera with a live hand skeleton drawn over it;
- the first name as letter tiles (Story 1 asks the name only; `NameScope.FullName` still works for later stories), the current letter shown large with its handshape picture;
- live tips from the matcher ("Adjust your finger angle", "Show your hands clearly to the camera"), tracking problems never counted;
- after 3 mistakes on a letter, "Skip letter"; "Close camera" returns to the scene; "Can't use the camera now" finishes the answer without checking.

A correct letter turns its tile green; the full name plays a success burst and sound, and Aysel answers "Nice to meet you, {name}!". Finishing gives the story's XP (+5 if every letter was camera-checked, half XP on replays), counts for the streak and marks the story Completed.

Honest limit: AzSLD letters without imported reference recordings can't be checked yet. For those the app only checks that a hand is clearly visible, shows a note saying so, and marks the tile light green instead of green. Real scoring takes over per letter once references are imported.

The story logic (`StorySession`, `FingerspellingTask`) is plain C# with unit tests; `StoryScreenUI` only animates it. Stories 2–10 have titles and unlock rules; their scenes still need writing (they show COMING SOON).

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

Subclass `LanguagePackDefinition` (as `AzSLDLanguagePack` does), give it a manifest, then fill the same folders with curriculum, signs, references, rules and an optional model, and register it. Each language's rules must come from its own linguists and Deaf community.

## Server (Supabase)

`Tools/supabase/schema.sql` creates five tables, all with row level security:
- `profiles`: name shown on the league table, tier, total XP, streak (readable by signed-in users, writable by the owner; the tier can only change through `settle_league`).
- `progress`: the app's whole save (JSON) per user, private to its owner.
- `weekly_xp`: XP per ISO week and tier, the source of the ranking.
- `league_settlements`: which weeks were settled, with rank and group size.
- `sign_samples`: camera attempts for the dataset, only from learners who allowed it (`profiles.data_consent`, asked at sign-up, changeable in account settings; turning it off deletes their samples). Each row has the target sign, lesson / story, judge, score, verdict, device and the MediaPipe landmarks of every frame (`landmarks` jsonb, format described in the schema). A 3 s, 144 px wide, 8 fps Motion-JPEG `.avi` of the attempt goes to the private bucket `sign-samples` (`video_path`). Code: `Cloud/SignSampleCollector.cs`, `Cloud/MjpegAvi.cs`, `UI/DataConsentUI.cs`.

Functions: `league_standings(week, tier)` and `settle_league(current_week)`. The app talks to Supabase over HTTPS (`SupabaseClient`, no SDK). Progress is saved on the phone first and uploaded 3 s after each change; on sign-in the newer copy wins. XP is reported by the app, so a modified app could inflate its own league XP; move XP awarding to the server before leagues matter for prizes.

## Placeholders

- **Story art and voices:** the street, Aysel and the hands are simple vector drawings, and the voice lines are synthetic (espeak-ng). Replace the files in `Assets/Core/Sprites/Story/` and `Assets/LanguagePacks/AzSLD/Stories/Audio/` with the same names.

## Licences of bundled assets

Galmuri11 font: SIL Open Font License (`Assets/Core/Fonts/Galmuri-OFL.txt`). DejaVu Sans: free licence (`DejaVu-LICENSE.txt`). Icons: Lucide, ISC licence (`Assets/Core/Sprites/Icons/LUCIDE-LICENSE.txt`); Instagram and Facebook glyphs from Simple Icons (CC0). Story art and voice clips were made for this project.

## Status

Not yet run in the Unity editor or on a phone:
- Layout and spacing of the generated screens (expect tweaks), sprite slicing, the Galmuri11 font asset creation, night-theme colours, TMP rendering of Ə.
- The sign-in screen and sync against a real Supabase project (the database schema itself was tested on Postgres 16: tables, access rules, tier protection and weekly settlement).
- The MediaPipe bridge: API names, the `Mediapipe.Runtime` assembly name, camera rotation and handedness on each phone.
- Matcher thresholds against real AzSLD data and pilot users.

Next pieces: reference landmarks for the 25 still letters (the moving letters Ç, G, Ğ, Ö, Ü, Y, Z are not taught), scenes for stories 2–10, mini-game screens, more interface languages.

Before a commercial release: get written permission from ADA University for AzSLD (CC BY 4.0 vs CC BY-NC), and have Deaf community representatives review every clip, rule and story.
