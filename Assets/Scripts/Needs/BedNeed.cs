using System.Collections.Generic;
using UnityEngine;

namespace PocketWeather
{
    /// <summary>
    /// A planted bed with a moisture band. Below the band the plants are thirsty sprouts, inside
    /// it they bloom (met), above it the bed is soggy: flowers droop in a puddle until the sun
    /// dries it back into the band.
    /// </summary>
    public class BedNeed : Need, IRainReceiver
    {
        public float Moisture { get; private set; }
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
            _ => "flower",
        };
        public override string ProblemIcon => "soggy";
        public Surface RainSurface => Surface.Leaf;
        public override Vector3 BubbleAnchor => transform.position + Vector3.up * 1.0f;

        class Plant
        {
            public Transform root, sprout, flower, head;
            public float phase, bounce, bloomPop;
            public bool bloomed;
        }

        readonly List<Plant> plants = new();
        Renderer soil;
        MaterialPropertyBlock mpb;
        float wasSoggy;
        float recentRain;
        Color petalColor = Color.white;
        float soggyOopsCooldown;

        protected override void Build()
        {
            mpb = new MaterialPropertyBlock();
            Moisture = Def.start;
            string bedModel = string.IsNullOrEmpty(Def.model) ? "flower_bed" : Def.model;
            var bed = Res.Spawn(bedModel, transform, Vector3.zero);
            float sx = Def.w / 1.5f, sz = Def.d / 0.95f;
            bed.transform.localScale = new Vector3(sx, 1, sz);
            foreach (var r in bed.GetComponentsInChildren<MeshRenderer>())
                if (r.name.Contains("soil")) soil = r;
            AddReceiver(new Vector3(0, 0.25f, 0), new Vector3(Def.w * 0.92f, 0.5f, Def.d * 0.92f));

            petalColor = Def.plant switch
            {
                "butter" => Res.Hex("FFD45C"),
                "lilac" => Res.Hex("B79CFF"),
                "pink" => Res.Hex("FF9EC4"),
                "white" => Res.Hex("FBFAF7"),
                _ => Res.Hex("FF6F61"),
            };
            int cols = Mathf.Max(2, Mathf.RoundToInt(Def.w / 0.38f));
            int rows = Mathf.Max(1, Mathf.RoundToInt(Def.d / 0.42f));
            var flowerName = "flower_" + (string.IsNullOrEmpty(Def.plant) ? "coral" : Def.plant);
            var rnd = new System.Random(Def.id.GetHashCode());
            for (int j = 0; j < rows; j++)
            for (int i = 0; i < cols; i++)
            {
                float x = ((i + 0.5f) / cols - 0.5f) * (Def.w - 0.3f);
                float z = ((j + 0.5f) / rows - 0.5f) * (Def.d - 0.3f);
                x += (float)(rnd.NextDouble() - 0.5) * 0.08f;
                z += (float)(rnd.NextDouble() - 0.5) * 0.08f;
                var p = new Plant { phase = (float)rnd.NextDouble() * 6f };
                p.root = new GameObject("Plant").transform;
                p.root.SetParent(transform, false);
                p.root.localPosition = new Vector3(x, 0.12f, z);
                p.root.localRotation = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0);
                p.sprout = Res.Spawn("plant_sprout", p.root, Vector3.zero).transform;
                p.flower = Res.Spawn(flowerName, p.root, Vector3.zero).transform;
                var head = p.flower.Find(flowerName + "_head");
                p.head = head != null ? head : p.flower;
                plants.Add(p);
            }
        }

        public void ReceiveRain(float amount, Vector3 point)
        {
            Moisture = Mathf.Min(Def.max, Moisture + amount);
            recentRain += amount;
            // the nearest plant bounces
            Plant best = null;
            float bd = 1e9f;
            foreach (var p in plants)
            {
                float d = (p.root.position - point).sqrMagnitude;
                if (d < bd) { bd = d; best = p; }
            }
            if (best != null) best.bounce = 1f;
        }

        public override void Tick(float dt)
        {
            float dry = (Level.Def.island.dryRate + Def.dry) * (Level.Day != null ? Level.Day.Heat : 1f);
            if (dry > 0)
            {
                bool shaded = Cloud.Instance != null && Cloud.Instance.Shades(transform.position, 0.3f);
                Moisture = Mathf.Max(0, Moisture - dry * (shaded ? 0.25f : 1f) * dt);
            }
            else if (Soggy)
            {
                // soggy beds always drain slowly back towards the band
                Moisture = Mathf.Max(BandMax - 0.01f, Moisture - 1.6f * dt);
            }
            recentRain = Mathf.Max(0, recentRain - dt * 6f);

            bool inBand = Moisture >= BandMin && Moisture <= BandMax;
            Problem = Soggy;
            Progress = Mathf.Clamp01(Moisture / Mathf.Max(1, BandMin));
            soggyOopsCooldown -= dt;
            if (Soggy && wasSoggy <= 0 && soggyOopsCooldown <= 0)
            {
                Oops(transform.position);
                soggyOopsCooldown = 3f;
                Fx.Ripple(transform.position + Vector3.up * 0.14f, Def.w * 0.7f, 0.5f);
            }
            wasSoggy = Soggy ? 1 : 0;
            SetMet(inBand);

            // ---- visuals
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
                float sproutScale = Mathf.Lerp(0.35f, 1.1f, Mathf.Clamp01(g / 0.65f)) * (g >= 0.98f ? 0f : 1f);
                float flowerScale = g >= 0.98f ? 1f : Mathf.Clamp01((g - 0.6f) / 0.38f) * 0.7f;
                if (inBand && !p.bloomed)
                {
                    p.bloomed = true;
                    p.bloomPop = 1f;
                    Fx.Petals(p.root.position + Vector3.up * 0.3f, petalColor, 4, 1.1f);
                    Sfx.Play("bloom", p.root.position, 0.7f, Random.Range(0.9f, 1.15f));
                }
                if (!inBand && !Soggy) p.bloomed = false;
                p.bloomPop = Mathf.MoveTowards(p.bloomPop, 0, dt * 2.2f);
                float pop = 1f + Ease.Punch(1 - p.bloomPop, 2.5f) * 0.45f;
                p.sprout.localScale = Vector3.one * Mathf.Max(0.001f, sproutScale);
                p.flower.localScale = Vector3.one * Mathf.Max(0.001f, flowerScale) * (p.bloomed ? pop : 1f);
                // thirsty droop / soggy droop / rain bounce / idle sway
                float thirsty = 1f - g;
                float droop = thirsty * 18f + soggy * 32f;
                float sway = Mathf.Sin(t * 1.8f + p.phase) * 3f + p.bounce * Mathf.Sin(t * 30f) * 9f;
                p.root.localRotation = Quaternion.Euler(droop + sway, p.root.localEulerAngles.y, sway * 0.5f);
                p.root.localScale = new Vector3(1, 1f - soggy * 0.15f - p.bounce * 0.08f, 1);
                if (p.head != p.flower) p.head.localScale = Vector3.one * (1f - soggy * 0.2f);
            }
            if (Soggy && Random.value < dt * 4f)
                Fx.Bubble(transform.position + new Vector3(Random.Range(-Def.w, Def.w) * 0.4f, 0.13f, Random.Range(-Def.d, Def.d) * 0.4f));
        }
    }
}
