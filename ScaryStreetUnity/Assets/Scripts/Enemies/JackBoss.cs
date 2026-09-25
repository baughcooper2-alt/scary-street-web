using UnityEngine;
using UnityEngine.AI;

// Jack, the boss after round 3 (DESIGN.md: 100 HP; normal attack: dumb jokes that stun you; mega attack: gas farts).
// Timings from the web build: a joke every 2.6 s within 16 m, a fart cloud every 4.5 s, and every 12 s
// "CROP DUST": a 1.5 s dash at you leaving a trail of clouds. He also punches up close.
// Damage is scaled for the Unity build's 25 HP player. Built entirely in code: JackBoss.Spawn(position).
[RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(CapsuleCollider))]
public class JackBoss : MonoBehaviour
{
    [Header("Stats")]
    public float maxHealth = 100f;
    public float speed = 3.1f;
    [Header("Punch")]
    public float punchDamage = 6f;
    public float punchRange = 1.3f;
    public float punchCooldown = 1.6f;
    [Header("Jokes")]
    public float jokeEvery = 2.6f;
    public float jokeRange = 16f;
    public float jokeDamage = 3f;
    public float jokeStun = 0.8f;
    public float jokeSlow = 1.4f;
    [Header("Farts")]
    public float fartEvery = 4.5f;
    public float fartDamagePerSecond = 3f;
    [Header("Crop Dust (mega)")]
    public float cropDustEvery = 12f;
    public float cropDustRange = 14f;
    public float dashSpeed = 9f;
    public float dashTime = 1.5f;

    static readonly string[] Jokes =
    {
        "Why did the scarecrow win an award?", "I'm reading a book on anti-gravity...",
        "I would tell you a UDP joke...", "Dad, are we pregnant?",
        "What do you call cheese that isn't yours?", "I used to hate facial hair, but then it grew on me",
    };

    public Health Health { get; private set; }
    public static JackBoss Current { get; private set; }
    void OnEnable() => Current = this;
    void OnDisable() { if (Current == this) Current = null; }

    NavMeshAgent agent;
    CharacterAnimator anim;
    Transform model, target;
    Health targetHealth;
    float jokeT = 2f, fartT = 4f, dustT = 10f, punchT, windT = -1f, dashT, trailT, repathT, deadT;
    Vector3 dashDir;
    bool hitDone;

    public static JackBoss Spawn(Vector3 pos)
    {
        var go = new GameObject("Jack");
        go.transform.position = pos;
        var col = go.AddComponent<CapsuleCollider>();
        col.radius = 0.35f; col.height = 1.85f; col.center = new Vector3(0, 0.92f, 0);
        var agent = go.AddComponent<NavMeshAgent>();
        agent.radius = 0.3f; agent.height = 1.85f; agent.stoppingDistance = 1f; agent.acceleration = 20f; agent.angularSpeed = 540f;
        go.AddComponent<Health>();
        var loot = go.AddComponent<LootDrop>();
        loot.xpGems = new Vector2Int(12, 15); loot.cashBills = 6;      // bosses pay out
        return go.AddComponent<JackBoss>();
    }

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        Health = GetComponent<Health>();
        Health.ResetHealth(maxHealth);
        Health.Died += OnDied;
        Health.Damaged += _ => { if (anim) anim.Flinch(); SoundKit.PlayAt(Sfx.Hit, transform.position + Vector3.up, 0.9f); };
        agent.speed = speed;

        var body = BlockyCharacter.Build(CharacterLook.Preset("jack"), transform, BlockyCharacter.RuntimeMaterials());
        anim = body.gameObject.AddComponent<CharacterAnimator>();
        model = body.transform;
    }

    void Start()
    {
        if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out var hit, 3f, NavMesh.AllAreas)) agent.Warp(hit.position);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (Health.IsDead) { if (anim) anim.Die(); return; }
        if ((repathT -= dt) <= 0 || !target) { repathT = 0.25f; target = Players.Nearest(transform.position, out targetHealth); if (anim) anim.lookAt = target; if (target && agent.isOnNavMesh) agent.SetDestination(target.position); }
        if (!target || !agent.isOnNavMesh) return;

        Vector3 to = target.position - transform.position;
        bool sameFloor = Mathf.Abs(to.y) < 1.5f; to.y = 0;
        float dist = to.magnitude;
        jokeT -= dt; fartT -= dt; dustT -= dt; punchT -= dt;

        // mega attack: Crop Dust
        if (dashT > 0)
        {
            dashT -= dt;
            agent.Move(dashDir * dashSpeed * dt);                     // Move stays on the NavMesh
            transform.rotation = Quaternion.LookRotation(dashDir);
            if ((trailT -= dt) <= 0) { trailT = 0.22f; FartCloud.Spawn(transform.position, 3.5f, fartDamagePerSecond, quiet: true); }
            if (dashT <= 0) agent.isStopped = false;
            return;
        }
        if (dustT <= 0 && dist < cropDustRange && sameFloor)
        {
            dustT = cropDustEvery; dashT = dashTime; trailT = 0;
            dashDir = to.normalized;
            agent.isStopped = true; agent.velocity = Vector3.zero;
            Announce("CROP DUST", "Get out of the way!");
            SoundKit.PlayAt(Sfx.BigFart, transform.position + Vector3.up, 1f);
            return;
        }

        // normal attacks
        if (jokeT <= 0 && dist < jokeRange)
        {
            jokeT = jokeEvery;
            JokeBubble.Spawn(transform.position + Vector3.up * 2.2f, target.position + Vector3.up * 1.4f,
                             Jokes[Random.Range(0, Jokes.Length)], jokeDamage, jokeStun, jokeSlow);
            if (anim) anim.Talk(1.3f);                                  // mouth going, hands gesturing
            SoundKit.PlayAt(Sfx.Blah, transform.position + Vector3.up * 1.7f, 0.8f);
        }
        if (fartT <= 0) { fartT = fartEvery; FartCloud.Spawn(transform.position - transform.forward * 0.4f, 6f, fartDamagePerSecond); if (anim) anim.Squat(0.8f); }

        // punch up close
        if (windT >= 0)
        {
            windT += dt;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 540f * dt);
            if (!hitDone && windT >= 0.35f)
            {
                hitDone = true;
                if (dist < punchRange + 0.35f && sameFloor && targetHealth) targetHealth.TakeDamage(punchDamage);
            }
            if (windT >= 0.6f) { windT = -1f; agent.isStopped = false; }
        }
        else if (dist < punchRange && sameFloor && punchT <= 0)
        {
            punchT = punchCooldown; windT = 0; hitDone = false;
            agent.isStopped = true; agent.velocity = Vector3.zero;
            if (anim) anim.Punch(0.6f, 0.58f);
        }
    }

    void Announce(string title, string sub)
    {
        foreach (var p in Players.All) { var inv = p ? p.GetComponent<WeaponInventory>() : null; if (inv) inv.Toast($"{title}: {sub}", 1.4f); }
    }

    void OnDied()
    {
        SoundKit.PlayAt(Sfx.EnemyDown, transform.position + Vector3.up, 1f);
        if (agent.isOnNavMesh) agent.isStopped = true;
        agent.enabled = false;
        GetComponent<Collider>().enabled = false;
        Destroy(gameObject, 5f);
    }
}
