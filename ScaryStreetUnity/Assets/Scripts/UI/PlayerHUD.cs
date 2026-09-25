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
    float hurtFlash, levelFlash, cashFlash, lastHurtSound;
    bool downed;
    Texture2D white;
    GUIStyle bigStyle, cashStyle;

    void Awake()
    {
        health = GetComponent<Health>();
        health.Damaged += _ =>
        {
            hurtFlash = 1f;
            if (Time.time - lastHurtSound > 0.45f) { lastHurtSound = Time.time; SoundKit.Play(Sfx.Hurt, 0.5f); }   // fart clouds hurt every frame; don't spam
        };
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

    // Downed: stop moving. Co-op: RoundManager revives you at the end of the round if a teammate survives it.
    void OnDied()
    {
        var fpc = GetComponent<FirstPersonController>();
        if (fpc) fpc.enabled = false;
        downed = true;
        if (!Players.AnyAlive) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }

    void Update()
    {
        hurtFlash = Mathf.MoveTowards(hurtFlash, 0, Time.deltaTime * 3f);
        levelFlash = Mathf.MoveTowards(levelFlash, 0, Time.deltaTime);
        cashFlash = Mathf.MoveTowards(cashFlash, 0, Time.deltaTime * 2f);
        if (downed && !health.IsDead)                  // revived
        {
            downed = false;
            var fpc = GetComponent<FirstPersonController>();
            if (fpc) fpc.enabled = true;
        }
        if (!health.IsDead) return;
        if (Players.AnyAlive) return;                 // co-op: you're only downed while a teammate is still up
        var c = PlayerControls.For(gameObject);
        if (c.RestartPressed) GameFlow.Restart();     // same characters, straight back in
        else if (c.MenuPressed) GameFlow.BackToMenu();
    }

    void OnGUI()
    {
        var area = HudArea.For(this);
        GUI.BeginGroup(area);
        DrawHud(area.width, area.height);
        GUI.EndGroup();
    }

    void DrawHud(float W, float H)
    {
        float w = 260, h = 22, x = 20, y = H - h - 20;
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
            GUI.Label(new Rect(x + 80, xy - 26, w + 200, 24), $"LV {progress.Level}   XP {progress.Xp} / {progress.XpToNext}");
            float line = xy - 54;
            if (upgrades && upgrades.Used > 0) { GUI.Label(new Rect(x, line, 900, 24), upgrades.Summary()); line -= 24; }
            var stats = GetComponent<PlayerStats>();
            if (stats && stats.Summary().Length > 0) GUI.Label(new Rect(x, line, 900, 24), stats.Summary());

            if (levelFlash > 0 && !LevelUpScreen.IsOpen)
            {
                GUI.color = new Color(0.55f, 0.85f, 1f, Mathf.Clamp01(levelFlash));
                GUI.Label(new Rect(0, H * 0.62f, W, 50), $"LEVEL {progress.Level}!", bigStyle);
                GUI.color = old;
            }
        }

        if (hurtFlash > 0) Box(new Rect(0, 0, W, H), new Color(0.8f, 0, 0, 0.35f * hurtFlash));

        if (health.IsDead)
        {
            Box(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.6f));
            var style = new GUIStyle(GUI.skin.label) { fontSize = 32, alignment = TextAnchor.MiddleCenter };
            string msg = Players.AnyAlive ? "You're down!\nYou'll be back up when the round ends"
                                          : Players.All.Count > 1 ? "Everybody got fried.\nR / Start to restart  ·  M / Select for main menu"
                                          : "You got fried.\nR to restart  ·  M for main menu";
            GUI.Label(new Rect(0, H / 2f - 40, W, 80), msg, style);
        }
        else Box(new Rect(W / 2f - 2, H / 2f - 2, 4, 4), Color.white);   // crosshair
    }

    void Box(Rect r, Color c)
    {
        var old = GUI.color; GUI.color = c;
        GUI.DrawTexture(r, white);
        GUI.color = old;
    }
}
