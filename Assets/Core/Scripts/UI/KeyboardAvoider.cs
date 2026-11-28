using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Cevir.Core.UI
{
    /// <summary>
    /// Keeps the text field being typed in visible above the phone keyboard: while a field is focused and the keyboard is
    /// open, the screen holding the field slides up just enough that the field sits above the keyboard, and slides back
    /// when typing ends. It also turns off the extra text strip Android/iOS show above the keyboard (the "mobile input"
    /// preview), so text appears only in the field itself. Installs itself when the app starts; nothing to set up.
    /// </summary>
    public sealed class KeyboardAvoider : MonoBehaviour
    {
        /// <summary>Gap between the field's bottom edge and the keyboard, in screen points of a 1080-wide layout.</summary>
        private const float Margin = 40f;
        private const float SlideSpeed = 14f;
        private const float RescanInterval = 0.5f;
        private const float MeasureInterval = 0.1f;

        private static KeyboardAvoider _instance;

        private readonly List<TMP_InputField> _fields = new List<TMP_InputField>();
        private RectTransform _moved;
        private Vector2 _movedHome;
        private float _offset;      // current upward shift, in the moved panel's units
        private float _nextScan;
        private float _nextMeasure;
        private float _keyboardPixels;
        private bool _nativeMeasureFailed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;
            var go = new GameObject("KeyboardAvoider");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<KeyboardAvoider>();
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + RescanInterval;
                HideMobileInputEverywhere();
            }

            TMP_InputField field = FocusedField();
            float target = 0f;
            RectTransform panel = null;

            if (field != null)
            {
                if (!field.shouldHideMobileInput) field.shouldHideMobileInput = true;
                panel = PanelOf(field);
                float keyboard = KeyboardHeightPixels();
                if (panel != null && keyboard > 0f) target = NeededShift(field, panel, keyboard, panel == _moved ? _offset : 0f);
            }

            // Switching to a field on another panel: put the old panel back first.
            if (panel != null && _moved != null && panel != _moved) Restore();
            if (panel != null && _moved == null && target > 0f)
            {
                _moved = panel;
                _movedHome = panel.anchoredPosition;
                _offset = 0f;
            }

            if (_moved == null) return;

            _offset = Mathf.Lerp(_offset, target, 1f - Mathf.Exp(-SlideSpeed * Time.unscaledDeltaTime));
            if (target <= 0f && _offset < 0.5f)
            {
                Restore();
                return;
            }

            _moved.anchoredPosition = _movedHome + new Vector2(0f, _offset);
        }

        private void OnDisable()
        {
            Restore();
        }

        private void Restore()
        {
            if (_moved != null) _moved.anchoredPosition = _movedHome;
            _moved = null;
            _offset = 0f;
        }

        /// <summary>Turns off the keyboard's own text strip on every input field, including ones built at runtime.</summary>
        private void HideMobileInputEverywhere()
        {
            _fields.Clear();
            _fields.AddRange(FindObjectsByType<TMP_InputField>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            for (int i = 0; i < _fields.Count; i++)
            {
                if (_fields[i] != null && !_fields[i].shouldHideMobileInput) _fields[i].shouldHideMobileInput = true;
            }
        }

        private static TMP_InputField FocusedField()
        {
            EventSystem events = EventSystem.current;
            GameObject selected = events != null ? events.currentSelectedGameObject : null;
            if (selected == null) return null;
            TMP_InputField field = selected.GetComponent<TMP_InputField>();
            return field != null && field.isFocused ? field : null;
        }

        /// <summary>The top-level panel (direct child of the root canvas) that holds the field; that is what slides.</summary>
        private static RectTransform PanelOf(TMP_InputField field)
        {
            Canvas canvas = field.GetComponentInParent<Canvas>();
            if (canvas == null) return null;
            Transform root = canvas.rootCanvas.transform;
            Transform t = field.transform;
            while (t.parent != null && t.parent != root) t = t.parent;
            return t.parent == root ? t as RectTransform : null;
        }

        /// <summary>How far (in the panel's units) the panel must rise so the field's bottom clears the keyboard.</summary>
        private static float NeededShift(TMP_InputField field, RectTransform panel, float keyboardPixels, float currentShift)
        {
            Canvas canvas = panel.GetComponentInParent<Canvas>().rootCanvas;
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            float scale = Mathf.Max(0.01f, canvas.scaleFactor);

            var corners = new Vector3[4];
            ((RectTransform)field.transform).GetWorldCorners(corners);
            // Where the field would be with the panel in its normal place (the current shift taken out).
            float bottom = RectTransformUtility.WorldToScreenPoint(cam, corners[0]).y - currentShift * scale;
            float top = RectTransformUtility.WorldToScreenPoint(cam, corners[1]).y - currentShift * scale;

            float margin = Margin * Screen.width / 1080f;
            float need = keyboardPixels + margin - bottom;
            if (need <= 0f) return 0f;

            // Never push the field's top above the screen (or the notch).
            float roomAbove = Screen.safeArea.yMax - margin - top;
            need = Mathf.Min(need, Mathf.Max(0f, roomAbove));
            return need / scale;
        }

        /// <summary>Height of the on-screen keyboard in screen pixels, 0 when it is closed.</summary>
        private float KeyboardHeightPixels()
        {
            if (!TouchScreenKeyboard.isSupported) return 0f;
            if (Time.unscaledTime < _nextMeasure) return _keyboardPixels;
            _nextMeasure = Time.unscaledTime + MeasureInterval;

            float height = TouchScreenKeyboard.area.height;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (height <= 0f && !_nativeMeasureFailed)
            {
                float native;
                if (TryAndroidKeyboardHeight(out native)) height = native;
                else _nativeMeasureFailed = true;
            }
#endif
            // Last resort when the system will not say: a typical phone keyboard is about 40% of the screen.
            if (height <= 0f && _nativeMeasureFailed && TouchScreenKeyboard.visible) height = Screen.height * 0.4f;

            _keyboardPixels = height;
            return height;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>The window's height minus the part the keyboard leaves visible, scaled to Unity's screen pixels.</summary>
        private static bool TryAndroidKeyboardHeight(out float height)
        {
            height = 0f;
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject window = activity.Call<AndroidJavaObject>("getWindow"))
                using (AndroidJavaObject view = window.Call<AndroidJavaObject>("getDecorView"))
                using (var rect = new AndroidJavaObject("android.graphics.Rect"))
                {
                    view.Call("getWindowVisibleDisplayFrame", rect);
                    int viewHeight = view.Call<int>("getHeight");
                    int visibleBottom = rect.Get<int>("bottom");
                    if (viewHeight <= 0) return false;
                    float covered = Mathf.Max(0, viewHeight - visibleBottom);
                    // Small gaps (navigation bar) are not a keyboard.
                    if (covered < viewHeight * 0.12f) covered = 0f;
                    height = covered * Screen.height / viewHeight;
                    return true;
                }
            }
            catch (System.Exception)
            {
                return false;
            }
        }
#endif
    }
}
