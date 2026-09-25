# Stage 1: import the CC body, fix its orientation (mesh data came in Y-up while the skeleton is Z-up),
# drop the extras, bake to metres, and save. The rest pose is CC's A-pose.
import bpy, sys, mathutils, math
sys.path.append(sys.argv[-1]); from render_util import *
W=sys.argv[-1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=W+'/Unity/unity.Fbx')
O=bpy.data.objects
arm=O['CC3_Base_Plus']
for o in O: o.animation_data_clear()                      # the FBX carries a T-pose clip that overrides the rest pose
for a in list(bpy.data.actions): bpy.data.actions.remove(a)
keep=['CC_Base_Body','CC_Game_Eye','CC_Game_Teeth']
for o in list(O):
    if o is arm or o.name in keep: continue
    bpy.data.objects.remove(o, do_unlink=True)
R=mathutils.Matrix.Rotation(math.radians(90),4,'X')
meshes=[O[n] for n in keep]
for m in meshes:
    mw=m.matrix_world.copy()
    m.parent=None; m.matrix_world=mathutils.Matrix.Identity(4)
    m.data.transform(R@mw, shape_keys=True)
    if m.data.shape_keys:                                   # keep only blink + mouth open (the rest block applying modifiers)
        keepk={'Basis','Eye_Blink_L','Eye_Blink_R','V_Open'} if m.name=='CC_Base_Body' else set()
        if not keepk: m.shape_key_clear()
        else:
            for k in list(m.data.shape_keys.key_blocks):
                if k.name not in keepk: m.shape_key_remove(k)
# armature: bake the 0.01 scale into the bones
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); bpy.context.view_layer.objects.active=arm
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for pb in arm.pose.bones: pb.matrix_basis=mathutils.Matrix.Identity(4)   # back to the rest (A) pose
for m in meshes:
    m.parent=arm; m.matrix_parent_inverse=mathutils.Matrix.Identity(4)
    for md in m.modifiers:
        if md.type=='ARMATURE': md.object=arm
    if not any(md.type=='ARMATURE' for md in m.modifiers):
        md=m.modifiers.new('Armature','ARMATURE'); md.object=arm
arm.name='Rig'
bpy.context.view_layer.update()
print('bones',len(arm.data.bones),'hip',tuple(round(x,3) for x in arm.data.bones['CC_Base_Hip'].head_local),'head',tuple(round(x,3) for x in arm.data.bones['CC_Base_Head'].head_local))
import numpy as np
dg=bpy.context.evaluated_depsgraph_get(); ev=O['CC_Base_Body'].evaluated_get(dg).to_mesh()
e=np.array([v.co[:] for v in ev.vertices]); d=np.array([v.co[:] for v in O['CC_Base_Body'].data.vertices])
print('arm scale',tuple(arm.scale),'rest deform error',float(np.abs(e-d).max()))
bpy.ops.wm.save_as_mainfile(filepath=W+'/stage1.blend')
shoot([O['CC_Base_Body']], W+'/s1_front.png', azim=0, elev=5)
shoot([O['CC_Base_Body']], W+'/s1_side.png', azim=90, elev=5)
# deformation test: a bent elbow and a raised knee
pb=arm.pose.bones
pb['CC_Base_L_Forearm'].rotation_mode='XYZ'; pb['CC_Base_L_Forearm'].rotation_euler=(0,0,math.radians(60))
pb['CC_Base_R_Thigh'].rotation_mode='XYZ'; pb['CC_Base_R_Thigh'].rotation_euler=(math.radians(50),0,0)
bpy.context.view_layer.update()
shoot([O['CC_Base_Body']], W+'/s1_posed.png', azim=30, elev=5)
