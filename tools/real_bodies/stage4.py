# Stage 4 (per character): hair, cap, shoes; hide the skin under the clothes.
import bpy, sys, mathutils, math, numpy as np, bmesh
W=sys.argv[-2]; who=sys.argv[-1]; sys.path.append(W)
from render_util import *; from cclib import *
bpy.ops.wm.open_mainfile(filepath=W+f'/stage3_{who}_urban.blend')
O=bpy.data.objects; arm=O['Rig']; body=O['CC_Base_Body']; top=O['Top']; pants=O['Pants']
for pb in arm.pose.bones: pb.matrix_basis=mathutils.Matrix.Identity(4)
bpy.context.view_layer.update()
# ---- face: tell Cooper and Nathan apart ----
_e=mesh_co(O['CC_Game_Eye']); _t=mesh_co(O['CC_Game_Teeth'])
eyeZ=float(_e[:,2].mean()); mouthZ=float(_t[:,2].mean())
Bw,bn=weights(body); bco=mesh_co(body)
_front=bco[(np.abs(bco[:,0])<0.015)&(bco[:,1]<-0.02)&(Bw[:,bn.index('CC_Base_Head')]>0.5)]
chinZ=float(_front[:,2].min())
shape_face(body, who, _e, eyeZ, mouthZ, chinZ, 0.009)
bco=mesh_co(body)
# brow area in the head texture (for the per-character eyebrows)
import json
uvl=body.data.uv_layers.active.data; uvs=[]
for poly in body.data.polygons:
    if body.data.materials[poly.material_index].name!='Std_Skin_Head': continue
    for li in poly.loop_indices:
        v=bco[body.data.loops[li].vertex_index]
        if eyeZ+0.012<v[2]<eyeZ+0.036 and 0.008<abs(v[0])<0.058 and v[1]<-0.05: uvs.append(tuple(uvl[li].uv))
uvs=np.array(uvs); json.dump({'min':uvs.min(0).tolist(),'max':uvs.max(0).tolist()},open(W+f'/brows_{who}.json','w'))
print('brow uv box',uvs.min(0).round(3),uvs.max(0).round(3))
headw=Bw[:,bn.index('CC_Base_Head')]
hv=bco[(headw>0.9)&(bco[:,2]>1.6)]
ctop=hv[:,2].max()
def slab(pts,z0,z1):
    s=pts[(pts[:,2]>z0)&(pts[:,2]<z1)]; return s[:,0].max()-s[:,0].min(), s[:,1].max()-s[:,1].min(), np.array([(s[:,0].max()+s[:,0].min())/2,(s[:,1].max()+s[:,1].min())/2])
cw,cd,cc=slab(hv,ctop-0.09,ctop-0.07)
print('CC head top',round(ctop,3),'width',round(cw,3),'depth',round(cd,3),'centre',cc.round(3))
head_bvh=bvh_of(body)

def imp(path):
    before=set(bpy.data.objects); bpy.ops.wm.obj_import(filepath=path); new=[o for o in bpy.data.objects if o not in before]
    for o in new: sel(o); bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)   # the importer's axis fix is an object rotation
    return new
def rigid_to_head(o):
    for g in list(o.vertex_groups): o.vertex_groups.remove(g)
    g=o.vertex_groups.new(name='CC_Base_Head'); g.add(list(range(len(o.data.vertices))),1.0,'REPLACE')
    o.parent=arm; o.matrix_parent_inverse=mathutils.Matrix.Identity(4)
    m=o.modifiers.new('Armature','ARMATURE'); m.object=arm

if who=='cooper':
    objs=imp(W+'/DummyHair.obj'); dummy=[o for o in objs if o.name.startswith('Dummy')][0]; hair=[o for o in objs if o.name.startswith('Hair')][0]
    dco=mesh_co(dummy); dtop=dco[:,2].max()
    dw,dd,dc=slab(dco,dtop-0.09,dtop-0.07)
    s=(cw/dw+cd/dd)/2
    print('dummy head width',round(dw,3),'depth',round(dd,3),'scale',round(s,3))
    h=mesh_co(hair); h=(h-np.array([dc[0],dc[1],dtop]))*s+np.array([cc[0],cc[1],ctop]); set_co(hair,h)
    bpy.data.objects.remove(dummy)
    push_out(hair, head_bvh, 0.003, passes=3, smooth_iters=6)
