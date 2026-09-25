using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[Serializable]
public class SpawnEntry
{
    public GameObject prefab;
    [Min(0)] public float weight = 1f;
    [Tooltip("Enemy level (McDonald's: 1 fists, 2 spatula / fryer basket, 3 throws food).")]
    [Range(1, 3)] public int level = 1;
}

[Serializable]
public class RoundDefinition
{
    public string name = "Round 1";
    public string subtitle;
    [Tooltip("Round length in seconds (DESIGN.md: 2:00, +30 s each round).")]
    public float duration = 120f;
    [Tooltip("What DESIGN.md calls for. Enemies that aren't built yet are stood in for by the list below.")]
    public string plannedEnemies;
    public List<SpawnEntry> enemies = new List<SpawnEntry>();
    [Tooltip("Seconds between spawns (per enemy) at the start and at the end of the round; it speeds up in between.")]
    public float spawnIntervalStart = 3f, spawnIntervalEnd = 1.5f;
    public int maxAlive = 6;
    [Tooltip("Enemies arrive in packs of 1 to this many.")]
    public int maxPackSize = 1;
    [Tooltip("Boss fought after this round (blank = none). Jack is built; others just show a 'coming soon' message.")]
    public string bossAfter;
}

// Runs the round loop from DESIGN.md: fight until the timer hits 0, remaining enemies drop,
// (boss after rounds 3, 7, 10), shop, next round. Shop and bosses are placeholders for now.
// Also draws a temporary IMGUI round HUD (round, timer, enemies left, banners).
public class RoundManager : MonoBehaviour
{
    public enum State { Fighting, Cleared, Boss, Shop, Victory, GameOver }

    public static RoundManager Instance { get; private set; }

    [Header("Rounds")]
    public List<RoundDefinition> rounds = new List<RoundDefinition>();
    [Tooltip("Round to start on when you press Play (1 = first). Handy for testing later rounds.")]
    [Min(1)] public int startAtRound = 1;
    [Tooltip("Refill the player's HP after each round (there's no shop or regen yet).")]
    public bool healBetweenRounds = true;

    [Header("Spawning")]
    public float minSpawnDistance = 10f;
    public float maxSpawnDistance = 40f;
    public float firstSpawnDelay = 1.5f;

    [Header("Timing")]
    public float bannerTime = 3f;
    public float clearedTime = 3f;
    public float bossNoticeTime = 3f;
    [Tooltip("Seconds between closing the DoorDash bag and the next round.")]
    public float shopToRoundDelay = 2.5f;

    public State CurrentState { get; private set; }
    public int RoundNumber => index + 1;
    public RoundDefinition Current => rounds[index];
    public float TimeLeft { get; private set; }
    public int AliveCount => alive.Count;
    public int KillsThisRound { get; private set; }

    public event Action<int> RoundStarted, RoundEnded;   // round number

    readonly List<Health> alive = new List<Health>();
    SpawnPoint[] spawnPoints;
    Vector3[] navVerts;
    int[] navTris;
    NavMeshPath path;
    Transform player, cam;
    Health playerHealth;
    int index;
    float stateT, spawnT, bannerT, nextRoundT = -1f;
    Health boss;
    bool bossDown;
    string banner, subBanner;
    GUIStyle bigStyle, smallStyle, hudStyle;

    [System.NonSerialized] public bool autoStart = true;   // GameFlow turns this off while the menus are up
    bool begun;

    void Awake() => Instance = this;

    void Start()
    {
        if (autoStart) Begin();
    }

