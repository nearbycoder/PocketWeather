using System.Collections.Generic;
using UnityEngine;

namespace PocketWeather
{
    // ===================================================================== Laundry
    /// <summary>Wet washing on a line: gusts dry it (clothes flap), rain soaks it again.</summary>
    public class LaundryNeed : Need, IGustReceiver, IRainReceiver
    {
        public float Wetness { get; private set; } = 1f;
        public override string Icon => "shirt";
        public override string ProblemIcon => "soggy";
        public Vector3 GustPoint => transform.position + Vector3.up * 0.6f;
        public Surface RainSurface => Surface.Fabric;
        public override Vector3 BubbleAnchor => transform.position + Vector3.up * 1.55f;

        readonly List<Transform> clothes = new();
        readonly List<Renderer> clothRenderers = new();
        Spring swing;
        float flap, dripTimer;
        bool wasDry;
        MaterialPropertyBlock mpb;

        protected override void Build()
        {
            mpb = new MaterialPropertyBlock();
            Wetness = Def.start > 0 ? Mathf.Clamp01(Def.start) : 1f;
            var m = Res.Spawn(string.IsNullOrEmpty(Def.model) ? "laundry_line" : Def.model, transform, Vector3.zero, Def.ry, Def.s);
            foreach (Transform c in m.transform)
                if (c.name.StartsWith("Cloth")) { clothes.Add(c); clothRenderers.Add(c.GetComponent<Renderer>()); }
            AddReceiver(new Vector3(0, 0.7f, 0), new Vector3(1.9f, 0.6f, 0.3f), m.transform);
        }

        public void ReceiveGust(Vector3 dir, float strength)
        {
            Wetness = Mathf.Max(0, Wetness - 0.36f * strength);
            swing.velocity += 260f * strength;
            flap = 1f;
            Sfx.Play("flap", transform.position, 0.8f);
            for (int i = 0; i < clothes.Count; i++)
                if (Wetness > 0.05f) Fx.Splash(clothes[i].position + Vector3.down * 0.25f, Surface.Fabric, 0.7f);
        }

        public void ReceiveRain(float amount, Vector3 point)
        {
            bool wasMet = Met;
            Wetness = Mathf.Min(1, Wetness + amount * 0.12f);
            if (wasMet && Wetness > 0.25f) Oops(point);
        }

        public override void Tick(float dt)
        {
            bool dry = Wetness < 0.05f;
            if (dry && !wasDry) Fx.Sparkles(BubbleAnchor - Vector3.up * 0.6f, 8, Color.white, 0.5f, 1.2f, 0.15f);
            wasDry = dry;
            SetMet(dry);
            Progress = 1f - Wetness;
            Problem = Wetness > 0.25f && MetTime < 0 && false;
            swing.Step(0, 9f, 0.25f, dt);
            flap = Mathf.MoveTowards(flap, 0, dt * 0.8f);
            float t = Time.time;
            for (int i = 0; i < clothes.Count; i++)
            {
                float a = swing.value * (0.7f + 0.15f * i) + Mathf.Sin(t * (1.5f + i * 0.3f) + i) * (3f + flap * 18f) * (0.3f + 0.7f * flap);
                clothes[i].localRotation = Quaternion.Euler(a, 0, Mathf.Sin(t * 7f + i) * flap * 10f);
                if (clothRenderers[i] != null)
                {
                    clothRenderers[i].GetPropertyBlock(mpb);
                    mpb.SetFloat("_Wet", Wetness * 0.9f);
                    clothRenderers[i].SetPropertyBlock(mpb);
                }
            }
            dripTimer -= dt;
            if (Wetness > 0.1f && dripTimer <= 0 && clothes.Count > 0)
            {
                dripTimer = Mathf.Lerp(0.6f, 0.12f, Wetness);
                var c = clothes[Random.Range(0, clothes.Count)];
                Fx.Splash(new Vector3(c.position.x, transform.position.y + 0.02f, c.position.z), Surface.Soil, 0.5f);
            }
        }
    }

    // ===================================================================== Windmill
    /// <summary>A windmill that needs a few good gusts to spin up and grind flour; then it keeps turning.</summary>
    public class WindmillNeed : Need, IGustReceiver
    {
        public float Charge { get; private set; }
        public override string Icon => "windmill";
        public Vector3 GustPoint => transform.position + Vector3.up * 1.2f;
        public override Vector3 BubbleAnchor => transform.position + Vector3.up * 2.5f;
        Transform sails;
        float spin, angle, puffTimer;

