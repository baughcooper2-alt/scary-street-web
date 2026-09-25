using System.Collections.Generic;
using UnityEngine;

// Everyone playing (1 now, up to 4 in co-op). FirstPersonController registers itself.
// Enemies use Nearest() so they chase whoever is closest and still alive.
public static class Players
{
    public static readonly List<FirstPersonController> All = new List<FirstPersonController>();

    public static bool AnyAlive
    {
        get { foreach (var p in All) if (p && Alive(p)) return true; return false; }
    }

    public static Transform Nearest(Vector3 from, out Health health)
    {
        Transform best = null; health = null;
        float bestD = float.MaxValue;
        foreach (var p in All)
        {
            if (!p || !Alive(p)) continue;
            float d = (p.transform.position - from).sqrMagnitude;
            if (d < bestD) { bestD = d; best = p.transform; health = p.GetComponent<Health>(); }
        }
        return best;
    }

    // Camera of the player nearest to `pos` (for billboards in split-screen); falls back to Camera.main.
    public static Camera NearestCamera(Vector3 pos)
    {
        Camera best = null; float bestD = float.MaxValue;
        foreach (var p in All)
        {
            var c = p ? p.GetComponentInChildren<Camera>() : null;
            if (!c || !c.enabled) continue;
            float d = (c.transform.position - pos).sqrMagnitude;
            if (d < bestD) { bestD = d; best = c; }
        }
        return best ? best : Camera.main;
    }

    static bool Alive(FirstPersonController p)
    {
        var h = p.GetComponent<Health>();
        return p.gameObject.activeInHierarchy && (!h || !h.IsDead);
    }
}
