using System;
using System.Collections.Generic;
using UnityEngine;

public enum Sfx
{
    Punch, Hit, Whoosh, Puff, Inhale, Ring, Blinker, Strum, Ding, Slam, Objection,
    DoorOpen, DoorClose, Xp, Cash, LevelUp, Hurt, EnemyDown, Knock, Doorbell,
    Throw, Splat, Blah, WahWah, Fart, BigFart, RoundStart, RoundClear, Boss, Click, Buy,
}

public enum MusicTrack { None, Menu, Fight }

// Every sound in the game, synthesized in code the first time it's played (no audio files needed):
// plucked strings (Karplus–Strong) for the guitar, filtered noise for smoke / whooshes / farts,
// pitch sweeps for hits, blips and arpeggios for pickups, and two procedurally written music loops.
// SoundKit.Play(Sfx.X) for flat UI-style sounds, SoundKit.PlayAt(Sfx.X, position) for sounds in the world.
public static class SoundKit
{
    const int Rate = 44100;
    public const string MusicVolumeKey = "musicVolume";

    static readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
    static readonly Dictionary<MusicTrack, AudioClip> music = new Dictionary<MusicTrack, AudioClip>();
    static GameObject host;
    static AudioSource flat, musicA, musicB;
    static AudioSource[] pool;
    static int next;
    static MusicTrack playing;
    static readonly System.Random rng = new System.Random(1234);

    // ---------- playback ----------

    public static void Play(Sfx s, float volume = 1f, float pitchJitter = 0.05f)
    {
        Ensure();
        flat.pitch = 1f + UnityEngine.Random.Range(-pitchJitter, pitchJitter);
        flat.PlayOneShot(Clip(s), volume);
    }

