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
    }
}
