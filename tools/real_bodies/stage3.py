# Stage 3 (per character): fit the garments to the body, skin them, and test a walking pose.
import bpy, sys, mathutils, math, numpy as np
sys.path.append(sys.argv[-3]); from render_util import *; from cclib import *
W=sys.argv[-3]; who=sys.argv[-2]; mode=sys.argv[-1]
sys.path.append(W)
bpy.ops.wm.open_mainfile(filepath=W+'/stage2.blend')
with bpy.data.libraries.load(W+'/garments.blend') as (src, dst): dst.objects=[n for n in src.objects]
for o in dst.objects: bpy.context.scene.collection.objects.link(o)
O=bpy.data.objects; arm=O['Rig']; body=O['CC_Base_Body']
body_build(body, arm, CHAR[who]['build'])                                   # Kenny skinnier, Isaiah thicker (before the clothes fit)
bvh=bvh_of(body)
bone=lambda n: np.array(arm.data.bones[n].head_local)

# ---- choose + place ----
pants=O['Urban']; set_co(pants, mesh_co(pants)*0.96)
if who=='nathan' or mode=='urban':
    top=O['Urban.002']; set_co(top, mesh_co(top)*0.96)
    for n in ['Sweat','Sweat.001','Sweat.002','Sweat.003']: bpy.data.objects.remove(O[n])
    clean_cut(top, lambda f: True, (0,0,1.555), (0,0,1))                   # crew neck for both (no mock neck), like the photo
else:
    for n in ['Sweat','Sweat.003']: bpy.data.objects.remove(O[n])
    top=join([O['Sweat.001'],O['Sweat.002']],'Top')
    c=mesh_co(top); s=2.35
    ring=c[c[:,2]>c[:,2].max()-0.01].mean(0)
    target=np.array([0.0, 0.035, 1.56])                                    # back of a crew neck on this body
    c=(c-np.array([ring[0],ring[1],c[:,2].max()]))*s+target
    set_co(top,c)
for n in ['Urban.001']: bpy.data.objects.remove(O[n])                     # beanie: Nathan wears a cap
top.name='Top'; pants.name='Pants'
material(top,'Shirt',(0.55,0.55,0.56,1) if who=='cooper' else (0.1,0.1,0.1,1)); material(pants,'Pants',(0.06,0.06,0.07,1))

# ---- fit ----
print('fit pants'); push_out(pants, bvh, 0.006)
print('fit top');   push_out(top, bvh, 0.008)
# the top goes over the pants
print('top over pants'); push_out(top, bvh_of(pants), 0.004, passes=2)

# ---- sleeves round the arms ----
# The draped sleeves can hang off-centre (Nathan's sat in front of his forearm, leaving the back of it outside the
# cloth). Slice each sleeve every 2 cm along shoulder-elbow-wrist and move the slice so it's centred on the arm,
# easing in from 10 cm below the shoulder so the shoulder seam stays put; then fit it again.
def centre_sleeves(top):
    sleeve,_,_=panel_vertex_sets(top); c=mesh_co(top)
    for side,verts in sleeve.items():
        S_=bone(f'CC_Base_{side}_Upperarm'); E_=bone(f'CC_Base_{side}_Forearm'); H_=bone(f'CC_Base_{side}_Hand')
        idx=np.array(sorted(verts)); segs=((S_,E_,0.0),(E_,H_,float(np.linalg.norm(E_-S_))))
        Q=[]; A=[]
        for i in idx:
            best=None
            for a_,b_,off in segs:
                ab=b_-a_; t=float(np.clip(np.dot(c[i]-a_,ab)/np.dot(ab,ab),0,1)); q=a_+ab*t; d=float(np.linalg.norm(c[i]-q))
                if best is None or d<best[0]: best=(d,q,off+t*float(np.linalg.norm(ab)))
            Q.append(best[1]); A.append(best[2])
        Q=np.array(Q); A=np.array(A); R=c[idx]-Q
        bins=np.floor(A/0.02).astype(int); cent={b:R[bins==b].mean(0) for b in np.unique(bins)}
        ease=np.clip((A-0.1)/0.1,0,1); ease=ease*ease*(3-2*ease)
        c[idx]-=np.array([cent[b] for b in bins])*ease[:,None]
        print('  centred sleeve',side,'max shift',round(float(np.linalg.norm(np.array([cent[b] for b in bins])*ease[:,None],axis=1).max()),3))
    set_co(top,c)
centre_sleeves(top)
print('refit top'); push_out(top, bvh, 0.008)

# ---- skin ----
transfer_weights(body, pants, arm); transfer_weights(body, top, arm); sleeve,seam=panel_weights(top, body)
if CHAR[who]['sleeves']=='short':                                          # short sleeves: cut straight across above the elbow
    for side in list(sleeve.keys()):
        sleeve,_,seam=panel_vertex_sets(top); verts=sleeve[side]           # fresh indices (the other side's cut renumbers)
        S_=mathutils.Vector(bone(f'CC_Base_{side}_Upperarm')); E_=mathutils.Vector(bone(f'CC_Base_{side}_Forearm'))
        ax=(E_-S_).normalized(); t=(1.30-S_.z)/(E_.z-S_.z); P_=S_+(E_-S_)*t
        only=verts-seam; allowed=verts|seam
        def pick(f, only=only, allowed=allowed):
            ix={v.index for v in f.verts}; return ix<=allowed and bool(ix&only)
        clean_cut(top, pick, P_, ax)                                           # (a folded hem poked out in teeth when the arm moved)
# a straight collar line again after fitting (push_out moves the first cut about)
clean_cut(top, lambda f: any(v.co.z>1.54 for v in f.verts), (0,0,1.548), (0,0,1))
bpy.ops.wm.save_as_mainfile(filepath=W+f'/stage3_{who}_{mode}.blend')
material(body,'SkinPrev',(0.85,0.7,0.6,1))
shoot([body,top,pants], W+f'/s3_{who}_{mode}_f.png', azim=0, elev=3)
shoot([body,top,pants], W+f'/s3_{who}_{mode}_s.png', azim=90, elev=3)
# walking test pose
walk_pose(arm)
shoot([body,top,pants], W+f'/s3_{who}_{mode}_walk.png', azim=60, elev=5)
