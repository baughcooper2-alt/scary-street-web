using UnityEngine;

// Colors, height and hair for one character. BlockyCharacter builds a body from this.
// Presets: the worker is from the web build's WORKER spec; Cooper and Nathan are matched to a photo of them.
[CreateAssetMenu(menuName = "Scary Street/Character Look", fileName = "NewCharacterLook")]
public class CharacterLook : ScriptableObject
{
    public enum HairStyle { Buzz, Swoop, Curly, Visor, Flow }   // add new styles at the end (saved as numbers)
    public enum ShirtGraphic { None, BasketballHoop }

    public string displayName = "Someone";
    [Range(1.4f, 2.1f)] public float height = 1.8f;
    [Tooltip("Use the web build's smooth, rigged human body (with fitted clothes and real hair) instead of the stylized one.")]
    public bool realisticBody;
    [Tooltip("Realistic body only: colour the clothes onto the body instead of separate clothing meshes (cheaper, for crowds).")]
    public bool simpleClothes;

    [Header("Body")]
    public Color skin = Hex("#e4b996");
    public Color eyes = Hex("#5a3f2a");
    public Color brows = Hex("#241810");

    [Header("Clothes")]
    public Color shirt = Hex("#c8201e");
    public bool longSleeves;
    [Tooltip("Baggy tee: wider, longer body and sleeves down toward the elbow.")]
    public bool oversizedShirt;
    public ShirtGraphic shirtGraphic;
    public Color pants = Hex("#1c1c1f");
    public bool shorts;
    public Color socks = Hex("#f4f4f4");
    public Color shoes = Hex("#141414");
    [Tooltip("Band on the left wrist.")]
    public bool wristband;
    public Color wristbandColor = Hex("#f2f2f2");

    [Header("Hair and hats")]
    public HairStyle hairStyle = HairStyle.Buzz;
    [Tooltip("Realistic body only: a real hair model from HairAssets (\"cooper_flow\", \"nathan_curls\"); blank = grown hair only.")]
    public string hairAsset = "";
    [Tooltip("Resources/RealBody/<name>.bytes to use instead of building a body (empty = the display name, if that file exists).")]
    public string realModel = "";
    public Color hair = Hex("#2a1d14");
    public bool wearsCap;
    public Color cap = Hex("#111111");

    [Header("Work uniform")]
    public bool workerUniform;
    public Color collar = Hex("#f2c230");
    [Tooltip("Visor band and brim, for the Visor hair style.")]
    public Color visorBand = Hex("#c8201e"), visorBrim = Hex("#f2c230");

    [Header("Crowds")]
    [Tooltip("If set, each spawned copy picks one of these skin tones.")]
    public Color[] skinVariants = new Color[0];
    public Color[] hairVariants = new Color[0];

