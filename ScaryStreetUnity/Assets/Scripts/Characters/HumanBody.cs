using System.Collections.Generic;
using UnityEngine;

// Dresses the web build's rigged human (HumanBase) as a character from a CharacterLook, the way the web build did:
// skinned body (skin, shoes and socks; skin under the clothes takes the clothing colour so nothing peeks through),
// fitted skinned clothes with the knit texture, a face (eyes with iris / pupil / lids, brows, nose, lips), hair grown
// from the head's own surface along a hairline, a cap or visor, sneakers, and the look's extras (tee print, wristband,
// name tag). Returns a BlockyCharacter whose joints point at the human bones, so CharacterAnimator drives it as-is.
public static class HumanBody
{
    public static bool CanBuild(CharacterLook look) => look && look.realisticBody && HumanBase.Available;

    const float ShortsCut = 0.6f;          // shorts end here (m above the floor on the 1.8 m body)
    static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

    // ---------- head measurements (computed once) ----------

    struct HeadInfo { public float hx, hy0, hy1, hz0, hz1, cy, hairCy, eyeY, mouthY, browY; public Vector3 bone; public List<int> tris, verts; }
    static HeadInfo? head;

    public static BlockyCharacter Build(CharacterLook look, Transform parent, BlockyCharacter.MaterialSource mat)
    {
        var bones = HumanBase.Bones;
        var root = new GameObject("Model").transform;
        root.SetParent(parent, false);
        root.localScale = Vector3.one * (look.height / 1.8f);
        var b = root.gameObject.AddComponent<BlockyCharacter>();
        b.look = look;

        // skeleton: an extra Hips joint above the spine and both leg roots so the whole body can bob
        int spineRoot = HumanBase.Index("Bone");
        var hips = new GameObject("Hips").transform;
        hips.SetParent(root, false);
        hips.localPosition = bones[spineRoot].head;
        var t = new Transform[bones.Length];
        for (int i = 0; i < bones.Length; i++) t[i] = new GameObject(bones[i].name).transform;
        for (int i = 0; i < bones.Length; i++)
        {
            var p = bones[i].parent >= 0 ? t[bones[i].parent] : hips;
            Vector3 parentHead = bones[i].parent >= 0 ? bones[bones[i].parent].head : bones[spineRoot].head;
            t[i].SetParent(p, false);
            t[i].localPosition = bones[i].head - parentHead;
        }
        Transform B(string n) => t[HumanBase.Index(n)];
        b.hips = hips;
        b.spine = B("Bone.001"); b.neck = B("Bone.004"); b.head = B("Bone.005");
        b.shoulderL = B("Bone.002_L.002"); b.elbowL = B("Bone.002_L.003");
        b.shoulderR = B("Bone.002_R.002"); b.elbowR = B("Bone.002_R.003"); b.handR = B("Bone.002_R.004");
        b.legL = B("Bone_L.002"); b.kneeL = B("Bone_L.003");
        b.legR = B("Bone_R.002"); b.kneeR = B("Bone_R.003");
        b.ankleL = B("Bone_L.004"); b.ankleR = B("Bone_R.004");

        string topKey = look.longSleeves ? "top" : look.oversizedShirt ? "tee_big" : "tee";
        string bottomKey = look.shorts ? "shorts" : "pants";

        // body
        var bodySmr = Skinned(root, "Body", BodyMesh(topKey, bottomKey), hips, t);
        bodySmr.sharedMaterials = new[]
        {
            mat("Skin", look.skin), mat("Shoes", look.shoes), mat("Socks", look.socks),
            Cloth(mat("Shirt", look.shirt)), Cloth(mat("Pants", look.pants)),
        };
        b.skinParts.Add(bodySmr);

        // clothes
        Skinned(root, "Top", GarmentMesh(topKey), hips, t).sharedMaterial = Cloth(mat("Shirt", look.shirt));
        Skinned(root, "Bottom", GarmentMesh(bottomKey), hips, t).sharedMaterial = Cloth(mat("Pants", look.pants));

        var h = Head();
        new Dresser(b, look, mat, h).Dress(B, topKey);
        return b;
    }

    static Material Cloth(Material m)
    {
        if (m && HumanBase.Knit && m.mainTexture != HumanBase.Knit)
        {
            m.mainTexture = HumanBase.Knit;
            m.mainTextureScale = Vector2.one;
        }
        return m;
    }

