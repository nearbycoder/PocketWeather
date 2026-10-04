using System;
using UnityEngine;

namespace PocketWeather
{
    /// <summary>
    /// Something in the diorama that wants something. Shows a thought bubble (icon + progress
    /// ring) until met. The level is saved when every required need is met at the same time.
    /// </summary>
    public abstract class Need : MonoBehaviour
    {
        public NeedDef Def { get; private set; }
        public Level Level { get; private set; }
        public string Id => Def.id;
        public bool Met { get; private set; }
        public float Progress { get; protected set; }
        public bool Problem { get; protected set; }      // soggy / grumpy / broken: bubble turns warning
        public virtual bool Required => !Def.hidden && !Dormant;
        public bool Dormant { get; private set; }

        /// <summary>Makes a dormant need required (and visible) mid-level.</summary>
        public void Wake()
        {
            if (!Dormant) return;
            Dormant = false;
            Level.NotifyNeedsChanged();
        }
        public abstract string Icon { get; }
        public virtual string ProblemIcon => "oops";
        public virtual Vector3 BubbleAnchor => transform.position + Vector3.up * 0.9f;
        public virtual bool ShowBubble => Required && !Met;
        public float MetTime { get; private set; } = -1f;

        public event Action<Need> OnMetChanged;

        public void Setup(Level level, NeedDef def)
        {
            Level = level;
            Def = def;
            Dormant = def.dormant;
            name = $"{def.type}:{def.id}";
            Build();
        }

        protected abstract void Build();
        public abstract void Tick(float dt);
        public virtual void OnRainbow(Rainbow rb) { }

        protected void SetMet(bool met)
        {
            if (met == Met) return;
            Met = met;
            MetTime = met ? Time.time : -1f;
            OnMetChanged?.Invoke(this);
            if (met)
            {
                Sfx.Play("need_met", BubbleAnchor);
                Fx.Sparkles(BubbleAnchor, 10, new Color(1f, 0.92f, 0.6f), 0.4f, 1.8f, 0.18f);
                var c = Cloud.Instance;
                if (c != null && Level.Running)
                {
                    c.Visual.Emote(CloudVisual.Delight, 1.1f);
                    c.Visual.Hop(0.8f);
                    c.Visual.LookAtPoint(BubbleAnchor, 1.0f);
                }
            }
        }

        /// <summary>Pip did something bad to this need (overwatered, soaked...).</summary>
        protected void Oops(Vector3 at)
        {
            var c = Cloud.Instance;
            if (c == null || !Level.Running) return;
            c.Visual.Emote(CloudVisual.Oops, 1.0f);
            c.Visual.LookAtPoint(at, 1.0f);
            Sfx.Play("oops", at);
            Sfx.Play("pip_eep", c.transform.position, 0.8f);
            Level.NoteOops(this);
        }

        protected Collider AddReceiver(Vector3 center, Vector3 size, Transform parent = null)
        {
            var go = new GameObject("RainCatcher");
            go.layer = Layers.Receiver;
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.localPosition = center;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            box.isTrigger = true;
            return box;
        }
    }
}
