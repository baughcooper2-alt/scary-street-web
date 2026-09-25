using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Generated UI art: rounded 9-slice shapes, gradients, a starburst, a display font, and a set of icons
// (drawn from signed-distance shapes, so they're crisp at any size). Animations live in UIMotion.cs and
// use unscaled time, so they work on the paused level-up screen.
public static class UIArt
{
    public enum Icon
    {
        Heart, Fist, Crosshair, Bolt, Clover, Shield, Star, Can, Glass, Drop, Bag, Drumstick, Box, Backpack,
        Board, Book, Guitar, Cart, Burger, Dice, Play, Gear, Gamepad, Coin,
        Cards, Chip, Crutch, Fish, Bottle,                                  // add new ones at the end
    }

    // The look: a night-street poster. Ink and paper, mustard caution tape, blood red, a teal for stats;
    // condensed display type over a clean sans; slanted tags; hard offset "print" shadows; a little film grain.
    public static class Theme
    {
        public static readonly Color Ink = new Color(0.06f, 0.047f, 0.05f), Ink2 = new Color(0.11f, 0.09f, 0.095f), Ink3 = new Color(0.17f, 0.14f, 0.145f);
        public static readonly Color Paper = new Color(0.95f, 0.92f, 0.87f), Muted = new Color(0.95f, 0.92f, 0.87f, 0.58f);
        public static readonly Color Mustard = new Color(0.95f, 0.71f, 0.2f), Blood = new Color(0.85f, 0.22f, 0.17f), Teal = new Color(0.25f, 0.72f, 0.65f);
        public static readonly Color Money = new Color(0.55f, 0.88f, 0.45f);
        public const float Shadow = 6f;
    }

    static Font body;
    // Clean sans for body text (falls back to Arial).
    public static Font Body => body ? body : body = Font.CreateDynamicFontFromOSFont(new[] { "Helvetica Neue", "Segoe UI", "Avenir Next", "Arial" }, 32);

    static Sprite stripe, tag, grain;

