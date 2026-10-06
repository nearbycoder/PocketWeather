using System.Collections.Generic;
using UnityEngine;

namespace PocketWeather
{
    /// <summary>
    /// A planted bed with a moisture band. Below the band the plants are thirsty sprouts, inside
    /// it they bloom (met), above it the bed is soggy: plants droop in a puddle until the sun
    /// dries it back into the band. Frames: flower_bed, crop_bed, window_box, mud_wallow,
    /// wedding_arch. Plants: flowers (coral/butter/lilac/pink/white), crops (carrot, cabbage,
    /// tomato, wheat, pumpkin, sunflower), sapling, mud. Layouts: grid, single, arch.
    /// </summary>
    public class BedNeed : Need, IRainReceiver
    {
        public float Moisture { get; protected set; }
        public float BandMin => Def.target[0];
        public float BandMax => Def.target.Length > 1 ? Def.target[1] : Def.target[0] * 1.8f;
        public bool Soggy => Moisture > BandMax;
        public override string Icon => Def.plant switch
        {
            "carrot" => "carrot",
            "cabbage" => "cabbage",
            "tomato" => "tomato",
            "wheat" => "wheat",
            "pumpkin" => "pumpkin",
            "mud" => "mud",
            "sunflower" => "sunflower",
            _ => "flower",
        };
        public override string ProblemIcon => "soggy";
        public override string WantIcon => "drop";
        public virtual Surface RainSurface => Def.plant == "mud" ? Surface.Soil : Surface.Leaf;
        public override Vector3 BubbleAnchor => transform.position + Vector3.up * (layout == "arch" ? 2.0f : layout == "single" ? 1.25f : 1.0f);
        public bool InBand => Moisture >= BandMin && Moisture <= BandMax;
        /// <summary>The moisture at a full bubble ring: the band sits at a fixed spot inside it, so the
        /// player can see how much more rain the bed takes before it goes soggy.</summary>
        public float GaugeMax => BandMax * 1.3f;
        /// <summary>Time.time of the last drop that landed here.</summary>
        public float LastRainedAt { get; private set; } = -99f;
        /// <summary>A happy bed still shows its bubble while rain is landing on it (and a moment after),
        /// so the player can watch the moisture climb toward the top of the band and stop in time.</summary>
        public override bool ShowBubble => Required && (!Met || Time.time - LastRainedAt < 1.2f);

        protected class Plant
        {
            public Transform root, sprout, flower, head;
            public float phase, bounce, bloomPop, baseScale = 1f;
            public bool bloomed;
            public Transform[] stages;
        }

        protected readonly List<Plant> plants = new();
        protected Renderer soil;
        MaterialPropertyBlock mpb;
        float wasSoggy;
        Color petalColor = Color.white;
        float soggyOopsCooldown;
        protected string layout = "grid";
        float soilY = 0.12f;
        Transform frame;

