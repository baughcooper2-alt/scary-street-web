using UnityEngine;

// First-person right-hand moves for the handheld weapons, layered on FirstPersonArms' override. They match the
// body's moves in third person: Throw (wind back, whip forward = Throw), Swing (sweep across = Sweep), Poke (the
// forearm comes up level and jabs = Punch), Flick (take one from the other hand, whip it out = Flick),
// Grab (over to the other hand and back), Drink (up to the mouth and tip). Each starts from and ends at the rest pose.
public class HandMotion
{
    public enum Move { None, Throw, Swing, Poke, Drink, Flick, Grab }

    Move move; float t = -1f, dur = 0.3f;
    public float hold;                                        // 0..1 extra "bring it up" (charging a drink)
    public Vector3? rest;                                     // camera-space resting spot (default: the arms' own)
    public Vector3 pickFrom;                                  // Flick: grab from here (the deck / case / carrier in the other hand)
    public Vector3 restEuler;

    public bool Busy => t >= 0;
    public Move Playing => t >= 0 ? move : Move.None;
    public float Progress => t >= 0 ? Mathf.Clamp01(t / dur) : -1f;       // 0..1 through the current move
    public const float FlickPinch = 0.35f, FlickRelease = 0.7f;          // Flick: has one between the fingers from here, lets go here
    public void Play(Move m, float duration) { move = m; dur = Mathf.Max(0.05f, duration); t = 0; }

    public void Apply(FirstPersonArms arms, float dt)
    {
        if (!arms) return;
        Vector3 rest = this.rest ?? arms.restPosition, pos = rest, euler = restEuler;
        if (t >= 0)
        {
            float p = Mathf.Clamp01((t += dt) / dur);
            float k = Mathf.Sin(p * Mathf.PI);
            float inOut = Mathf.SmoothStep(0, 1, Mathf.Min(1f, Mathf.Min(p / 0.2f, (1f - p) / 0.3f)));   // from the rest pose and back
            switch (move)
            {
                case Move.Throw:
                    float back = p < 0.35f ? Mathf.SmoothStep(0, 1, p / 0.35f) : 1f - Mathf.SmoothStep(0, 1, (p - 0.35f) / 0.4f);
                    float fwd = p < 0.35f ? 0f : Mathf.Sin((p - 0.35f) / 0.65f * Mathf.PI);
                    pos += new Vector3(0.02f, 0.1f * back + 0.06f * fwd, -0.12f * back + 0.22f * fwd);
                    euler = restEuler + new Vector3(-50f * back + 30f * fwd, 0, 0);
                    break;
                case Move.Swing:
                    // the forearm comes up level (the held item points ahead) and sweeps right to left
                    float across = Mathf.SmoothStep(0, 1, p);
                    pos = Vector3.Lerp(rest, rest + new Vector3(Mathf.Lerp(0.1f, -0.38f, across), 0.1f, 0.08f), inOut);
                    euler = Vector3.Lerp(restEuler, new Vector3(-8f, Mathf.Lerp(45f, -60f, across), -25f * k), inOut);
                    break;
                case Move.Poke:
                    pos += new Vector3(-0.08f * k, 0.05f * k, 0.3f * k);
                    euler = Vector3.Lerp(restEuler, new Vector3(-4f, -6f, 0), Mathf.Min(1f, k * 1.8f));
                    break;
                case Move.Flick:
                    // reach over to the other hand and pinch one between two fingers, cock it back by your shoulder,
                    // whip it out toward the crosshair (let go at FlickRelease), and come back to rest
                    {
                        Vector3 grab = pickFrom + new Vector3(0.03f, 0.03f, 0), cock = rest + new Vector3(0.03f, 0.09f, -0.07f), outPos = rest + new Vector3(0.0f, 0.06f, 0.2f);
                        Vector3 eGrab = new Vector3(-10f, -35f, 0), eCock = restEuler + new Vector3(-30f, 25f, 0), eOut = restEuler + new Vector3(12f, -15f, 0);
                        if (p < FlickPinch) { float a = Mathf.SmoothStep(0, 1, p / FlickPinch); pos = Vector3.Lerp(rest, grab, a); euler = Vector3.Lerp(restEuler, eGrab, a); }
                        else if (p < 0.55f) { float a = Mathf.SmoothStep(0, 1, (p - FlickPinch) / (0.55f - FlickPinch)); pos = Vector3.Lerp(grab, cock, a); euler = Vector3.Lerp(eGrab, eCock, a); }
                        else if (p < 0.75f) { float a = Mathf.SmoothStep(0, 1, (p - 0.55f) / 0.2f); pos = Vector3.Lerp(cock, outPos, a); euler = Vector3.Lerp(eCock, eOut, a); }
                        else { float a = Mathf.SmoothStep(0, 1, (p - 0.75f) / 0.25f); pos = Vector3.Lerp(outPos, rest, a); euler = Vector3.Lerp(eOut, restEuler, a); }
                    }
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
