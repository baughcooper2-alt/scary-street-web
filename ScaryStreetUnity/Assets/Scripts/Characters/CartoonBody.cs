using UnityEngine;

// Cartoon animal bodies for the DLC characters (CharacterLook.cartoon): a tall blue jay (Mordecai) and a short raccoon
// (Rigby), built from primitives and MeshKit shapes. They have the same joints as the other bodies (hips, spine, neck,
// head, shoulders / elbows / hands, legs / knees / ankles), limbs hanging straight down with no rotation, so
// CharacterAnimator walks and swings them and weapons sit in their hands like anyone's.
// Mordecai and Rigby belong to Cartoon Network (Regular Show): fine for a private build, not for a public release
// (see the licensed-content note at the end of DESIGN.md).
public static class CartoonBody
{
    public static bool CanBuild(CharacterLook look) => look && look.cartoon != CharacterLook.Cartoon.None;

    public static BlockyCharacter Build(CharacterLook look, Transform parent, BlockyCharacter.MaterialSource mat)
    {
        var root = new GameObject("Model").transform;
        root.SetParent(parent, false);
        var b = root.gameObject.AddComponent<BlockyCharacter>();
        b.look = look;
        b.randomizeTones = false;
        var k = new Kit(b, mat, look.displayName);
        float designHeight = look.cartoon == CharacterLook.Cartoon.BlueJay ? 1.95f : 1.25f;
        if (look.cartoon == CharacterLook.Cartoon.BlueJay) BlueJay(k, root); else Raccoon(k, root);
        root.localScale = Vector3.one * (look.height / designHeight);
        return b;
    }

    // ---------- Mordecai: long legs, a box of a body, a big head with a crest, beak and wide eyes ----------

    static readonly Color JayBlue = CharacterLook.Hex("#4f8fd0"), JayDark = CharacterLook.Hex("#3a70ad"), JayPale = CharacterLook.Hex("#cfe2f2"),
        JayMask = CharacterLook.Hex("#b9d3ea"), Beak = CharacterLook.Hex("#3d3d40"), LegGrey = CharacterLook.Hex("#58595c"), LegRing = CharacterLook.Hex("#26282b");

