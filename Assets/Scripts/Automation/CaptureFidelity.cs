using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PocketWeather
{
    /// <summary>Round 12: the Graphics Fidelity steps, side by side and timed.</summary>
    public partial class Capture
    {
        static readonly Quality.Tier[] Tiers = { Quality.Tier.Low, Quality.Tier.Medium, Quality.Tier.High, Quality.Tier.Ultra };

        /// <summary>Raining at <paramref name="x"/>,<paramref name="z"/> on a day at an hour, then the
        /// game is frozen and the same moment is shot at every fidelity step.</summary>
        IEnumerator FidelityScene(string name, int day, float hour, float x, float z, bool rain)
        {
            GameFlow.I.DebugStart(day, true);
            GameSettings.Hints = false;
            yield return new WaitForSeconds(1.2f);
            L.SetHour(hour);
            yield return MoveTo(x, z, 0.3f);
            if (rain)
            {
                C.Input.Virtual(new Vector3(x, 0, z), true);
                for (float t = 0; t < 1.6f; t += Time.deltaTime) { C.SetWater(90f); L.SetHour(hour); yield return null; }
            }
            else for (float t = 0; t < 0.8f; t += Time.deltaTime) { L.SetHour(hour); yield return null; }
            Time.timeScale = 0f;
            foreach (var tier in Tiers)
            {
                Quality.Forced = tier;
                Quality.Apply();
                yield return new WaitForSecondsRealtime(0.6f);
                yield return Shot($"{name}_{(int)tier}_{tier.ToString().ToLowerInvariant()}");
            }
            Time.timeScale = 1f;
            C.Input.Virtual(new Vector3(x, 0, z), false);
            Quality.Forced = null;
            Quality.Apply();
        }

        IEnumerator FidelityShots()
        {
            yield return new WaitForSeconds(1.0f);
            yield return FidelityScene("fid_d10", 9, 10.5f, 2.6f, 0.3f, true);    // the regatta: sea, sand, sunbathers
            yield return FidelityScene("fid_d06", 5, 15.5f, -1.5f, 1.2f, true);   // the picnic: grass, trees, people
            yield return FidelityScene("fid_d09", 8, 21.3f, 1.4f, -2.2f, false);  // campfire night: fires and their light
        }

        /// <summary>Day 9 at night with its fires burning, for R12-2's before and after.</summary>
        IEnumerator NightLightShots()
        {
            GameFlow.I.DebugStart(8, true);
            GameSettings.Hints = false;
            yield return new WaitForSeconds(1.2f);
            foreach (var n in L.Needs) if (n is FireNeed f) f.Ignite(0.9f);
            for (float t = 0; t < 1.5f; t += Time.deltaTime) { L.SetHour(21.3f); yield return null; }
            yield return MoveTo(-1.0f, -3.4f, 0.4f);
            L.SetHour(21.3f);
            yield return Shot("n01_night_fires");
            Time.timeScale = 0f;
            Quality.Forced = Quality.Tier.Low;   // Low draws no lights besides the sun
            Quality.Apply();
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("n02_night_fires_low");
            Time.timeScale = 1f;
            Quality.Forced = null;
            Quality.Apply();
        }
    }
}
