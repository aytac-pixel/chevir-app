using System.IO;
using Cevir.Core.Cloud;
using Cevir.Core.Editor;
using Cevir.LanguagePacks.AzSLD.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace Cevir.Setup.Editor
{
    /// <summary>
    /// One click from a fresh checkout to a buildable mobile project:
    /// TMP resources, an Azerbaijani-capable font, the AzSLD pack, the server settings asset, the main scene with
    /// the generated UI, build settings and Android/iOS player settings (icon from the logo). Safe to re-run.
    /// </summary>
    public static class CevirProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        public const string BundleId = "com.chevirapp.cevir";
        private const string AppIconPath = "Assets/Core/Brand/cevir_app_icon.png";

        private const string FontSource = "Assets/Core/Fonts/DejaVuSans.ttf";
        private const string FontAssetPath = "Assets/Core/Fonts/DejaVuSans SDF.asset";
        private const string MainFontSource = "Assets/Core/Fonts/Nunito-SemiBold.ttf";
        private const string MainFontAssetPath = "Assets/Core/Fonts/Nunito SDF.asset";
        private const string BoldFontSource = "Assets/Core/Fonts/Nunito-ExtraBold.ttf";
        private const string BoldFontAssetPath = "Assets/Core/Fonts/Nunito ExtraBold SDF.asset";

        /// <summary>
        /// Nunito is a clean, rounded sans (not a pixel font) that reads easily on small phone screens and sits well next
        /// to the pixel art. It covers every Azerbaijani letter (Ə/ə included) and full Cyrillic for the Russian interface.
        /// At 1.05 a line is a little narrower than the old pixel font setting, so every label still fits.
        /// </summary>
        private const float MainFontScale = 1.05f;

        /// <summary>Atlas sampling size: large enough for sharp edges, small enough that all letters fit one 2048 atlas.</summary>
        private const int MainSamplingSize = 72;

        /// <summary>Letters the UI needs up front; anything else is added to the dynamic atlas on demand.</summary>
        private const string Prewarm =
            "ABCÇDEƏFGĞHXIİJKQLMNOÖPRSŞTUÜVYZabcçdeəfgğhxıijkqlmnoöprsştuüvyz0123456789" +
            "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя" +
            ".,:;!?-–—+%/()·…\"'«» ";

        [MenuItem("Chevir/Setup/1. Configure Mobile Project (one click)", priority = 0)]
        private static void RunMenu()
        {
            if (Run(true))
            {
                EditorUtility.DisplayDialog("Chevir",
                    "Layihə hazırdır.\n\nPlay düyməsini bas və ya Chevir > Build > Android APK ilə telefona yığ.", "OK");
            }
        }

        public static bool Run(bool interactive)
        {
            if (!EnsureTmpResources(interactive)) return false;
            EnsureFontAsset();
            CevirSpriteImportRules.EnsureAll(); // story art must be Sprites before the pack bootstrapper references it

            AzSLDPackBootstrapper.Create();
            EnsureCloudConfig();

            if (!BuildMainScene(interactive)) return false;
            ConfigurePlayer();
            AssetDatabase.SaveAssets();
            Debug.Log("[Chevir] Project configured: scene " + ScenePath + ", bundle id " + BundleId + ".");
            return true;
        }

        // ------------------------------------------------------------------ TMP and font

        private static bool EnsureTmpResources(bool interactive)
        {
            if (TMP_Settings.instance != null) return true;

            TMP_PackageResourceImporter.ImportResources(true, false, false);
            AssetDatabase.Refresh();
            if (TMP_Settings.instance != null) return true;

            const string message = "TextMeshPro Essential Resources are being imported. Run Chevir > Setup > 1 again once importing finishes.";
            if (interactive) EditorUtility.DisplayDialog("Chevir", message, "OK");
            else Debug.LogError("[Chevir] " + message);
            return false;
        }

        /// <summary>
        /// Nunito SemiBold for normal text and Nunito ExtraBold for bold text (a real bold face through the font weight
        /// table, so no faux bold), with DejaVu Sans as the fallback for anything they lack (arrows, symbols).
        /// All atlases are dynamic.
        /// </summary>
        private static void EnsureFontAsset()
        {
            TMP_FontAsset fallback = CreateFontAsset(FontSource, FontAssetPath, "DejaVuSans SDF");
            TMP_FontAsset main = CreateFontAsset(MainFontSource, MainFontAssetPath, "Nunito SDF", MainSamplingSize, 8, 2048);
            if (main == null) return;
            TMP_FontAsset bold = CreateFontAsset(BoldFontSource, BoldFontAssetPath, "Nunito ExtraBold SDF", MainSamplingSize, 8, 2048);

            SetScale(main);
            if (bold != null)
            {
                SetScale(bold);
                TMP_FontWeightPair[] weights = main.fontWeightTable;
                if (weights != null && weights.Length > 7)
                {
                    weights[7].regularTypeface = bold; // FontWeight.Bold (700) = what FontStyles.Bold uses (array of structs, set in place)
                }
            }

            if (fallback != null)
            {
                foreach (TMP_FontAsset asset in new[] { main, bold })
                {
                    if (asset == null) continue;
                    if (asset.fallbackFontAssetTable == null) asset.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
                    if (!asset.fallbackFontAssetTable.Contains(fallback)) asset.fallbackFontAssetTable.Add(fallback);
                    EditorUtility.SetDirty(asset);
                }
            }

            EditorUtility.SetDirty(main);
            AssetDatabase.SaveAssets();
        }

        private static void SetScale(TMP_FontAsset asset)
        {
            var so = new SerializedObject(asset);
            SerializedProperty scale = so.FindProperty("m_FaceInfo.m_Scale");
            if (scale != null && !Mathf.Approximately(scale.floatValue, MainFontScale))
            {
                scale.floatValue = MainFontScale;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static TMP_FontAsset CreateFontAsset(string sourcePath, string assetPath, string assetName, int samplingSize = 90, int padding = 9, int atlasSize = 1024)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (font == null)
            {
                Debug.LogWarning("[Chevir] " + sourcePath + " not found; text falls back to the next font.");
                return null;
            }

            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, samplingSize, padding, GlyphRenderMode.SDFAA, atlasSize, atlasSize,
                AtlasPopulationMode.Dynamic, atlasSize == 1024);
            asset.name = assetName;
            AssetDatabase.CreateAsset(asset, assetPath);

            // Atlas texture and material must live inside the asset or they are lost on reload.
            asset.atlasTextures[0].name = assetName + " Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            asset.material.name = assetName + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);

            string missing;
            asset.TryAddCharacters(Prewarm, out missing);
            if (!string.IsNullOrEmpty(missing) && assetPath == FontAssetPath) Debug.LogWarning("[Chevir] Font is missing characters: " + missing);

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        // ------------------------------------------------------------------ server

        public const string CloudConfigPath = "Assets/Core/Resources/" + CloudConfig.ResourceName + ".asset";

        /// <summary>Creates the (empty) server settings asset; fill in the Supabase URL and anon key there.</summary>
        private static void EnsureCloudConfig()
        {
            CloudConfig config = CevirAssetUtility.LoadOrCreate<CloudConfig>(CloudConfigPath);
            if (!config.IsConfigured)
            {
                Debug.LogWarning("[Chevir] Fill in the Supabase URL and anon key in " + CloudConfigPath +
                                 " (QURASDIRMA.md: 'Verilənlər bazası'). Until then accounts and the league are off.");
            }
        }

        // ------------------------------------------------------------------ scene

        private static bool BuildMainScene(bool interactive)
        {
            if (interactive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;

            CevirAssetUtility.EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // A camera keeps Unity from warning about "no cameras rendering"; the UI is a screen-space overlay.
            var cameraGo = new GameObject("Main Camera");
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;
            camera.orthographic = true;
            cameraGo.tag = "MainCamera";

            if (!CevirUIBuilder.Generate(false)) return false;
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) return false;

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            return true;
        }

        // ------------------------------------------------------------------ player settings

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Chevir";
            PlayerSettings.productName = "Chevir I/O";
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "0.1" || PlayerSettings.bundleVersion == "1.0")
            {
                PlayerSettings.bundleVersion = "0.1.0";
            }

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            // App icon from the logo (all platforms and sizes are scaled from this one 1024 px image).
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconPath);
            if (icon != null) PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icon });
            else Debug.LogWarning("[Chevir] App icon not found: " + AppIconPath);

            // Accounts, sync and the league talk to the server.
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.runInBackground = false;

            // Android
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            if (PlayerSettings.Android.bundleVersionCode < 1) PlayerSettings.Android.bundleVersionCode = 1;

            // iOS
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.iOS.cameraUsageDescription =
                "Chevir I/O işarələrini yoxlamaq üçün kameradan istifadə edir. Görüntü telefondan çıxmır.";
            PlayerSettings.iOS.targetOSVersionString = "13.0";
        }
    }
}
