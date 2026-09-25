using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// First-person player for Scary Street: walk (WASD / left stick), look (mouse / right stick),
// jump (Space / A), crouch toggle (C or Shift / B). Put this on the Player object;
// the Main Camera should be a child of the Player. Esc / Start opens the PauseMenu, which frees the mouse.
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 5.2f;
    public float crouchSpeed = 2.6f;
    public float jumpHeight = 1.0f;
    public float gravity = -20f;

    [Header("Look")]
    public float mouseSensitivity = 0.12f;
    public float maxLookAngle = 85f;
    public bool invertY;

    [Header("Controller look")]
    [Tooltip("Degrees per second with the right stick pushed all the way.")]
    public float stickSensitivity = 160f;
    [Tooltip("1 = linear. Higher gives finer aim near the center of the stick and full speed at the edge.")]
    public float stickCurve = 1.8f;
    [Tooltip("Up/down turns this much slower than left/right.")]
    [Range(0.3f, 1f)] public float stickVerticalScale = 0.7f;
    [Tooltip("Aim assist: stick turn speed while the crosshair is on an enemy (1 = off).")]
    [Range(0.2f, 1f)] public float aimAssistSlowdown = 0.55f;
    public float aimAssistRange = 25f;
    public float aimAssistRadius = 0.4f;

    [Header("Body")]
    public float standHeight = 1.8f;
    public float crouchHeight = 1.1f;
    public float eyeFromTop = 0.12f;

    CharacterController cc;

    // for the third-person body's animation
    public bool IsCrouching => crouching;
    public bool IsGrounded => cc && cc.isGrounded;
    public float Pitch => pitch;
    PlayerControls controls;
    Health self;
    Transform cam;
    float pitch, verticalVel, currentHeight;
    bool crouching;

    void Awake()
    {
        // A copy of this on the camera itself applies gravity to the camera every frame, so the view
        // slowly sinks. Only the Player root should drive movement.
        if (GetComponent<Camera>() && transform.parent && transform.parent.GetComponentInParent<FirstPersonController>())
        {
            Debug.LogWarning($"{name}: extra FirstPersonController on the camera; disabling it. Remove it (and its CharacterController) from the camera.", this);
            enabled = false;
            if (TryGetComponent<CharacterController>(out var stray)) stray.enabled = false;
            return;
        }

        cc = GetComponent<CharacterController>();
        self = GetComponent<Health>();
        cam = GetComponentInChildren<Camera>() ? GetComponentInChildren<Camera>().transform : Camera.main.transform;
        cc.height = currentHeight = standHeight;
        cc.center = new Vector3(0, standHeight / 2f, 0);
        cc.radius = 0.3f;
        cc.stepOffset = 0.35f;
        cc.slopeLimit = 50f;
    }

    void OnEnable() => Players.All.Add(this);
    void OnDisable() => Players.All.Remove(this);

    // Set by attacks like Jack's jokes: stunned = can't move or jump; slowed = half speed.
    [System.NonSerialized] public float stunnedUntil, slowedUntil;
    [System.NonSerialized] public float dizzy;                    // 0..1: the camera sways (beer); wears off on its own

    void Start()
    {
        controls = PlayerControls.For(gameObject);
        if (controls.useKeyboardMouse) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
    }

    void Update()
    {
        // input comes from this player's own devices (co-op: P1 keyboard + mouse, P2 a controller)
        if (!controls) controls = PlayerControls.For(gameObject);
        Vector2 move = controls.Move, look = controls.MouseLook(mouseSensitivity) + StickLook(controls.LookStick);
        bool jump = controls.JumpPressed, crouchPressed = controls.CrouchPressed;

        // cursor (mouse player only): if the mouse got freed (alt-tab), click or touch the controller to grab it again
        if (controls.useKeyboardMouse)
        {
            if (controls.RelockPressed && Cursor.lockState != CursorLockMode.Locked) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            if (Cursor.lockState != CursorLockMode.Locked) look = Vector2.zero;
        }
        if (invertY) look.y = -look.y;

        // look
        transform.Rotate(0f, look.x, 0f);
        pitch = Mathf.Clamp(pitch - look.y, -maxLookAngle, maxLookAngle);
        dizzy = Mathf.MoveTowards(dizzy, 0f, Time.deltaTime * 0.04f);
        float sway = dizzy > 0 ? Mathf.Sin(Time.time * 1.1f) * 3.5f * dizzy : 0f, roll = dizzy > 0 ? Mathf.Sin(Time.time * 0.8f + 1f) * 7f * dizzy : 0f;
        cam.localRotation = Quaternion.Euler(pitch + sway, 0f, roll);

        // crouch (smoothly shrink the capsule and lower the eyes)
        if (crouchPressed) crouching = !crouching;
        float targetHeight = crouching ? crouchHeight : standHeight;
        if (!crouching && currentHeight < standHeight - 0.01f &&
            Physics.SphereCast(transform.position + Vector3.up * currentHeight, cc.radius * 0.9f, Vector3.up, out _, standHeight - currentHeight))
            targetHeight = currentHeight;                                   // something overhead: stay crouched
        currentHeight = Mathf.MoveTowards(currentHeight, targetHeight, 4f * Time.deltaTime);
        cc.height = currentHeight; cc.center = new Vector3(0, currentHeight / 2f, 0);
        cam.localPosition = new Vector3(0, currentHeight - eyeFromTop, 0);

        // walk + gravity + jump
        move = Vector2.ClampMagnitude(move, 1f);
        Vector3 dir = transform.right * move.x + transform.forward * move.y;
        float speed = crouching ? crouchSpeed : walkSpeed;
        if (Time.time < stunnedUntil) { dir = Vector3.zero; jump = false; }
        else if (Time.time < slowedUntil) speed *= 0.5f;
        if (cc.isGrounded && verticalVel < 0) verticalVel = -2f;
        if (jump && cc.isGrounded && !crouching) verticalVel = Mathf.Sqrt(jumpHeight * -2f * gravity);
        verticalVel += gravity * Time.deltaTime;
        cc.Move((dir * speed + Vector3.up * verticalVel) * Time.deltaTime);
    }

    // Right stick → degrees this frame: response curve, slower vertical, slowed down over enemies.
    Vector2 StickLook(Vector2 stick)
    {
        float m = stick.magnitude;
        if (m < 0.001f) return Vector2.zero;
        stick = stick / m * Mathf.Pow(Mathf.Min(m, 1f), stickCurve);
        stick.y *= stickVerticalScale;
        if (aimAssistSlowdown < 1f && OnEnemy()) stick *= aimAssistSlowdown;
        return stick * stickSensitivity * Time.deltaTime;
    }

    // Is the crosshair on something that can be hurt? Cast from the eyes like PlayerPunch (works in third person),
    // so our own capsule is skipped and walls block it.
    bool OnEnemy()
    {
        Vector3 eye = transform.position + Vector3.up * (currentHeight - eyeFromTop);
        if (!Physics.SphereCast(eye, aimAssistRadius, cam.forward, out var hit, aimAssistRange, ~0, QueryTriggerInteraction.Ignore)) return false;
        var h = hit.collider.GetComponentInParent<Health>();
        return h && h != self && !h.IsDead && !h.GetComponent<FirstPersonController>();   // not a teammate
    }
}
