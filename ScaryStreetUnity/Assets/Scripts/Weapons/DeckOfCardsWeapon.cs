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
    public float flickCooldown = 0.16f;
    public float fanCooldown = 0.7f;

    // first person: where the hands sit (camera space)
    static readonly Vector3 LeftHold = new Vector3(-0.1f, -0.2f, 0.42f), LeftEuler = new Vector3(-25f, 25f, 15f);
    static readonly Vector3 RightRest = new Vector3(0.17f, -0.23f, 0.44f);

    int cards = DeckSize;
    float cooldown;
    bool wasSecondary;
    readonly HandMotion hand = new HandMotion { rest = RightRest, restEuler = new Vector3(0, -10f, 0) };
    static Material face, back;

    protected override CharacterAnimator.Hold HoldPose => CharacterAnimator.Hold.CarryLeft;

    public override void Init(WeaponInventory inv) { base.Init(inv); displayName = "Deck of Cards"; }
    public override void Equip() { base.Equip(); SyncModels(); }
    public override void Unequip() { base.Unequip(); hand.Release(arms); if (arms) arms.overrideLeft = false; SyncModels(); }

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

        if (arms) { arms.overrideLeft = true; arms.leftTarget = LeftHold; arms.leftEuler = LeftEuler; }
        if (fpModel) hand.pickFrom = cam.InverseTransformPoint(fpModel.position);

        if (cooldown <= 0 && cards > 0)
        {
            if (fan) { for (int i = -2; i <= 2; i++) Throw(i * 9f); cooldown = fanCooldown; hand.Play(HandMotion.Move.Flick, 0.32f); if (BodyAnim) BodyAnim.Throw(0.35f); }
            else if (input.primaryHeld) { Throw(Random.Range(-1.5f, 1.5f)); cooldown = flickCooldown; hand.Play(HandMotion.Move.Flick, 0.16f); if (BodyAnim) BodyAnim.Throw(0.2f, 0.5f); }
        }
        hand.Apply(arms, dt);

        if (cards <= 0)
        {
            inventory.Toast("Out of cards: the deck's gone", 2f);
            inventory.Remove(this);
        }
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

    // a deck: red back on top, white edges
    Transform Deck(Transform parent, bool fp)
    {
        Look();
        var root = new GameObject("Deck").transform;
        root.SetParent(parent, false);
        Part(PrimitiveType.Cube, root, Vector3.zero, new Vector3(0.065f, 0.022f, 0.09f), face, fp);
        Part(PrimitiveType.Cube, root, new Vector3(0, 0.0115f, 0), new Vector3(0.061f, 0.001f, 0.086f), back, fp);
        return root;
    }

    protected override Transform BuildFirstPersonModel()
    {
        if (!arms.LeftHand) return null;
        var d = Deck(arms.LeftHand, true);
        d.localPosition = new Vector3(0.01f, 0.03f, 0.02f); d.localRotation = Quaternion.Euler(-15f, 0, -10f);   // on the fingers, face up
        return d;
    }

    protected override Transform BuildThirdPersonModel(BlockyCharacter body)
    {
        if (!body.handL) return null;
        var d = Deck(body.handL, false);
        d.localPosition = new Vector3(0, -0.07f, 0.04f);
        return d;
    }
}
