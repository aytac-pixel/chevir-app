using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>
    /// Full-screen pixel-art sky behind the Learn page that follows the learner's day: a sunrise in the morning, a
    /// bright sky with clouds at midday, a sunset in the evening and the moon and stars at night, over the Baku
    /// skyline and the Caspian. When it rains or snows where the learner is, the sky turns grey (or snowy) and drops
    /// or flakes fall across the screen.
    ///
    /// Time comes from the phone's clock. Weather needs no permission: the rough city is looked up from the internet
    /// address (get.geojs.io) and its current weather from Open-Meteo (free, no key). Without internet, or if either
    /// service fails, the sky simply follows the clock.
    /// </summary>
    public sealed class HomeSkyUI : MonoBehaviour
    {
        public enum Mood { Morning, Noon, Evening, Night, Rain, RainNight, Snow, SnowNight }
        private enum Weather { Clear, Rain, Snow }

        private const string GeoUrl = "https://get.geojs.io/v1/ip/geo.json";
        private const string WeatherUrl = "https://api.open-meteo.com/v1/forecast?latitude={0}&longitude={1}&current=weather_code";
        private const double WeatherFreshMinutes = 45;
        private const double WeatherKeepHours = 6;
        private const double PlaceFreshHours = 24;
        // Baku: used when the city cannot be found (the app's home and most learners).
        private const float DefaultLat = 40.41f, DefaultLon = 49.87f;

        private const string KeyWeather = "chevir.sky.weather";
        private const string KeyWeatherAt = "chevir.sky.weather_at";
        private const string KeyLat = "chevir.sky.lat";
        private const string KeyLon = "chevir.sky.lon";
        private const string KeyPlaceAt = "chevir.sky.place_at";

        public event Action<Mood> MoodChanged;
        public Mood Current { get; private set; }
        public bool IsNight => Current == Mood.Night || Current == Mood.RainNight || Current == Mood.SnowNight;
        public bool HasWeather => Current >= Mood.Rain;

        private Sprite[] _skies;
        private Image _image;
        private AspectRatioFitter _fitter;
        private RectTransform _particles;
        private readonly List<RectTransform> _drops = new List<RectTransform>();
        private readonly List<Vector3> _dropState = new List<Vector3>(); // x, y, speed factor
        private bool _ready;
        private bool _fetching;
        private float _nextCheck;

        /// <summary>Puts the sky as the very back layer of <paramref name="screen"/>.</summary>
        public static HomeSkyUI Create(RectTransform screen, CevirTheme theme)
        {
            var go = new GameObject("Sky", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(screen, false);
            rt.SetAsFirstSibling();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.gameObject.AddComponent<RectMask2D>();

            var picture = new GameObject("Picture", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            picture.transform.SetParent(rt, false);
            var sky = go.AddComponent<HomeSkyUI>();
            sky._image = picture.GetComponent<Image>();
            sky._image.raycastTarget = false;
            sky._image.preserveAspect = false;
            sky._fitter = picture.GetComponent<AspectRatioFitter>();
            sky._fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            sky._fitter.aspectRatio = 216f / 384f;
            // Pivot at the bottom: on tall phones the extra height is cut from the sky, the skyline stays in view.
            ((RectTransform)picture.transform).pivot = new Vector2(0.5f, 0f);

            var particles = new GameObject("Particles", typeof(RectTransform));
            particles.transform.SetParent(rt, false);
            sky._particles = (RectTransform)particles.transform;
            sky._particles.anchorMin = Vector2.zero;
            sky._particles.anchorMax = Vector2.one;
            sky._particles.offsetMin = sky._particles.offsetMax = Vector2.zero;

            sky._skies = theme != null ? theme.homeSkies : null;
            sky._ready = true;
            sky.Apply(true);
            return sky;
        }

        private void OnEnable()
        {
            _nextCheck = 0f;
        }

        private void Update()
        {
            if (!_ready) return;
            if (Time.unscaledTime >= _nextCheck)
            {
                _nextCheck = Time.unscaledTime + 60f;
                Apply(false);
                if (!_fetching && !WeatherIsFresh()) StartCoroutine(FetchWeather());
            }

            AnimateParticles();
        }

        // ------------------------------------------------------------------ mood

        private void Apply(bool force)
        {
            Mood mood = Pick(DateTime.Now.Hour, StoredWeather());
            if (!force && mood == Current) return;
            Current = mood;

            Sprite sprite = _skies != null && (int)mood < _skies.Length ? _skies[(int)mood] : null;
            _image.sprite = sprite;
            _image.enabled = sprite != null;
            _image.color = Color.white;
            BuildParticles(mood);
            if (MoodChanged != null) MoodChanged(mood);
        }

        private static Mood Pick(int hour, Weather weather)
        {
            bool night = hour >= 20 || hour < 5;
            if (weather == Weather.Rain) return night ? Mood.RainNight : Mood.Rain;
            if (weather == Weather.Snow) return night ? Mood.SnowNight : Mood.Snow;
            if (night) return Mood.Night;
            if (hour < 10) return Mood.Morning;
            if (hour < 17) return Mood.Noon;
            return Mood.Evening;
        }

        // ------------------------------------------------------------------ rain and snow

        private void BuildParticles(Mood mood)
        {
            bool rain = mood == Mood.Rain || mood == Mood.RainNight;
            bool snow = mood == Mood.Snow || mood == Mood.SnowNight;
            int count = rain ? 70 : snow ? 55 : 0;

            while (_drops.Count < count)
            {
                var go = new GameObject("Drop", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_particles, false);
                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                _drops.Add(rt);
                _dropState.Add(Vector3.zero);
            }

            var rnd = new System.Random(17);
            for (int i = 0; i < _drops.Count; i++)
            {
                bool on = i < count;
                _drops[i].gameObject.SetActive(on);
                if (!on) continue;
                var img = _drops[i].GetComponent<Image>();
                if (rain)
                {
                    img.color = mood == Mood.RainNight ? new Color(0.62f, 0.72f, 0.9f, 0.55f) : new Color(0.9f, 0.95f, 1f, 0.75f);
                    _drops[i].sizeDelta = new Vector2(5f, 30f + (float)rnd.NextDouble() * 18f);
                }
                else
                {
                    float s = rnd.NextDouble() < 0.3 ? 15f : 10f; // square pixel flakes
                    img.color = new Color(1f, 1f, 1f, 0.9f);
                    _drops[i].sizeDelta = new Vector2(s, s);
                }

                _dropState[i] = new Vector3((float)rnd.NextDouble(), (float)rnd.NextDouble(), 0.7f + (float)rnd.NextDouble() * 0.6f);
            }
        }

        private void AnimateParticles()
        {
            bool rain = Current == Mood.Rain || Current == Mood.RainNight;
            bool snow = Current == Mood.Snow || Current == Mood.SnowNight;
            if (!rain && !snow) return;

            Rect area = _particles.rect;
            if (area.width <= 1f || area.height <= 1f) return;
            float dt = Time.unscaledDeltaTime;
            float t = Time.unscaledTime;
            for (int i = 0; i < _drops.Count; i++)
            {
                if (!_drops[i].gameObject.activeSelf) continue;
                Vector3 s = _dropState[i];
                // Fractions of the screen per second: rain is fast and slanted, snow drifts and sways.
                float fall = rain ? 1.1f * s.z : 0.09f * s.z;
                float side = rain ? -0.12f * s.z : 0f;
                s.y -= fall * dt;
                s.x += side * dt;
                if (s.y < -0.05f) { s.y += 1.1f; s.x = Mathf.Repeat(s.x + 0.37f, 1f); }
                s.x = Mathf.Repeat(s.x, 1f);
                _dropState[i] = s;

                float sway = snow ? Mathf.Sin(t * 1.3f + i) * 18f : 0f;
                // Snap to a 5-unit grid so the pixels stay pixel-sized steps.
                float x = Mathf.Round((s.x * area.width + sway) / 5f) * 5f;
                float y = Mathf.Round((s.y * area.height) / 5f) * 5f;
                _drops[i].anchoredPosition = new Vector2(x, y);
                _drops[i].localRotation = rain ? Quaternion.Euler(0f, 0f, -6f) : Quaternion.identity;
            }
        }

        // ------------------------------------------------------------------ weather

        private static Weather StoredWeather()
        {
            double age = AgeHours(KeyWeatherAt);
            if (age > WeatherKeepHours) return Weather.Clear;
            return (Weather)Mathf.Clamp(PlayerPrefs.GetInt(KeyWeather, 0), 0, 2);
        }

        private static bool WeatherIsFresh()
        {
            return AgeHours(KeyWeatherAt) * 60.0 < WeatherFreshMinutes;
        }

        private static double AgeHours(string key)
        {
            long ticks;
            string stored = PlayerPrefs.GetString(key, string.Empty);
            if (!long.TryParse(stored, NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks)) return double.MaxValue;
            return (DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc)).TotalHours;
        }

        private static void Stamp(string key)
        {
            PlayerPrefs.SetString(key, DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
        }

        [Serializable] private sealed class GeoReply { public string latitude; public string longitude; }
        [Serializable] private sealed class MeteoCurrent { public int weather_code = -1; }
        [Serializable] private sealed class MeteoReply { public MeteoCurrent current; }

        private IEnumerator FetchWeather()
        {
            _fetching = true;
            try
            {
                if (Application.internetReachability == NetworkReachability.NotReachable) yield break;

                float lat = PlayerPrefs.GetFloat(KeyLat, DefaultLat);
                float lon = PlayerPrefs.GetFloat(KeyLon, DefaultLon);
                if (AgeHours(KeyPlaceAt) > PlaceFreshHours)
                {
                    using (UnityWebRequest geo = UnityWebRequest.Get(GeoUrl))
                    {
                        geo.timeout = 8;
                        yield return geo.SendWebRequest();
                        if (geo.result == UnityWebRequest.Result.Success)
                        {
                            GeoReply place = null;
                            try { place = JsonUtility.FromJson<GeoReply>(geo.downloadHandler.text); }
                            catch (Exception) { place = null; }
                            float a, b;
                            if (place != null &&
                                float.TryParse(place.latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out a) &&
                                float.TryParse(place.longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out b))
                            {
                                lat = a;
                                lon = b;
                                PlayerPrefs.SetFloat(KeyLat, lat);
                                PlayerPrefs.SetFloat(KeyLon, lon);
                                Stamp(KeyPlaceAt);
                            }
                        }
                    }
                }

                string url = string.Format(CultureInfo.InvariantCulture, WeatherUrl, lat.ToString("0.##", CultureInfo.InvariantCulture),
                    lon.ToString("0.##", CultureInfo.InvariantCulture));
                using (UnityWebRequest meteo = UnityWebRequest.Get(url))
                {
                    meteo.timeout = 8;
                    yield return meteo.SendWebRequest();
                    if (meteo.result != UnityWebRequest.Result.Success) yield break;
                    MeteoReply reply = null;
                    try { reply = JsonUtility.FromJson<MeteoReply>(meteo.downloadHandler.text); }
                    catch (Exception) { reply = null; }
                    if (reply == null || reply.current == null || reply.current.weather_code < 0) yield break;

                    PlayerPrefs.SetInt(KeyWeather, (int)Classify(reply.current.weather_code));
                    Stamp(KeyWeatherAt);
                    PlayerPrefs.Save();
                }

                Apply(false);
            }
            finally
            {
                _fetching = false;
            }
        }

        /// <summary>WMO weather codes: drizzle, rain, showers and storms are rain; snowfall and snow showers are snow.</summary>
        private static Weather Classify(int code)
        {
            if ((code >= 71 && code <= 77) || code == 85 || code == 86) return Weather.Snow;
            if ((code >= 51 && code <= 67) || (code >= 80 && code <= 82) || code >= 95) return Weather.Rain;
            return Weather.Clear;
        }
    }
}
