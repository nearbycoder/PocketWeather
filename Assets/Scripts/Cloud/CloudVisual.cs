using UnityEngine;

namespace PocketWeather
{
    /// <summary>
    /// Everything you see of Pip: jelly puffs that lag, breathe and squash, the SDF face with
    /// continuously blended expressions and roving eyes, the vapour column while drinking, the
    /// ground reticle and the gust aiming fan.
    /// </summary>
    public class CloudVisual : MonoBehaviour
    {
        public struct Expr
        {
            public float open, happy, squeeze, wide, curve, mouthOpen, width, round, wobble, blush, sweat, sparkle, brow;
            public static Expr Lerp(Expr a, Expr b, float t)
            {
                Expr r;
                r.open = Mathf.Lerp(a.open, b.open, t); r.happy = Mathf.Lerp(a.happy, b.happy, t);
                r.squeeze = Mathf.Lerp(a.squeeze, b.squeeze, t); r.wide = Mathf.Lerp(a.wide, b.wide, t);
                r.curve = Mathf.Lerp(a.curve, b.curve, t); r.mouthOpen = Mathf.Lerp(a.mouthOpen, b.mouthOpen, t);
                r.width = Mathf.Lerp(a.width, b.width, t); r.round = Mathf.Lerp(a.round, b.round, t);
                r.wobble = Mathf.Lerp(a.wobble, b.wobble, t); r.blush = Mathf.Lerp(a.blush, b.blush, t);
                r.sweat = Mathf.Lerp(a.sweat, b.sweat, t); r.sparkle = Mathf.Lerp(a.sparkle, b.sparkle, t);
                r.brow = Mathf.Lerp(a.brow, b.brow, t);
                return r;
            }
            public static Expr Make(float open = 1, float happy = 0, float squeeze = 0, float wide = 0, float curve = 0.55f,
                float mouthOpen = 0, float width = 0.9f, float round = 0, float wobble = 0, float blush = 0.45f,
                float sweat = 0, float sparkle = 0, float brow = 0)
            {
                return new Expr
                {
                    open = open, happy = happy, squeeze = squeeze, wide = wide, curve = curve, mouthOpen = mouthOpen,
                    width = width, round = round, wobble = wobble, blush = blush, sweat = sweat, sparkle = sparkle, brow = brow
                };
            }
        }

        public static readonly Expr Idle = Expr.Make();
        public static readonly Expr Content = Expr.Make(open: 0.55f, curve: 0.7f, blush: 0.55f);
        public static readonly Expr Drinking = Expr.Make(open: 0f, curve: 0.2f, mouthOpen: 0.45f, round: 1f, blush: 0.65f, width: 0.6f);
        public static readonly Expr Full = Expr.Make(happy: 1f, curve: 0.95f, mouthOpen: 0.4f, blush: 0.95f, sparkle: 0.4f);
        public static readonly Expr RainLight = Expr.Make(open: 0.6f, curve: 0.5f, mouthOpen: 0.12f, blush: 0.5f);
        public static readonly Expr RainHard = Expr.Make(squeeze: 1f, curve: -0.1f, mouthOpen: 0.3f, width: 0.75f, blush: 0.75f);
        public static readonly Expr Empty = Expr.Make(open: 0.55f, curve: -0.45f, wobble: 0.7f, sweat: 1f, blush: 0.2f, brow: 0.6f, width: 0.75f);
        public static readonly Expr Low = Expr.Make(open: 0.8f, curve: 0.05f, sweat: 0.45f, brow: 0.3f);
        public static readonly Expr Inhale = Expr.Make(wide: 0.5f, curve: 0f, round: 1f, mouthOpen: 0f, blush: 1f, width: 0.5f);
        public static readonly Expr Blow = Expr.Make(open: 0.35f, squeeze: 0.5f, round: 1f, mouthOpen: 1f, blush: 1f, width: 0.5f);
        public static readonly Expr Puff = Expr.Make(open: 0.8f, curve: -0.2f, round: 1f, mouthOpen: 0.2f, blush: 0.6f, sweat: 0.4f, width: 0.5f);
        public static readonly Expr Delight = Expr.Make(happy: 1f, sparkle: 1f, curve: 1f, mouthOpen: 0.55f, blush: 1f);
        public static readonly Expr Oops = Expr.Make(wide: 1f, curve: -0.55f, mouthOpen: 0.45f, round: 0.55f, sweat: 1f, brow: 0.8f, blush: 0.3f);
        public static readonly Expr Worried = Expr.Make(curve: -0.3f, brow: 0.7f, sweat: 0.35f, width: 0.7f);
        public static readonly Expr Sleepy = Expr.Make(open: 0.22f, curve: 0.35f, blush: 0.5f);
        public static readonly Expr Determined = Expr.Make(open: 0.85f, curve: 0.3f, brow: -0.6f, blush: 0.4f);
        public static readonly Expr AhChoo = Expr.Make(open: 0.35f, squeeze: 0.3f, curve: -0.2f, mouthOpen: 0.7f, round: 0.4f, brow: -0.4f, blush: 0.9f);

