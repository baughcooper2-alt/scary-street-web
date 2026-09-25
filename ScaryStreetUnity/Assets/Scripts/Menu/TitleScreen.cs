using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Title screen over the menu camera's view of the house: title, Start / Settings / Controls.
// Settings and Controls open as cards on the right; Esc (or B) goes back.
public class TitleScreen : MonoBehaviour
{
    GameFlow flow;
    GameObject main, settings, controls;
    Button startButton, settingsBack, controlsBack;

    public static TitleScreen Create(GameFlow flow)
    {
        var canvas = UIKit.MakeCanvas("TitleScreen", 50);
        var s = canvas.gameObject.AddComponent<TitleScreen>();
        s.flow = flow;
        s.Build(canvas.transform);
        return s;
    }

    void Build(Transform root)
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // darken the left side so the text reads over the house (stacked strips fake a gradient)
        for (int i = 0; i < 6; i++)
            UIKit.Place(UIKit.Panel(root, "Shade", new Color(0, 0, 0, 0.16f)).rectTransform, 0, 0, 1100 - i * 150, 1080);

        main = UIKit.Node("Main", root).gameObject;
        UIKit.Fill((RectTransform)main.transform);
        var top = UIKit.Label(main.transform, flow.titleTop, 64, UIKit.Gold, TextAnchor.LowerLeft, FontStyle.BoldAndItalic);
        UIKit.Place(UIKit.Outlined(top, Color.black).rectTransform, 110, 120, 1200, 80);
        var title = UIKit.Label(main.transform, flow.titleMain, 150, UIKit.Blood, TextAnchor.UpperLeft, FontStyle.Bold);
        UIKit.Place(UIKit.Outlined(title, Color.black, 5f).rectTransform, 104, 196, 1500, 180);
        var tag = UIKit.Label(main.transform, flow.tagline, 30, new Color(1, 1, 1, 0.8f), TextAnchor.UpperLeft, FontStyle.Italic);
        UIKit.Place(UIKit.Outlined(tag, Color.black, 1.5f).rectTransform, 112, 380, 1200, 44);

        startButton = MenuButton(main.transform, "START", 520, () => flow.ShowCharacterSelect());
        var settingsButton = MenuButton(main.transform, "SETTINGS", 610, () => Open(settings, settingsBack));
        var controlsButton = MenuButton(main.transform, "CONTROLS", 700, () => Open(controls, controlsBack));

        var foot = UIKit.Label(main.transform, "Early prototype · Mac & Windows", 20, new Color(1, 1, 1, 0.45f), TextAnchor.LowerLeft);
        UIKit.Place(foot.rectTransform, 112, 1010, 800, 40);

