using System;
using UnityEngine;

namespace PocketWeather
{
    /// <summary>
    /// Pip, the player's cloud. Owns the water resource and the three verbs: rain, gust, and
    /// (passively) shade; drinks automatically over open water. Movement is a slightly
    /// underdamped spring toward the input target on a fixed-altitude plane.
    /// </summary>
    public class Cloud : MonoBehaviour
    {
        public const float MaxWater = 100f;
        public const float RainRate = 14f;       // water per second at full shower
        public const float DrinkRate = 32f;
        public const float GustCost = 6f;
        public const float Altitude = 3.1f;      // above the island base
        public const float GustRange = 4.8f;
        public const float GustHalfAngle = 30f;

        public static Cloud Instance { get; private set; }

        public Level Level { get; private set; }
        public float Water { get; private set; }
        public float Fill => Water / MaxWater;
        public Vector3 Velocity => new Vector3(moveSpring.velocity.x, 0, moveSpring.velocity.z);
        public Vector3 Facing { get; private set; } = Vector3.forward;
        public float RainIntensity { get; private set; }
        public bool Raining => RainIntensity > 0.02f;
        public bool Drinking { get; private set; }
        public float DrinkAmount { get; private set; }
        public WaterBody DrinkingFrom { get; private set; }
        public float Radius => radiusSpring.value;
        public float ShadeRadius => radiusSpring.value * 1.05f;
        public Vector3 GroundPoint { get; private set; }
        public bool Frozen;                          // cutscenes / menus
        public float WaterUsed { get; private set; } // for stats
        public float RainBoost = 1f;                 // finale sneeze

        public CloudInput Input { get; private set; }
        public CloudVisual Visual { get; private set; }
        public RainSystem Rain { get; private set; }

        public event Action<Vector3, bool> OnGust;   // dir, strong
        public event Action OnFull;
        public event Action OnEmptyTry;

        Spring3 moveSpring;
        Spring radiusSpring;
        float gustTimer = -1f;
        Vector3 gustDir;
        bool gustStrong;
        float gustCooldown;
        bool wasFull;
        float emptyTryCooldown;
        float forcedRainTimer;

