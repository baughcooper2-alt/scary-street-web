using System.Collections.Generic;
using System.IO;
using UnityEngine;

// The web build's rigged human: a smooth 15k-vertex body on a 24-bone skeleton, plus fitted, skinned clothes
// (long-sleeve top, tee, pants, joggers) and a knit texture. Extracted into Resources/HumanBase.bytes and
// Resources/Knit.jpg by tools/extract_web_human.py (already in Unity coordinates).
// On load the arms are posed down from the A-pose, so every bone ends up hanging straight with no rotation:
// the same convention CharacterAnimator uses for the blocky rig (-X swings a limb forward).
public static class HumanBase
{
    public class Bone { public string name; public int parent; public Vector3 head, tail; }
    public class Part { public Vector3[] v; public Vector3[] n; public int[] t; public BoneWeight[] w; }
    public class Garment { public string key; public Part mesh; public int[] cover; }

    public static Bone[] Bones { get; private set; }
    public static Part Body { get; private set; }
    public static Dictionary<string, Garment> Garments { get; private set; }
    public static Texture2D Knit { get; private set; }

    static bool tried;

    public static bool Available { get { Load(); return Bones != null; } }

    public static int Index(string name)
    {
        for (int i = 0; i < Bones.Length; i++) if (Bones[i].name == name) return i;
        return -1;
    }

    static void Load()
    {
        if (tried) return;
        tried = true;
        var asset = Resources.Load<TextAsset>("HumanBase");
        if (!asset) return;
        Knit = Resources.Load<Texture2D>("Knit");

        using (var r = new BinaryReader(new MemoryStream(asset.bytes)))
        {
            if (new string(r.ReadChars(4)) != "SSHB" || r.ReadInt32() != 1) return;
            var bones = new Bone[r.ReadInt32()];
            for (int i = 0; i < bones.Length; i++)
                bones[i] = new Bone { name = Str(r), parent = r.ReadInt32(), head = V3(r), tail = V3(r) };
            var body = ReadPart(r);
            var garments = new Dictionary<string, Garment>();
            int g = r.ReadInt32();
            for (int i = 0; i < g; i++)
            {
                var key = Str(r);
                var part = ReadPart(r);
                var cover = new int[r.ReadInt32()];
                for (int c = 0; c < cover.Length; c++) cover[c] = r.ReadInt32();
                garments[key] = new Garment { key = key, mesh = part, cover = cover };
            }
            Bones = bones; Body = body; Garments = garments;
        }

        ArmsDown();
        Body.n = Normals(Body);
        foreach (var gm in Garments.Values) gm.mesh.n = Normals(gm.mesh);
    }

    static string Str(BinaryReader r) => System.Text.Encoding.UTF8.GetString(r.ReadBytes(r.ReadInt32()));
    static Vector3 V3(BinaryReader r) => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

    static Part ReadPart(BinaryReader r)
    {
        var p = new Part { v = new Vector3[r.ReadInt32()] };
        for (int i = 0; i < p.v.Length; i++) p.v[i] = V3(r);
        p.t = new int[r.ReadInt32()];
        for (int i = 0; i < p.t.Length; i++) p.t[i] = r.ReadInt32();
        p.w = new BoneWeight[p.v.Length];
        for (int i = 0; i < p.v.Length; i++)
        {
            int b0 = r.ReadInt32(); float w0 = r.ReadSingle();
            int b1 = r.ReadInt32(); float w1 = r.ReadSingle();
            int b2 = r.ReadInt32(); float w2 = r.ReadSingle();
            int b3 = r.ReadInt32(); float w3 = r.ReadSingle();
            float sum = w0 + w1 + w2 + w3; if (sum <= 0) { w0 = 1; sum = 1; }
            p.w[i] = new BoneWeight { boneIndex0 = b0, weight0 = w0 / sum, boneIndex1 = b1, weight1 = w1 / sum,
                                      boneIndex2 = b2, weight2 = w2 / sum, boneIndex3 = b3, weight3 = w3 / sum };
        }
        return p;
    }

    // Rotate each arm (upper arm, forearm, hand) about the shoulder so it hangs down at the side, moving the
    // skin with it by its bone weights (linear blend skinning), then treat that as the new rest pose.
    static void ArmsDown()
    {
        foreach (string side in new[] { "L", "R" })
        {
            int upper = Index($"Bone.002_{side}.002");
            if (upper < 0) continue;
            var chain = new HashSet<int> { upper, Index($"Bone.002_{side}.003"), Index($"Bone.002_{side}.004") };
            Vector3 pivot = Bones[upper].head;
            Vector3 dir = (Bones[upper].tail - Bones[upper].head).normalized;
            Vector3 want = new Vector3(Mathf.Sign(dir.x) * 0.1f, -1f, 0.03f).normalized;
            Quaternion rot = Quaternion.FromToRotation(dir, want);

            foreach (int b in chain)
            {
                Bones[b].head = pivot + rot * (Bones[b].head - pivot);
                Bones[b].tail = pivot + rot * (Bones[b].tail - pivot);
            }
            Repose(Body, chain, pivot, rot);
            foreach (var g in Garments.Values) Repose(g.mesh, chain, pivot, rot);
        }
    }

    static void Repose(Part p, HashSet<int> chain, Vector3 pivot, Quaternion rot)
    {
        for (int i = 0; i < p.v.Length; i++)
        {
            var w = p.w[i];
            float k = (chain.Contains(w.boneIndex0) ? w.weight0 : 0) + (chain.Contains(w.boneIndex1) ? w.weight1 : 0)
                    + (chain.Contains(w.boneIndex2) ? w.weight2 : 0) + (chain.Contains(w.boneIndex3) ? w.weight3 : 0);
            if (k <= 0) continue;
            Vector3 moved = pivot + rot * (p.v[i] - pivot);
            p.v[i] = Vector3.Lerp(p.v[i], moved, k);
        }
    }

    static Vector3[] Normals(Part p)
    {
        var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        m.SetVertices(p.v); m.SetTriangles(p.t, 0); m.RecalculateNormals();
        var n = m.normals;
        Object.DestroyImmediate(m);
        return n;
    }
}
