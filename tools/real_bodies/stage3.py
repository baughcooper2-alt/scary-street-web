# Stage 3 (per character): fit the garments to the body, skin them, and test a walking pose.
import bpy, sys, mathutils, math, numpy as np
sys.path.append(sys.argv[-3]); from render_util import *; from cclib import *
W=sys.argv[-3]; who=sys.argv[-2]; mode=sys.argv[-1]
sys.path.append(W)
bpy.ops.wm.open_mainfile(filepath=W+'/stage2.blend')
with bpy.data.libraries.load(W+'/garments.blend') as (src, dst): dst.objects=[n for n in src.objects]
for o in dst.objects: bpy.context.scene.collection.objects.link(o)
O=bpy.data.objects; arm=O['Rig']; body=O['CC_Base_Body']
bvh=bvh_of(body)
bone=lambda n: np.array(arm.data.bones[n].head_local)

# ---- choose + place ----
pants=O['Urban']; set_co(pants, mesh_co(pants)*0.96)
if who=='nathan' or mode=='urban':
    top=O['Urban.002']; set_co(top, mesh_co(top)*0.96)
    for n in ['Sweat','Sweat.001','Sweat.002','Sweat.003']: bpy.data.objects.remove(O[n])
    c=mesh_co(top); delete_verts(top, c[:,2]>1.555)                       # crew neck for both (no mock neck), like the photo
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
material(top,'Shirt',(0.1,0.1,0.1,1) if who=='nathan' else (0.55,0.55,0.56,1)); material(pants,'Pants',(0.06,0.06,0.07,1))

# ---- fit ----
print('fit pants'); push_out(pants, bvh, 0.006)
print('fit top');   push_out(top, bvh, 0.008)
# the top goes over the pants
print('top over pants'); push_out(top, bvh_of(pants), 0.004, passes=2)

# ---- skin ----
transfer_weights(body, pants, arm); transfer_weights(body, top, arm); sleeve,seam=panel_weights(top, body)
if who=='cooper':                                                          # short sleeves: cut the sleeve panels above the elbow
    c=mesh_co(top); cut=np.zeros(len(c),bool)
    for side,verts in sleeve.items():
        for v in verts-seam:
            if c[v,2]<1.30: cut[v]=True
    delete_verts(top, cut); print('cut sleeve verts',int(cut.sum()))
bpy.ops.wm.save_as_mainfile(filepath=W+f'/stage3_{who}_{mode}.blend')
material(body,'SkinPrev',(0.85,0.7,0.6,1))
shoot([body,top,pants], W+f'/s3_{who}_{mode}_f.png', azim=0, elev=3)
shoot([body,top,pants], W+f'/s3_{who}_{mode}_s.png', azim=90, elev=3)
# walking test pose
walk_pose(arm)
shoot([body,top,pants], W+f'/s3_{who}_{mode}_walk.png', azim=60, elev=5)
