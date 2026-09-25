using UnityEngine;
using UnityEngine.UI;

// Floating HP bar over an enemy's head. Builds its own world-space canvas at runtime,
// so there's nothing to wire up: just add it next to a Health component.
[RequireComponent(typeof(Health))]
public class WorldHealthBar : MonoBehaviour
{
    public float heightAboveRoot = 2.15f;
    public Vector2 size = new Vector2(0.8f, 0.09f);
    public Color fillColor = new Color(0.9f, 0.15f, 0.12f);
    public Color backColor = new Color(0, 0, 0, 0.65f);
    [Tooltip("Hide the bar until the enemy has been hit.")]
    public bool hideWhenFull = true;

    Health health;
    Transform bar, cam;
    RectTransform fill;
    float shown = 1f;

    void Awake()
    {
        health = GetComponent<Health>();

        var go = new GameObject("HealthBar", typeof(Canvas));
        bar = go.transform;
        bar.SetParent(transform, false);
        bar.localPosition = Vector3.up * heightAboveRoot;
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;
        var rt = (RectTransform)bar;
        rt.sizeDelta = size * 100f;          // work in "pixels", then scale down to meters
        rt.localScale = Vector3.one * 0.01f;

        MakeImage("Back", rt, backColor);
        fill = MakeImage("Fill", rt, fillColor);
        fill.pivot = new Vector2(0, 0.5f);
        fill.anchorMin = new Vector2(0, 0); fill.anchorMax = new Vector2(1, 1);
        fill.offsetMin = new Vector2(2, 2); fill.offsetMax = new Vector2(-2, -2);
    }

    static RectTransform MakeImage(string name, RectTransform parent, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = c;
        img.raycastTarget = false;
        return rt;
    }

    void LateUpdate()
    {
        if (!cam && Camera.main) cam = Camera.main.transform;
        bool visible = !health.IsDead && !(hideWhenFull && health.Fraction >= 0.999f);
        if (bar.gameObject.activeSelf != visible) bar.gameObject.SetActive(visible);
        if (!visible || !cam) return;

        shown = Mathf.MoveTowards(shown, health.Fraction, Time.deltaTime * 2f);
        fill.localScale = new Vector3(shown, 1, 1);
        bar.rotation = Quaternion.LookRotation(bar.position - cam.position);   // face the camera
    }
}
