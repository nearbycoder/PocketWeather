using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace PocketWeather
{
    /// <summary>What kind of device this is, for guesses made before the player touches anything.</summary>
    public static class Platform
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int PW_TouchFirst();
        [DllImport("__Internal")] static extern float PW_PixelsPerCssPx();
#endif
        static bool? touchFirst;

        /// <summary>A phone or tablet: its main pointer is a finger. Native mobile builds know it;
        /// in a browser, Unity's <c>Application.isMobilePlatform</c> can miss it (an iPad reports
        /// itself as a Mac), so the page is asked whether its pointer is coarse.</summary>
        public static bool TouchFirst
        {
            get
            {
                if (touchFirst == null)
                {
                    bool t = Application.isMobilePlatform;
#if UNITY_WEBGL && !UNITY_EDITOR
                    try { t |= PW_TouchFirst() != 0; } catch (System.Exception) { }
#endif
                    touchFirst = t;
                    Debug.Log($"[PW] touch-first device: {(t ? "yes" : "no")}");
                }
                return touchFirst.Value;
            }
        }

        static float pxPerCss = 1f;
        static int pxW, pxH;

        /// <summary>Screen pixels per CSS pixel: in a browser, the canvas's backing resolution over
        /// its size on the page (which the page caps at 1.5 on phones and 2 on desktops). 1
        /// elsewhere, where a window's pixels are what its size is judged by.</summary>
        public static float PixelsPerCssPx
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                if (Screen.width != pxW || Screen.height != pxH)
                {
                    pxW = Screen.width; pxH = Screen.height;
                    float r = 1f;
                    try { r = PW_PixelsPerCssPx(); } catch (System.Exception) { }
                    pxPerCss = r > 0.25f && r < 8f ? r : 1f;
                }
#endif
                return pxPerCss;
            }
        }
    }
}
