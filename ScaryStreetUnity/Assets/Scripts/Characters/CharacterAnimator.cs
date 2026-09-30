using UnityEngine;

// Animation for a BlockyCharacter (any body: real, web human, blocky, cartoon), matched to what the character is doing.
// Underneath is motion capture (Mocap.cs: Mixamo Idle / Walk / Run clips, and the Jab → Cross punch combo),
// retargeted onto the joints; the code-driven layers below take over limb by limb when they need to (a held
// weapon's arm pose, swings and throws, crouching, jumping, skating, dying) and fade back to the mocap after.
//   mocap    – breathing idle; walk ↔ run on one shared step cycle, played at the speed you're really moving
//              (backwards plays it in reverse, sideways turns the hips toward where you're going); punches:
//              the Jab clip, and a second punch soon after is the Cross (Punch(..., combo) or automatic)
//   moving   – (code-driven, when the mocap is off or for crouch-walking) walk → run blend by real speed,
//              backwards and sideways steps, little steps when turning on the spot
//   player   – jump / fall tuck and crouch from the FirstPersonController; head follows your aim
//   idle     – breathing, weight shifts, glances around, blinking; heads look at their target
//   holding  – guitar, law book, cart (and bringing it to the mouth), tray, nothing
//   actions  – Punch (jab), Swing (overhead chop), Sweep (across in front), Slam, Throw (overhand), Flick (take one
//              from the left hand and whip it out), Toss (both hands lift a box), Strum, Talk, Squat, Wave, Flinch
// Joint conventions (both bodies): limbs hang straight down at rest; −X swings a limb forward, knees bend +X,
// elbows bend −X, +Z raises the right arm out to the side (−Z the left).
[RequireComponent(typeof(BlockyCharacter))]
public class CharacterAnimator : MonoBehaviour
{
    public enum Hold { None, Guitar, Book, Cart, Tray, Phone, CarryLeft, HangLeft, Carton, Skate }   // add new ones at the end
    enum Act { None, Punch, Swing, Slam, Throw, Strum, Wave, Sweep, Flick, Toss }

    [Tooltip("Scales every stride (1 = the natural length for this speed).")]
    public float strideLength = 1.3f;
    [Tooltip("Speed (m/s) where the walk turns into a jog, and where it's a full run.")]
    public float jogFrom = 2.2f, runAt = 4.6f;
    [Tooltip("Head turns toward this (the target an enemy is chasing, for example).")]
    public Transform lookAt;
    [System.NonSerialized] public Hold hold;
    [System.NonSerialized] public bool inhaling;                  // cart (or a bottle) at the mouth
    [System.NonSerialized] public float charge;                   // 0..1: the held item raised overhead (Law Book charging a slam)
    [System.NonSerialized] public float skatePush = -1f;          // 0..1 through a push-kick while riding (Hold.Skate), -1 = none
    [Tooltip("Riding a skateboard: how far the body is lifted to stand on the deck (model units).")]
    public float skateLift = 0.05f;

    [Header("Motion capture (Resources/Anim)")]
    [Tooltip("Off = the old code-driven walk / idle / punch only.")]
    public bool useMocap = true;
    [Tooltip("A punch this soon after a jab is the cross (callers that don't say which one).")]
    public float comboWindow = 0.9f;
    [Tooltip("Walking faster than the clip: strides get up to this much longer (the rest is quicker steps).")]
    [Range(1f, 1.5f)] public float maxStrideStretch = 1.3f;

    [Header("Personal style (enemies randomise these)")]
    [Range(0.8f, 1.2f)] public float strideScale = 1f;
    [Range(0.4f, 1.5f)] public float armSwingScale = 1f;
    [Tooltip("Degrees of forward slouch.")] public float hunch;
    [Tooltip("Extra hip and shoulder roll when walking.")] [Range(0, 1)] public float swagger;

    bool dead; float deadT, fallDir = -1f;

    // Collapse: knees buckle, then the body goes down (backwards or forwards) and settles.
    public void Die(bool forwards = false)
    {
        if (dead) return;
        dead = true; deadT = 0; fallDir = forwards ? 1f : -1f;
        if (rig != null) for (int j = 0; j < deadFrom.Length; j++) if (rig.joint[j]) deadFrom[j] = rig.joint[j].localRotation;   // fall from the pose we're in
    }

    BlockyCharacter b;
    Quaternion hipsR, spineR, neckR, headR, shLR, shRR, elLR, elRR, legLR, legRR, knLR, knRR;
    Vector3 hipsP, lastPos;
    float lastYaw, phase, move, run, air, crouch, t, flinch, talkT, squatT, blinkT = 2f, blink, glanceT = 4f, glance;
    Act act; float actT = -1f, actDur = 0.6f, actHit = 0.55f;
    FirstPersonController fpc;
    Transform[] lids; Vector3[] lidScale; Transform mouth; Vector3 mouthScale;
    SkinnedMeshRenderer face; int blinkL = -1, blinkR = -1, jaw = -1;          // realistic faces blink / talk with blendshapes

    // ---------- mocap state ----------
    const int NJ = MocapClip.Joints;
    MocapRig rig; bool mocapOn;
    MocapClip idleClip, walkClip, runClip, jabClip, crossClip;
    readonly Quaternion[] gIdle = new Quaternion[NJ], gWalk = new Quaternion[NJ], gRun = new Quaternion[NJ], gPunch = new Quaternion[NJ],
                          dPose = new Quaternion[NJ], dPunch = new Quaternion[NJ], dPunchFrom = new Quaternion[NJ], deadFrom = new Quaternion[NJ];
    Vector3 mHips;                                     // hips offset from the clips (model units)
    float mPhase, mMove, idleT, idleRate = 1f, lowerYaw;
    float wLegs, wBody, wArmL, wArmR;                  // how much of each part the mocap drives (the rest is code-driven)
    Quaternion handLRest = Quaternion.identity;
    // punch layer: the Jab / Cross clip over the top (whole body standing still, upper body while moving)
    MocapClip pClip; float pTime, pRateIn, pW, pFade = 1f; bool pActive;
    float lastPunchAt = -99f; int lastCombo = 1;

    // smoothed joint angles (degrees) so switching poses never snaps
    Vector3 sL, sR, eL, eR, spineE, headE, lgL, lgR, knL, knR, anL, anR;
    Quaternion anLR = Quaternion.identity, anRR = Quaternion.identity;