else:
    objs=imp(W+'/curly.obj'); hair=objs[0]
    h=mesh_co(hair); h[:,0]*=-1                                     # MakeHuman is mirrored
    ext=h.max(0)-h.min(0); sx=cw*1.28/ext[0]; sy=cd*1.27/ext[1]
    ctr=(h.max(0)+h.min(0))/2
    h=np.column_stack([(h[:,0]-ctr[0])*sx+cc[0], (h[:,1]-ctr[1])*sy+cc[1]+0.004, (h[:,2]-h[:,2].max())*sx+ctop+0.012])
    cut=ctop-0.10; low=h[:,2]<cut; h[low,2]=cut+(h[low,2]-cut)*0.42            # long strands pulled up into short curls
    set_co(hair,h)
    # the mesh was mirrored: flip the winding back
    sel(hair); bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.flip_normals(); bpy.ops.object.mode_set(mode='OBJECT')
    push_out(hair, head_bvh, 0.004, passes=3, smooth_iters=4)
    # ---- cap: a dome over the skull and a curved brim ----
    rimF,rimB=ctop-0.058,ctop-0.088                                   # band on the forehead, lower at the back
    yF,yB=cc[1]-cd/2,cc[1]+cd/2
    def rimz(y): return rimF+(rimB-rimF)*min(1,max(0,(y-yF)/(yB-yF)))
    rim=rimF
    bm=bmesh.new(); bmesh.ops.create_uvsphere(bm,u_segments=40,v_segments=20,radius=1.0)
    for v in bm.verts:
        y=v.co.y*(cd/2+0.016)+cc[1]; z0=rimz(y)
        v.co=mathutils.Vector((v.co.x*(cw/2+0.016)+cc[0], y, z0+v.co.z*(ctop+0.02-z0)))
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z<rimz(v.co.y)-0.001 and v.co.z<ctop-0.05], context='VERTS')
    capm=bpy.data.meshes.new('Cap'); bm.to_mesh(capm); bm.free()
    cap=bpy.data.objects.new('Cap',capm); bpy.context.scene.collection.objects.link(cap)
    # brim: half an ellipse of quads sticking out from the front of the band, curving down at the sides
    bm=bmesh.new(); front=cc[1]-cd/2-0.016
    N,M=24,6; grid=[]
    for i in range(N+1):
        ang=math.pi*i/N                                                  # 0..pi across the front
        row=[]
        for j in range(M+1):
            r=j/M
            ex=math.cos(ang)*(cw/2+0.016)*(1+0.18*r); ey=-math.sin(ang)*(0.012+0.072*r)
            z=rimF+0.004-0.018*r*r-0.01*(1-math.sin(ang))
            row.append(bm.verts.new((cc[0]+ex, front+0.012+ey, z)))
        grid.append(row)
    for i in range(N):
        for j in range(M): bm.faces.new((grid[i][j],grid[i+1][j],grid[i+1][j+1],grid[i][j+1]))
    brm=bpy.data.meshes.new('Brim'); bm.to_mesh(brm); bm.free()
    brim=bpy.data.objects.new('Brim',brm); bpy.context.scene.collection.objects.link(brim)
    for o in (cap,brim):
        sd=o.modifiers.new('s','SOLIDIFY'); sd.thickness=0.004 if o is brim else 0.003; sel(o); bpy.ops.object.modifier_apply(modifier='s')
    cap=join([cap,brim],'Cap')
    sel(cap); bpy.ops.object.shade_smooth()
    material(cap,'Cap',(0.2,0.12,0.07,1)); rigid_to_head(cap)
    # curls only show below the cap
    hc=mesh_co(hair)
    under=np.array([z>rimz(y)+0.004 for y,z in zip(hc[:,1],hc[:,2])])
    face=(hc[:,1]<-0.028)&(hc[:,2]<eyeZ+0.034)                         # curls over the forehead, none over the eyes
    delete_verts(hair, under|face)
