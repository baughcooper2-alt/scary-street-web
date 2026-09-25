# Shared helpers for the character build (run inside Blender).
import bpy, mathutils, math, numpy as np
from mathutils.bvhtree import BVHTree

def sel(o):
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active=o

def mesh_co(o):
    a=np.empty(len(o.data.vertices)*3); o.data.vertices.foreach_get('co',a); return a.reshape(-1,3)
def set_co(o,a):
    o.data.vertices.foreach_set('co',a.reshape(-1).astype(np.float64)); o.data.update()
def eval_co(o):
    dg=bpy.context.evaluated_depsgraph_get(); e=o.evaluated_get(dg); m=e.to_mesh()
    a=np.empty(len(m.vertices)*3); m.vertices.foreach_get('co',a); e.to_mesh_clear(); return a.reshape(-1,3)

def aim_bone(arm, name, target_dir, straighten_children=()):
    """Rotate a pose bone about its head so it points along target_dir (armature space)."""
    pb=arm.pose.bones[name]
    bpy.context.view_layer.update()
    head=pb.head.copy(); d=(pb.tail-pb.head).normalized()
    q=d.rotation_difference(mathutils.Vector(target_dir).normalized())
    pb.matrix=mathutils.Matrix.Translation(head)@q.to_matrix().to_4x4()@mathutils.Matrix.Translation(-head)@pb.matrix
    bpy.context.view_layer.update()

def bake_pose_as_rest(arm, meshes):
    """Apply the current pose to every mesh (keeping shape keys) and make it the new rest pose."""
    bpy.context.view_layer.update()
    for m in meshes:
        mod=next((md for md in m.modifiers if md.type=='ARMATURE'),None)
        if not mod: continue
        keys=m.data.shape_keys
        if keys and len(keys.key_blocks)>1:
            kb=keys.key_blocks
            for k in kb: k.value=0
            base=eval_co(m); deltas={}
            for k in kb[1:]:
                k.value=1; deltas[k.name]=eval_co(m)-base; k.value=0
            names=[k.name for k in kb[1:]]
            m.shape_key_clear()
            sel(m); bpy.ops.object.modifier_apply(modifier=mod.name)
            m.shape_key_add(name='Basis')
            for n in names:
                k=m.shape_key_add(name=n, from_mix=False); k.value=0; co=np.array(base+deltas[n])
                k.data.foreach_set('co',co.reshape(-1))
        else:
            sel(m); bpy.ops.object.modifier_apply(modifier=mod.name)
        md=m.modifiers.new('Armature','ARMATURE'); md.object=arm
    sel(arm); bpy.ops.object.mode_set(mode='POSE'); bpy.ops.pose.armature_apply(selected=False); bpy.ops.object.mode_set(mode='OBJECT')

def weights(o):
    """(n_verts, n_groups) weight matrix and group names."""
    names=[g.name for g in o.vertex_groups]; W=np.zeros((len(o.data.vertices),len(names)),np.float32)
    for v in o.data.vertices:
        for g in v.groups: W[v.index,g.group]=g.weight
    return W,names

def adjacency(o):
    e=np.empty(len(o.data.edges)*2,np.int64); o.data.edges.foreach_get('vertices',e); return e.reshape(-1,2)

def smooth(co, edges, mask, iters=6, lam=0.5, mu=-0.53):
    """Taubin smoothing (keeps volume), blended per vertex by mask 0..1."""
    n=len(co); deg=np.bincount(edges.ravel(),minlength=n).astype(np.float64); deg[deg==0]=1
    for i in range(iters):
        for f in (lam,mu):
            s=np.zeros_like(co); np.add.at(s,edges[:,0],co[edges[:,1]]); np.add.at(s,edges[:,1],co[edges[:,0]])
            lap=s/deg[:,None]-co; co=co+f*lap*mask[:,None]
    return co

def bvh_of(o, evaluated=False):
    if evaluated:
        dg=bpy.context.evaluated_depsgraph_get(); return BVHTree.FromObject(o,dg)
    import bmesh
    bm=bmesh.new(); bm.from_mesh(o.data); bm.transform(o.matrix_world); t=BVHTree.FromBMesh(bm); bm.free(); return t

