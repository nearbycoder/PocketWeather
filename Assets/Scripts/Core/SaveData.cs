using System;
using System.Collections.Generic;
using UnityEngine;

namespace PocketWeather
{
    /// <summary>Progress: stamps per level (1 day saved, 2 before par, 4 delight), best finishing hour.</summary>
    public static class SaveData
    {
        public const int StampSaved = 1, StampPar = 2, StampDelight = 4;

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
        static string Key => GameRoot.HasArg("-pwAutopilot") || GameRoot.HasArg("-pwCapture") || GameRoot.HasArg("-pwTouchTest") || GameRoot.HasArg("-pwPadTest") || GameRoot.HasArg("-pwUiAudit") || GameRoot.HasArg("-pwKeyTest") || GameRoot.HasArg("-pwTrailer") ? "pw.save.test" : "pw.save.v1";

        static SaveFile Data
        {
            get
            {
                if (data != null) return data;
                if (GameRoot.HasArg("-pwFreshSave")) PlayerPrefs.DeleteKey(Key);
                if (GameRoot.HasArg("-pwCorruptSave")) PlayerPrefs.SetString(Key, "{\"levels\":[{\"id\":\"lev");   // test hook
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

        public static LevelSave Get(string id)
        {
            foreach (var l in Data.levels) if (l.id == id) return l;
            var n = new LevelSave { id = id };
            Data.levels.Add(n);
            return n;
        }

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
