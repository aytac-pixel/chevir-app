using System.Collections;
using TMPro;
using UnityEngine;

namespace Cevir.Core.UI
{
    /// <summary>Short message pill above the tab bar ("locked", "link copied").</summary>
    public sealed class ToastUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private float seconds = 2.2f;

        private Coroutine _routine;

        private void Awake()
        {
            if (group != null)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
            }
        }

        public void Show(string message)
        {
            if (group == null || !isActiveAndEnabled) return;
            if (label != null) label.text = message;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            const float fade = 0.15f;
            for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
            {
                group.alpha = t / fade;
                yield return null;
            }

            group.alpha = 1f;
            yield return new WaitForSecondsRealtime(seconds);
            for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
            {
                group.alpha = 1f - t / fade;
                yield return null;
            }

            group.alpha = 0f;
            _routine = null;
        }
    }
}
