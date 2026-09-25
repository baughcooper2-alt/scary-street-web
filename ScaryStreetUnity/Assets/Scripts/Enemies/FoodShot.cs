using UnityEngine;

// Food thrown by McDonald's L3 workers (DESIGN.md: burgers, fries, sodas). Lobbed in an arc at where the
// player is (web build: gravity 5), tumbling, and hurts the first player it reaches. Splats on walls.
public class FoodShot : MonoBehaviour
{
    public enum Food { Burger, Fries, Soda }

    const float Gravity = 5f, Speed = 9f, Life = 3f;

    Vector3 velocity;
    float damage, age;
    GameObject owner;

    public static void Throw(Vector3 from, Vector3 target, float damage, GameObject owner)
    {
        var go = new GameObject("ThrownFood");
        go.transform.position = from;
        var f = go.AddComponent<FoodShot>();
        f.damage = damage; f.owner = owner;

        // aim: fixed sideways speed, pick the upward speed that lands it on the target
        Vector3 flat = target - from; float dy = flat.y; flat.y = 0;
        float t = Mathf.Max(0.25f, flat.magnitude / Speed);
        f.velocity = flat.normalized * Speed + Vector3.up * ((dy + 0.5f * Gravity * t * t) / t);

        BuildFood((Food)Random.Range(0, 3), go.transform, Vector3.zero, BlockyCharacter.RuntimeMaterials());
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if ((age += dt) > Life) { Destroy(gameObject); return; }

        velocity.y -= Gravity * dt;
        Vector3 step = velocity * dt;
        if (Physics.Raycast(transform.position, step.normalized, out var hit, step.magnitude + 0.05f, ~0, QueryTriggerInteraction.Ignore)
            && (!owner || hit.transform.root != owner.transform.root) && !hit.collider.GetComponentInParent<FirstPersonController>())
        { SoundKit.PlayAt(Sfx.Splat, transform.position, 0.6f); Destroy(gameObject); return; }   // splat on the wall / floor
        transform.position += step;
        transform.Rotate(540f * dt, 360f * dt, 0, Space.Self);

        foreach (var p in Players.All)
        {
            if (!p) continue;
            Vector3 d = transform.position - p.transform.position;
            if (d.y < 0 || d.y > 1.9f || new Vector2(d.x, d.z).magnitude > 0.5f) continue;
            var h = p.GetComponent<Health>();
            if (h && !h.IsDead) h.TakeDamage(damage);
            SoundKit.PlayAt(Sfx.Splat, transform.position, 0.8f);
            Destroy(gameObject);
            return;
        }
    }

    // Little food models, shared with the tray the worker carries.
    public static void BuildFood(Food food, Transform parent, Vector3 pos, BlockyCharacter.MaterialSource mats)
    {
        var root = new GameObject(food.ToString()).transform;
        root.SetParent(parent, false);
        root.localPosition = pos;
        switch (food)
        {
            case Food.Burger:
                P(PrimitiveType.Sphere, root, new Vector3(0, 0.025f, 0), new Vector3(0.11f, 0.045f, 0.11f), mats("Bun", new Color(0.82f, 0.55f, 0.25f)));
                P(PrimitiveType.Cylinder, root, new Vector3(0, 0.005f, 0), new Vector3(0.1f, 0.012f, 0.1f), mats("Patty", new Color(0.32f, 0.18f, 0.1f)));
                P(PrimitiveType.Cylinder, root, new Vector3(0, 0.016f, 0), new Vector3(0.105f, 0.004f, 0.105f), mats("Lettuce", new Color(0.35f, 0.7f, 0.25f)));
                P(PrimitiveType.Cylinder, root, new Vector3(0, -0.012f, 0), new Vector3(0.1f, 0.012f, 0.1f), mats("Bun", new Color(0.82f, 0.55f, 0.25f)));
                break;
            case Food.Fries:
                P(PrimitiveType.Cube, root, Vector3.zero, new Vector3(0.07f, 0.08f, 0.04f), mats("FryBox", new Color(0.8f, 0.12f, 0.1f)));
                for (int i = 0; i < 5; i++)
                    P(PrimitiveType.Cube, root, new Vector3(-0.024f + i * 0.012f, 0.055f + (i % 2) * 0.01f, 0), new Vector3(0.008f, 0.06f, 0.008f), mats("Fries", new Color(0.97f, 0.8f, 0.3f)));
                break;
            default:
                P(PrimitiveType.Cylinder, root, Vector3.zero, new Vector3(0.06f, 0.06f, 0.06f), mats("Cup", new Color(0.92f, 0.92f, 0.9f)));
                P(PrimitiveType.Cylinder, root, new Vector3(0, 0.02f, 0), new Vector3(0.062f, 0.012f, 0.062f), mats("CupBand", new Color(0.8f, 0.12f, 0.1f)));
                P(PrimitiveType.Cylinder, root, new Vector3(0, 0.062f, 0), new Vector3(0.064f, 0.004f, 0.064f), mats("Lid", new Color(0.95f, 0.95f, 0.95f)));
                P(PrimitiveType.Cylinder, root, new Vector3(0.01f, 0.1f, 0), new Vector3(0.008f, 0.04f, 0.008f), mats("Straw", new Color(0.95f, 0.75f, 0.2f)));
                break;
        }
    }

    static void P(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material m)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos; go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = m;
    }
}
