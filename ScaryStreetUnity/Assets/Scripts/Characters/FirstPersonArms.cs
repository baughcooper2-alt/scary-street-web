using UnityEngine;
using UnityEngine.Rendering;

// Your own forearms and fists at the bottom of the screen, colored from your CharacterLook
// (sleeves + skin). They bob while you walk, and the right one jabs when PlayerPunch fires.
// Weapons use RightHand to hold things, `raise` to bring the right hand up to the mouth, and Kick() for recoil.
// Put this on the Main Camera (child of the Player).
[RequireComponent(typeof(Camera))]
public class FirstPersonArms : MonoBehaviour
{
    public CharacterLook look;
    [Tooltip("Where each fist rests, in camera space (right arm; the left is mirrored).")]
    public Vector3 restPosition = new Vector3(0.24f, -0.26f, 0.5f);
    public float punchReach = 0.3f;
    public float punchTime = 0.28f;
    [Tooltip("Hidden while ThirdPersonView is in third person.")]
    public bool visible = true;
    [Tooltip("0 = right hand at rest, 1 = up at the mouth (the cart sets this while you hit it).")]
    [Range(0, 1)] public float raise;
    public Vector3 mouthPosition = new Vector3(0.05f, -0.1f, 0.24f);

    public Transform RightHand => rightGrip;

    // Weapons can take over a hand for the frame: set the override flag, a camera-space position and a rotation.
    // (The walking bob is still added.) Clear the flags on Unequip.
    [System.NonSerialized] public bool overrideRight, overrideLeft;
    [System.NonSerialized] public Vector3 rightTarget, leftTarget, rightEuler, leftEuler;

    Transform right, left, rightGrip;
    float kick;
    Vector3 lastPlayerPos;
    Transform player;
    float punchT = -1f, bobPhase, bobAmount;

    void Start()
    {
        var cam = GetComponent<Camera>();
        cam.nearClipPlane = Mathf.Min(cam.nearClipPlane, 0.05f);   // so the arms aren't clipped away

        if (!look) look = CharacterLook.Preset("cooper");
        var mats = BlockyCharacter.RuntimeMaterials();
        right = BuildArm("RightArm", 1, mats);
        left = BuildArm("LeftArm", -1, mats);
        int layer = PlayerLayers.Arms(PlayerLayers.IndexOf(this));    // only this player's camera draws them
        PlayerLayers.Set(right.gameObject, layer); PlayerLayers.Set(left.gameObject, layer);
        rightGrip = new GameObject("Grip").transform;                 // where held items go: in the curl of the fist
        rightGrip.SetParent(right, false);
        rightGrip.localPosition = new Vector3(-0.01f, 0.035f, 0.01f);

        var punch = GetComponentInParent<PlayerPunch>();
        if (punch) punch.Punched += () => punchT = 0f;
        player = transform.parent ? transform.parent : transform;
        lastPlayerPos = player.position;
    }

    Transform BuildArm(string name, int side, BlockyCharacter.MaterialSource mats)
    {
        var arm = new GameObject(name).transform;
        arm.SetParent(transform, false);
        // forearm runs from the fist back toward the elbow (below and behind the view)
        Part(PrimitiveType.Capsule, arm, new Vector3(0.03f * side, -0.07f, -0.2f), new Vector3(0.085f, 0.17f, 0.085f),
             Quaternion.Euler(70f, -8f * side, 0), mats("Sleeve", look.longSleeves ? look.shirt : look.skin));
        if (look.wristband && side < 0)                                   // band just behind the fist, around the forearm
            Part(PrimitiveType.Cylinder, arm, new Vector3(0.009f * side, -0.02f, -0.056f), new Vector3(0.1f, 0.012f, 0.1f),
                 Quaternion.Euler(70f, -8f * side, 0), mats("Wristband", look.wristbandColor));
        // fist: rounded palm, a row of knuckles facing forward, thumb across the front
        var skin = mats("Skin", look.skin);
        Part(PrimitiveType.Sphere, arm, new Vector3(0, 0, -0.01f), new Vector3(0.085f, 0.08f, 0.1f), Quaternion.Euler(0, -8f * side, 0), skin);
        Part(PrimitiveType.Capsule, arm, new Vector3(0, 0.012f, 0.035f), new Vector3(0.036f, 0.04f, 0.036f), Quaternion.Euler(0, -8f * side, 90f), skin);
        Part(PrimitiveType.Capsule, arm, new Vector3(-0.025f * side, -0.022f, 0.03f), new Vector3(0.028f, 0.03f, 0.028f), Quaternion.Euler(0, -8f * side, 70f * side), skin);
        return arm;
    }

    static void Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Quaternion rot, Material m)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = ShadowCastingMode.Off;
        var t = go.transform;
        t.SetParent(parent, false);
        t.localPosition = pos; t.localRotation = rot; t.localScale = scale;
    }

    public void Kick(float amount = 1f) => kick = Mathf.Max(kick, amount);

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0 || !right) return;
        if (right.gameObject.activeSelf != visible) { right.gameObject.SetActive(visible); left.gameObject.SetActive(visible); }

        // walking bob from how fast the player is moving
        Vector3 d = player.position - lastPlayerPos; d.y = 0;
        lastPlayerPos = player.position;
        float speed = d.magnitude / dt;
        bobAmount = Mathf.MoveTowards(bobAmount, Mathf.Clamp01(speed / 5f), dt * 4f);
        bobPhase += speed * 1.6f * dt;
        Vector3 bob = new Vector3(Mathf.Cos(bobPhase) * 0.012f, -Mathf.Abs(Mathf.Sin(bobPhase)) * 0.018f, 0) * bobAmount;
        float breathe = Mathf.Sin(Time.time * 1.6f) * 0.004f;

        Vector3 rest = restPosition + bob + Vector3.up * breathe;
        Vector3 jab = Vector3.zero;
        if (punchT >= 0)
        {
            punchT += dt;
            float p = punchT / punchTime;
            float k = p < 0.3f ? p / 0.3f : 1f - (p - 0.3f) / 0.7f;   // fast out, slower back
            k = Mathf.SmoothStep(0, 1, Mathf.Clamp01(k));
            jab = new Vector3(-0.16f, 0.1f, punchReach) * k;           // toward the crosshair
            if (p >= 1f) punchT = -1f;
        }
        kick = Mathf.MoveTowards(kick, 0, dt * 6f);
        float r = Mathf.SmoothStep(0, 1, raise);
        right.localPosition = Vector3.Lerp(rest + jab, mouthPosition, r) + new Vector3(0, 0.01f, -0.05f) * kick;
        right.localRotation = Quaternion.Euler(-35f * r - 12f * kick, -20f * r, 0);
        left.localPosition = new Vector3(-rest.x, rest.y - 0.02f, rest.z - 0.04f) - bob * 0.5f;
        left.localRotation = Quaternion.identity;
        if (overrideRight) { right.localPosition = rightTarget + bob + new Vector3(0, 0.01f, -0.05f) * kick; right.localRotation = Quaternion.Euler(rightEuler); }
        if (overrideLeft) { left.localPosition = leftTarget + bob * 0.5f; left.localRotation = Quaternion.Euler(leftEuler); }
    }
}
