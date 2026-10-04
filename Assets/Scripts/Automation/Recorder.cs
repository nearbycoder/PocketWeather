using System;
using System.IO;
using Unity.Collections;
using UnityEngine;

namespace PocketWeather
{
    /// <summary>
    /// Offline video capture, active with <c>-pwVideo &lt;dir&gt; [-pwVideoFps 30] [-pwVideoQuality 92]</c>. Locks the game
    /// clock to the capture rate (so the result is smooth however slow the machine is), writes one
    /// JPEG per frame and the mixed audio output as raw stereo float32 (<c>audio.f32</c>), ready
    /// for ffmpeg. Usually combined with <c>-pwAutopilot</c> to have the bot play.
    /// </summary>
    public class Recorder : MonoBehaviour
    {
        string dir;
        int fps;
        int quality;
        int frame;
        public static int Frame { get; private set; } = -1;
        public static void Mark(string what) { if (Frame >= 0) Debug.Log($"[PW] mark {Frame} {what}"); }
        FileStream audio;
        bool audioOk;
        int channels;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-pwVideo");
            if (i < 0 || i + 1 >= args.Length) return;
            var r = new GameObject("Recorder").AddComponent<Recorder>();
            r.dir = args[i + 1];
            r.fps = int.Parse(GameRoot.Arg("-pwVideoFps", "30"));
            r.quality = int.Parse(GameRoot.Arg("-pwVideoQuality", "92"));
            DontDestroyOnLoad(r.gameObject);
        }

        void Start()
        {
            Directory.CreateDirectory(dir);
            Time.captureFramerate = fps;
            channels = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
            audioOk = AudioRenderer.Start();
            if (audioOk) audio = new FileStream(Path.Combine(dir, "audio.f32"), FileMode.Create);
            Debug.Log($"[PW] recording {fps} fps to {dir}; audio {(audioOk ? $"{AudioSettings.outputSampleRate} Hz x{channels}" : "unavailable")}");
            File.WriteAllText(Path.Combine(dir, "audio.txt"), $"{AudioSettings.outputSampleRate} {channels}");
        }

        void LateUpdate() { if (dir != null) StartCoroutine(Grab()); }

        System.Collections.IEnumerator Grab()
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(dir, $"f{frame:00000}.jpg"), tex.EncodeToJPG(quality));
            Destroy(tex);
            frame++;
            Frame = frame;
            if (audioOk)
            {
                int n = AudioRenderer.GetSampleCountForCaptureFrame();
                if (n > 0)
                {
                    using var buf = new NativeArray<float>(n * channels, Allocator.Temp);
                    AudioRenderer.Render(buf);
                    var bytes = new byte[buf.Length * 4];
                    Buffer.BlockCopy(buf.ToArray(), 0, bytes, 0, bytes.Length);
                    audio.Write(bytes, 0, bytes.Length);
                }
            }
        }

        void OnApplicationQuit()
        {
            if (audioOk) { AudioRenderer.Stop(); audio?.Flush(); audio?.Dispose(); audio = null; }
            Debug.Log($"[PW] recorded {frame} frames");
        }
    }
}