        protected override void Build()
        {
            mpb = new MaterialPropertyBlock();
            Moisture = Def.start;
            string plant = string.IsNullOrEmpty(Def.plant) ? "coral" : Def.plant;
            string frameModel = string.IsNullOrEmpty(Def.model) ? (IsCrop(plant) ? "crop_bed" : plant == "mud" ? "mud_wallow" : plant == "sapling" ? "none" : "flower_bed") : Def.model;
            layout = string.IsNullOrEmpty(Def.anim) ? (frameModel == "wedding_arch" ? "arch" : plant == "sapling" ? "single" : "grid") : Def.anim;
            Vector2 baseSize = frameModel switch
            {
                "window_box" => new Vector2(0.9f, 0.3f),
                "mud_wallow" => new Vector2(1.3f, 0.9f),
                "wedding_arch" => new Vector2(1.4f, 0.3f),
                "none" => new Vector2(Def.w, Def.d),
                _ => new Vector2(1.5f, 0.95f),
            };
            soilY = frameModel switch { "window_box" => 0.19f, "crop_bed" => 0.12f, "mud_wallow" => 0.05f, "none" => 0f, "wedding_arch" => 0f, _ => 0.12f };
            if (frameModel != "none")
            {
                frame = Res.Spawn(frameModel, transform, Vector3.zero).transform;
                if (frameModel != "wedding_arch") frame.localScale = new Vector3(Def.w / baseSize.x, 1, Def.d / baseSize.y);
                foreach (var r in frame.GetComponentsInChildren<MeshRenderer>())
                    if (r.name.Contains("soil")) soil = r;
            }
            float rh = layout == "arch" ? 1.6f : layout == "single" ? 0.8f : 0.5f;
            AddReceiver(new Vector3(0, rh / 2, 0), new Vector3(Mathf.Max(0.5f, Def.w * 0.92f), rh, Mathf.Max(0.5f, Def.d * 0.92f)));

            petalColor = plant switch
            {
                "butter" => Res.Hex("FFD45C"),
                "lilac" => Res.Hex("B79CFF"),
                "pink" => Res.Hex("FF9EC4"),
                "white" => Res.Hex("FBFAF7"),
                "carrot" => Res.Hex("F28C38"),
                "tomato" => Res.Hex("E8574A"),
                "pumpkin" => Res.Hex("F28C38"),
                "wheat" => Res.Hex("F2C14E"),
                "cabbage" => Res.Hex("8FD16A"),
                "sunflower" => Res.Hex("FFD23F"),
                "mud" => Res.Hex("7A5638"),
                _ => Res.Hex("FF6F61"),
            };
            if (plant == "mud") return;
            string plantModel = IsCrop(plant) ? "crop_" + plant : "flower_" + plant;
            var rnd = new System.Random(Def.id.GetHashCode());
            if (plant == "sapling")
            {
                var p = NewPlant(Vector3.zero, 0);
                p.stages = new Transform[3];
                for (int k = 0; k < 3; k++)
                {
                    p.stages[k] = Res.Spawn("sapling_" + k, p.root, Vector3.zero).transform;
                    p.stages[k].gameObject.SetActive(false);
                }
                return;
            }
            if (layout == "single")
            {
                var p = NewPlant(new Vector3(0, soilY, 0), (float)rnd.NextDouble() * 360f);
                p.baseScale = Def.s * 2.2f;
                AttachModels(p, plantModel);
                return;
            }
            if (layout == "arch")
            {
                int n = 11;
                for (int k = 0; k < n; k++)
                {
                    float a = Mathf.PI * k / (n - 1);
                    var pos = new Vector3(Mathf.Cos(a) * 0.62f, 0.95f + Mathf.Sin(a) * 0.62f - 0.08f, -0.1f);
                    var p = NewPlant(pos, 180f);
                    p.root.localRotation = Quaternion.Euler(-90f + 10f, 0, -Mathf.Rad2Deg * a + 90f);
                    p.baseScale = 0.75f;
                    AttachModels(p, plantModel);
                }
                for (int s = -1; s <= 1; s += 2)
                    for (int k = 0; k < 3; k++)
                    {
                        var p = NewPlant(new Vector3(s * 0.62f, 0.15f + k * 0.28f, -0.1f), 180f);
                        p.root.localRotation = Quaternion.Euler(-80f, 0, s * 90f);
                        p.baseScale = 0.7f;
                        AttachModels(p, plantModel);
                    }
                return;
            }
            int cols = Mathf.Max(2, Mathf.RoundToInt(Def.w / (IsCrop(plant) ? 0.34f : 0.38f)));
            int rows = Mathf.Max(1, Mathf.RoundToInt(Def.d / 0.42f));
            for (int j = 0; j < rows; j++)
            for (int i = 0; i < cols; i++)
            {
                float x = ((i + 0.5f) / cols - 0.5f) * (Def.w - 0.3f);
                float z = ((j + 0.5f) / rows - 0.5f) * (Def.d - 0.3f);
                x += (float)(rnd.NextDouble() - 0.5) * 0.08f;
                z += (float)(rnd.NextDouble() - 0.5) * 0.08f;
                var p = NewPlant(new Vector3(x, soilY, z), (float)rnd.NextDouble() * 360f);
                p.baseScale = Def.s;
                AttachModels(p, plantModel);
            }
        }

        static bool IsCrop(string p) => p == "carrot" || p == "cabbage" || p == "tomato" || p == "wheat" || p == "pumpkin" || p == "sunflower";

