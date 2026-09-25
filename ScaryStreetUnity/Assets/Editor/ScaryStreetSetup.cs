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
        if (!go.GetComponent<PlayerUpgrades>()) Undo.AddComponent<PlayerUpgrades>(go);
        if (!go.GetComponent<PlayerStats>()) Undo.AddComponent<PlayerStats>(go);
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
            $"Player set up: tag Player, Health 25, PlayerPunch, PlayerProgress, PlayerHUD, WeaponInventory (cart in slot 1), PlayerInteract (doors), PlayerUpgrades, PlayerStats (level-up picks), FirstPersonArms + ThirdPersonView (Cooper)." +
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
            float finish = BlockyCharacter.Finish(part);
            if (!Mathf.Approximately(m.GetFloat("_Smoothness"), finish)) { m.SetFloat("_Smoothness", finish); EditorUtility.SetDirty(m); }
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
        try
        {
            var built = BlockyCharacter.Build(look, parent, AssetMaterials(look.name));
            // realistic bodies put the knit texture on their clothing materials: save that too
            foreach (var r in built.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials) if (m && AssetDatabase.Contains(m)) EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            return built;
        }
        finally { MeshKit.Persist = null; }
    }

    // Cart smoke: SmokeFx's particle material + generated smoke sheet saved as assets, so player builds keep
    // URP's particle shader variants (soft particles, camera fade) and don't regenerate the texture.
    static Material SmokeMaterial()
    {
        EnsureFolder("Assets", "Weapons");
        const string texPath = "Assets/Weapons/SmokeSheet.png", path = "Assets/Weapons/SmokeParticles.mat";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (!tex)
        {
            System.IO.File.WriteAllBytes(texPath, SmokeFx.MakeSheet().EncodeToPNG());
            AssetDatabase.ImportAsset(texPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(texPath);
            imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp; imp.mipmapEnabled = true;
            imp.SaveAndReimport();
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        }
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Particles/Simple Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        SmokeFx.Setup(m);
        m.mainTexture = tex; m.SetTexture("_BaseMap", tex);
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return m;
    }

    // ---------- Mirrors ----------

    // The web build's mirrors (three.js coordinates): centre x, y, z, width, height; all face +X in the web build.
    static readonly (string name, float x, float y, float z, float w, float h)[] WebMirrors =
    {
        ("Bathroom mirror", 0.12f, 1.5f, 8.55f, 0.6f, 0.72f),
        ("Bedroom 1 mirror (full length)", 0.12f, 1.16f, 6.6f, 0.9f, 1.9f),
        ("Bathroom 2 mirror", 2.13f, 1.45f, 18.45f, 0.55f, 0.72f),
    };

    // Puts real mirrors where the web build had them. Safe to run again.
    [MenuItem("Tools/Scary Street/Set Up Mirrors")]
    static void SetUpMirrors()
    {
        var world = FindWorld();
        if (!world) { EditorUtility.DisplayDialog("Scary Street", "Couldn't find scary-street-world in the open scene.", "OK"); return; }
        var renderers = world.GetComponentsInChildren<MeshRenderer>(true);
        float sx = CountDoorMatches(renderers, -1f) >= CountDoorMatches(renderers, 1f) ? -1f : 1f;   // same mirroring as the doors

        var old = GameObject.Find("Mirrors");
        if (old) Undo.DestroyObjectImmediate(old);
        var root = new GameObject("Mirrors");
        Undo.RegisterCreatedObjectUndo(root, "Set Up Mirrors");
        foreach (var m in WebMirrors)
        {
            var go = new GameObject(m.name);
            go.transform.SetParent(root.transform, false);
            go.transform.SetPositionAndRotation(new Vector3(m.x * sx, m.y, m.z), Quaternion.LookRotation(new Vector3(sx, 0, 0)));
            var mirror = go.AddComponent<Mirror>();
            mirror.size = new Vector2(m.w, m.h);
        }
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        EditorUtility.DisplayDialog("Scary Street", $"Placed {WebMirrors.Length} mirrors (bathroom, bedroom 1 full-length, bathroom 2). They show up when you press Play.", "OK");
    }

    // ---------- Graphics ----------

    // Less "plastic toy", more film: tunes the scene's post-processing volume (ACES tonemapping, bloom, grading,
    // vignette, light grain, warm white balance, no motion blur), the URP asset (4x MSAA, 4K shadows),
    // the lighting (warm sun, sky/equator/ground ambient, light fog) and turns post-processing on for the player camera.
    [MenuItem("Tools/Scary Street/Improve Graphics")]
    static void ImproveGraphics()
    {
        var volume = Object.FindAnyObjectByType<UnityEngine.Rendering.Volume>();
        if (volume && volume.sharedProfile)
        {
            var p = volume.sharedProfile;
            Undo.RecordObject(p, "Improve Graphics");
            T Get<T>() where T : UnityEngine.Rendering.VolumeComponent { if (!p.TryGet<T>(out var c)) { c = p.Add<T>(true); AssetDatabase.AddObjectToAsset(c, p); } c.active = true; return c; }
            var tone = Get<UnityEngine.Rendering.Universal.Tonemapping>(); tone.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.ACES);
            var bloom = Get<UnityEngine.Rendering.Universal.Bloom>(); bloom.intensity.Override(0.35f); bloom.threshold.Override(1.05f); bloom.scatter.Override(0.65f);
            var grade = Get<UnityEngine.Rendering.Universal.ColorAdjustments>(); grade.postExposure.Override(0.25f); grade.contrast.Override(14f); grade.saturation.Override(-6f);
            var wb = Get<UnityEngine.Rendering.Universal.WhiteBalance>(); wb.temperature.Override(6f); wb.tint.Override(2f);
            var vig = Get<UnityEngine.Rendering.Universal.Vignette>(); vig.intensity.Override(0.26f); vig.smoothness.Override(0.45f);
            var grain = Get<UnityEngine.Rendering.Universal.FilmGrain>(); grain.type.Override(UnityEngine.Rendering.Universal.FilmGrainLookup.Thin1); grain.intensity.Override(0.18f); grain.response.Override(0.8f);
            var smh = Get<UnityEngine.Rendering.Universal.ShadowsMidtonesHighlights>(); smh.shadows.Override(new Vector4(0.96f, 0.98f, 1.04f, -0.02f)); smh.highlights.Override(new Vector4(1.03f, 1.0f, 0.96f, 0f));
            if (p.TryGet<UnityEngine.Rendering.Universal.MotionBlur>(out var blur)) blur.active = false;
            EditorUtility.SetDirty(p);
        }

        var rp = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
        if (rp)
        {
            var so = new SerializedObject(rp);
            void Set(string prop, int v) { var sp = so.FindProperty(prop); if (sp != null) sp.intValue = v; }
            void SetF(string prop, float v) { var sp = so.FindProperty(prop); if (sp != null) sp.floatValue = v; }
            Set("m_MSAA", 4); Set("m_MainLightShadowmapResolution", 4096); SetF("m_ShadowDistance", 45f); Set("m_SoftShadowsSupported", 1); Set("m_SoftShadowQuality", 3);
            so.ApplyModifiedProperties();
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.58f, 0.64f, 0.74f);
        RenderSettings.ambientEquatorColor = new Color(0.48f, 0.45f, 0.42f);
        RenderSettings.ambientGroundColor = new Color(0.22f, 0.19f, 0.17f);
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.62f, 0.64f, 0.68f); RenderSettings.fogDensity = 0.012f;
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional)
            {
                Undo.RecordObject(l, "Improve Graphics");
                l.color = new Color(1f, 0.93f, 0.82f); l.intensity = 1.35f; l.shadows = LightShadows.Soft; l.shadowStrength = 0.85f;
            }
        foreach (var fpc in Object.FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))
        {
            var cam = fpc.GetComponentInChildren<Camera>();
            if (!cam) continue;
            var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam);
            Undo.RecordObject(data, "Improve Graphics");
            data.renderPostProcessing = true;
            data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;   // 4x MSAA handles edges
            EditorUtility.SetDirty(data);
        }
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Scary Street", "Graphics improved: filmic colour, bloom, grading, grain, softer/higher-res shadows, 4x MSAA, warmer light with ambient and fog. Save the scene.", "OK");
    }

    // ---------- World detail ----------

    // Adds WorldDetail to the house (normal maps from its textures, fine detail layers, better finishes at runtime)
    // and a saved material that keeps URP's normal-map / detail-map shader variants in player builds.
    [MenuItem("Tools/Scary Street/Add World Detail")]
    static void AddWorldDetail()
    {
        var world = FindWorld();
        if (!world) { EditorUtility.DisplayDialog("Scary Street", "Couldn't find scary-street-world in the open scene.", "OK"); return; }
        var wd = world.GetComponent<WorldDetail>() ? world.GetComponent<WorldDetail>() : Undo.AddComponent<WorldDetail>(world);
        EnsureFolder("Assets", "World");
        const string path = "Assets/World/DetailVariants.mat";
        var keeper = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!keeper)
        {
            keeper = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            keeper.EnableKeyword("_NORMALMAP"); keeper.EnableKeyword("_DETAIL_MULX2");
            keeper.SetTexture("_BumpMap", Texture2D.normalTexture); keeper.SetTexture("_DetailAlbedoMap", Texture2D.grayTexture);
            AssetDatabase.CreateAsset(keeper, path);
        }
        Undo.RecordObject(wd, "Add World Detail");
        wd.variantKeeper = keeper;
        EditorSceneManager.MarkSceneDirty(world.scene);
        EditorUtility.DisplayDialog("Scary Street", "World detail added. It kicks in when you press Play (the editor view doesn't change).", "OK");
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
