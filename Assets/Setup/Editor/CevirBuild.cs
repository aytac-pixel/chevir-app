using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Cevir.Setup.Editor
{
    /// <summary>Menu and command-line builds. CI calls <see cref="CiBuildAndroid"/> on a fresh checkout.</summary>
    public static class CevirBuild
    {
        public const string AndroidApkPath = "Builds/Android/Cevir.apk";
        public const string AndroidAabPath = "Builds/Android/Cevir.aab";
        public const string IosPath = "Builds/iOS";

        [MenuItem("Chevir/Build/Android APK (install on phone)", priority = 10)]
        private static void AndroidApkMenu()
        {
            if (BuildAndroid(false)) EditorUtility.RevealInFinder(AndroidApkPath);
        }

        [MenuItem("Chevir/Build/Android App Bundle (Google Play)", priority = 11)]
        private static void AndroidAabMenu()
        {
            if (BuildAndroid(true)) EditorUtility.RevealInFinder(AndroidAabPath);
        }

        [MenuItem("Chevir/Build/iOS Xcode Project", priority = 12)]
        private static void IosMenu()
        {
            if (Build(BuildTarget.iOS, IosPath)) EditorUtility.RevealInFinder(IosPath);
        }

        public static bool BuildAndroid(bool appBundle)
        {
            EditorUserBuildSettings.buildAppBundle = appBundle;
            return Build(BuildTarget.Android, appBundle ? AndroidAabPath : AndroidApkPath);
        }

        /// <summary>Entry point for batch mode: -executeMethod Cevir.Setup.Editor.CevirBuild.CiBuildAndroid</summary>
        public static void CiBuildAndroid()
        {
            bool ok = CevirProjectSetup.Run(false);
            if (ok) ApplyCiBuildNumber();
            ok = ok && BuildAndroid(false);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>CI passes its run number so every APK gets a higher version code (0.1.N) than the one before.</summary>
        private static void ApplyCiBuildNumber()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int index = System.Array.IndexOf(args, "-cevirBuildNumber");
            if (index < 0 || index + 1 >= args.Length) return;
            if (!int.TryParse(args[index + 1], out int number) || number < 1) return;

            PlayerSettings.bundleVersion = "0.1." + number;
            PlayerSettings.Android.bundleVersionCode = number;
            Debug.Log("[Chevir] Version " + PlayerSettings.bundleVersion + " (code " + number + ").");
        }

        public static void CiBuildIos()
        {
            bool ok = CevirProjectSetup.Run(false) && Build(BuildTarget.iOS, IosPath);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool Build(BuildTarget target, string path)
        {
            if (EditorBuildSettings.scenes.Length == 0 && !CevirProjectSetup.Run(!Application.isBatchMode))
            {
                return false;
            }

            string directory = target == BuildTarget.iOS ? path : Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { CevirProjectSetup.ScenePath },
                locationPathName = path,
                target = target,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log("[Chevir] Build succeeded: " + path + " (" + (summary.totalSize / (1024 * 1024)) + " MB)");
                return true;
            }

            Debug.LogError("[Chevir] Build " + summary.result + " with " + summary.totalErrors + " error(s). See the Console.");
            return false;
        }
    }
}
