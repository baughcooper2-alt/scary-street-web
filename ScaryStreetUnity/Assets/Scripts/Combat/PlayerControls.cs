using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// One player's input. Solo: keyboard + mouse + any controller (like before).
// Co-op: player 1 keeps keyboard + mouse, player 2 gets one specific controller (GameFlow assigns it).
// Every player script reads input through this instead of the devices, so players don't steal each other's buttons.
// Everything reads as "nothing pressed" while the PauseMenu is up. Also: per-player button prompts and rumble.
// Added automatically (PlayerControls.For) if the Player doesn't have one.
public class PlayerControls : MonoBehaviour
{
    [Tooltip("0 = player 1.")]
    public int playerIndex;
    public bool useKeyboardMouse = true;
    [Tooltip("Solo play: listen to whichever controller was used last.")]
    public bool useAnyGamepad = true;

#if ENABLE_INPUT_SYSTEM
    [System.NonSerialized] public Gamepad pad;                  // a specific controller (co-op)
    // Gamepad.current can still be null right after a controller connects, so fall back to the first one.
    Gamepad MyPad => pad ?? (useAnyGamepad ? GamepadInfo.Current : null);
    Gamepad Pad => PauseMenu.InputBlocked ? null : MyPad;
    Keyboard Kb => useKeyboardMouse && !PauseMenu.InputBlocked ? Keyboard.current : null;
    Mouse Ms => useKeyboardMouse && !PauseMenu.InputBlocked ? Mouse.current : null;
#endif
    float rumbleUntil;

    public static PlayerControls For(GameObject player)
    {
        var c = player.GetComponent<PlayerControls>();
        return c ? c : player.AddComponent<PlayerControls>();
    }

    // Can this player act right now? Mouse players need the cursor captured; controller-only players always can.
    public bool Active => !useKeyboardMouse || Cursor.lockState == CursorLockMode.Locked;

    // Is this player on a controller? Co-op: fixed by their devices. Solo: whichever was touched last.
    public bool UsingGamepad => !useKeyboardMouse || (useAnyGamepad && GamepadInfo.UsingGamepad);

    // The on-screen hint for this player's device, e.g. Prompt("Left click", GamepadInfo.RT).
    public string Prompt(string keyboard, string gamepad) => UsingGamepad ? gamepad : keyboard;

    // low = the heavy left motor, high = the buzzy right one (0..1). Only on this player's controller,
    // only while they're using it, and not if Vibration is off in Settings. Stops by itself.
    public void Rumble(float low, float high, float seconds)
    {
#if ENABLE_INPUT_SYSTEM
        var p = MyPad;
        if (p == null || !UsingGamepad || !GameSettings.Vibration || PauseMenu.IsPaused) return;
        p.SetMotorSpeeds(low, high);
        rumbleUntil = Mathf.Max(rumbleUntil, Time.unscaledTime + seconds);
#endif
    }

    void StopRumble()
    {
        rumbleUntil = 0;
#if ENABLE_INPUT_SYSTEM
        MyPad?.SetMotorSpeeds(0f, 0f);
#endif
    }

    void Update() { if (rumbleUntil > 0 && (Time.unscaledTime >= rumbleUntil || PauseMenu.IsPaused)) StopRumble(); }
    void OnDisable() { if (rumbleUntil > 0) StopRumble(); }
    void OnApplicationFocus(bool focused) { if (!focused && rumbleUntil > 0) StopRumble(); }

#if ENABLE_INPUT_SYSTEM
    public Vector2 Move
    {
        get
        {
            Vector2 m = Vector2.zero;
            var kb = Kb;
            if (kb != null)
            {
                m.x = (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0);
                m.y = (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0);
            }
            if (Pad != null) m += Pad.leftStick.ReadValue();
            return Vector2.ClampMagnitude(m, 1f);
        }
    }

    // Degrees to turn this frame from the mouse.
    public Vector2 MouseLook(float mouseSensitivity) =>
        Ms != null && Cursor.lockState == CursorLockMode.Locked ? Ms.delta.ReadValue() * mouseSensitivity : Vector2.zero;

    // Raw right stick (-1..1); FirstPersonController shapes it (curve, aim assist) and turns it into degrees.
    public Vector2 LookStick => Pad != null ? Pad.rightStick.ReadValue() : Vector2.zero;

