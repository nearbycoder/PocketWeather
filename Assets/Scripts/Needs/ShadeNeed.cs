using UnityEngine;

namespace PocketWeather
{
    /// <summary>
    /// An overheated creature (sheep, cow, dog, cat, person). Comfort fills while it sits in
    /// Pip's shade and drains slowly in the sun; once full it flops down happily and stays met,
    /// unless you rain on it: then it's grumpy (unmet, comfort halved, sulks a moment).
    /// A rainbow over it forgives everything.
    /// </summary>
    public class ShadeNeed : Need, IRainReceiver
    {
        public float Comfort { get; private set; }
        public bool Grumpy => grumpyTimer > 0;
        public override string Icon => "sun";
        public override string ProblemIcon => "grumpy";
        public override string WantIcon => "pip";   // Pip's shadow
        public Surface RainSurface => Surface.Creature;
        public override Vector3 BubbleAnchor => transform.position + Vector3.up * (person ? 1.05f : 0.95f);
        public bool InShade { get; private set; }

        Critter critter;
        bool person;
        float wetAccum;
        float grumpyTimer;
        float shadeTime;
        float rainbowCooldown;

        protected override void Build()
        {
            string model = string.IsNullOrEmpty(Def.model) ? "sheep" : Def.model;
            person = model.StartsWith("person");
            var go = Res.Spawn(model, transform, Vector3.zero, Def.ry, Def.s);
            critter = gameObject.AddComponent<Critter>();
            critter.Init(go.transform, person);
            critter.mood = Critter.Mood.Hot;
            shadeTime = Mathf.Max(1f, Def.time);
            float h = person ? 0.55f : 0.45f;
            AddReceiver(new Vector3(0, h * 0.6f, 0), new Vector3(0.42f, h, 0.42f) * Def.s);
            if (Def.dislike == "umbrella")
            {
                var u = Res.Spawn("umbrella_small", go.transform, new Vector3(0.08f, 0.0f, 0f));
                critter.SetUmbrella(u.transform);
            }
        }

        public void ReceiveRain(float amount, Vector3 point)
        {
            if (Def.dislike == "none" || Def.dislike == "love") { Comfort = Mathf.Min(1, Comfort + amount * 0.02f); return; }
            if (Def.dislike == "umbrella") { critter.UmbrellaWanted = true; wetAccum += amount * 0.3f; }
            else wetAccum += amount;
            if (wetAccum > 0.9f && grumpyTimer <= 0)
            {
                grumpyTimer = 2.6f;
                Comfort *= 0.5f;
                wetAccum = 0;
                critter.Shake();
                Sfx.Play(SoundFor("grumpy"), transform.position);
                Oops(transform.position);
            }
        }

        string SoundFor(string what)
        {
            string m = Def.model ?? "";
            if (m.Contains("sheep") || m.Contains("lamb")) return "sheep";
            if (m.Contains("cow")) return "cow";
            if (m.Contains("cat")) return "cat";
            if (m.Contains("dog")) return "dog";
            if (m.Contains("pig")) return "pig";
            if (m.Contains("donkey")) return "donkey";
            return what == "happy" ? "person_happy" : "person_upset";
        }

        public override void Tick(float dt)
        {
            var cloud = Cloud.Instance;
            wetAccum = Mathf.Max(0, wetAccum - dt * 0.6f);
            rainbowCooldown -= dt;
            if (grumpyTimer > 0)
            {
                grumpyTimer -= dt;
                if (grumpyTimer <= 0) critter.UmbrellaWanted = false;
            }
            InShade = cloud != null && cloud.Shades(transform.position, 0.15f);
            bool raining = cloud != null && cloud.Raining && InShade;
            if (Level.Rainbows != null && Level.Rainbows.Covers(transform.position) && rainbowCooldown <= 0)
            {
                rainbowCooldown = 3f;
                grumpyTimer = 0;
                Comfort = Mathf.Min(1f, Comfort + 0.5f);
                critter.UmbrellaWanted = false;
                critter.Hop();
                Fx.Hearts(BubbleAnchor, 3);
            }
            if (!Met)
            {
                if (InShade && !Grumpy && !raining) Comfort += dt / shadeTime;
                else if (!InShade) Comfort -= dt * 0.06f;
                Comfort = Mathf.Clamp01(Comfort);
                if (Comfort >= 1f && !Grumpy)
                {
                    SetMet(true);
                    critter.Hop();
                    Fx.Hearts(BubbleAnchor, 2);
                    Sfx.Play(SoundFor("happy"), transform.position, 1f, 1.15f);
                }
            }
            else if (Grumpy)
            {
                SetMet(false);
            }
            Progress = Comfort;
            Problem = Grumpy;

            critter.mood = Grumpy ? Critter.Mood.Grumpy
                : Met ? (person ? Critter.Mood.Happy : Critter.Mood.Content)
                : InShade ? Critter.Mood.Idle : Critter.Mood.Hot;
        }
    }
}
