using UnityEngine;

// Drops XP gems and cash when this enemy is knocked out (web build: 3–4 gems + one $5 bill).
[RequireComponent(typeof(Health))]
public class LootDrop : MonoBehaviour
{
    public Vector2Int xpGems = new Vector2Int(3, 4);
    public int xpPerGem = 1;
    public int cashBills = 1;
    public int cashPerBill = 5;

    // RoundManager turns this off while it clears leftovers at 0:00, so time-ups don't pay out.
    public static bool Enabled = true;

    void Awake() => GetComponent<Health>().Died += Drop;

    void Drop()
    {
        if (!Enabled) return;
        int gems = Random.Range(xpGems.x, xpGems.y + 1);
        for (int i = 0; i < gems; i++) Pickup.Spawn(Pickup.Kind.Xp, xpPerGem, transform.position);
        int bills = cashBills + (Random.value < PlayerStats.BestExtraCashChance() ? 1 : 0);   // Luck
        for (int i = 0; i < bills; i++) Pickup.Spawn(Pickup.Kind.Cash, cashPerBill, transform.position);
    }
}
