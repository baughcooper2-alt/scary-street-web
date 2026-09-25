using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// The player's weapon slots (DESIGN.md: start with 5; the Backpack upgrade adds one each).
// An empty slot means fists, so PlayerPunch only listens while an empty slot is selected.
// Switch with 1–5, the mouse wheel, or the controller bumpers. Also draws the slot bar and the smoke haze.
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

    [System.NonSerialized] public float haze;   // screen smoke, 0..1; weapons add to it, it clears on its own

    readonly List<Weapon> slots = new List<Weapon>();
    PlayerPunch fists;
    Health health;
    Transform weaponRoot;
    string toast; float toastT;
    GUIStyle slotName, slotStatus, hintStyle, toastStyle;

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

    void Update()
    {
        haze = Mathf.MoveTowards(haze, 0, Time.deltaTime * 0.18f);
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

    // ---------- HUD ----------

    void OnGUI()
    {
        var area = HudArea.For(this);
        GUI.BeginGroup(area);
        DrawHud(area.width, area.height);
        GUI.EndGroup();
    }

    void DrawHud(float W, float H)
    {
        if (health && health.IsDead) return;
        if (slotName == null)
        {
            slotName = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter, wordWrap = true };
            slotStatus = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.LowerCenter };
            hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            toastStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        if (haze > 0.01f) Fill(new Rect(0, 0, W, H), new Color(0.86f, 0.9f, 0.86f, haze * 0.55f));

        const float w = 112, h = 74, gap = 8;
        float total = slots.Count * w + (slots.Count - 1) * gap;
        float x0 = (W - total) / 2f, y = H - h - 18;
        for (int i = 0; i < slots.Count; i++)
        {
            var r = new Rect(x0 + i * (w + gap), y, w, h);
            bool sel = i == Selected;
            if (sel) Fill(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), new Color(0.95f, 0.76f, 0.19f));
            Fill(r, new Color(0.06f, 0.04f, 0.05f, sel ? 0.92f : 0.7f));
            GUI.Label(new Rect(r.x + 6, r.y + 3, 20, 20), (i + 1).ToString());
            var wpn = slots[i];
            GUI.Label(new Rect(r.x + 4, r.y + 20, r.width - 8, 32), wpn ? wpn.displayName : "Fists", slotName);
            if (wpn) GUI.Label(new Rect(r.x + 4, r.y + 40, r.width - 8, 30), wpn.SlotStatus, slotStatus);
        }

        string hint = Current ? Current.Hint : "Left click to punch";
        GUI.Label(new Rect(0, y - 30, W, 24), hint, hintStyle);
        if (toastT > 0) GUI.Label(new Rect(0, H * 0.7f, W, 34), toast, toastStyle);
    }

    static void Fill(Rect r, Color c)
    {
        var old = GUI.color; GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }
}
