using UnityEngine;

namespace PocketWeather
{
    /// <summary>
    /// Pooled one-shot particle effects (splashes, ripples, sparkles, petals, leaves, wind
    /// streaks, steam, hearts...) built from code with the procedural PW/Fx shader.
    /// </summary>
    public static class Fx
    {
        public enum Shape { Dot = 0, Ring = 1, Star = 2, Drop = 3, Streak = 4, Heart = 5, Leaf = 6, Puff = 7, Flat = 8 }

        static Transform root;
        static ParticleSystem splash, splashRing, ripple, sparkle, petals, leaves, wind, steam, hearts, mist, confetti, smoke, glints, bubbles;

        public static Material Mat(Shape shape, bool additive = false, float softness = 0.5f, bool alwaysOnTop = false)
        {
            var m = Res.New("PW_Fx");
            m.SetFloat("_Shape", (int)shape);
            m.SetFloat("_Softness", softness);
            m.SetFloat("_Additive", additive ? 1 : 0);
            if (alwaysOnTop) m.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
            m.enableInstancing = true;
            return m;
        }

        public static void Init()
        {
            if (root != null) return;
            root = new GameObject("Fx").transform;
            Object.DontDestroyOnLoad(root.gameObject);
            splash = Make("Splash", Shape.Dot, 0.2f, 600, gravity: 1.6f, sizeOverLife: Curve(1, 0.3f));
            splashRing = Make("SplashRing", Shape.Ring, 0.3f, 200, horizontal: true, sizeOverLife: Curve(0.3f, 1f), fade: true);
            ripple = Make("Ripple", Shape.Ring, 0.15f, 300, horizontal: true, sizeOverLife: Curve(0.15f, 1f), fade: true);
            sparkle = Make("Sparkle", Shape.Star, 0.5f, 300, additive: true, sizeOverLife: Curve(1f, 0f), drag: 2f);
            petals = Make("Petals", Shape.Leaf, 0.5f, 300, gravity: 0.35f, sizeOverLife: Curve(1f, 0.6f), drag: 1.5f, rotate: true, fade: true);
            leaves = Make("Leaves", Shape.Leaf, 0.5f, 300, gravity: 0.15f, sizeOverLife: Curve(1f, 0.7f), drag: 1.2f, rotate: true, fade: true, noise: 1.2f);
            steam = Make("Steam", Shape.Puff, 0.6f, 300, gravity: -0.15f, sizeOverLife: Curve(0.5f, 1.4f), drag: 1f, fade: true, noise: 0.4f);
            smoke = Make("Smoke", Shape.Puff, 0.7f, 400, gravity: -0.08f, sizeOverLife: Curve(0.4f, 1.8f), drag: 0.6f, fade: true, noise: 0.5f);
            hearts = Make("Hearts", Shape.Heart, 0.5f, 100, gravity: -0.4f, sizeOverLife: Curve(0.4f, 1f, 0.8f), drag: 2f, fade: true);
            mist = Make("Mist", Shape.Star, 0.5f, 400, additive: true, sizeOverLife: Curve(0f, 1f, 0f), drag: 3f, noise: 0.25f);
            confetti = Make("Confetti", Shape.Flat, 0.5f, 600, gravity: 0.5f, drag: 1.2f, rotate: true, noise: 0.8f, sizeOverLife: Curve(1f, 0.8f));
            glints = Make("Glints", Shape.Star, 0.5f, 200, additive: true, sizeOverLife: Curve(0f, 1f, 0f));
            bubbles = Make("Bubbles", Shape.Ring, 0.4f, 200, gravity: -0.3f, sizeOverLife: Curve(0.6f, 1f), fade: true);
            wind = Make("Wind", Shape.Dot, 0.5f, 200, drag: 0.4f, noise: 0.9f, sizeOverLife: Curve(1f, 1f));
            var trails = wind.trails;
            trails.enabled = true;
            trails.ratio = 1f;
            trails.lifetime = new ParticleSystem.MinMaxCurve(0.55f);
            trails.minVertexDistance = 0.05f;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, Curve(1f, 0f));
            trails.dieWithParticles = true;
            trails.inheritParticleColor = true;
            trails.colorOverTrail = new ParticleSystem.MinMaxGradient(Gradient2(new Color(1, 1, 1, 0.9f), new Color(1, 1, 1, 0f)));
            var wr = wind.GetComponent<ParticleSystemRenderer>();
            wr.trailMaterial = Mat(Shape.Flat);
            wr.material = Mat(Shape.Dot);
        }

        static AnimationCurve Curve(params float[] keys)
        {
            var c = new AnimationCurve();
            for (int i = 0; i < keys.Length; i++) c.AddKey(keys.Length == 1 ? 0 : i / (float)(keys.Length - 1), keys[i]);
            return c;
        }

