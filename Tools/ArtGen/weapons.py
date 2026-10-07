"""1인칭 무기 5종. 시트 한 장에 8프레임: 0~2 대기(숨쉬기), 3~7 발사(섬광 → 반동 → 연기 → 복귀).

화면 아래 가운데에서 위로 뻗는 모양이다. 총구는 멀리 있으니 작고, 손잡이는 가까우니 크게 그려
원근감을 낸다.
"""

import math
import numpy as np
from core import Canvas
from rig import T
from parts import limb

V = lambda *a: np.array(a, np.float32)
FRAMES = 8
WSCALE = 1.3


def frame_motion(i):
    """(좌우 흔들림, 위아래, 반동, 섬광 크기, 연기 단계)"""
    if i < 3:
        t = i / 3 * 2 * math.pi
        return math.sin(t) * 0.6, math.cos(t) * 0.8 - 0.4, 0.0, 0.0, 0
    k = i - 3
    recoil = [0.6, 3.2, 2.2, 1.0, 0.3][k]
    flash = [1.0, 1.35, 0.55, 0.0, 0.0][k]
    smoke = [0, 0, 1, 2, 3][k]
    return 0.0, -recoil, recoil, flash, smoke


def hand(cv, c, r, mat="flesh", grip_dir=1):
    """주먹 쥔 손 + 손가락 마디 + 소매."""
    cv.ellipsoid(T(c), (r * 1.15, r, r), mat, bump=0.15, seed=3)
    for k in range(4):
        cv.sphere(T(c + V(grip_dir * (r * 0.75), r * 0.55 - k * r * 0.42, r * 0.5)), r * 0.38, mat, bump=0.1)
    limb(cv, c + V(-grip_dir * r * 0.2, -r * 0.6, -1), c + V(-grip_dir * r * 0.9, -r * 3.2, -2), r * 1.05, r * 1.2, "olive", bump=0.12, seed=5)
    limb(cv, c + V(-grip_dir * r * 0.45, -r * 1.3, 0), c + V(-grip_dir * r * 0.55, -r * 1.6, 0), r * 1.1, r * 1.1, "dark")


def flash(cv, tip, size, kind="fire"):
    if size <= 0:
        return
    mid, core = ("fire", "fire_core") if kind == "fire" else ("plasma", "plasma_core")
    s = size
    cv.ellipsoid(T(tip + V(0, 3.5 * s, 30)), (5.2 * s, 6.5 * s, 0.1), "glow:fire_dark" if kind == "fire" else "glow:plasma")
    cv.ellipsoid(T(tip + V(0, 3.0 * s, 31)), (3.6 * s, 4.6 * s, 0.1), "glow:" + mid)
    cv.ellipsoid(T(tip + V(0, 2.4 * s, 32)), (1.9 * s, 2.6 * s, 0.1), "glow:" + core)
    for a in (-0.9, 0.9, -2.2, 2.2):
        p = tip + V(math.sin(a) * 6.5 * s, 2.4 * s + math.cos(a) * 6.5 * s, 30.5)
        cv.ellipsoid(T(p), (1.2 * s, 1.6 * s, 0.1), "glow:" + mid)


def smoke(cv, tip, stage):
    if stage <= 0:
        return
    for k in range(3):
        y = min(tip[1] + 2 + stage * 1.6 + k * 2.4, 46.5 - k * 0.5)
        x = tip[0] + math.sin(stage + k) * 1.5
        r = 1.2 + stage * 0.4 + k * 0.3
        cv.ellipsoid((x, y, 40), (r, r, 0.1), "gray", bright=0.25 - stage * 0.05, alpha=0.75 - stage * 0.15)


def amp_pistol(i):
    sx, sy, rec, fl, sm = frame_motion(i)
    cv = Canvas(scale=WSCALE)
    b = V(cv.mid + 2 + sx, 0 + sy, 0)
    tip = b + V(-1, 34 + rec * 0.5, -40)
    # 총몸(뒤에서 본 슬라이드): 아래가 크고 위가 작다
    limb(cv, b + V(0, 14, 10), tip, 4.8, 3.0, "steel", bump=0.05, seed=1)
    cv.box(T(b + V(0, 22, 12)), (4.0, 7, 1), "gray", bevel=0.8)
    for k in range(4):
        cv.box(T(b + V(0, 18 + k * 2.4, 13.5)), (3.2, 0.5, 0.5), "dark", bevel=0.2)
    cv.box(T(tip + V(0, -1.6, 2)), (1.0, 0.9, 1), "gray", bevel=0.2)         # 가늠쇠
    cv.ellipsoid(T(b + V(0, 26, 14)), (1.5, 4.5, 0.4), "glow:plasma" if fl > 0 else "glow:eye_cyan")   # 증폭 코일
    limb(cv, b + V(0, 4, 8), b + V(0, 14, 10), 4.4, 4.2, "dark", bump=0.1)   # 손잡이
    hand(cv, b + V(1.5, 8, 15), 5.5, grip_dir=1)
    flash(cv, tip, fl * 0.9, kind="plasma")
    smoke(cv, tip, sm)
    return cv.render()


