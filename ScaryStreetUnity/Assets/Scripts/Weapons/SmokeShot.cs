using System.Collections.Generic;
using UnityEngine;

// A cloud of smoke from the cart: flies forward, swells, and hurts every enemy it passes through (once each)
// until it runs out of pierce. Stops when it hits a wall. The object itself is an invisible hitbox; the smoke you
// see is SmokeFx particles riding on it, which are let go to drift and fade when the shot ends.
// Numbers from the web build's spawnShot(): puff, O-ring and the Blinker blast.
public class SmokeShot : MonoBehaviour
{
    public enum Kind { Puff, Ring, Blast }

    Kind kind;
    Vector3 dir;
    float speed, life, damage, radius, grow, age;
    int pierce;
    ParticleSystem fx;
    GameObject owner;
    readonly HashSet<Health> hit = new HashSet<Health>();
    static readonly Collider[] overlap = new Collider[32];

    public static SmokeShot Spawn(Kind kind, Vector3 pos, Vector3 dir, GameObject owner, Material template, float damageMultiplier = 1f)
    {
        var go = new GameObject(kind == Kind.Ring ? "SmokeRing" : kind == Kind.Blast ? "Blinker" : "SmokePuff");
        go.transform.position = pos;

        var s = go.AddComponent<SmokeShot>();
        s.kind = kind; s.dir = dir.normalized; s.owner = owner;
        switch (kind)
        {
            case Kind.Puff:  s.speed = 9f;  s.life = 0.95f; s.damage = 10f; s.radius = 0.35f; s.grow = 1.6f; s.pierce = 99; break;
            case Kind.Ring:  s.speed = 17f; s.life = 1.1f;  s.damage = 16f; s.radius = 0.4f;  s.grow = 0.9f; s.pierce = 3;  break;
            default:         s.speed = 8f;  s.life = 1.3f;  s.damage = 60f; s.radius = 0.8f;  s.grow = 4.2f; s.pierce = 999; break;
        }
        s.damage *= damageMultiplier;
        s.fx = SmokeFx.Attach(go.transform, kind, s.dir, template);
        return s;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        float k = age / life;
        if (k >= 1f) { Finish(); return; }

        // move, but stop at walls (anything solid that isn't something we can hurt)
        float step = speed * dt;
        if (step > 0 && Physics.Raycast(transform.position, dir, out var wall, step + radius * 0.5f, ~0, QueryTriggerInteraction.Ignore)
            && !wall.collider.GetComponentInParent<Health>())
        {
            speed = 0;
            life = Mathf.Min(life, age + 0.25f);
        }
        transform.position += dir * step;
        speed *= 1f - 0.9f * dt;                                       // smoke slows as it spreads

        float swell = 1f + grow * k;

        int n = Physics.OverlapSphereNonAlloc(transform.position, radius * swell, overlap, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var h = overlap[i].GetComponentInParent<Health>();
            if (!h || h.IsDead || h.gameObject == owner || !hit.Add(h)) continue;
            h.TakeDamage(damage);
            if (--pierce < 0) { Finish(); return; }
        }
    }

    // Let the smoke keep drifting and fading after the hitbox is gone.
    void Finish()
    {
        SmokeFx.Release(fx);
        fx = null;
        Destroy(gameObject);
    }

    // Transparent URP Lit made in code (FartCloud uses it).
    static Material defaultSmoke;
    public static Material DefaultSmoke()
    {
        if (defaultSmoke) return defaultSmoke;
        defaultSmoke = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        MakeTransparent(defaultSmoke);
        return defaultSmoke;
    }

    public static void MakeTransparent(Material m)
    {
        m.SetFloat("_Surface", 1f);       // Transparent
        m.SetFloat("_Blend", 0f);         // Alpha
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_Smoothness", 0.1f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }
}
