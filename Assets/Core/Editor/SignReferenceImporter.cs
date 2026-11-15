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

            /// <summary>Optional: the media file name ("salam", "herf_a") when the sign id is not known offline.</summary>
            public string mediaSlug;

            /// <summary>Optional: true when the recording uses both hands for most of the sign.</summary>
            public bool twoHanded;

            /// <summary>Optional: when set, each frame's face array holds only these landmark indices, in this order.</summary>
            public int[] faceIndices = new int[0];

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

        [MenuItem("Chevir/Import Sign References From Folder...")]
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
                Debug.LogError("[Chevir] Not a SignDictionary: " + dictionaryPath);
                return;
            }

            string packRoot = Path.GetDirectoryName(Path.GetDirectoryName(dictionaryPath)).Replace('\\', '/');
            int count = ImportFolder(jsonFolder, dictionary, packRoot + "/Signs/References");
            Debug.Log("[Chevir] Imported " + count + " sign references.");
        }

        /// <param name="slugOf">Optional: the pack's media naming rule, to resolve files that carry a mediaSlug instead of a sign id.</param>
        public static int ImportFolder(string jsonFolder, SignDictionary dictionary, string outputFolder, Func<SignDefinition, string> slugOf = null)
        {
            Dictionary<string, SignDefinition> bySlug = null;
            if (slugOf != null)
            {
                bySlug = new Dictionary<string, SignDefinition>(StringComparer.OrdinalIgnoreCase);
                foreach (SignDefinition s in dictionary.Signs)
                {
                    string slug = s != null ? slugOf(s) : null;
                    if (!string.IsNullOrEmpty(slug) && !bySlug.ContainsKey(slug)) bySlug[slug] = s;
                }
            }

            CevirAssetUtility.EnsureFolder(outputFolder);
            var extractor = new SignFeatureExtractor();
            var frame = new LandmarkFrame();
            int imported = 0;

            string[] files = Directory.GetFiles(jsonFolder, "*.json", SearchOption.AllDirectories);
            try
            {
                for (int i = 0; i < files.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("Chevir", Path.GetFileName(files[i]), i / (float)files.Length);
                    RawFile raw = JsonUtility.FromJson<RawFile>(File.ReadAllText(files[i]));
                    if (raw == null || raw.frames.Count < 2) continue;

                    SignDefinition sign = string.IsNullOrEmpty(raw.signId) ? null : dictionary.Find(raw.signId);
                    if (sign == null && bySlug != null && !string.IsNullOrEmpty(raw.mediaSlug)) bySlug.TryGetValue(raw.mediaSlug, out sign);
                    if (sign == null)
                    {
                        Debug.LogWarning("[Chevir] No sign '" + (raw.signId ?? raw.mediaSlug) + "' in dictionary; skipped " + files[i]);
                        continue;
                    }

                    raw.signId = sign.SignId;

                    extractor.DominantHand = raw.dominantHand == "Left" ? HandSide.Left : HandSide.Right;
                    var features = new List<SignFeatureFrame>(raw.frames.Count);
                    foreach (RawFrame rf in raw.frames)
                    {
                        frame.Clear();
                        frame.TimestampSeconds = rf.t;
                        frame.ImageAspect = raw.imageAspect;
                        Load(rf.left, frame.LeftHand.Points, out frame.LeftHand.IsPresent);
                        Load(rf.right, frame.RightHand.Points, out frame.RightHand.IsPresent);
                        if (raw.faceIndices != null && raw.faceIndices.Length > 0) LoadSparse(rf.face, raw.faceIndices, frame.Face.Points, out frame.Face.IsPresent);
                        else Load(rf.face, frame.Face.Points, out frame.Face.IsPresent);

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
                    if (raw.twoHanded && !sign.TwoHanded) sign.EditorSetTwoHanded(true);
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

        private static void LoadSparse(float[] flat, int[] indices, Vector3[] target, out bool present)
        {
            present = flat != null && flat.Length >= indices.Length * 3;
            if (!present) return;
            for (int i = 0; i < target.Length; i++) target[i] = Vector3.zero;
            for (int k = 0; k < indices.Length; k++)
            {
                int index = indices[k];
                if (index >= 0 && index < target.Length) target[index] = new Vector3(flat[k * 3], flat[k * 3 + 1], flat[k * 3 + 2]);
            }
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
