using System;
using System.Collections.Generic;
using UnityEngine;

namespace PocketWeather
{
    /// <summary>Tiny tween runner (unscaled time by default) for UI juice.</summary>
    public class Tween : MonoBehaviour
    {
        public class Handle
        {
            public float from, to, duration, delay, t;
            public Action<float> setter;
            public Func<float, float> ease;
            public Action done;
            public bool unscaled = true, killed;
            public object owner;
            public void Kill() => killed = true;
        }

        static Tween runner;
        readonly List<Handle> active = new();
        readonly List<Handle> adding = new();

        static Tween Runner
        {
            get
            {
                if (runner == null)
                {
                    var go = new GameObject("Tweens");
                    DontDestroyOnLoad(go);
                    runner = go.AddComponent<Tween>();
                }
                return runner;
            }
        }

        public static Handle To(float from, float to, float duration, Action<float> setter, Func<float, float> ease = null,
            float delay = 0, Action done = null, object owner = null, bool unscaled = true)
        {
            var h = new Handle
            {
                from = from, to = to, duration = Mathf.Max(0.0001f, duration), delay = delay, setter = setter,
                ease = ease ?? Ease.OutCubic, done = done, owner = owner, unscaled = unscaled,
            };
            if (owner != null) KillOwner(owner);
            Runner.adding.Add(h);
            setter?.Invoke(from);
            return h;
        }

        public static void KillOwner(object owner)
        {
            if (runner == null || owner == null) return;
            foreach (var h in runner.active) if (h.owner == owner) h.killed = true;
            foreach (var h in runner.adding) if (h.owner == owner) h.killed = true;
        }

        public static Handle Delay(float seconds, Action action, bool unscaled = true) =>
            To(0, 1, seconds, null, null, 0, action, null, unscaled);

        public static Handle Scale(Transform t, Vector3 from, Vector3 to, float duration, Func<float, float> ease = null, float delay = 0, Action done = null)
        {
            return To(0, 1, duration, k => { if (t != null) t.localScale = Vector3.LerpUnclamped(from, to, k); }, ease, delay, done, t);
        }

        public static Handle Punch(Transform t, float amount = 0.15f, float duration = 0.35f)
        {
            var baseScale = Vector3.one;
            return To(0, 1, duration, k => { if (t != null) t.localScale = baseScale * (1 + Ease.Punch(k, 2.5f) * amount); }, k => k, 0, null, t);
        }

        void Update()
        {
            if (adding.Count > 0) { active.AddRange(adding); adding.Clear(); }
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var h = active[i];
                if (h.killed) { active.RemoveAt(i); continue; }
                float dt = h.unscaled ? Clock.UnscaledDelta : Time.deltaTime;
                if (h.delay > 0) { h.delay -= dt; continue; }
                h.t += dt;
                float k = Mathf.Clamp01(h.t / h.duration);
                try { h.setter?.Invoke(Mathf.LerpUnclamped(h.from, h.to, h.ease(k))); }
                catch (MissingReferenceException) { h.killed = true; }
                if (k >= 1f)
                {
                    active.RemoveAt(i);
                    if (!h.killed) h.done?.Invoke();
                }
            }
        }
    }
}
