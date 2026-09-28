# Per-character body and outfit helpers for the real-body pipeline (stage3 / stage4): a female build from the
# CG Cookie base mesh, arm length, strand hair, hoods, socks, a nose ring and real sneakers (the Nike scan).
import bpy, bmesh, mathutils, math, numpy as np
from cclib import *

ARM_KEYS=('Upperarm','Forearm','Elbow','Hand','Thumb','Index','Mid','Ring','Pinky')

def _arm_bones(arm, side):
    return [b.name for b in arm.data.bones if b.name.startswith(f'CC_Base_{side}_') and any(k in b.name for k in ARM_KEYS)]

def move_arms(body, arm, fn, extra=()):
    """Move each arm (bones + the skin weighted to them, blended by weight) through fn(side, point, S, H) -> point,
    where S is the shoulder (upper-arm head) and H the wrist. Shape keys and `extra` meshes (weighted the same way)
    move with it. Run before the clothes are fitted."""
    heads={}
    for side in ('L','R'):
        S=arm.data.bones[f'CC_Base_{side}_Upperarm'].head_local.copy(); H=arm.data.bones[f'CC_Base_{side}_Hand'].head_local.copy()
        heads[side]=(S,H)
    for o in (body,)+tuple(extra):
        Wt,names=weights(o); co=mesh_co(o); d=np.zeros_like(co)
        for side in ('L','R'):
            S,H=heads[side]
            cols=[i for i,n in enumerate(names) if n.startswith(f'CC_Base_{side}_') and any(k in n for k in ARM_KEYS)]
            w=np.clip(Wt[:,cols].sum(1),0,1) if cols else np.zeros(len(co))
            for i in np.nonzero(w>1e-4)[0]:
                p=mathutils.Vector(co[i]); d[i]+=w[i]*np.array(fn(side,p,S,H)-p)
        kb=o.data.shape_keys.key_blocks if o.data.shape_keys else []
        for k in kb:
            a=np.array([v.co[:] for v in k.data]); k.data.foreach_set('co',(a+d).reshape(-1))
        set_co(o,co+d)
    # the bones: remember every head / tail first (connected bones share points), then place them
    sel(arm); bpy.ops.object.mode_set(mode='EDIT')
    eb=arm.data.edit_bones; orig={b.name:(b.head.copy(),b.tail.copy()) for b in eb}
    for side in ('L','R'):
        S,H=heads[side]
        for n in _arm_bones(arm, side):
            h,t=orig[n]; eb[n].head=fn(side,h,S,H); eb[n].tail=fn(side,t,S,H)
        cl=eb.get(f'CC_Base_{side}_Clavicle')
        if cl and (orig[cl.name][1]-orig[f'CC_Base_{side}_Upperarm'][0]).length<0.02: cl.tail=eb[f'CC_Base_{side}_Upperarm'].head
    bpy.ops.object.mode_set(mode='OBJECT')

def arm_length(body, arm, k):
    """Longer (k > 1) or shorter arms: stretch shoulder → wrist, the hand moves with the wrist."""
    if abs(k-1)<1e-4: return
    def fn(side,p,S,H):
        L=(H-S).length; a=(H-S)/L; t=min(max((p-S).dot(a),0.0),L)
        return p+a*t*(k-1)
    move_arms(body, arm, fn); print('  arm length x',k)

def shift_arms(body, arm, dx):
    """Arms (and shoulders) in toward the body by dx metres each side (narrower shoulders)."""
    if abs(dx)<1e-5: return
    def fn(side,p,S,H): return p+mathutils.Vector((-dx if side=='L' else dx,0,0))
    move_arms(body, arm, fn); print('  arms in by',round(dx,4))

# ---------------------------------------------------------------------------------------------------------------
# female build: the CG Cookie base mesh (CC-BY 3.0, A-pose) as a shape target for the torso, hips and legs

SHIFT=0.0
def nor_y(o):
    n=np.empty(len(o.data.vertices)*3); o.data.vertices.foreach_get('normal',n); return n.reshape(-1,3)[:,1]