    static SkinnedMeshRenderer Skinned(Transform root, string name, Mesh mesh, Transform rootBone, Transform[] bones)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        var smr = go.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = mesh;
        smr.bones = bones;
        smr.rootBone = rootBone;
        smr.updateWhenOffscreen = false;
        smr.localBounds = new Bounds(new Vector3(0, -0.05f, 0), new Vector3(1.6f, 2.2f, 1f));   // around the hips joint
        return smr;
    }

    // ---------- meshes (shared by everyone with the same outfit) ----------

    static Matrix4x4[] BindPoses()
    {
        var bones = HumanBase.Bones;
        var bp = new Matrix4x4[bones.Length];
        for (int i = 0; i < bones.Length; i++) bp[i] = Matrix4x4.Translate(-bones[i].head);
        return bp;
    }

    static Mesh Persist(string key, Mesh m)
    {
        meshes[key] = m;
        return MeshKit.Persist != null ? MeshKit.Persist(key, m) : m;
    }

    static Mesh Cached(string key) => meshes.TryGetValue(key, out var m) && m ? (MeshKit.Persist != null ? MeshKit.Persist(key, m) : m) : null;

    // Body split into skin / shoes / socks / under-the-top / under-the-bottom submeshes, so each look only
    // needs different materials. Covered skin is pulled in 6 mm so it never pokes through the clothes.
    static Mesh BodyMesh(string topKey, string bottomKey)
    {
        string key = $"HumanBody_{topKey}_{bottomKey}";
        var cached = Cached(key); if (cached) return cached;

        var body = HumanBase.Body; var bones = HumanBase.Bones;
        int n = body.v.Length;
        var cover = new int[n];                          // 0 none, 1 top, 2 bottom
        var skip = new HashSet<int>();
        foreach (var name in new[] { "Bone.005", "Bone.004", "Bone.002_L.004", "Bone.002_R.004", "Bone_L.004", "Bone_R.004", "Bone_L.005", "Bone_R.005" })
            skip.Add(HumanBase.Index(name));
        Mark(cover, GarmentCover(topKey), 1, skip, false, false);
        Mark(cover, GarmentCover(bottomKey), 2, skip, true, bottomKey == "shorts");
        BoneCover(cover, topKey, bottomKey);                             // plus everything on the bones the clothes sit on
        Grow(cover, skip, bottomKey == "shorts");                        // one more ring so no skin peeks at shoulders / waist

        var v = new Vector3[n];
        for (int i = 0; i < n; i++) v[i] = cover[i] != 0 ? body.v[i] - body.n[i] * 0.01f : body.v[i];

        var groups = new List<int>[5];
        for (int g = 0; g < 5; g++) groups[g] = new List<int>();
        int foot(string s) => HumanBase.Index($"Bone_{s}.004"); int toe(string s) => HumanBase.Index($"Bone_{s}.005"); int shin(string s) => HumanBase.Index($"Bone_{s}.003");
        var shoeBones = new HashSet<int> { foot("L"), foot("R"), toe("L"), toe("R") };
        var shinBones = new HashSet<int> { shin("L"), shin("R") };
        for (int i = 0; i < body.t.Length; i += 3)
        {
            int a = body.t[i], bb = body.t[i + 1], c = body.t[i + 2];
            int covered = Majority(cover[a], cover[bb], cover[c]);
            int group;
            if (covered == 1) group = 3;
            else if (covered == 2) group = 4;
            else
            {
                int bone = Majority(body.w[a].boneIndex0, body.w[bb].boneIndex0, body.w[c].boneIndex0);
                float cy = (body.v[a].y + body.v[bb].y + body.v[c].y) / 3f;
                group = shoeBones.Contains(bone) ? 1 : shinBones.Contains(bone) && cy < 0.15f ? 2 : 0;
            }
            groups[group].Add(a); groups[group].Add(bb); groups[group].Add(c);
        }

        var m = new Mesh { name = key, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        m.SetVertices(v); m.SetNormals(body.n);
        m.boneWeights = body.w;
        m.bindposes = BindPoses();
        m.subMeshCount = 5;
        for (int g = 0; g < 5; g++) m.SetTriangles(groups[g], g);
        m.SetUVs(0, ClothUVs(v, body.n));
        m.RecalculateBounds();
        return Persist(key, m);
    }

    static int[] GarmentCover(string key)
    {
        string baseKey = key == "tee_big" ? "tee" : key == "shorts" ? "pants" : key;
        return HumanBase.Garments.TryGetValue(baseKey, out var g) ? g.cover : new int[0];
    }

    static void Mark(int[] cover, int[] verts, int value, HashSet<int> skip, bool bottom, bool shorts)
    {
        var body = HumanBase.Body;
        foreach (int v in verts)
        {
            if (v < 0 || v >= cover.Length || skip.Contains(body.w[v].boneIndex0)) continue;
            if (shorts && body.v[v].y < ShortsCut) continue;          // bare legs below the shorts
            if (cover[v] == 0 || (bottom && body.v[v].y < 0.98f)) cover[v] = value;   // the waistband wins below the belt line
        }
    }

    // Skin on the bones a garment covers: torso and arms for a top (upper half of the arm for a tee),
    // hips and legs for pants (thighs above the cut for shorts).
    static void BoneCover(int[] cover, string topKey, string bottomKey)
    {
        var body = HumanBase.Body; var bones = HumanBase.Bones;
        int I(string n) => HumanBase.Index(n);
        var torso = new HashSet<int> { I("Bone"), I("Bone.001"), I("Bone.002"), I("Bone.003"), I("Bone.002_L.001"), I("Bone.002_R.001") };
        var upper = new HashSet<int> { I("Bone.002_L.002"), I("Bone.002_R.002") };
        var fore = new HashSet<int> { I("Bone.002_L.003"), I("Bone.002_R.003") };
        var hips = new HashSet<int> { I("Bone"), I("Bone_L"), I("Bone_R") };
        var thighs = new HashSet<int> { I("Bone_L.002"), I("Bone_R.002") };
        var shins = new HashSet<int> { I("Bone_L.003"), I("Bone_R.003") };
        bool longSleeves = topKey == "top", shorts = bottomKey == "shorts";
        for (int v = 0; v < body.v.Length; v++)
        {
            int bone = body.w[v].boneIndex0; Vector3 p = body.v[v];
            bool bottom = p.y < 0.98f && (hips.Contains(bone) || (thighs.Contains(bone) && (!shorts || p.y > ShortsCut)) || (!shorts && shins.Contains(bone) && p.y > 0.1f));
            bool top = (torso.Contains(bone) && p.y > 0.95f && p.y < 1.47f)
                    || (upper.Contains(bone) && (longSleeves || Along(bone, p) < 0.55f))
                    || (longSleeves && fore.Contains(bone));
            if (bottom) cover[v] = 2;
            else if (top && cover[v] == 0) cover[v] = 1;
        }
    }

    static float Along(int bone, Vector3 p)
    {
        var b = HumanBase.Bones[bone]; Vector3 ax = b.tail - b.head;
        return Vector3.Dot(p - b.head, ax) / Mathf.Max(1e-6f, ax.sqrMagnitude);
    }

    // Spread the clothing cover to each covered vertex's neighbours (not onto the head, hands or feet, and not
    // below the shorts line).
    static void Grow(int[] cover, HashSet<int> skip, bool shorts)
    {
        var body = HumanBase.Body;
        var next = (int[])cover.Clone();
        for (int i = 0; i < body.t.Length; i += 3)
        {
            int a = body.t[i], b = body.t[i + 1], c = body.t[i + 2];
            int val = cover[a] != 0 ? cover[a] : cover[b] != 0 ? cover[b] : cover[c];
            if (val == 0) continue;
            foreach (int v in new[] { a, b, c })
            {
                if (next[v] != 0 || skip.Contains(body.w[v].boneIndex0)) continue;
                if (shorts && val == 2 && body.v[v].y < ShortsCut) continue;
                next[v] = val;
            }
        }
        System.Array.Copy(next, cover, cover.Length);
    }

    static int Majority(int a, int b, int c) => a == b || a == c ? a : b == c ? b : a;

    // Garment meshes: the web build's fitted clothes, plus shorts (the pants cut off above the knee) and an
    // oversized tee (the tee puffed out and hanging a little lower).
    static Mesh GarmentMesh(string key)
    {
        var cached = Cached("Garment_" + key); if (cached) return cached;
        string baseKey = key == "tee_big" ? "tee" : key == "shorts" ? "pants" : key;
        if (!HumanBase.Garments.TryGetValue(baseKey, out var g)) return null;
        var p = g.mesh;

        var v = (Vector3[])p.v.Clone();
        if (key == "tee_big")
            for (int i = 0; i < v.Length; i++)
            {
                float hang = v[i].y < 1.0f ? (1.0f - v[i].y) * 0.4f : 0f;
                v[i] += p.n[i] * 0.014f + Vector3.down * hang;
            }

        var tris = new List<int>(p.t.Length);
        for (int i = 0; i < p.t.Length; i += 3)
        {
            if (key == "shorts" && (p.v[p.t[i]].y < ShortsCut || p.v[p.t[i + 1]].y < ShortsCut || p.v[p.t[i + 2]].y < ShortsCut)) continue;
            tris.Add(p.t[i]); tris.Add(p.t[i + 1]); tris.Add(p.t[i + 2]);
        }

        var m = new Mesh { name = "Garment_" + key, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        m.SetVertices(v); m.SetTriangles(tris, 0);
        m.boneWeights = p.w;
        m.bindposes = BindPoses();
        m.RecalculateNormals();
        m.SetUVs(0, ClothUVs(v, m.normals));
        m.RecalculateBounds();
        return Persist("Garment_" + key, m);
    }

    // Web build: project the knit from the side or the front depending on which way the surface faces.
    static Vector2[] ClothUVs(Vector3[] v, Vector3[] n)
    {
        var uv = new Vector2[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            bool side = Mathf.Abs(n[i].x) > Mathf.Abs(n[i].z);
            uv[i] = new Vector2((side ? v[i].z : v[i].x) * 9f, v[i].y * 9f);
        }
        return uv;
    }

    static HeadInfo Head()
    {
        if (head.HasValue) return head.Value;
        var body = HumanBase.Body;
        int hb = HumanBase.Index("Bone.005");
        var h = new HeadInfo { hy0 = 9, hy1 = -9, hz0 = 9, hz1 = -9, tris = new List<int>(), verts = new List<int>(), bone = HumanBase.Bones[hb].head };
        for (int i = 0; i < body.v.Length; i++)
        {
            if (body.w[i].boneIndex0 != hb) continue;
            var p = body.v[i];
            h.verts.Add(i);
            h.hx = Mathf.Max(h.hx, Mathf.Abs(p.x));
            h.hy0 = Mathf.Min(h.hy0, p.y); h.hy1 = Mathf.Max(h.hy1, p.y);
            h.hz0 = Mathf.Min(h.hz0, p.z); h.hz1 = Mathf.Max(h.hz1, p.z);
        }
        for (int i = 0; i < body.t.Length; i += 3)
            if (Majority(body.w[body.t[i]].boneIndex0, body.w[body.t[i + 1]].boneIndex0, body.w[body.t[i + 2]].boneIndex0) == hb) h.tris.Add(i);
        h.cy = (h.hy0 + h.hy1) / 2f;
        float ry = (h.hy1 - h.hy0) / 2f;
        h.eyeY = h.cy + ry * 0.12f;
        h.mouthY = h.cy - ry * 0.52f;
        h.browY = h.eyeY + ry * 0.2f;
        h.hairCy = (h.eyeY + h.hy1) / 2f + 0.01f;
        head = h;
        return h;
    }

    // ---------- face, hair, hats, shoes, extras ----------

    class Dresser
    {
        readonly BlockyCharacter b; readonly CharacterLook L; readonly BlockyCharacter.MaterialSource mat; readonly HeadInfo H;
        float flare;
        public Dresser(BlockyCharacter b, CharacterLook look, BlockyCharacter.MaterialSource mat, HeadInfo h) { this.b = b; L = look; this.mat = mat; H = h; }

        Vector3 Local(Vector3 p) => p - H.bone;                     // head-bone space (bones have no rotation)

        // Front of the face at (x, y): cast a ray at the head's own triangles (web build's surfZ).
        float SurfZ(float x, float y)
        {
            var body = HumanBase.Body; float best = -9;
            foreach (int t in H.tris)
            {
                Vector3 a = body.v[body.t[t]], bb = body.v[body.t[t + 1]], c = body.v[body.t[t + 2]];
                float den = (bb.y - c.y) * (a.x - c.x) + (c.x - bb.x) * (a.y - c.y);
                if (Mathf.Abs(den) < 1e-9f) continue;
                float l1 = ((bb.y - c.y) * (x - c.x) + (c.x - bb.x) * (y - c.y)) / den;
                float l2 = ((c.y - a.y) * (x - c.x) + (a.x - c.x) * (y - c.y)) / den;
                float l3 = 1 - l1 - l2;
                if (l1 < -1e-4f || l2 < -1e-4f || l3 < -1e-4f) continue;
                best = Mathf.Max(best, l1 * a.z + l2 * bb.z + l3 * c.z);
            }
            if (best > -9) return best;
            float rz = (H.hz1 - H.hz0) / 2f, ry = (H.hy1 - H.hy0) / 2f;
            return (H.hz1 + H.hz0) / 2f + rz * Mathf.Sqrt(Mathf.Max(0.05f, 1 - (x / H.hx) * (x / H.hx) - ((y - H.cy) / ry) * ((y - H.cy) / ry)));
        }

        public void Dress(System.Func<string, Transform> B, string topKey)
        {
            Face();
            Hair();
            Shoes(B("Bone_L.004"), B("Bone_L.005"), "L");
            Shoes(B("Bone_R.004"), B("Bone_R.005"), "R");
            Extras(B, topKey);
        }

        void Face()
        {
            var skin = mat("Skin", L.skin);
            for (int sd = -1; sd <= 1; sd += 2)
            {
                float x = 0.031f * sd, z = SurfZ(x, H.eyeY);
                var white = Sphere(b.head, "EyeWhite", Local(new Vector3(x, H.eyeY, z - 0.004f)), new Vector3(1.4f, 0.78f, 0.5f) * 0.033f, mat("EyeWhite", new Color(0.96f, 0.95f, 0.93f)));
                white.localRotation = Quaternion.Euler(0, 0, 4.6f * sd);
                Sphere(b.head, "Iris", Local(new Vector3(x, H.eyeY, z + 0.004f)), new Vector3(0.95f, 1f, 0.3f) * 0.0184f, mat("Eyes", L.eyes));
                Sphere(b.head, "Pupil", Local(new Vector3(x, H.eyeY, z + 0.0065f)), new Vector3(1f, 1f, 0.3f) * 0.009f, mat("Pupil", new Color(0.04f, 0.035f, 0.03f)));
                Sphere(b.head, "Shine", Local(new Vector3(x + 0.003f, H.eyeY + 0.003f, z + 0.008f)), Vector3.one * 0.004f, mat("Shine", Color.white));
                var lid = Mesh(MeshKit.HairCap(76, 76, 76), b.head, "Lid", Local(new Vector3(x, H.eyeY + 0.001f, z - 0.005f)), new Vector3(1.42f, 0.8f, 0.55f) * 0.036f, skin);
                lid.localRotation = Quaternion.Euler(0, 0, 4.6f * sd);
                b.skinParts.Add(lid.GetComponent<Renderer>());
                var brow = Box(b.head, "Brow", Local(new Vector3(x * 1.05f, H.browY, SurfZ(x * 1.05f, H.browY) + 0.002f)), new Vector3(0.042f, 0.009f, 0.01f), mat("Brows", L.brows));
                brow.localRotation = Quaternion.Euler(0, 0, -6.9f * sd);
            }

            float tipY = H.mouthY + (H.eyeY - H.mouthY) * 0.38f, brY = (H.eyeY + tipY) / 2f;
            var bridge = Prim(PrimitiveType.Cylinder, b.head, "NoseBridge", Local(new Vector3(0, brY, SurfZ(0, brY) + 0.002f)), new Vector3(0.016f, (H.eyeY - tipY) / 2f, 0.013f), skin);
            bridge.localRotation = Quaternion.Euler(-18f, 0, 0);
            Sphere(b.head, "NoseTip", Local(new Vector3(0, tipY, SurfZ(0, tipY) + 0.009f)), new Vector3(1.05f, 0.85f, 0.9f) * 0.021f, skin);
            for (int sd = -1; sd <= 1; sd += 2)
                Sphere(b.head, "Nostril", Local(new Vector3(0.0115f * sd, tipY - 0.003f, SurfZ(0.0115f * sd, tipY) + 0.002f)), new Vector3(0.9f, 0.8f, 1f) * 0.0144f, skin);
            foreach (Transform c in b.head) if (c.name.StartsWith("Nos")) b.skinParts.Add(c.GetComponent<Renderer>());

            float mz = SurfZ(0, H.mouthY);
            Sphere(b.head, "Mouth", Local(new Vector3(0, H.mouthY, mz - 0.004f)), new Vector3(1.25f, 0.2f, 0.3f) * 0.04f, mat("Mouth", new Color(0.16f, 0.05f, 0.05f)));
            Color lip = Color.Lerp(L.skin, new Color(0.72f, 0.36f, 0.32f), 0.55f);
            Sphere(b.head, "UpperLip", Local(new Vector3(0, H.mouthY + 0.005f, mz - 0.003f)), new Vector3(1.15f, 0.24f, 0.26f) * 0.042f, mat("Lips", lip));
            Sphere(b.head, "LowerLip", Local(new Vector3(0, H.mouthY - 0.006f, mz - 0.003f)), new Vector3(1.05f, 0.3f, 0.28f) * 0.04f, mat("Lips", lip * 0.93f));
        }

        // Web build's hairShell(): push the head's own vertices out along their normals above a hairline
        // (front / side / back heights), thicker toward the crown, with an optional part, swoop or curls.
        void Hair()
        {
            float front, side, back, thick, top, swoop = 0, bumps = 0; bool part = false, coverEars = false, flat = false;
            flare = 0;
            switch (L.hairStyle)
            {
                case CharacterLook.HairStyle.Buzz: front = H.browY + 0.03f; side = H.eyeY + 0.03f; back = H.hy0 + 0.05f; thick = 0.006f; top = 0.004f; break;
                case CharacterLook.HairStyle.Visor: front = H.browY + 0.03f; side = H.eyeY + 0.03f; back = H.hy0 + 0.05f; thick = 0.008f; top = 0.006f; break;
                case CharacterLook.HairStyle.Curly: front = H.browY + 0.03f; side = H.eyeY + 0.03f; back = H.hy0 + 0.05f; thick = 0.014f; top = 0.01f; bumps = 0.012f; flat = true; break;
                case CharacterLook.HairStyle.Flow:   // Cooper: middle part swept out to the sides, over the ears, down to the nape, a little wave
                    front = H.browY + 0.034f; side = H.eyeY - 0.02f; back = H.hy0 + 0.01f; thick = 0.011f; top = 0.026f; part = true; swoop = 0.1f; bumps = 0.007f; coverEars = true; flare = 0.008f; break;
                default:                             // Swoop
                    front = H.browY + 0.024f; side = H.eyeY + 0.028f; back = H.hy0 + 0.04f; thick = 0.013f; top = 0.024f; part = true; swoop = 0.12f; break;
            }

            var body = HumanBase.Body;
            float zc = (H.hz0 + H.hz1) / 2f, rzz = (H.hz1 - H.hz0) / 2f;
            // a real hair model (fitted from HairAssets), with or without the grown shell under it
            HairAssets.Fit asset = null;
            bool hasAsset = !string.IsNullOrEmpty(L.hairAsset) && HairAssets.All.TryGetValue(L.hairAsset, out asset);
            if (hasAsset)
            {
                var am = HairAssets.Build(L.hairAsset, H.bone, H.hy1);
                if (am)
                {
                    var hairModel = Mesh(am, b.head, "HairModel", Vector3.zero, Vector3.one, HairAssets.Dress(mat("HairModel", L.hair), asset));
                    hairModel.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    b.hairParts.Add(hairModel.GetComponent<Renderer>());
                }
                else hasAsset = false;
            }
            if (!hasAsset || asset.keepShell)
            {
                string key = $"HumanHair_{L.hairStyle}";
                var mesh = Cached(key);
                if (!mesh) mesh = BuildHair(key, front, side, back, thick, top, swoop, bumps, part, coverEars, flat, zc, rzz);
                var hair = Mesh(mesh, b.head, "Hair", Vector3.zero, Vector3.one, mat("Hair", L.hair));
                b.hairParts.Insert(0, hair.GetComponent<Renderer>());
            }
            HatsAndVisor(zc, rzz);
        }

        Mesh BuildHair(string key, float front, float side, float back, float thick, float top, float swoop, float bumps,
                       bool part, bool coverEars, bool flat, float zc, float rzz)
        {
            var body = HumanBase.Body;
            var amt = new Dictionary<int, float>(); var outPos = new Dictionary<int, Vector3>();
            foreach (int v in H.verts)
            {
                Vector3 p = body.v[v], nn = body.n[v];
                float f = Mathf.Clamp((p.z - zc) / rzz, -1f, 1f);
                float line = f > 0 ? side + (front - side) * f * f : side + (back - side) * f * f;
                if (!coverEars && Mathf.Abs(p.x) > H.hx * 0.8f && p.y < H.eyeY + 0.035f && f > -0.55f) continue;   // keep the ears clear
                float a = Mathf.Clamp01((p.y - line) / 0.022f);
                if (a <= 0) continue;
                float th = a * (thick + top * Mathf.Max(0, (p.y - H.hairCy) / (H.hy1 - H.hairCy)));
                if (part && f > -0.2f && Mathf.Abs(p.x) < 0.012f && p.y > H.hairCy) th *= 0.35f + 0.65f * Mathf.Abs(p.x) / 0.012f;
                th += flare * a * (1f - Mathf.Clamp01((p.y - line) / 0.06f));      // ends flip out
                if (bumps > 0) th += a * bumps * (0.5f + 0.5f * Mathf.Sin(p.x * 190f) * Mathf.Sin(p.y * 170f + p.x * 50f) * Mathf.Sin(p.z * 180f));
                amt[v] = a;
                outPos[v] = p + nn * th + new Vector3(swoop > 0 ? p.x * swoop * a : 0, 0, 0);
            }

            var verts = new List<Vector3>(); var tris = new List<int>(); var map = new Dictionary<int, int>();
            foreach (int t in H.tris)
            {
                int a = body.t[t], bb = body.t[t + 1], c = body.t[t + 2];
                if (!amt.ContainsKey(a) || !amt.ContainsKey(bb) || !amt.ContainsKey(c)) continue;
                foreach (int v in new[] { a, bb, c })
                {
                    if (flat) { tris.Add(verts.Count); verts.Add(Local(outPos[v])); continue; }   // curls: faceted
                    if (!map.TryGetValue(v, out int idx)) { idx = verts.Count; map[v] = idx; verts.Add(Local(outPos[v])); }
                    tris.Add(idx);
                }
            }
            var mesh = new Mesh { name = key, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return Persist(key, mesh);
        }

        void HatsAndVisor(float zc, float rzz)
        {
            float topRel = H.hy1 - H.bone.y, zRel = zc - H.bone.z;
            if (L.hairStyle == CharacterLook.HairStyle.Visor)
            {
                float r0 = Mathf.Max(H.hx, rzz) * 1.08f;
                Mesh(MeshKit.Torus(0.14f), b.head, "Band", new Vector3(0, topRel - 0.075f, zRel), new Vector3(H.hx * 2.2f, 0.18f, rzz * 2.24f), mat("Band", L.visorBand));
                var brim = Box(b.head, "Brim", new Vector3(0, topRel - 0.08f, zRel + rzz * 1.12f + r0 * 0.35f), new Vector3(r0 * 1.4f, 0.012f, r0 * 0.9f), mat("Brim", L.visorBrim));
                brim.localRotation = Quaternion.Euler(10f, 0, 0);
            }
            if (L.wearsCap)
            {
                float capR = Mathf.Max(H.hx, rzz) * 1.12f;
                var cm = mat("Cap", L.cap);
                Mesh(MeshKit.HairCap(90, 90, 90), b.head, "Cap", new Vector3(0, topRel - capR * 0.72f, zRel - 0.004f), new Vector3(1.02f, 0.95f, 1.06f) * capR * 2f, cm);
                var brim = Box(b.head, "CapBrim", new Vector3(0, topRel - capR * 0.72f + 0.004f, zRel + capR * 1.28f), new Vector3(capR * 1.5f, 0.012f, capR * 0.95f), cm);
                brim.localRotation = Quaternion.Euler(6f, 0, 0);
                Sphere(b.head, "CapButton", new Vector3(0, topRel - capR * 0.72f + capR * 0.97f, zRel), Vector3.one * 0.018f, cm);
            }
        }

        // Sneakers sized to the foot: white sole, upper in the shoe colour, a toe cap.
        void Shoes(Transform foot, Transform toe, string s)
        {
            var body = HumanBase.Body;
            int fi = HumanBase.Index($"Bone_{s}.004"), ti = HumanBase.Index($"Bone_{s}.005");
            Vector3 fb = HumanBase.Bones[fi].head;
            float x0 = 9, x1 = -9, y1 = -9, z0 = 9, z1 = -9;
            for (int v = 0; v < body.v.Length; v++)
            {
                int bi = body.w[v].boneIndex0; if (bi != fi && bi != ti) continue;
                var p = body.v[v];
                x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); y1 = Mathf.Max(y1, p.y); z0 = Mathf.Min(z0, p.z); z1 = Mathf.Max(z1, p.z);
            }
            if (x0 > x1) return;
            float cx = (x0 + x1) / 2f - fb.x, cz = (z0 + z1) / 2f - fb.z, w = x1 - x0 + 0.02f, len = z1 - z0 + 0.03f, fy = -fb.y;
            var sole = mat("Sole", new Color(0.95f, 0.95f, 0.95f));
            Prim(PrimitiveType.Cylinder, foot, "Sole", new Vector3(cx, fy + 0.014f, cz), new Vector3(w, 0.014f, len), sole);
            Mesh(MeshKit.HairCap(90, 90, 90), foot, "Upper", new Vector3(cx, fy + 0.026f, cz - 0.005f), new Vector3(w * 0.98f, Mathf.Min(0.11f, y1 - 0.02f) * 1.15f, len * 0.98f), mat("Shoes", L.shoes));
            Sphere(foot, "ToeCap", new Vector3(cx, fy + 0.03f, cz + len * 0.33f), new Vector3(w * 0.9f, 0.05f, len * 0.32f), sole);
        }

        void Extras(System.Func<string, Transform> B, string topKey)
        {
            var body = HumanBase.Body;
            var chest = B("Bone.003"); Vector3 chestHead = HumanBase.Bones[HumanBase.Index("Bone.003")].head;
            float fz = -9;                                                  // front of the chest (web build)
            for (int v = 0; v < body.v.Length; v++)
                if (Mathf.Abs(body.v[v].x) < 0.05f && Mathf.Abs(body.v[v].y - 1.36f) < 0.05f) fz = Mathf.Max(fz, body.v[v].z);
            float cloth = topKey == "tee_big" ? 0.026f : 0.012f;

            if (L.shirtGraphic == CharacterLook.ShirtGraphic.BasketballHoop)
            {
                float z = fz + cloth + 0.004f - chestHead.z, gy = 1.33f - chestHead.y;
                var ball = Prim(PrimitiveType.Cylinder, chest, "PrintBall", new Vector3(0, gy + 0.05f, z), new Vector3(0.1f, 0.002f, 0.1f), mat("PrintOrange", CharacterLook.Hex("#e0772a")));
                ball.localRotation = Quaternion.Euler(90f, 0, 0);
                var flame = Box(chest, "PrintFlame", new Vector3(0.03f, gy + 0.09f, z - 0.001f), new Vector3(0.06f, 0.04f, 0.003f), mat("PrintYellow", CharacterLook.Hex("#f2b640")));
                flame.localRotation = Quaternion.Euler(0, 0, 20f);
                Box(chest, "PrintRim", new Vector3(0, gy, z + 0.001f), new Vector3(0.12f, 0.011f, 0.003f), mat("PrintOrange", CharacterLook.Hex("#e0772a")));
                Box(chest, "PrintNet", new Vector3(0, gy - 0.032f, z - 0.001f), new Vector3(0.075f, 0.045f, 0.002f), mat("PrintWhite", CharacterLook.Hex("#e8e8e8")));
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    Box(chest, "PrintPalm", new Vector3(0.085f * sd, gy + 0.01f, z - 0.003f), new Vector3(0.01f, 0.11f, 0.002f), mat("PrintBrown", CharacterLook.Hex("#6b5a3a")));
                    var frond = Box(chest, "PrintFrond", new Vector3(0.085f * sd, gy + 0.07f, z - 0.003f), new Vector3(0.055f, 0.016f, 0.002f), mat("PrintGreen", CharacterLook.Hex("#3f8f48")));
                    frond.localRotation = Quaternion.Euler(0, 0, 15f * sd);
                }
                Box(chest, "PrintWord", new Vector3(0, gy - 0.085f, z - 0.003f), new Vector3(0.18f, 0.026f, 0.002f), mat("PrintGreen", CharacterLook.Hex("#3f7a4a")));
            }
            if (L.workerUniform)
                Box(chest, "NameTag", new Vector3(0.075f, 1.4f - chestHead.y, fz + cloth + 0.006f - chestHead.z), new Vector3(0.05f, 0.022f, 0.004f), mat("Tag", Color.white));
            if (L.wristband)
            {
                var fore = B("Bone.002_L.003"); var fb = HumanBase.Bones[HumanBase.Index("Bone.002_L.003")];
                Vector3 along = (fb.tail - fb.head) * 0.85f;
                Mesh(MeshKit.Torus(0.35f), fore, "Wristband", along, new Vector3(0.075f, 0.05f, 0.075f), mat("Wristband", L.wristbandColor));
            }
        }

        // ---------- primitives ----------

        Transform Sphere(Transform p, string n, Vector3 pos, Vector3 scale, Material m) => Prim(PrimitiveType.Sphere, p, n, pos, scale, m);
        Transform Box(Transform p, string n, Vector3 pos, Vector3 scale, Material m) => Prim(PrimitiveType.Cube, p, n, pos, scale, m);

        static Transform Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go.transform;
        }

        static Transform Mesh(Mesh mesh, Transform parent, string name, Vector3 pos, Vector3 scale, Material m)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = scale;
            return go.transform;
        }
    }
}
