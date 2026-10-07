using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace PocketWeather
{
    /// <summary>
    /// All audio: pooled one-shots (panned by where they happen on the diorama), the rain and
    /// drinking loops driven by Pip, crossfading music with a beat/chord clock, ambience beds,
    /// stingers that duck the music, and the musical raindrops.
    /// </summary>
    public class AudioHub : MonoBehaviour
    {
        [Serializable] class ChordDef { public float start; public float beats; public string name; public int[] notes; }
        [Serializable] class TrackDef { public string name; public float bpm; public int beatsPerBar; public float lengthBeats; public ChordDef[] chords; }
        [Serializable] class MusicMeta { public TrackDef[] tracks; }

        public static AudioHub I { get; private set; }

        readonly Dictionary<string, AudioClip> clips = new();
        readonly Dictionary<string, TrackDef> tracks = new();
        readonly List<AudioSource> pool = new();
        int next;
        AudioSource musicA, musicB, amb, rainLoop, drinkLoop, fireLoop;
        AudioSource activeMusic;
        TrackDef activeTrack;
        float musicFade = 1f;
        float duck = 1f, duckTarget = 1f, duckTimer;
        float noteBudget = 3f;
        float lastRainNoteTime;
        string currentMusic = "", currentAmb = "";
        public float FireLevel;      // set by fire needs (0..1)
        /// <summary>Trailer capture: silence the music but keep it playing, so rain notes still follow its chords.</summary>
        public static bool MuteMusic;

        static readonly Dictionary<string, float> Volumes = new()
        {
            { "gust", 0.75f }, { "puff", 0.5f }, { "full", 0.6f }, { "bloom", 0.45f }, { "oops", 0.5f }, { "bell", 0.6f },
            { "sail", 0.5f }, { "splash", 0.5f }, { "sizzle", 0.55f }, { "flap", 0.45f }, { "creak", 0.5f },
            { "ui_pop", 0.5f }, { "ui_tick", 0.35f }, { "ui_back", 0.45f }, { "whoosh", 0.4f }, { "mote", 0.45f },
            { "need_met", 0.6f }, { "rainbow", 0.6f }, { "stamp", 0.75f }, { "day_saved", 0.85f }, { "sunset", 0.7f },
            { "level_start", 0.55f }, { "sheep", 0.5f }, { "cow", 0.5f }, { "cat", 0.5f }, { "dog", 0.5f }, { "pig", 0.5f },
            { "donkey", 0.45f }, { "duck", 0.5f }, { "frog", 0.45f }, { "gull", 0.4f }, { "robin", 0.45f }, { "seal", 0.5f },
            { "person_happy", 0.5f }, { "person_upset", 0.5f }, { "pip_yay", 0.45f }, { "pip_eep", 0.45f }, { "pip_hmm", 0.4f },
            { "pip_slurp", 0.4f }, { "achoo", 0.9f }, { "cheer", 0.7f }, { "aww", 0.6f }, { "applause", 0.6f },
            { "church_bells", 0.55f }, { "thunder", 0.6f }, { "hiss", 0.45f },
        };

        public static AudioHub Create()
        {
            var go = new GameObject("AudioHub");
            DontDestroyOnLoad(go);
            return go.AddComponent<AudioHub>();
        }

        void Awake()
        {
            I = this;
            for (int i = 0; i < 28; i++) pool.Add(NewSource("sfx" + i, false));
            musicA = NewSource("musicA", true);
            musicB = NewSource("musicB", true);
            amb = NewSource("amb", true);
            rainLoop = NewSource("rain", true);
            drinkLoop = NewSource("drink", true);
            fireLoop = NewSource("fire", true);
            rainLoop.clip = Clip("rain_loop");
            drinkLoop.clip = Clip("drink_loop");
            fireLoop.clip = Clip("fire_loop");
            foreach (var s in new[] { rainLoop, drinkLoop, fireLoop }) { s.volume = 0; if (s.clip != null) s.Play(); }
            var ta = Resources.Load<TextAsset>("Audio/music");
            if (ta != null)
            {
                var meta = JsonUtility.FromJson<MusicMeta>(ta.text);
                foreach (var t in meta.tracks) tracks[t.name] = t;
            }
            Sfx.Handler = (n, p, v, pi) => Play(n, p, v, pi);
            RainNotes.Handler = RainNote;
            // the web player starts fetching every track now, the title's first, one at a time
            if (FetchesMusic) foreach (var t in FetchOrder) Fetch(t, false);
        }

        AudioSource NewSource(string n, bool loop)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0;
            s.dopplerLevel = 0;
            return s;
        }

        public AudioClip Clip(string name)
        {
            if (clips.TryGetValue(name, out var c)) return c;
            c = Resources.Load<AudioClip>("Audio/Sfx/" + name)
                ?? Resources.Load<AudioClip>("Audio/Notes/" + name)
                ?? Resources.Load<AudioClip>("Audio/Music/sting_" + name)
                ?? Resources.Load<AudioClip>("Audio/Music/" + name)
                ?? Resources.Load<AudioClip>("Audio/Amb/" + name);
            if (c == null) Debug.LogWarning("[PW] missing audio clip " + name);
            clips[name] = c;
            return c;
        }

        static float PanFor(Vector3 pos)
        {
            if (float.IsNaN(pos.x)) return 0;
            var cam = Camera.main;
            if (cam == null) return 0;
            float x = Vector3.Dot(pos - cam.transform.position, cam.transform.right);
            return Mathf.Clamp(x / 9f, -0.75f, 0.75f);
        }

        public AudioSource Play(string name, Vector3 pos, float volume = 1f, float pitch = 1f)
        {
            var clip = Clip(name);
            if (clip == null) return null;
            var s = pool[next];
            next = (next + 1) % pool.Count;
            s.Stop();
            s.clip = clip;
            float baseVol = Volumes.TryGetValue(name, out var v) ? v : 0.6f;
            s.volume = baseVol * volume * GameSettings.Sfx;
            s.pitch = pitch * UnityEngine.Random.Range(0.97f, 1.03f);
            s.panStereo = PanFor(pos);
            s.Play();
            if (name == "day_saved" || name == "sunset" || name == "achoo") Duck(0.25f, clip.length * 0.7f);
            else if (name == "need_met" || name == "rainbow" || name == "stamp") Duck(0.6f, 1.0f);
            return s;
        }

        public void Duck(float level, float seconds)
        {
            duckTarget = Mathf.Min(duckTarget, level);
            duckTimer = Mathf.Max(duckTimer, seconds);
        }

        // ------------------------------------------------------------------ music tracks
        // The tracks aren't in Resources: each is an asset bundle in StreamingAssets/Music
        // (BuildScript.AddMusic). Desktop players open a track's bundle from disk the moment it's
        // first played. The web player downloads them in the background after it starts, so its
        // first download doesn't wait for the music. Browsers hold decoded audio as raw samples
        // (15-20 MB a track), so on the web a track is decoded only when it's about to play and
        // released once it has faded out; a track that isn't ready yet fades in when it is.
        readonly Dictionary<string, AudioClip> music = new();
        readonly Dictionary<string, AssetBundle> bundles = new();
        readonly List<string> fetchQueue = new();
        readonly Dictionary<string, int> fetchFailures = new();
        readonly HashSet<string> failedTracks = new();
        string fetching, preparing;
        static readonly string[] FetchOrder = { "title", "morning", "seaside", "afternoon", "evening", "night", "wedding" };
        static bool FetchesMusic => Application.platform == RuntimePlatform.WebGLPlayer;
        static string MusicPath(string track) => Application.streamingAssetsPath + "/Music/music_" + track + ".bundle";

        /// <summary>Desktop and editor: the track's clip, loaded on the spot (null if it doesn't exist).</summary>
        AudioClip MusicClipNow(string track)
        {
            if (music.TryGetValue(track, out var c)) return c;
#if UNITY_EDITOR
            c = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Music/music_{track}.ogg");
#else
            var path = MusicPath(track);
            var bundle = File.Exists(path) ? AssetBundle.LoadFromFile(path) : null;
            c = bundle != null ? bundle.LoadAsset<AudioClip>("music_" + track) : null;
#endif
            music[track] = c;
            return c;
        }

        /// <summary>Queues a track's bundle to download (web only); urgent ones jump the queue.</summary>
        void Fetch(string track, bool urgent)
        {
            if (bundles.ContainsKey(track) || failedTracks.Contains(track) || fetching == track) return;
            fetchQueue.Remove(track);
            if (urgent) fetchQueue.Insert(0, track); else fetchQueue.Add(track);
            if (fetching == null) StartCoroutine(FetchLoop());
        }

        IEnumerator FetchLoop()
        {
            while (fetchQueue.Count > 0)
            {
                string track = fetching = fetchQueue[0];
                fetchQueue.RemoveAt(0);
                float t0 = Time.realtimeSinceStartup;
                AssetBundle bundle = null;
                string error = null;
                using (var req = UnityWebRequestAssetBundle.GetAssetBundle(MusicPath(track)))
                {
                    yield return req.SendWebRequest();
                    if (req.result != UnityWebRequest.Result.Success) error = req.error;
                    else if ((bundle = DownloadHandlerAssetBundle.GetContent(req)) == null) error = "not an asset bundle";
                }
                fetching = null;
                if (error != null)
                {
                    int n = fetchFailures.TryGetValue(track, out var f) ? f + 1 : 1;
                    fetchFailures[track] = n;
                    Debug.LogWarning($"[PW] music {track} didn't arrive ({error}){(n < 3 ? ", trying again later" : ", giving up")}");
                    if (n < 3) fetchQueue.Add(track); else failedTracks.Add(track);
                    continue;
                }
                bundles[track] = bundle;
                Debug.Log($"[PW] fetched music {track} in {Time.realtimeSinceStartup - t0:0.0} s");
            }
        }

        /// <summary>Web: waits for the track's bundle, decodes it, and fades it in if it's still
        /// the one that should be playing.</summary>
        IEnumerator PrepareAndPlay(string track)
        {
            preparing = track;
            Fetch(track, true);
            while (!bundles.ContainsKey(track) && !failedTracks.Contains(track) && currentMusic == track) yield return null;
            if (!bundles.TryGetValue(track, out var bundle) || currentMusic != track) { if (preparing == track) preparing = null; yield break; }
            if (!music.TryGetValue(track, out var clip) || clip == null)
            {
                var load = bundle.LoadAssetAsync<AudioClip>("music_" + track);
                yield return load;
                music[track] = clip = load.asset as AudioClip;
            }
            if (clip != null && clip.loadState != AudioDataLoadState.Loaded)
            {
                clip.LoadAudioData();
                for (float w = 0; clip.loadState != AudioDataLoadState.Loaded && clip.loadState != AudioDataLoadState.Failed && w < 20f; w += Time.unscaledDeltaTime) yield return null;
            }
            if (preparing == track) preparing = null;
            if (clip == null || clip.loadState != AudioDataLoadState.Loaded) { Debug.LogWarning($"[PW] music {track} couldn't be decoded"); yield break; }
            if (currentMusic == track && activeMusic == null) StartTrack(track, clip, 2f);
        }

        // ------------------------------------------------------------------ music
        public void PlayMusic(string track, float fadeTime = 1.5f)
        {
            if (track == currentMusic) return;
            currentMusic = track;
            if (FetchesMusic)
            {
                music.TryGetValue(track, out var ready);
                if (ready != null && ready.loadState == AudioDataLoadState.Loaded) { StartTrack(track, ready, fadeTime); return; }
                // not downloaded or decoded yet: the old track fades out, and this one fades in when it's ready
                Debug.Log($"[PW] music {track}: waiting for it to be ready");
                activeMusic = null;
                activeTrack = null;
                fadeDuration = Mathf.Max(0.05f, fadeTime);
                if (preparing != track) StartCoroutine(PrepareAndPlay(track));
                return;
            }
            StartTrack(track, MusicClipNow(track), fadeTime);
        }

        void StartTrack(string track, AudioClip clip, float fadeTime)
        {
            Debug.Log($"[PW] music {track}: {(clip != null ? clip.length.ToString("0.0") + " s loop" : "MISSING")}");
            // after a wait both sources may be fading out: take over the quieter one
            var incoming = activeMusic == musicA ? musicB : activeMusic == musicB ? musicA : musicA.volume <= musicB.volume ? musicA : musicB;
            if (FetchesMusic && incoming.clip != null && incoming.clip != clip) incoming.clip.UnloadAudioData();   // the web frees what it replaces
            incoming.clip = clip;
            incoming.volume = 0;
            incoming.time = 0;
            if (clip != null) incoming.Play();
            trackStartDsp = AudioSettings.dspTime;
            trackStartReal = Time.realtimeSinceStartupAsDouble;
            clockLogged = false;
            activeMusic = incoming;
            tracks.TryGetValue(track, out activeTrack);
            musicFade = 0;
            fadeDuration = Mathf.Max(0.05f, fadeTime);
        }

        float fadeDuration = 1.5f;

        public void StopMusic(float fadeTime = 1f)
        {
            currentMusic = "";
            activeMusic = null;
            fadeDuration = Mathf.Max(0.05f, fadeTime);
        }

        public void PlayAmbience(string name)
        {
            if (name == currentAmb) return;
            currentAmb = name;
            var clip = string.IsNullOrEmpty(name) ? null : Resources.Load<AudioClip>("Audio/Amb/amb_" + name);
            amb.clip = clip;
            if (clip != null) amb.Play(); else amb.Stop();
        }

        public float Beat
        {
            get
            {
                if (activeMusic == null || activeTrack == null || activeMusic.clip == null) return Time.time * 1.5f;
                return MusicTime / (60f / activeTrack.bpm);
            }
        }

        // When the active track started, on the audio clock and the real-time clock.
        double trackStartDsp, trackStartReal;
        bool clockLogged;

        /// <summary>Seconds into the active track's loop. Desktop players read the source's own
        /// position. The web player's sources are the browser's (compressed audio), whose position
        /// Unity can't report ("getFrequency() is not supported for compressed sound"), so it's
        /// counted from when the track started: on the audio clock, which stops when the browser
        /// suspends the page's audio, or on real time if that clock doesn't run.</summary>
        float MusicTime
        {
            get
            {
                if (!FetchesMusic) return activeMusic.time;
                double dsp = AudioSettings.dspTime - trackStartDsp;
                double t = dsp > 0 ? dsp : Time.realtimeSinceStartupAsDouble - trackStartReal;
                return (float)(t % Mathf.Max(0.01f, activeMusic.clip.length));
            }
        }

        static readonly int[] DefaultChord = { 55, 59, 62 };

        public int[] ChordNotes
        {
            get
            {
                if (activeTrack == null || activeTrack.chords == null || activeTrack.chords.Length == 0) return DefaultChord;
                float b = Beat % Mathf.Max(1, activeTrack.lengthBeats);
                foreach (var c in activeTrack.chords)
                    if (b >= c.start && b < c.start + c.beats) return c.notes;
                return activeTrack.chords[0].notes;
            }
        }

        /// <summary>The n-th chord tone above C5 (for ascending collection arpeggios).</summary>
        public int ChordTone(int index, int baseMidi = 72)
        {
            var notes = ChordNotes;
            var pcs = new List<int>();
            foreach (var n in notes) if (!pcs.Contains(n % 12)) pcs.Add(n % 12);
            pcs.Sort();
            int oct = index / pcs.Count;
            int pc = pcs[index % pcs.Count];
            int m = baseMidi - baseMidi % 12 + pc + 12 * oct;
            if (m < baseMidi) m += 12;
            return m;
        }

        // ------------------------------------------------------------------ musical rain
        void RainNote(Vector3 pos, Surface surface)
        {
            if (noteBudget < 1f) return;
            if (Time.time - lastRainNoteTime < 0.07f) return;
            noteBudget -= 1f;
            lastRainNoteTime = Time.time;
            string clip;
            int baseMidi;
            switch (surface)
            {
                case Surface.Leaf: clip = "note_kalimba"; baseMidi = 72; break;
                case Surface.Water: clip = "note_water"; baseMidi = 72; break;
                case Surface.Roof:
                case Surface.Stone: clip = "note_glock"; baseMidi = 84; break;
                case Surface.Wood: clip = "note_wood"; baseMidi = 72; break;
                case Surface.Creature:
                case Surface.Fabric: clip = "note_pluck"; baseMidi = 72; break;
                default: clip = "note_marimba"; baseMidi = 72; break;
            }
            // left-to-right across the diorama climbs like a xylophone
            float hw = Level.Current != null ? Level.Current.Def.island.w / 2 : 7f;
            float t = Mathf.InverseLerp(-hw, hw, pos.x);
            int idx = Mathf.Clamp(Mathf.RoundToInt(t * 6f) + UnityEngine.Random.Range(-1, 2), 0, 7);
            int midi = ChordTone(idx, baseMidi);
            float pitch = Mathf.Pow(2f, (midi - baseMidi) / 12f);
            var s = pool[next];
            next = (next + 1) % pool.Count;
            s.Stop();
            s.clip = Clip(clip);
            if (s.clip == null) return;
            s.volume = UnityEngine.Random.Range(0.16f, 0.28f) * GameSettings.Sfx;
            s.pitch = pitch;
            s.panStereo = PanFor(pos);
            s.Play();
        }

        public void PlayMote(int combo, Vector3 pos)
        {
            int midi = ChordTone(Mathf.Min(combo, 9), 72);
            Play("mote", pos, 0.9f, Mathf.Pow(2f, (midi - 72) / 12f));
        }

        void Update()
        {
            float dt = Clock.UnscaledDelta;
            noteBudget = Mathf.Min(3f, noteBudget + dt * 9f);
            // ducking
            if (duckTimer > 0) duckTimer -= dt; else duckTarget = 1f;
            duck = Mathf.MoveTowards(duck, duckTarget, dt * (duck > duckTarget ? 3f : 0.8f));
            // music crossfade
            musicFade = Mathf.MoveTowards(musicFade, 1f, dt / fadeDuration);
            float mv = (MuteMusic ? 0f : GameSettings.Music) * 0.55f * duck;
            foreach (var src in new[] { musicA, musicB })
            {
                bool isActive = src == activeMusic;
                float target = isActive ? mv * musicFade : 0f;
                src.volume = isActive ? target : Mathf.MoveTowards(src.volume, 0f, dt / fadeDuration * mv + dt * 0.1f);
                if (!isActive && src.volume <= 0.001f && src.isPlaying)
                {
                    src.Stop();
                    // web: let the browser free the faded-out track's decoded samples
                    if (FetchesMusic && src.clip != null && (activeMusic == null || activeMusic.clip != src.clip)) src.clip.UnloadAudioData();
                }
            }
            amb.volume = Mathf.MoveTowards(amb.volume, GameSettings.Ambience * 0.6f, dt * 0.5f);
            // once per track, 10 s in: where each clock thinks the music is (the chords follow MusicTime)
            if (!clockLogged && activeMusic != null && activeMusic.clip != null && Time.realtimeSinceStartupAsDouble - trackStartReal > 10.0)
            {
                clockLogged = true;
                Debug.Log($"[PW] music clock {currentMusic}: source {activeMusic.time:0.00} s, audio clock {AudioSettings.dspTime - trackStartDsp:0.00} s, real {Time.realtimeSinceStartupAsDouble - trackStartReal:0.00} s, chords follow {MusicTime:0.00} s");
            }

            var cloud = Cloud.Instance;
            float rainV = 0, drinkV = 0;
            if (cloud != null && cloud.isActiveAndEnabled)
            {
                rainV = cloud.RainIntensity * 0.5f;
                drinkV = cloud.Drinking ? 0.45f : 0f;
                drinkLoop.pitch = 0.85f + cloud.Fill * 0.45f;
            }
            rainLoop.volume = Mathf.MoveTowards(rainLoop.volume, rainV * GameSettings.Sfx, dt * 3f);
            drinkLoop.volume = Mathf.MoveTowards(drinkLoop.volume, drinkV * GameSettings.Sfx, dt * 4f);
            fireLoop.volume = Mathf.MoveTowards(fireLoop.volume, FireLevel * 0.5f * GameSettings.Sfx, dt * 2f);
        }
    }
}
