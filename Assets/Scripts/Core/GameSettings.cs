using UnityEngine;

namespace PocketWeather
{
    /// <summary>Player preferences (PlayerPrefs-backed).</summary>
    public static class GameSettings
    {
        public static float Music { get => PlayerPrefs.GetFloat("pw.music", 0.8f); set => PlayerPrefs.SetFloat("pw.music", value); }
        /// <summary>The music volume to come back to when M un-mutes it.</summary>
        public static float MusicBeforeMute { get => PlayerPrefs.GetFloat("pw.music.unmuted", 0.8f); set => PlayerPrefs.SetFloat("pw.music.unmuted", value); }
        /// <summary>M: silence the music, or bring it back at the player's own volume.</summary>
        public static void ToggleMusicMute()
        {
            if (Music > 0.01f) { MusicBeforeMute = Music; Music = 0f; }
            else Music = Mathf.Max(0.05f, MusicBeforeMute);
            Save();
        }
        public static float Sfx { get => PlayerPrefs.GetFloat("pw.sfx", 0.9f); set => PlayerPrefs.SetFloat("pw.sfx", value); }
        public static float Ambience { get => PlayerPrefs.GetFloat("pw.amb", 0.7f); set => PlayerPrefs.SetFloat("pw.amb", value); }
        public static bool ScreenShake { get => PlayerPrefs.GetInt("pw.shake", 1) == 1; set => PlayerPrefs.SetInt("pw.shake", value ? 1 : 0); }
        public static float TiltShift { get => PlayerPrefs.GetFloat("pw.tilt", 1f); set => PlayerPrefs.SetFloat("pw.tilt", value); }
        /// <summary>0 = Auto, 1 = High, 2 = Low (see Quality).</summary>
        public static int Graphics { get => PlayerPrefs.GetInt("pw.gfx", 0); set => PlayerPrefs.SetInt("pw.gfx", value); }
        /// <summary>Accessibility: a press starts the rain and another stops it, instead of holding.</summary>
        public static bool RainToggle { get => PlayerPrefs.GetInt("pw.raintoggle", 0) == 1; set => PlayerPrefs.SetInt("pw.raintoggle", value ? 1 : 0); }
        /// <summary>Accessibility: on an ordinary day the sun takes half as long again to cross the sky
        /// (Encores keep their scorcher pace). Takes effect at once, even mid-day.</summary>
        public static bool RelaxedDays
        {
            // read every frame by the day clock. Bots and self-tests start with it off whatever their
            // sandbox's prefs say, so one test turning it on can't slow the next one's days
            // (-pwRelaxed starts them with it on)
            get => relaxed ??= GameRoot.HasArg("-pwRelaxed") || (!GameRoot.Automated && PlayerPrefs.GetInt("pw.relaxed", 0) == 1);
            set { relaxed = value; PlayerPrefs.SetInt("pw.relaxed", value ? 1 : 0); }
        }
        static bool? relaxed;
        public static bool Hints { get => PlayerPrefs.GetInt("pw.hints", 1) == 1; set => PlayerPrefs.SetInt("pw.hints", value ? 1 : 0); }
        /// <summary>0 auto (when touch is used), 1 always, 2 never.</summary>
        public static int TouchButtons { get => PlayerPrefs.GetInt("pw.touchbtn", 0); set => PlayerPrefs.SetInt("pw.touchbtn", value); }
        public static void Save() => PlayerPrefs.Save();
    }
}
