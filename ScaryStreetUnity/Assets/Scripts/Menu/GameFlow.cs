using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Title screen → character select → the run, all in the one scene (so the house is only loaded once).
// While the menus are up, the Player is switched off and the RoundManager waits; StartGame dresses the
// player as the chosen character, switches them on and starts Round 1. Restart (R) skips the menus.
// Co-op (2 picks): the Player is cloned for player 2, the screen splits left / right, player 1 keeps
// keyboard + mouse and player 2 gets the first controller.
public class GameFlow : MonoBehaviour
{
    public const string SensitivityKey = "mouseSensitivity", VolumeKey = "volume";

    public static CharacterLook Chosen => chosen.Count > 0 ? chosen[0] : null;
    public static int PlayerCount => Mathf.Max(1, chosen.Count);
    static readonly List<CharacterLook> chosen = new List<CharacterLook>();
    static bool startImmediately;
    public static bool FreeRoam { get; private set; }
    // set by the select screen: rounds keep coming after round 10 (scored on the Endless board)
    public static bool Endless { get; set; }
    public static string ChosenNames => string.Join(" & ", chosen.ConvertAll(l => l ? l.displayName : "?"));

    [Header("Title")]
    public string titleIntro = "BOOGYING DOWN ON";
    [Tooltip("Big red word, then the crossed-out one, then the last word in the intro's style.")]
    public string titleWord = "SCARY";
    public string titleStruck = "MAPLE";
    public string titleEnd = "STREET";
    public string tagline = "Survive the waves. Beat the bosses. Master the fade.";

    [Header("Characters")]
    [Tooltip("Looks that can be picked on the select screen, matched to the roster by Display Name.")]
    public List<CharacterLook> playableLooks = new List<CharacterLook>();

    [Header("Menu camera (used when there's no MenuCameraPoint in the scene)")]
    public Vector3 defaultCameraPosition = new Vector3(-1.5f, 1.7f, -14f);
    public Vector3 defaultCameraLookAt = new Vector3(-1.5f, 3.2f, 4f);

    GameObject player;
    RoundManager rounds;
    Transform menuCam;
    Vector3 camPos;
    Quaternion camRot;
    GameObject screen;          // the current menu screen's root

    // with "Enter Play Mode Options" (no domain reload) statics would survive between plays
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { chosen.Clear(); startImmediately = false; FreeRoam = false; Endless = false; }