        public static Cloud Create(Level level, Vector3 groundStart, float water)
        {
            var go = new GameObject("Pip");
            var c = go.AddComponent<Cloud>();
            c.Level = level;
            c.Water = Mathf.Clamp(water, 0, MaxWater);
            Instance = c;
            var start = new Vector3(groundStart.x, level.BaseHeight + Altitude, groundStart.z);
            go.transform.position = start;
            c.moveSpring.value = start;
            c.radiusSpring.value = c.TargetRadius;
            c.GroundPoint = new Vector3(start.x, level.GroundHeight(start.x, start.z), start.z);
            c.Input = go.AddComponent<CloudInput>();
            c.Input.Init(c);
            c.Visual = go.AddComponent<CloudVisual>();
            c.Visual.Init(c);
            c.Rain = go.AddComponent<RainSystem>();
            c.Rain.Init(c);
            return c;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public float TargetRadius => Mathf.Lerp(0.62f, 1.18f, Mathf.Sqrt(Fill));

        public Vector3 ClampToBounds(Vector3 p)
        {
            if (Level == null) return p;
            float hw = Level.Def.island.w / 2 - 0.35f, hd = Level.Def.island.d / 2 - 0.35f;
            p.x = Mathf.Clamp(p.x, -hw, hw);
            p.z = Mathf.Clamp(p.z, -hd, hd);
            return p;
        }

        public void AddWater(float amount)
        {
            Water = Mathf.Clamp(Water + amount, 0, MaxWater);
        }

        public void SetWater(float w) { Water = Mathf.Clamp(w, 0, MaxWater); }

        /// <summary>Involuntary downpour (the finale's sneeze).</summary>
        public void ForceRain(float seconds) { forcedRainTimer = seconds; }

        public void Teleport(Vector3 ground)
        {
            var p = new Vector3(ground.x, Level.BaseHeight + Altitude, ground.z);
            moveSpring.value = p;
            moveSpring.velocity = Vector3.zero;
            transform.position = p;
            Input.Virtual(p, false);
            Input.VirtualMode = false;
            Input.ResetTarget(new Vector3(ground.x, 0, ground.z));
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            if (dt <= 0) return;
            Input.Tick(dt);

            // ---- movement
            Vector3 target = Input.Target;
            target = ClampToBounds(target);
            if (Frozen) target = new Vector3(moveSpring.value.x, 0, moveSpring.value.z);
            var goal = new Vector3(target.x, Level.BaseHeight + Altitude, target.z);
            float omega = Raining ? 7.5f : 8.5f;
            if (gustTimer >= 0) omega = 3f;
            moveSpring.Step(goal, omega, 0.74f, dt);
            float maxSpeed = 16f;
            if (moveSpring.velocity.magnitude > maxSpeed) moveSpring.velocity = moveSpring.velocity.normalized * maxSpeed;
            // gentle bob
            var pos = moveSpring.value;
            pos.y = Level.BaseHeight + Altitude + Mathf.Sin(Time.time * 1.4f) * 0.06f;
            transform.position = pos;
            var flat = Velocity;
            if (flat.magnitude > 0.4f) Facing = Vector3.Slerp(Facing, flat.normalized, 1 - Mathf.Exp(-6 * dt)).normalized;
            GroundPoint = new Vector3(pos.x, Level.GroundHeight(pos.x, pos.z), pos.z);

            // ---- rain
            bool forced = forcedRainTimer > 0;
            if (forced) forcedRainTimer -= dt;
            bool wantRain = (Input.RainHeld && !Frozen && gustTimer < 0) || forced;
            if (wantRain && Water > 0.01f)
            {
                RainIntensity = Mathf.MoveTowards(RainIntensity, 1f, dt / (forced ? 0.1f : 0.35f));
            }
            else
            {
                RainIntensity = Mathf.MoveTowards(RainIntensity, 0f, dt / 0.12f);
                if (wantRain && Water <= 0.01f)
                {
                    emptyTryCooldown -= dt;
                    if (emptyTryCooldown <= 0)
                    {
                        emptyTryCooldown = 0.9f;
                        OnEmptyTry?.Invoke();
                        Rain.Sputter();
                    }
                }
            }
            float use = RainRate * RainIntensity * dt * (forced ? 2.2f : RainBoost);
            use = Mathf.Min(use, Water);
            Water -= use;
            WaterUsed += use;
            Rain.Emit(use, dt);

            // ---- drink
            Drinking = false;
            DrinkAmount = 0;
            DrinkingFrom = null;
            if (!wantRain && RainIntensity < 0.05f && gustTimer < 0 && !Frozen)
            {
                var wb = Level.WaterAt(GroundPoint);
                if (wb != null)
                {
                    DrinkingFrom = wb;
                    if (Water < MaxWater - 0.01f)
                    {
                        float want = Mathf.Min(DrinkRate * dt, MaxWater - Water);
                        float got = wb.Take(want);
                        Water += got;
                        DrinkAmount = got;
                        Drinking = got > 0;
                    }
                }
            }
            bool full = Water >= MaxWater - 0.01f;
            if (full && !wasFull && DrinkingFrom != null) OnFull?.Invoke();
            wasFull = full;

            // ---- gust
            gustCooldown -= dt;
            if (Input.HasGust)
            {
                Input.ConsumeGust();
                if (gustCooldown <= 0 && gustTimer < 0 && !Frozen) StartGust(Input.GustDir);
            }
            if (gustTimer >= 0)
            {
                float before = gustTimer;
                gustTimer += dt;
                if (before < 0.14f && gustTimer >= 0.14f) Blow();
                if (gustTimer > 0.5f) gustTimer = -1f;
            }

            // ---- size + shade
            radiusSpring.Step(TargetRadius, 9f, 0.38f, dt);
            Shader.SetGlobalVector("_PW_Shade", new Vector4(pos.x, pos.z, ShadeRadius, Frozen ? 0.6f : 0.75f));
        }

        public float GustPhase => gustTimer;        // -1 idle, 0..0.14 inhale, 0.14..0.5 blow
        public Vector3 GustDirection => gustDir;
        public bool GustStrong => gustStrong;

        void StartGust(Vector3 dir)
        {
            dir.y = 0;
            if (dir.sqrMagnitude < 1e-4) dir = Facing;
            gustDir = dir.normalized;
            gustStrong = Water >= GustCost;
            gustTimer = 0f;
            gustCooldown = 0.45f;
            Facing = gustDir;
        }

        void Blow()
        {
            if (gustStrong)
            {
                Water -= GustCost;
                WaterUsed += GustCost;
                // recoil
                moveSpring.velocity -= gustDir * 2.5f;
                Level.ApplyGust(GroundPoint, gustDir, 1f);
            }
            else
            {
                Level.ApplyGust(GroundPoint, gustDir, 0.18f);
            }
            OnGust?.Invoke(gustDir, gustStrong);
        }

        /// <summary>True if a ground point is inside the cloud's shade.</summary>
        public bool Shades(Vector3 p, float slack = 0f)
        {
            var d = new Vector2(p.x - transform.position.x, p.z - transform.position.z);
            return d.magnitude < ShadeRadius * 0.92f + slack;
        }
    }
}