    public void Begin()
    {
        if (begun) return;
        begun = true;
        var p = GameObject.FindWithTag("Player");
        if (!p) { Debug.LogError("RoundManager: no object tagged Player. Run Tools > Scary Street > Set Up Player.", this); enabled = false; return; }
        player = p.transform;
        playerHealth = p.GetComponent<Health>();
        if (playerHealth) playerHealth.Died += () => CurrentState = State.GameOver;
        cam = Camera.main ? Camera.main.transform : null;

        spawnPoints = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
        var tri = NavMesh.CalculateTriangulation();
        navVerts = tri.vertices; navTris = tri.indices;
        path = new NavMeshPath();
        if (spawnPoints.Length == 0 && navTris.Length == 0)
            Debug.LogError("RoundManager: no SpawnPoints and no baked NavMesh, so nothing can spawn.", this);

        if (rounds.Count == 0) { Debug.LogError("RoundManager: no rounds. Run Tools > Scary Street > Set Up Rounds.", this); enabled = false; return; }
        StartRound(Mathf.Clamp(startAtRound - 1, 0, rounds.Count - 1));
    }

    void StartRound(int i)
    {
        index = i;
        CurrentState = State.Fighting;
        nextRoundT = -1f;
        TimeLeft = Current.duration;
        spawnT = firstSpawnDelay;
        KillsThisRound = 0;
        Show(Current.name, Current.subtitle);
        RoundStarted?.Invoke(RoundNumber);
    }

    void Update()
    {
        if (!begun) return;
        bannerT -= Time.deltaTime;
        alive.RemoveAll(h => !h || h.IsDead);

        switch (CurrentState)
        {
            case State.Fighting:
                TimeLeft -= Time.deltaTime;
                spawnT -= Time.deltaTime;
                if (spawnT <= 0 && alive.Count < Current.maxAlive) SpawnPack();
                if (TimeLeft <= 0) EndRound();
                break;

            case State.Cleared:
                if ((stateT -= Time.deltaTime) > 0) break;
                if (!string.IsNullOrEmpty(Current.bossAfter)) StartBoss(Current.bossAfter);
                else GoToShop();
                break;

            case State.Boss:
                if (boss && !boss.IsDead) break;                          // fight until the boss is beaten (DESIGN.md)
                if (boss && boss.IsDead && !bossDown)
                {
                    bossDown = true; stateT = 4f;
                    Show($"{Current.bossAfter} is down!", "Grab the loot");
                }
                if ((stateT -= Time.deltaTime) <= 0) { Pickup.VacuumAll(); boss = null; GoToShop(); }
                break;

            case State.Shop:
                if (nextRoundT >= 0) { if ((nextRoundT -= Time.deltaTime) <= 0) StartRound(index + 1); }
                else if (ContinuePressed() && !DoorDashShop.IsOpen)          // skip the delivery
                {
                    DoorDashCourier.Dismiss();
                    StartRound(index + 1);
                }
                break;
        }
    }

    void EndRound()
    {
        TimeLeft = 0;
        CurrentState = State.Cleared;
        stateT = clearedTime;
        LootDrop.Enabled = false;                                                  // leftovers drop when time's up, but don't pay out
        foreach (var h in alive.ToArray()) if (h) h.TakeDamage(float.MaxValue);
        LootDrop.Enabled = true;
        alive.Clear();
        Pickup.VacuumAll();                                                        // collect whatever's still on the floor
        if (healBetweenRounds && playerHealth) playerHealth.Heal(playerHealth.maxHealth);
        Show($"{Current.name} cleared!", $"{KillsThisRound} knocked out");
        RoundEnded?.Invoke(RoundNumber);
    }

    void StartBoss(string name)
    {
        CurrentState = State.Boss;
        bossDown = false; boss = null;
        if (name == "Jack" && FindSpawnSpot(out var spot))
        {
            boss = JackBoss.Spawn(spot).Health;
            Show("BOSS: Jack", "Bad jokes, worse farts");
        }
        else
        {
            stateT = bossNoticeTime;                                  // not built yet
            Show($"Boss: {name}", "This boss is coming soon. Skipping ahead.");
        }
    }

