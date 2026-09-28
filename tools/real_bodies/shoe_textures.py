# Recolour the scanned sneaker's texture (shoes.blend's baked 'bakev2', a maroon suede Nike with white sole) for each
# character: the upper and laces, the swoosh and the sole each get their own colour, keeping the scan's shading.
#   python shoe_textures.py BAKED_PNG OUT_TEX_DIR [who ...]
# writes OUT_TEX_DIR/Shoe_<Who>_D.jpg (RealBody's "ShoeTex" material picks it up).
import sys, numpy as np
from PIL import Image, ImageFilter

src, out = sys.argv[1], sys.argv[2]
H = lambda h: tuple(int(h[i:i + 2], 16) / 255 for i in (1, 3, 5))
# (upper + laces, swoosh, sole)
WAYS = {
    'cooper':  (H('#f1f1ef'), H('#18181a'), H('#f4f3ef')),
    'nathan':  (H('#f4f4f2'), H('#d9dadd'), H('#f7f6f2')),   # all-white tennis shoes
    'kenny':   (H('#1c1c1f'), H('#f2f2f0'), H('#efeeea')),
    'isaiah':  (H('#23304f'), H('#f2f2f0'), H('#f2f1ec')),
    'thorton': (H('#f2f2f0'), H('#e8641c'), H('#f2f1ec')),
    'piper':   (H('#2a2a2e'), H('#f0f0ee'), H('#ecebe6')),
    'john':    (H('#1f4d34'), H('#f2f2f0'), H('#efeee9')),
    'will':    (H('#f3f3f1'), H('#c9a34d'), H('#f4f3ef')),
}

img = np.asarray(Image.open(src).convert('RGB')).astype(np.float32) / 255
r, g, b = img[..., 0], img[..., 1], img[..., 2]
mx, mn = img.max(-1), img.min(-1)
v = mx; s = np.where(mx > 1e-4, (mx - mn) / np.maximum(mx, 1e-4), 0)
lum = 0.3 * r + 0.59 * g + 0.11 * b
h = np.zeros_like(v)                                              # hue 0..1
d = np.maximum(mx - mn, 1e-5)
h = np.where(mx == r, ((g - b) / d) % 6, np.where(mx == g, (b - r) / d + 2, (r - g) / d + 4)) / 6
bg = v < 0.025                                                     # unused texels
blue = (h > 0.52) & (h < 0.72) & (s > 0.3) & ~bg                   # the insole: leave it
upper = ((h > 0.8) | (h < 0.06)) & (s > 0.16) & ~bg & ~blue        # maroon suede and laces
white = ~bg & ~blue & ~upper & (v > 0.18)                          # sole, outsole, midsole and the swoosh (dark lining stays)

# the swoosh: white patches mostly surrounded by upper (the sole's patches touch the background / edges instead)
lab = np.zeros(white.shape, np.int32); n = 0
Hh, Ww = white.shape
from collections import deque
sw = np.zeros_like(white)
up_d = np.asarray(Image.fromarray((upper * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(5))) > 0
bg_d = np.asarray(Image.fromarray((bg * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(5))) > 0
seen = np.zeros_like(white)
for y0 in range(0, Hh, 2):
    for x0 in range(0, Ww, 2):
        if not white[y0, x0] or seen[y0, x0]: continue
        q = deque([(y0, x0)]); seen[y0, x0] = True; pts = []
        while q:
            y, x = q.popleft(); pts.append((y, x))
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                yy, xx = y + dy, x + dx
                if 0 <= yy < Hh and 0 <= xx < Ww and white[yy, xx] and not seen[yy, xx]:
                    seen[yy, xx] = True; q.append((yy, xx))
        ys, xs = np.array(pts).T
        near_up, near_bg = up_d[ys, xs].mean(), bg_d[ys, xs].mean()
        if 400 < len(pts) < 60000 and near_up > 0.25: sw[ys, xs] = True

def paint(mask, col, base):
    m = mask[..., None]
    ref = np.median(lum[mask]) if mask.any() else 0.5
    shade = np.clip(lum / max(ref, 1e-3), 0.45, 1.4)[..., None]
    return np.where(m, np.clip(np.array(col)[None, None, :] * shade, 0, 1), base)

whos = sys.argv[3:] or list(WAYS)
for who in whos:
    up, swc, sole = WAYS[who]
    o = img.copy()
    o = paint(upper, up, o)
    o = paint(white & ~sw, sole, o)
    o = paint(sw, swc, o)
    Image.fromarray((o * 255).astype(np.uint8)).resize((1024, 1024), Image.LANCZOS).save(f'{out}/Shoe_{who[0].upper() + who[1:]}_D.jpg', quality=90)
    print('wrote', who, 'upper', int(upper.sum()), 'sole', int((white & ~sw).sum()), 'swoosh', int(sw.sum()))
