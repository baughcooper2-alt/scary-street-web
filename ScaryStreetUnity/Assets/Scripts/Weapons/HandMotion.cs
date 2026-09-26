using UnityEngine;

// First-person right-hand moves for the handheld weapons, layered on FirstPersonArms' override:
// Throw (wind back, whip forward), Swing (sweep across), Poke (straight jab), Drink (up to the mouth and tip).
public class HandMotion
{
    public enum Move { None, Throw, Swing, Poke, Drink, Flick, Grab }

    Move move; float t = -1f, dur = 0.3f;
    public float hold;                                        // 0..1 extra "bring it up" (charging a drink)
    public Vector3? rest;                                     // camera-space resting spot (default: the arms' own)
    public Vector3 pickFrom;                                  // Flick: grab from here (the deck / case / carrier in the other hand)
    public Vector3 restEuler;

    public bool Busy => t >= 0;
    public void Play(Move m, float duration) { move = m; dur = Mathf.Max(0.05f, duration); t = 0; }

    public void Apply(FirstPersonArms arms, float dt)
    {
        if (!arms) return;
        Vector3 rest = this.rest ?? arms.restPosition, pos = rest, euler = restEuler;
        if (t >= 0)
        {
            float p = Mathf.Clamp01((t += dt) / dur);
            float k = Mathf.Sin(p * Mathf.PI);
            switch (move)
            {
                case Move.Throw:
                    float back = p < 0.35f ? Mathf.SmoothStep(0, 1, p / 0.35f) : 1f - Mathf.SmoothStep(0, 1, (p - 0.35f) / 0.4f);
                    float fwd = p < 0.35f ? 0f : Mathf.Sin((p - 0.35f) / 0.65f * Mathf.PI);
                    pos += new Vector3(0.02f, 0.1f * back + 0.06f * fwd, -0.12f * back + 0.22f * fwd);
                    euler = new Vector3(-50f * back + 30f * fwd, 0, 0);
                    break;
                case Move.Swing:
                    pos += new Vector3(Mathf.Lerp(0.12f, -0.34f, p), 0.08f * k, 0.12f * k);
                    euler = new Vector3(-20f * k, Mathf.Lerp(40f, -60f, p), -30f * k);
                    break;
                case Move.Poke:
                    pos += new Vector3(-0.08f * k, 0.05f * k, 0.3f * k);
                    break;
                case Move.Flick:
                    // reach over to the other hand, pinch one, whip it out toward the crosshair
                    if (p < 0.35f) { float a = Mathf.SmoothStep(0, 1, p / 0.35f); pos = Vector3.Lerp(rest, pickFrom + new Vector3(0.03f, 0.03f, 0), a); euler = Vector3.Lerp(restEuler, new Vector3(-10f, -35f, 0), a); }
                    else { float b = (p - 0.35f) / 0.65f, out_ = Mathf.Sin(b * Mathf.PI); pos = Vector3.Lerp(pickFrom + new Vector3(0.03f, 0.03f, 0), rest, Mathf.SmoothStep(0, 1, b)) + new Vector3(0.06f, 0.05f, 0.2f) * out_; euler = Vector3.Lerp(new Vector3(-10f, -35f, 0), restEuler, b) + new Vector3(20f * out_, 30f * out_, 0); }
                    break;
                case Move.Grab:
                    // over to the other hand for the next one, and back
                    pos = Vector3.Lerp(rest, pickFrom + new Vector3(0.04f, 0.05f, 0), k);
                    euler = Vector3.Lerp(restEuler, new Vector3(-10f, -30f, 0), k);
                    break;
                case Move.Drink:
                    pos = Vector3.Lerp(rest, arms.mouthPosition + new Vector3(0, 0.02f, 0), k);
                    euler = new Vector3(-70f * k, -25f * k, 0);
                    break;
            }
            if (p >= 1f) t = -1f;
        }
        else if (hold > 0)
        {
            pos = Vector3.Lerp(rest, arms.mouthPosition, hold * 0.8f);
            euler = new Vector3(-55f * hold, -20f * hold, 0);
        }
        arms.overrideRight = t >= 0 || hold > 0 || this.rest.HasValue;
        arms.rightTarget = pos; arms.rightEuler = euler;
    }

    public void Release(FirstPersonArms arms) { t = -1f; hold = 0; if (arms) arms.overrideRight = false; }
}
