using UnityEngine;

#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace XCon.UI
{
    // On the web, lets Esc reach the game while it is fullscreen instead of only leaving fullscreen.
    // See Assets/Plugins/WebGL/FullscreenEscape.jslib.
    public static class FullscreenEscapeLock
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void XCon_InstallFullscreenEscapeLock();
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            XCon_InstallFullscreenEscapeLock();
#endif
        }
    }
}
