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
            bool ok = problems.Count == 0;
            Debug.Log($"[UiAudit] {(ok ? "PASS" : "FAIL")} {what}: scale {canvas.scaleFactor:0.000} at {Screen.width}x{Screen.height}" +
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
            f.DebugStart(11, true); yield return Settle(1.6f); CheckTopBar("hud top bar, day 12 (7 needs)");
            yield return WantBadges();
            Debug.Log($"[UiAudit] done: {passes} passed, {fails} failed");
            yield return new WaitForSecondsRealtime(0.3f);
            Application.Quit();
        }
    }
}
