using System.Collections.Generic;
using UnityEngine;

// Poker Chips (DESIGN.md): upgrades change the chip colour, white → red → blue → green → black, and every step
// hits harder and flies further. An open silver chip briefcase (a row of each colour, lid laid well back) lies on your
// left forearm, palm up under it; the right hand takes a chip out and flicks it, and it bounces on to the next enemy
// (twice from green). The case holds 50 chips and you can see them go; when it's empty you throw the case itself
// (it bursts on whoever it hits) and it's gone. Buy another from the DoorDash.
public class PokerChipsWeapon : Weapon
{
    public override UIArt.Icon Icon => UIArt.Icon.Chip;
    public const int CaseSize = 50;
    public float baseDamage = 9f;
    public float baseRange = 10f;
    public float throwCooldown = 0.32f;
    [Header("Throwing the empty case")]
    public float caseDamage = 30f, caseBlastRadius = 1.8f, caseBlastDamage = 15f, caseSpeed = 13f;

    static readonly string[] Names = { "White", "Red", "Blue", "Green", "Black" };
    static readonly Color[] Colors =
    {
        new Color(0.95f, 0.95f, 0.93f), new Color(0.82f, 0.12f, 0.12f), new Color(0.15f, 0.32f, 0.85f),
        new Color(0.12f, 0.62f, 0.25f), new Color(0.08f, 0.08f, 0.09f),
    };
    const int PerRow = CaseSize / 5;
    // first person (camera space), like the body: the case lies on top of the left forearm (palm up under it), tipped
    // toward you with the lid laid well back so you can see into it
    const float W = 0.34f, D = 0.24f, H = 0.075f, LidOpen = 128f;
    static readonly Vector3 CasePos = new Vector3(-0.22f, -0.11f, 0.66f), CaseEuler = new Vector3(-28f, 14f, 0);
    static readonly Vector3 LeftEuler = new Vector3(10f, 14f, 90f);
    static Vector3 LeftHold => CasePos + Quaternion.Euler(CaseEuler) * new Vector3(0.03f, -H - 0.028f, -0.02f);
    static readonly Vector3 RightRest = new Vector3(0.17f, -0.23f, 0.44f);
    static readonly Vector3 TossTo = new Vector3(0.02f, -0.04f, 0.5f);       // where the case is lifted to before it goes

    int chips = CaseSize, shown = -1;
    float cooldown, tossT = -1f;
    bool tossed;
    readonly HandMotion hand = new HandMotion { rest = RightRest, restEuler = new Vector3(0, -10f, 0) };
    GameObject[] fpChips, tpChips;                                           // in the order they're taken out
    Material chipMat, edgeMat;
    Transform fpCaseTop;

    protected override CharacterAnimator.Hold HoldPose => CharacterAnimator.Hold.CarryLeft;

    int Step => Mathf.Clamp(level - 1, 0, Names.Length - 1);
    public string ChipName => Names[Step];
    public int Chips => chips;
    float Damage => baseDamage * (1f + 0.35f * Step);
    float Range => baseRange + 3f * Step;

    public override void Init(WeaponInventory inv) { base.Init(inv); displayName = "Poker Chips"; }
    public override void Equip() { base.Equip(); SyncModels(); shown = -1; }
    public override void Unequip() { base.Unequip(); hand.Release(arms); if (arms) arms.overrideLeft = false; if (BodyAnim) BodyAnim.gripOverrideL = -1f; SyncModels(); }

    public void Restock() { if (tossed) return; chips = CaseSize; shown = -1; }

    public override bool CanLevelUp => level < Names.Length;
    public override string LevelUpText => level < Names.Length ? $"{Names[Step + 1]} chips: more damage and range" + (Step + 1 == 3 ? ", and they bounce twice" : "") : "Maxed out";
    public override string SlotStatus => $"{chips} chips";
    public override string Hint => chips > 0
        ? $"{Key("left click", GamepadInfo.RT)} to flick a {ChipName.ToLower()} chip: it bounces to the next worker · {chips} left, then you throw the case"
        : "Out of chips: throwing the case";

    public override void Tick(WeaponInput input)
    {
        float dt = Time.deltaTime;
        SyncModels();
        if (tossed) { HideModels(); return; }
        cooldown -= dt;
        Vector3 casePos = CasePos;
        if (arms) { arms.overrideLeft = true; arms.leftTarget = LeftHold; arms.leftEuler = LeftEuler; arms.leftFingers = FirstPersonHand.Grip.Flat; }
        if (BodyAnim) BodyAnim.gripOverrideL = 0.15f;                     // flat hand under the case
        if (fpCaseTop) hand.pickFrom = cam.InverseTransformPoint(fpCaseTop.position);

        if (chips > 0)
        {
            if (input.primaryHeld && cooldown <= 0) Throw();
        }
        else if (tossT < 0 && !hand.Busy) { tossT = 0; hand.Play(HandMotion.Move.Throw, 0.5f); if (BodyAnim) BodyAnim.Throw(0.5f, 0.55f); }
        if (tossT >= 0)
        {
            // lift the empty case off the arm into the right hand's throw, then let it go
            tossT += dt;
            float k = Mathf.SmoothStep(0, 1, tossT / 0.25f);
            casePos = Vector3.Lerp(CasePos, TossTo, k);
            if (arms) arms.leftTarget = Vector3.Lerp(LeftHold, LeftHold + new Vector3(0, 0.05f, 0.05f), k);
            if (tossT >= 0.27f) ThrowCase();
        }
        if (fpModel) fpModel.localPosition = casePos;
        hand.Apply(arms, dt);
        if (arms) arms.rightFingers = hand.Busy ? FirstPersonHand.Grip.Card : new FirstPersonHand.Grip(0.35f);   // pinches a chip out
        LevelOnForearm();
        ShowChips();
    }

