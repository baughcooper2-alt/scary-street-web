using System;
using UnityEngine;

// The player's money and XP for this run (DESIGN.md: XP fills your level bar; each level grants one upgrade pick).
// Picks are banked in PendingPicks until the upgrade screen exists.
public class PlayerProgress : MonoBehaviour
{
    [Tooltip("Thorton starts with $100 (DESIGN.md); everyone else 0.")]
    public int startingCash = 0;
    [Tooltip("XP needed for the next level = this × current level (10, 20, 30...; the web build used 10 then 20).")]
    public int xpPerLevel = 10;

    public int Cash { get; private set; }
    public int Xp { get; private set; }            // XP into the current level
    public int Level { get; private set; } = 1;
    public int PendingPicks { get; private set; }
    public int XpToNext => xpPerLevel * Level;
    public float XpFraction => (float)Xp / XpToNext;

    public event Action<int> LevelUp;              // new level
    public event Action<int> CashGained;           // amount

    void Awake() => Cash = startingCash;

    public void AddCash(int amount)
    {
        if (amount <= 0) return;
        Cash += amount;
        CashGained?.Invoke(amount);
    }

    public bool SpendCash(int amount)
    {
        if (amount > Cash) return false;
        Cash -= amount;
        return true;
    }

    public void AddXp(int amount)
    {
        Xp += Mathf.Max(0, amount);
        while (Xp >= XpToNext)
        {
            Xp -= XpToNext;
            Level++;
            PendingPicks++;
            LevelUp?.Invoke(Level);
        }
    }

    public bool UsePick()
    {
        if (PendingPicks <= 0) return false;
        PendingPicks--;
        return true;
    }
}
