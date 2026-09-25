using UnityEngine;
using UnityEngine.UI;

// Controller / look settings saved in PlayerPrefs, plus the settings rows shared by the title screen and the
// pause menu. Changing one mid-run applies it to the players straight away.
public static class GameSettings
{
    public const string StickSensitivityKey = "stickSensitivity", InvertYKey = "invertY", VibrationKey = "vibration";

    public static bool Vibration => PlayerPrefs.GetInt(VibrationKey, 1) == 1;

    // Unset settings leave the controller's Inspector values alone. Mouse sensitivity is only player 1's
    // (the mouse player); stick speed and invert go to everyone.
    public static void ApplyTo(FirstPersonController fpc, int playerIndex)
    {
        if (!fpc) return;
        if (playerIndex == 0) fpc.mouseSensitivity = PlayerPrefs.GetFloat(GameFlow.SensitivityKey, fpc.mouseSensitivity);
        fpc.stickSensitivity = PlayerPrefs.GetFloat(StickSensitivityKey, fpc.stickSensitivity);
        fpc.invertY = PlayerPrefs.GetInt(InvertYKey, fpc.invertY ? 1 : 0) == 1;
    }

    static void ApplyToPlayers()
    {
        foreach (var p in Players.All) if (p) ApplyTo(p, PlayerControls.For(p.gameObject).playerIndex);
    }

    static void SetFloat(string key, float v) { PlayerPrefs.SetFloat(key, v); PlayerPrefs.Save(); ApplyToPlayers(); }
    static void SetBool(string key, bool v) { PlayerPrefs.SetInt(key, v ? 1 : 0); PlayerPrefs.Save(); ApplyToPlayers(); }

    // Adds the settings rows to a card starting at y; returns the first control so the screen can select it.
    public static Selectable BuildRows(RectTransform card, float y, float rowHeight)
    {
        var first = Slider(card, "Mouse sensitivity", y, 0.02f, 0.4f, PlayerPrefs.GetFloat(GameFlow.SensitivityKey, 0.12f), v => SetFloat(GameFlow.SensitivityKey, v));
        Slider(card, "Controller look", y += rowHeight, 60f, 400f, PlayerPrefs.GetFloat(StickSensitivityKey, 160f), v => SetFloat(StickSensitivityKey, v));
        Toggle(card, "Invert look", y += rowHeight, PlayerPrefs.GetInt(InvertYKey, 0) == 1, v => SetBool(InvertYKey, v));
        Toggle(card, "Vibration", y += rowHeight, Vibration, v => SetBool(VibrationKey, v));
        Slider(card, "Volume", y += rowHeight, 0f, 1f, PlayerPrefs.GetFloat(GameFlow.VolumeKey, 1f),
            v => { AudioListener.volume = v; PlayerPrefs.SetFloat(GameFlow.VolumeKey, v); PlayerPrefs.Save(); });
        Slider(card, "Music", y += rowHeight, 0f, 1f, SoundKit.MusicVolume, v => { SoundKit.MusicVolume = v; PlayerPrefs.Save(); });
        Toggle(card, "Fullscreen", y += rowHeight, Screen.fullScreen, v => Screen.fullScreen = v);
        return first;
    }

    static Slider Slider(RectTransform card, string label, float y, float min, float max, float value, System.Action<float> onChange)
    {
        Row(card, label, y);
        var s = UIKit.Slider(card, min, max, value, onChange);
        UIKit.Place((RectTransform)s.transform, 380, y + 18, 320, 30);
        return s;
    }

    static Toggle Toggle(RectTransform card, string label, float y, bool value, System.Action<bool> onChange)
    {
        Row(card, label, y);
        var t = UIKit.Toggle(card, value, onChange);
        UIKit.Place((RectTransform)t.transform, 380, y + 14, 40, 40);
        return t;
    }

    static void Row(RectTransform card, string label, float y)
    {
        var l = UIKit.Label(card, label.ToUpper(), 26, UIArt.Theme.Paper, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIKit.Place(l.rectTransform, 50, y, 320, 64);
    }
}
