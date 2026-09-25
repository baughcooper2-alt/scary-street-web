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

    // level-ups upgrade it (web build), and the DoorDash shop can unlock the next tier early
    // the cart levels with you automatically (web build), so level-up picks don't offer it
    public override bool CanLevelUp => false;
    protected override CharacterAnimator.Hold HoldPose => CharacterAnimator.Hold.Cart;

    public int Tier => Mathf.Clamp(Mathf.Max(progress ? progress.Level : 1, boughtTier), 1, 3);
    int boughtTier = 1;

    public void UpgradeTier()
    {
        boughtTier = Mathf.Min(3, Tier + 1);
        inventory.Toast(Tier == 2 ? "Cart upgraded: O-rings now fly with every puff" : "Blinker unlocked: keep holding your hit after you're full, then puff", 3f);
    }

    float lung, holdFull, fireCd, blinkCd, ringT = -1f, oil = 100f, glow;
    bool blinkReady, warned, wasInhaling;
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
        int index = PlayerLayers.IndexOf(inventory);                   // first-person copy: your camera only; body copy: body layer
        if (fpModel && fpModel.gameObject.layer != PlayerLayers.Arms(index)) PlayerLayers.Set(fpModel.gameObject, PlayerLayers.Arms(index));
        if (tpModel && tpModel.gameObject.layer != PlayerLayers.Body(index))
        {
            PlayerLayers.Set(tpModel.gameObject, PlayerLayers.Body(index));
            foreach (var r in tpModel.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        // hit it
        bool inhaling = input.secondaryHeld && !input.primaryHeld;
        if (inhaling && !wasInhaling && lung < maxPuffs) SoundKit.Play(Sfx.Inhale, 0.45f);
        wasInhaling = inhaling;
        if (inhaling)
        {
            if (lung < maxPuffs) { lung = Mathf.Min(maxPuffs, lung + inhaleRate * dt); holdFull = 0; }
            else if (Tier >= 3 && blinkCd <= 0 && !blinkReady && (holdFull += dt) >= blinkerHold)
            {
                blinkReady = true;
                inventory.Toast($"Blinker loaded: {Key("left click", GamepadInfo.RT)} to let it out", 1.6f);
                Rumble(0.2f, 0.5f, 0.15f);
            }
            inventory.haze = Mathf.Min(0.55f, inventory.haze + dt * 0.12f);
        }
        else holdFull = 0;
        if (arms) arms.raise = Mathf.MoveTowards(arms.raise, inhaling ? 1f : 0f, dt * 6f);
        var anim = BodyAnim; if (anim) { anim.inhaling = inhaling; if (anim.hold != CharacterAnimator.Hold.Cart) anim.hold = CharacterAnimator.Hold.Cart; }
        glow = Mathf.MoveTowards(glow, inhaling || blinkReady ? 1f : 0f, dt * 8f);
        DrawScreen(maxPuffs > 0 ? lung / maxPuffs : 0f);
        if (ledMat) ledMat.color = Color.Lerp(new Color(0.18f, 0.42f, 0.28f), blinkReady ? new Color(1f, 0.35f, 0.12f) : new Color(1f, 0.7f, 0.28f), glow);

        // blow it
        if (input.primaryPressed && fireCd <= 0)
        {
            if (blinkReady)
            {
                Shoot(SmokeShot.Kind.Blast);
                SoundKit.Play(Sfx.Blinker, 0.9f);
                blinkReady = false; lung = 0; blinkCd = blinkerCooldown; fireCd = 0.6f;
                inventory.haze = 0.9f;
                inventory.Toast("BLINKER", 1f);
                if (arms) arms.Kick(1.6f);
                Rumble(0.9f, 1f, 0.45f);
            }
            else if (lung >= 1f)
            {
                Shoot(SmokeShot.Kind.Puff);
                SoundKit.Play(Sfx.Puff, 0.6f);
                if (Tier >= 2) ringT = ringDelay;
                lung -= 1f; fireCd = fireCooldown;
                oil = Mathf.Max(1f, oil - 0.4f);
                inventory.haze = Mathf.Min(inventory.haze + 0.08f, 0.5f);
                if (arms) arms.Kick();
                Rumble(0.1f, 0.3f, 0.08f);
            }
            else if (!warned) { warned = true; inventory.Toast($"Out of smoke: hold {Key("right click (or E)", GamepadInfo.LT)} to hit the cart", 1.6f); }
        }
        if (!input.primaryHeld) warned = false;

        if (ringT >= 0 && (ringT -= dt) < 0) { Shoot(SmokeShot.Kind.Ring); SoundKit.Play(Sfx.Ring, 0.45f); }
    }

    void Shoot(SmokeShot.Kind kind)
    {
        // from your mouth, along where you're looking (works from third person too)
        Vector3 mouth = cam.position;
        var cc = inventory.GetComponent<CharacterController>();
        if (cc) mouth = inventory.transform.position + Vector3.up * (cc.height - 0.2f);
        float dmg = PlayerStats.RangedDamage(1f, inventory.gameObject);                     // Precision, Shooter, crits (rolled once per shot)
        SmokeShot.Spawn(kind, mouth + cam.forward * 0.5f, cam.forward, inventory.gameObject, inventory.smokeMaterial, dmg);
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
                       : blinkReady ? $"Blinker loaded: {Key("left click", GamepadInfo.RT)}" : blinkCd > 0 ? $"Blinker in {blinkCd:0.0}s" : "Blinker: keep holding when full";
            return $"Hold {Key("right click (E)", GamepadInfo.LT)} to hit it · {Key("Left click", GamepadInfo.RT)} to blow · {lvl}";
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

    // Box disposable modelled on the real one: glossy black body with a squared mouthpiece up top, a watermelon-splash
    // label on the left of the front and a glowing screen on the right (logo block, 5-colour battery bar, hexagon logo,
    // two-digit readout, flavour tag) with a button below. No real branding. The screen shows how full your hit is.
    Transform BuildCart(Transform hand, bool firstPerson)
    {
        var root = new GameObject("Cart").transform;
        root.SetParent(hand, false);
        if (firstPerson) { root.localPosition = new Vector3(0, 0.03f, 0.01f); root.localRotation = Quaternion.Euler(-10f, 0, 0); }
        else { root.localPosition = new Vector3(0, -0.08f, 0.03f); root.localRotation = Quaternion.Euler(-80f, 0, 0); }
        root.localScale = Vector3.one * 0.55f;

        var mats = BlockyCharacter.RuntimeMaterials();
        var shell = mats("CartShell", new Color(0.035f, 0.035f, 0.04f)); shell.SetFloat("_Smoothness", 0.7f);
        var matte = mats("CartMouth", new Color(0.05f, 0.05f, 0.055f)); matte.SetFloat("_Smoothness", 0.25f);
        const float W = 0.085f, H = 0.13f, D = 0.036f;
        CartPart(PrimitiveType.Cube, root, Vector3.zero, new Vector3(W, H, D), shell, firstPerson);
        CartPart(PrimitiveType.Cube, root, new Vector3(-0.012f, H / 2 + 0.017f, 0), new Vector3(0.036f, 0.034f, 0.028f), matte, firstPerson);
        CartPart(PrimitiveType.Cube, root, new Vector3(-0.012f, H / 2 + 0.036f, 0), new Vector3(0.03f, 0.006f, 0.022f), matte, firstPerson);
        CartPart(PrimitiveType.Cube, root, new Vector3(0, H / 2 - 0.002f, 0), new Vector3(W + 0.002f, 0.006f, D + 0.002f), shell, firstPerson);   // top rim

        // label (left) and screen (right) on the front face
        if (!labelMat)
        {
            labelMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "CartLabel" };
            labelMat.mainTexture = LabelTexture(); labelMat.SetFloat("_Smoothness", 0.45f);
        }
        if (!screenMat)
        {
            screenTex = new Texture2D(48, 120, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "CartScreen" };
            screenMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "CartScreen", mainTexture = screenTex, color = new Color(1.6f, 1.6f, 1.6f) };
            shownLevel = -1; DrawScreen(0f);
        }
        Face(root, new Vector3(-0.0195f, 0, D / 2 + 0.0004f), new Vector2(0.044f, 0.124f), labelMat, firstPerson);
        Face(root, new Vector3(0.0215f, 0.012f, D / 2 + 0.0004f), new Vector2(0.03f, 0.075f), screenMat, firstPerson);
        CartPart(PrimitiveType.Cube, root, new Vector3(0.0215f, -0.043f, D / 2), new Vector3(0.015f, 0.006f, 0.002f), matte, firstPerson);   // button

        var led = CartPart(PrimitiveType.Cube, root, new Vector3(0.0215f, -0.0335f, D / 2 + 0.0003f), new Vector3(0.02f, 0.0015f, 0.001f), null, firstPerson);
        if (!ledMat) ledMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.18f, 0.42f, 0.28f) };
        led.GetComponent<Renderer>().sharedMaterial = ledMat;
        return root;
    }

    Material labelMat, screenMat; Texture2D screenTex; int shownLevel = -1;

    static void Face(Transform parent, Vector3 pos, Vector2 size, Material m, bool firstPerson)
    {
        var q = CartPart(PrimitiveType.Quad, parent, pos, new Vector3(size.x, size.y, 1f), m, firstPerson);
        q.localRotation = Quaternion.Euler(0, 180f, 0);                    // quads face -Z; the front is +Z
    }

    // Watermelon splash: sky-blue top, a splashy edge, red flesh with seeds, green rind and a pale band at the bottom.
    static Texture2D LabelTexture()
    {
        const int w = 64, h = 180;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "CartLabel" };
        var rng = new System.Random(7);
        var px = new Color[w * h];
        Color sky = new Color(0.38f, 0.74f, 0.86f), flesh = new Color(0.86f, 0.17f, 0.2f), rind = new Color(0.35f, 0.7f, 0.3f), pale = new Color(0.85f, 0.95f, 0.7f);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            float u = x / (float)w, v = y / (float)h;
            float edge = 0.58f + 0.05f * Mathf.Sin(u * 17f) + 0.03f * Mathf.Sin(u * 41f + 1.3f);          // splash line
            Color c = v > edge ? sky : flesh;
            if (v > edge && v < edge + 0.04f && Mathf.PerlinNoise(x * 0.4f, y * 0.4f) > 0.55f) c = Color.white;   // droplets
            if (v <= edge) c = Color.Lerp(c, new Color(0.6f, 0.08f, 0.12f), Mathf.PerlinNoise(x * 0.12f, y * 0.12f) * 0.35f);
            if (v < 0.08f) c = rind; else if (v < 0.11f) c = pale;
            if (v > edge) c = Color.Lerp(c, Color.white, Mathf.PerlinNoise(x * 0.08f + 3f, y * 0.05f) * 0.25f);
            px[y * w + x] = c;
        }
        for (int s = 0; s < 18; s++)                                       // seeds
        {
            int sx = rng.Next(4, w - 4), sy = rng.Next(24, (int)(h * 0.5f));
            for (int dy = -3; dy <= 3; dy++) for (int dx = -2; dx <= 2; dx++)
                if (dx * dx / 4f + dy * dy / 9f <= 1f) px[(sy + dy) * w + sx + dx] = new Color(0.08f, 0.04f, 0.04f);
        }
        tex.SetPixels(px); tex.Apply(true);
        return tex;
    }

    // Screen pixels: logo block, battery bar (how full your hit is), hexagon logo, two-digit readout, flavour tag.
    void DrawScreen(float full)
    {
        if (!screenTex) return;
        int level = Mathf.RoundToInt(full * 99f);
        if (level == shownLevel) return;
        shownLevel = level;
        const int w = 48, h = 120;
        var px = new Color[w * h];
        void Rect(int x0, int y0, int x1, int y1, Color c) { for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) if (x >= 0 && x < w && y >= 0 && y < h) px[(h - 1 - y) * w + x] = c; }
        Color red = new Color(1f, 0.12f, 0.1f), white = new Color(0.95f, 0.95f, 0.95f), dim = new Color(0.12f, 0.12f, 0.12f);
        Rect(0, 0, w, h, new Color(0.01f, 0.01f, 0.012f));
        Rect(6, 4, 42, 16, red); Rect(9, 7, 39, 9, new Color(0.35f, 0.02f, 0.02f)); Rect(12, 11, 36, 13, new Color(0.45f, 0.04f, 0.03f));   // logo block
        Rect(6, 20, 42, 21, white);
        Color[] bar = { new Color(1f, 0.2f, 0.1f), new Color(1f, 0.5f, 0.1f), new Color(1f, 0.85f, 0.1f), new Color(0.75f, 1f, 0.2f), new Color(0.3f, 1f, 0.3f) };
        int lit = Mathf.CeilToInt(full * 5f - 0.001f);
        for (int i = 0; i < 5; i++) Rect(7 + i * 7, 23, 12 + i * 7, 30, i < lit ? bar[i] : dim);
        Rect(6, 32, 42, 33, white);
        // hexagon outline
        for (int y = 36; y < 72; y++) for (int x = 6; x < 42; x++)
        {
            float dx = Mathf.Abs(x - 23.5f) / 17f, dy = Mathf.Abs(y - 53.5f) / 17f;
            float hex = Mathf.Max(dx * 0.866f + dy * 0.5f, dy);
            if (hex < 1f && hex > 0.8f) px[(h - 1 - y) * w + x] = red;
            else if (hex <= 0.8f && hex > 0.72f) px[(h - 1 - y) * w + x] = new Color(0.3f, 0.02f, 0.02f);
        }
        Rect(17, 46, 22, 61, red); Rect(25, 46, 31, 61, red); Rect(22, 46, 25, 49, red); Rect(22, 52, 25, 55, red); Rect(22, 58, 25, 61, red);   // letter marks
        // 7-segment digits
        void Digit(int d, int x0, int y0)
        {
            bool[] seg = { d != 1 && d != 4, d != 5 && d != 6, d != 2, d != 1 && d != 4 && d != 7, d == 0 || d == 2 || d == 6 || d == 8, d != 1 && d != 2 && d != 3 && d != 7, d > 1 && d != 7 };
            Color on = white, off = new Color(0.07f, 0.07f, 0.07f);
            Rect(x0 + 2, y0, x0 + 12, y0 + 2, seg[0] ? on : off);          // a
            Rect(x0 + 12, y0 + 2, x0 + 14, y0 + 10, seg[1] ? on : off);    // b
            Rect(x0 + 12, y0 + 12, x0 + 14, y0 + 20, seg[2] ? on : off);   // c
            Rect(x0 + 2, y0 + 20, x0 + 12, y0 + 22, seg[3] ? on : off);    // d
            Rect(x0, y0 + 12, x0 + 2, y0 + 20, seg[4] ? on : off);         // e
            Rect(x0, y0 + 2, x0 + 2, y0 + 10, seg[5] ? on : off);          // f
            Rect(x0 + 2, y0 + 10, x0 + 12, y0 + 12, seg[6] ? on : off);    // g
        }
        Digit(level / 10, 7, 78); Digit(level % 10, 25, 78);
        Rect(8, 106, 40, 114, new Color(0.95f, 0.45f, 0.12f)); Rect(11, 108, 37, 110, new Color(1f, 0.9f, 0.3f)); Rect(14, 111, 34, 112, new Color(0.25f, 0.6f, 0.2f));   // tag
        screenTex.SetPixels(px); screenTex.Apply(false);
    }

    static Transform CartPart(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material m, bool firstPerson)
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

    void OnDestroy()
    {
        if (ledMat) Destroy(ledMat);
        if (labelMat) { Destroy(labelMat.mainTexture); Destroy(labelMat); }
        if (screenMat) Destroy(screenMat);
        if (screenTex) Destroy(screenTex);
    }
}
