import sys
from PIL import Image
import numpy as np
from collections import deque

def shift_or(m, r):
    out = m.copy()
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            if dx * dx + dy * dy <= r * r:
                out |= np.roll(np.roll(m, dy, 0), dx, 1)
    return out

def fill_holes(m):
    h, w = m.shape; outside = np.zeros_like(m); q = deque()
    for y in range(h):
        for x in (0, w - 1):
            if not m[y, x] and not outside[y, x]: outside[y, x] = True; q.append((y, x))
    for x in range(w):
        for y in (0, h - 1):
            if not m[y, x] and not outside[y, x]: outside[y, x] = True; q.append((y, x))
    while q:
        y, x = q.popleft()
        for yy, xx in ((y+1,x),(y-1,x),(y,x+1),(y,x-1)):
            if 0 <= yy < h and 0 <= xx < w and not m[yy, xx] and not outside[yy, xx]:
                outside[yy, xx] = True; q.append((yy, xx))
    return ~outside

def cut(path, box, out, cut_below=None):
    a = np.asarray(Image.open(path).convert('RGB')).astype(float)
    bg = np.median(a[5:40, 5:40].reshape(-1, 3), 0)
    c = a[box[1]:box[3], box[0]:box[2]]
    mx, mn = c.max(-1), c.min(-1)
    notbg = np.abs(c - bg).sum(-1) > 24
    core = notbg & ((mx - mn >= 22) | (mx > 165))          # hair / fur / ears, not the gray-black hoodie
    core = fill_holes(shift_or(core, 2)) & notbg            # close gaps, fill, keep the dark outlines
    core = shift_or(core, 3) & notbg & (shift_or(core, 3))
    core = fill_holes(core) & notbg
    if cut_below is not None:
        core[cut_below - box[1]:] = False
    rgba = np.dstack([c, core * 255]).astype(np.uint8)
    Image.fromarray(rgba, 'RGBA').save(out)
    return rgba

if __name__ == '__main__':
    cut(sys.argv[1], tuple(map(int, sys.argv[2].split(','))), sys.argv[3], int(sys.argv[4]) if len(sys.argv) > 4 else None)
