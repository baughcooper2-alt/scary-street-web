using System.Collections.Generic;
using UnityEngine;

// Small procedural meshes Unity doesn't ship as primitives. All are unit-sized like the built-in
// sphere (diameter 1), so they scale the same way. Cached, so every character shares one copy.
public static class MeshKit
{
    static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

    // The editor sets this while building prefabs so the meshes get saved as assets (a prefab can't
    // keep a mesh that only exists in memory). Takes a file-safe name and the mesh, returns the asset.
    public static System.Func<string, Mesh, Mesh> Persist;

    static Mesh Get(string key, System.Func<Mesh> make)
    {
        if (!cache.TryGetValue(key, out var m) || !m) cache[key] = m = make();
        return Persist != null ? Persist(key, m) : m;
    }

    // Part of a sphere that follows a hairline: covers from the top down to `front` degrees at the
    // forehead, `side` over the ears and `back` at the nape (0 = top of the head, 180 = chin).
    // Also used for caps (a dome) by passing similar angles all round.
    public static Mesh HairCap(float front, float side, float back)
    {
        return Get($"HairCap_{front}_{side}_{back}", () => MakeHairCap(front, side, back));
    }

    static Mesh MakeHairCap(float front, float side, float back)
    {
        const int rings = 14, cols = 32;
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        var tris = new List<int>();
        for (int i = 0; i <= rings; i++)
            for (int j = 0; j <= cols; j++)
            {
                float phi = j / (float)cols * Mathf.PI * 2f;                   // 0 = facing +Z (the face)
                float c = Mathf.Cos(phi);
                float lim = (c >= 0 ? side + (front - side) * c * c : side + (back - side) * c * c) * Mathf.Deg2Rad;
                float theta = i / (float)rings * lim;
                var p = new Vector3(Mathf.Sin(theta) * Mathf.Sin(phi), Mathf.Cos(theta), Mathf.Sin(theta) * c);
                v.Add(p * 0.5f); n.Add(p);
            }
        for (int i = 0; i < rings; i++)
            for (int j = 0; j < cols; j++)
            {
                int a = i * (cols + 1) + j, b = a + cols + 1;
                tris.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });       // clockwise seen from outside (Unity front face)
            }
        return Build($"HairCap_{front}_{side}_{back}", v, n, tris);
    }

    // Ring lying flat in the XZ plane (axis = Y). `tube` is the tube radius relative to the ring's 0.5 radius.
    public static Mesh Torus(float tube = 0.12f)
    {
        return Get($"Torus_{tube}", () => MakeTorus(tube));
    }

    static Mesh MakeTorus(float tube)
    {
        const int segs = 28, sides = 10;
        float R = 0.5f - tube * 0.5f, r = tube * 0.5f;
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        var tris = new List<int>();
        for (int i = 0; i <= segs; i++)
        {
            float u = i / (float)segs * Mathf.PI * 2f;
            var center = new Vector3(Mathf.Cos(u), 0, Mathf.Sin(u)) * R;
            for (int j = 0; j <= sides; j++)
            {
                float w = j / (float)sides * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(u) * Mathf.Cos(w), Mathf.Sin(w), Mathf.Sin(u) * Mathf.Cos(w));
                v.Add(center + dir * r); n.Add(dir);
            }
        }
        for (int i = 0; i < segs; i++)
            for (int j = 0; j < sides; j++)
            {
                int a = i * (sides + 1) + j, b = a + sides + 1;
                tris.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
            }
        return Build($"Torus_{tube}", v, n, tris);
    }

    static Mesh Build(string name, List<Vector3> v, List<Vector3> n, List<int> tris)
    {
        var mesh = new Mesh { name = name };
        mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}