        protected override void Build()
        {
            var m = Res.Spawn("windmill", transform, Vector3.zero, Def.ry, Def.s);
            sails = m.transform.Find("Sails");
            var col = new GameObject("Collider");
            col.layer = Layers.Props;
            col.transform.SetParent(m.transform, false);
            col.transform.localPosition = new Vector3(0, 1.0f, 0);
            col.AddComponent<BoxCollider>().size = new Vector3(1.1f, 2.0f, 1.1f);
            col.AddComponent<SurfaceTag>().surface = Surface.Wood;
        }

        public void ReceiveGust(Vector3 dir, float strength)
        {
            Charge = Mathf.Min(1, Charge + strength / Mathf.Max(1f, Def.time));
            spin += 420f * strength;
            Sfx.Play("creak", transform.position, 0.8f);
        }

        public override void Tick(float dt)
        {
            if (!Met) Charge = Mathf.Max(0, Charge - dt * 0.025f);
            if (Charge >= 0.999f && !Met)
            {
                SetMet(true);
                Fx.Steam(transform.position + Vector3.up * 0.4f + transform.forward * -0.7f, 10, 0.3f, new Color(1, 0.98f, 0.92f, 0.8f));
            }
            Progress = Charge;
            float target = Met ? 160f : Charge * 120f;
            spin = Mathf.Lerp(spin, target, 1 - Mathf.Exp(-0.8f * dt));
            angle += spin * dt;
            if (sails != null) sails.localRotation = Quaternion.Euler(0, 0, -angle);
            if (Met)
            {
                puffTimer -= dt;
                if (puffTimer <= 0)
                {
                    puffTimer = 0.8f;
                    Fx.Steam(transform.position + Vector3.up * 0.15f + transform.rotation * new Vector3(0.4f, 0, -0.75f), 1, 0.15f, new Color(1, 0.98f, 0.92f, 0.6f));
                }
            }
        }
    }

    // ===================================================================== Rainbow wish
    /// <summary>Someone who wants to see a rainbow (a kid, or the couple at the wedding).</summary>
    public class RainbowWishNeed : Need, IRainReceiver
    {
        public override string Icon => "rainbow";
        public Surface RainSurface => Surface.Creature;
        public override Vector3 BubbleAnchor => transform.position + Vector3.up * 1.1f;
        Critter critter;
        Critter partner;

        protected override void Build()
        {
            var go = Res.Spawn(string.IsNullOrEmpty(Def.model) ? "person_kid" : Def.model, transform, Vector3.zero, Def.ry, Def.s);
            critter = gameObject.AddComponent<Critter>();
            critter.Init(go.transform, true);
            critter.mood = Critter.Mood.Sad;
            if (Def.links != null && Def.links.Length > 0 && !string.IsNullOrEmpty(Def.links[0]))
            {
                var p = Res.Spawn(Def.links[0], transform, new Vector3(0.32f, 0, 0), Def.ry, Def.s);
                partner = p.AddComponent<Critter>();
                partner.Init(p.transform, true);
                partner.mood = Critter.Mood.Sad;
            }
            AddReceiver(new Vector3(0, 0.3f, 0), new Vector3(0.5f, 0.6f, 0.5f));
        }

        public void ReceiveRain(float amount, Vector3 point) { }

        public override void OnRainbow(Rainbow rb)
        {
            if (Dormant) return;
            if (!Met)
            {
                SetMet(true);
                critter.mood = Critter.Mood.Cheer;
                if (partner != null) partner.mood = Critter.Mood.Cheer;
                Fx.Hearts(BubbleAnchor, 5);
                Sfx.Play("person_happy", transform.position, 1f, 1.1f);
            }
        }

        public override void Tick(float dt)
        {
            Progress = Met ? 1 : Mathf.Clamp01(Level.Rainbows.MistAt(transform.position) / 13f);
            if (Met && critter.mood == Critter.Mood.Cheer && Time.time - MetTime > 3f)
            {
                critter.mood = Critter.Mood.Happy;
                if (partner != null) partner.mood = Critter.Mood.Happy;
            }
        }

