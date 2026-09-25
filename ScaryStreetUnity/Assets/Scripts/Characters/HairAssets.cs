using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// Real hair models (MakeHuman community assets, see CREDITS.md) fitted to our human head.
// The .obj files live in Resources/Hair as .obj.bytes so Unity loads them as plain data (no importer scale or
// axis surprises); this parses them and fits each to the head by its measured width (and depth, or front edge),
// sits it just above the scalp, and can pull long strands up into short curls.
public static class HairAssets
{
    public class Fit
    {
        public string file, texture;
        public float width = 0.215f;          // metres across (our skull is ~0.16 m wide)
        public float depth;                    // > 0: fit front-to-back too, centred on the skull
        public float front = 0.14f;            // otherwise: where the front edge (bangs) sits
        public float lift = 0.03f;             // top of the hair above the top of the head
        public float cut;                      // > 0: strands below this height are pulled up...
        public float squash = 0.2f;            // ...to this fraction of their length (long → short curls)
        public bool keepShell;                 // also grow the procedural hair shell underneath
        public bool alphaClip;                 // strand texture with see-through gaps
    }

    // Keyed by CharacterLook.hairAsset.
    public static readonly Dictionary<string, Fit> All = new Dictionary<string, Fit>
    {
        // Cooper: medium, swept out from a middle part, over the ears (culturalibre hair_05, CC0)
        ["cooper_flow"] = new Fit { file = "Hair/hair_05.obj", texture = "Hair/hair_05", width = 0.215f, front = 0.14f, lift = 0.03f },
        // Nathan: dark curls poking out under his cap (punkduck alpha7 curly, CC-BY), shortened
        ["nathan_curls"] = new Fit { file = "Hair/curly.obj", texture = "Hair/curly", width = 0.205f, depth = 0.25f, lift = 0.012f,
                                     cut = 1.66f, squash = 0.12f, keepShell = true, alphaClip = true },
    };

    static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

    // Mesh in head-bone space (the head bone sits at `headBone` in body space, with no rotation).
    public static Mesh Build(string key, Vector3 headBone, float headTop)
    {
        if (!All.TryGetValue(key, out var fit)) return null;
        string cacheKey = $"HairAsset_{key}";
        if (meshes.TryGetValue(cacheKey, out var cached) && cached) return MeshKit.Persist != null ? MeshKit.Persist(cacheKey, cached) : cached;

        var asset = Resources.Load<TextAsset>(fit.file);
        if (!asset) { Debug.LogWarning($"Hair asset {fit.file} not found in Resources."); return null; }
        Parse(asset.text, out var pos, out var uv, out var faces);
        if (pos.Count == 0) return null;

        // measure the model
        Vector3 min = pos[0], max = pos[0];
        foreach (var p in pos) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
        float s = fit.width / Mathf.Max(1e-6f, max.x - min.x);
        float sz = fit.depth > 0 ? fit.depth / Mathf.Max(1e-6f, max.z - min.z) : s;

        var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        var map = new Dictionary<(int, int), int>();
        foreach (var f in faces)
        {
            int[] tri = new int[3];
            for (int k = 0; k < 3; k++)
            {
                var id = f[k];
                if (!map.TryGetValue(id, out int idx))
                {
                    Vector3 p = pos[id.Item1];
                    // MakeHuman is right-handed: mirror X for Unity
                    float x = -p.x * s;
                    float y = (p.y - max.y) * s + headTop + fit.lift;
                    float z = fit.depth > 0 ? (p.z - (max.z + min.z) / 2f) * sz + 0.04f : (p.z - max.z) * s + fit.front;
                    if (fit.cut > 0 && y < fit.cut) y = fit.cut + (y - fit.cut) * fit.squash;
                    idx = verts.Count; map[id] = idx;
                    verts.Add(new Vector3(x, y, z) - headBone);
                    uvs.Add(id.Item2 >= 0 && id.Item2 < uv.Count ? uv[id.Item2] : Vector2.zero);
                }
                tri[k] = idx;
            }
            tris.Add(tri[0]); tris.Add(tri[2]); tris.Add(tri[1]);          // mirrored, so flip the winding back
        }

        var mesh = new Mesh { name = cacheKey, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        meshes[cacheKey] = mesh;
        return MeshKit.Persist != null ? MeshKit.Persist(cacheKey, mesh) : mesh;
    }

    // Hair material: the model's texture tinted with the look's hair colour; double-sided, alpha-clipped strands.
    public static Material Dress(Material m, Fit fit)
    {
        var tex = Resources.Load<Texture2D>(fit.texture);
        if (tex && m.mainTexture != tex) m.mainTexture = tex;
        m.SetFloat("_Cull", 0f);                                           // strands are single sheets: show both sides
        m.SetFloat("_Smoothness", 0.35f);
        if (fit.alphaClip)
        {
            m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.4f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        }
        return m;
    }

    static void Parse(string text, out List<Vector3> pos, out List<Vector2> uv, out List<(int, int)[]> faces)
    {
        pos = new List<Vector3>(); uv = new List<Vector2>(); faces = new List<(int, int)[]>();
        var ci = CultureInfo.InvariantCulture;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("v "))
            {
                var t = line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                pos.Add(new Vector3(float.Parse(t[1], ci), float.Parse(t[2], ci), float.Parse(t[3], ci)));
            }
            else if (line.StartsWith("vt "))
            {
                var t = line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                uv.Add(new Vector2(float.Parse(t[1], ci), float.Parse(t[2], ci)));
            }
            else if (line.StartsWith("f "))
            {
                var t = line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                var corners = new List<(int, int)>();
                for (int i = 1; i < t.Length; i++)
                {
                    var parts = t[i].Split('/');
                    int v = int.Parse(parts[0], ci) - 1;
                    int vt = parts.Length > 1 && parts[1].Length > 0 ? int.Parse(parts[1], ci) - 1 : -1;
                    corners.Add((v, vt));
                }
                for (int k = 1; k < corners.Count - 1; k++) faces.Add(new[] { corners[0], corners[k], corners[k + 1] });   // fan into triangles
            }
        }
    }
}
