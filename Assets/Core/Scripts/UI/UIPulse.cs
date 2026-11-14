using UnityEngine;

namespace Cevir.Core.UI
{
    /// <summary>Gentle looping scale / alpha pulse for things that invite a tap (active unit glow, respond button).</summary>
    public sealed class UIPulse : MonoBehaviour
    {
        [SerializeField] private float scaleAmount = 0.06f;
        [SerializeField] private float alphaAmount = 0f;
        [SerializeField] private float period = 1.6f;

        private CanvasGroup _group;
        private Vector3 _baseScale = Vector3.one;

        private void Awake()
        {
            _baseScale = transform.localScale;
            if (alphaAmount > 0f)
            {
                _group = GetComponent<CanvasGroup>();
                if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            }
        }

        public void Configure(float scale, float alpha, float seconds)
        {
            scaleAmount = scale;
            alphaAmount = alpha;
            period = seconds;
        }

        private void OnDisable()
        {
            transform.localScale = _baseScale;
            if (_group != null) _group.alpha = 1f;
        }

        private void Update()
        {
            float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / Mathf.Max(0.1f, period));
            transform.localScale = _baseScale * (1f + scaleAmount * k);
            if (_group != null) _group.alpha = 1f - alphaAmount * (1f - k);
        }
    }
}
