using UnityEngine;

// Particle smoke for the cart: soft billowing puffs, O-rings made of wisps, and the big Blinker cloud.
// Visual only; SmokeShot does the damage. Uses a generated 2×2 sheet of noisy smoke blobs on URP's
// Particles/Unlit shader: cartoon puffs, white with a grey outline and shadow, so shots are easy to follow.
public static class SmokeFx
{
    static Material shared;
    static Texture2D sheet;

    // ---------- look ----------

    // 256×256: four 128 px cartoon cloud puffs (each a union of overlapping circles): flat white fill, a soft grey
    // shadow on the lower right, and a crisp grey outline, so every puff reads clearly against anything.
    public static Texture2D MakeSheet()
    {
        const int cell = 128, n = cell * 2;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "SmokeSheet", wrapMode = TextureWrapMode.Clamp };
        var px = new Color[n * n];
        var rng = new System.Random(5);
        for (int c = 0; c < 4; c++)
        {
            int cx = (c % 2) * cell, cy = (c / 2) * cell;
            int k = 4 + c % 2;
            var circles = new Vector3[k];                                    // x, y, radius (cell units, centre 0.5)
            circles[0] = new Vector3(0.5f, 0.47f, 0.27f);
            for (int i = 1; i < k; i++)
            {
                float a = (float)(i * 6.283 / (k - 1) + rng.NextDouble() * 0.8), d = 0.13f + (float)rng.NextDouble() * 0.06f;
                circles[i] = new Vector3(0.5f + Mathf.Cos(a) * d, 0.5f + Mathf.Sin(a) * d, 0.15f + (float)rng.NextDouble() * 0.07f);
            }
            float Sdf(float u, float v) { float m = 9f; foreach (var q in circles) m = Mathf.Min(m, new Vector2(u - q.x, v - q.y).magnitude - q.z); return m; }
            for (int y = 0; y < cell; y++) for (int x = 0; x < cell; x++)
            {
                float u = (x + 0.5f) / cell, v = (y + 0.5f) / cell;
                float d = Sdf(u, v);
                float alpha = Mathf.Clamp01(0.5f - d * cell / 1.5f);                  // crisp edge (1.5 px)
                float lit = Sdf(u + 0.07f, v - 0.07f) < -0.02f ? 1f : 0f;               // shadow on the side away from the light
                float shade = Mathf.Lerp(0.8f, 1f, Mathf.SmoothStep(0, 1, lit));
                float line = Mathf.Clamp01((d * cell + 4.5f) / 1.5f);                   // outline band just inside the edge
                float g = Mathf.Lerp(shade, 0.5f, line);
                px[(cy + y) * n + cx + x] = new Color(g, g, g, alpha);
            }
        }
        tex.SetPixels(px);
        tex.Apply(true);
        return tex;
    }

    static float Fbm(float x, float y)
    {
        float sum = 0, amp = 0.5f, norm = 0;
        for (int o = 0; o < 4; o++) { sum += Mathf.PerlinNoise(x, y) * amp; norm += amp; x *= 2.03f; y *= 2.03f; amp *= 0.5f; }
        return sum / norm;
    }

    // Transparent, unlit particles (the smoke's grey comes from the particle colours, so no sky tint), with a short
    // fade right in front of the camera so a cloud blown from your mouth doesn't white out the screen.
    // No soft particles: they need the camera depth texture and fade everything out without it.
    public const string ShaderName = "Universal Render Pipeline/Particles/Unlit";
    public static void Setup(Material m)
    {
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f); m.SetFloat("_ZWrite", 0f); m.SetFloat("_Cull", 2f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON"); m.DisableKeyword("_ALPHAMODULATE_ON");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        m.SetColor("_BaseColor", Color.white);

        const float near = 0.05f, far = 0.45f;
        m.SetFloat("_CameraFadingEnabled", 1f);
        m.SetFloat("_CameraNearFadeDistance", near); m.SetFloat("_CameraFarFadeDistance", far);
        m.SetVector("_CameraFadeParams", new Vector4(near, 1f / (far - near), 0, 0));      // URP: (start, 1 / length)
        m.EnableKeyword("_FADING_ON");
        m.SetFloat("_SoftParticlesEnabled", 0f); m.DisableKeyword("_SOFTPARTICLES_ON");
        m.DisableKeyword("_EMISSION");
    }

    // The saved material (Set Up Player makes Assets/Weapons/SmokeParticles.mat so builds keep the shader) or one made here.
    public static Material Material(Material template)
    {
        if (shared && shared.mainTexture && sheet) return shared;
        var sh = Shader.Find(ShaderName);
        if (template && template.shader.name == ShaderName) shared = new Material(template);
        else shared = new Material(sh ? sh : template ? template.shader : Shader.Find("Sprites/Default"));
        shared.name = "SmokeParticles (runtime)";
        Setup(shared);                                                          // also fixes up older saved copies
        if (!sheet) sheet = MakeSheet();                                        // runtime textures die when Play stops
        shared.mainTexture = sheet; shared.SetTexture("_BaseMap", sheet);      // always the current sheet
        return shared;
    }

    // ---------- the effects ----------

    // Builds the effect under `parent` (the moving SmokeShot), facing `dir`.
    public static ParticleSystem Attach(Transform parent, SmokeShot.Kind kind, Vector3 dir, Material template)
    {
        var mat = Material(template);
        var rot = Quaternion.LookRotation(dir);
        switch (kind)
        {
            case SmokeShot.Kind.Ring:
            {
                // the ring: wisps on a circle that travel with it and spread out; plus a faint trail left behind
                var ring = Make(parent, "RingFx", rot, mat, local: true, color: new Color(1f, 1f, 1f, 0.95f),
                                size: (0.08f, 0.14f), life: (1.1f, 1.3f), speed: (0f, 0.05f), grow: 2.4f, max: 160);
                Burst(ring, 130);
                var sh = ring.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.15f; sh.radiusThickness = 0f;
                var vel = ring.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
                vel.radial = new ParticleSystem.MinMaxCurve(0.22f, 0.32f);
                vel.x = vel.y = vel.z = new ParticleSystem.MinMaxCurve(0f);
                Noise(ring, 0.06f, 2.2f);
                Fade(ring, 0.04f, 0.65f);
                var trail = Make(ring.transform, "RingTrail", rot, mat, local: false, color: new Color(1f, 1f, 1f, 0.7f),
                                 size: (0.06f, 0.1f), life: (0.5f, 0.9f), speed: (0f, 0.1f), grow: 3f, max: 120);
                var te = trail.emission; te.rateOverDistance = 22f;
                var ts = trail.shape; ts.shapeType = ParticleSystemShapeType.Circle; ts.radius = 0.2f; ts.radiusThickness = 0f;
                Noise(trail, 0.25f, 1.2f);
                trail.gameObject.SetActive(true);
                ring.gameObject.SetActive(true);
                return ring;
            }
            case SmokeShot.Kind.Blast:
            {
                // the Blinker: a big, thick, slightly green cloud that rolls forward and hangs in the air
                var ps = Make(parent, "BlinkerFx", rot, mat, local: false, color: new Color(0.95f, 1f, 0.95f, 0.95f),
                              size: (0.45f, 0.85f), life: (1.8f, 3f), speed: (2f, 8.5f), grow: 4.5f, max: 220);
                var em = ps.emission;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, 38), new ParticleSystem.Burst(0.08f, 14) });
                em.rateOverDistance = 8f;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 20f; sh.radius = 0.15f;
                Drag(ps, 1.4f);
                Noise(ps, 0.6f, 0.4f);
                Fade(ps, 0.06f, 0.5f);
                var main = ps.main; main.gravityModifier = -0.03f;
                Head(ps, mat, new Color(0.95f, 1f, 0.95f, 1f), (0.8f, 1.1f), 45f);
                ps.gameObject.SetActive(true);
                return ps;
            }
            default:
            {
                // a puff: a burst that plumes out of your mouth, and a trail that thins behind it
                // short and airy, so the O-ring that follows it shows through
                var ps = Make(parent, "PuffFx", rot, mat, local: false, color: new Color(1f, 1f, 1f, 0.7f),
                              size: (0.16f, 0.3f), life: (0.7f, 1.2f), speed: (1f, 3f), grow: 3f, max: 90);
                Burst(ps, 7);
                var em = ps.emission; em.rateOverDistance = 6f;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 10f; sh.radius = 0.04f;
                Drag(ps, 3f);
                Noise(ps, 0.35f, 0.6f);
                Fade(ps, 0.08f, 0.45f);
                var main = ps.main; main.gravityModifier = -0.02f;
                Head(ps, mat, new Color(1f, 1f, 1f, 0.8f), (0.22f, 0.3f), 28f);
                ps.gameObject.SetActive(true);
                return ps;
            }
        }
    }

    // The dense core that rides on the hitbox, so you can see exactly where the shot is.
    static void Head(ParticleSystem parent, Material mat, Color color, (float, float) size, float rate)
    {
        var head = Make(parent.transform, "Head", parent.transform.rotation, mat, local: false, color: color,
                        size: size, life: (0.18f, 0.32f), speed: (0f, 0.2f), grow: 1.7f, max: 60);
        var main = head.main; main.loop = true;
        var em = head.emission; em.rateOverTime = rate;
        var sh = head.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = size.Item1 * 0.25f;
        Fade(head, 0.1f, 0.4f);
        head.gameObject.SetActive(true);
    }

    // Stop emitting and let what's in the air finish on its own (the SmokeShot is about to be destroyed).
    public static void Release(ParticleSystem ps)
    {
        if (!ps) return;
        ps.transform.SetParent(null, true);
        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    static ParticleSystem Make(Transform parent, string name, Quaternion rot, Material mat, bool local, Color color,
                               (float, float) size, (float, float) life, (float, float) speed, float grow, int max)
    {
        var go = new GameObject(name);
        go.SetActive(false);                                                    // configure before it starts playing
        go.transform.SetParent(parent, false);
        go.transform.rotation = rot;
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = false; main.duration = 1f; main.playOnAwake = true;
        main.simulationSpace = local ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life.Item1, life.Item2);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.Item1, speed.Item2);
        main.startSize = new ParticleSystem.MinMaxCurve(size.Item1, size.Item2);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(color.r * 0.93f, color.g * 0.93f, color.b * 0.93f, color.a), color);   // two tones so clumps stand apart
        main.maxParticles = max;
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.scalingMode = ParticleSystemScalingMode.Local;

        var em = ps.emission; em.rateOverTime = 0f;
        var sh = ps.shape; sh.enabled = true;

        var sol = ps.sizeOverLifetime; sol.enabled = true;                      // swell fast, then keep spreading
        sol.size = new ParticleSystem.MinMaxCurve(grow, new AnimationCurve(new Keyframe(0f, 1f / grow), new Keyframe(0.3f, 0.7f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0.55f)));
        var rol = ps.rotationOverLifetime; rol.enabled = true;                  // slow curl
        rol.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
        var tsa = ps.textureSheetAnimation; tsa.enabled = true;                 // each particle picks one of the 4 blobs
        tsa.numTilesX = 2; tsa.numTilesY = 2;
        tsa.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 0.999f);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sortMode = ParticleSystemSortMode.Distance;
        r.maxParticleSize = 1.5f;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    static void Burst(ParticleSystem ps, int count)
    {
        var em = ps.emission;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
    }

    static void Drag(ParticleSystem ps, float drag)
    {
        var lv = ps.limitVelocityOverLifetime; lv.enabled = true;
        lv.limit = 100f; lv.drag = drag; lv.multiplyDragByParticleSize = false; lv.multiplyDragByParticleVelocity = true;
    }

    static void Noise(ParticleSystem ps, float strength, float frequency)
    {
        var nz = ps.noise; nz.enabled = true;
        nz.strength = strength * 0.6f; nz.frequency = frequency;              // calmer drift keeps the cartoon shapes nz.scrollSpeed = 0.35f; nz.damping = true;
        nz.octaveCount = 2; nz.quality = ParticleSystemNoiseQuality.Medium;
    }

    // Quick fade in, hold, then thin out to nothing.
    static void Fade(ParticleSystem ps, float inAt, float holdTo)
    {
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, inAt), new GradientAlphaKey(1f, Mathf.Max(holdTo, 0.72f)), new GradientAlphaKey(0f, 1f) });
        col.color = g;
    }
}
