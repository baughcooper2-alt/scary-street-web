using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Look at something usable (a door, the DoorDash courier) and press F (gamepad X). Shows a prompt while you're aiming at one.
// Put this on the Player.
public class PlayerInteract : MonoBehaviour
{
    public float reach = 2.3f;

    Transform cam;
    CharacterController body;
    Health health;
    IInteractable looking;
    public string Prompt => looking != null && (looking as Object) != null ? looking.Prompt : null;

    void Start()
    {
        var c = GetComponentInChildren<Camera>();
        cam = c ? c.transform : Camera.main.transform;
        body = GetComponent<CharacterController>();
        health = GetComponent<Health>();
    }

    void Update()
    {
        looking = null;
        if (health && health.IsDead) return;

        // from the eyes (the camera may be behind us in third person); our own capsule is skipped because we start inside it
        Vector3 eye = body ? transform.position + Vector3.up * (body.height - 0.12f) : cam.position;
        if (Physics.SphereCast(eye, 0.1f, cam.forward, out var hit, reach, ~0, QueryTriggerInteraction.Ignore))
        {
            looking = hit.collider.GetComponentInParent<IInteractable>();
            if (looking != null && !looking.CanInteract) looking = null;
        }

        if (PlayerControls.For(gameObject).InteractPressed && looking != null) looking.Interact(gameObject);
    }




}
