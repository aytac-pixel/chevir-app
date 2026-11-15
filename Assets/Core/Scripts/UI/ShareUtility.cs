using UnityEngine;

namespace Cevir.Core.UI
{
    /// <summary>Native share sheet on Android; elsewhere the text is copied to the clipboard.</summary>
    public static class ShareUtility
    {
        /// <summary>Returns true if a share sheet opened, false if the text was copied instead.</summary>
        public static bool ShareText(string text, string chooserTitle)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var intentClass = new AndroidJavaClass("android.content.Intent"))
                using (var intent = new AndroidJavaObject("android.content.Intent"))
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
                    intent.Call<AndroidJavaObject>("setType", "text/plain");
                    intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), text);
                    using (AndroidJavaObject chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, chooserTitle))
                    {
                        activity.Call("startActivity", chooser);
                    }
                }

                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Chevir] Share sheet failed, copying instead: " + e.Message);
            }
#endif
            // iOS needs a native plugin for UIActivityViewController; until then the link is copied.
            GUIUtility.systemCopyBuffer = text;
            return false;
        }
    }
}
