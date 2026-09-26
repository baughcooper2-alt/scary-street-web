using System.Collections.Generic;
using UnityEngine;

// Poker Chips (DESIGN.md): upgrades change the chip colour, white → red → blue → green → black, and every step
// hits harder and flies further. You hold an open chip case in your left arm; the right hand takes a chip out and
// flicks it, and it bounces on to the next enemy (twice from green). 10 chips, then 1.6 s restacking.
public class PokerChipsWeapon : MagazineWeapon
{
    public override UIArt.Icon Icon => UIArt.Icon.Chip;
    public float baseDamage = 9f;
    public float baseRange = 10f;
    public float throwCooldown = 0.32f;

    static readonly string[] Names = { "White", "Red", "Blue", "Green", "Black" };
    static readonly Color[] Colors =
    {
        new Color(0.95f, 0.95f, 0.93f), new Color(0.82f, 0.12f, 0.12f), new Color(0.15f, 0.32f, 0.85f),
        new Color(0.12f, 0.62f, 0.25f), new Color(0.08f, 0.08f, 0.09f),
    };
    static readonly Vector3 LeftHold = new Vector3(-0.13f, -0.25f, 0.4f), LeftEuler = new Vector3(0f, 25f, 0f);
    static readonly Vector3 RightRest = new Vector3(0.17f, -0.23f, 0.44f);

    float cooldown;
    readonly HandMotion hand = new HandMotion { rest = RightRest, restEuler = new Vector3(0, -10f, 0) };
    readonly List<Renderer> chipRenderers = new List<Renderer>(), spotRenderers = new List<Renderer>();
    Material chipMat, edgeMat;
    Transform fpCaseTop;

    protected override int BaseMagazine => 10;
    protected override float ReloadTime => 1.6f;
    protected override string ReloadText => "Restacking…";
    protected override CharacterAnimator.Hold HoldPose => CharacterAnimator.Hold.CarryLeft;

    int Step => Mathf.Clamp(level - 1, 0, Names.Length - 1);
    public string ChipName => Names[Step];
    float Damage => baseDamage * (1f + 0.35f * Step);
    float Range => baseRange + 3f * Step;

    public override void Init(WeaponInventory inv) { base.Init(inv); displayName = "Poker Chips"; }
    public override void Equip() { base.Equip(); SyncModels(); }
    public override void Unequip() { base.Unequip(); hand.Release(arms); if (arms) arms.overrideLeft = false; SyncModels(); }

    public override bool CanLevelUp => level < Names.Length;
    public override string LevelUpText => level < Names.Length ? $"{Names[Step + 1]} chips: more damage and range" + (Step + 1 == 3 ? ", and they bounce twice" : "") : "Maxed out";
    public override void LevelUp() { base.LevelUp(); Recolor(); }
    public override string Hint => Reloading ? "Restacking your chips…" : $"{Key("left click", GamepadInfo.RT)} to flick a {ChipName.ToLower()} chip: it bounces to the next worker · {Key("R", "d-pad ↓")} to restack";

    public override void Tick(WeaponInput input)
    {
        float dt = Time.deltaTime;
        SyncModels();
        cooldown -= dt;
        bool reloading = TickReload(dt, input.reloadPressed);
        if (arms) { arms.overrideLeft = true; arms.leftTarget = LeftHold + (reloading ? new Vector3(0, Mathf.Sin(Time.time * 12f) * 0.006f, 0) : Vector3.zero); arms.leftEuler = LeftEuler; }
        if (fpCaseTop) hand.pickFrom = cam.InverseTransformPoint(fpCaseTop.position);
        if (!reloading && input.primaryHeld && cooldown <= 0 && ammo > 0) Throw();
        hand.Apply(arms, dt);
    }

    void Throw()
    {
        cooldown = throwCooldown;
        UseAmmo();
        hand.Play(HandMotion.Move.Flick, 0.28f);
        if (BodyAnim) BodyAnim.Throw(0.3f, 0.5f);
        float speed = 17f + 1.5f * Step;
        var p = Projectile.Spawn("PokerChip", Eye + cam.forward * 0.5f + Vector3.down * 0.12f, cam.forward * speed, inventory.gameObject);
        p.damage = PlayerStats.RangedDamage(Damage, inventory.gameObject);
        p.radius = 0.25f; p.life = Range / speed; p.bounces = Step >= 3 ? 2 : 1; p.bounceRange = 5f + Step;
        p.spin = 900f; p.spinAxis = Vector3.up; p.hitSound = Sfx.Chip;
        EnsureMats();
        Chip(p.transform, Vector3.zero, false);
        SoundKit.Play(Sfx.Chip, 0.4f, 0.1f);
    }

