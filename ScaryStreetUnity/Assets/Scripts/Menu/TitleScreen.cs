using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Title screen over the menu camera's view of the house: title, Start / Settings / Controls.
// Settings and Controls open as cards on the right; Esc (or B) goes back. Works with mouse, keys or a controller.
public class TitleScreen : MonoBehaviour
{
    GameFlow flow;
    GameObject main, settings, controls;
    Button startButton, settingsBack, controlsBack;
    Selectable settingsFirst;

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

        // vignette around the house, ink shade on the left that fades out under the title, film grain
        var vig = UIKit.Panel(root, "Vignette", new Color(0, 0, 0, 0.8f)); vig.sprite = UIArt.Vignette();
        UIKit.Fill(vig.rectTransform);
        var shade = UIKit.Panel(root, "Shade", new Color(UIArt.Theme.Ink.r, UIArt.Theme.Ink.g, UIArt.Theme.Ink.b, 0.9f));
        shade.sprite = UIArt.HorizontalFade();
        UIKit.Place(shade.rectTransform, 0, 0, 1500, 1080);
        UIArt.Grain(root, 0.045f);

        main = UIKit.Node("Main", root).gameObject;
        UIKit.Fill((RectTransform)main.transform);
        var mt = (RectTransform)main.transform;
        UIArt.PopIn(UIArt.Tag(mt, "1–4 PLAYER CO-OP SURVIVAL", UIArt.Theme.Mustard, UIArt.Theme.Ink, 110, 118, 380, 42, 24), 0f);
        // BOOGYING DOWN ON
        // ~~MAPLE~~ STREET
        //  SCARY            <- red, horror font, scrawled under the crossed-out word like a correction
        const float size = 84;
        var top = UIKit.Label(main.transform, flow.titleIntro, (int)size, UIArt.Theme.Paper, TextAnchor.LowerLeft);
        top.font = UIArt.Display;
        UIKit.Place(UIArt.Print(top, 5f).rectTransform, 108, 160, 1200, 104);
        var line = Pivoted(UIKit.Place(UIKit.Node("TitleLine", main.transform), 108, 262, 1200, 280), new Vector2(0.1f, 0.5f));
        line.gameObject.AddComponent<UIPulse>().amount = 0.01f;
        float x = 0;
        Text Word(string text, Color color, float shadow)
        {
            var t = UIKit.Label(line, text, (int)size, color, TextAnchor.LowerLeft);
            t.font = UIArt.Display; t.horizontalOverflow = HorizontalWrapMode.Overflow;
            float w = t.preferredWidth;
            UIKit.Place(UIArt.Print(t, shadow).rectTransform, x, 0, w + 10, 104);
            x += w + size * 0.28f;
            return t;
        }
        var struck = Word(flow.titleStruck, new Color(0.95f, 0.92f, 0.87f, 0.55f), 3f);
        var end = Word(flow.titleEnd, UIArt.Theme.Paper, 5f);

        // the cross-out: a thick red slash through the struck word, a little crooked like it was done by hand
        float sw = struck.preferredWidth + 24, baseline = 104 - size * 0.2f;       // Impact's descent is ~0.2 of the size
        var slash = UIArt.Print(UIArt.RoundPanel(line, "Strike", UIArt.Theme.Blood, 4), 3f);
        var srt = Pivoted(UIKit.Place(slash.rectTransform, -12, baseline - size * 0.4f - 7, sw, 14), new Vector2(0.5f, 0.5f));
        srt.localRotation = Quaternion.Euler(0, 0, 7f);

        // SCARY underneath, tilted, starting just left of the struck word
        var scary = UIKit.Label(line, flow.titleWord, UIArt.HorrorIsReal ? 190 : 140, UIArt.Theme.Blood, TextAnchor.UpperLeft);
        scary.font = UIArt.Horror; scary.horizontalOverflow = HorizontalWrapMode.Overflow;
        var crt = Pivoted(UIKit.Place(UIArt.Print(scary, 8f).rectTransform, -6, 92, scary.preferredWidth + 20, 230), new Vector2(0.2f, 0.5f));
        crt.localRotation = Quaternion.Euler(0, 0, 4f);
        UIArt.PopIn(top, 0.05f); UIArt.PopIn(struck, 0.15f); UIArt.PopIn(end, 0.22f); UIArt.PopIn(slash, 0.4f); UIArt.PopIn(scary, 0.55f);

        var tag = UIKit.Label(main.transform, flow.tagline, 26, UIArt.Theme.Muted, TextAnchor.UpperLeft);
        UIKit.Place(tag.rectTransform, 112, 584, 560, 70);

        startButton = MenuButton(main.transform, "START", 656, () => flow.ShowCharacterSelect(), UIArt.Icon.Play, 0.3f, UIArt.Theme.Blood);
        var settingsButton = MenuButton(main.transform, "SETTINGS", 748, () => Open(settings, settingsFirst), UIArt.Icon.Gear, 0.38f, UIArt.Theme.Ink3);
        var controlsButton = MenuButton(main.transform, "CONTROLS", 840, () => Open(controls, controlsBack), UIArt.Icon.Gamepad, 0.46f, UIArt.Theme.Ink3);

        UIArt.Stripe(mt, 112, 990, 120, 8);
        var foot = UIKit.Label(main.transform, "EARLY PROTOTYPE  ·  MAC & WINDOWS", 18, UIArt.Theme.Muted, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIKit.Place(foot.rectTransform, 246, 978, 600, 32);

        settings = BuildSettings(root);
        controls = BuildControls(root);
        settings.SetActive(false);
        controls.SetActive(false);
        Select(startButton);
    }

