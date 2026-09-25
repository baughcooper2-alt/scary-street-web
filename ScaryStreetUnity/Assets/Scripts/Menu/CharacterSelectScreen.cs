using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Fighting-game style select: game modes along the top, P1's fighter shown big on the left,
// the roster grid in the middle (DESIGN.md's 8 base characters + 8 DLC), P2 waiting on the right.
// Only 1 Player and the characters with a look (Cooper, Nathan) can be picked; the rest say COMING SOON.
// The fighters are real 3D models on a hidden stage far below the house, filmed into RenderTextures.
public class CharacterSelectScreen : MonoBehaviour
{
    static readonly Vector3 StageOrigin = new Vector3(0, -300f, 0);
    static readonly string[] Modes = { "1 PLAYER", "2 PLAYER", "3 PLAYER", "4 PLAYER", "FREE ROAM" };

    class Tile
    {
        public CharacterRoster.Entry entry;
        public CharacterLook look;             // null = coming soon
        public GameObject frame;
        public Button button;
    }

    class Spot
    {
        public Vector3 pos;
        public float height;
    }

    GameFlow flow;
    readonly List<Tile> tiles = new List<Tile>();
    readonly Dictionary<CharacterLook, Spot> spots = new Dictionary<CharacterLook, Spot>();
    readonly List<RenderTexture> textures = new List<RenderTexture>();
    Spot mystery;
    GameObject stage;
    Camera previewCam;
    Text nameText, weaponText, toastText, previewMark;
    Button fightButton;
    int cursor;
    float toastT;

    public static CharacterSelectScreen Create(GameFlow flow)
    {
        var canvas = UIKit.MakeCanvas("CharacterSelect", 50);
        var s = canvas.gameObject.AddComponent<CharacterSelectScreen>();
        s.flow = flow;
        s.BuildStage();
        s.Build(canvas.transform);
        return s;
    }

    void OnDestroy()
    {
        if (stage) Destroy(stage);
        foreach (var rt in textures) if (rt) { rt.Release(); Destroy(rt); }
    }

    // ---------- 3D stage ----------

    void BuildStage()
    {
        stage = new GameObject("CharacterSelectStage");
        stage.transform.position = StageOrigin;
        var mats = BlockyCharacter.RuntimeMaterials();

        int i = 0;
        foreach (var e in CharacterRoster.All)
        {
            var look = flow.LookFor(e.name);
            if (look && !spots.ContainsKey(look)) spots[look] = MakeSpot(look, i++, mats);
        }
        mystery = MakeSpot(CharacterLook.Preset("mystery"), i, mats);

        previewCam = MakeCamera("PreviewCam", 30f, 512, 768);
    }

    Spot MakeSpot(CharacterLook look, int index, BlockyCharacter.MaterialSource mats)
    {
        var spot = new Spot { pos = StageOrigin + Vector3.right * (index * 12f), height = look.height };
        var root = new GameObject(look.displayName).transform;
        root.SetParent(stage.transform, false);
        root.position = spot.pos;
        var body = BlockyCharacter.Build(look, root, mats);
        body.gameObject.AddComponent<CharacterAnimator>();

        var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);           // little round stage under their feet
        Destroy(floor.GetComponent<Collider>());
        floor.transform.SetParent(root, false);
        floor.transform.localPosition = new Vector3(0, -0.02f, 0);
        floor.transform.localScale = new Vector3(1.6f, 0.02f, 1.6f);
        floor.GetComponent<Renderer>().sharedMaterial = mats("Stage", new Color(0.35f, 0.07f, 0.06f));