def female_shape(body, arm, W):
    """The torso matched to the female base mesh slice by slice (width, and the front / back surface found with rays
    straight through it: chest, waist, belly, back), then rounder hips by hand. Arms, head, hands and feet stay."""
    with bpy.data.libraries.load(W+'/female_base.blend') as (src,dst): dst.objects=['female_body_basemesh']
    fo=dst.objects[0]; bpy.context.scene.collection.objects.link(fo)
    dg=bpy.context.evaluated_depsgraph_get(); fme=bpy.data.meshes.new_from_object(fo.evaluated_get(dg))
    F=np.array([fo.matrix_world@v.co for v in fme.vertices]); bpy.data.objects.remove(fo)
    co=mesh_co(body); H=co[:,2].max()
    def crotch(P,h): s_=P[(np.abs(P[:,0])<0.012*h)&(P[:,2]>0.3*h)]; return float(s_[:,2].min())
    Fh=F[:,2].max(); F=F*(H/Fh)                                           # same height
    cf,cc_=crotch(F,H),crotch(co,H)
    z=F[:,2]; F[:,2]=np.where(z<cf, z*cc_/cf, cc_+(z-cf)*(H-cc_)/(H-cf))    # crotch at the same height
    band=lambda P:(P[:,2]>0.55*H)&(P[:,2]<0.72*H)
    F[:,1]+=co[band(co),1].mean()-F[band(F),1].mean()                      # torso centred front to back
    bm=bmesh.new(); bm.from_mesh(fme)
    for v,p in zip(bm.verts,F): v.co=mathutils.Vector(p)
    tgt=BVHTree.FromBMesh(bm); bm.free(); bpy.data.meshes.remove(fme)
    print('  female target: crotch',round(cf,3),'->',round(cc_,3))

    Wt,names=weights(body)
    def fam(keys): cols=[i for i,n in enumerate(names) if any(k in n for k in keys)]; return np.clip(Wt[:,cols].sum(1),0,1) if cols else np.zeros(len(co))
    armw=fam(ARM_KEYS)
    skip=np.clip(armw+fam(('Head','Neck','Facial','Jaw','Eye','Tongue','Teeth'))+fam(('Foot','Toe')),0,1)
    zc=co[:,2]; z0,z1=cc_+0.17, 0.755*H                                   # above the target's underwear, below the armpits
    m=(1-skip)*np.clip((z1-zc)/0.04,0,1)*np.clip((zc-z0)/0.05,0,1)
    # 1) width, slice by slice (the target's arms are out in an A, so only |x| < 0.2 counts as torso)
    bands=np.linspace(z0-0.05,z1+0.05,36)
    def halfw(P,sel):
        out=[]
        for b0,b1 in zip(bands[:-1],bands[1:]):
            q=P[sel&(P[:,2]>=b0)&(P[:,2]<b1)]
            out.append(np.percentile(np.abs(q[:,0]),96) if len(q)>8 else np.nan)
        return np.array(out)
    hf=halfw(F,np.abs(F[:,0])<0.2); hc=halfw(co,armw<0.2)
    r=np.where(np.isnan(hf/hc),1.0,np.clip(hf/hc,0.82,1.2))
    r=np.convolve(np.pad(r,2,mode='edge'),np.ones(5)/5,mode='valid')
    mid=(bands[:-1]+bands[1:])/2
    r=1+(r-1)*np.clip((z1-0.12-mid)/0.06,0,1)                           # no narrowing up at the armpits (the sleeves sit there)
    rx=np.interp(zc,mid,r)
    d=np.zeros_like(co); d[:,0]=(co[:,0]*rx-co[:,0])
    # 2) a soft, smooth front: the male chest and ab definition smoothed away over the torso
    front=np.clip(-nor_y(body)/0.3,0,1)*m
    sm_=smooth(co+d, adjacency(body), front*0.9, iters=16); d=sm_-co
    # 3) breasts, on the rig's own breast bones: a round bulge forward and a little down
    for side in ('L','R'):
        b=arm.data.bones.get(f'CC_Base_{side}_Breast')
        if not b: continue
        c=np.array(b.head_local)+np.array([0,-0.02,0.008])
        r2=((co[:,0]-c[0])**2+((co[:,2]-c[2])*1.05)**2)/(0.07**2)
        bul=0.034*np.clip(1-r2,0,1)**1.6*np.clip(-nor_y(body)/0.2,0,1)*(1-skip)     # a round dome, not a point
        d[:,1]-=bul; d[:,2]-=0.1*bul
    d=smooth(d, adjacency(body), np.ones(len(co)), iters=4)*np.clip(m+0.0,0,1)[:,None]
    kb=body.data.shape_keys.key_blocks if body.data.shape_keys else []
    for k in kb:
        a=np.array([v.co[:] for v in k.data]); k.data.foreach_set('co',(a+d).reshape(-1))
    set_co(body,co+d)
    print('  female shape: width ratio',np.round(r[::6],2),'moved',int((np.linalg.norm(d,axis=1)>0.002).sum()),'verts, max',round(float(np.linalg.norm(d,axis=1).max()),4))
    # 3) hips: wider and rounder at the pelvis, fuller at the back
    co=mesh_co(body); zc=co[:,2]
    hip=np.clip(1-armw,0,1)*np.exp(-((zc-(cc_+0.06))/0.1)**2)
    d2=np.zeros_like(co); d2[:,0]=co[:,0]*0.075*hip
    d2[:,1]+=0.012*hip*np.clip(co[:,1]/0.05,0,1)
    for k in kb:
        a=np.array([v.co[:] for v in k.data]); k.data.foreach_set('co',(a+d2).reshape(-1))
    set_co(body,co+d2)
    if SHIFT: shift_arms(body, arm, SHIFT)

def wrap_follow(garment, co0, co1, k=6):
    """Move a garment the way the body under it moved (co0 → co1, the body's vertices before / after reshaping):
    each garment vertex takes the inverse-distance average of its k nearest body vertices' displacement, then the
    field is smoothed, so the clothes stay tailored to a slimmer, thicker, female or long-armed build."""
    from mathutils.kdtree import KDTree
    D=co1-co0
    if np.abs(D).max()<1e-5: return
    kt=KDTree(len(co0)); [kt.insert(v,i) for i,v in enumerate(co0)]; kt.balance()
    g=mesh_co(garment); out=np.zeros_like(g)
    for i,p in enumerate(g):
        hits=kt.find_n(p,k); w=np.array([1/(d*d+1e-6) for _,_,d in hits]); idx=[j for _,j,_ in hits]
        out[i]=(D[idx]*w[:,None]).sum(0)/w.sum()
    out=smooth(out, adjacency(garment), np.ones(len(g)), iters=4)
    set_co(garment, g+out); print('  wrapped',garment.name,'max',round(float(np.linalg.norm(out,axis=1).max()),4))

# ---------------------------------------------------------------------------------------------------------------
# hair strands: tapered, flattened clumps grown from the scalp, lying on the head / shoulders

