# Boogie Down Scary Street

1–4 player co-op PvE survival game for macOS/Windows: survive timed rounds of fast-food workers and cops, level up mid-round, shop (DoorDash) between rounds, fight bosses after rounds 3, 7 and 10. **DESIGN.md is the source of truth** for rounds, enemies, bosses, stats, characters, weapons and upgrades — check it before inventing numbers.

## Repo layout

- `index (5).html` — the original single-file three.js web build (~2.4 MB, minified-ish). Useful for porting feel/numbers (search for `WORKER`, `CREWS`, `WAVES`, `hurtPlayer`, `enemies.push`). Don't edit it; the Unity port is the active work.
- `DESIGN.md` — game design v2 for the Unity build.
- `ScaryStreetUnity/` — the Unity project. Open this folder in Unity Hub.

## Unity project

- Unity **6000.6.3f1** (Unity 6), **URP** (`Assets/Settings/PC_RPAsset`, `Mobile_RPAsset`).
- Packages of note: `com.unity.ai.navigation` 2.0.14 (NavMeshSurface, namespace `Unity.AI.Navigation`), `com.unity.cloud.gltfast` (imports the .glb world), `com.unity.inputsystem` 1.20.
- Active input handling = **Both** (`activeInputHandler: 1`). Scripts read devices directly (`Keyboard.current`, `Mouse.current`, `Gamepad.current`) under `#if ENABLE_INPUT_SYSTEM`, with a legacy `Input` fallback. `InputSystem_Actions.inputactions` exists but is not used yet.
- Only scene: `Assets/Scenes/SampleScene.unity` (huge, ~126k lines, because the world has thousands of MeshColliders). Don't hand-edit scene YAML; make scene changes through editor menu tools or give the user click steps.
- World: `Assets/Models/scary-street-world.glb` (glTFast import), placed in the scene as a prefab instance with MeshColliders added via `Tools > Scary Street > Add Colliders To Selection`.

## Code

