using UnityEngine;
using UnityEngine.Rendering;

// Your own forearms and fists at the bottom of the screen, colored from your CharacterLook
// (sleeves + skin). They bob while you walk, and the right one jabs when PlayerPunch fires.
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

    Transform right, left;
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
        Part(PrimitiveType.Cube, arm, Vector3.zero, new Vector3(0.085f, 0.085f, 0.1f),
             Quaternion.Euler(0, -8f * side, 0), mats("Skin", look.skin));
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
        right.localPosition = rest + jab;
        left.localPosition = new Vector3(-rest.x, rest.y - 0.02f, rest.z - 0.04f) - bob * 0.5f;
    }
}