        static readonly (Vector3 pos, float r, int mesh)[] Layout =
        {
            (new Vector3(0.00f, 0.10f, 0.00f), 0.62f, 0),
            (new Vector3(-0.58f, -0.04f, 0.04f), 0.46f, 1),
            (new Vector3(0.60f, -0.02f, 0.02f), 0.47f, 2),
            (new Vector3(0.14f, 0.40f, 0.22f), 0.42f, 1),
            (new Vector3(-0.34f, 0.27f, 0.30f), 0.38f, 2),
            (new Vector3(0.42f, 0.24f, 0.30f), 0.34f, 0),
            (new Vector3(-0.28f, -0.20f, -0.16f), 0.38f, 2),
            (new Vector3(0.31f, -0.22f, -0.14f), 0.37f, 1),
            (new Vector3(-0.96f, -0.14f, 0.02f), 0.28f, 0),
            (new Vector3(0.98f, -0.12f, 0.06f), 0.27f, 2),
        };

        Cloud cloud;
        Transform root, stretch, body, face;
        Transform[] puffs;
        Spring3[] puffSprings;
        float[] puffPhase;
        MaterialPropertyBlock mpb;
        Material puffMat, faceMat;
        MeshRenderer faceRenderer;
        Expr cur = Idle;
        Expr emote;
        float emoteTimer, emoteDur;
        float blinkTimer = 2f, blinkT = -1f;
        Vector2 look, lookTarget;
        float lookWander;
        Spring puffScale;
        Spring bankX, bankZ;
        ParticleSystem vapor;
        Transform reticle;
        Transform curtain;
        Material curtainMat;
        Material reticleMat;
        MeshRenderer aimFan;
        Material aimMat;
        public Vector3? LookPoint;
        public float LookPointTimer;
        public Expr? Override;
        float flash;

