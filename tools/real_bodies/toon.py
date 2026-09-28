# Cartoon bodies for the DLC characters (Mordecai the blue jay, Rigby the raccoon), built in Blender so they're smooth:
# primitive parts melted into one skin (voxel remesh + smoothing), coloured by region (pale face, black stripe, white
# wrist bands, ringed legs / tail), plus crisp separate eyes, pupils, beak and nose. A small skeleton with the same
# joint names as the real bodies (CC_Base_Hip, Waist, NeckTwist01, Head, L/R Upperarm, Forearm, Hand, Thigh, Calf, Foot)
# so CharacterAnimator and the weapons drive it the same way; bones unrotated at rest, arms down. Saved as
# stage5_<who>.blend for export_ssrb.py. Materials are named Toon_RRGGBB (RealBody reads the colour from the name).
#   Blender -b -P toon.py -- WORK mordecai|rigby
# Mordecai and Rigby belong to Cartoon Network (Regular Show): private builds only.
import bpy, bmesh, sys, math, numpy as np, mathutils
from mathutils import Vector as V
W=sys.argv[-2]; who=sys.argv[-1]; sys.path.append(W)
from cclib import *; from render_util import *
from mathutils.bvhtree import BVHTree
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene

def mat(hexc):
    name='Toon_'+hexc.lstrip('#').lower()
    m=bpy.data.materials.get(name)
    if not m:
        m=bpy.data.materials.new(name); c=hexc.lstrip('#'); m.diffuse_color=(int(c[0:2],16)/255,int(c[2:4],16)/255,int(c[4:6],16)/255,1)
    return m

def obj(name, bm):
    me=bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o=bpy.data.objects.new(name,me); scene.collection.objects.link(o); return o

