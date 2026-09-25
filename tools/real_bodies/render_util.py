import bpy, mathutils, math
def setup_render(res=(700,700)):
    sc=bpy.context.scene
    sc.render.engine='BLENDER_WORKBENCH'
    sc.display.shading.light='STUDIO'; sc.display.shading.color_type='MATERIAL'
    sc.display.shading.show_shadows=True; sc.display.shading.show_cavity=True
    sc.render.resolution_x,sc.render.resolution_y=res
    sc.render.film_transparent=False
    if not sc.world: sc.world=bpy.data.worlds.new('W')
def bounds(objs):
    pts=[o.matrix_world@mathutils.Vector(c) for o in objs for c in o.bound_box]
    mn=mathutils.Vector([min(p[i] for p in pts) for i in range(3)]); mx=mathutils.Vector([max(p[i] for p in pts) for i in range(3)])
    return mn,mx
def shoot(objs, path, azim=20, elev=8, res=(700,700), color_type='MATERIAL'):
    sc=bpy.context.scene; setup_render(res); sc.display.shading.color_type=color_type
    for o in sc.objects:
        if o.type=='MESH': o.hide_render = o not in objs
    mn,mx=bounds(objs); c=(mn+mx)/2; size=max((mx-mn).length,0.01)
    cam=bpy.data.objects.get('ShotCam')
    if not cam:
        cam=bpy.data.objects.new('ShotCam',bpy.data.cameras.new('ShotCam')); sc.collection.objects.link(cam)
    cam.data.type='ORTHO'; cam.data.ortho_scale=size*1.05
    a=math.radians(azim); e=math.radians(elev)
    d=mathutils.Vector((math.sin(a)*math.cos(e), -math.cos(a)*math.cos(e), math.sin(e)))
    cam.location=c+d*size*3; cam.data.clip_end=size*10
    cam.rotation_euler=(-d).to_track_quat('-Z','Y').to_euler()
    sc.camera=cam; sc.render.filepath=path
    bpy.ops.render.render(write_still=True)
