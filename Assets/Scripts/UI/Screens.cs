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
        int openedFrame = -1;
        /// <summary>True on the frame the screen opened, so the press that opened it isn't read again.</summary>
        protected bool JustOpened => Time.frameCount == openedFrame;

        static readonly List<MenuScreen> all = new();
        /// <summary>The menu a control is drawn on (its content lives under the canvas, not under the
        /// screen's own object), or null.</summary>
        public static MenuScreen Owning(Transform t)
        {
            foreach (var m in all) if (m != null && m.root != null && t.IsChildOf(m.root)) return m;
            return null;
        }

        public void Setup(Transform parent, string name)
        {
            all.Add(this);
            root = Ui.Stretch(name, parent);
            group = Ui.Group(root.gameObject);
            group.alpha = 0;
            group.blocksRaycasts = false;
            group.interactable = false;
            root.gameObject.SetActive(false);
            Build();
            ApplyLayout();
            Ui.MenuLayoutChanged += _ => ApplyLayout();
        }

        protected abstract void Build();
        protected virtual void OnOpen() { }

        MenuForm? laidOut;
        /// <summary>A phone draws the menus bigger (see Ui.MenuLayout). On its side that leaves a
        /// design area only about 820 to 900 units tall (Short), and upright one about 860 to 890
        /// units wide (Narrow): screens that wouldn't fit move things here. Called after Build and
        /// whenever the layout changes; each override sets everything it moves for every form.</summary>
        protected virtual void Layout(MenuForm form) { }
        void ApplyLayout()
        {
            if (laidOut == Ui.MenuLayout) return;
            laidOut = Ui.MenuLayout;
            Layout(Ui.MenuLayout);
        }

        public virtual void Open()
        {
            IsOpen = true;
            openedFrame = Time.frameCount;
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            group.blocksRaycasts = true;
            group.interactable = true;
            Ui.Fade(group, 1, 0.3f);
            ApplyLayout();
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
        Text tap, credit, subLine;
        RectTransform logo, subPill, creditPill;

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
            var sp = Ui.Panel(logo, new Vector2(860, 64), new Vector2(60, -178), Ui.Paper, null, 32f);
            sp.raycastTarget = false;
            subPill = sp.rectTransform;
            subLine = Ui.Label(subPill, "a tiny cloud helps a miniature world through its day", 34, Ui.Ink, new Vector2(840, 60), Vector2.zero, false, TextAnchor.MiddleCenter);
            tap = Ui.Label(safe, PromptFor(Platform.TouchFirst ? CloudInput.Device.Touch : CloudInput.Device.Mouse), 58, Color.white, new Vector2(800, 90), new Vector2(0, 150), true, TextAnchor.MiddleCenter, new Vector2(0.5f, 0));
            Ui.Outlined(tap, Res.Hex("6C7FCC"), 3);
            Debug.Log($"[PW] title prompt: {tap.text}");
            var hit = Ui.Image(root, null, new Color(0, 0, 0, 0), Vector2.zero, Vector2.zero, null, "Hit");
            hit.rectTransform.anchorMin = Vector2.zero; hit.rectTransform.anchorMax = Vector2.one;
            hit.raycastTarget = true;
            hit.transform.SetSiblingIndex(0);   // underneath everything, so the corner buttons get their taps
            var btn = hit.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => { Sfx.Ui("ui_pop"); OnPlay?.Invoke(); });
            var set = Ui.Button(safe, "", Ui.Lilac, new Vector2(100, 100), new Vector2(-80, 80), () => OnSettings?.Invoke(), "gear", new Vector2(1, 0), 40, "Settings");
            // desktop only: browsers and phones have their own way out
            if (!Application.isMobilePlatform && Application.platform != RuntimePlatform.WebGLPlayer)
                Ui.Button(safe, "Quit", Ui.Paper, new Vector2(170, 84), new Vector2(115, 80), () => { GameSettings.Save(); Application.Quit(); }, null, new Vector2(0, 0), 34, "Quit");
            firstSelected = btn.gameObject;
            var cp = Ui.Panel(safe, new Vector2(520, 46), new Vector2(0, 62), new Color(1, 1, 1, 0.7f), new Vector2(0.5f, 0), 23f, false);
            cp.raycastTarget = false;
            creditPill = cp.rectTransform;
            credit = Ui.Label(creditPill, "Mouse, touch, keyboard or gamepad", 26, Ui.Ink, new Vector2(500, 44), Vector2.zero, false, TextAnchor.MiddleCenter);
        }

        /// <summary>Narrow: the logo smaller (its subtitle line drawn bigger inside it, so it reads
        /// at the same size as elsewhere), and the credit and prompt above the corner buttons.</summary>
        protected override void Layout(MenuForm form)
        {
            bool narrow = form == MenuForm.Narrow;
            credit.fontSize = form == MenuForm.Usual ? 26 : 28;
            logo.localScale = Vector3.one * (narrow ? 0.7f : 1f);
            logo.anchoredPosition = new Vector2(0, narrow ? -200 : -230);
            subLine.fontSize = narrow ? 40 : 34;
            subPill.sizeDelta = narrow ? new Vector2(1080, 76) : new Vector2(860, 64);
            subPill.anchoredPosition = new Vector2(narrow ? 0 : 60, narrow ? -188 : -178);
            subLine.rectTransform.sizeDelta = narrow ? new Vector2(1060, 72) : new Vector2(840, 60);
            creditPill.sizeDelta = new Vector2(narrow ? 560 : 520, 46);
            creditPill.anchoredPosition = new Vector2(0, narrow ? 160 : 62);
            tap.rectTransform.anchoredPosition = new Vector2(0, narrow ? 260 : 150);
        }

        protected override void OnOpen()
        {
            Ui.PopIn(logo, 0.1f, 0.7f, Ui.MenuNarrow ? 0.7f : 1f);
        }

        /// <summary>The "... to play" prompt, in the words of the device in use.</summary>
        public string Prompt => tap.text;

        static string PromptFor(CloudInput.Device d) => d switch
        {
            CloudInput.Device.Touch => "Tap to play",
            CloudInput.Device.Keys => "Press Enter to play",
            CloudInput.Device.Pad => "Press A to play",
            _ => "Click to play",
        };

        float lastTouch = -10f;

        /// <summary>Starts as a guess (a finger on phones and tablets, a mouse elsewhere) and follows
        /// whatever the player touches next.</summary>
        void FollowDevice()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            var ts = UnityEngine.InputSystem.Touchscreen.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            CloudInput.Device? d = null;
            if (ts != null && ts.primaryTouch.press.isPressed) { d = CloudInput.Device.Touch; lastTouch = Clock.UnscaledTime; }
            else if (kb != null && kb.anyKey.wasPressedThisFrame) d = CloudInput.Device.Keys;
            else if (pad != null && (pad.wasUpdatedThisFrame && (pad.leftStick.ReadValue().magnitude > 0.5f || pad.dpad.ReadValue().magnitude > 0.5f ||
                     pad.buttonSouth.isPressed || pad.buttonEast.isPressed || pad.buttonWest.isPressed || pad.buttonNorth.isPressed || pad.startButton.isPressed)))
                d = CloudInput.Device.Pad;
            // browsers move the mouse pointer after a tap, so a finger doesn't count as a mouse
            else if (mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 9f || mouse.leftButton.wasPressedThisFrame) && Clock.UnscaledTime - lastTouch > 1f)
                d = CloudInput.Device.Mouse;
            if (d != null && tap.text != PromptFor(d.Value)) { tap.text = PromptFor(d.Value); Debug.Log($"[PW] title prompt: {tap.text}"); }
        }

        void Update()
        {
            if (!IsOpen || JustOpened) return;
            FollowDevice();
            float t = Clock.UnscaledTime;
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
        Text total, encoreTotal;
        RectTransform encorePill, stampsPill;
        RectTransform grid, back, gear;
        Text title, sub;

        protected override void Build()
        {
            Dim(0.28f);
            var safe = Ui.Stretch("Safe", root);
            safe.gameObject.AddComponent<SafeArea>();
            title = Ui.Label(safe, "Pocketvale", 96, Color.white, new Vector2(900, 120), new Vector2(0, -95), true, TextAnchor.MiddleCenter, new Vector2(0.5f, 1));
            Ui.Outlined(title, Res.Hex("7A8FD6"), 4);
            Ui.Shadowed(title, 8, 0.3f);
            sub = Ui.Label(safe, "One summer, twelve little days", 38, Color.white, new Vector2(900, 50), new Vector2(0, -170), false, TextAnchor.MiddleCenter, new Vector2(0.5f, 1));
            Ui.Shadowed(sub, 3, 0.4f);
            var tp = Ui.Panel(safe, new Vector2(230, 84), new Vector2(-150, -80), Ui.Paper, new Vector2(1, 1), 42f);
            stampsPill = tp.rectTransform;
            Ui.Icon(tp.transform, "stamp_flower", 64, new Vector2(-70, 2));
            total = Ui.Label(tp.transform, "0/36", 40, Ui.Ink, new Vector2(140, 70), new Vector2(30, 2), true);
            // Encore stamps get their own pill once the first Encore opens
            encorePill = Ui.Panel(safe, new Vector2(230, 72), new Vector2(-150, -176), Ui.Paper, new Vector2(1, 1), 36f, true, "EncorePill").rectTransform;
            Ui.Icon(encorePill, "stamp_encore", 56, new Vector2(-70, 2));
            encoreTotal = Ui.Label(encorePill, "0/12", 36, Ui.Ink, new Vector2(140, 64), new Vector2(30, 2), true);
            back = (RectTransform)Ui.Button(safe, "Title", Ui.Lilac, new Vector2(220, 90), new Vector2(150, 80), () => OnBack?.Invoke(), null, new Vector2(0, 0), 38).transform;
            gear = (RectTransform)Ui.Button(safe, "", Ui.Lilac, new Vector2(90, 90), new Vector2(-80, 80), () => OnSettings?.Invoke(), "gear", new Vector2(1, 0), 38, "Settings").transform;
            grid = Ui.Rect("Grid", safe, new Vector2(0.5f, 0.5f), new Vector2(1700, 700), new Vector2(0, -20));
        }

        /// <summary>Short: no subtitle, the cards a little smaller and the corner buttons lower, and
        /// the Encore pill beside the stamps pill instead of under it (where the cards now reach).
        /// Narrow: three cards a row, and the two pills side by side under the subtitle (the title
        /// fills the top).</summary>
        protected override void Layout(MenuForm form)
        {
            bool shortScreen = form == MenuForm.Short, narrow = form == MenuForm.Narrow;
            title.rectTransform.anchoredPosition = new Vector2(0, shortScreen ? -78 : -95);
            sub.gameObject.SetActive(!shortScreen);
            grid.localScale = Vector3.one * (shortScreen ? 0.88f : 1f);
            grid.anchoredPosition = new Vector2(0, shortScreen ? -24 : narrow ? -60 : -20);
            back.localScale = gear.localScale = Vector3.one * (shortScreen ? 0.9f : 1f);
            back.anchoredPosition = shortScreen ? new Vector2(125, 58) : new Vector2(150, 80);
            gear.anchoredPosition = shortScreen ? new Vector2(-70, 58) : new Vector2(-80, 80);
            var pillAnchor = narrow ? new Vector2(0.5f, 1f) : new Vector2(1f, 1f);
            stampsPill.anchorMin = stampsPill.anchorMax = encorePill.anchorMin = encorePill.anchorMax = pillAnchor;
            stampsPill.anchoredPosition = narrow ? new Vector2(-130, -262) : new Vector2(-150, -80);
            encorePill.anchoredPosition = narrow ? new Vector2(130, -262) : shortScreen ? new Vector2(-390, -80) : new Vector2(-150, -176);
            PlaceCards();
        }

        protected override void OnOpen()
        {
            foreach (Transform c in grid) Destroy(c.gameObject);
            cards.Clear();
            total.text = $"{SaveData.TotalStamps()}/{LevelLibrary.Campaign.Length * 3}";
            bool anyEncore = SaveData.EncoreUnlocked(LevelLibrary.Campaign[0]);
            encorePill.gameObject.SetActive(anyEncore);
            encoreTotal.text = $"{SaveData.EncoreStamps()}/{LevelLibrary.Campaign.Length}";
            int next = SaveData.FirstUnfinished();
            for (int i = 0; i < LevelLibrary.Campaign.Length; i++)
            {
                int idx = i;
                var pos = CardPos(i, Columns);
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
                string[] stampIcons = { "stamp_sun", "stamp_clock", "stamp_flower", "stamp_encore" };
                int count = unlocked && SaveData.EncoreUnlocked(LevelLibrary.Campaign[i]) ? 4 : 3;
                for (int s = 0; s < count; s++)
                {
                    bool has = (save.stamps & (1 << s)) != 0;
                    var st = Ui.Icon(face, has ? stampIcons[s] : "stamp_empty", count == 4 ? 46 : 52, new Vector2((s - (count - 1) / 2f) * (count == 4 ? 52 : 58), -118));
                    if (!has) st.color = new Color(1, 1, 1, 0.6f);
                }
                if (i == next && unlocked) card.gameObject.AddComponent<UiBob>().amplitude = 5;
                card.interactable = true;
                cards.Add(card);
                Ui.PopIn(card.transform, 0.03f * i, 0.4f);
            }
            firstSelected = cards[Mathf.Clamp(next, 0, cards.Count - 1)].gameObject;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(firstSelected);
            laidOutColumns = Columns;
        }

        int laidOutColumns;

        /// <summary>Six cards a row in landscape, four in portrait (the narrower design width), three
        /// on a phone held upright.</summary>
        static int Columns => Ui.MenuNarrow ? 3 : Ui.Portrait ? 4 : 6;

        static Vector2 CardPos(int i, int cols)
        {
            int rows = (LevelLibrary.Campaign.Length + cols - 1) / cols;
            int col = i % cols, row = i / cols;
            return new Vector2((col - (cols - 1) / 2f) * 272, ((rows - 1) / 2f - row) * 330);
        }

        void PlaceCards()
        {
            if (laidOutColumns == Columns) return;
            laidOutColumns = Columns;
            for (int i = 0; i < cards.Count; i++)
            {
                var rt = (RectTransform)cards[i].transform;
                rt.anchoredPosition = CardPos(i, laidOutColumns);
                rt.GetComponent<UiBob>()?.Rebase();
            }
        }

        void Update()
        {
            if (IsOpen) PlaceCards();
            if (!IsOpen || JustOpened) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame)) OnBack?.Invoke();
        }
    }

    // ===================================================================== Postcard (level intro)
    public class Postcard : MenuScreen
    {
        public Action OnStart;
        /// <summary>The Encore / Normal day button: true asks for the Encore.</summary>
        public Action<bool> OnEncore;
        RectTransform card;
        Text day, title, story, parText, best;
        RectTransform needsRow, stampsRow, parRow, helpLabel;
        Image icon, stripe, parIcon;
        JuicyButton startBtn, encoreBtn;
        bool showingEncore;
        public const string RelaxedParLine = "Relaxed day: no par stamp";
        public const string EncoreStory = "A scorcher! The sun races, the beds dry out as you watch, and Pip sets off half-empty.";

        protected override void Build()
        {
            Dim(0.25f);
            card = Ui.Panel(root, new Vector2(1000, 640), new Vector2(0, 10), Ui.Paper, null, 48f).rectTransform;
            stripe = Ui.Image(card, Ui.Rounded, Res.Hex("CFE6F7"), new Vector2(960, 120), new Vector2(0, 240), null, "Stripe");
            stripe.pixelsPerUnitMultiplier = 52f / 36f;
            icon = Ui.Icon(card, "flower", 150, new Vector2(-370, 245));
            day = Ui.Label(card, "Day 1", 34, Ui.InkSoft, new Vector2(600, 50), new Vector2(40, 272), true, TextAnchor.MiddleLeft);
            title = Ui.Label(card, "First Drops", 66, Ui.Ink, new Vector2(700, 80), new Vector2(90, 222), true, TextAnchor.MiddleLeft);
            story = Ui.Label(card, "", 36, Ui.Ink, new Vector2(860, 140), new Vector2(0, 100), false);
            helpLabel = Ui.Label(card, "Today, help:", 30, Ui.InkSoft, new Vector2(400, 40), new Vector2(0, 22), true).rectTransform;
            needsRow = Ui.Rect("Needs", card, new Vector2(0.5f, 0.5f), new Vector2(900, 110), new Vector2(0, -60));
            parRow = Ui.Rect("Par", card, new Vector2(0.5f, 0.5f), new Vector2(600, 60), new Vector2(-170, -175));
            parIcon = Ui.Icon(parRow, "clock", 54, new Vector2(-250, 0));
            parText = Ui.Label(parRow, "", 32, Ui.Ink, new Vector2(500, 60), new Vector2(40, 0), true, TextAnchor.MiddleLeft);
            stampsRow = Ui.Rect("Stamps", card, new Vector2(0.5f, 0.5f), new Vector2(240, 70), new Vector2(-330, -245));
            best = Ui.Label(card, "", 30, Ui.InkSoft, new Vector2(330, 50), new Vector2(-30, -245), true);
            startBtn = Ui.Button(card, "Start", Ui.Coral, new Vector2(300, 104), new Vector2(300, -240), () => OnStart?.Invoke(), null, null, 50);
            // a saved day offers its Encore (and an Encore offers the ordinary day back)
            encoreBtn = Ui.Button(card, "Encore", Res.Hex("FF9A5C"), new Vector2(260, 64), new Vector2(300, -150), () => OnEncore?.Invoke(!showingEncore), "stamp_encore", null, 32, "Encore");
            firstSelected = startBtn.gameObject;
        }

        /// <summary>Narrow: a taller card, the story wrapped over more lines, and the par line, best
        /// time, stamps and buttons each on a row of their own.</summary>
        protected override void Layout(MenuForm form)
        {
            bool n = form == MenuForm.Narrow;
            card.sizeDelta = n ? new Vector2(840, 1010) : new Vector2(1000, 640);
            stripe.rectTransform.sizeDelta = new Vector2(n ? 800 : 960, 120);
            stripe.rectTransform.anchoredPosition = new Vector2(0, n ? 425 : 240);
            icon.rectTransform.anchoredPosition = n ? new Vector2(-300, 430) : new Vector2(-370, 245);
            day.rectTransform.sizeDelta = new Vector2(n ? 560 : 600, 50);
            day.rectTransform.anchoredPosition = n ? new Vector2(60, 457) : new Vector2(40, 272);
            title.rectTransform.sizeDelta = new Vector2(n ? 600 : 700, 80);
            title.rectTransform.anchoredPosition = n ? new Vector2(80, 407) : new Vector2(90, 222);
            story.rectTransform.sizeDelta = n ? new Vector2(760, 200) : new Vector2(860, 140);
            story.rectTransform.anchoredPosition = new Vector2(0, n ? 245 : 100);
            helpLabel.anchoredPosition = new Vector2(0, n ? 115 : 22);
            needsRow.anchoredPosition = new Vector2(0, n ? 30 : -60);
            parRow.anchoredPosition = n ? new Vector2(30, -85) : new Vector2(-170, -175);
            best.rectTransform.sizeDelta = new Vector2(n ? 600 : 330, 50);
            best.rectTransform.anchoredPosition = n ? new Vector2(0, -150) : new Vector2(-30, -245);
            stampsRow.anchoredPosition = n ? new Vector2(0, -225) : new Vector2(-330, -245);
            PlaceButtons();
        }

        /// <summary>Narrow: Start and Encore side by side along the bottom, or Start in the middle
        /// when there's no Encore yet.</summary>
        void PlaceButtons()
        {
            bool n = Ui.MenuNarrow, both = encoreBtn.gameObject.activeSelf;
            ((RectTransform)startBtn.transform).anchoredPosition = n ? new Vector2(both ? 175 : 0, -405) : new Vector2(300, -240);
            ((RectTransform)encoreBtn.transform).anchoredPosition = n ? new Vector2(-185, -405) : new Vector2(300, -150);
        }

        public void Show(LevelDef def, int index)
        {
            showingEncore = def.encore;
            day.text = def.encore ? $"Day {index + 1}  ·  Encore" : $"Day {index + 1}";
            title.text = def.title;
            story.text = def.encore ? EncoreStory : def.story;
            icon.sprite = Ui.IconSprite(string.IsNullOrEmpty(def.icon) ? "flower" : def.icon);
            stripe.color = def.encore ? Res.Hex("FFD9B8") : Res.Hex("CFE6F7");
            // Settings > Relaxed days: a slower sun, and the par stamp kept for the usual pace
            bool relaxed = !def.encore && GameSettings.RelaxedDays;
            parIcon.sprite = Ui.IconSprite(def.encore ? "stamp_encore" : relaxed ? "snail" : "clock");
            parText.text = def.encore ? "The scorcher stamp for saving it" : relaxed ? RelaxedParLine : $"Stamp for finishing before {FormatHour(def.par)}";
            bool encoreOpen = SaveData.EncoreUnlocked(def.id);
            encoreBtn.gameObject.SetActive(encoreOpen);
            PlaceButtons();
            encoreBtn.SetLabel(def.encore ? "Normal day" : "Encore");
            // an Encore's postcard shows the scorcher's own best, never the ordinary day's
            var rec = SaveData.Get(def.id);
            best.text = def.encore ? (rec.HasEncoreBest ? $"Scorcher best: {FormatHour(rec.encoreBest)}" : "")
                      : rec.bestHour < 90f ? $"Your best: {FormatHour(rec.bestHour)}" : "";
            foreach (Transform c in needsRow) Destroy(c.gameObject);
            var reqs = new List<NeedDef>();
            foreach (var n in def.needs) if (!n.hidden) reqs.Add(n);
            // held upright the card is narrower: a long row (the wedding's seven) draws closer and smaller
            float step = Mathf.Min(124f, (Ui.MenuNarrow ? 780f : 900f) / Mathf.Max(1, reqs.Count));
            for (int i = 0; i < reqs.Count; i++)
            {
                var it = Ui.Rect("N" + i, needsRow, new Vector2(0.5f, 0.5f), new Vector2(110, 110), new Vector2((i - (reqs.Count - 1) / 2f) * step, 0));
                Ui.Image(it, Ui.Circle, Res.Hex("F1E8DA"), new Vector2(104, 104), Vector2.zero, null, "Bg");
                Ui.Icon(it, NeedIcon(reqs[i]), 74, new Vector2(0, 2));
                Ui.PopIn(it, 0.25f + 0.07f * i, 0.4f, step / 124f);
            }
            foreach (Transform c in stampsRow) Destroy(c.gameObject);
            var save = SaveData.Get(def.id);
            string[] icons = { "stamp_sun", "stamp_clock", "stamp_flower", "stamp_encore" };
            int count = encoreOpen ? 4 : 3;
            for (int s = 0; s < count; s++)
            {
                bool has = (save.stamps & (1 << s)) != 0;
                var st = Ui.Icon(stampsRow, has ? icons[s] : "stamp_empty", count == 4 ? 54 : 62, new Vector2((s - (count - 1) / 2f) * (count == 4 ? 60 : 72), 0));
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
            int total = Mathf.RoundToInt(h * 60f);   // round once, so 9:59.7 shows as 10:00, not 9:60
            return $"{total / 60}:{total % 60:00}";
        }

        void Update()
        {
            if (!IsOpen || JustOpened) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            // Enter/Space start the day unless the Encore button has the focus (then it's the
            // button's own submit); the pad's Start button always starts
            var sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            bool encoreFocused = sel != null && encoreBtn != null && sel == encoreBtn.gameObject;
            if ((kb != null && !encoreFocused && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) || (pad != null && pad.startButton.wasPressedThisFrame))
                OnStart?.Invoke();
        }
    }

    // ===================================================================== Pause
    public class PauseMenu : MenuScreen
    {
        public Action OnResume, OnRestart, OnMap, OnSettings;
        Text delight, controls;

        /// <summary>A one-line reminder of the controls for whatever the player is using.</summary>
        public static string ControlsLine(CloudInput.Device d)
        {
            if (d == CloudInput.Device.None || d == CloudInput.Device.Virtual)
                d = Platform.TouchFirst ? CloudInput.Device.Touch : CloudInput.Device.Mouse;
            bool tap = GameSettings.RainToggle;
            return d switch
            {
                CloudInput.Device.Touch => tap ? "Drag: fly  ·  hold still: rain on/off  ·  flick: blow" : "Drag: fly  ·  hold still: rain  ·  flick: blow",
                CloudInput.Device.Keys => tap ? "WASD / arrows: fly  ·  Space: rain on/off  ·  E: blow" : "WASD / arrows: fly  ·  hold Space: rain  ·  E: blow",
                CloudInput.Device.Pad => tap ? "Left stick: fly  ·  A: rain on/off  ·  X: blow (right stick aims)" : "Left stick: fly  ·  hold A: rain  ·  X: blow (right stick aims)",
                _ => tap ? "Point: fly  ·  click: rain on/off  ·  right-drag: blow" : "Point: fly  ·  hold left button: rain  ·  right-drag: blow",
            } + "  ·  hover over water: drink";
        }

        protected override void Build()
        {
            Dim(0.5f);
            panel = Ui.Panel(root, new Vector2(620, 720), Vector2.zero, Ui.Paper, null, 48f).rectTransform;
            heading = Ui.Label(panel, "Paused", 76, Ui.Ink, new Vector2(560, 100), new Vector2(0, 282), true).rectTransform;
            var r = Ui.Button(panel, "Resume", Ui.Coral, new Vector2(420, 100), new Vector2(0, 160), () => OnResume?.Invoke(), null, null, 46);
            buttons = new[]
            {
                (RectTransform)r.transform,
                (RectTransform)(restartBtn = Ui.Button(panel, "Restart", Ui.Sky, new Vector2(420, 96), new Vector2(0, 40), () => Confirm(restartBtn, OnRestart), null, null, 42)).transform,
                (RectTransform)Ui.Button(panel, "Settings", Ui.Mint, new Vector2(420, 96), new Vector2(0, -76), () => OnSettings?.Invoke(), null, null, 42).transform,
                (RectTransform)(mapBtn = Ui.Button(panel, "Map", Ui.Lilac, new Vector2(420, 96), new Vector2(0, -192), () => Confirm(mapBtn, OnMap), null, null, 42)).transform,
            };
            delight = Ui.Label(panel, "", 30, Ui.InkSoft, new Vector2(560, 90), new Vector2(0, -300), false);
            strip = Ui.Panel(root, new Vector2(1500, 76), new Vector2(0, -425), new Color(1, 1, 1, 0.86f), null, 38f, false).rectTransform;
            strip.GetComponent<Image>().raycastTarget = false;
            controls = Ui.Label(strip, "", 28, Ui.Ink, new Vector2(1460, 70), Vector2.zero, false, TextAnchor.MiddleCenter);
            firstSelected = r.gameObject;
        }

        RectTransform strip, panel, heading;
        RectTransform[] buttons;
        bool shortLayout;
        JuicyButton restartBtn, mapBtn, armed;

        /// <summary>Restart and Map throw away the day: once it's been played a while (set by
        /// GameFlow as the menu opens), the first press asks "Sure?" and only the second goes.</summary>
        public bool AskFirst;
        public string RestartLabel => restartBtn.Label;

        void Confirm(JuicyButton b, Action go)
        {
            if (AskFirst && armed != b) { Arm(b); return; }
            Arm(null);
            go?.Invoke();
        }

        /// <summary>Turns one of Restart and Map to "Sure?" (null puts both back).</summary>
        public void Arm(JuicyButton b)
        {
            armed = b;
            restartBtn.SetLabel(b == restartBtn ? "Sure?" : "Restart");
            mapBtn.SetLabel(b == mapBtn ? "Sure?" : "Map");
        }
        public void DebugArmRestart()
        {
            EventSystem.current?.SetSelectedGameObject(restartBtn.gameObject);
            Arm(restartBtn);
        }

        protected override void OnOpen() => Arm(null);

        /// <summary>Short: a wide panel with the four buttons in a 2x2 grid, and the controls line
        /// close under it.</summary>
        protected override void Layout(MenuForm form)
        {
            bool shortScreen = form == MenuForm.Short;
            shortLayout = shortScreen;
            panel.sizeDelta = shortScreen ? new Vector2(1000, 540) : new Vector2(620, 720);
            panel.anchoredPosition = new Vector2(0, shortScreen ? 40 : 0);
            heading.anchoredPosition = new Vector2(0, shortScreen ? 200 : 282);
            float[] y = { 160, 40, -76, -192 };
            for (int i = 0; i < buttons.Length; i++)
                buttons[i].anchoredPosition = shortScreen ? new Vector2(i % 2 == 0 ? -220 : 220, i < 2 ? 80 : -40) : new Vector2(0, y[i]);
            delight.rectTransform.sizeDelta = shortScreen ? new Vector2(900, 80) : new Vector2(560, 90);
            delight.rectTransform.anchoredPosition = new Vector2(0, shortScreen ? -170 : -300);
            PlaceStrip();
        }

        /// <summary>The controls line wraps onto two lines in portrait's narrower design width (and
        /// in a narrower strip on a phone held upright).</summary>
        public void SetControls(CloudInput.Device device)
        {
            controls.text = ControlsLine(device);
            PlaceStrip();
        }

        void PlaceStrip()
        {
            bool p = Ui.Portrait, n = Ui.MenuNarrow;
            strip.sizeDelta = n ? new Vector2(820, 150) : p ? new Vector2(1140, 116) : new Vector2(1500, 76);
            strip.anchoredPosition = new Vector2(0, n ? -470 : p ? -445 : shortLayout ? -320 : -425);
            controls.rectTransform.sizeDelta = n ? new Vector2(780, 144) : p ? new Vector2(1090, 110) : new Vector2(1460, 70);
        }
        public string ControlsText => controls.text;

        /// <summary>The controller in use dropped out: the controls line says so instead.</summary>
        public void SetControllerLost()
        {
            controls.text = ControllerLostLine;
            PlaceStrip();
        }
        public const string ControllerLostLine = "Controller disconnected: reconnect it, or carry on with the mouse, keys or touch";

        public void SetDelight(LevelDef def, bool found)
        {
            if (def == null || def.delight == null || string.IsNullOrEmpty(def.delight.type)) { delight.text = ""; return; }
            delight.text = found ? $"Delight found: {def.delight.title}" : $"Secret delight: {def.delight.hint}";
        }

        void Update()
        {
            if (!IsOpen || JustOpened) return;
            // moving the selection off the button that asked "Sure?" takes the question back
            if (armed != null && EventSystem.current != null && EventSystem.current.currentSelectedGameObject != armed.gameObject) Arm(null);
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
        JuicyButton resetBtn, touchButton;
        UnityEngine.UI.Slider gfxSlider;
        /// <summary>The graphics slider (for the self-tests).</summary>
        public UnityEngine.UI.Slider GraphicsSlider => gfxSlider;
        RectTransform panel;
        /// <summary>Each row with its place in the usual layout (one column) and the short one (two).</summary>
        readonly List<(RectTransform rt, Vector2 usual, Vector2 shortPos)> rows = new();
        UnityEngine.UI.Slider musicSlider;
        UnityEngine.UI.Toggle fullscreenToggle, relaxedToggle;
        bool confirmReset;
        public const string RelaxedLabel = "Relaxed days (slower sun)";

        static string TouchButtonsLabel() => GameSettings.TouchButtons switch { 1 => "On", 2 => "Off", _ => "Auto" };

        protected override void Build()
        {
            Dim(0.55f);
            var p = Ui.Panel(root, new Vector2(760, 1060), Vector2.zero, Ui.Paper, null, 48f);
            panel = p.rectTransform;
            Ui.Label(p.transform, "Settings", 70, Ui.Ink, new Vector2(600, 90), new Vector2(0, 455), true);
            float y = 362;
            musicSlider = Ui.Slider(p.transform, "Music", GameSettings.Music, new Vector2(0, y), v => { GameSettings.Music = v; }, 640); y -= 76;
            Ui.Slider(p.transform, "Sounds", GameSettings.Sfx, new Vector2(0, y), v => { GameSettings.Sfx = v; }, 640); y -= 76;
            Ui.Slider(p.transform, "Ambience", GameSettings.Ambience, new Vector2(0, y), v => { GameSettings.Ambience = v; }, 640); y -= 76;
            Ui.Slider(p.transform, "Tilt-shift", GameSettings.TiltShift, new Vector2(0, y), v => { GameSettings.TiltShift = v; PostFx.ApplySettings(); }, 640); y -= 78;
            // graphics fidelity: Auto, Low, Medium, High, Ultra (see Quality)
            gfxSlider = Ui.StepSlider(p.transform, "Graphics", Quality.SliderModes.Length, Quality.SliderIndex((Quality.Mode)GameSettings.Graphics), new Vector2(0, y), i =>
            {
                var mode = Quality.SliderModes[i];
                GameSettings.Graphics = (int)mode;
                if (mode == Quality.Mode.Auto) Quality.ResetAuto(); else Quality.Apply();
            }, i => Quality.ModeName(Quality.SliderModes[i]), 640);
            y -= 76;
            Ui.Toggle(p.transform, "Screen shake", GameSettings.ScreenShake, new Vector2(0, y), v => GameSettings.ScreenShake = v, 640); y -= 71;
            Ui.Toggle(p.transform, "Hints", GameSettings.Hints, new Vector2(0, y), v => GameSettings.Hints = v, 640); y -= 71;
            // touch buttons: tap to cycle Auto (when touch is used) / On / Off
            var touchRow = Ui.Rect("Row_TouchButtons", p.transform, new Vector2(0.5f, 0.5f), new Vector2(640, 76), new Vector2(0, y));
            Ui.Label(touchRow, "Touch buttons", 34, Ui.Ink, new Vector2(330, 60), new Vector2(-155, 0), false, TextAnchor.MiddleLeft);
            touchButton = Ui.Button(touchRow, TouchButtonsLabel(), Ui.Sky, new Vector2(230, 66), new Vector2(205, 0), () =>
            {
                GameSettings.TouchButtons = (GameSettings.TouchButtons + 1) % 3;
                touchButton.SetLabel(TouchButtonsLabel());
            }, null, null, 30, "TouchButtons");
            y -= 71;
            Ui.Toggle(p.transform, "Tap to rain (no holding)", GameSettings.RainToggle, new Vector2(0, y), v => GameSettings.RainToggle = v, 640); y -= 71;
            fullscreenToggle = Ui.Toggle(p.transform, "Fullscreen", UnityEngine.Screen.fullScreen, new Vector2(0, y), v =>
            {
                UnityEngine.Screen.fullScreenMode = v ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            }, 640);
            y -= 71;
            // a slower sun on ordinary days, for anyone who keeps running out of daylight (the sunset
            // card offers it too, from a day's second sunset)
            relaxedToggle = Ui.Toggle(p.transform, RelaxedLabel, GameSettings.RelaxedDays, new Vector2(0, y), v => GameSettings.RelaxedDays = v, 640);
            y -= 88;
            resetBtn = Ui.Button(p.transform, "Reset progress", Ui.Coral, new Vector2(300, 84), new Vector2(-170, y), () =>
            {
                if (!confirmReset) { confirmReset = true; resetBtn.SetLabel("Sure?"); return; }
                SaveData.Reset();
                confirmReset = false;
                resetBtn.SetLabel("Done!");
            }, null, null, 32);
            var done = Ui.Button(p.transform, "Done", Ui.Mint, new Vector2(280, 92), new Vector2(170, y), () => { GameSettings.Save(); OnClose?.Invoke(); }, null, null, 42);
            firstSelected = done.gameObject;
            // the short layout: sliders and graphics on the left, the switches on the right, the
            // buttons along the bottom (the panel's children, in the order they were made)
            Vector2[] shortPos =
            {
                new(0, 295),
                new(-350, 205), new(-350, 129), new(-350, 53), new(-350, -23), new(-350, -99),
                new(350, 205), new(350, 135), new(350, 65), new(350, -5), new(350, -75), new(350, -145),
                new(-200, -262), new(200, -262),
            };
            if (p.transform.childCount != shortPos.Length) Debug.LogWarning($"[PW] settings has {p.transform.childCount} rows, the short layout places {shortPos.Length}");
            for (int i = 0; i < p.transform.childCount && i < shortPos.Length; i++)
            {
                var rt = (RectTransform)p.transform.GetChild(i);
                rows.Add((rt, rt.anchoredPosition, shortPos[i]));
            }
        }

        /// <summary>Short: two columns. Narrow keeps the usual single column, which fits.</summary>
        protected override void Layout(MenuForm form)
        {
            bool shortScreen = form == MenuForm.Short;
            panel.sizeDelta = shortScreen ? new Vector2(1440, 720) : new Vector2(760, 1060);
            foreach (var (rt, usual, shortPos) in rows) rt.anchoredPosition = shortScreen ? shortPos : usual;
        }

        protected override void OnOpen()
        {
            confirmReset = false;
            resetBtn.SetLabel("Reset progress");
            gfxSlider.SetValueWithoutNotify(Quality.SliderIndex((Quality.Mode)GameSettings.Graphics));
            touchButton.SetLabel(TouchButtonsLabel());
            musicSlider.SetValueWithoutNotify(GameSettings.Music);   // M may have muted it since
            fullscreenToggle.SetIsOnWithoutNotify(UnityEngine.Screen.fullScreen);   // and the browser, a phone's first tap or Esc may have changed this
            relaxedToggle.SetIsOnWithoutNotify(GameSettings.RelaxedDays);   // and the sunset card's "Slower sun"
        }

        void Update()
        {
            if (!IsOpen || JustOpened) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame)) { GameSettings.Save(); OnClose?.Invoke(); }
        }
    }

    // ===================================================================== Results
    public class ResultsCard : MenuScreen
    {
        public Action OnNext, OnReplay, OnMap;
        RectTransform card;
        Text title, subtitle, timeLine;
        readonly Image[] stamps = new Image[3];
        readonly Text[] stampLabels = new Text[3];
        readonly RectTransform[] slots = new RectTransform[3], plates = new RectTransform[3], buttons = new RectTransform[3];
        RectTransform banner;
        JuicyButton next;

        protected override void Build()
        {
            Dim(0.3f);
            card = Ui.Panel(root, new Vector2(1060, 640), new Vector2(0, 0), Ui.Paper, null, 48f).rectTransform;
            var bn = Ui.Image(card, Ui.Rounded, Ui.Butter, new Vector2(700, 120), new Vector2(0, 300), null, "Banner");
            bn.pixelsPerUnitMultiplier = 52f / 40f;
            banner = bn.rectTransform;
            title = Ui.Label(banner, "Day saved!", 74, Color.white, new Vector2(680, 110), new Vector2(0, 2), true);
            Ui.Outlined(title, Res.Hex("E09A3A"), 4);
            subtitle = Ui.Label(card, "", 36, Ui.Ink, new Vector2(960, 50), new Vector2(0, 200), false);
            timeLine = Ui.Label(card, "", 28, Ui.InkSoft, new Vector2(960, 40), new Vector2(0, 158), false);
            string[] labels = { "Helped everyone", "Before 12:00", "Secret delight" };
            for (int i = 0; i < 3; i++)
            {
                var slot = Ui.Rect("Slot" + i, card, new Vector2(0.5f, 0.5f), new Vector2(300, 260), new Vector2((i - 1) * 320, 0));
                slots[i] = slot;
                plates[i] = Ui.Image(slot, Ui.Circle, Res.Hex("EFE6D8"), new Vector2(190, 190), new Vector2(0, 30), null, "Plate").rectTransform;
                stamps[i] = Ui.Icon(slot, "stamp_empty", 180, new Vector2(0, 30));
                stampLabels[i] = Ui.Label(slot, labels[i], 30, Ui.Ink, new Vector2(300, 70), new Vector2(0, -100), true);
            }
            buttons[0] = (RectTransform)Ui.Button(card, "Map", Ui.Lilac, new Vector2(230, 96), new Vector2(-330, -235), () => OnMap?.Invoke(), null, null, 42).transform;
            buttons[1] = (RectTransform)Ui.Button(card, "Replay", Ui.Sky, new Vector2(250, 96), new Vector2(-50, -235), () => OnReplay?.Invoke(), null, null, 42).transform;
            next = Ui.Button(card, "Next day", Ui.Coral, new Vector2(300, 104), new Vector2(270, -235), () => OnNext?.Invoke(), null, null, 46);
            buttons[2] = (RectTransform)next.transform;
            firstSelected = next.gameObject;
            // the first time a day is saved, its Encore opens: say so, or a player only finds it by
            // going back to a saved day's postcard
            var nt = Ui.Image(card, Ui.Rounded, Res.Hex("FFEBD9"), new Vector2(980, 56), new Vector2(0, -160), null, "EncoreNote");
            nt.pixelsPerUnitMultiplier = 52f / 24f;
            note = nt.rectTransform;
            noteIcon = Ui.Icon(note, "stamp_encore", 46, new Vector2(-415, 0)).rectTransform;
            noteText = Ui.Label(note, EncoreNote, 28, Ui.Ink, new Vector2(800, 52), new Vector2(25, 0), false);
            note.gameObject.SetActive(false);
        }

        public const string EncoreNote = "Encore unlocked! Play this day as a scorcher from its postcard.";
        RectTransform note, noteIcon;
        Text noteText;
        bool noteShown;
        /// <summary>The Encore note is showing (for the self-tests).</summary>
        public bool EncoreNoteShown => IsOpen && noteShown;

        /// <summary>Narrow: a taller card, its text wrapping, the stamps closer together and smaller,
        /// and the buttons in a row across the bottom.</summary>
        protected override void Layout(MenuForm form)
        {
            bool n = form == MenuForm.Narrow;
            // the Encore note makes the card 60 units taller: everything above it moves up 30, the
            // buttons down 30, and the note sits between the stamps and the buttons
            float dy = noteShown ? 30f : 0f;
            card.sizeDelta = (n ? new Vector2(840, 760) : new Vector2(1060, 640)) + new Vector2(0, 2 * dy);
            banner.anchoredPosition = new Vector2(0, (n ? 360 : 300) + dy);
            banner.sizeDelta = new Vector2(n ? 640 : 700, 120);
            subtitle.rectTransform.sizeDelta = n ? new Vector2(780, 100) : new Vector2(960, 50);
            subtitle.rectTransform.anchoredPosition = new Vector2(0, (n ? 235 : 200) + dy);
            timeLine.rectTransform.sizeDelta = new Vector2(n ? 780 : 960, 40);
            timeLine.rectTransform.anchoredPosition = new Vector2(0, (n ? 165 : 158) + dy);
            for (int i = 0; i < 3; i++)
            {
                slots[i].sizeDelta = new Vector2(n ? 250 : 300, 260);
                slots[i].anchoredPosition = new Vector2((i - 1) * (n ? 262 : 320), (n ? 10 : 0) + dy);
                plates[i].sizeDelta = Vector2.one * (n ? 170 : 190);
                stamps[i].rectTransform.sizeDelta = Vector2.one * (n ? 160 : 180);
                stampLabels[i].rectTransform.sizeDelta = new Vector2(n ? 250 : 300, 70);
            }
            buttons[0].anchoredPosition = n ? new Vector2(-290, -285 - dy) : new Vector2(-330, -235 - dy);
            buttons[1].anchoredPosition = n ? new Vector2(-40, -285 - dy) : new Vector2(-50, -235 - dy);
            buttons[2].anchoredPosition = n ? new Vector2(245, -285 - dy) : new Vector2(270, -235 - dy);
            // narrow, the note wraps onto two lines in a taller pill
            note.sizeDelta = n ? new Vector2(760, 96) : new Vector2(980, 56);
            note.anchoredPosition = new Vector2(0, n ? -175 : -160);
            noteIcon.anchoredPosition = new Vector2(n ? -322 : -450, 0);
            noteText.rectTransform.sizeDelta = n ? new Vector2(640, 92) : new Vector2(900, 52);
            noteText.rectTransform.anchoredPosition = new Vector2(n ? 35 : 20, 0);
            buttons[0].sizeDelta = new Vector2(n ? 190 : 230, 96);
            buttons[1].sizeDelta = new Vector2(n ? 210 : 250, 96);
            buttons[2].sizeDelta = new Vector2(n ? 260 : 300, 104);
        }

        static string TimeLine(float finishHour, float previousBest) =>
            previousBest > 90f ? $"Finished at {Postcard.FormatHour(finishHour)}"
            : finishHour < previousBest - 1f / 120f ? $"Finished at {Postcard.FormatHour(finishHour)}  ·  a new best!"
            : $"Finished at {Postcard.FormatHour(finishHour)}  ·  your best is {Postcard.FormatHour(previousBest)}";

        /// <param name="previousBest">the best finishing hour before this run (99 if never finished)</param>
        void ShowNote(bool show)
        {
            noteShown = show;
            note.gameObject.SetActive(show);
            Layout(Ui.MenuLayout);
        }

        /// <summary>The par stamp's label on a relaxed day that hasn't earned it before.</summary>
        public const string RelaxedParLabel = "Usual pace only";

        /// <param name="relaxed">the day ran relaxed: no par stamp or best time this time, and the card says so</param>
        public void Show(LevelDef def, int stampsEarnedThisRun, int fresh, float finishHour, bool isLast, float previousBest = 99f, bool relaxed = false)
        {
            title.text = "Day saved!";
            ShowNote((fresh & SaveData.StampSaved) != 0);
            for (int i = 0; i < 3; i++) stamps[i].transform.parent.gameObject.SetActive(true);
            subtitle.text = string.IsNullOrEmpty(def.thanks) ? "Everyone is happy!" : def.thanks;
            timeLine.text = relaxed ? $"Finished at {Postcard.FormatHour(finishHour)} on a relaxed day" : TimeLine(finishHour, previousBest);
            stampLabels[1].text = relaxed && !SaveData.Has(def.id, SaveData.StampPar) ? RelaxedParLabel : $"Before {Postcard.FormatHour(def.par)}";
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

        /// <summary>An Encore's card: one stamp, the scorcher's, slammed in if it's new.</summary>
        /// <param name="previousBest">the Encore's best finishing hour before this run (99 if never saved)</param>
        public void ShowEncore(LevelDef def, bool fresh, float finishHour, float previousBest = 99f)
        {
            title.text = "Encore saved!";
            ShowNote(false);
            subtitle.text = "Even on a scorcher, everyone is happy!";
            timeLine.text = TimeLine(finishHour, previousBest);
            for (int i = 0; i < 3; i++) stamps[i].transform.parent.gameObject.SetActive(i == 1);
            stampLabels[1].text = "Scorcher saved";
            int index = LevelLibrary.IndexOf(def.id);
            next.SetLabel(index >= LevelLibrary.Campaign.Length - 1 ? "Map" : "Next day");
            var st = stamps[1];
            st.sprite = Ui.IconSprite(fresh ? "stamp_empty" : "stamp_encore");
            st.color = fresh ? new Color(1, 1, 1, 0.55f) : Color.white;
            st.rectTransform.localScale = Vector3.one;
            Open();
            Tween.To(700, 0, 0.55f, y => card.anchoredPosition = new Vector2(0, y), k => Ease.OutBack(k, 1.2f), 0, null, card);
            if (!fresh) return;
            Tween.Delay(0.6f, () =>
            {
                st.sprite = Ui.IconSprite("stamp_encore");
                st.color = Color.white;
                var t = st.rectTransform;
                Tween.To(2.4f, 1f, 0.32f, k => { if (t != null) t.localScale = Vector3.one * k; }, Ease.InCubic, 0, () =>
                {
                    Sfx.Ui("stamp");
                    Tween.Punch(card, 0.03f, 0.3f);
                    GameRoot.Instance?.Rig.Shake(0.4f);
                });
                t.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(-12f, 12f));
            });
        }
    }

    // ===================================================================== Sunset (fail)
    public class FailCard : MenuScreen
    {
        public Action OnRetry, OnMap, OnSlower;
        RectTransform card, row, heading, subLine, tipPill, mapBtn, retryBtn, slowerBtn;
        Text tip;
        bool offerSlower;
        /// <summary>The "Slower sun" offer is showing (for the self-tests).</summary>
        public bool OfferingSlower => IsOpen && offerSlower;
        public const string SlowerLabel = "Slower sun";

        protected override void Build()
        {
            Dim(0.4f);
            card = Ui.Panel(root, new Vector2(980, 640), Vector2.zero, Ui.Paper, null, 48f).rectTransform;
            heading = Ui.Label(card, "The sun has set", 70, Ui.Ink, new Vector2(900, 90), new Vector2(0, 235), true).rectTransform;
            subLine = Ui.Label(card, "Some friends still needed you. Try again tomorrow!", 36, Ui.InkSoft, new Vector2(900, 50), new Vector2(0, 160), false).rectTransform;
            row = Ui.Rect("Row", card, new Vector2(0.5f, 0.5f), new Vector2(800, 120), new Vector2(0, 55));
            var tp = Ui.Image(card, Ui.Rounded, Res.Hex("EAF4FB"), new Vector2(880, 96), new Vector2(0, -78), null, "TipPill");
            tp.pixelsPerUnitMultiplier = 52f / 30f;
            tipPill = tp.rectTransform;
            tip = Ui.Label(tipPill, "", 30, Ui.Ink, new Vector2(840, 90), Vector2.zero, false);
            mapBtn = (RectTransform)Ui.Button(card, "Map", Ui.Lilac, new Vector2(240, 96), new Vector2(-180, -225), () => OnMap?.Invoke(), null, null, 42).transform;
            var r = Ui.Button(card, "Try again", Ui.Coral, new Vector2(320, 104), new Vector2(160, -225), () => OnRetry?.Invoke(), null, null, 46);
            retryBtn = (RectTransform)r.transform;
            // from a day's second sunset: Settings > Relaxed days, offered where it's needed
            slowerBtn = (RectTransform)Ui.Button(card, SlowerLabel, Ui.Sky, new Vector2(290, 96), new Vector2(-40, -225), () => OnSlower?.Invoke(), "snail", null, 34).transform;
            slowerBtn.gameObject.SetActive(false);
            firstSelected = r.gameObject;
        }

        /// <summary>Narrow: a taller card, with the line under the heading and the tip each wrapping
        /// over more lines. With "Slower sun" on offer, three buttons share the bottom row; narrow,
        /// it gets a row of its own above Map and Try again, in a card taller again.</summary>
        protected override void Layout(MenuForm form)
        {
            bool n = form == MenuForm.Narrow;
            float up = n && offerSlower ? 40f : 0f;   // narrow with the offer: everything above the buttons moves up
            card.sizeDelta = n ? new Vector2(840, 800 + 2 * up) : new Vector2(980, 640);
            heading.sizeDelta = new Vector2(n ? 780 : 900, 90);
            heading.anchoredPosition = new Vector2(0, (n ? 315 : 235) + up);
            subLine.sizeDelta = n ? new Vector2(760, 100) : new Vector2(900, 50);
            subLine.anchoredPosition = new Vector2(0, (n ? 215 : 160) + up);
            row.sizeDelta = new Vector2(n ? 760 : 800, 120);
            row.anchoredPosition = new Vector2(0, (n ? 85 : 55) + up);
            tipPill.sizeDelta = n ? new Vector2(760, 150) : new Vector2(880, 96);
            tipPill.anchoredPosition = new Vector2(0, (n ? -75 : -78) + up);
            tip.rectTransform.sizeDelta = n ? new Vector2(710, 140) : new Vector2(840, 90);
            if (!offerSlower)
            {
                mapBtn.anchoredPosition = new Vector2(-180, n ? -305 : -225);
                retryBtn.anchoredPosition = new Vector2(160, n ? -305 : -225);
            }
            else if (n)
            {
                slowerBtn.anchoredPosition = new Vector2(0, -185);
                mapBtn.anchoredPosition = new Vector2(-180, -335);
                retryBtn.anchoredPosition = new Vector2(160, -335);
            }
            else
            {
                mapBtn.anchoredPosition = new Vector2(-325, -225);
                slowerBtn.anchoredPosition = new Vector2(-40, -225);
                retryBtn.anchoredPosition = new Vector2(285, -225);
            }
        }

        /// <param name="offerSlower">show "Slower sun" (Settings > Relaxed days) beside "Try again"</param>
        public void Show(Level level, bool offerSlower = false)
        {
            this.offerSlower = offerSlower;
            slowerBtn.gameObject.SetActive(offerSlower);
            Layout(Ui.MenuLayout);
            foreach (Transform c in row) Destroy(c.gameObject);
            var unmet = new List<Need>();
            foreach (var n in level.Needs) if (n.Required && !n.Met) unmet.Add(n);
            float step = Mathf.Min(124f, (Ui.MenuNarrow ? 760f : 900f) / Mathf.Max(1, unmet.Count));
            // one tip per kind of friend left, in turn, with the icons it's about lit
            tips.Clear();
            tipFor.Clear();
            icons.Clear();
            for (int i = 0; i < unmet.Count; i++)
            {
                var it = Ui.Rect("U" + i, row, new Vector2(0.5f, 0.5f), new Vector2(110, 110), new Vector2((i - (unmet.Count - 1) / 2f) * step, 0));
                string t = Tip(unmet[i]);
                if (!tips.Contains(t)) tips.Add(t);
                tipFor.Add(tips.IndexOf(t));
                icons.Add(Ui.Group(it.gameObject));
                Ui.Image(it, Ui.Circle, Res.Hex("F6DCD6"), new Vector2(104, 104), Vector2.zero, null, "Bg");
                Ui.Icon(it, unmet[i].Icon, 72, new Vector2(0, 2));
                Ui.PopIn(it, 0.3f + 0.08f * i, 0.4f, step / 124f);
            }
            ShowTip(0);
            Open();
            Tween.To(600, 0, 0.55f, y => card.anchoredPosition = new Vector2(0, y), k => Ease.OutBack(k, 1.1f), 0, null, card);
        }

        readonly List<string> tips = new();
        readonly List<int> tipFor = new();          // each icon in the row: the tip it belongs to
        readonly List<CanvasGroup> icons = new();
        int tipIndex;
        float tipTimer;
        /// <summary>Seconds each tip shows before the next, when friends of more than one kind were left.</summary>
        public const float TipSeconds = 4f;
        /// <summary>The tip showing, and how many there are (for the self-tests).</summary>
        public string TipText => tip.text;
        public int TipCount => tips.Count;
        /// <summary>Which icons in the row are lit (the ones the tip is about).</summary>
        public bool[] LitIcons()
        {
            var lit = new bool[icons.Count];
            for (int i = 0; i < icons.Count; i++) lit[i] = icons[i] != null && icons[i].alpha > 0.9f;
            return lit;
        }
        /// <summary>The tip each icon in the row belongs to (for the self-tests).</summary>
        public int TipOfIcon(int i) => tipFor[i];

        void ShowTip(int i, bool next = false)
        {
            tipIndex = i;
            tipTimer = 0;
            tip.text = tips.Count > 0 ? tips[i] : "";
            for (int k = 0; k < icons.Count; k++)
                if (icons[k] != null) icons[k].alpha = tips.Count < 2 || tipFor[k] == i ? 1f : 0.35f;
            if (next) Tween.Punch(tipPill, 0.05f, 0.35f);
        }

        void Update()
        {
            if (!IsOpen || tips.Count < 2) return;
            tipTimer += Time.unscaledDeltaTime;
            if (tipTimer >= TipSeconds) ShowTip((tipIndex + 1) % tips.Count, true);
        }

        /// <summary>UI audit: every tip the card can show, and a way to show one.</summary>
        public static IEnumerable<string> AllTips() => TipTexts;
        public void DebugShowTip(string text) { tip.text = text; }

        static readonly string[] TipTexts =
        {
            "Tip: rain on a fire the moment it starts, before it spreads.",
            "Tip: give them a little rain, then fly away so the sun can shine on them.",
            "Tip: that bed got soggy. Rain in short bursts and stop inside the band.",
            "Tip: some beds were still thirsty. Drink your fill, then rain until the bar fills.",
            "Tip: hover over hot animals and keep your shadow on them for a while.",
            "Tip: blow from behind a boat, pointing where it needs to go.",
            "Tip: blow gusts at the washing to dry it, and keep the rain away.",
            "Tip: keep blowing gusts at the sails until the windmill spins up.",
            "Tip: rain right beside them, then move away so the sun makes a rainbow.",
            "Tip: the campfire should keep burning, so keep your rain off it.",
            "Tip: some things want to stay dry. Watch where your rain falls.",
            "Tip: don't drink the duck pond below its line. Raining into it fills it back up.",
            "Tip: the bubbles over everyone's heads show what they need.",
        };

        /// <summary>One concrete hint for a friend left unhelped.</summary>
        static string Tip(Need n) => TipTexts[n switch
        {
            FireNeed _ => 0,
            SunnyNeed _ => 1,
            BedNeed b when b.Soggy => 2,
            BedNeed _ => 3,
            ShadeNeed _ => 4,
            BoatNeed _ => 5,
            LaundryNeed _ => 6,
            WindmillNeed _ => 7,
            RainbowWishNeed _ => 8,
            CampfireNeed _ => 9,
            KeepDryNeed _ => 10,
            PondLineNeed _ => 11,
            _ => 12,
        }];
    }

    // ===================================================================== Ending
    public class EndingScreen : MenuScreen
    {
        public Action OnDone;
        RectTransform card, stampIcon, doneBtn, credits, banner, rainbowIcon, heartIcon, story, thanks;
        Text stampsText, creditsText;
        float cardY = 270, stampsY = -38;

        protected override void Build()
        {
            Dim(0.12f);
            card = Ui.Panel(root, new Vector2(1120, 380), new Vector2(0, 270), Ui.Paper, null, 48f).rectTransform;
            var bn = Ui.Image(card, Ui.Rounded, Res.Hex("F2A7C3"), new Vector2(620, 124), new Vector2(0, 186), null, "Banner");
            bn.pixelsPerUnitMultiplier = 52f / 40f;
            banner = bn.rectTransform;
            var title = Ui.Label(banner, "The End", 84, Color.white, new Vector2(600, 116), new Vector2(0, 2), true);
            Ui.Outlined(title, Res.Hex("C9638A"), 4);
            rainbowIcon = Ui.Icon(card, "rainbow", 120, new Vector2(-440, 186)).rectTransform;
            heartIcon = Ui.Icon(card, "heart", 96, new Vector2(440, 186)).rectTransform;
            story = Ui.Label(card, StoryText, 36, Ui.Ink, new Vector2(1060, 100), new Vector2(0, 62), false).rectTransform;
            stampsText = Ui.Label(card, "", 34, Ui.InkSoft, new Vector2(900, 56), new Vector2(30, -38), true);
            stampIcon = Ui.Icon(card, "stamp_flower", 64, new Vector2(-200, -38)).rectTransform;
            thanks = Ui.Label(card, "Thank you for helping Pip!", 32, Ui.Ink, new Vector2(900, 50), new Vector2(0, -122), true).rectTransform;

            var b = Ui.Button(root, "Back to Pocketvale", Ui.Coral, new Vector2(480, 104), new Vector2(0, -300), () => OnDone?.Invoke(), "heart", null, 40);
            doneBtn = (RectTransform)b.transform;
            var cp = Ui.Panel(root, new Vector2(1180, 96), new Vector2(0, -440), new Color(1, 1, 1, 0.78f), null, 30f, false);
            cp.raycastTarget = false;
            credits = cp.rectTransform;
            creditsText = Ui.Label(credits, "Code, models, music and sound made procedurally with Unity, Blender and numpy\nFonts: Fredoka and Nunito (SIL Open Font License)",
                26, Ui.InkSoft, new Vector2(1140, 90), Vector2.zero, false);
            firstSelected = b.gameObject;
        }

        const string StoryText = "Rosa and Tom were married under a rainbow,\nand Pocketvale had the loveliest summer it can remember.";

        /// <summary>Short: the card higher (its banner pokes above it) and the button and credits
        /// closer under it. Narrow: a taller, narrower card near the top, its lines wrapping, and the
        /// button and credits near the bottom, under the couple.</summary>
        protected override void Layout(MenuForm form)
        {
            bool shortScreen = form == MenuForm.Short, n = form == MenuForm.Narrow;
            cardY = shortScreen ? 151 : n ? 560 : 270;
            if (IsOpen) card.anchoredPosition = new Vector2(0, cardY);
            card.sizeDelta = n ? new Vector2(840, 560) : new Vector2(1120, 380);
            banner.anchoredPosition = new Vector2(0, n ? 276 : 186);
            banner.sizeDelta = new Vector2(n ? 540 : 620, 124);
            rainbowIcon.anchoredPosition = new Vector2(n ? -350 : -440, n ? 276 : 186);
            heartIcon.anchoredPosition = new Vector2(n ? 350 : 440, n ? 276 : 186);
            // the story's two lines would each wrap anyway: let it flow as one paragraph
            story.GetComponent<Text>().text = n ? StoryText.Replace("\n", " ") : StoryText;
            story.sizeDelta = n ? new Vector2(760, 190) : new Vector2(1060, 100);
            story.anchoredPosition = new Vector2(0, n ? 120 : 62);
            stampsY = n ? -40 : -38;
            stampsText.rectTransform.sizeDelta = new Vector2(n ? 700 : 900, 56);
            stampsText.rectTransform.anchoredPosition = new Vector2(30, stampsY);
            thanks.sizeDelta = new Vector2(n ? 760 : 900, 50);
            thanks.anchoredPosition = new Vector2(0, n ? -150 : -122);
            // the button stays low, clear of the rainbow arcing over the couple
            doneBtn.anchoredPosition = new Vector2(0, shortScreen ? -245 : n ? -620 : -300);
            credits.sizeDelta = n ? new Vector2(820, 140) : new Vector2(1180, 96);
            credits.anchoredPosition = new Vector2(0, shortScreen ? -355 : n ? -790 : -440);
            creditsText.rectTransform.sizeDelta = n ? new Vector2(780, 134) : new Vector2(1140, 90);
            creditsText.fontSize = form == MenuForm.Usual ? 26 : 28;
        }

        protected override void OnOpen()
        {
            int total = SaveData.TotalStamps(), max = LevelLibrary.Campaign.Length * 3;
            stampsText.text = total >= max ? $"Every stamp of the summer: {total} / {max}!" : $"Summer stamps collected: {total} / {max}";
            // keep the stamp icon just left of the (centred) text, whatever its length
            stampIcon.anchoredPosition = new Vector2(30 - Mathf.Min(stampsText.preferredWidth, stampsText.rectTransform.sizeDelta.x) / 2f - 46f, stampsY);
            card.anchoredPosition = new Vector2(0, 870);
            Tween.To(-600, 0, 0.7f, y => card.anchoredPosition = new Vector2(0, cardY - y), k => Ease.OutBack(k, 1.1f), 0.2f, null, card);
        }
    }

    // ===================================================================== Cloud wipe transition
    public class CloudWipe : MonoBehaviour
    {
        RectTransform root;
        readonly List<(RectTransform rt, float delay, float size)> puffs = new();
        Image back;
        public bool Busy { get; private set; }
        /// <summary>The new screen is already showing and the clouds are rolling away.</summary>
        public bool Revealing { get; private set; }

        public static CloudWipe Create(Transform parent)
        {
            var c = Ui.MakeCanvas("WipeCanvas", 100, parent);
            // the puffs are laid out over a 1080-high band: match the height so a portrait screen is
            // covered top to bottom (at 16:9 this is identical to the other canvases)
            Destroy(c.GetComponent<OrientationScaler>());
            var sc = c.GetComponent<CanvasScaler>();
            sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            sc.matchWidthOrHeight = 1f;
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
                Revealing = true;
                Tween.Delay(0.25f, () =>
                {
                    Sfx.Ui("whoosh", 0.7f);
                    Tween.To(1, 0, 0.3f, a => back.color = new Color(1, 1, 1, a), Ease.OutCubic);
                    foreach (var (rt, d, s) in puffs)
                    {
                        var r = rt;
                        Tween.To(1, 0, 0.4f, k => { if (r != null) r.localScale = Vector3.one * k; }, Ease.InCubic, 0.3f - d, null, r);
                    }
                    Tween.Delay(0.75f, () => { root.gameObject.SetActive(false); Busy = Revealing = false; finished?.Invoke(); });
                });
            });
        }
    }
}
