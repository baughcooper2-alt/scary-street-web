using System.Collections.Generic;
using UnityEngine;

// Poker Chips (DESIGN.md): upgrades change the chip colour, white → red → blue → green → black, and every step
// hits harder and flies further. You carry an open silver chip suitcase (a row of each colour) in your left arm; the right hand takes a chip out and
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
    static readonly Vector3 LeftHold = new Vector3(-0.21f, -0.31f, 0.44f), LeftEuler = new Vector3(-10f, 25f, 0f);
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
        LevelOnForearm();
    }

    // third person: the case rides level on the left forearm and hand, whatever angle the wrist is at
    void LevelOnForearm()
    {
        var body = inventory.GetComponentInChildren<BlockyCharacter>();
        if (!tpModel || !body || !body.handL || !body.elbowL) return;
        Vector3 hand = body.handL.TransformPoint(new Vector3(0.02f, -0.06f, 0)), elbow = body.elbowL.position;
        tpModel.position = Vector3.Lerp(elbow, hand, 0.7f) + Vector3.up * 0.06f;
        tpModel.rotation = Quaternion.LookRotation(body.transform.forward, Vector3.up) * Quaternion.Euler(-8f, 0, 0);
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

    // a silver aluminium chip suitcase, lid open: black felt, a row of each colour, handle and latches on the front
    Transform Case(Transform parent, bool fp, out Transform top)
    {
        EnsureMats();
        var mats = BlockyCharacter.RuntimeMaterials();
        var metal = mats("ChipCase", new Color(0.8f, 0.82f, 0.85f)); metal.SetFloat("_Smoothness", 0.8f);
        var edge = mats("ChipCaseEdge", new Color(0.35f, 0.36f, 0.38f)); edge.SetFloat("_Smoothness", 0.6f);
        var felt = mats("ChipCaseFelt", new Color(0.07f, 0.07f, 0.09f));
        var root = new GameObject("ChipCase").transform;
        root.SetParent(parent, false);
        const float W = 0.34f, D = 0.24f, H = 0.075f;
        Part(PrimitiveType.Cube, root, new Vector3(0, -H / 2, 0), new Vector3(W, H, D), metal, fp);                                 // bottom shell
        Part(PrimitiveType.Cube, root, new Vector3(0, -0.004f, 0), new Vector3(W - 0.012f, 0.004f, D - 0.012f), felt, fp);           // lining
        Part(PrimitiveType.Cube, root, new Vector3(0, 0.001f, 0), new Vector3(W + 0.004f, 0.006f, D + 0.004f), edge, fp);           // rim
        var lid = new GameObject("Lid").transform; lid.SetParent(root, false); lid.localPosition = new Vector3(0, 0, -D / 2);
        lid.localRotation = Quaternion.Euler(-165f, 0, 0);                                                                            // swung right back
        Part(PrimitiveType.Cube, lid, new Vector3(0, 0.02f, D / 2), new Vector3(W, 0.04f, D), metal, fp);
        Part(PrimitiveType.Cube, lid, new Vector3(0, -0.002f, D / 2), new Vector3(W - 0.012f, 0.004f, D - 0.012f), felt, fp);
        Part(PrimitiveType.Cube, root, new Vector3(0, -H / 2, D / 2 + 0.018f), new Vector3(0.1f, 0.018f, 0.018f), edge, fp);       // handle
        foreach (float x in new[] { -0.11f, 0.11f })
            Part(PrimitiveType.Cube, root, new Vector3(x, -0.012f, D / 2 + 0.004f), new Vector3(0.03f, 0.018f, 0.008f), edge, fp);   // latches
        var mats2 = BlockyCharacter.RuntimeMaterials();
        for (int row = 0; row < Colors.Length; row++)                                                                                // one colour per row, chips on edge
        {
            var rowMat = mats2("Chip" + Names[row], Colors[row]);
            var spotMat = mats2("ChipEdge" + Names[row], row == 0 ? new Color(0.2f, 0.35f, 0.8f) : new Color(0.96f, 0.96f, 0.94f));
            float x = -W / 2 + 0.04f + row * (W - 0.08f) / (Colors.Length - 1);
            for (int n = 0; n < 12; n++)
            {
                var chip = Part(PrimitiveType.Cylinder, root, new Vector3(x, 0.02f, -D / 2 + 0.03f + n * 0.0155f), new Vector3(0.04f, 0.0035f, 0.04f), n % 4 == 3 ? spotMat : rowMat, true, new Vector3(90f, 0, 0));
                if (!fp) chip.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }
        top = new GameObject("Top").transform; top.SetParent(root, false); top.localPosition = new Vector3(0.1f, 0.04f, 0.02f);
        return root;
    }

    void Recolor() { }                                                   // the case keeps a row of every colour; thrown chips use your level's

    protected override Transform BuildFirstPersonModel()
    {
        if (!arms.LeftHand) return null;
        var c = Case(arms.LeftHand, true, out fpCaseTop);                // resting on the hand, tipped toward you so you see the rows
        c.localPosition = new Vector3(0.08f, 0.04f, 0.02f); c.localRotation = Quaternion.Euler(-28f, -20f, 0);
        return c;
    }

    protected override Transform BuildThirdPersonModel(BlockyCharacter body)
    {
        if (!body.handL) return null;
        var c = Case(body.handL, false, out _);                          // carried flat on the forearm (placed every frame)
        return c;
    }
}