    void GoToShop()
    {
        if (index + 1 >= rounds.Count) { CurrentState = State.Victory; Show("You survived Scary Street!", "Endless mode is coming later."); }
        else
        {
            // web build: the DoorDash driver walks up to the porch; shopping, then 2.5 s later the next round
            CurrentState = State.Shop;
            nextRoundT = -1f;
            DoorDashCourier.Deliver(() =>
            {
                nextRoundT = shopToRoundDelay;
                Show($"{rounds[index + 1].name} incoming", rounds[index + 1].subtitle);
            });
            Show("Knock knock", "Your DoorDash is on the way up the front steps");
        }
    }

    static bool ContinuePressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current; var pad = Gamepad.current;
        return (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) ||
               (pad != null && pad.startButton.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
    }

    // ---------- spawning ----------

    void SpawnPack()
    {
        var r = Current;
        int pack = Mathf.Min(r.maxAlive - alive.Count, UnityEngine.Random.Range(1, Mathf.Max(1, r.maxPackSize) + 1));
        if (!FindSpawnSpot(out var spot)) { spawnT = 0.5f; return; }

        for (int n = 0; n < pack; n++)
        {
            var entry = PickEntry(r);
            var prefab = entry?.prefab;
            if (!prefab) { Debug.LogWarning($"RoundManager: {r.name} has no enemy prefabs.", this); spawnT = 5f; return; }

            Vector3 pos = spot;
            if (n > 0)
            {
                var jitter = UnityEngine.Random.insideUnitCircle * 1.2f;
                if (NavMesh.SamplePosition(spot + new Vector3(jitter.x, 0, jitter.y), out var hit, 1.5f, NavMesh.AllAreas)) pos = hit.position;
            }
            Vector3 face = player.position - pos; face.y = 0;
            var go = Instantiate(prefab, pos, face.sqrMagnitude > 0.01f ? Quaternion.LookRotation(face) : Quaternion.identity);

            var worker = go.GetComponent<McDonaldsWorker>();
            if (worker) worker.SetLevel(entry.level);
            var h = go.GetComponent<Health>();
            if (!h) continue;
            alive.Add(h);
            h.Died += () => { if (CurrentState == State.Fighting) KillsThisRound++; };
        }

        // speeds up over the round; bigger packs buy a longer gap (web build formula)
        float k = 1f - TimeLeft / r.duration;
        spawnT = Mathf.Lerp(r.spawnIntervalStart, r.spawnIntervalEnd, k) * pack;
    }

    static SpawnEntry PickEntry(RoundDefinition r)
    {
        float total = 0;
        foreach (var e in r.enemies) if (e.prefab) total += e.weight;
        float pick = UnityEngine.Random.value * total;
        foreach (var e in r.enemies)
        {
            if (!e.prefab) continue;
            if ((pick -= e.weight) <= 0) return e;
        }
        return null;
    }

