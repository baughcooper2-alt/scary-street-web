using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Each player's HUD, built with uGUI in the house style (UIArt.Theme) and fitted to that player's part of the
// screen (whole screen solo, one half in split-screen). Reads everything it shows from the gameplay scripts:
//   bottom-left   health (segmented, with a pale drain after hits), XP + level, cash, upgrades / stats
//   bottom-centre weapon slot cards (icon, key, name / ammo) and the weapon hint
//   top           round + timer tags, workers / knockouts, boss bar, DoorDash status, announcements
//   centre        crosshair, interact prompt, weapon messages, level-up flash
//   overlays      hurt vignette, smoke haze, downed / game-over card
// Also handles going down (controls off) and R / M after a game over.
[RequireComponent(typeof(Health))]
public class PlayerHUD : MonoBehaviour
{
    Health health;
    PlayerProgress progress;
    PlayerUpgrades upgrades;
    PlayerStats stats;
    WeaponInventory weapons;
    PlayerInteract interact;
    Camera cam;
    float hurtFlash, levelFlash, cashFlash, lastHurtSound, hpShown = 1f;
    bool downed;

    Canvas canvas;
    RectTransform area, slotBar;
    Image hpFill, hpGhost, xpFill, hurt, haze, bossFill;
    Text hpText, levelText, cashText, extrasText, roundText, timerText, countText, bannerText, subText,
         promptText, toastText, hintText, downText, bossText, levelUpText, statusText;
    GameObject bossRoot, promptRoot, toastRoot, downRoot, roundRoot, crosshair;
    CanvasGroup bannerGroup;
    readonly List<(RectTransform rt, Image frame, Image icon, Text key, Text name)> slots = new List<(RectTransform, Image, Image, Text, Text)>();
    static Sprite redVignette;

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
        stats = GetComponent<PlayerStats>();
        weapons = GetComponent<WeaponInventory>();
        interact = GetComponent<PlayerInteract>();
        if (progress)
        {
            progress.LevelUp += _ => levelFlash = 2.5f;
            progress.CashGained += _ => cashFlash = 1f;
        }
    }

    void Start()
    {
        cam = GetComponentInChildren<Camera>(true);
        Build();
    }

    // The player is switched off while the menus are up; take the HUD with it.
    void OnEnable() { if (canvas) canvas.gameObject.SetActive(true); }
    void OnDisable() { if (canvas) canvas.gameObject.SetActive(false); }
    void OnDestroy() { if (canvas) Destroy(canvas.gameObject); }

    // Downed: stop moving. Co-op: RoundManager revives you at the end of the round if a teammate survives it.
    void OnDied()
    {
        var fpc = GetComponent<FirstPersonController>();
        if (fpc) fpc.enabled = false;
        downed = true;
        if (!Players.AnyAlive) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }

    // ---------- building ----------

    void Build()
    {
        canvas = UIKit.MakeCanvas($"HUD ({name})", 20);
        canvas.GetComponent<GraphicRaycaster>().enabled = false;     // nothing to click
        area = UIKit.Node("Area", canvas.transform);
        var paper = UIArt.Theme.Paper;

        // overlays
        hurt = UIKit.Panel(area, "Hurt", Color.clear); hurt.sprite = RedVignette(); UIKit.Fill(hurt.rectTransform);
        haze = UIKit.Panel(area, "Haze", Color.clear); UIKit.Fill(haze.rectTransform);

        // --- bottom-left: cash / level tags, health, xp
        var vitals = Group(area, new Vector2(0, 0), new Vector2(36, 34), new Vector2(560, 124));
        cashText = UIArt.Tag(vitals, "$0", UIArt.Theme.Ink, UIArt.Theme.Money, 0, 0, 150, 44, 30);
        levelText = UIArt.Tag(vitals, "LV 1", UIArt.Theme.Mustard, UIArt.Theme.Ink, 158, 0, 110, 44, 28);
        extrasText = UIKit.Label(vitals, "", 17, UIArt.Theme.Muted, TextAnchor.LowerLeft);
        UIKit.Place(UIArt.Print(extrasText, 2f).rectTransform, 0, -60, 900, 52);
        var hpBack = UIArt.RoundPanel(vitals, "HpBack", UIArt.Theme.Ink, 8);
        UIKit.Place(UIArt.Print(hpBack, 4f).rectTransform, 0, 56, 520, 42);
        hpGhost = Bar(hpBack.rectTransform, new Color(1f, 0.86f, 0.76f, 0.55f));
        hpFill = Bar(hpBack.rectTransform, UIArt.Theme.Blood);
        for (int i = 1; i < 5; i++) UIKit.Place(UIKit.Panel(hpBack.rectTransform, "Tick", new Color(0, 0, 0, 0.35f)).rectTransform, 4 + i * 102.4f, 4, 2, 34);
        hpText = UIKit.Label(hpBack.rectTransform, "", 24, paper, TextAnchor.MiddleLeft); hpText.font = UIArt.Display;
        UIKit.Place(UIArt.Print(hpText, 2f).rectTransform, 16, 0, 300, 42);
        var xpBack = UIArt.RoundPanel(vitals, "XpBack", UIArt.Theme.Ink, 4);
        UIKit.Place(xpBack.rectTransform, 0, 106, 520, 12);
        xpFill = Bar(xpBack.rectTransform, UIArt.Theme.Mustard, 2);

        // --- bottom-centre: weapon slots (cards are made once the inventory is known)
        slotBar = Group(area, new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(620, 150));
        hintText = UIKit.Label(slotBar, "", 18, UIArt.Theme.Muted, TextAnchor.MiddleCenter);
        UIKit.Place(UIArt.Print(hintText, 2f).rectTransform, -80, 0, 780, 28);

        // --- top: round + timer, counts, boss, DoorDash status, announcements
        var top = Group(area, new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(900, 340));
        roundRoot = Layer(top, "Round");
        var rr = (RectTransform)roundRoot.transform;
        roundText = UIArt.Tag(rr, "ROUND 1", UIArt.Theme.Mustard, UIArt.Theme.Ink, 250, 0, 230, 46, 28);
        UIArt.Print(roundText.transform.parent.GetComponent<Image>(), 4f);
        timerText = UIArt.Tag(rr, "2:00", UIArt.Theme.Ink, paper, 488, 0, 150, 46, 30);
        UIArt.Print(timerText.transform.parent.GetComponent<Image>(), 4f);
        countText = UIKit.Label(rr, "", 19, paper, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIKit.Place(UIArt.Print(countText, 2f).rectTransform, 150, 52, 600, 26);
        bossRoot = Layer(top, "Boss");
        var br = (RectTransform)bossRoot.transform;
        bossText = UIArt.Tag(br, "JACK", UIArt.Theme.Blood, paper, 140, 88, 150, 36, 24);
        var bossBack = UIArt.RoundPanel(br, "BossBack", UIArt.Theme.Ink, 6);
        UIKit.Place(UIArt.Print(bossBack, 4f).rectTransform, 298, 92, 460, 28);
        bossFill = Bar(bossBack.rectTransform, new Color(0.55f, 0.75f, 0.23f), 3);
        statusText = UIKit.Label(top, "", 22, paper, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIKit.Place(UIArt.Print(statusText, 2f).rectTransform, 0, 136, 900, 32);
        var bannerRoot = UIKit.Node("Banner", top);
        UIKit.Place(bannerRoot, 0, 176, 900, 150);
        bannerGroup = bannerRoot.gameObject.AddComponent<CanvasGroup>();
        bannerText = UIKit.Label(bannerRoot, "", 72, UIArt.Theme.Mustard, TextAnchor.MiddleCenter); bannerText.font = UIArt.Display;
        UIKit.Place(UIArt.Print(bannerText, 5f).rectTransform, 0, 0, 900, 88);
        UIArt.Stripe(bannerRoot, 300, 92, 300, 8);
        subText = UIKit.Label(bannerRoot, "", 24, paper, TextAnchor.MiddleCenter);
        UIKit.Place(UIArt.Print(subText, 2f).rectTransform, 0, 106, 900, 34);

        // --- centre: crosshair, prompt, toast, level-up
        var mid = Group(area, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 440));
        crosshair = Layer(mid, "Crosshair");
        foreach (var (x, y, w, h) in new[] { (336f, 219f, 10f, 2f), (354f, 219f, 10f, 2f), (349f, 206f, 2f, 10f), (349f, 224f, 2f, 10f) })
            UIKit.Place(UIArt.Print(UIKit.Panel(crosshair.transform, "Tick", new Color(1, 1, 1, 0.9f)), 1f).rectTransform, x, y, w, h);
        promptRoot = Layer(mid, "Prompt");
        var pr = (RectTransform)promptRoot.transform;
        UIArt.Tag(pr, "F", paper, UIArt.Theme.Ink, 170, 280, 46, 40, 26);
        promptText = UIKit.Label(pr, "", 24, paper, TextAnchor.MiddleLeft); promptText.font = UIArt.Display;
        UIKit.Place(UIArt.Print(promptText, 2f).rectTransform, 226, 280, 440, 40);
        toastRoot = Layer(mid, "Toast");
        toastText = UIArt.Tag((RectTransform)toastRoot.transform, "", UIArt.Theme.Ink, UIArt.Theme.Mustard, 60, 340, 580, 48, 26);
        UIArt.Print(toastText.transform.parent.GetComponent<Image>(), 4f);
        levelUpText = UIKit.Label(mid, "", 64, UIArt.Theme.Teal, TextAnchor.MiddleCenter); levelUpText.font = UIArt.Display;
        UIKit.Place(UIArt.Print(levelUpText, 5f).rectTransform, 0, 90, 700, 80);

        // --- downed / game over
        downRoot = Layer(area, "Down");
        UIKit.Fill(UIKit.Panel(downRoot.transform, "Dim", new Color(0, 0, 0, 0.62f)).rectTransform);
        var card = Group((RectTransform)downRoot.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 230));
        UIKit.Fill(UIArt.Print(UIArt.RoundPanel(card, "Card", UIArt.Theme.Ink2, 18), 8f).rectTransform);
        UIArt.Stripe(card, 0, 0, 760, 12);
        downText = UIKit.Label(card, "", 44, paper, TextAnchor.MiddleCenter); downText.font = UIArt.Display;
        UIKit.Place(downText.rectTransform, 20, 30, 720, 180);
        downRoot.SetActive(false);
    }

    // A box anchored to a corner / edge of this player's area; children use UIKit.Place inside it.
    static RectTransform Group(RectTransform parent, Vector2 anchor, Vector2 offset, Vector2 size)
    {
        var rt = UIKit.Node("Group", parent);
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = offset; rt.sizeDelta = size;
        return rt;
    }

    // A full-size child used to show / hide a set of widgets together.
    static GameObject Layer(RectTransform parent, string name) => UIKit.Fill(UIKit.Node(name, parent)).gameObject;

    // Fill bar that scales from the left.
    static Image Bar(RectTransform back, Color c, float inset = 4f)
    {
        var img = UIKit.Panel(back, "Fill", c);
        img.sprite = UIArt.Rounded(6); img.type = Image.Type.Sliced;
        var rt = img.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0, 0.5f);
        rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
        return img;
    }

    // Red at the edges, clear in the middle (UIArt.Vignette is black, which can't be tinted).
    static Sprite RedVignette()
    {
        if (UIArt.Alive(redVignette)) return redVignette;
        const int n = 128; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float dx = (x - n / 2f) / (n / 2f), dy = (y - n / 2f) / (n / 2f);
            tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.SmoothStep(0.35f, 1.1f, Mathf.Sqrt(dx * dx * 0.7f + dy * dy))));
        }
        tex.Apply();
        return redVignette = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
    }

    void EnsureSlots()
    {
        int n = weapons ? weapons.Slots.Count : 0;
        if (slots.Count == n) return;
        foreach (var s in slots) Destroy(s.rt.gameObject);
        slots.Clear();
        float w = 96, gap = 12, x0 = (slotBar.sizeDelta.x - (n * w + (n - 1) * gap)) / 2f;
        for (int i = 0; i < n; i++)
        {
            var frame = UIArt.RoundPanel(slotBar, "Slot", UIArt.Theme.Ink2, 12);
            var rt = UIKit.Place(UIArt.Print(frame, 4f).rectTransform, x0 + i * (w + gap), 44, w, 96);
            var icon = UIArt.IconImage(rt, UIArt.Icon.Fist, paperAlpha);
            UIKit.Place(icon.rectTransform, 24, 12, 48, 48);
            var key = UIKit.Label(rt, (i + 1).ToString(), 16, UIArt.Theme.Muted, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Place(key.rectTransform, 9, 5, 30, 20);
            var nm = UIKit.Label(rt, "", 14, UIArt.Theme.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(nm.rectTransform, 2, 66, w - 4, 24);
            slots.Add((rt, frame, icon, key, nm));
        }
    }

    static readonly Color paperAlpha = new Color(0.95f, 0.92f, 0.87f, 0.9f);

    static UIArt.Icon IconFor(Weapon w) =>
        w is CartWeapon ? UIArt.Icon.Cart : w is LawBookWeapon ? UIArt.Icon.Book : w is GuitarWeapon ? UIArt.Icon.Guitar : UIArt.Icon.Fist;

    // ---------- every frame ----------

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
        if (health.IsDead && !Players.AnyAlive)       // co-op: you're only downed while a teammate is still up
        {
            var c = PlayerControls.For(gameObject);
            if (c.RestartPressed) GameFlow.Restart();     // same characters, straight back in
            else if (c.MenuPressed) GameFlow.BackToMenu();
        }
    }

    void LateUpdate()
    {
        if (!area) return;
        // fit this player's camera rect (split-screen halves)
        var r = cam ? cam.rect : new Rect(0, 0, 1, 1);
        area.anchorMin = r.min; area.anchorMax = r.max; area.offsetMin = area.offsetMax = Vector2.zero;
        bool dead = health.IsDead;

        // vitals
        float frac = health.Fraction;
        hpShown = hpShown < frac ? frac : Mathf.MoveTowards(hpShown, frac, Time.deltaTime * 0.5f);   // pale "ghost" drains after a hit
        hpFill.rectTransform.localScale = new Vector3(frac, 1, 1);
        hpGhost.rectTransform.localScale = new Vector3(hpShown, 1, 1);
        hpFill.color = Color.Lerp(UIArt.Theme.Blood, Color.white, hurtFlash * 0.5f);
        hpText.text = $"{Mathf.CeilToInt(health.Current)} / {health.maxHealth:0}";
        if (progress)
        {
            xpFill.rectTransform.localScale = new Vector3(progress.XpFraction, 1, 1);
            levelText.text = $"LV {progress.Level}";
            cashText.text = $"${progress.Cash}";
            cashText.color = Color.Lerp(UIArt.Theme.Money, Color.white, cashFlash);
        }
        string ex = upgrades && upgrades.Used > 0 ? upgrades.Summary() : "";
        string st = stats ? stats.Summary() : "";
        extrasText.text = st.Length > 0 ? (ex.Length > 0 ? ex + "\n" + st : st) : ex;
        hurt.color = new Color(0.75f, 0.03f, 0.02f, 0.8f * hurtFlash);
        haze.color = new Color(0.86f, 0.9f, 0.86f, weapons ? weapons.haze * 0.55f : 0);

        // weapons
        EnsureSlots();
        for (int i = 0; i < slots.Count; i++)
        {
            var w = weapons.Slots[i]; bool sel = i == weapons.Selected;
            var s = slots[i];
            s.frame.color = sel ? UIArt.Theme.Mustard : new Color(0.11f, 0.09f, 0.095f, 0.88f);
            s.icon.sprite = UIArt.Get(IconFor(w));
            s.icon.color = sel ? UIArt.Theme.Ink : (w ? paperAlpha : UIArt.Theme.Muted);
            s.key.color = sel ? UIArt.Theme.Ink : UIArt.Theme.Muted;
            s.name.color = sel ? UIArt.Theme.Ink : UIArt.Theme.Paper;
            string status = w ? w.SlotStatus : "";
            s.name.text = sel && status.Length > 0 ? status : (w ? w.displayName.ToUpper() : "FISTS");
            var p = s.rt.anchoredPosition;
            s.rt.anchoredPosition = new Vector2(p.x, Mathf.MoveTowards(p.y, sel ? -32f : -44f, Time.unscaledDeltaTime * 160f));
        }
        string hint = weapons && weapons.Current ? weapons.Current.Hint : "";
        hintText.text = hint.Length > 0 ? hint : "Left click to punch";
        slotBar.gameObject.SetActive(!dead);

        // prompt, toast, level up, crosshair
        string prompt = interact ? interact.Prompt : null;
        promptRoot.SetActive(!string.IsNullOrEmpty(prompt) && !dead);
        if (promptRoot.activeSelf) promptText.text = prompt.ToUpper();
        string toast = weapons ? weapons.ToastText : null;
        toastRoot.SetActive(!string.IsNullOrEmpty(toast) && !dead);
        if (toastRoot.activeSelf) toastText.text = toast.ToUpper();
        bool flash = levelFlash > 0 && !LevelUpScreen.IsOpen && progress;
        levelUpText.text = flash ? $"LEVEL {progress.Level}!" : "";
        levelUpText.color = new Color(UIArt.Theme.Teal.r, UIArt.Theme.Teal.g, UIArt.Theme.Teal.b, Mathf.Clamp01(levelFlash));
        crosshair.SetActive(!dead);

        // round info
        var rm = RoundManager.Instance;
        bool running = rm && rm.isActiveAndEnabled && rm.CurrentState != RoundManager.State.GameOver && rm.CurrentState != RoundManager.State.Victory;
        roundRoot.SetActive(running);
        if (running)
        {
            bool free = rm.IsFreeRoam, fighting = rm.CurrentState == RoundManager.State.Fighting;
            roundText.text = free ? "FREE ROAM" : rm.Current.name.ToUpper();
            timerText.text = free ? "TAB · MENU"
                           : fighting ? $"{Mathf.FloorToInt(rm.TimeLeft / 60)}:{Mathf.FloorToInt(rm.TimeLeft % 60):00}"
                           : rm.CurrentState == RoundManager.State.Boss ? "BOSS"
                           : rm.CurrentState == RoundManager.State.Shop ? "SHOP" : "0:00";
            timerText.color = fighting && rm.TimeLeft <= 10f && Mathf.Repeat(Time.time, 0.6f) < 0.3f ? UIArt.Theme.Blood : UIArt.Theme.Paper;
            countText.text = fighting ? $"WORKERS  {rm.AliveCount}      KNOCKED OUT  {rm.KillsThisRound}"
                           : free && rm.AliveCount > 0 ? $"ENEMIES  {rm.AliveCount}" : "";
        }
        var boss = rm ? rm.BossHealth : null;
        bossRoot.SetActive(boss);
        if (boss) { bossFill.rectTransform.localScale = new Vector3(boss.Fraction, 1, 1); bossText.text = rm.BossName.ToUpper(); }

        string line = "";
        if (rm && rm.CurrentState == RoundManager.State.Shop && !DoorDashShop.IsOpen)
        {
            var courier = DoorDashCourier.Current;
            line = rm.WaitingForNextRound ? $"{rm.NextRoundName} incoming…"
                 : courier && courier.State == DoorDashCourier.Phase.Waiting ? "Your DoorDash is on the porch: walk up and press F   ·   Enter (or Start) to skip"
                 : "Your DoorDash is on the way to the front porch   ·   Enter (or Start) to skip";
        }
        statusText.text = line;
        string banner = rm ? rm.Banner : null;
        bannerGroup.alpha = banner != null ? rm.BannerAlpha : 0f;
        if (banner != null) { bannerText.text = banner.ToUpper(); subText.text = rm.SubBanner ?? ""; }

        // down / game over
        downRoot.SetActive(dead);
        if (dead)
            downText.text = Players.AnyAlive ? "YOU'RE DOWN\n<size=24>You'll be back up when the round ends</size>"
                          : Players.All.Count > 1 ? "EVERYBODY GOT FRIED\n<size=24>R / Start to restart   ·   M / Select for the main menu</size>"
                          : "YOU GOT FRIED\n<size=24>R to restart   ·   M for the main menu</size>";
    }
}
