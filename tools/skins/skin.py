"""Character skin generator for the Darkwood co-op mod.

For every sprite on the normal-outfit sheets: finds the hat (largest warm-colored blob), turns the coat
into the character's (black) clothing, paints the hat brim as the hood, pastes the character's head+ears
(cut out of their top-down reference) scaled to the hat, and adds the tail behind.
Characters face UP in the sprite sheets (after un-rotating rotated sprites)."""
import sys, os, math
import numpy as np
from PIL import Image
sys.path.insert(0, os.path.dirname(__file__))
from frames_lib import Export

SKIP = {'BloodSplatsCol', 'AntagonistAct1NoneSprites', 'PlayerNakedNoneSprites',
        'PlayerNakedFlamethrowerSprites', 'PlayerEpilogueSprites'}
SS = 4

CHARACTERS = {
    'Kazuya': dict(head='kazuya_head.png', width=2.40, brightness=1.02, sat=1.05, contrast=1.30,
                   hair=lambda r, g, b: (r - b > 18) & (r < 215),
                   tail=(104, 74, 58), tail_r=0.30, cloth_tint=(0.93, 0.95, 1.06)),
    'Cookie': dict(head='cookie_head.png', width=2.70, brightness=0.86, sat=0.95, contrast=1.0,
                   hair=lambda r, g, b: (b - r > 14),
                   tail=(214, 220, 236), tail_r=0.34, cloth_tint=(0.94, 0.95, 1.05)),
}

def components(mask):
    h, w = mask.shape; lab = np.zeros((h, w), int); n = 0; sizes = {}
    for y in range(h):
        for x in range(w):
            if mask[y, x] and not lab[y, x]:
                n += 1; st = [(y, x)]; lab[y, x] = n; c = 0
                while st:
                    cy, cx = st.pop(); c += 1
                    for dy in (-1, 0, 1):
                        for dx in (-1, 0, 1):
                            yy, xx = cy + dy, cx + dx
                            if 0 <= yy < h and 0 <= xx < w and mask[yy, xx] and not lab[yy, xx]:
                                lab[yy, xx] = n; st.append((yy, xx))
                sizes[n] = c
    return lab, sizes

class Head:
    """The cut-out head, premultiplied, with the point that goes on the hat centre."""
    def __init__(self, path, ch):
        im = Image.open(path).convert('RGBA')
        a = np.asarray(im).astype(float)
        rgb, al = a[..., :3], a[..., 3:] / 255
        lum = rgb @ np.array([0.299, 0.587, 0.114])
        rgb = lum[..., None] + (rgb - lum[..., None]) * ch['sat']
        m = (rgb * al).sum((0, 1)) / max(al.sum(), 1)
        rgb = m + (rgb - m) * ch['contrast']
        rgb = np.clip(rgb * ch['brightness'], 0, 255)
        self.img = Image.fromarray(np.dstack([rgb, al * 255]).astype(np.uint8), 'RGBA')
        hair = ch['hair'](a[..., 0], a[..., 1], a[..., 2]) & (a[..., 3] > 128)
        ys, xs = np.nonzero(hair)
        ys0, xs0 = np.nonzero(a[..., 3] > 128)
        self.anchor = ((xs0.min() + xs0.max()) / 2, ys.mean())   # centred left-right, hair height
        self.w = xs0.max() - xs0.min() + 1
        self.x0 = xs0.min()
        self.cache = {}

    def scaled(self, target_w):
        k = round(target_w * SS)
        if k not in self.cache:
            s = k / self.w
            size = (max(1, round(self.img.width * s)), max(1, round(self.img.height * s)))
            self.cache[k] = (self.img.convert('RGBa').resize(size, Image.LANCZOS), s)
        return self.cache[k]

