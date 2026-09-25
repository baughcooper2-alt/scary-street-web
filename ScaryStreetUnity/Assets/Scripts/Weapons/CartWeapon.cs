using UnityEngine;

// The cart, ported from the web build. Hold right click (E / left trigger) to hit it and fill up to 6 puffs;
// left click (right trigger) blows a smoke puff that goes through everyone.
// It gets better as you level up (web build: the cart upgraded on level-up):
//   Lv 1  puffs            Lv 2  + an O-ring with every puff            Lv 3  Blinker: keep holding once full for
//                                                                              1 s, then click for a huge blast (5 s cooldown)
public class CartWeapon : Weapon
{
    [Header("Smoke")]
    public int maxPuffs = 6;
    public float inhaleRate = 5f;        // puffs per second while holding
    public float fireCooldown = 0.35f;
    public float ringDelay = 0.09f;
    [Header("Blinker (level 3)")]
    public float blinkerHold = 1f;
    public float blinkerCooldown = 5f;

    public int Tier => progress ? Mathf.Clamp(progress.Level, 1, 3) : 1;

    float lung, holdFull, fireCd, blinkCd, ringT = -1f, oil = 100f, glow;
    bool blinkReady, warned;
    Transform fpModel, tpModel;
    Material ledMat;

    public override void Init(WeaponInventory inv)
    {
        base.Init(inv);
        displayName = "Cart";
        if (progress) progress.LevelUp += lv =>
        {
            if (lv == 2) inventory.Toast("Cart upgraded: O-rings now fly with every puff", 3f);
            else if (lv == 3) inventory.Toast("Blinker unlocked: keep holding your hit after you're full, then puff", 3.5f);
        };
    }

    public override void Equip()
    {
        base.Equip();
        ShowModels(true);
    }

    public override void Unequip()
    {
        base.Unequip();
        ShowModels(false);
        if (arms) arms.raise = 0;
        holdFull = 0;
    }