    // "#rrggbb" → Color. Plain C# (not ColorUtility) so it's safe in field initializers.
    public static Color Hex(string hex)
    {
        int v = System.Convert.ToInt32(hex.TrimStart('#'), 16);
        return new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f);
    }

    // ---------- presets from the web build ----------

    public static CharacterLook Preset(string id)
    {
        var l = CreateInstance<CharacterLook>();
        l.ApplyPreset(id);
        return l;
    }

    public void ApplyPreset(string id)
    {
        switch (id)
        {
            case "worker":
                displayName = "McDonald's Worker"; height = 1.76f; realisticBody = true; simpleClothes = true;
                skin = Hex("#e4b996"); shirt = Hex("#c8201e"); longSleeves = false;
                pants = Hex("#1c1c1f"); shorts = false; shoes = Hex("#141414");
                hair = Hex("#2a1d14"); brows = Hex("#241810"); hairStyle = HairStyle.Visor; wearsCap = false;
                oversizedShirt = false; shirtGraphic = ShirtGraphic.None; wristband = false;
                workerUniform = true; collar = Hex("#f2c230"); visorBand = Hex("#c8201e"); visorBrim = Hex("#f2c230");
                skinVariants = new[] { Hex("#f0c9a4"), Hex("#e4b996"), Hex("#c98e66"), Hex("#9c6b4a"), Hex("#6e4a33") };
                hairVariants = new[] { Hex("#2a1d14"), Hex("#1a1410"), Hex("#7a5634"), Hex("#b98b52"), Hex("#0f0d0b") };
                break;

            case "cooper":                                                  // from Cooper's photo: faded gray oversized tee with a
                displayName = "Cooper"; height = 1.86f; realisticBody = true; simpleClothes = false;                    // basketball hoop graphic, black shorts, white wristband
                skin = Hex("#e6bea2"); eyes = Hex("#3f3129"); shirt = Hex("#5f5a55"); longSleeves = false;
                oversizedShirt = true; shirtGraphic = ShirtGraphic.BasketballHoop;
                pants = Hex("#1b1b1d"); shorts = false; socks = Hex("#f4f4f4"); shoes = Hex("#e8e8e8");
                wristband = true; wristbandColor = Hex("#f2f2f2");
                hair = Hex("#8f6c4c"); brows = Hex("#6e5038"); hairStyle = HairStyle.Flow; wearsCap = false; hairAsset = "cooper_flow";
                workerUniform = false; skinVariants = new Color[0]; hairVariants = new Color[0];
                break;

            case "mystery":                                                 // select-screen silhouette for characters not built yet
                displayName = "???"; height = 1.8f; realisticBody = false;
                skin = eyes = brows = shirt = pants = socks = shoes = hair = Hex("#0c0a0b");
                longSleeves = true; oversizedShirt = false; shirtGraphic = ShirtGraphic.None; shorts = false; wristband = false;
                hairStyle = HairStyle.Swoop; wearsCap = false; workerUniform = false;
                skinVariants = new Color[0]; hairVariants = new Color[0];
                break;

            case "jack":                                                    // web build's JACK: khaki tee, light-blue shorts, curly brown hair
                displayName = "Jack"; height = 1.84f; realisticBody = true; simpleClothes = false;
                skin = Hex("#e8c19c"); eyes = Hex("#4a3322"); shirt = Hex("#c8b48a"); longSleeves = false;
                oversizedShirt = false; shirtGraphic = ShirtGraphic.None; wristband = false;
                pants = Hex("#9cc8e0"); shorts = true; socks = Hex("#f4f4f4"); shoes = Hex("#f2f2f2");
                hair = Hex("#5a3a22"); brows = Hex("#4a2e1a"); hairStyle = HairStyle.Curly; wearsCap = false;
                workerUniform = false; skinVariants = new Color[0]; hairVariants = new Color[0];
                break;

            case "courier":                                                 // web build's DoorDash driver: red shirt and cap
                displayName = "DoorDash Driver"; height = 1.8f; realisticBody = true; simpleClothes = true;
                skin = Hex("#8d5a3b"); eyes = Hex("#2a1c14"); shirt = Hex("#b3261e"); longSleeves = true;
                oversizedShirt = false; shirtGraphic = ShirtGraphic.None; wristband = false;
                pants = Hex("#2b2b2e"); shorts = false; shoes = Hex("#1a1a1a");
                hair = Hex("#141010"); brows = Hex("#141010"); hairStyle = HairStyle.Curly; wearsCap = true; cap = Hex("#b3261e");
                workerUniform = false; skinVariants = new Color[0]; hairVariants = new Color[0];
                break;

            case "nathan":                                                  // from Nathan's photo: all black, dark brown cap over curls
                displayName = "Nathan"; height = 1.8f; realisticBody = true; simpleClothes = false;
                skin = Hex("#e2bb9e"); eyes = Hex("#3b2a20"); shirt = Hex("#1c1919"); longSleeves = true;
                oversizedShirt = false; shirtGraphic = ShirtGraphic.None; wristband = false;
                pants = Hex("#1d1713"); shorts = false; shoes = Hex("#1b1b1d");
                hair = Hex("#0c0b0b"); brows = Hex("#141010"); hairStyle = HairStyle.Curly; wearsCap = true; cap = Hex("#3f3530"); hairAsset = "nathan_curls";
                workerUniform = false; skinVariants = new Color[0]; hairVariants = new Color[0];
                break;
        }
    }
}
