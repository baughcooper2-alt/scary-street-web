using UnityEngine;

// Deck of Cards (DESIGN.md): throw cards; 64 per deck, and the deck is gone when it's empty.
// The deck sits in your left hand; the right hand pinches a card off the top and whips it out.
// Hold left click to flick cards fast and straight (6 damage each); right click throws a fan of 5.
// Level ups: +30% damage; from Lv 3 each card cuts through one extra enemy.
public class DeckOfCardsWeapon : Weapon
{
    public override UIArt.Icon Icon => UIArt.Icon.Cards;
    public const int DeckSize = 64;
    public float cardDamage = 6f;
    public float cardSpeed = 24f;
    [Tooltip("One card: reach to the deck, pinch it between two fingers, cock back, whip it out.")]
    public float flickTime = 0.42f, flickCooldown = 0.46f;
    public float fanTime = 0.55f, fanCooldown = 0.8f;

    // first person (camera space): the left hand comes up from below with the deck cupped in it, the right beside it
    static readonly Vector3 LeftHold = new Vector3(-0.15f, -0.22f, 0.46f), LeftEuler = new Vector3(-12f, 28f, 0f);
    static readonly Vector3 RightRest = new Vector3(0.17f, -0.24f, 0.46f);
    static readonly Vector3 DeckGrip = new Vector3(0, -0.07f, 0.04f);          // in the left hand (body hand space)
    static readonly FirstPersonHand.Grip Cupped = new FirstPersonHand.Grip(0.5f, 0.5f, 0.55f, 0.6f, 0.45f), Relaxed = new FirstPersonHand.Grip(0.35f);

    int cards = DeckSize;
    float cooldown;
    bool wasSecondary, flicking, released, fanning;
    readonly HandMotion hand = new HandMotion { rest = RightRest, restEuler = new Vector3(0, -10f, 0) };
    static Material face, back;
    Transform fpCard, tpCard;

    protected override CharacterAnimator.Hold HoldPose => CharacterAnimator.Hold.CarryLeft;

    public override void Init(WeaponInventory inv) { base.Init(inv); displayName = "Deck of Cards"; }
    public override void Equip() { base.Equip(); SyncModels(); }
    public override void Unequip()
    {
        base.Unequip(); hand.Release(arms); if (arms) arms.overrideLeft = false;
        flicking = false; ShowCard(false); SyncModels();
    }

    public void NewDeck() { cards = DeckSize; }
    public override string LevelUpText => level == 2 ? "+30% damage, and cards cut through an extra enemy" : "+30% damage";
    public override string SlotStatus => $"{cards} cards";
    public override string Hint => $"Hold {Key("left click", GamepadInfo.RT)} to flick cards · {Key("right click", GamepadInfo.LT)} for a fan of 5 · the deck is gone when it's empty";

    public override void Tick(WeaponInput input)
    {
        float dt = Time.deltaTime;
        SyncModels();
        cooldown -= dt;
        bool fan = input.secondaryHeld && !wasSecondary;
        wasSecondary = input.secondaryHeld;

        if (arms) { arms.overrideLeft = true; arms.leftTarget = LeftHold; arms.leftEuler = LeftEuler; arms.leftFingers = Cupped; }
        if (fpModel) hand.pickFrom = cam.InverseTransformPoint(fpModel.position);

        if (cooldown <= 0 && cards > 0 && !flicking && (fan || input.primaryHeld))
        {
            fanning = fan; flicking = true; released = false;
            float time = fan ? fanTime : flickTime;
            cooldown = fan ? fanCooldown : flickCooldown;
            hand.Play(HandMotion.Move.Flick, time);
            if (BodyAnim) BodyAnim.Flick(time);
        }
        hand.Apply(arms, dt);

        // the card: between two fingers from the pinch until it's thrown
        float p = hand.Playing == HandMotion.Move.Flick ? hand.Progress : -1f;
        if (flicking && !released && (p < 0 || p >= HandMotion.FlickRelease))
        {
            released = true;
            if (fanning) for (int i = -2; i <= 2; i++) Throw(i * 9f); else Throw(Random.Range(-1.5f, 1.5f));
        }
        if (p < 0) flicking = false;
        bool pinching = flicking && !released && p >= HandMotion.FlickPinch;
        bool open = flicking && p >= HandMotion.FlickPinch * 0.6f && p < HandMotion.FlickRelease + 0.08f;
        if (arms) arms.rightFingers = open ? FirstPersonHand.Grip.Card : Relaxed;
        if (BodyAnim && open) BodyAnim.fingersR = FirstPersonHand.Grip.Card;
        ShowCard(pinching);

        if (cards <= 0 && !flicking)
        {
            inventory.Toast("Out of cards: the deck's gone", 2f);
            inventory.Remove(this);
        }
    }