        public void Init(Cloud c)
        {
            cloud = c;
            mpb = new MaterialPropertyBlock();
            root = new GameObject("Visual").transform;
            root.SetParent(transform, false);
            stretch = new GameObject("Stretch").transform;
            stretch.SetParent(root, false);
            body = new GameObject("Body").transform;
            body.SetParent(stretch, false);

            puffMat = Res.New("PW_CloudPuff");
            var meshes = new Mesh[3];
            for (int i = 0; i < 3; i++)
            {
                var prefab = Res.ModelPrefab("cloud_puff_" + "abc"[i]);
                meshes[i] = prefab != null ? prefab.GetComponentInChildren<MeshFilter>().sharedMesh : null;
            }
            puffs = new Transform[Layout.Length];
            puffSprings = new Spring3[Layout.Length];
            puffPhase = new float[Layout.Length];
            for (int i = 0; i < Layout.Length; i++)
            {
                var go = new GameObject("Puff" + i);
                go.layer = Layers.Cloud;
                go.transform.SetParent(body, false);
                go.transform.localPosition = Layout[i].pos;
                go.transform.localScale = Vector3.one * Layout[i].r;
                go.AddComponent<MeshFilter>().sharedMesh = meshes[Layout[i].mesh];
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = puffMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                puffs[i] = go.transform;
                puffPhase[i] = i * 1.37f;
            }

            faceMat = Res.New("PW_CloudFace");
            var fgo = new GameObject("Face");
            fgo.layer = Layers.Cloud;
            fgo.transform.SetParent(transform, false);
            fgo.AddComponent<MeshFilter>().sharedMesh = Res.Quad;
            faceRenderer = fgo.AddComponent<MeshRenderer>();
            faceRenderer.sharedMaterial = faceMat;
            faceRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            face = fgo.transform;

            vapor = Fx.MakeVapor(transform);
            reticle = Fx.MakeGroundDecal("Reticle", 1, out reticleMat);
            reticle.SetParent(transform.parent, false);
            curtain = Fx.MakeRainCurtain(out curtainMat);
            curtain.SetParent(transform.parent, false);
            aimFan = Fx.MakeFan("AimFan", Cloud.GustRange, Cloud.GustHalfAngle, out aimMat);
            aimFan.transform.SetParent(transform.parent, false);
            aimFan.enabled = false;

            puffScale.value = 1;
            for (int i = 0; i < puffs.Length; i++) puffSprings[i].value = puffs[i].position;
            c.OnGust += (d, strong) => { puffScale.velocity -= strong ? 9f : 3f; };
            c.OnFull += () => Emote(Full, 1.1f);
            c.OnEmptyTry += () => Emote(Empty, 1.0f);
        }

        public void Emote(Expr e, float seconds)
        {
            emote = e;
            emoteTimer = seconds;
            emoteDur = seconds;
        }

        public void Hop(float strength = 1f) { puffScale.velocity += 6f * strength; }

        public void Flash(float amount = 1f) { flash = Mathf.Max(flash, amount); }

        public void LookAtPoint(Vector3 p, float seconds = 1.2f)
        {
            LookPoint = p;
            LookPointTimer = seconds;
        }

        void LateUpdate()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0) return;
            float t = Time.time;
            var cam = Camera.main;
            float r = cloud.Radius;
            Vector3 vel = cloud.Velocity;
            float speed = vel.magnitude;

            // ---- bank & stretch
            Vector3 dir = speed > 0.05f ? vel / speed : cloud.Facing;
            float bank = Mathf.Clamp(speed * 2.4f, 0, 15f);
            bankX.Step(dir.z * bank, 10f, 0.5f, dt);
            bankZ.Step(-dir.x * bank, 10f, 0.5f, dt);
            root.localRotation = Quaternion.Euler(bankX.value, 0, bankZ.value);

            float gp = cloud.GustPhase;
            float inhale = gp >= 0 && gp < 0.14f ? Ease.OutCubic(gp / 0.14f) : 0;
            puffScale.Step(1f + inhale * 0.16f, 11f, 0.32f, dt);
            float s = r * puffScale.value;
            float st = 1f + Mathf.Min(speed * 0.022f, 0.2f);
            float sq = 1f / Mathf.Sqrt(st);
            // rain squeeze pulses / drinking gulps
            float rainPulse = cloud.RainIntensity * (0.5f + 0.5f * Mathf.Sin(t * 15f));
            float gulp = cloud.Drinking ? (0.5f + 0.5f * Mathf.Sin(t * 10f)) : 0;
            float sy = 1f - rainPulse * 0.06f + gulp * 0.035f;
            float sxz = 1f + rainPulse * 0.03f + gulp * 0.03f;
            root.localScale = new Vector3(s * sxz, s * sy, s * sxz);
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            stretch.localRotation = Quaternion.Euler(0, yaw, 0);
            stretch.localScale = new Vector3(sq, sq, st);
            body.localRotation = Quaternion.Euler(0, -yaw, 0);

