using System;
using System.Collections.Generic;
using UnityEngine;

// DESIGN.md's upgrades. Each one levels up without limit, but you can hold only 5 different ones at a time
// (the To-go box adds 5 more slots each). Put this on the Player; the shop (and later the level-up picks) call Add().
public class PlayerUpgrades : MonoBehaviour
{
    public enum Id { EnergyDrink, Shooter, Pee, McDonaldsBag, CanesChicken, ToGoBox, Backpack, Skateboard }

    public class Def
    {
        public readonly Id id; public readonly string name, desc; public readonly int basePrice;
        public Def(Id id, string name, string desc, int basePrice) { this.id = id; this.name = name; this.desc = desc; this.basePrice = basePrice; }
    }

    public static readonly Def[] All =
    {
        new Def(Id.EnergyDrink,  "Energy drink",   "+15% move speed",                        25),
        new Def(Id.Shooter,      "Shooter",        "+20% damage, but you get tipsy",         30),
        new Def(Id.Pee,          "Pee",            "Hits knock enemies back further (stinky breath)", 30),
        new Def(Id.McDonaldsBag, "McDonald's bag", "Heal 0.4 HP/s when you haven't been hit for 2 s", 35),
        new Def(Id.CanesChicken, "Cane's chicken", "+10 max health",                         35),
        new Def(Id.ToGoBox,      "To-go box",      "+5 upgrade slots",                       60),
        new Def(Id.Backpack,     "Backpack",       "+1 weapon slot",                         70),
        new Def(Id.Skateboard,   "Skateboard",     "2× speed, 1.5× jump height",             80),
    };

    public static Def Get(Id id) => Array.Find(All, d => d.id == id);
    public static PlayerUpgrades Instance { get; private set; }

    [Min(1)] public int baseSlots = 5;

    public int Slots => baseSlots + 5 * Level(Id.ToGoBox);
    public int Used => levels.Count;
    public IReadOnlyDictionary<Id, int> Owned => levels;

    public float DamageMultiplier => 1f + 0.2f * Level(Id.Shooter);
    public float KnockbackBonus => 0.4f * Level(Id.Pee);             // extra meters of shove per hit

    public event Action Changed;

    readonly Dictionary<Id, int> levels = new Dictionary<Id, int>();
    FirstPersonController fpc;
    Health health;
    Transform cam;
    float baseWalk, baseCrouch, baseJump, lastHit = -99f;

    void Awake()
    {
        Instance = this;
        fpc = GetComponent<FirstPersonController>();
        health = GetComponent<Health>();
        if (fpc) { baseWalk = fpc.walkSpeed; baseCrouch = fpc.crouchSpeed; baseJump = fpc.jumpHeight; }
        if (health) health.Damaged += _ => lastHit = Time.time;
    }

    void Start()
    {
        var c = GetComponentInChildren<Camera>();
        cam = c ? c.transform : null;
    }

    public int Level(Id id) => levels.TryGetValue(id, out int l) ? l : 0;

    // Owning it already, or there's a free slot for a new kind.
    public bool CanAdd(Id id) => levels.ContainsKey(id) || Used < Slots;

    public bool Add(Id id)
    {
        if (!CanAdd(id)) return false;
        levels[id] = Level(id) + 1;
        if (id == Id.CanesChicken && health) health.SetMaxHealth(health.maxHealth + 10f, healDifference: true);
        if (id == Id.Backpack) { var inv = GetComponent<WeaponInventory>(); if (inv) inv.AddSlot(); }
        ApplyMovement();
        Changed?.Invoke();
        return true;
    }

    void ApplyMovement()
    {
        if (!fpc) return;
        int skate = Level(Id.Skateboard);
        float speed = (1f + 0.15f * Level(Id.EnergyDrink)) * (skate > 0 ? 2f + 0.1f * (skate - 1) : 1f);
        fpc.walkSpeed = baseWalk * speed;
        fpc.crouchSpeed = baseCrouch * speed;
        fpc.jumpHeight = baseJump * (skate > 0 ? 1.5f : 1f);
    }

    void Update()
    {
        int bag = Level(Id.McDonaldsBag);
        if (bag > 0 && health && !health.IsDead && Time.time - lastHit > 2f) health.Heal(0.4f * bag * Time.deltaTime);
    }

    // Shooter: the camera sways a little more with every level.
    void LateUpdate()
    {
        int s = Level(Id.Shooter);
        if (s == 0 || !cam || (health && health.IsDead) || (fpc && !fpc.enabled)) return;   // FPC resets the camera each frame; without it this would add up
        float a = Mathf.Min(4f, 1.2f * s), t = Time.time;
        cam.rotation *= Quaternion.Euler(Mathf.Sin(t * 1.3f) * a * 0.5f, Mathf.Sin(t * 0.7f) * a * 0.4f, Mathf.Sin(t * 0.9f) * a);
    }

    // For the HUD: "Energy drink 2 · Cane's chicken 1"
    public string Summary()
    {
        var parts = new List<string>();
        foreach (var kv in levels) parts.Add($"{Get(kv.Key).name} {kv.Value}");
        return parts.Count == 0 ? "" : string.Join(" · ", parts) + $"   ({Used}/{Slots} slots)";
    }
}
