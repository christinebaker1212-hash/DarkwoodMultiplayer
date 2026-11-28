# Skin generator

Builds co-op skins (`CoopSkins/<Name>/atlases/*.png`) from an F5 export (`CoopSkins/export`) and a
character's head cut out of a top-down reference picture. Needs Python 3 with numpy and Pillow.

```
python3 cutout.py <reference.png> <x0,y0,x1,y1 box around head+ears> refs/<name>_head.png
python3 skin.py <path/to/CoopSkins/export> refs <Kazuya|Cookie> <out/Kazuya>
python3 compare.py <export> <out/Kazuya> preview.png "PlayerNoneAnims:Idle:0,PlayerAxeAnims:Fire1:6" 7
```

`skin.py` finds the hat on every sprite of the normal-outfit sheets (largest warm-colored blob), recolors
the coat to black, paints the brim as the hood, pastes the head scaled to the hat (characters face up the
sprite sheets) and adds the tail at the back. Per-character settings are in `CHARACTERS`. Naked, epilogue,
antagonist and blood sheets are left alone. The exported sheets and generated skins are the game's art
and are not kept in this repository.
