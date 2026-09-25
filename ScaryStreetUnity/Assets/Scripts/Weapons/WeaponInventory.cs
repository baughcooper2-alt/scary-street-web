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

        var input = new WeaponInput();
        bool locked = Cursor.lockState == CursorLockMode.Locked;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current; var mouse = Mouse.current; var pad = Gamepad.current;
        if (kb != null)
        {
            if (kb.digit1Key.wasPressedThisFrame) Select(0);
            if (kb.digit2Key.wasPressedThisFrame) Select(1);
            if (kb.digit3Key.wasPressedThisFrame) Select(2);
            if (kb.digit4Key.wasPressedThisFrame) Select(3);
            if (kb.digit5Key.wasPressedThisFrame) Select(4);
            input.secondaryHeld |= kb.eKey.isPressed;
        }
        if (mouse != null)
        {
            float wheel = mouse.scroll.ReadValue().y;
            if (wheel > 0.1f) Cycle(-1); else if (wheel < -0.1f) Cycle(1);
            input.primaryPressed |= mouse.leftButton.wasPressedThisFrame;
            input.primaryHeld |= mouse.leftButton.isPressed;
            input.secondaryHeld |= mouse.rightButton.isPressed;
        }
        if (pad != null)
        {
            if (pad.rightShoulder.wasPressedThisFrame) Cycle(1);
            if (pad.leftShoulder.wasPressedThisFrame) Cycle(-1);
            input.primaryPressed |= pad.rightTrigger.wasPressedThisFrame;
            input.primaryHeld |= pad.rightTrigger.isPressed;
            input.secondaryHeld |= pad.leftTrigger.isPressed;
        }
#else
        for (int i = 0; i < 5; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Select(i);
        float wheel = Input.mouseScrollDelta.y;
        if (wheel > 0.1f) Cycle(-1); else if (wheel < -0.1f) Cycle(1);
        input.primaryPressed = Input.GetMouseButtonDown(0);
        input.primaryHeld = Input.GetMouseButton(0);
        input.secondaryHeld = Input.GetMouseButton(1) || Input.GetKey(KeyCode.E);
#endif
        if (!locked) input = new WeaponInput();                    // clicks that grab the mouse don't fire
        if (Current) Current.Tick(input);
    }

    void Cycle(int dir) => Select((Selected + dir + slots.Count) % slots.Count);

    // ---------- HUD ----------

    void OnGUI()
    {
        if (health && health.IsDead) return;
        if (slotName == null)
        {
            slotName = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter, wordWrap = true };
            slotStatus = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.LowerCenter };
            hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            toastStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        if (haze > 0.01f) Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.86f, 0.9f, 0.86f, haze * 0.55f));

        const float w = 112, h = 74, gap = 8;
        float total = slots.Count * w + (slots.Count - 1) * gap;
        float x0 = (Screen.width - total) / 2f, y = Screen.height - h - 18;
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
        GUI.Label(new Rect(0, y - 30, Screen.width, 24), hint, hintStyle);
        if (toastT > 0) GUI.Label(new Rect(0, Screen.height * 0.7f, Screen.width, 34), toast, toastStyle);
    }

    static void Fill(Rect r, Color c)
    {
        var old = GUI.color; GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }
}
