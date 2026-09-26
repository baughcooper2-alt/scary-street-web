using System.Collections.Generic;
using UnityEngine;

// Cooper's starting weapon (DESIGN.md): melee hits, and when charged you slam it on the ground yelling "OBJECTION".
// Toned down from the web build (it one-shot workers and cleared crowds): 16 damage to the 3 closest enemies in
// a ~105° cone 1.7 m in front of you, 0.65 s between swings. No reloading (it's a melee weapon).
// The OBJECTION slam takes 1.1 s to charge, hits 28 in 2.8 m, and needs 3.5 s before the next one.
// Right click throws the book (24 damage, goes through 2): it lands on the floor and you walk over it to get it
// back (fists until then). Lv 3+ reaches further, shoves harder.
// Click to swing. Keep holding after a swing to charge; let go once it's charged to slam everyone around you.
public class LawBookWeapon : Weapon
{
    public override UIArt.Icon Icon => UIArt.Icon.Book;
    [Header("Swing")]
    public float damage = 16f;
    public float range = 1.7f;
    public float knockback = 0.5f;
    public float swingCooldown = 0.65f;
    [Tooltip("Most enemies one swing can hit (closest first).")] public int maxTargets = 3;
    [Header("OBJECTION slam")]
    public float chargeTime = 1.1f;
    public float slamDamage = 28f;
    public float slamRadius = 2.8f;
    public float slamKnockback = 1.2f;
    public float slamCooldown = 3.5f;
    [Header("Throw")]
    public float throwDamage = 24f, throwSpeed = 15f;

    protected override CharacterAnimator.Hold HoldPose => thrown ? CharacterAnimator.Hold.None : CharacterAnimator.Hold.Book;

    bool thrown, wasSecondary;
    float slamReady;
    LawBookPickup onFloor;
    public bool Thrown => thrown;

    bool L3 => level >= 3;
    float cooldown, swingT = -1f, slamT = -1f, charge;
    bool charging;
    static readonly Collider[] hits = new Collider[32];

    public override void Init(WeaponInventory inv)
    {
        base.Init(inv);
        displayName = "Law Book";
    }

    public override void Equip() { base.Equip(); SyncModels(); }

    public override void Unequip()
    {
        base.Unequip();
        charging = false; charge = 0;
        if (arms) arms.overrideRight = false;
        SyncModels();
    }

    public override string LevelUpText => level + 1 >= 3 && level < 3 ? "+30% damage, more pages, longer reach and bigger shove" : "+30% damage, more pages";

    public override void Tick(WeaponInput input)
    {
        float dt = Time.deltaTime;
        SyncModels();
        cooldown -= dt;
        slamReady -= dt;
        bool throwPressed = input.secondaryHeld && !wasSecondary;
        wasSecondary = input.secondaryHeld;
        bool reloading = false;
        if (thrown)
        {
            // the book's on the floor somewhere: fists until you pick it up
            if (fpModel) fpModel.gameObject.SetActive(false);
            if (tpModel) tpModel.gameObject.SetActive(false);
            var fists = inventory.GetComponent<PlayerPunch>(); if (fists) fists.allowInput = true;
            if (arms) arms.overrideRight = false;
            return;
        }
        if (throwPressed && cooldown <= 0 && !charging) { Throw(); return; }

        if (!reloading)
        {
            if (input.primaryPressed && cooldown <= 0)
            {
                Swing();
                charging = true; charge = 0;
            }
            if (charging)
            {
                if (input.primaryHeld) charge = Mathf.Min(1f, charge + dt / chargeTime);
                else
                {
                    if (charge >= 1f && slamReady <= 0) Slam();
                    else if (charge >= 1f) inventory.Toast("Objection overruled: wait a sec", 1f);
                    charging = false; charge = 0;
                }
            }
        }
        else { charging = false; charge = 0; }

        Animate(dt, reloading);
    }

