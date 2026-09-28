# Paint a character's own face texture: eyebrows (and stubble for Nathan) onto the base head diffuse, using the
# per-loop UV weights stage4 writes to WORK/face_<who>.json. Needs Pillow + numpy (plain Python, not Blender):
#   python face_textures.py WORK who OUT_TEX_DIR
# writes OUT_TEX_DIR/Skin_Head_<Who>_D.jpg (RealBody uses it when it exists). The base is Skin_Head_D.jpg in OUT_TEX_DIR.
import sys, json, numpy as np
from PIL import Image, ImageFilter

W, who, tex = sys.argv[1], sys.argv[2], sys.argv[3]
STYLE = {   # brow colour (0..1), brow strength, stubble colour, stubble strength
    'nathan': dict(brow=(0.05, 0.04, 0.035), bs=0.95, stub=(0.22, 0.16, 0.13), ss=0.32),
    'kenny':  dict(brow=(0.03, 0.025, 0.02), bs=0.9,  stub=(0.1, 0.08, 0.07), ss=0.0),
    'isaiah': dict(brow=(0.05, 0.035, 0.03), bs=0.9,  stub=(0.12, 0.1, 0.09), ss=0.0),
    'cooper': dict(brow=(0.3, 0.2, 0.12), bs=0.7, stub=(0.3, 0.25, 0.2), ss=0.0),
    'thorton': dict(brow=(0.03, 0.025, 0.02), bs=0.9, stub=(0.04, 0.03, 0.025), ss=0.85),    # thin mustache
    'piper':  dict(brow=(0.42, 0.3, 0.18), bs=0.55, stub=(0.3, 0.25, 0.2), ss=0.0),          # light, thin brows
    'john':   dict(brow=(0.2, 0.13, 0.08), bs=0.8, stub=(0.2, 0.15, 0.1), ss=0.0),
    'will':   dict(brow=(0.3, 0.2, 0.12), bs=0.85, stub=(0.32, 0.22, 0.14), ss=0.8),          # short brown beard
}[who]

base = Image.open(f'{tex}/Skin_Head_D.jpg').convert('RGB')
img = np.asarray(base).astype(np.float32) / 255.0
H, Wd = img.shape[:2]
face = json.load(open(f'{W}/face_{who}.json'))
eyeZ, mouthZ, chinZ = face['eyeZ'], face['mouthZ'], face['chinZ']
BROW_T = {'nathan': 0.0072, 'kenny': 0.005, 'isaiah': 0.006, 'cooper': 0.0055, 'thorton': 0.0055, 'piper': 0.0038, 'john': 0.0058, 'will': 0.0065}[who]
# facial hair zones: (upper lip, chin + jaw, cheeks)
ZONES = {'nathan': (1, 1, 1), 'thorton': (1, 0.25, 0), 'will': (1, 1, 1)}.get(who)
STUB = 1.0 if ZONES else 0.0

def ssf(a, b, x): t = min(1, max(0, (x - a) / (b - a))); return t * t * (3 - 2 * t)

# eyebrows: an arched band over each eye (covers the base texture's faint brows); stubble: upper lip, chin, jaw
brow, stub = [], []
for u, v, x, y, z in face['loops']:
    ax = abs(x)
    bz = eyeZ + 0.019 - 0.005 * ((ax - 0.034) / 0.026) ** 2          # over the base texture's own brows
    wb = np.exp(-((z - bz) / BROW_T) ** 2) * ssf(0.006, 0.013, ax) * (1 - ssf(0.054, 0.064, ax)) * (1 if y < -0.05 else 0)
    brow.append((u, v, wb if wb > 0.03 else 0.0))                    # zeros too: they shape the edges
    if STUB > 0:
        lips = ax < 0.027 and mouthZ - 0.009 < z < mouthZ + 0.006
        lip = ssf(mouthZ + 0.004, mouthZ + 0.008, z) * (1 - ssf(mouthZ + 0.017, mouthZ + 0.022, z)) * (1 - ssf(0.024, 0.03, ax))
        jaw = (1 - ssf(mouthZ - 0.009, mouthZ - 0.004, z)) * ssf(chinZ - 0.022, chinZ - 0.004, z)
        cheek = ssf(0.03, 0.04, ax) * (1 - ssf(mouthZ + 0.004, mouthZ + 0.016, z)) * ssf(chinZ - 0.03, chinZ, z)
        zl, zj, zc = ZONES
        if who == 'thorton': lip *= 1 - ssf(0.014, 0.024, ax)                          # a thin mustache: over the lip only
        ws = 0 if lips else max(lip * zl, jaw * zj, cheek * zc) * STUB * (1 - ssf(0.046 if who != 'will' else 0.056, 0.06 if who != 'will' else 0.07, ax))
        stub.append((u, v, ws if ws > 0.03 else 0.0))
face = {'brow': brow, 'stubble': stub}
rng = np.random.default_rng(7)

def mask(samples, radius, soften=0.15):
    # smooth weighted average of the samples' weights (the face's UV samples are far apart in texture space)
    num = np.zeros((H, Wd), np.float32); den = np.zeros((H, Wd), np.float32)
    r = int(radius)
    yy, xx = np.mgrid[-r:r + 1, -r:r + 1]
    k = np.exp(-(xx ** 2 + yy ** 2) / (0.35 * radius * radius)).astype(np.float32)
    for u, v, w in samples:
        px, py = int(u * Wd), int((1 - v) * H)
        x0, x1, y0, y1 = max(px - r, 0), min(px + r + 1, Wd), max(py - r, 0), min(py + r + 1, H)
        if x0 >= x1 or y0 >= y1: continue
        kk = k[(y0 - py + r):(y1 - py + r), (x0 - px + r):(x1 - px + r)]
        num[y0:y1, x0:x1] += kk * w; den[y0:y1, x0:x1] += kk
    m = np.where(den > 1e-4, num / np.maximum(den, 1e-4), 0) * np.clip(den / 0.25, 0, 1)
    return np.asarray(Image.fromarray((m * 255).clip(0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(radius * soften))).astype(np.float32) / 255.0

def strokes(scale):
    """Short hair-like streaks: noise smeared along x."""
    n = rng.random((H, Wd)).astype(np.float32)
    s = sum(np.roll(n, k, axis=1) for k in range(scale)) / scale
    s = (s - s.mean()) / (s.std() + 1e-6)
    return np.clip(0.55 + 0.35 * s, 0, 1)

out = img.copy()
spacing = max(4, int(Wd / 512))
if face['brow'] and STYLE['bs'] > 0:
    a = mask(face['brow'], spacing * 7.0) * strokes(spacing * 2) * STYLE['bs']
    out = out * (1 - a[..., None]) + np.array(STYLE['brow'])[None, None, :] * a[..., None]
if face['stubble'] and STYLE['ss'] > 0:
    dots = (rng.random((H, Wd)) > 0.55).astype(np.float32)
    dots = np.asarray(Image.fromarray((dots * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.7))).astype(np.float32) / 255.0
    a = mask(face['stubble'], spacing * 7.0, soften=0.9) * (0.35 + 0.65 * dots) * STYLE['ss']
    out = out * (1 - a[..., None]) + np.array(STYLE['stub'])[None, None, :] * a[..., None]
name = who[0].upper() + who[1:]
Image.fromarray((out * 255).clip(0, 255).astype(np.uint8)).save(f'{tex}/Skin_Head_{name}_D.jpg', quality=92)
print('wrote', f'{tex}/Skin_Head_{name}_D.jpg', 'brow samples', len(face['brow']), 'stubble samples', len(face['stubble']), 'size', Wd, H)
