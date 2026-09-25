#if ENABLE_INPUT_SYSTEM
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
#if UNITY_EDITOR
using UnityEditor;
#endif

// The GameSir G7 Pro (wired, USB-C) on macOS. The Input System only knows Xbox pads by Microsoft's product names,
// so without this the G7 shows up as a generic "Joystick" that Gamepad.all (and so the whole game) ignores.
// macOS gives it a standard Android-style HID report (Tools > Scary Street > Controller Report shows it),
// 9 bytes, no report ID:
//   bytes 0–1  buttons 1–15: A, B, C, X, Y, Z, LB, RB, LT, RT, View, Menu, Home, left stick, right stick
//   byte 2     hat switch in the low 4 bits (0 = up, clockwise, 8 = centred)
//   bytes 3–6  left stick X / Y, right stick X (Z) / Y (Rz), 0–255 centred on 128
//   bytes 7–8  left trigger (Brake), right trigger (Gas), 0–255
// The sticks and d-pad are described the same way as Unity's own PlayStation HID layouts.
[StructLayout(LayoutKind.Explicit, Size = 9)]
public struct GameSirG7HIDState : IInputStateTypeInfo
{
    public FourCC format => new FourCC('H', 'I', 'D');

    [InputControl(name = "buttonSouth", displayName = "A", bit = 0)]
    [InputControl(name = "buttonEast", displayName = "B", bit = 1)]
    [InputControl(name = "buttonWest", displayName = "X", bit = 3)]
    [InputControl(name = "buttonNorth", displayName = "Y", bit = 4)]
    [InputControl(name = "leftShoulder", displayName = "LB", bit = 6)]
    [InputControl(name = "rightShoulder", displayName = "RB", bit = 7)]
    [FieldOffset(0)] public byte buttons0;

    [InputControl(name = "leftTriggerButton", layout = "Button", displayName = "LT (digital)", bit = 0)]
    [InputControl(name = "rightTriggerButton", layout = "Button", displayName = "RT (digital)", bit = 1)]
    [InputControl(name = "select", displayName = "View", bit = 2)]
    [InputControl(name = "start", displayName = "Menu", bit = 3)]
    [InputControl(name = "systemButton", layout = "Button", displayName = "Home", bit = 4)]
    [InputControl(name = "leftStickPress", bit = 5)]
    [InputControl(name = "rightStickPress", bit = 6)]
    [FieldOffset(1)] public byte buttons1;

    [InputControl(name = "dpad", format = "BIT", layout = "Dpad", sizeInBits = 4, defaultState = 8)]
    [InputControl(name = "dpad/up", format = "BIT", layout = "DiscreteButton", parameters = "minValue=7,maxValue=1,nullValue=8,wrapAtValue=7", bit = 0, sizeInBits = 4)]
    [InputControl(name = "dpad/right", format = "BIT", layout = "DiscreteButton", parameters = "minValue=1,maxValue=3", bit = 0, sizeInBits = 4)]
    [InputControl(name = "dpad/down", format = "BIT", layout = "DiscreteButton", parameters = "minValue=3,maxValue=5", bit = 0, sizeInBits = 4)]
    [InputControl(name = "dpad/left", format = "BIT", layout = "DiscreteButton", parameters = "minValue=5,maxValue=7", bit = 0, sizeInBits = 4)]
    [FieldOffset(2)] public byte hat;

    [InputControl(name = "leftStick", layout = "Stick", format = "VC2B")]
    [InputControl(name = "leftStick/x", offset = 0, format = "BYTE", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5")]
    [InputControl(name = "leftStick/left", offset = 0, format = "BYTE", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,clamp=1,clampMin=0,clampMax=0.5,invert")]
    [InputControl(name = "leftStick/right", offset = 0, format = "BYTE", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,clamp=1,clampMin=0.5,clampMax=1")]
    [InputControl(name = "leftStick/y", offset = 1, format = "BYTE", parameters = "invert,normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5")]
    [InputControl(name = "leftStick/up", offset = 1, format = "BYTE", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,clamp=1,clampMin=0,clampMax=0.5,invert")]
    [InputControl(name = "leftStick/down", offset = 1, format = "BYTE", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,clamp=1,clampMin=0.5,clampMax=1,invert=false")]
    [FieldOffset(3)] public byte leftStickX;
    [FieldOffset(4)] public byte leftStickY;

    [InputControl(name = "rightStick", layout = "Stick", format = "VC2B")]
    [InputControl(name = "rightStick/x", offset = 0, format = "BYTE", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5")]
    [InputControl(name = "rightStick/left", offset = 0, format = "BYTE", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,clamp=1,clampMin=0,clampMax=0.5,invert")]
    [InputControl(name = "rightStick/right", offset = 0, format = "BYTE", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,clamp=1,clampMin=0.5,clampMax=1")]
    [InputControl(name = "rightStick/y", offset = 1, format = "BYTE", parameters = "invert,normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5")]
    [InputControl(name = "rightStick/up", offset = 1, format = "BYTE", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,clamp=1,clampMin=0,clampMax=0.5,invert")]
    [InputControl(name = "rightStick/down", offset = 1, format = "BYTE", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,clamp=1,clampMin=0.5,clampMax=1,invert=false")]
    [FieldOffset(5)] public byte rightStickX;
    [FieldOffset(6)] public byte rightStickY;

    [InputControl(name = "leftTrigger", format = "BYTE")]
    [FieldOffset(7)] public byte leftTrigger;

    [InputControl(name = "rightTrigger", format = "BYTE")]
    [FieldOffset(8)] public byte rightTrigger;
}

// Registered in the editor (so it takes over the already-plugged-in controller) and in builds before the
// first scene. Matched by GameSir's USB vendor / product IDs.
#if UNITY_EDITOR
[InitializeOnLoad]
#endif
[InputControlLayout(stateType = typeof(GameSirG7HIDState), displayName = "GameSir G7 Pro")]
public class GameSirG7Gamepad : Gamepad
{
    public const int VendorId = 0x3537, ProductId = 0x1022;

    static GameSirG7Gamepad()
    {
        InputSystem.RegisterLayout<GameSirG7Gamepad>(
            matches: new InputDeviceMatcher()
                .WithInterface("HID")
                .WithCapability("vendorId", VendorId)
                .WithCapability("productId", ProductId));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init() { }   // touching the class runs the static constructor above
}
#endif