    void Awake()
    {
        b = GetComponent<BlockyCharacter>();
        hipsR = b.hips.localRotation; hipsP = b.hips.localPosition;
        spineR = b.spine.localRotation; neckR = b.neck ? b.neck.localRotation : Quaternion.identity; headR = b.head.localRotation;
        shLR = b.shoulderL.localRotation; shRR = b.shoulderR.localRotation;
        elLR = b.elbowL.localRotation; elRR = b.elbowR.localRotation;
        legLR = b.legL.localRotation; legRR = b.legR.localRotation;
        knLR = b.kneeL.localRotation; knRR = b.kneeR.localRotation;
        if (b.ankleL) anLR = b.ankleL.localRotation;
        if (b.ankleR) anRR = b.ankleR.localRotation;
        lastPos = transform.position; lastYaw = transform.eulerAngles.y;
        t = Random.value * 10f;
        if (b.handL) handLRest = b.handL.localRotation;
        if (b.handR) { handRRest = b.handR.localRotation; handRestSet = true; }

        // mocap: the same clips on every body (a crowd starts its idle at different points and breathes at its own pace)
        for (int j = 0; j < NJ; j++) dPose[j] = dPunch[j] = dPunchFrom[j] = deadFrom[j] = Quaternion.identity;
        rig = new MocapRig(b);
        idleClip = MocapClip.Get("Idle"); walkClip = MocapClip.Get("Walk"); runClip = MocapClip.Get("Run");
        jabClip = MocapClip.Get("Jab"); crossClip = MocapClip.Get("Cross");
        idleT = Random.value * 10f; idleRate = Random.Range(0.9f, 1.1f);
        fpc = GetComponentInParent<FirstPersonController>();

        var found = new System.Collections.Generic.List<Transform>();
        foreach (Transform c in b.head) { if (c.name == "Lid") found.Add(c); if (c.name == "Mouth") { mouth = c; mouthScale = c.localScale; } }
        lids = found.ToArray();
        lidScale = System.Array.ConvertAll(lids, l => l.localScale);
        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var m = smr.sharedMesh; if (!m) continue;
            int i = m.GetBlendShapeIndex("Eye_Blink_L");
            if (i < 0) continue;
            face = smr; blinkL = i; blinkR = m.GetBlendShapeIndex("Eye_Blink_R"); jaw = m.GetBlendShapeIndex("V_Open");
            break;
        }
    }

    // ---------- actions ----------

    // combo: 0 = jab (left hand), 1 = cross (right); -1 = pick it: a cross if the last punch was a jab not long ago.
    // Bare hands play the mocap clips; holding something (the crutch's poke) keeps the code-driven right-hand jab.
    public void Punch(float duration, float hitMoment, int combo = -1)
    {
        float now = Application.isPlaying ? Time.time : t;
        if (combo < 0) combo = lastCombo == 0 && now - lastPunchAt < comboWindow ? 1 : 0;
        lastPunchAt = now; lastCombo = combo;
        var clip = combo == 1 ? crossClip : jabClip;
        if (mocapOn && hold == Hold.None && clip != null) StartPunch(clip, duration, hitMoment);
        else Play(Act.Punch, duration, hitMoment);
    }
    public void Swing(float duration = 0.45f, float hitMoment = 0.45f) => Play(Act.Swing, duration, hitMoment);
    public void Slam(float duration = 0.6f) => Play(Act.Slam, duration, 0.55f);
    public void Throw(float duration = 0.55f, float release = 0.6f) => Play(Act.Throw, duration, release);
    public void Strum(float duration = 0.3f) => Play(Act.Strum, duration, 0.5f);
    public void Wave(float duration = 1.4f) => Play(Act.Wave, duration, 0.5f);
    public void Sweep(float duration = 0.4f, float hitMoment = 0.4f) => Play(Act.Sweep, duration, hitMoment);
    public void Flick(float duration = 0.3f) => Play(Act.Flick, duration, 0.55f);   // reach over, draw back, whip out (matches HandMotion.Flick)
    public void Toss(float duration = 0.35f) => Play(Act.Toss, duration, 0.5f);
    public void Talk(float duration = 1.2f) => talkT = Mathf.Max(talkT, duration);
    public void Squat(float duration = 0.8f) => squatT = Mathf.Max(squatT, duration);
    public void Flinch() { flinch = 1f; pActive = false; if (act == Act.Punch || act == Act.Throw || act == Act.Swing || act == Act.Sweep) actT = -1f; }

    // Gait curves: (cycle position, degrees) pairs; 0 = this foot's heel strike. From human gait data, simplified.
    // Hip is degrees forward of vertical; knee is bend. Walking: knee gives on landing, starts bending before the toes
    // leave (pre-swing), peaks early in the swing and straightens just before landing. Running: bent the whole time,
    // big tuck in the swing, thigh driven high and forward; the foot leaves at ~35% (flight after that).
    static readonly float[] WalkHip  = { 0f, 25f,  0.15f, 20f,  0.52f, -20f,  0.62f, -13f,  0.87f, 29f };
    static readonly float[] RunHip   = { 0f, 30f,  0.18f,  6f,  0.38f, -26f,  0.5f,  -16f,  0.85f, 48f };
    static readonly float[] WalkKnee = { 0f,  5f,  0.12f, 18f,  0.4f,    5f,  0.58f,  38f,  0.72f, 62f,  0.88f, 20f };
    static readonly float[] RunKnee  = { 0f, 20f,  0.14f, 40f,  0.32f,  20f,  0.45f,  48f,  0.66f, 108f, 0.86f, 42f };

    // Smooth looping curve through the keys (Catmull-Rom tangents).
    static float Loop(float[] k, float g)
    {
        int n = k.Length / 2;
        float X(int i) => k[2 * (((i % n) + n) % n)] + Mathf.Floor(i / (float)n);
        float Y(int i) => k[2 * (((i % n) + n) % n) + 1];
        int j = -1;
        while (j < n - 1 && X(j + 1) <= g) j++;
        float x0 = X(j), x1 = X(j + 1), h = x1 - x0, t = (g - x0) / h;
        float m0 = (Y(j + 1) - Y(j - 1)) / (X(j + 1) - X(j - 1)), m1 = (Y(j + 2) - Y(j)) / (X(j + 2) - X(j));
        float t2 = t * t, t3 = t2 * t;
        return (2 * t3 - 3 * t2 + 1) * Y(j) + (t3 - 2 * t2 + t) * h * m0 + (-2 * t3 + 3 * t2) * Y(j + 1) + (t3 - t2) * h * m1;
    }

    static float Near(float g, float centre, float width)
    {
        float d = Mathf.Repeat(g - centre + 0.5f, 1f) - 0.5f;
        return Mathf.Exp(-(d / width) * (d / width) * 2f);
    }

    // One leg at gait phase p (radians; this foot lands at p = π/2): hip (−X forward), knee bend, ankle (+ = toes down).
    // hipScale stretches the hip curve to the stride so the planted foot doesn't slide; amt fades it all in from standing.
    static void Gait(float p, float run, float amt, float hipScale, out float hip, out float knee, out float ankle)
    {
        float g = Mathf.Repeat((p - Mathf.PI * 0.5f) / (Mathf.PI * 2f), 1f);
        hip = -Mathf.Lerp(Loop(WalkHip, g), Loop(RunHip, g), run) * hipScale;
        knee = Mathf.Max(0f, Mathf.Lerp(Loop(WalkKnee, g), Loop(RunKnee, g), run)) * amt;
        // foot flat while planted (cancel the hip + knee), heel first on landing, toes push off, toes up to clear in the swing
        float toeOff = StanceEnd(run);
        float planted = Mathf.SmoothStep(0, 1, g / 0.07f) * (1f - Mathf.SmoothStep(0, 1, (g - (toeOff - 0.15f)) / 0.13f));
        ankle = -(hip + knee) * planted * 0.9f - 12f * amt * Near(g, 0f, 0.05f) + 24f * amt * Near(g, toeOff - 0.03f, 0.07f)
                - 8f * amt * Near(g, toeOff + 0.16f, 0.12f);
    }

    static float StanceEnd(float run) => Mathf.Lerp(0.6f, 0.36f, run);

    static float Bump(float p, float centre, float width)
    {
        float d = Mathf.DeltaAngle(centre * Mathf.Rad2Deg, p * Mathf.Rad2Deg) * Mathf.Deg2Rad / width;
        return Mathf.Exp(-d * d * 2f);
    }

    void Collapse(float dt)
    {
        deadT += dt;
        float buckle = Mathf.SmoothStep(0, 1, deadT / 0.3f), fall = Mathf.SmoothStep(0, 1, (deadT - 0.2f) / 0.65f);
        float k = 1f - Mathf.Exp(-dt * 14f);
        lgL = Vector3.Lerp(lgL, new Vector3(-40f * buckle, 0, -6f), k); lgR = Vector3.Lerp(lgR, new Vector3(-25f * buckle, 0, 8f), k);
        knL = Vector3.Lerp(knL, new Vector3(80f * buckle * (1f - 0.6f * fall), 0, 0), k); knR = Vector3.Lerp(knR, new Vector3(60f * buckle * (1f - 0.5f * fall), 0, 0), k);
        spineE = Vector3.Lerp(spineE, new Vector3(18f * buckle * -fallDir, 8f, 6f), k);
        headE = Vector3.Lerp(headE, new Vector3(-25f * fallDir * fall, 20f, 10f), k);
        sL = Vector3.Lerp(sL, new Vector3(-30f * fallDir * fall, 0, -35f * fall), k); sR = Vector3.Lerp(sR, new Vector3(-50f * fallDir * fall, 0, 45f * fall), k);
        eL = Vector3.Lerp(eL, new Vector3(-30f, 0, 0), k); eR = Vector3.Lerp(eR, new Vector3(-15f, 0, 0), k);
        // the whole body tips over from the feet and drops a little as the knees go
        transform.localRotation = Quaternion.Euler(88f * fallDir * fall, 0, 0);
        b.hips.localPosition = hipsP + Vector3.down * 0.25f * buckle * (1f - fall);
        b.spine.localRotation = spineR * Quaternion.Euler(spineE); b.head.localRotation = headR * Quaternion.Euler(headE);
        b.shoulderL.localRotation = shLR * Quaternion.Euler(sL); b.elbowL.localRotation = elLR * Quaternion.Euler(eL);
        b.shoulderR.localRotation = shRR * Quaternion.Euler(sR); b.elbowR.localRotation = elRR * Quaternion.Euler(eR);
        b.legL.localRotation = legLR * Quaternion.Euler(lgL); b.legR.localRotation = legRR * Quaternion.Euler(lgR);
        b.kneeL.localRotation = knLR * Quaternion.Euler(knL); b.kneeR.localRotation = knRR * Quaternion.Euler(knR);
        // ease out of whatever pose the mocap had us in
        float ease = 1f - Mathf.SmoothStep(0, 1, deadT / 0.25f);
        if (rig != null && ease > 0)
            for (int j = 0; j < NJ; j++) if (rig.joint[j]) rig.joint[j].localRotation = Quaternion.Slerp(rig.joint[j].localRotation, deadFrom[j], ease);
    }

    void Play(Act a, float duration, float hitMoment) { act = a; actT = 0f; actDur = Mathf.Max(0.05f, duration); actHit = hitMoment; }

    void LateUpdate()
    {
        float dt = Application.isPlaying ? Time.deltaTime : 1f / 30f;           // (edit-mode previews step it by hand)
        if (dt <= 0) return;
        t += dt;
        if (dead) { Collapse(dt); return; }

        // ---------- what the body is doing ----------
        Vector3 d = transform.position - lastPos; lastPos = transform.position;
        float vy = d.y / dt; d.y = 0;
        float speed = d.magnitude / dt;
        Vector3 local = transform.InverseTransformDirection(d / dt);          // forward / sideways parts of the motion
        float fwd = speed > 0.05f ? local.z / speed : 1f, side = speed > 0.05f ? local.x / speed : 0f;
        float yawRate = Mathf.DeltaAngle(lastYaw, transform.eulerAngles.y) / dt; lastYaw = transform.eulerAngles.y;

        // players: walking speed = the Walking clip, sprinting = Running (whatever upgrades do to the speeds)
        float lo = jogFrom, hi = runAt;
        if (fpc) { lo = fpc.WalkSpeedNow * 1.12f; hi = Mathf.Max(lo + 0.5f, fpc.SprintSpeedNow * 0.85f); }
        move = Mathf.MoveTowards(move, Mathf.Clamp01(speed / lo), dt * 5f);
        run = Mathf.MoveTowards(run, Mathf.SmoothStep(0, 1, (speed - lo) / (hi - lo)), dt * 4f);
        mocapOn = useMocap && rig != null && rig.Valid && idleClip != null && walkClip != null && runClip != null;
        bool inAir = fpc ? !fpc.IsGrounded && Mathf.Abs(vy) > 0.5f : Mathf.Abs(vy) > 2.5f;
        air = Mathf.MoveTowards(air, inAir ? 1f : 0f, dt * 6f);
        crouch = Mathf.MoveTowards(crouch, fpc && fpc.IsCrouching ? 1f : 0f, dt * 5f);
        flinch = Mathf.MoveTowards(flinch, 0, dt * 5f);
        talkT -= dt; squatT -= dt;
        float squat = Mathf.Clamp01(squatT * 4f) * Mathf.Clamp01(1f - (squatT - 0.8f) * 4f);

        // stepping: a natural cadence (about 120 steps a minute walking, 175 running) sets the stride for the speed;
        // backwards runs the cycle in reverse; little steps when turning on the spot
        float cadence = Mathf.Lerp(1.0f, 1.45f, run);                            // full strides (left + right) per second
        float stride = Mathf.Clamp(speed / cadence, 1.0f, 3.6f) * strideLength / 1.3f * strideScale;
        float stepSpeed = speed + (speed < 0.3f ? Mathf.Abs(yawRate) * 0.006f : 0f);
        phase = Mathf.Repeat(phase + stepSpeed / stride * Mathf.PI * 2f * dt * (fwd < -0.3f ? -1f : 1f), Mathf.PI * 2f);
        float stepAmt = Mathf.Max(move, speed < 0.3f ? Mathf.Clamp01(Mathf.Abs(yawRate) / 200f) * 0.35f : 0f);
        float s = Mathf.Sin(phase), c = Mathf.Cos(phase), idle = 1f - Mathf.Max(move, air);

        // ---------- legs: a real gait cycle per leg (left at phase, right half a cycle later) ----------
        // hip: forward-most at π/2 (heel strike), back-most at 3π/2 (toe off); the swing is the half where cos > 0.
        // knee: small give as the foot lands, big bend mid-swing. ankle: keeps the foot flat while it's planted,
        // heel first on landing, rolls onto the toes as it pushes off.
        // hip range that covers the ground the body moves over while the foot is down (legs ~0.9 m), vs the curve's own
        float travel = Mathf.Max(speed, 1f) / cadence * StanceEnd(run);
        float needRange = Mathf.Clamp(2f * Mathf.Asin(Mathf.Min(0.95f, travel / 1.8f)) * Mathf.Rad2Deg, 24f, 64f);
        float curveRange = Mathf.Lerp(45f, 56f, run);
        float hipScale = stepAmt * (1f - 0.6f * Mathf.Abs(side)) * Mathf.Lerp(1f, needRange / curveRange, move);
        float spread = side * 10f * stepAmt;                                     // sidestep: legs open and close
        Gait(phase, run, stepAmt, hipScale, out float hipL, out float kneeL, out float ankL);
        Gait(phase + Mathf.PI, run, stepAmt, hipScale, out float hipR, out float kneeR, out float ankR);
        Vector3 tLgL = new Vector3(hipL, 0, -Mathf.Abs(s) * spread), tLgR = new Vector3(hipR, 0, Mathf.Abs(s) * spread);
        Vector3 tKnL = new Vector3(2f + kneeL, 0, 0), tKnR = new Vector3(2f + kneeR, 0, 0);
        Vector3 tAnL = new Vector3(ankL, 0, 0), tAnR = new Vector3(ankR, 0, 0);
        // riding a skateboard: side-on (regular: left foot forward over the front bolts, right over the tail), knees soft;
        // a push takes the back foot off to kick along beside the board and back
        bool skating = hold == Hold.Skate;
        float push = 0;
        if (skating)
        {
            // the push: the back foot steps down beside the board by the front truck, sweeps back along the ground
            // past the tail, and comes back on; the front knee bends to reach the ground
            float pp = skatePush >= 0 ? Mathf.Clamp01(skatePush) : 0f;
            push = skatePush >= 0 ? Mathf.Sin(pp * Mathf.PI) : 0f;
            float sweep = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.15f, 0.75f, pp)) * (1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.75f, 1f, pp)));
            tLgL = new Vector3(-12f - 16f * push, 0, -11f); tLgR = new Vector3(-12f - 16f * push, 0, 11f - 8f * push + 34f * sweep);
            tKnL = new Vector3(28f + 34f * push, 0, 0); tKnR = new Vector3(28f - 24f * push, 0, 0);
            tAnL = new Vector3(-16f - 10f * push, 0, 0); tAnR = new Vector3(-16f + 18f * push, 0, 0);
        }
        // jump tuck, crouch and squat all bend the legs (and the ankles keep the feet flat)
        float bend = Mathf.Max(air * 0.8f, Mathf.Max(crouch, squat));
        tLgL = Vector3.Lerp(tLgL, new Vector3(-55f, 0, -4f), bend); tLgR = Vector3.Lerp(tLgR, new Vector3(-55f, 0, 4f), bend);
        tKnL = Vector3.Lerp(tKnL, new Vector3(95f, 0, 0), bend); tKnR = Vector3.Lerp(tKnR, new Vector3(95f, 0, 0), bend);
        tAnL = Vector3.Lerp(tAnL, new Vector3(air > 0.5f ? 25f : -40f, 0, 0), bend); tAnR = Vector3.Lerp(tAnR, new Vector3(air > 0.5f ? 25f : -40f, 0, 0), bend);
        if (skating && air > 0.05f) { tLgL.z = -11f; tLgR.z = 11f; }           // feet stay apart on the board in the air

        // ---------- pelvis: bob twice per stride (lowest as each heel lands), shift over the planted leg,
        // turn with the forward leg and dip on the swinging side ----------
        // (the code-driven gait fades out wherever the mocap legs are on: pw = its share)
        bool backwards = fwd < -0.3f;
        bool actBusy = actT >= 0;                                                // a code-driven move (swing, throw, ...)
        bool leftHold = hold == Hold.Guitar || hold == Hold.Tray || hold == Hold.CarryLeft || hold == Hold.HangLeft || hold == Hold.Carton;
        bool rightBusy = (hold != Hold.None && hold != Hold.Skate) || charge > 0.01f || inhaling || actBusy;
        bool leftBusy = leftHold || talkT > 0 || (actBusy && (act == Act.Punch || act == Act.Slam || act == Act.Throw || act == Act.Toss));
        float on = mocapOn ? 1f : 0f;
        wLegs = Mathf.MoveTowards(wLegs, on * (skating || (actBusy && act == Act.Slam) ? 0f : 1f - Mathf.Max(air, Mathf.Max(crouch, squat))), dt * 8f);
        wBody = Mathf.MoveTowards(wBody, on * (skating ? 0f : 1f), dt * 6f);
        wArmR = Mathf.MoveTowards(wArmR, on * (rightBusy || skating ? 0f : 1f - air), dt * 10f);
        wArmL = Mathf.MoveTowards(wArmL, on * (leftBusy || skating ? 0f : 1f - air), dt * 10f);
        float pw = 1f - wLegs;
        UpdatePunch(dt);
        if (mocapOn) MocapPose(dt, speed, stepSpeed, speed < 0.3f ? Mathf.Clamp01(Mathf.Abs(yawRate) / 200f) * 0.35f : 0f, backwards);
        // sideways: the legs walk toward where you're going and the chest turns back to the front
        float yawWant = mocapOn && speed > 0.4f && !skating ? Mathf.Clamp(Mathf.Atan2(backwards ? -local.x : local.x, Mathf.Abs(local.z)) * Mathf.Rad2Deg, -70f, 70f) : 0f;
        lowerYaw = Mathf.Lerp(lowerYaw, yawWant * wLegs, 1f - Mathf.Exp(-dt * 8f));

        float bobAmp = skating ? 0f : Mathf.Lerp(0.028f, 0.055f, run) * move * pw;
        // walking is lowest just after each heel lands; running is lowest mid-stance and highest in the flight
        float bob = bobAmp * (0.5f + 0.5f * Mathf.Cos(2f * (phase - run * 0.4f * Mathf.PI))) - bobAmp;
        float weight = 0.022f * Mathf.Lerp(1f, 0.4f, run) * move * c * pw;        // over the left leg when it's planted (cos < 0)
        float sway = (Mathf.Sin(t * 0.8f) * 0.012f * idle + Mathf.Sin(t * 0.23f) * 0.015f * idle) * pw;   // idle weight shifts
        Vector3 hipsOffset = new Vector3(sway + weight, bob - Mathf.Max(crouch * 0.32f, squat * 0.28f), 0) + (skating ? Vector3.up * (skateLift - 0.07f * push) : Vector3.zero);
        float pelvisYaw = s * Mathf.Lerp(8f, 12f, run) * stepAmt * (1f + swagger) * pw, pelvisDrop = c * 4f * move * (1f + swagger * 1.5f) * pw;
        if (skating) { pelvisYaw = 80f; pelvisDrop = 0; }                          // side-on to the board
        Vector3 hipsE = new Vector3(0, pelvisYaw, pelvisDrop + (Mathf.Sin(t * 0.8f) * 1.5f * idle - side * 4f * move) * pw);
        float breathe = Mathf.Sin(t * 1.7f) * pw;
        // chest counter-rotates against the hips, leans into a run, and rocks a touch against the pelvis dip
        Vector3 tSpine = new Vector3(hunch + (Mathf.Lerp(3f, 14f, run) * move + Mathf.Abs(Mathf.Cos(phase)) * 2f * run) * pw + breathe * idle
                                     + crouch * 22f + squat * 25f - flinch * 18f + air * 6f,
                                     -pelvisYaw * 1.9f, -pelvisDrop * 0.6f + side * 5f * move * pw);

        // ---------- arms: swing opposite the legs, a beat behind, elbows following through ----------
        float lag = 0.35f, sa = Mathf.Sin(phase - lag), ca = Mathf.Cos(phase - lag);
        float armSwing = Mathf.Lerp(24f, 48f, run) * move * armSwingScale, elbowBase = Mathf.Lerp(14f, 88f, run);
        Vector3 tSL = new Vector3(sa * armSwing + Mathf.Sin(t * 1.1f) * 1.5f * idle, 0, -3f - 6f * run);
        Vector3 tSR = new Vector3(-sa * armSwing + Mathf.Sin(t * 1.1f + 1f) * 1.5f * idle, 0, 3f + 6f * run);
        Vector3 tEL = new Vector3(-elbowBase - Mathf.Max(0, -sa) * armSwing * 0.9f + ca * 4f * move, 0, 0);
        Vector3 tER = new Vector3(-elbowBase - Mathf.Max(0, sa) * armSwing * 0.9f - ca * 4f * move, 0, 0);
        if (air > 0.01f) { tSL = Vector3.Lerp(tSL, new Vector3(-35f, 0, -35f), air); tSR = Vector3.Lerp(tSR, new Vector3(-35f, 0, 35f), air); }

        switch (hold)
        {
            case Hold.Guitar: tSL = new Vector3(-62f, 0, 4f); tEL = new Vector3(-75f, 0, 0); tSR = new Vector3(-22f, 0, 12f); tER = new Vector3(-70f, 0, 0); break;
            case Hold.Book:   tSR = new Vector3(-16f, 0, 8f) + new Vector3(s * armSwing * 0.3f, 0, 0); tER = new Vector3(-78f, 0, 0); break;   // forearm forward: the book by its spine, the crutch pointing out
            case Hold.Cart:   tSR = new Vector3(-14f, 0, 6f); tER = new Vector3(-95f, 0, 0); break;
            case Hold.Tray:   tSL = new Vector3(-48f, 0, 4f); tSR = new Vector3(-48f, 0, -4f); tEL = tER = new Vector3(-48f, 0, 0); break;
            case Hold.Phone:  tSR = new Vector3(-32f, 0, 14f); tER = new Vector3(-128f, 0, 0); tSpine.x += 6f; break;
            case Hold.CarryLeft: tSL = new Vector3(-16f, 0, 16f); tEL = new Vector3(-96f, 0, 0); tSR = new Vector3(-18f, 0, 6f) + new Vector3(s * armSwing * 0.3f, 0, 0); tER = new Vector3(-60f, 0, 0); break;   // deck / chip case in the left hand
            case Hold.HangLeft:  tSL = new Vector3(-4f, 0, -10f); tEL = new Vector3(-12f, 0, 0); tSR = new Vector3(-20f, 0, 6f) + new Vector3(s * armSwing * 0.3f, 0, 0); tER = new Vector3(-75f, 0, 0); break;  // 6-pack carrier at your side, bottle in the right
            case Hold.Carton:    tSL = new Vector3(-40f, 0, 17f); tSR = new Vector3(-40f, 0, -17f); tEL = tER = new Vector3(-62f, 0, 0); break;   // a big box in both hands
        }
        if (charge > 0) { tSR = Vector3.Lerp(tSR, new Vector3(-160f, 0, 12f), charge); tER = Vector3.Lerp(tER, new Vector3(-35f, 0, 0), charge); }
        if (inhaling) { tSR = new Vector3(-38f, 0, 18f); tER = new Vector3(-145f, 0, 0); }
        if (skating)
        {
            // chest turned back toward where you're going, arms loose and out for balance
            tSpine.y = -pelvisYaw * 0.55f; tSpine.x = 8f + air * 6f;
            float wob = Mathf.Sin(t * 1.3f) * 6f;
            tSL = new Vector3(-8f + wob, 0, -32f); tSR = new Vector3(-6f - wob, 0, 30f);
            tEL = tER = new Vector3(-28f, 0, 0);
        }

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
            // the newer moves blend from the hold pose into the wind-up, snap to the hit and ease back to the hold
            float into = p < actHit ? 0 : Mathf.SmoothStep(0, 1, (p - actHit) / Mathf.Max(0.01f, (1 - actHit) * 0.35f));
            Vector3 Blend(Vector3 hold, Vector3 windPose, Vector3 hitPose) => Vector3.Lerp(Vector3.Lerp(Vector3.Lerp(hold, windPose, wind), hitPose, into), hold, back);
            switch (act)
            {
                case Act.Sweep:   // arm out to the right, then across in front of you (the item leads), chest turning with it
                    tSR = Blend(tSR, new Vector3(-70f, 0, 55f), new Vector3(-80f, 0, -28f)); tER = Blend(tER, new Vector3(-50f, 0, 0), new Vector3(-15f, 0, 0));
                    tSpine.y += Mathf.Lerp(22f * wind, -30f, into) * (1f - back);
                    break;
                case Act.Flick:   // right hand over to the left for one, then whip it out toward the aim
                    tSR = Blend(tSR, new Vector3(-32f, 0, -24f), new Vector3(-78f, 0, 8f)); tER = Blend(tER, new Vector3(-105f, 0, 0), new Vector3(-12f, 0, 0));
                    tSpine.y += Mathf.Lerp(-10f * wind, 10f, into) * (1f - back);
                    break;
                case Act.Toss:    // both hands lift the box up and forward
                    float lift = Mathf.Sin(p * Mathf.PI);
                    tSL.x -= 32f * lift; tSR.x -= 32f * lift; tEL.x += 22f * lift; tER.x += 22f * lift; tSpine.x -= 5f * lift;
                    break;
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
        Vector3 tHead = new Vector3(-2f * move * pw - flinch * 10f, s * 6f * stepAmt * pw, 0);
        if (skating) tHead.y = -pelvisYaw * 0.4f;                                 // eyes where the board is going
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
        if (hold == Hold.Phone) { tHead.x += 22f; tHead.y = 0; }                // eyes on the screen
        if (mouth) mouth.localScale = talkT > 0 ? Vector3.Scale(mouthScale, new Vector3(1f, 1f + 2.5f * Mathf.Abs(Mathf.Sin(t * 14f)), 1f)) : mouthScale;

        // blinking every few seconds
        if ((blinkT -= dt) <= 0) { blinkT = Random.Range(2.5f, 5.5f); blink = 1f; }
        blink = Mathf.MoveTowards(blink, 0, dt * 9f);
        for (int i = 0; i < lids.Length; i++) if (lids[i]) lids[i].localScale = new Vector3(lidScale[i].x, lidScale[i].y * (1f + blink * 1.6f), lidScale[i].z);
        if (face)
        {
            float shut = Mathf.Clamp01(blink * 1.4f) * 100f;
            face.SetBlendShapeWeight(blinkL, shut); if (blinkR >= 0) face.SetBlendShapeWeight(blinkR, shut);
            if (jaw >= 0) face.SetBlendShapeWeight(jaw, talkT > 0 ? 60f * Mathf.Abs(Mathf.Sin(t * 14f)) : 0f);
        }

        // ---------- apply, smoothed ----------
        float k = 1f - Mathf.Exp(-dt * 18f), kl = 1f - Mathf.Exp(-dt * 40f);   // legs follow the gait tightly
        sL = Vector3.Lerp(sL, tSL, k); sR = Vector3.Lerp(sR, tSR, k); eL = Vector3.Lerp(eL, tEL, k); eR = Vector3.Lerp(eR, tER, k);
        spineE = Vector3.Lerp(spineE, tSpine, k); headE = Vector3.Lerp(headE, tHead, 1f - Mathf.Exp(-dt * 10f));
        lgL = Vector3.Lerp(lgL, tLgL, kl); lgR = Vector3.Lerp(lgR, tLgR, kl); knL = Vector3.Lerp(knL, tKnL, kl); knR = Vector3.Lerp(knR, tKnR, kl);
        anL = Vector3.Lerp(anL, tAnL, kl); anR = Vector3.Lerp(anR, tAnR, kl);

        // mocap underneath, by part: the hips / legs, spine / neck / head (the code-driven angles add on top of those),
        // and each arm (a code-driven arm pose replaces the mocap one while it's needed)
        Quaternion Mo(int j, float w) => w <= 0.001f ? Quaternion.identity : Quaternion.Slerp(Quaternion.identity, dPose[j], w);
        Quaternion Mix(Vector3 e, int j, float w) => w <= 0.001f ? Quaternion.Euler(e) : Quaternion.Slerp(Quaternion.Euler(e), dPose[j], w);
        Vector3 mocapHips = wLegs > 0.001f ? b.hips.parent.InverseTransformVector(transform.TransformVector(mHips)) * wLegs : Vector3.zero;
        b.hips.localPosition = hipsP + hipsOffset + mocapHips;
        Quaternion turn = rig != null && Mathf.Abs(lowerYaw) > 0.01f ? rig.InRest(0, Quaternion.AngleAxis(lowerYaw, Vector3.up)) : Quaternion.identity;
        Quaternion unturn = rig != null && Mathf.Abs(lowerYaw) > 0.01f ? rig.InRest(1, Quaternion.AngleAxis(-lowerYaw, Vector3.up)) : Quaternion.identity;
        b.hips.localRotation = hipsR * turn * Mo(0, wLegs) * Quaternion.Euler(hipsE);
        b.spine.localRotation = spineR * unturn * Mo(1, wBody) * Quaternion.Euler(spineE);
        if (b.neck) b.neck.localRotation = neckR * Mo(2, wBody);
        b.head.localRotation = headR * Mo(3, wBody) * Quaternion.Euler(headE);
        b.shoulderL.localRotation = shLR * Mix(sL, 4, wArmL); b.elbowL.localRotation = elLR * Mix(eL, 5, wArmL);
        if (b.handL) b.handL.localRotation = handLRest * Mo(6, wArmL);
        b.shoulderR.localRotation = shRR * Mix(sR, 7, wArmR); b.elbowR.localRotation = elRR * Mix(eR, 8, wArmR);
        b.legL.localRotation = legLR * Mix(lgL, 10, wLegs); b.legR.localRotation = legRR * Mix(lgR, 13, wLegs);
        b.kneeL.localRotation = knLR * Mix(knL, 11, wLegs); b.kneeR.localRotation = knRR * Mix(knR, 14, wLegs);
        if (b.ankleL) b.ankleL.localRotation = anLR * Mix(anL, 12, wLegs);
        if (b.ankleR) b.ankleR.localRotation = anRR * Mix(anR, 15, wLegs);

        ReachMouth(dt);
        Fingers(dt);
    }

    // ---------- mocap ----------

    // The base pose from the clips: idle, blended into walk ↔ run by how fast we're really going (turnStep: little
    // steps turning on the spot), then the punch on top. Result: dPose (per joint, relative to rest) and mHips.
    void MocapPose(float dt, float speed, float stepSpeed, float turnStep, bool backwards)
    {
        idleT += dt * idleRate;
        rig.Sample(idleClip, idleT, gIdle, out var hips);
        mMove = Mathf.MoveTowards(mMove, Mathf.Clamp01(speed / 1.1f), dt * 4f);
        float walk = Mathf.Max(mMove, turnStep);
        float stretch = 1f;
        if (walk > 0.001f)
        {
            // one step cycle shared by both clips (lined up on the left heel strike), advancing by the ground covered
            float size = transform.lossyScale.y;
            float strideW = walkClip.stride * rig.Scale(walkClip) * size, strideR = runClip.stride * rig.Scale(runClip) * size;
            float natural = Mathf.Lerp(strideW / walkClip.duration, strideR / runClip.duration, run);
            // walking faster than the clip does: longer strides as well as quicker steps
            stretch = Mathf.Lerp(Mathf.Clamp(Mathf.Sqrt(Mathf.Max(speed, 0.01f) / natural), 1f, maxStrideStretch), 1f, run);
            float cycles = Mathf.Max(stepSpeed, natural * 0.6f) / (Mathf.Lerp(strideW, strideR, run) * stretch * strideScale);   // (a crowd's own step lengths)
            mPhase = Mathf.Repeat(mPhase + cycles * dt * (backwards ? -1f : 1f), 1f);
            Vector3 hw = Vector3.zero, hr = Vector3.zero;
            if (run < 0.999f) rig.Sample(walkClip, (mPhase + walkClip.phase0) * walkClip.duration, gWalk, out hw);
            if (run > 0.001f) rig.Sample(runClip, (mPhase + runClip.phase0) * runClip.duration, gRun, out hr);
            for (int j = 0; j < NJ; j++)
            {
                var loco = run <= 0.001f ? gWalk[j] : run >= 0.999f ? gRun[j] : Quaternion.Slerp(gWalk[j], gRun[j], run);
                gIdle[j] = Quaternion.Slerp(gIdle[j], loco, walk);
            }
            hips = Vector3.Lerp(hips, Vector3.Lerp(hw, hr, run), walk);
        }
        // (gIdle now holds the base pose)
        rig.ToLocal(gIdle, dPose);

        if (pW > 0.001f && pClip != null)
        {
            rig.Sample(pClip, pTime, gPunch, out var ph);
            // on the move the legs keep walking and the punch turns the chest from where the hips are
            gPunch[0] = Quaternion.Slerp(gPunch[0], gIdle[0], walk);
            rig.ToLocal(gPunch, dPunch);
            if (pFade < 1f) { float f = Mathf.SmoothStep(0, 1, pFade); for (int j = 0; j < NJ; j++) dPunch[j] = Quaternion.Slerp(dPunchFrom[j], dPunch[j], f); }
            float upper = pW, lower = pW * (1f - walk);
            for (int j = 0; j < NJ; j++) dPose[j] = Quaternion.Slerp(dPose[j], dPunch[j], j >= 1 && j <= 9 ? upper : lower);
            hips = Vector3.Lerp(hips, ph, lower);
        }
        if (stretch > 1.001f)
        {
            dPose[10] = Quaternion.SlerpUnclamped(Quaternion.identity, dPose[10], stretch);
            dPose[13] = Quaternion.SlerpUnclamped(Quaternion.identity, dPose[13], stretch);
        }
        mHips = hips;
    }

    // Start the jab or cross so its punch lands hitMoment of the way through duration: the wind-up is squeezed in
    // (skipping the start of the clip if it would have to go more than 2.2× speed), the recovery plays a bit fast.
    void StartPunch(MocapClip clip, float duration, float hitMoment)
    {
        if (pClip != null && pW > 0.05f) { System.Array.Copy(dPunch, dPunchFrom, NJ); pFade = 0f; }   // straight from the last one
        else pFade = 1f;
        float h = Mathf.Max(0.06f, duration * hitMoment), impactT = clip.ImpactTime;
        float start = Mathf.Max(0f, impactT - h * 2.2f);
        pRateIn = Mathf.Max(0.5f, (impactT - start) / h);
        pClip = clip; pTime = start; pActive = true;
        actT = -1f;                                                               // (a code-driven move gives way)
    }

    void UpdatePunch(float dt)
    {
        if (pClip == null) return;
        if (pActive)
        {
            pTime += dt * (pTime < pClip.ImpactTime ? pRateIn : 1.35f);
            if (pTime >= pClip.duration - 0.02f) { pTime = pClip.duration - 0.02f; pActive = false; }
        }
        float target = pActive ? Mathf.Clamp01((pClip.duration - pTime) / 0.3f) : 0f;         // back to guard, then ease out
        pW = Mathf.MoveTowards(pW, target, dt * (pW < target ? 16f : 5f));
        pFade = Mathf.Min(1f, pFade + dt / 0.12f);
        if (!pActive && pW <= 0f) pClip = null;
    }

    // ---------- hands: fingers curl round what they hold ----------

    // Hold → how far each hand's fingers curl (0 relaxed … 1 closed round a handle).
    [System.NonSerialized] public float gripOverrideR = -1f, gripOverrideL = -1f;   // weapons can force a grip (-1 = automatic)
    [System.NonSerialized] public FirstPersonHand.Grip? fingersR;                     // or shape each finger (a card between two); cleared each frame
    readonly float[] curlR = new float[5], curlL = new float[5];
    Transform[][] fingR, fingL;                                                        // [finger][segment], thumb last

    void FindFingers()
    {
        Transform[][] Find(string side)
        {
            var names = new[] { "Index", "Mid", "Ring", "Pinky", "Thumb" };
            var list = new Transform[names.Length][];
            for (int f = 0; f < names.Length; f++)
            {
                list[f] = new Transform[3];
                for (int j = 0; j < 3; j++) list[f][j] = FindDeep(transform, $"CC_Base_{side}_{names[f]}{j + 1}");
                if (!list[f][0]) return null;
            }
            return list;
        }
        fingR = Find("R"); fingL = Find("L");
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t) { var r = FindDeep(c, name); if (r) return r; }
        return null;
    }

    void Fingers(float dt)
    {
        if (fingR == null && fingL == null) { if (!triedFingers) { triedFingers = true; FindFingers(); } if (fingR == null) return; }
        bool punching = (act == Act.Punch && actT >= 0) || pW > 0.05f;
        // how far each hand closes for what it's holding: a fist round handles and bottles, a hook over a book's edge,
        // flat palms on a box, a loose supporting hand under a case
        float wantR = gripOverrideR >= 0 ? gripOverrideR
            : punching || inhaling ? 1f
            : hold == Hold.Book ? 0.62f : hold == Hold.Carton ? 0.25f : hold == Hold.Tray ? 0.3f : hold == Hold.CarryLeft ? 0.3f : hold == Hold.HangLeft ? 0.72f : hold == Hold.None ? 0.2f : 0.95f;
        float wantL = gripOverrideL >= 0 ? gripOverrideL
            : hold == Hold.HangLeft || hold == Hold.Guitar ? 1f : hold == Hold.CarryLeft ? 0.45f : hold == Hold.Carton ? 0.25f : hold == Hold.Tray ? 0.3f : 0.2f;
        float k = 1f - Mathf.Exp(-dt * 16f);
        for (int f = 0; f < 5; f++)
        {
            curlR[f] = Mathf.Lerp(curlR[f], fingersR.HasValue ? fingersR.Value[f] : wantR, k);
            curlL[f] = Mathf.Lerp(curlL[f], pW > 0.05f && hold == Hold.None ? 1f : wantL, k);   // both fists up for the combo
        }
        fingersR = null;
        Curl(fingR, curlR, -1f); Curl(fingL, curlL, 1f);
    }
    bool triedFingers;

    // Fingers hang straight down at rest with the knuckles along Z; curling toward the palm is about Z
    // (the right palm faces -X, so the right hand turns the other way).
    static void Curl(Transform[][] hand, float[] curl, float sign)
    {
        if (hand == null) return;
        float[] seg = { 72f, 95f, 60f };
        for (int f = 0; f < 4; f++)
        {
            float g = curl[f];
            for (int j = 0; j < 3; j++)
                if (hand[f][j]) hand[f][j].localRotation = Quaternion.Euler(0, 0, sign * (8f + seg[j] * g) * (1f - 0.08f * f * (1f - g)));
        }
        var th = hand[4]; float gt = curl[4];                                          // thumb: across the front of the fingers
        if (th[0]) th[0].localRotation = Quaternion.Euler(-25f * gt, 0, sign * 20f * gt);
        if (th[1]) th[1].localRotation = Quaternion.Euler(0, 0, sign * 25f * gt);
        if (th[2]) th[2].localRotation = Quaternion.Euler(0, 0, sign * 30f * gt);
    }

    // ---------- the cart to the mouth (third person) ----------

    [Tooltip("What goes in the mouth while hitting the cart (its mouthpiece); set by the cart.")]
    [System.NonSerialized] public Transform mouthItemTip;
    float reach;
    Quaternion handRRest; bool handRestSet;

    // Two-bone reach for the right arm so the held item's tip ends up at the lips, elbow down and out.
    void ReachMouth(float dt)
    {
        reach = Mathf.MoveTowards(reach, inhaling && mouthItemTip ? 1f : 0f, dt * 5f);
        if (!b.handR || !b.head) return;
        if (!handRestSet) { handRRest = b.handR.localRotation; handRestSet = true; }
        b.handR.localRotation = handRRest * (wArmR > 0.001f ? Quaternion.Slerp(Quaternion.identity, dPose[9], wArmR) : Quaternion.identity);   // rest (+ the mocap wrist)
        if (reach <= 0.001f || !mouthItemTip) return;
        float s = transform.lossyScale.y;
        Vector3 mouth = b.head.position + transform.forward * 0.1f * s + transform.up * 0.015f * s;
        for (int iter = 0; iter < 2; iter++)
        {
            // where the hand has to be so the tip lands on the mouth (the item's tip → hand offset, kept)
            Vector3 handTarget = mouth - (mouthItemTip.position - b.handR.position);
            Solve(b.shoulderR, b.elbowR, b.handR, Vector3.Lerp(b.handR.position, handTarget, reach), transform.right * 0.6f - transform.up);
            // turn the hand so the tip points at the mouth
            Vector3 have = mouthItemTip.position - b.handR.position, want = mouth - b.handR.position;
            b.handR.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(have, want), reach) * b.handR.rotation;
        }
    }

    static void Solve(Transform shoulder, Transform elbow, Transform hand, Vector3 target, Vector3 pole)
    {
        Vector3 S = shoulder.position, E = elbow.position, H = hand.position;
        float l1 = (E - S).magnitude, l2 = (H - E).magnitude;
        Vector3 toT = target - S; float d = Mathf.Clamp(toT.magnitude, 0.05f, l1 + l2 - 0.001f);
        float cosA = Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2f * l1 * d), -1f, 1f);
        Vector3 dir = toT.normalized;
        Vector3 n = Vector3.Cross(dir, pole).normalized;
        if (n.sqrMagnitude < 1e-6f) n = Vector3.Cross(dir, Vector3.up).normalized;
        Vector3 upper = Quaternion.AngleAxis(Mathf.Acos(cosA) * Mathf.Rad2Deg, n) * dir;
        if (Vector3.Dot(upper, pole) < 0) upper = Quaternion.AngleAxis(-Mathf.Acos(cosA) * Mathf.Rad2Deg, n) * dir;
        shoulder.rotation = Quaternion.FromToRotation(E - S, upper) * shoulder.rotation;
        Vector3 E2 = elbow.position, H2 = hand.position;
        elbow.rotation = Quaternion.FromToRotation(H2 - E2, target - E2) * elbow.rotation;
    }
}