    // the chips still in the case, front to back, a little from every row
    void ShowChips()
    {
        if (chips == shown) return;
        shown = chips;
        int gone = CaseSize - chips;
        foreach (var list in new[] { fpChips, tpChips })
            if (list != null) for (int k = 0; k < list.Length; k++) if (list[k]) list[k].SetActive(k >= gone);
    }

    // third person: the case rides level on the left forearm and hand, whatever angle the wrist is at
    void LevelOnForearm()
    {
        var body = inventory.GetComponentInChildren<BlockyCharacter>();
        if (!tpModel || !body || !body.handL || !body.elbowL) return;
        Vector3 hand = body.handL.TransformPoint(new Vector3(0.02f, -0.06f, 0)), elbow = body.elbowL.position;
        tpModel.position = Vector3.Lerp(elbow, hand, 0.6f) + Vector3.up * (H + 0.04f);          // bottom resting on the forearm
        tpModel.rotation = Quaternion.LookRotation(body.transform.forward, Vector3.up) * Quaternion.Euler(-5f, 0, 0);
    }

    void Throw()
    {
        cooldown = throwCooldown;
        chips--;
        hand.Play(HandMotion.Move.Flick, 0.28f);
        if (BodyAnim) BodyAnim.Flick(0.28f);
        float speed = 17f + 1.5f * Step;
        var p = Projectile.Spawn("PokerChip", Eye + cam.forward * 0.5f + Vector3.down * 0.12f, cam.forward * speed, inventory.gameObject);
        p.damage = PlayerStats.RangedDamage(Damage, inventory.gameObject);
        p.radius = 0.25f; p.life = Range / speed; p.bounces = Step >= 3 ? 2 : 1; p.bounceRange = 5f + Step;
        p.spin = 900f; p.spinAxis = Vector3.up; p.hitSound = Sfx.Chip;
        EnsureMats();
        Chip(p.transform, Vector3.zero);
        SoundKit.Play(Sfx.Chip, 0.4f, 0.1f);
        if (chips == 0) inventory.Toast("Out of chips: throw the case!", 1.4f);
    }

    // the empty case goes too: it bursts on whoever it hits, and the weapon's gone
    void ThrowCase()
    {
        tossed = true; tossT = -1f;
        SoundKit.Play(Sfx.Throw, 0.7f);
        Vector3 from = fpModel && fpModel.gameObject.activeInHierarchy ? fpModel.position : Eye + cam.forward * 0.6f;
        var p = Projectile.Spawn("ChipCase", from, (cam.forward + Vector3.up * 0.18f).normalized * caseSpeed, inventory.gameObject);
        p.damage = PlayerStats.RangedDamage(caseDamage * LevelDamage, inventory.gameObject);
        p.gravity = 9f; p.life = 3f; p.radius = 0.35f; p.knockback = 1f; p.burstOnWall = true;
        p.blastRadius = caseBlastRadius; p.blastDamage = PlayerStats.RangedDamage(caseBlastDamage * LevelDamage, inventory.gameObject);
        p.hitSound = Sfx.Hit; p.burstSound = Sfx.Chip;
        p.spin = 540f; p.spinAxis = Vector3.right;
        var c = Case(p.transform, false, out _, out var empty);
        foreach (var g in empty) g.SetActive(false);
        c.localPosition = Vector3.zero;
        HideModels();
        inventory.Toast("Threw the chip case", 1.4f);
        Invoke(nameof(Gone), 0.35f);                                     // let the throw finish, then the slot empties
    }

    void Gone() { if (inventory) inventory.Remove(this); }

    void HideModels()
    {
        if (fpModel) fpModel.gameObject.SetActive(false);
        if (tpModel) tpModel.gameObject.SetActive(false);
        if (arms) arms.overrideLeft = false;
    }

    void EnsureMats()
    {
        var mats = BlockyCharacter.RuntimeMaterials();
        chipMat = mats("Chip" + ChipName, Colors[Step]);
        edgeMat = mats("ChipEdge" + ChipName, Step == 0 ? new Color(0.2f, 0.35f, 0.8f) : new Color(0.96f, 0.96f, 0.94f));
    }

