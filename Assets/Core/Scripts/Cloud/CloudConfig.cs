using UnityEngine;

namespace Cevir.Core.Cloud
{
    /// <summary>
    /// Server settings (Supabase project URL and its public "anon" key). Lives at Assets/Core/Resources/CevirCloudConfig.asset
    /// so the app finds it at runtime. The anon key is public by design: the database rules (row level security in
    /// Tools/supabase/schema.sql) decide what each signed-in user may read and write.
    /// </summary>
    [CreateAssetMenu(menuName = "Chevir/Cloud Config", fileName = "CevirCloudConfig")]
    public sealed class CloudConfig : ScriptableObject
    {
        public const string ResourceName = "CevirCloudConfig";

        [Tooltip("Supabase project URL, e.g. https://abcdefgh.supabase.co")]
        public string supabaseUrl = "";

        [Tooltip("Supabase anon (public) key: Project Settings > API.")]
        [TextArea(2, 5)] public string anonKey = "";

        [Tooltip("Seconds to wait after a change before uploading progress (several quick changes become one upload).")]
        [Min(0.5f)] public float uploadDelaySeconds = 3f;

        public bool IsConfigured => !string.IsNullOrEmpty(supabaseUrl) && !string.IsNullOrEmpty(anonKey) && supabaseUrl.StartsWith("https://");

        public string BaseUrl => supabaseUrl.TrimEnd('/');

        /// <summary>Plain-text settings committed with the project: Assets/Core/Resources/cevir_cloud.json.</summary>
        public const string JsonResourceName = "cevir_cloud";

        [System.Serializable]
        private sealed class JsonSettings
        {
            public string supabaseUrl;
            public string anonKey;
        }

        public static CloudConfig Load()
        {
            CloudConfig config = Resources.Load<CloudConfig>(ResourceName);
            if (config != null && config.IsConfigured) return config;

            var json = Resources.Load<TextAsset>(JsonResourceName);
            if (json == null) return config;
            JsonSettings settings = null;
            try
            {
                settings = JsonUtility.FromJson<JsonSettings>(json.text);
            }
            catch (System.Exception)
            {
                Debug.LogWarning("[Chevir] " + JsonResourceName + ".json could not be read.");
            }

            if (settings == null || string.IsNullOrEmpty(settings.supabaseUrl)) return config;
            CloudConfig fromJson = CreateInstance<CloudConfig>();
            fromJson.supabaseUrl = settings.supabaseUrl.Trim();
            fromJson.anonKey = (settings.anonKey ?? string.Empty).Trim();
            return fromJson;
        }
    }
}
