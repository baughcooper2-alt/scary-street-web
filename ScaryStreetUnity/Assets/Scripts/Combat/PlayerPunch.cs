using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Default weapon when your hands are empty (DESIGN.md: Fists).
// Left click / gamepad right trigger punches whatever Health is in front of the camera.
// Put this on the Player next to FirstPersonController.
public class PlayerPunch : MonoBehaviour
{
    public float damage = 10f;
    public float range = 2f;
    public float radius = 0.3f;
    public float cooldown = 0.45f;
    [Tooltip("WeaponInventory turns this off while a weapon is in your hands (empty slot = fists).")]
    public bool allowInput = true;

    public event Action Punched;          // for first-person arms / sounds

    Transform cam;
    Health self;
    CharacterController body;
    float cooldownT;

    void Awake()
    {
        var c = GetComponentInChildren<Camera>();
        cam = c ? c.transform : Camera.main.transform;
        self = GetComponent<Health>();
        body = GetComponent<CharacterController>();
    }

    void Update()
    {
        cooldownT -= Time.deltaTime;
        if (!allowInput || (self && self.IsDead)) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;   // first click just grabs the mouse

        bool pressed;
#if ENABLE_INPUT_SYSTEM
        pressed = (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                  (Gamepad.current != null && Gamepad.current.rightTrigger.wasPressedThisFrame);
#else
        pressed = Input.GetMouseButtonDown(0);
#endif
        if (!pressed || cooldownT > 0) return;
        cooldownT = cooldown;
        Punched?.Invoke();

        // from the eyes (not the camera, which may be behind us in third person) along the aim.
        // SphereCast skips colliders it starts inside, so our own CharacterController is ignored.
        Vector3 eye = body ? transform.position + Vector3.up * (body.height - 0.12f) : cam.position;
        if (Physics.SphereCast(eye, radius, cam.forward, out var hit, range, ~0, QueryTriggerInteraction.Ignore))
        {
            var target = hit.collider.GetComponentInParent<Health>();
            if (target && target != self) target.TakeDamage(damage);
        }
    }
}
