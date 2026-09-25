using UnityEngine;
using UnityEngine.AI;

// McDonald's worker (DESIGN.md), levels 1–3:
//   L1 fists · L2 a spatula or fryer basket (hits harder, reaches further) · L3 carries a tray of food and
//   throws burgers, fries and sodas from 3–10 m (web build: every 2.4–3.4 s), and bashes with the tray up close.
// Chases the nearest player over the NavMesh and throws a wind-up hit when close. RoundManager calls SetLevel on spawn.
// Numbers are ported from the web build. The body is a BlockyCharacter (built into the prefab by
// Tools > Scary Street > Create McDonald's Worker Prefab, or at runtime if the prefab has no model).
[RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(CapsuleCollider))]
[RequireComponent(typeof(WorldHealthBar), typeof(LootDrop))]
public class McDonaldsWorker : MonoBehaviour
{
    [Header("Level")]
    [Range(1, 3)] public int level = 1;
    [Tooltip("L3 throws food from this range (min, max).")]
    public Vector2 throwRange = new Vector2(3f, 10f);
    public float throwDamage = 4f;

    // Not every employee acts the same: most chase normally, some rush, slack off, circle, flank or scream for backup.
    public enum Personality { Normal, Rusher, Slacker, Circler, Flanker, Screamer }
    [Header("Personality")]
    public Personality personality;
    [Tooltip("Pick a personality (and walking style) at random when spawned.")]
    public bool randomPersonality = true;

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
    float repathT, cooldownT, attackT = -1f, staggerT, deadT, throwCd, throwT = -1f;
    bool hitDone, thrown;
    Transform heldItem;

