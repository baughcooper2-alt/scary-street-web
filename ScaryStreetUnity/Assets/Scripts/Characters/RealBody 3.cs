using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Cooper and Nathan's realistic bodies: a Character Creator base body slimmed to their build, dressed in fitted
// Marvelous Designer clothes, with real hair, a cap / wristband and sneakers. Built offline in Blender by
// tools/real_bodies (see CLAUDE.md) and saved as Resources/RealBody/<Name>.bytes (format "SSRB"),
// already in Unity coordinates with the arms hanging down and every bone unrotated at rest, the same convention
// as the blocky rig, so CharacterAnimator drives it unchanged. Textures live in Resources/RealBody/Tex.
public static class RealBody
{
    class Bone { public string name; public int parent; public Vector3 head; }
    class Part { public string name; public string[] mats; public Mesh mesh; }
    class Data { public float height; public int size; public Bone[] bones; public List<Part> parts = new List<Part>(); }

    static readonly Dictionary<string, Data> cache = new Dictionary<string, Data>();

    static string ModelName(CharacterLook look) => look ? (string.IsNullOrEmpty(look.realModel) ? look.displayName : look.realModel) : null;

    public static bool CanBuild(CharacterLook look) => look && Load(ModelName(look)) != null;

    // Cached per character, but re-read whenever the .bytes file changes (domain reload is off, so statics outlive
    // Play sessions) and never shared with editor prefab builds, whose meshes are saved assets.
    static Data Load(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        var asset = Resources.Load<TextAsset>("RealBody/" + name);
        if (!asset) return null;
        var bytes = asset.bytes;
        bool editorBuild = MeshKit.Persist != null;
        if (!editorBuild && cache.TryGetValue(name, out var d) && d != null && d.size == bytes.Length && d.parts.TrueForAll(p => p.mesh)) return d;
        try { d = Read(bytes, name); }
        catch (System.Exception e) { Debug.LogWarning($"RealBody {name}: couldn't read ({e.Message})"); return null; }
        if (d != null) d.size = bytes.Length;
        if (!editorBuild) cache[name] = d;
        return d;
    }

    static Data Read(byte[] bytes, string name)
    {
        using (var r = new BinaryReader(new MemoryStream(bytes)))
        {
            if (new string(r.ReadChars(4)) != "SSRB" || r.ReadInt32() != 1) return null;
            var d = new Data { height = r.ReadSingle() };
            d.bones = new Bone[r.ReadInt32()];
            for (int i = 0; i < d.bones.Length; i++) d.bones[i] = new Bone { name = Str(r), parent = r.ReadInt32(), head = V3(r) };
            var bind = new Matrix4x4[d.bones.Length];
            for (int i = 0; i < bind.Length; i++) bind[i] = Matrix4x4.Translate(-d.bones[i].head);

            int parts = r.ReadInt32();
            for (int p = 0; p < parts; p++)
            {
                var part = new Part { name = Str(r) };
                part.mats = new string[r.ReadInt32()];
                for (int m = 0; m < part.mats.Length; m++) part.mats[m] = Str(r);
                int n = r.ReadInt32();
                var pos = new Vector3[n]; var nor = new Vector3[n]; var uv = new Vector2[n]; var bw = new BoneWeight[n];
                for (int i = 0; i < n; i++) pos[i] = V3(r);
                for (int i = 0; i < n; i++) nor[i] = V3(r);
                for (int i = 0; i < n; i++) uv[i] = new Vector2(r.ReadSingle(), r.ReadSingle());
                for (int i = 0; i < n; i++)
                    bw[i] = new BoneWeight
                    {
                        boneIndex0 = r.ReadInt32(), weight0 = r.ReadSingle(), boneIndex1 = r.ReadInt32(), weight1 = r.ReadSingle(),
                        boneIndex2 = r.ReadInt32(), weight2 = r.ReadSingle(), boneIndex3 = r.ReadInt32(), weight3 = r.ReadSingle(),
                    };
                var mesh = new Mesh { name = $"Real_{name}_{part.name}_{bytes.Length}", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };   // size in the name: new data, new saved mesh
                mesh.vertices = pos; mesh.normals = nor; mesh.uv = uv; mesh.boneWeights = bw; mesh.bindposes = bind;
                int subs = r.ReadInt32();
                mesh.subMeshCount = subs;
                for (int s = 0; s < subs; s++)
                {
                    var idx = new int[r.ReadInt32()];
                    for (int i = 0; i < idx.Length; i++) idx[i] = r.ReadInt32();
                    if (part.mats[s] == "Std_Eyelash") idx = new int[0];            // no usable lash opacity in the export
                    mesh.SetTriangles(idx, s);
                }
                int shapes = r.ReadInt32();
                for (int s = 0; s < shapes; s++)
                {
                    string sn = Str(r); var delta = new Vector3[n];
                    for (int i = 0; i < n; i++) delta[i] = V3(r);
                    mesh.AddBlendShapeFrame(sn, 100f, delta, null, null);
                }
                mesh.RecalculateTangents();                                          // normal maps
                mesh.RecalculateBounds();
                part.mesh = MeshKit.Persist != null ? MeshKit.Persist(mesh.name, mesh) : mesh;
                d.parts.Add(part);
            }
            return d;
        }
    }

