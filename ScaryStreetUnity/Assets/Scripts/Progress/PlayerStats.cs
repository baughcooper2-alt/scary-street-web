using System;
using System.Collections.Generic;
using UnityEngine;

// DESIGN.md's seven player stats. Every stat starts at 0 (= base level) and goes up by one point per
// level-up pick. Weapons ask Melee() / Ranged() for their final damage (stats, the Shooter upgrade, crits).
// Opening the level-up picks screen when you level up also lives here. Put this on the Player.
public class PlayerStats : MonoBehaviour
{
    public enum Stat { Health, Strength, Precision, Speed, Luck, Defense, CritChance }

    public static PlayerStats Instance { get; private set; }

    // What one point does (shown on the pick cards too).
    public const float HealthPerPoint = 5f;          // max HP
    public const float StrengthPerPoint = 0.10f;     // melee damage
    public const float PrecisionPerPoint = 0.10f;    // ranged damage
    public const float SpeedPerPoint = 0.05f;        // move speed
    public const float LuckPerPoint = 0.05f;         // chance of an extra cash drop, +2% crit
    public const float DefensePerPoint = 0.05f;      // damage ignored (max 60%)
    public const float CritPerPoint = 0.05f;         // chance of double damage

    public static string Name(Stat s) => s == Stat.CritChance ? "Crit Chance" : s.ToString();

    public static string Describe(Stat s) => s switch
    {
        Stat.Health => $"+{HealthPerPoint:0} max health",
        Stat.Strength => $"+{StrengthPerPoint * 100:0}% melee damage",
        Stat.Precision => $"+{PrecisionPerPoint * 100:0}% ranged damage",
        Stat.Speed => $"+{SpeedPerPoint * 100:0}% move speed",
        Stat.Luck => $"+{LuckPerPoint * 100:0}% chance of extra cash, +2% crit chance",
        Stat.Defense => $"Take {DefensePerPoint * 100:0}% less damage",
        _ => $"+{CritPerPoint * 100:0}% chance to deal double damage",
    };

    public event Action Changed;

    readonly Dictionary<Stat, int> points = new Dictionary<Stat, int>();
    Health health;
    PlayerProgress progress;

    public int Get(Stat s) => points.TryGetValue(s, out int v) ? v : 0;

    public float SpeedMultiplier => 1f + SpeedPerPoint * Get(Stat.Speed);
    public float ExtraCashChance => LuckPerPoint * Get(Stat.Luck);
    public float CritChance => Mathf.Min(0.75f, CritPerPoint * Get(Stat.CritChance) + 0.02f * Get(Stat.Luck));

    void Awake()
    {
        Instance = this;
        health = GetComponent<Health>();
        progress = GetComponent<PlayerProgress>();
    }

    void Start()
    {
        if (progress) progress.LevelUp += _ => LevelUpScreen.Show(this);
    }

    public void Add(Stat s)
    {
        points[s] = Get(s) + 1;
        if (s == Stat.Health && health) health.SetMaxHealth(health.maxHealth + HealthPerPoint, healDifference: true);
        if (s == Stat.Defense && health) health.damageReduction = Mathf.Min(0.6f, DefensePerPoint * Get(Stat.Defense));
        Changed?.Invoke();
    }

    // Final damage for a hit, including crits and the Shooter upgrade.
    public float Melee(float baseDamage) => Roll(baseDamage * (1f + StrengthPerPoint * Get(Stat.Strength)));
    public float Ranged(float baseDamage) => Roll(baseDamage * (1f + PrecisionPerPoint * Get(Stat.Precision)));

    float Roll(float dmg)
    {
        var up = GetComponent<PlayerUpgrades>();
        if (up) dmg *= up.DamageMultiplier;
        return UnityEngine.Random.value < CritChance ? dmg * 2f : dmg;
    }

    // Final damage for a hit by `player` (their own stats and upgrades; co-op safe). Works without PlayerStats too.
    public static float MeleeDamage(float d, GameObject player)
    {
        var s = player ? player.GetComponent<PlayerStats>() : null;
        return s ? s.Melee(d) : d * PlayerUpgrades.DamageMultiplierFor(player);
    }

    public static float RangedDamage(float d, GameObject player)
    {
        var s = player ? player.GetComponent<PlayerStats>() : null;
        return s ? s.Ranged(d) : d * PlayerUpgrades.DamageMultiplierFor(player);
    }

    // Best Luck among the players (loot doesn't know who got the kill).
    public static float BestExtraCashChance()
    {
        float best = 0;
        foreach (var p in Players.All) { var s = p ? p.GetComponent<PlayerStats>() : null; if (s) best = Mathf.Max(best, s.ExtraCashChance); }
        return best;
    }

    public string Summary()
    {
        var parts = new List<string>();
        foreach (Stat s in Enum.GetValues(typeof(Stat))) if (Get(s) > 0) parts.Add($"{Name(s)} {Get(s)}");
        return string.Join(" · ", parts);
    }
}
