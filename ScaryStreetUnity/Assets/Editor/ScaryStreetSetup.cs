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
            $"Player set up: tag Player, Health 25, PlayerPunch, PlayerProgress, PlayerHUD, FirstPersonArms + ThirdPersonView (Cooper)." +
            (removed > 0 ? $"\nRemoved {removed} extra controller(s) from the camera." : ""), "OK");
    }

    // ---------- NavMesh ----------

    // Select the world root (scary-street-world) first. Sizes the Humanoid agent to the workers
    // so they fit through doorways, then adds a NavMeshSurface built from the MeshColliders and bakes it.
    [MenuItem("Tools/Scary Street/Bake NavMesh On Selection")]
    static void BakeNavMesh()
    {
        SetHumanoidAgentSize(radius: 0.3f, height: 1.8f, climb: 0.4f);

        var root = Selection.activeGameObject;
        var surface = root.GetComponent<NavMeshSurface>();
        if (!surface) surface = Undo.AddComponent<NavMeshSurface>(root);
        Undo.RecordObject(surface, "Bake NavMesh");
        surface.agentTypeID = 0;                                           // Humanoid
        surface.collectObjects = CollectObjects.Children;                  // just the world, not the player
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;

        NavMeshAssetManager.instance.StartBakingSurfaces(new Object[] { surface });
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("Scary Street: baking NavMesh (progress in the bottom-right). Save the scene when it finishes.");
    }

    [MenuItem("Tools/Scary Street/Bake NavMesh On Selection", true)]
    static bool ValidateBake() => Selection.activeGameObject && !EditorUtility.IsPersistent(Selection.activeGameObject);

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
        var body = BlockyCharacter.Build(look, go.transform, AssetMaterials(look.name));
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
            var body = BlockyCharacter.Build(look, go.transform, AssetMaterials(look.name));
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

    static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
    }
}
