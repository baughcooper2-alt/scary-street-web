using System.Collections.Generic;
using UnityEngine;

// Keeps everyone on the map. When a scene with the world loads it finds the built-up ground (downward rays on a 2 m
// grid over the world; hits near ground level count, roofs don't) and stands four tall invisible walls just outside
// it. Anyone who still ends up well below the ground (a gap in an odd corner) is put back where they last stood.
// Made on its own after the scene loads; nothing to set up.
public class MapBarrier : MonoBehaviour
{
    public float wallHeight = 40f, wallThickness = 1f, margin = 0.3f;
    [Tooltip("Metres below the ground at which a player is rescued.")]
    public float fallDepth = 8f;

    Transform world;
    float groundY;
    readonly Dictionary<FirstPersonController, Vector3> lastSafe = new Dictionary<FirstPersonController, Vector3>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Spawn()
    {
        if (FindAnyObjectByType<MapBarrier>()) return;
        var w = GameObject.Find("scary-street-world");
        if (!w) return;
        new GameObject("Map Barrier").AddComponent<MapBarrier>().world = w.transform;
    }

    void Start()
    {
        if (!world) return;
        var rs = world.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        var all = rs[0].bounds;
        foreach (var r in rs) all.Encapsulate(r.bounds);
        groundY = all.min.y;

        // the ground's extent: cells whose top hit is near ground level (streets, yards; not roofs or treetops).
        // Ground level = the most common height the rays land on (streets and yards cover the most area).
        const float step = 2f;
        var hits = new List<Vector3>();
        for (float x = all.min.x; x <= all.max.x; x += step)
            for (float z = all.min.z; z <= all.max.z; z += step)
                if (Physics.Raycast(new Vector3(x, all.max.y + 5f, z), Vector3.down, out var hit, all.size.y + 10f, ~0, QueryTriggerInteraction.Ignore) && hit.normal.y > 0.7f)
                    hits.Add(new Vector3(x, hit.point.y, z));
        if (hits.Count == 0) return;
        var bins = new Dictionary<int, int>();
        foreach (var h in hits) { int k = Mathf.RoundToInt(h.y * 4f); bins[k] = (bins.TryGetValue(k, out var c) ? c : 0) + 1; }
        int best = 0, bestN = -1;
        foreach (var kv in bins) if (kv.Value > bestN) { bestN = kv.Value; best = kv.Key; }
        groundY = best / 4f;
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var h in hits)
        {
            if (Mathf.Abs(h.y - groundY) > 1.5f) continue;
            minX = Mathf.Min(minX, h.x); maxX = Mathf.Max(maxX, h.x); minZ = Mathf.Min(minZ, h.z); maxZ = Mathf.Max(maxZ, h.z);
        }
        if (minX > maxX) return;
        minX -= margin; maxX += margin; minZ -= margin; maxZ += margin;
        float cx = (minX + maxX) / 2, cz = (minZ + maxZ) / 2, sx = maxX - minX, sz = maxZ - minZ, y = groundY + wallHeight / 2 - 5f;
        Wall("North", new Vector3(cx, y, maxZ + wallThickness / 2), new Vector3(sx + 2 * wallThickness, wallHeight, wallThickness));
        Wall("South", new Vector3(cx, y, minZ - wallThickness / 2), new Vector3(sx + 2 * wallThickness, wallHeight, wallThickness));
        Wall("East", new Vector3(maxX + wallThickness / 2, y, cz), new Vector3(wallThickness, wallHeight, sz));
        Wall("West", new Vector3(minX - wallThickness / 2, y, cz), new Vector3(wallThickness, wallHeight, sz));
        Debug.Log($"MapBarrier: walls round x {minX:0.#}..{maxX:0.#}, z {minZ:0.#}..{maxZ:0.#}, ground y {groundY:0.##}");
    }

    void Wall(string name, Vector3 centre, Vector3 size)
    {
        var go = new GameObject("Barrier " + name);
        go.transform.SetParent(transform, false);
        go.transform.position = centre;
        go.AddComponent<BoxCollider>().size = size;
    }

    void Update()
    {
        foreach (var p in Players.All)
        {
            if (!p) continue;
            if (p.IsGrounded) lastSafe[p] = p.transform.position;
            if (p.transform.position.y > groundY - fallDepth) continue;
            // fell through somewhere: back to the last ground they stood on
            var cc = p.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            p.transform.position = (lastSafe.TryGetValue(p, out var safe) ? safe : world.position) + Vector3.up * 0.5f;
            if (cc) cc.enabled = true;
        }
    }
}
