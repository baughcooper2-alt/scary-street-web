using UnityEngine;

// Jack's gas: a green cloud that swells up, hangs around, and hurts any player standing in it.
// Web build: grows to 1.6 m across over about a second, 6 s life (3.5 s for the Crop Dust trail).
public class FartCloud : MonoBehaviour
{
    float life, age, damagePerSecond, maxRadius;
    Material mat;
    static readonly Color Green = new Color(0.56f, 0.75f, 0.23f, 0.45f);

    public static void Spawn(Vector3 pos, float life, float damagePerSecond, float maxRadius = 1.6f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(go.GetComponent<Collider>());
        go.name = "FartCloud";
        go.transform.position = pos + Vector3.up * 0.6f;
        go.transform.localScale = Vector3.one * 0.6f;
        var c = go.AddComponent<FartCloud>();
        c.life = life; c.damagePerSecond = damagePerSecond; c.maxRadius = maxRadius;
        c.mat = new Material(SmokeShot.DefaultSmoke()) { color = Green };
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = c.mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if ((age += dt) >= life) { Destroy(gameObject); return; }

        float radius = Mathf.Min(maxRadius, 0.3f + age * 1.5f);
        transform.localScale = Vector3.one * radius * 2f;
        transform.position += Vector3.up * Mathf.Sin(age * 2f) * 0.002f;
        mat.color = new Color(Green.r, Green.g, Green.b, Green.a * Mathf.Min(1f, (life - age) / 1.5f));

        foreach (var p in Players.All)
        {
            if (!p) continue;
            Vector3 d = p.transform.position - transform.position;
            if (Mathf.Abs(d.y + 0.6f) > 1.5f || new Vector2(d.x, d.z).magnitude > radius) continue;
            var h = p.GetComponent<Health>();
            if (h && !h.IsDead) h.TakeDamage(damagePerSecond * dt);
        }
    }

    void OnDestroy() { if (mat) Destroy(mat); }
}