def bear_killer(i):
    sx, sy, rec, fl, sm = frame_motion(i)
    cv = Canvas(scale=WSCALE)
    b = V(cv.mid + 0 + sx, -2 + sy, 0)
    tips = []
    for side in (-1, 1):
        base = b + V(side * 4.2, 10, 10)
        tip = b + V(side * 2.4, 36 + rec * 0.4, -40)
        limb(cv, base, tip, 4.0, 2.4, "steel", bump=0.04, seed=side + 2)
        cv.ellipsoid(T(tip + V(0, 0.2, 1)), (1.6, 1.6, 0.4), "dark", bright=-0.3)   # 총구 구멍
        tips.append(tip)
    limb(cv, b + V(0, 12, 12), b + V(0, 30, -20), 1.5, 1.0, "gray", bright=0.1)    # 가운데 리브
    # 나무 앞손잡이와 개머리
    limb(cv, b + V(0, 0, 20), b + V(0, 14, 8), 9.0, 6.5, "wood", bump=0.15, seed=4)
    for k in range(3):
        cv.ellipsoid(T(b + V(0, 4 + k * 3.5, 22 - k * 3)), (7.5 - k, 0.5, 1), "wood", bright=-0.3)
    hand(cv, b + V(-9, 8, 18), 5.5, grip_dir=1)
    hand(cv, b + V(10, 4, 18), 5.5, grip_dir=-1)
    for t in tips:
        flash(cv, t, fl * 0.8)
        smoke(cv, t, sm)
    return cv.render()


def h_chaingun(i):
    sx, sy, rec, fl, sm = frame_motion(i)
    cv = Canvas(scale=WSCALE)
    b = V(cv.mid + 0 + sx, -2 + sy * 0.6, 0)
    spin = (i - 3) * 0.55 if i >= 3 else 0
    tip_c = b + V(0, 34 + rec * 0.3, -40)
    # 몸통
    limb(cv, b + V(0, 4, 12), b + V(0, 18, 0), 11, 9, "olive", bump=0.12, seed=1)
    cv.box(T(b + V(0, 12, 14)), (8, 5, 1.5), "steel", bevel=0.8)
    cv.box(T(b + V(0, 12, 16)), (2.5, 2.5, 0.4), "glow:eye_yellow" if fl == 0 else "glow:fire")
    # 6연발 총열
    for k in range(6):
        a = k / 6 * 2 * math.pi + spin
        off = V(math.cos(a) * 4.6, math.sin(a) * 2.2, 0)
        far = V(math.cos(a) * 2.4, math.sin(a) * 1.2, 0)
        limb(cv, b + V(0, 18, 2) + off, tip_c + far, 1.8, 1.1, "steel" if k % 2 else "gray", bump=0.04)
    for t in (0.3, 0.8):
        cv.ellipsoid(T(b + V(0, 18 + 16 * t, 2 - 42 * t)), (5.5 - 2.5 * t, 3.0 - 1.4 * t, 1), "dark", bump=0.05)
    hand(cv, b + V(-12, 6, 16), 5.8, grip_dir=1)
    hand(cv, b + V(12, 8, 16), 5.8, grip_dir=-1)
    flash(cv, tip_c, fl * (1.0 + 0.2 * (i % 2)))
    smoke(cv, tip_c, sm)
    return cv.render()


def auto_cannon(i):
    sx, sy, rec, fl, sm = frame_motion(i)
    cv = Canvas(scale=WSCALE)
    b = V(cv.mid + -2 + sx, -2 + sy, 0)
    tip = b + V(2, 36 + rec * 0.3, -40)
    # 탄띠 통(왼쪽)과 황동 탄
    cv.ellipsoid(T(b + V(-13, 10, 6)), (7, 8, 6), "olive", bump=0.1)
    for k in range(5):
        cv.ellipsoid(T(b + V(-7 + k * 1.6, 14 + k * 1.2, 10)), (1.0, 1.6, 0.8), "orange", bright=0.1)
    limb(cv, b + V(2, 6, 12), tip, 7.5, 4.2, "steel", bump=0.05, seed=2)
    for t in (0.25, 0.5, 0.75):
        cv.sphere(T(b + V(2, 6, 12) + (tip - b - V(2, 6, 12)) * t), 7.5 - 3.3 * t + 0.8, "gray", bump=0.04)
    cv.ellipsoid(T(tip + V(0, 0.4, 1)), (2.6, 2.6, 0.4), "dark", bright=-0.4)
    hand(cv, b + V(12, 6, 16), 6.0, grip_dir=-1)
    hand(cv, b + V(-6, 0, 18), 5.6, grip_dir=1)
    flash(cv, tip, fl * 1.3)
    smoke(cv, tip, sm)
    return cv.render()


def dual_berettas(i):
    sx, sy, rec, fl, sm = frame_motion(i)
    cv = Canvas(scale=WSCALE)
    # 좌우 번갈아 쏜다: 홀수 발사 프레임은 왼쪽 반동이 더 크다
    tips = []
    for side in (-1, 1):
        k = 1.0 if side > 0 else 0.7
        b = V(cv.mid + side * 11.5 + sx, -2 + sy * k, 0)
        tip = b + V(-side * 2, 30 + rec * 0.4 * k, -40)
        limb(cv, b + V(0, 14, 10), tip, 3.8, 2.4, "gray", bump=0.05, seed=side + 3)
        cv.box(T(b + V(0, 20, 12)), (3.0, 5, 1), "steel", bevel=0.6)
        cv.box(T(tip + V(0, -1.4, 2)), (0.8, 0.8, 1), "dark", bevel=0.2)
        limb(cv, b + V(0, 5, 8), b + V(0, 14, 10), 3.6, 3.4, "wood", bump=0.1)
        hand(cv, b + V(side * -1, 8, 15), 4.8, grip_dir=-side)
        tips.append((tip, k))
    for tip, k in tips:
        flash(cv, tip, fl * 0.7 * k)
        smoke(cv, tip, sm)
    return cv.render()


DRAW = {"amp_pistol": amp_pistol, "bear_killer": bear_killer, "h_chaingun": h_chaingun,
        "auto_cannon": auto_cannon, "dual_berettas": dual_berettas}


def sheet(name):
    """가로 8프레임 시트(512×64)."""
    fr = [DRAW[name](i) for i in range(FRAMES)]
    return np.concatenate(fr, axis=1)
