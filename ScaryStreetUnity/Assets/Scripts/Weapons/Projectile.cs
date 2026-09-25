using System.Collections.Generic;
using UnityEngine;

// A thrown thing (card, poker chip, goldfish cracker, the goldfish box, a beer bottle): flies with optional gravity
// and spin, hurts enemies it touches, can go through a few (pierce) or bounce on to the next one (chips), and can
// burst when it lands (splash damage, stun). Stops at walls. Build the look as children of the returned object.
public class Projectile : MonoBehaviour
{
    public float damage, radius = 0.2f, gravity, life = 2f, knockback;
    public int pierce;                       // extra enemies it can go through
    public int bounces;                      // hops to the nearest other enemy after a hit (poker chips)
    public float bounceRange = 5f;
    public float blastRadius, blastDamage, stunSeconds;
    public Vector3 spinAxis = Vector3.up; public float spin;
    public Sfx hitSound = Sfx.Hit, burstSound = Sfx.Splat;
    public bool burstOnWall;                 // bottles and the goldfish box break when they land
    public System.Action<Vector3> onBurst;

    Vector3 velocity;
    GameObject owner;
    float age;
    readonly HashSet<Health> hit = new HashSet<Health>();
    static readonly Collider[] overlap = new Collider[24];

    public static Projectile Spawn(string name, Vector3 pos, Vector3 velocity, GameObject owner)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.LookRotation(velocity.sqrMagnitude > 1e-4f ? velocity : Vector3.forward);
        var p = go.AddComponent<Projectile>();
        p.velocity = velocity; p.owner = owner;
        return p;
    }

    // A primitive piece of the projectile's look (no collider, no shadow).
    public Transform Piece(PrimitiveType type, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        var t = go.transform;
        t.SetParent(transform, false);
        t.localPosition = pos; t.localScale = scale; t.localRotation = Quaternion.Euler(euler);
        var r = go.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return t;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if ((age += dt) >= life) { if (blastRadius > 0) Burst(); Destroy(gameObject); return; }
        velocity += Vector3.down * gravity * dt;
        Vector3 step = velocity * dt;
        if (Physics.Raycast(transform.position, step.normalized, out var wall, step.magnitude + 0.05f, ~0, QueryTriggerInteraction.Ignore)
            && !wall.collider.GetComponentInParent<Health>())
        {
            transform.position = wall.point - step.normalized * 0.05f;
            if (blastRadius > 0 || burstOnWall) Burst();
            Destroy(gameObject);
            return;
        }
        transform.position += step;
        if (spin != 0) transform.Rotate(spinAxis, spin * dt, Space.Self);

        if (damage <= 0) return;                                          // just for show (spilled crackers)
        int n = Physics.OverlapSphereNonAlloc(transform.position, radius, overlap, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var h = overlap[i].GetComponentInParent<Health>();
            if (!h || h.IsDead || (owner && h.gameObject == owner) || h.GetComponent<FirstPersonController>() || !hit.Add(h)) continue;
            h.TakeDamage(damage, knockback);
            SoundKit.PlayAt(hitSound, transform.position, 0.6f);
            if (blastRadius > 0) { Burst(); Destroy(gameObject); return; }
            if (bounces > 0)
            {
                var next = EnemyTargets.Nearest(transform.position, bounceRange, hit);
                if (next)
                {
                    bounces--;
                    velocity = (next.transform.position + Vector3.up * 1.1f - transform.position).normalized * velocity.magnitude;
                    age = 0;
                    return;
                }
            }
            if (--pierce < 0) { Destroy(gameObject); return; }
        }
    }

    void Burst()
    {
        SoundKit.PlayAt(burstSound, transform.position, 0.8f);
        onBurst?.Invoke(transform.position);
        if (blastRadius <= 0) return;
        int n = Physics.OverlapSphereNonAlloc(transform.position, blastRadius, overlap, ~0, QueryTriggerInteraction.Ignore);
        var done = new HashSet<Health>();
        for (int i = 0; i < n; i++)
        {
            var h = overlap[i].GetComponentInParent<Health>();
            if (!h || h.IsDead || (owner && h.gameObject == owner) || h.GetComponent<FirstPersonController>() || !done.Add(h)) continue;
            h.TakeDamage(blastDamage, knockback);
            if (stunSeconds > 0) { var w = h.GetComponent<McDonaldsWorker>(); if (w) w.Stun(stunSeconds); }
        }
    }
}

// Living enemies, for weapons that look for a target.
public static class EnemyTargets
{
    public static Health Nearest(Vector3 from, float range, HashSet<Health> skip = null)
    {
        Health best = null; float bestD = range * range;
        foreach (var w in McDonaldsWorker.All)
        {
            if (!w || !w.IsAlive) continue;
            var h = w.GetComponent<Health>();
            if (skip != null && skip.Contains(h)) continue;
            float d = (w.transform.position - from).sqrMagnitude;
            if (d < bestD) { bestD = d; best = h; }
        }
        var jack = JackBoss.Current;
        if (jack && !jack.Health.IsDead && (skip == null || !skip.Contains(jack.Health)) && (jack.transform.position - from).sqrMagnitude < bestD) best = jack.Health;
        return best;
    }

    // Everything with Health within `radius` of `center` except players (melee weapons).
    public static List<Health> Around(Vector3 center, float radius)
    {
        var list = new List<Health>();
        var cols = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore);
        foreach (var c in cols)
        {
            var h = c.GetComponentInParent<Health>();
            if (h && !h.IsDead && !h.GetComponent<FirstPersonController>() && !list.Contains(h)) list.Add(h);
        }
        return list;
    }
}