- `Assets/Models/FirstPersonController.cs` — player movement/look (CharacterController, camera is a child). Player starts 25 HP per design.
- `Assets/Models/ScaryStreetTools.cs` — editor menu items under `Tools > Scary Street` (wrapped in `#if UNITY_EDITOR` because it lives outside an `Editor` folder).
- `Assets/Editor/ScaryStreetSetup.cs` — `Tools > Scary Street > Set Up Player` (idempotent: tag, Health, PlayerPunch, PlayerProgress, PlayerHUD, FirstPersonArms), `Bake NavMesh` (always bakes scary-street-world regardless of selection; sizes the Humanoid agent to r 0.3 / h 1.8, uses Physics Colliders, and removes stray NavMeshSurfaces on other objects plus their bake files), `Create McDonald's Worker Prefab` (writes `Assets/Prefabs/McDonaldsWorker.prefab` + materials). Also `Create Character Prefabs` (looks + `Assets/Prefabs/Characters/Cooper|Nathan.prefab` + rebuilds the worker), `Set Up Rounds` (adds/resets a RoundManager with DESIGN.md's rounds 1–10) and `Add Spawn Point` (also under GameObject > Scary Street).
- `Assets/Scripts/Combat/Health.cs` — shared HP component for players and enemies (`TakeDamage`, `Heal`, `Damaged`/`Died` events).
- `Assets/Scripts/Combat/PlayerPunch.cs` — default fists (left click / gamepad right trigger).
- `Assets/Scripts/UI/WorldHealthBar.cs` — floating billboard HP bar, builds its own world-space canvas at runtime.
- `Assets/Scripts/UI/PlayerHUD.cs` — temporary IMGUI HP readout + game-over / R to restart.
- `Assets/Scripts/Enemies/McDonaldsWorker.cs` — McDonald's L1 enemy: NavMeshAgent chase, windup punch.
- `Assets/Scripts/Rounds/RoundManager.cs` — round loop: timer, pack spawning that speeds up over the round, leftovers drop at 0:00, boss notice after rounds 3/7/10, placeholder shop (Enter / Start continues), victory after 10. Draws a temporary IMGUI round HUD. `DesignRounds()` holds the defaults; round lengths come from DESIGN.md, and spawn pacing is a first guess for a 25 HP player with fists. Every round spawns McDonald's L1 until the other enemies exist.
- `Assets/Scripts/Progress/PlayerProgress.cs` — cash, XP, level (XP to next = 10 × level), banked upgrade picks (`PendingPicks`) until the upgrade screen exists.
- `Assets/Scripts/Progress/Pickup.cs` / `LootDrop.cs` — enemies drop 3–4 XP gems + one $5 bill (web build numbers); pickups pop out, bob, magnet in at 2.4 m, collect at 0.55 m. Time-up kills don't drop (`LootDrop.Enabled`), and the round end pulls every pickup to the player (`Pickup.VacuumAll`).
- `Assets/Scripts/Characters/CharacterLook.cs` — ScriptableObject with a character's height, colors, sleeves/shorts, hair style (Buzz/Swoop/Curly/Visor/Flow; add new ones at the end), cap, oversized tee, shirt graphic, wristband, worker uniform, and crowd skin/hair variants. The `worker` preset comes from the web build. `cooper` and `nathan` are matched to a photo of the real Cooper (gray oversized tee with a basketball-hoop print, black shorts, white left wristband, medium flowing light-brown hair) and Nathan (all black, dark brown cap over dark curls). `Tools > Scary Street > Reset Character Looks To Presets` re-applies the presets to the assets and rebuilds the prefabs. Assets live in `Assets/Characters/`.
- `Assets/Scripts/Characters/BlockyCharacter.cs` — builds a stylized jointed body from a look: rounded belly + chest, shoulder/elbow/knee caps, oval skull + jaw, eyes (white, iris, pupil, lid), nose, lips, ears, hairline caps, fists with knuckles and thumbs, and sneakers. Built for 1.8 m, then scaled to the look's height. The editor saves materials per part to `Assets/Characters/Materials/<Look>_<Part>.mat`; runtime uses throwaway materials.
- `Assets/Scripts/Characters/CharacterAnimator.cs` — code-driven walk/idle/punch/flinch on BlockyCharacter joints. Limb sign convention: −X swings forward, knees bend +X, elbows bend −X.
- `Assets/Scripts/Characters/FirstPersonArms.cs` — first-person forearms and fists on the Main Camera, colored from the player's look; the right one jabs on `PlayerPunch.Punched`. Sets camera near clip to 0.05.
- `Assets/Scripts/Menu/` — `GameFlow` (single-scene flow: title → character select → run; while the menus are up the Player is inactive and `RoundManager.autoStart` is off; `StartGame` dresses the player and calls `RoundManager.Begin()`; `GameFlow.Restart()` (R) skips the menus with the same character, `BackToMenu()` (M)). `TitleScreen` (Start / Settings / Controls; settings are PlayerPrefs `mouseSensitivity` and `volume`, plus fullscreen). `CharacterSelectScreen` (fighting-game style: mode row where only 1 Player works, roster grid from `CharacterRoster` with COMING SOON on anything without a look, big P1 3D preview, P2 placeholder; the models stand on a hidden stage at y = −300, filmed into RenderTextures). `UIKit` builds uGUI in code (legacy `Text` with LegacyRuntime.ttf, 1920×1080 top-left layout). `MenuCameraPoint` frames the house for the title screen.
- `Assets/Scripts/Characters/ThirdPersonView.cs` — player body plus a V / gamepad-Y over-the-shoulder camera with wall collision. In first person the body is shadows-only. `PlayerPunch` casts from eye height so it also works from third person.
- `Assets/Scripts/Weapons/` — `WeaponInventory` (on Player: 5 slots per DESIGN.md, `AddSlot()` for Backpack; empty slot = fists via `PlayerPunch.allowInput`; 1–5 / wheel / LB-RB; draws the slot bar, weapon hint, toasts and smoke haze). `Weapon` base (Init / Equip / Unequip / Tick(WeaponInput)). `CartWeapon`, ported from the web build: hold RMB / E / LT to inhale up to 6 puffs, LMB / RT to blow. Tier = player level capped at 3 (web: the cart upgraded on level-up): Lv 2 adds an O-ring per puff, Lv 3 the Blinker (hold 1 s once full, 60-damage blast, 5 s cooldown). `SmokeShot` (puff 10 dmg / pierce 99, ring 16 / 3, blast 60 / 999; stops at walls). The smoke material is saved at `Assets/Weapons/Smoke.mat` so builds keep URP's transparent variant.
- `Assets/Scripts/World/Door.cs` + `PlayerInteract.cs` — hinged doors. F / gamepad X toggles the one you're looking at; enemies (any NavMeshAgent) push doors open within 1.4 m, and those auto-close 3 s later; doors swing away from whoever opens them. `Tools > Scary Street > Set Up Doors` matches the web build's 12-door `DOORS` table to the slab + knob meshes in the glb (glTF → Unity mirrors X; the tool detects that), hides the originals, and builds working copies under a `Doors` object outside the world (so the NavMesh bake ignores them). Rebake after running it.
- `Assets/Scripts/Characters/MeshKit.cs` — procedural `HairCap(front, side, back)` (sphere cut along a hairline) and `Torus`. While building prefabs the editor sets `MeshKit.Persist` so these get saved under `Assets/Characters/Meshes/`.
- `Assets/Scripts/Rounds/SpawnPoint.cs` — optional spawn marker. With none placed, RoundManager spawns at random NavMesh spots 10–40 m away that can reach the player and are out of the camera's view.

Conventions: plain MonoBehaviours, public tunable fields with `[Header]`s, short comments explaining intent, no third-party packages. Enemies find the player via the `Player` tag (fallback: `FindAnyObjectByType<FirstPersonController>`). Art is intentionally blocky placeholder (the user chose this over exporting the web build's rigged models or Mixamo). Enemies build a body at runtime if their prefab has no model.

## Numbers ported from the web build

McDonald's L1: 30 HP, speed ~2.9–3.5, starts a punch at 1.1 m, 0.62 s windup, hit lands at 55% if still within 1.45 m and on the same floor, 5 damage, 1.8 s cooldown.

## Known issues / notes

- Fixed: the Main Camera had a duplicate `FirstPersonController` + `CharacterController`, which made the view slowly sink. `Tools > Scary Street > Set Up Player` removes them, and `FirstPersonController` now disables any copy found on a child camera.
- NavMesh must be baked (NavMeshSurface on the world root, geometry = Physics Colliders). Closed door meshes block paths.
- The select screen lists Mordecai and Rigby (Regular Show) as DLC, as DESIGN.md does; they're licensed characters, so rename them or make parody versions before release.
- Licensed names/brands: see the note at the end of DESIGN.md; use lookalike names/colors before any public release.
- The user usually has the Unity editor open, so don't launch it in batch mode. To check that scripts compile, run Unity's bundled Roslyn (`Unity.app/Contents/Resources/Scripting/DotNetSdk`) against `Resources/Scripting/Managed/UnityEngine/*.dll` and `Library/ScriptAssemblies/*.dll`. Then give the user exact editor steps.
