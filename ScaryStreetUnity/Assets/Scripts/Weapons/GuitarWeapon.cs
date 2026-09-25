using UnityEngine;

// Nathan's starting weapon (DESIGN.md): strum musical notes at enemies.
// Web build numbers: each strum sends 2 notes (3 from Lv 3) that chase the nearest worker, 12 damage each,
// 0.5 s between strums, 8 strums then 1.3 s "tuning the strings". Hold left click to keep strumming.
public class GuitarWeapon : MagazineWeapon
{
    public override UIArt.Icon Icon => UIArt.Icon.Guitar;
    public float noteDamage = 12f;
    public float noteSpeed = 8f;
    public float noteLife = 2.4f;
    public float homing = 5f;
    public float strumCooldown = 0.5f;

    protected override int BaseMagazine => 8;
    protected override CharacterAnimator.Hold HoldPose => CharacterAnimator.Hold.Guitar;
    protected override float ReloadTime => 1.3f;
    protected override string ReloadText => "Tuning…";

    float cooldown, strumT = -1f;
    int strums;
    Transform neckGrip, strumPoint;

    public override void Init(WeaponInventory inv)
    {
        base.Init(inv);
        displayName = "Guitar";
    }

    public override void Equip() { base.Equip(); SyncModels(); }

    public override void Unequip()
    {
        base.Unequip();
        if (arms) { arms.overrideRight = false; arms.overrideLeft = false; }
        SyncModels();
    }

    public override string LevelUpText => level == 2 ? "+30% damage, more strums, and a third note every strum" : "+30% damage, more strums";

    public override void Tick(WeaponInput input)
    {
        float dt = Time.deltaTime;
        SyncModels();
        cooldown -= dt;
        bool reloading = TickReload(dt, input.reloadPressed);

        if (!reloading && input.primaryHeld && cooldown <= 0 && ammo > 0) Strum();
        Animate(dt, reloading);
    }

    void Strum()
    {
        cooldown = strumCooldown;
        strumT = 0;
        UseAmmo();
        if (BodyAnim) BodyAnim.Strum(0.25f);
        SoundKit.Play(Sfx.Strum, 0.7f, 0f);

        Vector3 dir = cam.forward, from = Eye + dir * 0.6f + Vector3.down * 0.25f;
        int notes = level >= 3 ? 3 : 2;
        for (int k = 0; k < notes; k++)
        {
            var d = dir + cam.right * Random.Range(-0.12f, 0.12f) + Vector3.up * Random.Range(0.05f, 0.13f);
            MusicNote.Spawn(from + Vector3.up * (k * 0.1f), d, PlayerStats.RangedDamage(noteDamage * LevelDamage, inventory.gameObject),
                            noteSpeed, noteLife, homing, inventory.gameObject, strums + k);
        }
        strums++;
    }

    // First-person: left hand on the neck, right hand strumming over the sound hole.
    void Animate(float dt, bool reloading)
    {
        if (!arms || !fpModel || !neckGrip) return;
        float s = 0;
        if (strumT >= 0) { float p = (strumT += dt) / 0.18f; s = Mathf.Sin(Mathf.Clamp01(p) * Mathf.PI); if (p >= 1f) strumT = -1f; }
        float tune = reloading ? Mathf.Sin(Time.time * 9f) * 0.01f : 0f;   // fiddling with the tuning pegs

        arms.overrideLeft = true;
        arms.leftTarget = cam.InverseTransformPoint(neckGrip.position) + new Vector3(0, tune, 0);
        arms.leftEuler = new Vector3(-20f, 30f, -40f);
        arms.overrideRight = true;
        arms.rightTarget = cam.InverseTransformPoint(strumPoint.position) + new Vector3(0, 0.05f - 0.13f * s, -0.01f);
        arms.rightEuler = new Vector3(20f * s, -10f, 10f);
    }

    public override string Hint => Reloading ? "Tuning the strings…" : $"Hold {Key("left click", GamepadInfo.RT)} to strum: the notes chase the nearest worker · {Key("R", "d-pad ↓")} to reload";

    // ---------- models ----------

