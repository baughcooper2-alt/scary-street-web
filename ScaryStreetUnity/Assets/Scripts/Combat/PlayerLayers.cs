using UnityEngine;

// Render layers that let each player's own body show up in mirrors (and to other players) but not block their
// own first-person view, and keep each player's first-person arms out of everyone else's view and the mirrors.
//   body of player i  → layer 9 + i      (hidden from player i's camera in first person)
//   first-person arms → layer 13 + i     (only player i's camera sees them)
//   mirror surfaces   → layer 4 ("Water", unused otherwise), never drawn in reflections
public static class PlayerLayers
{
    public const int Mirror = 4;
    public static int Body(int player) => 9 + Mathf.Clamp(player, 0, 3);
    public static int Arms(int player) => 13 + Mathf.Clamp(player, 0, 3);

    static int AllArms { get { int m = 0; for (int i = 0; i < 4; i++) m |= 1 << Arms(i); return m; } }

    public static int CameraMask(int player, bool thirdPerson)
    {
        int mask = ~AllArms;                                                    // third person: your body shows, no arms
        if (!thirdPerson) { mask &= ~(1 << Body(player)); mask |= 1 << Arms(player); }   // first person: arms, not the body
        return mask;
    }

    public static int MirrorMask => ~AllArms & ~(1 << Mirror);

    public static void Set(GameObject go, int layer)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }

    public static int IndexOf(Component c)
    {
        var controls = c ? c.GetComponentInParent<PlayerControls>() : null;
        return controls ? controls.playerIndex : 0;
    }
}
