# Stage 2: arms down (our animation rest pose), shoulders dropped a touch, then a slimmer, less bodybuilder build.
import bpy, sys, mathutils, math, numpy as np
sys.path.append(sys.argv[-1]); from render_util import *; from cclib import *
W=sys.argv[-1]
bpy.ops.wm.open_mainfile(filepath=W+'/stage1.blend')
O=bpy.data.objects; arm=O['Rig']; body=O['CC_Base_Body']; meshes=[O['CC_Base_Body'],O['CC_Game_Eye'],O['CC_Game_Teeth']]
out=math.radians(7)
for side,sx in (('L',1),('R',-1)):
    pb=arm.pose.bones[f'CC_Base_{side}_Clavicle']; pb.rotation_mode='XYZ'
    d=(pb.tail-pb.head).normalized(); aim_bone(arm,f'CC_Base_{side}_Clavicle',(d.x,d.y,d.z-0.09))
    aim_bone(arm,f'CC_Base_{side}_Upperarm',(sx*math.sin(out),0.02,-math.cos(out)))
    aim_bone(arm,f'CC_Base_{side}_Forearm',(sx*math.sin(out)*0.8,-0.08,-1))      # a touch of natural bend
    aim_bone(arm,f'CC_Base_{side}_Hand',(sx*math.sin(out)*0.5,-0.02,-1))
bake_pose_as_rest(arm, meshes)

# ---- slimmer build: pull flesh toward each bone's axis, by skin weight ----
slim={'Upperarm':0.2,'UpperarmTwist01':0.2,'UpperarmTwist02':0.2,'Forearm':0.08,'ForearmTwist01':0.08,'ForearmTwist02':0.08,
      'Thigh':0.12,'ThighTwist01':0.12,'ThighTwist02':0.12,'Calf':0.06,'CalfTwist01':0.06,'CalfTwist02':0.06,
      'Clavicle':0.18,'RibsTwist':0.15,'Spine02':0.14,'Spine01':0.1,'Waist':0.05,'NeckTwist01':0.12,'NeckTwist02':0.06}
co=mesh_co(body); Wt,names=weights(body)
disp=np.zeros_like(co)
for gi,n in enumerate(names):
    key=n.replace('CC_Base_','').replace('L_','').replace('R_','')
    k=slim.get(key)
    if not k or n not in arm.data.bones: continue
    b=arm.data.bones[n]; h=np.array(b.head_local); t=np.array(b.tail_local); seg=t-h
    tt=np.clip(((co-h)@seg)/max(seg@seg,1e-9),0,1); near=h+tt[:,None]*seg
    disp+=Wt[:,gi:gi+1]*k*(near-co)
# chest: flatten the pecs backwards (pulling them toward the breast bone made them conical)
for n in ('CC_Base_L_Breast','CC_Base_R_Breast'):
    if n in names: disp[:,1]+=Wt[:,names.index(n)]*0.022
co=co+disp
# soften the muscle definition on the torso and arms (not the head, hands or feet)
def gw(n): return Wt[:,names.index(n)] if n in names else 0
mask=np.clip(sum(gw('CC_Base_'+n) for n in ['Spine01','Spine02','Waist','L_Breast','R_Breast','L_RibsTwist','R_RibsTwist','L_Clavicle','R_Clavicle',
     'L_Upperarm','R_Upperarm','L_UpperarmTwist01','R_UpperarmTwist01','L_UpperarmTwist02','R_UpperarmTwist02','L_Thigh','R_Thigh','L_ThighTwist01','R_ThighTwist01']),0,1)*0.8
co=smooth(co, adjacency(body), mask, iters=8)
# shape keys must move with the base
kb=body.data.shape_keys.key_blocks; base_old=np.array([v.co[:] for v in kb[0].data])
for k in kb:
    a=np.array([v.co[:] for v in k.data]); k.data.foreach_set('co',(co+(a-base_old)).reshape(-1))
set_co(body,co)
bpy.ops.wm.save_as_mainfile(filepath=W+'/stage2.blend')
shoot([body], W+'/s2_front.png', azim=0, elev=3)
shoot([body], W+'/s2_side.png', azim=90, elev=3)
shoot([body], W+'/s2_34.png', azim=35, elev=10)