    // Called right after spawning (before Start). Numbers per level follow the web build's waves.
    public void SetLevel(int newLevel)
    {
        level = Mathf.Clamp(newLevel, 1, 3);
        if (!health) health = GetComponent<Health>();
        switch (level)
        {
            case 1: health.ResetHealth(30f); damage = 5f; break;
            case 2:
                health.ResetHealth(36f);
                damage = 7f; attackStartRange = 1.3f; hitRange = 1.7f;
                break;
            default:
                health.ResetHealth(40f);
                damage = 8f; attackStartRange = 1.2f; hitRange = 1.6f;
                speedRange = new Vector2(2.5f, 3.1f);                 // the tray slows them down
                if (agent) agent.speed = Random.Range(speedRange.x, speedRange.y);
                throwCd = Random.Range(1f, 2.5f);
                break;
        }
    }

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
        agent.angularSpeed = 300f; agent.acceleration = 14f;   // smoother starts and turns than snapping round
        cooldownT = Random.Range(0f, 0.5f);   // so a crowd doesn't punch in sync
        if (randomPersonality)
        {
            float r = Random.value;
            personality = r < 0.42f ? Personality.Normal : r < 0.57f ? Personality.Rusher : r < 0.71f ? Personality.Slacker
                        : r < 0.83f ? Personality.Circler : r < 0.94f ? Personality.Flanker : Personality.Screamer;
        }
        flankSide = Random.value < 0.5f ? -1f : 1f;
        breakT = Random.Range(4f, 9f);
    }

    // speed and timing per personality (called from Start, after SetLevel)
    void ApplyPersonality()
    {
        switch (personality)
        {
            case Personality.Rusher:  agent.speed *= 1.3f; cooldown *= 0.7f; windupTime *= 0.85f; break;
            case Personality.Slacker: agent.speed *= 0.72f; cooldown *= 1.2f; break;
            case Personality.Circler: agent.speed *= 1.05f; break;
        }
        if (!body) return;
        // everyone walks a little differently
        body.strideScale = Random.Range(0.9f, 1.1f);
        body.armSwingScale = Random.Range(0.75f, 1.25f);
        body.hunch = Random.Range(-2f, 6f);
        body.swagger = Random.Range(0f, 0.4f);
        if (personality == Personality.Rusher) { body.hunch = 11f; body.armSwingScale = 1.3f; }
        if (personality == Personality.Slacker) { body.hunch = 7f; body.armSwingScale = 0.55f; body.swagger = 0.8f; }
    }

    float flankSide, breakT, breakLeft, backOffT, normalSpeed;
    bool yelled;
    static readonly System.Collections.Generic.List<McDonaldsWorker> rallied = new System.Collections.Generic.List<McDonaldsWorker>();
    float rallyT;

    // Screamer: "GET HIM!" — nearby coworkers speed up for a few seconds.
    void Rally()
    {
        SoundKit.PlayAt(Sfx.Blah, transform.position + Vector3.up * 1.7f, 1f, 0.05f);
        if (body) body.Talk(1.2f);
        foreach (var w in All)
            if (w && w != this && w.IsAlive && (w.transform.position - transform.position).sqrMagnitude < 15f * 15f) w.rallyT = 6f;
    }

    // Where to walk to this repath, by personality.
    Vector3 ChaseTarget(Vector3 to, float dist)
    {
        Vector3 p = player.position;
        switch (personality)
        {
            case Personality.Flanker when dist > 3f:
                return p + player.right * (2.5f * flankSide) - player.forward * 1.5f;   // come in from the side / behind
            case Personality.Circler when dist < 4f && (cooldownT > 0 || backOffT > 0):
            {
                if (backOffT > 0) return transform.position - to.normalized * 2.5f;   // back off after a hit
                Vector3 around = Quaternion.Euler(0, 45f * flankSide, 0) * -to.normalized;
                return p + around * 2.4f;                                              // orbit at arm's length
            }
        }
        return p;
    }

    void Start()
    {
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (!GetComponentInChildren<Renderer>()) model = BuildPlaceholder();
        else foreach (Transform c in transform) if (c.GetComponentInChildren<Renderer>()) { model = c; break; }
        if (model) modelScale = model.localScale;
        body = GetComponentInChildren<CharacterAnimator>();
        ApplyPersonality();
        BuildHeldItem();
        if (body && level >= 3) body.hold = CharacterAnimator.Hold.Tray;

        // spawned slightly off the mesh? snap onto it so the agent doesn't error
        if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out var hitPos, 3f, NavMesh.AllAreas))
            agent.Warp(hitPos.position);
        if (!agent.isOnNavMesh)
            Debug.LogWarning($"{name}: not on a NavMesh. Bake one (NavMeshSurface > Bake) and place the worker on the floor.", this);

        FindPlayer();
    }

    // Chase whoever is nearest and still alive (co-op ready).
    void FindPlayer()
    {
        player = Players.Nearest(transform.position, out playerHealth);
        if (body) body.lookAt = player;                                     // keep an eye on who we're chasing
    }

    void Update()
    {
        if (health.IsDead) { UpdateCorpse(); return; }
        float dtp = Time.deltaTime;
        rallyT -= dtp; backOffT -= dtp;
        if (normalSpeed <= 0) normalSpeed = agent.speed;
        agent.speed = normalSpeed * (rallyT > 0 ? 1.2f : 1f);      // a screamer's rally speeds us up for a bit
        if ((repathT <= 0 || !player) ) FindPlayer();                 // re-pick the nearest player whenever we repath
        if (!player) { if (agent.isOnNavMesh) agent.isStopped = true; UpdateAnim(); return; }
        if (!agent.isOnNavMesh) return;

        Vector3 to = player.position - transform.position;
        float dy = Mathf.Abs(to.y); to.y = 0;
        float dist = to.magnitude;
        bool sameFloor = dy < sameFloorHeight;
        bool playerDead = playerHealth && playerHealth.IsDead;

        cooldownT -= Time.deltaTime;
        if (staggerT > 0) { staggerT -= Time.deltaTime; UpdateAnim(); return; }

        throwCd -= Time.deltaTime;
        if (throwT >= 0) UpdateThrow(to);
        else if (attackT >= 0) UpdatePunch(to, dist, sameFloor);
        else if (playerDead) agent.isStopped = true;
        else if (personality == Personality.Slacker && (breakLeft > 0 || (breakT -= dtp) <= 0) && dist > 6f)
        {
            // on a break: stand there scrolling until the player gets close
            if (breakLeft <= 0) { breakLeft = Random.Range(1.5f, 3.5f); breakT = Random.Range(5f, 10f); }
            breakLeft -= dtp;
            agent.isStopped = true;
            if (body) body.hold = breakLeft > 0 ? CharacterAnimator.Hold.Phone : CharacterAnimator.Hold.None;
        }
        else if (personality == Personality.Screamer && !yelled && dist < 12f && CanSee())
        {
            yelled = true; agent.isStopped = true; staggerT = 1.1f; Face(to);
            Rally();
        }
        else
        {
            breakLeft = 0;
            if (body && body.hold == CharacterAnimator.Hold.Phone) body.hold = CharacterAnimator.Hold.None;
            agent.isStopped = false;
            repathT -= Time.deltaTime;
            if (repathT <= 0) { repathT = repathInterval; agent.SetDestination(ChaseTarget(to, dist)); }

            if (dist < attackStartRange && sameFloor && cooldownT <= 0) StartPunch();
            else if (level >= 3 && throwCd <= 0 && sameFloor && dist > throwRange.x && dist < throwRange.y && CanSee()) StartThrow();
            else if (dist < agent.stoppingDistance + 0.2f) Face(to);   // agent stops turning once it arrives
        }
        UpdateAnim();
    }

    // ---------- L3: throwing food ----------

    bool CanSee()
    {
        Vector3 eye = transform.position + Vector3.up * 1.5f, target = player.position + Vector3.up * 1.2f;
        return !Physics.Linecast(eye, target, out var hit, ~0, QueryTriggerInteraction.Ignore) || hit.transform.root == player.root;
    }

    void StartThrow()
    {
        throwT = 0; thrown = false;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        if (body) body.Throw(0.55f, 0.6f);
    }

    void UpdateThrow(Vector3 to)
    {
        throwT += Time.deltaTime;
        Face(to);
        if (!thrown && throwT >= 0.33f)                               // let go 60% through the wind-up
        {
            thrown = true;
            Vector3 hand = transform.position + Vector3.up * 1.5f + transform.forward * 0.3f;
            FoodShot.Throw(hand, player.position + Vector3.up * 1.1f, throwDamage, gameObject);
            SoundKit.PlayAt(Sfx.Throw, hand, 0.7f);
        }
        if (throwT >= 0.55f) { throwT = -1f; throwCd = Random.Range(2.4f, 3.4f); }
    }

    // ---------- held items ----------

    void BuildHeldItem()
    {
        var b = GetComponentInChildren<BlockyCharacter>();
        if (!b || level < 2) return;
        var mats = BlockyCharacter.RuntimeMaterials();
        if (level == 2 && Random.value < 0.5f)
        {
            // spatula: black handle, steel blade pointing down out of the fist
            heldItem = new GameObject("Spatula").transform;
            heldItem.SetParent(b.handR, false);
            heldItem.localPosition = new Vector3(0, -0.06f, 0.03f);
            Piece(PrimitiveType.Cube, heldItem, new Vector3(0, 0, 0.06f), new Vector3(0.022f, 0.022f, 0.2f), mats("Handle", new Color(0.08f, 0.08f, 0.08f)));
            Piece(PrimitiveType.Cube, heldItem, new Vector3(0, -0.01f, 0.21f), new Vector3(0.09f, 0.008f, 0.12f), mats("Steel", new Color(0.75f, 0.77f, 0.8f)));
        }
        else if (level == 2)
        {
            // fryer basket: wire basket on a long handle
            damage = 8f; windupTime = 0.75f;
            heldItem = new GameObject("FryerBasket").transform;
            heldItem.SetParent(b.handR, false);
            heldItem.localPosition = new Vector3(0, -0.06f, 0.03f);
            Piece(PrimitiveType.Cube, heldItem, new Vector3(0, 0, 0.1f), new Vector3(0.025f, 0.025f, 0.24f), mats("Handle", new Color(0.08f, 0.08f, 0.08f)));
            Piece(PrimitiveType.Cube, heldItem, new Vector3(0, -0.02f, 0.3f), new Vector3(0.16f, 0.09f, 0.2f), mats("Basket", new Color(0.55f, 0.56f, 0.58f)));
            Piece(PrimitiveType.Cube, heldItem, new Vector3(0, 0.01f, 0.3f), new Vector3(0.14f, 0.04f, 0.18f), mats("Fries", new Color(0.95f, 0.78f, 0.3f)));
        }
        else
        {
            // tray of food carried in front of the chest
            heldItem = new GameObject("Tray").transform;
            heldItem.SetParent(b.spine, false);
            heldItem.localPosition = new Vector3(0, 0.2f, 0.32f);
            Piece(PrimitiveType.Cube, heldItem, Vector3.zero, new Vector3(0.36f, 0.02f, 0.26f), mats("Tray", new Color(0.55f, 0.12f, 0.1f)));
            FoodShot.BuildFood(FoodShot.Food.Burger, heldItem, new Vector3(-0.09f, 0.04f, 0), mats);
            FoodShot.BuildFood(FoodShot.Food.Fries, heldItem, new Vector3(0.05f, 0.06f, -0.05f), mats);
            FoodShot.BuildFood(FoodShot.Food.Soda, heldItem, new Vector3(0.11f, 0.08f, 0.06f), mats);
        }
    }

    static void Piece(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material m)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos; go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = m;
    }

    void StartPunch()
    {
        attackT = 0; hitDone = false;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        if (body)
        {
            if (level == 2) body.Swing(windupTime, hitMoment);               // spatula / basket: overhead chop
            else body.Punch(windupTime, hitMoment);                           // fists, or a shove with the tray
        }
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
            if (personality == Personality.Circler) backOffT = 1f;
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
        SoundKit.PlayAt(Sfx.Hit, transform.position + Vector3.up, 0.8f);
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
            float shove = knockback + health.LastKnockback;           // the attacker's weapon + Pee upgrade ride along with the hit
            agent.Move(away.normalized * shove);   // Move stays on the NavMesh, so no shoving through walls
        }
        if (model) model.localScale = Vector3.Scale(modelScale, new Vector3(1.15f, 0.85f, 1.15f));   // squash, eases back in UpdateAnim
        if (animator) SetTrigger("Hit");
    }

    void OnDied()
    {
        SoundKit.PlayAt(Sfx.EnemyDown, transform.position + Vector3.up, 0.6f);
        if (agent.isOnNavMesh) agent.isStopped = true;
        agent.enabled = false;
        GetComponent<Collider>().enabled = false;
        if (animator) SetTrigger("Die");
        if (body) body.Die(Random.value < 0.35f);                  // crumple (mostly backwards)
        Destroy(gameObject, corpseLifetime);
    }

    void UpdateCorpse()
    {
        if (animator || !model || body) return;                    // CharacterAnimator handles the collapse
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
