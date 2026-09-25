using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// The DoorDash bag, opened on the porch between rounds. Like the web build it offers 4 random items, with
// prices up 15% every round; you can reroll the offer for a few bucks. Items: DESIGN.md's upgrades
// (see PlayerUpgrades), food that heals, and the cart upgrade. The player can't move while it's open.
public class DoorDashShop : MonoBehaviour
{
    public static bool IsOpen => current;
    static DoorDashShop current;

    class Item
    {
        public string category, name, desc;
        public int price;
        public bool sold;
        public Func<string> blocked;    // non-null result = why you can't buy it right now
        public Action buy;
    }

    const int OfferSize = 4;

    GameObject player;
    DoorDashCourier courier;
    PlayerProgress wallet;
    PlayerUpgrades upgrades;
    Health health;
    WeaponInventory weapons;
    FirstPersonController fpc;
    int round, rerolls;
    readonly List<Item> offer = new List<Item>();

    RectTransform cardArea;
    Text cashText, upgradesText, noteText;
    Button rerollButton, doneButton;

    public static void Open(GameObject player, DoorDashCourier courier)
    {
        if (current) return;
        var canvas = UIKit.MakeCanvas("DoorDashShop", 60);
        var s = canvas.gameObject.AddComponent<DoorDashShop>();
        current = s;
        s.player = player; s.courier = courier;
        s.wallet = player.GetComponent<PlayerProgress>();
        s.upgrades = player.GetComponent<PlayerUpgrades>();
        s.health = player.GetComponent<Health>();
        s.weapons = player.GetComponent<WeaponInventory>();
        s.fpc = player.GetComponent<FirstPersonController>();
        s.round = RoundManager.Instance ? RoundManager.Instance.RoundNumber : 1;

        if (s.fpc) s.fpc.enabled = false;                         // hold still while you shop
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        s.RollOffer();
        s.Build(canvas.transform);
        s.Refresh();
    }

    int Cost(float basePrice) => Mathf.RoundToInt(basePrice * (1f + 0.15f * round));   // web build pricing

    // ---------- the pool ----------

    List<Item> Pool()
    {
        var list = new List<Item>();
        if (upgrades)
            foreach (var def in PlayerUpgrades.All)
            {
                var d = def;
                int lvl = upgrades.Level(d.id);
                list.Add(new Item
                {
                    category = "Upgrade",
                    name = lvl > 0 ? $"{d.name}  Lv {lvl + 1}" : d.name,
                    desc = d.desc,
                    price = Cost(d.basePrice * (1f + 0.5f * lvl)),
                    blocked = () => upgrades.CanAdd(d.id) ? null : $"Upgrade slots full ({upgrades.Used}/{upgrades.Slots})",
                    buy = () => upgrades.Add(d.id),
                });
            }

        if (health)
        {
            list.Add(new Item { category = "Food", name = "Snack", desc = "Heal 10", price = Cost(15),
                                blocked = () => health.Fraction >= 1f ? "Already at full health" : null, buy = () => health.Heal(10f) });
            list.Add(new Item { category = "Food", name = "Full meal", desc = "Heal to full", price = Cost(35),
                                blocked = () => health.Fraction >= 1f ? "Already at full health" : null, buy = () => health.Heal(health.maxHealth) });
        }

        var cart = weapons ? weapons.GetComponentInChildren<CartWeapon>() : null;
        if (cart && cart.Tier < 3)
            list.Add(new Item { category = "Weapon", name = "Cart upgrade", desc = cart.Tier == 1 ? "Unlock O-rings now" : "Unlock the Blinker now",
                                price = Cost(55), blocked = () => cart.Tier >= 3 ? "Fully upgraded" : null, buy = () => cart.UpgradeTier() });
        return list;
    }

    void RollOffer()
    {
        var pool = Pool();
        offer.Clear();
        while (offer.Count < OfferSize && pool.Count > 0)
        {
            int i = UnityEngine.Random.Range(0, pool.Count);
            offer.Add(pool[i]);
            pool.RemoveAt(i);
        }
    }

    int RerollCost => Cost(10 + 5 * rerolls);

    void Buy(Item it)
    {
        if (it.sold || it.blocked?.Invoke() != null || !wallet || !wallet.SpendCash(it.price)) return;
        it.sold = true;
        it.buy();
        SoundKit.Play(Sfx.Buy, 0.6f, 0f);
        Refresh();
    }

    void Reroll()
    {
        if (!wallet || !wallet.SpendCash(RerollCost)) return;
        rerolls++;
        RollOffer();
        animateCards = true;
        Refresh();
    }

    void Close()
    {
        if (fpc) fpc.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        current = null;
        Destroy(gameObject);
        if (courier) courier.FinishShopping(player);
    }

    // ---------- UI ----------

    static readonly Color Red = new Color(0.86f, 0.16f, 0.12f), Green = new Color(0.2f, 0.66f, 0.3f), Ink = new Color(0.07f, 0.045f, 0.05f, 0.98f);
    bool animateCards = true;