        public void SetSad(bool sad)
        {
            critter.mood = sad ? Critter.Mood.Sad : Critter.Mood.Idle;
            if (partner != null) partner.mood = critter.mood;
        }
    }

    // ===================================================================== Flame visual
    /// <summary>Flames, embers, smoke and a warm flickering light, scaled by an intensity.</summary>
    public class Flame : MonoBehaviour
    {
        public float Intensity;
        public float Size = 1f;
        ParticleSystem flames, embers;
        Light glow;
        float smokeTimer;

        public static Flame Create(Transform parent, Vector3 localPos, float size)
        {
            var go = new GameObject("Flame");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var f = go.AddComponent<Flame>();
            f.Size = size;
            f.flames = MakePs(go.transform, "Flames", Fx.Shape.Puff, true, size);
            f.embers = MakePs(go.transform, "Embers", Fx.Shape.Star, true, size * 0.35f);
            var em = f.embers.main;
            em.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            em.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
            var lgo = new GameObject("Glow");
            lgo.transform.SetParent(go.transform, false);
            lgo.transform.localPosition = Vector3.up * 0.4f * size;
            f.glow = lgo.AddComponent<Light>();
            f.glow.type = LightType.Point;
            f.glow.color = new Color(1f, 0.6f, 0.25f);
            f.glow.range = 2.8f * size;
            f.glow.intensity = 0;
            f.glow.shadows = LightShadows.None;
            return f;
        }