def strands(roots, normals, surfs, name, step=0.01, length=(0.03,0.05), grow=None, gravity=0.0, wave=(0.0,0.1),
            width=(0.008,0.012), thick=0.3, layer=(0.002,0.01), keepout=None, stop_z=None, seed=1, sides=6, uv_len=0.4, hug=True, hug_len=1.0):
    """roots/normals: scalp points; surfs: BVH trees to stay outside of; grow(p, n, rnd) -> start direction;
    gravity: pull down per step; wave: (amplitude, wavelength) sideways; width: clump width root → (taper to a point);
    thick: thickness / width; layer: how far off the surface each clump sits (volume); keepout(p) -> pushed point
    (keep the face clear); stop_z(i) -> tip height for long hair. UV v runs 0 at the root to 1 at uv_len metres."""
    rnd=np.random.default_rng(seed); bm=bmesh.new(); uvl=bm.loops.layers.uv.new()
    down=mathutils.Vector((0,0,-1)); made=0
    for i,(p0,n0) in enumerate(zip(roots,normals)):
        p0=mathutils.Vector(p0); n0=mathutils.Vector(n0).normalized()
        d=grow(p0,n0,rnd) if grow else n0
        L=rnd.uniform(*length); off=rnd.uniform(*layer); tipz=stop_z(i) if stop_z else None
        pts=[p0+n0*0.001]; p=pts[0]; s=0.0
        while s<L and len(pts)<120:
            d=(d+down*gravity).normalized(); q=p+d*step
            for ti,t in enumerate(surfs):
                loc,nrm,_,dist=t.find_nearest(q, off+0.03)
                if loc is None: continue
                sd=(q-loc).dot(nrm)
                # outside every surface; along the head (the first) while it's close to the root, then hanging free
                if sd<off or (hug and ti==0 and s<hug_len and sd>off): q=loc+nrm*off
            if keepout: q=keepout(q)
            d=(q-p).normalized() if (q-p).length>1e-6 else d
            s+=(q-p).length; pts.append(q); p=q
            if tipz is not None and q.z<tipz: break
        if len(pts)<3: continue
        # sideways wave (grows from the root), on the plane of the surface
        amp,wl=wave; ph=rnd.uniform(0,6.283)
        if amp>0:
            acc=0.0; out=[pts[0]]
            for k in range(1,len(pts)):
                t=(pts[k]-pts[k-1]); acc+=t.length; tn=t.normalized()
                side=tn.cross(n0)
                side=side.normalized() if side.length>1e-6 else tn.orthogonal().normalized()
                out.append(pts[k]+side*amp*math.sin(6.283*acc/wl+ph)*min(1.0,acc/0.05))
            pts=out
        # rings: an ellipse flat against the surface, tapering to the tip
        w0=rnd.uniform(*width); rings=[]; n=len(pts); acc=0.0
        for k in range(n):
            t=(pts[min(k+1,n-1)]-pts[max(k-1,0)]).normalized()
            if k>0: acc+=(pts[k]-pts[k-1]).length
            ref=n0
            for tr in surfs[:1]:
                loc,nrm,_,_=tr.find_nearest(pts[k], 0.1)
                if loc is not None: ref=nrm
            b=(ref-t*ref.dot(t)); b=b.normalized() if b.length>1e-6 else t.orthogonal().normalized()
            a=t.cross(b).normalized()
            u=k/(n-1); w=w0*(1-0.85*u**1.6)*(0.7+0.3*min(1,k/2))
            ring=[bm.verts.new(pts[k]+a*math.cos(j*6.283/sides)*w/2+b*math.sin(j*6.283/sides)*w*thick/2) for j in range(sides)]
            rings.append((ring,min(1.0,acc/uv_len)))
        for k in range(n-1):
            (r0,v0),(r1,v1)=rings[k],rings[k+1]
            for j in range(sides):
                f=bm.faces.new((r0[j],r0[(j+1)%sides],r1[(j+1)%sides],r1[j]))
                for lp,(uu,vv) in zip(f.loops,((j/sides,v0),((j+1)/sides,v0),((j+1)/sides,v1),(j/sides,v1))): lp[uvl].uv=(uu,vv)
        made+=1
    me=bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o=bpy.data.objects.new(name,me); bpy.context.scene.collection.objects.link(o)
    print('  strands',name,made,'verts',len(me.vertices)); return o

def fuse(o, voxel=0.004, smooth_iters=6, faces=24000, v_of=None):
    """Melt overlapping strands into one smooth volume (voxel remesh), soften it, thin it to about `faces` faces,
    and give it UVs (v from v_of(point), u = 0.5) for the gradient material."""
    sel(o)
    md=o.modifiers.new('r','REMESH'); md.mode='VOXEL'; md.voxel_size=voxel; md.use_smooth_shade=True
    bpy.ops.object.modifier_apply(modifier='r')
    md=o.modifiers.new('s','LAPLACIANSMOOTH'); md.iterations=smooth_iters; md.lambda_factor=0.6; md.use_volume_preserve=True
    bpy.ops.object.modifier_apply(modifier='s')
    n=len(o.data.polygons)
    if n>faces:
        md=o.modifiers.new('d','DECIMATE'); md.ratio=faces/n; bpy.ops.object.modifier_apply(modifier='d')
    me=o.data
    uv=me.uv_layers.new(name='UVMap') if not me.uv_layers else me.uv_layers[0]
    for poly in me.polygons:
        for li in poly.loop_indices:
            p=me.vertices[me.loops[li].vertex_index].co
            uv.data[li].uv=(0.5, v_of(p) if v_of else 0.5)
    bpy.ops.object.shade_smooth(); print('  fused',o.name,'faces',len(me.polygons)); return o

