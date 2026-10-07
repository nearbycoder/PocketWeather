using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PocketWeather
{
    /// <summary>
    /// Runtime uGUI toolkit for Pocket Weather's "cloud paper" look: procedural rounded sprites,
    /// Fredoka/Nunito text, Blender-rendered icons, toy buttons that squish, sliders and toggles.
    /// </summary>
    public enum MenuForm { Usual, Short, Narrow }

    public static class Ui
    {
        public static readonly Color Ink = Res.Hex("3B3A5A");
        public static readonly Color InkSoft = Res.Hex("6E6A8E");
        public static readonly Color Paper = Res.Hex("FFF8EC");
        public static readonly Color PaperShade = Res.Hex("EADFD0");
        public static readonly Color Coral = Res.Hex("FF7A6B");
        public static readonly Color Sky = Res.Hex("5FB8E6");
        public static readonly Color Butter = Res.Hex("FFD45C");
        public static readonly Color Mint = Res.Hex("6CCB8A");
        public static readonly Color Lilac = Res.Hex("B79CFF");
        public static readonly Color White = Color.white;
        public static readonly Color Shadow = new Color(0.23f, 0.2f, 0.4f, 0.22f);

        static Font heading, body;
        public static Font Heading => heading ??= Resources.Load<Font>("Fonts/Fredoka-SemiBold");
        public static Font Body => body ??= Resources.Load<Font>("Fonts/Nunito-Bold");

        static Sprite rounded, circle, ring, softShadow, ringThin, arrow;
        public static Sprite Rounded => rounded ??= MakeRounded(128, 52, false);
        public static Sprite SoftShadow => softShadow ??= MakeRounded(128, 52, true);
        public static Sprite Circle => circle ??= MakeCircle(128, 0f);
        public static Sprite Ring => ring ??= MakeCircle(128, 0.16f);
        public static Sprite RingThin => ringThin ??= MakeCircle(128, 0.07f);
        /// <summary>A soft-cornered triangle pointing right.</summary>
        public static Sprite Arrow => arrow ??= MakeArrow(64);

        static readonly Dictionary<string, Sprite> icons = new();

        public static Sprite IconSprite(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (icons.TryGetValue(name, out var s)) return s;
            s = Resources.Load<Sprite>("Icons/" + name);
            if (s == null)
            {
                var tex = Resources.Load<Texture2D>("Icons/" + name);
                if (tex != null) s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100);
            }
            icons[name] = s;
            return s;
        }

        static Sprite MakeRounded(int size, int radius, bool shadow)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                float d = Mathf.Sqrt(Mathf.Max(qx, 0) * Mathf.Max(qx, 0) + Mathf.Max(qy, 0) * Mathf.Max(qy, 0)) + Mathf.Min(Mathf.Max(qx, qy), 0) - radius;
                float a = shadow ? Mathf.Clamp01(1f - (d + radius * 0.7f) / (radius * 0.75f)) : Mathf.Clamp01(0.5f - d);
                if (shadow) a = a * a * (3 - 2 * a);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            int b = radius + 2;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        static Sprite MakeCircle(int size, float ringWidth)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            var px = new Color32[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - half) * (x + 0.5f - half) + (y + 0.5f - half) * (y + 0.5f - half));
                float a = Mathf.Clamp01(half - 1 - d + 0.5f);
                if (ringWidth > 0) a *= Mathf.Clamp01(d - (half - 1 - half * ringWidth * 2f) + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
        }

        static Sprite MakeArrow(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            var px = new Color32[size * size];
            // an equilateral-ish triangle pointing +x, its corners rounded by r (signed distance)
            Vector2 a = new(size * 0.84f, size * 0.5f), b = new(size * 0.22f, size * 0.86f), c = new(size * 0.22f, size * 0.14f);
            float r = size * 0.07f;
            float Edge(Vector2 p, Vector2 p0, Vector2 p1)
            {
                var e = p1 - p0; var n = new Vector2(e.y, -e.x).normalized;
                return Vector2.Dot(p - p0, n);
            }
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float d = Mathf.Max(Edge(p, a, b), Mathf.Max(Edge(p, b, c), Edge(p, c, a))) + r;
                float al = Mathf.Clamp01(0.5f - d);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(al * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
        }

        // ------------------------------------------------------------------ construction
        public static Canvas MakeCanvas(string name, int order, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            // Expand: the whole 1920x1080 design area always fits, on 20:9 phones, 4:3 tablets
            // and even portrait windows; at 16:9 it's identical to a fixed reference
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            go.AddComponent<OrientationScaler>();
            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        /// <summary>Taller than wide: a phone held upright, or a narrow window.</summary>
        public static bool Portrait => Screen.width < Screen.height;
        /// <summary>The design width in portrait. 1920 would show the UI at 0.375x on a 720-wide
        /// phone; at 1200 it's 0.6x, and the few layouts wider than that reflow.</summary>
        public const float PortraitWidth = 1200f;
        /// <summary>On a phone the menus are drawn at least this many CSS px per design unit (28-unit
        /// text is then 12 px), scaled up by at most MenuMaxBoost (held sideways) or
        /// MenuMaxBoostPortrait (upright), and never so far that fewer than MenuMinWidth (sideways)
        /// or MenuMinWidthPortrait (upright) design units fit across.</summary>
        public const float MenuMinCss = 0.44f, MenuMaxBoost = 1.35f, MenuMinWidth = 1600f, MenuMaxBoostPortrait = 1.4f, MenuMinWidthPortrait = 840f;
        /// <summary>How much the menus are scaled up (1 on desktops and larger tablets).</summary>
        public static float MenuBoost { get; private set; } = 1f;
        /// <summary>Which layout the menus use: their usual one; Short when a phone on its side draws
        /// them bigger, which leaves a design area only about 820 to 900 units tall; or Narrow when a
        /// phone held upright does, which leaves one about 860 to 890 units wide.</summary>
        public static MenuForm MenuLayout { get; private set; } = MenuForm.Usual;
        public static bool MenuShort => MenuLayout == MenuForm.Short;
        public static bool MenuNarrow => MenuLayout == MenuForm.Narrow;
        /// <summary>Raised when the menus switch layouts.</summary>
        public static event Action<MenuForm> MenuLayoutChanged;
        internal static void SetMenuBoost(float k)
        {
            MenuBoost = k;
            var form = k <= 1f ? MenuForm.Usual : Portrait ? MenuForm.Narrow : MenuForm.Short;
            if (form == MenuLayout) return;
            MenuLayout = form;
            MenuLayoutChanged?.Invoke(form);
        }
        /// <summary>Raised when the screen turns between landscape and portrait.</summary>
        public static event Action<bool> OrientationChanged;
        static bool? lastPortrait;
        internal static void PollOrientation()
        {
            bool p = Portrait;
            if (lastPortrait == p) return;
            lastPortrait = p;
            OrientationChanged?.Invoke(p);
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 size, Vector2 pos, Vector2? pivot = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return rt;
        }

        public static RectTransform Stretch(string name, Transform parent, float inset = 0)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static Image Image(Transform parent, Sprite sprite, Color color, Vector2 size, Vector2 pos, Vector2? anchor = null, string name = "Image")
        {
            var rt = Rect(name, parent, anchor ?? new Vector2(0.5f, 0.5f), size, pos);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (sprite != null && sprite.border != Vector4.zero) img.type = UnityEngine.UI.Image.Type.Sliced;
            img.preserveAspect = sprite != null && sprite.border == Vector4.zero;
            return img;
        }

        /// <summary>A cloud-paper panel with a soft drop shadow. Returns the panel (content goes inside).</summary>
        public static Image Panel(Transform parent, Vector2 size, Vector2 pos, Color? color = null, Vector2? anchor = null, float radius = 40f, bool shadow = true, string name = "Panel")
        {
            var a = anchor ?? new Vector2(0.5f, 0.5f);
            Image sh = null;
            if (shadow)
            {
                sh = Image(parent, SoftShadow, Shadow, size + new Vector2(60, 60), pos + new Vector2(0, -10), a, name + "Shadow");
                sh.pixelsPerUnitMultiplier = 52f / (radius + 30f);
            }
            var img = Image(parent, Rounded, color ?? Paper, size, pos, a, name);
            img.pixelsPerUnitMultiplier = 52f / radius;
            img.raycastTarget = true;
            if (sh != null) sh.gameObject.AddComponent<ShadowFollow>().target = img.rectTransform;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, Vector2 rectSize, Vector2 pos, bool heading = false,
            TextAnchor align = TextAnchor.MiddleCenter, Vector2? anchor = null, string name = "Label")
        {
            var rt = Rect(name, parent, anchor ?? new Vector2(0.5f, 0.5f), rectSize, pos);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = heading ? Heading : Body;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.lineSpacing = 0.95f;
            return t;
        }

        public static void Shadowed(Graphic g, float dist = 3f, float alpha = 0.25f)
        {
            var s = g.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            s.effectColor = new Color(Ink.r, Ink.g, Ink.b, alpha);
            s.effectDistance = new Vector2(0, -dist);
        }

        public static void Outlined(Graphic g, Color c, float dist = 3f)
        {
            var o = g.gameObject.AddComponent<Outline>();
            o.effectColor = c;
            o.effectDistance = new Vector2(dist, -dist);
            var o2 = g.gameObject.AddComponent<Outline>();
            o2.effectColor = c;
            o2.effectDistance = new Vector2(-dist, dist);
        }

        public static Image Icon(Transform parent, string icon, float size, Vector2 pos, Vector2? anchor = null)
        {
            var img = Image(parent, IconSprite(icon), Color.white, new Vector2(size, size), pos, anchor, "Icon_" + icon);
            img.preserveAspect = true;
            return img;
        }

        /// <summary>A chunky toy button: darker lip below, face that squishes down when pressed.</summary>
        public static JuicyButton Button(Transform parent, string label, Color color, Vector2 size, Vector2 pos, Action onClick,
            string icon = null, Vector2? anchor = null, int fontSize = 44, string name = null)
        {
            var root = Rect(name ?? ("Btn_" + label), parent, anchor ?? new Vector2(0.5f, 0.5f), size, pos);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(1, 1, 1, 0);
            float radius = Mathf.Min(size.y / 2f, 46f);
            var lip = Image(root, Rounded, Color.Lerp(color, Ink, 0.32f), size, new Vector2(0, -7), null, "Lip");
            lip.pixelsPerUnitMultiplier = 52f / radius;
            var face = Image(root, Rounded, color, size, Vector2.zero, null, "Face");
            face.pixelsPerUnitMultiplier = 52f / radius;
            var shine = Image(face.transform, Rounded, new Color(1, 1, 1, 0.22f), new Vector2(size.x - 24, size.y * 0.38f), new Vector2(0, size.y * 0.22f), null, "Shine");
            shine.pixelsPerUnitMultiplier = 52f / Mathf.Max(8f, radius * 0.5f);
            Text txt = null;
            float iconSize = Mathf.Min(size.y * 0.72f, 84f);
            if (!string.IsNullOrEmpty(label))
            {
                float tx = icon != null ? iconSize * 0.45f : 0;
                bool lightFace = color.r * 0.3f + color.g * 0.59f + color.b * 0.11f > 0.85f;   // paper-coloured buttons get ink text
                txt = Label(face.transform, label, fontSize, lightFace ? Ink : Color.white, new Vector2(size.x - 20 - (icon != null ? iconSize : 0), size.y), new Vector2(tx, 2), true);
                if (!lightFace) Shadowed(txt, 3, 0.35f);
            }
            if (icon != null)
            {
                float ix = string.IsNullOrEmpty(label) ? 0 : -size.x / 2 + iconSize * 0.62f + 10;
                Icon(face.transform, icon, iconSize, new Vector2(ix, 2));
            }
            var btn = root.gameObject.AddComponent<JuicyButton>();
            btn.Init(face.rectTransform, txt, onClick);
            var nav = btn.navigation;
            nav.mode = Navigation.Mode.Automatic;
            btn.navigation = nav;
            return btn;
        }

        public static Slider Slider(Transform parent, string label, float value, Vector2 pos, Action<float> onChange, float width = 560)
        {
            var root = Rect("Slider_" + label, parent, new Vector2(0.5f, 0.5f), new Vector2(width, 80), pos);
            Label(root, label, 34, Ink, new Vector2(220, 60), new Vector2(-width / 2 + 110, 0), false, TextAnchor.MiddleLeft);
            float trackW = width - 250;
            var track = Image(root, Rounded, PaperShade, new Vector2(trackW, 22), new Vector2(width / 2 - trackW / 2 - 10, 0), null, "Track");
            track.pixelsPerUnitMultiplier = 52f / 11f;
            var fillArea = Rect("FillArea", track.transform, new Vector2(0.5f, 0.5f), new Vector2(trackW, 22), Vector2.zero);
            fillArea.anchorMin = Vector2.zero; fillArea.anchorMax = Vector2.one; fillArea.offsetMin = Vector2.zero; fillArea.offsetMax = Vector2.zero;
            var fill = Image(fillArea, Rounded, Sky, new Vector2(0, 22), Vector2.zero, null, "Fill");
            fill.pixelsPerUnitMultiplier = 52f / 11f;
            fill.rectTransform.anchorMin = new Vector2(0, 0); fill.rectTransform.anchorMax = new Vector2(0, 1);
            fill.rectTransform.sizeDelta = new Vector2(0, 0);
            var handleArea = Rect("HandleArea", track.transform, new Vector2(0.5f, 0.5f), new Vector2(trackW, 22), Vector2.zero);
            handleArea.anchorMin = Vector2.zero; handleArea.anchorMax = Vector2.one; handleArea.offsetMin = Vector2.zero; handleArea.offsetMax = Vector2.zero;
            var handle = Image(handleArea, Circle, White, new Vector2(54, 54), Vector2.zero, null, "Handle");
            handle.raycastTarget = true;
            Image(handle.transform, Ring, Sky, new Vector2(54, 54), Vector2.zero, null, "HandleRing");
            var s = root.gameObject.AddComponent<UnityEngine.UI.Slider>();
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.targetGraphic = handle;
            s.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            s.minValue = 0; s.maxValue = 1;
            s.value = value;
            s.onValueChanged.AddListener(v => { onChange(v); });
            var bg = root.gameObject.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0);
            return s;
        }

        public static Toggle Toggle(Transform parent, string label, bool value, Vector2 pos, Action<bool> onChange, float width = 560)
        {
            var root = Rect("Toggle_" + label, parent, new Vector2(0.5f, 0.5f), new Vector2(width, 76), pos);
            var bg = root.gameObject.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0);
            Label(root, label, 34, Ink, new Vector2(width - 160, 60), new Vector2(-80, 0), false, TextAnchor.MiddleLeft);
            var track = Image(root, Rounded, PaperShade, new Vector2(110, 56), new Vector2(width / 2 - 65, 0), null, "Track");
            track.pixelsPerUnitMultiplier = 52f / 28f;
            var knob = Image(track.transform, Circle, White, new Vector2(46, 46), new Vector2(-27, 0), null, "Knob");
            var t = root.gameObject.AddComponent<UnityEngine.UI.Toggle>();
            t.targetGraphic = track;
            t.isOn = value;
            void Visual(bool on, bool animate)
            {
                Color c = on ? Mint : PaperShade;
                float x = on ? 27 : -27;
                if (!animate) { track.color = c; knob.rectTransform.anchoredPosition = new Vector2(x, 0); return; }
                var start = knob.rectTransform.anchoredPosition.x;
                var c0 = track.color;
                Tween.To(0, 1, 0.22f, k =>
                {
                    knob.rectTransform.anchoredPosition = new Vector2(Mathf.LerpUnclamped(start, x, k), 0);
                    track.color = Color.Lerp(c0, c, k);
                }, k => Ease.OutBack(k), 0, null, knob);
            }
            Visual(value, false);
            var sync = root.gameObject.AddComponent<ToggleSync>();
            sync.toggle = t; sync.shown = value; sync.show = on => Visual(on, false);
            t.onValueChanged.AddListener(v => { sync.shown = v; Visual(v, true); Sfx.Ui("ui_tick"); onChange(v); });
            return t;
        }

        public static CanvasGroup Group(GameObject go)
        {
            var g = go.GetComponent<CanvasGroup>();
            return g != null ? g : go.AddComponent<CanvasGroup>();
        }

        public static void Fade(CanvasGroup g, float to, float duration, Action done = null, float delay = 0)
        {
            float from = g.alpha;
            Tween.To(from, to, duration, a => { if (g != null) g.alpha = a; }, Ease.OutCubic, delay, done, g);
        }

        /// <summary>Pops a UI element in (scale from 0 with overshoot, to its scale `to`).</summary>
        public static void PopIn(Transform t, float delay = 0, float duration = 0.45f, float to = 1f)
        {
            t.localScale = Vector3.zero;
            Tween.To(0, to, duration, k => { if (t != null) t.localScale = Vector3.one * k; }, k => Ease.OutBack(k, 2.2f), delay, null, t);
        }
    }

    /// <summary>Button with a squishy face, hover lift, and UI sounds. Works with mouse, touch, keys and pads.</summary>
    public class JuicyButton : Selectable, IPointerClickHandler, ISubmitHandler
    {
        RectTransform face;
        Text label;
        Action onClick;
        float press, hover;
        public bool Silent;

        public void Init(RectTransform faceRect, Text txt, Action click)
        {
            face = faceRect;
            label = txt;
            onClick = click;
            transition = Transition.None;
        }

        public void SetLabel(string s) { if (label != null) label.text = s; }
        public string Label => label != null ? label.text : "";

        public void OnPointerClick(PointerEventData e)
        {
            if (!IsInteractable() || e.button != PointerEventData.InputButton.Left) return;
            Click();
        }

        public void OnSubmit(BaseEventData e)
        {
            if (!IsInteractable()) return;
            press = 1f;
            Click();
        }

        void Click()
        {
            if (!Silent) Sfx.Ui("ui_pop");
            Tween.Punch(transform, 0.08f, 0.3f);
            onClick?.Invoke();
        }

        public override void OnPointerEnter(PointerEventData e)
        {
            base.OnPointerEnter(e);
            if (IsInteractable() && e.pointerId < 0) Sfx.Ui("ui_tick", 0.6f);
        }

        public override void OnSelect(BaseEventData e)
        {
            base.OnSelect(e);
            if (!(e is PointerEventData)) Sfx.Ui("ui_tick", 0.5f);
        }

        void Update()
        {
            if (face == null) return;
            bool pressed = IsPressed();
            bool hovered = IsHighlighted() || currentSelectionState == SelectionState.Selected;
            float dt = Clock.UnscaledDelta;
            press = Mathf.MoveTowards(press, pressed ? 1 : 0, dt * (pressed ? 14 : 6));
            hover = Mathf.MoveTowards(hover, hovered ? 1 : 0, dt * 8);
            face.anchoredPosition = new Vector2(0, -press * 6f + hover * 2f);
            float s = 1f - press * 0.03f + hover * 0.03f;
            face.localScale = new Vector3(s, s, 1);
            var cg = GetComponent<CanvasGroup>();
            if (!IsInteractable())
            {
                if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0.5f;
            }
            else if (cg != null) cg.alpha = 1f;
        }
    }

    /// <summary>A drop shadow sibling that tracks its panel's position, scale and visibility.</summary>
    public class ShadowFollow : MonoBehaviour
    {
        public RectTransform target;
        RectTransform rt;
        Vector2 offset = new Vector2(0, -10);
        void LateUpdate()
        {
            if (target == null) { Destroy(gameObject); return; }
            if (rt == null) rt = (RectTransform)transform;
            rt.anchoredPosition = target.anchoredPosition + offset;
            rt.sizeDelta = target.sizeDelta + new Vector2(60, 60);   // a panel resized for a short screen
            rt.localScale = target.localScale;
            rt.localRotation = target.localRotation;
            var g = GetComponent<UnityEngine.UI.Image>();
            if (g != null) g.enabled = target.gameObject.activeInHierarchy;
        }
    }

    /// <summary>Keeps a RectTransform inside the device safe area (notches).</summary>
    public class SafeArea : MonoBehaviour
    {
        Rect last;
        void Update()
        {
            var sa = Screen.safeArea;
            if (sa == last || Screen.width == 0) return;
            last = sa;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            rt.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }

    /// <summary>Gives a canvas a narrower design width while the screen is in portrait (see
    /// Ui.PortraitWidth); landscape keeps the 1920x1080 reference exactly.</summary>
    public class OrientationScaler : MonoBehaviour
    {
        CanvasScaler scaler;
        bool menus;
        void Awake() { scaler = GetComponent<CanvasScaler>(); Apply(); }
        void Update() { Ui.PollOrientation(); Apply(); }

        /// <summary>The menus' canvas: on a phone it's drawn bigger, up to Ui.MenuMinCss, and the
        /// screens switch to their short (sideways) or narrow (upright) layouts.</summary>
        public void ForMenus() { menus = true; Apply(); }

        void Apply()
        {
            var want = Ui.Portrait ? new Vector2(Ui.PortraitWidth, 1080) : new Vector2(1920, 1080);
            if (menus)
            {
                float k = 1f;
                if (Screen.width > 0 && Screen.height > 0)
                {
                    // Expand: the canvas's scale is the smaller of the two fits, here in CSS px
                    float fit = Mathf.Min(Screen.width / want.x, Screen.height / want.y);
                    float css = fit / Platform.PixelsPerCssPx;
                    bool p = Ui.Portrait;
                    k = Mathf.Clamp(Ui.MenuMinCss / Mathf.Max(css, 0.01f), 1f, p ? Ui.MenuMaxBoostPortrait : Ui.MenuMaxBoost);
                    // keep enough design units across for the layouts (sideways: the pause menu's
                    // controls line; upright: the narrow cards)
                    k = Mathf.Max(1f, Mathf.Min(k, Screen.width / (fit * (p ? Ui.MenuMinWidthPortrait : Ui.MenuMinWidth))));
                    if (k < 1.01f) k = 1f;
                    if (k != Ui.MenuBoost || (k > 1f && p != (Ui.MenuLayout == MenuForm.Narrow)))
                        Debug.Log($"[PW] menu scale: {css * k:0.000} CSS px per design unit, x{k:0.00} at {Screen.width}x{Screen.height} ({Platform.PixelsPerCssPx:0.##} px per CSS px)");
                }
                want /= k;
                Ui.SetMenuBoost(k);
            }
            if (scaler != null && scaler.referenceResolution != want) scaler.referenceResolution = want;
        }
    }

    /// <summary>Moves a toggle's knob when its value is set without a notification (a setting that
    /// changed somewhere else, such as fullscreen).</summary>
    public class ToggleSync : MonoBehaviour
    {
        public UnityEngine.UI.Toggle toggle;
        public bool shown;
        public Action<bool> show;
        void LateUpdate()
        {
            if (toggle == null || toggle.isOn == shown) return;
            shown = toggle.isOn;
            show?.Invoke(shown);
        }
    }

    /// <summary>Gentle idle bob / pulse for UI elements.</summary>
    public class UiBob : MonoBehaviour
    {
        public float amplitude = 6f, speed = 2f, pulse = 0f, phase;
        Vector2 basePos;
        RectTransform rt;
        void Start() { rt = (RectTransform)transform; basePos = rt.anchoredPosition; }
        void Update()
        {
            if (rt == null) return;
            float t = Clock.UnscaledTime * speed + phase;
            rt.anchoredPosition = basePos + new Vector2(0, Mathf.Sin(t) * amplitude);
            if (pulse > 0) rt.localScale = Vector3.one * (1 + Mathf.Sin(t * 1.3f) * pulse);
        }
        public void Rebase() { if (rt != null) basePos = rt.anchoredPosition; }
    }
}
