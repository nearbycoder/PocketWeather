using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PocketWeather
{
    /// <summary>
    /// The tilt-shift miniature camera: narrow FOV, pitched down, distance solved so the whole
    /// diorama (and Pip's flight height) fits any aspect ratio. Adds a slow idle drift, screen
    /// shake, push-ins, and keeps depth of field focused on the diorama.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        public float Pitch = 52f;
        public float Fov = 24f;
        public float Zoom = 1f;          // < 1 pushes in
        public Vector3 FocusOffset;
        float distance = 25f;
        Vector3 look;
        float shake;
        float islandW = 14, islandD = 9;
        float zoomCurrent = 1f;
        public float FocusDistance { get; private set; }
        public float Yaw;

        public static CameraRig Create()
        {
            var go = new GameObject("CameraRig");
            var rig = go.AddComponent<CameraRig>();
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(go.transform, false);
            rig.Cam = camGo.AddComponent<Camera>();
            rig.Cam.clearFlags = CameraClearFlags.Skybox;
            rig.Cam.nearClipPlane = 0.5f;
            rig.Cam.farClipPlane = 200f;
            rig.Cam.allowHDR = true;
            rig.Cam.allowMSAA = true;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None;
            data.requiresDepthOption = CameraOverrideOption.On;
            data.requiresColorOption = CameraOverrideOption.On;
            camGo.AddComponent<AudioListener>();
            return rig;
        }

        public void Frame(float w, float d)
        {
            islandW = w;
            islandD = d;
            Solve();
        }

        public void Shake(float amount) { shake = Mathf.Max(shake, amount); }

        void Solve()
        {
            Cam.fieldOfView = Fov;
            float aspect = Mathf.Max(0.5f, Cam.aspect);
            look = new Vector3(0, 0.4f, 0.25f);
            // sample points: island top corners (kept clear of the HUD), island bottom, and Pip's
            // flight height near the edges (only has to stay on screen)
            var pts = new System.Collections.Generic.List<(Vector3 p, float top)>();
            float hw = islandW / 2f, hd = islandD / 2f;
            foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
            {
                pts.Add((new Vector3(sx * hw, 0.3f, sz * hd), 0.8f));
                pts.Add((new Vector3(sx * hw, -1.3f, sz * hd), 0.8f));
                pts.Add((new Vector3(sx * (hw - 0.6f), Cloud.Altitude + 0.7f, sz * (hd - 0.5f)), 0.97f));
            }
            var rot = Quaternion.Euler(Pitch, Yaw, 0);
            float lo = 5f, hi = 80f;
            for (int it = 0; it < 30; it++)
            {
                float mid = (lo + hi) / 2;
                if (Fits(pts, rot, mid, aspect)) hi = mid; else lo = mid;
            }
            distance = hi;
        }

        bool Fits(System.Collections.Generic.List<(Vector3 p, float top)> pts, Quaternion rot, float dist, float aspect)
        {
            var pos = look - rot * Vector3.forward * dist;
            var inv = Quaternion.Inverse(rot);
            float tanV = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
            float tanH = tanV * aspect;
            float mx = 0.97f, bottom = 0.92f;
            foreach (var (p, top) in pts)
            {
                var l = inv * (p - pos);
                if (l.z <= 0.1f) return false;
                if (Mathf.Abs(l.x / l.z) > tanH * mx) return false;
                float v = l.y / l.z;
                if (v > tanV * top || v < -tanV * bottom) return false;
            }
            return true;
        }

        float lastAspect;

        void LateUpdate()
        {
            if (Cam == null) return;
            if (Mathf.Abs(Cam.aspect - lastAspect) > 0.001f)
            {
                lastAspect = Cam.aspect;
                Solve();
            }
            float dt = Time.unscaledDeltaTime;
            zoomCurrent = Ease.Damp(zoomCurrent, Zoom, 2.5f, dt);
            float t = Time.unscaledTime;
            float driftYaw = Mathf.Sin(t * 0.11f) * 0.8f;
            float driftPitch = Mathf.Sin(t * 0.07f + 1f) * 0.4f;
            var rot = Quaternion.Euler(Pitch + driftPitch, Yaw + driftYaw, 0);
            var target = look + FocusOffset;
            var pos = target - rot * Vector3.forward * distance * zoomCurrent;
            shake = Mathf.MoveTowards(shake, 0, dt * 2.5f);
            if (shake > 0 && GameSettings.ScreenShake)
            {
                float s = shake * shake * 0.25f;
                pos += new Vector3(Mathf.PerlinNoise(t * 25f, 0) - 0.5f, Mathf.PerlinNoise(0, t * 25f) - 0.5f, 0) * s;
            }
            Cam.transform.SetPositionAndRotation(pos, rot);
            FocusDistance = Vector3.Distance(pos, target);
            PostFx.SetFocus(FocusDistance);
        }
    }
}
