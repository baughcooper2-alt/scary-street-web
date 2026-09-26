using UnityEngine;

// A thrown Law Book lying on the floor: bobs with a glowing ring so you can find it; walk over it (or press F)
// and it goes back in your hand. If it lands somewhere unreachable (no floor under it) it comes back on its own.
public class LawBookPickup : MonoBehaviour, IInteractable
{
    LawBookWeapon book;
    Transform model;
    float t, lostT = -1f;
    static Material glow;

    public static LawBookPickup Drop(Vector3 at, LawBookWeapon book)
    {
        var go = new GameObject("LawBook (on the floor)");
        var p = go.AddComponent<LawBookPickup>();
        p.book = book;
        if (Physics.Raycast(at + Vector3.up * 0.3f, Vector3.down, out var hit, 30f, ~0, QueryTriggerInteraction.Ignore)) go.transform.position = hit.point;
        else { go.transform.position = at; p.lostT = 2f; }
        go.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        p.model = book.BuildBook(go.transform, false);
        p.model.localPosition = new Vector3(0, 0.05f, 0); p.model.localRotation = Quaternion.Euler(0, 0, 90f);   // lying flat
        if (!glow) glow = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(1f, 0.85f, 0.35f) };
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(ring.GetComponent<Collider>());
        ring.transform.SetParent(go.transform, false);
        ring.transform.localPosition = new Vector3(0, 0.01f, 0); ring.transform.localScale = new Vector3(0.55f, 0.004f, 0.55f);
        ring.GetComponent<Renderer>().sharedMaterial = glow;
        ring.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var col = go.AddComponent<SphereCollider>(); col.isTrigger = true; col.radius = 0.5f; col.center = Vector3.up * 0.2f;
        return p;
    }

    void Update()
    {
        if (!book) { Destroy(gameObject); return; }
        t += Time.deltaTime;
        if (model) { model.localPosition = new Vector3(0, 0.08f + Mathf.Sin(t * 3f) * 0.03f, 0); model.Rotate(0, 60f * Time.deltaTime, 0, Space.World); }
        if (lostT >= 0 && (lostT -= Time.deltaTime) < 0) { Return(); return; }
        var owner = book.transform.root;                          // the player who threw it
        Vector3 d = owner.position - transform.position; d.y = Mathf.Max(0, Mathf.Abs(d.y) - 1f);
        if (d.magnitude < 1.3f) Return();
    }

    void Return()
    {
        if (book) book.PickedUp();
        Destroy(gameObject);
    }

    public string Prompt => "Pick up your Law Book";
    public bool CanInteract => true;
    public void Interact(GameObject player) { if (book && player == book.transform.root.gameObject) Return(); }
}
