using System.Collections.Generic;
using UnityEngine;

// A blocky, jointed person built from primitives out of a CharacterLook: capsule limbs, box torso and head,
// a face (eyes, brows, nose, mouth), hair per style, and clothes (sleeves, shorts, worker collar and visor).
// Joints sit at the real pivots (hips, knees, shoulders, elbows, neck) so CharacterAnimator can walk and punch.
// Built for a 1.8 m person, then the whole thing is scaled to the look's height.
public class BlockyCharacter : MonoBehaviour
{
    public CharacterLook look;
    [Tooltip("Each copy picks a random skin / hair tone from the look's variants (crowds of workers).")]
    public bool randomizeTones = true;

    [Header("Joints (filled in by Build)")]
    public Transform hips;
    public Transform spine, neck, head;
    public Transform shoulderL, shoulderR, elbowL, elbowR, handR;
    public Transform legL, legR, kneeL, kneeR;
    public List<Renderer> skinParts = new List<Renderer>(), hairParts = new List<Renderer>();

    public delegate Material MaterialSource(string part, Color color);

    readonly List<Material> tintMats = new List<Material>();

    void Awake()
    {
        if (!randomizeTones || !look) return;
        if (look.skinVariants != null && look.skinVariants.Length > 0) Tint(skinParts, look.skinVariants[Random.Range(0, look.skinVariants.Length)]);
        if (look.hairVariants != null && look.hairVariants.Length > 0) Tint(hairParts, look.hairVariants[Random.Range(0, look.hairVariants.Length)]);
    }

    // one material copy per character (keeps the SRP batcher happy, unlike property blocks)
    void Tint(List<Renderer> parts, Color c)
    {
        if (parts.Count == 0 || !parts[0]) return;
        var m = new Material(parts[0].sharedMaterial) { color = c };
        tintMats.Add(m);
        foreach (var r in parts) if (r) r.sharedMaterial = m;
    }

    void OnDestroy() { foreach (var m in tintMats) Destroy(m); }

    // ---------- building ----------

    public static BlockyCharacter Build(CharacterLook look, Transform parent, MaterialSource mat)
    {
        var root = new GameObject("Model").transform;
        root.SetParent(parent, false);
        root.localScale = Vector3.one * (look.height / 1.8f);
        var b = root.gameObject.AddComponent<BlockyCharacter>();
        b.look = look;
        new Builder(b, look, mat).Body(root);
        return b;
    }

    class Builder
    {
        readonly BlockyCharacter b;
        readonly CharacterLook L;
        readonly MaterialSource mat;
        public Builder(BlockyCharacter b, CharacterLook look, MaterialSource mat) { this.b = b; L = look; this.mat = mat; }

        public void Body(Transform root)
        {
            // hips and legs (sole bottom lands at y = 0)
            b.hips = Joint("Hips", root, new Vector3(0, 0.95f, 0));
            Box(b.hips, "Pelvis", Vector3.zero, new Vector3(0.34f, 0.16f, 0.21f), "Pants", L.pants);
            b.legL = Leg(-1, out b.kneeL);
            b.legR = Leg(1, out b.kneeR);

            // torso
            b.spine = Joint("Spine", b.hips, new Vector3(0, 0.08f, 0));
            if (L.oversizedShirt) Box(b.spine, "Torso", new Vector3(0, 0.22f, 0), new Vector3(0.47f, 0.53f, 0.26f), "Shirt", L.shirt);   // hangs past the waist
            else Box(b.spine, "Torso", new Vector3(0, 0.24f, 0), new Vector3(0.44f, 0.48f, 0.24f), "Shirt", L.shirt);
            if (L.shirtGraphic == CharacterLook.ShirtGraphic.BasketballHoop) HoopGraphic(L.oversizedShirt ? 0.13f : 0.12f);
            if (L.workerUniform)
            {
                Box(b.spine, "Collar", new Vector3(0, 0.465f, 0.005f), new Vector3(0.22f, 0.05f, 0.25f), "Collar", L.collar);
                Box(b.spine, "NameTag", new Vector3(0.11f, 0.36f, 0.122f), new Vector3(0.07f, 0.03f, 0.01f), "Tag", Color.white);
            }

            // head and face
            b.neck = Joint("Neck", b.spine, new Vector3(0, 0.48f, 0));
            Cylinder(b.neck, "NeckMesh", new Vector3(0, 0.035f, 0), new Vector3(0.1f, 0.04f, 0.1f), "Skin", L.skin);
            b.head = Joint("Head", b.neck, new Vector3(0, 0.06f, 0));
            Box(b.head, "HeadMesh", new Vector3(0, 0.115f, 0), new Vector3(0.23f, 0.24f, 0.24f), "Skin", L.skin);
            Face();
            Hair();

            // arms
            b.shoulderL = Arm(-1, out b.elbowL, out _);
            b.shoulderR = Arm(1, out b.elbowR, out b.handR);
        }

