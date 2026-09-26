using System.Collections.Generic;
using UnityEngine;

// The first-person hand's fingers (the real bodies export an open hand with finger bones). Weapons ask for a grip
// through FirstPersonArms (a fist round a handle, a hook round the book's spine, flat palms on a box, a card between
// two fingers) and this curls each finger to it, smoothly. Curl 1 is the old baked fist.
public class FirstPersonHand : MonoBehaviour
{
    public struct Grip
    {
        public float index, middle, ring, pinky, thumb;
        public Grip(float all) { index = middle = ring = pinky = thumb = all; }
        public Grip(float index, float middle, float ring, float pinky, float thumb) { this.index = index; this.middle = middle; this.ring = ring; this.pinky = pinky; this.thumb = thumb; }
        public float this[int f] => f == 0 ? index : f == 1 ? middle : f == 2 ? ring : f == 3 ? pinky : thumb;
        public static readonly Grip Fist = new Grip(1f);
        public static readonly Grip Flat = new Grip(0.1f, 0.1f, 0.12f, 0.15f, 0.2f);
        public static readonly Grip Card = new Grip(0.12f, 0.18f, 0.95f, 1f, 0.75f);   // index + middle out, a card between them
    }

    static readonly string[] Names = { "Index", "Mid", "Ring", "Pinky", "Thumb" };
    static readonly float[] FingerSeg = { 80f, 95f, 65f }, ThumbSeg = { 25f, 25f, 35f };   // degrees at curl 1 (the stage5 fist)
    readonly Transform[,] seg = new Transform[5, 3];
    readonly Vector3[] axis = new Vector3[5];
    readonly float[] now = { 1, 1, 1, 1, 1 };

    public bool Ready => seg[1, 0];
    public Transform Bone(int finger, int s) => seg[finger, s];

    public void Init(int[] bones, Vector3[] axes, Dictionary<int, Transform> map, System.Func<int, string> name)
    {
        for (int i = 0; i < bones.Length; i++)
        {
            string n = name(bones[i]);
            for (int f = 0; f < 5; f++)
                for (int j = 0; j < 3; j++)
                    if (n.EndsWith("_" + Names[f] + (j + 1))) { seg[f, j] = map[bones[i]]; axis[f] = axes[i]; }
        }
        Pose(Grip.Fist, 0);
    }

    // dt 0 snaps to the grip
    public void Pose(Grip g, float dt)
    {
        float k = dt <= 0 ? 1f : 1f - Mathf.Exp(-dt * 18f);
        for (int f = 0; f < 5; f++)
        {
            now[f] = Mathf.Lerp(now[f], g[f], k);
            var a = f == 4 ? ThumbSeg : FingerSeg;
            for (int j = 0; j < 3; j++)
                if (seg[f, j]) seg[f, j].localRotation = Quaternion.AngleAxis(a[j] * now[f], axis[f]);
        }
    }
}