def process(img, ch, head):
    a = np.asarray(img).astype(float)
    rgb, al = a[..., :3], a[..., 3] / 255.
    h, w = al.shape
    mx, mn = rgb.max(-1), rgb.min(-1); chroma = mx - mn
    R, G, B = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    lum = rgb @ np.array([0.299, 0.587, 0.114])
    out = rgb.copy()
    cloth = (al > 0) & (chroma < 24) & (mx < 115)
    out[cloth] = np.clip(lum[cloth, None] * 0.9 * np.array(ch['cloth_tint']), 0, 255)
    hat = (al > 0.4) & (chroma >= 19) & (mx >= 40) & (G >= B) & (R - G < 40)
    lab, sizes = components(hat)
    if not sizes or max(sizes.values()) < 10:
        return Image.fromarray(np.dstack([out, al * 255]).round().astype(np.uint8), 'RGBA')
    big = max(sizes, key=sizes.get)
    ys, xs = np.nonzero(lab == big)
    r = math.sqrt(len(ys) / math.pi); cy, cx = ys.mean(), xs.mean()
    for _ in range(3):  # robust centre: ignore a wooden handle touching the hat
        d = np.hypot(ys - cy, xs - cx); keep = d < 1.35 * r
        cy, cx = ys[keep].mean(), xs[keep].mean()
        r = max(r * 0.9, math.sqrt(keep.sum() / math.pi))
    R0 = r * 1.08
    ccx, ccy = cx + 0.5, cy + 0.5          # hat centre in pixel coordinates
    # supersampled layer: hood (where the brim was), tail, head
    H, W = h * SS, w * SS
    layer = Image.new('RGBa', (W, H), (0, 0, 0, 0))
    gy, gx = (np.mgrid[0:H, 0:W] + 0.5) / SS
    dist = np.hypot(gx - ccx, gy - ccy)
    lum_up = np.asarray(Image.fromarray(lum.astype(np.uint8)).resize((W, H), Image.BILINEAR)).astype(float)
    hood = dist < R0
    hood_rgb = np.clip((lum_up[..., None] * 0.5 + 16) * np.array(ch['cloth_tint']), 0, 255)
    base = np.zeros((H, W, 4)); base[hood, :3] = hood_rgb[hood]; base[hood, 3] = 255
    # tail: behind the character (down the sprite), at the bottom of the body below the hat
    col = int(round(cx))
    below = np.nonzero(al[int(round(cy)):, max(0, col - 1):col + 2].max(1) > 0.5)[0]
    if len(below):
        bottom = cy + below.max()
        if 0.7 * R0 < bottom - cy < 2.4 * R0:
            tx, ty, tr = ccx, bottom + 0.5 - 0.12 * R0, ch['tail_r'] * R0
            ang = np.arctan2(gy - ty, gx - tx)
            fluff = tr * (1 + 0.12 * np.sin(ang * 9) + 0.08 * np.sin(ang * 14 + 1.3))
            dt = np.hypot(gx - tx, gy - ty)
            m = dt < fluff
            shade = np.clip(1.08 - 0.35 * (dt / tr) ** 2 + 0.15 * (ty - gy) / tr, 0.6, 1.15)
            base[m, :3] = (np.array(ch['tail'], float) * shade[..., None])[m]; base[m, 3] = 255
    layer = Image.fromarray(np.clip(base, 0, 255).astype(np.uint8), 'RGBA').convert('RGBa')
    # head
    himg, s = head.scaled(ch['width'] * R0)
    px = int(round(ccx * SS - (head.anchor[0]) * s))
    py = int(round(ccy * SS - (head.anchor[1]) * s))
    hl = Image.new('RGBa', (W, H), (0, 0, 0, 0)); hl.paste(himg, (px, py))
    layer = Image.alpha_composite(layer.convert('RGBA'), hl.convert('RGBA')).convert('RGBa')
    small = np.asarray(layer.resize((w, h), Image.BOX).convert('RGBA')).astype(float)
    ca = small[..., 3] / 255
    out = out * (1 - ca[..., None]) + small[..., :3] * ca[..., None]
    alpha = np.maximum(al, ca)
    return Image.fromarray(np.clip(np.dstack([out, alpha * 255]), 0, 255).round().astype(np.uint8), 'RGBA')

def build(exp_dir, refs_dir, name, out_dir, only=None):
    ch = CHARACTERS[name]
    head = Head(os.path.join(refs_dir, ch['head']), ch)
    E = Export(exp_dir)
    os.makedirs(os.path.join(out_dir, 'atlases'), exist_ok=True)
    for coll, info in E.j['collections'].items():
        if coll in SKIP or (only and coll not in only):
            continue
        sheet = E.sheet(coll).copy()
        n = 0
        for sp in info['sprites']:
            if not sp['rect'] or sp['atlas'] != 0 or sp['rect'][2] < 2 or sp['rect'][3] < 2:
                continue
            x, y, w, h = sp['rect']
            res = process(E.crop(coll, sp), ch, head)
            if sp['rotated']:
                res = res.rotate(-90, expand=True)
            sheet.paste(res, (x, y))
            n += 1
        fname = os.path.basename(info['atlases'][0]['file'])
        sheet.save(os.path.join(out_dir, 'atlases', fname), optimize=True)
        print(f'{name} {coll}: {n} sprites', flush=True)

if __name__ == '__main__':
    exp, refs, name, out = sys.argv[1:5]
    build(exp, refs, name, out, set(sys.argv[5].split(',')) if len(sys.argv) > 5 else None)