            // ---- puffs: jelly lag + breathing
            float fill = cloud.Fill;
            for (int i = 0; i < puffs.Length; i++)
            {
                var desired = body.TransformPoint(Layout[i].pos);
                puffSprings[i].Step(desired, 15f + i * 0.6f, 0.4f, dt);
                puffs[i].position = puffSprings[i].value;
                float breathe = 1f + 0.035f * Mathf.Sin(t * 1.7f + puffPhase[i]) + 0.02f * Mathf.Sin(t * 3.1f + puffPhase[i] * 2f);
                puffs[i].localScale = Vector3.one * Layout[i].r * breathe;
            }
            flash = Mathf.MoveTowards(flash, 0, dt * 3f);
            puffMat.SetFloat("_Fill", fill);
            puffMat.SetFloat("_Flash", flash);
            puffMat.SetFloat("_Wobble", 0.05f + cloud.RainIntensity * 0.03f + (cloud.Drinking ? 0.03f : 0));

            // ---- face placement: on the camera-facing side, looking slightly ahead
            if (cam != null)
            {
                Vector3 toCam = (cam.transform.position - transform.position).normalized;
                var center = root.TransformPoint(new Vector3(0, 0.02f, 0));
                face.position = center + toCam * (r * puffScale.value * 0.8f) + Vector3.down * r * 0.04f + vel * 0.012f;
                face.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
                float fs = r * 1.18f * Mathf.Lerp(1f, puffScale.value, 0.6f);
                face.localScale = new Vector3(fs * sxz, fs * sy, fs);
            }

            // ---- expression
            Expr target = ChooseExpression();
            float rate = 14f;
            cur = Expr.Lerp(cur, target, 1 - Mathf.Exp(-rate * dt));
            // blink
            blinkTimer -= dt;
            if (blinkTimer <= 0 && blinkT < 0)
            {
                blinkT = 0;
                blinkTimer = Random.Range(1.8f, 4.5f);
            }
            float blink = 1f;
            if (blinkT >= 0)
            {
                blinkT += dt;
                blink = 1f - Mathf.Sin(Mathf.Clamp01(blinkT / 0.13f) * Mathf.PI);
                if (blinkT > 0.13f) blinkT = -1f;
            }
            // look
            lookWander -= dt;
            if (LookPointTimer > 0)
            {
                LookPointTimer -= dt;
                if (LookPoint.HasValue && cam != null) lookTarget = ScreenLook(cam, LookPoint.Value);
            }
            else if (speed > 0.6f && cam != null)
            {
                lookTarget = ScreenLook(cam, transform.position + vel);
            }
            else if (cloud.Raining && cam != null)
            {
                lookTarget = new Vector2(0, -0.9f);
            }
            else if (lookWander <= 0)
            {
                lookWander = Random.Range(1.2f, 3.5f);
                lookTarget = Random.insideUnitCircle * 0.6f;
            }
            look = Vector2.Lerp(look, lookTarget, 1 - Mathf.Exp(-8f * dt));

            faceMat.SetFloat("_EyeOpen", cur.open * (cur.happy > 0.5f || cur.squeeze > 0.5f ? 1 : blink));
            faceMat.SetFloat("_EyeHappy", cur.happy);
            faceMat.SetFloat("_EyeSqueeze", cur.squeeze);
            faceMat.SetFloat("_EyeWide", cur.wide);
            faceMat.SetVector("_Look", new Vector4(look.x, look.y, 0, 0));
            faceMat.SetFloat("_MouthCurve", cur.curve);
            faceMat.SetFloat("_MouthOpen", cur.mouthOpen + (cloud.Drinking ? 0.12f * Mathf.Sin(t * 10f) : 0));
            faceMat.SetFloat("_MouthWidth", cur.width);
            faceMat.SetFloat("_MouthRound", cur.round);
            faceMat.SetFloat("_MouthWobble", cur.wobble);
            faceMat.SetFloat("_Blush", cur.blush);
            faceMat.SetFloat("_Sweat", cur.sweat);
            faceMat.SetFloat("_Sparkle", cur.sparkle * (0.75f + 0.25f * Mathf.Sin(t * 12f)));
            faceMat.SetFloat("_Brow", cur.brow);