    void Swing()
    {
        cooldown = swingCooldown;
        swingT = 0;
        if (BodyAnim) BodyAnim.Swing(0.35f, 0.4f);
        SoundKit.Play(Sfx.Whoosh, 0.55f);

        Vector3 eye = Eye, fwd = cam.forward; fwd.y = 0; fwd.Normalize();
        float reach = range + (L3 ? 0.6f : 0f), shove = knockback * (L3 ? 1.5f : 1f);
        var targets = Nearby(inventory.transform.position, reach);
        targets.Sort((a, b) => (a.transform.position - inventory.transform.position).sqrMagnitude.CompareTo((b.transform.position - inventory.transform.position).sqrMagnitude));
        int struck = 0;
        foreach (var h in targets)
        {
            if (struck >= maxTargets) break;
            Vector3 to = h.transform.position - inventory.transform.position; to.y = 0;
            if (to.sqrMagnitude > 0.01f && Vector3.Dot(to.normalized, fwd) < 0.6f) continue;    // only in front of you
            if (!ClearLine(eye, h.transform.position + Vector3.up * 1.2f)) continue;             // not through walls
            struck++;
            h.TakeDamage(PlayerStats.MeleeDamage(damage * LevelDamage, inventory.gameObject), shove + PlayerUpgrades.KnockbackFor(inventory.gameObject));
            SoundKit.PlayAt(Sfx.Punch, h.transform.position + Vector3.up, 0.9f);
            Rumble(0.3f, 0.5f, 0.1f);
        }
    }

    void Slam()
    {
        slamT = 0;
        slamReady = slamCooldown;
        cooldown = swingCooldown * 1.5f;
        inventory.Toast("OBJECTION!", 1.4f);
        SoundKit.Play(Sfx.Slam, 0.9f); SoundKit.Play(Sfx.Objection, 0.7f, 0f);
        if (arms) arms.Kick(1.8f);
        Rumble(1f, 0.8f, 0.4f);
        if (BodyAnim) BodyAnim.Slam(0.5f);
        float radius = slamRadius + (L3 ? 0.5f : 0f);
        foreach (var h in Nearby(inventory.transform.position, radius))
            h.TakeDamage(PlayerStats.MeleeDamage(slamDamage * LevelDamage, inventory.gameObject), slamKnockback + PlayerUpgrades.KnockbackFor(inventory.gameObject));
    }

    // Enemies on your floor within `radius` (anything with Health except you).
    List<Health> Nearby(Vector3 center, float radius)
    {
        var list = new List<Health>();
        int n = Physics.OverlapSphereNonAlloc(center + Vector3.up * 0.9f, radius, hits, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var h = hits[i].GetComponentInParent<Health>();
            if (!h || h == owner || h.IsDead || list.Contains(h)) continue;
            if (Mathf.Abs(h.transform.position.y - center.y) > 1.2f) continue;
            list.Add(h);
        }
        return list;
    }

    static bool ClearLine(Vector3 from, Vector3 to)
    {
        if (!Physics.Linecast(from, to, out var hit, ~0, QueryTriggerInteraction.Ignore)) return true;
        return hit.collider.GetComponentInParent<Health>();          // the first thing in the way is an enemy, not a wall
    }

    // First-person: swing across, raise it overhead while charging, bring it down on the slam.
    void Animate(float dt, bool reloading)
    {
        if (!arms) return;
        Vector3 pos = arms.restPosition + new Vector3(-0.02f, 0.02f, 0);
        Vector3 rot = new Vector3(0, -12f, 0);

        if (reloading)                                               // hold it open in front of you and read
        {
            pos = new Vector3(0.08f, -0.14f, 0.42f); rot = new Vector3(-40f, -30f, 0);
        }
        if (swingT >= 0)
        {
            float p = (swingT += dt) / 0.3f, s = Mathf.Sin(Mathf.Clamp01(p) * Mathf.PI);
            pos += new Vector3(-0.3f, 0.06f, 0.12f) * s;
            rot += new Vector3(-10f, 0, 55f) * s;
            if (p >= 1f) swingT = -1f;
        }
        if (charging && charge > 0.15f)
        {
            float k = Mathf.SmoothStep(0, 1, (charge - 0.15f) / 0.85f);
            pos = Vector3.Lerp(pos, new Vector3(0.06f, 0.14f, 0.36f), k);
            rot = Vector3.Lerp(rot, new Vector3(-75f, -10f, 0), k);
            pos += Random.insideUnitSphere * 0.004f * k;             // trembling with the charge
        }
        if (slamT >= 0)
        {
            float p = (slamT += dt) / 0.35f, s = Mathf.Sin(Mathf.Clamp01(p) * Mathf.PI);
            pos = Vector3.Lerp(arms.restPosition, new Vector3(0.02f, -0.3f, 0.5f), s);
            rot = new Vector3(55f * s, -10f, 0);
            if (p >= 1f) slamT = -1f;
        }
        arms.overrideRight = true;
        arms.rightTarget = pos;
        arms.rightEuler = rot;
    }