    void ShowCard(bool on)
    {
        if (on && !fpCard && arms && arms.RightFingers && arms.RightFingers.Ready) fpCard = Card(null, true);
        if (on && !tpCard && inventory.GetComponentInChildren<BlockyCharacter>()) tpCard = Card(null, false);
        if (fpCard)
        {
            fpCard.gameObject.SetActive(on && fpModel && fpModel.gameObject.activeInHierarchy);
            if (on) { var f = arms.RightFingers; BetweenFingers(fpCard, f.Bone(0, 1), f.Bone(0, 2), f.Bone(1, 1)); }
        }
        if (tpCard)
        {
            var body = inventory.GetComponentInChildren<BlockyCharacter>();
            Transform i2 = Finger(body, "Index2"), i3 = Finger(body, "Index3"), m2 = Finger(body, "Mid2");
            tpCard.gameObject.SetActive(on && i2 && tpModel && tpModel.gameObject.activeInHierarchy);
            if (on && i2) BetweenFingers(tpCard, i2, i3, m2);
        }
    }

    readonly System.Collections.Generic.Dictionary<string, Transform> fingerCache = new System.Collections.Generic.Dictionary<string, Transform>();
    Transform Finger(BlockyCharacter body, string n)
    {
        if (!body) return null;
        if (fingerCache.TryGetValue(n, out var t) && t) return t;
        foreach (var c in body.GetComponentsInChildren<Transform>(true)) if (c.name == "CC_Base_R_" + n) { fingerCache[n] = c; return c; }
        return null;
    }

    // a card held between the index and middle fingers: its long side along the fingers
    static void BetweenFingers(Transform card, Transform i2, Transform i3, Transform m2)
    {
        if (!i2 || !i3 || !m2) return;
        if (card.parent != i2) card.SetParent(i2, false);
        Vector3 along = i3.position - i2.position, across = m2.position - i2.position;
        card.position = i2.position + across * 0.5f + along.normalized * 0.03f;
        card.rotation = Quaternion.LookRotation(along, across);
    }

    void Throw(float yaw)
    {
        if (cards <= 0) return;
        cards--;
        Vector3 dir = Quaternion.AngleAxis(yaw, Vector3.up) * cam.forward;
        var p = Projectile.Spawn("Card", Eye + cam.forward * 0.5f + Vector3.down * 0.12f, dir * cardSpeed, inventory.gameObject);
        p.damage = PlayerStats.RangedDamage(cardDamage * LevelDamage, inventory.gameObject);
        p.radius = 0.2f; p.life = 1.1f; p.pierce = level >= 3 ? 1 : 0;
        p.spin = 1400f; p.spinAxis = Vector3.up; p.hitSound = Sfx.Card;
        Look();
        p.Piece(PrimitiveType.Cube, Vector3.zero, new Vector3(0.063f, 0.003f, 0.088f), face);
        p.Piece(PrimitiveType.Cube, new Vector3(0, -0.0021f, 0), new Vector3(0.06f, 0.0012f, 0.085f), back);
        SoundKit.Play(Sfx.Card, 0.45f, 0.15f);
    }

    static void Look()
    {
        var mats = BlockyCharacter.RuntimeMaterials();
        if (!face) face = mats("CardFace", new Color(0.96f, 0.95f, 0.92f));
        if (!back) back = mats("CardBack", new Color(0.72f, 0.1f, 0.12f));
    }

    // one card (red back, white face), thin along its local Y
    Transform Card(Transform parent, bool fp)
    {
        Look();
        var root = new GameObject("HeldCard").transform;
        if (parent) root.SetParent(parent, false);
        Part(PrimitiveType.Cube, root, Vector3.zero, new Vector3(0.063f, 0.0016f, 0.088f), face, true);
        Part(PrimitiveType.Cube, root, new Vector3(0, 0.0012f, 0), new Vector3(0.059f, 0.0008f, 0.084f), back, true);
        PlayerLayers.Set(root.gameObject, fp ? PlayerLayers.Arms(PlayerLayers.IndexOf(inventory)) : PlayerLayers.Body(PlayerLayers.IndexOf(inventory)));
        return root;
    }

    // a deck: red back on top, white edges
    Transform Deck(Transform parent, bool fp)
    {
        Look();
        var root = new GameObject("Deck").transform;
        root.SetParent(parent, false);
        Rounded(root, Vector3.zero, new Vector3(0.065f, 0.022f, 0.09f), 0.003f, face, fp);
        Rounded(root, new Vector3(0, 0.0112f, 0), new Vector3(0.061f, 0.0012f, 0.086f), 0.0005f, back, fp);
        return root;
    }

    protected override Transform BuildFirstPersonModel()
    {
        if (!arms.LeftFist) return null;
        var d = Deck(arms.LeftFist, true);
        arms.HoldLikeHand(d, true, DeckGrip, Quaternion.identity);         // cupped in the left hand, as the body holds it
        return d;
    }

    protected override Transform BuildThirdPersonModel(BlockyCharacter body)
    {
        if (!body.handL) return null;
        var d = Deck(body.handL, false);
        d.localPosition = DeckGrip;
        return d;
    }

    void OnDestroy() { if (fpCard) Destroy(fpCard.gameObject); if (tpCard) Destroy(tpCard.gameObject); }
}