    public override void Tick(WeaponInput input)
    {
        float dt = Time.deltaTime;
        fireCd -= dt;
        blinkCd = Mathf.Max(0, blinkCd - dt);
        EnsureModels();
        if (tpModel)                                                   // the body's copy only shows in third person
        {
            var tpv = inventory.GetComponent<ThirdPersonView>();
            var mode = tpv && tpv.IsThirdPerson ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            foreach (var r in tpModel.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = mode;
        }

        // hit it
        bool inhaling = input.secondaryHeld && !input.primaryHeld;
        if (inhaling)
        {
            if (lung < maxPuffs) { lung = Mathf.Min(maxPuffs, lung + inhaleRate * dt); holdFull = 0; }
            else if (Tier >= 3 && blinkCd <= 0 && !blinkReady && (holdFull += dt) >= blinkerHold)
            {
                blinkReady = true;
                inventory.Toast("Blinker loaded: left click to let it out", 1.6f);
            }
            inventory.haze = Mathf.Min(0.55f, inventory.haze + dt * 0.12f);
        }
        else holdFull = 0;
        if (arms) arms.raise = Mathf.MoveTowards(arms.raise, inhaling ? 1f : 0f, dt * 6f);
        glow = Mathf.MoveTowards(glow, inhaling || blinkReady ? 1f : 0f, dt * 8f);
        if (ledMat) ledMat.color = Color.Lerp(new Color(0.18f, 0.42f, 0.28f), blinkReady ? new Color(1f, 0.35f, 0.12f) : new Color(1f, 0.7f, 0.28f), glow);

        // blow it
        if (input.primaryPressed && fireCd <= 0)
        {
            if (blinkReady)
            {
                Shoot(SmokeShot.Kind.Blast);
                blinkReady = false; lung = 0; blinkCd = blinkerCooldown; fireCd = 0.6f;
                inventory.haze = 0.9f;
                inventory.Toast("BLINKER", 1f);
                if (arms) arms.Kick(1.6f);
            }
            else if (lung >= 1f)
            {
                Shoot(SmokeShot.Kind.Puff);
                if (Tier >= 2) ringT = ringDelay;
                lung -= 1f; fireCd = fireCooldown;
                oil = Mathf.Max(1f, oil - 0.4f);
                inventory.haze = Mathf.Min(inventory.haze + 0.08f, 0.5f);
                if (arms) arms.Kick();
            }
            else if (!warned) { warned = true; inventory.Toast("Out of smoke: hold right click (or E) to hit the cart", 1.6f); }
        }
        if (!input.primaryHeld) warned = false;

        if (ringT >= 0 && (ringT -= dt) < 0) Shoot(SmokeShot.Kind.Ring);
    }

    void Shoot(SmokeShot.Kind kind)
    {
        // from your mouth, along where you're looking (works from third person too)
        Vector3 mouth = cam.position;
        var cc = inventory.GetComponent<CharacterController>();
        if (cc) mouth = inventory.transform.position + Vector3.up * (cc.height - 0.2f);
        SmokeShot.Spawn(kind, mouth + cam.forward * 0.5f, cam.forward, inventory.gameObject, inventory.smokeMaterial);
    }

    public override string SlotStatus
    {
        get
        {
            int n = Mathf.FloorToInt(lung + 0.001f);
            string dots = new string('●', n) + new string('○', maxPuffs - n);
            return blinkReady ? "BLINKER" : $"{dots}  {Mathf.RoundToInt(oil)}%";
        }
    }

    public override string Hint
    {
        get
        {
            string lvl = Tier == 1 ? "Lv 1 · O-rings at Lv 2" : Tier == 2 ? "Lv 2 O-rings · Blinker at Lv 3"
                       : blinkReady ? "Blinker loaded: left click" : blinkCd > 0 ? $"Blinker in {blinkCd:0.0}s" : "Blinker: keep holding when full";
            return $"Hold right click (E) to hit it · Left click to blow · {lvl}";
        }
    }

    // ---------- held model ----------

    void EnsureModels()
    {
        if (!fpModel && arms && arms.RightHand) { fpModel = BuildCart(arms.RightHand, true); fpModel.gameObject.SetActive(Equipped); }
        if (!tpModel)
        {
            var body = inventory.GetComponentInChildren<BlockyCharacter>();
            if (body && body.handR) { tpModel = BuildCart(body.handR, false); tpModel.gameObject.SetActive(Equipped); }
        }
    }

    void ShowModels(bool on)
    {
        EnsureModels();
        if (fpModel) fpModel.gameObject.SetActive(on);
        if (tpModel) tpModel.gameObject.SetActive(on);
    }

    // Flat disposable (no branding): red-to-orange body, orange band, mouthpiece up top, dark base with a light.
    Transform BuildCart(Transform hand, bool firstPerson)
    {
        var root = new GameObject("Cart").transform;
        root.SetParent(hand, false);
        if (firstPerson) { root.localPosition = new Vector3(0, 0.03f, 0.01f); root.localRotation = Quaternion.Euler(-10f, 0, 0); }
        else { root.localPosition = new Vector3(0, -0.08f, 0.03f); root.localRotation = Quaternion.Euler(-80f, 0, 0); }
        root.localScale = Vector3.one * 0.55f;

        var mats = BlockyCharacter.RuntimeMaterials();
        Part(PrimitiveType.Cube, root, new Vector3(0, -0.035f, 0), new Vector3(0.05f, 0.17f, 0.026f), mats("CartBody", new Color(0.88f, 0.25f, 0.17f)), firstPerson);
        Part(PrimitiveType.Cylinder, root, new Vector3(-0.025f, -0.035f, 0), new Vector3(0.026f, 0.085f, 0.026f), mats("CartSide", new Color(0.93f, 0.35f, 0.15f)), firstPerson);
        Part(PrimitiveType.Cylinder, root, new Vector3(0.025f, -0.035f, 0), new Vector3(0.026f, 0.085f, 0.026f), mats("CartSide", new Color(0.93f, 0.35f, 0.15f)), firstPerson);
        Part(PrimitiveType.Cube, root, new Vector3(0, 0.046f, 0), new Vector3(0.077f, 0.008f, 0.028f), mats("CartBand", new Color(0.96f, 0.54f, 0.12f)), firstPerson);
        Part(PrimitiveType.Cube, root, new Vector3(0, 0.08f, 0), new Vector3(0.044f, 0.05f, 0.016f), mats("CartOil", new Color(0.85f, 0.64f, 0.25f)), firstPerson);
        Part(PrimitiveType.Cube, root, new Vector3(0, 0.128f, 0), new Vector3(0.036f, 0.04f, 0.016f), mats("CartTip", new Color(0.96f, 0.54f, 0.12f)), firstPerson);
        Part(PrimitiveType.Cube, root, new Vector3(0, -0.124f, 0), new Vector3(0.074f, 0.012f, 0.03f), mats("CartBase", new Color(0.1f, 0.07f, 0.13f)), firstPerson);
        Part(PrimitiveType.Cube, root, new Vector3(0, 0.012f, 0.0135f), new Vector3(0.046f, 0.058f, 0.002f), mats("CartScreen", new Color(0.42f, 0.3f, 0.6f)), firstPerson);

        var led = Part(PrimitiveType.Sphere, root, new Vector3(0, -0.128f, 0.012f), new Vector3(0.02f, 0.01f, 0.02f), null, firstPerson);
        if (!ledMat) ledMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.18f, 0.42f, 0.28f) };
        led.GetComponent<Renderer>().sharedMaterial = ledMat;
        return root;
    }

    static Transform Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material m, bool firstPerson)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        var t = go.transform;
        t.SetParent(parent, false);
        t.localPosition = pos; t.localScale = scale;
        var r = go.GetComponent<Renderer>();
        if (m) r.sharedMaterial = m;
        if (firstPerson) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return t;
    }

    void OnDestroy() { if (ledMat) Destroy(ledMat); }
}
