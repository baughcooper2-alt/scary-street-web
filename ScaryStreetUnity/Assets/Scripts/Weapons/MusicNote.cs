using UnityEngine;

// A music note from Nathan's guitar: floats forward with a little bob, curves toward the nearest worker
// within 14 m (web build: homing 5), and pops on the first enemy it touches. Stops at walls.
public class MusicNote : MonoBehaviour
{
    const float SeekRange = 14f;

    Vector3 velocity;
    float damage, life, homing, age, retargetT;
    GameObject owner;
    Transform target;
    static Material gold, teal;
    static readonly Collider[] overlap = new Collider[16];

    public static void Spawn(Vector3 pos, Vector3 dir, float damage, float speed, float life, float homing, GameObject owner, int variant)
    {
        var go = new GameObject("MusicNote");
        go.transform.position = pos;
        var n = go.AddComponent<MusicNote>();
        n.velocity = dir.normalized * speed; n.damage = damage; n.life = life; n.homing = homing; n.owner = owner;

        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (!gold) gold = new Material(unlit) { color = new Color(1f, 0.82f, 0.3f) };
        if (!teal) teal = new Material(unlit) { color = new Color(0.4f, 0.95f, 0.9f) };
        var m = variant % 2 == 0 ? gold : teal;
        Piece(PrimitiveType.Sphere, go.transform, new Vector3(0, 0, 0), new Vector3(0.1f, 0.075f, 0.06f), new Vector3(0, 0, 20f), m);       // head
        Piece(PrimitiveType.Cube, go.transform, new Vector3(0.042f, 0.085f, 0), new Vector3(0.016f, 0.17f, 0.016f), Vector3.zero, m);       // stem
        Piece(PrimitiveType.Cube, go.transform, new Vector3(0.065f, 0.15f, 0), new Vector3(0.05f, 0.018f, 0.016f), new Vector3(0, 0, -35f), m);   // flag
    }

    static void Piece(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Vector3 euler, Material m)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        var t = go.transform;
        t.SetParent(parent, false);
        t.localPosition = pos; t.localScale = scale; t.localRotation = Quaternion.Euler(euler);
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if ((age += dt) >= life) { Destroy(gameObject); return; }

        if ((retargetT -= dt) <= 0) { retargetT = 0.1f; target = Nearest(); }
        if (target)
        {
            Vector3 want = (target.position + Vector3.up * 1.2f - transform.position).normalized * velocity.magnitude;
            velocity = Vector3.Lerp(velocity, want, Mathf.Min(1f, dt * homing));
        }

        Vector3 step = velocity * dt;
        if (Physics.Raycast(transform.position, step.normalized, out var hit, step.magnitude + 0.05f, ~0, QueryTriggerInteraction.Ignore)
            && !hit.collider.GetComponentInParent<Health>()) { Destroy(gameObject); return; }
        transform.position += step + Vector3.up * Mathf.Sin(age * 14f) * 0.004f;
        transform.Rotate(0, 180f * dt, 0, Space.World);

        int n = Physics.OverlapSphereNonAlloc(transform.position, 0.35f, overlap, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var h = overlap[i].GetComponentInParent<Health>();
            if (!h || h.IsDead || h.gameObject == owner) continue;
            h.TakeDamage(damage);
            SoundKit.PlayAt(Sfx.Ding, transform.position, 0.5f, 0.2f);
            Destroy(gameObject);
            return;
        }
    }

    Transform Nearest()
    {
        Transform best = null;
        float bestD = SeekRange * SeekRange;
        foreach (var w in McDonaldsWorker.All)
        {
            if (!w || !w.IsAlive) continue;
            float d = (w.transform.position - transform.position).sqrMagnitude;
            if (d < bestD) { bestD = d; best = w.transform; }
        }
        var jack = JackBoss.Current;                                   // bosses count too
        if (jack && !jack.Health.IsDead && (jack.transform.position - transform.position).sqrMagnitude < bestD) best = jack.transform;
        return best;
    }
}
