using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Esc or Start (any player's controller) pauses a run: Resume, Settings, Restart, Main menu, all reachable with
// the stick. B (or Esc / Start again) backs out. Not while the level-up, DoorDash or sandbox screens are up
// (they have their own back button), and not in the shop, where Start skips the delivery.
// While paused every PlayerControls reads as idle. Makes its own object when play starts; nothing to set up.
// Runs before the gameplay scripts so they see InputBlocked in the same frame.
[DefaultExecutionOrder(-50)]
public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }
    // Also true on the frame you resume, so the A / click that pressed Resume doesn't jump or punch.
    public static bool InputBlocked => IsPaused || Time.frameCount == resumedFrame;
    static int resumedFrame = -1;

    GameObject ui, main, settings;
    Button resumeButton;
    Selectable settingsFirst;
    float prevTimeScale = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { IsPaused = false; resumedFrame = -1; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var go = new GameObject("PauseMenu") { hideFlags = HideFlags.HideInHierarchy };
        DontDestroyOnLoad(go);
        go.AddComponent<PauseMenu>();
    }

    void Update()
    {
        bool toggle = false, back = false;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        toggle = kb != null && kb.escapeKey.wasPressedThisFrame;
        foreach (var pad in Gamepad.all)
        {
            toggle |= pad.startButton.wasPressedThisFrame && (IsPaused || !InShop());
            back |= pad.buttonEast.wasPressedThisFrame;
        }
#else
        toggle = Input.GetKeyDown(KeyCode.Escape);
#endif
        if (!IsPaused)
        {
            if (toggle && CanPause()) Pause();
            return;
        }
        if (toggle || back)
        {
            if (settings.activeSelf) ShowSettings(false);
            else Resume();
            return;
        }
        UIKit.KeepSelected(settings.activeSelf ? settingsFirst : resumeButton);
    }

    static bool InShop() => RoundManager.Instance && RoundManager.Instance.CurrentState == RoundManager.State.Shop;

    // Only during a run (the players are switched off while the title / select screens are up), while someone is
    // still standing, and not over a screen that already stops the game.
    static bool CanPause() =>
        Players.All.Count > 0 && Players.AnyAlive && !LevelUpScreen.IsOpen && !DoorDashShop.IsOpen && !SandboxMenu.IsOpen;

    void Pause()
    {
        IsPaused = true;
        prevTimeScale = Time.timeScale > 0 ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        AudioListener.pause = true;
#if ENABLE_INPUT_SYSTEM
        InputSystem.ResetHaptics();
#endif
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Build();
        Select(resumeButton);
    }

    public void Resume()
    {
        Close();
        resumedFrame = Time.frameCount;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Close()
    {
        if (ui) Destroy(ui);
        IsPaused = false;
        Time.timeScale = prevTimeScale;
        AudioListener.pause = false;
    }

    void OnDestroy() { if (IsPaused) Close(); }

    // ---------- UI (the title screen's look) ----------

    void Build()
    {
        var canvas = UIKit.MakeCanvas("PauseMenu", 90);
        ui = canvas.gameObject;
        var root = canvas.transform;
        UIKit.Fill(UIKit.Panel(root, "Dim", new Color(UIArt.Theme.Ink.r, UIArt.Theme.Ink.g, UIArt.Theme.Ink.b, 0.72f)).rectTransform);
        var vig = UIKit.Panel(root, "Vignette", new Color(0, 0, 0, 0.8f)); vig.sprite = UIArt.Vignette();
        UIKit.Fill(vig.rectTransform);
        UIArt.Grain(root, 0.045f);

        main = UIKit.Node("Main", root).gameObject;
        var mt = UIKit.Fill((RectTransform)main.transform);
        var title = UIKit.Label(mt, "PAUSED", 120, UIArt.Theme.Paper, TextAnchor.LowerLeft);
        title.font = UIArt.Display;
        UIKit.Place(UIArt.Print(title, 6f).rectTransform, 108, 250, 900, 150);
        UIArt.Stripe(mt, 112, 410, 200, 10);
        resumeButton = TitleScreen.MenuButton(mt, "RESUME", 470, Resume, UIArt.Icon.Play, 0f, UIArt.Theme.Blood);
        TitleScreen.MenuButton(mt, "SETTINGS", 562, () => ShowSettings(true), UIArt.Icon.Gear, 0.05f, UIArt.Theme.Ink3);
        TitleScreen.MenuButton(mt, "RESTART", 654, () => { Close(); GameFlow.Restart(); }, UIArt.Icon.Dice, 0.1f, UIArt.Theme.Ink3);
        TitleScreen.MenuButton(mt, "MAIN MENU", 746, () => { Close(); GameFlow.BackToMenu(); }, UIArt.Icon.Board, 0.15f, UIArt.Theme.Ink3);
        bool pad = GamepadInfo.UsingGamepad;
        var hint = UIKit.Label(mt, pad ? $"{GamepadInfo.StartButton} OR {GamepadInfo.B} TO RESUME" : "ESC TO RESUME", 18, UIArt.Theme.Muted, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIKit.Place(hint.rectTransform, 112, 850, 600, 32);

        var card = TitleScreen.Card(root, "SETTINGS");
        settingsFirst = GameSettings.BuildRows(card, 140, 76);
        TitleScreen.BackButton(card, () => ShowSettings(false));
        settings = card.gameObject;
        settings.SetActive(false);
    }

    void ShowSettings(bool on)
    {
        settings.SetActive(on);
        foreach (var b in main.GetComponentsInChildren<Button>()) b.interactable = !on;   // keep the stick inside the card
        Select(on ? settingsFirst : resumeButton);
    }

    static void Select(Selectable s)
    {
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(s ? s.gameObject : null);
    }
}
