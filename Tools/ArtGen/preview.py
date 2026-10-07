"""생성 결과를 한 장에 모아 보는 미리보기. 사용: python preview.py 출력.png 모듈:이름 [...]"""

import sys
import importlib
import numpy as np
from PIL import Image
from core import upscale


def sheet(entries, scale=3, bg=(48, 44, 52)):
    rows = []
    for mod, name in entries:
        m = importlib.import_module(mod)
        fr = m.frames(name)
        row = []
        for anim in ("IDLE", "MOVE", "ATTACK", "DEATH"):
            row += fr[anim]
        rows.append(row)
    cols = max(len(r) for r in rows)
    W = 64 * scale
    out = Image.new("RGBA", (cols * (W + 4), len(rows) * (W + 4)), bg + (255,))
    for y, row in enumerate(rows):
        for x, a in enumerate(row):
            im = Image.fromarray(upscale(a, scale), "RGBA")
            out.alpha_composite(im, (x * (W + 4), y * (W + 4)))
    return out


if __name__ == "__main__":
    path = sys.argv[1]
    entries = [tuple(s.split(":")) for s in sys.argv[2:]]
    sheet(entries, scale=int(__import__("os").environ.get("SCALE", "3"))).save(path)
