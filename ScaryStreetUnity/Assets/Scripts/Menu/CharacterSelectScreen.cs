using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Fighting-game style select: game modes along the top, P1's fighter shown big on the left,
// the roster grid in the middle (DESIGN.md's 8 base characters + 8 DLC), P2 waiting on the right.
// 1 Player, or 2 Player split-screen (needs a controller for P2; P1 picks, then P2). Only characters with a look
// (Cooper, Nathan) can be picked; the rest say COMING SOON, as do 3 / 4 Player. Free Roam is the sandbox (no enemies).
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
    int players = 1, picking;                    // how many are playing, and whose turn it is to pick
    bool freeRoam;
    Button storyButton, endlessButton;
    readonly CharacterLook[] picks = new CharacterLook[2];
    readonly List<Button> modeButtons = new List<Button>();
    Text titleText, p2Text, p2Note, p2Name, p2Mark;
    RawImage p2Preview;
    Camera previewCam2;
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

    void AimPreview(Camera previewCam, Spot spot)
    {
        if (!previewCam) return;
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

        var bg = UIKit.Panel(root, "Background", UIArt.Theme.Ink);
        UIKit.Fill(bg.rectTransform);
        UIKit.Place(UIKit.Panel(root, "TopBand", UIArt.Theme.Ink2).rectTransform, 0, 0, 1920, 176);
        UIArt.Stripe((RectTransform)root, 0, 176, 1920, 10);
        UIArt.Grain(root, 0.04f);

        titleText = UIKit.Label(root, "CHOOSE YOUR FIGHTER", 64, UIArt.Theme.Paper, TextAnchor.MiddleCenter);
        titleText.font = UIArt.Display;
        UIKit.Place(UIArt.Print(titleText, 5f).rectTransform, 0, 16, 1920, 80);

        // game modes
        for (int m = 0; m < Modes.Length; m++)
        {
            int mode = m;
            float x = 303 + m * 266;
            var b = UIKit.Button(root, "", 30, () => OnMode(mode));
            var bi = b.GetComponent<Image>(); bi.sprite = UIArt.Rounded(10); bi.type = Image.Type.Sliced;
            UIArt.Print(bi, 4f);
            modeButtons.Add(b);
            var rt = UIKit.Place((RectTransform)b.transform, x, 100, 250, 60);
            bool available = m <= 1 || m == 4;
            var label = UIKit.Label(rt, Modes[m], available ? 30 : 26, available ? UIArt.Theme.Paper : UIArt.Theme.Muted, TextAnchor.MiddleCenter);
            label.font = UIArt.Display;
            UIKit.Fill(label.rectTransform);
            if (m == 4)
            {
                label.rectTransform.offsetMax = new Vector2(0, -8);
                var sub = UIKit.Label(rt, "NO ENEMIES · SANDBOX", 13, UIKit.Gold, TextAnchor.LowerCenter, FontStyle.Bold);
                UIKit.Fill(sub.rectTransform); sub.rectTransform.offsetMin = new Vector2(0, 3);
            }
            else if (m == 1)
            {
                label.rectTransform.offsetMax = new Vector2(0, -8);
                var pad = UIKit.Label(rt, "SPLIT-SCREEN · CONTROLLER", 13, UIKit.Gold, TextAnchor.LowerCenter, FontStyle.Bold);
                UIKit.Fill(pad.rectTransform); pad.rectTransform.offsetMin = new Vector2(0, 3);
            }
            else if (!available)
            {
                label.rectTransform.offsetMax = new Vector2(0, -8);
                var soon = UIKit.Label(rt, "COMING SOON", 15, UIKit.Blood, TextAnchor.LowerCenter, FontStyle.Bold);
                UIKit.Fill(soon.rectTransform); soon.rectTransform.offsetMin = new Vector2(0, 3);
            }
        }

        // P1 (left): big preview
        var p1Img = UIArt.RoundPanel(root, "P1Frame", UIArt.Theme.Mustard, 14);
        var p1 = UIKit.Place(UIArt.Print(p1Img, 8f).rectTransform, 56, 210, 468, 684);
        var preview = UIKit.Node("Preview", p1).gameObject.AddComponent<RawImage>();
        preview.texture = previewCam.targetTexture; preview.raycastTarget = false;
        UIKit.Fill(preview.rectTransform, 4);
        Badge(p1, "P1", UIArt.Theme.Blood);
        previewMark = UIKit.Label(p1, "?", 260, new Color(1, 1, 1, 0.12f), TextAnchor.MiddleCenter, FontStyle.Bold);
        UIKit.Fill(previewMark.rectTransform);
        nameText = UIArt.Print(UIKit.Label(root, "", 60, UIArt.Theme.Paper, TextAnchor.MiddleLeft), 5f);
        nameText.font = UIArt.Display;
        UIKit.Place(nameText.rectTransform, 60, 904, 700, 66);
        weaponText = UIKit.Label(root, "", 22, UIArt.Theme.Muted, TextAnchor.UpperLeft);
        UIKit.Place(weaponText.rectTransform, 62, 970, 520, 60);

        // roster grid (middle): base 8, then DLC 8
        var mysteryPortrait = Portrait(mystery);
        var portraits = new Dictionary<CharacterLook, RenderTexture>();
        foreach (var kv in spots) portraits[kv.Key] = Portrait(kv.Value);

        UIArt.Tag((RectTransform)root, "DLC", UIArt.Theme.Mustard, UIArt.Theme.Ink, 599, 570, 90, 32, 20);
        UIKit.Place(UIKit.Panel(root, "DlcRule", new Color(1, 1, 1, 0.1f)).rectTransform, 700, 585, 620, 2);
        for (int i = 0; i < CharacterRoster.All.Length; i++)
        {
            var e = CharacterRoster.All[i];
            int col = i % 4, row = i / 4;
            float x = 599 + col * 184, y = 196 + row * 184 + (row >= 2 ? 30 : 0);
            var look = flow.LookFor(e.name);
            tiles.Add(MakeTile(root, i, e, look, x, y, look ? portraits[look] : mysteryPortrait));
        }

        // P2 (right): waiting
        var p2Img = UIArt.RoundPanel(root, "P2Frame", UIArt.Theme.Ink3, 14);
        var p2 = UIKit.Place(UIArt.Print(p2Img, 8f).rectTransform, 1396, 210, 468, 684);
        UIKit.Fill(UIArt.RoundPanel(p2, "Inside", new Color(0.07f, 0.03f, 0.035f), 12).rectTransform, 4);
        Badge(p2, "P2", UIArt.Theme.Teal);
        var q = UIKit.Label(p2, "?", 260, new Color(1, 1, 1, 0.1f), TextAnchor.MiddleCenter, FontStyle.Bold);
        UIKit.Fill(q.rectTransform);
        p2Preview = UIKit.Node("Preview", p2).gameObject.AddComponent<RawImage>();
        p2Preview.raycastTarget = false; p2Preview.enabled = false;
        UIKit.Fill(p2Preview.rectTransform, 4);
        p2Mark = q;
        p2Text = UIKit.Label(p2, "PLAYER 2\nPICK 2 PLAYER TO JOIN", 36, UIArt.Theme.Muted, TextAnchor.MiddleCenter);
        p2Text.font = UIArt.Display;
        UIKit.Place(p2Text.rectTransform, 0, 520, 468, 110);
        p2Note = UIKit.Label(p2, "Split-screen: player 2 plays on a controller. LAN and online come later.", 19, UIArt.Theme.Muted, TextAnchor.UpperCenter);
        UIKit.Place(p2Note.rectTransform, 30, 632, 408, 60);
        p2Name = UIArt.Print(UIKit.Label(root, "", 48, UIArt.Theme.Paper, TextAnchor.MiddleRight), 4f);
        p2Name.font = UIArt.Display;
        UIKit.Place(p2Name.rectTransform, 1160, 860, 700, 60);

        // bottom buttons + messages
        var back = UIArt.Button(root, "BACK", 32, () => flow.ShowTitle(), UIArt.Theme.Ink3);
        UIKit.Place((RectTransform)back.transform, 1396, 930, 200, 64);
        fightButton = UIArt.Button(root, "FIGHT!", 38, Fight, UIArt.Theme.Blood);
        UIKit.Place((RectTransform)fightButton.transform, 1620, 930, 244, 64);
        toastText = UIKit.Label(root, "", 26, UIArt.Theme.Mustard, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIKit.Place(toastText.rectTransform, 560, 1010, 800, 44);

        // run type: the story (10 rounds and a win) or endless (it keeps going; your score goes on the board)
        var runLabel = UIKit.Label(root, "RUN", 20, UIArt.Theme.Muted, TextAnchor.MiddleRight, FontStyle.Bold);
        UIKit.Place(runLabel.rectTransform, 600, 956, 90, 44);
        storyButton = UIArt.Button(root, "STORY", 24, () => { GameFlow.Endless = false; HighlightRun(); }, UIArt.Theme.Ink3);
        UIKit.Place((RectTransform)storyButton.transform, 700, 956, 190, 44);
        endlessButton = UIArt.Button(root, "ENDLESS", 24, () => { GameFlow.Endless = true; HighlightRun(); }, UIArt.Theme.Ink3);
        UIKit.Place((RectTransform)endlessButton.transform, 900, 956, 190, 44);
        HighlightRun();
        HighlightModes();
        SetCursor(0);
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(tiles[0].button.gameObject);
    }

    void HighlightRun()
    {
        foreach (var (b, on) in new[] { (storyButton, !GameFlow.Endless), (endlessButton, GameFlow.Endless) })
        {
            var cb = b.colors; cb.normalColor = on ? UIArt.Theme.Mustard : UIArt.Theme.Ink3;
            cb.highlightedColor = cb.selectedColor = on ? new Color(1f, 0.82f, 0.38f) : new Color(0.3f, 0.24f, 0.24f); b.colors = cb;
            foreach (var t in b.GetComponentsInChildren<Text>()) t.color = on ? UIArt.Theme.Ink : UIArt.Theme.Paper;
        }
    }

    void HighlightModes()
    {
        for (int m = 0; m < modeButtons.Count; m++)
        {
            var b = modeButtons[m]; var cb = b.colors;
            bool on = freeRoam ? m == 4 : m == players - 1;
            cb.normalColor = on ? UIArt.Theme.Mustard : UIArt.Theme.Ink3;
            cb.highlightedColor = cb.selectedColor = on ? new Color(1f, 0.82f, 0.38f) : new Color(0.3f, 0.24f, 0.24f);
            b.colors = cb;
            foreach (var t in b.GetComponentsInChildren<Text>())
                t.color = t.fontSize >= 26 ? (on ? UIArt.Theme.Ink : (m <= 1 || m == 4 ? UIArt.Theme.Paper : UIArt.Theme.Muted))
                        : on ? new Color(0.06f, 0.047f, 0.05f, 0.75f) : (m <= 1 || m == 4 ? UIArt.Theme.Mustard : UIArt.Theme.Blood);
        }
    }

    Tile MakeTile(Transform root, int index, CharacterRoster.Entry e, CharacterLook look, float x, float y, Texture portrait)
    {
        var t = new Tile { entry = e, look = look };
        t.frame = UIKit.Place(UIArt.RoundPanel(root, "Cursor", UIArt.Theme.Mustard, 14).rectTransform, x - 6, y - 6, 182, 182).gameObject;

        t.button = UIKit.Button(root, "", 20, () => OnTileClicked(index));
        var rt = UIKit.Place((RectTransform)t.button.transform, x, y, 170, 170);
        var ti = t.button.GetComponent<Image>(); ti.sprite = UIArt.Rounded(10); ti.type = Image.Type.Sliced;
        var cb = t.button.colors; cb.normalColor = UIArt.Theme.Ink3; cb.highlightedColor = cb.selectedColor = Color.white; t.button.colors = cb;

        var img = UIKit.Node("Portrait", rt).gameObject.AddComponent<RawImage>();
        img.texture = portrait; img.raycastTarget = false;
        UIKit.Fill(img.rectTransform, 4);
        if (!look) img.color = new Color(0.55f, 0.55f, 0.55f);

        var strip = UIKit.Place(UIKit.Panel(rt, "NameStrip", new Color(0.06f, 0.047f, 0.05f, 0.85f)).rectTransform, 4, 132, 162, 34);
        var name = UIKit.Label(strip, e.name.ToUpper(), 22, look ? UIArt.Theme.Paper : UIArt.Theme.Muted, TextAnchor.MiddleCenter);
        name.font = UIArt.Display;
        UIKit.Fill(name.rectTransform);
        if (!look)
        {
            var soonBg = UIKit.Place(UIKit.Panel(rt, "SoonBg", new Color(0, 0, 0, 0.7f)).rectTransform, 4, 8, 162, 26);
            var soon = UIKit.Label(soonBg, "COMING SOON", 15, UIArt.Theme.Blood, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Fill(soon.rectTransform);
        }

        var hover = t.button.gameObject.AddComponent<TileHover>();
        hover.onHover = () => SetCursor(index);
        return t;
    }

    static void Badge(RectTransform panel, string text, Color color)
    {
        UIArt.Print(UIArt.Tag(panel, text, color, UIArt.Theme.Paper, 14, 14, 84, 46, 30).transform.parent.GetComponent<Image>(), 3f);
    }

    // ---------- behavior ----------

    void SetCursor(int index)
    {
        cursor = index;
        for (int i = 0; i < tiles.Count; i++) tiles[i].frame.SetActive(i == index);
        var t = tiles[index];
        string info = t.look ? $"Starts with: {t.entry.startsWith}"
                    : t.entry.dlc ? "DLC character · coming soon" : $"Starts with: {t.entry.startsWith}\nComing soon";
        fightButton.interactable = t.look;
        if (picking == 0)
        {
            nameText.text = t.entry.name.ToUpper();
            nameText.color = t.look ? Color.white : UIKit.Dim;
            weaponText.text = info;
            previewMark.enabled = !t.look;
            AimPreview(previewCam, t.look ? spots[t.look] : mystery);
        }
        else
        {
            p2Name.text = t.entry.name.ToUpper();
            p2Name.color = t.look ? Color.white : UIKit.Dim;
            p2Note.text = info;
            p2Mark.enabled = !t.look;
            AimPreview(previewCam2, t.look ? spots[t.look] : mystery);
        }
    }

    // Clicking (or Enter / A on) a fighter picks them, like a fighting game.
    void OnTileClicked(int index)
    {
        SetCursor(index);
        Fight();
    }

    void OnMode(int mode)
    {
        if (mode == 4) { freeRoam = !freeRoam; players = 1; picking = 0; HighlightModes(); titleText.text = freeRoam ? "FREE ROAM: PICK WHO TO EXPLORE AS" : "CHOOSE YOUR FIGHTER"; return; }
        if (mode >= 2) { Toast($"{Modes[mode].ToLower()} is coming soon"); return; }
        freeRoam = false;
        if (mode == 1 && !ControllerConnected()) { Toast("Plug in a controller for player 2 first"); return; }
        players = mode + 1;
        picking = 0;
        HighlightModes();
        titleText.text = "CHOOSE YOUR FIGHTER";
        if (players == 2)
        {
            if (!previewCam2) previewCam2 = MakeCamera("PreviewCam2", 30f, 512, 768);
            p2Preview.texture = previewCam2.targetTexture;
            p2Text.text = "PLAYER 2 IS IN\nPLAYER 1 PICKS FIRST";
            p2Text.color = Color.white;
        }
        else
        {
            p2Preview.enabled = false; p2Mark.enabled = true; p2Name.text = "";
            p2Text.text = "PLAYER 2\nPICK 2 PLAYER TO JOIN"; p2Text.color = UIKit.Dim;
            p2Note.text = "Split-screen: player 2 plays on a controller. LAN and online come later.";
        }
        SetCursor(cursor);
    }

    static bool ControllerConnected()
    {
#if ENABLE_INPUT_SYSTEM
        return Gamepad.all.Count > 0;
#else
        return false;
#endif
    }

    void Fight()
    {
        var t = tiles[cursor];
        if (!t.look) { Toast($"{t.entry.name} is coming soon"); return; }
        if (freeRoam) { flow.StartFreeRoam(t.look); return; }
        picks[picking] = t.look;
        if (players == 2 && picking == 0)
        {
            // player 1 is locked in; player 2's turn (their pick shows on the right)
            picking = 1;
            titleText.text = "PLAYER 2: CHOOSE YOUR FIGHTER";
            p2Text.text = ""; p2Preview.enabled = true;
            nameText.text = $"{t.entry.name.ToUpper()}  ✓";
            SetCursor(cursor);
            return;
        }
        flow.StartGame(players == 2 ? new[] { picks[0], picks[1] } : new[] { picks[0] });
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
        if (back && picking == 1) { picking = 0; titleText.text = "CHOOSE YOUR FIGHTER"; p2Preview.enabled = false; OnMode(1); }   // back to P1's pick
        else if (back) flow.ShowTitle();
        else if (confirm) Fight();
        else UIKit.KeepSelected(tiles[cursor].button);
    }
}
