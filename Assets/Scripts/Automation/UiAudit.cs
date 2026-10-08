using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PocketWeather
{
    /// <summary>
    /// UI audit, active with <c>-pwUiAudit</c>. Opens every screen (title, map, postcard, HUD,
    /// pause, settings, results, sunset card, ending) and, for each visible button, slider and
    /// toggle, casts a UI ray at its centre: the control itself must be the one that receives it
    /// (nothing invisible on top) and it must sit fully inside the screen. Run it at several window
    /// sizes (PW_W / PW_H). Prints PASS/FAIL per screen, then quits.
    /// </summary>
    public class UiAudit : MonoBehaviour
    {
        int passes, fails;
        readonly List<RaycastResult> hits = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-pwUiAudit") < 0) return;
            DontDestroyOnLoad(new GameObject("UiAudit").AddComponent<UiAudit>().gameObject);
        }

        void Audit(string screen)
        {
            var es = EventSystem.current;
            var problems = new List<string>();
            var controls = new List<(Selectable sel, Rect r)>();
            int count = 0;
            // while a menu is open it's modal: whatever sits behind it (the HUD) is meant to be covered
            bool menuOpen = false;
            foreach (var g in FindObjectsByType<CanvasGroup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (g.alpha > 0.5f && g.blocksRaycasts && g.GetComponentInParent<Canvas>().rootCanvas.name == "MenuCanvas") menuOpen = true;
            foreach (var sel in FindObjectsByType<Selectable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!sel.IsInteractable() || !sel.gameObject.activeInHierarchy) continue;
                var cg = sel.GetComponentInParent<CanvasGroup>();
                if (cg != null && (cg.alpha < 0.5f || !cg.blocksRaycasts)) continue;   // a closed / closing screen
                if (menuOpen && sel.GetComponentInParent<Canvas>().rootCanvas.name != "MenuCanvas") continue;
                var rt = (RectTransform)sel.transform;
                var corners = new Vector3[4];
                rt.GetWorldCorners(corners);
                Vector2 min = corners[0], max = corners[2];
                if (max.x - min.x < 1 || max.y - min.y < 1) continue;
                count++;
                var screenRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                // a full-screen backdrop (the title's "tap anywhere") sits under everything by design
                if (screenRect.width * screenRect.height < 0.9f * Screen.width * Screen.height) controls.Add((sel, screenRect));
                string name = Path(sel.transform);
                if (min.x < -1 || min.y < -1 || max.x > Screen.width + 1 || max.y > Screen.height + 1)
                    problems.Add($"{name} off-screen ({min.x:0},{min.y:0})-({max.x:0},{max.y:0})");
                var centre = (min + max) / 2f;
                hits.Clear();
                es.RaycastAll(new PointerEventData(es) { position = centre }, hits);
                if (hits.Count == 0) { problems.Add($"{name}: nothing receives a tap at its centre"); continue; }
                var top = hits[0].gameObject;
                var owner = top.GetComponentInParent<Selectable>();
                if (owner != sel) problems.Add($"{name}: covered by {Path(top.transform)}");
            }
            // no two controls overlap (rows may touch: a few design units of slack)
            for (int i = 0; i < controls.Count; i++)
            for (int j = i + 1; j < controls.Count; j++)
            {
                var (a, ra) = controls[i];
                var (b, rb) = controls[j];
                if (a.transform.IsChildOf(b.transform) || b.transform.IsChildOf(a.transform)) continue;
                float unit = a.GetComponentInParent<Canvas>().rootCanvas.scaleFactor;
                float ox = Mathf.Min(ra.xMax, rb.xMax) - Mathf.Max(ra.xMin, rb.xMin), oy = Mathf.Min(ra.yMax, rb.yMax) - Mathf.Max(ra.yMin, rb.yMin);
                if (ox > 6f * unit && oy > 6f * unit) problems.Add($"{Path(a.transform)} overlaps {Path(b.transform)} ({ox / unit:0}x{oy / unit:0} units)");
            }
            string texts = menuOpen ? MenuTexts(controls, problems) : "";
            bool ok = problems.Count == 0 && count > 0;
            Debug.Log($"[UiAudit] {(ok ? "PASS" : "FAIL")} {screen}: {count} controls{texts} at {Screen.width}x{Screen.height}" +
                      (problems.Count > 0 ? "\n    " + string.Join("\n    ", problems) : count == 0 ? " (none found)" : ""));
            if (ok) passes++; else fails++;
        }

        /// <summary>Every text showing on the menus: on screen, not over a control it isn't part of,
        /// and on a phone held either way (short side 500 CSS px or less) at least 11.5 CSS px. A
        /// desktop-sized screen (short side 720 or more) keeps the menus at their design scale.</summary>
        string MenuTexts(List<(Selectable sel, Rect r)> controls, List<string> problems)
        {
            float px = Platform.PixelsPerCssPx;
            float smallest = 999f;
            string smallestText = "";
            int n = 0;
            foreach (var t in FindObjectsByType<Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (string.IsNullOrWhiteSpace(t.text) || !t.enabled || t.color.a < 0.3f) continue;
                if (t.canvas == null || t.canvas.rootCanvas.name != "MenuCanvas") continue;
                var cg = t.GetComponentInParent<CanvasGroup>();
                if (cg != null && (cg.alpha < 0.5f || !cg.blocksRaycasts)) continue;
                if (!GlyphRect(t, out var g)) continue;
                n++;
                int size = t.resizeTextForBestFit ? t.cachedTextGenerator.fontSizeUsedForBestFit : t.fontSize;
                float cssPx = size * t.rectTransform.lossyScale.y / px;
                string label = t.text.Replace("\n", " ");
                if (label.Length > 28) label = label.Substring(0, 28) + "…";
                if (cssPx < smallest) { smallest = cssPx; smallestText = label; }
                if (g.xMin < -1 || g.yMin < -1 || g.xMax > Screen.width + 1 || g.yMax > Screen.height + 1)
                    problems.Add($"text \"{label}\" off screen ({g.xMin:0},{g.yMin:0})-({g.xMax:0},{g.yMax:0})");
                float unit = t.canvas.rootCanvas.scaleFactor;
                // a text drawn on a card, pill or button stays inside it (a wrapped line once spilled
                // out of the bottom of its pill); a settings row's label sits beside its switch or
                // slider by design
                var bg = t.transform.parent != null ? t.transform.parent.GetComponent<Image>() : null;
                if (bg != null && bg.enabled && bg.GetComponent<Toggle>() == null && bg.GetComponent<Slider>() == null)
                {
                    var c = new Vector3[4];
                    bg.rectTransform.GetWorldCorners(c);
                    float slack = 2f * unit;
                    if (g.xMin < c[0].x - slack || g.yMin < c[0].y - slack || g.xMax > c[2].x + slack || g.yMax > c[2].y + slack)
                        problems.Add($"text \"{label}\" spills out of {Path(bg.transform)}");
                }
                foreach (var (sel, r) in controls)
                {
                    if (t.transform.IsChildOf(sel.transform)) continue;
                    float ox = Mathf.Min(g.xMax, r.xMax) - Mathf.Max(g.xMin, r.xMin), oy = Mathf.Min(g.yMax, r.yMax) - Mathf.Max(g.yMin, r.yMin);
                    if (ox > 6f * unit && oy > 6f * unit) problems.Add($"text \"{label}\" runs into {Path(sel.transform)} ({ox / unit:0}x{oy / unit:0} units)");
                }
            }
            float shortCss = Mathf.Min(Screen.width, Screen.height) / px;
            if (shortCss <= 500f && smallest < 11.5f) problems.Add($"smallest text {smallest:0.0} CSS px (\"{smallestText}\"), want 11.5");
            if (shortCss >= 720f && Ui.MenuBoost != 1f) problems.Add($"menus scaled x{Ui.MenuBoost:0.00} on a desktop-sized screen");
            return $", {n} texts, smallest {smallest:0.0} CSS px (\"{smallestText}\"), menus x{Ui.MenuBoost:0.00}";
        }

        /// <summary>Where a text's glyphs actually are on screen (its rect is often far wider).</summary>
        static bool GlyphRect(Text t, out Rect r)
        {
            r = default;
            var verts = t.cachedTextGenerator.verts;
            int count = Mathf.Min(verts.Count, t.cachedTextGenerator.characterCountVisible * 4);
            if (count == 0) return false;
            float inv = 1f / t.pixelsPerUnit;
            Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
            for (int i = 0; i < count; i++)
            {
                Vector2 w = t.rectTransform.TransformPoint(verts[i].position * inv);
                min = Vector2.Min(min, w); max = Vector2.Max(max, w);
            }
            if (max.x - min.x < 1f) return false;
            r = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        /// <summary>The HUD's gauge, sun track, needs tray and pause button sit on screen without
        /// overlapping, and the UI isn't drawn smaller than its design scale (portrait included).</summary>
        void CheckTopBar(string what)
        {
            var problems = new List<string>();
            var bar = GameFlow.I.Hud.TopBar;
            string[] names = { "gauge", "sun track", "tray", "pause" };
            var rects = new Rect[bar.Length];
            var c = new Vector3[4];
            for (int i = 0; i < bar.Length; i++)
            {
                bar[i].GetWorldCorners(c);
                rects[i] = Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
                if (rects[i].xMin < -1 || rects[i].yMin < -1 || rects[i].xMax > Screen.width + 1 || rects[i].yMax > Screen.height + 1)
                    problems.Add($"{names[i]} off-screen {rects[i]}");
            }
            for (int i = 0; i < rects.Length; i++)
            for (int j = i + 1; j < rects.Length; j++)
            {
                var a = rects[i]; var b = rects[j];
                float ox = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin), oy = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
                if (ox > 1 && oy > 1) problems.Add($"{names[i]} overlaps {names[j]} ({ox:0}x{oy:0} px)");
            }
            var canvas = GameFlow.I.Hud.GetComponentInChildren<Canvas>();
            float want = Ui.Portrait ? Screen.width / Ui.PortraitWidth : Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            if (canvas.scaleFactor < want * 0.99f) problems.Add($"UI scale {canvas.scaleFactor:0.000}, want {want:0.000}");
            string sizes = PhoneSizes(rects[3], problems);
            bool ok = problems.Count == 0;
            Debug.Log($"[UiAudit] {(ok ? "PASS" : "FAIL")} {what}: scale {canvas.scaleFactor:0.000} at {Screen.width}x{Screen.height}; {sizes}" +
                      (ok ? "" : "\n    " + string.Join("\n    ", problems)));
            if (ok) passes++; else fails++;
        }

        /// <summary>On a phone-sized screen (short side 500 CSS px or less) the HUD must reach finger
        /// and eye sizes: a 44 px pause button (with half a pixel's rounding), a 15 px clock, and tray
        /// items no smaller than the old layout drew them (a centred 620-wide sun track, no top-bar
        /// scaling; worked out here from the same numbers). Larger screens keep the design scale.</summary>
        string PhoneSizes(Rect pause, List<string> problems)
        {
            var hud = GameFlow.I.Hud;
            float px = Platform.PixelsPerCssPx, k = hud.ChromeScale, css = hud.CssScale;
            float pausePx = pause.height / px;
            float clockPx = hud.ClockText.fontSize * css * k;
            float trayPx = 84f * css * k * hud.TrayScale;
            // the old layout, on the same screen
            var safe = (RectTransform)hud.TopBar[0].parent.parent;
            float w = safe.rect.width, trayW = hud.TrayCount * 92f;
            float oldAvail = Ui.Portrait ? w - 470f - 150f - 24f : w * 0.5f - 310f - 174f;
            float oldTrayPx = 84f * css * (trayW > 1f ? Mathf.Clamp(oldAvail / trayW, 0.55f, 1f) : 1f);
            float shortCss = Mathf.Min(Screen.width, Screen.height) / px;
            if (shortCss <= 500f)
            {
                if (pausePx < 43.5f) problems.Add($"pause button {pausePx:0.0} px, want 44");
                if (clockPx < 15f) problems.Add($"clock text {clockPx:0.0} px, want 15");
                if (trayPx < oldTrayPx - 0.05f) problems.Add($"tray items {trayPx:0.0} px, smaller than the old layout's {oldTrayPx:0.0}");
            }
            else if (!Ui.Portrait && Screen.height / px >= 720f)
            {
                if (k != 1f) problems.Add($"top bar scaled x{k:0.00} on a desktop-sized screen");
                // the sun track makes room, so even Day 12's seven needs keep their full size
                if (hud.TrayScale < 0.999f) problems.Add($"tray shrunk to x{hud.TrayScale:0.00} on a desktop-sized screen");
            }
            return $"top bar x{k:0.00}, pause {pausePx:0.0} px, clock {clockPx:0.0} px, {hud.TrayCount} tray items {trayPx:0.0} px at x{hud.TrayScale:0.00} (old layout {oldTrayPx:0.0})";
        }

        /// <summary>Every hint caption, in every device's words, and the longest toasts: each fits
        /// on screen with its text inside its pill, no smaller than 75% of its usual size, and (held
        /// upright, with the touch buttons showing) the hint stays clear of them.</summary>
        IEnumerator CheckCaptions(int day)
        {
            var hud = GameFlow.I.Hud;
            GameFlow.I.DebugStart(day, true);
            Hud.ForceTouchButtons = true;
            yield return Settle(1.2f);
            var problems = new List<string>();
            var c = new Vector3[4];
            hud.TouchRect.GetWorldCorners(c);
            var t = Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
            if (!hud.TouchButtonsVisible) problems.Add("touch buttons not shown");
            int hints = 0, toasts = 0, wrapped = 0;
            float widest = 0, smallest = 99f;
            var toastTexts = new List<string> { "Rain into the pond to fill it back up!", "Careful! The ducks need their pond", "Oh no! Make them a rainbow!", "Encore: a scorcher!", GameFlow.LateWarning(7) };
            foreach (var id in LevelLibrary.Campaign)
            {
                var def = LevelLibrary.Load(id);
                if (def?.delight != null && !string.IsNullOrEmpty(def.delight.title)) toastTexts.Add($"Delight! {def.delight.title}");
            }
            void Measure(string kind, string text, RectTransform pill, Text label, int usual)
            {
                pill.GetWorldCorners(c);
                var r = Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
                widest = Mathf.Max(widest, r.width / Screen.width);
                // a margin of 4 CSS px: a pill that meets the screen's edges looks cut off
                float m = 4f * Platform.PixelsPerCssPx;
                if (r.xMin < m - 0.5f || r.yMin < -1 || r.xMax > Screen.width - m + 0.5f || r.yMax > Screen.height + 1) problems.Add($"{kind} \"{text}\" off screen or touching its edges {r}");
                if (kind == "hint" && r.Overlaps(t)) problems.Add($"hint \"{text}\" {r} overlaps the touch buttons {t}");
                Canvas.ForceUpdateCanvases();
                // a font atlas rebuilt this frame (shrunk captions ask for new sizes) can leave the
                // text's layout empty until it's rebuilt: lay it out once more before giving up
                bool laidOut = GlyphRect(label, out var g);
                if (!laidOut) { label.SetAllDirty(); Canvas.ForceUpdateCanvases(); laidOut = GlyphRect(label, out g); }
                if (!laidOut) problems.Add($"{kind} \"{text}\" has no visible glyphs");
                else if (g.xMin < r.xMin - 1 || g.xMax > r.xMax + 1 || g.yMin < r.yMin - 1 || g.yMax > r.yMax + 1) problems.Add($"{kind} \"{text}\" runs out of its pill: text {g}, pill {r}");
                if (label.cachedTextGenerator.lineCount > 1) wrapped++;
                float k = label.fontSize / (float)usual;
                smallest = Mathf.Min(smallest, k);
                if (k < 0.74f) problems.Add($"{kind} \"{text}\" shrunk to {k:0.00}");
            }
            foreach (var h in new HashSet<string>(Onboarding.AllHints()))
            {
                hud.ShowHint(h, "wind", 10f);
                hud.DebugSettleCaptions();
                yield return null;
                hints++;
                Measure("hint", h, hud.HintRect, hud.HintLabel, 40);
            }
            foreach (var s in toastTexts)
            {
                hud.Toast(s, "stamp_flower", 10f);
                hud.DebugSettleCaptions();
                yield return null;
                toasts++;
                Measure("toast", s, hud.ToastRect, hud.ToastLabel, 38);
            }
            Hud.ForceTouchButtons = false;
            hud.HideHint();
            bool ok = problems.Count == 0;
            Debug.Log($"[UiAudit] {(ok ? "PASS" : "FAIL")} captions fit, day {day + 1}: {hints} hints and {toasts} toasts, widest {widest * 100f:0}% of the screen, " +
                      $"{wrapped} on two lines, text at least x{smallest:0.00} at {Screen.width}x{Screen.height}" +
                      (ok ? "" : "\n    " + string.Join("\n    ", problems)));
            if (ok) passes++; else fails++;
        }

        static string Path(Transform t)
        {
            string p = t.name;
            for (int i = 0; i < 2 && t.parent != null; i++) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        IEnumerator Settle(float s = 1.2f) { yield return new WaitForSecondsRealtime(s); }

        /// <summary>Every need in every day has a decided "wants" badge, and its icon exists.</summary>
        IEnumerator WantBadges()
        {
            var problems = new List<string>();
            int count = 0;
            for (int i = 0; i < LevelLibrary.Campaign.Length; i++)
            {
                GameFlow.I.DebugStart(i, false);
                yield return null;
                foreach (var n in Level.Current.Needs)
                {
                    count++;
                    if (n.WantIcon == "?") problems.Add($"{Level.Current.Def.id}/{n.Id} ({n.GetType().Name}) has no want badge decided");
                    else if (n.WantIcon != null && Ui.IconSprite(n.WantIcon) == null) problems.Add($"{n.Id}: icon '{n.WantIcon}' missing");
                }
            }
            bool ok = problems.Count == 0 && count > 0;
            Debug.Log($"[UiAudit] {(ok ? "PASS" : "FAIL")} want badges: {count} needs over {LevelLibrary.Campaign.Length} days" + (problems.Count > 0 ? "\n    " + string.Join("\n    ", problems) : ""));
            if (ok) passes++; else fails++;
            GameFlow.I.DebugCloseMenus();
        }

        /// <summary>Framing: landscape keeps the old whole-island framing exactly; portrait frames
        /// a bigger island than that (at least 1.5x closer on a 20:9 phone). Pip stays in frame at
        /// both ends of the island, and every bubble on screen, pinned to the edge or not, is
        /// fully inside it.</summary>
        IEnumerator CheckFraming(int day)
        {
            var f = GameFlow.I;
            var rig = GameRoot.Instance.Rig;
            f.DebugStart(day, true);
            yield return Settle(1.6f);
            var problems = new List<string>();
            var cam = rig.Cam;
            var def = Level.Current.Def;
            float aspect = cam.aspect, ratio = rig.LegacyDistance / rig.Distance;
            float hd = def.island.d / 2f;
            float yLo = cam.WorldToScreenPoint(new Vector3(rig.Pan, -1.3f, -hd)).y, yHi = cam.WorldToScreenPoint(new Vector3(rig.Pan, 0.3f, hd)).y;
            float tall = Mathf.Abs(yHi - yLo) / Screen.height;
            if (aspect >= 1f)
            {
                if (Mathf.Abs(ratio - 1f) > 1e-4f) problems.Add($"landscape framing changed: distance {rig.Distance:0.000}, was {rig.LegacyDistance:0.000}");
            }
            else
            {
                float want = aspect < 0.5f ? 1.5f : 1.2f;
                if (ratio < want) problems.Add($"portrait island only {ratio:0.00}x closer than whole-width framing, want {want:0.0}x");
            }
            string ends = "";
            foreach (float side in new[] { -1f, 1f })
            {
                var c = Cloud.Instance;
                c.Teleport(new Vector3(side * (def.island.w / 2f - 0.8f), 0, 0));
                yield return Settle(2.5f);
                var sp = cam.WorldToScreenPoint(c.transform.position);
                string end = side < 0 ? "left" : "right";
                if (sp.x < 0 || sp.x > Screen.width || sp.y < 0 || sp.y > Screen.height) problems.Add($"Pip off screen at the {end} end ({sp.x:0},{sp.y:0})");
                int shown = 0, pinned = 0;
                foreach (var (id, r, pin) in f.Hud.VisibleBubbles())
                {
                    shown++;
                    if (pin) pinned++;
                    if (r.xMin < -1 || r.xMax > Screen.width + 1 || r.yMin < -1 || r.yMax > Screen.height + 1) problems.Add($"bubble {id} off screen at the {end} end {r}");
                }
                ends += $", {end} end: pan {rig.Pan:0.0}/{rig.PanMax:0.0}, {shown} bubbles ({pinned} pinned)";
            }
            bool ok = problems.Count == 0;
            Debug.Log($"[UiAudit] {(ok ? "PASS" : "FAIL")} framing, day {day + 1}: distance {rig.Distance:0.00} (whole-width {rig.LegacyDistance:0.00}, {ratio:0.00}x closer), " +
                      $"frames {rig.ViewHalfWidth * 2f / def.island.w * 100f:0}% of the width, island {tall * 100f:0}% of the screen's height{ends} at {Screen.width}x{Screen.height}" +
                      (ok ? "" : "\n    " + string.Join("\n    ", problems)));
            if (ok) passes++; else fails++;
        }

        IEnumerator Start()
        {
            var f = GameFlow.I;
            float w = 0;
            while ((GameFlow.I == null || GameFlow.I.Current != GameFlow.State.Title) && w < 20f) { w += Time.unscaledDeltaTime; yield return null; }
            f = GameFlow.I;
            yield return Settle(1.5f);
            Audit("title");
            f.DebugShowMap(); yield return Settle(); Audit("map");
            f.DebugStart(0, false); yield return Settle(1.6f); Audit("postcard");
            SaveData.Award(LevelLibrary.Campaign[0], SaveData.StampSaved);   // opens Day 1's Encore
            f.DebugShowMap(); yield return Settle(); Audit("map with an Encore open");
            f.DebugStart(0, false, true); f.DebugShowPostcard(); yield return Settle(1.6f); Audit("encore postcard");
            f.DebugStart(11, false); f.DebugShowPostcard(); yield return Settle(1.6f); Audit("postcard, day 12 (7 needs)");
            f.DebugStart(0, false); yield return Settle(1.6f);
            f.DebugBeginPlay(); yield return Settle(); Audit("hud"); CheckTopBar("hud top bar, day 1");
            f.DebugPause(); yield return Settle(); Audit("pause");
            GameObject.Find("PauseScreen").GetComponent<PauseMenu>().DebugArmRestart(); yield return Settle(0.3f); Audit("pause, Restart asking Sure?");
            GameObject.Find("PauseScreen").GetComponent<PauseMenu>().SetControllerLost(); yield return Settle(0.3f); Audit("pause, controller disconnected");
            f.DebugSettings(); yield return Settle(); Audit("settings");
            f.DebugCloseSettings(); yield return Settle(0.6f);
            f.DebugResume(); yield return Settle(0.6f);
            f.DebugResults(); yield return Settle(1.6f); Audit("results");
            f.DebugCloseMenus();
            f.DebugStart(0, true); yield return Settle(1.2f);
            f.DebugResults(true); yield return Settle(2.4f); Audit("results, first save (Encore note)");
            f.DebugCloseMenus();
            f.DebugStart(0, true, true); yield return Settle(1.2f);
            f.DebugEncoreResults(); yield return Settle(1.6f); Audit("encore results");
            f.DebugCloseMenus();
            f.DebugStart(1, true); yield return Settle(1.2f);
            f.DebugSunset(); yield return Settle(3.2f); Audit("sunset");
            f.DebugStart(1, true); yield return Settle(1.2f);
            f.DebugSunset(true); yield return Settle(3.2f); Audit("sunset, offering a slower sun");
            // Settings > Relaxed days: the postcard's par line and the results card's par stamp say so
            GameSettings.RelaxedDays = true;
            f.DebugStart(2, false); yield return Settle(1.6f); Audit("postcard, relaxed day");
            f.DebugCloseMenus();
            f.DebugStart(2, true); yield return Settle(1.2f);
            f.DebugResults(false, true); yield return Settle(1.6f); Audit("results, relaxed day");
            f.DebugCloseMenus();
            GameSettings.RelaxedDays = false;
            f.DebugEnding(); yield return Settle(4f); Audit("ending");
            f.DebugStart(3, true); yield return Settle(1.6f); CheckTopBar("hud top bar, day 4");
            f.DebugStart(11, true); yield return Settle(1.6f); CheckTopBar("hud top bar, day 12 (7 needs)");
            foreach (int d in new[] { 0, 3, 11 }) yield return CheckFraming(d);
            yield return CheckCaptions(4);
            yield return WantBadges();
            Debug.Log($"[UiAudit] done: {passes} passed, {fails} failed");
            yield return new WaitForSecondsRealtime(0.3f);
            Application.Quit();
        }
    }
}
