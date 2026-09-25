using System.Collections.Generic;
using UnityEngine;

// A cloud of smoke from the cart: flies forward, swells and fades, and hurts every enemy it passes
// through (once each) until it runs out of pierce. Stops and thins out when it hits a wall.
// Numbers from the web build's spawnShot(): puff, O-ring and the Blinker blast.
public class SmokeShot : MonoBehaviour
{
    public enum Kind { Puff, Ring, Blast }

    Kind kind;
    Vector3 dir;
    float speed, life, damage, radius, grow, age;
    int pierce;
    Vector3 baseScale;
    Material mat;
    Color baseColor;
    GameObject owner;
    readonly HashSet<Health> hit = new HashSet<Health>();
    static readonly Collider[] overlap = new Collider[32];

    public static SmokeShot Spawn(Kind kind, Vector3 pos, Vector3 dir, GameObject owner, Material template)
    {
        GameObject go;
        if (kind == Kind.Ring)
        {
            go = new GameObject("SmokeRing", typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = MeshKit.Torus(0.3f);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>());
            go.name = kind == Kind.Blast ? "Blinker" : "SmokePuff";
        }
        go.transform.position = pos;

        var s = go.AddComponent<SmokeShot>();
        s.kind = kind; s.dir = dir.normalized; s.owner = owner;
        switch (kind)
        {
            case Kind.Puff:  s.speed = 9f;  s.life = 0.95f; s.damage = 10f; s.radius = 0.35f; s.grow = 1.6f; s.pierce = 99; break;
            case Kind.Ring:  s.speed = 17f; s.life = 1.1f;  s.damage = 16f; s.radius = 0.4f;  s.grow = 0.9f; s.pierce = 3;  break;
            default:         s.speed = 8f;  s.life = 1.3f;  s.damage = 60f; s.radius = 0.8f;  s.grow = 4.2f; s.pierce = 999; break;
        }
        s.baseScale = kind == Kind.Ring ? new Vector3(0.52f, 0.52f, 0.52f) : Vector3.one * (s.radius * 1.4f);
        go.transform.localScale = s.baseScale;
        if (kind == Kind.Ring) go.transform.rotation = Quaternion.FromToRotation(Vector3.up, s.dir);   // ring faces where it's going

        s.baseColor = kind == Kind.Blast ? new Color(0.84f, 0.96f, 0.87f, 0.8f) : new Color(0.93f, 0.94f, 0.96f, 0.85f);
        s.mat = new Material(template ? template : DefaultSmoke()) { color = s.baseColor };
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = s.mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return s;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        float k = age / life;
        if (k >= 1f) { Destroy(gameObject); return; }

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
        transform.localScale = baseScale * swell;
        if (kind == Kind.Ring) transform.Rotate(Vector3.up, 90f * dt, Space.Self);
        mat.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * (1f - k));

        int n = Physics.OverlapSphereNonAlloc(transform.position, radius * swell, overlap, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var h = overlap[i].GetComponentInParent<Health>();
            if (!h || h.IsDead || h.gameObject == owner || !hit.Add(h)) continue;
            h.TakeDamage(damage);
            if (--pierce < 0) { Destroy(gameObject); return; }
        }
    }

    void OnDestroy() { if (mat) Destroy(mat); }

    // Transparent URP Lit made in code, for when no smoke material was assigned.
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
