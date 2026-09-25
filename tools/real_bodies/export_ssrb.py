# Export a finished character to Scary Street's real-body format (SSRB v1) in Unity coordinates.
#   Blender (x, y, z), front -Y, Z up  ->  Unity (-x, z, -y), front +Z, Y up (mirror, so triangles flip).
# Layout: "SSRB" int32 version | height | bones: count, (name, parent, head xyz) | parts: count, each:
#   name, material count + names, vertex count, pos xyz, normal xyz, uv xy, 4x(bone int, weight float),
#   submesh count, each (index count, indices) | blendshapes: count, each (name, delta xyz per vertex)
import bpy, sys, struct, numpy as np
W=sys.argv[-3]; who=sys.argv[-2]; out=sys.argv[-1]; sys.path.append(W)
from cclib import *
bpy.ops.wm.open_mainfile(filepath=W+f'/stage4_{who}.blend')
O=bpy.data.objects; arm=O['Rig']
for pb in arm.pose.bones: pb.matrix_basis=mathutils.Matrix.Identity(4)
bpy.context.view_layer.update()
def U(v): return (-v[0], v[2], -v[1])
# bones: parents before children
order=[]; 
def walk(b):
    order.append(b); [walk(c) for c in b.children]
for b in arm.data.bones:
    if b.parent is None: walk(b)
bidx={b.name:i for i,b in enumerate(order)}
f=open(out,'wb')
def S(s): b=s.encode(); f.write(struct.pack('<i',len(b))); f.write(b)
f.write(b'SSRB'); f.write(struct.pack('<i',1))
body=O['CC_Base_Body']; f.write(struct.pack('<f', float(mesh_co(body)[:,2].max())))
f.write(struct.pack('<i',len(order)))
for b in order:
    S(b.name); f.write(struct.pack('<i', bidx[b.parent.name] if b.parent else -1)); f.write(struct.pack('<3f',*U(b.head_local)))
names=['CC_Base_Body','CC_Game_Eye','CC_Game_Teeth','Top','Pants','Shoes','Hair','Cap','Wristband']
parts=[O[n] for n in names if n in O]
f.write(struct.pack('<i',len(parts)))
for o in parts:
    me=o.data; me.calc_loop_triangles()
    uvl=me.uv_layers.active.data if me.uv_layers.active else None
    cn=me.corner_normals
    gname={g.index:g.name for g in o.vertex_groups}
    vw=[]
    for v in me.vertices:
        ws=[(g.weight,bidx[gname[g.group]]) for g in v.groups if gname[g.group] in bidx and g.weight>0.0005]
        ws.sort(reverse=True); ws=ws[:4]; t=sum(w for w,_ in ws) or 1
        ws=[(bi,w/t) for w,bi in ws]+[(0,0.0)]*(4-len(ws)); vw.append(ws)
    keys=me.shape_keys.key_blocks if me.shape_keys else []
    kco=[np.array([d.co[:] for d in k.data]) for k in keys]
    remap={}; P=[];N=[];UV=[];BW=[];SRC=[]
    subs={}
    for tri in me.loop_triangles:
        idx=[]
        for li in tri.loops:
            vi=me.loops[li].vertex_index
            uv=tuple(round(c,5) for c in uvl[li].uv) if uvl else (0.0,0.0)
            nrm=tuple(round(c,3) for c in cn[li].vector)
            key=(vi,uv,nrm)
            if key not in remap:
                remap[key]=len(P); P.append(U(me.vertices[vi].co)); N.append(U(cn[li].vector)); UV.append(uv); BW.append(vw[vi]); SRC.append(vi)
            idx.append(remap[key])
        subs.setdefault(tri.material_index,[]).extend([idx[0],idx[2],idx[1]])   # mirrored: flip winding
    mats=[(m.name if m else 'Default') for m in me.materials] or ['Default']
    S(o.name); f.write(struct.pack('<i',len(mats))); [S(m) for m in mats]
    f.write(struct.pack('<i',len(P)))
    f.write(np.array(P,np.float32).tobytes()); f.write(np.array(N,np.float32).tobytes()); f.write(np.array(UV,np.float32).tobytes())
    for ws in BW:
        for bi,w in ws: f.write(struct.pack('<if',bi,w))
    f.write(struct.pack('<i',len(mats)))
    for mi in range(len(mats)):
        ind=subs.get(mi,[]); f.write(struct.pack('<i',len(ind))); f.write(np.array(ind,np.int32).tobytes())
    f.write(struct.pack('<i',max(0,len(kco)-1)))
    for k,co in list(zip(keys,kco))[1:]:
        S(k.name); d=(co-kco[0])[SRC]; d=np.column_stack([-d[:,0],d[:,2],-d[:,1]])
        f.write(d.astype(np.float32).tobytes())
    print('PART',o.name,'verts',len(P),'tris',sum(len(v) for v in subs.values())//3,'mats',mats,'shapes',max(0,len(kco)-1))
f.close()
import os; print('WROTE',out,os.path.getsize(out)//1024,'KB')
