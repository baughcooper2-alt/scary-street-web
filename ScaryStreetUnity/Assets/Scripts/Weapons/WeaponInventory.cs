using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// The player's weapon slots (DESIGN.md: start with 5; the Backpack upgrade adds one each).
// An empty slot means fists, so PlayerPunch only listens while an empty slot is selected.
// Switch with 1–5, the mouse wheel, or the controller bumpers.
public class WeaponInventory : MonoBehaviour
{
    [Min(1)] public int capacity = 5;
    [Tooltip("Puts the cart in slot 1 at the start of a run.")]
    public bool startWithCart = true;
    [Tooltip("Adds your character's starting weapon (DESIGN.md: Cooper = Law Book, Nathan = Guitar) in the next slot.")]
    public bool startWithCharacterWeapon = true;
    [Tooltip("Transparent material for smoke; the setup tool saves one so builds keep the shader variant.")]
    public Material smokeMaterial;

    public int Selected { get; private set; }
    public Weapon Current => Selected < slots.Count ? slots[Selected] : null;
    public IReadOnlyList<Weapon> Slots => slots;


    readonly List<Weapon> slots = new List<Weapon>();
    PlayerPunch fists;
    Health health;
    Transform weaponRoot;
    string toast; float toastT;

    void Awake()
    {
        fists = GetComponent<PlayerPunch>();
        health = GetComponent<Health>();
        weaponRoot = new GameObject("Weapons").transform;
        weaponRoot.SetParent(transform, false);
        for (int i = 0; i < capacity; i++) slots.Add(null);
    }

    void Start()
    {
        if (startWithCart) Add<CartWeapon>();
        if (startWithCharacterWeapon)
        {
            // who you're playing: the select-screen pick, or whoever the first-person arms are dressed as
            var arms = GetComponentInChildren<FirstPersonArms>(true);
            var look = arms && arms.look ? arms.look : GameFlow.Chosen;   // each player's own pick (co-op)
            var entry = look ? CharacterRoster.Find(look.displayName) : null;
            if (entry != null && entry.weapon == CharacterRoster.StartingWeapon.LawBook) Add<LawBookWeapon>();
            else if (entry != null && entry.weapon == CharacterRoster.StartingWeapon.Guitar) Add<GuitarWeapon>();
        }
        Select(0);
    }

    // Adds a weapon to the first empty slot; false if every slot is full.
    public T Add<T>() where T : Weapon
    {
        int i = slots.IndexOf(null);
        if (i < 0) return null;
        var w = weaponRoot.gameObject.AddComponent<T>();
        w.Init(this);
        slots[i] = w;
        if (i == Selected) Select(i);
        return w;
    }

    public void AddSlot() { capacity++; slots.Add(null); }   // Backpack upgrade

    public void Select(int index)
    {
        if (index < 0 || index >= slots.Count) return;
        if (Current && Current.Equipped) Current.Unequip();
        Selected = index;
        if (Current) Current.Equip();
        if (fists) fists.allowInput = !Current;
    }

    public void Toast(string msg, float time = 2f) { toast = msg; toastT = time; }
    public string ToastText => toastT > 0 ? toast : null;
    public float ToastAge => toastT;

    void Update()
    {
        toastT -= Time.deltaTime;
        if (health && health.IsDead) { if (Current && Current.Equipped) Current.Unequip(); return; }

        if (Current && !Current.Equipped) Current.Equip();            // back up after being downed
        var input = new WeaponInput();
        if (LevelUpScreen.IsOpen || DoorDashShop.IsOpen) return;     // their number keys and clicks aren't for us
        var c = PlayerControls.For(gameObject);
        bool locked = c.Active;
        if (c.SlotPressed >= 0) Select(c.SlotPressed);
        if (c.SlotCycle != 0) Cycle(c.SlotCycle);
        input.primaryPressed = c.PrimaryPressed;
        input.primaryHeld = c.PrimaryHeld;
        input.secondaryHeld = c.SecondaryHeld;
        input.reloadPressed = c.ReloadPressed;
        if (!locked) input = new WeaponInput();                    // clicks that grab the mouse don't fire
        if (Current) Current.Tick(input);
    }

    void Cycle(int dir) => Select((Selected + dir + slots.Count) % slots.Count);







}
