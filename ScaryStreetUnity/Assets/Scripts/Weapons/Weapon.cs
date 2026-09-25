using UnityEngine;

public struct WeaponInput
{
    public bool primaryPressed;   // left click / right trigger, this frame
    public bool primaryHeld;
    public bool secondaryHeld;    // right click (or E) / left trigger
}

// Something that goes in a weapon slot. WeaponInventory creates it, calls Equip / Unequip when you
// switch slots, and Tick every frame while it's in your hands.
public abstract class Weapon : MonoBehaviour
{
    public string displayName = "Weapon";

    protected WeaponInventory inventory;
    protected FirstPersonArms arms;
    protected Transform cam;
    protected PlayerProgress progress;
    protected Health owner;

    public bool Equipped { get; private set; }

    public virtual void Init(WeaponInventory inv)
    {
        inventory = inv;
        var c = inv.GetComponentInChildren<Camera>(true);
        cam = c ? c.transform : Camera.main.transform;
        arms = cam.GetComponent<FirstPersonArms>();
        progress = inv.GetComponent<PlayerProgress>();
        owner = inv.GetComponent<Health>();
    }

    public virtual void Equip() => Equipped = true;
    public virtual void Unequip() => Equipped = false;
    public abstract void Tick(WeaponInput input);

    // Short text under the slot (ammo, charge...) and a hint line while it's equipped.
    public virtual string SlotStatus => "";
    public virtual string Hint => "";
}
