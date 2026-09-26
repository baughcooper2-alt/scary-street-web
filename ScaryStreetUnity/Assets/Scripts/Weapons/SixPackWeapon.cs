using System.Collections.Generic;
using UnityEngine;

// 6-pack of beer (DESIGN.md, John's weapon): drink, then throw the bottles or hit with them until they break;
// refill at the fridge; drinking boosts strength but makes you dizzy (Will never gets dizzy).
// You carry the carrier in your left hand (it shows what's left) and a bottle in your right:
//   left click: bash with the bottle (15 damage); it breaks on the 3rd hit
//   right click, tap: throw it (20 damage, shatters with a small splash), then grab the next from the carrier
//   right click, hold: drink it (+20% melee damage for 25 s, stacks to +60%; the screen sways)
// Empty? Open the fridge (F) for a fresh pack; with no fridge in the scene it refills at the end of the round.
public class SixPackWeapon : Weapon
{
    public override UIArt.Icon Icon => UIArt.Icon.Bottle;
    public const int PackSize = 6;
    public float bashDamage = 15f, bashReach = 1.8f, bashCooldown = 0.55f;
    public float throwDamage = 20f, throwSpeed = 14f;
    public float drinkTime = 0.9f, drinkBoost = 0.2f, boostSeconds = 25f, dizzyPerDrink = 0.35f;

    // first person, like the body: the carrier hangs from the left fist at the left edge (forearm up out of view);
    // the bottle stands up in the right fist. The same hand-space grips as third person:
    static readonly Vector3 LeftHold = new Vector3(-0.4f, -0.1f, 0.5f), LeftEuler = new Vector3(95f, 40f, 40f);
    static readonly Vector3 BottleGrip = new Vector3(-0.02f, -0.08f, -0.06f), CarrierGrip = new Vector3(0, -0.02f, 0.02f);
    static readonly Quaternion BottleTurn = Quaternion.Euler(90f, 0, 0), CarrierTurn = Quaternion.Euler(0, 90f, 0);

    int bottles = PackSize, bashes;
    float cooldown, secondaryT = -1f, grabT = -1f, gulpT = -1f;
    readonly HandMotion hand = new HandMotion();
    static Material glass, label, cap, card, cardDark;
    Transform fpBottle, tpBottle, fpCarrier, fpTip, tpTip;
    readonly List<GameObject> fpSlots = new List<GameObject>(), tpSlots = new List<GameObject>();
    int shown = -1;

    public int Bottles => bottles;
    protected override CharacterAnimator.Hold HoldPose => CharacterAnimator.Hold.HangLeft;

    public override void Init(WeaponInventory inv)
    {
        base.Init(inv);
        displayName = "6-Pack";
        if (RoundManager.Instance) RoundManager.Instance.RoundEnded += OnRoundEnded;
    }
    void OnDestroy() { if (RoundManager.Instance) RoundManager.Instance.RoundEnded -= OnRoundEnded; }
    void OnRoundEnded(int round) { if (!Fridge.Any && bottles < PackSize) { Refill(); inventory.Toast("Fresh 6-pack for the next round", 2f); } }

    public void Refill() { bottles = PackSize; bashes = 0; shown = -1; }

    public override void Equip() { base.Equip(); SyncModels(); }
    public override void Unequip()
    {
        base.Unequip(); hand.Release(arms);
        if (arms) { arms.overrideLeft = false; arms.raise = 0; arms.mouthTip = null; }
        secondaryT = -1f; gulpT = -1f; SyncModels();
    }
    public override string LevelUpText => "+30% damage";
    public override string SlotStatus => bottles > 0 ? $"{bottles} / {PackSize}" : "Empty";
    public override string Hint => bottles <= 0
        ? (Fridge.Any ? "Empty: open the fridge (F) for a fresh 6-pack" : "Empty: you'll get a fresh 6-pack after the round")
        : $"{Key("left click", GamepadInfo.RT)} to bash · tap {Key("right click", GamepadInfo.LT)} to throw · hold it to drink (stronger, but dizzy)";

    bool Immune => arms && arms.look && arms.look.displayName == "Will";         // alcohol never makes him dizzy

