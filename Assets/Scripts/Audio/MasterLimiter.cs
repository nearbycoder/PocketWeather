using UnityEngine;

namespace PocketWeather
{
    /// <summary>
    /// Master bus limiter on the AudioListener. Busy moments (a rain chord, a cheer and the
    /// celebration jingle together) used to sum past full scale and hard-clip at Unity's output;
    /// measured on the recorded mix, ~2000 clipped samples in 30 of 220 seconds. This trims the
    /// bus a little, then a 2 ms look-ahead peak limiter holds it under -1 dBFS, with a soft clip
    /// as a last safety net. Runs on the audio thread, allocation-free.
    /// </summary>
    [RequireComponent(typeof(AudioListener))]
    public class MasterLimiter : MonoBehaviour
    {
        const float PreGain = 0.84f;       // -1.5 dB of headroom for the whole mix
        const float Threshold = 0.89f;     // -1 dBFS
        const float LookaheadSec = 0.002f;
        const float AttackSec = 0.0015f;
        const float ReleaseSec = 0.15f;
        const int MaxChannels = 8;

        float[] delay;
        int delayFrames, writePos, holdLeft, channelsSeen;
        float gain = 1f, heldPeak, attackCoef, releaseCoef;
        public static float GainReductionDb;   // for diagnostics: most recent reduction

        void Awake()
        {
            int sr = AudioSettings.outputSampleRate;
            delayFrames = Mathf.Max(1, Mathf.RoundToInt(LookaheadSec * sr));
            delay = new float[delayFrames * MaxChannels];
            attackCoef = 1f - Mathf.Exp(-1f / (AttackSec * sr));
            releaseCoef = 1f - Mathf.Exp(-1f / (ReleaseSec * sr));
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (delay == null || channels > MaxChannels) return;
            if (channels != channelsSeen) { System.Array.Clear(delay, 0, delay.Length); channelsSeen = channels; }
            float minGain = 1f;
            for (int i = 0; i < data.Length; i += channels)
            {
                // peak of this frame, held for the look-ahead window so the gain is already down
                // when the peak comes out of the delay line
                float peak = 0f;
                for (int c = 0; c < channels; c++)
                {
                    float x = data[i + c] * PreGain;
                    data[i + c] = x;
                    float ax = x < 0 ? -x : x;
                    if (ax > peak) peak = ax;
                }
                if (peak >= heldPeak) { heldPeak = peak; holdLeft = delayFrames; }
                else if (--holdLeft <= 0) { heldPeak = peak; holdLeft = 0; }
                float target = heldPeak > Threshold ? Threshold / heldPeak : 1f;
                gain += (target - gain) * (target < gain ? attackCoef : releaseCoef);
                if (gain < minGain) minGain = gain;

                int d = writePos * channels;
                for (int c = 0; c < channels; c++)
                {
                    float delayed = delay[d + c];
                    delay[d + c] = data[i + c];
                    float y = delayed * gain;
                    // soft clip above 0.95: smooth, never past 1
                    float ay = y < 0 ? -y : y;
                    if (ay > 0.95f)
                    {
                        float over = (ay - 0.95f) / 0.05f;
                        ay = 0.95f + 0.05f * (over / (1f + over));
                        y = y < 0 ? -ay : ay;
                    }
                    data[i + c] = y;
                }
                writePos = (writePos + 1) % delayFrames;
            }
            GainReductionDb = 20f * Mathf.Log10(Mathf.Max(minGain, 1e-4f));
        }
    }
}
