using System.Collections.Generic;
using UnityEngine;

// Cooper's starting weapon (DESIGN.md): melee hits, and when charged you slam it on the ground yelling "OBJECTION".
// Web build numbers: 28 damage in a cone 2 m in front of you, knockback, 0.55 s between swings,
// 6 swings then 3 s "reading up on the law". Lv 3+ reaches further and shoves harder.
// Click to swing. Keep holding after a swing to charge; let go once it's charged to slam everyone around you.
public class LawBookWeapon : MagazineWeapon
{
    [Header("Swing")]
    public float damage = 28f;
    public float range = 2f;
    public float knockback = 0.6f;
    public float swingCooldown = 0.55f;
    [Header("OBJECTION slam")]
    public float chargeTime = 0.8f;
    public float slamDamage = 45f;
    public float slamRadius = 3.5f;
    public float slamKnockback = 1.5f;
    public int slamPages = 2;

    protected override int BaseMagazine => 6;
    protected override CharacterAnimator.Hold HoldPose => CharacterAnimator.Hold.Book;
    protected override float ReloadTime => 3f;
    protected override string ReloadText => "Reading up…";

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
        bool reloading = TickReload(dt, input.reloadPressed);

        if (!reloading)
        {
            if (input.primaryPressed && cooldown <= 0 && ammo > 0)
            {
                Swing();
                charging = true; charge = 0;
            }
            if (charging)
            {
                if (input.primaryHeld) charge = Mathf.Min(1f, charge + dt / chargeTime);
                else
                {
                    if (charge >= 1f && ammo > 0) Slam();
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
        UseAmmo();
        if (BodyAnim) BodyAnim.Swing(0.35f, 0.4f);
        SoundKit.Play(Sfx.Whoosh, 0.55f);

        Vector3 eye = Eye, fwd = cam.forward; fwd.y = 0; fwd.Normalize();
        float reach = range + (L3 ? 0.6f : 0f), shove = knockback * (L3 ? 1.5f : 1f);
        foreach (var h in Nearby(inventory.transform.position, reach))
        {
            Vector3 to = h.transform.position - inventory.transform.position; to.y = 0;
            if (to.sqrMagnitude > 0.01f && Vector3.Dot(to.normalized, fwd) < 0.35f) continue;   // only in front of you
            if (!ClearLine(eye, h.transform.position + Vector3.up * 1.2f)) continue;             // not through walls
            h.TakeDamage(PlayerStats.MeleeDamage(damage * LevelDamage, inventory.gameObject), shove + PlayerUpgrades.KnockbackFor(inventory.gameObject));
            SoundKit.PlayAt(Sfx.Punch, h.transform.position + Vector3.up, 0.9f);
            Rumble(0.3f, 0.5f, 0.1f);
        }
    }

    void Slam()
    {
        slamT = 0;
        UseAmmo(Mathf.Min(slamPages, ammo));
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
            if (Reloading) return "Reading up on the law…";
            if (charging && charge >= 1f) return "Charged: let go for OBJECTION!";
            return $"{Key("Left click", GamepadInfo.RT)} to swing · keep holding to charge the OBJECTION slam · {Key("R", "d-pad ↓")} to reload";
        }
    }

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
        var m = Book(body.handR, false);
        m.localPosition = new Vector3(0, -0.09f, 0.03f);
        m.localRotation = Quaternion.Euler(-90f, 90f, 0);
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
