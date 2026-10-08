using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace PocketWeather
{
    /// <summary>
    /// Gamepad self-test, active with <c>-pwPadTest</c>. Adds a virtual Gamepad to the Input System
    /// and drives the whole game with it, from the title screen through the map and postcard into
    /// play: stick to fly, A to rain, X + right stick to gust, Start/B for pause. Prints PASS/FAIL
    /// lines, then quits.
    /// </summary>
    public class PadTest : MonoBehaviour
    {
        Gamepad pad;
        GamepadState state;
        int passes, fails;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-pwPadTest") < 0) return;
            DontDestroyOnLoad(new GameObject("PadTest").AddComponent<PadTest>().gameObject);
        }

        Level L => Level.Current;
        Cloud C => Cloud.Instance;
        GameFlow.State Now => GameFlow.I.Current;

        void Check(string name, bool ok, string detail = "")
        {
            Debug.Log($"[PadTest] {(ok ? "PASS" : "FAIL")} {name}{(detail != "" ? " (" + detail + ")" : "")}");
            if (ok) passes++; else fails++;
        }

        // ------------------------------------------------------------------ pad primitives
        void Apply() => InputSystem.QueueStateEvent(pad, state);

        void Stick(Vector2 left, Vector2 right = default)
        {
            state.leftStick = left;
            state.rightStick = right;
            Apply();
        }

        void SetButton(GamepadButton b, bool down)
        {
            state = state.WithButton(b, down);
            Apply();
        }

        IEnumerator Press(GamepadButton b)
        {
            SetButton(b, true);
            yield return null;
            yield return null;
            SetButton(b, false);
            yield return null;
            yield return null;
        }

        /// <summary>The focus ring is showing, settled around this control.</summary>
        static bool RingAround(GameObject go, out string detail)
        {
            if (go == null) { detail = "nothing selected"; return false; }
            var want = FocusRing.RectFor((RectTransform)go.transform, out _);
            var r = FocusRing.ScreenRect;
            float off = Vector2.Distance(r.center, want.center);
            detail = $"visible {FocusRing.Visible}, around {(FocusRing.Target != null ? FocusRing.Target.name : "none")}, {off:0.0} px off centre, {r.width:0}x{r.height:0} for {want.width:0}x{want.height:0}";
            return FocusRing.Visible && FocusRing.Target == go.transform && off < 3f && r.width >= want.width - 2f && r.height >= want.height - 2f;
        }

        IEnumerator WaitFor(Func<bool> cond, float timeout)
        {
            float t = 0;
            while (!cond() && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
        }

        string Selected => EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
            ? EventSystem.current.currentSelectedGameObject.name : "none";

        float Dist2D(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        // ------------------------------------------------------------------ the test
        IEnumerator Start()
        {
            pad = InputSystem.AddDevice<Gamepad>();
            state = new GamepadState();
            Apply();
            yield return WaitFor(() => GameFlow.I != null && Now == GameFlow.State.Title, 20f);
            yield return new WaitForSecondsRealtime(1.0f);

            // --- menus
            Check("game boots to the title", Now == GameFlow.State.Title, Now.ToString());
            yield return Press(GamepadButton.North);
            Check("after a pad button, the title says press A", GameFlow.I.TitlePrompt == "Press A to play", GameFlow.I.TitlePrompt);
            SaveData.RecordPlay("level01");   // a returning player: A goes to the map (a brand-new one goes to Day 1, see KeyTest)
            yield return Press(GamepadButton.South);
            yield return WaitFor(() => Now == GameFlow.State.Map, 5f);
            Check("A on the title opens the map", Now == GameFlow.State.Map, Now.ToString());
            yield return new WaitForSecondsRealtime(0.8f);
            string first = Selected;
            Check("a level card is selected", first.StartsWith("Card"), first);
            // navigate from the first card (the auto-selected one depends on save progress)
            first = "Card0";
            EventSystem.current.SetSelectedGameObject(GameObject.Find(first));
            yield return null;
            yield return Press(GamepadButton.DpadRight);
            yield return new WaitForSecondsRealtime(0.2f);
            string second = Selected;
            Check("d-pad moves between cards", second.StartsWith("Card") && second != first, $"{first} -> {second}");
            yield return Press(GamepadButton.DpadLeft);
            yield return new WaitForSecondsRealtime(0.2f);
            Check("d-pad moves back", Selected == first, Selected);
            yield return Press(GamepadButton.East);
            yield return WaitFor(() => Now == GameFlow.State.Title, 5f);
            Check("B on the map goes back to the title", Now == GameFlow.State.Title, Now.ToString());
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Press(GamepadButton.South);
            yield return WaitFor(() => Now == GameFlow.State.Map, 5f);
            yield return new WaitForSecondsRealtime(0.8f);
            // pick the first card (always unlocked)
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(GameObject.Find("Card0"));
            yield return null;
            yield return Press(GamepadButton.South);
            yield return WaitFor(() => Now == GameFlow.State.Intro, 5f);
            Check("A on a card opens its postcard", Now == GameFlow.State.Intro, Now.ToString());
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Press(GamepadButton.South);
            yield return WaitFor(() => Now == GameFlow.State.Playing, 5f);
            Check("A on the postcard starts the day", Now == GameFlow.State.Playing, Now.ToString());
            yield return new WaitForSeconds(1.0f);

            // --- flying and raining in level 1
            var bed = FindFirst<BedNeed>();
            var target = bed.transform.position;
            float t = 0;
            var from = C.GroundPoint;
            while (t < 6f && Dist2D(C.transform.position, target) > 0.3f)
            {
                var d = target - C.GroundPoint;
                Stick(new Vector2(d.x, d.z).normalized);   // full tilt, let go on arrival like a player would
                t += Time.deltaTime;
                yield return null;
            }
            Stick(Vector2.zero);
            var released = C.GroundPoint;
            yield return new WaitForSeconds(0.8f);
            Check("left stick flies Pip", Dist2D(released, target) < 0.4f && Dist2D(from, released) > 2f,
                $"travelled {Dist2D(from, released):0.0} in {t:0.0}s");
            Check("Pip stops within a cloud-width of where the stick was released", Dist2D(released, C.transform.position) < C.Radius * 1.1f,
                $"glided {Dist2D(released, C.transform.position):0.00}");
            Check("device switches to gamepad", C.Input.LastDevice == CloudInput.Device.Pad, C.Input.LastDevice.ToString());
            var rest = C.transform.position;
            yield return new WaitForSeconds(0.5f);
            Check("Pip stays put afterwards", Dist2D(rest, C.transform.position) < 0.25f, $"drifted {Dist2D(rest, C.transform.position):0.00}");
            // line up over the bed with small stick nudges, the way a player corrects a glide, so the
            // whole shower lands on it (the glide alone can leave Pip half off the bed)
            for (int tries = 0; tries < 10 && Dist2D(C.transform.position, target) > 0.3f; tries++)
            {
                var d = target - C.transform.position;
                Stick(new Vector2(d.x, d.z).normalized * 0.5f);
                yield return new WaitForSeconds(0.06f);
                Stick(Vector2.zero);
                float settle = 0;
                while (C.Velocity.magnitude > 0.15f && settle < 1.5f) { settle += Time.deltaTime; yield return null; }
            }
            float m0 = bed.Moisture;
            SetButton(GamepadButton.South, true);
            yield return new WaitForSeconds(1.5f);
            Check("holding A rains", C.Raining);
            Check("rain waters the bed", bed.Moisture > m0 + 3f, $"{m0:0.0} -> {bed.Moisture:0.0}, {Dist2D(C.transform.position, target):0.00} from the bed");
            SetButton(GamepadButton.South, false);
            yield return new WaitForSeconds(0.4f);
            Check("releasing A stops the rain", !C.Raining);

            // --- accessibility: toggle rain (a press starts it, another stops it)
            bool hadToggle = GameSettings.RainToggle;
            GameSettings.RainToggle = true;
            C.AddWater(60f);   // toggle rain switches itself off when Pip runs dry, which isn't what's tested here
            yield return Press(GamepadButton.South);
            yield return new WaitForSeconds(0.6f);
            Check("toggle mode: one press keeps it raining", C.Raining);
            yield return Press(GamepadButton.South);
            yield return new WaitForSeconds(0.4f);
            Check("toggle mode: a second press stops it", !C.Raining);
            GameSettings.RainToggle = hadToggle;

            // --- pause with Start, resume with B
            yield return Press(GamepadButton.Start);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("Start pauses", Now == GameFlow.State.Paused, Now.ToString());
            Check("pause menu has a selection", Selected != "none", Selected);
            yield return new WaitForSecondsRealtime(0.3f);
            Check("the focus ring is around the pause menu's selection", RingAround(EventSystem.current.currentSelectedGameObject, out var ringPause), ringPause);
            var pauseText = GameObject.Find("PauseScreen")?.GetComponent<PauseMenu>()?.ControlsText ?? "";
            Check("the pause menu lists the gamepad controls", pauseText.Contains("hold A"), pauseText);
            yield return Press(GamepadButton.East);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("B resumes", Now == GameFlow.State.Playing, Now.ToString());

            // --- the controller drops out mid-flight: the day pauses and the menu says why; a pad
            // coming back puts its controls back, and B on it resumes
            Stick(new Vector2(1f, 0f));
            yield return new WaitForSeconds(0.3f);
            float hourAtDrop = L.Hour;
            InputSystem.RemoveDevice(pad);
            yield return new WaitForSecondsRealtime(0.4f);
            var pauseMenu = GameObject.Find("PauseScreen")?.GetComponent<PauseMenu>();
            Check("a controller dropping out mid-flight pauses the day", Now == GameFlow.State.Paused && GameFlow.I.PadLost, $"{Now}, pad lost {GameFlow.I.PadLost}");
            Check("the pause menu says the controller dropped out", pauseMenu != null && pauseMenu.ControlsText == PauseMenu.ControllerLostLine, pauseMenu?.ControlsText ?? "no menu");
            yield return new WaitForSecondsRealtime(0.6f);
            Check("the day stays paused while it's away", Now == GameFlow.State.Paused && Mathf.Abs(L.Hour - hourAtDrop) < 0.01f, $"{Now}, hour {L.Hour:0.000} (was {hourAtDrop:0.000})");
            pad = InputSystem.AddDevice<Gamepad>();
            state = new GamepadState();
            Apply();
            yield return new WaitForSecondsRealtime(0.4f);
            Check("a controller coming back puts its controls back on the menu", pauseMenu != null && pauseMenu.ControlsText.Contains("hold A"), pauseMenu?.ControlsText ?? "no menu");
            yield return Press(GamepadButton.East);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("B on the controller that came back resumes", Now == GameFlow.State.Playing, Now.ToString());

            // --- flying with the keys, an idle pad dropping out leaves the day alone
            var keys = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keys, new KeyboardState(Key.LeftArrow));
            yield return new WaitForSeconds(0.3f);
            InputSystem.QueueStateEvent(keys, new KeyboardState());
            yield return new WaitForSeconds(0.2f);
            var flying = C.Input.LastDevice;
            InputSystem.RemoveDevice(pad);
            yield return new WaitForSecondsRealtime(0.4f);
            Check("an idle pad dropping out under the keys doesn't pause", flying == CloudInput.Device.Keys && Now == GameFlow.State.Playing, $"flying with {flying}, {Now}");
            InputSystem.RemoveDevice(keys);
            pad = InputSystem.AddDevice<Gamepad>();
            state = new GamepadState();
            Apply();
            yield return new WaitForSeconds(0.3f);

            // --- level 4: gust the boat with X, aimed by the right stick
            GameFlow.I.DebugStart(3, true);
            yield return new WaitForSeconds(1.0f);
            var boat = FindFirst<BoatNeed>();
            var dir = boat.Goal - boat.Position; dir.y = 0; dir.Normalize();
            var spot = boat.Position - dir * 1.4f;
            C.Teleport(new Vector3(spot.x, 0, spot.z));
            yield return new WaitForSeconds(0.5f);
            float used0 = C.WaterUsed;
            Stick(Vector2.zero, new Vector2(dir.x, dir.z));
            yield return null;
            yield return Press(GamepadButton.West);
            Stick(Vector2.zero);
            yield return new WaitForSeconds(0.3f);
            float align = Vector3.Dot(C.GustDirection.normalized, dir);
            yield return new WaitForSeconds(0.5f);
            Check("X blows a gust", C.WaterUsed >= used0 + Cloud.GustCost - 0.01f, $"water used {C.WaterUsed - used0:0.0}");
            Check("right stick aims the gust", align > 0.9f, $"alignment {align:0.00}");
            Check("gust pushes the boat", boat.Velocity.magnitude > 0.2f || boat.Met, $"boat speed {boat.Velocity.magnitude:0.00}");
            // an un-aimed gust goes the way Pip faces
            used0 = C.WaterUsed;
            yield return new WaitForSeconds(0.4f);
            yield return Press(GamepadButton.RightShoulder);
            yield return new WaitForSeconds(0.3f);
            Check("RB also gusts", C.WaterUsed >= used0 + Cloud.GustCost - 0.01f, $"water used {C.WaterUsed - used0:0.0}");

            // --- Settings > Graphics with the d-pad: up to the slider, right a step, B to close
            int gfx0 = GameSettings.Graphics;
            GameSettings.Graphics = (int)Quality.Mode.Low;
            Quality.Apply();
            GameFlow.I.DebugShowMap();
            yield return new WaitForSecondsRealtime(0.8f);
            GameFlow.I.DebugSettingsFromMap();
            yield return new WaitForSecondsRealtime(0.6f);
            var gfx = GameFlow.I.Settings.GraphicsSlider;
            for (int i = 0; i < 16 && Selected != gfx.name; i++)
            {
                yield return Press(GamepadButton.DpadUp);
                yield return new WaitForSecondsRealtime(0.1f);
            }
            Check("the d-pad reaches the graphics slider", Selected == gfx.name, Selected);
            yield return new WaitForSecondsRealtime(0.35f);
            Check("the focus ring is around the graphics slider", RingAround(gfx.gameObject, out var ringGfx), ringGfx);
            yield return Press(GamepadButton.DpadRight);
            yield return new WaitForSecondsRealtime(0.2f);
            Check("d-pad right steps Low to Medium, saved and applied", GameSettings.Graphics == (int)Quality.Mode.Medium && Quality.Current == Quality.Tier.Medium,
                  $"pref {GameSettings.Graphics}, tier {Quality.Current}");
            yield return Press(GamepadButton.East);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("B closes settings", !GameFlow.I.Settings.IsOpen, Now.ToString());
            GameSettings.Graphics = gfx0;
            Quality.Apply();

            Debug.Log($"[PadTest] done: {passes} passed, {fails} failed");
            yield return new WaitForSecondsRealtime(0.3f);
            Application.Quit();
        }

        T FindFirst<T>() where T : Need
        {
            foreach (var n in L.Needs) if (n is T x) return x;
            return null;
        }
    }
}