    bool FindSpawnSpot(out Vector3 spot)
    {
        Vector3 p = player.position;
        spot = default;

        // placed SpawnPoints win: one that's far enough away and out of sight, else far enough, else any
        if (spawnPoints.Length > 0)
        {
            var choices = new List<Vector3>();
            foreach (var sp in spawnPoints) if (sp && Flat(sp.transform.position - p) >= minSpawnDistance && !InView(sp.transform.position)) choices.Add(sp.transform.position);
            if (choices.Count == 0) foreach (var sp in spawnPoints) if (sp && Flat(sp.transform.position - p) >= minSpawnDistance) choices.Add(sp.transform.position);
            if (choices.Count == 0) foreach (var sp in spawnPoints) if (sp) choices.Add(sp.transform.position);
            if (choices.Count == 0) return false;
            spot = choices[UnityEngine.Random.Range(0, choices.Count)];
            if (NavMesh.SamplePosition(spot, out var hit, 2f, NavMesh.AllAreas)) spot = hit.position;
            return true;
        }

        // none placed: random NavMesh spot the player can be reached from (not a rooftop), ideally out of sight
        if (navTris.Length == 0 || !NavMesh.SamplePosition(p, out var ph, 2f, NavMesh.AllAreas)) return false;
        bool haveFallback = false;
        Vector3 fallback = default;
        for (int tries = 0; tries < 30; tries++)
        {
            Vector3 c = RandomNavPoint();
            float d = Vector3.Distance(c, p);
            if (d < minSpawnDistance || d > maxSpawnDistance) continue;
            bool seen = InView(c);
            if (seen && haveFallback) continue;
            if (!NavMesh.CalculatePath(c, ph.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            if (!seen) { spot = c; return true; }
            fallback = c; haveFallback = true;
        }
        spot = fallback;
        return haveFallback;
    }

    Vector3 RandomNavPoint()
    {
        int t = UnityEngine.Random.Range(0, navTris.Length / 3) * 3;
        Vector3 a = navVerts[navTris[t]], b = navVerts[navTris[t + 1]], c = navVerts[navTris[t + 2]];
        float u = UnityEngine.Random.value, v = UnityEngine.Random.value;
        if (u + v > 1) { u = 1 - u; v = 1 - v; }
        return a + u * (b - a) + v * (c - a);
    }

    // true if the camera could see someone standing here
    bool InView(Vector3 pos)
    {
        if (!cam) return false;
        Vector3 target = pos + Vector3.up * 1.2f;
        Vector3 vp = cam.GetComponent<Camera>().WorldToViewportPoint(target);
        if (vp.z < 0 || vp.x < 0 || vp.x > 1 || vp.y < 0 || vp.y > 1) return false;
        return !Physics.Linecast(cam.position, target, ~0, QueryTriggerInteraction.Ignore);
    }

    static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }

    // ---------- HUD ----------

    void Show(string title, string sub) { banner = title; subBanner = sub; bannerT = bannerTime; }

    void OnGUI()
    {
        if (!begun || rounds.Count == 0) return;
        if (bigStyle == null)
        {
            bigStyle = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            hudStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }
        float w = Screen.width;

        // top bar: round, timer, enemies left
        string timer = CurrentState == State.Fighting ? $"{Mathf.FloorToInt(TimeLeft / 60)}:{Mathf.FloorToInt(TimeLeft % 60):00}"
                     : CurrentState == State.Boss && boss && !boss.IsDead ? "BOSS FIGHT" : "--:--";
        GUI.Label(new Rect(0, 12, w, 30), $"{Current.name.ToUpper()}    {timer}", hudStyle);
        if (CurrentState == State.Fighting) GUI.Label(new Rect(0, 40, w, 26), $"Workers: {alive.Count}    Knocked out: {KillsThisRound}", smallStyle);

        if (bannerT > 0 && !string.IsNullOrEmpty(banner))
        {
            var old = GUI.color; GUI.color = new Color(1, 1, 1, Mathf.Clamp01(bannerT));
            GUI.Label(new Rect(0, Screen.height * 0.22f, w, 60), banner, bigStyle);
            if (!string.IsNullOrEmpty(subBanner)) GUI.Label(new Rect(0, Screen.height * 0.22f + 55, w, 30), subBanner, smallStyle);
            GUI.color = old;
        }

        if (CurrentState == State.Boss && boss && !boss.IsDead)
        {
            float bw = Mathf.Min(700, w * 0.6f), bx = (w - bw) / 2f, by = 70;
            var old = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.7f); GUI.DrawTexture(new Rect(bx - 3, by - 3, bw + 6, 26), Texture2D.whiteTexture);
            GUI.color = new Color(0.55f, 0.75f, 0.23f); GUI.DrawTexture(new Rect(bx, by, bw * boss.Fraction, 20), Texture2D.whiteTexture);
            GUI.color = old;
            GUI.Label(new Rect(0, by - 2, w, 24), $"{Current.bossAfter.ToUpper()}  {Mathf.CeilToInt(boss.Current)} / {boss.maxHealth:0}", smallStyle);
        }

        if (CurrentState == State.Shop && nextRoundT < 0 && !DoorDashShop.IsOpen)
        {
            var c = DoorDashCourier.Current;
            string line = !c ? "" : c.State == DoorDashCourier.Phase.Waiting
                ? "Your DoorDash is on the front porch: go look at them and press F"
                : "Your DoorDash is on the way to the front porch";
            GUI.Label(new Rect(0, 74, w, 30), line, hudStyle);
            GUI.Label(new Rect(0, 104, w, 26), $"Enter (or Start) to skip the shop and start {rounds[index + 1].name}", smallStyle);
        }
    }