        Transform Leg(int side, out Transform knee)
        {
            var hip = Joint(side < 0 ? "LegL" : "LegR", b.hips, new Vector3(0.095f * side, -0.04f, 0));
            Capsule(hip, "Thigh", new Vector3(0, -0.21f, 0), new Vector3(0.15f, 0.23f, 0.15f), "Pants", L.pants);
            knee = Joint(side < 0 ? "KneeL" : "KneeR", hip, new Vector3(0, -0.42f, 0));
            if (L.shorts)
            {
                Capsule(knee, "Shin", new Vector3(0, -0.2f, 0), new Vector3(0.12f, 0.21f, 0.12f), "Skin", L.skin);
                Cylinder(knee, "Sock", new Vector3(0, -0.36f, 0), new Vector3(0.125f, 0.04f, 0.125f), "Socks", L.socks);
            }
            else Capsule(knee, "Shin", new Vector3(0, -0.2f, 0), new Vector3(0.13f, 0.22f, 0.13f), "Pants", L.pants);
            var ankle = Joint("Ankle", knee, new Vector3(0, -0.41f, 0));
            Box(ankle, "Shoe", new Vector3(0, -0.035f, 0.045f), new Vector3(0.12f, 0.08f, 0.26f), "Shoes", L.shoes);
            Box(ankle, "Sole", new Vector3(0, -0.07f, 0.045f), new Vector3(0.125f, 0.02f, 0.27f), "Sole", new Color(0.95f, 0.95f, 0.95f));
            return hip;
        }

        Transform Arm(int side, out Transform elbow, out Transform hand)
        {
            var sh = Joint(side < 0 ? "ShoulderL" : "ShoulderR", b.spine, new Vector3(0.28f * side, 0.42f, 0));
            sh.localRotation = Quaternion.Euler(0, 0, 4f * side);            // arms hang a little out from the body
            if (L.longSleeves)
                Capsule(sh, "UpperArm", new Vector3(0, -0.14f, 0), new Vector3(0.12f, 0.16f, 0.12f), "Shirt", L.shirt);
            else
            {
                if (L.oversizedShirt) Box(sh, "Sleeve", new Vector3(0, -0.1f, 0), new Vector3(0.165f, 0.24f, 0.165f), "Shirt", L.shirt);   // baggy, near the elbow
                else Box(sh, "Sleeve", new Vector3(0, -0.06f, 0), new Vector3(0.145f, 0.15f, 0.145f), "Shirt", L.shirt);
                Capsule(sh, "UpperArm", new Vector3(0, -0.15f, 0), new Vector3(0.1f, 0.15f, 0.1f), "Skin", L.skin);
            }
            elbow = Joint(side < 0 ? "ElbowL" : "ElbowR", sh, new Vector3(0, -0.29f, 0));
            Capsule(elbow, "Forearm", new Vector3(0, -0.13f, 0), new Vector3(0.1f, 0.145f, 0.1f),
                    L.longSleeves ? "Shirt" : "Skin", L.longSleeves ? L.shirt : L.skin);
            if (L.wristband && side < 0)
                Cylinder(elbow, "Wristband", new Vector3(0, -0.235f, 0), new Vector3(0.108f, 0.022f, 0.108f), "Wristband", L.wristbandColor);
            hand = Joint(side < 0 ? "HandL" : "HandR", elbow, new Vector3(0, -0.27f, 0));
            Box(hand, "Fist", new Vector3(0, -0.04f, 0), new Vector3(0.09f, 0.1f, 0.095f), "Skin", L.skin);
            return sh;
        }

        void Face()
        {
            float z = 0.12f;                                                  // front of the head
            for (int side = -1; side <= 1; side += 2)
            {
                Box(b.head, "EyeWhite", new Vector3(0.05f * side, 0.13f, z + 0.002f), new Vector3(0.05f, 0.035f, 0.01f), "EyeWhite", new Color(0.96f, 0.95f, 0.93f));
                Box(b.head, "Pupil", new Vector3(0.047f * side, 0.128f, z + 0.007f), new Vector3(0.022f, 0.03f, 0.01f), "Eyes", L.eyes);
                var brow = Box(b.head, "Brow", new Vector3(0.05f * side, 0.168f, z + 0.005f), new Vector3(0.06f, 0.015f, 0.012f), "Brows", L.brows);
                brow.localRotation = Quaternion.Euler(0, 0, -6f * side);
                Box(b.head, "Ear", new Vector3(0.122f * side, 0.11f, -0.005f), new Vector3(0.03f, 0.06f, 0.045f), "Skin", L.skin);
            }
            Box(b.head, "Nose", new Vector3(0, 0.095f, z + 0.015f), new Vector3(0.035f, 0.05f, 0.035f), "Skin", L.skin);
            Box(b.head, "Mouth", new Vector3(0, 0.05f, z + 0.002f), new Vector3(0.08f, 0.015f, 0.01f), "Mouth", new Color(0.35f, 0.12f, 0.11f));
        }

