using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// DESIGN.md: "XP fills your level bar; each level grants one upgrade pick" (weapon or stat).
// Pauses the game and offers 3 cards: stat points (the 7 stats) and level-ups for weapons you carry.
// Click one (or press 1 / 2 / 3). If you earned several levels at once, it keeps going until they're spent.
public class LevelUpScreen : MonoBehaviour
{
    public static bool IsOpen => current;
    static LevelUpScreen current;

    class Choice { public string kind, title, pill, desc; public Color color; public UIArt.Icon icon; public Action take; }

    PlayerStats stats;
    PlayerProgress progress;
    WeaponInventory weapons;
    FirstPersonController fpc;
    readonly List<Choice> choices = new List<Choice>();
    Transform root;
    GameObject page;
    float prevTimeScale;
    bool prevFpc;
    CursorLockMode prevLock;
    bool prevCursorVisible;

    public static void Show(PlayerStats stats)
    {
        if (current || !stats) return;                               // already open: it'll use the extra picks too
        var progress = stats.GetComponent<PlayerProgress>();
        if (!progress || progress.PendingPicks <= 0) return;

        var canvas = UIKit.MakeCanvas("LevelUp", 70);
        var s = canvas.gameObject.AddComponent<LevelUpScreen>();
        current = s;
        s.stats = stats; s.progress = progress;
        s.weapons = stats.GetComponent<WeaponInventory>();
        s.fpc = stats.GetComponent<FirstPersonController>();
        s.root = canvas.transform;

        s.prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;                                         // everything waits while you choose
        if (s.fpc) { s.prevFpc = s.fpc.enabled; s.fpc.enabled = false; }
        s.prevLock = Cursor.lockState; s.prevCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;

        UIKit.Fill(UIKit.Panel(s.root, "Dim", new Color(0, 0, 0, 0.55f)).rectTransform);
        var vig = UIKit.Panel(s.root, "Vignette", new Color(0.15f, 0.02f, 0.02f, 0.9f)); vig.sprite = UIArt.Vignette();
        UIKit.Fill(vig.rectTransform);
        UIArt.Grain(s.root, 0.04f);
        SoundKit.Play(Sfx.LevelUp, 0.7f, 0f);
        s.NextPage();
    }

    void NextPage()
    {
        if (page) Destroy(page);
        RollChoices();

        page = UIKit.Node("Page", root).gameObject;
        UIKit.Fill((RectTransform)page.transform);
        var t = page.transform;

        // starburst behind the title
        var burst = UIKit.Panel(t, "Burst", new Color(1f, 0.8f, 0.3f, 0.35f));
        burst.sprite = UIArt.Burst();
        var brt = burst.rectTransform; brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 1f); brt.sizeDelta = new Vector2(1100, 1100); brt.anchoredPosition = new Vector2(0, -175);
        burst.gameObject.AddComponent<UISpin>();

        var who = Players.All.Count > 1 ? $"PLAYER {PlayerControls.For(stats.gameObject).playerIndex + 1}  " : "";   // co-op: whose pick
        var title = UIKit.Label(t, $"{who}LEVEL {progress.Level - progress.PendingPicks + 1}!", 120, UIArt.Theme.Mustard, TextAnchor.MiddleCenter);
        title.font = UIArt.Display;
        UIKit.Place(UIArt.Print(title, 8f).rectTransform, 0, 100, 1920, 140);
        UIArt.PopIn(title, 0f);
        string more = progress.PendingPicks > 1 ? $"   ·   {progress.PendingPicks - 1} more after this" : "";
        UIArt.Stripe((RectTransform)t, 760, 236, 400, 8);
        var sub = UIKit.Label(t, "PICK ONE" + more, 30, UIArt.Theme.Paper, TextAnchor.MiddleCenter);
        sub.font = UIArt.Display;
        UIKit.Place(UIArt.Print(sub, 3f).rectTransform, 0, 250, 1920, 44);

