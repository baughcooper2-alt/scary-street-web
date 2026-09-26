using UnityEngine;

// Box of Goldfish (DESIGN.md, Kenny's weapon): a big orange milk-carton-style box, open at the top, held in both hands.
// Left click tosses a handful out of the top: 6 crackers in a spread, 4.5 damage each, short range (a snack shotgun).
// Right click lobs the whole carton (needs 4 handfuls; uses what's left): it bursts for 22 damage in 3 m and the
// workers it hits stop to snack for 1.5 s. 12 handfuls, then 2.2 s to open a new carton.
public class GoldfishWeapon : MagazineWeapon
{
    public override UIArt.Icon Icon => UIArt.Icon.Fish;
    public float crackerDamage = 4.5f;
    public int crackersPerHandful = 6;
    public float handfulCooldown = 0.45f;
    public float boxDamage = 22f, boxRadius = 3f, snackTime = 1.5f;
    public int boxCost = 4;

    // first person: the carton sits low in front of you, a hand on each side
    static readonly Vector3 BoxRest = new Vector3(0f, -0.25f, 0.56f);
    static readonly Vector3 BoxSize = new Vector3(0.15f, 0.26f, 0.15f);        // a big milk carton

    float cooldown, tossT = -1f, tossDur = 0.3f;
    bool wasSecondary;
    static Material cracker, boxMat, boxBand, boxDark;

    protected override int BaseMagazine => 12;
    protected override float ReloadTime => 2.2f;
    protected override string ReloadText => "New carton…";
    protected override CharacterAnimator.Hold HoldPose => CharacterAnimator.Hold.Carton;

