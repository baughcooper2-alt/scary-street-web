using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Title screen → character select → the run, all in the one scene (so the house is only loaded once).
// While the menus are up, the Player is switched off and the RoundManager waits; StartGame dresses the
// player as the chosen character, switches them on and starts Round 1. Restart (R) skips the menus.
public class GameFlow : MonoBehaviour
{
    public const string SensitivityKey = "mouseSensitivity", VolumeKey = "volume";

    public static CharacterLook Chosen { get; private set; }
    static bool startImmediately;

    [Header("Title")]
    public string titleTop = "BOOGIE DOWN";
    public string titleMain = "SCARY STREET";
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
    static void ResetStatics() { Chosen = null; startImmediately = false; }

    public static void Restart()
    {
        startImmediately = Chosen != null;
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
            Dress(Chosen);                  // RoundManager starts on its own
            return;
        }
        startImmediately = false;

        if (rounds) rounds.autoStart = false;
        if (player) player.SetActive(false);
        MakeMenuCamera();
        ShowTitle();
    }

    public CharacterLook LookFor(string displayName) => playableLooks.Find(l => l && l.displayName == displayName);

    public void ShowTitle() => Swap(TitleScreen.Create(this).gameObject);
    public void ShowCharacterSelect() => Swap(CharacterSelectScreen.Create(this).gameObject);

    void Swap(GameObject next)
    {
        if (screen) Destroy(screen);
        screen = next;
    }

    public void StartGame(CharacterLook look)
    {
        Chosen = look;
        if (screen) Destroy(screen);
        if (menuCam) Destroy(menuCam.gameObject);
        Dress(look);
        if (player) player.SetActive(true);
        if (rounds) rounds.Begin();
    }

    // Put the chosen character on the player: first-person arms, third-person body, mouse sensitivity.
    void Dress(CharacterLook look)
    {
        if (!player) return;
        var arms = player.GetComponentInChildren<FirstPersonArms>(true);
        if (arms) arms.look = look;
        var body = player.GetComponent<ThirdPersonView>();
        if (body) body.look = look;
        var fpc = player.GetComponent<FirstPersonController>();
        if (fpc) fpc.mouseSensitivity = PlayerPrefs.GetFloat(SensitivityKey, fpc.mouseSensitivity);
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
