using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace PocketWeather
{
    /// <summary>
    /// Scripted screenshot tour, active only with <c>-pwCapture &lt;dir&gt;</c>. Saves PNGs and quits.
    /// </summary>
    public class Capture : MonoBehaviour
    {
        string outDir;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-pwCapture");
            if (i < 0 || i + 1 >= args.Length) return;
            var c = new GameObject("Capture").AddComponent<Capture>();
            c.outDir = args[i + 1];
            DontDestroyOnLoad(c.gameObject);
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(outDir);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("smoke");
            Debug.Log("[PW] capture done");
            Application.Quit();
        }

        public IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
            Debug.Log($"[PW] shot {name}");
        }
    }
}
