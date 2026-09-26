using System.Collections.Generic;
using UnityEngine;

// Skateboard (DESIGN.md: Isaiah's): a board you ride. Having it out means you're on it (switch slots to step off):
// 2× speed and 1.5× jump height, and on wheels your speed builds up and coasts. Every jump is a trick (kickflip,
// heelflip or a 360 shove-it) and landing one stomps whoever's close (14 damage in 1.8 m). Right click pushes off hard:
// a burst of speed that bowls over the enemies you run into (10 damage, a big shove). Left click still punches.
// The board: a popsicle deck (82.5 × 21 cm, nose and tail kicked up, grip tape on top, a graphic underneath) on two
// trucks 36 cm apart with 53 mm wheels, standard street proportions. It rides under the player (not the camera), so
// you see it when you look down in first person.
public class SkateboardWeapon : Weapon
{
    public override UIArt.Icon Icon => UIArt.Icon.Board;
    public float speedMultiplier = 2f, jumpMultiplier = 1.5f;
    [Header("Push (right click)")]
    public float pushSpeed = 7f, pushCooldown = 1.1f, pushDamage = 10f, pushShove = 1.4f, pushReach = 1.3f;
    [Header("Tricks (every jump)")]
    public float landDamage = 14f, landRadius = 1.8f, landShove = 1f;
    public Color deckColor = CharacterLook.Hex("#e0622a");

    enum Trick { Kickflip, Heelflip, ShoveIt }
    static readonly string[] TrickNames = { "KICKFLIP!", "HEELFLIP!", "360 SHOVE-IT!" };

    Transform board, deck;                          // board: at the player's feet, wheels on the ground; deck: the part that flips
    FirstPersonController fpc;
    bool wasGrounded = true, tricking, wasSecondary;
    float airT, pushT = -1f, pushCd;
    Trick trick;
    readonly HashSet<Health> pushHit = new HashSet<Health>();

    protected override CharacterAnimator.Hold HoldPose => CharacterAnimator.Hold.Skate;

    public override void Init(WeaponInventory inv) { base.Init(inv); displayName = "Skateboard"; fpc = inv.GetComponent<FirstPersonController>(); }
    public override void Equip() { base.Equip(); Ride(true); SoundKit.Play(Sfx.Clack, 0.45f); }
    public override void Unequip() { base.Unequip(); Ride(false); }
    public override string LevelUpText => "+30% trick and push damage";
    public override string SlotStatus => pushCd > 0 ? "" : "Push ready";
    public override string Hint => $"Riding: 2× speed, higher jumps · {Key("Space", GamepadInfo.A)}: every jump's a trick, and landing it hits whoever's close · {Key("right click", GamepadInfo.LT)} to push off and bowl them over · {Key("left click", GamepadInfo.RT)} still punches";

    void Ride(bool on)
    {
        if (!fpc) fpc = inventory.GetComponent<FirstPersonController>();
        if (fpc) { fpc.speedMultiplier = on ? speedMultiplier : 1f; fpc.jumpMultiplier = on ? jumpMultiplier : 1f; fpc.glide = on; }
        if (on && !board) Build();
        if (board) board.gameObject.SetActive(on);
        tricking = false; pushT = -1f;
        if (deck) { deck.localRotation = Quaternion.identity; deck.localPosition = DeckAt; }
        if (BodyAnim) BodyAnim.skatePush = -1f;
    }

    public override void Tick(WeaponInput input)
    {
        float dt = Time.deltaTime;
        SyncModels();
        if (!board) Build();
        if (!board.gameObject.activeSelf) Ride(true);
        var fists = inventory.GetComponent<PlayerPunch>(); if (fists) fists.allowInput = true;    // hands are free on a board
        pushCd -= dt;

        bool push = input.secondaryHeld && !wasSecondary;
        wasSecondary = input.secondaryHeld;
        if (push && pushCd <= 0 && fpc && fpc.IsGrounded) Push();
        if (pushT >= 0)
        {
            float p = (pushT += dt) / 0.45f;
            if (BodyAnim) BodyAnim.skatePush = Mathf.Clamp01(p);
            if (p < 0.8f) BowlOver();
            if (p >= 1f) { pushT = -1f; if (BodyAnim) BodyAnim.skatePush = -1f; }
        }

        // tricks: off the ground going up = a jump
        bool grounded = fpc && fpc.IsGrounded;
        if (!grounded && wasGrounded && fpc && fpc.Velocity.y > 1f) StartTrick();
        if (tricking)
        {
            airT += dt;
            AnimateTrick();
            if (grounded && airT > 0.12f) Land();
        }
        wasGrounded = grounded;
    }

    // ---------- push ----------

    void Push()
    {
        pushT = 0; pushCd = pushCooldown; pushHit.Clear();
        if (fpc) fpc.shove += inventory.transform.forward * pushSpeed;
        SoundKit.Play(Sfx.Whoosh, 0.55f, 0.2f);
        Rumble(0.2f, 0.3f, 0.1f);
    }

    // anyone in front of you while you're flying along gets knocked over (once per push)
    void BowlOver()
    {
        Vector3 at = inventory.transform.position + inventory.transform.forward * 0.6f + Vector3.up * 0.9f;
        foreach (var h in EnemyTargets.Around(at, pushReach))
        {
            if (!pushHit.Add(h)) continue;
            h.TakeDamage(PlayerStats.MeleeDamage(pushDamage * LevelDamage, inventory.gameObject), pushShove + PlayerUpgrades.KnockbackFor(inventory.gameObject));
            SoundKit.PlayAt(Sfx.Punch, h.transform.position + Vector3.up, 0.9f);
            Rumble(0.4f, 0.6f, 0.12f);
        }
    }