    void Build(Transform root)
    {
        UIKit.Fill(UIKit.Panel(root, "Dim", new Color(0, 0, 0, 0.5f)).rectTransform);
        var vig = UIKit.Panel(root, "Vignette", new Color(0, 0, 0, 0.8f)); vig.sprite = UIArt.Vignette();
        UIKit.Fill(vig.rectTransform);

        // the "app": rounded card sliding up
        var bag = UIArt.RoundPanel(root, "Bag", Ink, 36);
        var card = UIKit.Place(bag.rectTransform, 240, 90, 1440, 900);
        bag.raycastTarget = true;
        UIArt.Shadowed(bag, 16f, 0.7f);
        UIArt.PopIn(bag, 0f).GetComponent<UIPop>().from = 0.9f;

        var header = UIArt.RoundPanel(card, "Header", Red, 36);
        UIKit.Place(header.rectTransform, 0, 0, 1440, 130);
        UIKit.Place(UIKit.Panel(card, "HeaderEdge", Red).rectTransform, 0, 96, 1440, 34);
        var sheen = UIKit.Panel(card, "Sheen", new Color(1, 1, 1, 0.14f)); sheen.sprite = UIArt.VerticalFade();
        UIKit.Place(sheen.rectTransform, 0, 0, 1440, 130);
        var bagIcon = UIArt.IconImage(card, UIArt.Icon.Bag, Color.white);
        UIKit.Place(bagIcon.rectTransform, 44, 25, 80, 80);

        var title = UIKit.Label(card, "DOORDASH", 70, Color.white, TextAnchor.MiddleLeft);
        title.font = UIArt.Display;
        UIKit.Place(UIArt.Shadowed(title, 3f).rectTransform, 140, 14, 600, 90);
        var arms = player.GetComponentInChildren<FirstPersonArms>(true);
        string who = arms && arms.look ? arms.look.displayName : "you";
        var sub = UIKit.Label(card, $"Delivery for {who}  ·  after Round {round}", 24, new Color(1, 1, 1, 0.9f), TextAnchor.MiddleLeft);
        UIKit.Place(sub.rectTransform, 142, 88, 700, 34);

        var cashPill = UIArt.RoundPanel(card, "Cash", new Color(0, 0, 0, 0.3f), 30);
        UIKit.Place(cashPill.rectTransform, 1130, 30, 270, 70);
        var coin = UIArt.IconImage(cashPill.rectTransform, UIArt.Icon.Coin, new Color(1f, 0.85f, 0.35f));
        UIKit.Place(coin.rectTransform, 16, 13, 44, 44);
        cashText = UIKit.Label(cashPill.rectTransform, "", 46, new Color(0.7f, 1f, 0.7f), TextAnchor.MiddleRight);
        cashText.font = UIArt.Display;
        UIKit.Place(cashText.rectTransform, 64, 4, 186, 62);

        var head = UIKit.Label(card, "TODAY'S BAG", 30, new Color(1, 1, 1, 0.55f), TextAnchor.MiddleLeft);
        head.font = UIArt.Display;
        UIKit.Place(head.rectTransform, 60, 150, 600, 40);
        cardArea = UIKit.Place(UIKit.Node("Items", card), 60, 200, 1320, 500);

        upgradesText = UIKit.Label(card, "", 22, new Color(1, 1, 1, 0.6f), TextAnchor.UpperLeft);
        UIKit.Place(upgradesText.rectTransform, 60, 718, 1320, 36);
        noteText = UIKit.Label(card, "", 24, UIKit.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIKit.Place(noteText.rectTransform, 380, 800, 520, 40);

        rerollButton = UIArt.Button(card, "", 30, Reroll, new Color(0.22f, 0.14f, 0.14f), UIArt.Icon.Dice, TextAnchor.MiddleLeft);
        UIKit.Place((RectTransform)rerollButton.transform, 60, 784, 290, 76);
        doneButton = UIArt.Button(card, $"START ROUND {round + 1}", 34, Close, Red, UIArt.Icon.Play, TextAnchor.MiddleLeft);
        UIKit.Place((RectTransform)doneButton.transform, 960, 784, 420, 76);
    }

    static UIArt.Icon ItemIcon(Item it)
    {
        if (it.category == "Food") return UIArt.Icon.Burger;
        if (it.category == "Weapon") return UIArt.Icon.Cart;
        foreach (var d in PlayerUpgrades.All)
            if (it.name.StartsWith(d.name))
                return d.id switch
                {
                    PlayerUpgrades.Id.EnergyDrink => UIArt.Icon.Can, PlayerUpgrades.Id.Shooter => UIArt.Icon.Glass,
                    PlayerUpgrades.Id.Pee => UIArt.Icon.Drop, PlayerUpgrades.Id.McDonaldsBag => UIArt.Icon.Bag,
                    PlayerUpgrades.Id.CanesChicken => UIArt.Icon.Drumstick, PlayerUpgrades.Id.ToGoBox => UIArt.Icon.Box,
                    PlayerUpgrades.Id.Backpack => UIArt.Icon.Backpack, _ => UIArt.Icon.Board,
                };
        return UIArt.Icon.Star;
    }

    void Refresh()
    {
        cashText.text = wallet ? $"${wallet.Cash}" : "$0";
        for (int i = cardArea.childCount - 1; i >= 0; i--) Destroy(cardArea.GetChild(i).gameObject);

        Button first = null;
        const float w = 312, h = 500, gap = 24;
        for (int i = 0; i < offer.Count; i++)
        {
            var it = offer[i];
            string why = it.sold ? null : it.blocked?.Invoke();
            bool afford = wallet && wallet.Cash >= it.price;
            var color = it.category == "Upgrade" ? new Color(0.95f, 0.7f, 0.2f) : it.category == "Food" ? new Color(0.35f, 0.8f, 0.4f) : new Color(0.9f, 0.3f, 0.25f);

            var tileImg = UIArt.RoundPanel(cardArea, it.name, new Color(0.13f, 0.09f, 0.09f), 26);
            var tile = UIKit.Place(tileImg.rectTransform, i * (w + gap), 0, w, h);
            UIArt.Shadowed(tileImg, 8f, 0.5f);
            if (animateCards) UIArt.PopIn(tileImg, 0.1f + i * 0.08f);

            // picture area: coloured, with the icon
            var art = UIArt.RoundPanel(tile, "Art", new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.55f), 26);
            UIKit.Place(art.rectTransform, 10, 10, w - 20, 190);
            var glow = UIKit.Panel(tile, "Glow", new Color(color.r, color.g, color.b, 0.45f)); glow.sprite = UIArt.VerticalFade();
            UIKit.Place(glow.rectTransform, 10, 10, w - 20, 190);
            var icon = UIArt.IconImage(tile, ItemIcon(it), Color.white);
            UIKit.Place(UIArt.Shadowed(icon, 4f, 0.45f).rectTransform, w / 2f - 70, 35, 140, 140);
            var chip = UIArt.RoundPanel(tile, "Chip", new Color(0, 0, 0, 0.45f), 16);
            UIKit.Place(chip.rectTransform, 22, 22, 118, 34);
            var cat = UIKit.Label(chip.rectTransform, it.category.ToUpper(), 18, color, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Fill(cat.rectTransform);

            var nm = UIKit.Label(tile, it.name.ToUpper(), 34, Color.white, TextAnchor.UpperLeft);
            nm.font = UIArt.Display;
            UIKit.Place(nm.rectTransform, 22, 214, w - 44, 80);
            var ds = UIKit.Label(tile, it.desc, 23, new Color(1, 1, 1, 0.75f), TextAnchor.UpperLeft);
            UIKit.Place(ds.rectTransform, 22, 288, w - 44, 90);
            if (why != null)
            {
                var wl = UIKit.Label(tile, why, 19, new Color(1f, 0.45f, 0.4f), TextAnchor.UpperLeft, FontStyle.Italic);
                UIKit.Place(wl.rectTransform, 22, 372, w - 44, 40);
            }

            bool can = !it.sold && why == null && afford;
            string label = it.sold ? "BOUGHT" : $"${it.price}";
            var b = UIArt.Button(tile, label, 36, () => Buy(it), can ? Green : new Color(0.28f, 0.25f, 0.25f));
            UIKit.Place((RectTransform)b.transform, 22, h - 88, w - 44, 70);
            b.interactable = can;
            if (it.sold)
            {
                var stamp = UIKit.Label(tile, "BOUGHT", 64, new Color(0.35f, 0.9f, 0.45f, 0.85f), TextAnchor.MiddleCenter);
                stamp.font = UIArt.Display;
                UIKit.Place(UIKit.Outlined(stamp, new Color(0, 0.2f, 0.05f), 3f).rectTransform, 0, 60, w, 90);
                stamp.rectTransform.localRotation = Quaternion.Euler(0, 0, 14f);
                tileImg.color = new Color(0.1f, 0.12f, 0.1f);
            }
            if (can && !first) first = b;
        }
        animateCards = false;

        rerollButton.GetComponentInChildren<Text>().text = $"REROLL  ${RerollCost}";
        rerollButton.interactable = wallet && wallet.Cash >= RerollCost;
        upgradesText.text = upgrades && upgrades.Used > 0 ? "Your upgrades:  " + upgrades.Summary() : $"Your upgrades: none yet (hold up to {(upgrades ? upgrades.Slots : 5)} different ones)";
        noteText.text = offer.TrueForAll(o => o.sold) ? "Bag's empty. Reroll for more!" : "";
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject((first ? first : doneButton).gameObject);
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
        if (back) Close();
    }

    void OnDestroy() { if (current == this) current = null; }
}