    // ---------- defaults from DESIGN.md ----------

    // Round lengths, crews and boss slots from DESIGN.md. Spawn pacing is a first guess for a 25 HP
    // player with fists (the web build had 100 HP and ranged weapons); tune in the Inspector.
    // DESIGN.md: round 1 L1, round 2 L1–2, round 3 on L1–3 (Cane's and cops aren't built yet, so McDonald's fills in).
    static List<SpawnEntry> McDonaldsMix(GameObject worker, int roundIndex)
    {
        SpawnEntry E(int lvl, float w) => new SpawnEntry { prefab = worker, level = lvl, weight = w };
        if (roundIndex == 0) return new List<SpawnEntry> { E(1, 1f) };
        if (roundIndex == 1) return new List<SpawnEntry> { E(1, 0.6f), E(2, 0.4f) };
        return new List<SpawnEntry> { E(1, 0.4f), E(2, 0.3f), E(3, 0.3f) };
    }

    public static List<RoundDefinition> DesignRounds(GameObject mcdonaldsL1)
    {
        var rows = new (string planned, string sub, float start, float end, int max, int pack, string boss)[]
        {
            ("McDonald's L1",                          "McDonald's workers are coming in the front door and the back gate", 3.0f, 1.5f,  6, 1, ""),
            ("McDonald's L1–2",                        "Now they brought spatulas and fryer baskets",                          2.8f, 1.4f,  8, 2, ""),
            ("McDonald's L1–3",                        "Heads up: they throw burgers, fries and sodas",                      2.6f, 1.3f, 10, 2, "Jack"),
            ("McDonald's L1–3, Cane's L1",             "The chicken finger crew shows up throwing tenders",                  2.4f, 1.2f, 11, 3, ""),
            ("McDonald's L1–3, Cane's L1–2",           "Texas toast bombs incoming",                                         2.2f, 1.1f, 12, 3, ""),
            ("McDonald's L1–3, Cane's L1–3",           "Sauce guns: they blind you",                                         2.0f, 1.0f, 13, 4, ""),
            ("McDonald's L1–3, Cane's L1–3",           "Everybody's here before Eva",                                        1.9f, 0.95f, 14, 4, "Eva"),
            ("McDonald's L1–3, Cane's L1–3, Cops L1",  "The cops showed up with batons",                                     1.8f, 0.9f, 15, 5, ""),
            ("McDonald's L1–3, Cane's L1–3, Cops L1–2","Now the cops have pistols",                                          1.7f, 0.85f, 16, 5, ""),
            ("McDonald's L1–3, Cane's L1–3, Cops L1–3","Shotguns. Good luck.",                                               1.6f, 0.8f, 18, 5, "Andique"),
        };

        var list = new List<RoundDefinition>();
        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            list.Add(new RoundDefinition
            {
                name = $"Round {i + 1}",
                subtitle = row.sub,
                duration = 120f + 30f * i,
                plannedEnemies = row.planned,
                enemies = McDonaldsMix(mcdonaldsL1, i),
                spawnIntervalStart = row.start,
                spawnIntervalEnd = row.end,
                maxAlive = row.max,
                maxPackSize = row.pack,
                bossAfter = row.boss,
            });
        }
        return list;
    }
}