        Plant NewPlant(Vector3 pos, float yaw)
        {
            var p = new Plant { phase = Random.value * 6f };
            p.root = new GameObject("Plant").transform;
            p.root.SetParent(transform, false);
            p.root.localPosition = pos;
            p.root.localRotation = Quaternion.Euler(0, yaw, 0);
            plants.Add(p);
            return p;
        }

        void AttachModels(Plant p, string plantModel)
        {
            p.sprout = Res.Spawn("plant_sprout", p.root, Vector3.zero).transform;
            p.flower = Res.Spawn(plantModel, p.root, Vector3.zero).transform;
            var head = p.flower.Find(plantModel + "_head");
            p.head = head != null ? head : p.flower;
        }

        public void ReceiveRain(float amount, Vector3 point)
        {
            Moisture = Mathf.Min(Def.max, Moisture + amount);
            LastRainedAt = Time.time;
            Plant best = null;
            float bd = 1e9f;
            foreach (var p in plants)
            {
                float d = (p.root.position - point).sqrMagnitude;
                if (d < bd) { bd = d; best = p; }
            }
            if (best != null) best.bounce = 1f;
            OnRained(amount);
        }

        protected virtual void OnRained(float amount) { }

        protected virtual bool ExtraMet => true;

        public override void Tick(float dt)
        {
            float dry = (Level.Def.island.dryRate + Def.dry) * (Level.Day != null ? Level.Day.Heat : 1f);
            if (dry > 0 && Level.Running)
            {
                bool shaded = Cloud.Instance != null && Cloud.Instance.Shades(transform.position, 0.3f);
                Moisture = Mathf.Max(0, Moisture - dry * (shaded ? 0.25f : 1f) * dt);
            }
            if (Soggy)
                Moisture = Mathf.Max(BandMax - 0.01f, Moisture - (1.6f + dry) * dt);

            bool inBand = InBand;
            Problem = Soggy;
            Progress = Mathf.Clamp01(Moisture / Mathf.Max(1, BandMin));
            soggyOopsCooldown -= dt;
            if (Soggy && wasSoggy <= 0 && soggyOopsCooldown <= 0)
            {
                Oops(transform.position);
                soggyOopsCooldown = 3f;
                Fx.Ripple(transform.position + Vector3.up * 0.14f, Mathf.Max(Def.w, 0.6f) * 0.7f, 0.5f);
            }
            wasSoggy = Soggy ? 1 : 0;
            bool met = inBand && ExtraMet;
            if (met && !Met && Def.hidden) Level.ReportDelight(Id, "fill");
            SetMet(met);
            Animate(dt, inBand);
        }

