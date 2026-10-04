using UnityEngine;

namespace PocketWeather
{
    public static class Ease
    {
        public static float OutCubic(float t) { t = Mathf.Clamp01(t); return 1 - Mathf.Pow(1 - t, 3); }
        public static float InCubic(float t) { t = Mathf.Clamp01(t); return t * t * t; }
        public static float InOutCubic(float t) { t = Mathf.Clamp01(t); return t < 0.5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2; }
        public static float OutBack(float t, float s = 1.70158f) { t = Mathf.Clamp01(t) - 1; return t * t * ((s + 1) * t + s) + 1; }
        public static float OutElastic(float t)
        {
            t = Mathf.Clamp01(t);
            if (t == 0 || t == 1) return t;
            return Mathf.Pow(2, -10 * t) * Mathf.Sin((t * 10 - 0.75f) * (2 * Mathf.PI / 3)) + 1;
        }
        public static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
        /// <summary>Frame-rate independent exponential approach.</summary>
        public static float Damp(float current, float target, float rate, float dt) => Mathf.Lerp(target, current, Mathf.Exp(-rate * dt));
        public static Vector3 Damp(Vector3 current, Vector3 target, float rate, float dt) => Vector3.Lerp(target, current, Mathf.Exp(-rate * dt));
        public static Color Damp(Color current, Color target, float rate, float dt) => Color.Lerp(target, current, Mathf.Exp(-rate * dt));
        /// <summary>1 at t=0 decaying to 0, with a few wobbles (for punch scales).</summary>
        public static float Punch(float t, float freq = 3f) { t = Mathf.Clamp01(t); return Mathf.Sin(t * Mathf.PI * freq) * (1 - t); }
    }

    /// <summary>Damped spring (semi-implicit), for jelly motion.</summary>
    public struct Spring
    {
        public float value, velocity;
        public void Step(float target, float omega, float zeta, float dt)
        {
            float f = 1 + 2 * dt * zeta * omega;
            float oo = omega * omega;
            float hoo = dt * oo;
            float hhoo = dt * hoo;
            float detInv = 1 / (f + hhoo);
            float detX = f * value + dt * velocity + hhoo * target;
            float detV = velocity + hoo * (target - value);
            value = detX * detInv;
            velocity = detV * detInv;
        }
    }

    public struct Spring3
    {
        public Vector3 value, velocity;
        public void Step(Vector3 target, float omega, float zeta, float dt)
        {
            float f = 1 + 2 * dt * zeta * omega;
            float oo = omega * omega;
            float hoo = dt * oo;
            float hhoo = dt * hoo;
            float detInv = 1 / (f + hhoo);
            Vector3 detX = f * value + dt * velocity + hhoo * target;
            Vector3 detV = velocity + hoo * (target - value);
            value = detX * detInv;
            velocity = detV * detInv;
        }
    }
}
