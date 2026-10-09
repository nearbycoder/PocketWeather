using UnityEngine;
using UnityEngine.InputSystem;
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
        [DllImport("__Internal")] static extern float PW_SafeInset(int side);
        [DllImport("__Internal")] static extern int PW_LastInput();
        [DllImport("__Internal")] static extern int PW_InputCount();
        [DllImport("__Internal")] static extern void PW_SetUi(string json);
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

        static Rect safe;
        static int safeW = -1, safeH = -1, safeFrame = -100;

        /// <summary>The part of the screen clear of notches, rounded corners and the home indicator,
        /// in screen pixels. A browser's comes from the page's CSS safe-area insets (Unity's
        /// Screen.safeArea is the whole canvas there), checked twice a second and on every resize.</summary>
        public static Rect SafeArea
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                if (Screen.width != safeW || Screen.height != safeH || Time.frameCount - safeFrame >= 30)
                {
                    safeW = Screen.width; safeH = Screen.height; safeFrame = Time.frameCount;
                    float k = PixelsPerCssPx, l = 0, r = 0, t = 0, b = 0;
                    try { l = PW_SafeInset(0) * k; r = PW_SafeInset(1) * k; t = PW_SafeInset(2) * k; b = PW_SafeInset(3) * k; } catch (System.Exception) { }
                    // never more than a third of the screen from any side
                    l = Mathf.Clamp(l, 0, safeW / 3f); r = Mathf.Clamp(r, 0, safeW / 3f);
                    t = Mathf.Clamp(t, 0, safeH / 3f); b = Mathf.Clamp(b, 0, safeH / 3f);
                    safe = new Rect(l, b, safeW - l - r, safeH - t - b);
                }
                return safe;
#else
                return Screen.safeArea;
#endif
            }
        }

        static bool? touchMode;
        static int inputCount, modeFrame = -1;

        /// <summary>The player is playing by touch, so the on-screen buttons are wanted (with the
        /// setting on Auto): from the start on a touch-first device, after any touch elsewhere, and
        /// not from the moment a key, a mouse or a gamepad is used, until the next touch.</summary>
        public static bool TouchMode
        {
            get
            {
                if (modeFrame != Time.frameCount) { modeFrame = Time.frameCount; TickTouchMode(); }
                return touchMode.Value;
            }
        }

        static void TickTouchMode()
        {
            touchMode ??= TouchFirst;
#if UNITY_WEBGL && !UNITY_EDITOR
            // the page tells fingers, mice and keys apart by the browser's own events
            int n = 0, kind = 0;
            try { n = PW_InputCount(); kind = PW_LastInput(); } catch (System.Exception) { }
            if (n != inputCount)
            {
                inputCount = n;
                if (kind == 1) touchMode = true;
                else if (kind == 2 || kind == 3) touchMode = false;
            }
#else
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.wasPressedThisFrame) touchMode = true;
            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.delta.ReadValue().sqrMagnitude > 16f) &&
                (ts == null || !ts.primaryTouch.press.isPressed)) touchMode = false;
            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame) touchMode = false;
#endif
            var pad = Gamepad.current;
            if (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame ||
                pad.buttonNorth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame || pad.leftStick.ReadValue().sqrMagnitude > 0.25f ||
                pad.dpad.ReadValue().sqrMagnitude > 0.25f)) touchMode = false;
        }

        /// <summary>Answers a test tool's question (GameRoot.PwReportUi) into the page's window.pwUi.</summary>
        public static void SendUi(string json)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { PW_SetUi(json); } catch (System.Exception) { }
#endif
        }
    }
}