def sphere(name, c, r, seg=40, rot=(0,0,0)):
    bm=bmesh.new(); bmesh.ops.create_uvsphere(bm,u_segments=seg,v_segments=seg//2,radius=1.0)
    M=mathutils.Matrix.Translation(V(c))@mathutils.Euler(rot).to_matrix().to_4x4()@mathutils.Matrix.Diagonal((r[0],r[1],r[2],1))
    bmesh.ops.transform(bm,matrix=M,verts=bm.verts); return obj(name,bm)

def tube(name, pts, radii, seg=20):
    """A round tube through points with a radius per point, capped."""
    bm=bmesh.new(); rings=[]; pts=[V(p) for p in pts]
    for i,p in enumerate(pts):
        t=(pts[min(i+1,len(pts)-1)]-pts[max(i-1,0)]).normalized(); a=t.orthogonal().normalized(); b=t.cross(a)
        rings.append([bm.verts.new(p+(a*math.cos(k*6.283/seg)+b*math.sin(k*6.283/seg))*radii[i]) for k in range(seg)])
    for i in range(len(pts)-1):
        for k in range(seg): bm.faces.new((rings[i][k],rings[i][(k+1)%seg],rings[i+1][(k+1)%seg],rings[i+1][k]))
    for r in (rings[0],rings[-1]):
        c=bm.verts.new(sum((v.co for v in r),V())/len(r))
        for k in range(seg): bm.faces.new((r[k],r[(k+1)%seg],c) if r is rings[-1] else (r[(k+1)%seg],r[k],c))
    return obj(name,bm)

def cone(name, base, tip, r0, seg=40, rings=16):
    pts=[V(base).lerp(V(tip),i/rings) for i in range(rings+1)]
    radii=[r0*(1-(i/rings))**0.9+0.004 for i in range(rings+1)]
    return tube(name,pts,radii,seg)

def rounded_box(name, c, size, bevel):
    bm=bmesh.new(); bmesh.ops.create_cube(bm,size=1.0)
    bmesh.ops.scale(bm,vec=V(size),verts=bm.verts); bmesh.ops.translate(bm,vec=V(c),verts=bm.verts)
    o=obj(name,bm); md=o.modifiers.new('b','BEVEL'); md.width=bevel; md.segments=8; md.limit_method='NONE'
    sel(o); bpy.ops.object.modifier_apply(modifier='b'); return o

def smooth_join(parts, voxel=0.0035, faces=60000, smooth_iters=4):
    """Melt the parts into one skin; returns it and each part's BVH (to colour the skin by which part it came from)."""
    trees=[]
    for p,rule in parts:
        bm=bmesh.new(); bm.from_mesh(p.data); bm.transform(p.matrix_world); trees.append((BVHTree.FromBMesh(bm),rule)); bm.free()
    o=join([p for p,_ in parts],'CC_Base_Body'); sel(o)
    md=o.modifiers.new('r','REMESH'); md.mode='VOXEL'; md.voxel_size=voxel; md.use_smooth_shade=True; bpy.ops.object.modifier_apply(modifier='r')
    md=o.modifiers.new('s','LAPLACIANSMOOTH'); md.iterations=smooth_iters; md.lambda_factor=0.5; md.use_volume_preserve=True; bpy.ops.object.modifier_apply(modifier='s')
    n=len(o.data.polygons)
    if n>faces: md=o.modifiers.new('d','DECIMATE'); md.ratio=faces/n; bpy.ops.object.modifier_apply(modifier='d')
    for poly in o.data.polygons: poly.use_smooth=True                     # smooth everywhere (exported normals are per corner)
    for a in [a for a in o.data.attributes if a.name in ('sharp_face','sharp_edge')]: o.data.attributes.remove(a)
    # colour each face by the nearest part's rule
    me=o.data; me.materials.clear(); slots={}
    for poly in me.polygons:
        c=poly.center; best=None
        for t,rule in trees:
            loc,nrm,_,d=t.find_nearest(c,0.05)
            if loc is not None and (best is None or d<best[0]): best=(d,rule)
        col=best[1](c,poly.normal) if best else '#ff00ff'
        if col not in slots: slots[col]=len(me.materials); me.materials.append(mat(col))
        poly.material_index=slots[col]
    print('  toon skin faces',len(me.polygons),'colours',list(slots)); return o

def rigid(o, bone, arm):
    for g in list(o.vertex_groups): o.vertex_groups.remove(g)
    g=o.vertex_groups.new(name=bone); g.add(list(range(len(o.data.vertices))),1.0,'REPLACE')
    o.parent=arm; o.modifiers.new('Armature','ARMATURE').object=arm; o['ssrb_part']=1
    sel(o); bpy.ops.object.shade_smooth()

def rig(J):
    """J: bone name -> (head, tail, parent)."""
    a=bpy.data.armatures.new('Rig'); arm=bpy.data.objects.new('Rig',a); scene.collection.objects.link(arm)
    sel(arm); bpy.ops.object.mode_set(mode='EDIT')
    for n,(h,t,p) in J.items():
        b=a.edit_bones.new(n); b.head=V(h); b.tail=V(t)
    for n,(h,t,p) in J.items():
        if p: a.edit_bones[n].parent=a.edit_bones[p]
    bpy.ops.object.mode_set(mode='OBJECT'); return arm

def std_joints(hip, waist_top, neck, head, head_top, sh, el, wr, hand_end, th, kn, an, toe):
    J={'CC_Base_Hip':(hip,(0,0,hip[2]+0.08),None),
       'CC_Base_Waist':((0,0,hip[2]+0.08),waist_top,'CC_Base_Hip'),
       'CC_Base_NeckTwist01':(neck,head,'CC_Base_Waist'),
       'CC_Base_Head':(head,head_top,'CC_Base_NeckTwist01')}
    for s,sx in (('L',1),('R',-1)):
        f=lambda p:(p[0]*sx,p[1],p[2])
        J[f'CC_Base_{s}_Upperarm']=(f(sh),f(el),'CC_Base_Waist')
        J[f'CC_Base_{s}_Forearm']=(f(el),f(wr),f'CC_Base_{s}_Upperarm')
        J[f'CC_Base_{s}_Hand']=(f(wr),f(hand_end),f'CC_Base_{s}_Forearm')
        J[f'CC_Base_{s}_Thigh']=(f(th),f(kn),'CC_Base_Hip')
        J[f'CC_Base_{s}_Calf']=(f(kn),f(an),f'CC_Base_{s}_Thigh')
        J[f'CC_Base_{s}_Foot']=(f(an),f(toe),f'CC_Base_{s}_Calf')
    return J

def skin_auto(body, arm):
    sel(body); arm.select_set(True); bpy.context.view_layer.objects.active=arm
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    ok=len(body.vertex_groups)>0 and any(len([1 for v in body.data.vertices if any(g.group==vg.index for g in v.groups)])>0 for vg in body.vertex_groups)
    print('  auto weights groups',[g.name for g in body.vertex_groups][:6],'ok',ok)

parts=[]; extras=[]
if who=='mordecai':
    BLUE,PALE,BLACK,WHITE,GREY,RING,BEAK,BEAK2='#4d8fd0','#c9e0f2','#141820','#f6f6f4','#5a5e63','#1f2226','#3e3f43','#2b2c2f'
    # legs: long, thin and ringed
    for sx in (1,-1):
        x=0.055*sx
        leg=tube(f'leg{sx}',[(x,0,0.91),(x,0,0.69),(x,0,0.47),(x,0,0.27),(x,0,0.06)],[0.02,0.018,0.021,0.017,0.017],20)
        parts.append((leg,lambda c,n:RING if (c.z%0.05)<0.006 and c.z<0.9 else GREY))
        for ang,L in ((-26,0.14),(0,0.15),(26,0.14),(180,0.065)):             # three toes forward, one back
            a=math.radians(ang); d=V((math.sin(a),-math.cos(a),0))
            toe=tube(f'toe{sx}{ang}',[(x,0,0.045),(x+d.x*L*0.5,d.y*L*0.5,0.022),(x+d.x*L,d.y*L,0.012)],[0.013,0.011,0.007],14)
            parts.append((toe,lambda c,n:GREY))
    body=rounded_box('body',(0,0,1.15),(0.21,0.15,0.55),0.055)
    parts.append((body,lambda c,n:PALE if n.y<-0.55 and abs(c.x)<0.1 and c.z>0.89 else BLUE))
    parts.append((cone('tail',(0,0.06,0.93),(0.02,0.19,0.86),0.045,24,8),lambda c,n:BLUE))
    neck=tube('neck',[(0,0,1.39),(0,-0.005,1.47)],[0.05,0.05],24); parts.append((neck,lambda c,n:PALE))
    # head: skull, fuller cheeks, a tall crest; pale face, a black stripe back from each eye, blue cap and crest
    def head_rule(c,n):
        if abs(c.x)>0.095 and 1.575<c.z<1.64 and -0.35<n.y<0.6: return BLACK
        if (c.z<1.61 and n.y<0.3) or (n.y<-0.3 and c.z<1.655) or c.z<1.47: return PALE
        return BLUE
    parts.append((sphere('skull',(0,0.005,1.575),(0.148,0.135,0.14)),head_rule))
    parts.append((sphere('cheeks',(0,-0.025,1.5),(0.158,0.12,0.1)),head_rule))
    parts.append((cone('crest',(0,0.01,1.63),(0,0.05,1.95),0.12,40,20),lambda c,n:BLUE))
    # arms: long and thin, two white bands above the wrist; three-fingered hands with a thumb
    for sx in (1,-1):
        S,E,Wr=(0.125*sx,0,1.37),(0.145*sx,0,1.08),(0.155*sx,0,0.82)
        arm_=tube(f'arm{sx}',[S,E,Wr],[0.022,0.019,0.018],18)
        parts.append((arm_,lambda c,n:WHITE if (0.875<c.z<0.9 or 0.92<c.z<0.945) else BLUE))
        parts.append((sphere(f'palm{sx}',(0.157*sx,0,0.79),(0.022,0.03,0.036)),lambda c,n:BLUE))
        for k,yo in enumerate((-0.018,0.0,0.018)):
            f_=tube(f'fin{sx}{k}',[(0.158*sx,yo,0.77),(0.16*sx,yo*1.3,0.73),(0.158*sx,yo*1.3-0.012,0.7)],[0.009,0.008,0.005],12)
            parts.append((f_,lambda c,n:BLUE))
        th=tube(f'thumb{sx}',[(0.155*sx,-0.02,0.79),(0.152*sx,-0.045,0.77),(0.15*sx,-0.055,0.755)],[0.008,0.007,0.005],12)
        parts.append((th,lambda c,n:BLUE))
    skin=smooth_join(parts, voxel=0.0033, faces=70000)
    # crisp separate parts: eyes, pupils, beak
    for sx in (1,-1):
        e=sphere(f'eye{sx}',(0.047*sx,-0.103,1.655),(0.046,0.042,0.048),32); e.data.materials.append(mat(WHITE)); extras.append((e,'CC_Base_Head'))
        p=sphere(f'pupil{sx}',(0.04*sx,-0.146,1.652),(0.0095,0.006,0.0095),16); p.data.materials.append(mat('#0a0a0a')); extras.append((p,'CC_Base_Head'))
    bm=bmesh.new(); bmesh.ops.create_cube(bm,size=1.0)
    for v in bm.verts:                                                     # a flat wedge: wide at the face, narrow at the tip
        t=0.5-v.co.y                                                       # 0 at the face side (y=+0.5), 1 at the tip
        v.co=V((v.co.x*(0.14-0.12*t), -0.115-0.14*t, 1.585+v.co.z*(0.06-0.045*t)-0.012*t))   # tapering to a point
    beak=obj('beak',bm); md=beak.modifiers.new('b','BEVEL'); md.width=0.006; md.segments=4; sel(beak); bpy.ops.object.modifier_apply(modifier='b')
    md=beak.modifiers.new('s','SUBSURF'); md.levels=2; bpy.ops.object.modifier_apply(modifier='s')
    beak.data.materials.append(mat(BEAK)); beak.data.materials.append(mat(BEAK2))
    for poly in beak.data.polygons: poly.material_index=1 if poly.normal.z<-0.4 else 0
    extras.append((beak,'CC_Base_Head'))
    J=std_joints(hip=(0,0,0.9),waist_top=(0,0,1.38),neck=(0,0,1.40),head=(0,0,1.47),head_top=(0,0,1.75),
                 sh=(0.125,0,1.37),el=(0.145,0,1.08),wr=(0.155,0,0.82),hand_end=(0.158,0,0.7),
                 th=(0.055,0,0.9),kn=(0.055,0,0.47),an=(0.055,0,0.06),toe=(0.055,-0.12,0.015))
else:
    FUR,DARK,TAN,NOSE,WHITE='#8a5a38','#3a2619','#c29a6b','#161110','#f6f6f4'
    for sx in (1,-1):
        x=0.075*sx
        leg=tube(f'leg{sx}',[(x,0,0.37),(x,0,0.21),(x,0,0.06)],[0.042,0.037,0.034],20); parts.append((leg,lambda c,n:FUR))
        parts.append((sphere(f'foot{sx}',(x,-0.035,0.035),(0.048,0.085,0.035)),lambda c,n:DARK))
    def belly(c,n): return TAN if n.y<-0.55 and abs(c.x)<0.1 and 0.34<c.z<0.64 else FUR
    parts.append((sphere('body',(0,0,0.56),(0.165,0.14,0.25)),belly))
    parts.append((sphere('hips',(0,0.005,0.44),(0.18,0.15,0.15)),belly))
    # tail: thick, curling up behind, ringed
    tp=[(0,0.13,0.4),(0,0.25,0.42),(0,0.33,0.52),(0,0.34,0.66),(0,0.3,0.78)]
    tl=tube('tail',tp,[0.05,0.065,0.068,0.06,0.04],22)
    parts.append((tl,lambda c,n:DARK if int((c.y*1.0+c.z)/0.07)%2 else FUR))
    def head_rule(c,n):
        if 0.925<c.z<0.99 and n.y<0.15 and abs(c.x)<0.19: return DARK       # the mask across the eyes
        if n.y<-0.5 and c.z<0.9 and abs(c.x)<0.09: return TAN
        return FUR
    parts.append((sphere('head',(0,0,0.93),(0.2,0.17,0.175)),head_rule))
    parts.append((sphere('muzzle',(0,-0.145,0.865),(0.085,0.07,0.058)),lambda c,n:TAN))
    for sx in (1,-1):
        parts.append((sphere(f'ear{sx}',(0.13*sx,0.02,1.1),(0.058,0.026,0.065),24,(0,0,-0.35*sx)),lambda c,n:DARK if n.y<-0.4 else FUR))
        S,E,Wr=(0.15*sx,0,0.72),(0.168*sx,0,0.57),(0.178*sx,0,0.42)
        parts.append((tube(f'arm{sx}',[S,E,Wr],[0.024,0.022,0.02],16),lambda c,n:FUR))
        parts.append((sphere(f'paw{sx}',(0.18*sx,-0.004,0.395),(0.025,0.028,0.032)),lambda c,n:DARK))
        for k,yo in enumerate((-0.014,0.0,0.014)):
            parts.append((tube(f'pf{sx}{k}',[(0.181*sx,yo,0.375),(0.182*sx,yo*1.2,0.352)],[0.0075,0.006],10),lambda c,n:DARK))
    skin=smooth_join(parts, voxel=0.0035, faces=60000)
    for sx in (1,-1):
        e=sphere(f'eye{sx}',(0.062*sx,-0.148,0.965),(0.046,0.028,0.058),32); e.data.materials.append(mat(WHITE)); extras.append((e,'CC_Base_Head'))
        p=sphere(f'pupil{sx}',(0.056*sx,-0.176,0.962),(0.0095,0.005,0.012),16); p.data.materials.append(mat('#0a0a0a')); extras.append((p,'CC_Base_Head'))
    n_=sphere('nose',(0,-0.214,0.878),(0.024,0.018,0.018),24); n_.data.materials.append(mat(NOSE)); extras.append((n_,'CC_Base_Head'))
    J=std_joints(hip=(0,0,0.38),waist_top=(0,0,0.72),neck=(0,0,0.74),head=(0,0,0.78),head_top=(0,0,1.1),
                 sh=(0.15,0,0.72),el=(0.168,0,0.57),wr=(0.178,0,0.42),hand_end=(0.182,0,0.34),
                 th=(0.075,0,0.37),kn=(0.075,0,0.21),an=(0.075,0,0.06),toe=(0.075,-0.1,0.02))

arm=rig(J)
skin_auto(skin, arm)
for o,b in extras: rigid(o,b,arm)
for o in scene.objects: o.select_set(False)
bpy.ops.wm.save_as_mainfile(filepath=W+f'/stage5_{who}.blend')
allp=[skin]+[o for o,_ in extras]
shoot(allp, W+f'/toon_{who}_f.png', azim=0, elev=5, color_type='MATERIAL')
shoot(allp, W+f'/toon_{who}_34.png', azim=35, elev=10, color_type='MATERIAL')
shoot(allp, W+f'/toon_{who}_side.png', azim=90, elev=5, color_type='MATERIAL')
walk_pose(arm) if False else None
print('toon',who,'done')
