using System.Collections.Generic;
using UnityEngine;

// A stylized, jointed person built from primitives out of a CharacterLook: rounded torso and limbs with knee,
// elbow and shoulder caps, an oval head with jaw, a face (eyes with iris + pupil, lids, brows, nose, lips, ears),
// hair caps that follow a hairline, fists with knuckles and thumbs, sneakers, and clothes from the look.
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
    public Transform legL, legR, kneeL, kneeR, ankleL, ankleR;
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
        var original = parts[0].sharedMaterial;                           // slot 0 is always the skin / hair material
        var m = new Material(original) { color = c };
        tintMats.Add(m);
        foreach (var r in parts)
        {
            if (!r) continue;
            var list = r.sharedMaterials;                                  // bodies have several materials: swap only the skin
            for (int i = 0; i < list.Length; i++) if (list[i] == original) list[i] = m;
            r.sharedMaterials = list;
        }
    }

    void OnDestroy() { foreach (var m in tintMats) Destroy(m); }

    // ---------- building ----------

    public static BlockyCharacter Build(CharacterLook look, Transform parent, MaterialSource mat)
    {
        if (HumanBody.CanBuild(look)) return HumanBody.Build(look, parent, mat);   // realistic body (Cooper, Nathan)
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
        readonly Color lips;
        public Builder(BlockyCharacter b, CharacterLook look, MaterialSource mat)
        {
            this.b = b; L = look; this.mat = mat;
            lips = Color.Lerp(look.skin, new Color(0.72f, 0.33f, 0.32f), 0.4f);
        }

        public void Body(Transform root)
        {
            // hips and legs (sole bottom lands at y = 0)
            b.hips = Joint("Hips", root, new Vector3(0, 0.95f, 0));
            Sphere(b.hips, "Pelvis", new Vector3(0, 0.01f, 0), new Vector3(0.34f, 0.2f, 0.23f), "Pants", L.pants);
            b.legL = Leg(-1, out b.kneeL);
            b.legR = Leg(1, out b.kneeR);
            b.ankleL = b.kneeL.Find("Ankle"); b.ankleR = b.kneeR.Find("Ankle");

            // torso: rounded belly + broader chest, like a real torso tapering to the waist
            b.spine = Joint("Spine", b.hips, new Vector3(0, 0.08f, 0));
            float baggy = L.oversizedShirt ? 1.08f : 1f;
            Capsule(b.spine, "Belly", new Vector3(0, L.oversizedShirt ? 0.1f : 0.12f, 0),
                    new Vector3(0.34f * baggy, L.oversizedShirt ? 0.19f : 0.16f, 0.22f * baggy), "Shirt", L.shirt);
            Sphere(b.spine, "Chest", new Vector3(0, 0.34f, 0), new Vector3(0.47f * baggy, 0.33f, 0.26f * baggy), "Shirt", L.shirt);
            if (L.shirtGraphic == CharacterLook.ShirtGraphic.BasketballHoop) HoopGraphic(0.128f * baggy);
            if (L.workerUniform)
            {
                Mesh(MeshKit.Torus(0.3f), b.spine, "Collar", new Vector3(0, 0.49f, 0.005f), new Vector3(0.2f, 0.12f, 0.2f), "Collar", L.collar);
                Box(b.spine, "NameTag", new Vector3(0.1f, 0.4f, 0.125f), new Vector3(0.06f, 0.028f, 0.01f), "Tag", Color.white);
            }
            else Mesh(MeshKit.Torus(0.35f), b.spine, "Neckline", new Vector3(0, 0.49f, 0.005f), new Vector3(0.16f, 0.08f, 0.16f), "Shirt", L.shirt);

            // neck and head
            b.neck = Joint("Neck", b.spine, new Vector3(0, 0.48f, 0));
            Capsule(b.neck, "NeckMesh", new Vector3(0, 0.04f, 0), new Vector3(0.095f, 0.06f, 0.095f), "Skin", L.skin);
            b.head = Joint("Head", b.neck, new Vector3(0, 0.07f, 0));
            Sphere(b.head, "Skull", new Vector3(0, 0.13f, 0), new Vector3(0.205f, 0.25f, 0.23f), "Skin", L.skin);
            Sphere(b.head, "Jaw", new Vector3(0, 0.055f, 0.018f), new Vector3(0.17f, 0.13f, 0.18f), "Skin", L.skin);
            Face();
            Hair();

            // arms
            b.shoulderL = Arm(-1, out b.elbowL, out _);
            b.shoulderR = Arm(1, out b.elbowR, out b.handR);
        }

        Transform Leg(int side, out Transform knee)
        {
            var hip = Joint(side < 0 ? "LegL" : "LegR", b.hips, new Vector3(0.095f * side, -0.03f, 0));
            if (L.shorts)
            {
                Capsule(hip, "Shorts", new Vector3(0, -0.12f, 0), new Vector3(0.18f, 0.15f, 0.18f), "Pants", L.pants);
                Capsule(hip, "Thigh", new Vector3(0, -0.22f, 0), new Vector3(0.14f, 0.21f, 0.14f), "Skin", L.skin);
            }
            else Capsule(hip, "Thigh", new Vector3(0, -0.2f, 0), new Vector3(0.16f, 0.23f, 0.16f), "Pants", L.pants);

            knee = Joint(side < 0 ? "KneeL" : "KneeR", hip, new Vector3(0, -0.42f, 0));
            string part = L.shorts ? "Skin" : "Pants";
            Color c = L.shorts ? L.skin : L.pants;
            Sphere(knee, "KneeCap", Vector3.zero, Vector3.one * (L.shorts ? 0.12f : 0.135f), part, c);
            Capsule(knee, "Calf", new Vector3(0, -0.14f, -0.01f), new Vector3(L.shorts ? 0.125f : 0.14f, 0.14f, L.shorts ? 0.13f : 0.145f), part, c);
            Capsule(knee, "Shin", new Vector3(0, -0.24f, 0), new Vector3(L.shorts ? 0.1f : 0.125f, 0.15f, L.shorts ? 0.1f : 0.125f), part, c);
            if (L.shorts) Cylinder(knee, "Sock", new Vector3(0, -0.36f, 0), new Vector3(0.105f, 0.045f, 0.105f), "Socks", L.socks);

            // sneaker: rounded upper, white sole, toe cap
            var ankle = Joint("Ankle", knee, new Vector3(0, -0.41f, 0));
            Sphere(ankle, "Shoe", new Vector3(0, -0.03f, 0.045f), new Vector3(0.115f, 0.1f, 0.27f), "Shoes", L.shoes);
            Box(ankle, "Sole", new Vector3(0, -0.068f, 0.045f), new Vector3(0.11f, 0.024f, 0.27f), "Sole", new Color(0.95f, 0.95f, 0.95f));
            Sphere(ankle, "Toe", new Vector3(0, -0.05f, 0.145f), new Vector3(0.1f, 0.055f, 0.08f), "Sole", new Color(0.95f, 0.95f, 0.95f));
            return hip;
        }

        Transform Arm(int side, out Transform elbow, out Transform hand)
        {
            var sh = Joint(side < 0 ? "ShoulderL" : "ShoulderR", b.spine, new Vector3(0.25f * side, 0.42f, 0));
            sh.localRotation = Quaternion.Euler(0, 0, 5f * side);            // arms hang a little out from the body
            Sphere(sh, "Deltoid", Vector3.zero, Vector3.one * 0.15f, "Shirt", L.shirt);
            if (L.longSleeves)
                Capsule(sh, "UpperArm", new Vector3(0, -0.14f, 0), new Vector3(0.115f, 0.16f, 0.115f), "Shirt", L.shirt);
            else
            {
                if (L.oversizedShirt) Capsule(sh, "Sleeve", new Vector3(0, -0.1f, 0), new Vector3(0.16f, 0.14f, 0.16f), "Shirt", L.shirt);   // baggy, near the elbow
                else Capsule(sh, "Sleeve", new Vector3(0, -0.055f, 0), new Vector3(0.14f, 0.1f, 0.14f), "Shirt", L.shirt);
                Capsule(sh, "UpperArm", new Vector3(0, -0.15f, 0), new Vector3(0.1f, 0.15f, 0.1f), "Skin", L.skin);
            }

            elbow = Joint(side < 0 ? "ElbowL" : "ElbowR", sh, new Vector3(0, -0.29f, 0));
            string part = L.longSleeves ? "Shirt" : "Skin";
            Color c = L.longSleeves ? L.shirt : L.skin;
            Sphere(elbow, "ElbowCap", Vector3.zero, Vector3.one * 0.095f, part, c);
            Capsule(elbow, "Forearm", new Vector3(0, -0.13f, 0.005f), new Vector3(0.095f, 0.14f, 0.1f), part, c);
            if (L.wristband && side < 0)
                Cylinder(elbow, "Wristband", new Vector3(0, -0.235f, 0), new Vector3(0.095f, 0.022f, 0.095f), "Wristband", L.wristbandColor);

            // a loose fist: palm, a row of knuckles and a thumb across the front
            hand = Joint(side < 0 ? "HandL" : "HandR", elbow, new Vector3(0, -0.27f, 0));
            Sphere(hand, "Palm", new Vector3(0, -0.045f, 0.005f), new Vector3(0.08f, 0.1f, 0.075f), "Skin", L.skin);
            var knuckles = Capsule(hand, "Knuckles", new Vector3(0, -0.08f, 0.025f), new Vector3(0.035f, 0.038f, 0.04f), "Skin", L.skin);
            knuckles.localRotation = Quaternion.Euler(0, 0, 90f);
            var thumb = Capsule(hand, "Thumb", new Vector3(-0.03f * side, -0.05f, 0.035f), new Vector3(0.026f, 0.032f, 0.026f), "Skin", L.skin);
            thumb.localRotation = Quaternion.Euler(35f, 0, 25f * side);
            return sh;
        }

        void Face()
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float x = 0.042f * side;
                Sphere(b.head, "EyeWhite", new Vector3(x, 0.14f, 0.1f), new Vector3(0.042f, 0.026f, 0.02f), "EyeWhite", new Color(0.96f, 0.95f, 0.93f));
                Sphere(b.head, "Iris", new Vector3(x, 0.139f, 0.108f), new Vector3(0.02f, 0.022f, 0.012f), "Eyes", L.eyes);
                Sphere(b.head, "Pupil", new Vector3(x, 0.139f, 0.113f), new Vector3(0.009f, 0.01f, 0.006f), "Pupil", new Color(0.03f, 0.02f, 0.02f));
                var lid = Sphere(b.head, "Lid", new Vector3(x, 0.148f, 0.101f), new Vector3(0.046f, 0.014f, 0.022f), "Skin", L.skin);
                lid.localRotation = Quaternion.Euler(0, 0, -4f * side);
                var brow = Capsule(b.head, "Brow", new Vector3(0.045f * side, 0.17f, 0.105f), new Vector3(0.012f, 0.026f, 0.012f), "Brows", L.brows);
                brow.localRotation = Quaternion.Euler(0, 0, 90f - 8f * side);
                var ear = Sphere(b.head, "Ear", new Vector3(0.103f * side, 0.125f, -0.005f), new Vector3(0.022f, 0.055f, 0.038f), "Skin", L.skin);
                ear.localRotation = Quaternion.Euler(0, 15f * side, 0);
            }
            var bridge = Capsule(b.head, "NoseBridge", new Vector3(0, 0.12f, 0.11f), new Vector3(0.022f, 0.026f, 0.022f), "Skin", L.skin);
            bridge.localRotation = Quaternion.Euler(-20f, 0, 0);
            Sphere(b.head, "NoseTip", new Vector3(0, 0.098f, 0.12f), new Vector3(0.034f, 0.03f, 0.032f), "Skin", L.skin);
            Sphere(b.head, "UpperLip", new Vector3(0, 0.066f, 0.108f), new Vector3(0.05f, 0.012f, 0.02f), "Lips", lips);
            Sphere(b.head, "LowerLip", new Vector3(0, 0.056f, 0.106f), new Vector3(0.045f, 0.015f, 0.02f), "Lips", lips);
        }

        // Hair is a cap that follows a hairline (see MeshKit.HairCap) sized just over the skull,
        // plus per-style volume pieces. The skull is centered at (0, 0.13, 0), half-size (0.1, 0.125, 0.115).
        void Hair()
        {
            switch (L.hairStyle)
            {
                case CharacterLook.HairStyle.Buzz:
                    Cap(62, 88, 112, new Vector3(0.215f, 0.262f, 0.24f));
                    break;

                case CharacterLook.HairStyle.Swoop:                           // side part swooped up at the front
                    Cap(58, 92, 116, new Vector3(0.225f, 0.275f, 0.25f));
                    var quiff = HairSphere("Quiff", new Vector3(0.02f, 0.245f, 0.075f), new Vector3(0.17f, 0.075f, 0.11f));
                    quiff.localRotation = Quaternion.Euler(-20f, 0, -8f);
                    break;

                case CharacterLook.HairStyle.Flow:                            // Cooper: full, medium-length hair, middle part, over the ears
                    Cap(64, 118, 130, new Vector3(0.24f, 0.29f, 0.262f), -0.008f);
                    HairSphere("Volume", new Vector3(0, 0.235f, -0.012f), new Vector3(0.215f, 0.1f, 0.225f));
                    HairSphere("Nape", new Vector3(0, 0.045f, -0.095f), new Vector3(0.2f, 0.12f, 0.08f));
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var bang = HairSphere("Bang", new Vector3(0.05f * side, 0.212f, 0.098f), new Vector3(0.1f, 0.05f, 0.05f));
                        bang.localRotation = Quaternion.Euler(0, 0, 25f * side);   // parted in the middle, swept out
                        HairSphere("SideFlip", new Vector3(0.105f * side, 0.08f, -0.03f), new Vector3(0.045f, 0.09f, 0.12f));
                    }
                    break;

                case CharacterLook.HairStyle.Curly:                           // Nathan: curls poking out, optional cap
                    var rng = new System.Random(7);
                    Cap(60, 96, 118, new Vector3(0.22f, 0.268f, 0.245f));
                    if (L.wearsCap)
                    {
                        Mesh(MeshKit.HairCap(78, 82, 88), b.head, "Cap", new Vector3(0, 0.145f, -0.004f), new Vector3(0.24f, 0.27f, 0.255f), "Cap", L.cap);
                        var brim = Sphere(b.head, "CapBrim", new Vector3(0, 0.182f, 0.135f), new Vector3(0.19f, 0.018f, 0.13f), "Cap", L.cap);
                        brim.localRotation = Quaternion.Euler(10f, 0, 0);
                        Sphere(b.head, "CapButton", new Vector3(0, 0.279f, -0.004f), Vector3.one * 0.018f, "Cap", L.cap);
                        for (float x = -0.07f; x <= 0.075f; x += 0.035f)       // curls peeking out under the brim
                            HairSphere("Curl", new Vector3(x, 0.165f + (float)rng.NextDouble() * 0.01f, 0.1f), Vector3.one * 0.04f);
                    }
                    else
                        for (int i = 0; i < 26; i++)                          // curls all over the top
                        {
                            float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = (float)rng.NextDouble();
                            var p = new Vector3(Mathf.Cos(a) * r * 0.09f, 0.225f + (1 - r) * 0.035f, Mathf.Sin(a) * r * 0.1f - 0.005f);
                            HairSphere("Curl", p, Vector3.one * (0.045f + (float)rng.NextDouble() * 0.02f));
                        }
                    for (int i = 0; i < 22; i++)                              // around the back and over the ears (show under a cap too)
                    {
                        float a = Mathf.Lerp(-2.3f, 2.3f, i / 21f) + (float)rng.NextDouble() * 0.1f;   // radians round the head, 0 = back
                        float y = 0.12f + (float)rng.NextDouble() * 0.1f;
                        var p = new Vector3(Mathf.Sin(a) * 0.105f, y, -Mathf.Cos(a) * 0.118f);
                        if (Mathf.Abs(a) > 1.3f && y < 0.15f) continue;         // keep the ears clear
                        HairSphere("Curl", p, Vector3.one * (0.042f + (float)rng.NextDouble() * 0.018f));
                    }
                    break;

                case CharacterLook.HairStyle.Visor:                           // worker: short hair under a red visor with a yellow brim
                    Cap(62, 88, 112, new Vector3(0.215f, 0.262f, 0.24f));
                    Mesh(MeshKit.Torus(0.28f), b.head, "Band", new Vector3(0, 0.2f, -0.002f), new Vector3(0.2f, 0.2f, 0.225f), "Band", L.visorBand);
                    var visor = Sphere(b.head, "Brim", new Vector3(0, 0.188f, 0.145f), new Vector3(0.2f, 0.016f, 0.12f), "Brim", L.visorBrim);
                    visor.localRotation = Quaternion.Euler(12f, 0, 0);
                    break;
            }
        }

        void Cap(float front, float side, float back, Vector3 size, float z = -0.004f) =>
            Mesh(MeshKit.HairCap(front, side, back), b.head, "Hair", new Vector3(0, 0.13f, z), size, "Hair", L.hair);

        Transform HairSphere(string name, Vector3 pos, Vector3 size) => Sphere(b.head, name, pos, size, "Hair", L.hair);

        // Front print: flaming basketball over a hoop and net, palm trees either side, a green word banner.
        void HoopGraphic(float front)
        {
            float z = front + 0.002f;
            var ball = Cylinder(b.spine, "PrintBall", new Vector3(0, 0.36f, z), new Vector3(0.1f, 0.003f, 0.1f), "PrintOrange", CharacterLook.Hex("#e0772a"));
            ball.localRotation = Quaternion.Euler(90f, 0, 0);
            var flame = Box(b.spine, "PrintFlame", new Vector3(0.03f, 0.4f, z - 0.002f), new Vector3(0.065f, 0.045f, 0.003f), "PrintYellow", CharacterLook.Hex("#f2b640"));
            flame.localRotation = Quaternion.Euler(0, 0, 20f);
            Box(b.spine, "PrintRim", new Vector3(0, 0.31f, z + 0.002f), new Vector3(0.12f, 0.012f, 0.004f), "PrintOrange", CharacterLook.Hex("#e0772a"));
            Box(b.spine, "PrintNet", new Vector3(0, 0.278f, z - 0.001f), new Vector3(0.08f, 0.05f, 0.003f), "PrintWhite", CharacterLook.Hex("#e8e8e8"));
            for (int side = -1; side <= 1; side += 2)
            {
                Box(b.spine, "PrintPalm", new Vector3(0.085f * side, 0.32f, z - 0.006f), new Vector3(0.011f, 0.12f, 0.003f), "PrintBrown", CharacterLook.Hex("#6b5a3a"));
                var frond = Box(b.spine, "PrintFrond", new Vector3(0.085f * side, 0.385f, z - 0.006f), new Vector3(0.06f, 0.018f, 0.003f), "PrintGreen", CharacterLook.Hex("#3f8f48"));
                frond.localRotation = Quaternion.Euler(0, 0, 15f * side);
            }
            Box(b.spine, "PrintWord", new Vector3(0, 0.225f, z - 0.004f), new Vector3(0.19f, 0.028f, 0.003f), "PrintGreen", CharacterLook.Hex("#3f7a4a"));
        }

        // ---------- primitives ----------

        static Transform Joint(string name, Transform parent, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        Transform Box(Transform p, string n, Vector3 pos, Vector3 size, string part, Color c) => Prim(PrimitiveType.Cube, p, n, pos, size, part, c);
        Transform Sphere(Transform p, string n, Vector3 pos, Vector3 size, string part, Color c) => Prim(PrimitiveType.Sphere, p, n, pos, size, part, c);
        Transform Capsule(Transform p, string n, Vector3 pos, Vector3 scale, string part, Color c) => Prim(PrimitiveType.Capsule, p, n, pos, scale, part, c);
        Transform Cylinder(Transform p, string n, Vector3 pos, Vector3 scale, string part, Color c) => Prim(PrimitiveType.Cylinder, p, n, pos, scale, part, c);

        Transform Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, string part, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            DestroyImmediate(go.GetComponent<Collider>());                   // the character's own capsule is the hitbox
            return Setup(go, parent, name, pos, scale, part, color);
        }

        Transform Mesh(Mesh mesh, Transform parent, string name, Vector3 pos, Vector3 scale, string part, Color color)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            return Setup(go, parent, name, pos, scale, part, color);
        }

        Transform Setup(GameObject go, Transform parent, string name, Vector3 pos, Vector3 scale, string part, Color color)
        {
            go.name = name;
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
