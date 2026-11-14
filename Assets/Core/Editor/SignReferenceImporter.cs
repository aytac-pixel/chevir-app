using System;
using System.Collections.Generic;
using System.IO;
using Cevir.Core.LanguagePacks;
using Cevir.Core.Recognition;
using UnityEditor;
using UnityEngine;

namespace Cevir.Core.Editor
{
    /// <summary>
    /// Imports raw landmark JSON produced offline (Tools/landmark_extraction/extract_landmarks.py) and
    /// converts it with the same <see cref="SignFeatureExtractor"/> the app uses live, so reference and
    /// learner features can never drift apart.
    /// </summary>
    public static class SignReferenceImporter
    {
        [Serializable]
        private sealed class RawFile
        {
            public string signId;
            public string source;
            public string signerId;
            public float imageAspect = 1f;
            public string dominantHand = "Right";
            public List<RawFrame> frames = new List<RawFrame>();
        }

        [Serializable]
        private sealed class RawFrame
        {
            public double t;

            /// <summary>21 × (x, y, z) normalized image coordinates, or empty when not detected.</summary>
            public float[] left = new float[0];
            public float[] right = new float[0];

            /// <summary>478 × (x, y, z), or empty.</summary>
            public float[] face = new float[0];
        }

        [MenuItem("Çevir/Import Sign References From Folder...")]
        private static void ImportMenu()
        {
            string jsonFolder = EditorUtility.OpenFolderPanel("Folder with landmark JSON files", "", "");
            if (string.IsNullOrEmpty(jsonFolder)) return;

            string dictionaryPath = EditorUtility.OpenFilePanel("Pack SignDictionary asset", Application.dataPath, "asset");
            if (string.IsNullOrEmpty(dictionaryPath)) return;
            dictionaryPath = "Assets" + dictionaryPath.Substring(Application.dataPath.Length);
            var dictionary = AssetDatabase.LoadAssetAtPath<SignDictionary>(dictionaryPath);
            if (dictionary == null)
            {
                Debug.LogError("[Çevir] Not a SignDictionary: " + dictionaryPath);
                return;
            }

            string packRoot = Path.GetDirectoryName(Path.GetDirectoryName(dictionaryPath)).Replace('\\', '/');
            int count = ImportFolder(jsonFolder, dictionary, packRoot + "/Signs/References");
            Debug.Log("[Çevir] Imported " + count + " sign references.");
        }

        public static int ImportFolder(string jsonFolder, SignDictionary dictionary, string outputFolder)
        {
            CevirAssetUtility.EnsureFolder(outputFolder);
            var extractor = new SignFeatureExtractor();
            var frame = new LandmarkFrame();
            int imported = 0;

            string[] files = Directory.GetFiles(jsonFolder, "*.json", SearchOption.AllDirectories);
            try
            {
                for (int i = 0; i < files.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("Çevir", Path.GetFileName(files[i]), i / (float)files.Length);
                    RawFile raw = JsonUtility.FromJson<RawFile>(File.ReadAllText(files[i]));
                    if (raw == null || string.IsNullOrEmpty(raw.signId) || raw.frames.Count < 2) continue;

                    SignDefinition sign = dictionary.Find(raw.signId);
                    if (sign == null)
                    {
                        Debug.LogWarning("[Çevir] No sign '" + raw.signId + "' in dictionary; skipped " + files[i]);
                        continue;
                    }

                    extractor.DominantHand = raw.dominantHand == "Left" ? HandSide.Left : HandSide.Right;
                    var features = new List<SignFeatureFrame>(raw.frames.Count);
                    foreach (RawFrame rf in raw.frames)
                    {
                        frame.Clear();
                        frame.TimestampSeconds = rf.t;
                        frame.ImageAspect = raw.imageAspect;
                        Load(rf.left, frame.LeftHand.Points, out frame.LeftHand.IsPresent);
                        Load(rf.right, frame.RightHand.Points, out frame.RightHand.IsPresent);
                        Load(rf.face, frame.Face.Points, out frame.Face.IsPresent);

                        var f = new SignFeatureFrame();
                        extractor.Extract(frame, f);
                        features.Add(f);
                    }

                    string name = CevirAssetUtility.SafeFileName(raw.signId + "_" + (raw.signerId ?? "x") + "_" + Path.GetFileNameWithoutExtension(files[i]));
                    string path = outputFolder + "/" + name + ".asset";
                    var reference = CevirAssetUtility.LoadOrCreate<SignReference>(path);
                    reference.Initialize(raw.signId, raw.source, raw.signerId, features);
                    EditorUtility.SetDirty(reference);

                    sign.EditorAddReference(reference);
                    EditorUtility.SetDirty(sign);
                    imported++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
            }

            return imported;
        }

        private static void Load(float[] flat, Vector3[] target, out bool present)
        {
            present = flat != null && flat.Length >= target.Length * 3;
            if (!present) return;
            for (int i = 0; i < target.Length; i++)
            {
                target[i] = new Vector3(flat[i * 3], flat[i * 3 + 1], flat[i * 3 + 2]);
            }
        }
    }
}
