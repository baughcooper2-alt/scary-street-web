using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Free-roam testing panel: Tab (controller: Select) opens it. Spawn enemies, call the DoorDash, give yourself cash
// and levels, heal, god mode, open every door. Added by GameFlow when a free-roam run starts.
public class SandboxMenu : MonoBehaviour
{
    GameObject panel;
    Text status;
    bool god;
    FirstPersonController fpc;
    GameObject player;

    void Start()
    {
        var p = Players.Nearest(Vector3.zero, out _);
        player = p ? p.gameObject : GameObject.FindWithTag("Player");
        fpc = player ? player.GetComponent<FirstPersonController>() : null;
        Build();
        panel.SetActive(false);
    }

    void Build()
    {
        var canvas = UIKit.MakeCanvas("Sandbox", 65);
        canvas.transform.SetParent(transform, false);
        var root = canvas.transform;
        var card = UIArt.RoundPanel(root, "Card", new Color(0.06f, 0.05f, 0.05f, 0.94f), 26);
        var rt = UIKit.Place(card.rectTransform, 60, 150, 560, 800);
        card.raycastTarget = true;
        UIArt.Shadowed(card, 10f, 0.6f);
        panel = card.gameObject;

        var head = UIKit.Label(rt, "SANDBOX", 54, UIArt.Theme.Mustard, TextAnchor.MiddleLeft);
        head.font = UIArt.Display;
        UIKit.Place(head.rectTransform, 36, 22, 480, 70);
        var sub = UIKit.Label(rt, "Free roam · no rounds, no enemies unless you spawn them", 20, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleLeft);
        UIKit.Place(sub.rectTransform, 38, 88, 500, 30);
        UIArt.Stripe(rt, 36, 128, 488);

        float y = 150;
        void Row(string a, Action fa, string b, Action fb)
        {
            var ba = UIArt.Button(rt, a, 24, fa, UIArt.Theme.Ink2); UIKit.Place((RectTransform)ba.transform, 36, y, 236, 56);
            if (b != null) { var bb = UIArt.Button(rt, b, 24, fb, UIArt.Theme.Ink2); UIKit.Place((RectTransform)bb.transform, 288, y, 236, 56); }
            y += 66;
        }
        var rounds = RoundManager.Instance;
        Row("WORKER L1", () => Spawn(1, 1), "WORKER L2", () => Spawn(2, 1));
        Row("WORKER L3", () => Spawn(3, 1), "5 MIXED", () => { for (int i = 0; i < 5; i++) Spawn(UnityEngine.Random.Range(1, 4), 1); });
        Row("SPAWN JACK", () => { if (rounds) rounds.SpawnJack(); }, "CLEAR ENEMIES", () => { if (rounds) rounds.ClearEnemies(); Say("Cleared"); });
        Row("CALL DOORDASH", () => { if (rounds) rounds.CallDoorDash(); Close(); }, "OPEN ALL DOORS", OpenDoors);
        Row("+$100", () => { var pr = Get<PlayerProgress>(); if (pr) pr.AddCash(100); Say("+$100"); }, "+1 LEVEL", () => { var pr = Get<PlayerProgress>(); if (pr) pr.AddXp(pr.XpToNext - pr.Xp); Close(); });
        Row("HEAL", () => { var h = Get<Health>(); if (h) { if (h.IsDead) h.ResetHealth(h.maxHealth); else h.Heal(h.maxHealth); } Say("Healed"); }, "GOD MODE", ToggleGod);

        status = UIKit.Label(rt, "", 22, UIArt.Theme.Mustard, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIKit.Place(status.rectTransform, 38, y + 6, 480, 34);
        var close = UIArt.Button(rt, "CLOSE  (TAB)", 26, Close, UIArt.Theme.Blood);
        UIKit.Place((RectTransform)close.transform, 36, 720, 488, 58);
    }

    T Get<T>() where T : Component => player ? player.GetComponent<T>() : null;

    void Spawn(int level, int count)
    {
        var rounds = RoundManager.Instance;
        int made = 0;
        for (int i = 0; i < count; i++) if (rounds && rounds.SpawnWorker(level)) made++;
        Say(made > 0 ? $"Spawned a level {level} worker" : "Couldn't find a spot (is the NavMesh baked?)");
    }

    void OpenDoors()
    {
        var from = player ? player.transform.position : Vector3.zero;
        foreach (var d in FindObjectsByType<Door>(FindObjectsSortMode.None)) if (!d.IsOpen) d.OpenAwayFrom(from);
        Say("Doors open");
    }

    void ToggleGod()
    {
        god = !god;
        foreach (var p in Players.All) { var h = p ? p.GetComponent<Health>() : null; if (h) h.invincible = god; }
        Say(god ? "God mode ON" : "God mode off");
    }

    void Say(string msg) { if (status) status.text = msg; }

    void Open()
    {
        panel.SetActive(true);
        if (fpc) fpc.enabled = false;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        var first = panel.GetComponentInChildren<Button>();
        if (EventSystem.current && first) EventSystem.current.SetSelectedGameObject(first.gameObject);
    }

    void Close()
    {
        panel.SetActive(false);
        if (fpc) fpc.enabled = true;
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
    }

    void Update()
    {
        if (LevelUpScreen.IsOpen || DoorDashShop.IsOpen) return;
        bool toggle;
#if ENABLE_INPUT_SYSTEM
        toggle = (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame) ||
                 (Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame);
#else
        toggle = Input.GetKeyDown(KeyCode.Tab);
#endif
        if (toggle) { if (panel.activeSelf) Close(); else Open(); }
    }
}