    public override void Tick(WeaponInput input)
    {
        float dt = Time.deltaTime;
        SyncModels();
        cooldown -= dt;
        if (arms) { arms.overrideLeft = true; arms.leftTarget = LeftHold; arms.leftEuler = LeftEuler; }
        if (fpCarrier) hand.pickFrom = cam.InverseTransformPoint(fpCarrier.TransformPoint(new Vector3(0, -0.14f, 0)));   // the bottle necks

        // after a throw the right hand goes back to the carrier for the next one
        if (grabT >= 0 && (grabT -= dt) < 0 && bottles > 0) hand.Play(HandMotion.Move.Grab, 0.35f);

        // right click: tap to throw, hold to drink (the bottle comes up to your mouth while you hold)
        gulpT -= dt;
        if (input.secondaryHeld && bottles > 0 && cooldown <= 0)
        {
            if (secondaryT < 0) secondaryT = 0;
            secondaryT += dt;
            if (secondaryT >= drinkTime) { Drink(); secondaryT = -1f; }
        }
        else if (secondaryT >= 0)
        {
            if (secondaryT < 0.25f && bottles > 0) ThrowBottle();
            secondaryT = -1f;
        }
        else if (input.primaryPressed && bottles > 0 && cooldown <= 0) Bash();
        hand.Apply(arms, dt);
        bool drinking = secondaryT >= 0.2f || gulpT > 0;
        if (arms) { arms.mouthTip = fpTip; arms.raise = Mathf.MoveTowards(arms.raise, drinking ? 1f : 0f, dt * 5f); }
        if (BodyAnim) { BodyAnim.mouthItemTip = tpTip; BodyAnim.inhaling = drinking; }
        ShowBottles();
    }

    // the bottle in your hand (hidden right after a throw until you grab the next) and the ones left in the carrier
    void ShowBottles()
    {
        bool inHand = gulpT > 0 || bottles > 0 && grabT < 0;              // (the one you're finishing stays till it's empty)
        if (fpBottle) fpBottle.gameObject.SetActive(inHand);
        if (tpBottle) tpBottle.gameObject.SetActive(inHand);
        int left = Mathf.Max(0, bottles - 1);
        if (left == shown) return;
        shown = left;
        for (int i = 0; i < fpSlots.Count; i++) if (fpSlots[i]) fpSlots[i].SetActive(i < left);
        for (int i = 0; i < tpSlots.Count; i++) if (tpSlots[i]) tpSlots[i].SetActive(i < left);
    }

    void Bash()
    {
        cooldown = bashCooldown;
        hand.Play(HandMotion.Move.Swing, 0.32f);
        if (BodyAnim) BodyAnim.Sweep(0.32f, 0.4f);
        SoundKit.Play(Sfx.Whoosh, 0.5f, 0.15f);
        Vector3 fwd = cam.forward; fwd.y = 0; fwd.Normalize();
        Health best = null; float bestD = float.MaxValue;
        foreach (var h in EnemyTargets.Around(inventory.transform.position + Vector3.up * 0.9f, bashReach))
        {
            Vector3 to = h.transform.position - inventory.transform.position; to.y = 0;
            if (to.sqrMagnitude > 0.01f && Vector3.Dot(to.normalized, fwd) < 0.6f) continue;
            if (to.sqrMagnitude < bestD) { bestD = to.sqrMagnitude; best = h; }
        }
        if (!best) return;
        best.TakeDamage(PlayerStats.MeleeDamage(bashDamage * LevelDamage, inventory.gameObject), 0.4f + PlayerUpgrades.KnockbackFor(inventory.gameObject));
        SoundKit.PlayAt(Sfx.Hit, best.transform.position + Vector3.up, 0.9f);
        Rumble(0.3f, 0.5f, 0.1f);
        if (++bashes >= 3) { bashes = 0; bottles--; grabT = 0.2f; SoundKit.PlayAt(Sfx.Glass, best.transform.position + Vector3.up, 0.9f); inventory.Toast("The bottle broke", 1f); }
    }

    void ThrowBottle()
    {
        bottles--; bashes = 0; cooldown = 0.6f; grabT = 0.28f;
        hand.Play(HandMotion.Move.Throw, 0.28f);
        if (BodyAnim) BodyAnim.Throw(0.4f, 0.55f);
        SoundKit.Play(Sfx.Throw, 0.6f);
        var p = Projectile.Spawn("Bottle", Eye + cam.forward * 0.5f, (cam.forward + Vector3.up * 0.12f).normalized * throwSpeed, inventory.gameObject);
        p.damage = PlayerStats.RangedDamage(throwDamage * LevelDamage, inventory.gameObject);
        p.gravity = 9f; p.life = 2.5f; p.radius = 0.22f; p.burstOnWall = true;
        p.blastRadius = 1.2f; p.blastDamage = p.damage * 0.4f; p.hitSound = Sfx.Glass; p.burstSound = Sfx.Glass;
        p.spin = 720f; p.spinAxis = Vector3.right;
        Bottle(p.transform, true);
    }

    void Drink()
    {
        bottles--; bashes = 0; cooldown = 0.3f;
        gulpT = 0.45f; grabT = 0.75f;                                      // finish it at your mouth, then reach for the next
        SoundKit.Play(Sfx.Gulp, 0.7f, 0f);
        var stats = inventory.GetComponent<PlayerStats>();
        if (stats) stats.Drink(drinkBoost, boostSeconds);
        var fpc = inventory.GetComponent<FirstPersonController>();
        if (fpc && !Immune) fpc.dizzy = Mathf.Min(1f, fpc.dizzy + dizzyPerDrink);
        inventory.Toast(stats ? $"Cheers: +{stats.DrinkBoost * 100:0}% melee damage" + (Immune ? "" : ", and a little dizzy") : "Cheers", 1.6f);
    }