    // Caution tape: mustard and ink diagonal stripes (tile it).
    public static Sprite StripeSprite()
    {
        if (Alive(stripe)) return stripe;
        const int n = 32; var tex = NewTex(n, n); tex.wrapMode = TextureWrapMode.Repeat;
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            tex.SetPixel(x, y, ((x + y) / 8) % 2 == 0 ? Theme.Mustard : Theme.Ink);
        tex.Apply();
        return stripe = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 32);
    }

    public static Image Stripe(RectTransform parent, float x, float y, float w, float h = 10f)
    {
        var img = UIKit.Panel(parent, "Stripe", Color.white);
        img.sprite = StripeSprite(); img.type = Image.Type.Tiled;
        UIKit.Place(img.rectTransform, x, y, w, h);
        return img;
    }

    // Slanted tag shape (parallelogram with soft edges), 9-sliced.
    public static Sprite TagSprite()
    {
        if (Alive(tag)) return tag;
        const int w = 64, h = 32; const float slant = 8f; var tex = NewTex(w, h);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            float off = slant * (y / (float)(h - 1));
            float left = x + 0.5f - off, right = (w - slant + off) - (x + 0.5f);
            tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(Mathf.Min(left, right) + 0.5f)));
        }
        tex.Apply();
        return tag = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(14, 2, 14, 2));
    }

    // A slanted label chip: "ROUND 1", "UPGRADE", key hints.
    public static Text Tag(RectTransform parent, string text, Color back, Color fore, float x, float y, float w, float h, int size = 22)
    {
        var img = UIKit.Panel(parent, "Tag", back); img.sprite = TagSprite(); img.type = Image.Type.Sliced;
        UIKit.Place(img.rectTransform, x, y, w, h);
        var t = UIKit.Label(img.rectTransform, text, size, fore, TextAnchor.MiddleCenter);
        t.font = Display; UIKit.Fill(t.rectTransform);
        return t;
    }

    // Faint film grain for full-screen menus.
    public static Image Grain(Transform parent, float alpha = 0.05f)
    {
        if (!Alive(grain))
        {
            const int n = 128; var tex = NewTex(n, n); tex.wrapMode = TextureWrapMode.Repeat; tex.filterMode = FilterMode.Point;
            var r = new System.Random(3);
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { float v = (float)r.NextDouble(); tex.SetPixel(x, y, new Color(v, v, v, 1)); }
            tex.Apply();
            grain = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100);
        }
        var img = UIKit.Panel(parent, "Grain", new Color(1, 1, 1, alpha)); img.sprite = grain; img.type = Image.Type.Tiled;
        UIKit.Fill(img.rectTransform);
        return img;
    }

    // Hard offset shadow, like a printed sticker.
    public static T Print<T>(T g, float distance = Theme.Shadow) where T : Graphic
    {
        var s = g.gameObject.AddComponent<Shadow>();
        s.effectColor = new Color(0, 0, 0, 0.85f); s.effectDistance = new Vector2(distance, -distance);
        return g;
    }

    // Sprites made at runtime are destroyed when Play stops, but with domain reload off the static caches keep
    // pointing at them (they'd draw as white squares next time), so every cache checks this before reusing.
    public static bool Alive(Sprite s) => s && s.texture;

    static readonly Dictionary<int, Sprite> rounded = new Dictionary<int, Sprite>();
    static readonly Dictionary<Icon, Sprite> icons = new Dictionary<Icon, Sprite>();
    static Sprite vertical, horizontal, burst, vignette;
    static Font display;

    // Impact on Mac and Windows (Arial Black / Helvetica as fallbacks) for titles.
    // Horror lettering for "SCARY": Resources/Fonts/Creepster.ttf (Google Fonts, OFL), falling back to Display.
    static Font horror; static bool horrorLooked;
    public static Font Horror { get { if (!horrorLooked || !horror) { horrorLooked = true; horror = Resources.Load<Font>("Fonts/Creepster"); } return horror ? horror : Display; } }
    public static bool HorrorIsReal => Horror != Display;
    public static Font Display => display ? display : display = Font.CreateDynamicFontFromOSFont(new[] { "Impact", "Arial Black", "Helvetica Neue Condensed Black", "Helvetica Neue", "Arial" }, 64);

    // White rounded rectangle for Image.type = Sliced (tint it with Image.color).
    public static Sprite Rounded(int radius = 18)
    {
        if (rounded.TryGetValue(radius, out var s) && Alive(s)) return s;
        int size = radius * 2 + 4;
        var tex = NewTex(size, size);
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - size / 2f) - (size / 2f - radius - 1), 0);
                float dy = Mathf.Max(Mathf.Abs(y + 0.5f - size / 2f) - (size / 2f - radius - 1), 0);
                float d = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                px[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(0.5f - d));
            }
        tex.SetPixels(px); tex.Apply();
        float b = radius + 2;
        return rounded[radius] = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
    }

    // Top-to-bottom white→transparent, for shading panels (tint with Image.color).
    public static Sprite VerticalFade()
    {
        if (Alive(vertical)) return vertical;
        var tex = NewTex(4, 64);
        for (int y = 0; y < 64; y++) for (int x = 0; x < 4; x++) tex.SetPixel(x, y, new Color(1, 1, 1, y / 63f));
        tex.Apply();
        return vertical = Sprite.Create(tex, new Rect(0, 0, 4, 64), new Vector2(0.5f, 0.5f));
    }

    // Solid on the left, fading out to the right (starts fading at `hold` of the width).
    public static Sprite HorizontalFade()
    {
        if (Alive(horizontal)) return horizontal;
        var tex = NewTex(128, 4);
        for (int x = 0; x < 128; x++) for (int y = 0; y < 4; y++) tex.SetPixel(x, y, new Color(1, 1, 1, 1f - Mathf.SmoothStep(0.45f, 1f, x / 127f)));
        tex.Apply();
        return horizontal = Sprite.Create(tex, new Rect(0, 0, 128, 4), new Vector2(0.5f, 0.5f));
    }

    // Dark around the edges, clear in the middle.
    public static Sprite Vignette()
    {
        if (Alive(vignette)) return vignette;
        const int n = 128; var tex = NewTex(n, n);
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float dx = (x - n / 2f) / (n / 2f), dy = (y - n / 2f) / (n / 2f);
            tex.SetPixel(x, y, new Color(0, 0, 0, Mathf.SmoothStep(0.15f, 1.05f, Mathf.Sqrt(dx * dx * 0.8f + dy * dy))));
        }
        tex.Apply();
        return vignette = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
    }

    // Rays for behind "LEVEL UP!".
    public static Sprite Burst()
    {
        if (Alive(burst)) return burst;
        const int n = 256; var tex = NewTex(n, n);
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float dx = x - n / 2f, dy = y - n / 2f, r = Mathf.Sqrt(dx * dx + dy * dy) / (n / 2f), a = Mathf.Atan2(dy, dx);
            float ray = Mathf.Clamp01(Mathf.Cos(a * 12f) * 3f - 1.5f);
            tex.SetPixel(x, y, new Color(1, 1, 1, ray * Mathf.Clamp01(1f - r) * 0.8f + Mathf.Clamp01(1f - r * 2.2f) * 0.5f));
        }
        tex.Apply();
        return burst = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
    }

    static Texture2D NewTex(int w, int h) => new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

    // ---------- widgets ----------

    public static Image RoundPanel(Transform parent, string name, Color color, int radius = 18)
    {
        var img = UIKit.Panel(parent, name, color);
        img.sprite = Rounded(radius);
        img.type = Image.Type.Sliced;
        return img;
    }

    public static Image IconImage(Transform parent, Icon icon, Color color)
    {
        var img = UIKit.Panel(parent, icon.ToString(), color);
        img.sprite = Get(icon);
        img.preserveAspect = true;
        return img;
    }

    public static T Shadowed<T>(T g, float distance = 6f, float alpha = 0.55f) where T : Graphic
    {
        var s = g.gameObject.AddComponent<Shadow>();
        s.effectColor = new Color(0, 0, 0, alpha); s.effectDistance = new Vector2(0, -distance);
        return g;
    }

    // Styled button: rounded, with an optional icon, lift + glow on hover, click sound (from UIKit.Button).
    public static Button Button(Transform parent, string label, int size, System.Action onClick, Color baseColor, Icon? icon = null, TextAnchor align = TextAnchor.MiddleCenter)
    {
        var b = UIKit.Button(parent, label, size, onClick, align);
        var img = b.GetComponent<Image>();
        img.sprite = Rounded(12); img.type = Image.Type.Sliced;
        Print(img, 4f);
        var cb = b.colors;
        cb.normalColor = baseColor;
        cb.highlightedColor = cb.selectedColor = Color.Lerp(baseColor, Color.white, 0.25f);
        cb.pressedColor = Color.Lerp(baseColor, Color.white, 0.5f);
        b.colors = cb;
        var text = b.GetComponentInChildren<Text>();
        text.font = Display; text.fontStyle = FontStyle.Normal;
        bool light = baseColor.grayscale > 0.55f;                        // dark text on mustard / paper buttons
        text.color = light ? Theme.Ink : Theme.Paper;
        if (icon.HasValue)
        {
            var ic = IconImage(b.transform, icon.Value, light ? Theme.Ink : Theme.Paper);
            var rt = ic.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 0.5f);
            rt.sizeDelta = new Vector2(size * 1.1f, size * 1.1f);
            rt.anchoredPosition = new Vector2(22, 0);
            text.rectTransform.offsetMin = new Vector2(34 + size * 1.1f, 0);
        }
        b.gameObject.AddComponent<UIHover>();
        return b;
    }

    public static T PopIn<T>(T c, float delay) where T : Component
    {
        var p = c.gameObject.AddComponent<UIPop>(); p.delay = delay;
        return c;
    }

    // ---------- icons (signed-distance shapes, 96 px) ----------

    public static Sprite Get(Icon icon)
    {
        if (icons.TryGetValue(icon, out var s) && Alive(s)) return s;
        const int n = 96;
        var tex = NewTex(n, n);
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // p in -1..1, y up
                Vector2 p = new Vector2((x + 0.5f) / n * 2f - 1f, (y + 0.5f) / n * 2f - 1f);
                float d = Shape(icon, p);
                px[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(0.5f - d * n / 2f));
            }
        tex.SetPixels(px); tex.Apply();
        return icons[icon] = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
    }

    // Signed distance (negative inside) for each icon, built from circles, boxes and segments.
    static float Shape(Icon icon, Vector2 p)
    {
        switch (icon)
        {
            case Icon.Heart:
            {
                var q = new Vector2(Mathf.Abs(p.x), p.y + 0.1f);
                float lobes = Circle(q - new Vector2(0.32f, 0.28f), 0.36f);
                float tip = Poly(q, new Vector2(0, -0.78f), new Vector2(0.64f, 0.12f), new Vector2(0, 0.25f));
                return Mathf.Min(lobes, tip);
            }
            case Icon.Fist:
                return Mathf.Min(Box(p - new Vector2(0, -0.1f), new Vector2(0.55f, 0.45f), 0.18f),
                       Mathf.Min(Box(p - new Vector2(-0.62f, 0f), new Vector2(0.14f, 0.3f), 0.12f), Box(p - new Vector2(0.05f, 0.45f), new Vector2(0.5f, 0.16f), 0.14f)));
            case Icon.Crosshair:
                return Mathf.Min(Ring(p, 0.58f, 0.09f), Mathf.Max(Mathf.Min(Box(p, new Vector2(0.07f, 0.92f), 0.03f), Box(p, new Vector2(0.92f, 0.07f), 0.03f)), -Circle(p, 0.2f)));
            case Icon.Bolt:
                return Mathf.Min(Poly(p, new Vector2(0.15f, 0.95f), new Vector2(-0.5f, -0.05f), new Vector2(0.05f, -0.05f)),
                                 Poly(p, new Vector2(-0.15f, -0.95f), new Vector2(0.5f, 0.1f), new Vector2(-0.05f, 0.1f)));
            case Icon.Clover:
                return Mathf.Min(Mathf.Min(Circle(p - new Vector2(0, 0.36f), 0.35f), Circle(p - new Vector2(0.36f, 0), 0.35f)),
                       Mathf.Min(Mathf.Min(Circle(p - new Vector2(-0.36f, 0), 0.35f), Circle(p - new Vector2(0, -0.36f), 0.35f)), Box(p - new Vector2(0.3f, -0.7f), new Vector2(0.06f, 0.28f), 0.03f)));
            case Icon.Shield:
                return Mathf.Min(Box(p - new Vector2(0, 0.3f), new Vector2(0.65f, 0.5f), 0.12f), Poly(p, new Vector2(-0.65f, 0.1f), new Vector2(0.65f, 0.1f), new Vector2(0, -0.92f)));
            case Icon.Star:   // five-pointed star (Inigo Quilez's sdStar5)
            {
                Vector2 k1 = new Vector2(0.809017f, -0.587785f), k2 = new Vector2(-k1.x, k1.y);
                Vector2 q = new Vector2(Mathf.Abs(p.x), p.y + 0.08f);
                q -= 2f * Mathf.Max(Vector2.Dot(k1, q), 0f) * k1;
                q -= 2f * Mathf.Max(Vector2.Dot(k2, q), 0f) * k2;
                q.x = Mathf.Abs(q.x); q.y -= 0.9f;
                Vector2 ba = 0.45f * new Vector2(-k1.y, k1.x) - new Vector2(0, 1);
                float h = Mathf.Clamp(Vector2.Dot(q, ba) / Vector2.Dot(ba, ba), 0f, 0.9f);
                return (q - ba * h).magnitude * Mathf.Sign(q.y * ba.x - q.x * ba.y);
            }
            case Icon.Can:
                return Mathf.Min(Box(p, new Vector2(0.38f, 0.72f), 0.18f), Box(p - new Vector2(0, 0.82f), new Vector2(0.26f, 0.06f), 0.03f));
            case Icon.Glass:   // shot glass: a tapered cup
                return Mathf.Min(Poly(p, new Vector2(-0.55f, 0.75f), new Vector2(0.55f, 0.75f), new Vector2(0.35f, -0.8f)),
                                 Poly(p, new Vector2(-0.55f, 0.75f), new Vector2(0.35f, -0.8f), new Vector2(-0.35f, -0.8f)));
            case Icon.Drop:
                return Mathf.Min(Circle(p - new Vector2(0, -0.25f), 0.52f), Poly(p, new Vector2(0, 0.95f), new Vector2(0.46f, -0.02f), new Vector2(-0.46f, -0.02f)));
            case Icon.Bag:
                return Mathf.Min(Box(p - new Vector2(0, -0.15f), new Vector2(0.62f, 0.62f), 0.1f), Ring(p - new Vector2(0, 0.52f), 0.28f, 0.07f));
            case Icon.Drumstick:
                return Mathf.Min(Circle(p - new Vector2(0.18f, 0.18f), 0.55f), Mathf.Min(Segment(p, new Vector2(-0.2f, -0.2f), new Vector2(-0.62f, -0.62f), 0.13f), Circle(p - new Vector2(-0.72f, -0.62f), 0.14f)));
            case Icon.Box:
                return Mathf.Min(Poly(p, new Vector2(-0.8f, 0.2f), new Vector2(0.8f, 0.2f), new Vector2(0.55f, -0.8f)),
                       Mathf.Min(Poly(p, new Vector2(-0.8f, 0.2f), new Vector2(0.55f, -0.8f), new Vector2(-0.55f, -0.8f)), Box(p - new Vector2(0, 0.42f), new Vector2(0.72f, 0.14f), 0.06f)));
            case Icon.Backpack:
                return Mathf.Min(Box(p - new Vector2(0, -0.1f), new Vector2(0.55f, 0.75f), 0.3f), Ring(p - new Vector2(0, 0.72f), 0.2f, 0.06f));
            case Icon.Board:
                return Mathf.Min(Box(p - new Vector2(0, 0.12f), new Vector2(0.9f, 0.14f), 0.14f), Mathf.Min(Circle(p - new Vector2(-0.55f, -0.22f), 0.16f), Circle(p - new Vector2(0.55f, -0.22f), 0.16f)));
            case Icon.Book:
                return Mathf.Max(Box(p, new Vector2(0.6f, 0.78f), 0.08f), -Box(p - new Vector2(0.08f, 0.1f), new Vector2(0.34f, 0.08f), 0.02f));
            case Icon.Guitar:
                return Mathf.Min(Mathf.Min(Circle(p - new Vector2(-0.25f, -0.35f), 0.42f), Circle(p - new Vector2(0.05f, -0.02f), 0.3f)), Segment(p, new Vector2(0.1f, 0.1f), new Vector2(0.72f, 0.72f), 0.08f));
            case Icon.Cart:
                return Mathf.Min(Box(p - new Vector2(0, -0.15f), new Vector2(0.28f, 0.62f), 0.2f), Box(p - new Vector2(0, 0.62f), new Vector2(0.16f, 0.16f), 0.05f));
            case Icon.Burger:
                return Mathf.Min(Mathf.Min(Box(p - new Vector2(0, 0.3f), new Vector2(0.75f, 0.28f), 0.28f), Box(p - new Vector2(0, -0.08f), new Vector2(0.82f, 0.08f), 0.06f)), Box(p - new Vector2(0, -0.42f), new Vector2(0.75f, 0.16f), 0.14f));
            case Icon.Dice:
                return Mathf.Max(Box(p, new Vector2(0.72f, 0.72f), 0.18f), -Mathf.Min(Mathf.Min(Circle(p - new Vector2(-0.32f, 0.32f), 0.13f), Circle(p, 0.13f)), Circle(p - new Vector2(0.32f, -0.32f), 0.13f)));
            case Icon.Play:
                return Poly(p, new Vector2(-0.5f, 0.75f), new Vector2(0.75f, 0f), new Vector2(-0.5f, -0.75f));
            case Icon.Gear:
            {
                float a = Mathf.Atan2(p.y, p.x), r = p.magnitude;
                float teeth = 0.72f + 0.14f * Mathf.Clamp(Mathf.Cos(a * 8f) * 3f, -1f, 1f);
                return Mathf.Max(r - teeth, -(r - 0.26f));
            }
            case Icon.Gamepad:
                return Mathf.Max(Mathf.Min(Box(p, new Vector2(0.85f, 0.4f), 0.35f), Mathf.Min(Circle(p - new Vector2(-0.55f, -0.3f), 0.32f), Circle(p - new Vector2(0.55f, -0.3f), 0.32f))),
                                 -Mathf.Min(Box(p - new Vector2(-0.45f, 0.05f), new Vector2(0.2f, 0.06f), 0.02f), Box(p - new Vector2(-0.45f, 0.05f), new Vector2(0.06f, 0.2f), 0.02f)));
            case Icon.Cards:   // two playing cards, fanned
            {
                Vector2 R(Vector2 q, float deg) { float c = Mathf.Cos(deg * Mathf.Deg2Rad), s = Mathf.Sin(deg * Mathf.Deg2Rad); return new Vector2(c * q.x + s * q.y, -s * q.x + c * q.y); }
                return Mathf.Min(Box(R(p - new Vector2(-0.22f, -0.02f), 14f), new Vector2(0.4f, 0.58f), 0.08f),
                                 Mathf.Max(Box(R(p - new Vector2(0.2f, 0.05f), -12f), new Vector2(0.4f, 0.58f), 0.08f), -Circle(p - new Vector2(0.2f, 0.05f), 0.14f)));
            }
            case Icon.Chip:    // poker chip: disc with edge notches and an inner ring
            {
                float a = Mathf.Atan2(p.y, p.x);
                float notch = Mathf.Cos(a * 6f) > 0.6f ? Ring(p, 0.72f, 0.1f) : 9f;
                return Mathf.Max(Mathf.Max(Circle(p, 0.85f), -notch), -Ring(p, 0.45f, 0.05f));
            }
            case Icon.Crutch:  // long pole, arm pad on top, hand grip part way down
                return Mathf.Min(Segment(p, new Vector2(-0.45f, -0.85f), new Vector2(0.35f, 0.62f), 0.07f),
                       Mathf.Min(Segment(p, new Vector2(0.12f, 0.8f), new Vector2(0.62f, 0.52f), 0.11f), Segment(p, new Vector2(-0.12f, 0.02f), new Vector2(0.2f, 0.2f), 0.07f)));
            case Icon.Fish:    // cracker fish: oval body, tail, a smile cut out
                return Mathf.Max(Mathf.Min(Box(p - new Vector2(0.15f, 0f), new Vector2(0.55f, 0.34f), 0.32f), Poly(p, new Vector2(-0.3f, 0f), new Vector2(-0.88f, 0.42f), new Vector2(-0.88f, -0.42f))),
                                 -Circle(p - new Vector2(0.42f, 0.1f), 0.07f));
            case Icon.Bottle:  // beer bottle
                return Mathf.Min(Box(p - new Vector2(0, -0.3f), new Vector2(0.3f, 0.55f), 0.18f), Box(p - new Vector2(0, 0.52f), new Vector2(0.12f, 0.32f), 0.05f));
            default:   // Coin
                return Mathf.Max(Circle(p, 0.85f), -Ring(p, 0.6f, 0.05f));
        }
    }

    static float Circle(Vector2 p, float r) => p.magnitude - r;
    static float Ring(Vector2 p, float r, float w) => Mathf.Abs(p.magnitude - r) - w;
    static float Box(Vector2 p, Vector2 half, float round)
    {
        Vector2 d = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - half + new Vector2(round, round);
        return new Vector2(Mathf.Max(d.x, 0), Mathf.Max(d.y, 0)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0) - round;
    }
    static float Segment(Vector2 p, Vector2 a, Vector2 b, float r)
    {
        Vector2 pa = p - a, ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / ba.sqrMagnitude);
        return (pa - ba * h).magnitude - r;
    }
    // Triangle (any winding).
    static float Poly(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d = Mathf.Min(Mathf.Min(SegDist(p, a, b), SegDist(p, b, c)), SegDist(p, c, a));
        float s1 = Cross(b - a, p - a), s2 = Cross(c - b, p - b), s3 = Cross(a - c, p - c);
        bool inside = (s1 >= 0 && s2 >= 0 && s3 >= 0) || (s1 <= 0 && s2 <= 0 && s3 <= 0);
        return (inside ? -d : d) - 0.008f;           // a hair bigger so shapes made of two triangles have no seam
    }
    static float SegDist(Vector2 p, Vector2 a, Vector2 b) => Segment(p, a, b, 0f);
    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
}
