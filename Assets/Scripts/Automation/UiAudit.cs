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

        static string Path(Transform t)
        {
            string p = t.name;
            for (int i = 0; i < 2 && t.parent != null; i++) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        IEnumerator Settle(float s = 1.2f) { yield return new WaitForSecondsRealtime(s); }

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
            f.DebugBeginPlay(); yield return Settle(); Audit("hud");
            f.DebugPause(); yield return Settle(); Audit("pause");
            f.DebugSettings(); yield return Settle(); Audit("settings");
            f.DebugCloseSettings(); yield return Settle(0.6f);
            f.DebugResume(); yield return Settle(0.6f);
            f.DebugResults(); yield return Settle(1.6f); Audit("results");
            f.DebugCloseMenus();
            f.DebugStart(1, true); yield return Settle(1.2f);
            f.DebugSunset(); yield return Settle(3.2f); Audit("sunset");
            f.DebugEnding(); yield return Settle(4f); Audit("ending");
            Debug.Log($"[UiAudit] done: {passes} passed, {fails} failed");
            yield return new WaitForSecondsRealtime(0.3f);
            Application.Quit();
        }
    }
}
