using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Which device was touched last (so solo prompts can say "RT" instead of "Left click") and the button
// names for the pad in your hands (Xbox letters, or PlayStation names on a DualShock / DualSense).
// Per-player prompts go through PlayerControls.Prompt, which uses this for the solo player.
// Makes its own hidden object when play starts; nothing to set up in the scene.
public class GamepadInfo : MonoBehaviour
{
    public static bool UsingGamepad { get; private set; }

#if ENABLE_INPUT_SYSTEM
    // The last controller used, or the first one plugged in if none has been touched yet.
    public static Gamepad Current => Gamepad.current ?? (Gamepad.all.Count > 0 ? Gamepad.all[0] : null);
    public static bool IsPlayStation => Current != null && InputSystem.IsFirstLayoutBasedOnSecond(Current.layout, "DualShockGamepad");
#else
    public const bool IsPlayStation = false;
#endif

    public static string A => IsPlayStation ? "Cross" : "A";
    public static string B => IsPlayStation ? "Circle" : "B";
    public static string X => IsPlayStation ? "Square" : "X";
    public static string Y => IsPlayStation ? "Triangle" : "Y";
    public static string LB => IsPlayStation ? "L1" : "LB";
    public static string RB => IsPlayStation ? "R1" : "RB";
    public static string LT => IsPlayStation ? "L2" : "LT";
    public static string RT => IsPlayStation ? "R2" : "RT";
    public static string StartButton => IsPlayStation ? "Options" : "Start";
    public static string SelectButton => IsPlayStation ? "Share" : "Select";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => UsingGamepad = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var go = new GameObject("GamepadInfo") { hideFlags = HideFlags.HideInHierarchy };
        DontDestroyOnLoad(go);
        go.AddComponent<GamepadInfo>();
    }

#if ENABLE_INPUT_SYSTEM
    void Update()
    {
        foreach (var pad in Gamepad.all) if (Touched(pad)) UsingGamepad = true;
        var kb = Keyboard.current; var mouse = Mouse.current;
        if ((kb != null && kb.anyKey.wasPressedThisFrame) ||
            (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.delta.ReadValue().sqrMagnitude > 25f)))
            UsingGamepad = false;
    }

    // Anything pressed or pushed past the stick deadzone.
    public static bool Touched(Gamepad p) =>
        p.leftStick.ReadValue().sqrMagnitude > 0.25f || p.rightStick.ReadValue().sqrMagnitude > 0.25f ||
        p.buttonSouth.isPressed || p.buttonEast.isPressed || p.buttonWest.isPressed || p.buttonNorth.isPressed ||
        p.leftShoulder.isPressed || p.rightShoulder.isPressed || p.leftTrigger.isPressed || p.rightTrigger.isPressed ||
        p.startButton.isPressed || p.selectButton.isPressed || p.dpad.ReadValue().sqrMagnitude > 0.25f;
#endif
}
