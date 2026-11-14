using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cevir.Core.Audio
{
    public enum SfxId
    {
        NodeTap,
        NodeLocked,
        Correct,
        Incorrect,
        HeartLost,
        LessonComplete,
        PerfectLesson,
        StreakIncreased,
        XpGained,
        GatePassed,
        Countdown
    }

    [Serializable]
    public struct SfxEntry
    {
        public SfxId id;
        public AudioClip[] variants;
        [Range(0f, 1f)] public float volume;
        public bool haptic;
    }

    /// <summary>
    /// Sound effects and music with a small voice pool. Many learners are Deaf or hard of hearing, so every
    /// cue that matters is also optionally a haptic pulse, and the UI must never rely on sound alone.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        private const string SfxVolumeKey = "cevir.audio.sfx";
        private const string MusicVolumeKey = "cevir.audio.music";
        private const string HapticsKey = "cevir.audio.haptics";

        [SerializeField] private List<SfxEntry> effects = new List<SfxEntry>();
        [SerializeField] private AudioSource musicSource;
        [SerializeField, Range(1, 16)] private int voices = 6;

        private readonly Dictionary<SfxId, SfxEntry> _lookup = new Dictionary<SfxId, SfxEntry>();
        private AudioSource[] _pool;
        private int _next;

        public static AudioManager Instance { get; private set; }

        public float SfxVolume { get; private set; }
        public float MusicVolume { get; private set; }
        public bool HapticsEnabled { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            foreach (SfxEntry entry in effects) _lookup[entry.id] = entry;

            _pool = new AudioSource[voices];
            for (int i = 0; i < voices; i++)
            {
                _pool[i] = gameObject.AddComponent<AudioSource>();
                _pool[i].playOnAwake = false;
            }

            SfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
            MusicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 0.6f);
            HapticsEnabled = PlayerPrefs.GetInt(HapticsKey, 1) == 1;
            if (musicSource != null) musicSource.volume = MusicVolume;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Play(SfxId id)
        {
            SfxEntry entry;
            if (!_lookup.TryGetValue(id, out entry)) return;

            if (entry.variants != null && entry.variants.Length > 0 && SfxVolume > 0f)
            {
                AudioClip clip = entry.variants[UnityEngine.Random.Range(0, entry.variants.Length)];
                AudioSource source = _pool[_next];
                _next = (_next + 1) % _pool.Length;
                source.pitch = 1f + UnityEngine.Random.Range(-0.03f, 0.03f);
                source.PlayOneShot(clip, (entry.volume > 0f ? entry.volume : 1f) * SfxVolume);
            }

            if (entry.haptic && HapticsEnabled)
            {
#if UNITY_ANDROID || UNITY_IOS
                Handheld.Vibrate();
#endif
            }
        }

        public void PlayMusic(AudioClip clip, bool loop = true)
        {
            if (musicSource == null) return;
            if (musicSource.clip == clip && musicSource.isPlaying) return;
            musicSource.clip = clip;
            musicSource.loop = loop;
            musicSource.volume = MusicVolume;
            musicSource.Play();
        }

        public void StopMusic()
        {
            if (musicSource != null) musicSource.Stop();
        }

        public void SetSfxVolume(float value)
        {
            SfxVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SfxVolumeKey, SfxVolume);
        }

        public void SetMusicVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            if (musicSource != null) musicSource.volume = MusicVolume;
            PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
        }

        public void SetHaptics(bool enabled)
        {
            HapticsEnabled = enabled;
            PlayerPrefs.SetInt(HapticsKey, enabled ? 1 : 0);
        }
    }
}