def hair_curtain(head_bvh, surfs, crown, eyeZ, chinZ, tip_z, rng_seed=3, guides=72, rows=44, offset=(0.006,0.02), thick=0.012,
                 face_half=0.075, name='HairCurtain'):
    """Long hair as one continuous surface: guides run from the crown down each meridian of the head (hugging it,
    getting a little fuller as they go), hang to tip_z below the head, and a surface is lofted between neighbours and
    given thickness. The front sector stops at the hairline so the face stays clear; the guides at its edges sweep
    to the sides to frame the face. UV v = distance from the crown (for the gradient)."""
    rnd=np.random.default_rng(rng_seed); down=mathutils.Vector((0,0,-1))
    lines=[]
    for gi in range(guides+1):
        phi=math.radians(-150+300*gi/guides)                                  # 0 = the back of the head, ±150 = the temples
        d=mathutils.Vector((math.sin(phi),math.cos(phi),0)).normalized()
        front=abs(phi)>math.radians(118)                                        # over the forehead / temples
        p=mathutils.Vector(crown)+d*0.008; pts=[p]; s=0.0; step=0.011
        L=0.62 if not front else 0.2
        dirv=(d*1.0+down*0.15).normalized()
        while s<L and len(pts)<200:
            off=offset[0]+(offset[1]-offset[0])*min(1,s/0.25)
            q=p+dirv*step
            loc,nrm,_,dist=head_bvh.find_nearest(q,0.06)
            hug=loc is not None and s<0.3
            if loc is not None and ((q-loc).dot(nrm)<off or hug): q=loc+nrm*off
            for t in surfs:
                l2,n2,_,_=t.find_nearest(q,off+0.02)
                if l2 is not None and (q-l2).dot(n2)<off: q=l2+n2*off
            if abs(q.x)<face_half and q.y<-0.02 and chinZ-0.04<q.z<eyeZ+0.07:          # keep the face clear
                q=mathutils.Vector((face_half*(1 if q.x>=0 else -1),q.y,q.z))
            dirv=((q-p).normalized()+down*(0.1 if hug else 0.35)).normalized()
            s+=(q-p).length; pts.append(q); p=q
            if front and q.z<eyeZ+0.075: break
            if q.z<tip_z+rnd.uniform(-0.02,0.03): break
        # resample to `rows` points by length
        ac=[0.0]
        for k in range(1,len(pts)): ac.append(ac[-1]+(pts[k]-pts[k-1]).length)
        tot=ac[-1]; res=[]
        for r in range(rows):
            t=tot*r/(rows-1); k=max(1,next((k for k in range(1,len(ac)) if ac[k]>=t),len(ac)-1))
            f=(t-ac[k-1])/max(1e-6,ac[k]-ac[k-1]); res.append((pts[k-1].lerp(pts[k],min(1,max(0,f))),t))
        lines.append(res)
    bm=bmesh.new(); uvl=bm.loops.layers.uv.new(); grid=[]
    for gi,line in enumerate(lines):
        grid.append([bm.verts.new(p) for p,_ in line])
    for gi in range(len(lines)-1):
        for r in range(rows-1):
            vs=(grid[gi][r],grid[gi+1][r],grid[gi+1][r+1],grid[gi][r+1])
            f=bm.faces.new(vs)
            for lp,(gg,rr) in zip(f.loops,((gi,r),(gi+1,r),(gi+1,r+1),(gi,r+1))): lp[uvl].uv=(gg/len(lines), min(1.0,lines[gg][rr][1]/0.35))
    me=bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o=bpy.data.objects.new(name,me); bpy.context.scene.collection.objects.link(o)
    # strand grooves: ripple the surface along its normal, a little wave down its length
    me.update(); co=mesh_co(o)
    nor=np.empty(len(co)*3); me.vertices.foreach_get('normal',nor); nor=nor.reshape(-1,3)
    rows_i=np.arange(len(co))%rows; g_i=np.arange(len(co))//rows
    rip=0.0035*np.sin(g_i*2.1)+0.0025*np.sin(g_i*5.3+rows_i*0.35)
    set_co(o, co+nor*rip[:,None])
    sel(o); md=o.modifiers.new('sub','SUBSURF'); md.levels=1; bpy.ops.object.modifier_apply(modifier='sub')
    md=o.modifiers.new('t','SOLIDIFY'); md.thickness=thick; md.offset=-1; md.use_rim=True; bpy.ops.object.modifier_apply(modifier='t')
    bpy.ops.object.shade_smooth(); print('  hair curtain verts',len(o.data.vertices)); return o

