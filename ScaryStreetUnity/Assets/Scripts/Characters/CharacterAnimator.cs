using UnityEngine;

// Code-driven animation for a BlockyCharacter (stylized or realistic body), matched to what the character is doing:
//   moving   – walk → run blend by real speed (stride, knee lift, arm pump, lean), backwards and sideways steps,
//              little steps when turning on the spot
//   player   – jump / fall tuck and crouch from the FirstPersonController; head follows your aim
//   idle     – breathing, weight shifts, glances around, blinking; heads look at their target
//   holding  – guitar, law book, cart (and bringing it to the mouth), tray, nothing
//   actions  – Punch (jab), Swing (overhead chop), Slam, Throw (overhand), Strum, Talk, Squat, Wave, Flinch
// Joint conventions (both bodies): limbs hang straight down at rest; −X swings a limb forward, knees bend +X,
// elbows bend −X, +Z raises the right arm out to the side (−Z the left).
[RequireComponent(typeof(BlockyCharacter))]
public class CharacterAnimator : MonoBehaviour
{
    public enum Hold { None, Guitar, Book, Cart, Tray }
    enum Act { None, Punch, Swing, Slam, Throw, Strum, Wave }

    [Tooltip("Meters covered per full stride (left + right step) when walking; running strides are longer.")]
    public float strideLength = 1.3f;
    public float walkSpeed = 3f, runSpeed = 5.5f;
    [Tooltip("Head turns toward this (the target an enemy is chasing, for example).")]
    public Transform lookAt;
    [System.NonSerialized] public Hold hold;
    [System.NonSerialized] public bool inhaling;                  // cart at the mouth

    BlockyCharacter b;
    Quaternion hipsR, spineR, neckR, headR, shLR, shRR, elLR, elRR, legLR, legRR, knLR, knRR;
    Vector3 hipsP, lastPos;
    float lastYaw, phase, move, run, air, crouch, t, flinch, talkT, squatT, blinkT = 2f, blink, glanceT = 4f, glance;
    Act act; float actT = -1f, actDur = 0.6f, actHit = 0.55f;
    FirstPersonController fpc;
    Transform[] lids; Vector3[] lidScale; Transform mouth; Vector3 mouthScale;

    // smoothed joint angles (degrees) so switching poses never snaps
    Vector3 sL, sR, eL, eR, spineE, headE, lgL, lgR, knL, knR;

    void Awake()
    {
        b = GetComponent<BlockyCharacter>();
        hipsR = b.hips.localRotation; hipsP = b.hips.localPosition;
        spineR = b.spine.localRotation; neckR = b.neck ? b.neck.localRotation : Quaternion.identity; headR = b.head.localRotation;
        shLR = b.shoulderL.localRotation; shRR = b.shoulderR.localRotation;
        elLR = b.elbowL.localRotation; elRR = b.elbowR.localRotation;
        legLR = b.legL.localRotation; legRR = b.legR.localRotation;
        knLR = b.kneeL.localRotation; knRR = b.kneeR.localRotation;
        lastPos = transform.position; lastYaw = transform.eulerAngles.y;
        t = Random.value * 10f;
        fpc = GetComponentInParent<FirstPersonController>();

        var found = new System.Collections.Generic.List<Transform>();
        foreach (Transform c in b.head) { if (c.name == "Lid") found.Add(c); if (c.name == "Mouth") { mouth = c; mouthScale = c.localScale; } }
        lids = found.ToArray();
        lidScale = System.Array.ConvertAll(lids, l => l.localScale);
    }

    // ---------- actions ----------

    public void Punch(float duration, float hitMoment) => Play(Act.Punch, duration, hitMoment);
    public void Swing(float duration = 0.45f, float hitMoment = 0.45f) => Play(Act.Swing, duration, hitMoment);
    public void Slam(float duration = 0.6f) => Play(Act.Slam, duration, 0.55f);
    public void Throw(float duration = 0.55f, float release = 0.6f) => Play(Act.Throw, duration, release);
    public void Strum(float duration = 0.3f) => Play(Act.Strum, duration, 0.5f);
    public void Wave(float duration = 1.4f) => Play(Act.Wave, duration, 0.5f);
    public void Talk(float duration = 1.2f) => talkT = Mathf.Max(talkT, duration);
    public void Squat(float duration = 0.8f) => squatT = Mathf.Max(squatT, duration);
    public void Flinch() { flinch = 1f; if (act == Act.Punch || act == Act.Throw || act == Act.Swing) actT = -1f; }

