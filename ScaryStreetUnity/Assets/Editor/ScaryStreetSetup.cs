using Unity.AI.Navigation;
using Unity.AI.Navigation.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-click setup steps under Tools > Scary Street.
public static class ScaryStreetSetup
{
    // ---------- Player ----------

    // Tags the player, gives it Health (25 HP), fists, money/XP, the test HUD and first-person arms (as Cooper),
    // and removes the stray FirstPersonController + CharacterController that ended up on the Main Camera.
    // Safe to run again: it only adds what's missing.
    [MenuItem("Tools/Scary Street/Set Up Player")]
    static void SetUpPlayer()
    {
        FirstPersonController player = null;
        int removed = 0;
        foreach (var fpc in Object.FindObjectsByType<FirstPersonController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            bool strayOnCamera = fpc.GetComponent<Camera>() && fpc.transform.parent &&
                                 fpc.transform.parent.GetComponentInParent<FirstPersonController>();
            if (!strayOnCamera) { player = fpc; continue; }
            var cc = fpc.GetComponent<CharacterController>();
            Undo.DestroyObjectImmediate(fpc);             // controller first: it RequireComponents the CharacterController
            if (cc) Undo.DestroyObjectImmediate(cc);
            removed++;
        }

        if (!player)
        {
            EditorUtility.DisplayDialog("Scary Street", "No Player with a FirstPersonController found in the open scene.", "OK");
            return;
        }

        var go = player.gameObject;
        Undo.RecordObject(go, "Set Up Player");
        go.tag = "Player";
        var health = go.GetComponent<Health>() ? go.GetComponent<Health>() : Undo.AddComponent<Health>(go);
        Undo.RecordObject(health, "Set Up Player");
        health.maxHealth = 25f;
        if (!go.GetComponent<PlayerPunch>()) Undo.AddComponent<PlayerPunch>(go);
        if (!go.GetComponent<PlayerProgress>()) Undo.AddComponent<PlayerProgress>(go);
        if (!go.GetComponent<PlayerHUD>()) Undo.AddComponent<PlayerHUD>(go);
        var inv = go.GetComponent<WeaponInventory>() ? go.GetComponent<WeaponInventory>() : Undo.AddComponent<WeaponInventory>(go);
        Undo.RecordObject(inv, "Set Up Player");
        inv.smokeMaterial = SmokeMaterial();
        if (!go.GetComponent<PlayerInteract>()) Undo.AddComponent<PlayerInteract>(go);
        if (!go.GetComponent<ThirdPersonView>()) Undo.AddComponent<ThirdPersonView>(go).look = Look("Cooper", "cooper");

        var cam = go.GetComponentInChildren<Camera>();
        if (cam && !cam.GetComponent<FirstPersonArms>())
        {
            var arms = Undo.AddComponent<FirstPersonArms>(cam.gameObject);
            arms.look = Look("Cooper", "cooper");
        }

        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeGameObject = go;
        EditorUtility.DisplayDialog("Scary Street",
            $"Player set up: tag Player, Health 25, PlayerPunch, PlayerProgress, PlayerHUD, WeaponInventory (cart in slot 1), PlayerInteract (doors), FirstPersonArms + ThirdPersonView (Cooper)." +
            (removed > 0 ? $"\nRemoved {removed} extra controller(s) from the camera." : ""), "OK");
    }

    // ---------- NavMesh ----------

    // Bakes the NavMesh on the world model (scary-street-world), whatever is selected. Sizes the Humanoid
    // agent to the workers so they fit through doorways, builds from the MeshColliders, and removes any
    // NavMeshSurface that ended up on another object (plus its baked file) so only the house bake is used.
    [MenuItem("Tools/Scary Street/Bake NavMesh")]
    static void BakeNavMesh()
    {
        var root = FindWorld();
        if (!root) { EditorUtility.DisplayDialog("Scary Street", "Couldn't find scary-street-world in the open scene.", "OK"); return; }
        SetHumanoidAgentSize(radius: 0.3f, height: 1.8f, climb: 0.4f);

        int removed = 0;
        foreach (var stray in Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (stray.gameObject == root) continue;
            string asset = stray.navMeshData ? AssetDatabase.GetAssetPath(stray.navMeshData) : null;
            Undo.DestroyObjectImmediate(stray);
            if (!string.IsNullOrEmpty(asset)) AssetDatabase.DeleteAsset(asset);
            removed++;
        }

        var surface = root.GetComponent<NavMeshSurface>();
        if (!surface) surface = Undo.AddComponent<NavMeshSurface>(root);
        Undo.RecordObject(surface, "Bake NavMesh");
        surface.agentTypeID = 0;                                           // Humanoid
        surface.collectObjects = CollectObjects.Children;                  // just the world, not the player or the Doors copies
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;

        NavMeshAssetManager.instance.StartBakingSurfaces(new Object[] { surface });
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log("Scary Street: baking the NavMesh on scary-street-world (progress in the bottom-right)." +
                  (removed > 0 ? $" Removed {removed} stray NavMesh Surface(s)." : "") + " Save the scene when it finishes.");
    }