    static void Mats()
    {
        var mats = BlockyCharacter.RuntimeMaterials();
        if (!glass) { glass = mats("BeerGlass", new Color(0.42f, 0.24f, 0.08f)); glass.SetFloat("_Smoothness", 0.85f); }
        if (!label) label = mats("BeerLabel", new Color(0.92f, 0.86f, 0.62f));
        if (!cap) cap = mats("BeerCap", new Color(0.78f, 0.1f, 0.1f));
        if (!card) card = mats("SixPackCard", new Color(0.72f, 0.12f, 0.1f));
        if (!cardDark) cardDark = mats("SixPackCardDark", new Color(0.35f, 0.06f, 0.05f));
    }

    // brown bottle: body, label, shoulder, neck, red cap (origin at the middle of the body; a "Tip" at the cap)
    static Transform Bottle(Transform parent, bool fp)
    {
        Mats();
        var root = new GameObject("Bottle").transform;
        root.SetParent(parent, false);
        Part(PrimitiveType.Cylinder, root, Vector3.zero, new Vector3(0.055f, 0.07f, 0.055f), glass, fp);
        Part(PrimitiveType.Cylinder, root, new Vector3(0, 0.005f, 0), new Vector3(0.057f, 0.03f, 0.057f), label, fp);
        Part(PrimitiveType.Sphere, root, new Vector3(0, 0.075f, 0), new Vector3(0.055f, 0.04f, 0.055f), glass, fp);
        Part(PrimitiveType.Cylinder, root, new Vector3(0, 0.12f, 0), new Vector3(0.022f, 0.035f, 0.022f), glass, fp);
        Part(PrimitiveType.Cylinder, root, new Vector3(0, 0.158f, 0), new Vector3(0.025f, 0.006f, 0.025f), cap, fp);
        var tip = new GameObject("Tip").transform; tip.SetParent(root, false); tip.localPosition = new Vector3(0, 0.16f, 0);
        return root;
    }

    // cardboard carrier: open box in two rows of three, a tall centre panel with a hand hole; the handle is in your fist
    Transform Carrier(Transform parent, bool fp, List<GameObject> slots)
    {
        Mats();
        var root = new GameObject("SixPack").transform;
        root.SetParent(parent, false);
        Part(PrimitiveType.Cube, root, new Vector3(0, -0.2f, 0), new Vector3(0.19f, 0.09f, 0.13f), card, fp);       // the box
        Part(PrimitiveType.Cube, root, new Vector3(0, -0.1f, 0), new Vector3(0.19f, 0.2f, 0.006f), card, fp);        // centre panel up to the handle
        Part(PrimitiveType.Cube, root, new Vector3(0, -0.02f, 0), new Vector3(0.07f, 0.025f, 0.008f), cardDark, fp); // hand hole
        slots.Clear();
        for (int i = 0; i < 5; i++)                                                                                    // bottles left (one's in your other hand)
        {
            int row = i % 2, col = i / 2;
            var b = Bottle(root, fp);
            b.localPosition = new Vector3(-0.058f + col * 0.058f, -0.24f, row == 0 ? -0.035f : 0.035f);
            slots.Add(b.gameObject);
        }
        shown = -1;
        return root;
    }

    protected override Transform BuildFirstPersonModel()
    {
        if (!arms.LeftFist) return null;
        var root = new GameObject("SixPackFP").transform;
        arms.HoldLikeHand(root, false, BottleGrip, BottleTurn);           // through the fist, neck up, as the body holds it
        fpBottle = Bottle(root, true); fpTip = fpBottle.Find("Tip");
        fpCarrier = Carrier(arms.LeftFist, true, fpSlots);
        arms.HoldLikeHand(fpCarrier, true, CarrierGrip, CarrierTurn);     // hangs from the left hand (shown with the weapon in LateUpdate)
        return root;
    }

    protected override Transform BuildThirdPersonModel(BlockyCharacter body)
    {
        var root = new GameObject("SixPackTP").transform;
        root.SetParent(body.handR, false);
        tpBottle = Bottle(root, false);                                    // through the fist, like you'd really hold it
        tpBottle.localPosition = BottleGrip; tpBottle.localRotation = BottleTurn; tpTip = tpBottle.Find("Tip");
        if (body.handL)
        {
            var carrier = Carrier(body.handL, false, tpSlots);
            carrier.localPosition = CarrierGrip; carrier.localRotation = CarrierTurn;
            tpCarrierObj = carrier.gameObject;
        }
        return root;
    }

    GameObject tpCarrierObj;

    void LateUpdate()
    {
        // the carriers live under the other hand, so show / hide them with the weapon
        if (fpCarrier) fpCarrier.gameObject.SetActive(fpModel && fpModel.gameObject.activeSelf && bottles > 0);
        if (tpCarrierObj) tpCarrierObj.SetActive(tpModel && tpModel.gameObject.activeSelf && bottles > 0);
    }
}