if who=='cooper':                                                            # white wristband on the left wrist
    wrist=arm.data.bones['CC_Base_L_Hand'].head_local; fore=arm.data.bones['CC_Base_L_Forearm'].head_local
    axis=(wrist-fore).normalized()
    bm=bmesh.new(); bmesh.ops.create_cone(bm,cap_ends=False,segments=24,radius1=0.036,radius2=0.034,depth=0.045)
    rot=axis.to_track_quat('Z','Y').to_matrix().to_4x4()
    bmesh.ops.transform(bm,matrix=mathutils.Matrix.Translation(wrist-axis*0.035)@rot,verts=bm.verts)
    wm=bpy.data.meshes.new('Wristband'); bm.to_mesh(wm); bm.free()
    band=bpy.data.objects.new('Wristband',wm); bpy.context.scene.collection.objects.link(band)
    sd=band.modifiers.new('s','SOLIDIFY'); sd.thickness=0.005; sel(band); bpy.ops.object.modifier_apply(modifier='s'); bpy.ops.object.shade_smooth()
    push_out(band, head_bvh, 0.003, passes=2, smooth_iters=2)
    for g in list(band.vertex_groups): band.vertex_groups.remove(g)
    g=band.vertex_groups.new(name='CC_Base_L_ForearmTwist02'); g.add(list(range(len(band.data.vertices))),1.0,'REPLACE')
    band.parent=arm; md=band.modifiers.new('Armature','ARMATURE'); md.object=arm
    material(band,'Wristband',(0.95,0.95,0.95,1))
if who=='nathan':                                                            # extra 3D curls on the sides, back and forehead
    hw_=Bw[:,bn.index('CC_Base_Head')]
    nrm=np.empty(len(bco)*3); body.data.vertices.foreach_get('normal',nrm); nrm=nrm.reshape(-1,3)
    cand=[]
    for i,(v,nv) in enumerate(zip(bco,nrm)):
        if hw_[i]<0.6: continue
        side=v[1]>-0.045 and eyeZ-0.012<v[2]<rimz(v[1])-0.002
        fore=v[1]<-0.05 and abs(v[0])<0.062 and eyeZ+0.042<v[2]<rimF-0.002
        if (side and not (abs(v[0])<0.03 and v[1]<0)) or fore: cand.append((i,fore))
    import random; rnd=random.Random(11); rnd.shuffle(cand)
    picked=[]
    for i,fore in cand:
        if all(np.linalg.norm(bco[i]-bco[j])>(0.009 if fore else 0.011) for j,_ in picked): picked.append((i,fore))
        if len(picked)>=150: break
    cu=coils([bco[i] for i,_ in picked],[nrm[i] for i,_ in picked],forehead=[f for _,f in picked])
    material(cu,'Curls',(0.05,0.04,0.035,1)); rigid_to_head(cu); sel(cu); bpy.ops.object.shade_smooth()
    print('curls',len(picked),'verts',len(cu.data.vertices))
hair.name='Hair'; material(hair,'Hair',(0.45,0.3,0.18,1) if who=='cooper' else (0.07,0.05,0.04,1)); rigid_to_head(hair)
sel(hair); bpy.ops.object.shade_smooth()

# ---- shoes: the feet, puffed out and smoothed into sneakers ----
footw=sum(Bw[:,i] for i,n in enumerate(bn) if 'Foot' in n or 'Toe' in n)
infoot=(footw>0.5)&(bco[:,2]<0.12)
bm=bmesh.new(); bm.from_mesh(body.data); bm.verts.ensure_lookup_table()
keepf=[f for f in bm.faces if all(infoot[v.index] for v in f.verts)]
bmesh.ops.delete(bm, geom=[f for f in bm.faces if f not in set(keepf)], context='FACES')
shm=bpy.data.meshes.new('Shoes'); bm.to_mesh(shm); bm.free()
shoes=bpy.data.objects.new('Shoes',shm); bpy.context.scene.collection.objects.link(shoes)
for g in body.vertex_groups: shoes.vertex_groups.new(name=g.name)
# carry the weights over (same vertex order as the kept body verts)
sb=bmesh.new(); sb.from_mesh(body.data)
shoes.data.update()
sco=mesh_co(shoes)
from mathutils.kdtree import KDTree
kt=KDTree(len(bco)); [kt.insert(v,i) for i,v in enumerate(bco)]; kt.balance()
for i,v in enumerate(sco):
    _,j,_=kt.find(v); copy_weights_from(shoes,i,Bw,bn,j)
sb.free()
shoes.shape_key_clear() if shoes.data.shape_keys else None
edges=adjacency(shoes); sco=smooth(sco,edges,np.ones(len(sco)),iters=10)
# puff along smoothed normals, flatten the sole
shoes.data.vertices.foreach_set('co',sco.reshape(-1)); shoes.data.update()
nor=np.empty(len(sco)*3); shoes.data.vertices.foreach_get('normal',nor); nor=nor.reshape(-1,3)
sco=sco+nor*0.009
sole=sco[:,2]<0.02; sco[sole,2]=np.minimum(sco[sole,2],0.0)-0.002
set_co(shoes,sco)
shoes.parent=arm; md=shoes.modifiers.new('Armature','ARMATURE'); md.object=arm
sel(shoes); bpy.ops.object.shade_smooth()
material(shoes,'Shoes',(0.92,0.92,0.92,1) if who=='cooper' else (0.1,0.1,0.1,1))