    static void BlueJay(Kit k, Transform root)
    {
        var b = k.b;
        b.hips = k.Joint("Hips", root, new Vector3(0, 0.97f, 0));
        foreach (int s in new[] { -1, 1 })
        {
            var hip = k.Joint(s < 0 ? "LegL" : "LegR", b.hips, new Vector3(0.075f * s, -0.02f, 0));
            SegmentedLeg(k, hip, 0.45f);
            var knee = k.Joint(s < 0 ? "KneeL" : "KneeR", hip, new Vector3(0, -0.46f, 0));
            SegmentedLeg(k, knee, 0.44f);
            var ankle = k.Joint("Ankle", knee, new Vector3(0, -0.46f, 0));
            BirdFoot(k, ankle);
            if (s < 0) { b.legL = hip; b.kneeL = knee; b.ankleL = ankle; } else { b.legR = hip; b.kneeR = knee; b.ankleR = ankle; }
        }
        k.Mesh(MeshKit.Lathe("JayTail", new[] { new Vector2(0.03f, 0), new Vector2(0.045f, 0.06f), new Vector2(0.0f, 0.2f) }, 24), b.hips, "Tail",
               new Vector3(-0.1f, 0.02f, -0.1f), new Vector3(1f, 1f, 0.5f), new Vector3(-120f, 25f, 0), "Blue", JayBlue);

        // body: a tall rounded block, pale front
        b.spine = k.Joint("Spine", b.hips, new Vector3(0, 0.02f, 0));
        k.Rounded(b.spine, "Body", new Vector3(0, 0.27f, 0), new Vector3(0.3f, 0.58f, 0.2f), 0.06f, "Blue", JayBlue);
        k.Rounded(b.spine, "Front", new Vector3(0, 0.27f, 0.075f), new Vector3(0.23f, 0.54f, 0.06f), 0.03f, "Pale", JayPale);
        k.Box(b.spine, "Crease", new Vector3(0.005f, 0.41f, 0.106f), new Vector3(0.004f, 0.12f, 0.002f), "Dark", JayDark);

        b.neck = k.Joint("Neck", b.spine, new Vector3(0, 0.56f, 0));
        k.Prim(PrimitiveType.Cylinder, b.neck, "NeckMesh", new Vector3(0, 0.02f, 0.01f), new Vector3(0.12f, 0.04f, 0.1f), Vector3.zero, "Pale", JayPale);
        b.head = k.Joint("Head", b.neck, new Vector3(0, 0.05f, 0));
        k.Prim(PrimitiveType.Sphere, b.head, "Skull", new Vector3(0, 0.17f, -0.01f), new Vector3(0.29f, 0.32f, 0.28f), Vector3.zero, "Blue", JayBlue);
        k.Prim(PrimitiveType.Sphere, b.head, "Mask", new Vector3(0, 0.13f, 0.06f), new Vector3(0.26f, 0.25f, 0.2f), Vector3.zero, "Mask", JayMask);
        k.Prim(PrimitiveType.Sphere, b.head, "Chin", new Vector3(0, 0.06f, 0.05f), new Vector3(0.2f, 0.14f, 0.17f), Vector3.zero, "Pale", JayPale);
        foreach (int s in new[] { -1, 1 })
        {
            k.Prim(PrimitiveType.Sphere, b.head, "Stripe", new Vector3(0.118f * s, 0.16f, 0.02f), new Vector3(0.035f, 0.12f, 0.08f), new Vector3(0, 0, -18f * s), "Black", new Color(0.07f, 0.08f, 0.1f));
            Eye(k, b.head, new Vector3(0.048f * s, 0.23f, 0.135f), 0.085f, new Vector3(0.012f * s, 0, 0));
        }
        // beak: a flattened wedge, slightly open
        k.Mesh(MeshKit.Lathe("JayBeak", new[] { new Vector2(0.045f, 0), new Vector2(0.04f, 0.05f), new Vector2(0.015f, 0.12f), new Vector2(0.0f, 0.14f) }, 28), b.head, "Beak",
               new Vector3(0, 0.155f, 0.15f), new Vector3(1.3f, 1f, 0.55f), new Vector3(80f, 0, 0), "Beak", Beak);
        // crest: a tall cone leaning back
        k.Mesh(MeshKit.Lathe("JayCrest", new[] { new Vector2(0.11f, 0), new Vector2(0.09f, 0.12f), new Vector2(0.04f, 0.28f), new Vector2(0.0f, 0.36f) }, 32), b.head, "Crest",
               new Vector3(0, 0.25f, -0.05f), new Vector3(0.75f, 1f, 1f), new Vector3(-16f, 0, 0), "Blue", JayBlue);

        // arms: long and thin, two white bands above the wrist, a three-fingered hand
        foreach (int s in new[] { -1, 1 })
        {
            var sh = k.Joint(s < 0 ? "ShoulderL" : "ShoulderR", b.spine, new Vector3(0.17f * s, 0.5f, 0));
            sh.localRotation = Quaternion.Euler(0, 0, 6f * s);
            k.Prim(PrimitiveType.Sphere, sh, "Shoulder", Vector3.zero, Vector3.one * 0.075f, Vector3.zero, "Blue", JayBlue);
            k.Prim(PrimitiveType.Capsule, sh, "UpperArm", new Vector3(0, -0.16f, 0), new Vector3(0.06f, 0.17f, 0.06f), Vector3.zero, "Blue", JayBlue);
            var el = k.Joint(s < 0 ? "ElbowL" : "ElbowR", sh, new Vector3(0, -0.33f, 0));
            k.Prim(PrimitiveType.Capsule, el, "Forearm", new Vector3(0, -0.14f, 0), new Vector3(0.058f, 0.16f, 0.058f), Vector3.zero, "Blue", JayBlue);
            foreach (float y in new[] { -0.19f, -0.235f })
                k.Prim(PrimitiveType.Cylinder, el, "Band", new Vector3(0, y, 0), new Vector3(0.063f, 0.012f, 0.063f), Vector3.zero, "White", Color.white);
            var hand = k.Joint(s < 0 ? "HandL" : "HandR", el, new Vector3(0, -0.29f, 0));
            k.Prim(PrimitiveType.Sphere, hand, "Palm", new Vector3(0, -0.02f, 0), new Vector3(0.06f, 0.07f, 0.045f), Vector3.zero, "Blue", JayBlue);
            for (int f = 0; f < 3; f++)
                k.Prim(PrimitiveType.Capsule, hand, "Finger", new Vector3(0, -0.07f, (f - 1) * 0.018f), new Vector3(0.018f, 0.04f, 0.018f), new Vector3((f - 1) * 12f, 0, 0), "Blue", JayBlue);
            k.Prim(PrimitiveType.Capsule, hand, "Thumb", new Vector3(0, -0.04f, 0.03f), new Vector3(0.018f, 0.03f, 0.018f), new Vector3(40f, 0, 0), "Blue", JayBlue);
            if (s < 0) { b.shoulderL = sh; b.elbowL = el; b.handL = hand; } else { b.shoulderR = sh; b.elbowR = el; b.handR = hand; }
        }
    }

