using System.Collections.Generic;
using UnityEngine;

// Cash bill or XP gem dropped by enemies. Pops out, settles and bobs on the floor, then flies
// to the player when they get close (web build: 2.4 m magnet, collected at 0.55 m).
// Builds its own look, so no prefab is needed: Pickup.Spawn(kind, amount, position).
public class Pickup : MonoBehaviour
{
    public enum Kind { Cash, Xp }

    public Kind kind;
    public int amount = 1;
    public float magnetRadius = 2.4f;
    public float collectRadius = 0.55f;

    static readonly List<Pickup> all = new List<Pickup>();
    static Transform player;
    static PlayerProgress progress;
    static Material cashMat, xpMat;
    static float lastSound;

    Transform visual;
    Vector3 velocity;
    float floorY, t;
    bool landed, vacuum;

    public static Pickup Spawn(Kind kind, int amount, Vector3 at)
    {
        var go = new GameObject(kind == Kind.Cash ? "Cash" : "XP");
        go.transform.position = at + Vector3.up * 0.9f;
        var p = go.AddComponent<Pickup>();
        p.kind = kind; p.amount = amount;
        p.floorY = at.y + 0.25f;
        float a = Random.value * Mathf.PI * 2f, s = 1f + Random.value * 1.5f;
        p.velocity = new Vector3(Mathf.Cos(a) * s, 3f + Random.value * 2f, Mathf.Sin(a) * s);
        p.t = Random.value * 6f;
        p.BuildVisual();
        return p;
    }

    // Round over: everything on the floor flies to the player.
    public static void VacuumAll() { foreach (var p in all) p.vacuum = true; }

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    void BuildVisual()
    {
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (!cashMat) cashMat = new Material(unlit) { color = new Color(0.33f, 0.72f, 0.3f) };
        if (!xpMat) xpMat = new Material(unlit) { color = new Color(0.35f, 0.75f, 1f) };

        var prim = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(prim.GetComponent<Collider>());
        visual = prim.transform;
        visual.SetParent(transform, false);
        var r = prim.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (kind == Kind.Cash)
        {
            r.sharedMaterial = cashMat;
            visual.localScale = new Vector3(0.24f, 0.015f, 0.11f);          // a folded bill
        }
        else
        {
            r.sharedMaterial = xpMat;
            visual.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            visual.localRotation = Quaternion.Euler(45f, 0f, 45f);           // a little diamond
        }
    }

    void Update()
    {
        t += Time.deltaTime;
        if (!player) FindPlayer();

        Vector3 target = player ? player.position + Vector3.up * 0.9f : transform.position;
        float dist = Vector3.Distance(transform.position, target);

        if (player && (vacuum || dist < magnetRadius))
        {
            float speed = vacuum ? 18f : 6f + 14f * (1f - dist / magnetRadius);
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            if (dist < collectRadius) Collect();
        }
        else if (!landed)
        {
            // little pop out of the enemy; stop sideways movement at walls
            velocity.y -= 14f * Time.deltaTime;
            Vector3 flat = new Vector3(velocity.x, 0, velocity.z) * Time.deltaTime;
            if (Physics.Raycast(transform.position, flat.normalized, flat.magnitude + 0.15f, ~0, QueryTriggerInteraction.Ignore))
                velocity.x = velocity.z = 0;
            transform.position += velocity * Time.deltaTime;
            if (transform.position.y <= floorY && velocity.y < 0)
            {
                transform.position = new Vector3(transform.position.x, floorY, transform.position.z);
                landed = true;
            }
        }

        // bob and spin
        visual.localPosition = landed ? Vector3.up * Mathf.Sin(t * 3f) * 0.06f : Vector3.zero;
        transform.Rotate(0f, 120f * Time.deltaTime, 0f, Space.World);
    }

    void Collect()
    {
        if (progress)
        {
            if (kind == Kind.Cash) progress.AddCash(amount);
            else progress.AddXp(amount);
        }
        if (Time.time - lastSound > 0.06f) { lastSound = Time.time; SoundKit.Play(kind == Kind.Cash ? Sfx.Cash : Sfx.Xp, kind == Kind.Cash ? 0.5f : 0.3f, 0.1f); }
        Destroy(gameObject);
    }

    static void FindPlayer()
    {
        var go = GameObject.FindWithTag("Player");
        if (!go) return;
        player = go.transform;
        progress = go.GetComponent<PlayerProgress>();
    }
}
