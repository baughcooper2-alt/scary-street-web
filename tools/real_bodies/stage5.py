# Stage 5 (per character): first-person arms. Pose each forearm forward with the hand in a relaxed fist, cut the
# forearm + hand (and sleeve / wristband) out of the posed meshes, and centre them on the fist.
import bpy, sys, mathutils, math, numpy as np, bmesh
W=sys.argv[-2]; who=sys.argv[-1]; sys.path.append(W)
from render_util import *; from cclib import *
bpy.ops.wm.open_mainfile(filepath=W+f'/stage4_{who}.blend')
O=bpy.data.objects; arm=O['Rig']; body=O['CC_Base_Body']
for k in (body.data.shape_keys.key_blocks if body.data.shape_keys else []): k.value=0
pb=arm.pose.bones
def head(n): bpy.context.view_layer.update(); return pb[n].head.copy()
def rotate(n, axis, deg):
    bpy.context.view_layer.update()
    h=pb[n].head.copy()
    R=mathutils.Matrix.Rotation(math.radians(deg),4,axis)
    pb[n].matrix=mathutils.Matrix.Translation(h)@R@mathutils.Matrix.Translation(-h)@pb[n].matrix
    bpy.context.view_layer.update()
def tip(n): bpy.context.view_layer.update(); return pb[n].tail.copy()
made=[]
for side,sx in (('R',-1),('L',1)):
    P=lambda n: f'CC_Base_{side}_{n}'
    fwd=mathutils.Vector((-sx*0.12,-0.93,0.34)).normalized()
    aim_bone(arm,P('Upperarm'),fwd); aim_bone(arm,P('Forearm'),fwd); aim_bone(arm,P('Hand'),fwd)   # straight arm: no elbow squash
    palm=mathutils.Vector((-sx,0,0))                                   # palm faces the middle, thumb up
    K=(head(P('Pinky1'))-head(P('Index1'))).normalized()
    # which way curls toward the palm (decided once, on the first knuckle)
    t0=tip(P('Mid3')); rotate(P('Mid1'),K,30); sgn=1 if (tip(P('Mid3'))-t0).dot(palm)>0 else -1; rotate(P('Mid1'),K,-30)
    for f in ('Index','Mid','Ring','Pinky'):
        for j,ang in zip((1,2,3),(80,95,65)): rotate(P(f'{f}{j}'),K,sgn*ang)
    t0=tip(P('Thumb3')); rotate(P('Thumb1'),fwd,15); tsg=1 if (tip(P('Thumb3'))-t0).dot(palm)>0 else -1; rotate(P('Thumb1'),fwd,-15)
    for j,ang in zip((1,2,3),(25,25,35)): rotate(P(f'Thumb{j}'),fwd,tsg*ang)
    fist=sum((head(P(f'{f}2')) for f in ('Index','Mid','Ring','Pinky')),mathutils.Vector())/4
    fist=(fist*2+head(P('Mid1')))/3
    wrist=head(P('Hand'))
    pieces=[]
    for src in [body]+[o for o in (O.get('Top'),O.get('Wristband')) if o]:
        Wt,names=weights(src)
        fam=[i for i,nm in enumerate(names) if nm.startswith(f'CC_Base_{side}_') and any(k in nm for k in ('Forearm','Hand','Thumb','Index','Mid','Ring','Pinky','Elbow'))]
        armw=Wt[:,fam].sum(1) if fam else np.zeros(len(src.data.vertices))
        dg=bpy.context.evaluated_depsgraph_get(); ev=src.evaluated_get(dg)
        me=bpy.data.meshes.new_from_object(ev); bm=bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
        sleeved=O.get('Top') is not None and who=='nathan'
        back=0.035 if (src is body and sleeved) else 0.2                 # under Nathan's sleeve only the hand shows
        keep=np.array([armw[v.index]>0.5 and (v.co-wrist).dot(-fwd)<back for v in bm.verts])
        bmesh.ops.delete(bm, geom=[f_ for f_ in bm.faces if not all(keep[v.index] for v in f_.verts)], context='FACES')
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
        if not bm.faces: bm.free(); continue
        _d=np.array([(v.co-wrist).dot(-fwd) for v in bm.verts]); _r=np.array([((v.co-wrist)-(-fwd)*((v.co-wrist).dot(-fwd))).length for v in bm.verts])
        print('  piece',src.name,'verts',len(bm.verts),'back range',_d.min().round(3),_d.max().round(3),'radius mean',_r.mean().round(3),'max',_r.max().round(3))
        if src.name=='Top':
            # centre the loose sleeve on the forearm, snug it in, and bring the cuff up to the wrist
            ax=-fwd; T=np.array([(v.co-wrist).dot(ax) for v in bm.verts])
            Rv=[(v.co-wrist)-ax*t for v,t in zip(bm.verts,T)]
            bins=np.floor(T/0.01).astype(int); cent={}
            for b in set(bins): cent[b]=sum((Rv[i] for i in np.nonzero(bins==b)[0]),mathutils.Vector())/int((bins==b).sum())
            shift=0.014-T.min()
            for i,v in enumerate(bm.verts):
                r=Rv[i]-cent[bins[i]]; L=r.length
                if L>1e-5:
                    L2=0.042+(L-0.042)*0.45 if L>0.042 else max(L,0.038); r=r*(L2/L)
                v.co=wrist+ax*(T[i]-shift*0)+r - ax*shift
        bmesh.ops.translate(bm, vec=-fist, verts=bm.verts)
        bm.to_mesh(me); bm.free()
        o=bpy.data.objects.new(f'FP_{side}_{src.name}',me); bpy.context.scene.collection.objects.link(o)
        if o.data.shape_keys: o.shape_key_clear()
        pieces.append(o)
    fp=join(pieces,f'FP_{side}') if len(pieces)>1 else pieces[0]; fp.name=f'FP_{side}'
    fp.vertex_groups.clear(); fp.modifiers.clear(); fp.parent=None
    made.append(fp)
    print('FP',side,'verts',len(fp.data.vertices),'mats',[m.name for m in fp.data.materials])
for p in pb: p.matrix_basis=mathutils.Matrix.Identity(4)
bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=W+f'/stage5_{who}.blend')
# preview: both fists side by side, seen from where your eyes would be
made[0].location=(-0.24,0,0); made[1].location=(0.24,0,0)
for o in made:
    for i,m in enumerate(o.data.materials):
        c={'Std_Skin_Arm':(0.85,0.68,0.58,1),'Std_Nails':(0.9,0.75,0.7,1),'Shirt':(0.1,0.1,0.1,1),'Wristband':(0.95,0.95,0.95,1)}.get(m.name if m else '', (0.5,0.5,0.5,1))
        if m: m.diffuse_color=c
shoot(made, W+f'/s5_{who}_fp.png', azim=180, elev=-15)
shoot(made[:1], W+f'/s5_{who}_fp_r34.png', azim=140, elev=20)
shoot(made, W+f'/s5_{who}_fp_side.png', azim=90, elev=0)
