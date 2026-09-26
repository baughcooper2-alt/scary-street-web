using UnityEngine;

// A fridge you can open: F (gamepad X) swings the main door open (the light comes on: shelves, a milk carton,
// beer and a 6-pack inside) and grabs you a fresh 6-pack if yours isn't full; F again closes it.
// Tools > Scary Street > Set Up Kitchen Fridge swaps the kitchen's solid block (the one the microwave sits on) for
// this, sized and coloured to match; Add Fridge drops a plain one wherever you like. Builds its own look on Awake.
// On Start it checks what's round it (walls, counters): if it's facing something (the kitchen one faced the end of
// the counter) it turns to face the open floor, hinges the door on whichever side lets it swing furthest, and stops
// it short of anything in the way.
// Local space: origin on the floor in the middle, the front faces +Z.
public class Fridge : MonoBehaviour, IInteractable
{
    public static int Count { get; private set; }
    public static bool Any => Count > 0;

    public Vector3 size = new Vector3(0.8f, 1.8f, 0.72f);
    public Color color = new Color(0.9f, 0.9f, 0.88f);
    [Tooltip("The model's own solid block, hidden at runtime (set by Set Up Kitchen Fridge).")]
    public Renderer replaces;
    [Range(0.2f, 0.45f)] public float freezerShare = 0.3f;
    [Tooltip("Hinge on the left as you face it (the door swings out on that side). Set Up Kitchen Fridge picks the side away from the counter.")]
    public bool hingeOnLeft = true;
    [Tooltip("How far the door opens (degrees). On Start it's cut back so the door stops before any wall or counter.")]
    public float maxOpen = 105f;

    Transform door;
    Light lamp;
    bool open;
    float angle;

    void OnEnable() => Count++;
    void OnDisable() => Count--;

    void Awake()
    {
        if (replaces)
        {
            replaces.enabled = false;
            var c = replaces.GetComponent<Collider>(); if (c) c.enabled = false;
        }
        var box = GetComponent<BoxCollider>() ? GetComponent<BoxCollider>() : gameObject.AddComponent<BoxCollider>();
        box.size = new Vector3(size.x, size.y, size.z - 0.06f); box.center = new Vector3(0, size.y / 2, -0.03f);
        if (!transform.Find("Build")) Build();
    }

    // Swing the door through its arc on both sides; keep the side that opens widest and stop short of the first thing hit.
    void Start() => FitToRoom(true);

    // Face the open floor and hinge the door where it swings furthest (Set Up Kitchen Fridge calls this too, without
    // the rebuild, so the editor shows the same thing Play does).
    public void FitToRoom(bool rebuild)
    {
        Physics.SyncTransforms();
        // try the four facings (the footprint is near square): open floor in front first, then the widest door swing;
        // a small bonus keeps the placed facing when it's as good
        Quaternion placed = transform.rotation, best = placed;
        float bestScore = float.MinValue, bestAngle = 0; bool bestLeft = hingeOnLeft;
        for (int k = 0; k < 4; k++)
        {
            transform.rotation = placed * Quaternion.Euler(0, 90f * k, 0);
            float l = FreeAngle(true), r = FreeAngle(false);
            bool left = l > r + 10f || (l >= r - 10f && hingeOnLeft);
            float swing = left ? l : r;
            float score = (FrontClear() ? 1000f : 0f) + swing + (k == 0 ? 5f : 0f);
            if (score > bestScore) { bestScore = score; best = transform.rotation; bestAngle = swing; bestLeft = left; }
        }
        transform.rotation = best;
        if (bestLeft != hingeOnLeft)
        {
            hingeOnLeft = bestLeft;
            if (!rebuild) { maxOpen = Mathf.Clamp(bestAngle, 40f, 105f); return; }
            var old = transform.Find("Build");
            if (old) { old.name = "Build (old)"; old.gameObject.SetActive(false); Destroy(old.gameObject); }
            Build();
        }
        maxOpen = Mathf.Clamp(bestAngle, 40f, 105f);
        Physics.SyncTransforms();
    }

    // room to stand in front of it (a body's width, 0.8 m out)
    bool FrontClear()
    {
        Vector3 centre = transform.TransformPoint(new Vector3(0, 0.95f, size.z / 2 + 0.4f));
        Vector3 half = Vector3.Scale(new Vector3(size.x / 2 - 0.12f, 0.7f, 0.3f), transform.lossyScale);
        foreach (var c in Physics.OverlapBox(centre, half, transform.rotation, ~0, QueryTriggerInteraction.Ignore))
            if (Solid(c)) return false;
        return true;
    }