        settings = BuildSettings(root);
        controls = BuildControls(root);
        settings.SetActive(false);
        controls.SetActive(false);
        Select(startButton);
    }

    Button MenuButton(Transform parent, string label, float y, System.Action onClick)
    {
        var b = UIKit.Button(parent, label, 40, onClick, TextAnchor.MiddleLeft);
        UIKit.Place((RectTransform)b.transform, 110, y, 380, 74);
        return b;
    }

    // ---------- Settings ----------

    GameObject BuildSettings(Transform root)
    {
        var card = Card(root, "SETTINGS");
        var fpcSensitivity = 0.12f;
        float y = 150;

        Row(card, "Mouse sensitivity", y);
        var sens = UIKit.Slider(card, 0.02f, 0.4f, PlayerPrefs.GetFloat(GameFlow.SensitivityKey, fpcSensitivity),
            v => { PlayerPrefs.SetFloat(GameFlow.SensitivityKey, v); PlayerPrefs.Save(); });
        UIKit.Place((RectTransform)sens.transform, 380, y + 18, 320, 30);

        y += 90;
        Row(card, "Volume", y);
        var vol = UIKit.Slider(card, 0f, 1f, PlayerPrefs.GetFloat(GameFlow.VolumeKey, 1f),
            v => { AudioListener.volume = v; PlayerPrefs.SetFloat(GameFlow.VolumeKey, v); PlayerPrefs.Save(); });
        UIKit.Place((RectTransform)vol.transform, 380, y + 18, 320, 30);

        y += 90;
        Row(card, "Fullscreen", y);
        var full = UIKit.Toggle(card, Screen.fullScreen, v => Screen.fullScreen = v);
        UIKit.Place((RectTransform)full.transform, 380, y + 14, 40, 40);

        var note = UIKit.Label(card, "More options (graphics, key rebinding) are coming later.", 22, UIKit.Dim, TextAnchor.UpperLeft, FontStyle.Italic);
        UIKit.Place(note.rectTransform, 50, y + 100, 660, 60);

        settingsBack = BackButton(card, () => Close(settings));
        return card.gameObject;
    }

    // ---------- Controls ----------

    static readonly string[,] ControlRows =
    {
        { "Move",                     "W A S D",             "Left stick" },
        { "Look",                     "Mouse",               "Right stick" },
        { "Jump / Crouch",            "Space / C or Shift",  "A / B" },
        { "Punch · blow smoke",       "Left click",          "Right trigger" },
        { "Hit the cart",             "Right click or E",    "Left trigger" },
        { "Weapon slots · reload",    "1–5, wheel · R",      "LB / RB · d-pad ↓" },
        { "Doors · DoorDash driver",  "F",                   "X" },
        { "Switch camera view",       "V",                   "Y" },
        { "Next round (after shop)",  "Enter",               "Start" },
        { "After game over",          "R restart · M menu",  "" },
    };

    GameObject BuildControls(Transform root)
    {
        var card = Card(root, "CONTROLS");
        Header(card, "KEYBOARD & MOUSE", 300, 130);
        Header(card, "CONTROLLER", 540, 130);
        for (int i = 0; i < ControlRows.GetLength(0); i++)
        {
            float y = 175 + i * 50;
            var a = UIKit.Label(card, ControlRows[i, 0], 24, Color.white);
            UIKit.Place(a.rectTransform, 50, y, 250, 44);
            var k = UIKit.Label(card, ControlRows[i, 1], 24, UIKit.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Place(k.rectTransform, 300, y, 240, 44);
            var g = UIKit.Label(card, ControlRows[i, 2], 24, UIKit.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Place(g.rectTransform, 540, y, 200, 44);
        }
        controlsBack = BackButton(card, () => Close(controls));
        return card.gameObject;
    }

    // ---------- shared bits ----------

    RectTransform Card(Transform root, string title)
    {
        var card = UIKit.Place(UIKit.Panel(root, title, new Color(0.04f, 0.02f, 0.03f, 0.92f)).rectTransform, 1080, 150, 760, 780);
        card.GetComponent<Image>().raycastTarget = true;
        UIKit.Place(UIKit.Panel(card, "Stripe", UIKit.Blood).rectTransform, 0, 0, 760, 8);
        var t = UIKit.Label(card, title, 56, UIKit.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIKit.Place(UIKit.Outlined(t, Color.black).rectTransform, 50, 30, 660, 80);
        return card;
    }

    static void Row(RectTransform card, string label, float y)
    {
        var l = UIKit.Label(card, label, 28, Color.white);
        UIKit.Place(l.rectTransform, 50, y, 320, 64);
    }

    static void Header(RectTransform card, string label, float x, float y)
    {
        var l = UIKit.Label(card, label, 20, UIKit.Dim, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIKit.Place(l.rectTransform, x, y, 240, 36);
    }

    Button BackButton(RectTransform card, System.Action onClick)
    {
        var b = UIKit.Button(card, "BACK", 32, onClick);
        UIKit.Place((RectTransform)b.transform, 50, 690, 220, 64);
        return b;
    }

    void Open(GameObject panel, Button focus)
    {
        settings.SetActive(panel == settings);
        controls.SetActive(panel == controls);
        Select(focus);
    }

    void Close(GameObject panel)
    {
        panel.SetActive(false);
        Select(startButton);
    }

    static void Select(Selectable s)
    {
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(s ? s.gameObject : null);
    }

    void Update()
    {
        bool back;
#if ENABLE_INPUT_SYSTEM
        back = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
               (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
#else
        back = Input.GetKeyDown(KeyCode.Escape);
#endif
        if (!back) return;
        if (settings.activeSelf) Close(settings);
        else if (controls.activeSelf) Close(controls);
    }
}
