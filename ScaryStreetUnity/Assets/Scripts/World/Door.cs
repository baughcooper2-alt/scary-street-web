using UnityEngine;
using UnityEngine.AI;

// A door on a hinge (this object sits on the hinge line; the slab and knob are children).
// The player opens and closes it with F (PlayerInteract). Enemies push it open when they walk up to it,
// and a door they opened swings shut again a few seconds after they've gone through.
// It always swings away from whoever opens it, like the web build.
// Created by Tools > Scary Street > Set Up Doors from the web build's door list.
public class Door : MonoBehaviour
{
    public string doorName = "Door";
    public float openAngle = 86f;
    [Tooltip("Degrees per second.")]
    public float swingSpeed = 260f;
    [Header("Enemies")]
    public bool enemiesCanOpen = true;
    public float enemyReach = 1.4f;
    public float closeAfterEnemies = 3f;

    [Tooltip("The original door meshes in the world model, hidden while this door replaces them.")]
    public GameObject[] replacedOriginals = new GameObject[0];

    public bool IsOpen => target != 0f;

    float angle, target, checkT, enemyT;
    bool openedByEnemy;
    Quaternion closedRot;
    Vector3 middle;                 // world-space middle of the closed door
    static readonly Collider[] nearby = new Collider[16];

    void Awake()
    {
        closedRot = transform.localRotation;
        var r = GetComponentsInChildren<Renderer>();
        var b = r.Length > 0 ? r[0].bounds : new Bounds(transform.position, Vector3.one);
        foreach (var x in r) b.Encapsulate(x.bounds);
        middle = b.center;
    }

    public void Toggle(Vector3 from)
    {
        if (IsOpen) { target = 0f; openedByEnemy = false; }
        else OpenAwayFrom(from);
    }

    public void OpenAwayFrom(Vector3 from)
    {
        // try both swings and keep the one that moves the slab further from whoever is opening it
        Vector3 hinge = transform.position, arm = middle - hinge;
        float best = 1f, bestDist = -1f;
        foreach (float s in new[] { 1f, -1f })
        {
            Vector3 swung = hinge + Quaternion.AngleAxis(s * openAngle, Vector3.up) * arm;
            float d = Vector2.Distance(new Vector2(swung.x, swung.z), new Vector2(from.x, from.z));
            if (d > bestDist) { bestDist = d; best = s; }
        }
        target = best * openAngle;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        angle = Mathf.MoveTowards(angle, target, swingSpeed * dt);
        transform.localRotation = closedRot * Quaternion.Euler(0, angle, 0);

        if (!enemiesCanOpen || (checkT -= dt) > 0) { TickEnemyClose(dt); return; }
        checkT = 0.2f;
        int n = Physics.OverlapSphereNonAlloc(middle, enemyReach, nearby, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var agent = nearby[i].GetComponentInParent<NavMeshAgent>();
            if (!agent || !agent.enabled || Mathf.Abs(agent.transform.position.y - transform.position.y) > 1.5f) continue;
            enemyT = closeAfterEnemies;
            if (!IsOpen) { OpenAwayFrom(agent.transform.position); openedByEnemy = true; }
            break;
        }
        TickEnemyClose(dt);
    }

    void TickEnemyClose(float dt)
    {
        if (!openedByEnemy) return;
        if ((enemyT -= dt) <= 0) { target = 0f; openedByEnemy = false; }
    }
}