        Button first = null;
        float w = 420, h = 520, gap = 44, x0 = (1920 - (choices.Count * w + (choices.Count - 1) * gap)) / 2f;
        for (int i = 0; i < choices.Count; i++)
        {
            var c = choices[i];
            var b = UIKit.Button(t, "", 20, () => Take(c));
            var card = UIKit.Place((RectTransform)b.transform, x0 + i * (w + gap), 310, w, h);
            card.pivot = new Vector2(0.5f, 0.5f); card.anchoredPosition += new Vector2(w / 2f, -h / 2f);   // lift from the centre
            var img = b.GetComponent<Image>(); img.sprite = UIArt.Rounded(28); img.type = Image.Type.Sliced;
            var cb = b.colors; cb.normalColor = UIArt.Theme.Ink2; cb.highlightedColor = cb.selectedColor = UIArt.Theme.Ink3; b.colors = cb;
            UIArt.Print(img, 10f);
            b.gameObject.AddComponent<UIHover>().lift = 1.07f;
            UIArt.PopIn(b, 0.12f + i * 0.09f);

            var band = UIArt.RoundPanel(card, "Band", c.color, 28);
            UIKit.Place(band.rectTransform, 0, 0, w, 190);
            UIKit.Place(UIKit.Panel(card, "BandEdge", c.color).rectTransform, 0, 160, w, 30);
            var glow = UIKit.Panel(card, "Glow", new Color(1, 1, 1, 0.18f)); glow.sprite = UIArt.VerticalFade();
            UIKit.Place(glow.rectTransform, 0, 0, w, 190);
            var icon = UIArt.IconImage(card, c.icon, Color.white);
            UIKit.Place(UIArt.Shadowed(icon, 4f, 0.4f).rectTransform, w / 2f - 65, 28, 130, 130);

            var key = UIArt.RoundPanel(card, "Key", new Color(0, 0, 0, 0.35f), 20);
            UIKit.Place(key.rectTransform, w - 64, 18, 44, 44);
            var keyText = UIKit.Label(key.rectTransform, (i + 1).ToString(), 26, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Fill(keyText.rectTransform);
            var kind = UIKit.Label(card, c.kind.ToUpper(), 20, new Color(1, 1, 1, 0.85f), TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Place(kind.rectTransform, 22, 24, 200, 30);

            var nm = UIKit.Label(card, c.title, 44, Color.white, TextAnchor.MiddleCenter);
            nm.font = UIArt.Display;
            UIKit.Place(nm.rectTransform, 20, 206, w - 40, 60);
            var pill = UIArt.RoundPanel(card, "Pill", new Color(c.color.r, c.color.g, c.color.b, 0.25f), 20);
            UIKit.Place(pill.rectTransform, w / 2f - 90, 272, 180, 44);
            var pillText = UIKit.Label(pill.rectTransform, c.pill, 26, c.color, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Fill(pillText.rectTransform);
            var ds = UIKit.Label(card, c.desc, 27, new Color(1, 1, 1, 0.82f), TextAnchor.UpperCenter);
            UIKit.Place(ds.rectTransform, 34, 340, w - 68, 160);
            if (!first) first = b;
        }
        if (EventSystem.current && first) EventSystem.current.SetSelectedGameObject(first.gameObject);
    }

    void RollChoices()
    {
        var pool = new List<Choice>();
        foreach (PlayerStats.Stat s in Enum.GetValues(typeof(PlayerStats.Stat)))
        {
            var stat = s;
            int cur = stats.Get(stat);
            pool.Add(new Choice
            {
                kind = "Stat", color = new Color(0.3f, 0.62f, 0.95f), icon = StatIcon(stat),
                title = PlayerStats.Name(stat).ToUpper(), pill = $"{cur}  →  {cur + 1}",
                desc = PlayerStats.Describe(stat),
                take = () => stats.Add(stat),
            });
        }
        if (weapons)
            foreach (var w in weapons.Slots)
            {
                if (!w || !w.CanLevelUp) continue;
                var wpn = w;
                pool.Add(new Choice
                {
                    kind = "Weapon", color = new Color(0.85f, 0.2f, 0.15f), icon = WeaponIcon(wpn),
                    title = wpn.displayName.ToUpper(), pill = $"LV {wpn.level}  →  {wpn.level + 1}",
                    desc = wpn.LevelUpText,
                    take = () => wpn.LevelUp(),
                });
            }

        choices.Clear();
        while (choices.Count < 3 && pool.Count > 0)
        {
            int i = UnityEngine.Random.Range(0, pool.Count);
            choices.Add(pool[i]);
            pool.RemoveAt(i);
        }
    }

    static UIArt.Icon StatIcon(PlayerStats.Stat s) => s switch
    {
        PlayerStats.Stat.Health => UIArt.Icon.Heart, PlayerStats.Stat.Strength => UIArt.Icon.Fist,
        PlayerStats.Stat.Precision => UIArt.Icon.Crosshair, PlayerStats.Stat.Speed => UIArt.Icon.Bolt,
        PlayerStats.Stat.Luck => UIArt.Icon.Clover, PlayerStats.Stat.Defense => UIArt.Icon.Shield, _ => UIArt.Icon.Star,
    };

    static UIArt.Icon WeaponIcon(Weapon w) => w is LawBookWeapon ? UIArt.Icon.Book : w is GuitarWeapon ? UIArt.Icon.Guitar : w is CartWeapon ? UIArt.Icon.Cart : UIArt.Icon.Fist;

    void Take(Choice c)
    {
        if (!progress.UsePick()) { Close(); return; }
        c.take();
        if (progress.PendingPicks > 0) NextPage();
        else Close();
    }

    void Close()
    {
        Time.timeScale = prevTimeScale > 0 ? prevTimeScale : 1f;
        if (fpc) fpc.enabled = prevFpc;
        Cursor.lockState = prevLock; Cursor.visible = prevCursorVisible;
        current = null;
        Destroy(gameObject);
    }

    void Update()
    {
        int pick = -1;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.digit1Key.wasPressedThisFrame) pick = 0;
            else if (kb.digit2Key.wasPressedThisFrame) pick = 1;
            else if (kb.digit3Key.wasPressedThisFrame) pick = 2;
        }
#else
        if (Input.GetKeyDown(KeyCode.Alpha1)) pick = 0; else if (Input.GetKeyDown(KeyCode.Alpha2)) pick = 1; else if (Input.GetKeyDown(KeyCode.Alpha3)) pick = 2;
#endif
        if (pick >= 0 && pick < choices.Count) Take(choices[pick]);
    }

    void OnDestroy() { if (current == this) { current = null; if (Time.timeScale == 0f) Time.timeScale = 1f; } }
}