def push_out(garment, body_bvh, offset, passes=4, smooth_iters=12):
    """Move garment vertices that sit inside (or too close to) the body out to `offset`, smoothing the push over
    the cloth so folds survive instead of getting dented."""
    co=mesh_co(garment); edges=adjacency(garment); n=len(co)
    deg=np.bincount(edges.ravel(),minlength=n).astype(np.float64); deg[deg==0]=1
    for p in range(passes):
        D=np.zeros_like(co); need=0
        for i,v in enumerate(co):
            loc,nor,idx,dist=body_bvh.find_nearest(mathutils.Vector(v))
            if loc is None: continue
            d=(mathutils.Vector(v)-loc).dot(nor)
            if d<offset:
                D[i]=np.array(nor)*(offset-d); need+=1
        raw=D.copy()
        for it in range(smooth_iters):
            s=np.zeros_like(D); np.add.at(s,edges[:,0],D[edges[:,1]]); np.add.at(s,edges[:,1],D[edges[:,0]])
            D=0.5*D+0.5*s/deg[:,None]
            big=np.linalg.norm(raw,axis=1)>np.linalg.norm(D,axis=1)   # never less than what a vertex itself needs
            D[big]=raw[big]
        co=co+D
        print('  push pass',p,'inside/too close:',need)
        if need==0: break
    set_co(garment,co)

def transfer_weights(src, dst, arm):
    md=dst.modifiers.new('dt','DATA_TRANSFER'); md.object=src
    md.use_vert_data=True; md.data_types_verts={'VGROUP_WEIGHTS'}; md.vert_mapping='POLYINTERP_NEAREST'
    md.layers_vgroup_select_src='ALL'; md.layers_vgroup_select_dst='NAME'
    sel(dst)
    bpy.ops.object.datalayout_transfer(modifier='dt')
    bpy.ops.object.modifier_apply(modifier='dt')
    bpy.ops.object.mode_set(mode='WEIGHT_PAINT')
    bpy.ops.object.vertex_group_smooth(group_select_mode='ALL', factor=0.5, repeat=4)
    bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    dst.parent=arm; dst.matrix_parent_inverse=mathutils.Matrix.Identity(4)
    m=dst.modifiers.new('Armature','ARMATURE'); m.object=arm

def group_sum(o, prefixes):
    Wt,names=weights(o)
    cols=[i for i,n in enumerate(names) if any(n.startswith(p) for p in prefixes)]
    return Wt[:,cols].sum(1) if cols else np.zeros(len(o.data.vertices))

def delete_verts(o, mask):
    import bmesh
    bm=bmesh.new(); bm.from_mesh(o.data); bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.verts[i] for i in np.nonzero(mask)[0]], context='VERTS')
    bm.to_mesh(o.data); bm.free(); o.data.update()

def join(objs, name):
    sel(objs[0])
    for o in objs[1:]: o.select_set(True)
    bpy.ops.object.join(); objs[0].name=name; return objs[0]

def material(o, name, color):
    m=bpy.data.materials.get(name) or bpy.data.materials.new(name); m.diffuse_color=color
    o.data.materials.clear(); o.data.materials.append(m)
    for p in o.data.polygons: p.material_index=0

ARM_PREFIX=['CC_Base_{s}_Upperarm','CC_Base_{s}_Forearm','CC_Base_{s}_Hand','CC_Base_{s}_Elbow']
def fix_sleeve_weights(garment, body, armpit_z=1.40):
    """Sleeves follow the arm: garment verts below the armpit that sit closer to an arm than to the torso copy
    the weights of the nearest arm vertex (plain nearest-surface transfer lets the inner sleeve follow the ribs)."""
    from mathutils.kdtree import KDTree
    Bw,bn=weights(body); bco=mesh_co(body)
    gco=mesh_co(garment)
    for s in ('L','R'):
        cols=[i for i,n in enumerate(bn) if any(n.startswith(p.format(s=s)) for p in ARM_PREFIX)]
        armness=Bw[:,cols].sum(1)
        A=np.nonzero(armness>0.7)[0]; T=np.nonzero(armness<0.1)[0]
        ka=KDTree(len(A)); [ka.insert(bco[i],j) for j,i in enumerate(A)]; ka.balance()
        kt=KDTree(len(T)); [kt.insert(bco[i],j) for j,i in enumerate(T)]; kt.balance()
        sx=1 if s=='L' else -1
        gnames=[g.name for g in garment.vertex_groups]
        changed=0
        for gi,v in enumerate(gco):
            if v[2]>armpit_z or v[0]*sx<0.08: continue
            (pa,ja,da)=ka.find(v); (pt,jt,dt)=kt.find(v)
            if da<dt+0.02:
                src=A[ja]
                for g in list(garment.vertex_groups): g.remove([gi])
                for k in np.nonzero(Bw[src]>0.001)[0]:
                    name=bn[k]
                    g=garment.vertex_groups.get(name) or garment.vertex_groups.new(name=name)
                    g.add([gi], float(Bw[src,k]), 'REPLACE')
                changed+=1
        print('  sleeve weights',s,changed)
    sel(garment); bpy.ops.object.mode_set(mode='WEIGHT_PAINT')
    bpy.ops.object.vertex_group_smooth(group_select_mode='ALL', factor=0.4, repeat=2)
    bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
    bpy.ops.object.mode_set(mode='OBJECT')