def hair_skirt(shell, head_bvh, surfs, tip_z, eyeZ, chinZ, centre, face_half=0.074, rows=44, thick=0.01, seed=3,
               span=128, cols=90, name='HairSkirt'):
    """Long hair hanging from the head: columns start just inside the scalp shell on a ring above the ears (from the
    back round to `span` degrees either side, so the front ones frame the face), fall outward and down, never moving
    back in toward the head's axis (so it hangs away from the neck like real hair), clear of the face and resting
    on the shoulders, to about tip_z. Lofted into one surface with a ripple of strands, thickened. UV v = distance
    down (for the gradient)."""
    rnd=np.random.default_rng(seed); down=mathutils.Vector((0,0,-1))
    bm=bmesh.new(); bm.from_mesh(shell.data); sh=BVHTree.FromBMesh(bm); bm.free()
    cx,cy=float(centre[0]),float(centre[1]); zr=eyeZ+0.035
    grid=[]
    for ci in range(cols+1):
        phi=math.radians(-span+2*span*ci/cols)                              # 0 = the back
        d=mathutils.Vector((math.sin(phi),math.cos(phi),0))
        loc,_,_,_=sh.ray_cast(mathutils.Vector((cx,cy,zr))+d*0.3,-d,0.4)
        if loc is None: continue
        p=loc-d*0.006; line=[(p,0.0)]; s=0.0; touched=0
        rmin=(mathutils.Vector((p.x-cx,p.y-cy,0))).length
        tz=tip_z+rnd.uniform(-0.015,0.03)*(1 if abs(phi)<math.radians(100) else 0.5)
        dirv=(down*0.85+d*0.22).normalized()
        while len(line)<300:
            q=p+dirv*0.008
            loc,nrm,_,_=head_bvh.find_nearest(q,0.08)
            if loc is not None and (q-loc).dot(nrm)<0.012: q=loc+nrm*0.012
            for t in surfs:
                l2,n2,_,_=t.find_nearest(q,0.03)
                if l2 is not None and (q-l2).dot(n2)<0.01: q=l2+n2*0.01; touched+=1
            h=mathutils.Vector((q.x-cx,q.y-cy,0)); r=h.length
            if r<rmin: q=mathutils.Vector((cx,cy,q.z))+h.normalized()*rmin                    # never back in toward the axis
            if abs(q.x)<face_half and q.y<-0.015 and chinZ-0.05<q.z<eyeZ+0.08:
                q=mathutils.Vector((face_half*(1 if q.x>=0 else -1),q.y,q.z))
            rmin=max(rmin,(mathutils.Vector((q.x-cx,q.y-cy,0))).length)
            dirv=((q-p).normalized()*0.3+down*0.7).normalized()
            s+=(q-p).length; line.append((q,s)); p=q
            if q.z<tz or touched>3: break                                     # ends where it rests on the shoulder
        L=line[-1][1]; res=[]; k=1
        for rr in range(rows):
            t=L*rr/(rows-1)
            while k<len(line)-1 and line[k][1]<t: k+=1
            a_,b_=line[k-1],line[k]; f=(t-a_[1])/max(1e-6,b_[1]-a_[1])
            res.append((a_[0].lerp(b_[0],min(1,max(0,f))),t))
        grid.append(res)
    me_bm=bmesh.new(); uvl=me_bm.loops.layers.uv.new()
    vs=[[me_bm.verts.new(p) for p,_ in col] for col in grid]
    for c in range(len(grid)-1):
        for r in range(rows-1):
            f=me_bm.faces.new((vs[c][r],vs[c][r+1],vs[c+1][r+1],vs[c+1][r]))
            for lp,(cc_,rr) in zip(f.loops,((c,r),(c,r+1),(c+1,r+1),(c+1,r))): lp[uvl].uv=(0.5,min(1.0,0.15+grid[cc_][rr][1]/0.35))
    me=bpy.data.meshes.new(name); me_bm.to_mesh(me); me_bm.free()
    o=bpy.data.objects.new(name,me); bpy.context.scene.collection.objects.link(o)
    me.update(); co=mesh_co(o)
    nor=np.empty(len(co)*3); me.vertices.foreach_get('normal',nor); nor=nor.reshape(-1,3)
    c_i=np.arange(len(co))//rows; r_i=np.arange(len(co))%rows
    rip=(0.0035*np.sin(c_i*1.7)+0.002*np.sin(c_i*4.3+r_i*0.28))*np.clip(r_i/8,0,1)
    wav=0.004*np.sin(r_i*0.45+c_i*0.2)*np.clip(r_i/10,0,1)                                    # soft waves down the length
    set_co(o, co+nor*(rip+wav)[:,None])
    sel(o); md=o.modifiers.new('sub','SUBSURF'); md.levels=1; bpy.ops.object.modifier_apply(modifier='sub')
    md=o.modifiers.new('t','SOLIDIFY'); md.thickness=thick; md.offset=-1; md.use_rim=True; bpy.ops.object.modifier_apply(modifier='t')
    bpy.ops.object.shade_smooth(); print('  hair skirt cols',len(grid),'verts',len(o.data.vertices)); return o

