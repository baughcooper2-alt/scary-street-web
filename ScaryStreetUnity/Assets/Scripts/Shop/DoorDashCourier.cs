using System;
using UnityEngine;
using UnityEngine.AI;

// The DoorDash driver from the web build: after a round is cleared they walk up the front path to the
// porch with a red delivery bag and wait. Press F on them to open the shop; when you're done they head
// back to the street and the next round starts.
// The path comes from DeliveryPoint markers if the scene has them, otherwise from the web build's
// front path (x 1.7, z −8.5 → −0.6), mirrored to match the Front door the way the world model was imported.
public class DoorDashCourier : MonoBehaviour, IInteractable
{
    public enum Phase { Walking, Waiting, Shopping, Leaving }

    public static DoorDashCourier Current { get; private set; }
    public Phase State { get; private set; }

    public string Prompt => "Open your DoorDash";
    public bool CanInteract => State == Phase.Waiting;

    // co-op: everyone alive gets their own order before the driver leaves
    readonly System.Collections.Generic.HashSet<GameObject> served = new System.Collections.Generic.HashSet<GameObject>();

    NavMeshAgent agent;
    Vector3 street, porch;
    Transform player;
    Action onDone;
    float leaveT;

    public static DoorDashCourier Deliver(Action onFinishedShopping)
    {
        Dismiss();
        FindPath(out var street, out var porch);

        var go = new GameObject("DoorDashCourier");
        go.transform.SetPositionAndRotation(street, Quaternion.LookRotation(Flat(porch - street)));
        var c = go.AddComponent<DoorDashCourier>();
        c.street = street; c.porch = porch; c.onDone = onFinishedShopping;

        var body = BlockyCharacter.Build(CharacterLook.Preset("courier"), go.transform, BlockyCharacter.RuntimeMaterials());
        body.gameObject.AddComponent<CharacterAnimator>();
        BuildBag(body.spine);

        var col = go.AddComponent<CapsuleCollider>();
        col.radius = 0.35f; col.height = 1.8f; col.center = new Vector3(0, 0.9f, 0);

        c.agent = go.AddComponent<NavMeshAgent>();
        c.agent.speed = 2.2f; c.agent.radius = 0.3f; c.agent.height = 1.8f; c.agent.stoppingDistance = 0.2f;
        c.agent.acceleration = 12f; c.agent.angularSpeed = 360f;
        if (c.agent.isOnNavMesh) c.agent.SetDestination(porch);
        Current = c;
        return c;
    }

    public static void Dismiss()
    {
        if (Current) Destroy(Current.gameObject);
        Current = null;
    }

    // Called by the shop when a player closes it. The driver waits for anyone who hasn't shopped yet.
    public void FinishShopping(GameObject shopper)
    {
        if (shopper) served.Add(shopper);
        foreach (var p in Players.All)
        {
            var h = p ? p.GetComponent<Health>() : null;
            if (p && h && !h.IsDead && !served.Contains(p.gameObject)) { State = Phase.Waiting; return; }
        }
        State = Phase.Leaving;
        leaveT = 0;
        if (agent && agent.isOnNavMesh) { agent.isStopped = false; agent.SetDestination(street); }
        onDone?.Invoke();
    }

    public void Interact(GameObject who)
    {
        if (!CanInteract || served.Contains(who)) return;
        State = Phase.Shopping;
        DoorDashShop.Open(who, this);
    }

    void Update()
    {
        player = Players.Nearest(transform.position, out _);
        float dt = Time.deltaTime;

        switch (State)
        {
            case Phase.Walking:
                if (Arrived(porch, dt))
                {
                    State = Phase.Waiting;
                    if (agent.isOnNavMesh) agent.isStopped = true;
                    SoundKit.PlayAt(Sfx.Knock, transform.position + Vector3.up, 1f, 0f);   // knock knock
                    SoundKit.Play(Sfx.Doorbell, 0.5f, 0f);
                }
                break;
            case Phase.Waiting:
            case Phase.Shopping:
                if (player)                                           // turn to face you
                {
                    var to = Flat(player.position - transform.position);
                    if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 240f * dt);
                }
                break;
            case Phase.Leaving:
                if (Arrived(street, dt) || (leaveT += dt) > 25f) { if (Current == this) Current = null; Destroy(gameObject); }
                break;
        }
    }

    // Walks with the NavMesh if we're on it, otherwise straight at the target.
    bool Arrived(Vector3 target, float dt)
    {
        if (agent && agent.isOnNavMesh)
            return !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f;
        transform.position = Vector3.MoveTowards(transform.position, target, 2.2f * dt);
        var to = Flat(target - transform.position);
        if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(to);
        return Vector3.Distance(transform.position, target) < 0.15f;
    }

    static void FindPath(out Vector3 street, out Vector3 porch)
    {
        street = porch = Vector3.zero;
        bool haveStreet = false, havePorch = false;
        foreach (var p in FindObjectsByType<DeliveryPoint>(FindObjectsSortMode.None))
        {
            if (p.role == DeliveryPoint.Role.Street) { street = p.transform.position; haveStreet = true; }
            else { porch = p.transform.position; havePorch = true; }
        }
        if (haveStreet && havePorch) return;

        // web build coordinates, mirrored on X if the Front door came in mirrored (it's at x = +3.5 in the web build)
        float sx = -1f;
        foreach (var d in FindObjectsByType<Door>(FindObjectsSortMode.None))
            if (d.doorName == "Front door") { sx = Mathf.Sign(d.transform.position.x); break; }
        if (!haveStreet) street = Snap(new Vector3(1.7f * sx, 0, -8.5f));
        if (!havePorch) porch = Snap(new Vector3(1.7f * sx, 0, -0.6f));
    }

    static Vector3 Snap(Vector3 p) => NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas) ? hit.position : p;
    static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

    // Red insulated delivery bag on their back (no logo).
    static void BuildBag(Transform spine)
    {
        var mats = BlockyCharacter.RuntimeMaterials();
        var bag = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(bag.GetComponent<Collider>());
        bag.name = "DeliveryBag";
        bag.transform.SetParent(spine, false);
        bag.transform.localPosition = new Vector3(0, 0.32f, -0.3f);
        bag.transform.localScale = new Vector3(0.45f, 0.42f, 0.35f);
        bag.GetComponent<Renderer>().sharedMaterial = mats("Bag", new Color(0.7f, 0.15f, 0.12f));
        var lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(lid.GetComponent<Collider>());
        lid.transform.SetParent(spine, false);
        lid.transform.localPosition = new Vector3(0, 0.54f, -0.3f);
        lid.transform.localScale = new Vector3(0.46f, 0.04f, 0.36f);
        lid.GetComponent<Renderer>().sharedMaterial = mats("BagLid", new Color(0.85f, 0.85f, 0.85f));
    }
}
