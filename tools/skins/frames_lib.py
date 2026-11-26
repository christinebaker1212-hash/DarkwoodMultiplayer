import json, os
from PIL import Image
class Export:
    def __init__(self, d):
        self.d = d; self.j = json.load(open(os.path.join(d, 'frames.json'))); self._sh = {}
    def sheet(self, coll, atlas=0):
        k = (coll, atlas)
        if k not in self._sh:
            self._sh[k] = Image.open(os.path.join(self.d, self.j['collections'][coll]['atlases'][atlas]['file'])).convert('RGBA')
        return self._sh[k]
    def sprite(self, coll, sid):
        return next(x for x in self.j['collections'][coll]['sprites'] if x['id'] == sid)
    def crop(self, coll, s):
        x, y, w, h = s['rect']
        im = self.sheet(coll, s['atlas']).crop((x, y, x + w, y + h))
        return im.rotate(90, expand=True) if s['rotated'] else im
    def frames(self, lib, clip=None):
        return [f for f in self.j['frames'] if f['library'] == lib and (clip is None or f['clip'] == clip)]
