using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

// Tiny helpers for building uGUI screens in code (no prefabs or TextMeshPro setup needed).
// Positions use a 1920×1080 layout measured from the top-left corner; the CanvasScaler fits it to any screen.
public static class UIKit
{
    public static readonly Color Gold = UIArt.Theme.Mustard;
    public static readonly Color Blood = UIArt.Theme.Blood;
    public static readonly Color Ink = new Color(0.05f, 0.03f, 0.035f);
    public static readonly Color Dim = new Color(1f, 1f, 1f, 0.55f);

    static Font font;
    public static Font Font => font ? font : font = (UIArt.Body ? UIArt.Body : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

    public static Canvas MakeCanvas(string name, int order)
    {
        EnsureEventSystem();
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    public static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>()) return;
#if ENABLE_INPUT_SYSTEM
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
#else
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
#endif
    }

    // With a controller something has to stay selected or the stick can't move around the menu
    // (clicking empty space with the mouse clears the selection). Call from a screen's Update.
    public static void KeepSelected(Selectable fallback)
    {
        var es = EventSystem.current;
        if (!es || !fallback || !GamepadInfo.UsingGamepad) return;
        var current = es.currentSelectedGameObject;
        if (!current || !current.activeInHierarchy) es.SetSelectedGameObject(fallback.gameObject);
    }

    // ---------- layout ----------

    public static RectTransform Node(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    // Top-left based placement in the 1920×1080 layout (or in the parent's own pixels).
    public static RectTransform Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    public static RectTransform Fill(RectTransform rt, float inset = 0)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
        return rt;
    }

    // ---------- widgets ----------

    public static Image Panel(Transform parent, string name, Color color)
    {
        var img = Node(name, parent).gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
    {
        var t = Node("Label", parent).gameObject.AddComponent<Text>();
        t.font = Font; t.text = text; t.fontSize = size; t.color = color;
        t.alignment = align; t.fontStyle = style;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    public static T Outlined<T>(T g, Color c, float px = 3f) where T : Graphic
    {
        var o = g.gameObject.AddComponent<Outline>(); o.effectColor = c; o.effectDistance = new Vector2(px, -px);
        var s = g.gameObject.AddComponent<Shadow>(); s.effectColor = new Color(0, 0, 0, 0.8f); s.effectDistance = new Vector2(px * 2, -px * 2);
        return g;
    }

    public static Button Button(Transform parent, string label, int size, Action onClick, TextAnchor align = TextAnchor.MiddleCenter)
    {
        var rt = Node(label, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = Color.white;
        var b = rt.gameObject.AddComponent<Button>();
        var cb = b.colors;
        cb.normalColor = UIArt.Theme.Ink2;
        cb.highlightedColor = UIArt.Theme.Ink3;
        cb.selectedColor = UIArt.Theme.Ink3;
        cb.pressedColor = Gold;
        cb.disabledColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);
        cb.fadeDuration = 0.08f;
        b.colors = cb;
        b.onClick.AddListener(() => SoundKit.Play(Sfx.Click, 0.35f, 0f));
        if (onClick != null) b.onClick.AddListener(() => onClick());
        var t = Label(rt, label, size, Color.white, align, FontStyle.Bold);
        Fill(t.rectTransform, 0);
        if (align == TextAnchor.MiddleLeft) t.rectTransform.offsetMin = new Vector2(28, 0);
        return b;
    }

    public static Slider Slider(Transform parent, float min, float max, float value, Action<float> onChange)
    {
        var go = DefaultControls.CreateSlider(new DefaultControls.Resources());
        go.transform.SetParent(parent, false);
        var s = go.GetComponent<Slider>();
        s.minValue = min; s.maxValue = max; s.value = value;
        s.onValueChanged.AddListener(v => onChange(v));
        // plain colored bars instead of Unity's default sprites
        foreach (var img in go.GetComponentsInChildren<Image>()) img.color = new Color(1, 1, 1, 0.25f);
        s.fillRect.GetComponent<Image>().color = Blood;
        s.handleRect.GetComponent<Image>().color = Gold;
        s.handleRect.sizeDelta = new Vector2(24, 0);
        return s;
    }

    public static Toggle Toggle(Transform parent, bool value, Action<bool> onChange)
    {
        var go = DefaultControls.CreateToggle(new DefaultControls.Resources());
        go.transform.SetParent(parent, false);
        var tg = go.GetComponent<Toggle>();
        tg.isOn = value;
        tg.onValueChanged.AddListener(v => onChange(v));
        foreach (var txt in go.GetComponentsInChildren<Text>()) txt.text = "";
        var imgs = go.GetComponentsInChildren<Image>();
        imgs[0].color = new Color(1, 1, 1, 0.25f);               // box
        if (imgs.Length > 1) imgs[1].color = Gold;                // check
        var box = (RectTransform)imgs[0].transform; box.sizeDelta = new Vector2(36, 36);
        if (imgs.Length > 1) ((RectTransform)imgs[1].transform).sizeDelta = new Vector2(26, 26);
        return tg;
    }
}
