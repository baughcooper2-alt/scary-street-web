using UnityEngine;
using UnityEngine.Rendering;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Gives the player a full BlockyCharacter body and a V / gamepad-Y camera cycle: first person → over the shoulder →
// from the front (look at yourself) → first person. In first person the body only casts a shadow; otherwise it
// shows and the first-person arms hide. The front view only moves the camera while it renders, so aiming, punches
// and weapons still go where you face.
// Put this on the Player (GameFlow sets `look` to the character picked on the select screen).
[RequireComponent(typeof(FirstPersonController))]
public class ThirdPersonView : MonoBehaviour
{
    public CharacterLook look;
    public bool startInThirdPerson;
    [Header("Third-person camera")]
    public float distance = 2.8f;
    public float shoulderOffset = 0.45f;
    public float heightOffset = 0.25f;
    [Tooltip("Keeps the camera from going through walls.")]
    public float collisionRadius = 0.2f;

    [Header("Front camera")]
    public float frontDistance = 2.1f;
    public float frontHeight = 0.05f;

    public enum View { First, Behind, Front }
    public View Mode { get; private set; }
    public bool IsThirdPerson => Mode != View.First;
    public bool IsFrontView => Mode == View.Front;

    Transform cam;
    FirstPersonArms arms;
    CharacterAnimator anim;
    Renderer[] bodyRenderers;
    GameObject bodyRoot;
    float currentDistance;

    void Start()
    {
        var c = GetComponentInChildren<Camera>();
        cam = c ? c.transform : Camera.main.transform;
        arms = cam.GetComponent<FirstPersonArms>();
        if (!look) look = arms && arms.look ? arms.look : CharacterLook.Preset("cooper");

        var body = BlockyCharacter.Build(look, transform, BlockyCharacter.RuntimeMaterials());
        body.name = "Body";
        anim = body.gameObject.AddComponent<CharacterAnimator>();
        bodyRenderers = body.GetComponentsInChildren<Renderer>();
        bodyRoot = body.gameObject;

        var punch = GetComponent<PlayerPunch>();
        if (punch) punch.Punched += () => anim.Punch(0.3f, 0.35f);

        currentDistance = distance; frontCurrent = frontDistance;
        Apply(startInThirdPerson ? View.Behind : View.First);
    }

    void OnEnable() { RenderPipelineManager.beginCameraRendering += BeginRender; RenderPipelineManager.endCameraRendering += EndRender; }
    void OnDisable() { RenderPipelineManager.beginCameraRendering -= BeginRender; RenderPipelineManager.endCameraRendering -= EndRender; }

    // First person: your body is on your own body layer, which your camera skips (mirrors and other players
    // still see it). Third person: your camera shows it and the first-person arms hide.
    void Apply(View mode)
    {
        Mode = mode;
        bool third = mode != View.First;
        int index = PlayerLayers.IndexOf(this);
        foreach (var r in bodyRenderers) r.shadowCastingMode = ShadowCastingMode.On;
        PlayerLayers.Set(bodyRoot, PlayerLayers.Body(index));
        var c = cam ? cam.GetComponent<Camera>() : null;
        if (c) c.cullingMask = PlayerLayers.CameraMask(index, third);
        if (arms) arms.visible = !third;
    }

    void Update()
    {
        if (PlayerControls.For(gameObject).ToggleViewPressed) Apply((View)(((int)Mode + 1) % 3));
    }

    // FirstPersonController puts the camera at eye level every Update; this pulls it back behind the shoulder.
    void LateUpdate()
    {
        if (Mode != View.Behind || !cam) return;
        Vector3 eye = Eye();
        Vector3 offset = -cam.forward * distance + cam.right * shoulderOffset + Vector3.up * heightOffset;
        float want = offset.magnitude;
        if (Physics.SphereCast(eye, collisionRadius, offset / want, out var hit, want, ~0, QueryTriggerInteraction.Ignore))
            want = Mathf.Max(0.3f, hit.distance - 0.05f);
        // snap in when blocked, ease back out when clear
        currentDistance = want < currentDistance ? want : Mathf.MoveTowards(currentDistance, want, Time.deltaTime * 4f);
        cam.position = eye + offset.normalized * currentDistance;
    }

    Vector3 Eye()
    {
        var cc = GetComponent<CharacterController>();
        var fpc = GetComponent<FirstPersonController>();
        return cc && fpc ? transform.position + Vector3.up * (cc.height - fpc.eyeFromTop) : cam.position;
    }

    // ---------- front view: swing the camera round to face you for this camera's render only ----------

    Vector3 savedPos; Quaternion savedRot; bool moved; float frontCurrent;

    void BeginRender(ScriptableRenderContext ctx, Camera c)
    {
        if (Mode != View.Front || !cam || c.transform != cam) return;
        savedPos = cam.position; savedRot = cam.rotation; moved = true;
        Vector3 eye = Eye();
        Vector3 fwd = transform.forward; fwd.y = 0; fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
        Vector3 offset = fwd * frontDistance + Vector3.up * frontHeight;
        float want = offset.magnitude;
        if (Physics.SphereCast(eye, collisionRadius, offset / want, out var hit, want, ~0, QueryTriggerInteraction.Ignore))
            want = Mathf.Max(0.35f, hit.distance - 0.05f);
        frontCurrent = want < frontCurrent ? want : Mathf.MoveTowards(frontCurrent, want, Time.deltaTime * 4f);
        cam.position = eye + offset.normalized * frontCurrent;
        cam.rotation = Quaternion.LookRotation((eye - Vector3.up * 0.18f) - cam.position);   // face and chest in frame
    }

    void EndRender(ScriptableRenderContext ctx, Camera c)
    {
        if (!moved || !cam || c.transform != cam) return;
        cam.position = savedPos; cam.rotation = savedRot; moved = false;
    }
}