    // a thin grey leg bone with dark rings every few centimetres
    static void SegmentedLeg(Kit k, Transform joint, float length)
    {
        k.Prim(PrimitiveType.Capsule, joint, "Leg", new Vector3(0, -length / 2, 0), new Vector3(0.042f, length / 2 + 0.02f, 0.042f), Vector3.zero, "Leg", LegGrey);
        for (float y = -0.04f; y > -length + 0.02f; y -= 0.055f)
            k.Prim(PrimitiveType.Cylinder, joint, "Ring", new Vector3(0, y, 0), new Vector3(0.046f, 0.004f, 0.046f), Vector3.zero, "LegRing", LegRing);
    }

    // three long toes forward and one back
    static void BirdFoot(Kit k, Transform ankle)
    {
        k.Prim(PrimitiveType.Sphere, ankle, "Heel", new Vector3(0, -0.01f, 0), Vector3.one * 0.05f, Vector3.zero, "Leg", LegGrey);
        for (int t = -1; t <= 1; t++)
            k.Prim(PrimitiveType.Capsule, ankle, "Toe", new Vector3(t * 0.04f, -0.018f, 0.075f), new Vector3(0.022f, 0.08f, 0.022f), new Vector3(90f, t * 22f, 0), "Leg", LegGrey);
        k.Prim(PrimitiveType.Capsule, ankle, "BackToe", new Vector3(0, -0.018f, -0.05f), new Vector3(0.02f, 0.05f, 0.02f), new Vector3(90f, 0, 0), "Leg", LegGrey);
    }

    // ---------- Rigby: short and round, a masked face, big ears, a ringed tail ----------

    static readonly Color Fur = CharacterLook.Hex("#8a5a38"), FurLight = CharacterLook.Hex("#c49a6c"), FurDark = CharacterLook.Hex("#4a2f1e"), MaskBlack = CharacterLook.Hex("#1c1714");

