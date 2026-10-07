"""모든 게임 그림을 다시 만든다. 저장소 루트에서: python Tools/ArtGen/build.py

출력(기존 파일을 지우고 새로 쓴다):
  Game/Images/Enemy/<이름>/{IDLE,MOVE,ATTACK,DEATH}/<이름>_<동작><번호>.png   64×64
  Game/Images/Bosses/<이름>/...                                               64×64
  Game/Images/Gun/<이름>/<이름>_sprite_sheet.png                               512×64 (8프레임)
  Game/Images/World/{wall,floor,ceiling,Door,DoorOpen}.png                     256×256
미리보기: Tools/ArtGen/preview/*.png (저장소에는 넣지 않는다)
"""

import os
import shutil
import sys
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import zombies   # noqa: E402
import demons     # noqa: E402
import bosses     # noqa: E402
import weapons    # noqa: E402
import world      # noqa: E402
from preview import sheet as preview_sheet   # noqa: E402

ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
IMAGES = os.path.join(ROOT, "Game", "Images")

ENEMIES = [("zombies", n) for n in zombies.STYLES] + [("demons", n) for n in demons.DRAW]
BOSSES = [("bosses", n) for n in bosses.DRAW]


def save(arr, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    Image.fromarray(arr, "RGBA").save(path, optimize=True)


def write_entity(group, mod, name):
    m = {"zombies": zombies, "demons": demons, "bosses": bosses}[mod]
    root = os.path.join(IMAGES, group, name)
    if os.path.isdir(root):
        shutil.rmtree(root)
    for anim, frames in m.frames(name).items():
        for i, a in enumerate(frames):
            save(a, os.path.join(root, anim, f"{name}_{anim}{i + 1}.png"))


def main():
    for mod, name in ENEMIES:
        write_entity("Enemy", mod, name)
        print("enemy", name)
    for mod, name in BOSSES:
        write_entity("Bosses", mod, name)
        print("boss ", name)
    for name in weapons.DRAW:
        d = os.path.join(IMAGES, "Gun", name)
        if os.path.isdir(d):
            shutil.rmtree(d)
        save(weapons.sheet(name), os.path.join(d, f"{name}_sprite_sheet.png"))
        print("gun  ", name)
    for fname, arr in world.all_textures().items():
        save(arr, os.path.join(IMAGES, "World", fname))
        print("world", fname)
    if "--preview" in sys.argv:
        pv = os.path.join(HERE, "preview")
        os.makedirs(pv, exist_ok=True)
        preview_sheet(ENEMIES, scale=2).save(os.path.join(pv, "enemies.png"))
        preview_sheet(BOSSES, scale=2).save(os.path.join(pv, "bosses.png"))
        print("preview", pv)


if __name__ == "__main__":
    main()