    void Play(Act a, float duration, float hitMoment) { act = a; actT = 0f; actDur = Mathf.Max(0.05f, duration); actHit = hitMoment; }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0) return;
        t += dt;

        // ---------- what the body is doing ----------
        Vector3 d = transform.position - lastPos; lastPos = transform.position;
        float vy = d.y / dt; d.y = 0;
        float speed = d.magnitude / dt;
        Vector3 local = transform.InverseTransformDirection(d / dt);          // forward / sideways parts of the motion
        float fwd = speed > 0.05f ? local.z / speed : 1f, side = speed > 0.05f ? local.x / speed : 0f;
        float yawRate = Mathf.DeltaAngle(lastYaw, transform.eulerAngles.y) / dt; lastYaw = transform.eulerAngles.y;

        move = Mathf.MoveTowards(move, Mathf.Clamp01(speed / walkSpeed), dt * 5f);
        run = Mathf.MoveTowards(run, Mathf.Clamp01((speed - walkSpeed) / (runSpeed - walkSpeed)), dt * 3f);
        bool inAir = fpc ? !fpc.IsGrounded && Mathf.Abs(vy) > 0.5f : Mathf.Abs(vy) > 2.5f;
        air = Mathf.MoveTowards(air, inAir ? 1f : 0f, dt * 6f);
        crouch = Mathf.MoveTowards(crouch, fpc && fpc.IsCrouching ? 1f : 0f, dt * 5f);
        flinch = Mathf.MoveTowards(flinch, 0, dt * 5f);
        talkT -= dt; squatT -= dt;
        float squat = Mathf.Clamp01(squatT * 4f) * Mathf.Clamp01(1f - (squatT - 0.8f) * 4f);

        // stepping: forwards, backwards (phase runs the other way) or turning on the spot
        float stride = strideLength * (1f + 0.5f * run);
        float stepSpeed = speed + (speed < 0.3f ? Mathf.Abs(yawRate) * 0.006f : 0f);
        phase += stepSpeed / stride * Mathf.PI * 2f * dt * (fwd < -0.3f ? -1f : 1f);
        float stepAmt = Mathf.Max(move, speed < 0.3f ? Mathf.Clamp01(Mathf.Abs(yawRate) / 200f) * 0.35f : 0f);
        float s = Mathf.Sin(phase), c = Mathf.Cos(phase), idle = 1f - Mathf.Max(move, air);

        // ---------- legs ----------
        float legSwing = Mathf.Lerp(30f, 48f, run) * stepAmt * (1f - 0.6f * Mathf.Abs(side));
        float kneeLift = Mathf.Lerp(55f, 90f, run) * stepAmt;
        float spread = side * 12f * stepAmt;                                    // sidestep: legs open and close
        Vector3 tLgL = new Vector3(-s * legSwing, 0, -Mathf.Abs(s) * spread), tLgR = new Vector3(s * legSwing, 0, Mathf.Abs(s) * spread);
        Vector3 tKnL = new Vector3(3f + Mathf.Max(0, c) * kneeLift, 0, 0), tKnR = new Vector3(3f + Mathf.Max(0, -c) * kneeLift, 0, 0);
        // jump tuck, crouch and squat all bend the legs
        float bend = Mathf.Max(air * 0.8f, Mathf.Max(crouch, squat));
        tLgL = Vector3.Lerp(tLgL, new Vector3(-55f, 0, -4f), bend); tLgR = Vector3.Lerp(tLgR, new Vector3(-55f, 0, 4f), bend);
        tKnL = Vector3.Lerp(tKnL, new Vector3(95f, 0, 0), bend); tKnR = Vector3.Lerp(tKnR, new Vector3(95f, 0, 0), bend);

        // ---------- body ----------
        float bob = Mathf.Abs(c) * Mathf.Lerp(0.03f, 0.06f, run) * move;
        float sway = Mathf.Sin(t * 0.8f) * 0.012f * idle + Mathf.Sin(t * 0.23f) * 0.015f * idle;   // weight shifts
        b.hips.localPosition = hipsP + new Vector3(sway, bob - 0.012f * move - Mathf.Max(crouch * 0.32f, squat * 0.28f), 0);
        b.hips.localRotation = hipsR * Quaternion.Euler(0, s * 7f * stepAmt, Mathf.Sin(t * 0.8f) * 1.5f * idle - side * 4f * move);
        float breathe = Mathf.Sin(t * 1.7f);
        Vector3 tSpine = new Vector3(Mathf.Lerp(4f, 14f, run) * move + breathe * idle + crouch * 22f + squat * 25f - flinch * 18f + air * 6f,
                                     -s * Mathf.Lerp(12f, 18f, run) * stepAmt, side * 5f * move);

        // ---------- arms: locomotion swing, then holds, then actions on top ----------
        float armSwing = Mathf.Lerp(26f, 45f, run) * move, elbowRun = Mathf.Lerp(12f, 85f, run);
        Vector3 tSL = new Vector3(s * armSwing + Mathf.Sin(t * 1.1f) * 1.5f * idle, 0, -3f - 6f * run);
        Vector3 tSR = new Vector3(-s * armSwing + Mathf.Sin(t * 1.1f + 1f) * 1.5f * idle, 0, 3f + 6f * run);
        Vector3 tEL = new Vector3(-elbowRun - Mathf.Max(0, -s * armSwing) * 0.8f, 0, 0), tER = new Vector3(-elbowRun - Mathf.Max(0, s * armSwing) * 0.8f, 0, 0);
        if (air > 0.01f) { tSL = Vector3.Lerp(tSL, new Vector3(-35f, 0, -35f), air); tSR = Vector3.Lerp(tSR, new Vector3(-35f, 0, 35f), air); }

        switch (hold)
        {
            case Hold.Guitar: tSL = new Vector3(-62f, 0, 4f); tEL = new Vector3(-75f, 0, 0); tSR = new Vector3(-22f, 0, 12f); tER = new Vector3(-70f, 0, 0); break;
            case Hold.Book:   tSR = new Vector3(-22f, 0, 6f) + new Vector3(s * armSwing * 0.3f, 0, 0); tER = new Vector3(-85f, 0, 0); break;
            case Hold.Cart:   tSR = new Vector3(-14f, 0, 6f); tER = new Vector3(-95f, 0, 0); break;
            case Hold.Tray:   tSL = new Vector3(-48f, 0, 4f); tSR = new Vector3(-48f, 0, -4f); tEL = tER = new Vector3(-48f, 0, 0); break;
        }
        if (inhaling) { tSR = new Vector3(-38f, 0, 18f); tER = new Vector3(-145f, 0, 0); }

        if (talkT > 0)                                                  // Jack delivering a joke: hand gestures, head bobs
        {
            tSL = new Vector3(-40f + Mathf.Sin(t * 7f) * 10f, 0, -18f); tEL = new Vector3(-75f, 0, 0);
            tSpine.x -= 4f;
        }

        if (actT >= 0)
        {
            actT += dt;
            float p = Mathf.Clamp01(actT / actDur), wind = Mathf.SmoothStep(0, 1, Mathf.Clamp01(p / actHit));
            float back = p < actHit ? 0 : Mathf.SmoothStep(0, 1, Mathf.InverseLerp(actHit + (1 - actHit) * 0.35f, 1f, p));
            float hit = p < actHit ? 0 : 1f - back;                     // 0→1→0 over the follow-through
            switch (act)
            {
                case Act.Punch:   // wind up fist by the cheek, jab straight out, guard up
                    tSR = Vector3.Lerp(new Vector3(-40f * wind, 0, 6f), new Vector3(-88f, 0, 2f), hit); tER = Vector3.Lerp(new Vector3(-10f - 110f * wind, 0, 0), new Vector3(-5f, 0, 0), hit);
                    tSL = new Vector3(-45f, 0, -8f); tEL = new Vector3(-95f, 0, 0);
                    tSpine.y += Mathf.Lerp(18f * wind, -22f, hit);
                    break;
                case Act.Swing:   // raise it overhead, chop down and across
                    tSR = Vector3.Lerp(new Vector3(-150f * wind, 0, 20f * wind), new Vector3(-35f, 0, -12f), hit); tER = Vector3.Lerp(new Vector3(-60f * wind, 0, 0), new Vector3(-15f, 0, 0), hit);
                    tSpine.y += Mathf.Lerp(15f * wind, -25f, hit); tSpine.x += 10f * hit;
                    break;
                case Act.Slam:    // both hands up, bring it down with the knees
                    Vector3 up = new Vector3(-165f, 0, 10f), down = new Vector3(-30f, 0, 5f);
                    tSR = Vector3.Lerp(up * wind, down, hit); tSL = Vector3.Lerp(new Vector3(-165f, 0, -10f) * wind, new Vector3(-30f, 0, -5f), hit);
                    tER = tEL = new Vector3(-30f * (1 - hit), 0, 0);
                    tSpine.x += 30f * hit - 8f * wind;
                    tKnL.x += 40f * hit; tKnR.x += 40f * hit; tLgL.x -= 30f * hit; tLgR.x -= 30f * hit;
                    break;
                case Act.Throw:   // overhand: hand back behind the head, whip forward
                    tSR = Vector3.Lerp(new Vector3(-150f * wind, 0, 15f), new Vector3(-55f, 0, -5f), hit); tER = Vector3.Lerp(new Vector3(-110f * wind, 0, 0), new Vector3(-5f, 0, 0), hit);
                    tSL = new Vector3(-50f * wind, 0, -12f); tSpine.y += Mathf.Lerp(28f * wind, -28f, hit); tSpine.x += Mathf.Lerp(-8f * wind, 12f, hit);
                    break;
                case Act.Strum:   // quick down-strum across the strings
                    tER.x += Mathf.Sin(p * Mathf.PI) * 30f; tSR.x -= Mathf.Sin(p * Mathf.PI) * 8f;
                    break;
                case Act.Wave:
                    tSR = new Vector3(-15f, 0, 150f); tER = new Vector3(0, 0, 25f * Mathf.Sin(p * Mathf.PI * 6f));
                    break;
            }
            if (actT >= actDur) actT = -1f;
        }

        // ---------- head: look at the target, or follow the player's aim; glances and talking ----------
        Vector3 tHead = new Vector3(-2f * move - flinch * 10f, s * 6f * stepAmt, 0);
        if ((glanceT -= dt) <= 0) { glanceT = Random.Range(3f, 7f); glance = Random.Range(-35f, 35f); }
        float glanceNow = glance * Mathf.Clamp01(Mathf.Sin(Mathf.Clamp01((glanceT - 1f) / 1.5f) * Mathf.PI)) * idle;
        if (fpc) tHead.x += fpc.Pitch * 0.7f;
        else if (lookAt)
        {
            Vector3 to = transform.InverseTransformPoint(lookAt.position + Vector3.up * 1.5f) - transform.InverseTransformPoint(b.head.position);
            tHead.y += Mathf.Clamp(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, -60f, 60f);
            tHead.x += Mathf.Clamp(-Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg, -30f, 30f);
        }
        else tHead.y += glanceNow;
        if (talkT > 0) tHead.x += Mathf.Sin(t * 9f) * 5f;
        if (mouth) mouth.localScale = talkT > 0 ? Vector3.Scale(mouthScale, new Vector3(1f, 1f + 2.5f * Mathf.Abs(Mathf.Sin(t * 14f)), 1f)) : mouthScale;

        // blinking every few seconds
        if ((blinkT -= dt) <= 0) { blinkT = Random.Range(2.5f, 5.5f); blink = 1f; }
        blink = Mathf.MoveTowards(blink, 0, dt * 9f);
        for (int i = 0; i < lids.Length; i++) if (lids[i]) lids[i].localScale = new Vector3(lidScale[i].x, lidScale[i].y * (1f + blink * 1.6f), lidScale[i].z);

        // ---------- apply, smoothed ----------
        float k = 1f - Mathf.Exp(-dt * 18f);
        sL = Vector3.Lerp(sL, tSL, k); sR = Vector3.Lerp(sR, tSR, k); eL = Vector3.Lerp(eL, tEL, k); eR = Vector3.Lerp(eR, tER, k);
        spineE = Vector3.Lerp(spineE, tSpine, k); headE = Vector3.Lerp(headE, tHead, 1f - Mathf.Exp(-dt * 10f));
        lgL = Vector3.Lerp(lgL, tLgL, k); lgR = Vector3.Lerp(lgR, tLgR, k); knL = Vector3.Lerp(knL, tKnL, k); knR = Vector3.Lerp(knR, tKnR, k);

        b.spine.localRotation = spineR * Quaternion.Euler(spineE);
        b.head.localRotation = headR * Quaternion.Euler(headE);
        b.shoulderL.localRotation = shLR * Quaternion.Euler(sL); b.elbowL.localRotation = elLR * Quaternion.Euler(eL);
        b.shoulderR.localRotation = shRR * Quaternion.Euler(sR); b.elbowR.localRotation = elRR * Quaternion.Euler(eR);
        b.legL.localRotation = legLR * Quaternion.Euler(lgL); b.legR.localRotation = legRR * Quaternion.Euler(lgR);
        b.kneeL.localRotation = knLR * Quaternion.Euler(knL); b.kneeR.localRotation = knRR * Quaternion.Euler(knR);
    }
}
