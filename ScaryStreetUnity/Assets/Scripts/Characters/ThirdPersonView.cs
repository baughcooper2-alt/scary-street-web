using UnityEngine;
using UnityEngine.Rendering;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Gives the player a full BlockyCharacter body and a V / gamepad-Y toggle to an over-the-shoulder camera.
// In first person the body only casts a shadow; in third person it shows and the first-person arms hide.
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

    public bool IsThirdPerson { get; private set; }

    Transform cam;
    FirstPersonArms arms;
    CharacterAnimator anim;
    Renderer[] bodyRenderers;
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

        var punch = GetComponent<PlayerPunch>();
        if (punch) punch.Punched += () => anim.Punch(0.3f, 0.35f);

        currentDistance = distance;
        Apply(startInThirdPerson);
    }

    void Apply(bool third)
    {
        IsThirdPerson = third;
        foreach (var r in bodyRenderers) r.shadowCastingMode = third ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
        if (arms) arms.visible = !third;
    }

    void Update()
    {
        if (PlayerControls.For(gameObject).ToggleViewPressed) Apply(!IsThirdPerson);
    }

    // FirstPersonController puts the camera at eye level every Update; this pulls it back behind the shoulder.
    void LateUpdate()
    {
        if (!IsThirdPerson || !cam) return;
        var cc = GetComponent<CharacterController>();
        var fpc = GetComponent<FirstPersonController>();
        Vector3 eye = cc && fpc ? transform.position + Vector3.up * (cc.height - fpc.eyeFromTop) : cam.position;
        Vector3 offset = -cam.forward * distance + cam.right * shoulderOffset + Vector3.up * heightOffset;
        float want = offset.magnitude;
        if (Physics.SphereCast(eye, collisionRadius, offset / want, out var hit, want, ~0, QueryTriggerInteraction.Ignore))
            want = Mathf.Max(0.3f, hit.distance - 0.05f);
        // snap in when blocked, ease back out when clear
        currentDistance = want < currentDistance ? want : Mathf.MoveTowards(currentDistance, want, Time.deltaTime * 4f);
        cam.position = eye + offset.normalized * currentDistance;
    }
}