    bool Solid(Collider c)
    {
        if (!c.enabled || c.transform.IsChildOf(transform) || c.attachedRigidbody) return false;
        if (replaces && c.transform == replaces.transform) return false;
        return !(c.GetComponentInParent<CharacterController>() || c.GetComponentInParent<Health>() || c.GetComponentInParent<Door>());
    }

    float FreeAngle(bool left)
    {
        const float limit = 105f;
        for (float a = 5f; a <= limit; a += 5f)
            if (DoorBlocked(left, a)) return a - 5f;
        return limit;
    }

    // Is anything solid (not the fridge, not people or doors) where the door would be at this angle?
    bool DoorBlocked(bool left, float angle)
    {
        float w = size.x, d = size.z, split = size.y * (1f - freezerShare), hs = left ? 1f : -1f;
        var hinge = new Vector3(hs * w / 2, 0, d / 2);
        var turn = Quaternion.Euler(0, left ? angle : -angle, 0);
        // the door panel, less a few cm at each end (the hinge corner hardly moves, and the fridge sits flush to walls)
        Vector3 centre = transform.TransformPoint(hinge + turn * new Vector3(-hs * w / 2, split / 2 + 0.04f, -0.02f));
        Vector3 half = Vector3.Scale(new Vector3(w - 0.14f, split - 0.14f, 0.06f) * 0.5f, transform.lossyScale);
        foreach (var c in Physics.OverlapBox(centre, half, transform.rotation * turn, ~0, QueryTriggerInteraction.Ignore))
            if (Solid(c)) return true;
        return false;
    }

