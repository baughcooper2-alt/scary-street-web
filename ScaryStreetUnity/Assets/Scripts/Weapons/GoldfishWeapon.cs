using UnityEngine;

// Box of Goldfish (DESIGN.md, Kenny's weapon): a throwable. Left click tosses a handful: 6 crackers in a spread,
// 4.5 damage each, short range (a snack shotgun). Right click lobs the whole box (costs 4 handfuls): it bursts
// for 22 damage in 3 m and the workers it hits stop to snack for 1.5 s. 12 handfuls, then 2.2 s for a new box.
public class GoldfishWeapon : MagazineWeapon
{
    public override UIArt.Icon Icon => UIArt.Icon.Fish;
    public float crackerDamage = 4.5f;
    public int crackersPerHandful = 6;
    public float handfulCooldown = 0.45f;
    public float boxDamage = 22f, boxRadius = 3f, snackTime = 1.5f;
    public int boxCost = 4;

    float cooldown;
    bool wasSecondary;
    readonly HandMotion hand = new HandMotion();
    static Material cracker, boxMat, boxBand;

    protected override int BaseMagazine => 12;
    protected override float ReloadTime => 2.2f;
    protected override string ReloadText => "New box…";
    protected override CharacterAnimator.Hold HoldPose => CharacterAnimator.Hold.Book;

    public override void Init(WeaponInventory inv) { base.Init(inv); displayName = "Goldfish"; }
    public override void Equip() { base.Equip(); SyncModels(); }
    public override void Unequip() { base.Unequip(); hand.Release(arms); SyncModels(); }
    public override string LevelUpText => level == 2 ? "+30% damage, more handfuls, and a bigger box burst" : "+30% damage, more handfuls";
    public override string Hint => Reloading ? "Opening a new box…"
        : $"{Key("left click", GamepadInfo.RT)} to toss a handful · {Key("right click", GamepadInfo.LT)} to lob the whole box (they stop to snack)";

    public override void Tick(WeaponInput input)
    {
        float dt = Time.deltaTime;
        SyncModels();
        cooldown -= dt;
        bool lob = input.secondaryHeld && !wasSecondary;
        wasSecondary = input.secondaryHeld;
        bool reloading = TickReload(dt, input.reloadPressed);
        if (!reloading && cooldown <= 0)
        {
            if (lob && ammo >= boxCost) LobBox();
            else if (lob) inventory.Toast($"Need {boxCost} handfuls to throw the box", 1.2f);
            else if (input.primaryPressed && ammo > 0) Handful();
        }
        hand.Apply(arms, dt);
    }

    static void Mats()
    {
        var mats = BlockyCharacter.RuntimeMaterials();
        if (!cracker) cracker = mats("Goldfish", new Color(0.98f, 0.62f, 0.15f));
        if (!boxMat) boxMat = mats("GoldfishBox", new Color(0.2f, 0.45f, 0.85f));
        if (!boxBand) boxBand = mats("GoldfishBoxBand", new Color(0.98f, 0.8f, 0.2f));
    }

    void Handful()
    {
        cooldown = handfulCooldown;
        UseAmmo();
        hand.Play(HandMotion.Move.Throw, 0.25f);
        if (BodyAnim) BodyAnim.Throw(0.3f, 0.5f);
        SoundKit.Play(Sfx.Throw, 0.5f, 0.15f);
        Mats();
        for (int i = 0; i < crackersPerHandful; i++)
        {
            Vector3 dir = Quaternion.AngleAxis(Random.Range(-12f, 12f), Vector3.up) * Quaternion.AngleAxis(Random.Range(-6f, 4f), cam.right) * cam.forward;
            var p = Projectile.Spawn("Goldfish", Eye + cam.forward * 0.45f + Vector3.down * 0.12f, dir * Random.Range(11f, 15f), inventory.gameObject);
            p.damage = PlayerStats.RangedDamage(crackerDamage * LevelDamage, inventory.gameObject);
            p.radius = 0.16f; p.life = 0.6f; p.gravity = 3f; p.hitSound = Sfx.Crunch;
            p.spin = Random.Range(-600f, 600f); p.spinAxis = Random.onUnitSphere;
            p.Piece(PrimitiveType.Sphere, Vector3.zero, new Vector3(0.022f, 0.014f, 0.034f), cracker);
            p.Piece(PrimitiveType.Cube, new Vector3(0, 0, -0.02f), new Vector3(0.018f, 0.006f, 0.01f), cracker, new Vector3(0, 45f, 0));
        }
    }

    void LobBox()
    {
        cooldown = 1f;
        UseAmmo(boxCost);
        hand.Play(HandMotion.Move.Throw, 0.4f);
        if (BodyAnim) BodyAnim.Throw(0.5f, 0.55f);
        SoundKit.Play(Sfx.Throw, 0.7f);
        Mats();
        Vector3 v = (cam.forward + Vector3.up * 0.45f).normalized * 10f;
        var p = Projectile.Spawn("GoldfishBox", Eye + cam.forward * 0.5f, v, inventory.gameObject);
        p.gravity = 12f; p.life = 3f; p.radius = 0.3f; p.burstOnWall = true;
        p.blastRadius = boxRadius + (level >= 3 ? 0.6f : 0f);
        p.blastDamage = PlayerStats.RangedDamage(boxDamage * LevelDamage, inventory.gameObject);
        p.stunSeconds = snackTime; p.burstSound = Sfx.Crunch;
        p.spin = 360f; p.spinAxis = Vector3.right;
        Box(p.transform, true);
        p.onBurst = SpillCrackers;
    }

    // a little shower of crackers where the box burst (just for show)
    static void SpillCrackers(Vector3 at)
    {
        Mats();
        for (int i = 0; i < 14; i++)
        {
            var q = Projectile.Spawn("Goldfish", at + Vector3.up * 0.2f, (Random.insideUnitSphere + Vector3.up) * 3f, null);
            q.damage = 0; q.radius = 0.01f; q.life = Random.Range(0.4f, 0.8f); q.gravity = 9f;
            q.spin = 500f; q.spinAxis = Random.onUnitSphere;
            q.Piece(PrimitiveType.Sphere, Vector3.zero, new Vector3(0.022f, 0.014f, 0.034f), cracker);
        }
    }

    // a small blue box with a yellow band (a smiling cracker on the front)
    static Transform Box(Transform parent, bool fp)
    {
        Mats();
        var root = new GameObject("GoldfishBox").transform;
        root.SetParent(parent, false);
        Part(PrimitiveType.Cube, root, Vector3.zero, new Vector3(0.07f, 0.1f, 0.035f), boxMat, fp);
        Part(PrimitiveType.Cube, root, new Vector3(0, 0.015f, 0), new Vector3(0.071f, 0.025f, 0.036f), boxBand, fp);
        Part(PrimitiveType.Sphere, root, new Vector3(0, -0.02f, 0.0185f), new Vector3(0.035f, 0.022f, 0.004f), cracker, fp);
        return root;
    }

    protected override Transform BuildFirstPersonModel()
    {
        var b = Box(arms.RightHand, true);
        b.localPosition = new Vector3(0, 0.03f, 0.02f); b.localRotation = Quaternion.Euler(-15f, 10f, 0);
        return b;
    }

    protected override Transform BuildThirdPersonModel(BlockyCharacter body)
    {
        var b = Box(body.handR, false);
        b.localPosition = new Vector3(0, -0.08f, 0.03f);
        return b;
    }
}
