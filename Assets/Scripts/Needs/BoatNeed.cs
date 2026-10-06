using UnityEngine;

namespace PocketWeather
{
    /// <summary>
    /// A becalmed sailboat. Gusts push it (strongest when the gust is aimed straight at it);
    /// it glides with water drag, turns into its motion with the sail filling, bounces off the
    /// shore, and moors when it reaches its goal (dock or buoy).
    /// </summary>
    public class BoatNeed : Need, IGustReceiver, IRainReceiver
    {
        public override string Icon => "boat";
        public override string WantIcon => "wind";
        public Vector3 GustPoint => boat.position;
        public Surface RainSurface => Surface.Wood;
        public override Vector3 BubbleAnchor => boat.position + Vector3.up * 1.45f;
        public Vector3 Goal => new Vector3(Def.goal[0], 0, Def.goal[1]);
        public Vector3 Position => boat.position;
        public Vector3 Velocity => vel;

        Transform boat, sail, hull;
        Vector3 vel;
        float yaw, yawVel, heel, sailFill;
        WaterBody water;
        Transform goalMarker;
        Material goalMat;
        float moorT = -1f;

        protected override void Build()
        {
            water = Level.FindWater(Def.water);
            var go = Res.Spawn(string.IsNullOrEmpty(Def.model) ? "sailboat" : Def.model, transform, Vector3.zero, 0, Def.s);
            boat = go.transform;
            sail = boat.Find("Sail");
            hull = boat.Find("Hull");
            yaw = Def.ry;
            float lvl = water != null ? water.Level : 0;
            transform.position = new Vector3(Def.x, 0, Def.z);
            boat.position = new Vector3(Def.x, lvl, Def.z);
            AddReceiver(new Vector3(0, 0.5f, 0), new Vector3(0.4f, 1.0f, 0.9f), boat);
            // goal ring on the water
            goalMarker = Fx.MakeGroundDecal("BoatGoal", Def.goalR * 2f, out goalMat);
            goalMarker.SetParent(Level.transform, false);
            goalMarker.position = new Vector3(Def.goal[0], lvl + 0.03f, Def.goal[1]);
            goalMat.SetColor("_Color", new Color(1f, 0.95f, 0.7f, 0.6f));
        }

        public void ReceiveGust(Vector3 dir, float strength)
        {
            if (Met) return;
            vel += dir * (2.3f * strength);
            sailFill = Mathf.Max(sailFill, strength);
            yawVel += Vector3.SignedAngle(Forward(), dir, Vector3.up) * 0.8f * strength;
            Sfx.Play("sail", boat.position, 0.8f);
        }

        public void ReceiveRain(float amount, Vector3 point) { }

        Vector3 Forward() => Quaternion.Euler(0, yaw, 0) * Vector3.forward;

        public override void Tick(float dt)
        {
            float lvl = water != null ? water.Level : 0;
            var p = boat.position;
            if (!Met)
            {
                var next = p + vel * dt;
                // stay on water: probe the bow and the centre
                bool ok = Level.WaterAt(next, true) == water && Level.WaterAt(next + vel.normalized * 0.35f, true) == water;
                if (!ok)
                {
                    // bounce off the shore: reflect against the gradient towards deeper water
                    var n = Level.ShoreNormal(next, water);
                    vel = Vector3.Reflect(vel, n) * 0.35f;
                    next = p + n * 0.02f;
                    Fx.Splash(next, Surface.Water, 0.8f);
                }
                p = next;
                vel *= Mathf.Exp(-0.75f * dt);
                if (vel.magnitude < 0.02f) vel = Vector3.zero;
                // turn into motion
                if (vel.magnitude > 0.15f)
                {
                    float targetYaw = Mathf.Atan2(vel.x, vel.z) * Mathf.Rad2Deg;
                    yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, dt * 90f * Mathf.Clamp01(vel.magnitude));
                }
                yaw += yawVel * dt;
                yawVel *= Mathf.Exp(-3f * dt);
                if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(Def.goal[0], Def.goal[1])) < Def.goalR)
                {
                    SetMet(true);
                    moorT = 0;
                    Sfx.Play("bell", p);
                    Fx.Ripple(p, 1.2f, 0.7f);
                }
                if (vel.magnitude > 0.3f && Random.value < dt * 12f)
                    Fx.Ripple(p - Forward() * 0.4f, 0.3f, 0.35f);
            }
            else
            {
                moorT += dt;
                var goal = new Vector3(Def.goal[0], lvl, Def.goal[1]);
                p = Vector3.Lerp(p, goal, 1 - Mathf.Exp(-2f * dt));
                vel = Vector3.zero;
            }
            float t = Time.time;
            sailFill = Mathf.MoveTowards(sailFill, Mathf.Clamp01(vel.magnitude * 0.6f), dt * 1.5f);
            heel = Mathf.Lerp(heel, Mathf.Clamp(yawVel * 0.2f + sailFill * 10f, -18f, 18f), 1 - Mathf.Exp(-4f * dt));
            p.y = lvl + Mathf.Sin(t * 1.7f + Def.x) * 0.015f;
            boat.position = p;
            boat.rotation = Quaternion.Euler(Mathf.Sin(t * 1.3f) * 2f, yaw, -heel + Mathf.Sin(t * 1.1f + 1f) * 2.5f);
            if (sail != null) sail.localScale = new Vector3(1f + sailFill * 0.6f, 1, 1);
            if (goalMarker != null)
            {
                goalMarker.position = new Vector3(Def.goal[0], lvl + 0.02f, Def.goal[1]);
                float pulse = Met ? 0f : 0.45f + 0.15f * Mathf.Sin(t * 3f);
                goalMat.SetColor("_Color", new Color(1f, 0.95f, 0.7f, pulse));
            }
            Progress = Met ? 1f : Mathf.Clamp01(1f - Vector2.Distance(new Vector2(p.x, p.z), new Vector2(Def.goal[0], Def.goal[1])) / 6f);
        }
    }
}