    static string Str(BinaryReader r) => System.Text.Encoding.UTF8.GetString(r.ReadBytes(r.ReadInt32()));
    static Vector3 V3(BinaryReader r) => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

    // ---------- building ----------

    public static BlockyCharacter Build(CharacterLook look, Transform parent, BlockyCharacter.MaterialSource mat)
    {
        var d = Load(ModelName(look));
        var root = new GameObject("Model").transform;
        root.SetParent(parent, false);
        root.localScale = Vector3.one * (look.height / d.height);
        var b = root.gameObject.AddComponent<BlockyCharacter>();
        b.look = look;
        b.randomizeTones = false;

        var t = new Transform[d.bones.Length];
        for (int i = 0; i < t.Length; i++)
        {
            var bone = d.bones[i];
            t[i] = new GameObject(bone.name).transform;
            t[i].SetParent(bone.parent >= 0 ? t[bone.parent] : root, false);
            t[i].localPosition = bone.head - (bone.parent >= 0 ? d.bones[bone.parent].head : Vector3.zero);
        }
        Transform B(string n) { for (int i = 0; i < d.bones.Length; i++) if (d.bones[i].name == "CC_Base_" + n) return t[i]; return null; }
        b.hips = B("Hip"); b.spine = B("Waist"); b.neck = B("NeckTwist01"); b.head = B("Head");
        b.shoulderL = B("L_Upperarm"); b.elbowL = B("L_Forearm");
        b.shoulderR = B("R_Upperarm"); b.elbowR = B("R_Forearm"); b.handR = B("R_Hand");
        b.legL = B("L_Thigh"); b.kneeL = B("L_Calf"); b.ankleL = B("L_Foot");
        b.legR = B("R_Thigh"); b.kneeR = B("R_Calf"); b.ankleR = B("R_Foot");

        string model = ModelName(look);
        foreach (var part in d.parts)
        {
            if (part.name.StartsWith("FP_")) continue;                               // first-person arms: see FirstPersonArm
            if (part.name == "Cap" && !look.wearsCap) continue;
            if (part.name == "Wristband" && !look.wristband) continue;
            var go = new GameObject(part.name);
            go.transform.SetParent(root, false);
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = part.mesh;
            smr.bones = t;
            smr.rootBone = b.hips;
            smr.localBounds = new Bounds(new Vector3(0, -0.1f, 0), new Vector3(1.4f, 2.2f, 1f));   // around the hips
            var mats = new Material[part.mats.Length];
            for (int m = 0; m < mats.Length; m++) mats[m] = Dress(part.mats[m], look, mat, model);
            smr.sharedMaterials = mats;
            if (part.name == "CC_Base_Body") b.skinParts.Add(smr);
            if (part.name == "Hair") b.hairParts.Add(smr);
        }
        return b;
    }