            // ---- vapour column while drinking
            var em = vapor.emission;
            em.rateOverTime = cloud.Drinking ? 55f : 0f;
            if (cloud.DrinkingFrom != null)
            {
                var vp = vapor.transform;
                vp.position = new Vector3(transform.position.x, cloud.DrinkingFrom.Level + 0.05f, transform.position.z);
                var shape = vapor.shape;
                shape.radius = r * 0.75f;
                var main = vapor.main;
                main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 3.0f);
            }

            // ---- rain curtain under the cloud
            if (curtain != null)
            {
                float ri = cloud.RainIntensity;
                curtain.gameObject.SetActive(ri > 0.01f);
                if (ri > 0.01f)
                {
                    var gp2 = cloud.GroundPoint;
                    float top = transform.position.y - r * 0.25f;
                    float h = Mathf.Max(0.2f, top - gp2.y);
                    curtain.position = new Vector3(transform.position.x, gp2.y, transform.position.z);
                    curtain.localScale = new Vector3(r * 1.35f, h, r * 1.35f);
                    curtainMat.SetColor("_Color", new Color(0.85f, 0.94f, 1f, 0.55f * ri));
                }
            }

            // ---- reticle & aim fan
            if (reticle != null)
            {
                var gpnt = cloud.GroundPoint;
                reticle.position = gpnt + Vector3.up * 0.04f;
                float rr = cloud.Raining ? r * 0.72f * 2f : 0.45f;
                reticle.localScale = Vector3.Lerp(reticle.localScale, new Vector3(rr, 1, rr), 1 - Mathf.Exp(-12f * dt));
                reticleMat.SetColor("_Color", new Color(1, 1, 1, cloud.Raining ? 0.12f : 0.2f));
            }
            bool aiming = cloud.Input.Aiming;
            aimFan.enabled = aiming;
            if (aiming)
            {
                var ad = cloud.Input.AimDir;
                ad.y = 0;
                if (ad.sqrMagnitude < 0.01f) ad = cloud.Facing;
                aimFan.transform.position = cloud.GroundPoint + Vector3.up * 0.06f;
                aimFan.transform.rotation = Quaternion.LookRotation(ad.normalized, Vector3.up);
                float pulse = 0.28f + 0.07f * Mathf.Sin(t * 8f);
                aimMat.SetColor("_Color", new Color(1, 1, 1, pulse));
                lookTarget = ScreenLook(cam, transform.position + ad);
            }
        }

        Vector2 ScreenLook(Camera cam, Vector3 worldPoint)
        {
            var d = worldPoint - transform.position;
            float x = Vector3.Dot(d, cam.transform.right);
            float y = Vector3.Dot(d, cam.transform.up);
            var v = new Vector2(x, y);
            if (v.magnitude > 1) v.Normalize();
            return v;
        }

        Expr ChooseExpression()
        {
            if (Override.HasValue) return Override.Value;
            float gp = cloud.GustPhase;
            if (gp >= 0)
            {
                if (!cloud.GustStrong) return Puff;
                return gp < 0.14f ? Inhale : Blow;
            }
            if (emoteTimer > 0)
            {
                emoteTimer -= Time.deltaTime;
                return emote;
            }
            if (cloud.Raining)
                return cloud.RainIntensity > 0.7f && cloud.Fill > 0.2f ? Expr.Lerp(RainLight, RainHard, 0.65f) : RainLight;
            if (cloud.Drinking) return Drinking;
            if (cloud.Fill < 0.02f) return Empty;
            if (cloud.Fill < 0.15f) return Low;
            if (cloud.Fill > 0.98f) return Expr.Lerp(Idle, Full, 0.5f);
            return Idle;
        }
    }
}
