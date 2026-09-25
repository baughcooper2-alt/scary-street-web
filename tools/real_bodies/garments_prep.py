# Import the garments, bring them to metres / Z-up, decimate the dense one, split by material, save.
import bpy, sys, mathutils, math, bmesh
W=sys.argv[-1]
bpy.ops.wm.read_factory_settings(use_empty=True)
def imp(path):
    before=set(bpy.data.objects); bpy.ops.wm.obj_import(filepath=path)
    return [o for o in bpy.data.objects if o not in before][0]
def sel(o):
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active=o
u=imp(W+'/UrbanOutfit/Export OBJ/Beanie_Outfit_V01.obj'); u.name='Urban'
u.scale=(0.001,)*3; sel(u); bpy.ops.object.transform_apply(scale=True, rotation=True, location=True)
s=imp(W+'/obj_0.obj'); s.name='Sweat'
sel(s); bpy.ops.object.transform_apply(scale=True, rotation=True, location=True)
m=s.modifiers.new('dec','DECIMATE'); m.ratio=0.03
bpy.ops.object.modifier_apply(modifier='dec')
# split by material
for o in (u,s):
    sel(o); bpy.ops.mesh.separate(type='MATERIAL')
for o in bpy.data.objects:
    if o.type=='MESH':
        import numpy as np
        a=np.array([v.co[:] for v in o.data.vertices])
        print('PART',o.name,[mm.name for mm in o.data.materials],len(a),'min',a.min(0).round(3),'max',a.max(0).round(3), 'uv',len(o.data.uv_layers))
bpy.ops.wm.save_as_mainfile(filepath=W+'/garments.blend')
