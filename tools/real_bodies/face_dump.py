# Re-dump a built character's face UVs (face_<who>.json) from WORK/stage4_<who>.blend without rebuilding:
#   Blender -b -P face_dump.py -- WORK who
import bpy, sys, numpy as np
W=sys.argv[-2]; who=sys.argv[-1]; sys.path.append(W)
from cclib import *
bpy.ops.wm.open_mainfile(filepath=W+f'/stage4_{who}.blend')
O=bpy.data.objects; arm=O['Rig']; body=O['CC_Base_Body']
for pb in arm.pose.bones: pb.matrix_basis=mathutils.Matrix.Identity(4)
bpy.context.view_layer.update()
_e=mesh_co(O['CC_Game_Eye']); _t=mesh_co(O['CC_Game_Teeth'])
eyeZ=float(_e[:,2].mean()); mouthZ=float(_t[:,2].mean())
Bw,bn=weights(body); bco=mesh_co(body)
_hw=sum(Bw[:,i] for i,n in enumerate(bn) if n in ('CC_Base_Head','CC_Base_JawRoot','CC_Base_FacialBone') or 'Jaw' in n)
_front=bco[(np.abs(bco[:,0])<0.015)&(bco[:,1]<-0.02)&(_hw>0.5)&(bco[:,2]<mouthZ)]
dump_face(body, W, who, eyeZ, mouthZ, float(_front[:,2].min()))
