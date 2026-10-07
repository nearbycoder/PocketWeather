using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PocketWeather
{
    /// <summary>
    /// Turns mouse, touch, keyboard and gamepad into three intents: where the cloud should go
    /// (a point on the ground), whether it's raining, and gust requests. The AutoPilot and
    /// capture tools drive the same intents through <see cref="Virtual"/>.
    ///
    /// Touch: drag to move · hold still a moment to start raining (then drag to keep raining) ·
    /// flick to gust. Mouse: the cloud follows the cursor · hold left to rain · right-drag to aim
    /// a gust and release. Keys: WASD/arrows, Space rain, E gust. Pad: stick, A rain, X gust.
    /// </summary>
    public class CloudInput : MonoBehaviour
    {
        public enum Device { None, Mouse, Touch, Keys, Pad, Virtual }

        public Vector3 Target { get; private set; }
        public bool RainHeld { get; private set; }
        public bool HasGust { get; private set; }
        public Vector3 GustDir { get; private set; }
        public bool Aiming { get; private set; }
        public Vector3 AimDir { get; private set; }
        public Device LastDevice { get; private set; } = Device.None;
        public float HoldCharge { get; private set; }     // touch hold-to-rain ring 0..1
        public Vector2 HoldScreenPos { get; private set; }
        public bool Enabled = true;

        public bool VirtualMode;
        public Vector3 VirtualTarget;
        public bool VirtualRain;

        Cloud cloud;
        Camera cam;
        Vector2 lastMousePos;
        bool mouseSeeded;
        bool mouseRainArmed;
        Vector3 aimAnchor;
        // touch state
        int touchId = -1;
        float touchStart;
        Vector2 touchStartPos, touchLastPos;
        Vector3 touchStartTarget;
        float stillTimer;
        bool touchRaining;
        bool rawRainBefore, rainToggled;   // toggle-rain mode (accessibility setting)
        Vector2 touchVel;
        bool touchOverUi;
        Vector3 keyTarget;
        float padGustCooldown;

        // on-screen buttons (TouchButtons UI sets these)
        public bool ButtonRain;
        public bool ButtonGust;

        public void Init(Cloud c)
        {
            cloud = c;
            Target = c.transform.position;
            keyTarget = Target;
            if (Mouse.current != null) lastMousePos = Mouse.current.position.ReadValue();
            mouseSeeded = true;
        }

        public void Virtual(Vector3 target, bool rain)
        {
            VirtualMode = true;
            VirtualTarget = target;
            VirtualRain = rain;
        }

        /// <summary>Re-aims every input source at p (after the cloud is moved directly).</summary>
        public void ResetTarget(Vector3 p)
        {
            Target = keyTarget = VirtualTarget = p;
        }

        public void VirtualGust(Vector3 dir)
        {
            VirtualMode = true;
            HasGust = true;
            GustDir = new Vector3(dir.x, 0, dir.z).normalized;
        }

        public void ConsumeGust() { HasGust = false; }

        public void Tick(float dt)
        {
            cam = Camera.main;
            if (cam == null) return;
            if (VirtualMode)
            {
                LastDevice = Device.Virtual;
                Target = VirtualTarget;
                RainHeld = VirtualRain;
                Aiming = false;
                return;
            }
            if (!Enabled)
            {
                // menus and postcards: the pointer moving here isn't Pip being steered by a mouse
                // (a phone's tap on the page outside the game also moves it), so it mustn't count
                // as mouse movement when play starts
                if (Mouse.current != null) lastMousePos = Mouse.current.position.ReadValue();
                RainHeld = false;
                rainToggled = rawRainBefore = false;
                Aiming = false;
                touchId = -1;
                return;
            }

            bool rain = false;
            TickTouch(dt, ref rain);
            if (touchId < 0)
            {
                TickMouse(ref rain);
                TickKeys(dt, ref rain);
                TickPad(dt, ref rain);
            }
            if (ButtonRain) rain = true;
            if (ButtonGust)
            {
                ButtonGust = false;
                RequestGust(cloud.Facing);
            }
            if (GameSettings.RainToggle)
            {
                // each fresh press flips the rain; running dry switches it off so Pip can drink again
                if (rain && !rawRainBefore) rainToggled = !rainToggled;
                if (cloud.Water <= 0.5f) rainToggled = false;
                rawRainBefore = rain;
                rain = rainToggled;
            }
            else rainToggled = rawRainBefore = false;
            RainHeld = rain;
        }

        void RequestGust(Vector3 dir)
        {
            dir.y = 0;
            if (dir.sqrMagnitude < 1e-4) dir = cloud.Facing;
            HasGust = true;
            GustDir = dir.normalized;
        }

        // ------------------------------------------------------------------ mouse
        void TickMouse(ref bool rain)
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 pos = mouse.position.ReadValue();
            bool moved = (pos - lastMousePos).sqrMagnitude > 4f;
            lastMousePos = pos;
            bool any = mouse.leftButton.isPressed || mouse.rightButton.isPressed;
            if (moved || any) LastDevice = Device.Mouse;
            if (LastDevice != Device.Mouse) return;
            bool overUi = PointerOverUi(pos, -1);

            if (mouse.leftButton.wasPressedThisFrame) mouseRainArmed = !overUi;
            if (!mouse.leftButton.isPressed) mouseRainArmed = false;

            if (mouse.rightButton.wasPressedThisFrame && !overUi)
            {
                Aiming = true;
                aimAnchor = cloud.GroundPoint;
            }
            if (Aiming)
            {
                if (GroundPointFromScreen(pos, out var p))
                {
                    var d = p - aimAnchor;
                    d.y = 0;
                    AimDir = d;
                }
                Target = aimAnchor;
                if (mouse.rightButton.wasReleasedThisFrame || !mouse.rightButton.isPressed)
                {
                    Aiming = false;
                    RequestGust(AimDir.magnitude > 0.35f ? AimDir : cloud.Facing);
                }
            }
            else if (!overUi || mouse.leftButton.isPressed)
            {
                if (GroundPointFromScreen(pos, out var p)) Target = p;
            }
            if (mouseRainArmed) rain = true;
            keyTarget = Target;
        }

        // ------------------------------------------------------------------ touch
        /// <summary>Touch distances and speeds are judged in units of 1/1080 of the screen's short
        /// side, so a flick or a still finger measures the same on a phone held either way (by the
        /// long side, an upright phone needed a flick twice as long as the same phone on its side).</summary>
        static float TouchUnit => 1080f / Mathf.Max(1, Mathf.Min(Screen.width, Screen.height));

        void TickTouch(float dt, ref bool rain)
        {
            var ts = Touchscreen.current;
            if (ts == null) { touchId = -1; return; }
            var touches = ts.touches;
            if (touchId < 0)
            {
                for (int i = 0; i < touches.Count; i++)
                {
                    var t = touches[i];
                    if (t.press.wasPressedThisFrame)
                    {
                        Vector2 p = t.position.ReadValue();
                        touchOverUi = PointerOverUi(p, t.touchId.ReadValue());
                        if (touchOverUi) continue;
                        touchId = t.touchId.ReadValue();
                        touchStart = Time.unscaledTime;
                        touchStartPos = touchLastPos = p;
                        touchStartTarget = Target;
                        stillTimer = 0;
                        touchRaining = false;
                        touchVel = Vector2.zero;
                        LastDevice = Device.Touch;
                        break;
                    }
                }
                if (touchId < 0) { HoldCharge = 0; return; }
            }

            UnityEngine.InputSystem.Controls.TouchControl tc = null;
            for (int i = 0; i < touches.Count; i++)
                if (touches[i].touchId.ReadValue() == touchId) { tc = touches[i]; break; }

            if (tc == null || !tc.press.isPressed)
            {
                // released: flick?
                float dur = Time.unscaledTime - touchStart;
                float px = TouchUnit;
                float speed = touchVel.magnitude * px;
                // short flicks span only a few frames, so also judge them by their average speed
                Vector2 travel = touchLastPos - touchStartPos;
                float avg = travel.magnitude * px / Mathf.Max(dur, 1f / 60f);
                bool fast = speed > 1500f;
                bool swipe = travel.magnitude * px > 80f && avg > 700f;
                if (!touchRaining && dur < 0.4f && (fast || swipe))
                {
                    Vector3 dir = ScreenDirToGround(fast ? touchVel : travel);
                    RequestGust(dir);
                    Target = touchStartTarget;
                }
                touchId = -1;
                touchRaining = false;
                HoldCharge = 0;
                return;
            }

            Vector2 pos = tc.position.ReadValue();
            Vector2 delta = pos - touchLastPos;
            touchLastPos = pos;
            touchVel = Vector2.Lerp(touchVel, delta / Mathf.Max(dt, 1e-3f), 0.5f);
            float scale = TouchUnit;
            bool still = delta.magnitude * scale < 3.5f;
            stillTimer = still ? stillTimer + dt : Mathf.Max(0, stillTimer - dt * 3f);
            if (!touchRaining)
            {
                HoldCharge = Mathf.Clamp01((stillTimer - 0.08f) / 0.3f);
                HoldScreenPos = pos;
                if (HoldCharge >= 1f) touchRaining = true;
            }
            else HoldCharge = 1;
            // a flick in progress blows from where Pip is, so don't drag Pip along with it
            bool flicking = !touchRaining && Time.unscaledTime - touchStart < 0.4f && delta.magnitude * scale / Mathf.Max(dt, 1e-3f) > 1200f;
            if (!flicking && GroundPointFromScreen(pos, out var g)) Target = g;
            keyTarget = Target;
            if (touchRaining) rain = true;
        }

        // ------------------------------------------------------------------ keyboard
        void TickKeys(float dt, ref bool rain)
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            Vector2 mv = Vector2.zero;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) mv.x -= 1;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) mv.x += 1;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) mv.y += 1;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) mv.y -= 1;
            bool act = kb.spaceKey.isPressed || kb.eKey.wasPressedThisFrame;
            if (mv != Vector2.zero || act)
            {
                if (LastDevice != Device.Keys) keyTarget = cloud.GroundPoint;
                LastDevice = Device.Keys;
            }
            if (LastDevice == Device.Keys)
            {
                keyTarget += new Vector3(mv.x, 0, mv.y).normalized * 6.5f * dt;
                keyTarget = cloud.ClampToBounds(keyTarget);
                // keep the target close so releasing keys stops quickly
                if (mv == Vector2.zero) SettleKeyTarget(dt);
                Target = keyTarget;
            }
            if (kb.spaceKey.isPressed) rain = true;
            if (kb.eKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame)
            {
                Vector3 dir = mv != Vector2.zero ? new Vector3(mv.x, 0, mv.y) : cloud.Facing;
                RequestGust(dir);
            }
        }

        // ------------------------------------------------------------------ gamepad
        void TickPad(float dt, ref bool rain)
        {
            var pad = Gamepad.current;
            if (pad == null) return;
            Vector2 st = pad.leftStick.ReadValue();
            if (st.magnitude < 0.18f) st = Vector2.zero;
            bool act = pad.buttonSouth.isPressed || pad.rightTrigger.isPressed || pad.buttonWest.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame;
            if (st != Vector2.zero || act)
            {
                if (LastDevice != Device.Pad) keyTarget = cloud.GroundPoint;
                LastDevice = Device.Pad;
            }
            if (LastDevice != Device.Pad) return;
            keyTarget += new Vector3(st.x, 0, st.y) * 7f * dt;
            keyTarget = cloud.ClampToBounds(keyTarget);
            if (st == Vector2.zero) SettleKeyTarget(dt);
            Target = keyTarget;
            if (pad.buttonSouth.isPressed || pad.rightTrigger.isPressed) rain = true;
            padGustCooldown -= dt;
            if ((pad.buttonWest.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame) && padGustCooldown <= 0)
            {
                Vector2 rs = pad.rightStick.ReadValue();
                Vector3 dir = rs.magnitude > 0.3f ? new Vector3(rs.x, 0, rs.y) : (st != Vector2.zero ? new Vector3(st.x, 0, st.y) : cloud.Facing);
                RequestGust(dir);
                padGustCooldown = 0.2f;
            }
        }

        // ------------------------------------------------------------------ helpers
        /// <summary>Stick/keys released: pull the target back to just ahead of Pip, so it glides a
        /// little and stops near where the player let go instead of catching up with a lead.</summary>
        void SettleKeyTarget(float dt)
        {
            var stop = cloud.GroundPoint + cloud.Velocity * 0.07f;
            keyTarget = Vector3.Lerp(keyTarget, new Vector3(stop.x, keyTarget.y, stop.z), 1f - Mathf.Exp(-14f * dt));
        }


        public bool GroundPointFromScreen(Vector2 screen, out Vector3 point)
        {
            var ray = cam.ScreenPointToRay(screen);
            float groundY = cloud.Level != null ? cloud.Level.BaseHeight : 0f;
            if (Physics.Raycast(ray, out var hit, 200f, Layers.GroundMask))
                point = hit.point;
            else
            {
                var plane = new Plane(Vector3.up, new Vector3(0, groundY, 0));
                if (!plane.Raycast(ray, out float t)) { point = Target; return false; }
                point = ray.GetPoint(t);
            }
            point = cloud.ClampToBounds(point);
            return true;
        }

        Vector3 ScreenDirToGround(Vector2 dir)
        {
            var right = cam.transform.right;
            var fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            right.y = 0;
            return (right.normalized * dir.x + fwd * dir.y).normalized;
        }

        static readonly System.Collections.Generic.List<RaycastResult> uiHits = new();
        public static bool PointerOverUi(Vector2 pos, int pointerId)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var data = new PointerEventData(es) { position = pos, pointerId = pointerId };
            uiHits.Clear();
            es.RaycastAll(data, uiHits);
            foreach (var h in uiHits)
                if (h.gameObject != null && h.gameObject.GetComponentInParent<UnityEngine.UI.Selectable>() != null) return true;
            return false;
        }
    }
}
