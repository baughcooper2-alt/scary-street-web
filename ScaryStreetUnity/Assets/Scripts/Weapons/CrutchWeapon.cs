using UnityEngine;

// Crutch (DESIGN.md, Piper's weapon): long-reach melee. Left click swings it wide (18 damage to up to 2 enemies in
// front of you, out to 2.6 m, big shove); right click pokes straight ahead (11 damage, 3.2 m, quick).
// No ammo. Level ups: +30% damage; Lv 3+ reaches further.
public class CrutchWeapon : Weapon
{
    public override UIArt.Icon Icon => UIArt.Icon.Crutch;
    public float swingDamage = 18f, swingReach = 2.6f, swingCooldown = 0.8f, swingShove = 1.2f;
    public float pokeDamage = 11f, pokeReach = 3.2f, pokeCooldown = 0.4f, pokeShove = 0.6f;

    float cooldown;
    bool wasSecondary;
    readonly HandMotion hand = new HandMotion();
    static readonly Collider[] hits = new Collider[16];

    protected override CharacterAnimator.Hold HoldPose => CharacterAnimator.Hold.Book;
    float Extra => level >= 3 ? 0.4f : 0f;

    public override void Init(WeaponInventory inv) { base.Init(inv); displayName = "Crutch"; }
    public override void Equip() { base.Equip(); SyncModels(); }
    public override void Unequip() { base.Unequip(); hand.Release(arms); SyncModels(); }
    public override string LevelUpText => level == 2 ? "+30% damage and longer reach" : "+30% damage";
    public override string Hint => $"{Key("left click", GamepadInfo.RT)} to swing wide · {Key("right click", GamepadInfo.LT)} to poke far";

    public override void Tick(WeaponInput input)
    {
        float dt = Time.deltaTime;
        SyncModels();
        cooldown -= dt;
        bool poke = input.secondaryHeld && !wasSecondary;
        wasSecondary = input.secondaryHeld;
        if (cooldown <= 0)
        {
            if (input.primaryPressed) Swing();
            else if (poke) Poke();
        }
        hand.Apply(arms, dt);
    }

    void Swing()
    {
        cooldown = swingCooldown;
        hand.Play(HandMotion.Move.Swing, 0.4f);
        if (BodyAnim) BodyAnim.Swing(0.45f, 0.45f);
        SoundKit.Play(Sfx.Whoosh, 0.6f);
        Vector3 fwd = cam.forward; fwd.y = 0; fwd.Normalize();
        var targets = EnemyTargets.Around(inventory.transform.position + Vector3.up * 0.9f, swingReach + Extra);
        targets.Sort((a, b) => (a.transform.position - inventory.transform.position).sqrMagnitude.CompareTo((b.transform.position - inventory.transform.position).sqrMagnitude));
        int struck = 0;
        foreach (var h in targets)
        {
            if (struck >= 2) break;
            Vector3 to = h.transform.position - inventory.transform.position; to.y = 0;
            if (to.sqrMagnitude > 0.01f && Vector3.Dot(to.normalized, fwd) < 0.72f) continue;          // a narrow arc
            if (Physics.Linecast(Eye, h.transform.position + Vector3.up * 1.2f, out var wall, ~0, QueryTriggerInteraction.Ignore) && !wall.collider.GetComponentInParent<Health>()) continue;
            h.TakeDamage(PlayerStats.MeleeDamage(swingDamage * LevelDamage, inventory.gameObject), swingShove + PlayerUpgrades.KnockbackFor(inventory.gameObject));
            SoundKit.PlayAt(Sfx.Punch, h.transform.position + Vector3.up, 0.9f);
            Rumble(0.35f, 0.55f, 0.12f);
            struck++;
        }
    }

    void Poke()
    {
        cooldown = pokeCooldown;
        hand.Play(HandMotion.Move.Poke, 0.22f);
        if (BodyAnim) BodyAnim.Punch(0.25f, 0.45f);
        SoundKit.Play(Sfx.Whoosh, 0.4f, 0.2f);
        Vector3 fwd = cam.forward;
        var hitsAll = Physics.SphereCastAll(Eye, 0.35f, fwd, pokeReach + Extra, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hitsAll, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hitsAll)
        {
            var h = hit.collider.GetComponentInParent<Health>();
            if (!h) { if (hit.distance > 0.3f) break; else continue; }                                   // a wall first: nothing
            if (h.IsDead || h.GetComponent<FirstPersonController>()) continue;
            h.TakeDamage(PlayerStats.MeleeDamage(pokeDamage * LevelDamage, inventory.gameObject), pokeShove + PlayerUpgrades.KnockbackFor(inventory.gameObject));
            SoundKit.PlayAt(Sfx.Hit, hit.point, 0.8f);
            Rumble(0.2f, 0.4f, 0.08f);
            break;
        }
    }

    // aluminium crutch: long pole, grey arm pad on top, hand grip part way down, rubber foot
    Transform Crutch(Transform parent, bool fp)
    {
        var mats = BlockyCharacter.RuntimeMaterials();
        var metal = mats("CrutchMetal", new Color(0.72f, 0.74f, 0.78f)); metal.SetFloat("_Smoothness", 0.7f);
        var pad = mats("CrutchPad", new Color(0.18f, 0.18f, 0.2f));
        var root = new GameObject("Crutch").transform;
        root.SetParent(parent, false);
        Part(PrimitiveType.Cylinder, root, new Vector3(0, 0.2f, 0), new Vector3(0.025f, 0.6f, 0.025f), metal, fp);               // pole
        Part(PrimitiveType.Capsule, root, new Vector3(0, 0.82f, 0), new Vector3(0.045f, 0.09f, 0.045f), pad, fp, new Vector3(0, 0, 90f));   // arm pad
        Part(PrimitiveType.Cylinder, root, new Vector3(0, 0.74f, 0), new Vector3(0.018f, 0.06f, 0.018f), metal, fp);             // pad post
        Part(PrimitiveType.Cylinder, root, new Vector3(0.05f, 0f, 0), new Vector3(0.022f, 0.05f, 0.022f), pad, fp, new Vector3(0, 0, 90f));  // hand grip
        Part(PrimitiveType.Cylinder, root, new Vector3(0, -0.41f, 0), new Vector3(0.035f, 0.02f, 0.035f), pad, fp);              // rubber foot
        return root;
    }

    protected override Transform BuildFirstPersonModel()
    {
        var c = Crutch(arms.RightHand, true);
        c.localPosition = new Vector3(-0.04f, 0f, 0.02f); c.localRotation = Quaternion.Euler(75f, 0, 0); c.localScale = Vector3.one * 0.8f;
        return c;
    }

    protected override Transform BuildThirdPersonModel(BlockyCharacter body)
    {
        var c = Crutch(body.handR, false);
        c.localPosition = new Vector3(-0.05f, -0.08f, 0.03f); c.localRotation = Quaternion.Euler(90f, 0, 0);
        return c;
    }
}
