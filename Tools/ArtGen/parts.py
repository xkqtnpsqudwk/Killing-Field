"""몬스터들이 함께 쓰는 몸 부위 그리기."""

import math
import numpy as np
from core import Canvas
from rig import T, rx


def limb(cv: Canvas, a, b, ra, rb, mat, **kw):
    cv.capsule(T(a), T(b), ra, rb, mat, **kw)


def arm(cv, J, side, r_up, r_fore, mat, hand_r=None, hand_mat=None, **kw):
    limb(cv, J[side + "_shoulder"], J[side + "_elbow"], r_up, r_up * 0.85, mat, **kw)
    limb(cv, J[side + "_elbow"], J[side + "_hand"], r_fore, r_fore * 0.85, mat, **kw)
    if hand_r:
        cv.sphere(T(J[side + "_hand"]), hand_r, hand_mat or mat, **kw)


def leg(cv, J, side, r_thigh, r_shin, mat, foot_mat=None, foot_r=None, **kw):
    limb(cv, J[side + "_hip"], J[side + "_knee"], r_thigh, r_thigh * 0.8, mat, **kw)
    limb(cv, J[side + "_knee"], J[side + "_foot"], r_shin, r_shin * 0.75, mat, **kw)
    if foot_mat:
        f = J[side + "_foot"]
        fr = foot_r or r_shin * 1.1
        cv.ellipsoid((f[0], f[1] + fr * 0.3, f[2] + fr * 0.6), (fr, fr * 0.7, fr * 1.4), foot_mat, **kw)


def torso(cv, J, w_chest, w_belly, w_hip, mat, depth=0.75, **kw):
    """골반 → 가슴을 좌우 두 줄 캡슐로 채운 몸통."""
    P, B, C = J["pelvis"], J["belly"], J["chest"]
    rot = J["spine_rot"]
    for sx in (-1, 0, 1):
        off_c = rot @ np.array([sx * w_chest * 0.45, 0, 0], np.float32)
        off_b = rot @ np.array([sx * w_belly * 0.45, 0, 0], np.float32)
        off_h = rot @ np.array([sx * w_hip * 0.45, 0, 0], np.float32)
        rc = w_chest * (0.62 if sx == 0 else 0.55)
        rb = w_belly * (0.62 if sx == 0 else 0.55)
        rh = w_hip * (0.62 if sx == 0 else 0.55)
        cv.capsule(T(P + off_h), T(B + off_b), rh, rb, mat, **kw)
        cv.capsule(T(B + off_b), T(C + off_c), rb, rc, mat, **kw)


def along(J, key, rot_key, v):
    """관절 key에서 회전 rot_key 기준 방향 v만큼 떨어진 점."""
    return J[key] + J[rot_key] @ np.array(v, np.float32)


def blood_pool(cv: Canvas, x, w, amount=1.0, seed=0):
    """바닥 핏물. 납작한 타원체 여러 개."""
    if amount <= 0:
        return
    rng = np.random.default_rng(seed)
    for i in range(4):
        ox = (rng.random() - 0.5) * w * 0.8
        cv.ellipsoid((x + ox, 0.6, 2 + rng.random() * 4), (w * 0.35 * amount + 1, 1.6, 3), "blood", bright=0.15)


def gibs(cv: Canvas, x, y, w, n, seed, mats=("flesh", "blood", "bone")):
    rng = np.random.default_rng(seed)
    for i in range(n):
        m = mats[i % len(mats)]
        cx = x + (rng.random() - 0.5) * w
        cy = y + rng.random() * 3
        r = 1.0 + rng.random() * 1.8
        cv.ellipsoid((cx, cy, 4 + rng.random() * 4), (r * 1.3, r, r), m, bump=0.15, seed=i)


def muzzle_flash(cv: Canvas, x, y, z, size=1.0, kind="fire"):
    core, mid = ("fire_core", "fire") if kind == "fire" else ("plasma_core", "plasma")
    cv.ellipsoid((x, y, z + 20), (3.2 * size, 3.2 * size, 0.1), "glow:" + mid)
    cv.ellipsoid((x, y, z + 21), (1.7 * size, 1.7 * size, 0.1), "glow:" + core)
    for a in range(4):
        ang = a * math.pi / 2 + 0.6
        cv.ellipsoid((x + math.cos(ang) * 3.6 * size, y + math.sin(ang) * 3.6 * size, z + 20),
                     (1.0 * size, 1.0 * size, 0.1), "glow:" + mid)