def hair_parted(head_bvh, surfs, tip_z, eyeZ, chinZ, centre, ctop, face_half=0.074, rows=60, thick=0.012, seed=3,
                side_cols=34, back_cols=40, name='HairParted'):
    """Long hair with a middle part as one continuous surface: on each side columns start along the part (front
    hairline back to the crown) and comb sideways over the scalp; round the back they fan out from the crown. Each
    column hugs the head while above the ears, then falls, never swinging back in toward the head's axis (it hangs
    away from the neck), clear of the face, and stops where it rests on the shoulders or at about tip_z. Columns are
    lofted in order (left front → back → right front), rippled into strands, thickened. UV v = distance from the root."""
    rnd=np.random.default_rng(seed); down=mathutils.Vector((0,0,-1))
    cx,cy=float(centre[0]),float(centre[1])
    def surf_pt(x,y):
        loc,_,_,_=head_bvh.ray_cast(mathutils.Vector((x,y,ctop+0.2)),down,0.5); return loc
    starts=[]
    yF,yC=cy-0.1,cy+0.03                                                    # from right at the front hairline (covers the forehead edge)
    for sgn in (1,-1):
        col=[]
        for k in range(side_cols):
            y=yF+(yC-yF)*k/(side_cols-1); p=surf_pt(sgn*0.003,y)
            if p is None: continue
            d=mathutils.Vector((sgn,0.05+0.5*k/side_cols,-0.3+0.15*k/side_cols)).normalized(); col.append((p,d))
        starts.append(col)
    crown=surf_pt(0,yC+0.004)
    back=[]
    for k in range(back_cols+1):
        phi=math.radians(-80+160*k/back_cols)
        d=mathutils.Vector((math.sin(phi),math.cos(phi),-0.1)).normalized(); back.append((crown,d))
    order=starts[1][::-1]+[b for b in back]+starts[0]                       # right side front→crown? (mirror below)
    order=[(p,d) for p,d in starts[0][::-1]]+[(p,mathutils.Vector((-d.x,d.y,d.z))) for p,d in back[::-1]]+[(p,d) for p,d in starts[1]]
    grid=[]
    for p0,d0 in order:
        p=p0.copy(); line=[(p,0.0)]; s=0.0; dirv=d0.copy(); touched=0; rmin=0.0
        front=p0.y<cy-0.04
        tz=tip_z+rnd.uniform(-0.015,0.03)
        while len(line)<400:
            q=p+dirv*0.007
            off=0.009+0.009*min(1,s/0.15)
            loc,nrm,_,_=head_bvh.find_nearest(q,0.1)
            hugging=q.z>eyeZ+0.01
            if loc is not None and ((q-loc).dot(nrm)<off or hugging): q=loc+nrm*off
            for t in surfs:
                l2,n2,_,_=t.find_nearest(q,0.03)
                if l2 is not None and (q-l2).dot(n2)<0.01: q=l2+n2*0.01; touched+=1
            h=mathutils.Vector((q.x-cx,q.y-cy,0)); r=h.length
            if not hugging:
                if r<rmin: q=mathutils.Vector((cx,cy,q.z))+h.normalized()*rmin
                rmin=max(rmin,(mathutils.Vector((q.x-cx,q.y-cy,0))).length)
            else: rmin=r
            if abs(q.x)<face_half and q.y<-0.015 and chinZ-0.05<q.z<eyeZ+0.08:
                q=mathutils.Vector((face_half*(1 if q.x>=0 else -1),q.y,q.z))
            g=0.25 if hugging else 0.7
            dirv=((q-p).normalized()*(1-g)+down*g).normalized()
            s+=(q-p).length; line.append((q,s)); p=q
            if (not hugging and (q.z<tz or touched>3)): break
        # smooth the kinks out of the path (keeping the root in place)
        P_=[q for q,_ in line]
        for _ in range(6): P_=[P_[0]]+[(P_[i-1]+P_[i]*2+P_[i+1])/4 for i in range(1,len(P_)-1)]+[P_[-1]]
        acc=[0.0]
        for i in range(1,len(P_)): acc.append(acc[-1]+(P_[i]-P_[i-1]).length)
        line=list(zip(P_,acc))
        L=line[-1][1]; res=[]; k=1
        for rr in range(rows):
            t=L*rr/(rows-1)
            while k<len(line)-1 and line[k][1]<t: k+=1
            a_,b_=line[k-1],line[k]; f=(t-a_[1])/max(1e-6,b_[1]-a_[1])
            res.append((a_[0].lerp(b_[0],min(1,max(0,f))),t))
        grid.append(res)
    me_bm=bmesh.new(); uvl=me_bm.loops.layers.uv.new()
    vs=[[me_bm.verts.new(p) for p,_ in col] for col in grid]
    for c in range(len(grid)-1):
        for r in range(rows-1):
            f=me_bm.faces.new((vs[c][r],vs[c][r+1],vs[c+1][r+1],vs[c+1][r]))
            for lp,(cc_,rr) in zip(f.loops,((c,r),(c,r+1),(c+1,r+1),(c+1,r))): lp[uvl].uv=(0.5,min(1.0,grid[cc_][rr][1]/0.35))
    me=bpy.data.meshes.new(name); me_bm.to_mesh(me); me_bm.free()
    o=bpy.data.objects.new(name,me); bpy.context.scene.collection.objects.link(o)
    me.update(); co=mesh_co(o)
    nor=np.empty(len(co)*3); me.vertices.foreach_get('normal',nor); nor=nor.reshape(-1,3)
    c_i=np.arange(len(co))//rows; r_i=np.arange(len(co))%rows
    rip=(0.0016*np.sin(c_i*2.3)+0.0009*np.sin(c_i*6.1+r_i*0.28))*np.clip(r_i/5,0,1)        # fine strands, not tubes
    wav=0.004*np.sin(r_i*0.38+c_i*0.15)*np.clip((r_i-18)/10,0,1)                            # soft waves lower down
    set_co(o, co+nor*(rip+wav)[:,None])
    sel(o); md=o.modifiers.new('sub','SUBSURF'); md.levels=1; bpy.ops.object.modifier_apply(modifier='sub')
    md=o.modifiers.new('t','SOLIDIFY'); md.thickness=thick; md.offset=-1; md.use_rim=True; bpy.ops.object.modifier_apply(modifier='t')
    bpy.ops.object.shade_smooth(); print('  parted hair cols',len(grid),'verts',len(o.data.vertices)); return o

def part_and_comb(shell, ctop, hairline, amt=0.0022, spacing=0.011):
    """A middle part (a soft crease along x = 0 on top) and strand grooves combed down to the sides (lines of
    roughly constant y), pressed into the scalp shell."""
    co=mesh_co(shell); nor=np.empty(len(co)*3); shell.data.vertices.foreach_get('normal',nor); nor=nor.reshape(-1,3)
    x,y,z=co[:,0],co[:,1],co[:,2]
    on=np.array([zz>hairline(yy)+0.004 for yy,zz in zip(y,z)])
    part=np.exp(-(x/0.006)**2)*np.clip((z-(ctop-0.06))/0.03,0,1)
    comb=0.5+0.5*np.sin(y*2*np.pi/spacing+np.sign(x)*0.8)
    d=(-0.004*part-amt*comb*np.clip(np.abs(x)/0.02,0,1))*on
    set_co(shell, co+nor*d[:,None]); print('  parted and combed')