    public static void Restart()
    {
        startImmediately = chosen.Count > 0;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public static void BackToMenu()
    {
        startImmediately = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void Awake()
    {
        player = GameObject.FindWithTag("Player");
        rounds = FindAnyObjectByType<RoundManager>();
        AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 1f);

        if (startImmediately && Chosen)
        {
            startImmediately = false;
            SetUpPlayers();                  // RoundManager starts on its own
            if (FreeRoam && rounds) { rounds.autoStart = false; rounds.BeginFreeRoam(); gameObject.AddComponent<SandboxMenu>(); }
            return;
        }
        startImmediately = false;

        if (rounds) rounds.autoStart = false;
        if (player) player.SetActive(false);
        MakeMenuCamera();
        ShowTitle();
        SoundKit.PlayMusic(MusicTrack.Menu);
    }

    public CharacterLook LookFor(string displayName) => playableLooks.Find(l => l && l.displayName == displayName);

    public void ShowTitle() => Swap(TitleScreen.Create(this).gameObject);
    public void ShowCharacterSelect() => Swap(CharacterSelectScreen.Create(this).gameObject);

    void Swap(GameObject next)
    {
        if (screen) Destroy(screen);
        screen = next;
    }

    public void StartFreeRoam(CharacterLook look)
    {
        FreeRoam = true;
        chosen.Clear(); chosen.Add(look);
        if (screen) Destroy(screen);
        if (menuCam) Destroy(menuCam.gameObject);
        SetUpPlayers();
        if (rounds) rounds.BeginFreeRoam();
        gameObject.AddComponent<SandboxMenu>();
    }

    public void StartGame(params CharacterLook[] looks)
    {
        FreeRoam = false;
        chosen.Clear();
        chosen.AddRange(looks);
        if (screen) Destroy(screen);
        if (menuCam) Destroy(menuCam.gameObject);
        SetUpPlayers();
        if (rounds) rounds.Begin();
    }

    // One Player per pick: clone the scene's Player for the others, dress them, give each their input and
    // their part of the screen, and switch them on.
    void SetUpPlayers()
    {
        if (!player) return;
        int count = PlayerCount;
        var all = new List<GameObject> { player };
        for (int i = 1; i < count; i++)
        {
            var clone = Instantiate(player, player.transform.position + player.transform.right * (1.2f * i), player.transform.rotation);
            clone.name = $"Player {i + 1}";
            all.Add(clone);
        }
        for (int i = 0; i < all.Count; i++)
        {
            Dress(all[i], chosen.Count > i ? chosen[i] : Chosen, i);
            SetUpInput(all[i], i, count);
            SetUpCamera(all[i], i, count);
            all[i].SetActive(true);
        }
    }

    // Put the chosen character on a player: first-person arms, third-person body, look settings.
    static void Dress(GameObject p, CharacterLook look, int index)
    {
        var arms = p.GetComponentInChildren<FirstPersonArms>(true);
        if (arms) arms.look = look;
        var body = p.GetComponent<ThirdPersonView>();
        if (body) body.look = look;
        GameSettings.ApplyTo(p.GetComponent<FirstPersonController>(), index);
    }

    static void SetUpInput(GameObject p, int index, int count)
    {
        var c = PlayerControls.For(p);
        c.playerIndex = index;
        if (count == 1) { c.useKeyboardMouse = true; c.useAnyGamepad = true; return; }
        c.useKeyboardMouse = index == 0;                         // P1: keyboard + mouse
        c.useAnyGamepad = false;
#if ENABLE_INPUT_SYSTEM
        int padIndex = index - 1;                                // P2: controller 1, P3: controller 2...
        c.pad = padIndex >= 0 && padIndex < Gamepad.all.Count ? Gamepad.all[padIndex] : null;
#endif
    }

    // Side by side for two players; only player 1 keeps the audio listener.
    static void SetUpCamera(GameObject p, int index, int count)
    {
        var cam = p.GetComponentInChildren<Camera>(true);
        if (!cam) return;
        cam.rect = count == 2 ? new Rect(index * 0.5f, 0, 0.5f, 1) : new Rect(0, 0, 1, 1);
        var listener = cam.GetComponent<AudioListener>();
        if (listener) listener.enabled = index == 0;
    }

    void MakeMenuCamera()
    {
        var go = new GameObject("MenuCamera", typeof(Camera), typeof(AudioListener));
        menuCam = go.transform;
        var point = FindAnyObjectByType<MenuCameraPoint>();
        if (point) { camPos = point.transform.position; camRot = point.transform.rotation; }
        else { camPos = defaultCameraPosition; camRot = Quaternion.LookRotation(defaultCameraLookAt - defaultCameraPosition); }
        menuCam.SetPositionAndRotation(camPos, camRot);
        go.GetComponent<Camera>().fieldOfView = 50f;
        UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(go.GetComponent<Camera>()).renderPostProcessing = true;
    }

    void Update()
    {
        if (!menuCam) return;
        // slow handheld drift so the title screen feels alive
        float t = Time.time;
        menuCam.SetPositionAndRotation(
            camPos + camRot * new Vector3(Mathf.Sin(t * 0.25f) * 0.08f, Mathf.Sin(t * 0.18f) * 0.04f, 0),
            camRot * Quaternion.Euler(Mathf.Sin(t * 0.21f) * 0.4f, Mathf.Sin(t * 0.13f) * 0.8f, 0));
    }
}
