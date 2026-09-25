#!/usr/bin/env python3
"""Pull the web build's rigged human body, fitted clothes and knit texture out of index (5).html
into Unity: ScaryStreetUnity/Assets/Resources/HumanBase.bytes and Knit.jpg.

Coordinates are converted from three.js (right-handed) to Unity (left-handed) by flipping X and
reversing triangle winding, so the Unity side can use them as-is.

Binary layout (little endian): b"SSHB", int version,
  int boneCount, then per bone: str name, int parent (-1 = none), 3f head, 3f tail
  mesh "body", then int garmentCount and per garment: str key, mesh, int coverCount, int[] cover
  mesh = int vertCount, 3f[] verts, int indexCount, int[] indices, per vertex 4 × (int bone, float weight)
  str = int byteLength + utf8
"""
import base64, json, struct, pathlib

ROOT = pathlib.Path(__file__).resolve().parent.parent
SRC = ROOT / "index (5).html"
OUT = ROOT / "ScaryStreetUnity" / "Assets" / "Resources"

def grab(src, name):
    i = src.index(f"const {name} = ") + len(f"const {name} = ")
    return json.loads(src[i:src.index("\n", i)].rstrip().rstrip(";"))

def s(out, text):
    b = text.encode("utf-8"); out += struct.pack("<i", len(b)) + b

def mesh(out, verts, tris, weights, scale=1.0):
    n = len(verts) // 3
    out += struct.pack("<i", n)
    for i in range(n):
        x, y, z = (verts[i*3] * scale, verts[i*3+1] * scale, verts[i*3+2] * scale)
        out += struct.pack("<3f", -x, y, z)                      # three.js -> Unity: mirror X
    idx = []
    for t in range(0, len(tris), 3):
        idx += [tris[t], tris[t+2], tris[t+1]]                   # ...and flip the winding
    out += struct.pack("<i", len(idx)) + struct.pack(f"<{len(idx)}i", *idx)
    for w in weights:
        pairs = [(w[j], w[j+1]) for j in range(0, len(w) - 1, 2)][:4]
        pairs += [(0, 0.0)] * (4 - len(pairs))
        for b, wt in pairs: out += struct.pack("<if", int(b), float(wt))

def main():
    src = SRC.read_text(encoding="utf-8")
    man, clothes = grab(src, "MAN"), grab(src, "CLOTHES")
    names = [b[0] for b in man["b"]]
    out = bytearray(b"SSHB") + struct.pack("<i", 1)
    out += struct.pack("<i", len(man["b"]))
    for name, parent, head, tail in man["b"]:
        s(out, name)
        out += struct.pack("<i", names.index(parent) if parent else -1)
        out += struct.pack("<3f", -head[0], head[1], head[2]) + struct.pack("<3f", -tail[0], tail[1], tail[2])
    body = man["body"]
    mesh(out, body["v"], body["t"], body["w"])
    out += struct.pack("<i", len(clothes))
    for key, g in clothes.items():
        s(out, key)
        mesh(out, g["v"], g["t"], g["w"], scale=0.001)           # garment coords are stored in millimetres
        out += struct.pack("<i", len(g["cover"])) + struct.pack(f"<{len(g['cover'])}i", *g["cover"])
    (OUT / "HumanBase.bytes").write_bytes(out)

    i = src.index("const KNIT = new THREE.TextureLoader().load('") + len("const KNIT = new THREE.TextureLoader().load('")
    data = src[i:src.index("'", i)].split(",", 1)[1]
    (OUT / "Knit.jpg").write_bytes(base64.b64decode(data))
    print(f"HumanBase.bytes {len(out) // 1024} KB, {len(names)} bones, garments: {', '.join(clothes)}")

if __name__ == "__main__":
    main()
