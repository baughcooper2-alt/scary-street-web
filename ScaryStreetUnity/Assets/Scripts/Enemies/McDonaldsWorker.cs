using UnityEngine;
using UnityEngine.AI;

// McDonald's worker, level 1 (DESIGN.md: round 1, fists).
// Chases the player over the NavMesh and throws a wind-up punch when close.
// Numbers are ported from the web build. The body is a BlockyCharacter (built into the prefab by
// Tools > Scary Street > Create McDonald's Worker Prefab, or at runtime if the prefab has no model).
[RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(CapsuleCollider))]
[RequireComponent(typeof(WorldHealthBar), typeof(LootDrop))]
public class McDonaldsWorker : MonoBehaviour
{
    [Header("Movement")]
    public Vector2 speedRange = new Vector2(2.9f, 3.5f);
    [Tooltip("How often the path to the player is recalculated (seconds).")]
    public float repathInterval = 0.2f;
    public float turnSpeed = 540f;

    [Header("Punch")]
    [Tooltip("Starts a punch when the player is this close (m).")]
    public float attackStartRange = 1.1f;
    [Tooltip("The punch only lands if the player is still within this range (m).")]
    public float hitRange = 1.45f;
    public float damage = 5f;
    public float windupTime = 0.62f;
    [Range(0, 1)] public float hitMoment = 0.55f;
    public float cooldown = 1.8f;
    [Tooltip("Max height difference that still counts as the same floor (m).")]
    public float sameFloorHeight = 1.5f;

    [Header("Getting hit")]
    public float staggerTime = 0.2f;
    [Tooltip("How far a hit shoves the worker back (m).")]
    public float knockback = 0.35f;
    public float corpseLifetime = 3f;

    [Header("Optional imported model")]
    [Tooltip("Animator with optional 'Speed' float and 'Punch' / 'Hit' / 'Die' triggers (for a real rigged model later).")]
    public Animator animator;

    // every living worker, for things that home in on enemies (Guitar notes)
    public static readonly System.Collections.Generic.List<McDonaldsWorker> All = new System.Collections.Generic.List<McDonaldsWorker>();
    public bool IsAlive => health && !health.IsDead;
    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    NavMeshAgent agent;
    Health health, playerHealth;
    Transform player, model;
    Vector3 modelScale = Vector3.one;
    CharacterAnimator body;
    float repathT, cooldownT, attackT = -1f, staggerT, deadT;
    bool hitDone;

    // Runs when the component is first added in the editor: enemy-sized defaults.
    void Reset() => ApplyDefaults();