    public override void Init(WeaponInventory inv) { base.Init(inv); displayName = "Goldfish"; }
    public override void Equip() { base.Equip(); SyncModels(); }
    public override void Unequip() { base.Unequip(); if (arms) { arms.overrideLeft = false; arms.overrideRight = false; } SyncModels(); }
    public override string LevelUpText => level == 2 ? "+30% damage, more handfuls, and a bigger carton burst" : "+30% damage, more handfuls";
    public override string Hint => Reloading ? "Opening a new carton…"
        : $"{Key("left click", GamepadInfo.RT)} to toss a handful · {Key("right click", GamepadInfo.LT)} to lob the whole carton (they stop to snack)";

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
            else if (lob) inventory.Toast($"Need {boxCost} handfuls to throw the carton", 1.2f);
            else if (input.primaryPressed && ammo > 0) Handful();
        }
        Animate(dt, reloading);
        HoldBetweenHands();
    }

    // third person: the carton sits between the two hands
    void HoldBetweenHands()
    {
        var body = inventory.GetComponentInChildren<BlockyCharacter>();
        if (!tpModel || !body || !body.handL || !body.handR) return;
        Vector3 l = body.handL.TransformPoint(new Vector3(0.02f, -0.07f, 0)), r = body.handR.TransformPoint(new Vector3(-0.02f, -0.07f, 0));
        tpModel.position = (l + r) * 0.5f;
        tpModel.rotation = Quaternion.LookRotation(body.transform.forward, Vector3.up);
    }

    // both hands on the carton; a toss lifts and tips it forward; a new carton comes up from below
    void Animate(float dt, bool reloading)
    {
        if (!arms || !fpModel) return;
        float k = 0;
        if (tossT >= 0) { float p = (tossT += dt) / tossDur; k = Mathf.Sin(Mathf.Clamp01(p) * Mathf.PI); if (p >= 1f) tossT = -1f; }
        float down = reloading ? Mathf.Clamp01(1f - Mathf.Abs(1f - 2f * (1f - reloadT / ReloadTime))) : 0f;   // dips out and back
        fpModel.localPosition = BoxRest + new Vector3(0, 0.07f * k - 0.35f * down, 0.09f * k);
        fpModel.localRotation = Quaternion.Euler(-24f - 30f * k, 0, 0);          // top tipped toward you
        Vector3 side = fpModel.localRotation * new Vector3(BoxSize.x * 0.5f + 0.025f, -0.03f, -0.01f);
        arms.overrideLeft = arms.overrideRight = true;
        arms.leftTarget = fpModel.localPosition + new Vector3(-side.x, side.y, side.z);
        arms.rightTarget = fpModel.localPosition + side;
        arms.leftEuler = arms.rightEuler = new Vector3(-24f - 30f * k, 0, 0);
    }

    static void Mats()
    {
        var mats = BlockyCharacter.RuntimeMaterials();
        if (!cracker) cracker = mats("Goldfish", new Color(0.98f, 0.62f, 0.15f));
        if (!boxMat) boxMat = mats("GoldfishCarton", new Color(0.98f, 0.5f, 0.08f));
        if (!boxBand) boxBand = mats("GoldfishCartonBand", new Color(0.97f, 0.93f, 0.85f));
        if (!boxDark) boxDark = mats("GoldfishCartonInside", new Color(0.35f, 0.18f, 0.05f));
    }

    Vector3 Spout => fpModel ? fpModel.position + fpModel.up * (BoxSize.y * 0.5f + 0.04f) + fpModel.forward * BoxSize.z * 0.5f : Eye + cam.forward * 0.45f;

    void Handful()
    {
        cooldown = handfulCooldown;
        UseAmmo();
        tossT = 0; tossDur = 0.3f;
        if (BodyAnim) BodyAnim.Throw(0.3f, 0.5f);
        SoundKit.Play(Sfx.Throw, 0.5f, 0.15f);
        Mats();
        Vector3 from = ThirdPerson ? Eye + cam.forward * 0.45f + Vector3.down * 0.25f : Spout;
        for (int i = 0; i < crackersPerHandful; i++)
        {
            Vector3 dir = Quaternion.AngleAxis(Random.Range(-12f, 12f), Vector3.up) * Quaternion.AngleAxis(Random.Range(-8f, 2f), cam.right) * cam.forward;
            var p = Projectile.Spawn("Goldfish", from, dir * Random.Range(11f, 15f), inventory.gameObject);
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
        UseAmmo(ammo);                                                    // the whole carton goes: a new one after the reload
        tossT = 0; tossDur = 0.4f;
        if (BodyAnim) BodyAnim.Throw(0.5f, 0.55f);
        SoundKit.Play(Sfx.Throw, 0.7f);
        Mats();
        Vector3 v = (cam.forward + Vector3.up * 0.45f).normalized * 10f;
        var p = Projectile.Spawn("GoldfishCarton", Eye + cam.forward * 0.6f, v, inventory.gameObject);
        p.gravity = 12f; p.life = 3f; p.radius = 0.35f; p.burstOnWall = true;
        p.blastRadius = boxRadius + (level >= 3 ? 0.6f : 0f);
        p.blastDamage = PlayerStats.RangedDamage(boxDamage * LevelDamage, inventory.gameObject);
        p.stunSeconds = snackTime; p.burstSound = Sfx.Crunch;
        p.spin = 360f; p.spinAxis = Vector3.right;
        Carton(p.transform, true);
        p.onBurst = SpillCrackers;
    }

    // a shower of crackers where the carton burst (just for show)
    static void SpillCrackers(Vector3 at)
    {
        Mats();
        for (int i = 0; i < 18; i++)
        {
            var q = Projectile.Spawn("Goldfish", at + Vector3.up * 0.2f, (Random.insideUnitSphere + Vector3.up) * 3f, null);
            q.damage = 0; q.radius = 0.01f; q.life = Random.Range(0.4f, 0.8f); q.gravity = 9f;
            q.spin = 500f; q.spinAxis = Random.onUnitSphere;
            q.Piece(PrimitiveType.Sphere, Vector3.zero, new Vector3(0.022f, 0.014f, 0.034f), cracker);
        }
    }

    // a big orange milk carton: square body, gable top with the ridge fin, the front of the spout pulled open
    // (crackers showing inside), a cracker fish and a white band on the front
    static Transform Carton(Transform parent, bool fp)
    {
        Mats();
        var root = new GameObject("GoldfishCarton").transform;
        root.SetParent(parent, false);
        float W = BoxSize.x, H = BoxSize.y, D = BoxSize.z, g = 0.075f;
        float slope = Mathf.Atan2(g, D / 2) * Mathf.Rad2Deg, len = Mathf.Sqrt(g * g + D * D / 4);
        Part(PrimitiveType.Cube, root, Vector3.zero, new Vector3(W, H, D), boxMat, fp);                                           // body
        Part(PrimitiveType.Cube, root, new Vector3(0, 0.03f, 0), new Vector3(W + 0.002f, 0.045f, D + 0.002f), boxBand, fp);       // white band
        Part(PrimitiveType.Sphere, root, new Vector3(0, -0.05f, D / 2 + 0.004f), new Vector3(0.09f, 0.055f, 0.012f), cracker, fp); // the fish
        Part(PrimitiveType.Cube, root, new Vector3(-0.052f, -0.05f, D / 2 + 0.004f), new Vector3(0.028f, 0.036f, 0.01f), cracker, fp, new Vector3(0, 0, 45f));
        Part(PrimitiveType.Cube, root, new Vector3(0, H / 2 - 0.002f, 0), new Vector3(W - 0.01f, 0.004f, D - 0.01f), boxDark, fp);  // inside, seen through the spout
        // gable: back panel slopes up to the ridge; the front panel is folded out (the spout is open)
        Part(PrimitiveType.Cube, root, new Vector3(0, H / 2 + g / 2, -D / 4), new Vector3(W, 0.004f, len), boxMat, fp, new Vector3(-slope, 0, 0));
        Part(PrimitiveType.Cube, root, new Vector3(0, H / 2 + g * 0.35f, D / 2 + 0.02f), new Vector3(W * 0.96f, 0.004f, len), boxMat, fp, new Vector3(35f, 0, 0));
        Part(PrimitiveType.Cube, root, new Vector3(0, H / 2 + g + 0.008f, -0.004f), new Vector3(W, 0.018f, 0.004f), boxMat, fp);  // ridge fin
        foreach (float x in new[] { -W / 2, W / 2 })                                                                               // the folded side gussets
            Part(PrimitiveType.Cube, root, new Vector3(x, H / 2 + g * 0.4f, -D / 8), new Vector3(0.004f, g * 0.8f, D * 0.6f), boxMat, fp, new Vector3(-slope * 0.5f, 0, 0));
        for (int i = 0; i < 5; i++)                                                                                                 // crackers heaped at the opening
            Part(PrimitiveType.Sphere, root, new Vector3(-0.05f + i * 0.025f, H / 2 + 0.008f, 0.02f + (i % 2) * 0.02f), new Vector3(0.024f, 0.014f, 0.035f), cracker, fp, new Vector3(0, i * 40f, 0));
        return root;
    }

    protected override Transform BuildFirstPersonModel()
    {
        var b = Carton(cam, true);                                        // held out in front, both hands on it
        b.localPosition = BoxRest; b.localRotation = Quaternion.Euler(-8f, 0, 0);
        return b;
    }

    protected override Transform BuildThirdPersonModel(BlockyCharacter body)
    {
        var anchor = body.spine ? body.spine : body.handR;
        var b = Carton(anchor, false);                                    // at the chest, between the hands
        b.localPosition = new Vector3(0, 0.22f, 0.3f);
        return b;
    }
}
