using UnityEngine;

namespace PocketWeather
{
    /// <summary>
    /// Little lives that make the diorama feel inhabited: butterflies flitting over the grass by
    /// day and fireflies drifting in the dusk. Rain scatters the butterflies for a moment.
    /// </summary>
    public class AmbientLife : MonoBehaviour
    {
        Level level;
        ParticleSystem butterflies, fireflies;

        public void Init(Level l)
        {
            level = l;
            butterflies = Make("Butterflies", Fx.Shape.Leaf, false, 14, new Vector2(0.07f, 0.11f), 1.6f);
            fireflies = Make("Fireflies", Fx.Shape.Dot, true, 40, new Vector2(0.07f, 0.12f), 0.6f);
        }

        ParticleSystem Make(string name, Fx.Shape shape, bool additive, int max, Vector2 size, float noise)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 14f);
            main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.maxParticles = max;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            var d = level.Def.island;
            sh.scale = new Vector3(d.w * 0.8f, 0.8f, d.d * 0.75f);
            sh.position = new Vector3(0, level.BaseHeight + 0.9f, 0);
            var em = ps.emission;
            em.rateOverTime = 0;
            var n = ps.noise;
            n.enabled = true;
            n.strength = noise;
            n.frequency = 0.35f;
            n.scrollSpeed = 0.25f;
            n.damping = true;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.1f), new GradientAlphaKey(1, 0.85f), new GradientAlphaKey(0, 1) });
            col.color = g;
            if (shape == Fx.Shape.Leaf)
            {
                var rot = ps.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
                var sz = ps.sizeOverLifetime;
                sz.enabled = true;
                // wing flap: rapid size wobble on x via separate axes
                sz.separateAxes = true;
                var flap = new AnimationCurve();
                for (int k = 0; k <= 60; k++) flap.AddKey(k / 60f, 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(k * 1.9f)));
                sz.x = new ParticleSystem.MinMaxCurve(1f, flap);
                sz.y = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0, 1, 1f));
                sz.z = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0, 1, 1f));
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.35f), new Color(0.75f, 0.6f, 1f));
            }
            else
            {
                main.startColor = new Color(0.85f, 1f, 0.45f, 1f);
                var sz = ps.sizeOverLifetime;
                sz.enabled = true;
                var pulse = new AnimationCurve();
                for (int k = 0; k <= 40; k++) pulse.AddKey(k / 40f, 0.3f + 0.7f * Mathf.Pow(Mathf.Abs(Mathf.Sin(k * 0.9f)), 3f));
                sz.size = new ParticleSystem.MinMaxCurve(1f, pulse);
            }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = Fx.Mat(shape, additive, 0.6f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        void Update()
        {
            if (level == null || level.Day == null) return;
            float night = level.Day.Night;
            bool raining = Cloud.Instance != null && Cloud.Instance.Raining;
            var e1 = butterflies.emission;
            e1.rateOverTime = (1f - night) * (raining ? 0.2f : 0.9f) * level.Def.island.scatter;
            var e2 = fireflies.emission;
            e2.rateOverTime = Mathf.Clamp01((night - 0.15f) * 2f) * 4f;
        }
    }
}