        var key = new GameObject("KeyLight").AddComponent<Light>();                // warm key light from the front-left
        key.type = LightType.Point; key.range = 9f; key.intensity = 6f; key.color = new Color(1f, 0.85f, 0.7f);
        key.transform.SetParent(root, false);
        key.transform.localPosition = new Vector3(-1.4f, 2.4f, 2.4f);
        var rim = new GameObject("RimLight").AddComponent<Light>();                // red rim from behind
        rim.type = LightType.Point; rim.range = 6f; rim.intensity = 4f; rim.color = new Color(1f, 0.25f, 0.2f);
        rim.transform.SetParent(root, false);
        rim.transform.localPosition = new Vector3(1.2f, 2f, -1.5f);
        return spot;
    }

    Camera MakeCamera(string name, float fov, int w, int h)
    {
        var cam = new GameObject(name).AddComponent<Camera>();
        cam.transform.SetParent(stage.transform, false);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.07f, 0.03f, 0.035f);
        cam.fieldOfView = fov;
        cam.nearClipPlane = 0.05f; cam.farClipPlane = 30f;
        var rt = new RenderTexture(w, h, 24) { antiAliasing = 4, name = name };
        textures.Add(rt);
        cam.targetTexture = rt;
        return cam;
    }

    // Head-and-shoulders portrait camera for one spot (renders every frame so the idle breathing shows).
    RenderTexture Portrait(Spot spot)
    {
        var cam = MakeCamera("PortraitCam", 26f, 256, 256);
        float head = 1.66f * spot.height / 1.8f;
        cam.transform.position = spot.pos + new Vector3(0, head, 1.05f);
        cam.transform.LookAt(spot.pos + new Vector3(0, head - 0.03f, 0));
        return cam.targetTexture;
    }

    void AimPreview(Spot spot)
    {
        float s = spot.height / 1.8f;
        var target = spot.pos + new Vector3(0, 0.93f * s, 0);
        previewCam.transform.position = target + Quaternion.Euler(0, -18f, 0) * new Vector3(0, 0.12f, 4.6f);   // three-quarter view
        previewCam.transform.LookAt(target);
    }

    // ---------- UI ----------

    void Build(Transform root)
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var bg = UIKit.Panel(root, "Background", new Color(0.05f, 0.025f, 0.03f, 0.96f));
        UIKit.Fill(bg.rectTransform);
        UIKit.Place(UIKit.Panel(root, "Glow", new Color(0.7f, 0.08f, 0.05f, 0.22f)).rectTransform, 0, 0, 1920, 170);
        UIKit.Place(UIKit.Panel(root, "GlowLine", UIKit.Blood).rectTransform, 0, 170, 1920, 4);

        var title = UIKit.Label(root, "CHOOSE YOUR FIGHTER", 64, UIKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIKit.Place(UIKit.Outlined(title, Color.black, 4f).rectTransform, 0, 18, 1920, 80);

        // game modes
        for (int m = 0; m < Modes.Length; m++)
        {
            int mode = m;
            float x = 303 + m * 266;
            var b = UIKit.Button(root, "", 30, () => OnMode(mode));
            var rt = UIKit.Place((RectTransform)b.transform, x, 100, 250, 60);
            var label = UIKit.Label(rt, Modes[m], m == 0 ? 30 : 26, m == 0 ? Color.black : UIKit.Dim, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Fill(label.rectTransform);
            if (m == 0)
            {
                var cb = b.colors; cb.normalColor = UIKit.Gold; cb.highlightedColor = cb.selectedColor = new Color(1f, 0.86f, 0.35f); b.colors = cb;
            }
            else
            {
                label.rectTransform.offsetMax = new Vector2(0, -8);
                var soon = UIKit.Label(rt, "COMING SOON", 15, UIKit.Blood, TextAnchor.LowerCenter, FontStyle.Bold);
                UIKit.Fill(soon.rectTransform); soon.rectTransform.offsetMin = new Vector2(0, 3);
            }
        }

        // P1 (left): big preview
        var p1 = UIKit.Place(UIKit.Panel(root, "P1Frame", UIKit.Gold).rectTransform, 56, 196, 468, 698);
        var preview = UIKit.Node("Preview", p1).gameObject.AddComponent<RawImage>();
        preview.texture = previewCam.targetTexture; preview.raycastTarget = false;
        UIKit.Fill(preview.rectTransform, 4);
        Badge(p1, "P1", UIKit.Blood);
        previewMark = UIKit.Label(p1, "?", 260, new Color(1, 1, 1, 0.12f), TextAnchor.MiddleCenter, FontStyle.Bold);
        UIKit.Fill(previewMark.rectTransform);
        nameText = UIKit.Outlined(UIKit.Label(root, "", 54, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold), Color.black);
        UIKit.Place(nameText.rectTransform, 60, 904, 700, 66);
        weaponText = UIKit.Label(root, "", 24, UIKit.Dim, TextAnchor.UpperLeft);
        UIKit.Place(weaponText.rectTransform, 62, 970, 520, 60);

        // roster grid (middle): base 8, then DLC 8
        var mysteryPortrait = Portrait(mystery);
        var portraits = new Dictionary<CharacterLook, RenderTexture>();
        foreach (var kv in spots) portraits[kv.Key] = Portrait(kv.Value);

        var dlcLabel = UIKit.Label(root, "DLC", 24, UIKit.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIKit.Place(dlcLabel.rectTransform, 599, 566, 400, 40);
        for (int i = 0; i < CharacterRoster.All.Length; i++)
        {
            var e = CharacterRoster.All[i];
            int col = i % 4, row = i / 4;
            float x = 599 + col * 184, y = 196 + row * 184 + (row >= 2 ? 30 : 0);
            var look = flow.LookFor(e.name);
            tiles.Add(MakeTile(root, i, e, look, x, y, look ? portraits[look] : mysteryPortrait));
        }

        // P2 (right): waiting
        var p2 = UIKit.Place(UIKit.Panel(root, "P2Frame", new Color(1, 1, 1, 0.12f)).rectTransform, 1396, 196, 468, 698);
        UIKit.Fill(UIKit.Panel(p2, "Inside", new Color(0.07f, 0.03f, 0.035f)).rectTransform, 4);
        Badge(p2, "P2", new Color(0.2f, 0.35f, 0.8f));
        var q = UIKit.Label(p2, "?", 260, new Color(1, 1, 1, 0.1f), TextAnchor.MiddleCenter, FontStyle.Bold);
        UIKit.Fill(q.rectTransform);
        var p2Text = UIKit.Label(p2, "PLAYER 2\nCOMING SOON", 40, UIKit.Dim, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIKit.Place(p2Text.rectTransform, 0, 520, 468, 110);
        var p2Note = UIKit.Label(p2, "Co-op for up to 4 (LAN, split-screen, online) is planned.", 20, UIKit.Dim, TextAnchor.UpperCenter, FontStyle.Italic);
        UIKit.Place(p2Note.rectTransform, 30, 632, 408, 60);

        // bottom buttons + messages
        var back = UIKit.Button(root, "BACK", 32, () => flow.ShowTitle());
        UIKit.Place((RectTransform)back.transform, 1396, 930, 200, 64);
        fightButton = UIKit.Button(root, "FIGHT!", 38, Fight);
        UIKit.Place((RectTransform)fightButton.transform, 1640, 930, 224, 64);
        var fcb = fightButton.colors; fcb.normalColor = UIKit.Blood; fightButton.colors = fcb;
        toastText = UIKit.Label(root, "", 26, UIKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIKit.Place(toastText.rectTransform, 560, 1010, 800, 44);

        SetCursor(0);
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(tiles[0].button.gameObject);
    }

    Tile MakeTile(Transform root, int index, CharacterRoster.Entry e, CharacterLook look, float x, float y, Texture portrait)
    {
        var t = new Tile { entry = e, look = look };
        t.frame = UIKit.Place(UIKit.Panel(root, "Cursor", UIKit.Gold).rectTransform, x - 6, y - 6, 182, 182).gameObject;

        t.button = UIKit.Button(root, "", 20, () => OnTileClicked(index));
        var rt = UIKit.Place((RectTransform)t.button.transform, x, y, 170, 170);
        var cb = t.button.colors; cb.normalColor = new Color(0.15f, 0.1f, 0.1f); cb.highlightedColor = cb.selectedColor = Color.white; t.button.colors = cb;

        var img = UIKit.Node("Portrait", rt).gameObject.AddComponent<RawImage>();
        img.texture = portrait; img.raycastTarget = false;
        UIKit.Fill(img.rectTransform, 4);
        if (!look) img.color = new Color(0.55f, 0.55f, 0.55f);

        var strip = UIKit.Place(UIKit.Panel(rt, "NameStrip", new Color(0, 0, 0, 0.75f)).rectTransform, 4, 132, 162, 34);
        var name = UIKit.Label(strip, e.name.ToUpper(), 20, look ? Color.white : UIKit.Dim, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIKit.Fill(name.rectTransform);
        if (!look)
        {
            var soonBg = UIKit.Place(UIKit.Panel(rt, "SoonBg", new Color(0, 0, 0, 0.7f)).rectTransform, 4, 8, 162, 26);
            var soon = UIKit.Label(soonBg, "COMING SOON", 16, UIKit.Blood, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Fill(soon.rectTransform);
        }

        var hover = t.button.gameObject.AddComponent<TileHover>();
        hover.onHover = () => SetCursor(index);
        return t;
    }

    static void Badge(RectTransform panel, string text, Color color)
    {
        var b = UIKit.Place(UIKit.Panel(panel, "Badge", color).rectTransform, 12, 12, 78, 46);
        var l = UIKit.Label(b, text, 30, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIKit.Fill(l.rectTransform);
    }

    // ---------- behavior ----------

    void SetCursor(int index)
    {
        cursor = index;
        for (int i = 0; i < tiles.Count; i++) tiles[i].frame.SetActive(i == index);
        var t = tiles[index];
        nameText.text = t.entry.name.ToUpper();
        nameText.color = t.look ? Color.white : UIKit.Dim;
        weaponText.text = t.look ? $"Starts with: {t.entry.startsWith}"
                        : t.entry.dlc ? "DLC character · coming soon" : $"Starts with: {t.entry.startsWith}\nComing soon";
        previewMark.enabled = !t.look;
        AimPreview(t.look ? spots[t.look] : mystery);
        fightButton.interactable = t.look;
    }

    // Clicking (or Enter / A on) a fighter picks them, like a fighting game.
    void OnTileClicked(int index)
    {
        SetCursor(index);
        Fight();
    }

    void OnMode(int mode)
    {
        if (mode != 0) Toast($"{Modes[mode].ToLower()} is coming soon. 1 Player only for now");
    }

    void Fight()
    {
        var t = tiles[cursor];
        if (!t.look) { Toast($"{t.entry.name} is coming soon"); return; }
        flow.StartGame(t.look);
    }

    void Toast(string msg)
    {
        toastText.text = msg.Substring(0, 1).ToUpper() + msg.Substring(1);
        toastT = 2.5f;
    }

    void Update()
    {
        if (toastT > 0 && (toastT -= Time.deltaTime) <= 0) toastText.text = "";

        bool back = false, confirm = false;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current; var pad = Gamepad.current;
        back = (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame);
        confirm = pad != null && pad.startButton.wasPressedThisFrame;
#else
        back = Input.GetKeyDown(KeyCode.Escape);
#endif
        if (back) flow.ShowTitle();
        else if (confirm) Fight();
    }
}
