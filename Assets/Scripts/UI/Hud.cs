using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PocketWeather
{
    /// <summary>
    /// In-play HUD: Pip's water gauge, the sun track (time to sundown + par flag), the needs tray,
    /// pause, world-anchored thought bubbles, touch buttons, hint captions and toasts.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        Canvas canvas;
        RectTransform root, safe;
        CanvasGroup group;
        Level level;

        // water gauge
        RectTransform gauge, gaugeFill, gaugeIcon;
        Image gaugeFillImg;
        float gaugeShown, gaugeShake;
        // sun track
        RectTransform sunTrack, sunMarker, parFlag;
        Image trackFill, parIcon;
        Text clock;
        float trackW = 470f;
        // tray
        RectTransform tray;
        readonly List<(Need need, Image bg, Image icon, Image badge, bool met, bool problem)> trayItems = new();
        // bubbles
        RectTransform bubbleLayer;
        readonly List<Bubble> bubbles = new();
        // touch buttons
        RectTransform touchRoot;
        HoldButton rainBtn;
        // hint + toast
        RectTransform hintRoot;
        Text hintText;
        Image hintIcon;
        CanvasGroup hintGroup;
        RectTransform hand;
        float hintTimer;
        RectTransform toast;
        Text toastText;
        RectTransform toastPanel;
        Image toastIcon;
        public JuicyButton PauseButton { get; private set; }
        public System.Action OnPause;

        class Bubble
        {
            public Need need;
            public RectTransform rt;
            public Image ring, icon, bg;
            public CanvasGroup group;
            public float shown, phase, shake;
            public bool wasMet, wasProblem;
            public string iconName;
        }

        public static Hud Create(Transform parent)
        {
            var go = new GameObject("Hud");
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<Hud>();
            h.Build();
            return h;
        }

        void Build()
        {
            canvas = Ui.MakeCanvas("HudCanvas", 10, transform);
            root = (RectTransform)canvas.transform;
            group = Ui.Group(canvas.gameObject);
            bubbleLayer = Ui.Stretch("Bubbles", root);
            safe = Ui.Stretch("Safe", root);
            safe.gameObject.AddComponent<SafeArea>();

            // ---- water gauge (top-left)
            var gp = Ui.Panel(safe, new Vector2(400, 92), new Vector2(250, -74), Ui.Paper, new Vector2(0, 1), 46f);
            gauge = gp.rectTransform;
            var track = Ui.Image(gauge, Ui.Rounded, Ui.PaperShade, new Vector2(280, 34), new Vector2(46, 0), null, "Track");
            track.pixelsPerUnitMultiplier = 52f / 17f;
            gaugeFillImg = Ui.Image(track.transform, Ui.Rounded, Ui.Sky, new Vector2(280, 34), Vector2.zero, null, "Fill");
            gaugeFillImg.pixelsPerUnitMultiplier = 52f / 17f;
            gaugeFill = gaugeFillImg.rectTransform;
            gaugeFill.anchorMin = gaugeFill.anchorMax = new Vector2(0, 0.5f);
            gaugeFill.pivot = new Vector2(0, 0.5f);
            gaugeFill.anchoredPosition = Vector2.zero;
            var shine = Ui.Image(gaugeFill, Ui.Rounded, new Color(1, 1, 1, 0.35f), new Vector2(0, 10), new Vector2(0, 7), null, "Shine");
            shine.rectTransform.anchorMin = new Vector2(0, 0.5f); shine.rectTransform.anchorMax = new Vector2(1, 0.5f);
            shine.rectTransform.sizeDelta = new Vector2(-16, 9);
            shine.pixelsPerUnitMultiplier = 52f / 5f;
            Ui.Image(gauge, Ui.Circle, Ui.Sky, new Vector2(104, 104), new Vector2(-150, 4), null, "PipDisc");
            Ui.Image(gauge, Ui.RingThin, Color.white, new Vector2(104, 104), new Vector2(-150, 4), null, "PipRing");
            gaugeIcon = Ui.Icon(gauge, "pip", 96, new Vector2(-150, 6)).rectTransform;
            Ui.Icon(gauge, "drop", 44, new Vector2(-104, -22));

            // ---- sun track (top-centre)
            var sp = Ui.Panel(safe, new Vector2(620, 92), new Vector2(0, -74), Ui.Paper, new Vector2(0.5f, 1), 46f);
            sunTrack = sp.rectTransform;
            var line = Ui.Image(sunTrack, Ui.Rounded, Ui.PaperShade, new Vector2(trackW, 16), new Vector2(-40, -4), null, "Line");
            line.pixelsPerUnitMultiplier = 52f / 8f;
            trackFill = Ui.Image(line.transform, Ui.Rounded, Ui.Butter, new Vector2(0, 16), Vector2.zero, null, "Fill");
            trackFill.pixelsPerUnitMultiplier = 52f / 8f;
            trackFill.rectTransform.anchorMin = trackFill.rectTransform.anchorMax = new Vector2(0, 0.5f);
            trackFill.rectTransform.pivot = new Vector2(0, 0.5f);
            parFlag = Ui.Rect("Par", line.transform, new Vector2(0, 0.5f), new Vector2(46, 46), new Vector2(0, 30));
            parIcon = Ui.Icon(parFlag, "clock", 44, Vector2.zero);
            sunMarker = Ui.Icon(line.transform, "sun", 64, Vector2.zero, new Vector2(0, 0.5f)).rectTransform;
            clock = Ui.Label(sunTrack, "6:00", 34, Ui.Ink, new Vector2(140, 60), new Vector2(240, -2), true);

            // ---- needs tray + pause (top-right)
            PauseButton = Ui.Button(safe, "", Ui.Lilac, new Vector2(92, 92), new Vector2(-74, -74), () => OnPause?.Invoke(), null, new Vector2(1, 1), 40, "Pause");
            var face = PauseButton.transform.Find("Face");
            Ui.Image(face, Ui.Rounded, Color.white, new Vector2(14, 38), new Vector2(-11, 2), null, "Bar1").pixelsPerUnitMultiplier = 52f / 7f;
            Ui.Image(face, Ui.Rounded, Color.white, new Vector2(14, 38), new Vector2(11, 2), null, "Bar2").pixelsPerUnitMultiplier = 52f / 7f;
            tray = Ui.Rect("Tray", safe, new Vector2(1, 1), new Vector2(600, 92), new Vector2(-150, -74), new Vector2(1, 0.5f));

            // ---- touch buttons (bottom-right)
            touchRoot = Ui.Rect("Touch", safe, new Vector2(1, 0), new Vector2(420, 260), new Vector2(-230, 160));
            rainBtn = HoldButton.Create(touchRoot, "drop", Ui.Sky, 190, new Vector2(70, -10));
            var gust = Ui.Button(touchRoot, "", Ui.Mint, new Vector2(140, 140), new Vector2(-130, 30), () =>
            {
                if (Cloud.Instance != null) Cloud.Instance.Input.ButtonGust = true;
            }, "wind", null, 40, "GustBtn");
            gust.Silent = true;
            touchRoot.gameObject.SetActive(false);

            // ---- hint caption (bottom-centre)
            hintRoot = Ui.Rect("Hint", safe, new Vector2(0.5f, 0), new Vector2(560, 100), new Vector2(0, 110));
            hintGroup = Ui.Group(hintRoot.gameObject);
            var hp = Ui.Panel(hintRoot, new Vector2(560, 96), Vector2.zero, Ui.Paper, null, 48f);
            hintIcon = Ui.Icon(hp.transform, "hand", 76, new Vector2(-220, 2));
            hintText = Ui.Label(hp.transform, "", 40, Ui.Ink, new Vector2(420, 80), new Vector2(40, 2), true);
            hintText.horizontalOverflow = HorizontalWrapMode.Overflow;
            hintGroup.alpha = 0;
            hand = Ui.Icon(root, "hand", 130, Vector2.zero).rectTransform;
            hand.gameObject.SetActive(false);

            // ---- toast (top-centre, below the sun track)
            toast = Ui.Rect("Toast", safe, new Vector2(0.5f, 1), new Vector2(640, 96), new Vector2(0, -190));
            var tp = Ui.Panel(toast, new Vector2(640, 96), Vector2.zero, Ui.Butter, null, 48f);
            toastPanel = tp.rectTransform;
            toastIcon = Ui.Icon(tp.transform, "stamp_flower", 84, new Vector2(-260, 2));
            toastText = Ui.Label(tp.transform, "", 38, Ui.Ink, new Vector2(500, 80), new Vector2(40, 2), true);
            toast.gameObject.SetActive(false);
        }

        public bool TouchButtonsVisible => touchRoot != null && touchRoot.gameObject.activeInHierarchy;
        /// <summary>Trailer capture: show the on-screen touch buttons without touching the saved setting.</summary>
        public static bool ForceTouchButtons;
        /// <summary>A hint caption is on screen (faded in and not hidden behind the pause menu).</summary>
        public bool HintVisible => hintRoot != null && hintRoot.gameObject.activeInHierarchy && hintGroup.alpha > 0.5f;
        public string HintText => hintText != null ? hintText.text : "";
        public bool HandVisible => hand != null && hand.gameObject.activeInHierarchy;

        /// <summary>Shows or hides the corner panels (gauge, sun track, tray, pause) but keeps thought
        /// bubbles, hints and toasts, for cinematic trailer shots.</summary>
        public void SetChrome(bool visible)
        {
            foreach (Transform c in safe)
                if (c != toast && c != hintRoot && c != touchRoot) c.gameObject.SetActive(visible);
        }

        public void Bind(Level lvl)
        {
            level = lvl;
            foreach (var b in bubbles) if (b.rt != null) Destroy(b.rt.gameObject);
            bubbles.Clear();
            foreach (Transform c in tray) Destroy(c.gameObject);
            trayItems.Clear();
            if (lvl == null) return;
            int n = 0;
            foreach (var need in lvl.Needs) if (need.Required) n++;
            int i = 0;
            foreach (var need in lvl.Needs)
            {
                if (!need.Required) continue;
                var item = Ui.Rect("Need_" + need.Id, tray, new Vector2(1, 0.5f), new Vector2(84, 84), new Vector2(-42 - (n - 1 - i) * 92, 0));
                var bg = Ui.Image(item, Ui.Circle, Ui.Paper, new Vector2(84, 84), Vector2.zero, null, "Bg");
                Ui.Image(item, Ui.RingThin, new Color(Ui.Ink.r, Ui.Ink.g, Ui.Ink.b, 0.12f), new Vector2(84, 84), Vector2.zero, null, "Edge");
                var ic = Ui.Icon(item, need.Icon, 60, new Vector2(0, 2));
                var badge = Ui.Icon(item, "check", 40, new Vector2(28, -26));
                badge.gameObject.SetActive(false);
                trayItems.Add((need, bg, ic, badge, false, false));
                MakeBubble(need);
                i++;
            }
            float trayW = n * 92;
            tray.sizeDelta = new Vector2(trayW, 92);
            FitTray();
        }

        /// <summary>Shrinks the tray when many needs would run into the sun track.</summary>
        void FitTray()
        {
            var safeRt = (RectTransform)tray.parent;
            float avail = safeRt.rect.width * 0.5f - sunTrack.rect.width * 0.5f - 150f - 24f;
            float s = tray.sizeDelta.x > 1f ? Mathf.Clamp(avail / tray.sizeDelta.x, 0.55f, 1f) : 1f;
            tray.localScale = new Vector3(s, s, 1f);
        }

        void MakeBubble(Need need)
        {
            var b = new Bubble { need = need, phase = Random.value * 6f, iconName = need.Icon };
            b.rt = Ui.Rect("Bubble_" + need.Id, bubbleLayer, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            b.group = Ui.Group(b.rt.gameObject);
            b.group.blocksRaycasts = false;
            Ui.Image(b.rt, Ui.Circle, Color.white, new Vector2(18, 18), new Vector2(-6, 10), null, "Tail2");
            Ui.Image(b.rt, Ui.Circle, Color.white, new Vector2(28, 28), new Vector2(6, 30), null, "Tail1");
            Ui.Image(b.rt, Ui.SoftShadow, new Color(0.2f, 0.18f, 0.35f, 0.18f), new Vector2(140, 140), new Vector2(0, 86), null, "Shadow");
            b.bg = Ui.Image(b.rt, Ui.Circle, Color.white, new Vector2(108, 108), new Vector2(0, 92), null, "Bg");
            var ringBg = Ui.Image(b.bg.transform, Ui.Ring, new Color(0.85f, 0.88f, 0.95f), new Vector2(108, 108), Vector2.zero, null, "RingBg");
            b.ring = Ui.Image(b.bg.transform, Ui.Ring, Ui.Sky, new Vector2(108, 108), Vector2.zero, null, "Ring");
            b.ring.type = Image.Type.Filled;
            b.ring.fillMethod = Image.FillMethod.Radial360;
            b.ring.fillOrigin = (int)Image.Origin360.Top;
            b.ring.fillClockwise = true;
            b.icon = Ui.Icon(b.bg.transform, need.Icon, 66, new Vector2(0, 0));
            b.group.alpha = 0;
            bubbles.Add(b);
        }

        public void SetVisible(bool v, float duration = 0.3f)
        {
            Ui.Fade(group, v ? 1 : 0, duration);
        }

        public void ShowHint(string text, string icon = "hand", float seconds = 4f)
        {
            if (!GameSettings.Hints) return;
            hintText.text = text;
            hintIcon.sprite = Ui.IconSprite(icon);
            float w = Mathf.Clamp(hintText.preferredWidth + 170f, 420f, 1100f);
            hintRoot.sizeDelta = new Vector2(w, 100);
            var panel = hintRoot.GetChild(hintRoot.childCount - 1) as RectTransform;
            foreach (Transform c in hintRoot)
            {
                var rt = (RectTransform)c;
                if (c.name.StartsWith("Panel") && !c.name.Contains("Shadow")) rt.sizeDelta = new Vector2(w, 96);
                if (c.name.StartsWith("Panel") && c.name.Contains("Shadow")) rt.sizeDelta = new Vector2(w + 60, 156);
            }
            hintIcon.rectTransform.anchoredPosition = new Vector2(-w / 2 + 66, 2);
            hintText.rectTransform.sizeDelta = new Vector2(w - 150, 80);
            hintText.rectTransform.anchoredPosition = new Vector2(40, 2);
            hintTimer = seconds;
            Ui.Fade(hintGroup, 1, 0.3f);
            Ui.PopIn(hintRoot, 0, 0.4f);
        }

        public void HideHint()
        {
            hintTimer = 0;
            Ui.Fade(hintGroup, 0, 0.25f);
        }

        /// <summary>Animated ghost hand: "drag" sweeps between points, "hold" presses, "flick" swipes.</summary>
        public void Hand(string mode, Vector3 worldA, Vector3 worldB, float seconds)
        {
            handMode = mode; handA = worldA; handB = worldB; handTimer = seconds;
            hand.gameObject.SetActive(seconds > 0);
        }

        string handMode;
        Vector3 handA, handB;
        float handTimer;

        public void Toast(string text, string icon, float seconds = 2.8f, Color? color = null)
        {
            toastText.text = text;
            toastIcon.sprite = Ui.IconSprite(icon);
            // fit the banner to the message so it stays on one line
            float w = Mathf.Clamp(toastText.preferredWidth + 170f, 520f, 1300f);
            toastPanel.sizeDelta = new Vector2(w, 96);
            toast.sizeDelta = new Vector2(w, 96);
            toastIcon.rectTransform.anchoredPosition = new Vector2(-w / 2f + 62f, 2);
            toastText.rectTransform.sizeDelta = new Vector2(w - 150f, 80);
            toastText.rectTransform.anchoredPosition = new Vector2(40, 2);
            toast.GetComponentInChildren<Image>().color = color ?? Ui.Butter;
            toast.gameObject.SetActive(true);
            Tween.KillOwner(toast);
            Tween.To(260, 0, 0.5f, y => toast.anchoredPosition = new Vector2(0, -190 + y), k => Ease.OutBack(k), 0, null, toast);
            Tween.Delay(seconds, () =>
            {
                Tween.To(0, 300, 0.4f, y => { if (toast != null) toast.anchoredPosition = new Vector2(0, -190 + y); }, Ease.InCubic, 0,
                    () => { if (toast != null) toast.gameObject.SetActive(false); });
            });
        }

        void FlyCheck(Vector2 from, Need need)
        {
            RectTransform target = null;
            foreach (var it in trayItems) if (it.need == need) target = (RectTransform)it.bg.transform.parent;
            if (target == null) return;
            var img = Ui.Icon(bubbleLayer, "check", 96, from);
            var rt = img.rectTransform;
            Vector2 end = (Vector2)bubbleLayer.InverseTransformPoint(target.TransformPoint(Vector3.zero));
            Vector2 start = from;
            Vector2 ctrl = start + new Vector2(0, 160);
            Tween.To(0, 1, 0.75f, k =>
            {
                if (rt == null) return;
                Vector2 a = Vector2.Lerp(start, ctrl, k), bb = Vector2.Lerp(ctrl, end, k);
                rt.anchoredPosition = Vector2.Lerp(a, bb, k);
                float s = k < 0.2f ? Ease.OutBack(k / 0.2f) * 1.2f : Mathf.Lerp(1.2f, 0.45f, (k - 0.2f) / 0.8f);
                rt.localScale = Vector3.one * s;
            }, Ease.InOutCubic, 0, () => { if (rt != null) Destroy(rt.gameObject); Tween.Punch(target, 0.35f, 0.45f); });
        }

        public void ShakeGauge(float amount = 1f) { gaugeShake = Mathf.Max(gaugeShake, amount); }

        public void PunchTrayFor(Need n)
        {
            foreach (var t in trayItems) if (t.need == n) Tween.Punch(t.bg.transform.parent, 0.3f, 0.45f);
        }

        Vector2 WorldToCanvas(Vector3 world, out bool visible)
        {
            var cam = Camera.main;
            visible = false;
            if (cam == null) return Vector2.zero;
            var sp = cam.WorldToScreenPoint(world);
            visible = sp.z > 0;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(bubbleLayer, sp, null, out var lp);
            return lp;
        }

        void Update()
        {
            float dt = Clock.UnscaledDelta;
            float t = Clock.UnscaledTime;
            var cloud = Cloud.Instance;
            if (trayItems.Count > 0) FitTray();

            // ---- gauge
            if (cloud != null)
            {
                gaugeShown = Mathf.Lerp(gaugeShown, cloud.Fill, 1 - Mathf.Exp(-10f * dt));
                float w = Mathf.Max(34f, 280f * gaugeShown);
                gaugeFill.sizeDelta = new Vector2(w, 34);
                gaugeFillImg.enabled = gaugeShown > 0.01f;
                bool low = cloud.Fill < 0.15f;
                gaugeFillImg.color = Color.Lerp(Ui.Coral, Ui.Sky, Mathf.Clamp01((gaugeShown - 0.05f) / 0.2f));
                float pulse = cloud.Drinking ? 1f + Mathf.Sin(t * 14f) * 0.04f : 1f;
                gaugeIcon.localScale = Vector3.one * (pulse * (0.75f + 0.25f * Mathf.Sqrt(cloud.Fill) + 0.1f));
                if (low && !cloud.Drinking) gaugeShake = Mathf.Max(gaugeShake, 0.25f);
            }
            gaugeShake = Mathf.MoveTowards(gaugeShake, 0, dt * 2f);
            gauge.anchoredPosition = new Vector2(250 + Mathf.Sin(t * 50f) * 8f * gaugeShake, -74);

            // ---- sun track
            if (level != null)
            {
                float p = Mathf.Clamp01(level.DayProgress);
                sunMarker.anchoredPosition = new Vector2(p * trackW, Mathf.Sin(p * Mathf.PI) * 6f);
                trackFill.rectTransform.sizeDelta = new Vector2(Mathf.Max(16, p * trackW), 16);
                float parP = Mathf.InverseLerp(level.Def.startHour, level.Def.endHour, level.Def.par);
                parFlag.anchoredPosition = new Vector2(parP * trackW, 34);
                bool pastPar = level.Hour > level.Def.par;
                parIcon.color = pastPar ? new Color(1, 1, 1, 0.35f) : Color.white;
                float h = Mathf.Min(level.Hour, 23.99f);
                int hh = Mathf.FloorToInt(h);
                int mm = Mathf.FloorToInt((h - hh) * 60f / 10f) * 10;
                clock.text = $"{hh}:{mm:00}";
                bool late = p > 0.85f && !level.AllMet;
                trackFill.color = late ? Color.Lerp(Ui.Butter, Ui.Coral, 0.5f + 0.5f * Mathf.Sin(t * 6f)) : Ui.Butter;
                sunMarker.localScale = Vector3.one * (late ? 1f + 0.1f * Mathf.Sin(t * 8f) : 1f);
            }

            // ---- tray
            for (int i = 0; i < trayItems.Count; i++)
            {
                var it = trayItems[i];
                bool met = it.need.Met;
                bool problem = it.need.Problem;
                if (met != it.met || problem != it.problem)
                {
                    it.badge.gameObject.SetActive(met || problem);
                    it.badge.sprite = Ui.IconSprite(met ? "check" : "oops");
                    it.bg.color = met ? Color.Lerp(Ui.Paper, Ui.Mint, 0.45f) : problem ? Color.Lerp(Ui.Paper, Ui.Coral, 0.35f) : Ui.Paper;
                    Ui.PopIn(it.badge.transform, 0, 0.35f);
                    Tween.Punch(it.bg.transform.parent, 0.25f, 0.4f);
                    trayItems[i] = (it.need, it.bg, it.icon, it.badge, met, problem);
                }
            }

            // ---- bubbles
            bool running = level != null && level.Running;
            foreach (var b in bubbles)
            {
                if (b.need == null) continue;
                bool show = running && b.need.ShowBubble;
                b.shown = Mathf.MoveTowards(b.shown, show ? 1 : 0, dt * 4f);
                var lp = WorldToCanvas(b.need.BubbleAnchor, out bool vis);
                float bob = Mathf.Sin(t * 2.2f + b.phase) * 5f;
                b.shake = Mathf.MoveTowards(b.shake, 0, dt * 2f);
                b.rt.anchoredPosition = lp + new Vector2(Mathf.Sin(t * 40f) * 6f * b.shake, bob);
                b.group.alpha = vis ? Ease.OutCubic(b.shown) : 0;
                b.rt.localScale = Vector3.one * (0.6f + 0.4f * Ease.OutBack(b.shown));
                bool problem = b.need.Problem;
                string icon = problem ? b.need.ProblemIcon : b.need.Icon;
                if (icon != b.iconName)
                {
                    b.iconName = icon;
                    b.icon.sprite = Ui.IconSprite(icon);
                    Tween.Punch(b.bg.transform, 0.25f, 0.4f);
                }
                if (problem && !b.wasProblem) b.shake = 1f;
                b.wasProblem = problem;
                bool metNow = b.need.Met;
                if (metNow && !b.wasMet && running && vis) FlyCheck(b.rt.anchoredPosition + new Vector2(0, 92), b.need);
                b.wasMet = metNow;
                b.ring.fillAmount = Mathf.Lerp(b.ring.fillAmount, b.need.Progress, 1 - Mathf.Exp(-10f * dt));
                b.ring.color = problem ? Ui.Coral : b.need.Progress > 0.999f ? Ui.Mint : Ui.Sky;
            }

            // ---- touch buttons
            bool wantTouch = ForceTouchButtons || GameSettings.TouchButtons == 1 || (GameSettings.TouchButtons == 0 && cloud != null && cloud.Input.LastDevice == CloudInput.Device.Touch);
            if (touchRoot.gameObject.activeSelf != (wantTouch && running)) touchRoot.gameObject.SetActive(wantTouch && running);
            if (cloud != null) cloud.Input.ButtonRain = rainBtn != null && rainBtn.Held && running;

            // ---- hint (tucked away, with its clock stopped, while the pause menu is up)
            bool paused = GameFlow.I != null && GameFlow.I.Current == GameFlow.State.Paused;
            if (hintRoot.gameObject.activeSelf == paused) hintRoot.gameObject.SetActive(!paused);
            if (hand.gameObject.activeSelf && paused) hand.gameObject.SetActive(false);
            if (paused) return;
            if (hintTimer > 0)
            {
                hintTimer -= dt;
                if (hintTimer <= 0) Ui.Fade(hintGroup, 0, 0.3f);
            }
            if (handTimer > 0)
            {
                handTimer -= dt;
                hand.gameObject.SetActive(handTimer > 0);
                if (handTimer > 0)
                {
                    var a = WorldToCanvasRoot(handA);
                    var bpos = WorldToCanvasRoot(handB);
                    float k = (t * 0.6f) % 1f;
                    Vector2 p = a;
                    float s = 1f;
                    float alpha = 1f;
                    switch (handMode)
                    {
                        case "drag":
                            p = Vector2.Lerp(a, bpos, Ease.InOutCubic(Mathf.Clamp01(k * 1.4f)));
                            alpha = Mathf.Clamp01(Mathf.Min(k * 8f, (1 - k) * 6f));
                            break;
                        case "hold":
                            s = k < 0.3f ? 1f - Ease.OutCubic(k / 0.3f) * 0.18f : 0.82f;
                            break;
                        case "flick":
                            float fk = Mathf.Clamp01(k * 3f);
                            p = Vector2.Lerp(a, bpos, Ease.InCubic(fk));
                            alpha = k < 0.4f ? 1f : Mathf.Clamp01(1 - (k - 0.4f) * 4f);
                            break;
                    }
                    hand.anchoredPosition = p + new Vector2(30, -55);
                    hand.localScale = Vector3.one * s;
                    hand.GetComponent<Image>().color = new Color(1, 1, 1, alpha * 0.92f);
                }
            }
        }

        Vector2 WorldToCanvasRoot(Vector3 w)
        {
            var cam = Camera.main;
            if (cam == null) return Vector2.zero;
            var sp = cam.WorldToScreenPoint(w);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, null, out var lp);
            return lp;
        }
    }

    /// <summary>Round on-screen button that reports while held (touch "rain").</summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public bool Held { get; private set; }
        RectTransform face;

        public static HoldButton Create(Transform parent, string icon, Color color, float size, Vector2 pos)
        {
            var rt = Ui.Rect("Hold_" + icon, parent, new Vector2(0.5f, 0.5f), new Vector2(size, size), pos);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.sprite = Ui.Circle;
            hit.color = new Color(1, 1, 1, 0.001f);
            Ui.Image(rt, Ui.Circle, Color.Lerp(color, Ui.Ink, 0.3f), new Vector2(size, size), new Vector2(0, -8), null, "Lip");
            var f = Ui.Image(rt, Ui.Circle, color, new Vector2(size, size), Vector2.zero, null, "Face");
            Ui.Icon(f.transform, icon, size * 0.6f, Vector2.zero);
            var hb = rt.gameObject.AddComponent<HoldButton>();
            hb.face = f.rectTransform;
            // it's a Selectable-ish target for PointerOverUi checks
            rt.gameObject.AddComponent<Selectable>().transition = Selectable.Transition.None;
            return hb;
        }

        public void OnPointerDown(PointerEventData e) { Held = true; }
        public void OnPointerUp(PointerEventData e) { Held = false; }
        public void OnPointerExit(PointerEventData e) { }

        void Update()
        {
            if (face != null) face.anchoredPosition = new Vector2(0, Held ? -6 : 0);
        }
    }
}
