using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PocketWeather
{
    /// <summary>
    /// The tilt-shift miniature camera: narrow FOV, pitched down, distance solved so the whole
    /// diorama (and Pip's flight height) fits any aspect ratio. Adds a slow idle drift, screen
    /// shake, push-ins, and keeps depth of field focused on the diorama. Held upright (portrait),
    /// fitting the whole width would leave the island a thin strip, so the camera frames part of
    /// it and slides sideways to follow Pip while a day is being played.
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
        /// <summary>Set by GameFlow while a day is played: the view follows Pip side to side
        /// (portrait only; in landscape the whole island is framed and there's nothing to pan).</summary>
        public bool FollowPip;
        float pan;              // world-x offset of the view
        float panMax;           // how far it can slide either way before showing past the island's edge
        float viewHalfW = 7f;   // half the island width that's framed
        public float Pan => pan;
        public float PanMax => panMax;
        public float ViewHalfWidth => viewHalfW;
        public float Distance => distance;
        /// <summary>The distance the camera used before portrait framing (whole width, aspect
        /// floored at 0.5), for the UI audit's before/after comparison.</summary>
        public float LegacyDistance { get; private set; }

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
            camGo.AddComponent<MasterLimiter>();
            return rig;
        }

        public void Frame(float w, float d)
        {
            islandW = w;
            islandD = d;
            Solve();
        }

        public void Shake(float amount) { shake = Mathf.Max(shake, amount); }

        /// <summary>Jumps to <see cref="Zoom"/> instead of easing there (for cuts).</summary>
        public void SnapZoom() { zoomCurrent = Zoom; }

        /// <summary>The share of the island's width framed at this aspect: all of it in landscape
        /// and down to a 4:5 window, then less as the screen narrows (57% on a 20:9 phone held
        /// upright, 70% at 9:16), never under half.</summary>
        public static float WidthFraction(float aspect) => aspect >= 0.8f ? 1f : Mathf.Clamp(aspect / 0.8f, 0.5f, 1f);

        void Solve()
        {
            Cam.fieldOfView = Fov;
            look = new Vector3(0, 0.4f, 0.25f);
            float hw = islandW / 2f;
            LegacyDistance = SolveDistance(hw, Mathf.Max(0.5f, Cam.aspect));
            if (Cam.aspect >= 1f)
            {
                // landscape: exactly the framing the game has always had
                viewHalfW = hw;
                distance = LegacyDistance;
            }
            else
            {
                viewHalfW = hw * WidthFraction(Cam.aspect);
                distance = SolveDistance(viewHalfW, Cam.aspect);
            }
            panMax = Mathf.Max(0f, hw - viewHalfW);
            pan = Mathf.Clamp(pan, -panMax, panMax);
        }

        /// <summary>The closest distance at which ±hw of the island's width, its full depth and
        /// Pip's flight height all fit the screen.</summary>
        float SolveDistance(float hw, float aspect)
        {
            // sample points: island top corners (kept clear of the HUD), island bottom, and Pip's
            // flight height near the edges (only has to stay on screen)
            var pts = new System.Collections.Generic.List<(Vector3 p, float top)>();
            float hd = islandD / 2f;
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
            return hi;
        }

        /// <summary>Moves the view straight to where it's heading (after a teleport or a cut).</summary>
        public void SnapPan()
        {
            pan = Mathf.Clamp(PanGoal(), -panMax, panMax);
        }

        float PanGoal()
        {
            var cloud = Cloud.Instance;
            if (!FollowPip || cloud == null || panMax < 0.01f) return 0f;
            // a dead zone in the middle third: small moves don't slide the world under the finger
            float x = cloud.transform.position.x, dz = viewHalfW * 0.3f;
            if (x > pan + dz) return x - dz;
            if (x < pan - dz) return x + dz;
            return pan;
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
            float dt = Clock.UnscaledDelta;
            zoomCurrent = Ease.Damp(zoomCurrent, Zoom, 2.5f, dt);
            if (panMax > 0f || pan != 0f)
            {
                pan = Mathf.Clamp(Ease.Damp(pan, Mathf.Clamp(PanGoal(), -panMax, panMax), 4f, dt), -panMax, panMax);
                // however fast Pip flies, it never leaves the frame
                var cloud = Cloud.Instance;
                if (FollowPip && cloud != null)
                {
                    float lead = viewHalfW * 0.75f, x = cloud.transform.position.x;
                    pan = Mathf.Clamp(Mathf.Clamp(pan, x - lead, x + lead), -panMax, panMax);
                }
            }
            float t = Clock.UnscaledTime;
            float driftYaw = Mathf.Sin(t * 0.11f) * 0.8f;
            float driftPitch = Mathf.Sin(t * 0.07f + 1f) * 0.4f;
            var rot = Quaternion.Euler(Pitch + driftPitch, Yaw + driftYaw, 0);
            var target = look + FocusOffset + new Vector3(pan, 0, 0);
            var pos = target - rot * Vector3.forward * distance * zoomCurrent;
            shake = Mathf.MoveTowards(shake, 0, dt * 2.5f);
            if (shake > 0 && GameSettings.ScreenShake)
            {
                float s = shake * shake * 0.25f;
                pos += new Vector3(Mathf.PerlinNoise(t * 25f, 0) - 0.5f, Mathf.PerlinNoise(0, t * 25f) - 0.5f, 0) * s;
            }
            Cam.transform.SetPositionAndRotation(pos, rot);
            // focus a little above the ground, between the island and Pip's flying height
            FocusDistance = Vector3.Distance(pos, target) - Cloud.Altitude * 0.4f * Mathf.Sin(Pitch * Mathf.Deg2Rad);
            PostFx.SetFocus(FocusDistance, zoomCurrent);
        }
    }
}
