using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace PocketWeather
{
    /// <summary>
    /// Keyboard self-test, active with <c>-pwKeyTest</c>. Adds a virtual keyboard and drives the
    /// game with it from the title: Enter through the menus, arrows/WASD to fly, Space to rain, Esc to
    /// pause and resume, E to gust. Prints PASS/FAIL lines, then quits.
    /// </summary>
    public class KeyTest : MonoBehaviour
    {
        Keyboard kb;
        KeyboardState state;
        int passes, fails;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-pwKeyTest") < 0) return;
            DontDestroyOnLoad(new GameObject("KeyTest").AddComponent<KeyTest>().gameObject);
        }

        Level L => Level.Current;
        Cloud C => Cloud.Instance;
        GameFlow.State Now => GameFlow.I.Current;

        void Check(string name, bool ok, string detail = "")
        {
            Debug.Log($"[KeyTest] {(ok ? "PASS" : "FAIL")} {name}{(detail != "" ? " (" + detail + ")" : "")}");
            if (ok) passes++; else fails++;
        }

        void Set(Key k, bool down)
        {
            if (down) state.Press(k); else state.Release(k);
            InputSystem.QueueStateEvent(kb, state);
        }

        IEnumerator Press(Key k)
        {
            Set(k, true); yield return null; yield return null;
            Set(k, false); yield return null; yield return null;
        }

        IEnumerator WaitFor(Func<bool> cond, float timeout)
        {
            float t = 0;
            while (!cond() && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
        }

        float Dist2D(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        IEnumerator Start()
        {
            kb = InputSystem.AddDevice<Keyboard>();
            state = new KeyboardState();
            InputSystem.QueueStateEvent(kb, state);
            yield return WaitFor(() => GameFlow.I != null && Now == GameFlow.State.Title, 20f);
            yield return new WaitForSecondsRealtime(1.0f);

            // --- a brand-new player's first press goes straight to Day 1
            SaveData.Reset();
            yield return Press(Key.Enter);
            yield return WaitFor(() => Now == GameFlow.State.Intro, 5f);
            Check("a new player's first press opens Day 1's postcard", Now == GameFlow.State.Intro && L != null && L.Def.id == "level01", $"{Now}, {L?.Def.id}");
            SaveData.RecordPlay("level01");   // from now on a returning player
            GameFlow.I.DebugShowTitle();
            yield return new WaitForSecondsRealtime(1.0f);

            // --- menus
            yield return Press(Key.Enter);
            yield return WaitFor(() => Now == GameFlow.State.Map, 5f);
            Check("Enter on the title opens the map", Now == GameFlow.State.Map, Now.ToString());
            yield return new WaitForSecondsRealtime(0.8f);
            EventSystem.current.SetSelectedGameObject(GameObject.Find("Card0"));
            yield return Press(Key.RightArrow);
            yield return new WaitForSecondsRealtime(0.2f);
            var sel = EventSystem.current.currentSelectedGameObject;
            Check("arrow keys move between cards", sel != null && sel.name == "Card1", sel != null ? sel.name : "none");
            yield return Press(Key.LeftArrow);
            yield return new WaitForSecondsRealtime(0.2f);
            yield return Press(Key.Enter);
            yield return WaitFor(() => Now == GameFlow.State.Intro, 5f);
            Check("Enter on a card opens its postcard", Now == GameFlow.State.Intro, Now.ToString());
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Press(Key.Enter);
            yield return WaitFor(() => Now == GameFlow.State.Playing, 5f);
            Check("Enter on the postcard starts the day", Now == GameFlow.State.Playing, Now.ToString());
            yield return new WaitForSeconds(1.0f);

            // --- fly with arrows/WASD toward the first bed
            BedNeed bed = null;
            foreach (var n in L.Needs) if (n is BedNeed b) { bed = b; break; }
            var target = bed.transform.position;
            var from = C.GroundPoint;
            float t = 0;
            Key held = Key.None;
            while (t < 6f && Dist2D(C.GroundPoint, target) > 0.35f)
            {
                var d = target - C.GroundPoint;
                // one axis at a time, like a person tapping arrows: the bigger gap first
                Key want = Mathf.Abs(d.x) > Mathf.Abs(d.z) ? (d.x > 0 ? Key.D : Key.A) : (d.z > 0 ? Key.UpArrow : Key.DownArrow);
                if (want != held) { if (held != Key.None) Set(held, false); Set(want, true); held = want; }
                t += Time.deltaTime;
                yield return null;
            }
            if (held != Key.None) Set(held, false);
            var released = C.GroundPoint;
            yield return new WaitForSeconds(0.8f);
            Check("arrows/WASD fly Pip", Dist2D(from, released) > 2f && Dist2D(released, target) < 0.5f, $"travelled {Dist2D(from, released):0.0} in {t:0.0}s");
            Check("device switches to keyboard", C.Input.LastDevice == CloudInput.Device.Keys, C.Input.LastDevice.ToString());
            Check("Pip stops near where the keys were released", Dist2D(released, C.GroundPoint) < C.Radius * 1.1f, $"glided {Dist2D(released, C.GroundPoint):0.00}");

            // line up over the bed with short taps, the way a player corrects a glide, so the
            // whole shower lands on it (the glide alone can leave Pip half off the bed)
            for (int tries = 0; tries < 10 && Dist2D(C.GroundPoint, target) > 0.3f; tries++)
            {
                var d = target - C.GroundPoint;
                Key nudge = Mathf.Abs(d.x) > Mathf.Abs(d.z) ? (d.x > 0 ? Key.D : Key.A) : (d.z > 0 ? Key.UpArrow : Key.DownArrow);
                Set(nudge, true);
                yield return new WaitForSeconds(0.04f);
                Set(nudge, false);
                yield return WaitFor(() => C.Velocity.magnitude < 0.15f, 1.5f);
            }
            float m0 = bed.Moisture;
            Set(Key.Space, true);
            yield return new WaitForSeconds(1.5f);
            Check("holding Space rains", C.Raining);
            Check("rain waters the bed", bed.Moisture > m0 + 3f, $"{m0:0.0} -> {bed.Moisture:0.0}, {Dist2D(C.GroundPoint, target):0.00} from the bed");
            Set(Key.Space, false);
            yield return new WaitForSeconds(0.4f);
            Check("releasing Space stops the rain", !C.Raining);

            var hud = GameFlow.I.Hud;
            hud.ShowHint("Test hint", "drop", 30f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Press(Key.Escape);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("Esc pauses", Now == GameFlow.State.Paused, Now.ToString());
            Check("hints hide behind the pause menu", !hud.HintVisible);
            var pauseText = GameObject.Find("PauseScreen")?.GetComponent<PauseMenu>()?.ControlsText ?? "";
            Check("the pause menu lists the keyboard controls", pauseText.Contains("Space"), pauseText);
            yield return Press(Key.Escape);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("Esc resumes", Now == GameFlow.State.Playing, Now.ToString());
            Check("the hint comes back on resume", hud.HintVisible);
            hud.HideHint();

            // M mutes the music and brings it back at the player's own volume
            float vol0 = GameSettings.Music;
            GameSettings.Music = 0.42f;
            yield return Press(Key.M);
            float muted = GameSettings.Music;
            yield return Press(Key.M);
            Check("M mutes, then restores the player's music volume", muted < 0.01f && Mathf.Abs(GameSettings.Music - 0.42f) < 0.001f, $"{muted:0.00} then {GameSettings.Music:0.00}");
            GameSettings.Music = vol0;

            // --- level 4: E blows the way Pip is moving
            GameFlow.I.DebugStart(3, true);
            yield return new WaitForSeconds(1.0f);
            BoatNeed boat = null;
            foreach (var n in L.Needs) if (n is BoatNeed bt) { boat = bt; break; }
            var dir = boat.Goal - boat.Position; dir.y = 0; dir.Normalize();
            C.Teleport(boat.Position - dir * 1.6f);
            yield return new WaitForSeconds(0.5f);
            Key push = Mathf.Abs(dir.x) > Mathf.Abs(dir.z) ? (dir.x > 0 ? Key.RightArrow : Key.LeftArrow) : (dir.z > 0 ? Key.UpArrow : Key.DownArrow);
            float used0 = C.WaterUsed;
            Set(push, true);
            yield return new WaitForSeconds(0.15f);
            yield return Press(Key.E);
            Set(push, false);
            yield return new WaitForSeconds(0.6f);
            Check("E blows a gust", C.WaterUsed >= used0 + Cloud.GustCost - 0.01f, $"water used {C.WaterUsed - used0:0.0}");
            Check("the gust pushes the boat", boat.Velocity.magnitude > 0.2f || boat.Met, $"boat speed {boat.Velocity.magnitude:0.00}");

            // --- pause menu: Restart, then Map; results: Next day (real selection + Enter)
            yield return new WaitForSeconds(0.8f);
            float hour0 = L.Hour;
            yield return Press(Key.Escape);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Press(Key.DownArrow);                          // Resume -> Restart
            yield return new WaitForSecondsRealtime(0.2f);
            var cur = EventSystem.current.currentSelectedGameObject;
            Check("arrows move through the pause menu", cur != null && cur.name == "Btn_Restart", cur != null ? cur.name : "none");
            yield return Press(Key.Enter);
            yield return WaitFor(() => Now == GameFlow.State.Intro || (Now == GameFlow.State.Playing && L.Hour < hour0 - 0.01f), 6f);
            Check("Restart reloads the day", L != null && L.Def.id == "level04" && L.Hour <= L.Def.startHour + 0.05f, $"{Now}, hour {L?.Hour:0.00}");
            yield return new WaitForSecondsRealtime(1.2f);
            if (Now == GameFlow.State.Intro) { yield return Press(Key.Enter); yield return WaitFor(() => Now == GameFlow.State.Playing, 5f); }
            yield return new WaitForSeconds(0.5f);
            yield return Press(Key.Escape);
            yield return new WaitForSecondsRealtime(0.5f);
            for (int i = 0; i < 3; i++) { yield return Press(Key.DownArrow); yield return new WaitForSecondsRealtime(0.15f); }
            yield return Press(Key.Enter);
            yield return WaitFor(() => Now == GameFlow.State.Map, 6f);
            Check("pause > Map goes to the map", Now == GameFlow.State.Map, Now.ToString());

            GameFlow.I.DebugStart(0, true);
            yield return new WaitForSeconds(1.0f);
            GameFlow.I.DebugResults();
            yield return new WaitForSecondsRealtime(1.8f);
            bool bestShown = false;
            foreach (var txt in FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Exclude))
                if (txt.isActiveAndEnabled && txt.text.Contains("your best is")) bestShown = true;
            Check("results show the best finishing time", bestShown);
            cur = EventSystem.current.currentSelectedGameObject;
            Check("results card selects Next day", cur != null && cur.name == "Btn_Next day", cur != null ? cur.name : "none");
            yield return Press(Key.Enter);
            yield return WaitFor(() => Now == GameFlow.State.Intro, 6f);
            Check("Next day opens day 2's postcard", Now == GameFlow.State.Intro && L != null && L.Def.id == "level02", $"{Now}, {L?.Def.id}");

            // --- days that bring in a new idea say so within a few seconds (fresh hints each start)
            var dayHints = new (int day, string words)[] { (5, "washing"), (7, "sails"), (9, "fire"), (10, "sandcastle") };
            foreach (var (day, words) in dayHints)
            {
                GameFlow.I.DebugStart(day - 1, true);
                float waited = 0;
                while (waited < 3f && !(hud.HintVisible && hud.HintText.Contains(words))) { waited += Time.unscaledDeltaTime; yield return null; }
                Check($"day {day} hints at its new idea", hud.HintVisible && hud.HintText.Contains(words), $"'{hud.HintText}' after {waited:0.0}s");
            }
            GameFlow.I.DebugStart(8, true);
            yield return new WaitForSeconds(9.5f);
            Check("campfire night follows up with the campfire", hud.HintText.Contains("campfire"), hud.HintText);

            Debug.Log($"[KeyTest] done: {passes} passed, {fails} failed");
            yield return new WaitForSecondsRealtime(0.3f);
            Application.Quit();
        }
    }
}
