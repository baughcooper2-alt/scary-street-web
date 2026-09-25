using UnityEngine;

// Deck of Cards (DESIGN.md): throw cards; 64 per deck, and the deck is gone when it's empty.
// Hold left click to flick cards fast and straight (6 damage each); right click throws a fan of 5.
// Level ups: +30% damage; from Lv 3 each card cuts through one extra enemy.
public class DeckOfCardsWeapon : Weapon
{
    public override UIArt.Icon Icon => UIArt.Icon.Cards;
    public const int DeckSize = 64;
    public float cardDamage = 6f;
    public float cardSpeed = 24f;
    public float flickCooldown = 0.14f;
    public float fanCooldown = 0.7f;

    int cards = DeckSize;
    float cooldown;
    bool wasSecondary;
    readonly HandMotion hand = new HandMotion();
    static Material face, back;

    protected override CharacterAnimator.Hold HoldPose => CharacterAnimator.Hold.Book;

    public override void Init(WeaponInventory inv) { base.Init(inv); displayName = "Deck of Cards"; }
    public override void Equip() { base.Equip(); SyncModels(); }
    public override void Unequip() { base.Unequip(); hand.Release(arms); SyncModels(); }

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

        if (cooldown <= 0 && cards > 0)
        {
            if (fan) { for (int i = -2; i <= 2; i++) Throw(i * 9f); cooldown = fanCooldown; hand.Play(HandMotion.Move.Throw, 0.3f); if (BodyAnim) BodyAnim.Throw(0.35f); }
            else if (input.primaryHeld) { Throw(Random.Range(-1.5f, 1.5f)); cooldown = flickCooldown; hand.Play(HandMotion.Move.Throw, 0.16f); }
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

    // a deck in your hand: red back, white edges
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
        var d = Deck(arms.RightHand, true);
        d.localPosition = new Vector3(0, 0.02f, 0.03f); d.localRotation = Quaternion.Euler(-60f, 0, 10f);
        return d;
    }

    protected override Transform BuildThirdPersonModel(BlockyCharacter body)
    {
        var d = Deck(body.handR, false);
        d.localPosition = new Vector3(0, -0.08f, 0.03f); d.localRotation = Quaternion.Euler(0, 0, 90f);
        return d;
    }
}
