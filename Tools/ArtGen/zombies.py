"""좀비 과학자 계열 5종: 권총 과학자, 우지 병사, 플라스마 기술자, 유탄 과학자, 연구소 도살자."""

import math
from dataclasses import replace
import numpy as np
from core import Canvas
from rig import Dims, Pose, skeleton, T, idle_pose, walk_pose, death_pose
from parts import arm, leg, torso, limb, along, blood_pool, gibs, muzzle_flash

SCALE = 1.3

ANIMS = {"IDLE": 4, "MOVE": 6, "ATTACK": 4, "DEATH": 6}

STYLES = {
    # coat: 겉옷 재질, pants: 바지, skin: 피부, weapon: 무기 종류, pack: 등짐 유리관 빛
    "zombie_scientist":    dict(coat="coat", pants="gray", skin="zskin", weapon="pistol", pack="eye_green", vest=None, hood=False, apron=False),
    "uzi_trooper":         dict(coat="olive", pants="dark", skin="zskin", weapon="uzi", pack=None, vest="dark", hood=False, apron=False, helmet="olive"),
    "plasma_tech":         dict(coat="teal", pants="steel", skin="zskin", weapon="plasma", pack="eye_cyan", vest=None, hood=True, apron=False),
    "grenadier_scientist": dict(coat="orange", pants="dark", skin="zskin", weapon="grenade", pack="eye_yellow", vest="brown", hood=True, apron=False),
    "lab_butcher":         dict(coat="coat", pants="dark", skin="pink", weapon="cleaver", pack=None, vest=None, hood=False, apron=True),
}


def base_dims(name):
    d = Dims(hip_y=23.0, spine=14.5, neck=5.0, shoulder_w=8.2, upper_arm=9.0, fore_arm=8.5, thigh=11.0, shin=11.0)
    if name == "lab_butcher":
        d = Dims(hip_y=22.0, spine=15.5, neck=5.0, shoulder_w=10.0, upper_arm=9.5, fore_arm=9.0, thigh=10.5, shin=10.5)
    return d


def base_pose(name):
    # 좀비답게 앞으로 숙이고 고개를 기울인다
    return Pose(pitch=0.18, head_pitch=0.25, head_roll=0.18, l_swing=0.15, r_swing=0.25,
                l_abduct=0.2, r_abduct=0.18, l_elbow=0.35, r_elbow=0.5)


def attack_pose(base: Pose, f, melee):
    if melee:
        # 0 치켜듦, 1 내려침, 2 끝까지, 3 돌아옴
        sw = [2.6, 1.1, 0.2, 1.2][f]
        el = [1.2, 0.3, 0.1, 0.6][f]
        pt = [0.0, 0.35, 0.45, 0.2][f]
        return replace(base, r_swing=sw, r_elbow=el, r_abduct=0.25, pitch=pt, l_swing=0.6, l_elbow=0.6)
    lift = [1.25, 1.45, 1.35, 1.0][f]
    return replace(base, r_swing=lift, r_elbow=0.05, r_abduct=0.55, l_swing=0.5, l_elbow=0.9, l_abduct=0.1,
                   pitch=0.08 - (0.06 if f == 2 else 0), head_pitch=0.05)


