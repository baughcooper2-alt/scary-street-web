using UnityEngine;

// Upgrades a character starts a run with (Isaiah's skateboard). Added by GameFlow when the player is dressed; applied
// on Start, once PlayerUpgrades has read the player's base speed.
public class StartingItems : MonoBehaviour
{
    public PlayerUpgrades.Id[] upgrades = new PlayerUpgrades.Id[0];

    void Start()
    {
        var up = GetComponent<PlayerUpgrades>();
        if (!up) return;
        foreach (var id in upgrades) if (up.Level(id) == 0) up.Add(id);
    }
}
