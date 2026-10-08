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

        static bool TextShowing(string part)
        {
            foreach (var t in FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Exclude))
                if (t.isActiveAndEnabled && t.text.Contains(part)) return true;
            return false;
        }

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

            // --- the title's prompt speaks mouse on a desktop, until a key is pressed
            Check("the title says click on a desktop", GameFlow.I.TitlePrompt == "Click to play", GameFlow.I.TitlePrompt);
            yield return Press(Key.LeftShift);
            Check("after a key, the title says press Enter", GameFlow.I.TitlePrompt == "Press Enter to play", GameFlow.I.TitlePrompt);

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

            // --- pause menu: Restart (which asks first, this far into the day), then Map early in a
            // fresh day (which doesn't); results: Next day (real selection + Enter)
            yield return new WaitForSeconds(0.8f);
            yield return WaitFor(() => L.Elapsed > GameFlow.RestartAsksAfter + 0.5f, 10f);
            yield return Press(Key.Escape);
            yield return new WaitForSecondsRealtime(0.5f);
            float hour0 = L.Hour;                                       // the clock has stopped
            yield return Press(Key.DownArrow);                          // Resume -> Restart
            yield return new WaitForSecondsRealtime(0.2f);
            var cur = EventSystem.current.currentSelectedGameObject;
            Check("arrows move through the pause menu", cur != null && cur.name == "Btn_Restart", cur != null ? cur.name : "none");
            var pauseMenu = GameObject.Find("PauseScreen")?.GetComponent<PauseMenu>();
            yield return Press(Key.Enter);
            yield return new WaitForSecondsRealtime(0.6f);
            Check("Restart asks first, well into a day", Now == GameFlow.State.Paused && L.Hour == hour0 && pauseMenu != null && pauseMenu.RestartLabel == "Sure?",
                  $"{Now}, hour {L.Hour:0.00} (was {hour0:0.00}), label '{pauseMenu?.RestartLabel}', {L.Elapsed:0.0}s in");
            yield return Press(Key.UpArrow);
            yield return new WaitForSecondsRealtime(0.2f);
            Check("moving off Restart takes the question back", pauseMenu != null && pauseMenu.RestartLabel == "Restart", $"label '{pauseMenu?.RestartLabel}'");
            yield return Press(Key.DownArrow);
            yield return new WaitForSecondsRealtime(0.2f);
            yield return Press(Key.Enter);
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Press(Key.Enter);
            yield return WaitFor(() => Now == GameFlow.State.Playing && L.Hour < hour0 - 0.01f, 6f);
            Check("Restart goes straight back into the day, with no postcard", L != null && L.Def.id == "level04" && Now == GameFlow.State.Playing && !GameFlow.I.PostcardOpen && L.Hour <= L.Def.startHour + 0.05f,
                  $"{Now}, postcard {GameFlow.I.PostcardOpen}, hour {L?.Hour:0.00}");
            yield return new WaitForSecondsRealtime(1.2f);
            yield return new WaitForSeconds(0.5f);
            yield return Press(Key.Escape);
            yield return new WaitForSecondsRealtime(0.5f);
            for (int i = 0; i < 3; i++) { yield return Press(Key.DownArrow); yield return new WaitForSecondsRealtime(0.15f); }
            yield return Press(Key.Enter);
            yield return WaitFor(() => Now == GameFlow.State.Map, 6f);
            Check("pause > Map early in a day goes straight to the map", Now == GameFlow.State.Map, Now.ToString());

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

            // --- the first time a day is saved, its results card says its Encore is open; the next
            // time, it doesn't
            SaveData.DebugForget("level03");
            for (int round = 0; round < 2; round++)
            {
                GameFlow.I.DebugCloseMenus();
                GameFlow.I.DebugStart(2, true);
                yield return new WaitForSeconds(0.8f);
                GameFlow.I.DebugSaveDay();
                yield return WaitFor(() => Now == GameFlow.State.Results, 10f);
                yield return new WaitForSecondsRealtime(0.8f);
                bool note = GameFlow.I.ResultsEncoreNote && TextShowing("Encore unlocked");
                if (round == 0) Check("a day's first save says its Encore is open", Now == GameFlow.State.Results && note && SaveData.EncoreUnlocked("level03"), $"{Now}, note {note}");
                else Check("saving it again doesn't repeat the Encore note", Now == GameFlow.State.Results && !note, $"{Now}, note {note}");
            }
            GameFlow.I.DebugCloseMenus();

            // --- Encore: a saved day's postcard offers it, and it runs as a scorcher
            SaveData.Award("level01", SaveData.StampSaved);
            GameFlow.I.DebugStart(0, false);
            GameFlow.I.DebugShowPostcard();
            yield return new WaitForSecondsRealtime(1.0f);
            var encoreBtn = GameObject.Find("Encore");
            Check("a saved day's postcard offers its Encore", encoreBtn != null && encoreBtn.activeInHierarchy);
            if (encoreBtn != null)
            {
                EventSystem.current.SetSelectedGameObject(encoreBtn);
                yield return Press(Key.Enter);
                yield return WaitFor(() => Now == GameFlow.State.Intro && L != null && L.Def.encore, 8f);
                yield return new WaitForSecondsRealtime(1.0f);
                Check("Enter on Encore opens Day 1's Encore postcard", L != null && L.Def.encore && Now == GameFlow.State.Intro, $"{Now}, encore {L?.Def.encore}");
                yield return Press(Key.Enter);
                yield return WaitFor(() => Now == GameFlow.State.Playing, 5f);
                bool rules = L.Def.encore && Mathf.Abs(L.Def.dayLength - 120f * LevelLibrary.EncoreDayScale) < 0.01f
                             && C.Water <= 30f * LevelLibrary.EncoreWaterScale + 0.5f && L.Def.island.dryRate >= LevelLibrary.EncoreDryRate;
                Check("the Encore runs as a scorcher (shorter day, half water, drying beds)", rules,
                      $"day {L.Def.dayLength:0}s, water {C.Water:0}, dry {L.Def.island.dryRate:0.00}");

                // --- saving it records the scorcher's own best, and leaves the ordinary day's alone
                SaveData.RecordFinish("level01", 10.5f);   // the ordinary day was finished at 10:30 once
                float dayBest = SaveData.Get("level01").bestHour;
                yield return new WaitForSeconds(1.0f);
                float hour = L.Hour;
                GameFlow.I.DebugSaveDay();
                yield return WaitFor(() => Now == GameFlow.State.Results, 10f);
                yield return new WaitForSecondsRealtime(1.0f);
                var rec = SaveData.Get("level01");
                Check("saving an Encore records the scorcher's best", rec.HasEncoreBest && Mathf.Abs(rec.encoreBest - hour) < 0.1f && Mathf.Approximately(rec.bestHour, dayBest),
                      $"scorcher best {rec.encoreBest:0.00} (saved at about {hour:0.00}), day best {rec.bestHour:0.00} (was {dayBest:0.00})");
                string want = "Scorcher best: " + Postcard.FormatHour(rec.encoreBest);
                GameFlow.I.DebugCloseMenus();
                GameFlow.I.DebugStart(0, false, true);
                GameFlow.I.DebugShowPostcard();
                yield return new WaitForSecondsRealtime(1.0f);
                Check("the Encore postcard shows the scorcher's best", TextShowing(want) && !TextShowing("Your best"), want);
                GameFlow.I.DebugStart(0, false);
                GameFlow.I.DebugShowPostcard();
                yield return new WaitForSecondsRealtime(1.0f);
                Check("the day's own postcard still shows the day's best", !TextShowing("Scorcher best") &&
                      (dayBest > 90f ? !TextShowing("Your best") : TextShowing("Your best: " + Postcard.FormatHour(dayBest))), $"day best {dayBest:0.00}");
            }

            // --- the wedding bouquet: the smallest Pip, waiting on the ring, catches it whichever way
            // it's thrown; a Pip away from the ring doesn't
            GameFlow.I.DebugCloseMenus();
            GameFlow.I.DebugStart(11, true);
            yield return new WaitForSeconds(1.0f);
            var script = L.GetComponent<LevelScript>();
            var throws = new (float angle, float off)[] { (-40f, 0f), (0f, 0f), (40f, 0f), (0f, 2.5f) };
            foreach (var (angle, off) in throws)
            {
                C.SetWater(0);
                LevelScript.DebugThrowAngle = angle;
                script.DebugToss();
                yield return WaitFor(() => script.BouquetComing, 3f);
                var spot = script.BouquetCatchSpot + Vector3.right * off;
                for (float w = 0; script.BouquetComing && w < 6f; w += Time.deltaTime)
                {
                    C.SetWater(0);
                    C.Input.Virtual(spot, false);
                    yield return null;
                }
                string detail = $"thrown at {angle:0}°, Pip {off:0.0} from the ring, radius {C.Radius:0.00}";
                if (off == 0) Check($"Pip on the ring catches the bouquet ({angle:0}°)", script.LastCatch == true, detail);
                else Check("Pip away from the ring doesn't catch it", script.LastCatch == false, detail);
                yield return new WaitForSeconds(1.0f);
            }
            C.Input.VirtualMode = false;

            // --- Settings > Relaxed days: an ordinary day's sun runs at 2/3, an Encore's doesn't
            GameFlow.I.DebugCloseMenus();
            GameSettings.RelaxedDays = true;
            foreach (bool encore in new[] { false, true })
            {
                GameFlow.I.DebugStart(0, true, encore);
                yield return new WaitForSeconds(0.5f);
                float h0 = L.Hour, e0 = L.Elapsed;
                yield return new WaitForSeconds(2.5f);
                float rate = (L.Hour - h0) / Mathf.Max(0.001f, L.Elapsed - e0);
                float usual = (L.Def.endHour - L.Def.startHour) / L.Def.dayLength;   // an Encore's dayLength is already its scorcher's
                float want = encore ? usual : usual / LevelLibrary.RelaxedDayScale;
                Check(encore ? "relaxed days leave an Encore's scorcher pace alone" : "relaxed days run an ordinary day's sun at 2/3",
                      Mathf.Abs(rate / want - 1f) < 0.03f, $"{rate * 60f:0.000} h/min over {L.Elapsed - e0:0.0}s, want {want * 60f:0.000} (usual {usual * 60f:0.000})");
            }

            // --- a relaxed day is saved (stamp and all) but keeps "before par" and the best time for the usual pace
            GameFlow.I.DebugCloseMenus();
            SaveData.DebugForget("level02");
            GameFlow.I.DebugStart(1, true);
            yield return new WaitForSeconds(1.0f);
            GameFlow.I.DebugSaveDay();
            yield return WaitFor(() => Now == GameFlow.State.Results, 10f);
            yield return new WaitForSecondsRealtime(1.0f);
            var rel = SaveData.Get("level02");
            Check("a relaxed day is saved without the par stamp or a best time",
                  Now == GameFlow.State.Results && SaveData.Has("level02", SaveData.StampSaved) && !SaveData.Has("level02", SaveData.StampPar) && rel.bestHour > 90f,
                  $"{Now}, stamps {rel.stamps}, best {rel.bestHour:0.00}");
            Check("its results card says the par stamp needs the usual pace", TextShowing(ResultsCard.RelaxedParLabel) && TextShowing("on a relaxed day"));
            GameFlow.I.DebugCloseMenus();
            GameFlow.I.DebugStart(1, false);
            GameFlow.I.DebugShowPostcard();
            yield return new WaitForSecondsRealtime(1.0f);
            Check("a relaxed day's postcard says so", TextShowing(Postcard.RelaxedParLine));
            GameFlow.I.DebugCloseMenus();

            // --- the sunset card offers a slower sun from the day's second sunset, and it works
            GameSettings.RelaxedDays = false;
            GameFlow.I.DebugStart(2, true);
            for (int sunset = 1; sunset <= 2; sunset++)
            {
                yield return new WaitForSeconds(0.5f);
                L.SetHour(L.Def.endHour - 0.02f);
                yield return WaitFor(() => Now == GameFlow.State.Failed, 5f);
                yield return new WaitForSecondsRealtime(2.5f);
                bool offered = GameFlow.I.SunsetOffersSlower && GameObject.Find("Btn_" + FailCard.SlowerLabel) != null;
                if (sunset == 1) Check("the first sunset doesn't offer a slower sun", Now == GameFlow.State.Failed && !offered, $"{Now}, offered {offered}");
                else Check("the second sunset on the same day offers a slower sun", Now == GameFlow.State.Failed && offered, $"{Now}, offered {offered}");
                if (sunset == 2) break;
                // "Try again" (selected when the card opens) goes straight back into the day
                yield return Press(Key.Enter);
                yield return WaitFor(() => Now == GameFlow.State.Playing, 6f);
                Check("Try again goes straight back into the day, with no postcard", Now == GameFlow.State.Playing && !GameFlow.I.PostcardOpen && L.Def.id == "level03" && L.Hour <= L.Def.startHour + 0.05f,
                      $"{Now}, postcard {GameFlow.I.PostcardOpen}, {L?.Def.id}, hour {L?.Hour:0.00}");
            }
            var slower = GameObject.Find("Btn_" + FailCard.SlowerLabel);
            if (slower != null)
            {
                EventSystem.current.SetSelectedGameObject(slower);
                yield return Press(Key.Enter);
                yield return WaitFor(() => Now == GameFlow.State.Playing, 6f);
                Check("Slower sun turns relaxed days on and goes straight back into the day",
                      GameSettings.RelaxedDays && Now == GameFlow.State.Playing && !GameFlow.I.PostcardOpen && L.Def.id == "level03" && L.Pace < 1f && L.Hour < L.Def.startHour + 0.5f,
                      $"relaxed {GameSettings.RelaxedDays}, {Now}, {L?.Def.id}, pace {L?.Pace:0.00}, hour {L?.Hour:0.00}");
            }
            GameSettings.RelaxedDays = false;
            GameSettings.Save();
            GameFlow.I.DebugCloseMenus();

            // --- "Not long left": once, as 85% of the day goes by, with the count of friends waiting
            GameFlow.I.DebugStart(3, true);
            var hudL = GameFlow.I.Hud;
            yield return WaitFor(() => !hudL.ToastShowing, 5f);
            L.SetHour(Mathf.Lerp(L.Def.startHour, L.Def.endHour, GameFlow.LateWarningAt - 0.006f));
            yield return new WaitForSeconds(0.2f);
            bool early = hudL.ToastText.StartsWith("Not long left");
            yield return WaitFor(() => L.DayProgress >= GameFlow.LateWarningAt, 5f);
            float crossed = Time.unscaledTime;
            yield return WaitFor(() => hudL.ToastText.StartsWith("Not long left"), 2f);
            float after = Time.unscaledTime - crossed;
            int waiting = 0;
            foreach (var n in L.Needs) if (n.Required && !n.Met) waiting++;
            string said = hudL.ToastText;
            Check("\"Not long left\" comes as 85% of the day goes by, with the count",
                  !early && said == GameFlow.LateWarning(waiting) && after <= 1f, $"'{said}' {after:0.00}s after crossing, {waiting} waiting, early {early}");
            yield return WaitFor(() => !hudL.ToastShowing, 6f);
            bool again = false;
            for (float w = 0; w < 2f; w += Time.unscaledDeltaTime) { again |= hudL.ToastText.StartsWith("Not long left"); yield return null; }
            Check("\"Not long left\" is said only once a day", !again && Now == GameFlow.State.Playing, $"{Now}");
            // a day being saved sweeps its clock past 85% in the timelapse: nobody's waiting, so nothing's said
            GameFlow.I.DebugStart(3, true);
            yield return WaitFor(() => !hudL.ToastShowing, 5f);
            GameFlow.I.DebugSaveDay();
            bool saidOnSave = false;
            for (float w = 0; w < 6f && Now != GameFlow.State.Results; w += Time.unscaledDeltaTime) { saidOnSave |= hudL.ToastText.StartsWith("Not long left"); yield return null; }
            Check("a day being saved doesn't say \"Not long left\"", !saidOnSave && Now == GameFlow.State.Results, $"{Now}, said {saidOnSave}");
            GameFlow.I.DebugCloseMenus();

            // --- a save from before Encore best times (round 6) loads with its stamps and none
            var old = SaveData.DebugParse("{\"levels\":[{\"id\":\"level01\",\"stamps\":15,\"bestHour\":10.5,\"plays\":3}],\"seenTitle\":true}");
            Check("a round-6 save loads with its stamps and no scorcher best", old != null && old.Count == 1 && old[0].stamps == 15 && Mathf.Approximately(old[0].bestHour, 10.5f) && !old[0].HasEncoreBest,
                  old != null && old.Count > 0 ? $"stamps {old[0].stamps}, best {old[0].bestHour}, scorcher best {old[0].encoreBest}" : "unreadable");

            Debug.Log($"[KeyTest] done: {passes} passed, {fails} failed");
            yield return new WaitForSecondsRealtime(0.3f);
            Application.Quit();
        }
    }
}
