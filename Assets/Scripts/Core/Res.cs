using System.Collections.Generic;
using UnityEngine;

namespace PocketWeather
{
    /// <summary>Physics layers (named in ProjectSetup).</summary>
    public static class Layers
    {
        public const int Terrain = 6, Water = 7, Receiver = 8, Props = 9, Cloud = 10, Fx = 11;
        public static readonly int RainMask = (1 << Terrain) | (1 << Water) | (1 << Receiver) | (1 << Props);
        public static readonly int GroundMask = (1 << Terrain);
    }

    /// <summary>Loads and caches shared materials, models and other Resources.</summary>
    public static class Res
    {
        static readonly Dictionary<string, Material> templates = new();
        static readonly Dictionary<string, GameObject> models = new();
        static readonly Dictionary<string, Material> toonByColor = new();

        /// <summary>The material template saved by ProjectSetup in Resources/Materials (keeps the shader in builds).</summary>
        public static Material Template(string name)
        {
            if (templates.TryGetValue(name, out var m)) return m;
            m = Resources.Load<Material>("Materials/" + name);
            if (m == null) Debug.LogError("[PW] missing material template " + name);
            templates[name] = m;
            return m;
        }

        public static Material New(string template) => new Material(Template(template));

        static Material toon, toonSway, toonSwaySmall;
        public static Material ToonSwaySmall
        {
            get
            {
                if (toonSwaySmall != null) return toonSwaySmall;
                toonSwaySmall = New("PW_Toon");
                toonSwaySmall.SetFloat("_Sway", 1.6f);
                toonSwaySmall.SetFloat("_SwayHeight", 0.18f);
                return toonSwaySmall;
            }
        }
        public static Material Toon => toon ??= New("PW_Toon");
        public static Material ToonSway
        {
            get
            {
                if (toonSway != null) return toonSway;
                toonSway = New("PW_Toon");
                toonSway.SetFloat("_Sway", 1f);
                toonSway.SetFloat("_SwayHeight", 1.2f);
                return toonSway;
            }
        }

        public static GameObject ModelPrefab(string name)
        {
            if (models.TryGetValue(name, out var go)) return go;
            go = Resources.Load<GameObject>("Models/" + name);
            if (go == null) Debug.LogWarning("[PW] missing model " + name);
            models[name] = go;
            return go;
        }

        /// <summary>Instantiates a Blender model and gives every part the right shared material.</summary>
        public static GameObject Spawn(string name, Transform parent, Vector3 pos, float yaw = 0, float scale = 1)
        {
            var prefab = ModelPrefab(name);
            GameObject go;
            if (prefab == null)
            {
                go = new GameObject(name + " (missing)");
            }
            else
            {
                go = Object.Instantiate(prefab);
                go.name = name;
                ApplyMaterials(go);
            }
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        public static void ApplyMaterials(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = r.gameObject.name.ToLowerInvariant();
                bool small = n.Contains("grass") || n.Contains("wildflower") || n.Contains("reed") || n.Contains("clover");
                bool sway = n.Contains("canopy") || n.Contains("leaves") || n.Contains("wheat");
                r.sharedMaterial = small ? ToonSwaySmall : sway ? ToonSway : Toon;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
        }

        /// <summary>World-space bounds of all renderers under a transform.</summary>
        public static Bounds RenderBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        public static Color Hex(string hex, float a = 1f)
        {
            if (!hex.StartsWith("#")) hex = "#" + hex;
            ColorUtility.TryParseHtmlString(hex, out var c);
            c.a = a;
            return c;
        }

        static Mesh quad;
        /// <summary>Unit quad in XY facing -Z (towards a default camera), uv 0..1.</summary>
        public static Mesh Quad
        {
            get
            {
                if (quad != null) return quad;
                quad = new Mesh { name = "PWQuad" };
                quad.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(0.5f, 0.5f), new Vector3(-0.5f, 0.5f) };
                quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                quad.RecalculateNormals();
                quad.RecalculateBounds();
                return quad;
            }
        }
    }
}
