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
            bool ok = problems.Count == 0 && count > 0;
            Debug.Log($"[UiAudit] {(ok ? "PASS" : "FAIL")} {screen}: {count} controls at {Screen.width}x{Screen.height}" +
                      (problems.Count > 0 ? "\n    " + string.Join("\n    ", problems) : count == 0 ? " (none found)" : ""));
            if (ok) passes++; else fails++;
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
            else if (!Ui.Portrait && Screen.height / px >= 720f && k != 1f) problems.Add($"top bar scaled x{k:0.00} on a desktop-sized screen");
            return $"top bar x{k:0.00}, pause {pausePx:0.0} px, clock {clockPx:0.0} px, {hud.TrayCount} tray items {trayPx:0.0} px (old layout {oldTrayPx:0.0})";
        }

        /// <summary>The hint caption (a long one) stays clear of the touch buttons and on screen.</summary>
        IEnumerator CheckHintClear(int day)
        {
            var hud = GameFlow.I.Hud;
            GameFlow.I.DebugStart(day, true);
            Hud.ForceTouchButtons = true;
            yield return Settle(1.2f);
            hud.ShowHint("Blow the washing dry (flick)", "wind", 10f);
            yield return Settle(0.8f);
            var problems = new List<string>();
            var c = new Vector3[4];
            hud.HintRect.GetWorldCorners(c);
            var h = Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
            hud.TouchRect.GetWorldCorners(c);
            var t = Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
            if (!hud.TouchButtonsVisible) problems.Add("touch buttons not shown");
            if (h.xMin < -1 || h.yMin < -1 || h.xMax > Screen.width + 1 || h.yMax > Screen.height + 1) problems.Add($"hint off screen {h}");
            if (h.Overlaps(t)) problems.Add($"hint {h} overlaps the touch buttons {t}");
            Hud.ForceTouchButtons = false;
            hud.HideHint();
            bool ok = problems.Count == 0;
            Debug.Log($"[UiAudit] {(ok ? "PASS" : "FAIL")} hint clear of the touch buttons, day {day + 1}: hint {h.width:0}x{h.height:0} px at {Screen.width}x{Screen.height}" +
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
            f.DebugStart(0, false); yield return Settle(1.6f);
            f.DebugBeginPlay(); yield return Settle(); Audit("hud"); CheckTopBar("hud top bar, day 1");
            f.DebugPause(); yield return Settle(); Audit("pause");
            f.DebugSettings(); yield return Settle(); Audit("settings");
            f.DebugCloseSettings(); yield return Settle(0.6f);
            f.DebugResume(); yield return Settle(0.6f);
            f.DebugResults(); yield return Settle(1.6f); Audit("results");
            f.DebugCloseMenus();
            f.DebugStart(1, true); yield return Settle(1.2f);
            f.DebugSunset(); yield return Settle(3.2f); Audit("sunset");
            f.DebugEnding(); yield return Settle(4f); Audit("ending");
            f.DebugStart(3, true); yield return Settle(1.6f); CheckTopBar("hud top bar, day 4");
            f.DebugStart(11, true); yield return Settle(1.6f); CheckTopBar("hud top bar, day 12 (7 needs)");
            foreach (int d in new[] { 0, 3, 11 }) yield return CheckFraming(d);
            yield return CheckHintClear(4);
            yield return WantBadges();
            Debug.Log($"[UiAudit] done: {passes} passed, {fails} failed");
            yield return new WaitForSecondsRealtime(0.3f);
            Application.Quit();
        }
    }
}