# ---- hide the skin under the clothes (it would poke through when animating) ----
cover=[bvh_of(o) for o in (top,pants,shoes)]
nor=np.empty(len(bco)*3); body.data.vertices.foreach_get('normal',nor); nor=nor.reshape(-1,3)
bound=[]
for o in (top,pants):
    bmx=bmesh.new(); bmx.from_mesh(o.data)
    bound+= [tuple(v.co) for v in bmx.verts if v.is_boundary]; bmx.free()
kb=KDTree(len(bound)); [kb.insert(v,i) for i,v in enumerate(bound)]; kb.balance()
keep_parts=sum(Bw[:,i] for i,n in enumerate(bn) if any(k in n for k in ('Head','Hand','Thumb','Index','Mid','Ring','Pinky','Neck','Facial','Jaw','Eye','Tongue','Teeth')))
hidden=np.zeros(len(bco),bool)
for i,(v,n) in enumerate(zip(bco,nor)):
    if keep_parts[i]>0.2: continue
    if infoot[i]: hidden[i]=True; continue
    p=mathutils.Vector(v); d=mathutils.Vector(n)
    hit=any(t.ray_cast(p+d*0.001, d, 0.2)[0] is not None or (t.find_nearest(p,0.05)[0] is not None) for t in cover)
    if hit and kb.find(v)[2]>0.06: hidden[i]=True
# skin kept near the clothing edges sits just under the cloth and flickers through when moving: sink it 6 mm
headonly=sum(Bw[:,i] for i,n in enumerate(bn) if any(k in n for k in ('Head','Hand','Thumb','Index','Mid','Ring','Pinky','Facial','Jaw','Eye','Tongue','Teeth')))
sink=np.zeros(len(bco))
for i,(v,n) in enumerate(zip(bco,nor)):
    if hidden[i] or headonly[i]>0.3: continue
    p=mathutils.Vector(v); d=mathutils.Vector(n)
    if any(t.ray_cast(p+d*0.001, d, 0.035)[0] is not None for t in cover): sink[i]=1
sink=smooth(sink[:,None].repeat(3,1),adjacency(body),np.ones(len(bco)),iters=2)[:,0].clip(0,1)
dsp=-nor*0.006*sink[:,None]
for k in (body.data.shape_keys.key_blocks if body.data.shape_keys else []):
    kc=np.array([q.co[:] for q in k.data]); k.data.foreach_set('co',(kc+dsp).reshape(-1))
set_co(body,bco+dsp); bco=bco+dsp
print('sunk skin verts',int((sink>0.5).sum()))
bm=bmesh.new(); bm.from_mesh(body.data); bm.verts.ensure_lookup_table()
bmesh.ops.delete(bm, geom=[f for f in bm.faces if all(hidden[v.index] for v in f.verts)], context='FACES_ONLY')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bm.to_mesh(body.data); bm.free(); body.data.update()
print('hidden skin verts',int(hidden.sum()),'body verts now',len(body.data.vertices))
bpy.ops.wm.save_as_mainfile(filepath=W+f'/stage4_{who}.blend')

material(body,'SkinPrev',(0.85,0.68,0.58,1))
for k in (body.data.shape_keys.key_blocks if body.data.shape_keys else []): k.value=0
parts=[body,top,pants,shoes,hair]+([O['Cap']] if who=='nathan' else [])
parts=[p for p in parts if p]
headparts=[hair]+([O['Cap'],O['Curls']] if who=='nathan' else [])
parts+= [O['Curls']] if who=='nathan' else []
shoot(parts, W+f'/s4_{who}_f.png', azim=0, elev=3)
shoot(parts, W+f'/s4_{who}_34.png', azim=35, elev=8)
for i,az in enumerate((0,60,150)):
    # head close-ups: frame the head only, but render the body too
    sc=bpy.context.scene
    shoot(headparts+[body,O['CC_Game_Eye'],top], W+f'/s4_{who}_head{i}.png', azim=az, elev=5, frame=headparts)
walk_pose(arm); bpy.context.view_layer.update()
shoot(parts, W+f'/s4_{who}_walk.png', azim=60, elev=5)