    void EnsureMats()
    {
        var mats = BlockyCharacter.RuntimeMaterials();
        chipMat = mats("Chip" + ChipName, Colors[Step]);
        edgeMat = mats("ChipEdge" + ChipName, Step == 0 ? new Color(0.2f, 0.35f, 0.8f) : new Color(0.96f, 0.96f, 0.94f));
    }

    // a clay chip: coloured disc with contrasting edge spots (flat in its parent's XZ plane)
    void Chip(Transform parent, Vector3 at, bool keep)
    {
        var disc = Part(PrimitiveType.Cylinder, parent, at, new Vector3(0.04f, 0.0035f, 0.04f), chipMat, true);
        if (keep) chipRenderers.Add(disc.GetComponent<Renderer>());
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 3f;
            var spot = Part(PrimitiveType.Cube, parent, at + new Vector3(Mathf.Cos(a) * 0.018f, 0, Mathf.Sin(a) * 0.018f), new Vector3(0.006f, 0.0074f, 0.006f), edgeMat, true, new Vector3(0, -a * Mathf.Rad2Deg, 0));
            if (keep) spotRenderers.Add(spot.GetComponent<Renderer>());
        }
    }

    // an open aluminium chip case: silver tray, lid tipped open at the back, rows of chips standing on edge
    Transform Case(Transform parent, bool fp, out Transform top)
    {
        EnsureMats();
        var mats = BlockyCharacter.RuntimeMaterials();
        var metal = mats("ChipCase", new Color(0.78f, 0.8f, 0.83f)); metal.SetFloat("_Smoothness", 0.75f);
        var felt = mats("ChipCaseFelt", new Color(0.08f, 0.08f, 0.1f));
        var root = new GameObject("ChipCase").transform;
        root.SetParent(parent, false);
        Part(PrimitiveType.Cube, root, new Vector3(0, -0.012f, 0), new Vector3(0.2f, 0.028f, 0.13f), metal, fp);           // tray
        Part(PrimitiveType.Cube, root, new Vector3(0, 0.0025f, 0), new Vector3(0.19f, 0.004f, 0.12f), felt, fp);           // lining
        var lid = Part(PrimitiveType.Cube, root, new Vector3(0, 0.05f, 0.07f), new Vector3(0.2f, 0.1f, 0.01f), metal, fp, new Vector3(-12f, 0, 0));
        for (int row = 0; row < 4; row++)                                                                                     // chips on edge, in rows
            for (int n = 0; n < 7; n++)
                Chip(root, new Vector3(-0.075f + row * 0.05f, 0.022f, -0.045f + n * 0.014f), true);
        foreach (var r in chipRenderers) r.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        foreach (var r in spotRenderers) r.enabled = false;                                                                   // too small to see on edge
        top = new GameObject("Top").transform; top.SetParent(root, false); top.localPosition = new Vector3(0.02f, 0.04f, -0.01f);
        return root;
    }

    void Recolor()
    {
        EnsureMats();
        foreach (var r in chipRenderers) if (r) r.sharedMaterial = chipMat;
        foreach (var r in spotRenderers) if (r) r.sharedMaterial = edgeMat;
    }

    protected override Transform BuildFirstPersonModel()
    {
        if (!arms.LeftHand) return null;
        var c = Case(arms.LeftHand, true, out fpCaseTop);
        c.localPosition = new Vector3(0.02f, 0.03f, 0.03f); c.localRotation = Quaternion.Euler(0, -20f, 0);
        return c;
    }

    protected override Transform BuildThirdPersonModel(BlockyCharacter body)
    {
        if (!body.handL) return null;
        var c = Case(body.handL, false, out _);
        c.localPosition = new Vector3(0, -0.06f, 0.06f); c.localScale = Vector3.one * 1.1f;
        return c;
    }
}