    // In first person the guitar hangs across the bottom of the view, neck up to the left.
    protected override Transform BuildFirstPersonModel()
    {
        var g = Guitar(cam, true, out neckGrip, out strumPoint);   // only the first-person guitar drives the hands
        g.localPosition = new Vector3(0.12f, -0.4f, 0.55f);
        g.localRotation = Quaternion.Euler(-10f, 10f, 38f);
        g.localScale = Vector3.one * 0.62f;
        return g;
    }

    protected override Transform BuildThirdPersonModel(BlockyCharacter body)
    {
        var g = Guitar(body.spine, false, out _, out _);
        g.localPosition = new Vector3(0.02f, 0.18f, 0.2f);
        g.localRotation = Quaternion.Euler(0, 0, 40f);
        g.localScale = Vector3.one * 0.75f;
        return g;
    }

    // Acoustic: two-bout body, sound hole, bridge, neck with frets, headstock, strings (built facing ±Z).
    static Transform Guitar(Transform parent, bool fp, out Transform neckGrip, out Transform strumPoint)
    {
        var root = new GameObject("Guitar").transform;
        root.SetParent(parent, false);
        var mats = BlockyCharacter.RuntimeMaterials();
        var wood = mats("GuitarWood", new Color(0.78f, 0.52f, 0.28f));
        var edge = mats("GuitarEdge", new Color(0.42f, 0.24f, 0.12f));
        var dark = mats("GuitarDark", new Color(0.08f, 0.06f, 0.05f));
        var neck = mats("GuitarNeck", new Color(0.35f, 0.2f, 0.1f));
        var metal = mats("GuitarStrings", new Color(0.85f, 0.85f, 0.8f));
        Vector3 face = new Vector3(90f, 0, 0);                           // cylinders turned so their round side faces us

        Part(PrimitiveType.Cylinder, root, Vector3.zero, new Vector3(0.4f, 0.05f, 0.4f), edge, fp, face);                       // lower bout rim
        Part(PrimitiveType.Cylinder, root, Vector3.zero, new Vector3(0.38f, 0.052f, 0.38f), wood, fp, face);
        Part(PrimitiveType.Cylinder, root, new Vector3(0, 0.24f, 0), new Vector3(0.3f, 0.05f, 0.3f), edge, fp, face);          // upper bout
        Part(PrimitiveType.Cylinder, root, new Vector3(0, 0.24f, 0), new Vector3(0.285f, 0.052f, 0.285f), wood, fp, face);
        foreach (float z in new[] { -0.054f, 0.054f })
        {
            Part(PrimitiveType.Cylinder, root, new Vector3(0, 0.13f, z), new Vector3(0.1f, 0.002f, 0.1f), dark, fp, face);    // sound hole (both sides)
            Part(PrimitiveType.Cube, root, new Vector3(0, -0.07f, z), new Vector3(0.12f, 0.02f, 0.01f), dark, fp);            // bridge
        }
        Part(PrimitiveType.Cube, root, new Vector3(0, 0.62f, 0), new Vector3(0.055f, 0.55f, 0.035f), neck, fp);                // neck
        for (int f = 0; f < 6; f++)
            Part(PrimitiveType.Cube, root, new Vector3(0, 0.42f + f * 0.08f, 0), new Vector3(0.057f, 0.004f, 0.037f), metal, fp);   // frets
        Part(PrimitiveType.Cube, root, new Vector3(0, 0.95f, 0), new Vector3(0.075f, 0.12f, 0.03f), dark, fp);                 // headstock
        foreach (float x in new[] { -0.015f, 0f, 0.015f })
            foreach (float z in new[] { -0.03f, 0.03f })
                Part(PrimitiveType.Cube, root, new Vector3(x, 0.44f, z), new Vector3(0.002f, 1.02f, 0.002f), metal, fp);       // strings

        neckGrip = new GameObject("NeckGrip").transform;
        neckGrip.SetParent(root, false);
        neckGrip.localPosition = new Vector3(0, 0.62f, -0.05f);
        strumPoint = new GameObject("StrumPoint").transform;
        strumPoint.SetParent(root, false);
        strumPoint.localPosition = new Vector3(0.05f, 0.08f, -0.08f);
        return root;
    }
}
