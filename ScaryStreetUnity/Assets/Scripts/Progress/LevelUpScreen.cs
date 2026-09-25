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

    class Choice { public string kind, title, desc; public Color color; public Action take; }

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

        UIKit.Fill(UIKit.Panel(s.root, "Dim", new Color(0, 0, 0, 0.6f)).rectTransform);
        s.NextPage();
    }

    void NextPage()
    {
        if (page) Destroy(page);
        RollChoices();

        page = UIKit.Node("Page", root).gameObject;
        UIKit.Fill((RectTransform)page.transform);
        var t = page.transform;

        var title = UIKit.Label(t, $"LEVEL {progress.Level - progress.PendingPicks + 1}!", 90, UIKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIKit.Place(UIKit.Outlined(title, Color.black, 5f).rectTransform, 0, 120, 1920, 110);
        string more = progress.PendingPicks > 1 ? $"  ({progress.PendingPicks - 1} more after this)" : "";
        var sub = UIKit.Label(t, "Pick one" + more, 30, Color.white, TextAnchor.MiddleCenter);
        UIKit.Place(sub.rectTransform, 0, 236, 1920, 44);

        Button first = null;
        float w = 400, gap = 40, x0 = (1920 - (choices.Count * w + (choices.Count - 1) * gap)) / 2f;
        for (int i = 0; i < choices.Count; i++)
        {
            var c = choices[i];
            var b = UIKit.Button(t, "", 20, () => Take(c));
            var card = UIKit.Place((RectTransform)b.transform, x0 + i * (w + gap), 320, w, 460);
            var cb = b.colors; cb.normalColor = new Color(0.1f, 0.07f, 0.08f, 0.96f); cb.highlightedColor = cb.selectedColor = new Color(0.25f, 0.12f, 0.1f); b.colors = cb;
            UIKit.Place(UIKit.Panel(card, "Edge", c.color).rectTransform, 0, 0, w, 10);
            var key = UIKit.Label(card, (i + 1).ToString(), 26, UIKit.Dim, TextAnchor.MiddleRight, FontStyle.Bold);
            UIKit.Place(key.rectTransform, w - 70, 26, 44, 36);
            var kind = UIKit.Label(card, c.kind.ToUpper(), 22, c.color, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Place(kind.rectTransform, 30, 26, 300, 36);
            var nm = UIKit.Label(card, c.title, 42, Color.white, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Place(nm.rectTransform, 30, 80, w - 60, 120);
            var ds = UIKit.Label(card, c.desc, 28, new Color(1, 1, 1, 0.8f), TextAnchor.UpperLeft);
            UIKit.Place(ds.rectTransform, 30, 220, w - 60, 200);
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
                kind = "Stat", color = new Color(0.4f, 0.75f, 1f),
                title = $"{PlayerStats.Name(stat)}  {cur} → {cur + 1}",
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
                    kind = "Weapon", color = UIKit.Blood,
                    title = $"{wpn.displayName}  Lv {wpn.level + 1}",
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
