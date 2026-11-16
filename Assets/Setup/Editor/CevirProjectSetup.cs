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
        private const string MainFontSource = "Assets/Core/Fonts/VT323-Regular.ttf";
        private const string MainFontAssetPath = "Assets/Core/Fonts/VT323 SDF.asset";

        /// <summary>
        /// VT323 draws smaller than the old Nunito at the same point size (cap height 0.56 vs 0.705 em), so the font
        /// asset is scaled up once and every label keeps its designed size.
        /// </summary>
        private const float MainFontScale = 1.25f;

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
        /// VT323 (the pixel typeface of the design; covers every Azerbaijani letter, Ə/ə included) with DejaVu Sans as
        /// the fallback for anything VT323 lacks (arrows, symbols). VT323 has one weight, so bold text is TMP's faux
        /// bold. All atlases are dynamic.
        /// </summary>
        private static void EnsureFontAsset()
        {
            TMP_FontAsset fallback = CreateFontAsset(FontSource, FontAssetPath, "DejaVuSans SDF");
            TMP_FontAsset main = CreateFontAsset(MainFontSource, MainFontAssetPath, "VT323 SDF");
            if (main == null) return;

            var so = new SerializedObject(main);
            SerializedProperty scale = so.FindProperty("m_FaceInfo.m_Scale");
            if (scale != null && !Mathf.Approximately(scale.floatValue, MainFontScale))
            {
                scale.floatValue = MainFontScale;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (fallback != null)
            {
                if (main.fallbackFontAssetTable == null) main.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
                if (!main.fallbackFontAssetTable.Contains(fallback)) main.fallbackFontAssetTable.Add(fallback);
            }

            EditorUtility.SetDirty(main);
            AssetDatabase.SaveAssets();
        }

        private static TMP_FontAsset CreateFontAsset(string sourcePath, string assetPath, string assetName)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (font == null)
            {
                Debug.LogWarning("[Chevir] " + sourcePath + " not found; text falls back to the next font.");
                return null;
            }

            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
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
            PlayerSettings.productName = "Chevir";
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
                "Chevir işarələrini yoxlamaq üçün kameradan istifadə edir. Görüntü telefondan çıxmır.";
            PlayerSettings.iOS.targetOSVersionString = "13.0";
        }
    }
}
