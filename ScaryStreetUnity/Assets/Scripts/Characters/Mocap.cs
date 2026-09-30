using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Motion capture for every body: Mixamo clips baked by tools/anim/bake_mixamo.py into Resources/Anim/<Name>.bytes
// (Idle, Walk, Run, Jab, Cross). A clip stores, per frame, each joint's rotation in character space relative to
// Mixamo's T-pose, so it isn't tied to any skeleton. MocapRig retargets it onto a BlockyCharacter's joints (real
// bodies, the web human, blocky and cartoon bodies, and anything built later with the same joints): it turns our
// arms-down rest into the T-pose with a per-joint offset from the bone directions, applies the clip's rotation, and
// hands CharacterAnimator a local rotation per joint (relative to the joint's rest, like its own Euler angles).
public enum MocapJoint { Hips, Spine, Neck, Head, ArmL, ForearmL, HandL, ArmR, ForearmR, HandR, ThighL, CalfL, FootL, ThighR, CalfR, FootR }

public class MocapClip
{
    public const int Joints = 16;
    public string name;
    public float fps, duration;
    public int frames;
    public float stride;          // meters per loop (0 = in place)
    public float phase0;          // 0..1: the left heel strike (walk and run line up on it)
    public float impact;          // 0..1: a punch's furthest reach
    public int impactSide;        // -1 left hand, +1 right
    public float legLength;       // Mixamo's hips height above its ankles (m)
    public Vector3[] restPos;
    Vector3[] hips;
    Quaternion[] rot;

    public float Speed => duration > 0 ? stride / duration : 0f;   // natural m/s for Mixamo's body
    public float ImpactTime => impact * duration;

    static readonly Dictionary<string, MocapClip> cache = new Dictionary<string, MocapClip>();

    // null if the file is missing (the animator falls back to its code-driven motion)
    public static MocapClip Get(string name)
    {
        if (cache.TryGetValue(name, out var c)) return c;
        var asset = Resources.Load<TextAsset>("Anim/" + name);
        if (!asset) return null;
        c = Read(name, asset.bytes);
        cache[name] = c;
        return c;
    }

    static MocapClip Read(string name, byte[] data)
    {
        using var r = new BinaryReader(new MemoryStream(data));
        if (new string(r.ReadChars(4)) != "SSAN") { Debug.LogWarning($"Anim/{name}: not a mocap clip"); return null; }
        r.ReadInt32();                                                                   // version
        var c = new MocapClip { name = name, fps = r.ReadSingle(), frames = r.ReadInt32() };
        int joints = r.ReadInt32();
        if (joints != Joints) { Debug.LogWarning($"Anim/{name}: {joints} joints, expected {Joints}"); return null; }
        c.stride = r.ReadSingle(); c.phase0 = r.ReadSingle(); c.impact = r.ReadSingle(); c.impactSide = r.ReadInt32(); c.legLength = r.ReadSingle();
        c.duration = c.frames / c.fps;
        c.restPos = new Vector3[Joints];
        for (int j = 0; j < Joints; j++) c.restPos[j] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        c.hips = new Vector3[c.frames];
        c.rot = new Quaternion[c.frames * Joints];
        for (int f = 0; f < c.frames; f++)
        {
            c.hips[f] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            for (int j = 0; j < Joints; j++) c.rot[f * Joints + j] = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        }
        return c;
    }

    // The pose at this time (seconds, wraps around: every clip loops): character-space rotations + hips offset (m).
    public void Sample(float time, Quaternion[] g, out Vector3 hipsOffset)
    {
        float x = Mathf.Repeat(time * fps, frames);
        int i = Mathf.Min((int)x, frames - 1), k = (i + 1) % frames;
        float a = x - i;
        for (int j = 0; j < Joints; j++) g[j] = Quaternion.Slerp(rot[i * Joints + j], rot[k * Joints + j], a);
        hipsOffset = Vector3.Lerp(hips[i], hips[k], a);
    }
}

// One body's joints, set up for clips. Build once per character (CharacterAnimator does, in Awake).
public class MocapRig
{
    // Each joint's parent among the 16 (the head hangs off the spine when there's no neck, and so on).
    static readonly int[] Parent = { -1, 0, 1, 2, 1, 4, 5, 1, 7, 8, 0, 10, 11, 0, 13, 14 };

