using UnityEngine;

namespace PocketWeather
{
    /// <summary>Player preferences (PlayerPrefs-backed).</summary>
    public static class GameSettings
    {
        public static float Music { get => PlayerPrefs.GetFloat("pw.music", 0.8f); set => PlayerPrefs.SetFloat("pw.music", value); }
        public static float Sfx { get => PlayerPrefs.GetFloat("pw.sfx", 0.9f); set => PlayerPrefs.SetFloat("pw.sfx", value); }
        public static float Ambience { get => PlayerPrefs.GetFloat("pw.amb", 0.7f); set => PlayerPrefs.SetFloat("pw.amb", value); }
        public static bool ScreenShake { get => PlayerPrefs.GetInt("pw.shake", 1) == 1; set => PlayerPrefs.SetInt("pw.shake", value ? 1 : 0); }
        public static float TiltShift { get => PlayerPrefs.GetFloat("pw.tilt", 1f); set => PlayerPrefs.SetFloat("pw.tilt", value); }
        /// <summary>0 = Auto, 1 = High, 2 = Low (see Quality).</summary>
        public static int Graphics { get => PlayerPrefs.GetInt("pw.gfx", 0); set => PlayerPrefs.SetInt("pw.gfx", value); }
        public static bool Hints { get => PlayerPrefs.GetInt("pw.hints", 1) == 1; set => PlayerPrefs.SetInt("pw.hints", value ? 1 : 0); }
        /// <summary>0 auto (when touch is used), 1 always, 2 never.</summary>
        public static int TouchButtons { get => PlayerPrefs.GetInt("pw.touchbtn", 0); set => PlayerPrefs.SetInt("pw.touchbtn", value); }
        public static void Save() => PlayerPrefs.Save();
    }
}