def walk_pose(arm):
    import math
    s7=math.sin(math.radians(7))
    def d(a): return math.sin(math.radians(a)), math.cos(math.radians(a))
    def aim(n,x,fw):  # fw: degrees forward of straight down (negative = back)
        sn,cs=d(fw); aim_bone(arm,n,(x,-sn,-cs))
    aim('CC_Base_L_Thigh',0.05,30); aim('CC_Base_L_Calf',0.05,-20); aim('CC_Base_L_Foot',0.0,-80)
    aim('CC_Base_R_Thigh',-0.05,-20); aim('CC_Base_R_Calf',-0.05,-35)
    aim('CC_Base_L_Upperarm',s7,-35); aim('CC_Base_L_Forearm',s7,-10)
    aim('CC_Base_R_Upperarm',-s7,40); aim('CC_Base_R_Forearm',-0.1,85)

def islands(o):
    """Connected-component label per vertex."""
    e=adjacency(o); n=len(o.data.vertices); p=np.arange(n)
    def find(a):
        while p[a]!=a: p[a]=p[p[a]]; a=p[a]
        return a
    for a,b in e:
        ra,rb=find(a),find(b)
        if ra!=rb: p[ra]=rb
    return np.array([find(i) for i in range(n)])

def uv_islands(o):
    """Panel label per face: faces joined across an edge only where their UVs agree (garment panels)."""
    import bmesh
    bm=bmesh.new(); bm.from_mesh(o.data); uv=bm.loops.layers.uv.active
    bm.faces.ensure_lookup_table(); n=len(bm.faces); p=list(range(n))
    def find(a):
        while p[a]!=a: p[a]=p[p[a]]; a=p[a]
        return a
    for e in bm.edges:
        if len(e.link_faces)!=2: continue
        f1,f2=e.link_faces
        def uvs(f):
            d={}
            for l in f.loops: d[l.vert.index]=l[uv].uv.copy()
            return d
        a,b=uvs(f1),uvs(f2)
        if all((a[v.index]-b[v.index]).length<1e-4 for v in e.verts):
            ra,rb=find(f1.index),find(f2.index)
            if ra!=rb: p[ra]=rb
    lab=np.array([find(i) for i in range(n)]); bm.free(); return lab

def panel_vertex_sets(o):
    """Sleeve vertices per side, torso vertices, and seam vertices, from the garment's UV panels."""
    lab=uv_islands(o); c=mesh_co(o)
    fc=np.array([c[list(p.vertices)].mean(0) for p in o.data.polygons])
    sleeve={}; torso=set()
    for pid in np.unique(lab):
        m=lab==pid; mx=fc[m,0].mean()
        verts=set(v for fi in np.nonzero(m)[0] for v in o.data.polygons[fi].vertices)
        if abs(mx)>0.19: sleeve.setdefault('L' if mx>0 else 'R',set()).update(verts)
        else: torso|=verts
    seam=set()
    for s in sleeve: seam|=sleeve[s]&torso
    return sleeve, torso, seam

def copy_weights_from(garment, gi, Bw, bn, src):
    for g in garment.vertex_groups: g.remove([gi])
    for k in np.nonzero(Bw[src]>0.001)[0]:
        g=garment.vertex_groups.get(bn[k]) or garment.vertex_groups.new(name=bn[k])
        g.add([gi], float(Bw[src,k]), 'REPLACE')

def panel_weights(garment, body, armpit_z=1.40):
    """Sleeve panels follow their arm; torso panels (below the armpit) follow the torso; seams keep the blend."""
    from mathutils.kdtree import KDTree
    sleeve, torso, seam = panel_vertex_sets(garment)
    Bw,bn=weights(body); bco=mesh_co(body); gco=mesh_co(garment)
    def armness(s): return Bw[:,[i for i,n in enumerate(bn) if any(n.startswith(p.format(s=s)) for p in ARM_PREFIX)]].sum(1)
    aL,aR=armness('L'),armness('R')
    def tree(idx):
        t=KDTree(len(idx)); [t.insert(bco[i],j) for j,i in enumerate(idx)]; t.balance(); return t,idx
    trees={s:tree(np.nonzero((aL if s=='L' else aR)>0.6)[0]) for s in ('L','R')}
    ttree=tree(np.nonzero((aL+aR)<0.05)[0])
    n=0
    for s,verts in sleeve.items():
        t,idx=trees[s]
        for gi in verts-seam:
            _,j,_=t.find(gco[gi]); copy_weights_from(garment,gi,Bw,bn,idx[j]); n+=1
    t,idx=ttree
    for gi in torso-seam:
        if gco[gi][2]<armpit_z:
            _,j,_=t.find(gco[gi]); copy_weights_from(garment,gi,Bw,bn,idx[j]); n+=1
    print('  panel weights set',n,'sleeve panels',{k:len(v) for k,v in sleeve.items()},'seam',len(seam))
    sel(garment); bpy.ops.object.mode_set(mode='WEIGHT_PAINT')
    bpy.ops.object.vertex_group_smooth(group_select_mode='ALL', factor=0.4, repeat=2)
    bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    return sleeve, seam