    public void ApplyDefaults()
    {
        GetComponent<Health>().maxHealth = 30f;
        var a = GetComponent<NavMeshAgent>();
        a.radius = 0.3f; a.height = 1.8f; a.stoppingDistance = 0.9f;
        a.acceleration = 20f; a.angularSpeed = 540f;
        var c = GetComponent<CapsuleCollider>();
        c.radius = 0.3f; c.height = 1.8f; c.center = new Vector3(0, 0.9f, 0);
    }

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        health.Damaged += OnDamaged;
        health.Died += OnDied;
        agent.speed = Random.Range(speedRange.x, speedRange.y);
        cooldownT = Random.Range(0f, 0.5f);   // so a crowd doesn't punch in sync
    }

    void Start()
    {
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (!GetComponentInChildren<Renderer>()) model = BuildPlaceholder();
        else foreach (Transform c in transform) if (c.GetComponentInChildren<Renderer>()) { model = c; break; }
        if (model) modelScale = model.localScale;
        body = GetComponentInChildren<CharacterAnimator>();

        // spawned slightly off the mesh? snap onto it so the agent doesn't error
        if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out var hitPos, 3f, NavMesh.AllAreas))
            agent.Warp(hitPos.position);
        if (!agent.isOnNavMesh)
            Debug.LogWarning($"{name}: not on a NavMesh. Bake one (NavMeshSurface > Bake) and place the worker on the floor.", this);

        FindPlayer();
    }

    void FindPlayer()
    {
        var go = GameObject.FindWithTag("Player");
        if (go) player = go.transform;
        else { var fpc = FindAnyObjectByType<FirstPersonController>(); if (fpc) player = fpc.transform; }
        playerHealth = player ? player.GetComponent<Health>() : null;
    }

    void Update()
    {
        if (health.IsDead) { UpdateCorpse(); return; }
        if (!player) { FindPlayer(); if (!player) return; }
        if (!agent.isOnNavMesh) return;

        Vector3 to = player.position - transform.position;
        float dy = Mathf.Abs(to.y); to.y = 0;
        float dist = to.magnitude;
        bool sameFloor = dy < sameFloorHeight;
        bool playerDead = playerHealth && playerHealth.IsDead;

        cooldownT -= Time.deltaTime;
        if (staggerT > 0) { staggerT -= Time.deltaTime; UpdateAnim(); return; }

        if (attackT >= 0) UpdatePunch(to, dist, sameFloor);
        else if (playerDead) agent.isStopped = true;
        else
        {
            agent.isStopped = false;
            repathT -= Time.deltaTime;
            if (repathT <= 0) { repathT = repathInterval; agent.SetDestination(player.position); }

            if (dist < attackStartRange && sameFloor && cooldownT <= 0) StartPunch();
            else if (dist < agent.stoppingDistance + 0.2f) Face(to);   // agent stops turning once it arrives
        }
        UpdateAnim();
    }

    void StartPunch()
    {
        attackT = 0; hitDone = false;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        if (body) body.Punch(windupTime, hitMoment);
        if (animator) SetTrigger("Punch");
    }

    void UpdatePunch(Vector3 to, float dist, bool sameFloor)
    {
        attackT += Time.deltaTime;
        float p = attackT / windupTime;
        Face(to);

        if (!hitDone && p >= hitMoment)
        {
            hitDone = true;
            if (dist < hitRange && sameFloor && playerHealth) playerHealth.TakeDamage(damage);
        }

        if (p >= 1f)
        {
            attackT = -1f;
            cooldownT = cooldown;
        }
    }

    void Face(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), turnSpeed * Time.deltaTime);
    }

    void OnDamaged(float amount)
    {
        if (health.IsDead) return;
        // flinch: cancel the punch, get shoved away from the player
        attackT = -1f;
        if (body) body.Flinch();
        staggerT = staggerTime;
        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            Vector3 away = player ? transform.position - player.position : -transform.forward;
            away.y = 0;
            float shove = knockback + health.LastKnockback + (PlayerUpgrades.Instance ? PlayerUpgrades.Instance.KnockbackBonus : 0f);   // weapon + Pee upgrade
            agent.Move(away.normalized * shove);   // Move stays on the NavMesh, so no shoving through walls
        }
        if (model) model.localScale = Vector3.Scale(modelScale, new Vector3(1.15f, 0.85f, 1.15f));   // squash, eases back in UpdateAnim
        if (animator) SetTrigger("Hit");
    }

    void OnDied()
    {
        if (agent.isOnNavMesh) agent.isStopped = true;
        agent.enabled = false;
        GetComponent<Collider>().enabled = false;
        if (animator) SetTrigger("Die");
        Destroy(gameObject, corpseLifetime);
    }

    void UpdateCorpse()
    {
        if (animator || !model) return;
        // no death animation: tip over backwards
        deadT += Time.deltaTime;
        model.localRotation = Quaternion.Euler(-90f * Mathf.SmoothStep(0, 1, deadT / 0.5f), 0, 0);
        model.localScale = modelScale;
    }

    void UpdateAnim()
    {
        if (model) model.localScale = Vector3.MoveTowards(model.localScale, modelScale, Time.deltaTime * 3f);
        if (animator && HasParam("Speed")) animator.SetFloat("Speed", agent.velocity.magnitude);
    }

    void SetTrigger(string p) { if (HasParam(p)) animator.SetTrigger(p); }

    bool HasParam(string p)
    {
        foreach (var prm in animator.parameters) if (prm.name == p) return true;
        return false;
    }

    // Runtime fallback when the prefab has no model: the worker look built with throwaway materials.
    Transform BuildPlaceholder()
    {
        var body = BlockyCharacter.Build(CharacterLook.Preset("worker"), transform, BlockyCharacter.RuntimeMaterials());
        body.gameObject.AddComponent<CharacterAnimator>();
        return body.transform;
    }
}