    void Build()
    {
        var mats = BlockyCharacter.RuntimeMaterials();
        var body = mats("FridgeBody", color); body.SetFloat("_Smoothness", 0.55f);
        var inside = mats("FridgeInside", new Color(0.93f, 0.94f, 0.95f)); inside.SetFloat("_Smoothness", 0.4f);
        var shelf = mats("FridgeShelf", new Color(0.8f, 0.88f, 0.92f)); shelf.SetFloat("_Smoothness", 0.9f);
        var trim = mats("FridgeTrim", new Color(0.55f, 0.56f, 0.58f)); trim.SetFloat("_Smoothness", 0.75f);
        var rubber = mats("FridgeSeal", new Color(0.12f, 0.12f, 0.13f));
        var build = new GameObject("Build").transform; build.SetParent(transform, false);

        float w = size.x, h = size.y, d = size.z, t = 0.035f;
        float split = h * (1f - freezerShare);                          // top of the fridge section
        Transform P(string n, Vector3 pos, Vector3 scale, Material m, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = n;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent ? parent : build, false);
            go.transform.localPosition = pos; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go.transform;
        }
        // shell: back, sides, top, bottom (the front is the doors)
        P("Back", new Vector3(0, h / 2, -d / 2 + t / 2), new Vector3(w, h, t), body);
        P("Left", new Vector3(-w / 2 + t / 2, h / 2, 0), new Vector3(t, h, d), body);
        P("Right", new Vector3(w / 2 - t / 2, h / 2, 0), new Vector3(t, h, d), body);
        P("Top", new Vector3(0, h - t / 2, 0), new Vector3(w, t, d), body);
        P("Bottom", new Vector3(0, 0.05f, 0), new Vector3(w, 0.1f, d), body);
        // inside: white liner, divider under the freezer, two glass shelves
        float iw = w - 2 * t, id = d - t - 0.06f, iz = -d / 2 + t + id / 2;
        P("LinerBack", new Vector3(0, split / 2 + 0.05f, -d / 2 + t + 0.003f), new Vector3(iw, split - 0.1f, 0.006f), inside);
        P("LinerL", new Vector3(-iw / 2 + 0.003f, split / 2 + 0.05f, iz), new Vector3(0.006f, split - 0.1f, id), inside);
        P("LinerR", new Vector3(iw / 2 - 0.003f, split / 2 + 0.05f, iz), new Vector3(0.006f, split - 0.1f, id), inside);
        P("LinerFloor", new Vector3(0, 0.103f, iz), new Vector3(iw, 0.006f, id), inside);
        P("Divider", new Vector3(0, split, 0), new Vector3(w, t, d), body);
        P("LinerCeiling", new Vector3(0, split - t / 2 - 0.003f, iz), new Vector3(iw, 0.006f, id), inside);
        float s1 = 0.1f + (split - 0.1f) * 0.36f, s2 = 0.1f + (split - 0.1f) * 0.68f;
        P("Shelf1", new Vector3(0, s1, iz), new Vector3(iw - 0.01f, 0.012f, id - 0.02f), shelf);
        P("Shelf2", new Vector3(0, s2, iz), new Vector3(iw - 0.01f, 0.012f, id - 0.02f), shelf);
        // things on the shelves: a 6-pack, loose bottles, a milk carton
        var beer = mats("BeerGlass", new Color(0.42f, 0.24f, 0.08f));
        var card = mats("SixPackCard", new Color(0.72f, 0.12f, 0.1f));
        var milk = mats("FridgeMilk", new Color(0.97f, 0.97f, 0.95f));
        P("SixPack", new Vector3(-iw * 0.2f, s1 + 0.05f, iz), new Vector3(0.19f, 0.09f, 0.13f), card);
        for (int i = 0; i < 6; i++)
            P("Neck", new Vector3(-iw * 0.2f - 0.06f + (i / 2) * 0.06f, s1 + 0.13f, iz + (i % 2 == 0 ? -0.03f : 0.03f)), new Vector3(0.03f, 0.08f, 0.03f), beer);
        for (int i = 0; i < 3; i++)
            P("Bottle", new Vector3(iw * 0.15f + i * 0.07f, s2 + 0.1f, iz - 0.05f), new Vector3(0.055f, 0.19f, 0.055f), beer);
        P("Milk", new Vector3(iw * 0.25f, s1 + 0.12f, iz + 0.02f), new Vector3(0.09f, 0.22f, 0.09f), milk);
        // freezer door (stays shut)
        P("FreezerDoor", new Vector3(0, split + (h - split) / 2, d / 2 - 0.025f), new Vector3(w - 0.01f, h - split - 0.01f, 0.05f), body);
        P("FreezerHandle", new Vector3((hingeOnLeft ? -1f : 1f) * (w / 2 - 0.08f), split + 0.08f, d / 2 + 0.02f), new Vector3(0.025f, 0.12f, 0.03f), trim);
        // main door: facing the fridge, your left is its local +X (hingeOnLeft) and your right is -X
        float hs = hingeOnLeft ? 1f : -1f;                               // hinge side; the panel runs back across from it
        door = new GameObject("Door").transform; door.SetParent(build, false);
        door.localPosition = new Vector3(hs * w / 2, 0, d / 2);
        P("Panel", new Vector3(-hs * w / 2, split / 2 + 0.005f, -0.025f), new Vector3(w - 0.01f, split - 0.01f, 0.05f), body, door);
        P("Seal", new Vector3(-hs * w / 2, split / 2 + 0.005f, -0.052f), new Vector3(w - 0.05f, split - 0.05f, 0.004f), rubber, door);
        P("Handle", new Vector3(-hs * (w - 0.08f), split * 0.62f, 0.02f), new Vector3(0.025f, split * 0.35f, 0.03f), trim, door);
        P("DoorShelf1", new Vector3(-hs * w / 2, split * 0.35f, -0.08f), new Vector3(w - 0.12f, 0.08f, 0.05f), inside, door);
        P("DoorShelf2", new Vector3(-hs * w / 2, split * 0.7f, -0.08f), new Vector3(w - 0.12f, 0.08f, 0.05f), inside, door);
        var doorCol = door.gameObject.AddComponent<BoxCollider>();
        doorCol.center = new Vector3(-hs * w / 2, split / 2, -0.03f); doorCol.size = new Vector3(w, split, 0.08f);
        // light inside
        lamp = new GameObject("Light").AddComponent<Light>();
        lamp.transform.SetParent(build, false); lamp.transform.localPosition = new Vector3(0, split - 0.12f, iz + 0.05f);
        lamp.type = LightType.Point; lamp.range = 1.6f; lamp.intensity = 0f; lamp.color = new Color(1f, 0.97f, 0.9f);
    }

    void Update()
    {
        angle = Mathf.MoveTowards(angle, open ? maxOpen : 0f, Time.deltaTime * 320f);
        if (door) door.localRotation = Quaternion.Euler(0, hingeOnLeft ? angle : -angle, 0);   // always swings out toward you
        if (lamp) lamp.intensity = Mathf.MoveTowards(lamp.intensity, open ? 1.6f : 0f, Time.deltaTime * 8f);
    }

    public string Prompt => open ? "Close the fridge" : "Open the fridge";
    public bool CanInteract => true;

    public void Interact(GameObject player)
    {
        open = !open;
        SoundKit.PlayAt(open ? Sfx.DoorOpen : Sfx.DoorClose, transform.position + Vector3.up, 0.6f);
        if (!open) return;
        var inv = player.GetComponent<WeaponInventory>();
        var pack = inv ? inv.Get<SixPackWeapon>() : null;
        if (pack && pack.Bottles < SixPackWeapon.PackSize) { pack.Refill(); inv.Toast("Grabbed a fresh 6-pack", 1.8f); }
    }
}