    public override string Hint
    {
        get
        {
            if (thrown) return "Your book's on the floor: walk over it to pick it up (fists till then)";
            if (charging && charge >= 1f) return slamReady > 0 ? "Slam's not ready yet" : "Charged: let go for OBJECTION!";
            return $"{Key("Left click", GamepadInfo.RT)} to swing · keep holding to charge the OBJECTION slam · {Key("right click", GamepadInfo.LT)} to throw it";
        }
    }

    public override string SlotStatus => thrown ? "Thrown" : slamReady > 0 ? "" : "Slam ready";

    // ---------- throwing it ----------

    void Throw()
    {
        thrown = true; cooldown = 0.4f; charging = false; charge = 0;
        if (BodyAnim) BodyAnim.Throw(0.45f, 0.5f);
        SoundKit.Play(Sfx.Throw, 0.7f);
        var p = Projectile.Spawn("ThrownLawBook", Eye + cam.forward * 0.5f, cam.forward * throwSpeed + Vector3.up * 1.5f, inventory.gameObject);
        p.damage = PlayerStats.MeleeDamage(throwDamage * LevelDamage, inventory.gameObject);
        p.gravity = 6f; p.life = 3f; p.radius = 0.35f; p.pierce = 1; p.knockback = 1f;
        p.spin = 800f; p.spinAxis = Vector3.right; p.hitSound = Sfx.Punch;
        Book(p.transform, true);
        p.onEnd = at => onFloor = LawBookPickup.Drop(at, this);
        if (fpModel) fpModel.gameObject.SetActive(false);
        if (tpModel) tpModel.gameObject.SetActive(false);
        if (BodyAnim) BodyAnim.hold = CharacterAnimator.Hold.None;
    }

    // Walked over it: back in your hand.
    public void PickedUp()
    {
        thrown = false; onFloor = null;
        inventory.Toast("Got your book back", 1.2f);
        SoundKit.Play(Sfx.Xp, 0.5f, 0f);
        if (inventory.Current == this) { var fists = inventory.GetComponent<PlayerPunch>(); if (fists) fists.allowInput = false; }
        SyncModels();
    }

    public Transform BuildBook(Transform parent, bool fp) => Book(parent, fp);

    // ---------- models ----------

    protected override Transform BuildFirstPersonModel()
    {
        var m = Book(arms.RightHand, true);
        m.localPosition = new Vector3(0, 0.06f, 0.02f);
        m.localRotation = Quaternion.Euler(0, -75f, 0);
        return m;
    }

    protected override Transform BuildThirdPersonModel(BlockyCharacter body)
    {
        var m = Book(body.handR, false);                                 // hanging from the hand, fingers hooked over the top edge
        m.localPosition = new Vector3(-0.02f, -0.18f, 0.01f);
        m.localRotation = Quaternion.identity;
        return m;
    }

    // Thick maroon hardcover with cream pages and gold bands on the spine.
    Transform Book(Transform parent, bool fp)
    {
        var root = new GameObject("LawBook").transform;
        root.SetParent(parent, false);
        var mats = BlockyCharacter.RuntimeMaterials();
        Part(PrimitiveType.Cube, root, Vector3.zero, new Vector3(0.05f, 0.23f, 0.17f), mats("Cover", new Color(0.42f, 0.12f, 0.14f)), fp);
        Part(PrimitiveType.Cube, root, new Vector3(0, 0, 0.006f), new Vector3(0.042f, 0.22f, 0.165f), mats("Pages", new Color(0.93f, 0.9f, 0.8f)), fp);
        foreach (float y in new[] { 0.08f, -0.08f })
            Part(PrimitiveType.Cube, root, new Vector3(0, y, -0.0855f), new Vector3(0.052f, 0.012f, 0.002f), mats("Gold", new Color(0.85f, 0.67f, 0.25f)), fp);
        Part(PrimitiveType.Cube, root, new Vector3(0, 0.02f, -0.0855f), new Vector3(0.036f, 0.05f, 0.002f), mats("Gold", new Color(0.85f, 0.67f, 0.25f)), fp);
        return root;
    }
}
