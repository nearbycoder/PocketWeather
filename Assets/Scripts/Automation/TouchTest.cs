using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace PocketWeather
{
    /// <summary>
    /// Touch self-test, active with <c>-pwTouchTest</c>. Adds a virtual Touchscreen to the Input
    /// System and plays real touch gestures through it (the same code path as a finger): drag to
    /// fly, hold still to rain, drag while raining, flick to gust, tap HUD and menu buttons, and
    /// checks the on-screen touch buttons appear. Prints PASS/FAIL lines, then quits.
    /// </summary>
    public class TouchTest : MonoBehaviour
    {
        Touchscreen ts;
        int passes, fails;
        readonly List<string> report = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-pwTouchTest") < 0) return;
            DontDestroyOnLoad(new GameObject("TouchTest").AddComponent<TouchTest>().gameObject);
        }

        Level L => Level.Current;
        Cloud C => Cloud.Instance;

        void Check(string name, bool ok, string detail = "")
        {
            string line = $"{(ok ? "PASS" : "FAIL")} {name}{(detail != "" ? " (" + detail + ")" : "")}";
            Debug.Log("[TouchTest] " + line);
            report.Add(line);
            if (ok) passes++; else fails++;
        }

        // ------------------------------------------------------------------ touch primitives
        void Send(TouchPhase phase, Vector2 pos)
        {
            InputSystem.QueueStateEvent(ts, new TouchState { touchId = 1, phase = phase, position = pos, pressure = 1f });
        }

        Vector2 Screen(Vector3 world) => Camera.main.WorldToScreenPoint(world);

        IEnumerator Drag(Vector2 from, Vector2 to, float seconds, bool release = true, bool begin = true)
        {
            if (begin) { Send(TouchPhase.Began, from); yield return null; }
            float t = 0;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                Send(TouchPhase.Moved, Vector2.Lerp(from, to, Mathf.Clamp01(t / seconds)));
                yield return null;
            }
            if (release) { Send(TouchPhase.Ended, to); yield return null; yield return null; }
        }

        IEnumerator Hold(Vector2 at, float seconds)
        {
            float t = 0;
            while (t < seconds) { t += Time.unscaledDeltaTime; Send(TouchPhase.Stationary, at); yield return null; }
        }

        IEnumerator Tap(Vector2 at)
        {
            Send(TouchPhase.Began, at);
            yield return null;
            yield return null;
            Send(TouchPhase.Ended, at);
            yield return null;
            yield return null;
        }

        Vector2 UiCenter(Transform t) => RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)t).position);

        Transform FindLabel(string text)
        {
            foreach (var l in FindObjectsByType<Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (l.text == text && l.gameObject.activeInHierarchy) return l.transform.parent;
            return null;
        }

        float Dist2D(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        // ------------------------------------------------------------------ the test
        IEnumerator Start()
        {
            ts = InputSystem.AddDevice<Touchscreen>();
            yield return new WaitForSecondsRealtime(1.5f);
            GameSettings.TouchButtons = 0;

            // --- title: the corner buttons must win over the full-screen "tap to play"
            float tw = 0;
            while (GameFlow.I.Current != GameFlow.State.Title && tw < 15f) { tw += Time.unscaledDeltaTime; yield return null; }
            yield return new WaitForSecondsRealtime(1.0f);
            var gear = GameObject.Find("Settings");
            if (gear != null) yield return Tap(UiCenter(gear.transform));
            yield return new WaitForSecondsRealtime(0.6f);
            var done = FindLabel("Done");
            Check("tap the title's settings gear", done != null && GameFlow.I.Current == GameFlow.State.Title, done == null ? "settings didn't open" : "");
            if (done != null) yield return Tap(UiCenter(done));
            yield return new WaitForSecondsRealtime(0.6f);
            Check("closing settings returns to the title", GameFlow.I.Current == GameFlow.State.Title && FindLabel("Done") == null);

            // --- level 1: drag, hold-to-rain, drag-while-raining
            GameFlow.I.DebugStart(0, true);
            yield return new WaitForSeconds(1.0f);
            var bed = L.FindNeed("bedA") as BedNeed ?? FindFirst<BedNeed>();
            var start = C.GroundPoint;
            yield return Drag(Screen(start), Screen(bed.transform.position), 0.8f, release: false);
            yield return Hold(Screen(bed.transform.position), 0.05f);
            Check("drag moves Pip to the finger", Dist2D(C.transform.position, bed.transform.position) < 0.5f,
                $"{Dist2D(C.transform.position, bed.transform.position):0.00} from target");
            Check("device switches to touch", C.Input.LastDevice == CloudInput.Device.Touch, C.Input.LastDevice.ToString());
            Check("no rain while dragging", !C.Raining);
            float m0 = bed.Moisture;
            yield return Hold(Screen(bed.transform.position), 0.9f);
            Check("holding still starts rain", C.Raining && C.Input.HoldCharge >= 1f, $"charge {C.Input.HoldCharge:0.00}");
            yield return Hold(Screen(bed.transform.position), 0.8f);
            Check("rain waters the bed", bed.Moisture > m0 + 3f, $"{m0:0.0} -> {bed.Moisture:0.0}");
            var side = bed.transform.position + new Vector3(1.5f, 0, 0);
            yield return Drag(Screen(bed.transform.position), Screen(side), 0.6f, release: false, begin: false);
            Check("dragging keeps raining", C.Raining);
            Send(TouchPhase.Ended, Screen(side));
            yield return new WaitForSeconds(0.4f);
            Check("lifting the finger stops rain", !C.Raining);
            yield return null;
            Check("touch buttons appear for touch players", GameFlow.I.Hud.TouchButtonsVisible);
            GameSettings.TouchButtons = 2;   // Settings > Touch buttons: Off
            yield return null; yield return null;
            Check("touch buttons Off hides them, even for touch players", !GameFlow.I.Hud.TouchButtonsVisible);
            GameSettings.TouchButtons = 0;
            yield return null;

            // --- tap the HUD pause button, then Resume in the menu
            yield return new WaitForSeconds(0.6f);
            Vector3 before = C.transform.position;
            yield return Tap(UiCenter(GameFlow.I.Hud.PauseButton.transform));
            yield return new WaitForSecondsRealtime(0.5f);
            Check("tap pause button", GameFlow.I.Current == GameFlow.State.Paused, GameFlow.I.Current.ToString());
            var pauseText = GameObject.Find("PauseScreen")?.GetComponent<PauseMenu>()?.ControlsText ?? "";
            Check("the pause menu lists the touch controls", pauseText.Contains("flick"), pauseText);
            var resume = FindLabel("Resume");
            if (resume != null) yield return Tap(UiCenter(resume));
            yield return new WaitForSecondsRealtime(0.6f);
            Check("tap Resume", GameFlow.I.Current == GameFlow.State.Playing, GameFlow.I.Current.ToString());
            yield return new WaitForSeconds(0.3f);
            Check("tapping UI doesn't move Pip", Dist2D(before, C.transform.position) < 0.3f);

            // --- level 4: flick to gust the boat
            GameFlow.I.DebugStart(3, true);
            yield return new WaitForSeconds(1.0f);
            var boat = FindFirst<BoatNeed>();
            var dir = boat.Goal - boat.Position; dir.y = 0; dir.Normalize();
            var spot = boat.Position - dir * 1.4f;
            C.Teleport(new Vector3(spot.x, 0, spot.z));
            yield return new WaitForSeconds(0.5f);
            float used0 = C.WaterUsed;
            var pipBefore = C.transform.position;
            Vector2 a = Screen(C.GroundPoint), b = Screen(C.GroundPoint + dir * 2.5f);
            yield return Drag(a, b, 0.09f);
            yield return new WaitForSeconds(0.3f);
            float align = Vector3.Dot(C.GustDirection.normalized, dir);
            yield return new WaitForSeconds(0.5f);
            Check("flick blows a gust", C.WaterUsed >= used0 + Cloud.GustCost - 0.01f, $"water used {C.WaterUsed - used0:0.0}");
            Check("gust pushes the boat", boat.Velocity.magnitude > 0.2f || boat.Met, $"boat speed {boat.Velocity.magnitude:0.00}, boat {boat.Position}, spot {spot}, pip {C.GroundPoint}, gust {C.GustDirection}");
            Check("gust goes the flick's way", align > 0.8f, $"alignment {align:0.00}");
            Check("a flick doesn't drag Pip away", Dist2D(pipBefore, C.transform.position) < 0.7f, $"moved {Dist2D(pipBefore, C.transform.position):0.00}");

            // --- a slow drag must not count as a flick
            used0 = C.WaterUsed;
            yield return Drag(Screen(C.GroundPoint), Screen(C.GroundPoint + dir * 1.5f), 0.7f);
            yield return new WaitForSeconds(0.5f);
            Check("slow drag is not a gust", C.WaterUsed - used0 < 1f, $"water used {C.WaterUsed - used0:0.0}");

            Debug.Log($"[TouchTest] done: {passes} passed, {fails} failed");
            yield return new WaitForSecondsRealtime(0.3f);
            Application.Quit();
        }

        T FindFirst<T>() where T : Need
        {
            foreach (var n in L.Needs) if (n is T t) return t;
            return null;
        }
    }
}
