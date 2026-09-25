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
    GUIStyle style;

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

        bool pressed;
#if ENABLE_INPUT_SYSTEM
        pressed = (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) ||
                  (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame);
#else
        pressed = Input.GetKeyDown(KeyCode.F);
#endif
        if (pressed && looking != null) looking.Interact(gameObject);
    }

    void OnGUI()
    {
        if (looking == null || (looking as Object) == null) return;
        if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(0, Screen.height * 0.56f, Screen.width, 30), $"F  {looking.Prompt}", style);
    }
}
