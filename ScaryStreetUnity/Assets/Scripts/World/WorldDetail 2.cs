using System.Collections.Generic;
using UnityEngine;

// Adds surface detail to the whole house when the game starts (the model and scene aren't touched):
// every glTF material is swapped for a URP Lit copy with the same colour, texture and tiling, plus
//   • a normal map baked from its own texture (brick mortar, plank seams, siding and plaster get real relief)
//   • a fine detail layer picked by what the surface looks like: wood grain, plaster stipple, concrete speckle,
//     fabric weave, grass blades, or plain grain (the house UVs are in metres, so it tiles every 0.5 m)
//   • a sensible finish: floors, wood and tiles a little glossy, walls and fabric matte.
// Put this on the world root (Tools > Scary Street > Add World Detail).
public class WorldDetail : MonoBehaviour
{
    public enum Surface { Plain, Wood, Plaster, Concrete, Fabric, Grass, Brick, Metal }

    [Range(0, 2)] public float bumpStrength = 1f;
    [Range(0, 2)] public float detailStrength = 1f;
    [Tooltip("Metres per repeat of the fine detail layer.")]
    public float detailSize = 0.5f;
    [Tooltip("A saved material with normal + detail maps switched on, so player builds keep those shader variants.")]
    public Material variantKeeper;

    static readonly Dictionary<Texture, Texture2D> normals = new Dictionary<Texture, Texture2D>();
    static readonly Dictionary<Surface, (Texture2D albedo, Texture2D normal)> details = new Dictionary<Surface, (Texture2D, Texture2D)>();
    readonly Dictionary<Material, Material> upgraded = new Dictionary<Material, Material>();

    static readonly int BaseColorTex = Shader.PropertyToID("baseColorTexture"), BaseColorFactor = Shader.PropertyToID("baseColorFactor");
    static readonly int RoughnessFactor = Shader.PropertyToID("roughnessFactor"), MetallicFactor = Shader.PropertyToID("metallicFactor");

