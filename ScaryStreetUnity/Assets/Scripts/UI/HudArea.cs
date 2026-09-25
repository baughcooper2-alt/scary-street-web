using UnityEngine;

// Where a player's HUD goes: their camera's part of the screen (the whole screen solo, half in split-screen),
// in IMGUI coordinates (top-left origin). HUD scripts draw inside GUI.BeginGroup(HudArea.For(this)).
public static class HudArea
{
    public static Rect For(Component player)
    {
        var cam = player ? player.GetComponentInChildren<Camera>() : null;
        if (!cam || !cam.enabled) return new Rect(0, 0, Screen.width, Screen.height);
        var r = cam.pixelRect;
        return new Rect(r.x, Screen.height - r.yMax, r.width, r.height);
    }
}
