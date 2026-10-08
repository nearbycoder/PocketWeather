using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PocketWeather
{
    /// <summary>
    /// With the keys or a gamepad, the menu control that Enter or A will press wears a soft ring that
    /// breathes gently and glides from control to control. It hides as soon as the mouse moves or
    /// clicks or a finger touches the screen, and comes back with the next key or button. Drawn on a
    /// canvas of its own, in screen pixels, over the menus and under the cloud wipe.
    /// </summary>
    public class FocusRing : MonoBehaviour
    {
        public static FocusRing I { get; private set; }
        /// <summary>The keys or a pad were used last (the ring's shown when there's a menu control).</summary>
        public static bool NavMode { get; private set; }
        /// <summary>The ring is on screen.</summary>
        public static bool Visible => I != null && I.alpha > 0.5f;
        /// <summary>The control it's around (or gliding to).</summary>
        public static RectTransform Target => I != null ? I.target : null;
        /// <summary>Where the ring is drawn now, in screen pixels.</summary>
        public static Rect ScreenRect => I != null ? I.rect : default;

        public static readonly Color RingColor = Res.Hex("3E9BD6");
        /// <summary>How far past its place the ring breathes, in design units.</summary>
        public const float MaxGrow = 5f;
        RectTransform ring, glow;
        Image ringImg, glowImg;
        RectTransform target;
        Rect rect;
        float alpha, pop;

        public static void Create(Transform parent)
        {
            if (I != null) return;
            var go = new GameObject("FocusRingCanvas");
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 60;   // over the menus (50), under the cloud wipe (100)
            I = go.AddComponent<FocusRing>();
            // a faint second line just outside the ring (an outline, so the control itself isn't tinted)
            I.glowImg = Ui.Image(go.transform, Ui.RoundedOutline, new Color(RingColor.r, RingColor.g, RingColor.b, 0.3f), Vector2.one, Vector2.zero, null, "Glow");
            I.glowImg.type = Image.Type.Sliced;
            I.glowImg.raycastTarget = false;
            I.glow = I.glowImg.rectTransform;
            I.ringImg = Ui.Image(go.transform, Ui.RoundedOutline, RingColor, Vector2.one, Vector2.zero, null, "Ring");
            I.ringImg.type = Image.Type.Sliced;
            I.ringImg.raycastTarget = false;
            I.ring = I.ringImg.rectTransform;
            foreach (var rt in new[] { I.glow, I.ring })
            {
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);
            }
            I.Apply();
        }

        /// <summary>The ring's place around a control, in screen pixels: the control's bounds plus a
        /// margin that grows with how big the menus are drawn.</summary>
        public static Rect RectFor(RectTransform rt, out float scale)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            var canvas = rt.GetComponentInParent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]), b = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            scale = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
            // a control's own scale (a button's lift, a card's pop) is left out, so the ring doesn't
            // jitter with them
            var ls = rt.lossyScale.x / Mathf.Max(0.0001f, scale);
            var c = (a + b) / 2f;
            var size = new Vector2(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y)) / Mathf.Max(0.0001f, ls);
            float pad = 10f * scale;
            size += Vector2.one * pad * 2f;
            // a control near the screen's edge (the title's Quit) keeps its ring on screen, breathing
            // included: the margin on that side gives way
            float edge = MaxGrow * scale + 1f;
            var r = Rect.MinMaxRect(Mathf.Max(edge, c.x - size.x / 2f), Mathf.Max(edge, c.y - size.y / 2f),
                                    Mathf.Min(Screen.width - edge, c.x + size.x / 2f), Mathf.Min(Screen.height - edge, c.y + size.y / 2f));
            return r;
        }

        /// <summary>A control on an open menu that's smaller than the screen: the title's "tap
        /// anywhere" backdrop gets no ring (its prompt already says what Enter or A will do).</summary>
        public static bool Rings(GameObject go)
        {
            var menu = MenuScreen.Owning(go.transform);
            if (menu == null || !menu.IsOpen) return false;
            var rt = (RectTransform)go.transform;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            float area = Mathf.Abs((corners[2].x - corners[0].x) * (corners[2].y - corners[0].y));
            return area < 0.9f * Screen.width * Screen.height;
        }

        void Update()
        {
            ReadDevices();
            var sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            var rt = sel != null && sel.activeInHierarchy && Rings(sel) ? sel.transform as RectTransform : null;
            float groupAlpha = 1f;
            if (rt != null)
            {
                var g = rt.GetComponentInParent<CanvasGroup>();
                if (g != null) groupAlpha = g.alpha;
            }
            bool show = NavMode && rt != null && groupAlpha > 0.5f;
            float dt = Clock.UnscaledDelta;
            if (show)
            {
                var want = RectFor(rt, out _);
                if (alpha <= 0.01f || target == null) { rect = want; pop = 1f; }   // appears in place, with a little pop
                else if (rt != target) pop = Mathf.Max(pop, 0.35f);
                float k = 1f - Mathf.Exp(-dt * 20f);
                rect = new Rect(Vector2.Lerp(rect.position, want.position, k), Vector2.Lerp(rect.size, want.size, k));
                target = rt;
            }
            else if (alpha <= 0.01f) target = null;
            alpha = Mathf.MoveTowards(alpha, show ? groupAlpha : 0f, dt * (show ? 8f : 10f));
            pop = Mathf.MoveTowards(pop, 0f, dt * 3.5f);
            Apply();
        }

        void Apply()
        {
            var canvas = target != null ? target.GetComponentInParent<Canvas>() : null;
            float scale = canvas != null ? Mathf.Max(0.3f, canvas.rootCanvas.scaleFactor) : 1f;
            float breathe = 0.5f + 0.5f * Mathf.Sin(Clock.UnscaledTime * 3.2f);
            float grow = (MaxGrow - 3f + 3f * breathe) * scale;   // a slow in-and-out of a few pixels
            float popGrow = pop * pop * 18f * scale;
            var size = rect.size + Vector2.one * (grow + popGrow) * 2f;
            ring.anchoredPosition = glow.anchoredPosition = rect.center;
            ring.sizeDelta = size;
            glow.sizeDelta = size + Vector2.one * 12f * scale;
            // corners: about a button's, never rounder than half the ring's height
            float radius = Mathf.Min(size.y / 2f, 40f * scale);
            ringImg.pixelsPerUnitMultiplier = 52f / Mathf.Max(4f, radius);
            glowImg.pixelsPerUnitMultiplier = 52f / Mathf.Max(4f, radius + 6f * scale);
            ringImg.color = new Color(RingColor.r, RingColor.g, RingColor.b, alpha * (0.8f + 0.2f * breathe));
            glowImg.color = new Color(RingColor.r, RingColor.g, RingColor.b, alpha * (0.15f + 0.2f * breathe));
            ringImg.enabled = glowImg.enabled = alpha > 0.001f;
        }

        /// <summary>Keys and pads turn the ring on; the mouse moving or clicking, or a touch, turn it off.</summary>
        static void ReadDevices()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame) NavMode = true;
            foreach (var pad in Gamepad.all)
            {
                if (pad.dpad.ReadValue().sqrMagnitude > 0.25f || pad.leftStick.ReadValue().sqrMagnitude > 0.25f ||
                    pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame)
                    NavMode = true;
            }
            var mouse = Mouse.current;
            if (mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 9f || mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
                NavMode = false;
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) NavMode = false;
        }

        /// <summary>Captures: as if a key had just been pressed.</summary>
        public static void DebugKeysUsed() => NavMode = true;
        /// <summary>Tests: as if the mouse had just moved.</summary>
        public static void DebugPointerUsed() => NavMode = false;
    }
}
