using UnityEngine;
using UnityEngine.EventSystems;

// Small UI animations (unscaled time, so they run on paused screens too).

// Scales and fades in after `delay` seconds, with a little overshoot.
public class UIPop : MonoBehaviour
{
    public float delay, duration = 0.28f, from = 0.82f;
    CanvasGroup group; float t;

    void OnEnable()
    {
        t = -delay;
        group = GetComponent<CanvasGroup>(); if (!group) group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0; transform.localScale = Vector3.one * from;
    }

    void Update()
    {
        t += Time.unscaledDeltaTime;
        float p = Mathf.Clamp01(t / duration);
        float back = 1f + 2.2f * Mathf.Pow(p - 1f, 3) + 1.2f * Mathf.Pow(p - 1f, 2);   // ease-out-back
        group.alpha = p;
        transform.localScale = Vector3.one * Mathf.LerpUnclamped(from, 1f, back);
        if (p >= 1f) { transform.localScale = Vector3.one; enabled = false; }
    }
}

// Lifts (scales up) while pointed at or selected with keys / controller.
public class UIHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    public float lift = 1.06f;
    bool pointer, selected;
    UIPop pop;

    void Awake() => pop = GetComponent<UIPop>();
    public void OnPointerEnter(PointerEventData e) => pointer = true;
    public void OnPointerExit(PointerEventData e) => pointer = false;
    public void OnSelect(BaseEventData e) => selected = true;
    public void OnDeselect(BaseEventData e) => selected = false;

    void Update()
    {
        if (pop && pop.enabled) return;
        float target = pointer || selected ? lift : 1f;
        transform.localScale = Vector3.one * Mathf.MoveTowards(transform.localScale.x, target, Time.unscaledDeltaTime * 1.2f);
    }
}

// Spins slowly (the level-up starburst).
public class UISpin : MonoBehaviour
{
    public float degreesPerSecond = 12f;
    void Update() => transform.Rotate(0, 0, degreesPerSecond * Time.unscaledDeltaTime);
}

// Gentle breathing scale (titles).
public class UIPulse : MonoBehaviour
{
    public float amount = 0.025f, speed = 1.4f;
    void Update() => transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * speed) * amount);
}