    public readonly Transform[] joint = new Transform[MocapClip.Joints];
    public readonly float legLength;                  // hips above the ankles, in the model's own units
    readonly Transform root;
    readonly int[] parent = new int[MocapClip.Joints];
    readonly Quaternion[] restW = new Quaternion[MocapClip.Joints], restWInv = new Quaternion[MocapClip.Joints];
    readonly Vector3[] restP = new Vector3[MocapClip.Joints];
    readonly Dictionary<MocapClip, Quaternion[]> offsets = new Dictionary<MocapClip, Quaternion[]>();

    public bool Valid { get; }

    public MocapRig(BlockyCharacter b)
    {
        root = b.transform;
        var list = new[] { b.hips, b.spine, b.neck, b.head, b.shoulderL, b.elbowL, b.handL, b.shoulderR, b.elbowR, b.handR,
                           b.legL, b.kneeL, b.ankleL, b.legR, b.kneeR, b.ankleR };
        for (int j = 0; j < list.Length; j++)
        {
            joint[j] = list[j];
            int p = Parent[j];
            while (p >= 0 && !list[p]) p = Parent[p];
            parent[j] = p;
            if (!joint[j]) continue;
            restW[j] = Quaternion.Inverse(root.rotation) * joint[j].rotation;          // rest pose, relative to the body
            restWInv[j] = Quaternion.Inverse(restW[j]);
            restP[j] = root.InverseTransformPoint(joint[j].position);
        }
        Valid = b.hips && b.spine && b.head && b.shoulderL && b.shoulderR && b.elbowL && b.elbowR && b.legL && b.legR && b.kneeL && b.kneeR;
        if (!Valid) return;
        legLength = b.ankleL && b.ankleR ? restP[0].y - 0.5f * (restP[12].y + restP[15].y) : 2f * (restP[0].y - restP[11].y);
        if (legLength < 0.05f) Valid = false;
    }

    // How this clip's hips offset scales to this body, and how fast it covers ground (world m/s) at natural speed.
    public float Scale(MocapClip c) => legLength / c.legLength;

    // Our rest bones → Mixamo's T-pose: turn each limb bone from its rest direction onto the clip's.
    // (The hands and feet follow their forearm / shin; hips, spine and head stand the same way in both.)
    Quaternion[] Offsets(MocapClip c)
    {
        if (offsets.TryGetValue(c, out var o)) return o;
        o = new Quaternion[MocapClip.Joints];
        for (int j = 0; j < o.Length; j++) o[j] = Quaternion.identity;
        void Bone(int a, int child)
        {
            if (!joint[a]) return;
            if (!joint[child]) { o[a] = o[Parent[a]]; return; }                          // no end joint: like its parent
            o[a] = Quaternion.FromToRotation(restP[child] - restP[a], c.restPos[child] - c.restPos[a]);
        }
        Bone(4, 5); Bone(5, 6); Bone(7, 8); Bone(8, 9); Bone(10, 11); Bone(11, 12); Bone(13, 14); Bone(14, 15);
        o[6] = o[5]; o[9] = o[8]; o[12] = o[11]; o[15] = o[14];
        offsets[c] = o;
        return o;
    }

    // The clip at this time as character-space rotations for our body (g) and the hips offset in model units.
    public void Sample(MocapClip c, float time, Quaternion[] g, out Vector3 hipsOffset)
    {
        c.Sample(time, g, out hipsOffset);
        var o = Offsets(c);
        for (int j = 0; j < g.Length; j++) g[j] *= o[j];
        hipsOffset *= Scale(c);
    }

    // Character-space rotations → each joint's rotation relative to its rest, in its own rest frame
    // (joint.localRotation = rest local * d), with the parents in the same pose.
    public void ToLocal(Quaternion[] g, Quaternion[] d)
    {
        for (int j = 0; j < d.Length; j++)
        {
            if (!joint[j]) { d[j] = Quaternion.identity; continue; }
            var gp = parent[j] >= 0 ? g[parent[j]] : Quaternion.identity;
            d[j] = restWInv[j] * Quaternion.Inverse(gp) * g[j] * restW[j];
        }
    }

    // A character-space rotation expressed in this joint's rest frame (to add a turn about the body's up axis).
    public Quaternion InRest(int j, Quaternion q) => restWInv[j] * q * restW[j];
}