    static void Raccoon(Kit k, Transform root)
    {
        var b = k.b;
        b.hips = k.Joint("Hips", root, new Vector3(0, 0.44f, 0));
        foreach (int s in new[] { -1, 1 })
        {
            var hip = k.Joint(s < 0 ? "LegL" : "LegR", b.hips, new Vector3(0.085f * s, -0.02f, 0));
            k.Prim(PrimitiveType.Capsule, hip, "Thigh", new Vector3(0, -0.1f, 0), new Vector3(0.09f, 0.12f, 0.09f), Vector3.zero, "Fur", Fur);
            var knee = k.Joint(s < 0 ? "KneeL" : "KneeR", hip, new Vector3(0, -0.2f, 0));
            k.Prim(PrimitiveType.Capsule, knee, "Shin", new Vector3(0, -0.09f, 0), new Vector3(0.075f, 0.11f, 0.075f), Vector3.zero, "Fur", Fur);
            var ankle = k.Joint("Ankle", knee, new Vector3(0, -0.2f, 0));
            k.Prim(PrimitiveType.Sphere, ankle, "Foot", new Vector3(0, -0.01f, 0.045f), new Vector3(0.09f, 0.05f, 0.15f), Vector3.zero, "Black", MaskBlack);
            if (s < 0) { b.legL = hip; b.kneeL = knee; b.ankleL = ankle; } else { b.legR = hip; b.kneeR = knee; b.ankleR = ankle; }
        }
        // ringed tail, curling up behind
        Transform seg = k.Joint("Tail", b.hips, new Vector3(0, 0.02f, -0.14f));
        seg.localRotation = Quaternion.Euler(-60f, 0, 0);
        for (int i = 0; i < 6; i++)
        {
            float r = 0.075f - i * 0.006f;
            k.Prim(PrimitiveType.Capsule, seg, "TailSeg", new Vector3(0, 0.05f, 0), new Vector3(r, 0.055f, r), Vector3.zero, i % 2 == 0 ? "Fur" : "FurDark", i % 2 == 0 ? Fur : FurDark);
            var next = k.Joint("TailJoint", seg, new Vector3(0, 0.085f, 0));
            next.localRotation = Quaternion.Euler(-12f, 0, 0);
            seg = next;
        }

        b.spine = k.Joint("Spine", b.hips, new Vector3(0, 0.02f, 0));
        k.Prim(PrimitiveType.Sphere, b.spine, "Body", new Vector3(0, 0.17f, 0), new Vector3(0.34f, 0.42f, 0.28f), Vector3.zero, "Fur", Fur);
        k.Prim(PrimitiveType.Sphere, b.spine, "Belly", new Vector3(0, 0.14f, 0.07f), new Vector3(0.24f, 0.3f, 0.16f), Vector3.zero, "FurLight", FurLight);

        b.neck = k.Joint("Neck", b.spine, new Vector3(0, 0.36f, 0));
        b.head = k.Joint("Head", b.neck, new Vector3(0, 0.02f, 0));
        k.Prim(PrimitiveType.Sphere, b.head, "Skull", new Vector3(0, 0.15f, 0), new Vector3(0.36f, 0.31f, 0.3f), Vector3.zero, "Fur", Fur);
        k.Prim(PrimitiveType.Sphere, b.head, "Mask", new Vector3(0, 0.17f, 0.075f), new Vector3(0.33f, 0.1f, 0.16f), Vector3.zero, "Black", MaskBlack);
        k.Prim(PrimitiveType.Sphere, b.head, "Muzzle", new Vector3(0, 0.09f, 0.12f), new Vector3(0.15f, 0.09f, 0.1f), Vector3.zero, "FurLight", FurLight);
        k.Prim(PrimitiveType.Sphere, b.head, "Nose", new Vector3(0, 0.11f, 0.175f), new Vector3(0.04f, 0.03f, 0.03f), Vector3.zero, "Black", MaskBlack);
        foreach (int s in new[] { -1, 1 })
        {
            Eye(k, b.head, new Vector3(0.05f * s, 0.18f, 0.135f), 0.07f, new Vector3(-0.004f * s, 0, 0));
            k.Prim(PrimitiveType.Sphere, b.head, "Ear", new Vector3(0.11f * s, 0.3f, -0.01f), new Vector3(0.09f, 0.1f, 0.035f), new Vector3(0, 0, -20f * s), "Fur", Fur);
            k.Prim(PrimitiveType.Sphere, b.head, "EarInside", new Vector3(0.11f * s, 0.3f, 0.006f), new Vector3(0.055f, 0.065f, 0.02f), new Vector3(0, 0, -20f * s), "FurDark", FurDark);
        }

        foreach (int s in new[] { -1, 1 })
        {
            var sh = k.Joint(s < 0 ? "ShoulderL" : "ShoulderR", b.spine, new Vector3(0.14f * s, 0.3f, 0));
            sh.localRotation = Quaternion.Euler(0, 0, 8f * s);
            k.Prim(PrimitiveType.Capsule, sh, "UpperArm", new Vector3(0, -0.08f, 0), new Vector3(0.06f, 0.09f, 0.06f), Vector3.zero, "Fur", Fur);
            var el = k.Joint(s < 0 ? "ElbowL" : "ElbowR", sh, new Vector3(0, -0.16f, 0));
            k.Prim(PrimitiveType.Capsule, el, "Forearm", new Vector3(0, -0.07f, 0), new Vector3(0.055f, 0.08f, 0.055f), Vector3.zero, "Fur", Fur);
            var hand = k.Joint(s < 0 ? "HandL" : "HandR", el, new Vector3(0, -0.15f, 0));
            k.Prim(PrimitiveType.Sphere, hand, "Paw", new Vector3(0, -0.02f, 0), new Vector3(0.06f, 0.065f, 0.05f), Vector3.zero, "Black", MaskBlack);
            if (s < 0) { b.shoulderL = sh; b.elbowL = el; b.handL = hand; } else { b.shoulderR = sh; b.elbowR = el; b.handR = hand; }
        }
    }