def shape_face(body, who, eyes, eyeZ, mouthZ, chinZ, cy):
    """Per-character face: Nathan narrow and long with a stronger nose and slimmer cheeks; Cooper broader."""
    co=mesh_co(body); d=np.zeros_like(co)
    def ss(a,b,x): t=np.clip((x-a)/(b-a),0,1); return t*t*(3-2*t)
    x,y,z=co[:,0],co[:,1],co[:,2]
    front=ss(cy+0.02,cy-0.05,y)
    lower=ss(eyeZ-0.005,eyeZ-0.03,z)*ss(chinZ-0.02,chinZ+0.005,z)
    def blob(c,r):
        dist=np.linalg.norm(co-np.array(c),axis=1); return np.clip(1-dist/r,0,1)**2
    if who=='nathan':
        d[:,0]+= -x*0.08*lower*front                                            # narrower cheeks and jaw
        d[:,2]+= -0.007*ss(mouthZ-0.01,chinZ,z)*front*(np.abs(x)<0.05)*ss(chinZ-0.02,chinZ,z)   # longer chin
        nose=blob((0,-0.105,mouthZ+0.035),0.026); d[:,1]-=0.0045*nose; d[:,2]-=0.0015*nose       # longer, stronger nose
        bridge=blob((0,-0.092,eyeZ-0.005),0.016); d[:,1]-=0.0022*bridge
        for sx in (1,-1):
            ch=blob((0.045*sx,-0.072,mouthZ+0.02),0.026); d[:,0]-=0.0028*sx*ch; d[:,1]+=0.0012*ch   # hollower cheeks
    else:
        d[:,0]+= x*0.035*lower*front                                            # broader jaw
        for sx in (1,-1):
            ch=blob((0.045*sx,-0.072,mouthZ+0.02),0.026); d[:,1]-=0.0015*ch      # fuller cheeks
    kb=body.data.shape_keys.key_blocks if body.data.shape_keys else []
    for k in kb:
        a=np.array([v.co[:] for v in k.data]); k.data.foreach_set('co',(a+d).reshape(-1))
    set_co(body,co+d)
    print('  face shaped',who,'max move',round(float(np.linalg.norm(d,axis=1).max()),4))

def coils(points, normals, seed=3, forehead=None):
    """Little 3D curls: a helix tube hanging from each scalp point (for curly hair that reads from a distance)."""
    import bmesh, random
    rnd=random.Random(seed); bm=bmesh.new()
    for i,(p,n) in enumerate(zip(points,normals)):
        n=mathutils.Vector(n).normalized(); down=mathutils.Vector((0,0,-1))
        a=(down-n*down.dot(n)); a=a.normalized() if a.length>1e-4 else down
        short=forehead is not None and forehead[i]
        a=(a+n*rnd.uniform(0.25,0.55)+mathutils.Vector((rnd.uniform(-.3,.3),rnd.uniform(-.3,.3),0))).normalized()
        L=rnd.uniform(0.012,0.02) if short else rnd.uniform(0.02,0.036)
        R=rnd.uniform(0.0055,0.0085); tube=rnd.uniform(0.0022,0.003); pitch=rnd.uniform(0.007,0.01)
        u=a.orthogonal().normalized(); v=a.cross(u)
        start=mathutils.Vector(p)+n*0.003; ph=rnd.uniform(0,6.28)
        steps=max(6,int(L/pitch*7)); rings=[]
        for s in range(steps+1):
            t=s/steps; ang=ph+t*L/pitch*6.283
            c=start+a*(t*L)+(u*math.cos(ang)+v*math.sin(ang))*R*(0.6+0.4*t)
            tang=(a*(L/steps)+(u*-math.sin(ang)+v*math.cos(ang))*R*(6.283*L/pitch/steps)).normalized()
            q1=tang.orthogonal().normalized(); q2=tang.cross(q1)
            ring=[bm.verts.new(c+(q1*math.cos(k*6.283/5)+q2*math.sin(k*6.283/5))*tube*(1-0.5*t)) for k in range(5)]
            rings.append(ring)
        for s in range(steps):
            for k in range(5):
                bm.faces.new((rings[s][k],rings[s][(k+1)%5],rings[s+1][(k+1)%5],rings[s+1][k]))
    me=bpy.data.meshes.new('Curls'); bm.to_mesh(me); bm.free()
    o=bpy.data.objects.new('Curls',me); bpy.context.scene.collection.objects.link(o); return o
