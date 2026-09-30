# Bake Mixamo clips into Scary Street's mocap format (SSAN v1) for CharacterAnimator.
#   /Applications/Blender.app/Contents/MacOS/Blender -b -P tools/anim/bake_mixamo.py -- tools/anim/mixamo ScaryStreetUnity/Assets/Resources/Anim
# Every <Name>.fbx in the source folder (a Mixamo "Without Skin" download, 30 fps) becomes <Name>.bytes.
#
# What's stored is rig-independent: for each of 16 joints, per frame, the joint's rotation in character space
# relative to Mixamo's own rest pose (a T-pose), in Unity coordinates (Blender (x, y, z) -> Unity (-x, z, -y),
# front +Z). CharacterAnimator turns our arms-down rest into that T-pose with a per-joint offset (from the bone
# directions stored here), then applies the delta, so the same clip drives every body type.
#
# Layout: "SSAN" int32 version | fps, frame count, joint count | stride (m per loop, 0 = in place), phase0 (0..1,
#   left heel strike), impact (0..1, a punch's furthest reach), impact side (-1 left hand, +1 right, 0 none),
#   leg length (rest hips height above the ankles, m) | rest joint positions (xyz per joint, m) |
#   per frame: hips offset from rest (xyz, m, walking drift removed), then a quaternion (xyzw) per joint.
import bpy, sys, os, struct
from mathutils import Matrix

args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
SRC = args[0] if len(args) > 0 else 'tools/anim/mixamo'
OUT = args[1] if len(args) > 1 else 'ScaryStreetUnity/Assets/Resources/Anim'

# order matters: CharacterAnimator's MocapJoint enum
JOINTS = ['Hips', 'Spine2', 'Neck', 'Head',
          'LeftArm', 'LeftForeArm', 'LeftHand', 'RightArm', 'RightForeArm', 'RightHand',
          'LeftUpLeg', 'LeftLeg', 'LeftFoot', 'RightUpLeg', 'RightLeg', 'RightFoot']
C = Matrix(((-1, 0, 0), (0, 0, 1), (0, -1, 0)))            # Blender -> Unity basis (a mirror)
def U(v): return (-v[0], v[2], -v[1])


def bake(path, out):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    sc = bpy.context.scene
    fs, fe = (int(round(x)) for x in arm.animation_data.action.frame_range)
    fps = sc.render.fps / sc.render.fps_base
    Mw = arm.matrix_world
    pre = next(b.name for b in arm.data.bones if b.name.endswith('Hips'))[:-4]   # "mixamorig:" (or another prefix)
    bones = [arm.data.bones[pre + j] for j in JOINTS]
    pbones = [arm.pose.bones[pre + j] for j in JOINTS]
    rest = [(Mw @ b.matrix_local).to_3x3().normalized() for b in bones]
    restInv = [r.inverted() for r in rest]
    restPos = [U(Mw @ b.head_local) for b in bones]

    frames = []
    for f in range(fs, fe + 1):
        sc.frame_set(f)
        hips = U(Mw @ pbones[0].head)
        q = []
        for i, pb in enumerate(pbones):
            d = (Mw @ pb.matrix).to_3x3().normalized() @ restInv[i]
            q.append((C @ d @ C.transposed()).to_quaternion())
        feet = [U(Mw @ pbones[12].head), U(Mw @ pbones[15].head)]
        hands = [U(Mw @ pbones[6].head), U(Mw @ pbones[9].head)]
        frames.append((hips, q, feet, hands))

    n = len(frames) - 1                                         # the last frame repeats the first (+ the distance walked)
    drift = [frames[n][0][k] - frames[0][0][k] for k in range(3)]
    stride = (drift[0] ** 2 + drift[2] ** 2) ** 0.5
    if stride < 0.2: drift, stride = [0, 0, 0], 0.0             # in place

    # left heel strike = the left foot furthest ahead of the hips
    phase0 = max(range(n), key=lambda i: frames[i][2][0][2] - frames[i][0][2]) / n
    # a punch lands when a hand is furthest ahead of the hips
    reach = [(frames[i][3][s][2] - frames[i][0][2], i, s) for i in range(n) for s in (0, 1)]
    best = max(reach)
    base = min(frames[0][3][s][2] - frames[0][0][2] for s in (0, 1))
    impact, side = (best[1] / n, -1 if best[2] == 0 else 1) if best[0] - base > 0.25 and stride == 0 else (0.0, 0)
    leg = restPos[0][1] - 0.5 * (restPos[12][1] + restPos[15][1])

    with open(out, 'wb') as f:
        f.write(b'SSAN'); f.write(struct.pack('<i', 1))
        f.write(struct.pack('<fii', fps, n, len(JOINTS)))
        f.write(struct.pack('<fffif', stride, phase0, impact, side, leg))
        for p in restPos: f.write(struct.pack('<3f', *p))
        prev = None
        for i in range(n):
            hips, q, _, _ = frames[i]
            off = [hips[k] - restPos[0][k] - drift[k] * i / n for k in range(3)]
            f.write(struct.pack('<3f', *off))
            for j, r in enumerate(q):
                if prev and r.dot(prev[j]) < 0: r.negate()        # keep neighbouring frames in the same hemisphere
                f.write(struct.pack('<4f', r.x, r.y, r.z, r.w))
            prev = q
    print(f'BAKED {os.path.basename(out)}: {n} frames @ {fps:g} fps ({n / fps:.2f} s), stride {stride:.3f} m '
          f'({stride * fps / n:.2f} m/s), phase0 {phase0:.2f}, impact {impact:.2f} side {side}, leg {leg:.3f} m')


os.makedirs(OUT, exist_ok=True)
for name in sorted(os.listdir(SRC)):
    if name.lower().endswith('.fbx'):
        bake(os.path.join(SRC, name), os.path.join(OUT, name[:-4] + '.bytes'))