    public static void PlayAt(Sfx s, Vector3 pos, float volume = 1f, float pitchJitter = 0.06f)
    {
        Ensure();
        var src = pool[next = (next + 1) % pool.Length];
        src.transform.position = pos;
        src.pitch = 1f + UnityEngine.Random.Range(-pitchJitter, pitchJitter);
        src.clip = Clip(s);
        src.volume = volume;
        src.Play();
    }

    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat(MusicVolumeKey, 0.5f);
        set { PlayerPrefs.SetFloat(MusicVolumeKey, value); if (musicA) musicA.volume = value; }
    }

    public static void PlayMusic(MusicTrack track)
    {
        Ensure();
        if (track == playing) return;
        playing = track;
        // swap sources so the old track fades out while the new one fades in
        (musicA, musicB) = (musicB, musicA);
        if (track == MusicTrack.None) { musicA.Stop(); return; }
        if (!music.TryGetValue(track, out var clip) || !clip) music[track] = clip = track == MusicTrack.Menu ? MakeMusic(88, false) : MakeMusic(112, true);
        musicA.clip = clip;
        musicA.volume = 0;
        musicA.Play();
        host.GetComponent<MusicFader>().Begin(musicA, musicB);
    }

    static void Ensure()
    {
        if (host) return;
        host = new GameObject("SoundKit");
        UnityEngine.Object.DontDestroyOnLoad(host);
        flat = host.AddComponent<AudioSource>();
        flat.playOnAwake = false;
        musicA = host.AddComponent<AudioSource>(); musicA.loop = true; musicA.playOnAwake = false;
        musicB = host.AddComponent<AudioSource>(); musicB.loop = true; musicB.playOnAwake = false;
        host.AddComponent<MusicFader>();
        pool = new AudioSource[16];
        for (int i = 0; i < pool.Length; i++)
        {
            var go = new GameObject("Sfx3D");
            go.transform.SetParent(host.transform);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false; s.spatialBlend = 1f; s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 2f; s.maxDistance = 35f; s.dopplerLevel = 0f;
            pool[i] = s;
        }
    }

    // ---------- sound design ----------

    static AudioClip Clip(Sfx s)
    {
        if (clips.TryGetValue(s, out var c) && c) return c;
        float[] b = s switch
        {
            Sfx.Punch => Mix(Sweep(0.14f, 170, 55, 0.9f), Noise(0.05f, 0.35f, 0.4f)),
            Sfx.Hit => Mix(Sweep(0.12f, 260, 90, 0.7f), Noise(0.06f, 0.5f, 0.6f)),
            Sfx.Whoosh => Swell(Noise(0.25f, 0.5f, 0.15f), 0.4f),
            Sfx.Puff => Swell(Noise(0.45f, 0.45f, 0.06f), 0.15f),
            Sfx.Inhale => Swell(Noise(0.6f, 0.3f, 0.12f), 0.85f),
            Sfx.Ring => Mix(Swell(Noise(0.35f, 0.35f, 0.08f), 0.2f), Sweep(0.3f, 300, 520, 0.25f)),
            Sfx.Blinker => Mix(Swell(Noise(1.0f, 0.9f, 0.05f), 0.1f), Sweep(0.8f, 90, 30, 0.9f)),
            Sfx.Strum => Strum(),
            Sfx.Ding => Tone(0.35f, 1320, 0.4f, 9f, true),
            Sfx.Slam => Mix(Sweep(0.5f, 110, 30, 1f), Noise(0.3f, 0.7f, 0.25f)),
            Sfx.Objection => Stinger(),
            Sfx.DoorOpen => Creak(0.55f, 240, 1),
            Sfx.DoorClose => Mix(Sweep(0.15f, 120, 60, 0.8f), Noise(0.08f, 0.4f, 0.3f)),
            Sfx.Xp => Arp(new[] { 880f, 1175f }, 0.05f, 0.35f),
            Sfx.Cash => Mix(Arp(new[] { 1568f, 2093f }, 0.07f, 0.4f), Noise(0.12f, 0.15f, 0.9f)),
            Sfx.LevelUp => Arp(new[] { 523f, 659f, 784f, 1047f }, 0.09f, 0.5f),
            Sfx.Hurt => Sweep(0.22f, 320, 140, 0.6f, square: true),
            Sfx.EnemyDown => Sweep(0.45f, 420, 90, 0.5f, square: true),
            Sfx.Knock => Knocks(),
            Sfx.Doorbell => Mix(Tone(0.7f, 784, 0.45f, 3f, true), Delay(Tone(0.9f, 622, 0.45f, 3f, true), 0.4f)),
            Sfx.Throw => Swell(Noise(0.2f, 0.4f, 0.2f), 0.5f),
            Sfx.Splat => Mix(Noise(0.18f, 0.6f, 0.35f), Sweep(0.1f, 200, 80, 0.4f)),
            Sfx.Blah => Blah(),
            Sfx.WahWah => WahWah(),
            Sfx.Fart => Fart(0.6f),
            Sfx.BigFart => Fart(1.3f),
            Sfx.RoundStart => Arp(new[] { 392f, 392f, 523f }, 0.14f, 0.45f, square: true),
            Sfx.RoundClear => Arp(new[] { 523f, 659f, 784f, 659f, 1047f }, 0.1f, 0.45f, square: true),
            Sfx.Boss => Mix(Sweep(1.2f, 70, 40, 0.8f, square: true), Delay(Arp(new[] { 233f, 220f, 208f }, 0.3f, 0.35f, square: true), 0.1f)),
            Sfx.Click => Tone(0.04f, 1500, 0.25f, 60f, false),
            Sfx.Buy => Mix(Arp(new[] { 1319f, 1760f, 2637f }, 0.06f, 0.4f), Noise(0.1f, 0.12f, 0.9f)),
            _ => Tone(0.1f, 440, 0.3f, 20f, false),
        };
        c = AudioClip.Create(s.ToString(), b.Length, 1, Rate, false);
        c.SetData(b, 0);
        clips[s] = c;
        return c;
    }

    // ---------- building blocks (all mono, -1..1) ----------

    static float[] Buf(float seconds) => new float[Mathf.Max(1, (int)(seconds * Rate))];
    static float R() => (float)rng.NextDouble() * 2f - 1f;

    // Pitch sweep with an exponential fade: thumps, zaps, hurt sounds.
    static float[] Sweep(float dur, float f0, float f1, float vol, bool square = false)
    {
        var b = Buf(dur); float ph = 0;
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)b.Length, f = Mathf.Lerp(f0, f1, t);
            ph += 2f * Mathf.PI * f / Rate;
            float w = square ? Mathf.Sign(Mathf.Sin(ph)) * 0.5f : Mathf.Sin(ph);
            b[i] = w * vol * Mathf.Exp(-4f * t);
        }
        return b;
    }

    // White noise through a one-pole low-pass (`bright` 0..1), fading out.
    static float[] Noise(float dur, float vol, float bright)
    {
        var b = Buf(dur); float y = 0, a = Mathf.Clamp(bright, 0.01f, 1f);
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)b.Length;
            y += a * (R() - y);
            b[i] = y * vol * Mathf.Exp(-3f * t) * (bright < 0.3f ? 2.5f : 1f);
        }
        return b;
    }

    // Reshape a sound's envelope to rise then fall, peaking at `peak` (0..1 of its length).
    static float[] Swell(float[] b, float peak)
    {
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)b.Length;
            float env = t < peak ? t / peak : 1f - (t - peak) / (1f - peak);
            b[i] *= Mathf.Sqrt(Mathf.Max(0, env)) * Mathf.Exp(3f * t);   // undo Noise's fade, then apply the swell
        }
        return b;
    }

    static float[] Tone(float dur, float f, float vol, float decay, bool bell)
    {
        var b = Buf(dur);
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)Rate, w = 2f * Mathf.PI * f * t;
            float s = bell ? Mathf.Sin(w) + 0.35f * Mathf.Sin(w * 2.76f) + 0.15f * Mathf.Sin(w * 5.4f) : Mathf.Sin(w);
            b[i] = s * vol * Mathf.Exp(-decay * t) * Mathf.Min(1f, t * 400f);
        }
        return b;
    }

    static float[] Arp(float[] notes, float step, float vol, bool square = false)
    {
        var b = Buf(step * notes.Length + 0.25f);
        for (int n = 0; n < notes.Length; n++)
        {
            int start = (int)(n * step * Rate);
            for (int i = 0; start + i < b.Length; i++)
            {
                float t = i / (float)Rate, w = Mathf.Sin(2f * Mathf.PI * notes[n] * t);
                b[start + i] += (square ? Mathf.Sign(w) * 0.4f : w) * vol * Mathf.Exp(-9f * t);
            }
        }
        return b;
    }

    // Karplus–Strong plucked string: a burst of noise ringing in a delay line.
    static void Pluck(float[] b, int start, float f, float vol, float decay = 0.996f)
    {
        int n = Mathf.Max(2, (int)(Rate / f));
        var line = new float[n];
        for (int i = 0; i < n; i++) line[i] = R();
        int idx = 0;
        for (int i = start; i < b.Length; i++)
        {
            int nx = (idx + 1) % n;
            float v = line[idx];
            line[idx] = decay * 0.5f * (line[idx] + line[nx]);
            idx = nx;
            b[i] += v * vol;
        }
    }

    static float[] Strum()
    {
        // a random open chord, strummed low to high
        float[][] chords =
        {
            new[] { 82.4f, 123.5f, 164.8f, 196f, 246.9f, 329.6f },   // E minor
            new[] { 110f, 164.8f, 220f, 261.6f, 329.6f },            // A minor
            new[] { 98f, 123.5f, 146.8f, 196f, 293.7f, 392f },        // G
            new[] { 130.8f, 164.8f, 196f, 261.6f, 329.6f },           // C
        };
        var chord = chords[rng.Next(chords.Length)];
        var b = Buf(1.1f);
        for (int k = 0; k < chord.Length; k++) Pluck(b, (int)(k * 0.018f * Rate), chord[k], 0.22f, 0.9965f);
        return b;
    }

    // The OBJECTION! slam: a big brassy chord hit.
    static float[] Stinger()
    {
        var b = Buf(1.2f);
        foreach (float f in new[] { 146.8f, 220f, 293.7f, 349.2f })
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate, ph = (f * t) % 1f;
                float saw = 2f * ph - 1f;
                b[i] += saw * 0.13f * Mathf.Exp(-2.2f * t) * Mathf.Min(1f, t * 60f);
            }
        return Mix(b, Sweep(0.4f, 90, 35, 0.8f));
    }

    static float[] Creak(float dur, float f, float vol)
    {
        var b = Buf(dur); float ph = 0;
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)b.Length;
            ph += 2f * Mathf.PI * (f + 60f * Mathf.Sin(t * 9f) + 25f * R()) / Rate;
            float grit = Mathf.Sin(ph) > 0.2f ? 1f : -0.3f;             // rough, sticky squeak
            b[i] = grit * 0.12f * vol * Mathf.Sin(Mathf.PI * t);
        }
        return b;
    }

    static float[] Knocks()
    {
        var b = Buf(0.7f);
        foreach (float at in new[] { 0f, 0.22f })
        {
            int s = (int)(at * Rate);
            var k = Mix(Tone(0.12f, 310, 0.6f, 40f, false), Noise(0.05f, 0.4f, 0.3f));
            for (int i = 0; i < k.Length && s + i < b.Length; i++) b[s + i] += k[i];
        }
        return b;
    }

    // Jack talking: quick nasal syllables.
    static float[] Blah()
    {
        var b = Buf(0.6f);
        for (int syl = 0; syl < 4; syl++)
        {
            int s = (int)(syl * 0.14f * Rate); float f = 180f + rng.Next(0, 90);
            for (int i = 0; i < (int)(0.11f * Rate) && s + i < b.Length; i++)
            {
                float t = i / (float)Rate, ph = (f * t) % 1f;
                b[s + i] += (ph < 0.3f ? 0.35f : -0.15f) * Mathf.Sin(Mathf.PI * t / 0.11f);
            }
        }
        return b;
    }

    // Sad trombone when a joke lands.
    static float[] WahWah()
    {
        var b = Buf(1.6f); float ph = 0;
        float[] steps = { 311f, 294f, 277f, 262f };
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)Rate; int n = Mathf.Min(3, (int)(t / 0.3f));
            float f = steps[n] * (n == 3 ? 1f + 0.03f * Mathf.Sin(t * 30f) : 1f);
            ph += f / Rate;
            float saw = 2f * (ph % 1f) - 1f;
            float env = n == 3 ? Mathf.Exp(-1.2f * (t - 0.9f)) : Mathf.Sin(Mathf.PI * ((t % 0.3f) / 0.3f));
            b[i] = saw * 0.2f * env;
        }
        return b;
    }

    static float[] Fart(float dur)
    {
        var b = Buf(dur); float ph = 0, y = 0;
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)b.Length;
            float f = 70f + 25f * Mathf.Sin(t * 23f) + 15f * R();
            ph += f / Rate;
            float buzz = 2f * (ph % 1f) - 1f;
            y += 0.2f * (R() - y);
            b[i] = (buzz * 0.45f + y * 0.6f) * Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 1.1f)) * 0.8f;
        }
        return b;
    }

    static float[] Delay(float[] b, float seconds)
    {
        int d = (int)(seconds * Rate);
        var o = new float[b.Length + d];
        Array.Copy(b, 0, o, d, b.Length);
        return o;
    }

    static float[] Mix(float[] a, float[] b)
    {
        var o = new float[Mathf.Max(a.Length, b.Length)];
        for (int i = 0; i < o.Length; i++) o[i] = (i < a.Length ? a[i] : 0) + (i < b.Length ? b[i] : 0);
        return o;
    }

    // ---------- music ----------

    // A looping 8-bar groove in A minor (Am – F – C – E, two bars each): kick, snare, hats, a funky bass line,
    // and either spooky bell arpeggios (menu) or organ stabs and 16th hats (fight).
    static AudioClip MakeMusic(float bpm, bool fight)
    {
        float beat = 60f / bpm;
        int bars = 8, total = (int)(bars * 4 * beat * Rate);
        var b = new float[total];
        float[] roots = { 110f, 87.31f, 130.81f, 82.41f };
        float[][] chords = { new[] { 220f, 261.63f, 329.63f }, new[] { 174.61f, 220f, 261.63f }, new[] { 261.63f, 329.63f, 392f }, new[] { 164.81f, 207.65f, 246.94f } };

        void Add(float[] s, float at, float vol)
        {
            int start = (int)(at * Rate) % total;
            for (int i = 0; i < s.Length; i++) b[(start + i) % total] += s[i] * vol;   // wraps so the loop is seamless
        }

        var kick = Sweep(0.18f, 120, 42, 1f);
        var snare = Mix(Noise(0.16f, 0.6f, 0.55f), Tone(0.12f, 190, 0.4f, 25f, false));
        var hat = Noise(0.035f, 0.35f, 1f);
        for (int bar = 0; bar < bars; bar++)
        {
            int ch = bar / 2;
            float t0 = bar * 4 * beat;
            // drums
            Add(kick, t0, 0.8f); Add(kick, t0 + 2 * beat, 0.8f); Add(kick, t0 + 2.5f * beat, 0.5f);
            Add(snare, t0 + beat, 0.5f); Add(snare, t0 + 3 * beat, 0.5f);
            int hats = fight ? 16 : 8;
            for (int h = 0; h < hats; h++) Add(hat, t0 + h * 4 * beat / hats, h % 2 == 0 ? 0.35f : 0.2f);
            // bass: root on the eighths with octave jumps
            float[] pattern = { 1, 0, 2, 1, 0, 1, 2, 0 };
            for (int e = 0; e < 8; e++)
            {
                if (pattern[e] == 0) continue;
                float f = roots[ch] * (pattern[e] == 2 ? 2f : 1f);
                var note = new float[(int)(beat * 0.45f * Rate)];
                float ph = 0;
                for (int i = 0; i < note.Length; i++) { float t = i / (float)Rate; ph += f / Rate; note[i] = (2f * (ph % 1f) - 1f) * Mathf.Exp(-6f * t) * 0.5f; }
                Add(note, t0 + e * beat / 2, fight ? 0.45f : 0.38f);
            }
            if (fight)
            {
                // organ stabs on the offbeats
                for (int s = 0; s < 4; s++)
                {
                    var stab = Buf(beat * 0.3f);
                    foreach (float f in chords[ch])
                        for (int i = 0; i < stab.Length; i++) { float t = i / (float)Rate; stab[i] += Mathf.Sign(Mathf.Sin(2 * Mathf.PI * f * t)) * 0.08f * Mathf.Exp(-8f * t); }
                    Add(stab, t0 + (s + 0.5f) * beat, 0.5f);
                }
            }
            else
            {
                // spooky bell arpeggio
                for (int n = 0; n < 8; n++)
                {
                    float f = chords[ch][n % 3] * (n >= 3 && n < 6 ? 2f : 1f);
                    Add(Tone(beat * 1.2f, f * 2f, 0.12f, 4f, true), t0 + n * beat / 2, 0.6f);
                }
            }
        }
        for (int i = 0; i < b.Length; i++) b[i] = (float)Math.Tanh(b[i] * 0.9f);   // soft limiter
        var clip = AudioClip.Create(fight ? "FightMusic" : "MenuMusic", total, 1, Rate, false);
        clip.SetData(b, 0);
        return clip;
    }
}
