# Stage 4 (per character): hair, cap, shoes; hide the skin under the clothes.
import bpy, sys, mathutils, math, numpy as np, bmesh
W=sys.argv[-2]; who=sys.argv[-1]; sys.path.append(W)
from render_util import *; from cclib import *; from people import *
bpy.ops.wm.open_mainfile(filepath=W+f'/stage3_{who}_urban.blend')
O=bpy.data.objects; arm=O['Rig']; body=O['CC_Base_Body']; top=O['Top']; pants=O['Pants']
for pb in arm.pose.bones: pb.matrix_basis=mathutils.Matrix.Identity(4)
bpy.context.view_layer.update()
# ---- face: tell Cooper and Nathan apart ----
_e=mesh_co(O['CC_Game_Eye']); _t=mesh_co(O['CC_Game_Teeth'])
eyeZ=float(_e[:,2].mean()); mouthZ=float(_t[:,2].mean())
Bw,bn=weights(body); bco=mesh_co(body)
_hw=sum(Bw[:,i] for i,n in enumerate(bn) if n in ('CC_Base_Head','CC_Base_JawRoot','CC_Base_FacialBone') or 'Jaw' in n)   # the chin is on the jaw
_front=bco[(np.abs(bco[:,0])<0.015)&(bco[:,1]<-0.02)&(_hw>0.5)&(bco[:,2]<mouthZ)]
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
dump_face(body, W, who, eyeZ, mouthZ, chinZ)                                 # for tools/real_bodies/face_textures.py
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
    hair=None                                                               # grown below: curls / a low cut, no cap
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
style=CHAR[who]['hair']
STRAND=('long_wavy','short_messy','short_neat')
if style in ('curly_top','tight_curls','low_cut')+STRAND:
    # ---- hair: a dark shell on the scalp (thicker toward the crown) and 3D curls on it, by style ----
    #   curly_top (Nathan): big loose curls, volume on top, falling forward over the forehead; short sides
    #   tight_curls (Isaiah): small tight curls, fuller on top, short sides
    #   low_cut (Kenny): a short even cut with a crisp line and a fine curl texture
    import random
    hw_=Bw[:,bn.index('CC_Base_Head')]
    nrm=np.empty(len(bco)*3); body.data.vertices.foreach_get('normal',nrm); nrm=nrm.reshape(-1,3)
    HL=np.array([(-0.11,eyeZ+0.054),(-0.06,eyeZ+0.05),(-0.035,eyeZ+0.008),(-0.012,eyeZ-0.006),(0.03,eyeZ-0.014),(0.11,mouthZ+0.004)])
    if style=='low_cut': HL[:,1]+=np.array([0.002,0.002,0.006,0.01,0.008,0.012])     # a crisp line, faded at the sides
    if CHAR[who].get('fade'): HL[:,1]+=np.array([0.0,0.002,0.012,0.022,0.02,0.018])  # higher, tighter fade on the sides and back
    if style=='long_wavy': HL[:,1]+=np.array([-0.004,-0.002,0.0,0.0,0.0,-0.004])
    def hairline(y): return float(np.interp(y,HL[:,0],HL[:,1]))
    def on_scalp(i):
        x,y,z=bco[i]
        if hw_[i]<0.6 or z<hairline(y): return False
        if abs(x)>0.066 and eyeZ-0.05<z<eyeZ+0.03 and -0.04<y<0.04: return False    # ears
        return True
    scalp=np.array([on_scalp(i) for i in range(len(bco))])
    def on_scalp_pt(p):
        x,y,z=p
        if z<hairline(y): return False
        if abs(x)>0.066 and eyeZ-0.05<z<eyeZ+0.03 and -0.04<y<0.04: return False
        return True
    def height(i):
        z=bco[i][2]; h0=hairline(bco[i][1]); return float(np.clip((z-h0)/max(ctop-h0,0.01),0,1))
    T={'curly_top':(0.004,0.013),'tight_curls':(0.003,0.01),'low_cut':(0.0025,0.0065),
       'long_wavy':(0.004,0.008),'short_messy':(0.003,0.008),'short_neat':(0.003,0.007)}[style]
    if CHAR[who].get('fade'): T=(0.0012,0.006)
    def thick(h): return T[0]+(T[1]-T[0])*h
    bm=bmesh.new(); bm.from_mesh(body.data); bm.verts.ensure_lookup_table()
    # faces with any corner on the scalp; the corners off it tuck just under the skin, so the edge of the hair is the
    # smooth line where the shell meets the skin (not a staircase of whole triangles)
    keepf=set(f for f in bm.faces if any(scalp[v.index] for v in f.verts))
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f not in keepf], context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    dl=bm.verts.layers.deform.active
    if dl: bm.verts.layers.deform.remove(dl)                                  # the body's weights don't belong here
    for v in bm.verts:
        z=v.co.z; h0=hairline(v.co.y); h=float(np.clip((z-h0)/max(ctop-h0,0.01),0,1))
        e=float(np.clip((z-h0+0.012)/0.016,0,1)); e=e*e*(3-2*e)              # fades in across the hairline
        if not on_scalp_pt(v.co) and z>=h0: e=0.0                              # (ears)
        v.co=v.co+v.normal*(-0.004+(thick(h)+0.004)*e)
    sm=bpy.data.meshes.new('HairBase'); bm.to_mesh(sm); bm.free()
    base=bpy.data.objects.new('HairBase',sm); bpy.context.scene.collection.objects.link(base)
    if base.data.shape_keys: base.shape_key_clear()
    if style in STRAND:                                                        # the strands' gradient material, root end
        for lp in base.data.uv_layers.active.data: lp.uv=(0.5,0.02)
        material(base,'HairStrand',(0.3,0.25,0.2,1))
    else: material(base,'Curls',(0.05,0.04,0.035,1))
    rigid_to_head(base); sel(base); bpy.ops.object.shade_smooth()
    # roots for the curls: scattered over the scalp triangles, kept a spacing apart (the head mesh has too few
    # vertices for dense hair)
    rnd=random.Random(11)
    me=body.data; me.calc_loop_triangles()
    tris=[lt.vertices[:] for lt in me.loop_triangles if all(scalp[i] for i in lt.vertices)]
    area=np.array([np.linalg.norm(np.cross(bco[b]-bco[a],bco[c]-bco[a]))/2 for a,b,c in tris])
    spacing={'curly_top':0.0068,'tight_curls':0.0046,'low_cut':0.0044,'long_wavy':0.0095,'short_messy':0.0062,'short_neat':0.005}[style]
    want=int(area.sum()/(spacing*spacing)*1.6)
    nrng=np.random.default_rng(5); pick=nrng.choice(len(tris),want,p=area/area.sum())
    grid={}; P=[]; N=[]
    def cell(p): return (int(p[0]//spacing),int(p[1]//spacing),int(p[2]//spacing))
    for t in pick:
        a,b,c=tris[t]; r1,r2=nrng.random(),nrng.random()
        if r1+r2>1: r1,r2=1-r1,1-r2
        p=bco[a]+(bco[b]-bco[a])*r1+(bco[c]-bco[a])*r2
        if not on_scalp_pt(p): continue
        if style=='low_cut' and (p[2]-hairline(p[1]))/max(ctop-hairline(p[1]),0.01)<0.08: continue
        k=cell(p); ok=True
        for dx in (-1,0,1):
            for dy in (-1,0,1):
                for dz in (-1,0,1):
                    for q in grid.get((k[0]+dx,k[1]+dy,k[2]+dz),()):
                        if np.linalg.norm(P[q]-p)<spacing: ok=False; break
                    if not ok: break
                if not ok: break
            if not ok: break
        if not ok: continue
        grid.setdefault(k,[]).append(len(P)); P.append(p)
        nn=nrm[a]*(1-r1-r2)+nrm[b]*r1+nrm[c]*r2; N.append(nn/np.linalg.norm(nn))
    P=np.array(P); N=np.array(N)
    def hgt(p): h0=hairline(p[1]); return float(np.clip((p[2]-h0)/max(ctop-h0,0.01),0,1))
    Hs=np.array([hgt(p) for p in P])
    fwd=mathutils.Vector((0,-1,0))
    groups=[]
    def grp(sel, **kw):
        sel=[i for i in sel]
        if not sel: return
        extra=kw.pop('extra',0.0)
        lift=[thick(Hs[i])-0.002+extra*rnd.random() for i in sel]
        groups.append(coils([P[i] for i in sel],[N[i] for i in sel],seed=len(groups)+5,lift=lift,**kw))
    allp=range(len(P))
    crown=[i for i in allp if Hs[i]>=0.45]; low=[i for i in allp if Hs[i]<0.45]
    fringe=set(i for i in allp if P[i][1]<-0.055 and Hs[i]<0.7)
    picked=P
    if style in STRAND:
        top_bvh=bvh_of(O['Top']); shoulder=float(arm.data.bones['CC_Base_L_Upperarm'].head_local.z)
        back=mathutils.Vector((0,1,0)); dn=mathutils.Vector((0,0,-1)); xh=mathutils.Vector((1,0,0))
        roots=[P[i] for i in allp]; norms=[N[i] for i in allp]
        def jit(r,a): return mathutils.Vector((r.uniform(-a,a),r.uniform(-a,a),r.uniform(-a,a)))
        if style=='long_wavy':
            # parted in the middle, falling to the shoulders, framing the face
            def grow(p,n,r):
                side=1.0 if p.x>0.004 else -1.0 if p.x<-0.004 else (1.0 if r.random()<0.5 else -1.0)
                front=p.y<-0.045
                return (n*0.3+xh*side*(0.8 if front else 0.3)+back*(0.45 if front else 0.15)+dn*0.35+jit(r,0.08)).normalized()
            def keepout(q):
                if q.y<-0.015 and abs(q.x)<0.074 and chinZ-0.035<q.z<eyeZ+0.075:
                    q=mathutils.Vector((0.074 if q.x>=0 else -0.074, q.y, q.z))
                return q
            tips=np.random.default_rng(3).uniform(shoulder-0.04,shoulder+0.05,len(roots))
            # one continuous hair surface over the head, hanging to the shoulders (face clear), then looser locks over it
            hair_o=hair_parted(head_bvh,[top_bvh],shoulder-0.02,eyeZ,chinZ,cc,ctop)   # middle part, combed down, to the shoulders
            hair_o.name='Curls'
        else:
            neat=style=='short_neat'
            def grow(p,n,r):
                if p.y<-0.03: d=n*0.2+(-back)*(0.3 if neat else 0.5)+dn*0.15                    # front: forward over the hairline
                elif p.y>0.04: d=n*0.1+back*0.3+dn*0.6                                           # back: down
                elif abs(p.x)>0.05: d=n*0.1+xh*(1 if p.x>0 else -1)*0.2+dn*0.6+back*0.15        # sides: down, back
                else: d=n*(0.15 if neat else 0.3)+back*(0.45 if neat else 0.2)                   # crown
                d=d+jit(r,0.12 if neat else 0.4)
                return (d-n*(d.dot(n)-(0.12 if neat else 0.28))).normalized()                    # mostly along the scalp
            hair_o=strands(roots,norms,[head_bvh],'Curls',step=0.006,length=(0.025,0.045) if neat else (0.035,0.065),grow=grow,
                           gravity=0.12 if neat else 0.07,wave=(0.0015,0.03),width=(0.012,0.018) if neat else (0.01,0.016),
                           thick=0.35,layer=(0.002,0.006 if neat else 0.011),seed=6,sides=5,uv_len=0.12)
        material(hair_o,'HairStrand',(0.3,0.25,0.2,1)); rigid_to_head(hair_o); sel(hair_o); bpy.ops.object.shade_smooth()
        groups=[]
    elif style=='curly_top':
        grp([i for i in crown if i not in fringe], L=(0.022,0.038), R=(0.006,0.0095), pitch=(0.007,0.011), tube=(0.0024,0.0032), outward=0.45, bias=fwd*0.5, extra=0.004)
        grp([i for i in low if i not in fringe], L=(0.006,0.011), R=(0.0035,0.005), pitch=(0.005,0.007), tube=(0.0019,0.0024), sides=4, turn=5)   # short sides
        fr=sorted(fringe)
        grp(fr, L=(0.026,0.042), R=(0.0055,0.0085), pitch=(0.007,0.01), tube=(0.0023,0.003), bias=fwd*0.2,
            maxlen=[max(0.006,P[i][2]-(eyeZ+0.03)) for i in fr])                        # falls over the forehead, stops above the brows
        grp(crown[::2], L=(0.018,0.03), R=(0.006,0.009), pitch=(0.007,0.011), tube=(0.0024,0.003), outward=0.55, bias=fwd*0.4, extra=0.012)   # volume
    elif style=='tight_curls':
        grp(crown, L=(0.012,0.022), R=(0.0032,0.005), pitch=(0.0038,0.0055), tube=(0.0016,0.0022), outward=0.55, extra=0.004, sides=4, turn=4.5)
        grp(low, L=(0.004,0.008), R=(0.0025,0.0035), pitch=(0.003,0.004), tube=(0.0013,0.0017), sides=4, turn=4)
        grp(crown[::2], L=(0.01,0.018), R=(0.0032,0.0048), pitch=(0.0038,0.0052), tube=(0.0016,0.002), outward=0.6, extra=0.008, sides=4, turn=4.5)
    else:
        grp(allp, L=(0.0035,0.006), R=(0.0017,0.0023), pitch=(0.0022,0.003), tube=(0.0012,0.0015), sides=4, outward=0.75, turn=4)
    if groups:
        cu=join(groups,'Curls') if len(groups)>1 else groups[0]; cu.name='Curls'
        material(cu,'Curls',(0.05,0.04,0.035,1)); rigid_to_head(cu); sel(cu); bpy.ops.object.shade_smooth()
    print('hair',style,'roots',len(picked),'hair verts',len(O['Curls'].data.vertices),'base verts',len(base.data.vertices))
if CHAR[who].get('glasses'):
    # ---- thin black rectangular frames: two rims, a bridge, temples back over the ears ----
    ev=mesh_co(O['CC_Game_Eye']); gy=float(ev[:,1].min())-0.013
    bm=bmesh.new(); inner=[]
    for sx in (1,-1):
        side=ev[ev[:,0]*sx>0]; cx=float(side[:,0].mean()); cz=float(side[:,2].mean())+0.001
        w,h,r=0.026,0.0155,0.007
        pts=[]
        for qx,qz,a0 in ((w-r,h-r,0),(-(w-r),h-r,90),(-(w-r),-(h-r),180),(w-r,-(h-r),270)):
            for k in range(5):
                ang=math.radians(a0+k*22.5); pts.append((cx+qx+r*math.cos(ang), gy, cz+qz+r*math.sin(ang)))
        tube_path(bm,pts,0.0019,6,True)
        ox=cx+sx*w; inner.append((cx-sx*w,cz))
        tube_path(bm,[(ox,gy,cz+h*0.6),(ox+sx*0.01,gy+0.018,cz+h*0.55),(sx*0.075,-0.01,cz+0.006),(sx*0.075,0.03,cz),(sx*0.071,0.048,cz-0.018)],0.0016,6)
    (lx,lz),(rx,rz)=inner
    tube_path(bm,[(lx,gy,lz+0.006),(0,gy-0.002,(lz+rz)/2+0.009),(rx,gy,rz+0.006)],0.0018,6)
    gm=bpy.data.meshes.new('Glasses'); bm.to_mesh(gm); bm.free()
    gl=bpy.data.objects.new('Glasses',gm); bpy.context.scene.collection.objects.link(gl)
    push_out(gl, head_bvh, 0.0025, passes=2, smooth_iters=0)
    material(gl,'Glasses',(0.02,0.02,0.02,1)); rigid_to_head(gl); sel(gl); bpy.ops.object.shade_smooth()
    print('glasses verts',len(gl.data.vertices))
if CHAR[who].get('nose_ring'): nose_ring(body, mouthZ, rigid_to_head)
if hair:
    hair.name='Hair'; material(hair,'Hair',(0.45,0.3,0.18,1) if who=='cooper' else (0.07,0.05,0.04,1)); rigid_to_head(hair)
    sel(hair); bpy.ops.object.shade_smooth()

# ---- shoes: the feet, puffed out and smoothed into sneakers ----
footw=sum(Bw[:,i] for i,n in enumerate(bn) if 'Foot' in n or 'Toe' in n)
infoot=(footw>0.5)&(bco[:,2]<0.12)
from mathutils.kdtree import KDTree
shoes=fit_shoes(body, arm, W, bco, Bw, bn, infoot)                         # the scanned sneakers, recoloured per character in Unity
# the foot inside the shoe: a sock (the scanned shoe is an open shell: skin showed through the lacing), tucked in a little
if 'Socks' not in [m.name for m in body.data.materials]: body.data.materials.append(bpy.data.materials.get('Socks') or bpy.data.materials.new('Socks'))
si=[m.name for m in body.data.materials].index('Socks')
sockz=0.11
foot_v=infoot|((footw>0.5)&(bco[:,2]<sockz))
for poly in body.data.polygons:
    if all(foot_v[v] or bco[v][2]<sockz for v in poly.vertices) and np.mean([bco[v][2] for v in poly.vertices])<sockz: poly.material_index=si
# shrink the foot toward its bone line (ankle → toe base) so it sits well inside the shoe (the toes poked through the
# scan's low toe box); smoothly less toward the ankle
_d=np.zeros_like(bco)
for side in ('L','R'):
    A=np.array(arm.data.bones[f'CC_Base_{side}_Foot'].head_local); B=np.array(arm.data.bones[f'CC_Base_{side}_ToeBase'].head_local) if f'CC_Base_{side}_ToeBase' in arm.data.bones else A+np.array([0,-0.13,-0.06])
    B=B+(B-A)*0.35                                                      # on past the toe base
    ab=B-A; sel_=(np.sign(bco[:,0])==(1 if side=='L' else -1))&(footw>0.3)&(bco[:,2]<0.13)
    t=np.clip(((bco-A)@ab)/(ab@ab),0,1); near=A+t[:,None]*ab; near[:,2]=np.minimum(near[:,2],0.035)
    w=np.clip((0.13-bco[:,2])/0.05,0,1)*np.clip(footw,0,1)*sel_
    _d+=(near-bco)*(0.45*w)[:,None]
for k in (body.data.shape_keys.key_blocks if body.data.shape_keys else []):
    kc=np.array([q.co[:] for q in k.data]); k.data.foreach_set('co',(kc+_d).reshape(-1))
set_co(body,bco+_d); bco=bco+_d
extra_cover=[]
if CHAR[who].get('socks'):
    sk=socks(body, arm); material(sk,'Socks',(0.95,0.95,0.95,1)); extra_cover.append(sk)

# ---- hide the skin under the clothes (it would poke through when animating) ----
cover=[bvh_of(o) for o in (top,pants,shoes)+tuple(extra_cover)]
nor=np.empty(len(bco)*3); body.data.vertices.foreach_get('normal',nor); nor=nor.reshape(-1,3)
bound=[]
for o in (top,pants):
    bmx=bmesh.new(); bmx.from_mesh(o.data)
    bound+= [tuple(v.co) for v in bmx.verts if v.is_boundary]; bmx.free()
kb=KDTree(len(bound)); [kb.insert(v,i) for i,v in enumerate(bound)]; kb.balance()
keep_parts=sum(Bw[:,i] for i,n in enumerate(bn) if any(k in n for k in ('Head','Hand','Thumb','Index','Mid','Ring','Pinky','Neck','Facial','Jaw','Eye','Tongue','Teeth')))
hidden=np.zeros(len(bco),bool)
headhand=sum(Bw[:,i] for i,n in enumerate(bn) if any(k in n for k in ('Head','Hand','Thumb','Index','Mid','Ring','Pinky','Facial','Jaw','Eye','Tongue','Teeth')) and 'Toe' not in n)
armish=sum(Bw[:,i] for i,n in enumerate(bn) if any(k in n for k in ('Upperarm','Forearm','Elbow')))
for i,(v,n) in enumerate(zip(bco,nor)):
    if headhand[i]>0.2: continue
    if infoot[i] or (bco[i][2]<0.11 and footw[i]>0.5): continue           # the foot stays: it's the sock inside the shoe
    p=mathutils.Vector(v); d=mathutils.Vector(n)
    hit=any(t.ray_cast(p+d*0.001, d, 0.2)[0] is not None or (t.find_nearest(p,0.05)[0] is not None) for t in cover)
    # neck-weighted skin (the trapezius runs under the shirt onto the shoulders) only hides well away from the collar
    # arm skin stays whole for 9 cm inside a sleeve opening, so what you see up the sleeve is an arm, not a cut edge
    if hit and kb.find(v)[2]>(0.09 if armish[i]>0.5 else 0.06 if keep_parts[i]>0.2 else 0.04): hidden[i]=True
# skin kept near the clothing edges sits just under the cloth and pokes through when moving (the shoulders did): sink it 1 cm
headonly=sum(Bw[:,i] for i,n in enumerate(bn) if any(k in n for k in ('Head','Hand','Thumb','Index','Mid','Ring','Pinky','Facial','Jaw','Eye','Tongue','Teeth')))
sink=np.zeros(len(bco))
for i,(v,n) in enumerate(zip(bco,nor)):
    if hidden[i] or headonly[i]>0.3: continue
    p=mathutils.Vector(v); d=mathutils.Vector(n)
    if any(t.ray_cast(p+d*0.001, d, 0.035)[0] is not None for t in cover): sink[i]=1
sink=smooth(sink[:,None].repeat(3,1),adjacency(body),np.ones(len(bco)),iters=2)[:,0].clip(0,1)
dsp=-nor*0.01*sink[:,None]
for k in (body.data.shape_keys.key_blocks if body.data.shape_keys else []):
    kc=np.array([q.co[:] for q in k.data]); k.data.foreach_set('co',(kc+dsp).reshape(-1))
set_co(body,bco+dsp); bco=bco+dsp
print('sunk skin verts',int((sink>0.5).sum()))
# anything still outside the cloth (shoulder tops and upper arms stuck out through the shirt even at rest): pull it
# 8 mm inside, measured along the skin's normal. Not the head / hands, and not right at a garment edge.
cover_t=[bvh_of(o) for o in (top,pants)]
dsp=np.zeros_like(bco); pulled=0
for i,(v,n) in enumerate(zip(bco,nor)):
    if headonly[i]>0.3: continue                               # (hidden ones too: they stay where a face still has a kept corner)
    p=mathutils.Vector(v); nb=mathutils.Vector(n)
    if kb.find(v)[2]<0.02: continue
    best=None
    for t in cover_t:
        loc,_,_,dist=t.find_nearest(p,0.03)
        if loc is not None and (best is None or dist<best[1]): best=(loc,dist)
    if best is None: continue
    out=(p-best[0]).dot(nb)                                    # > 0: the skin is outside the cloth
    if out>-0.008: dsp[i]=-np.array(nb)*(out+0.008); pulled+=1
raw=dsp.copy(); dsp=smooth(dsp,adjacency(body),np.ones(len(bco)),iters=2)
hard=np.linalg.norm(raw,axis=1)>0; dsp[hard]=raw[hard]                  # full pull where it pokes, eased round it
for k in (body.data.shape_keys.key_blocks if body.data.shape_keys else []):
    kc=np.array([q.co[:] for q in k.data]); k.data.foreach_set('co',(kc+dsp).reshape(-1))
set_co(body,bco+dsp); bco=bco+dsp
print('pulled skin verts inside the cloth',pulled)
bm=bmesh.new(); bm.from_mesh(body.data); bm.verts.ensure_lookup_table()
bmesh.ops.delete(bm, geom=[f for f in bm.faces if all(hidden[v.index] for v in f.verts)], context='FACES_ONLY')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bm.to_mesh(body.data); bm.free(); body.data.update()
print('hidden skin verts',int(hidden.sum()),'body verts now',len(body.data.vertices))
bpy.ops.wm.save_as_mainfile(filepath=W+f'/stage4_{who}.blend')

material(body,'SkinPrev',(0.85,0.68,0.58,1))
for k in (body.data.shape_keys.key_blocks if body.data.shape_keys else []): k.value=0
parts=[p for p in [body,top,pants,shoes,hair,O.get('Cap'),O.get('HairBase'),O.get('Curls'),O.get('Glasses'),O.get('Socks'),O.get('Jewelry')] if p]
parts=[p for p in parts if p]
headparts=[p for p in [hair,O.get('Cap'),O.get('Curls'),O.get('HairBase'),O.get('Glasses')] if p]
parts+= [O['Curls']] if O.get('Curls') else []
shoot(parts, W+f'/s4_{who}_f.png', azim=0, elev=3)
shoot(parts, W+f'/s4_{who}_34.png', azim=35, elev=8)
for i,az in enumerate((0,60,150)):
    # head close-ups: frame the head only, but render the body too
    sc=bpy.context.scene
    shoot(headparts+[body,O['CC_Game_Eye'],top], W+f'/s4_{who}_head{i}.png', azim=az, elev=5, frame=headparts)
walk_pose(arm); bpy.context.view_layer.update()
shoot(parts, W+f'/s4_{who}_walk.png', azim=60, elev=5)
