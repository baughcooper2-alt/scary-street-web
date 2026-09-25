using UnityEngine;

public struct WeaponInput
{
    public bool primaryPressed;   // left click / right trigger, this frame
    public bool primaryHeld;
    public bool secondaryHeld;    // right click (or E) / left trigger
    public bool reloadPressed;    // R / d-pad down
}

// Something that goes in a weapon slot. WeaponInventory creates it, calls Equip / Unequip when you
// switch slots, and Tick every frame while it's in your hands.
public abstract class Weapon : MonoBehaviour
{
    public string displayName = "Weapon";
    [Tooltip("Raised by level-up picks. Web build: each level is +30% damage and a bigger magazine.")]
    public int level = 1;

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

    public virtual void Equip() { Equipped = true; if (BodyAnim) BodyAnim.hold = HoldPose; }
    public virtual void Unequip() { Equipped = false; if (BodyAnim) { BodyAnim.hold = CharacterAnimator.Hold.None; BodyAnim.inhaling = false; } }

    // How the third-person body holds this weapon.
    protected virtual CharacterAnimator.Hold HoldPose => CharacterAnimator.Hold.None;
    public abstract void Tick(WeaponInput input);

    // Short text under the slot (ammo, charge...) and a hint line while it's equipped.
    public virtual UIArt.Icon Icon => UIArt.Icon.Fist;               // slot card, level-up card, shop tile
    public virtual string SlotStatus => "";
    public virtual string Hint => "";

    // The owner's device decides the button names in hints ("Left click" vs "RT"), and gets the rumble.
    protected string Key(string keyboard, string gamepad) => inventory ? PlayerControls.For(inventory.gameObject).Prompt(keyboard, gamepad) : keyboard;
    protected void Rumble(float low, float high, float seconds) { if (inventory) PlayerControls.For(inventory.gameObject).Rumble(low, high, seconds); }

    // ---------- levels (level-up picks) ----------

    public virtual bool CanLevelUp => true;
    public virtual string LevelUpText => "+30% damage, bigger magazine";
    public virtual void LevelUp() => level++;
    protected float LevelDamage => 1f + 0.3f * (level - 1);

    // ---------- held models: one in your first-person hand, one on your body for third person ----------

    protected Transform fpModel, tpModel;
    ThirdPersonView tpv;

    protected bool ThirdPerson => tpv && tpv.IsThirdPerson;
    protected CharacterAnimator BodyAnim => inventory.GetComponentInChildren<CharacterAnimator>();

    public void DestroyModels() { if (fpModel) Destroy(fpModel.gameObject); if (tpModel) Destroy(tpModel.gameObject); }

    // Build the model under `parent`; `firstPerson` models shouldn't cast shadows.
    protected virtual Transform BuildFirstPersonModel() => null;
    protected virtual Transform BuildThirdPersonModel(BlockyCharacter body) => null;

    // Call every Tick and from Equip / Unequip: builds models once the arms / body exist, keeps them in the right view.
    protected void SyncModels()
    {
        if (!tpv) tpv = inventory.GetComponent<ThirdPersonView>();
        var anim = BodyAnim;
        if (Equipped && anim && anim.hold != HoldPose) anim.hold = HoldPose;       // the body may appear after we equip
        if (!fpModel && arms && arms.RightHand) fpModel = BuildFirstPersonModel();
        if (!tpModel) { var body = inventory.GetComponentInChildren<BlockyCharacter>(); if (body && body.handR) tpModel = BuildThirdPersonModel(body); }
        int index = PlayerLayers.IndexOf(inventory);
        if (fpModel)
        {
            fpModel.gameObject.SetActive(Equipped && !ThirdPerson);
            if (fpModel.gameObject.layer != PlayerLayers.Arms(index)) PlayerLayers.Set(fpModel.gameObject, PlayerLayers.Arms(index));
        }
        if (tpModel)                                  // held by the body: shows in third person, mirrors and to other players
        {
            tpModel.gameObject.SetActive(Equipped);
            if (tpModel.gameObject.layer != PlayerLayers.Body(index)) PlayerLayers.Set(tpModel.gameObject, PlayerLayers.Body(index));
        }
    }

    protected static Transform Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material m, bool noShadow, Vector3 euler = default)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        var t = go.transform;
        t.SetParent(parent, false);
        t.localPosition = pos; t.localScale = scale; t.localRotation = Quaternion.Euler(euler);
        var r = go.GetComponent<Renderer>();
        if (m) r.sharedMaterial = m;
        if (noShadow) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return t;
    }

    // Eye point of the player (the camera may be behind us in third person).
    protected Vector3 Eye
    {
        get
        {
            var cc = inventory.GetComponent<CharacterController>();
            return cc ? inventory.transform.position + Vector3.up * (cc.height - 0.12f) : cam.position;
        }
    }
}

// A weapon with a magazine that reloads on its own when empty (or with R), like the web build's WSTAT table.
public abstract class MagazineWeapon : Weapon
{
    protected abstract int BaseMagazine { get; }
    protected abstract float ReloadTime { get; }
    protected abstract string ReloadText { get; }

    public int Magazine => Mathf.RoundToInt(BaseMagazine * (1f + 0.5f * (level - 1)));
    public bool Reloading => reloadT > 0;

    protected int ammo = -1;
    protected float reloadT;

    // Returns true while reloading (don't attack).
    protected bool TickReload(float dt, bool reloadPressed)
    {
        if (ammo < 0) ammo = Magazine;
        if (reloadT > 0)
        {
            if ((reloadT -= dt) <= 0) ammo = Magazine;
            return reloadT > 0;
        }
        if (reloadPressed && ammo < Magazine) reloadT = ReloadTime;
        return reloadT > 0;
    }

    protected void UseAmmo(int n = 1)
    {
        ammo = Mathf.Max(0, ammo - n);
        if (ammo == 0) reloadT = ReloadTime;
    }

    public override void LevelUp() { base.LevelUp(); ammo = Magazine; reloadT = 0; }

    public override string SlotStatus => Reloading ? ReloadText : $"{Mathf.Max(0, ammo)} / {Magazine}";
}
