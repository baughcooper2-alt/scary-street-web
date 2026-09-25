using UnityEngine;
using UnityEngine.UI;

// One of Jack's dumb jokes: a speech bubble that drifts at the player (web build: 3.2 m/s, 4 s).
// If it reaches you it stuns you briefly, then slows you (DESIGN.md: "dumb jokes that stun you").
public class JokeBubble : MonoBehaviour
{
    Vector3 velocity;
    float damage, stun, slow, age;
    const float Life = 4f;
    CanvasGroup fade;

    public static void Spawn(Vector3 pos, Vector3 target, string joke, float damage, float stun, float slow)
    {
        var go = new GameObject("Joke", typeof(Canvas), typeof(CanvasGroup));
        go.transform.position = pos;
        var b = go.AddComponent<JokeBubble>();
        Vector3 dir = target - pos; dir.y = 0;
        b.velocity = dir.normalized * 3.2f;
        b.damage = damage; b.stun = stun; b.slow = slow;
        b.fade = go.GetComponent<CanvasGroup>();

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(420, 110);
        rt.localScale = Vector3.one * 0.0045f;                         // about 1.9 m wide
        var bubble = UIKit.Panel(rt, "Bubble", new Color(1, 1, 1, 0.93f));
        UIKit.Fill(bubble.rectTransform);
        var tail = UIKit.Panel(rt, "Tail", new Color(1, 1, 1, 0.93f));
        tail.rectTransform.anchorMin = tail.rectTransform.anchorMax = new Vector2(0.3f, 0);
        tail.rectTransform.sizeDelta = new Vector2(30, 30);
        tail.rectTransform.anchoredPosition = new Vector2(0, -8);
        tail.rectTransform.localRotation = Quaternion.Euler(0, 0, 45f);
        var text = UIKit.Label(rt, joke, 30, new Color(0.23f, 0.16f, 0.1f), TextAnchor.MiddleCenter, FontStyle.Bold);
        UIKit.Fill(text.rectTransform, 10);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if ((age += dt) > Life) { Destroy(gameObject); return; }
        fade.alpha = Mathf.Min(1f, 3f * (Life - age));

        Vector3 step = velocity * dt;
        if (Physics.Raycast(transform.position, velocity.normalized, out var wall, step.magnitude + 0.3f, ~0, QueryTriggerInteraction.Ignore)
            && !wall.collider.GetComponentInParent<FirstPersonController>() && !wall.collider.GetComponentInParent<JackBoss>())
        { Destroy(gameObject); return; }                               // a wall ate the joke
        transform.position += step;
        if (Camera.main) transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);

        foreach (var p in Players.All)
        {
            if (!p) continue;
            Vector3 d = transform.position - p.transform.position;
            if (new Vector2(d.x, d.z).magnitude > 0.7f || d.y < 0 || d.y > 2.6f) continue;
            var health = p.GetComponent<Health>();
            if (health && !health.IsDead)
            {
                health.TakeDamage(damage);
                p.stunnedUntil = Time.time + stun;
                p.slowedUntil = Time.time + stun + slow;
                var inv = p.GetComponent<WeaponInventory>();
                if (inv) inv.Toast("Cringe… that joke stunned you", 1.4f);
            }
            Destroy(gameObject);
            return;
        }
    }
}