    // ---------- tricks ----------

    void StartTrick()
    {
        tricking = true; airT = 0;
        trick = (Trick)Random.Range(0, 3);
        SoundKit.Play(Sfx.Whoosh, 0.35f, 0.3f);                        // the pop
    }

    void AnimateTrick()
    {
        if (!deck) return;
        float spin = Mathf.SmoothStep(0, 1, Mathf.Clamp01((airT - 0.06f) / 0.45f));
        float pop = Mathf.Sin(Mathf.Clamp01(airT / 0.22f) * Mathf.PI);            // ollie: tail snaps down, nose up
        float lift = Mathf.Sin(Mathf.Clamp01(airT / 0.7f) * Mathf.PI);            // the board comes up with your feet
        deck.localPosition = DeckAt + Vector3.up * 0.2f * lift;
        var flip = trick == Trick.Kickflip ? Quaternion.Euler(0, 0, 360f * spin)
                 : trick == Trick.Heelflip ? Quaternion.Euler(0, 0, -360f * spin)
                 : Quaternion.Euler(0, 360f * spin, 0);
        deck.localRotation = Quaternion.Euler(-18f * pop, 0, 0) * flip;
    }

    void Land()
    {
        tricking = false;
        if (deck) { deck.localRotation = Quaternion.identity; deck.localPosition = DeckAt; }
        SoundKit.Play(Sfx.Clack, 0.8f);
        if (airT < 0.3f) return;                                                  // a hop, not a trick
        inventory.Toast(TrickNames[(int)trick], 0.8f);
        Rumble(0.5f, 0.5f, 0.15f);
        int hit = 0;
        foreach (var h in EnemyTargets.Around(inventory.transform.position + Vector3.up * 0.5f, landRadius))
        {
            h.TakeDamage(PlayerStats.MeleeDamage(landDamage * LevelDamage, inventory.gameObject), landShove + PlayerUpgrades.KnockbackFor(inventory.gameObject));
            hit++;
        }
        if (hit > 0) SoundKit.Play(Sfx.Slam, 0.6f);
    }

    // ---------- the board ----------

    static readonly Vector3 DeckAt = new Vector3(0, 0.072f, 0);                  // deck centre above the ground

    void Build()
    {
        var mats = BlockyCharacter.RuntimeMaterials();
        var grip = mats("SkateGrip", new Color(0.09f, 0.09f, 0.1f)); grip.SetFloat("_Smoothness", 0.05f);
        var wood = mats("SkateDeck", deckColor); wood.SetFloat("_Smoothness", 0.35f);
        var graphic = mats("SkateGraphic", new Color(0.97f, 0.93f, 0.82f));
        var metal = mats("SkateTruck", new Color(0.72f, 0.74f, 0.77f)); metal.SetFloat("_Smoothness", 0.75f);
        var wheel = mats("SkateWheel", new Color(0.95f, 0.91f, 0.8f)); wheel.SetFloat("_Smoothness", 0.4f);
        var bearing = mats("SkateBearing", new Color(0.15f, 0.15f, 0.17f));

        board = new GameObject("Skateboard").transform;
        board.SetParent(inventory.transform, false);                          // under the player (its forward = the board's nose)
        deck = new GameObject("Deck").transform;
        deck.SetParent(board, false); deck.localPosition = DeckAt;
        var top = MeshPart(MeshKit.SkateDeck(), deck, Vector3.zero, null, false, default, "DeckMesh");
        top.GetComponent<MeshRenderer>().sharedMaterials = new[] { grip, wood };
        // a graphic underneath: a pale stripe with a darker band across it
        Rounded(deck, new Vector3(0, -0.0072f, 0), new Vector3(0.11f, 0.0015f, 0.34f), 0.0006f, graphic, false);
        Rounded(deck, new Vector3(0, -0.0078f, 0.05f), new Vector3(0.15f, 0.0015f, 0.05f), 0.0006f, mats("SkateGraphic2", new Color(0.18f, 0.2f, 0.45f)), false);
        foreach (float z in new[] { -0.181f, 0.181f })
        {
            Rounded(deck, new Vector3(0, -0.0105f, z), new Vector3(0.056f, 0.007f, 0.075f), 0.002f, metal, false);   // baseplate
            Rounded(deck, new Vector3(0, -0.026f, z - Mathf.Sign(z) * 0.006f), new Vector3(0.045f, 0.026f, 0.034f), 0.008f, metal, false);   // truck body
            Part(PrimitiveType.Cylinder, deck, new Vector3(0, -0.0455f, z), new Vector3(0.022f, 0.068f, 0.022f), metal, false, new Vector3(0, 0, 90f));   // hanger
            Part(PrimitiveType.Cylinder, deck, new Vector3(0, -0.0455f, z), new Vector3(0.008f, 0.1f, 0.008f), metal, false, new Vector3(0, 0, 90f));     // axle
            foreach (float x in new[] { -0.088f, 0.088f })
            {
                Part(PrimitiveType.Cylinder, deck, new Vector3(x, -0.0455f, z), new Vector3(0.053f, 0.016f, 0.053f), wheel, false, new Vector3(0, 0, 90f));
                Part(PrimitiveType.Cylinder, deck, new Vector3(x + Mathf.Sign(x) * 0.001f, -0.0455f, z), new Vector3(0.022f, 0.0162f, 0.022f), bearing, false, new Vector3(0, 0, 90f));
            }
        }
        board.gameObject.SetActive(Equipped);
    }

    void OnDestroy()
    {
        if (fpc && Equipped) { fpc.speedMultiplier = 1f; fpc.jumpMultiplier = 1f; fpc.glide = false; }
        if (board) Destroy(board.gameObject);
    }
}