        void Hair()
        {
            switch (L.hairStyle)
            {
                case CharacterLook.HairStyle.Buzz:
                    HairBox("HairTop", new Vector3(0, 0.24f, -0.005f), new Vector3(0.24f, 0.035f, 0.25f));
                    HairBox("HairBack", new Vector3(0, 0.17f, -0.118f), new Vector3(0.24f, 0.14f, 0.025f));
                    break;

                case CharacterLook.HairStyle.Swoop:                           // Cooper: side part swooped up at the front
                    HairBox("HairTop", new Vector3(0, 0.245f, -0.01f), new Vector3(0.25f, 0.06f, 0.25f));
                    HairBox("HairBack", new Vector3(0, 0.165f, -0.118f), new Vector3(0.25f, 0.17f, 0.04f));
                    for (int side = -1; side <= 1; side += 2)
                        HairBox("HairSide", new Vector3(0.121f * side, 0.2f, -0.03f), new Vector3(0.025f, 0.09f, 0.18f));
                    var swoop = HairBox("Swoop", new Vector3(0.02f, 0.265f, 0.1f), new Vector3(0.22f, 0.07f, 0.09f));
                    swoop.localRotation = Quaternion.Euler(-18f, 0, -8f);
                    break;

                case CharacterLook.HairStyle.Flow:                            // Cooper: full, medium-length hair, middle part, over the ears
                    HairBox("HairTop", new Vector3(0, 0.255f, -0.01f), new Vector3(0.265f, 0.085f, 0.265f));
                    HairBox("HairBack", new Vector3(0, 0.13f, -0.122f), new Vector3(0.265f, 0.25f, 0.05f));
                    for (int side = -1; side <= 1; side += 2)
                    {
                        HairBox("HairSide", new Vector3(0.127f * side, 0.16f, -0.025f), new Vector3(0.035f, 0.17f, 0.2f));
                        var bang = HairBox("Bang", new Vector3(0.06f * side, 0.225f, 0.118f), new Vector3(0.12f, 0.065f, 0.05f));
                        bang.localRotation = Quaternion.Euler(-10f, 0, 16f * side);   // parted in the middle, swept out
                        var wing = HairBox("Wing", new Vector3(0.118f * side, 0.2f, 0.085f), new Vector3(0.04f, 0.1f, 0.06f));
                        wing.localRotation = Quaternion.Euler(0, 12f * side, 0);
                    }
                    break;

                case CharacterLook.HairStyle.Curly:                           // Nathan: curls poking out, optional cap
                    var rng = new System.Random(7);
                    if (L.wearsCap)
                    {
                        Box(b.head, "Cap", new Vector3(0, 0.26f, -0.005f), new Vector3(0.26f, 0.09f, 0.26f), "Cap", L.cap);
                        var capBrim = Box(b.head, "CapBrim", new Vector3(0, 0.222f, 0.175f), new Vector3(0.21f, 0.015f, 0.12f), "Cap", L.cap);
                        capBrim.localRotation = Quaternion.Euler(6f, 0, 0);
                        for (float x = -0.075f; x <= 0.08f; x += 0.05f)        // curls peeking out under the brim
                            HairBall(new Vector3(x, 0.205f + (float)rng.NextDouble() * 0.01f, 0.112f), 0.05f);
                        for (int side = -1; side <= 1; side += 2)             // and at the temples
                            HairBall(new Vector3(0.118f * side, 0.2f, 0.06f), 0.055f);
                    }
                    else
                    {
                        HairBox("HairTop", new Vector3(0, 0.24f, 0), new Vector3(0.245f, 0.04f, 0.245f));
                        for (float x = -0.09f; x <= 0.1f; x += 0.06f)
                            for (float zz = -0.09f; zz <= 0.1f; zz += 0.06f)
                                HairBall(new Vector3(x, 0.265f + (float)rng.NextDouble() * 0.015f, zz), 0.075f);
                    }
                    for (float x = -0.1f; x <= 0.11f; x += 0.05f)                // back and sides (show under a cap too)
                        for (float y = 0.12f; y <= 0.23f; y += 0.055f)
                            HairBall(new Vector3(x, y + (float)rng.NextDouble() * 0.01f, -0.12f), 0.065f);
                    for (int side = -1; side <= 1; side += 2)
                        HairBall(new Vector3(0.118f * side, 0.2f, -0.05f), 0.06f);
                    break;

                case CharacterLook.HairStyle.Visor:                           // worker: short hair under a red visor with a yellow brim
                    HairBox("HairTop", new Vector3(0, 0.24f, -0.005f), new Vector3(0.235f, 0.035f, 0.24f));
                    HairBox("HairBack", new Vector3(0, 0.16f, -0.118f), new Vector3(0.235f, 0.13f, 0.025f));
                    Box(b.head, "BandFront", new Vector3(0, 0.21f, 0.127f), new Vector3(0.25f, 0.05f, 0.02f), "Band", L.visorBand);
                    Box(b.head, "BandBack", new Vector3(0, 0.21f, -0.127f), new Vector3(0.25f, 0.05f, 0.02f), "Band", L.visorBand);
                    for (int side = -1; side <= 1; side += 2)
                        Box(b.head, "BandSide", new Vector3(0.125f * side, 0.21f, 0), new Vector3(0.02f, 0.05f, 0.25f), "Band", L.visorBand);
                    var brim = Box(b.head, "Brim", new Vector3(0, 0.19f, 0.18f), new Vector3(0.22f, 0.015f, 0.11f), "Brim", L.visorBrim);
                    brim.localRotation = Quaternion.Euler(10f, 0, 0);
                    break;
            }
        }