    static GameObject FindWorld()
    {
        var world = GameObject.Find("scary-street-world");
        if (world) return world;
        foreach (var s in Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None))
            if (!s.GetComponentInChildren<Door>()) return s.gameObject;           // an earlier bake on the (renamed) world
        return null;
    }

    static void SetHumanoidAgentSize(float radius, float height, float climb)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset");
        if (assets == null || assets.Length == 0) return;
        var so = new SerializedObject(assets[0]);
        var settings = so.FindProperty("m_Settings");
        if (settings == null || settings.arraySize == 0) return;
        var humanoid = settings.GetArrayElementAtIndex(0);
        humanoid.FindPropertyRelative("agentRadius").floatValue = radius;
        humanoid.FindPropertyRelative("agentHeight").floatValue = height;
        humanoid.FindPropertyRelative("agentClimb").floatValue = climb;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- Enemies ----------

    // Rebuilds the worker prefab (same file, so the RoundManager and scene keep pointing at it)
    // with the blocky body from Assets/Characters/McDonaldsWorker.asset.
    [MenuItem("Tools/Scary Street/Create McDonald's Worker Prefab")]
    static void CreateWorkerPrefabMenu()
    {
        var prefab = CreateWorkerPrefab();
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
    }

    static GameObject CreateWorkerPrefab()
    {
        EnsureFolder("Assets", "Prefabs");
        var look = Look("McDonaldsWorker", "worker");

        var go = new GameObject("McDonaldsWorker");
        var worker = go.AddComponent<McDonaldsWorker>();   // pulls in NavMeshAgent, Health, CapsuleCollider, WorldHealthBar, LootDrop
        worker.ApplyDefaults();
        var body = BuildSaved(look, go.transform);
        body.gameObject.AddComponent<CharacterAnimator>();

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, WorkerPrefabPath);
        Object.DestroyImmediate(go);
        Debug.Log($"Scary Street: saved {WorkerPrefabPath}.");
        return prefab;
    }

    // ---------- Characters ----------

    // Creates the look assets (if missing) for Cooper, Nathan and the McDonald's worker, then builds
    // Assets/Prefabs/Characters/Cooper.prefab and Nathan.prefab and rebuilds the worker prefab.
    // Edit a look's colors / hair in the Inspector, then run this again to rebuild.
    [MenuItem("Tools/Scary Street/Create Character Prefabs")]
    static void CreateCharacterPrefabs()
    {
        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets/Prefabs", "Characters");
        foreach (var (file, preset) in new[] { ("Cooper", "cooper"), ("Nathan", "nathan") })
        {
            var look = Look(file, preset);
            var go = new GameObject(file);
            var body = BuildSaved(look, go.transform);
            body.gameObject.AddComponent<CharacterAnimator>();
            PrefabUtility.SaveAsPrefabAsset(go, $"Assets/Prefabs/Characters/{file}.prefab");
            Object.DestroyImmediate(go);
        }
        CreateWorkerPrefab();

        var folder = AssetDatabase.LoadAssetAtPath<Object>("Assets/Prefabs/Characters");
        Selection.activeObject = folder;
        EditorGUIUtility.PingObject(folder);
        EditorUtility.DisplayDialog("Scary Street",
            "Built Cooper and Nathan (Assets/Prefabs/Characters) and rebuilt the McDonald's worker.\n\n" +
            "Change colors and hair on the looks in Assets/Characters, then run this again.", "OK");
    }

    // Overwrites the Cooper, Nathan and worker looks with the presets in CharacterLook.cs
    // (Cooper and Nathan are matched to their photo), then rebuilds all the character prefabs.
    [MenuItem("Tools/Scary Street/Reset Character Looks To Presets")]
    static void ResetLooks()
    {
        if (!EditorUtility.DisplayDialog("Scary Street",
                "Reset Cooper, Nathan and the McDonald's worker to their preset looks?\nAny color / hair changes you made to them in the Inspector will be replaced.",
                "Reset and rebuild", "Cancel")) return;
        foreach (var (file, preset) in new[] { ("Cooper", "cooper"), ("Nathan", "nathan"), ("McDonaldsWorker", "worker") })
        {
            var look = Look(file, preset);
            Undo.RecordObject(look, "Reset Character Looks");
            look.ApplyPreset(preset);
            EditorUtility.SetDirty(look);
        }
        AssetDatabase.SaveAssets();
        CreateCharacterPrefabs();
    }

    // Loads Assets/Characters/<file>.asset, creating it from a web-build preset the first time.
    static CharacterLook Look(string file, string preset)
    {
        EnsureFolder("Assets", "Characters");
        string path = $"Assets/Characters/{file}.asset";
        var look = AssetDatabase.LoadAssetAtPath<CharacterLook>(path);
        if (look) return look;
        look = ScriptableObject.CreateInstance<CharacterLook>();
        look.ApplyPreset(preset);
        AssetDatabase.CreateAsset(look, path);
        return look;
    }

    // One saved material per look and body part (Assets/Characters/Materials/Cooper_Shirt.mat, ...),
    // recolored on every rebuild so edits to the look show up.
    static BlockyCharacter.MaterialSource AssetMaterials(string lookName)
    {
        EnsureFolder("Assets/Characters", "Materials");
        return (part, color) =>
        {
            string path = $"Assets/Characters/Materials/{lookName}_{part}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            if (m.color != color) { m.color = color; EditorUtility.SetDirty(m); }
            return m;
        };
    }

    // ---------- Rounds ----------

    const string WorkerPrefabPath = "Assets/Prefabs/McDonaldsWorker.prefab";

    // Adds a RoundManager to the scene with the 10 rounds from DESIGN.md, all spawning the
    // McDonald's L1 worker until the other crews exist. Running it again resets the rounds.
    [MenuItem("Tools/Scary Street/Set Up Rounds")]
    static void SetUpRounds()
    {
        var worker = AssetDatabase.LoadAssetAtPath<GameObject>(WorkerPrefabPath);
        if (!worker)
        {
            EditorUtility.DisplayDialog("Scary Street", "Make the worker first: Tools > Scary Street > Create McDonald's Worker Prefab.", "OK");
            return;
        }

        var rm = Object.FindAnyObjectByType<RoundManager>();
        if (!rm)
        {
            var go = new GameObject("RoundManager");
            Undo.RegisterCreatedObjectUndo(go, "Set Up Rounds");
            rm = go.AddComponent<RoundManager>();
        }
        Undo.RecordObject(rm, "Set Up Rounds");
        rm.rounds = RoundManager.DesignRounds(worker);

        EditorSceneManager.MarkSceneDirty(rm.gameObject.scene);
        Selection.activeGameObject = rm.gameObject;
        EditorUtility.DisplayDialog("Scary Street",
            "RoundManager set up with rounds 1–10.\nEvery round spawns McDonald's L1 workers for now.", "OK");
    }

    // ---------- Menus ----------

    // Adds the GameFlow (title screen → character select → run) with Cooper and Nathan playable,
    // and makes sure the Player has what the menus dress up (arms + third-person body).
    [MenuItem("Tools/Scary Street/Set Up Menu")]
    static void SetUpMenu()
    {
        var flow = Object.FindAnyObjectByType<GameFlow>();
        if (!flow)
        {
            var go = new GameObject("GameFlow");
            Undo.RegisterCreatedObjectUndo(go, "Set Up Menu");
            flow = go.AddComponent<GameFlow>();
        }
        Undo.RecordObject(flow, "Set Up Menu");
        flow.playableLooks = new System.Collections.Generic.List<CharacterLook> { Look("Cooper", "cooper"), Look("Nathan", "nathan") };
        EditorSceneManager.MarkSceneDirty(flow.gameObject.scene);
        SetUpPlayer();
        Selection.activeGameObject = flow.gameObject;
        EditorUtility.DisplayDialog("Scary Street",
            "Menu set up: title screen → character select (Cooper, Nathan) → Round 1.\n\n" +
            "To frame the house for the title screen, line up the Scene view on the front of the house, then " +
            "Tools > Scary Street > Set Menu Camera From Scene View.", "OK");
    }

    // Saves the Scene view's current camera as the title-screen camera.
    [MenuItem("Tools/Scary Street/Set Menu Camera From Scene View")]
    static void SetMenuCamera()
    {
        var view = SceneView.lastActiveSceneView;
        if (!view || !view.camera) { EditorUtility.DisplayDialog("Scary Street", "Open a Scene view first.", "OK"); return; }
        var point = Object.FindAnyObjectByType<MenuCameraPoint>();
        if (!point)
        {
            var go = new GameObject("MenuCameraPoint");
            Undo.RegisterCreatedObjectUndo(go, "Set Menu Camera");
            point = go.AddComponent<MenuCameraPoint>();
        }
        Undo.RecordObject(point.transform, "Set Menu Camera");
        point.transform.SetPositionAndRotation(view.camera.transform.position, view.camera.transform.rotation);
        EditorSceneManager.MarkSceneDirty(point.gameObject.scene);
        Selection.activeGameObject = point.gameObject;
        Debug.Log("Scary Street: title-screen camera saved. Press Play to see it.");
    }

    // Drops a SpawnPoint on the floor under the middle of the Scene view.
    [MenuItem("GameObject/Scary Street/Spawn Point", false, 10)]
    [MenuItem("Tools/Scary Street/Add Spawn Point")]
    static void AddSpawnPoint()
    {
        var view = SceneView.lastActiveSceneView;
        Vector3 pos = view ? view.pivot : Vector3.zero;
        if (Physics.Raycast(pos + Vector3.up * 1.5f, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore)) pos = hit.point;

        var go = new GameObject("SpawnPoint");
        go.transform.position = pos;
        go.AddComponent<SpawnPoint>();
        var parent = GameObject.Find("SpawnPoints");
        if (!parent) { parent = new GameObject("SpawnPoints"); Undo.RegisterCreatedObjectUndo(parent, "Add Spawn Point"); }
        go.transform.SetParent(parent.transform, true);
        Undo.RegisterCreatedObjectUndo(go, "Add Spawn Point");
        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);
    }

    // Builds a character with saved materials and saved generated meshes (hair caps, rings),
    // so the prefab doesn't lose anything that only existed in memory.
    static BlockyCharacter BuildSaved(CharacterLook look, Transform parent)
    {
        EnsureFolder("Assets", "Characters");
        EnsureFolder("Assets/Characters", "Meshes");
        MeshKit.Persist = (key, mesh) =>
        {
            string path = $"Assets/Characters/Meshes/{key}.asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved) return saved;
            saved = Object.Instantiate(mesh);
            saved.name = key;
            AssetDatabase.CreateAsset(saved, path);
            return saved;
        };
        try { return BlockyCharacter.Build(look, parent, AssetMaterials(look.name)); }
        finally { MeshKit.Persist = null; }
    }

    // Transparent smoke material saved as an asset, so player builds keep URP's transparent shader variant.
    static Material SmokeMaterial()
    {
        EnsureFolder("Assets", "Weapons");
        const string path = "Assets/Weapons/Smoke.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.93f, 0.94f, 0.96f, 0.85f) };
        SmokeShot.MakeTransparent(m);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // ---------- Doors ----------

    // The web build's doors (three.js coordinates): axis 'x' = doorway in a wall along x at z = f, spanning x a..b;
    // axis 'z' = wall along z at x = f, spanning z a..b; y0 = floor height (basement = -3.2).
    static readonly (string name, char axis, float f, float a, float b, float y0)[] WebDoors =
    {
        ("Front door", 'z', 3.5f, 1.0f, 2.2f, 0), ("Back door", 'x', 21f, 3.95f, 5.05f, 0),
        ("Bedroom 1 door", 'z', 3.5f, 3.85f, 4.95f, 0), ("Bedroom 1 hall door", 'x', 8f, 2.45f, 3.35f, 0),
        ("Bathroom door", 'z', 2.3f, 9f, 10f, 0), ("Bedroom 2 door", 'x', 11f, 2.45f, 3.35f, 0),
        ("Kitchen door", 'x', 15f, 8.4f, 9.3f, 0),
        ("Storage room door", 'z', 3.2f, 4f, 5.2f, -3.2f), ("Spare room door", 'z', 3.2f, 8.2f, 9.4f, -3.2f),
        ("Bathroom 2 door", 'z', 3.5f, 16.9f, 17.9f, 0), ("Bedroom 3 door", 'x', 21f, 8.75f, 9.85f, 0),
        ("Garage side door", 'x', 38f, 1.4f, 2.4f, 0),
    };

    // Finds each door slab + knob in the world model, hides them, and puts working copies on a hinge
    // (Door component) under a "Doors" object. Safe to run again. Rebake the NavMesh afterwards.
    [MenuItem("Tools/Scary Street/Set Up Doors")]
    static void SetUpDoors()
    {
        var world = FindWorld();
        if (!world) { EditorUtility.DisplayDialog("Scary Street", "Couldn't find scary-street-world in the open scene.", "OK"); return; }

        // undo a previous run: show the originals again and drop the old copies
        var old = GameObject.Find("Doors");
        if (old)
        {
            foreach (var d in old.GetComponentsInChildren<Door>(true))
                foreach (var o in d.replacedOriginals) if (o) { Undo.RecordObject(o, "Set Up Doors"); o.SetActive(true); }
            Undo.DestroyObjectImmediate(old);
        }

        var renderers = world.GetComponentsInChildren<MeshRenderer>(true);
        // glTF importers mirror one axis; check which way this import went by counting matches
        float sx = CountDoorMatches(renderers, -1f) >= CountDoorMatches(renderers, 1f) ? -1f : 1f;

        var root = new GameObject("Doors");
        Undo.RegisterCreatedObjectUndo(root, "Set Up Doors");
        int made = 0; var missing = new System.Collections.Generic.List<string>();
        foreach (var d in WebDoors)
        {
            FindDoorParts(renderers, d, sx, out var leaf, out var knob);
            if (!leaf) { missing.Add(d.name); continue; }

            float L = d.b - d.a;
            var hingePos = d.axis == 'x' ? new Vector3(d.a * sx, d.y0, d.f) : new Vector3(d.f * sx, d.y0, d.a);
            var hinge = new GameObject(d.name);
            hinge.transform.SetParent(root.transform, false);
            hinge.transform.position = hingePos;
            var door = hinge.AddComponent<Door>();
            door.doorName = d.name;

            var copyLeaf = CopyMesh(leaf, hinge.transform, "Slab");
            copyLeaf.AddComponent<BoxCollider>();
            var originals = new System.Collections.Generic.List<GameObject> { leaf.gameObject };
            if (knob) { CopyMesh(knob, hinge.transform, "Knob"); originals.Add(knob.gameObject); }
            foreach (var o in originals) { Undo.RecordObject(o, "Set Up Doors"); o.SetActive(false); }
            door.replacedOriginals = originals.ToArray();
            made++;
        }

        EditorSceneManager.MarkSceneDirty(root.scene);
        bool bake = EditorUtility.DisplayDialog("Scary Street",
            $"Set up {made} of {WebDoors.Length} doors." + (missing.Count > 0 ? $"\nNot found: {string.Join(", ", missing)}" : "") +
            "\n\nThe NavMesh needs rebaking so enemies can path through the doorways. Bake it now?",
            "Bake now", "Later");
        if (bake) BakeNavMesh();
        else Selection.activeGameObject = world;
    }

    static int CountDoorMatches(MeshRenderer[] renderers, float sx)
    {
        int n = 0;
        foreach (var d in WebDoors) { FindDoorParts(renderers, d, sx, out var leaf, out _); if (leaf) n++; }
        return n;
    }

    static void FindDoorParts(MeshRenderer[] renderers, (string name, char axis, float f, float a, float b, float y0) d, float sx,
                              out MeshRenderer leaf, out MeshRenderer knob)
    {
        float L = d.b - d.a;
        var leafCenter = d.axis == 'x' ? new Vector3((d.a + L / 2) * sx, d.y0 + 1.05f, d.f) : new Vector3(d.f * sx, d.y0 + 1.05f, d.a + L / 2);
        var leafSize = d.axis == 'x' ? new Vector3(L - 0.02f, 2.1f, 0.05f) : new Vector3(0.05f, 2.1f, L - 0.02f);
        var knobCenter = d.axis == 'x' ? new Vector3((d.a + L - 0.12f) * sx, d.y0 + 1f, d.f) : new Vector3(d.f * sx, d.y0 + 1f, d.a + L - 0.12f);
        leaf = knob = null;
        float bestLeaf = 0.12f, bestKnob = 0.08f;
        foreach (var r in renderers)
        {
            var bnd = r.bounds;
            float dc = Vector3.Distance(bnd.center, leafCenter), ds = Vector3.Distance(bnd.size, leafSize);
            if (dc + ds < bestLeaf) { bestLeaf = dc + ds; leaf = r; }
            float dk = Vector3.Distance(bnd.center, knobCenter);
            if (bnd.size.magnitude < 0.3f && dk < bestKnob) { bestKnob = dk; knob = r; }
        }
    }

    static GameObject CopyMesh(MeshRenderer src, Transform parent, string name)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.GetComponent<MeshFilter>().sharedMesh = src.GetComponent<MeshFilter>().sharedMesh;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterials = src.sharedMaterials;
        go.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
        go.transform.localScale = src.transform.lossyScale;
        go.transform.SetParent(parent, true);
        return go;
    }

    static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
    }
}
