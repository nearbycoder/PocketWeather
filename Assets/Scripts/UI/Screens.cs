using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PocketWeather
{
    /// <summary>Base for full-screen menus: a canvas group that fades and pops its content in.</summary>
    public abstract class MenuScreen : MonoBehaviour
    {
        protected RectTransform root;
        protected CanvasGroup group;
        public bool IsOpen { get; private set; }
        protected GameObject firstSelected;

        public void Setup(Transform parent, string name)
        {
            root = Ui.Stretch(name, parent);
            group = Ui.Group(root.gameObject);
            group.alpha = 0;
            group.blocksRaycasts = false;
            group.interactable = false;
            root.gameObject.SetActive(false);
            Build();
        }

        protected abstract void Build();
        protected virtual void OnOpen() { }

        public virtual void Open()
        {
            IsOpen = true;
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            group.blocksRaycasts = true;
            group.interactable = true;
            Ui.Fade(group, 1, 0.3f);
            OnOpen();
            if (firstSelected != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(firstSelected);
        }

        public virtual void Close(Action done = null)
        {
            if (!IsOpen) { done?.Invoke(); return; }
            IsOpen = false;
            group.blocksRaycasts = false;
            group.interactable = false;
            Ui.Fade(group, 0, 0.22f, () => { if (!IsOpen && root != null) root.gameObject.SetActive(false); done?.Invoke(); });
        }

        protected Image Dim(float alpha = 0.45f)
        {
            var d = Ui.Image(root, null, new Color(0.16f, 0.13f, 0.3f, alpha), Vector2.zero, Vector2.zero, null, "Dim");
            d.rectTransform.anchorMin = Vector2.zero; d.rectTransform.anchorMax = Vector2.one;
            d.rectTransform.sizeDelta = Vector2.zero;
            d.raycastTarget = true;
            return d;
        }
    }

    // ===================================================================== Title
    public class TitleScreen : MenuScreen
    {
        public Action OnPlay, OnSettings;
        Text tap;
        RectTransform logo;

        protected override void Build()
        {
            var safe = Ui.Stretch("Safe", root);
            safe.gameObject.AddComponent<SafeArea>();
            logo = Ui.Rect("Logo", safe, new Vector2(0.5f, 1f), new Vector2(1200, 420), new Vector2(0, -230));
            var pip = Ui.Icon(logo, "pip", 210, new Vector2(-430, 40));
            pip.gameObject.AddComponent<UiBob>().amplitude = 10;
            var l1 = Ui.Label(logo, "Pocket", 170, Color.white, new Vector2(900, 190), new Vector2(60, 80), true);
            Ui.Outlined(l1, Res.Hex("7A8FD6"), 5);
            Ui.Shadowed(l1, 10, 0.3f);
            var l2 = Ui.Label(logo, "Weather", 170, Color.white, new Vector2(900, 190), new Vector2(120, -60), true);
            Ui.Outlined(l2, Res.Hex("7A8FD6"), 5);
            Ui.Shadowed(l2, 10, 0.3f);
            var sub = Ui.Label(logo, "a tiny cloud helps a miniature world through its day", 40, Color.white, new Vector2(1100, 60), new Vector2(40, -175), false);
            Ui.Shadowed(sub, 3, 0.45f);
            tap = Ui.Label(safe, "Tap to play", 58, Color.white, new Vector2(800, 90), new Vector2(0, 150), true, TextAnchor.MiddleCenter, new Vector2(0.5f, 0));
            Ui.Outlined(tap, Res.Hex("6C7FCC"), 3);
            var hit = Ui.Image(root, null, new Color(0, 0, 0, 0), Vector2.zero, Vector2.zero, null, "Hit");
            hit.rectTransform.anchorMin = Vector2.zero; hit.rectTransform.anchorMax = Vector2.one;
            hit.raycastTarget = true;
            var btn = hit.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => { Sfx.Ui("ui_pop"); OnPlay?.Invoke(); });
            var set = Ui.Button(safe, "", Ui.Lilac, new Vector2(100, 100), new Vector2(-80, 80), () => OnSettings?.Invoke(), "star", new Vector2(1, 0), 40, "Settings");
            firstSelected = btn.gameObject;
            var credit = Ui.Label(safe, "Mouse, touch, keyboard or gamepad", 28, new Color(1, 1, 1, 0.8f), new Vector2(800, 50), new Vector2(0, 60), false, TextAnchor.MiddleCenter, new Vector2(0.5f, 0));
            Ui.Shadowed(credit, 2, 0.4f);
        }

        protected override void OnOpen()
        {
            Ui.PopIn(logo, 0.1f, 0.7f);
        }

        void Update()
        {
            if (!IsOpen) return;
            float t = Time.unscaledTime;
            tap.color = new Color(1, 1, 1, 0.65f + 0.35f * Mathf.Sin(t * 3f));
            tap.rectTransform.localScale = Vector3.one * (1 + 0.04f * Mathf.Sin(t * 3f));
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if ((kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) ||
                (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame)))
            {
                Sfx.Ui("ui_pop");
                OnPlay?.Invoke();
            }
        }
    }

    // ===================================================================== Map (level select)
    public class MapScreen : MenuScreen
    {
        public Action<int> OnPick;
        public Action OnBack, OnSettings;
        readonly List<JuicyButton> cards = new();
        Text total;
        RectTransform grid;

        protected override void Build()
        {
            Dim(0.28f);
            var safe = Ui.Stretch("Safe", root);
            safe.gameObject.AddComponent<SafeArea>();
            var title = Ui.Label(safe, "Pocketvale", 96, Color.white, new Vector2(900, 120), new Vector2(0, -95), true, TextAnchor.MiddleCenter, new Vector2(0.5f, 1));
            Ui.Outlined(title, Res.Hex("7A8FD6"), 4);
            Ui.Shadowed(title, 8, 0.3f);
            var sub = Ui.Label(safe, "One summer, twelve little days", 38, Color.white, new Vector2(900, 50), new Vector2(0, -170), false, TextAnchor.MiddleCenter, new Vector2(0.5f, 1));
            Ui.Shadowed(sub, 3, 0.4f);
            var tp = Ui.Panel(safe, new Vector2(230, 84), new Vector2(-150, -80), Ui.Paper, new Vector2(1, 1), 42f);
            Ui.Icon(tp.transform, "stamp_flower", 64, new Vector2(-70, 2));
            total = Ui.Label(tp.transform, "0/36", 40, Ui.Ink, new Vector2(140, 70), new Vector2(30, 2), true);
            var back = Ui.Button(safe, "Title", Ui.Lilac, new Vector2(220, 90), new Vector2(150, 80), () => OnBack?.Invoke(), null, new Vector2(0, 0), 38);
            Ui.Button(safe, "", Ui.Lilac, new Vector2(90, 90), new Vector2(-80, 80), () => OnSettings?.Invoke(), "star", new Vector2(1, 0), 38, "Settings");
            grid = Ui.Rect("Grid", safe, new Vector2(0.5f, 0.5f), new Vector2(1700, 700), new Vector2(0, -20));
        }

        protected override void OnOpen()
        {
            foreach (Transform c in grid) Destroy(c.gameObject);
            cards.Clear();
            total.text = $"{SaveData.TotalStamps()}/{LevelLibrary.Campaign.Length * 3}";
            int next = SaveData.FirstUnfinished();
            for (int i = 0; i < LevelLibrary.Campaign.Length; i++)
            {
                int idx = i;
                int col = i % 6, row = i / 6;
                var pos = new Vector2((col - 2.5f) * 272, (0.5f - row) * 330);
                var def = LevelLibrary.Load(LevelLibrary.Campaign[i]);
                bool unlocked = SaveData.Unlocked(i);
                var save = SaveData.Get(LevelLibrary.Campaign[i]);
                var card = Ui.Button(grid, "", unlocked ? (i == next ? Ui.Butter : Ui.Paper) : Res.Hex("D8D3E6"), new Vector2(244, 300), pos, () =>
                {
                    if (SaveData.Unlocked(idx)) OnPick?.Invoke(idx);
                    else { Sfx.Ui("ui_back"); }
                }, null, null, 30, "Card" + i);
                var face = card.transform.Find("Face");
                Ui.Label(face, $"Day {i + 1}", 30, Ui.InkSoft, new Vector2(220, 40), new Vector2(0, 118), true);
                Ui.Icon(face, unlocked ? (def != null && !string.IsNullOrEmpty(def.icon) ? def.icon : "flower") : "lock", 120, new Vector2(0, 38));
                Ui.Label(face, def != null ? def.title : "?", 30, Ui.Ink, new Vector2(226, 80), new Vector2(0, -62), true);
                string[] stampIcons = { "stamp_sun", "stamp_clock", "stamp_flower" };
                for (int s = 0; s < 3; s++)
                {
                    bool has = (save.stamps & (1 << s)) != 0;
                    var st = Ui.Icon(face, has ? stampIcons[s] : "stamp_empty", 52, new Vector2((s - 1) * 58, -118));
                    if (!has) st.color = new Color(1, 1, 1, 0.6f);
                }
                if (i == next && unlocked) card.gameObject.AddComponent<UiBob>().amplitude = 5;
                card.interactable = true;
                cards.Add(card);
                Ui.PopIn(card.transform, 0.03f * i, 0.4f);
            }
            firstSelected = cards[Mathf.Clamp(next, 0, cards.Count - 1)].gameObject;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(firstSelected);
        }

        void Update()
        {
            if (!IsOpen) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) OnBack?.Invoke();
        }
    }

    // ===================================================================== Postcard (level intro)
    public class Postcard : MenuScreen
    {
        public Action OnStart;
        RectTransform card;
        Text day, title, story, parText;
        RectTransform needsRow, stampsRow;
        Image icon;

        protected override void Build()
        {
            Dim(0.25f);
            card = Ui.Panel(root, new Vector2(1000, 640), new Vector2(0, 10), Ui.Paper, null, 48f).rectTransform;
            var stripe = Ui.Image(card, Ui.Rounded, Res.Hex("CFE6F7"), new Vector2(960, 120), new Vector2(0, 240), null, "Stripe");
            stripe.pixelsPerUnitMultiplier = 52f / 36f;
            icon = Ui.Icon(card, "flower", 150, new Vector2(-370, 245));
            day = Ui.Label(card, "Day 1", 34, Ui.InkSoft, new Vector2(600, 50), new Vector2(40, 272), true, TextAnchor.MiddleLeft);
            title = Ui.Label(card, "First Drops", 66, Ui.Ink, new Vector2(700, 80), new Vector2(90, 222), true, TextAnchor.MiddleLeft);
            story = Ui.Label(card, "", 36, Ui.Ink, new Vector2(860, 140), new Vector2(0, 100), false);
            Ui.Label(card, "Today, help:", 30, Ui.InkSoft, new Vector2(400, 40), new Vector2(0, 22), true);
            needsRow = Ui.Rect("Needs", card, new Vector2(0.5f, 0.5f), new Vector2(900, 110), new Vector2(0, -60));
            var parRow = Ui.Rect("Par", card, new Vector2(0.5f, 0.5f), new Vector2(600, 60), new Vector2(-170, -175));
            Ui.Icon(parRow, "clock", 54, new Vector2(-250, 0));
            parText = Ui.Label(parRow, "", 32, Ui.Ink, new Vector2(500, 60), new Vector2(40, 0), true, TextAnchor.MiddleLeft);
            stampsRow = Ui.Rect("Stamps", card, new Vector2(0.5f, 0.5f), new Vector2(240, 70), new Vector2(-330, -245));
            var start = Ui.Button(card, "Start", Ui.Coral, new Vector2(300, 104), new Vector2(300, -230), () => OnStart?.Invoke(), null, null, 50);
            firstSelected = start.gameObject;
        }

        public void Show(LevelDef def, int index)
        {
            day.text = $"Day {index + 1}";
            title.text = def.title;
            story.text = def.story;
            icon.sprite = Ui.IconSprite(string.IsNullOrEmpty(def.icon) ? "flower" : def.icon);
            parText.text = $"Stamp for finishing before {FormatHour(def.par)}";
            foreach (Transform c in needsRow) Destroy(c.gameObject);
            var reqs = new List<NeedDef>();
            foreach (var n in def.needs) if (!n.hidden) reqs.Add(n);
            for (int i = 0; i < reqs.Count; i++)
            {
                var it = Ui.Rect("N" + i, needsRow, new Vector2(0.5f, 0.5f), new Vector2(110, 110), new Vector2((i - (reqs.Count - 1) / 2f) * 124, 0));
                Ui.Image(it, Ui.Circle, Res.Hex("F1E8DA"), new Vector2(104, 104), Vector2.zero, null, "Bg");
                Ui.Icon(it, NeedIcon(reqs[i]), 74, new Vector2(0, 2));
                Ui.PopIn(it, 0.25f + 0.07f * i, 0.4f);
            }
            foreach (Transform c in stampsRow) Destroy(c.gameObject);
            var save = SaveData.Get(def.id);
            string[] icons = { "stamp_sun", "stamp_clock", "stamp_flower" };
            for (int s = 0; s < 3; s++)
            {
                bool has = (save.stamps & (1 << s)) != 0;
                var st = Ui.Icon(stampsRow, has ? icons[s] : "stamp_empty", 62, new Vector2((s - 1) * 72, 0));
                if (!has) st.color = new Color(1, 1, 1, 0.6f);
            }
            Open();
            card.localScale = Vector3.one;
            Tween.To(-900, 0, 0.6f, y => card.anchoredPosition = new Vector2(0, 10 + y), k => Ease.OutBack(k, 1.3f), 0, null, card);
            card.localRotation = Quaternion.Euler(0, 0, -3);
            Tween.To(-6, -1.5f, 0.7f, a => { if (card != null) card.localRotation = Quaternion.Euler(0, 0, a); }, k => Ease.OutBack(k));
        }

        public static string NeedIcon(NeedDef n)
        {
            switch (n.type)
            {
                case "bed":
                    return n.plant switch { "carrot" => "carrot", "cabbage" => "cabbage", "tomato" => "tomato", "wheat" => "wheat", "pumpkin" => "pumpkin", "mud" => "mud", "sapling" => "flower", _ => "flower" };
                case "shade":
                    return n.model.Contains("sheep") || n.model.Contains("lamb") ? "sheep" : n.model.StartsWith("person") ? "person" : "sun";
                case "boat": return "boat";
                case "laundry": return "shirt";
                case "windmill": return "windmill";
                case "rainbow": return "rainbow";
                case "fire": return "fire";
                case "campfire": return "campfire";
                case "keepdry": return n.model.Contains("castle") ? "castle" : n.model.Contains("cake") ? "cake" : "shirt";
                case "sunny": return "sunflower";
                case "pondline": return "duck";
                default: return "star";
            }
        }

        public static string FormatHour(float h)
        {
            int hh = Mathf.FloorToInt(h);
            int mm = Mathf.RoundToInt((h - hh) * 60);
            return $"{hh}:{mm:00}";
        }

        void Update()
        {
            if (!IsOpen) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if ((kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) || (pad != null && pad.startButton.wasPressedThisFrame))
                OnStart?.Invoke();
        }
    }

    // ===================================================================== Pause
    public class PauseMenu : MenuScreen
    {
        public Action OnResume, OnRestart, OnMap, OnSettings;
        Text delight;

        protected override void Build()
        {
            Dim(0.5f);
            var p = Ui.Panel(root, new Vector2(620, 720), Vector2.zero, Ui.Paper, null, 48f);
            Ui.Label(p.transform, "Paused", 76, Ui.Ink, new Vector2(560, 100), new Vector2(0, 282), true);
            var r = Ui.Button(p.transform, "Resume", Ui.Coral, new Vector2(420, 100), new Vector2(0, 160), () => OnResume?.Invoke(), null, null, 46);
            Ui.Button(p.transform, "Restart", Ui.Sky, new Vector2(420, 96), new Vector2(0, 40), () => OnRestart?.Invoke(), null, null, 42);
            Ui.Button(p.transform, "Settings", Ui.Mint, new Vector2(420, 96), new Vector2(0, -76), () => OnSettings?.Invoke(), null, null, 42);
            Ui.Button(p.transform, "Map", Ui.Lilac, new Vector2(420, 96), new Vector2(0, -192), () => OnMap?.Invoke(), null, null, 42);
            delight = Ui.Label(p.transform, "", 30, Ui.InkSoft, new Vector2(560, 90), new Vector2(0, -300), false);
            firstSelected = r.gameObject;
        }

        public void SetDelight(LevelDef def, bool found)
        {
            if (def == null || def.delight == null || string.IsNullOrEmpty(def.delight.type)) { delight.text = ""; return; }
            delight.text = found ? $"Delight found: {def.delight.title}" : $"Secret delight: {def.delight.hint}";
        }

        void Update()
        {
            if (!IsOpen) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if ((kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)) || (pad != null && (pad.startButton.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame)))
                OnResume?.Invoke();
        }
    }

    // ===================================================================== Settings
    public class SettingsMenu : MenuScreen
    {
        public Action OnClose;
        JuicyButton resetBtn;
        bool confirmReset;

        protected override void Build()
        {
            Dim(0.55f);
            var p = Ui.Panel(root, new Vector2(760, 900), Vector2.zero, Ui.Paper, null, 48f);
            Ui.Label(p.transform, "Settings", 70, Ui.Ink, new Vector2(600, 90), new Vector2(0, 380), true);
            float y = 270;
            Ui.Slider(p.transform, "Music", GameSettings.Music, new Vector2(0, y), v => { GameSettings.Music = v; }, 640); y -= 90;
            Ui.Slider(p.transform, "Sounds", GameSettings.Sfx, new Vector2(0, y), v => { GameSettings.Sfx = v; }, 640); y -= 90;
            Ui.Slider(p.transform, "Ambience", GameSettings.Ambience, new Vector2(0, y), v => { GameSettings.Ambience = v; }, 640); y -= 90;
            Ui.Slider(p.transform, "Tilt-shift", GameSettings.TiltShift, new Vector2(0, y), v => { GameSettings.TiltShift = v; PostFx.ApplySettings(); }, 640); y -= 92;
            Ui.Toggle(p.transform, "Screen shake", GameSettings.ScreenShake, new Vector2(0, y), v => GameSettings.ScreenShake = v, 640); y -= 84;
            Ui.Toggle(p.transform, "Hints", GameSettings.Hints, new Vector2(0, y), v => GameSettings.Hints = v, 640); y -= 84;
            Ui.Toggle(p.transform, "Touch buttons", GameSettings.TouchButtons == 1, new Vector2(0, y), v => GameSettings.TouchButtons = v ? 1 : 0, 640); y -= 84;
            Ui.Toggle(p.transform, "Fullscreen", UnityEngine.Screen.fullScreen, new Vector2(0, y), v =>
            {
                UnityEngine.Screen.fullScreenMode = v ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            }, 640);
            y -= 112;
            resetBtn = Ui.Button(p.transform, "Reset progress", Ui.Coral, new Vector2(300, 84), new Vector2(-170, y), () =>
            {
                if (!confirmReset) { confirmReset = true; resetBtn.SetLabel("Sure?"); return; }
                SaveData.Reset();
                confirmReset = false;
                resetBtn.SetLabel("Done!");
            }, null, null, 32);
            var done = Ui.Button(p.transform, "Done", Ui.Mint, new Vector2(280, 92), new Vector2(170, y), () => { GameSettings.Save(); OnClose?.Invoke(); }, null, null, 42);
            firstSelected = done.gameObject;
        }

        protected override void OnOpen()
        {
            confirmReset = false;
            resetBtn.SetLabel("Reset progress");
        }

        void Update()
        {
            if (!IsOpen) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { GameSettings.Save(); OnClose?.Invoke(); }
        }
    }

    // ===================================================================== Results
    public class ResultsCard : MenuScreen
    {
        public Action OnNext, OnReplay, OnMap;
        RectTransform card;
        Text title, subtitle;
        readonly Image[] stamps = new Image[3];
        readonly Text[] stampLabels = new Text[3];
        JuicyButton next;

        protected override void Build()
        {
            Dim(0.3f);
            card = Ui.Panel(root, new Vector2(1060, 640), new Vector2(0, 0), Ui.Paper, null, 48f).rectTransform;
            var banner = Ui.Image(card, Ui.Rounded, Ui.Butter, new Vector2(700, 120), new Vector2(0, 300), null, "Banner");
            banner.pixelsPerUnitMultiplier = 52f / 40f;
            title = Ui.Label(banner.transform, "Day saved!", 74, Color.white, new Vector2(680, 110), new Vector2(0, 2), true);
            Ui.Outlined(title, Res.Hex("E09A3A"), 4);
            subtitle = Ui.Label(card, "", 36, Ui.InkSoft, new Vector2(960, 60), new Vector2(0, 190), false);
            string[] labels = { "Helped everyone", "Before 12:00", "Secret delight" };
            for (int i = 0; i < 3; i++)
            {
                var slot = Ui.Rect("Slot" + i, card, new Vector2(0.5f, 0.5f), new Vector2(300, 260), new Vector2((i - 1) * 320, 20));
                Ui.Image(slot, Ui.Circle, Res.Hex("EFE6D8"), new Vector2(190, 190), new Vector2(0, 30), null, "Plate");
                stamps[i] = Ui.Icon(slot, "stamp_empty", 180, new Vector2(0, 30));
                stampLabels[i] = Ui.Label(slot, labels[i], 30, Ui.Ink, new Vector2(300, 70), new Vector2(0, -100), true);
            }
            Ui.Button(card, "Map", Ui.Lilac, new Vector2(230, 96), new Vector2(-330, -235), () => OnMap?.Invoke(), null, null, 42);
            Ui.Button(card, "Replay", Ui.Sky, new Vector2(250, 96), new Vector2(-50, -235), () => OnReplay?.Invoke(), null, null, 42);
            next = Ui.Button(card, "Next day", Ui.Coral, new Vector2(300, 104), new Vector2(270, -235), () => OnNext?.Invoke(), null, null, 46);
            firstSelected = next.gameObject;
        }

        public void Show(LevelDef def, int stampsEarnedThisRun, int fresh, float finishHour, bool isLast)
        {
            subtitle.text = $"{(string.IsNullOrEmpty(def.thanks) ? "Everyone is happy!" : def.thanks)}  Finished at {Postcard.FormatHour(finishHour)}.";
            stampLabels[1].text = $"Before {Postcard.FormatHour(def.par)}";
            stampLabels[2].text = SaveData.Has(def.id, SaveData.StampDelight) ? def.delight.title : "Secret delight";
            next.SetLabel(isLast ? "The end" : "Next day");
            var save = SaveData.Get(def.id);
            string[] icons = { "stamp_sun", "stamp_clock", "stamp_flower" };
            for (int i = 0; i < 3; i++)
            {
                bool had = (save.stamps & (1 << i)) != 0 && (fresh & (1 << i)) == 0;
                stamps[i].sprite = Ui.IconSprite(had ? icons[i] : "stamp_empty");
                stamps[i].color = had ? Color.white : new Color(1, 1, 1, 0.55f);
                stamps[i].rectTransform.localScale = Vector3.one;
            }
            Open();
            Tween.To(700, 0, 0.55f, y => card.anchoredPosition = new Vector2(0, y), k => Ease.OutBack(k, 1.2f), 0, null, card);
            // slam in each newly earned stamp
            float delay = 0.6f;
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                bool earned = (stampsEarnedThisRun & (1 << i)) != 0;
                bool isFresh = (fresh & (1 << i)) != 0;
                if (!earned && !((save.stamps & (1 << i)) != 0)) continue;
                if (!isFresh && (save.stamps & (1 << i)) != 0 && !earned) continue;
                if (!isFresh) continue;
                Tween.Delay(delay, () =>
                {
                    stamps[k].sprite = Ui.IconSprite(icons[k]);
                    stamps[k].color = Color.white;
                    var t = stamps[k].rectTransform;
                    Tween.To(2.4f, 1f, 0.32f, s => { if (t != null) t.localScale = Vector3.one * s; }, Ease.InCubic, 0, () =>
                    {
                        Sfx.Ui("stamp");
                        Tween.Punch(card, 0.03f, 0.3f);
                        GameRoot.Instance?.Rig.Shake(0.4f);
                    });
                    t.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(-12f, 12f));
                });
                delay += 0.55f;
            }
        }
    }

    // ===================================================================== Sunset (fail)
    public class FailCard : MenuScreen
    {
        public Action OnRetry, OnMap;
        RectTransform card, row;

        protected override void Build()
        {
            Dim(0.4f);
            card = Ui.Panel(root, new Vector2(900, 560), Vector2.zero, Ui.Paper, null, 48f).rectTransform;
            Ui.Label(card, "The sun has set", 70, Ui.Ink, new Vector2(820, 90), new Vector2(0, 200), true);
            Ui.Label(card, "Some friends still needed you. Try again tomorrow!", 36, Ui.InkSoft, new Vector2(800, 90), new Vector2(0, 110), false);
            row = Ui.Rect("Row", card, new Vector2(0.5f, 0.5f), new Vector2(800, 120), new Vector2(0, -10));
            Ui.Button(card, "Map", Ui.Lilac, new Vector2(240, 96), new Vector2(-180, -190), () => OnMap?.Invoke(), null, null, 42);
            var r = Ui.Button(card, "Try again", Ui.Coral, new Vector2(320, 104), new Vector2(160, -190), () => OnRetry?.Invoke(), null, null, 46);
            firstSelected = r.gameObject;
        }

        public void Show(Level level)
        {
            foreach (Transform c in row) Destroy(c.gameObject);
            var unmet = new List<Need>();
            foreach (var n in level.Needs) if (n.Required && !n.Met) unmet.Add(n);
            for (int i = 0; i < unmet.Count; i++)
            {
                var it = Ui.Rect("U" + i, row, new Vector2(0.5f, 0.5f), new Vector2(110, 110), new Vector2((i - (unmet.Count - 1) / 2f) * 124, 0));
                Ui.Image(it, Ui.Circle, Res.Hex("F6DCD6"), new Vector2(104, 104), Vector2.zero, null, "Bg");
                Ui.Icon(it, unmet[i].Icon, 72, new Vector2(0, 2));
                Ui.PopIn(it, 0.3f + 0.08f * i, 0.4f);
            }
            Open();
            Tween.To(600, 0, 0.55f, y => card.anchoredPosition = new Vector2(0, y), k => Ease.OutBack(k, 1.1f), 0, null, card);
        }
    }

    // ===================================================================== Ending
    public class EndingScreen : MenuScreen
    {
        public Action OnDone;
        RectTransform scroll;
        float t;

        protected override void Build()
        {
            Dim(0.35f);
            var title = Ui.Label(root, "The End", 120, Color.white, new Vector2(1000, 150), new Vector2(0, 330), true);
            Ui.Outlined(title, Res.Hex("D9779B"), 5);
            Ui.Shadowed(title, 8, 0.3f);
            var sub = Ui.Label(root, "Rosa and Tom were married under a rainbow,\nand Pocketvale had the loveliest summer it can remember.", 40, Color.white, new Vector2(1400, 120), new Vector2(0, 190), false);
            Ui.Shadowed(sub, 3, 0.45f);
            scroll = Ui.Rect("Credits", root, new Vector2(0.5f, 0.5f), new Vector2(1000, 400), new Vector2(0, -60));
            string credits = "POCKET WEATHER\n\nDesign, code, models, music & sound\nmade procedurally with Unity, Blender and numpy\n\nFonts: Fredoka and Nunito (SIL Open Font License)\n\nThank you for helping Pip!";
            var c = Ui.Label(scroll, credits, 36, Color.white, new Vector2(1000, 400), Vector2.zero, false);
            Ui.Shadowed(c, 3, 0.45f);
            var b = Ui.Button(root, "Back to Pocketvale", Ui.Coral, new Vector2(460, 104), new Vector2(0, -360), () => OnDone?.Invoke(), "heart", null, 40);
            firstSelected = b.gameObject;
        }

        protected override void OnOpen() { t = 0; }
    }

    // ===================================================================== Cloud wipe transition
    public class CloudWipe : MonoBehaviour
    {
        RectTransform root;
        readonly List<(RectTransform rt, float delay, float size)> puffs = new();
        Image back;
        public bool Busy { get; private set; }

        public static CloudWipe Create(Transform parent)
        {
            var c = Ui.MakeCanvas("WipeCanvas", 100, parent);
            var w = c.gameObject.AddComponent<CloudWipe>();
            w.root = (RectTransform)c.transform;
            w.Build();
            return w;
        }

        void Build()
        {
            back = Ui.Image(root, null, Color.white, Vector2.zero, Vector2.zero, null, "Back");
            back.rectTransform.anchorMin = Vector2.zero; back.rectTransform.anchorMax = Vector2.one;
            back.color = new Color(1, 1, 1, 0);
            var rnd = new System.Random(5);
            for (int i = 0; i < 26; i++)
            {
                float x = (float)rnd.NextDouble() * 2300 - 1150;
                float y = (float)rnd.NextDouble() * 1400 - 700;
                float size = 420 + (float)rnd.NextDouble() * 360;
                var shade = Ui.Image(root, Ui.Circle, Res.Hex("D9DEF2"), new Vector2(size, size), new Vector2(x, y - size * 0.06f), null, "PuffShade");
                var img = Ui.Image(shade.transform, Ui.Circle, Color.white, new Vector2(size, size), new Vector2(0, size * 0.06f), null, "Puff");
                float delay = (x + 1150) / 2300f * 0.28f + (float)rnd.NextDouble() * 0.08f;
                puffs.Add((shade.rectTransform, delay, size));
                shade.rectTransform.localScale = Vector3.zero;
            }
            root.gameObject.SetActive(false);
        }

        /// <summary>Clouds roll in, run the action while covered, then roll away.</summary>
        public void Run(Action covered, Action finished = null)
        {
            Busy = true;
            root.gameObject.SetActive(true);
            Sfx.Ui("whoosh");
            foreach (var (rt, d, s) in puffs)
            {
                var r = rt;
                Tween.To(0, 1, 0.42f, k => { if (r != null) r.localScale = Vector3.one * k; }, k => Ease.OutBack(k, 1.4f), d, null, r);
            }
            Tween.To(0, 1, 0.3f, a => back.color = new Color(1, 1, 1, a), Ease.InCubic, 0.4f);
            Tween.Delay(0.85f, () =>
            {
                covered?.Invoke();
                Tween.Delay(0.25f, () =>
                {
                    Sfx.Ui("whoosh", 0.7f);
                    Tween.To(1, 0, 0.3f, a => back.color = new Color(1, 1, 1, a), Ease.OutCubic);
                    foreach (var (rt, d, s) in puffs)
                    {
                        var r = rt;
                        Tween.To(1, 0, 0.4f, k => { if (r != null) r.localScale = Vector3.one * k; }, Ease.InCubic, 0.3f - d, null, r);
                    }
                    Tween.Delay(0.75f, () => { root.gameObject.SetActive(false); Busy = false; finished?.Invoke(); });
                });
            });
        }
    }
}
