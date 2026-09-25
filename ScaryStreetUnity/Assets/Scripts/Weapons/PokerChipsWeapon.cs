using UnityEngine;

// Poker Chips (DESIGN.md): upgrades change the chip colour, white → red → blue → green → black, and every step
// hits harder and flies further. Left click flicks a spinning chip that bounces on to the next enemy
// (twice from green). 10 chips, then 1.6 s restacking.
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

    float cooldown;
    readonly HandMotion hand = new HandMotion();
    Material chipMat, edgeMat;

    protected override int BaseMagazine => 10;
    protected override float ReloadTime => 1.6f;
    protected override string ReloadText => "Restacking…";
    protected override CharacterAnimator.Hold HoldPose => CharacterAnimator.Hold.Book;

    int Step => Mathf.Clamp(level - 1, 0, Names.Length - 1);
    public string ChipName => Names[Step];
    float Damage => baseDamage * (1f + 0.35f * Step);
    float Range => baseRange + 3f * Step;

    public override void Init(WeaponInventory inv) { base.Init(inv); displayName = "Poker Chips"; }
    public override void Equip() { base.Equip(); SyncModels(); }
    public override void Unequip() { base.Unequip(); hand.Release(arms); SyncModels(); }

    public override bool CanLevelUp => level < Names.Length;
    public override string LevelUpText => level < Names.Length ? $"{Names[Step + 1]} chips: more damage and range" + (Step + 1 == 3 ? ", and they bounce twice" : "") : "Maxed out";
    public override void LevelUp() { base.LevelUp(); RecolorModels(); }
    public override string Hint => Reloading ? "Restacking your chips…" : $"{Key("left click", GamepadInfo.RT)} to flick a {ChipName.ToLower()} chip: it bounces to the next worker · {Key("R", "d-pad ↓")} to restack";

    public override void Tick(WeaponInput input)
    {
        float dt = Time.deltaTime;
        SyncModels();
        cooldown -= dt;
        bool reloading = TickReload(dt, input.reloadPressed);
        if (!reloading && input.primaryHeld && cooldown <= 0 && ammo > 0) Throw();
        hand.Apply(arms, dt);
    }

    void Throw()
    {
        cooldown = throwCooldown;
        UseAmmo();
        hand.Play(HandMotion.Move.Throw, 0.22f);
        if (BodyAnim) BodyAnim.Throw(0.3f, 0.5f);
        float speed = 17f + 1.5f * Step;
        var p = Projectile.Spawn("PokerChip", Eye + cam.forward * 0.5f + Vector3.down * 0.12f, cam.forward * speed, inventory.gameObject);
        p.damage = PlayerStats.RangedDamage(Damage, inventory.gameObject);
        p.radius = 0.25f; p.life = Range / speed; p.bounces = Step >= 3 ? 2 : 1; p.bounceRange = 5f + Step;
        p.spin = 900f; p.spinAxis = Vector3.up; p.hitSound = Sfx.Chip;
        Chip(p.transform, false);
        SoundKit.Play(Sfx.Chip, 0.4f, 0.1f);
    }

    void EnsureMats()
    {
        var mats = BlockyCharacter.RuntimeMaterials();
        chipMat = mats("Chip" + ChipName, Colors[Step]);
        edgeMat = mats("ChipEdge" + ChipName, Step == 0 ? new Color(0.2f, 0.35f, 0.8f) : new Color(0.96f, 0.96f, 0.94f));
    }

    // a clay chip: coloured disc with contrasting edge spots
    void Chip(Transform parent, bool fp)
    {
        EnsureMats();
        var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(parent, false); body.transform.localScale = new Vector3(0.04f, 0.0035f, 0.04f);
        var r = body.GetComponent<Renderer>(); r.sharedMaterial = chipMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 3f;
            Part(PrimitiveType.Cube, parent, new Vector3(Mathf.Cos(a) * 0.018f, 0, Mathf.Sin(a) * 0.018f), new Vector3(0.006f, 0.0074f, 0.006f), edgeMat, true, new Vector3(0, -a * Mathf.Rad2Deg, 0));
        }
    }

    // a short stack of chips between your fingers
    Transform Stack(Transform parent, bool fp)
    {
        var root = new GameObject("ChipStack").transform;
        root.SetParent(parent, false);
        for (int i = 0; i < 4; i++)
        {
            var c = new GameObject("Chip").transform; c.SetParent(root, false);
            c.localPosition = new Vector3(0.002f * i, 0.0075f * i, 0);
            Chip(c, fp);
        }
        return root;
    }

    void RecolorModels()
    {
        EnsureMats();
        foreach (var m in new[] { fpModel, tpModel })
            if (m) foreach (var r in m.GetComponentsInChildren<Renderer>())
                r.sharedMaterial = r.transform.localScale.x > 0.02f ? chipMat : edgeMat;
    }

    protected override Transform BuildFirstPersonModel()
    {
        var s = Stack(arms.RightHand, true);
        s.localPosition = new Vector3(0, 0.03f, 0.03f); s.localRotation = Quaternion.Euler(-70f, 0, 0);
        return s;
    }

    protected override Transform BuildThirdPersonModel(BlockyCharacter body)
    {
        var s = Stack(body.handR, false);
        s.localPosition = new Vector3(0, -0.08f, 0.03f);
        return s;
    }
}
