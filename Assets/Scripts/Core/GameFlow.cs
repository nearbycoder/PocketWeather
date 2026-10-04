using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace PocketWeather
{
    /// <summary>
    /// The game's state machine: title → map → postcard → play → (day saved → results) or
    /// (sunset → retry), plus pause/settings, the finale's ending, transitions, music and saves.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        public enum State { Boot, Title, Map, Intro, Playing, Paused, Celebrate, Results, Failed, Ending }

        public static GameFlow I { get; private set; }
        public State Current { get; private set; } = State.Boot;
        public Level Level { get; private set; }
        public int LevelIndex { get; private set; } = -1;
        public Hud Hud { get; private set; }
        public bool DelightFoundThisRun { get; private set; }

        GameRoot root;
        TitleScreen title;
        MapScreen map;
        Postcard postcard;
        PauseMenu pause;
        SettingsMenu settings;
        ResultsCard results;
        FailCard fail;
        EndingScreen ending;
        CloudWipe wipe;
        MenuScreen returnAfterSettings;
        Onboarding onboarding;
        float pausedTimeScale = 1f;

        public static GameFlow Create(GameRoot r)
        {
            var go = new GameObject("GameFlow");
            DontDestroyOnLoad(go);
            var f = go.AddComponent<GameFlow>();
            I = f;
            f.root = r;
            f.Build();
            return f;
        }

        void Build()
        {
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem");
                DontDestroyOnLoad(es);
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }
            Hud = Hud.Create(transform);
            Hud.SetVisible(false, 0.01f);
            Hud.OnPause = Pause;
            var menus = Ui.MakeCanvas("MenuCanvas", 50, transform);
            title = Make<TitleScreen>(menus.transform, "Title");
            map = Make<MapScreen>(menus.transform, "Map");
            postcard = Make<Postcard>(menus.transform, "Postcard");
            pause = Make<PauseMenu>(menus.transform, "Pause");
            results = Make<ResultsCard>(menus.transform, "Results");
            fail = Make<FailCard>(menus.transform, "Fail");
            ending = Make<EndingScreen>(menus.transform, "Ending");
            settings = Make<SettingsMenu>(menus.transform, "Settings");
            wipe = CloudWipe.Create(transform);

            title.OnPlay = () => { if (Current == State.Title) Transition(ShowMapNow); };
            title.OnSettings = () => OpenSettings(title);
            map.OnBack = () => Transition(ShowTitleNow);
            map.OnSettings = () => OpenSettings(map);
            map.OnPick = i => StartLevel(i);
            postcard.OnStart = BeginPlay;
            pause.OnResume = Resume;
            pause.OnRestart = () => { Resume(); StartLevel(LevelIndex); };
            pause.OnMap = () => { Resume(); Transition(ShowMapNow); };
            pause.OnSettings = () => OpenSettings(pause);
            settings.OnClose = CloseSettings;
            results.OnNext = NextLevel;
            results.OnReplay = () => StartLevel(LevelIndex);
            results.OnMap = () => Transition(ShowMapNow);
            fail.OnRetry = () => StartLevel(LevelIndex);
            fail.OnMap = () => Transition(ShowMapNow);
            ending.OnDone = () => Transition(ShowMapNow);
        }

        T Make<T>(Transform parent, string name) where T : MenuScreen
        {
            var go = new GameObject(name + "Screen");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<T>();
            s.Setup(parent, name);
            return s;
        }

        public void Boot()
        {
            string direct = GameRoot.Arg("-pwLevel");
            if (direct != null)
            {
                int idx = LevelLibrary.IndexOf(direct);
                if (idx >= 0) { StartLevelNow(idx); return; }
                LoadLevelObject(LevelLibrary.Load(direct));
                BeginPlay();
                return;
            }
            ShowTitleNow();
        }

        /// <summary>Automation: load a campaign level instantly and start playing (no wipe/postcard).</summary>
        public void DebugStart(int index, bool play = true)
        {
            Time.timeScale = 1f;
            StartLevelNow(index);
            postcard.Close();
            if (play) BeginPlay();
        }

        public void DebugShowPostcard() { if (Level != null) postcard.Show(Level.Def, LevelIndex); }
        public void DebugBeginPlay() => BeginPlay();
        public void DebugShowMap() => ShowMapNow();
        public void DebugShowTitle() => ShowTitleNow();
        public void DebugPause() => Pause();
        public void DebugSettings() => OpenSettings(pause);
        public void DebugCloseSettings() => CloseSettings();
        public void DebugResume() => Resume();

        System.Action queuedTransition;

        void Transition(System.Action covered)
        {
            if (wipe.Busy)
            {
                // a press on the new screen while the clouds are still rolling away runs right after;
                // presses on the old screen while it's being covered are dropped
                if (wipe.Revealing) queuedTransition = covered;
                return;
            }
            wipe.Run(covered, () =>
            {
                var next = queuedTransition;
                queuedTransition = null;
                if (next != null) Transition(next);
            });
        }

        // ------------------------------------------------------------------ title & map
        void LoadDisplayLevel()
        {
            int idx = Mathf.Clamp(SaveData.FirstUnfinished(), 0, LevelLibrary.Campaign.Length - 1);
            string id = LevelLibrary.Campaign[0];
            var def = LevelLibrary.Load(id) ?? LevelLibrary.Load("proto");
            if (Level != null && Level.Def.id == def.id && !Level.Running) return;
            LoadLevelObject(def);
            Level.SetHour(Mathf.Lerp(def.startHour, def.endHour, 0.25f));
            Level.Cloud.Input.Enabled = false;
            Level.Cloud.gameObject.AddComponent<Wander>();
        }

        public void ShowTitleNow()
        {
            CloseAll();
            Current = State.Title;
            LoadDisplayLevel();
            Hud.SetVisible(false);
            root.Rig.Zoom = 1.18f;
            root.Rig.FocusOffset = new Vector3(0, 0, 3.0f);
            AudioHub.I?.PlayMusic("title");
            AudioHub.I?.PlayAmbience("meadow");
            title.Open();
        }

        public void ShowMapNow()
        {
            CloseAll();
            Time.timeScale = 1f;
            Current = State.Map;
            LoadDisplayLevel();
            Hud.SetVisible(false);
            root.Rig.Zoom = 1.08f;
            root.Rig.FocusOffset = Vector3.zero;
            AudioHub.I?.PlayMusic("title");
            map.Open();
        }

        void CloseAll()
        {
            foreach (var s in new MenuScreen[] { title, map, postcard, pause, results, fail, ending, settings }) if (s.IsOpen) s.Close();
            Hud.HideHint();
        }

        // ------------------------------------------------------------------ levels
        public void StartLevel(int index)
        {
            Time.timeScale = 1f;
            Transition(() => StartLevelNow(index));
        }

        void StartLevelNow(int index)
        {
            CloseAll();
            LevelIndex = index;
            var def = LevelLibrary.Load(LevelLibrary.Campaign[index]);
            LoadLevelObject(def);
            Current = State.Intro;
            Level.Cloud.Input.Enabled = false;
            root.Rig.Zoom = 1.04f;
            root.Rig.FocusOffset = Vector3.zero;
            AudioHub.I?.PlayMusic(string.IsNullOrEmpty(def.music) ? "morning" : def.music);
            AudioHub.I?.PlayAmbience(string.IsNullOrEmpty(def.ambience) ? "meadow" : def.ambience);
            Hud.Bind(Level);
            Hud.SetVisible(false, 0.01f);
            if (GameRoot.HasArg("-pwSkipIntro")) { BeginPlay(); return; }
            Tween.Delay(0.35f, () => { if (Current == State.Intro) postcard.Show(def, index); });
        }

        void LoadLevelObject(LevelDef def)
        {
            if (Level != null)
            {
                // Destroy is deferred to the end of the frame: deactivate first so the old island's
                // colliders are gone before the new one raycasts for ground heights and scatter
                Level.gameObject.SetActive(false);
                Destroy(Level.gameObject);
            }
            if (onboarding != null) Destroy(onboarding);
            Level = Level.Load(def, root.Day);
            root.SetLevel(Level);
            root.Rig.Frame(def.island.w, def.island.d);
            Level.OnAllMet += OnAllMet;
            Level.OnSunset += OnSunset;
            Level.OnDelight += OnDelight;
            Level.OnOops += n => Hud.PunchTrayFor(n);
            Level.NeedsChanged += () => Hud.Bind(Level);
            Level.Cloud.OnEmptyTry += () => Hud.ShakeGauge(1f);
            DelightFoundThisRun = false;
        }

        void BeginPlay()
        {
            if (Level == null) return;
            postcard.Close();
            Current = State.Playing;
            Level.Running = true;
            Level.Cloud.Input.Enabled = true;
            root.Rig.Zoom = 1f;
            Hud.Bind(Level);
            Hud.SetVisible(true);
            SaveData.RecordPlay(Level.Def.id);
            Sfx.Ui("level_start");
            onboarding = gameObject.AddComponent<Onboarding>();
            onboarding.Init(Level, Hud);
            Level.Cloud.Visual.Emote(CloudVisual.Determined, 0.8f);
        }

        void OnDelight(string title)
        {
            DelightFoundThisRun = true;
            int fresh = SaveData.Award(Level.Def.id, SaveData.StampDelight);
            Hud.Toast(fresh != 0 ? $"Delight! {title}" : title, "stamp_flower");
            Sfx.Ui("stamp");
            Level.Cloud.Visual.Emote(CloudVisual.Delight, 1.4f);
            Level.Cloud.Visual.Hop(1f);
            Sfx.Play("pip_yay", Level.Cloud.transform.position);
        }

        void OnAllMet()
        {
            if (Current != State.Playing) return;
            StartCoroutine(Celebrate());
        }

        IEnumerator Celebrate()
        {
            Current = State.Celebrate;
            var lvl = Level;
            float finish = lvl.Hour;
            lvl.Running = false;
            lvl.Cloud.Input.Enabled = false;
            lvl.Cloud.Input.Virtual(lvl.Cloud.GroundPoint, false);
            Hud.HideHint();
            Sfx.Ui("day_saved");
            Sfx.Play("pip_yay", lvl.Cloud.transform.position);
            lvl.Cloud.Visual.Override = CloudVisual.Delight;
            lvl.Cloud.Visual.Hop(1.4f);
            // hit-stop
            Time.timeScale = 0.08f;
            yield return new WaitForSecondsRealtime(0.12f);
            Time.timeScale = 1f;
            foreach (var n in lvl.Needs)
                if (n.Required) Fx.Sparkles(n.BubbleAnchor, 14, new Color(1f, 0.92f, 0.6f), 0.6f, 2.2f, 0.2f);
            Fx.Confetti(lvl.Cloud.transform.position, 70, 2f);
            root.Rig.Zoom = 0.94f;
            yield return new WaitForSeconds(1.0f);
            Hud.SetVisible(false, 0.5f);
            // timelapse to sunset
            float from = lvl.Hour, to = Mathf.Max(lvl.Def.endHour + 0.6f, from + 0.5f);
            float t = 0, dur = 3.2f;
            lvl.Cloud.Visual.Override = null;
            while (t < dur)
            {
                t += Time.deltaTime;
                lvl.SetHour(Mathf.Lerp(from, to, Ease.InOutCubic(t / dur)));
                if (t > dur * 0.6f) lvl.Cloud.Visual.Override = CloudVisual.Sleepy;
                yield return null;
            }
            int stamps = SaveData.StampSaved;
            if (finish <= lvl.Def.par + 1e-3f) stamps |= SaveData.StampPar;
            if (DelightFoundThisRun) stamps |= SaveData.StampDelight;
            int before = SaveData.Get(lvl.Def.id).stamps;
            int fresh = SaveData.Award(lvl.Def.id, stamps & ~SaveData.StampDelight);
            fresh |= (DelightFoundThisRun && (before & SaveData.StampDelight) == 0) ? SaveData.StampDelight : 0;
            SaveData.RecordFinish(lvl.Def.id, finish);
            Current = State.Results;
            bool last = LevelIndex >= LevelLibrary.Campaign.Length - 1;
            results.Show(lvl.Def, stamps, fresh, finish, last);
            Debug.Log($"[PW] level {lvl.Def.id} saved at {finish:0.00} (par {lvl.Def.par}) stamps={stamps} fresh={fresh} oopses={lvl.Oopses}");
        }

        void OnSunset()
        {
            if (Current != State.Playing) return;
            StartCoroutine(SunsetFail());
        }

        IEnumerator SunsetFail()
        {
            Current = State.Failed;
            var lvl = Level;
            lvl.Running = false;
            lvl.Cloud.Input.Enabled = false;
            Hud.HideHint();
            Sfx.Ui("sunset");
            lvl.Cloud.Visual.Override = CloudVisual.Sleepy;
            float from = lvl.Hour;
            float t = 0;
            while (t < 1.5f)
            {
                t += Time.deltaTime;
                lvl.SetHour(Mathf.Lerp(from, from + 0.8f, t / 1.5f));
                yield return null;
            }
            Hud.SetVisible(false, 0.4f);
            fail.Show(lvl);
            Debug.Log($"[PW] level {lvl.Def.id} sunset failed");
        }

        void NextLevel()
        {
            if (LevelIndex >= LevelLibrary.Campaign.Length - 1)
            {
                Transition(() =>
                {
                    CloseAll();
                    Current = State.Ending;
                    LoadLevelObject(LevelLibrary.Load(LevelLibrary.Campaign[LevelLibrary.Campaign.Length - 1]));
                    Level.SetHour(18.8f);
                    Level.Cloud.Input.Enabled = false;
                    Level.Cloud.gameObject.AddComponent<Wander>();
                    AudioHub.I?.PlayMusic("wedding");
                    ending.Open();
                });
                return;
            }
            StartLevel(LevelIndex + 1);
        }

        // ------------------------------------------------------------------ pause & settings
        public void Pause()
        {
            if (Current != State.Playing) return;
            pauseToggleFrame = Time.frameCount;
            Current = State.Paused;
            pausedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            Level.Cloud.Input.Enabled = false;
            pause.SetDelight(Level.Def, SaveData.Has(Level.Def.id, SaveData.StampDelight) || DelightFoundThisRun);
            pause.Open();
            AudioHub.I?.Duck(0.5f, 9999f);
        }

        int pauseToggleFrame = -1;

        public void Resume()
        {
            if (Current != State.Paused) return;
            pauseToggleFrame = Time.frameCount;
            pause.Close();
            if (settings.IsOpen) settings.Close();
            Time.timeScale = pausedTimeScale <= 0 ? 1f : pausedTimeScale;
            Current = State.Playing;
            Level.Cloud.Input.Enabled = true;
            AudioHub.I?.Duck(1f, 0f);
        }

        void OpenSettings(MenuScreen from)
        {
            returnAfterSettings = from;
            from.Close();
            settings.Open();
        }

        void CloseSettings()
        {
            settings.Close();
            if (returnAfterSettings != null) returnAfterSettings.Open();
        }

        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (Current == State.Playing && Time.frameCount != pauseToggleFrame)   // the press that resumed mustn't re-pause
            {
                if ((kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)) || (pad != null && pad.startButton.wasPressedThisFrame))
                    Pause();
                if (kb != null && kb.mKey.wasPressedThisFrame) GameSettings.Music = GameSettings.Music > 0.01f ? 0f : 0.8f;
            }
        }
    }

    /// <summary>Lets Pip drift around idly on the title and map screens.</summary>
    public class Wander : MonoBehaviour
    {
        Cloud cloud;
        float timer;
        float rainTimer;

        void Start() { cloud = GetComponent<Cloud>(); }

        void Update()
        {
            if (cloud == null || cloud.Level == null) return;
            timer -= Time.deltaTime;
            rainTimer -= Time.deltaTime;
            var d = cloud.Level.Def.island;
            if (timer <= 0)
            {
                timer = Random.Range(2.5f, 5f);
                var p = new Vector3(Random.Range(-d.w * 0.35f, d.w * 0.35f), 0, Random.Range(-d.d * 0.32f, d.d * 0.05f));
                cloud.Input.Virtual(p, false);
                if (Random.value < 0.3f) rainTimer = 1.2f;
            }
            if (cloud.Water < 30) cloud.AddWater(40);
            cloud.Input.VirtualRain = rainTimer > 0 && rainTimer < 1.0f;
        }
    }
}
