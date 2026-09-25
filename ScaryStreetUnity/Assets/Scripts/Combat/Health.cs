using System;
using UnityEngine;

// Hit points for anything that can be hurt: players, enemies, bosses.
// Other scripts listen to Damaged / Died instead of polling current.
public class Health : MonoBehaviour
{
    [Tooltip("Players start at 25 HP (DESIGN.md). Enemies set their own.")]
    public float maxHealth = 25f;
    [Tooltip("Seconds of invulnerability after a hit (0 = none).")]
    public float invulnerableTime = 0f;

    public float Current { get; private set; }
    public float Fraction => maxHealth > 0 ? Current / maxHealth : 0f;
    public bool IsDead { get; private set; }

    public event Action<float> Damaged;   // amount
    public event Action Died;

    float invulnUntil;

    void Awake() => Current = maxHealth;

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0 || Time.time < invulnUntil) return;
        Current = Mathf.Max(0, Current - amount);
        invulnUntil = Time.time + invulnerableTime;
        Damaged?.Invoke(amount);
        if (Current <= 0) { IsDead = true; Died?.Invoke(); }
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        Current = Mathf.Min(maxHealth, Current + amount);
    }

    // Raise or lower max HP (Cane's chicken); optionally heal by however much the max went up.
    public void SetMaxHealth(float newMax, bool healDifference)
    {
        float gained = newMax - maxHealth;
        maxHealth = Mathf.Max(1f, newMax);
        Current = Mathf.Min(maxHealth, Current + (healDifference && gained > 0 ? gained : 0));
    }

    public void ResetHealth(float newMax)
    {
        maxHealth = newMax;
        Current = newMax;
        IsDead = false;
    }
}
