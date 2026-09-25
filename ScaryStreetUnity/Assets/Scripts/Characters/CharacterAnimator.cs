using UnityEngine;

// Code-driven animation for a BlockyCharacter, in the spirit of the web build's poseCharacter():
// walk cycle from how fast the character is actually moving, idle breathing and sway,
// a wind-up punch with the right hand, and a flinch when hit.
[RequireComponent(typeof(BlockyCharacter))]
public class CharacterAnimator : MonoBehaviour
{
    [Tooltip("Meters covered per full stride (left + right step).")]
    public float strideLength = 1.3f;
    [Tooltip("Speed (m/s) at which the walk swing is at full size.")]
    public float fullSwingSpeed = 3f;
    public float legSwing = 32f, armSwing = 26f;

    BlockyCharacter b;
    Quaternion hipsR, spineR, headR, shLR, shRR, elLR, elRR, legLR, legRR, knLR, knRR;
    Vector3 hipsP, lastPos;
    float phase, move, t;
    float punchT = -1f, punchDur = 0.6f, punchHit = 0.55f, flinch;

    void Awake()
    {
        b = GetComponent<BlockyCharacter>();
        hipsR = b.hips.localRotation; hipsP = b.hips.localPosition;
        spineR = b.spine.localRotation; headR = b.head.localRotation;
        shLR = b.shoulderL.localRotation; shRR = b.shoulderR.localRotation;
        elLR = b.elbowL.localRotation; elRR = b.elbowR.localRotation;
        legLR = b.legL.localRotation; legRR = b.legR.localRotation;
        knLR = b.kneeL.localRotation; knRR = b.kneeR.localRotation;
        lastPos = transform.position;
        t = Random.value * 10f;                                   // crowds don't breathe in sync
    }

    // Right-hand punch: pull back until hitMoment (0..1 of duration), then jab and recover.
    public void Punch(float duration, float hitMoment)
    {
        punchT = 0f; punchDur = Mathf.Max(0.05f, duration); punchHit = hitMoment;
    }

    public void Flinch() { flinch = 1f; punchT = -1f; }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0) return;
        t += dt;

        Vector3 d = transform.position - lastPos; d.y = 0;
        lastPos = transform.position;
        float speed = d.magnitude / dt;
        move = Mathf.MoveTowards(move, Mathf.Clamp01(speed / fullSwingSpeed), dt * 5f);
        phase += speed / strideLength * Mathf.PI * 2f * dt;
        flinch = Mathf.MoveTowards(flinch, 0, dt * 5f);

        float s = Mathf.Sin(phase), c = Mathf.Cos(phase), idle = 1f - move;

        // Unity rotation signs for limbs hanging down: -X swings forward, +X swings back; knees bend with +X, elbows with -X.
        b.legL.localRotation = legLR * Quaternion.Euler(-s * legSwing * move, 0, 0);
        b.legR.localRotation = legRR * Quaternion.Euler(s * legSwing * move, 0, 0);
        b.kneeL.localRotation = knLR * Quaternion.Euler(2f + Mathf.Max(0, c) * 55f * move, 0, 0);
        b.kneeR.localRotation = knRR * Quaternion.Euler(2f + Mathf.Max(0, -c) * 55f * move, 0, 0);

        // body: bob with each step, twist with the stride, breathe and sway when standing
        b.hips.localPosition = hipsP + new Vector3(Mathf.Sin(t * 0.8f) * 0.01f * idle, Mathf.Abs(c) * 0.03f * move - 0.01f * move, 0);
        b.hips.localRotation = hipsR * Quaternion.Euler(0, s * 7f * move, Mathf.Sin(t * 0.8f) * 1.2f * idle);
        float breathe = Mathf.Sin(t * 1.7f);
        b.spine.localRotation = spineR * Quaternion.Euler(4f * move + breathe * 1f * idle - flinch * 18f, -s * 12f * move, 0);
        b.head.localRotation = headR * Quaternion.Euler(-2f * move - flinch * 10f + Mathf.Sin(t * 0.5f) * 2f * idle,
                                                       s * 8f * move + Mathf.Sin(t * 0.4f) * 6f * idle, 0);

        // arms swing opposite the legs, elbows bend more on the forward swing
        float swL = s * armSwing * move, swR = -s * armSwing * move;
        Quaternion shL = Quaternion.Euler(swL + Mathf.Sin(t * 1.1f) * 1.5f * idle, 0, 0), elL = Quaternion.Euler(-10f - Mathf.Max(0, -swL) * 0.8f, 0, 0);
        Quaternion shR = Quaternion.Euler(swR + Mathf.Sin(t * 1.1f + 1f) * 1.5f * idle, 0, 0), elR = Quaternion.Euler(-10f - Mathf.Max(0, -swR) * 0.8f, 0, 0);

        if (punchT >= 0)
        {
            punchT += dt;
            float p = punchT / punchDur;
            float twist;
            if (p < punchHit)
            {
                float k = Mathf.SmoothStep(0, 1, p / punchHit);   // wind up: fist back by the cheek, shoulder turned back
                shR = Quaternion.Euler(Mathf.Lerp(0, -40f, k), 0, 0); elR = Quaternion.Euler(Mathf.Lerp(-10f, -120f, k), 0, 0);
                twist = 18f * k;
            }
            else
            {
                float k = Mathf.InverseLerp(punchHit, 1f, p);       // jab straight out, then ease back
                float out1 = k < 0.35f ? k / 0.35f : 1f - (k - 0.35f) / 0.65f;
                shR = Quaternion.Euler(Mathf.Lerp(-40f, -88f, out1), 0, 0); elR = Quaternion.Euler(Mathf.Lerp(-120f, -5f, out1), 0, 0);
                twist = Mathf.Lerp(18f, -22f, out1);
            }
            shL = Quaternion.Euler(-45f, 0, 0); elL = Quaternion.Euler(-95f, 0, 0);   // guard up
            b.spine.localRotation *= Quaternion.Euler(0, twist, 0);
            if (p >= 1f) punchT = -1f;
        }

        b.shoulderL.localRotation = shLR * shL; b.elbowL.localRotation = elLR * elL;
        b.shoulderR.localRotation = shRR * shR; b.elbowR.localRotation = elRR * elR;
    }
}