    // This character's own forearm and fist for the first-person view (posed in Blender with the fingers curled,
    // centred on the fist, forward = +Z). side: 1 = right, -1 = left. Null if the character has none.
    public static Transform FirstPersonArm(CharacterLook look, int side, Transform parent, BlockyCharacter.MaterialSource mat)
    {
        string model = ModelName(look);
        var d = Load(model);
        var part = d?.parts.Find(p => p.name == (side > 0 ? "FP_R" : "FP_L"));
        if (part == null) return null;
        var go = new GameObject(part.name);
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * (look.height / d.height);
        go.AddComponent<MeshFilter>().sharedMesh = part.mesh;
        var mr = go.AddComponent<MeshRenderer>();
        var mats = new Material[part.mats.Length];
        for (int m = 0; m < mats.Length; m++) mats[m] = Dress(part.mats[m], look, mat, model);
        mr.sharedMaterials = mats;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    // Material for one slot: the skin / eye textures from the base body tinted toward the look, the fabric normal map
    // on the clothes, and flat look colours for hair, cap, shoes and the wristband.
    static Material Dress(string slot, CharacterLook L, BlockyCharacter.MaterialSource mat, string model)
    {
        // a character can have its own face texture (eyebrows): Skin_Head_<Model>_D
        string face = Resources.Load<Texture2D>($"RealBody/Tex/Skin_Head_{model}_D") ? $"Skin_Head_{model}" : "Skin_Head";
        Color skinTint = Color.Lerp(Color.white, Div(L.skin, new Color(0.76f, 0.55f, 0.45f)), 0.5f);   // texture average → the look
        switch (slot)
        {
            case "Std_Skin_Head": return Textured(mat("Real_SkinHead_" + model, skinTint), face, 0.35f, "Skin_Head");
            case "Std_Skin_Body": return Textured(mat("Real_SkinBody", skinTint), "Skin_Body", 0.32f);
            case "Std_Skin_Arm":  return Textured(mat("Real_SkinArm", skinTint), "Skin_Arm", 0.32f);
            case "Std_Skin_Leg":  return Textured(mat("Real_SkinLeg", skinTint), "Skin_Leg", 0.32f);
            case "Std_Nails":     return Textured(mat("Real_Nails", skinTint), "Nails", 0.5f);
            case "Ga_Eye":        return Textured(mat("Real_Eye", Color.white), "Eye_Brown", 0.9f);
            case "Ga_Teeth":      return Textured(mat("Real_Teeth", Color.white), "Teeth", 0.6f);
            case "Shirt":         return TwoSided(Textured(mat("Real_Shirt", L.shirt), null, 0.08f, "Fabric", 0.8f));   // the outfit's own fabric normals
            case "Pants":         return TwoSided(Textured(mat("Real_Pants", L.pants), null, 0.1f, "Fabric", 0.8f));
            case "Shoes":         return TwoSided(Smooth(mat("Real_Shoes", L.shoes), 0.35f));
            case "Hair":          return HairMat(mat("Real_Hair", L.hair), L);
            case "Curls":         return TwoSided(Smooth(mat("Real_Curls", L.hair), 0.3f));
            case "Cap":           return TwoSided(Smooth(mat("Real_Cap", L.cap), 0.2f));
            case "Wristband":     return Smooth(mat("Real_Wristband", L.wristbandColor), 0.15f);
            default:              return mat("Real_" + slot, Color.gray);
        }
    }

    static Color Div(Color a, Color b) => new Color(a.r / b.r, a.g / b.g, a.b / b.b, 1f);

    static Material Smooth(Material m, float s) { m.SetFloat("_Smoothness", s); return m; }
    static Material TwoSided(Material m) { m.SetFloat("_Cull", 0f); return m; }             // cloth is a single sheet

    static Material Textured(Material m, string tex, float smoothness, string normal = null, float bump = 1f)
    {
        var d = tex != null ? Resources.Load<Texture2D>("RealBody/Tex/" + tex + "_D") : null;
        var n = Resources.Load<Texture2D>("RealBody/Tex/" + (normal ?? tex) + "_N");
        if (d) { m.mainTexture = d; m.SetTexture("_BaseMap", d); }
        if (n)
        {
            m.SetTexture("_BumpMap", n); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", bump);
        }
        m.SetFloat("_Smoothness", smoothness);
        return m;
    }

    // Nathan's curls use the strand texture (alpha clipped, both sides); Cooper's sculpted hair is a solid colour.
    static Material HairMat(Material m, CharacterLook L)
    {
        m.SetFloat("_Smoothness", 0.35f);
        m.SetFloat("_Cull", 0f);
        if (L.hairAsset == "nathan_curls" && HairAssets.All.TryGetValue(L.hairAsset, out var fit)) HairAssets.Dress(m, fit);
        return m;
    }
}