    public bool JumpPressed => (Kb?.spaceKey.wasPressedThisFrame ?? false) || (Pad?.buttonSouth.wasPressedThisFrame ?? false);
    public bool CrouchPressed => (Kb != null && (Kb.cKey.wasPressedThisFrame || Kb.leftShiftKey.wasPressedThisFrame)) || (Pad?.buttonEast.wasPressedThisFrame ?? false);
    public bool PrimaryPressed => (Ms?.leftButton.wasPressedThisFrame ?? false) || (Pad?.rightTrigger.wasPressedThisFrame ?? false);
    public bool PrimaryHeld => (Ms?.leftButton.isPressed ?? false) || (Pad?.rightTrigger.isPressed ?? false);
    public bool SecondaryHeld => (Ms?.rightButton.isPressed ?? false) || (Kb?.eKey.isPressed ?? false) || (Pad?.leftTrigger.isPressed ?? false);
    public bool ReloadPressed => (Kb?.rKey.wasPressedThisFrame ?? false) || (Pad?.dpad.down.wasPressedThisFrame ?? false);
    public bool InteractPressed => (Kb?.fKey.wasPressedThisFrame ?? false) || (Pad?.buttonWest.wasPressedThisFrame ?? false);
    public bool ToggleViewPressed => (Kb?.vKey.wasPressedThisFrame ?? false) || (Pad?.buttonNorth.wasPressedThisFrame ?? false);
    public bool RestartPressed => (Kb?.rKey.wasPressedThisFrame ?? false) || (Pad?.startButton.wasPressedThisFrame ?? false);
    public bool MenuPressed => (Kb?.mKey.wasPressedThisFrame ?? false) || (Pad?.selectButton.wasPressedThisFrame ?? false);
    // Click, or (solo) touch the controller, to grab the mouse again after it was freed.
    public bool RelockPressed => (Ms?.leftButton.wasPressedThisFrame ?? false) || (useAnyGamepad && Pad != null && GamepadInfo.Touched(Pad));

    // Weapon slot keys 1–5 (keyboard players): -1 if none this frame.
    public int SlotPressed
    {
        get
        {
            var kb = Kb; if (kb == null) return -1;
            if (kb.digit1Key.wasPressedThisFrame) return 0;
            if (kb.digit2Key.wasPressedThisFrame) return 1;
            if (kb.digit3Key.wasPressedThisFrame) return 2;
            if (kb.digit4Key.wasPressedThisFrame) return 3;
            if (kb.digit5Key.wasPressedThisFrame) return 4;
            return -1;
        }
    }

    // Mouse wheel / bumpers: -1, 0 or +1.
    public int SlotCycle
    {
        get
        {
            if (Ms != null) { float w = Ms.scroll.ReadValue().y; if (w > 0.1f) return -1; if (w < -0.1f) return 1; }
            if (Pad != null) { if (Pad.rightShoulder.wasPressedThisFrame) return 1; if (Pad.leftShoulder.wasPressedThisFrame) return -1; }
            return 0;
        }
    }
#else
    public Vector2 Move => Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
    public Vector2 MouseLook(float mouseSensitivity) =>
        Cursor.lockState == CursorLockMode.Locked ? new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 2f : Vector2.zero;
    public Vector2 LookStick => Vector2.zero;
    public bool JumpPressed => Input.GetButtonDown("Jump");
    public bool CrouchPressed => Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftShift);
    public bool PrimaryPressed => Input.GetMouseButtonDown(0);
    public bool PrimaryHeld => Input.GetMouseButton(0);
    public bool SecondaryHeld => Input.GetMouseButton(1) || Input.GetKey(KeyCode.E);
    public bool ReloadPressed => Input.GetKeyDown(KeyCode.R);
    public bool InteractPressed => Input.GetKeyDown(KeyCode.F);
    public bool ToggleViewPressed => Input.GetKeyDown(KeyCode.V);
    public bool RestartPressed => Input.GetKeyDown(KeyCode.R);
    public bool MenuPressed => Input.GetKeyDown(KeyCode.M);
    public bool RelockPressed => Input.GetMouseButtonDown(0);
    public int SlotPressed { get { for (int i = 0; i < 5; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) return i; return -1; } }
    public int SlotCycle { get { float w = Input.mouseScrollDelta.y; return w > 0.1f ? -1 : w < -0.1f ? 1 : 0; } }
#endif
}
