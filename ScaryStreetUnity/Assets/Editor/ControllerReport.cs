#if ENABLE_INPUT_SYSTEM
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;

// Tools > Scary Street > Controller Report: lists every input device Unity sees (name, layout, whether the game
// can use it as a gamepad, and its raw HID description), plus the devices Unity couldn't support, and copies
// it all to the clipboard. The HID description is what's needed to give an unrecognised controller a layout.
public static class ControllerReport
{
    [MenuItem("Tools/Scary Street/Controller Report (copies to clipboard)")]
    static void Run()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Unity {Application.unityVersion} · Input System {InputSystem.version} · {SystemInfo.operatingSystem}");
        sb.AppendLine($"Gamepads the game can use: {Gamepad.all.Count}");
        foreach (var d in InputSystem.devices)
        {
            if (d is Keyboard || d is Mouse) { sb.AppendLine($"== {d.displayName} ({d.layout})"); continue; }
            sb.AppendLine($"== {d.displayName}  layout={d.layout}  type={d.GetType().Name}  gamepad={d is Gamepad}  enabled={d.enabled}");
            Describe(sb, d.description);
        }
        var unsupported = new List<InputDeviceDescription>();
        InputSystem.GetUnsupportedDevices(unsupported);
        foreach (var desc in unsupported)
        {
            sb.AppendLine("== UNSUPPORTED");
            Describe(sb, desc);
        }

        string report = sb.ToString();
        EditorGUIUtility.systemCopyBuffer = report;
        Debug.Log(report);
        EditorUtility.DisplayDialog("Controller Report",
            $"Found {InputSystem.devices.Count} devices ({Gamepad.all.Count} usable as gamepads, {unsupported.Count} unsupported).\n\n" +
            "The full report is on your clipboard and in the Console. Paste it into the chat.", "OK");
    }

    static void Describe(StringBuilder sb, InputDeviceDescription d)
    {
        sb.AppendLine($"   interface={d.interfaceName}  class={d.deviceClass}  manufacturer={d.manufacturer}  product={d.product}  version={d.version}");
        if (!string.IsNullOrEmpty(d.capabilities)) sb.AppendLine("   capabilities=" + d.capabilities);
    }
}
#endif