def scalp_points(bco, nrm, tris, ok, spacing, seed=5, extra=None):
    """Points spread over the scalp triangles (the head mesh is too coarse for dense hair) at least `spacing` apart."""
    area=np.array([np.linalg.norm(np.cross(bco[b]-bco[a],bco[c]-bco[a]))/2 for a,b,c in tris])
    rng=np.random.default_rng(seed); want=int(area.sum()/(spacing*spacing)*1.6)
    pick=rng.choice(len(tris),want,p=area/area.sum()); grid={}; P=[]; N=[]
    for t in pick:
        a,b,c=tris[t]; r1,r2=rng.random(),rng.random()
        if r1+r2>1: r1,r2=1-r1,1-r2
        p=bco[a]+(bco[b]-bco[a])*r1+(bco[c]-bco[a])*r2
        if not ok(p): continue
        k=tuple(int(x//spacing) for x in p)
        if any(np.linalg.norm(P[q]-p)<spacing for dx in (-1,0,1) for dy in (-1,0,1) for dz in (-1,0,1) for q in grid.get((k[0]+dx,k[1]+dy,k[2]+dz),())): continue
        grid.setdefault(k,[]).append(len(P)); P.append(p)
        nn=nrm[a]*(1-r1-r2)+nrm[b]*r1+nrm[c]*r2; N.append(nn/np.linalg.norm(nn))
    return np.array(P),np.array(N)

# ---------------------------------------------------------------------------------------------------------------
# hood (lying behind the neck), socks, nose ring

def hood(top, body, arm):
    """A hoodie's hood lying on the upper back: a fat half ring round the back of the neck, weighted like the
    shirt under it."""
    tc=mesh_co(top); ring=tc[tc[:,2]>tc[:,2].max()-0.012]
    cx,cy=float(ring[:,0].mean()),float(ring[:,1].mean()); rx=float((ring[:,0].max()-ring[:,0].min())/2); ry=float((ring[:,1].max()-ring[:,1].min())/2)
    z0=float(tc[:,2].max())-0.012
    bm=bmesh.new(); N=28; M=10; rings=[]
    for i in range(N+1):
        th=math.radians(-118+236*i/N); back=math.cos(th)                 # 0 = straight back
        c=mathutils.Vector((cx+math.sin(th)*(rx+0.02), cy+back*(ry+0.03)+0.01, z0-0.012-0.05*back*back))
        r=0.022+0.03*back*back; out=mathutils.Vector((math.sin(th),back,0)).normalized()
        up=mathutils.Vector((0,0,1)); rings.append([bm.verts.new(c+(out*math.cos(j*6.283/M)*1.25+up*math.sin(j*6.283/M)*0.8)*r) for j in range(M)])
    for i in range(N):
        for j in range(M): bm.faces.new((rings[i][j],rings[i][(j+1)%M],rings[i+1][(j+1)%M],rings[i+1][j]))
    for r in (rings[0],rings[-1]): bm.faces.new(r)
    me=bpy.data.meshes.new('Hood'); bm.to_mesh(me); bm.free()
    h=bpy.data.objects.new('Hood',me); bpy.context.scene.collection.objects.link(h)
    push_out(h, bvh_of(top), 0.004, passes=2, smooth_iters=2)
    transfer_weights(top, h, arm); sel(h); bpy.ops.object.shade_smooth()
    print('  hood verts',len(me.vertices)); return h

def socks(body, arm, z0=0.075, z1=0.2, off=0.0035):
    """Socks: the ankle skin between z0 and z1, copied a few mm out, skinned like the leg."""
    bm=bmesh.new(); bm.from_mesh(body.data)
    keep=set(f for f in bm.faces if any(z0<v.co.z<z1+0.03 for v in f.verts) and all(v.co.z>z0-0.03 for v in f.verts))
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f not in keep], context='FACES')
    geom=list(bm.verts)+list(bm.edges)+list(bm.faces)
    bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0,0,z1), plane_no=(0,0,1), clear_outer=True)     # a straight top edge
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    for v in bm.verts: v.co=v.co+v.normal*off
    me=bpy.data.meshes.new('Socks'); bm.to_mesh(me); bm.free()
    s=bpy.data.objects.new('Socks',me); bpy.context.scene.collection.objects.link(s)
    if s.data.shape_keys: s.shape_key_clear()
    for g in body.vertex_groups: s.vertex_groups.new(name=g.name)          # the copied weights line up by index
    s.parent=arm; md=s.modifiers.new('Armature','ARMATURE'); md.object=arm
    sel(s); bpy.ops.object.shade_smooth(); print('  socks verts',len(me.vertices)); return s

