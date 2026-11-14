using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>Success burst: a check badge pops in with a short message while confetti falls.</summary>
    public sealed class CelebrationUI : MonoBehaviour
    {
        [SerializeField] private RectTransform badge;
        [SerializeField] private TextMeshProUGUI message;
        [SerializeField] private Sprite confettiSprite;
        [SerializeField] private int confettiCount = 36;
        [SerializeField] private float seconds = 1.5f;

        private static readonly Color[] Colors =
        {
            new Color32(0x00, 0x6E, 0xF0, 0xFF), new Color32(0x1E, 0xB9, 0xF0, 0xFF), new Color32(0xFF, 0xB8, 0x1F, 0xFF),
            new Color32(0x2B, 0xB6, 0x73, 0xFF), new Color32(0xF0, 0x44, 0x5A, 0xFF), new Color32(0xA3, 0x6B, 0xDB, 0xFF)
        };

        private readonly List<RectTransform> _pieces = new List<RectTransform>();
        private readonly List<Vector2> _velocity = new List<Vector2>();

        public bool IsPlaying { get; private set; }

        public void Play(string text)
        {
            gameObject.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(Run(text));
        }

        private IEnumerator Run(string text)
        {
            IsPlaying = true;
            if (message != null) message.text = text;
            Spawn();

            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = t / seconds;
                if (badge != null)
                {
                    float pop = k < 0.25f ? Mathf.Lerp(0.2f, 1.15f, k / 0.25f) : Mathf.Lerp(1.15f, 1f, Mathf.Min(1f, (k - 0.25f) / 0.15f));
                    badge.localScale = Vector3.one * pop;
                }

                float dt = Time.unscaledDeltaTime;
                for (int i = 0; i < _pieces.Count; i++)
                {
                    _velocity[i] += new Vector2(0f, -1600f * dt);
                    _pieces[i].anchoredPosition += _velocity[i] * dt;
                    _pieces[i].localEulerAngles += new Vector3(0f, 0f, 360f * dt * (i % 2 == 0 ? 1f : -1f));
                }

                yield return null;
            }

            IsPlaying = false;
            gameObject.SetActive(false);
        }

        private void Spawn()
        {
            while (_pieces.Count < confettiCount)
            {
                var go = new GameObject("Confetti", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                var image = go.GetComponent<Image>();
                image.sprite = confettiSprite;
                image.raycastTarget = false;
                image.color = Colors[_pieces.Count % Colors.Length];
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(18f, 30f);
                _pieces.Add(rt);
                _velocity.Add(Vector2.zero);
            }

            for (int i = 0; i < _pieces.Count; i++)
            {
                _pieces[i].anchoredPosition = new Vector2(Random.Range(-60f, 60f), Random.Range(-20f, 40f));
                float angle = Random.Range(25f, 155f) * Mathf.Deg2Rad;
                float speed = Random.Range(900f, 1500f);
                _velocity[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            }
        }
    }
}