        // Front print: flaming basketball over a hoop and net, palm trees either side, a green word banner.
        void HoopGraphic(float front)
        {
            float z = front + 0.002f;
            var ball = Cylinder(b.spine, "PrintBall", new Vector3(0, 0.31f, z), new Vector3(0.11f, 0.003f, 0.11f), "PrintOrange", CharacterLook.Hex("#e0772a"));
            ball.localRotation = Quaternion.Euler(90f, 0, 0);
            var flame = Box(b.spine, "PrintFlame", new Vector3(0.035f, 0.35f, z - 0.0005f), new Vector3(0.07f, 0.05f, 0.003f), "PrintYellow", CharacterLook.Hex("#f2b640"));
            flame.localRotation = Quaternion.Euler(0, 0, 20f);
            Box(b.spine, "PrintRim", new Vector3(0, 0.255f, z + 0.002f), new Vector3(0.13f, 0.012f, 0.004f), "PrintOrange", CharacterLook.Hex("#e0772a"));
            Box(b.spine, "PrintNet", new Vector3(0, 0.22f, z), new Vector3(0.085f, 0.055f, 0.003f), "PrintWhite", CharacterLook.Hex("#e8e8e8"));
            for (int side = -1; side <= 1; side += 2)
            {
                Box(b.spine, "PrintPalm", new Vector3(0.105f * side, 0.26f, z), new Vector3(0.012f, 0.13f, 0.003f), "PrintBrown", CharacterLook.Hex("#6b5a3a"));
                var frond = Box(b.spine, "PrintFrond", new Vector3(0.105f * side, 0.33f, z + 0.001f), new Vector3(0.07f, 0.02f, 0.003f), "PrintGreen", CharacterLook.Hex("#3f8f48"));
                frond.localRotation = Quaternion.Euler(0, 0, 15f * side);
            }
            Box(b.spine, "PrintWord", new Vector3(0, 0.15f, z), new Vector3(0.22f, 0.03f, 0.003f), "PrintGreen", CharacterLook.Hex("#3f7a4a"));
        }

        Transform HairBox(string name, Vector3 pos, Vector3 size) => Box(b.head, name, pos, size, "Hair", L.hair);
        void HairBall(Vector3 pos, float d) => Prim(PrimitiveType.Sphere, b.head, "Curl", pos, Vector3.one * d, "Hair", L.hair);

        // ---------- primitives ----------

        static Transform Joint(string name, Transform parent, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        Transform Box(Transform p, string n, Vector3 pos, Vector3 size, string part, Color c) => Prim(PrimitiveType.Cube, p, n, pos, size, part, c);
        Transform Capsule(Transform p, string n, Vector3 pos, Vector3 scale, string part, Color c) => Prim(PrimitiveType.Capsule, p, n, pos, scale, part, c);
        Transform Cylinder(Transform p, string n, Vector3 pos, Vector3 scale, string part, Color c) => Prim(PrimitiveType.Cylinder, p, n, pos, scale, part, c);

        Transform Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, string part, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            DestroyImmediate(go.GetComponent<Collider>());                   // the character's own capsule is the hitbox
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat(part, color);
            if (part == "Skin") b.skinParts.Add(r);
            else if (part == "Hair") b.hairParts.Add(r);
            return t;
        }
    }

    // Throwaway materials for characters built while playing (one per color).
    public static MaterialSource RuntimeMaterials()
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var cache = new Dictionary<Color, Material>();
        return (part, color) =>
        {
            if (!cache.TryGetValue(color, out var m)) cache[color] = m = new Material(lit) { color = color };
            return m;
        };
    }
}