def nose_ring(body, mouthZ, rigid):
    """A thin silver hoop through the right nostril (the character's right = -X)."""
    co=mesh_co(body); cand=co[(co[:,0]<-0.006)&(co[:,0]>-0.022)&(np.abs(co[:,2]-(mouthZ+0.022))<0.008)]
    p=cand[np.argmin(cand[:,1])]                                           # front of the nostril wing
    c=mathutils.Vector((p[0]-0.003,p[1]+0.003,p[2]-0.004))
    bm=bmesh.new(); bmesh.ops.create_circle(bm, segments=20, radius=0.0045)
    ring=bm.verts[:]
    tube_path(bm,[v.co.copy() for v in ring],0.0007,6,True)
    for v in ring: bm.verts.remove(v)
    bmesh.ops.transform(bm, matrix=mathutils.Matrix.Translation(c)@mathutils.Matrix.Rotation(math.radians(80),4,'Y')@mathutils.Matrix.Rotation(math.radians(20),4,'Z'), verts=bm.verts)
    me=bpy.data.meshes.new('Jewelry'); bm.to_mesh(me); bm.free()
    o=bpy.data.objects.new('Jewelry',me); bpy.context.scene.collection.objects.link(o)
    material(o,'Jewelry',(0.8,0.8,0.82,1)); rigid(o); sel(o); bpy.ops.object.shade_smooth(); return o

# ---------------------------------------------------------------------------------------------------------------
# sneakers: the scanned Nike pair (shoes.blend, 'rednikeinstant.002', baked texture), one shoe fitted to each foot

def fit_shoes(body, arm, W, bco, Bw, bn, infoot):
    with bpy.data.libraries.load(W+'/shoes.blend') as (src,dst): dst.objects=['rednikeinstant.002']
    o=dst.objects[0]; bpy.context.scene.collection.objects.link(o)
    sel(o); bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    # the two shoes: split the pair by loose parts, grouped into the two biggest clusters
    bm=bmesh.new(); bm.from_mesh(o.data); bm.verts.ensure_lookup_table()
    comp=[-1]*len(bm.verts); comps=[]
    for v in bm.verts:
        if comp[v.index]>=0: continue
        stack=[v]; comp[v.index]=len(comps); idx=[]
        while stack:
            x=stack.pop(); idx.append(x.index)
            for e in x.link_edges:
                y=e.other_vert(x)
                if comp[y.index]<0: comp[y.index]=len(comps); stack.append(y)
        comps.append(idx)
    P=np.array([v.co[:] for v in bm.verts]); bm.free()
    comps.sort(key=len, reverse=True)
    A=P[comps[0]].mean(0); B=P[comps[1]].mean(0)
    keep=np.array([np.linalg.norm(P[i]-A)<np.linalg.norm(P[i]-B) for i in range(len(P))])      # the shoe with the biggest part
    delete_verts(o, ~keep)
    co=mesh_co(o); co-=co.mean(0)
    # long axis (PCA on the ground plane); the toe is the end whose top is lower
    xy=co[:,:2]; ev,evec=np.linalg.eigh(np.cov(xy.T)); ax=evec[:,np.argmax(ev)]
    t=xy@ax; front=co[t>np.percentile(t,80)][:,2].max(); back=co[t<np.percentile(t,20)][:,2].max()
    if front>back: ax=-ax                                                    # +ax = toward the toe
    ang=math.atan2(ax[1],ax[0]); R=mathutils.Matrix.Rotation(-math.pi/2-ang,3,'Z')   # toe → -Y (forward)
    co=np.array([R@mathutils.Vector(p) for p in co]); co[:,2]-=co[:,2].min()
    L0=co[:,1].max()-co[:,1].min(); W0=co[:,0].max()-co[:,0].min()
    parts=[]
    for side,sx in (('L',1),('R',-1)):
        f=bco[infoot&(np.sign(bco[:,0])==sx)]
        fl=f[:,1].max()-f[:,1].min(); fw=f[:,0].max()-f[:,0].min()
        s=fl*1.1/L0; wx=np.clip(fw*1.22/(W0*s),0.92,1.18)
        c=co*np.array([s*wx,s,s])
        if sx<0: c[:,0]*=-1                                                  # the other foot: mirrored
        c+=np.array([(f[:,0].max()+f[:,0].min())/2, f[:,1].max()+0.016-c[:,1].max(), -0.004])
        shoe=o.copy(); shoe.data=o.data.copy(); bpy.context.scene.collection.objects.link(shoe); set_co(shoe,c)
        if sx<0:
            b2=bmesh.new(); b2.from_mesh(shoe.data); bmesh.ops.reverse_faces(b2, faces=b2.faces); b2.to_mesh(shoe.data); b2.free()
        shoe.data.update(); parts.append(shoe)
    bpy.data.objects.remove(o)
    shoes=join(parts,'Shoes')
    b3=bmesh.new(); b3.from_mesh(shoes.data); bmesh.ops.recalc_face_normals(b3, faces=b3.faces); b3.to_mesh(shoes.data); b3.free()   # the scan's faces pointed in
    shoes.data.materials.clear(); material(shoes,'ShoeTex',(1,1,1,1))
    from mathutils.kdtree import KDTree
    fi=np.nonzero(infoot)[0]; kt=KDTree(len(fi)); [kt.insert(bco[i],j) for j,i in enumerate(fi)]; kt.balance()
    for g in list(shoes.vertex_groups): shoes.vertex_groups.remove(g)
    for g in body.vertex_groups: shoes.vertex_groups.new(name=g.name)
    add={}
    for i,v in enumerate(mesh_co(shoes)):
        _,j,_=kt.find(v); src=fi[j]
        for k in np.nonzero(Bw[src]>0.001)[0]: add.setdefault((bn[k],round(float(Bw[src,k]),3)),[]).append(i)
    for (g,w),idx in add.items(): shoes.vertex_groups[g].add(idx,w,'REPLACE')
    shoes.parent=arm; md=shoes.modifiers.new('Armature','ARMATURE'); md.object=arm
    sel(shoes); bpy.ops.object.shade_smooth()
    print('  shoes: scan length',round(L0,3),'verts',len(shoes.data.vertices)); return shoes