    // a round white eye with a black pupil looking ahead (plus a lid child so CharacterAnimator can blink it)
    static void Eye(Kit k, Transform head, Vector3 at, float size, Vector3 look)
    {
        k.Prim(PrimitiveType.Sphere, head, "EyeWhite", at, new Vector3(size, size * 1.1f, size * 0.8f), Vector3.zero, "EyeWhite", Color.white);
        k.Prim(PrimitiveType.Sphere, head, "Pupil", at + new Vector3(0, -0.004f, size * 0.38f) + look, Vector3.one * size * 0.22f, Vector3.zero, "Pupil", new Color(0.05f, 0.05f, 0.05f));
    }

    // Builds parts with the character's materials (saved per part by the editor, throwaway at runtime).
    class Kit
    {
        public readonly BlockyCharacter b;
        readonly BlockyCharacter.MaterialSource mat;
        readonly string who;
        public Kit(BlockyCharacter b, BlockyCharacter.MaterialSource mat, string who) { this.b = b; this.mat = mat; this.who = who; }

        public Transform Joint(string name, Transform parent, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        public Transform Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Vector3 euler, string part, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return Setup(go, parent, name, pos, scale, euler, part, color);
        }

        public Transform Box(Transform parent, string name, Vector3 pos, Vector3 size, string part, Color color)
            => Prim(PrimitiveType.Cube, parent, name, pos, size, Vector3.zero, part, color);

        public Transform Rounded(Transform parent, string name, Vector3 pos, Vector3 size, float radius, string part, Color color)
            => Mesh(MeshKit.RoundedBox(size, radius), parent, name, pos, Vector3.one, Vector3.zero, part, color);

        public Transform Mesh(Mesh mesh, Transform parent, string name, Vector3 pos, Vector3 scale, Vector3 euler, string part, Color color)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            return Setup(go, parent, name, pos, scale, euler, part, color);
        }

        Transform Setup(GameObject go, Transform parent, string name, Vector3 pos, Vector3 scale, Vector3 euler, string part, Color color)
        {
            go.name = name;
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = pos; t.localScale = scale; t.localRotation = Quaternion.Euler(euler);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat(who + "_" + part, color);
            if (part == "Blue" || part == "Fur") b.skinParts.Add(r);
            return t;
        }
    }
}