    // a clay chip: coloured disc with contrasting edge spots (flat in its parent's XZ plane)
    void Chip(Transform parent, Vector3 at)
    {
        Part(PrimitiveType.Cylinder, parent, at, new Vector3(0.04f, 0.0035f, 0.04f), chipMat, true);
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 3f;
            Part(PrimitiveType.Cube, parent, at + new Vector3(Mathf.Cos(a) * 0.018f, 0, Mathf.Sin(a) * 0.018f), new Vector3(0.006f, 0.0074f, 0.006f), edgeMat, true, new Vector3(0, -a * Mathf.Rad2Deg, 0));
        }
    }

    // a silver aluminium chip briefcase, open: black felt, a row of each colour on edge (10 each), the lid hinged at the
    // far side and laid well back, handle and latches on the near side. Origin at the middle of the rim. `chipsOut`
    // lists the chips in the order they're taken out (front to back, one from each row in turn).
    Transform Case(Transform parent, bool fp, out Transform top, out GameObject[] chipsOut)
    {
        EnsureMats();
        var mats = BlockyCharacter.RuntimeMaterials();
        var metal = mats("ChipCase", new Color(0.8f, 0.82f, 0.85f)); metal.SetFloat("_Smoothness", 0.8f);
        var edge = mats("ChipCaseEdge", new Color(0.35f, 0.36f, 0.38f)); edge.SetFloat("_Smoothness", 0.6f);
        var felt = mats("ChipCaseFelt", new Color(0.07f, 0.07f, 0.09f));
        var root = new GameObject("ChipCase").transform;
        root.SetParent(parent, false);
        Rounded(root, new Vector3(0, -H / 2, 0), new Vector3(W, H, D), 0.012f, metal, fp);                                   // bottom shell
        Part(PrimitiveType.Cube, root, new Vector3(0, -0.004f, 0), new Vector3(W - 0.014f, 0.004f, D - 0.014f), felt, fp);   // lining
        Rounded(root, new Vector3(0, 0.001f, 0), new Vector3(W + 0.004f, 0.007f, D + 0.004f), 0.003f, edge, fp);             // rim
        var lid = new GameObject("Lid").transform; lid.SetParent(root, false); lid.localPosition = new Vector3(0, 0.004f, D / 2);
        lid.localRotation = Quaternion.Euler(LidOpen, 0, 0);                                                                  // laid well back
        Rounded(lid, new Vector3(0, 0.02f, -D / 2), new Vector3(W, 0.04f, D), 0.012f, metal, fp);
        Part(PrimitiveType.Cube, lid, new Vector3(0, -0.002f, -D / 2), new Vector3(W - 0.014f, 0.004f, D - 0.014f), felt, fp);
        Rounded(lid, new Vector3(0, -0.001f, -D / 2), new Vector3(W + 0.004f, 0.007f, D + 0.004f), 0.003f, edge, fp);
        var handle = Part(PrimitiveType.Capsule, root, new Vector3(0, -H / 2, -D / 2 - 0.02f), new Vector3(0.018f, 0.055f, 0.018f), edge, fp, new Vector3(0, 0, 90f));
        foreach (float x in new[] { -0.05f, 0.05f })                                                                          // handle posts
            Part(PrimitiveType.Cylinder, root, new Vector3(x, -H / 2, -D / 2 - 0.01f), new Vector3(0.012f, 0.01f, 0.012f), edge, fp, new Vector3(90f, 0, 0));
        foreach (float x in new[] { -0.11f, 0.11f })
            Rounded(root, new Vector3(x, -0.012f, -D / 2 - 0.004f), new Vector3(0.03f, 0.018f, 0.008f), 0.003f, edge, fp);    // latches
        chipsOut = new GameObject[CaseSize];
        for (int row = 0; row < Colors.Length; row++)                                                                        // one colour per row, chips on edge
        {
            var rowMat = mats("Chip" + Names[row], Colors[row]);
            var spotMat = mats("ChipEdge" + Names[row], row == 0 ? new Color(0.2f, 0.35f, 0.8f) : new Color(0.96f, 0.96f, 0.94f));
            float x = -W / 2 + 0.04f + row * (W - 0.08f) / (Colors.Length - 1);
            for (int n = 0; n < PerRow; n++)
            {
                var chip = Part(PrimitiveType.Cylinder, root, new Vector3(x, 0.018f, -D / 2 + 0.03f + n * 0.018f), new Vector3(0.04f, 0.0035f, 0.04f), n % 4 == 3 ? spotMat : rowMat, true, new Vector3(90f, 0, 0));
                if (!fp) chip.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                chipsOut[n * Colors.Length + row] = chip.gameObject;
            }
        }
        top = new GameObject("Top").transform; top.SetParent(root, false); top.localPosition = new Vector3(0.1f, 0.04f, -0.02f);
        return root;
    }

    protected override Transform BuildFirstPersonModel()
    {
        var c = Case(cam, true, out fpCaseTop, out fpChips);              // lying on your left forearm, as the body carries it
        c.localPosition = CasePos; c.localRotation = Quaternion.Euler(CaseEuler);
        shown = -1;
        return c;
    }

    protected override Transform BuildThirdPersonModel(BlockyCharacter body)
    {
        if (!body.handL) return null;
        var c = Case(body.handL, false, out _, out tpChips);             // carried flat on the forearm (placed every frame)
        shown = -1;
        return c;
    }
}
