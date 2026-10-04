using UnityEngine;

namespace PocketWeather
{
    /// <summary>
    /// Unscaled (pause-proof) time for menus, tweens, the camera and the mix. In play it's just
    /// Time.unscaled*. During a fixed-clock video capture (Recorder sets Time.captureFramerate)
    /// Unity's unscaled time still follows the wall clock, so on a slow machine every menu
    /// animation and toast would race by; here each frame counts as exactly one capture step.
    /// </summary>
    public static class Clock
    {
        static int lastFrame = -1;
        static float captureTime = -1f;

        static bool Capturing => Time.captureDeltaTime > 0f;

        public static float UnscaledDelta => Capturing ? Time.captureDeltaTime : Time.unscaledDeltaTime;

        public static float UnscaledTime
        {
            get
            {
                if (!Capturing) return Time.unscaledTime;
                if (captureTime < 0f) { captureTime = Time.unscaledTime; lastFrame = Time.frameCount; }
                if (Time.frameCount != lastFrame)
                {
                    captureTime += (Time.frameCount - lastFrame) * Time.captureDeltaTime;
                    lastFrame = Time.frameCount;
                }
                return captureTime;
            }
        }
    }
}
