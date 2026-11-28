import sys, os
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.dirname(__file__))
from frames_lib import Export
exp, skin, out = sys.argv[1], sys.argv[2], sys.argv[3]
picks = [tuple(p.split(':')) for p in sys.argv[4].split(',')]   # lib:clip:frame
scale = int(sys.argv[5]) if len(sys.argv) > 5 else 6
E = Export(exp); K = Export(exp)
def crop(ex, f, override):
    s = ex.sprite(f['collection'], f['sprite']); coll = f['collection']
    if override:
        p = os.path.join(skin, 'atlases', os.path.basename(ex.j['collections'][coll]['atlases'][0]['file']))
        if os.path.exists(p): ex._sh[(coll, 0)] = Image.open(p).convert('RGBA')
    return ex.crop(coll, s)
cells = []
for lib, clip, n in picks:
    fs = E.frames(lib, clip); f = fs[min(int(n), len(fs) - 1)]
    a = crop(E, f, False); b = crop(K, f, True)
    cells.append((f"{lib.replace('Player','').replace('Anims','')} {clip} {n}", a, b))
cw = max(max(a.width, b.width) for _, a, b in cells) * scale + 20
ch = max(max(a.height, b.height) for _, a, b in cells) * scale + 20
img = Image.new('RGB', (cw * len(cells), ch * 2 + 24), (62, 66, 50))
d = ImageDraw.Draw(img)
for i, (label, a, b) in enumerate(cells):
    for row, im in enumerate((a, b)):
        big = im.resize((im.width * scale, im.height * scale), Image.BILINEAR)
        img.paste(big, (i * cw + (cw - big.width) // 2, row * ch + (ch - big.height) // 2), big)
    d.text((i * cw + 6, ch * 2 + 6), label, fill=(230, 230, 230))
img.save(out); print(out, img.size)