def draw(name, anim, f):
    st = STYLES[name]
    d = base_dims(name)
    bp = base_pose(name)
    melee = st["weapon"] == "cleaver"
    if anim == "IDLE":
        p = idle_pose(bp, f)
    elif anim == "MOVE":
        p = walk_pose(bp, f, stride=0.45, arm=0.3)
    elif anim == "ATTACK":
        p = attack_pose(bp, f, melee)
    else:
        p = death_pose(bp, f, d, fall_side=1 if name != "uzi_trooper" else -1)
    cv = Canvas(scale=SCALE)
    d.cx = cv.mid
    J = skeleton(d, p)
    big = 1.25 if name == "lab_butcher" else 1.0
    skin = st["skin"]

    dead = anim == "DEATH"
    if dead and f >= 2:
        blood_pool(cv, J["pelvis"][0], 16 + f * 3, amount=0.4 + 0.15 * f, seed=f)

    # 다리(바지·신발)
    for s in ("l", "r"):
        leg(cv, J, s, 2.6 * big, 2.2 * big, st["pants"], foot_mat="brown", foot_r=2.3, bump=0.06, seed=3)

    # 등짐 유리관(어깨 뒤로 보인다)
    if st["pack"]:
        top = along(J, "chest", "spine_rot", [-3.5, 3.5, -6])
        bot = along(J, "chest", "spine_rot", [-3.5, -7, -6])
        limb(cv, bot, top, 2.6, 2.6, "steel", bump=0.05)
        cv.ellipsoid(T(top + np.array([0, 1.8, 0.4], np.float32)), (2.3, 1.4, 2.3), "glow:" + st["pack"])

    # 몸통
    torso(cv, J, 7.0 * big, 6.0 * big, 6.4 * big, st["coat"], bump=0.10, seed=11)
    if st["vest"]:
        v1 = along(J, "chest", "spine_rot", [0, -2, 3.5])
        v2 = along(J, "belly", "spine_rot", [0, 0, 3.5])
        limb(cv, v1, v2, 5.6 * big, 5.2 * big, st["vest"], bump=0.08, seed=4)
    if st["apron"]:
        a1 = along(J, "chest", "spine_rot", [0, -3, 4.5])
        a2 = along(J, "pelvis", "spine_rot", [0, -6, 4.5])
        for sx in (-2.2, 2.2):
            limb(cv, a1 + np.array([sx, 0, 0], np.float32), a2 + np.array([sx * 1.3, 0, 0], np.float32), 3.4, 3.8, "tan", bump=0.12, seed=9)
        # 앞치마 핏자국
        for i, (ox, oy) in enumerate(((-2, -1), (1.5, -5), (-1, -9), (2.5, -11))):
            c = a1 + np.array([ox, oy, 2.6], np.float32)
            cv.ellipsoid(T(c), (1.8, 1.4, 1.0), "blood", bright=0.25)
    # 가운 자락(엉덩이 아래로 늘어짐)
    if st["coat"] in ("coat", "teal", "orange") and not st["apron"]:
        for sx in (-1, 1):
            top = along(J, "pelvis", "spine_rot", [sx * 4.0, 2, 1.5])
            low = top + np.array([sx * 1.2, -9.5 * big, 0.5], np.float32)
            if dead and f >= 3:
                low = top + np.array([sx * 4, -3, 2], np.float32)
            limb(cv, top, low, 3.4 * big, 3.0 * big, st["coat"], bump=0.10, seed=12)
        # 열린 가운 사이 셔츠와 넥타이
        s1 = along(J, "chest", "spine_rot", [0, 0.5, 3.2])
        s2 = along(J, "belly", "spine_rot", [0, -2, 3.0])
        limb(cv, s1, s2, 2.3, 2.0, "tan" if st["coat"] == "coat" else "dark", bump=0.1, seed=31)
        if st["coat"] == "coat":
            limb(cv, s1 + np.array([0, 0, 2.2], np.float32), s2 + np.array([0, 3, 2.0], np.float32), 0.55, 0.6, "red")
        # 단추 줄과 핏자국
        c1 = along(J, "chest", "spine_rot", [0, -2, 5.5 * big])
        cv.ellipsoid(T(c1 + np.array([1.5, -4, 0], np.float32)), (2.2, 2.6, 1.0), "blood", bright=0.2)
        cv.ellipsoid(T(c1 + np.array([-2.6, -9, 0], np.float32)), (1.6, 1.4, 1.0), "blood", bright=0.2)

    # 팔
    sleeve = st["coat"]
    for s in ("l", "r"):
        arm(cv, J, s, 2.4 * big, 2.0 * big, sleeve, hand_r=1.9 * big, hand_mat=skin, bump=0.08, seed=5)

    # 머리
    H = J["head"]
    hr = 4.3 * big
    neck = J["neck"]
    limb(cv, neck, H, 2.0 * big, 2.0 * big, skin)
    cv.ellipsoid(T(H + np.array([0, 1.2, 0], np.float32)), (hr * 0.95, hr * 1.05, hr), skin, bump=0.18, seed=21)
    jaw = H + J["head_rot"] @ np.array([0, -2.6, 1.0], np.float32)
    cv.ellipsoid(T(jaw), (hr * 0.72, hr * 0.5, hr * 0.7), skin, bump=0.15, seed=22)
    if st.get("helmet"):
        cv.ellipsoid(T(H + np.array([0, 3.0, -0.3], np.float32)), (hr * 1.08, hr * 0.7, hr * 1.05), st["helmet"], bump=0.05)
    elif st["hood"]:
        cv.ellipsoid(T(H + np.array([0, 1.8, -1.2], np.float32)), (hr * 1.15, hr * 1.12, hr * 1.0), st["coat"], bump=0.08)
        # 방독면 유리
        cv.ellipsoid(T(H + J["head_rot"] @ np.array([0, 1.0, hr * 0.6], np.float32)), (hr * 0.75, hr * 0.5, 1.2), "steel", bright=0.15)
    else:
        # 듬성듬성한 머리카락
        cv.ellipsoid(T(H + np.array([0.6, 3.6, -0.8], np.float32)), (hr * 0.85, hr * 0.45, hr * 0.8), "dark", bump=0.3, seed=7)

    # 얼굴 장식: 눈(빛남)과 입
    eye_y = H[1] + 1.2
    if not (dead and f >= 3):
        if st["hood"]:
            cv.glow_dot(H[0] - 1.5, eye_y + 0.2, "eye_red", r=0.75)
            cv.glow_dot(H[0] + 1.5, eye_y + 0.2, "eye_red", r=0.75)
        else:
            cv.glow_dot(H[0] - 1.6, eye_y, "eye_yellow" if name != "lab_butcher" else "eye_red", r=0.7)
            cv.glow_dot(H[0] + 1.6, eye_y, "eye_yellow" if name != "lab_butcher" else "eye_red", r=0.7)
            mouth = jaw + np.array([0, -0.4, hr * 0.62], np.float32)
            cv.ellipsoid(T(mouth), (1.8, 0.7, 0.3), "blood", bright=-0.2)

    # 무기
    hand = J["r_hand"]
    fr = J["r_fore_rot"]
    tip_dir = fr @ np.array([0, -1, 0], np.float32)
    w = st["weapon"]
    if w in ("pistol", "uzi", "plasma"):
        length = {"pistol": 4.5, "uzi": 6.0, "plasma": 8.5}[w]
        mat = {"pistol": "gray", "uzi": "dark", "plasma": "steel"}[w]
        muzzle = hand + tip_dir * length
        limb(cv, hand, muzzle, 1.6 if w != "plasma" else 2.2, 1.3 if w != "plasma" else 1.8, mat, bump=0.04)
        if w == "uzi":
            mag = hand + np.array([0, -3.5, 0.5], np.float32)
            limb(cv, hand, mag, 1.0, 1.0, "dark")
        if w == "plasma":
            coil = hand + tip_dir * 4
            cv.ellipsoid(T(coil + np.array([0, 0, 1.2], np.float32)), (1.6, 1.6, 1.2), "glow:plasma")
        if anim == "ATTACK" and f in (1, 2):
            muzzle_flash(cv, muzzle[0], muzzle[1], muzzle[2], size=1.0 if f == 1 else 0.7,
                         kind="plasma" if w == "plasma" else "fire")
    elif w == "grenade":
        g = hand + np.array([0, 1.4, 1.2], np.float32)
        if not (anim == "ATTACK" and f in (2, 3)):
            cv.sphere(T(g), 1.9, "olive", bump=0.1)
            cv.glow_dot(g[0] + 0.6, g[1] + 1.6, "eye_red", r=0.55)
    elif w == "cleaver":
        blade_end = hand + tip_dir * 8.5
        side = np.array([2.6, 0, 0], np.float32)
        limb(cv, hand, hand + tip_dir * 2.0, 1.1, 1.1, "wood")
        for k in range(3):
            t0 = hand + tip_dir * (2 + k * 0.1)
            limb(cv, t0 + side * (k / 2), blade_end + side * (k / 2), 0.9, 0.9, "steel", bright=0.08)
        cv.ellipsoid(T(blade_end + side * 0.6 + np.array([0, 0, 1], np.float32)), (1.4, 1.4, 0.6), "blood")

    if dead:
        if f >= 4:
            gibs(cv, J["pelvis"][0], 3, 18, 6 + f, seed=100 + f)
        if f == 0:
            # 맞는 순간 피가 튄다
            for i in range(5):
                a = i * 1.2
                c = J["chest"] + np.array([math.cos(a) * 6, math.sin(a) * 5 + 2, 8], np.float32)
                cv.ellipsoid(T(c), (1.2, 1.2, 0.5), "blood", bright=0.3)
    return cv.render()


def frames(name):
    return {a: [draw(name, a, f) for f in range(n)] for a, n in ANIMS.items()}