        static ParticleSystem MakePs(Transform parent, string name, Fx.Shape shape, bool additive, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f * size, 1.1f * size);
            main.startSize = new ParticleSystem.MinMaxCurve(0.22f * size, 0.42f * size);
            main.maxParticles = 200;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.95f, 0.55f), 0), new GradientColorKey(new Color(1f, 0.5f, 0.15f), 0.5f), new GradientColorKey(new Color(0.8f, 0.2f, 0.1f), 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0, 1) });
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = grad;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.6f), new Keyframe(0.3f, 1f), new Keyframe(1, 0.2f)));
            var shp = ps.shape;
            shp.shapeType = ParticleSystemShapeType.Circle;
            shp.radius = 0.18f * size;
            shp.rotation = new Vector3(-90, 0, 0);
            var em = ps.emission;
            em.rateOverTime = 0;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.4f;
            noise.frequency = 1.5f;
            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.y = new ParticleSystem.MinMaxCurve(0.6f * size, 1.2f * size);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = Fx.Mat(shape, additive, 0.7f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        void Update()
        {
            float i = Mathf.Clamp01(Intensity);
            var e1 = flames.emission; e1.rateOverTime = 42f * i * Size;
            var e2 = embers.emission; e2.rateOverTime = 6f * i * Size;
            glow.intensity = i * (1.6f + 0.5f * Mathf.PerlinNoise(Time.time * 6f, 0f)) * (0.6f + (Level.Current != null && Level.Current.Day != null ? Level.Current.Day.Night * 1.6f : 0));
            smokeTimer -= Time.deltaTime;
            if (i > 0.02f && smokeTimer <= 0)
            {
                smokeTimer = Mathf.Lerp(0.6f, 0.18f, i);
                Fx.Smoke(transform.position + Vector3.up * 0.6f * Size, new Color(0.35f, 0.32f, 0.36f, 0.45f), 0.3f * Size);
            }
        }
    }

    // ===================================================================== Fire (put it out)
    /// <summary>
    /// Something burning (a haystack, a bush). Rain douses it (sizzling steam you can drink);
    /// left alone it grows back and spreads to linked neighbours; gusts make it flare.
    /// </summary>
    public class FireNeed : Need, IRainReceiver, IGustReceiver
    {
        public float Intensity { get; private set; }
        public override string Icon => "fire";
        public Surface RainSurface => Surface.Fire;
        public Vector3 GustPoint => transform.position + Vector3.up * 0.4f;
        public override Vector3 BubbleAnchor => transform.position + Vector3.up * 1.6f;
        public bool Burning => Intensity > 0.01f;
        Flame flame;
        float hotTime;
        float burnt;
        Renderer[] renderers;
        MaterialPropertyBlock mpb;
        float sizzleCooldown;

        protected override void Build()
        {
            mpb = new MaterialPropertyBlock();
            var m = Res.Spawn(string.IsNullOrEmpty(Def.model) ? "haystack" : Def.model, transform, Vector3.zero, Def.ry, Def.s);
            renderers = m.GetComponentsInChildren<Renderer>();
            Intensity = Def.start;
            flame = Flame.Create(transform, new Vector3(0, 0.45f * Def.s, 0), 1.9f * Def.s);
            AddReceiver(new Vector3(0, 0.45f, 0), new Vector3(1.0f, 0.9f, 1.0f) * Def.s);
        }

        public void Ignite(float amount = 0.6f)
        {
            if (Burning) return;
            Intensity = amount;
            Fx.Sparkles(transform.position + Vector3.up * 0.5f, 10, new Color(1, 0.6f, 0.2f), 0.5f, 2f, 0.2f);
            Sfx.Play("oops", transform.position, 0.5f, 0.8f);
        }

        public void ReceiveRain(float amount, Vector3 point)
        {
            if (!Burning) return;
            Intensity = Mathf.Max(0, Intensity - amount * 0.045f);
            sizzleCooldown -= 0.1f;
            if (sizzleCooldown <= 0) { sizzleCooldown = 0.4f; Sfx.Play("sizzle", transform.position, 0.6f); }
            if (Random.value < 0.25f) Fx.Steam(point + Vector3.up * 0.2f, 1, 0.2f);
            if (!Burning)
            {
                Fx.Steam(transform.position + Vector3.up * 0.5f, 14, 0.5f);
                Level.SpawnMotes(transform.position + Vector3.up * 0.8f, 3);
            }
        }

        public void ReceiveGust(Vector3 dir, float strength)
        {
            if (!Burning) return;
            Intensity = Mathf.Min(1.2f, Intensity + 0.3f * strength);
            hotTime += 4f * strength;
            Fx.Sparkles(transform.position + Vector3.up * 0.6f + dir * 0.4f, 8, new Color(1, 0.55f, 0.2f), 0.4f, 2.5f, 0.18f);
        }

        public override void Tick(float dt)
        {
            if (Burning && Level.Running)
            {
                Intensity = Mathf.Min(1f, Intensity + dt * 0.03f);
                hotTime += dt * (Intensity > 0.7f ? 1f : 0.2f);
                burnt = Mathf.Min(1, burnt + dt * 0.02f);
                if (hotTime > 9f && Def.links != null)
                {
                    hotTime = 0;
                    foreach (var id in Def.links)
                        if (Level.FindNeed(id) is FireNeed n && !n.Burning) { n.Ignite(); break; }
                }
            }
            else hotTime = Mathf.Max(0, hotTime - dt);
            flame.Intensity = Mathf.Clamp01(Intensity);
            float fireSum = 0;
            foreach (var n in Level.Needs) if (n is FireNeed f) fireSum += f.Intensity;
            if (AudioHub.I != null) AudioHub.I.FireLevel = Mathf.Max(AudioHub.I.FireLevel * 0.98f, Mathf.Clamp01(fireSum));
            foreach (var r in renderers)
            {
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", Color.Lerp(Color.white, new Color(0.35f, 0.3f, 0.28f), burnt));
                r.SetPropertyBlock(mpb);
            }
            SetMet(!Burning);
            Progress = 1f - Mathf.Clamp01(Intensity);
        }
    }

    // ===================================================================== Campfire (keep it lit)
    /// <summary>The campers' fire: rain puts it out (they relight it after a while); a gust makes it roar.</summary>
    public class CampfireNeed : Need, IRainReceiver, IGustReceiver
    {
        public float Lit { get; private set; } = 1f;
        public override string Icon => "campfire";
        public override string ProblemIcon => "campfire";
        public override bool ShowBubble => Required && (!Met || Lit < 0.5f);
        public Surface RainSurface => Surface.Fire;
        public Vector3 GustPoint => transform.position + Vector3.up * 0.3f;
        public override Vector3 BubbleAnchor => transform.position + Vector3.up * 1.2f;
        Flame flame;
        float outTimer;
        float roar;

        protected override void Build()
        {
            Res.Spawn("campfire", transform, Vector3.zero, Def.ry, Def.s);
            flame = Flame.Create(transform, new Vector3(0, 0.1f, 0), 1.25f);
            AddReceiver(new Vector3(0, 0.25f, 0), new Vector3(0.7f, 0.5f, 0.7f));
        }

        public void ReceiveRain(float amount, Vector3 point)
        {
            if (Lit <= 0) return;
            Lit = Mathf.Max(0, Lit - amount * 0.07f);
            if (Random.value < 0.3f) Fx.Steam(point + Vector3.up * 0.15f, 1, 0.15f);
            if (Lit <= 0)
            {
                outTimer = 5f;
                Fx.Steam(transform.position + Vector3.up * 0.3f, 10, 0.3f);
                Sfx.Play("sizzle", transform.position, 0.8f);
                Oops(transform.position);
            }
        }

        public void ReceiveGust(Vector3 dir, float strength)
        {
            if (Lit <= 0.05f) return;
            roar = Mathf.Min(1.5f, roar + strength);
            Lit = 1f;
            Fx.Sparkles(transform.position + Vector3.up * 0.5f, 14, new Color(1, 0.6f, 0.2f), 0.4f, 3f, 0.2f);
            if (strength > 0.5f) Level.ReportDelight(Id, "gust_on");
        }

        public override void Tick(float dt)
        {
            if (Lit <= 0)
            {
                outTimer -= dt;
                if (outTimer <= 0) { Lit = 0.6f; Fx.Sparkles(transform.position + Vector3.up * 0.3f, 8, new Color(1, 0.7f, 0.3f), 0.2f, 1.5f, 0.15f); }
            }
            else Lit = Mathf.Min(1, Lit + dt * 0.08f);
            roar = Mathf.MoveTowards(roar, 0, dt * 0.4f);
            flame.Intensity = Mathf.Clamp01(Lit) * (1f + roar * 0.8f);
            flame.Size = 1.25f + roar * 0.7f;
            if (AudioHub.I != null) AudioHub.I.FireLevel = Mathf.Max(AudioHub.I.FireLevel * 0.98f, Lit * 0.6f);
            SetMet(Lit > 0.15f);
            Problem = Lit <= 0;
            Progress = Lit;
        }
    }

    // ===================================================================== Keep dry
    /// <summary>Something that must not get soaked (a sandcastle, the wedding cake, a market stall).
    /// Rain damages it; once ruined it's unmet until it's been fixed (rebuilt/dried) over time.</summary>
    public class KeepDryNeed : Need, IRainReceiver
    {
        public float Damage { get; private set; }
        public bool Ruined { get; private set; }
        public override string Icon => (Def.model ?? "").Contains("castle") ? "castle" : (Def.model ?? "").Contains("cake") ? "cake" : "shirt";
        public override string ProblemIcon => "soggy";
        public override bool ShowBubble => Required && Ruined;
        public Surface RainSurface => Surface.Fabric;
        public override Vector3 BubbleAnchor => transform.position + Vector3.up * 1.2f;
        Transform model;
        float repair;
        Renderer[] renderers;
        MaterialPropertyBlock mpb;

        protected override void Build()
        {
            mpb = new MaterialPropertyBlock();
            model = Res.Spawn(Def.model, transform, Vector3.zero, Def.ry, Def.s).transform;
            renderers = model.GetComponentsInChildren<Renderer>();
            var b = Res.RenderBounds(model.gameObject);
            AddReceiver(transform.InverseTransformPoint(b.center), b.size * 1.05f);
            SetMet(true);
        }

        public void ReceiveRain(float amount, Vector3 point)
        {
            if (Ruined) return;
            Damage = Mathf.Min(1, Damage + amount * 0.09f);
            repair = 0;
            if (Damage >= 1f)
            {
                Ruined = true;
                Oops(point);
                Fx.Splash(transform.position + Vector3.up * 0.3f, Surface.Soil, 2f);
            }
        }

        public override void Tick(float dt)
        {
            if (Ruined)
            {
                repair += dt;
                if (repair > Mathf.Max(3f, Def.time))
                {
                    Ruined = false;
                    Damage = 0;
                    Fx.Sparkles(transform.position + Vector3.up * 0.4f, 10, Color.white, 0.4f, 1.5f, 0.15f);
                }
            }
            else Damage = Mathf.Max(0, Damage - dt * 0.05f);
            SetMet(!Ruined);
            Problem = Ruined;
            Progress = Ruined ? repair / Mathf.Max(3f, Def.time) : 1f - Damage;
            float sag = Ruined ? Mathf.Lerp(0.35f, 1f, Ease.Smooth(repair / Mathf.Max(3f, Def.time))) : 1f - Damage * 0.12f;
            bool melts = (Def.model ?? "").Contains("castle");
            model.localScale = new Vector3(Def.s * (melts && Ruined ? 1.15f : 1f), Def.s * (melts ? sag : 1f), Def.s * (melts && Ruined ? 1.15f : 1f));
            foreach (var r in renderers)
            {
                r.GetPropertyBlock(mpb);
                mpb.SetFloat("_Wet", Mathf.Max(Damage, Ruined ? 0.9f : 0f));
                r.SetPropertyBlock(mpb);
            }
        }
    }

    // ===================================================================== Pond line
    /// <summary>The ducks need their pond: don't drink it below the line. Rain into it refills it.</summary>
    public class PondLineNeed : Need
    {
        public override string Icon => "duck";
        public override string ProblemIcon => "oops";
        public override bool ShowBubble => Required && !Met;
        public override Vector3 BubbleAnchor => (water != null ? new Vector3(water.Def.x, water.Level, water.Def.z) : transform.position) + Vector3.up * 1.0f;
        WaterBody water;
        readonly List<(Transform t, float phase, float r)> ducks = new();
        float line;
        bool wasMet = true;

        protected override void Build()
        {
            water = Level.FindWater(Def.water);
            line = Def.target != null && Def.target.Length > 0 ? Def.target[0] : 0.4f;
            int n = Mathf.Max(1, Mathf.RoundToInt(Def.time));
            for (int i = 0; i < n; i++)
            {
                var d = Res.Spawn(i == 0 ? "duck" : "duckling", transform, Vector3.zero, 0, i == 0 ? 1.4f : 0.85f);
                ducks.Add((d.transform, i * 0.9f, 0.5f + i * 0.12f));
            }
            SetMet(true);
        }

        public override void Tick(float dt)
        {
            if (water == null) return;
            float f = water.Fraction;
            bool met = f >= line;
            if (!met && wasMet)
            {
                Oops(BubbleAnchor);
                Sfx.Play("duck", BubbleAnchor);
            }
            wasMet = met;
            SetMet(met);
            Problem = !met;
            Progress = Mathf.Clamp01(f / Mathf.Max(0.01f, line));
            float t = Time.time;
            var c = new Vector3(water.Def.x, water.Level, water.Def.z);
            for (int i = 0; i < ducks.Count; i++)
            {
                var (tr, ph, r) = ducks[i];
                float speed = met ? 0.25f : 0.9f;
                float a = t * speed + ph;
                float rx = Mathf.Min(water.Def.rx * 0.55f, r * 1.4f) * Mathf.Lerp(0.5f, 1f, f), rz = Mathf.Min(water.Def.rz * 0.55f, r) * Mathf.Lerp(0.5f, 1f, f);
                var p = c + new Vector3(Mathf.Cos(a) * rx, 0.01f + Mathf.Sin(t * 3f + ph) * 0.01f, Mathf.Sin(a) * rz);
                tr.position = p;
                tr.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)), Vector3.up);
                if (!met && Random.value < dt * 0.6f) Sfx.Play("duck", p, 0.4f, Random.Range(1f, 1.3f));
            }
        }
    }

    // ===================================================================== Reactive critter (delights)
    /// <summary>
    /// A secret: a critter or prop that reacts when you rain on it, gust it, shade it or put a
    /// rainbow over it (a snail pokes out, a kid jumps in puddles, a kite takes off, a bell rings).
    /// </summary>
    public class ReactNeed : Need, IRainReceiver, IGustReceiver
    {
        public override string Icon => "star";
        public override bool Required => false;
        public Surface RainSurface => Surface.Creature;
        public Vector3 GustPoint => transform.position + Vector3.up * 0.4f;
        public override Vector3 BubbleAnchor => transform.position + Vector3.up * 1.0f;
        Critter critter;
        Transform model, kite, bell;
        float acc, shadeT;
        bool done;
        float kiteT = -1f, bellSwing;
        string trigger, react;

        protected override void Build()
        {
            trigger = string.IsNullOrEmpty(Def.trigger) ? "rain" : Def.trigger;
            react = Def.react ?? "";
            model = Res.Spawn(Def.model, transform, Vector3.zero, Def.ry, Def.s).transform;
            bool alive = Def.model.StartsWith("person") || Def.model == "snail" || Def.model == "seal" || Def.model == "robin" || Def.model == "lamb" ||
                         Def.model == "pig" || Def.model == "frog" || Def.model == "crab" || Def.model == "cat" || Def.model == "dog";
            if (alive)
            {
                critter = gameObject.AddComponent<Critter>();
                critter.Init(model, Def.model.StartsWith("person"));
            }
            if (react == "kite")
            {
                kite = Res.Spawn("kite", transform, new Vector3(0.15f, 0.35f, 0.05f), Def.ry, 0.8f).transform;
            }
            if (react == "bell") bell = model.Find("Bell");
            if (react == "snail" && model.Find("Head") != null) model.Find("Head").localScale = Vector3.one * 0.2f;
            var b = Res.RenderBounds(model.gameObject);
            AddReceiver(transform.InverseTransformPoint(b.center), Vector3.Max(b.size * 1.2f, new Vector3(0.4f, 0.4f, 0.4f)));
        }

        public void ReceiveRain(float amount, Vector3 point)
        {
            if (trigger != "rain") return;
            acc += amount;
            if (acc >= Mathf.Max(0.5f, Def.amount)) Fire();
        }

        public void ReceiveGust(Vector3 dir, float strength)
        {
            if (trigger != "gust") return;
            if (react == "bell") { bellSwing += 40f * strength; Sfx.Play("bell", transform.position, 0.7f); }
            acc += strength;
            if (acc >= Mathf.Max(0.5f, Def.amount)) Fire();
        }

        public override void OnRainbow(Rainbow rb) { if (trigger == "rainbow") Fire(); }

        void Fire()
        {
            if (done) return;
            done = true;
            SetMet(true);
            Level.ReportDelight(Id, trigger + "_on");
            Fx.Hearts(BubbleAnchor, 4);
            Fx.Sparkles(BubbleAnchor, 12, new Color(1f, 0.85f, 0.95f), 0.5f, 1.8f, 0.18f);
            if (!string.IsNullOrEmpty(Def.sound)) Sfx.Play(Def.sound, transform.position);
            if (critter != null) critter.mood = react == "splash" || react == "cheer" ? Critter.Mood.Cheer : Critter.Mood.Happy;
            if (react == "kite") kiteT = 0;
            if (react == "snail" && model.Find("Head") != null)
            {
                var h = model.Find("Head");
                Tween.To(0.2f, 1f, 0.6f, s => { if (h != null) h.localScale = Vector3.one * s; }, k => Ease.OutBack(k), 0, null, null, false);
            }
        }

        public override void Tick(float dt)
        {
            if (trigger == "shade" && !done)
            {
                var c = Cloud.Instance;
                if (c != null && c.Shades(transform.position, 0.2f) && !c.Raining) shadeT += dt; else shadeT = Mathf.Max(0, shadeT - dt);
                if (shadeT > Mathf.Max(1.5f, Def.amount)) Fire();
            }
            if (kite != null)
            {
                if (kiteT >= 0)
                {
                    kiteT += dt;
                    float k = Ease.OutCubic(Mathf.Clamp01(kiteT / 2.5f));
                    var t = Time.time;
                    kite.localPosition = Vector3.Lerp(new Vector3(0.15f, 0.35f, 0.05f), new Vector3(0.9f + Mathf.Sin(t * 0.8f) * 0.3f, 2.3f + Mathf.Sin(t * 1.3f) * 0.2f, 1.2f), k);
                    kite.localRotation = Quaternion.Euler(-60 * k + Mathf.Sin(t * 2f) * 10f, 0, Mathf.Sin(t * 1.7f) * 20f * k);
                }
            }
            if (bell != null)
            {
                bellSwing *= Mathf.Exp(-1.5f * dt);
                bell.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 9f) * bellSwing, 0, 0);
            }
            if (done && react == "splash" && Random.value < dt * 5f)
                Fx.Splash(transform.position + Random.insideUnitSphere * 0.2f, Surface.Water, 1.2f);
            Progress = done ? 1 : 0;
        }
    }
}
