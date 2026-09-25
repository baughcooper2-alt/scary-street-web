using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Temporary test HUD: HP bar, cash, level + XP bar, crosshair, red flash when hit, and game over (R restarts).
// Uses IMGUI so it needs no Canvas setup; replace with a real UI later.
[RequireComponent(typeof(Health))]
public class PlayerHUD : MonoBehaviour
{
    Health health;
    PlayerProgress progress;
    PlayerUpgrades upgrades;
    float hurtFlash, levelFlash, cashFlash;
    Texture2D white;
    GUIStyle bigStyle, cashStyle;

    void Awake()
    {
        health = GetComponent<Health>();
        health.Damaged += _ => hurtFlash = 1f;
        health.Died += OnDied;
        progress = GetComponent<PlayerProgress>();
        upgrades = GetComponent<PlayerUpgrades>();
        if (progress)
        {
            progress.LevelUp += _ => levelFlash = 2.5f;
            progress.CashGained += _ => cashFlash = 1f;
        }
        white = Texture2D.whiteTexture;
    }

    void OnDied()
    {
        var fpc = GetComponent<FirstPersonController>();
        if (fpc) fpc.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        hurtFlash = Mathf.MoveTowards(hurtFlash, 0, Time.deltaTime * 3f);
        levelFlash = Mathf.MoveTowards(levelFlash, 0, Time.deltaTime);
        cashFlash = Mathf.MoveTowards(cashFlash, 0, Time.deltaTime * 2f);
        if (!health.IsDead) return;
        bool restart;
#if ENABLE_INPUT_SYSTEM
        restart = Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
        restart = Input.GetKeyDown(KeyCode.R);
#endif
        bool menu;
#if ENABLE_INPUT_SYSTEM
        menu = Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame;
#else
        menu = Input.GetKeyDown(KeyCode.M);
#endif
        if (restart) GameFlow.Restart();              // same character, straight back in
        else if (menu) GameFlow.BackToMenu();
    }

    void OnGUI()
    {
        float w = 260, h = 22, x = 20, y = Screen.height - h - 20;
        Box(new Rect(x - 2, y - 2, w + 4, h + 4), new Color(0, 0, 0, 0.6f));
        Box(new Rect(x, y, w * health.Fraction, h), new Color(0.85f, 0.15f, 0.12f));
        GUI.Label(new Rect(x + 8, y + 2, w, h), $"HP {Mathf.CeilToInt(health.Current)} / {health.maxHealth:0}");

        if (progress)
        {
            if (bigStyle == null)
            {
                bigStyle = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                cashStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            }
            // XP bar just above HP, cash and level above that
            float xy = y - 14;
            Box(new Rect(x - 2, xy - 2, w + 4, 10), new Color(0, 0, 0, 0.6f));
            Box(new Rect(x, xy, w * progress.XpFraction, 6), new Color(0.35f, 0.75f, 1f));
            var old = GUI.color;
            GUI.color = Color.Lerp(Color.white, new Color(0.45f, 1f, 0.45f), cashFlash);
            GUI.Label(new Rect(x, xy - 30, w, 28), $"${progress.Cash}", cashStyle);
            GUI.color = old;
            string picks = progress.PendingPicks > 0 ? $"   ({progress.PendingPicks} upgrade pick{(progress.PendingPicks > 1 ? "s" : "")} saved)" : "";
            GUI.Label(new Rect(x + 80, xy - 26, w + 200, 24), $"LV {progress.Level}   XP {progress.Xp} / {progress.XpToNext}{picks}");
            if (upgrades && upgrades.Used > 0) GUI.Label(new Rect(x, xy - 54, 700, 24), upgrades.Summary());

            if (levelFlash > 0)
            {
                GUI.color = new Color(0.55f, 0.85f, 1f, Mathf.Clamp01(levelFlash));
                GUI.Label(new Rect(0, Screen.height * 0.62f, Screen.width, 50), $"LEVEL {progress.Level}!", bigStyle);
                GUI.Label(new Rect(0, Screen.height * 0.62f + 42, Screen.width, 24), "Upgrade picks are saved until the upgrade screen exists", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
                GUI.color = old;
            }
        }

        if (hurtFlash > 0) Box(new Rect(0, 0, Screen.width, Screen.height), new Color(0.8f, 0, 0, 0.35f * hurtFlash));

        if (health.IsDead)
        {
            Box(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, 0.6f));
            var style = new GUIStyle(GUI.skin.label) { fontSize = 32, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(0, Screen.height / 2f - 40, Screen.width, 80), "You got fried.\nR to restart  ·  M for main menu", style);
        }
        else Box(new Rect(Screen.width / 2f - 2, Screen.height / 2f - 2, 4, 4), Color.white);   // crosshair
    }

    void Box(Rect r, Color c)
    {
        var old = GUI.color; GUI.color = c;
        GUI.DrawTexture(r, white);
        GUI.color = old;
    }
}