    void Awake()
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        if (!lit) return;
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (!m) continue;
                if (!upgraded.TryGetValue(m, out var up)) upgraded[m] = up = Upgrade(m, lit);
                if (up != m) { mats[i] = up; changed = true; }
            }
            if (changed) r.sharedMaterials = mats;
        }
    }

    Material Upgrade(Material src, Shader lit)
    {
        // leave see-through, unlit and already-custom materials alone
        if (src.shader == lit || src.renderQueue >= 2450 || src.shader.name.ToLower().Contains("unlit")) return src;

        Texture tex = src.HasProperty(BaseColorTex) ? src.GetTexture(BaseColorTex) : src.mainTexture;
        Color color = src.HasProperty(BaseColorFactor) ? src.GetColor(BaseColorFactor) : src.color;
        Vector2 scale = src.HasProperty(BaseColorTex) ? src.GetTextureScale(BaseColorTex) : Vector2.one;
        Vector2 offset = src.HasProperty(BaseColorTex) ? src.GetTextureOffset(BaseColorTex) : Vector2.zero;
        float rough = src.HasProperty(RoughnessFactor) ? src.GetFloat(RoughnessFactor) : 0.8f;
        float metal = src.HasProperty(MetallicFactor) ? src.GetFloat(MetallicFactor) : 0f;
        var kind = Classify(color, tex, metal);

        var m = new Material(lit) { name = src.name + " (detail)" };
        m.SetColor("_BaseColor", color);
        if (tex) { m.SetTexture("_BaseMap", tex); m.SetTextureScale("_BaseMap", scale); m.SetTextureOffset("_BaseMap", offset); }
        m.SetFloat("_Metallic", metal);
        m.SetFloat("_Smoothness", Finish(kind, 1f - rough));

        // relief from the surface's own pattern
        if (tex && bumpStrength > 0)
        {
            var n = NormalFrom(tex, kind);
            if (n) { m.SetTexture("_BumpMap", n); m.SetFloat("_BumpScale", bumpStrength); m.EnableKeyword("_NORMALMAP"); }
        }
        // fine detail on top (UVs are in metres)
        if (detailStrength > 0)
        {
            var d = Detail(kind);
            m.SetTexture("_DetailAlbedoMap", d.albedo);
            m.SetTexture("_DetailNormalMap", d.normal);
            m.SetTextureScale("_DetailAlbedoMap", Vector2.one / Mathf.Max(0.05f, detailSize));
            m.SetFloat("_DetailAlbedoMapScale", detailStrength);
            m.SetFloat("_DetailNormalMapScale", detailStrength * (kind == Surface.Grass || kind == Surface.Fabric ? 1f : 0.6f));
            m.SetTexture("_DetailMask", Texture2D.whiteTexture);
            m.EnableKeyword(Mathf.Approximately(detailStrength, 1f) ? "_DETAIL_MULX2" : "_DETAIL_SCALED");
        }
        return m;
    }

    // What kind of surface is this, going by its colour (the web build tinted shared grey textures).
    static Surface Classify(Color c, Texture tex, float metal)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        if (metal > 0.5f) return Surface.Metal;
        if (h > 0.18f && h < 0.45f && s > 0.25f) return Surface.Grass;
        if (h < 0.06f && s > 0.45f && v > 0.35f) return Surface.Brick;
        if ((h < 0.12f || h > 0.95f) && s > 0.3f && v > 0.15f && v < 0.8f) return Surface.Wood;
        if (s < 0.12f && v > 0.7f) return Surface.Plaster;
        if (s < 0.15f) return Surface.Concrete;
        return tex ? Surface.Plain : Surface.Fabric;
    }

    static float Finish(Surface k, float gltfSmooth) => k switch
    {
        Surface.Wood => 0.38f, Surface.Metal => 0.7f, Surface.Plaster => 0.12f, Surface.Concrete => 0.1f,
        Surface.Fabric => 0.05f, Surface.Grass => 0.08f, Surface.Brick => 0.08f, _ => Mathf.Clamp(gltfSmooth, 0.1f, 0.5f),
    };

    // ---------- normal map from a texture's brightness ----------

    static Texture2D NormalFrom(Texture tex, Surface kind)
    {
        if (normals.TryGetValue(tex, out var n) && n) return n;
        int w = Mathf.Min(tex.width, 512), h = Mathf.Min(tex.height, 512);
        if (w < 8 || h < 8) return normals[tex] = null;
        // read any texture (imported ones aren't CPU-readable) by drawing it into a RenderTexture
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        Graphics.Blit(tex, rt);
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var read = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
        read.ReadPixels(new Rect(0, 0, w, h), 0, 0); read.Apply();
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);

        var px = read.GetPixels(); Destroy(read);
        var height = new float[w * h];
        for (int i = 0; i < px.Length; i++) height[i] = px[i].grayscale;
        float k = kind == Surface.Brick || kind == Surface.Wood ? 5f : kind == Surface.Plaster ? 2.5f : 3.5f;
        n = ToNormalMap(height, w, h, k);
        n.name = tex.name + "_normal";
        return normals[tex] = n;
    }

    static Texture2D ToNormalMap(float[] height, int w, int h, float strength)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
        var o = new Color[w * h];
        float H(int x, int y) => height[((y % h + h) % h) * w + ((x % w + w) % w)];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (H(x + 1, y) - H(x - 1, y)) * strength, dy = (H(x, y + 1) - H(x, y - 1)) * strength;
                var nrm = new Vector3(-dx, -dy, 1f).normalized;
                o[y * w + x] = new Color(nrm.x * 0.5f + 0.5f, nrm.y * 0.5f + 0.5f, nrm.z * 0.5f + 0.5f, 1f);
            }
        tex.SetPixels(o); tex.Apply(true);
        return tex;
    }

    // ---------- fine detail layers (tileable, mid-grey = no change) ----------

    static (Texture2D albedo, Texture2D normal) Detail(Surface kind)
    {
        if (details.TryGetValue(kind, out var d) && d.albedo && d.normal) return d;
        const int n = 256;
        var hgt = new float[n * n];
        var rng = new System.Random((int)kind * 97 + 13);
        float[] seeds = new float[8]; for (int i = 0; i < 8; i++) seeds[i] = (float)rng.NextDouble() * 100f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)n, v = y / (float)n, val;
                switch (kind)
                {
                    case Surface.Wood:       // long grain lines with a little waviness
                        val = 0.5f + 0.35f * Mathf.Sin((v * 38f + Tile(u, v, 4, seeds[0]) * 2.5f) * Mathf.PI) * (0.6f + 0.4f * Tile(u, v, 16, seeds[1]));
                        break;
                    case Surface.Plaster:    // soft stipple
                        val = 0.5f + 0.25f * (Tile(u, v, 32, seeds[0]) - 0.5f) + 0.15f * (Tile(u, v, 8, seeds[1]) - 0.5f);
                        break;
                    case Surface.Concrete:   // speckle + blotches
                        val = 0.5f + 0.3f * (Tile(u, v, 64, seeds[0]) - 0.5f) + 0.2f * (Tile(u, v, 6, seeds[1]) - 0.5f);
                        break;
                    case Surface.Fabric:     // woven threads
                        val = 0.5f + 0.18f * Mathf.Sin(u * 96f * Mathf.PI) * Mathf.Sin(v * 96f * Mathf.PI) + 0.1f * (Tile(u, v, 32, seeds[0]) - 0.5f);
                        break;
                    case Surface.Grass:      // blades: stretched noise
                        val = 0.5f + 0.4f * (Tile(u * 4f, v, 24, seeds[0]) - 0.5f) + 0.25f * (Tile(u, v, 48, seeds[1]) - 0.5f);   // whole-number stretch keeps it tileable
                        break;
                    case Surface.Brick:      // pitted clay
                        val = 0.5f + 0.25f * (Tile(u, v, 48, seeds[0]) - 0.5f) + 0.15f * (Tile(u, v, 12, seeds[1]) - 0.5f);
                        break;
                    case Surface.Metal:      // brushed
                        val = 0.5f + 0.12f * (Tile(u, v * 8f, 8, seeds[0]) - 0.5f);
                        break;
                    default:                 // light grain
                        val = 0.5f + 0.18f * (Tile(u, v, 32, seeds[0]) - 0.5f);
                        break;
                }
                hgt[y * n + x] = Mathf.Clamp01(val);
            }

        var albedo = new Texture2D(n, n, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4, name = kind + "_detail" };
        var cols = new Color[n * n];
        float contrast = kind == Surface.Metal ? 0.3f : 0.6f;
        for (int i = 0; i < cols.Length; i++) { float g = 0.5f + (hgt[i] - 0.5f) * contrast; cols[i] = new Color(g, g, g, 1); }
        albedo.SetPixels(cols); albedo.Apply(true);
        var normal = ToNormalMap(hgt, n, n, kind == Surface.Grass || kind == Surface.Fabric ? 6f : 3f);
        normal.name = kind + "_detailNormal";
        return details[kind] = (albedo, normal);
    }

    // Tileable value noise: `cells` cells across the texture, wrapping at the edges.
    static float Tile(float u, float v, int cells, float seed)
    {
        float x = u * cells, y = v * cells;
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        float a = Hash(x0, y0, cells, seed), b = Hash(x0 + 1, y0, cells, seed), c = Hash(x0, y0 + 1, cells, seed), d = Hash(x0 + 1, y0 + 1, cells, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static float Hash(int x, int y, int cells, float seed)
    {
        x = ((x % cells) + cells) % cells; y = ((y % cells) + cells) % cells;
        float h = Mathf.Sin(x * 127.1f + y * 311.7f + seed * 74.7f) * 43758.5453f;
        return h - Mathf.Floor(h);
    }
}