    // Change a placed rect's pivot without moving it (UIKit.Place leaves the pivot at the top-left).
    static RectTransform Pivoted(RectTransform rt, Vector2 pivot)
    {
        var size = rt.sizeDelta;
        rt.anchoredPosition += new Vector2((pivot.x - rt.pivot.x) * size.x, (pivot.y - rt.pivot.y) * size.y);
        rt.pivot = pivot;
        return rt;
    }

    public static Button MenuButton(Transform parent, string label, float y, System.Action onClick, UIArt.Icon icon, float delay, Color color)
    {
        var b = UIArt.Button(parent, label, 40, onClick, color, icon, TextAnchor.MiddleLeft);
        var rt = UIKit.Place((RectTransform)b.transform, 110, y, 460, 76);
        rt.pivot = new Vector2(0, 0.5f); rt.anchoredPosition += new Vector2(0, -38);   // grow to the right on hover
        UIArt.PopIn(b, delay);
        return b;
    }

    // ---------- Settings ----------

    GameObject BuildSettings(Transform root)
    {
        var card = Card(root, "SETTINGS");
        settingsFirst = GameSettings.BuildRows(card, 140, 76);
        settingsBack = BackButton(card, () => Close(settings));
        return card.gameObject;
    }

    // ---------- Controls ----------

    // Controller names follow the pad that's plugged in (Xbox letters or PlayStation names).
    static string[,] ControlRows() => new string[,]
    {
        { "Move",                     "W A S D",             "Left stick" },
        { "Look",                     "Mouse",               "Right stick" },
        { "Jump / Crouch",            "Space / C or Shift",  $"{GamepadInfo.A} / {GamepadInfo.B}" },
        { "Punch · blow smoke",       "Left click",          GamepadInfo.RT },
        { "Hit the cart",             "Right click or E",    GamepadInfo.LT },
        { "Weapon slots · reload",    "1–5, wheel · R",      $"{GamepadInfo.LB} / {GamepadInfo.RB} · d-pad ↓" },
        { "Doors · DoorDash driver",  "F",                   GamepadInfo.X },
        { "Switch camera view",       "V",                   GamepadInfo.Y },
        { "Pause",                    "Esc",                 GamepadInfo.StartButton },
        { "Next round (after shop)",  "Enter",               GamepadInfo.StartButton },
        { "Game over: restart / menu","R / M",               $"{GamepadInfo.StartButton} / {GamepadInfo.SelectButton}" },
    };

    GameObject BuildControls(Transform root)
    {
        var card = Card(root, "CONTROLS");
        var rows = ControlRows();
        Header(card, "KEYBOARD & MOUSE", 300, 130);
        Header(card, "CONTROLLER", 540, 130);
        for (int i = 0; i < rows.GetLength(0); i++)
        {
            float y = 170 + i * 46;
            if (i % 2 == 0) UIKit.Place(UIArt.RoundPanel(card, "Zebra", new Color(1, 1, 1, 0.035f), 8).rectTransform, 36, y, 688, 44);
            var a = UIKit.Label(card, rows[i, 0], 22, UIArt.Theme.Paper);
            UIKit.Place(a.rectTransform, 50, y, 250, 44);
            var k = UIKit.Label(card, rows[i, 1], 20, UIArt.Theme.Mustard, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Place(k.rectTransform, 300, y, 240, 44);
            var g = UIKit.Label(card, rows[i, 2], 20, UIArt.Theme.Teal, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Place(g.rectTransform, 540, y, 200, 44);
        }
        controlsBack = BackButton(card, () => Close(controls));
        return card.gameObject;
    }

    // ---------- shared bits ----------

    // Also used by the PauseMenu's settings card.
    public static RectTransform Card(Transform root, string title)
    {
        var img = UIArt.RoundPanel(root, title, UIArt.Theme.Ink2, 18);
        var card = UIKit.Place(img.rectTransform, 1080, 150, 760, 780);
        img.raycastTarget = true;
        UIArt.Print(img, 10f);
        UIArt.Stripe(card, 0, 0, 760, 14);
        var t = UIKit.Label(card, title, 64, UIArt.Theme.Mustard, TextAnchor.MiddleLeft);
        t.font = UIArt.Display;
        UIKit.Place(UIArt.Print(t, 4f).rectTransform, 50, 30, 460, 86);
        UIArt.Tag(card, "ESC · BACK", UIArt.Theme.Ink3, UIArt.Theme.Muted, 550, 52, 160, 38, 20);
        UIKit.Place(UIKit.Panel(card, "Rule", new Color(1, 1, 1, 0.08f)).rectTransform, 50, 124, 660, 2);
        UIArt.PopIn(img, 0f);
        return card;
    }

    static void Header(RectTransform card, string label, float x, float y)
    {
        var l = UIKit.Label(card, label, 18, UIArt.Theme.Muted, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIKit.Place(l.rectTransform, x, y, 240, 36);
    }

    public static Button BackButton(RectTransform card, System.Action onClick)
    {
        var b = UIArt.Button(card, "BACK", 32, onClick, UIArt.Theme.Ink3);
        UIKit.Place((RectTransform)b.transform, 50, 690, 220, 64);
        return b;
    }

    void Open(GameObject panel, Selectable focus)
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
        if (back)
        {
            if (settings.activeSelf) Close(settings);
            else if (controls.activeSelf) Close(controls);
        }
        UIKit.KeepSelected(settings.activeSelf ? settingsFirst : controls.activeSelf ? controlsBack : startButton);
    }
}
