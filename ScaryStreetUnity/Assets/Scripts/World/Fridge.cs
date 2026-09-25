using UnityEngine;

// The fridge: F (gamepad X) while looking at it refills your 6-pack. Place one in the kitchen with
// Tools > Scary Street > Add Fridge; it builds its own look (white body, freezer door, handles) if it has none.
[RequireComponent(typeof(BoxCollider))]
public class Fridge : MonoBehaviour, IInteractable
{
    public static int Count { get; private set; }
    public static bool Any => Count > 0;

    void OnEnable() => Count++;
    void OnDisable() => Count--;

    void Awake()
    {
        var box = GetComponent<BoxCollider>();
        box.size = new Vector3(0.75f, 1.8f, 0.7f); box.center = new Vector3(0, 0.9f, 0);
        if (GetComponentInChildren<Renderer>()) return;
        var mats = BlockyCharacter.RuntimeMaterials();
        var white = mats("FridgeWhite", new Color(0.9f, 0.9f, 0.88f)); white.SetFloat("_Smoothness", 0.6f);
        var trim = mats("FridgeTrim", new Color(0.55f, 0.56f, 0.58f)); trim.SetFloat("_Smoothness", 0.7f);
        Part(PrimitiveType.Cube, new Vector3(0, 0.9f, 0), new Vector3(0.75f, 1.8f, 0.7f), white);
        Part(PrimitiveType.Cube, new Vector3(0, 1.3f, 0.352f), new Vector3(0.72f, 0.012f, 0.01f), trim);            // freezer seam
        Part(PrimitiveType.Cube, new Vector3(0.3f, 1.52f, 0.37f), new Vector3(0.025f, 0.28f, 0.03f), trim);          // handles
        Part(PrimitiveType.Cube, new Vector3(0.3f, 0.9f, 0.37f), new Vector3(0.025f, 0.45f, 0.03f), trim);
    }

    void Part(PrimitiveType type, Vector3 pos, Vector3 scale, Material m)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos; go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = m;
    }

    public string Prompt => "Grab a fresh 6-pack";
    public bool CanInteract => true;

    public void Interact(GameObject player)
    {
        var inv = player.GetComponent<WeaponInventory>();
        var pack = inv ? inv.Get<SixPackWeapon>() : null;
        if (!pack) { if (inv) inv.Toast("Nothing to drink from right now (buy a 6-pack from DoorDash)", 2f); return; }
        if (pack.Bottles >= SixPackWeapon.PackSize) { inv.Toast("Your 6-pack is already full", 1.5f); return; }
        pack.Refill();
        SoundKit.Play(Sfx.DoorOpen, 0.5f, 0f);
        inv.Toast("Fresh 6-pack", 1.5f);
    }
}
