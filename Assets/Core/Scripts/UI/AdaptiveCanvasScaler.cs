using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>
    /// Fits the 1080-wide layout to any phone: on tall phones (16:9 and taller) the width decides the scale, so the
    /// layout fills the width exactly; on short or wide screens (tablets, foldables, landscape-ish windows) it blends
    /// towards the height, so nothing is cut off at the bottom. Also lays grids out to the real width.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    [ExecuteAlways]
    public sealed class AdaptiveCanvasScaler : MonoBehaviour
    {
        [Tooltip("Height / width at and above which only the width counts (1920 / 1080).")]
        [SerializeField] private float tallAspect = 16f / 9f;

        [Tooltip("Height / width at and below which only the height counts (tablets).")]
        [SerializeField] private float shortAspect = 4f / 3f;

        private CanvasScaler _scaler;
        private Vector2Int _screen;

        private void OnEnable()
        {
            _scaler = GetComponent<CanvasScaler>();
            Apply();
        }

        private void Update()
        {
            if (_screen.x != Screen.width || _screen.y != Screen.height) Apply();
        }

        private void Apply()
        {
            _screen = new Vector2Int(Screen.width, Screen.height);
            if (_scaler == null || Screen.width <= 0 || Screen.height <= 0) return;
            float aspect = Screen.height / (float)Screen.width;
            _scaler.matchWidthOrHeight = Mathf.Clamp01(Mathf.InverseLerp(tallAspect, shortAspect, aspect));
        }
    }
}