        void Animate(float dt, bool inBand)
        {
            float g = Mathf.Clamp01(Moisture / Mathf.Max(1, BandMin));
            float soggy = Mathf.Clamp01((Moisture - BandMax) / Mathf.Max(1f, BandMax * 0.3f));
            float t = Time.time;
            if (soil != null)
            {
                soil.GetPropertyBlock(mpb);
                mpb.SetFloat("_Wet", Mathf.Clamp01(Moisture / Mathf.Max(1, BandMax)) * 0.85f + soggy * 0.15f);
                soil.SetPropertyBlock(mpb);
            }
            foreach (var p in plants)
            {
                p.bounce = Mathf.MoveTowards(p.bounce, 0, dt * 3f);
                if (p.stages != null)
                {
                    int stage = g < 0.5f ? 0 : g < 0.98f ? 1 : 2;
                    for (int k = 0; k < 3; k++) p.stages[k].gameObject.SetActive(k == stage);
                    float s = stage == 0 ? Mathf.Lerp(0.5f, 1f, g / 0.5f) : stage == 1 ? Mathf.Lerp(0.8f, 1f, (g - 0.5f) / 0.5f) : 1f;
                    if (inBand && !p.bloomed) { p.bloomed = true; p.bloomPop = 1f; Fx.Leaves(p.root.position + Vector3.up, Vector3.up * 0.3f, 8); Sfx.Play("bloom", p.root.position); }
                    p.bloomPop = Mathf.MoveTowards(p.bloomPop, 0, dt * 2f);
                    p.root.localScale = Vector3.one * s * (1f + Ease.Punch(1 - p.bloomPop, 2.5f) * 0.3f) * Def.s;
                    continue;
                }
                float sproutScale = Mathf.Lerp(0.35f, 1.1f, Mathf.Clamp01(g / 0.65f)) * (g >= 0.98f ? 0f : 1f);
                float flowerScale = g >= 0.98f ? 1f : Mathf.Clamp01((g - 0.6f) / 0.38f) * 0.7f;
                if (inBand && !p.bloomed)
                {
                    p.bloomed = true;
                    p.bloomPop = 1f;
                    Fx.Petals(p.root.position + Vector3.up * 0.3f, petalColor, 4, 1.1f);
                    Sfx.Play("bloom", p.root.position, 0.6f, Random.Range(0.9f, 1.15f));
                }
                if (!inBand && !Soggy) p.bloomed = false;
                p.bloomPop = Mathf.MoveTowards(p.bloomPop, 0, dt * 2.2f);
                float pop = 1f + Ease.Punch(1 - p.bloomPop, 2.5f) * 0.45f;
                float bs = p.baseScale;
                if (layout == "single") bs *= Mathf.Lerp(0.5f, 1f, g) * (1f + Mathf.Clamp01((Moisture - BandMin) / Mathf.Max(1, BandMax - BandMin)) * 0.25f);
                p.sprout.localScale = Vector3.one * Mathf.Max(0.001f, sproutScale * bs);
                p.flower.localScale = Vector3.one * Mathf.Max(0.001f, flowerScale * bs) * (p.bloomed ? pop : 1f);
                float thirsty = 1f - g;
                float droop = thirsty * 18f + soggy * 32f;
                float sway = Mathf.Sin(t * 1.8f + p.phase) * 3f + p.bounce * Mathf.Sin(t * 30f) * 9f;
                if (layout == "arch")
                {
                    p.sprout.localRotation = Quaternion.Euler(droop * 0.5f + sway, 0, 0);
                    p.flower.localRotation = p.sprout.localRotation;
                }
                else
                {
                    var e = p.root.localEulerAngles;
                    p.root.localRotation = Quaternion.Euler(droop + sway, e.y, sway * 0.5f);
                    p.root.localScale = new Vector3(1, 1f - soggy * 0.15f - p.bounce * 0.08f, 1);
                }
                if (p.head != p.flower) p.head.localScale = Vector3.one * (1f - soggy * 0.2f);
            }
            if (Soggy && Random.value < dt * 4f)
                Fx.Bubble(transform.position + new Vector3(Random.Range(-Def.w, Def.w) * 0.4f, soilY + 0.02f, Random.Range(-Def.d, Def.d) * 0.4f));
        }
    }

    /// <summary>
    /// A sun-loving bed (sunflowers): needs its moisture band like any bed, but sulks and droops
    /// if Pip shades it for more than a couple of seconds, and recovers after a moment of sun.
    /// </summary>
    public class SunnyNeed : BedNeed
    {
        float shadeTime, sunTime = 99f;
        bool sulking;
        public override string ProblemIcon => sulking ? "sun" : "soggy";
        public override string WantIcon => "sun";

        protected override bool ExtraMet => !sulking;

        public override void Tick(float dt)
        {
            var c = Cloud.Instance;
            bool shaded = c != null && c.Shades(transform.position, 0.2f);
            if (shaded) { shadeTime += dt; sunTime = 0; }
            else { sunTime += dt; shadeTime = Mathf.Max(0, shadeTime - dt * 2f); }
            if (!sulking && shadeTime > 2.6f)
            {
                sulking = true;
                Oops(transform.position);
            }
            if (sulking && sunTime > 1.8f) sulking = false;
            base.Tick(dt);
            if (sulking) Problem = true;
            foreach (var p in plants)
                if (p.head != null && p.head != p.flower)
                    p.head.localRotation = Quaternion.Slerp(p.head.localRotation, Quaternion.Euler(sulking ? 55f : 0f, 0, 0), 1 - Mathf.Exp(-5f * dt));
        }
    }
}
