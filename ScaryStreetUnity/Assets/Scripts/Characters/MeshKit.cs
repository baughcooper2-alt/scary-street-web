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
    // A box with rounded edges and corners (real size in metres, not unit-sized: the radius has to stay round).
    // Each face is a grid that bunches up at the edges; every vertex is pushed onto a sphere of `radius` round the
    // inner box, so the normals are smooth across the bevels.
    public static Mesh RoundedBox(Vector3 size, float radius, int seg = 4)
    {
        radius = Mathf.Clamp(radius, 0.0005f, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.5f);
        return Get($"RoundedBox_{size.x:0.####}_{size.y:0.####}_{size.z:0.####}_{radius:0.####}", () => MakeRoundedBox(size, radius, seg));
    }

    static Mesh MakeRoundedBox(Vector3 size, float r, int seg)
    {
        Vector3 half = size * 0.5f, inner = half - Vector3.one * r;
        float[] Steps(float h)                                                   // -h .. -h+r (curved), then h-r .. h
        {
            var l = new List<float>();
            for (int k = 0; k <= seg; k++) l.Add(-h + r * (1f - Mathf.Cos(k / (float)seg * Mathf.PI * 0.5f)));
            for (int k = seg; k >= 0; k--) l.Add(h - r * (1f - Mathf.Cos(k / (float)seg * Mathf.PI * 0.5f)));
            return l.ToArray();
        }
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tris = new List<int>();
        // (normal axis, u axis, v axis) per face; u x v = normal so the winding faces out
        var faces = new[] { (0, 1, 2), (1, 2, 0), (2, 0, 1) };
        foreach (var (a, b, c) in faces)
            foreach (float sign in new[] { 1f, -1f })
            {
                float[] us = Steps(half[b]), vs = Steps(half[c]);
                int start = v.Count;
                for (int i = 0; i < us.Length; i++)
                    for (int j = 0; j < vs.Length; j++)
                    {
                        var p = Vector3.zero; p[a] = half[a] * sign; p[b] = us[i]; p[c] = vs[j];
                        var q = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                        var d = p - q; var nn = d.sqrMagnitude > 1e-12f ? d.normalized : Vector3.zero;
                        if (nn == Vector3.zero) nn[a] = sign;
                        v.Add(q + nn * r); n.Add(nn); uv.Add(new Vector2((us[i] + half[b]) / size[b], (vs[j] + half[c]) / size[c]));
                    }
                int cols = vs.Length;
                for (int i = 0; i < us.Length - 1; i++)
                    for (int j = 0; j < cols - 1; j++)
                    {
                        int i0 = start + i * cols + j, i1 = i0 + cols, i2 = i0 + 1, i3 = i1 + 1;
                        if (sign > 0) { tris.Add(i0); tris.Add(i1); tris.Add(i3); tris.Add(i0); tris.Add(i3); tris.Add(i2); }
                        else { tris.Add(i0); tris.Add(i3); tris.Add(i1); tris.Add(i0); tris.Add(i2); tris.Add(i3); }
                    }
            }
        var m = new Mesh { name = "RoundedBox" };
        m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        return m;
    }

    // A turned (lathe) shape round Y from a profile of (radius, height) points, bottom to top, closed with caps.
    // Smooth normals from the profile's slope.
    public static Mesh Lathe(string key, Vector2[] profile, int sides = 28)
    {
        return Get("Lathe_" + key, () => MakeLathe(profile, sides));
    }

    static Mesh MakeLathe(Vector2[] pr, int sides)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tris = new List<int>();
        int rows = pr.Length;
        for (int i = 0; i < rows; i++)
        {
            Vector2 prev = pr[Mathf.Max(0, i - 1)], next = pr[Mathf.Min(rows - 1, i + 1)];
            Vector2 t = (next - prev).normalized, pn = new Vector2(t.y, -t.x);          // outward in the (r, y) plane
            for (int j = 0; j <= sides; j++)
            {
                float a = j / (float)sides * Mathf.PI * 2f, c = Mathf.Cos(a), s = Mathf.Sin(a);
                v.Add(new Vector3(pr[i].x * c, pr[i].y, pr[i].x * s)); n.Add(new Vector3(pn.x * c, pn.y, pn.x * s).normalized);
                uv.Add(new Vector2(j / (float)sides, i / (float)(rows - 1)));
            }
        }
        for (int i = 0; i < rows - 1; i++)
            for (int j = 0; j < sides; j++)
            {
                int a = i * (sides + 1) + j, b = a + sides + 1;
                tris.Add(a); tris.Add(b); tris.Add(a + 1); tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
            }
        foreach (int end in new[] { 0, rows - 1 })                               // flat caps
        {
            if (pr[end].x < 1e-4f) continue;
            float y = pr[end].y; bool top = end > 0; int c0 = v.Count;
            v.Add(new Vector3(0, y, 0)); n.Add(top ? Vector3.up : Vector3.down); uv.Add(new Vector2(0.5f, 0.5f));
            for (int j = 0; j <= sides; j++)
            {
                float a = j / (float)sides * Mathf.PI * 2f;
                v.Add(new Vector3(pr[end].x * Mathf.Cos(a), y, pr[end].x * Mathf.Sin(a))); n.Add(top ? Vector3.up : Vector3.down);
                uv.Add(new Vector2(0.5f + 0.5f * Mathf.Cos(a), 0.5f + 0.5f * Mathf.Sin(a)));
            }
            for (int j = 0; j < sides; j++)
                if (top) { tris.Add(c0); tris.Add(c0 + 2 + j); tris.Add(c0 + 1 + j); }
                else { tris.Add(c0); tris.Add(c0 + 1 + j); tris.Add(c0 + 2 + j); }
        }
        var m = new Mesh { name = "Lathe" };
        m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        return m;
    }

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
