using System;
using System.Collections.Generic;
using UnityEngine;

namespace PocketWeather
{
    /// <summary>Progress: stamps per level (1 day saved, 2 before par, 4 delight), best finishing hour.</summary>
    public static class SaveData
    {
        public const int StampSaved = 1, StampPar = 2, StampDelight = 4, StampEncore = 8;

        [Serializable]
        public class LevelSave
        {
            public string id;
            public int stamps;
            public float bestHour = 99f;
            public int plays;
        }

        [Serializable]
        class SaveFile
        {
            public List<LevelSave> levels = new();
            public bool seenTitle;
        }

        static SaveFile data;
        const string RealKey = "pw.save.v1";
        static string Key => GameRoot.HasArg("-pwAutopilot") || GameRoot.HasArg("-pwCapture") || GameRoot.HasArg("-pwTouchTest") || GameRoot.HasArg("-pwPadTest") || GameRoot.HasArg("-pwUiAudit") || GameRoot.HasArg("-pwKeyTest") || GameRoot.HasArg("-pwTrailer") ? "pw.save.test" : RealKey;

        static SaveFile Data
        {
            get
            {
                if (data != null) return data;
                if (GameRoot.HasArg("-pwFreshSave")) PlayerPrefs.DeleteKey(Key);
                if (GameRoot.HasArg("-pwCorruptSave")) PlayerPrefs.SetString(Key, "{\"levels\":[{\"id\":\"lev");   // test hook
                if (Key == RealKey) ImportOldLinuxSave();
                var json = PlayerPrefs.GetString(Key, "");
                try
                {
                    data = string.IsNullOrEmpty(json) ? new SaveFile() : (JsonUtility.FromJson<SaveFile>(json) ?? new SaveFile());
                }
                catch (System.Exception e)
                {
                    // a damaged save must never stop the game booting: start fresh, keep the old text aside
                    Debug.LogWarning($"[PW] save unreadable ({e.Message}); starting fresh, kept a copy in {Key}.corrupt");
                    PlayerPrefs.SetString(Key + ".corrupt", json);
                    data = new SaveFile();
                }
                data.levels ??= new System.Collections.Generic.List<LevelSave>();
                return data;
            }
        }

        /// <summary>Started with <c>-screen-fullscreen</c> (as <c>Tools/play.sh</c> did until round 4),
        /// the Linux player keeps its prefs in <c>unity3d/unknown/unknown/</c>, a file other Unity
        /// games share, instead of the game's own folder. The first time there's no save in the right
        /// place, bring the progress over from there (once; settings stay behind, and the old file is
        /// only read, never written).</summary>
        static void ImportOldLinuxSave()
        {
#if UNITY_STANDALONE_LINUX && !UNITY_EDITOR
            const string Done = "pw.save.imported";
            if (PlayerPrefs.HasKey(RealKey) || PlayerPrefs.GetInt(Done, 0) == 1) return;
            try
            {
                var cfg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                if (string.IsNullOrEmpty(cfg)) cfg = System.IO.Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? "", ".config");
                var path = System.IO.Path.Combine(cfg, "unity3d", "unknown", "unknown", "prefs");
                if (!System.IO.File.Exists(path)) return;
                var m = System.Text.RegularExpressions.Regex.Match(System.IO.File.ReadAllText(path),
                    "<pref name=\"" + System.Text.RegularExpressions.Regex.Escape(RealKey) + "\" type=\"string\">([^<]*)</pref>");
                if (!m.Success) return;
                var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(m.Groups[1].Value.Trim()));
                var old = JsonUtility.FromJson<SaveFile>(json);   // throws on a damaged save: then leave it be
                if (old?.levels == null) return;
                PlayerPrefs.SetString(RealKey, json);
                PlayerPrefs.SetInt(Done, 1);
                PlayerPrefs.Save();
                Debug.Log($"[PW] imported progress from {path}: {old.levels.FindAll(l => l.stamps != 0).Count} days with stamps");
            }
            catch (Exception e) { Debug.LogWarning($"[PW] couldn't import the old save: {e.Message}"); }
#endif
        }

        public static LevelSave Get(string id)
        {
            foreach (var l in Data.levels) if (l.id == id) return l;
            var n = new LevelSave { id = id };
            Data.levels.Add(n);
            return n;
        }

        /// <summary>Nobody has played a day yet (a brand-new player).</summary>
        public static bool IsFresh => Data.levels.TrueForAll(l => l.plays == 0 && l.stamps == 0);

        public static bool Has(string id, int stamp) => (Get(id).stamps & stamp) != 0;

        /// <summary>Adds stamps; returns the newly earned ones.</summary>
        public static int Award(string id, int stamps)
        {
            var l = Get(id);
            int fresh = stamps & ~l.stamps;
            l.stamps |= stamps;
            Save();
            return fresh;
        }

        public static void RecordFinish(string id, float hour)
        {
            var l = Get(id);
            if (hour < l.bestHour) l.bestHour = hour;
            Save();
        }

        public static void RecordPlay(string id)
        {
            Get(id).plays++;
            Save();
        }

        public static bool Unlocked(int index)
        {
            if (index <= 0) return true;
            if (Application.isEditor || GameRoot.HasArg("-pwUnlockAll")) return true;
            return Has(LevelLibrary.Campaign[index - 1], StampSaved);
        }

        /// <summary>The summer's 36 stamps (saved, before par, delight per day).</summary>
        public static int TotalStamps()
        {
            int n = 0;
            foreach (var id in LevelLibrary.Campaign)
            {
                int s = Get(id).stamps;
                for (int b = 0; b < 3; b++) if ((s & (1 << b)) != 0) n++;
            }
            return n;
        }

        /// <summary>Encore stamps (one per day, for saving its scorcher).</summary>
        public static int EncoreStamps()
        {
            int n = 0;
            foreach (var id in LevelLibrary.Campaign) if (Has(id, StampEncore)) n++;
            return n;
        }

        /// <summary>A day's Encore opens once the day itself has been saved.</summary>
        public static bool EncoreUnlocked(string id) => Has(id, StampSaved) || GameRoot.HasArg("-pwUnlockAll");

        public static int FirstUnfinished()
        {
            for (int i = 0; i < LevelLibrary.Campaign.Length; i++)
                if (!Has(LevelLibrary.Campaign[i], StampSaved)) return i;
            return LevelLibrary.Campaign.Length - 1;
        }

        public static void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
        }

        public static void Reset()
        {
            data = new SaveFile();
            Save();
        }
    }
}