        static Gradient Gradient2(Color a, Color b)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0), new GradientColorKey(b, 1) },
                      new[] { new GradientAlphaKey(a.a, 0), new GradientAlphaKey(b.a, 1) });
            return g;
        }

        static ParticleSystem Make(string name, Shape shape, float softness, int max, float gravity = 0, bool additive = false,
            bool horizontal = false, AnimationCurve sizeOverLife = null, bool fade = true, float drag = 0, bool rotate = false, float noise = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.layer = Layers.Fx;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.gravityModifier = gravity;
            main.startLifetime = 1;
            main.startSpeed = 0;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            var em = ps.emission;
            em.rateOverTime = 0;
            var sh = ps.shape;
            sh.enabled = false;
            if (sizeOverLife != null)
            {
                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, sizeOverLife);
            }
            if (fade)
            {
                var col = ps.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                          new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.6f), new GradientAlphaKey(0, 1) });
                col.color = new ParticleSystem.MinMaxGradient(g);
            }
            if (drag > 0)
            {
                var lim = ps.limitVelocityOverLifetime;
                lim.enabled = true;
                lim.drag = drag;
                lim.multiplyDragByParticleSize = false;
                lim.multiplyDragByParticleVelocity = true;
                lim.limit = 100;
            }
            if (rotate)
            {
                var rol = ps.rotationOverLifetime;
                rol.enabled = true;
                rol.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
            }
            if (noise > 0)
            {
                var n = ps.noise;
                n.enabled = true;
                n.strength = noise;
                n.frequency = 0.8f;
                n.scrollSpeed = 0.5f;
                n.quality = ParticleSystemNoiseQuality.Medium;
            }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = Mat(shape, additive, softness);
            r.renderMode = horizontal ? ParticleSystemRenderMode.HorizontalBillboard : ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortMode = ParticleSystemSortMode.Distance;
            ps.Play();
            return ps;
        }

        static void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, float size, float life, Color color, float rot = 0)
        {
            var p = new ParticleSystem.EmitParams
            {
                position = pos,
                velocity = vel,
                startSize = size,
                startLifetime = life,
                startColor = color,
                rotation = rot,
                applyShapeToPosition = false,
            };
            ps.Emit(p, 1);
        }

        // ------------------------------------------------------------------ effects
        public static void Splash(Vector3 pos, Surface surface, float scale = 1f)
        {
            if (splash == null) return;
            Color c = surface == Surface.Water ? new Color(0.85f, 0.95f, 1f, 0.85f) : new Color(0.75f, 0.88f, 1f, 0.8f);
            int n = surface == Surface.Leaf ? 2 : 3;
            for (int i = 0; i < n; i++)
            {
                var v = new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(0.9f, 1.6f), Random.Range(-0.6f, 0.6f)) * scale;
                Emit(splash, pos + Vector3.up * 0.02f, v, Random.Range(0.025f, 0.045f) * scale, Random.Range(0.25f, 0.4f), c);
            }
            if (surface == Surface.Water)
                Emit(ripple, pos + Vector3.up * 0.01f, Vector3.zero, Random.Range(0.35f, 0.55f) * scale, 0.7f, new Color(1, 1, 1, 0.55f));
            else if (surface != Surface.Leaf && surface != Surface.Creature)
                Emit(splashRing, pos + Vector3.up * 0.015f, Vector3.zero, Random.Range(0.14f, 0.22f) * scale, 0.28f, new Color(0.85f, 0.93f, 1f, 0.45f));
        }

        public static void Ripple(Vector3 pos, float size = 0.6f, float alpha = 0.5f)
        {
            if (ripple != null) Emit(ripple, pos, Vector3.zero, size, 0.9f, new Color(1, 1, 1, alpha));
        }

        public static void Sparkles(Vector3 pos, int count, Color color, float spread = 0.4f, float speed = 1.5f, float size = 0.16f)
        {
            if (sparkle == null) return;
            for (int i = 0; i < count; i++)
            {
                var d = Random.insideUnitSphere;
                Emit(sparkle, pos + d * spread * 0.3f, d * speed + Vector3.up * 0.6f, size * Random.Range(0.6f, 1.3f), Random.Range(0.5f, 0.9f), color);
            }
        }

        public static void Petals(Vector3 pos, Color color, int count = 10, float speed = 1.6f)
        {
            if (petals == null) return;
            for (int i = 0; i < count; i++)
            {
                var d = Random.insideUnitSphere; d.y = Mathf.Abs(d.y) + 0.6f;
                var c = Color.Lerp(color, Color.white, Random.Range(0f, 0.25f));
                Emit(petals, pos, d * speed, Random.Range(0.07f, 0.12f), Random.Range(0.8f, 1.4f), c, Random.Range(0, 360f));
            }
        }

        public static void Leaves(Vector3 pos, Vector3 dir, int count = 6, Color? color = null)
        {
            if (leaves == null) return;
            var baseC = color ?? new Color(0.45f, 0.75f, 0.3f, 1);
            for (int i = 0; i < count; i++)
            {
                var v = dir * Random.Range(2f, 4f) + Random.insideUnitSphere * 0.8f + Vector3.up * Random.Range(0.3f, 1.2f);
                var c = Color.Lerp(baseC, new Color(0.85f, 0.75f, 0.3f, 1), Random.Range(0f, 0.35f));
                Emit(leaves, pos + Random.insideUnitSphere * 0.3f, v, Random.Range(0.08f, 0.13f), Random.Range(1.0f, 1.8f), c, Random.Range(0, 360f));
            }
        }

        public static void WindStreaks(Vector3 origin, Vector3 dir, float range, float halfAngle, int count = 22)
        {
            if (wind == null) return;
            for (int i = 0; i < count; i++)
            {
                float ang = Random.Range(-halfAngle, halfAngle) * 0.8f;
                var d = Quaternion.Euler(0, ang, 0) * dir;
                var start = origin + d * Random.Range(0.2f, 0.9f) + Vector3.up * Random.Range(0.25f, 1.1f) + Random.insideUnitSphere * 0.2f;
                float speed = Random.Range(8f, 12f);
                Emit(wind, start, d * speed, Random.Range(0.08f, 0.12f), range / speed * Random.Range(0.85f, 1.2f), new Color(1, 1, 1, 0.95f));
                if (i % 3 == 0) Emit(steam, start, d * speed * 0.55f, Random.Range(0.35f, 0.55f), 0.7f, new Color(1, 1, 1, 0.28f));
            }
        }

        public static void Steam(Vector3 pos, int count = 4, float spread = 0.25f, Color? color = null)
        {
            if (steam == null) return;
            var c = color ?? new Color(1, 1, 1, 0.55f);
            for (int i = 0; i < count; i++)
                Emit(steam, pos + Random.insideUnitSphere * spread, new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(0.4f, 0.9f), Random.Range(-0.15f, 0.15f)),
                     Random.Range(0.18f, 0.32f), Random.Range(1.0f, 1.6f), c);
        }

        public static void Smoke(Vector3 pos, Color color, float size = 0.3f)
        {
            if (smoke == null) return;
            Emit(smoke, pos + Random.insideUnitSphere * 0.05f, new Vector3(Random.Range(-0.1f, 0.1f), Random.Range(0.35f, 0.55f), Random.Range(-0.1f, 0.1f)),
                 size * Random.Range(0.8f, 1.2f), Random.Range(2.2f, 3.2f), color);
        }

        public static void Hearts(Vector3 pos, int count = 3)
        {
            if (hearts == null) return;
            for (int i = 0; i < count; i++)
                Emit(hearts, pos + Random.insideUnitSphere * 0.15f, new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.6f, 1.1f), 0), Random.Range(0.16f, 0.24f),
                     Random.Range(1.1f, 1.6f), new Color(1f, 0.42f, 0.55f, 1f));
        }

        public static void Mist(Vector3 pos, float radius)
        {
            if (mist == null) return;
            var p = pos + new Vector3(Random.Range(-radius, radius), Random.Range(0.2f, 1.4f), Random.Range(-radius, radius));
            Emit(mist, p, Vector3.up * 0.05f, Random.Range(0.05f, 0.1f), Random.Range(0.8f, 1.4f), new Color(0.9f, 0.95f, 1f, 0.7f));
        }

        public static void Glint(Vector3 pos, float size, Color color)
        {
            if (glints != null) Emit(glints, pos, Vector3.zero, size, 0.6f, color);
        }

        public static void Bubble(Vector3 pos)
        {
            if (bubbles != null) Emit(bubbles, pos, new Vector3(0, 0.3f, 0), Random.Range(0.05f, 0.1f), 0.8f, new Color(1, 1, 1, 0.7f));
        }

        public static void Confetti(Vector3 pos, int count = 60, float spread = 1.5f)
        {
            if (confetti == null) return;
            Color[] cols = { Res.Hex("FF6F61"), Res.Hex("FFD45C"), Res.Hex("6FC3E8"), Res.Hex("B79CFF"), Res.Hex("8FD16A"), Res.Hex("FF9EC4") };
            for (int i = 0; i < count; i++)
            {
                var v = new Vector3(Random.Range(-1f, 1f) * spread, Random.Range(2.5f, 4.5f), Random.Range(-1f, 1f) * spread);
                Emit(confetti, pos, v, Random.Range(0.05f, 0.09f), Random.Range(2f, 3.2f), cols[Random.Range(0, cols.Length)], Random.Range(0, 360f));
            }
        }

        // ------------------------------------------------------------------ persistent helpers
        public static ParticleSystem MakeVapor(Transform parent)
        {
            var go = new GameObject("Vapor");
            go.transform.SetParent(parent, false);
            go.layer = Layers.Fx;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 3.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.5f);
            main.startColor = new Color(1, 1, 1, 0.55f);
            main.maxParticles = 300;
            var em = ps.emission;
            em.rateOverTime = 0;
            var sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 0f;
            sh.radius = 0.8f;
            sh.rotation = new Vector3(-90, 0, 0);
            sh.radiusThickness = 1;
            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.Local;
            vol.orbitalY = new ParticleSystem.MinMaxCurve(2.4f);
            vol.radial = new ParticleSystem.MinMaxCurve(-0.7f);
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, Curve(0.6f, 1.2f, 0.4f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(0.85f, 0.95f, 1f), 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.8f, 0.2f), new GradientAlphaKey(0.6f, 0.7f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = Mat(Shape.Dot, false, 0.9f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        /// <summary>A flat ring decal on the ground (parent scales in XZ).</summary>
        public static Transform MakeGroundDecal(string name, float size, out Material mat, Shape shape = Shape.Ring)
        {
            var parent = new GameObject(name).transform;
            var q = new GameObject("Quad");
            q.layer = Layers.Fx;
            q.transform.SetParent(parent, false);
            q.transform.localRotation = Quaternion.Euler(90, 0, 0);
            q.transform.localScale = Vector3.one * size;
            q.AddComponent<MeshFilter>().sharedMesh = Res.Quad;
            var mr = q.AddComponent<MeshRenderer>();
            mat = Mat(shape, false, 0.4f);
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return parent;
        }

        /// <summary>An open cylinder (radius 0.5, height 1, base at y=0) with scrolling rain streaks.</summary>
        public static Transform MakeRainCurtain(out Material mat)
        {
            var go = new GameObject("RainCurtain");
            go.layer = Layers.Fx;
            var mesh = new Mesh { name = "Curtain" };
            int seg = 28;
            var verts = new Vector3[(seg + 1) * 2];
            var uvs = new Vector2[verts.Length];
            var cols = new Color[verts.Length];
            var tris = new int[seg * 6];
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.5f;
                verts[i * 2] = d;
                verts[i * 2 + 1] = d + Vector3.up;
                uvs[i * 2] = new Vector2(i / (float)seg, 0);
                uvs[i * 2 + 1] = new Vector2(i / (float)seg, 1);
                cols[i * 2] = cols[i * 2 + 1] = Color.white;
                if (i < seg)
                {
                    int k = i * 6, v = i * 2;
                    tris[k] = v; tris[k + 1] = v + 1; tris[k + 2] = v + 3;
                    tris[k + 3] = v; tris[k + 4] = v + 3; tris[k + 5] = v + 2;
                }
            }
            mesh.vertices = verts; mesh.uv = uvs; mesh.colors = cols; mesh.triangles = tris;
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mat = Mat((Shape)9);
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.SetActive(false);
            return go.transform;
        }

        /// <summary>A flat fan (circular sector) along +Z for the gust aim preview.</summary>
        public static MeshRenderer MakeFan(string name, float radius, float halfAngle, out Material mat)
        {
            var go = new GameObject(name);
            go.layer = Layers.Fx;
            var mesh = new Mesh { name = "Fan" };
            int seg = 24;
            var verts = new Vector3[seg + 2];
            var cols = new Color[seg + 2];
            var uvs = new Vector2[seg + 2];
            verts[0] = Vector3.zero;
            cols[0] = new Color(1, 1, 1, 0.9f);
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.Lerp(-halfAngle, halfAngle, i / (float)seg) * Mathf.Deg2Rad;
                verts[i + 1] = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * radius;
                cols[i + 1] = new Color(1, 1, 1, 0f);
                uvs[i + 1] = new Vector2(0.5f, 0.5f);
            }
            uvs[0] = new Vector2(0.5f, 0.5f);
            var tris = new int[seg * 3];
            for (int i = 0; i < seg; i++) { tris[i * 3] = 0; tris[i * 3 + 1] = i + 1; tris[i * 3 + 2] = i + 2; }
            mesh.vertices = verts; mesh.colors = cols; mesh.uv = uvs; mesh.triangles = tris;
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mat = Mat(Shape.Flat);
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return mr;
        }
    }
}
