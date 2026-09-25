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
        Refresh();
    }

    void Close()
    {
        if (fpc) fpc.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        current = null;
        Destroy(gameObject);
        if (courier) courier.FinishShopping();
    }

    // ---------- UI ----------

    void Build(Transform root)
    {
        UIKit.Fill(UIKit.Panel(root, "Dim", new Color(0, 0, 0, 0.55f)).rectTransform);
        var card = UIKit.Place(UIKit.Panel(root, "Bag", new Color(0.06f, 0.03f, 0.035f, 0.97f)).rectTransform, 260, 110, 1400, 860);
        card.GetComponent<Image>().raycastTarget = true;
        UIKit.Place(UIKit.Panel(card, "Band", new Color(0.78f, 0.13f, 0.1f)).rectTransform, 0, 0, 1400, 110);

        var title = UIKit.Label(card, "DOORDASH", 64, Color.white, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
        UIKit.Place(UIKit.Outlined(title, new Color(0.3f, 0.02f, 0.02f)).rectTransform, 50, 14, 700, 84);
        string who = GameFlow.Chosen ? GameFlow.Chosen.displayName : "you";
        var sub = UIKit.Label(card, $"Delivery for {who} · after Round {round}", 24, new Color(1, 1, 1, 0.85f), TextAnchor.MiddleLeft);
        UIKit.Place(sub.rectTransform, 380, 34, 600, 50);
        cashText = UIKit.Outlined(UIKit.Label(card, "", 52, new Color(0.55f, 1f, 0.55f), TextAnchor.MiddleRight, FontStyle.Bold), Color.black);
        UIKit.Place(cashText.rectTransform, 950, 14, 400, 84);

        cardArea = UIKit.Place(UIKit.Node("Items", card), 50, 150, 1300, 470);

        upgradesText = UIKit.Label(card, "", 22, UIKit.Dim, TextAnchor.UpperLeft);
        UIKit.Place(upgradesText.rectTransform, 50, 650, 1300, 60);
        noteText = UIKit.Label(card, "", 24, UIKit.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIKit.Place(noteText.rectTransform, 50, 720, 800, 40);

        rerollButton = UIKit.Button(card, "", 28, Reroll);
        UIKit.Place((RectTransform)rerollButton.transform, 50, 770, 300, 64);
        doneButton = UIKit.Button(card, $"DONE: START ROUND {round + 1}", 30, Close);
        UIKit.Place((RectTransform)doneButton.transform, 890, 770, 460, 64);
        var cb = doneButton.colors; cb.normalColor = new Color(0.78f, 0.13f, 0.1f); doneButton.colors = cb;
    }

    void Refresh()
    {
        cashText.text = wallet ? $"${wallet.Cash}" : "$0";
        for (int i = cardArea.childCount - 1; i >= 0; i--) Destroy(cardArea.GetChild(i).gameObject);

        Button first = null;
        for (int i = 0; i < offer.Count; i++)
        {
            var it = offer[i];
            string why = it.sold ? null : it.blocked?.Invoke();
            bool afford = wallet && wallet.Cash >= it.price;
            var color = it.category == "Upgrade" ? UIKit.Gold : it.category == "Food" ? new Color(0.45f, 0.85f, 0.45f) : UIKit.Blood;

            var tile = UIKit.Place(UIKit.Panel(cardArea, it.name, new Color(0.12f, 0.08f, 0.08f)).rectTransform, i * 330, 0, 310, 470);
            UIKit.Place(UIKit.Panel(tile, "Edge", color).rectTransform, 0, 0, 310, 8);
            var cat = UIKit.Label(tile, it.category.ToUpper(), 20, color, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Place(cat.rectTransform, 24, 24, 260, 30);
            var nm = UIKit.Label(tile, it.name, 36, Color.white, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Place(nm.rectTransform, 24, 62, 270, 100);
            var ds = UIKit.Label(tile, it.desc, 24, new Color(1, 1, 1, 0.75f), TextAnchor.UpperLeft);
            UIKit.Place(ds.rectTransform, 24, 168, 270, 150);
            if (why != null)
            {
                var w = UIKit.Label(tile, why, 20, UIKit.Blood, TextAnchor.UpperLeft, FontStyle.Italic);
                UIKit.Place(w.rectTransform, 24, 320, 270, 50);
            }

            string label = it.sold ? "BOUGHT" : $"${it.price}";
            var b = UIKit.Button(tile, label, 34, () => Buy(it));
            UIKit.Place((RectTransform)b.transform, 24, 386, 262, 64);
            b.interactable = !it.sold && why == null && afford;
            if (b.interactable && !first) first = b;
        }

        rerollButton.GetComponentInChildren<Text>().text = $"REROLL  ${RerollCost}";
        rerollButton.interactable = wallet && wallet.Cash >= RerollCost;
        upgradesText.text = upgrades && upgrades.Used > 0 ? "Your upgrades: " + upgrades.Summary() : $"Your upgrades: none yet (hold up to {(upgrades ? upgrades.Slots : 5)} different ones)";
        noteText.text = offer.TrueForAll(o => o.sold) ? "Bag's empty. Reroll for more, or start the next round." : "";
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
