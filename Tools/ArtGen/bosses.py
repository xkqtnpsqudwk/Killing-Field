"""보스 11종. 사람형 7종, 거미형 3종, 떠다니는 구체 1종."""

import math
from dataclasses import replace
import numpy as np
from core import Canvas
from rig import Dims, Pose, skeleton, T, idle_pose, walk_pose, death_pose
from parts import arm, leg, torso, limb, along, blood_pool, gibs, muzzle_flash
from demons import mouth, horn, claws

ANIMS = {"IDLE": 4, "MOVE": 6, "ATTACK": 4, "DEATH": 6}
V = lambda *a: np.array(a, np.float32)


def cycle(anim, f):
    n = ANIMS[anim]
    return math.sin(f / n * 2 * math.pi)


def humanoid_pose(anim, f, d, bp, attack):
    if anim == "IDLE":
        return idle_pose(bp, f, amount=1.2)
    if anim == "MOVE":
        return walk_pose(bp, f, stride=0.45, arm=0.35, bob=1.4)
    if anim == "ATTACK":
        return attack(bp, f)
    return death_pose(bp, f, d)


def boss_death_fx(cv, J, anim, f, mats, seed, width=26):
    if anim != "DEATH":
        return
    x = J["pelvis"][0]
    if f >= 1:
        blood_pool(cv, x, width + f * 3, 0.5 + 0.12 * f, seed=seed + f)
    if f in (0, 1, 2):
        # 터지는 불꽃
        c = J["chest"]
        for i in range(5 + f * 2):
            a = i * 2.39996 + f
            rr = 3 + f * 3
            p = c + V(math.cos(a) * rr, math.sin(a) * rr * 0.8, 10)
            cv.sphere(T(p), 2.4 - f * 0.4, "glow:fire" if i % 2 else "glow:fire_hot")
    if f >= 3:
        gibs(cv, x, 3, width, 10 + f * 2, seed=seed * 7 + f, mats=mats)


def wings(cv, J, span, mat, flap, membrane="dark", glow=None):
    """등 뒤 박쥐 날개: 뼈대 캡슐 4개 + 뼈 사이 삼각형 막(아래 가장자리는 톱니 모양)."""
    C = J["chest"]
    for sx in (-1, 1):
        root = C + V(sx * 3, 2, -5)
        elbow = root + V(sx * span * 0.45, 7 + flap * 5, -1.5)
        tips = [elbow + V(sx * span * 0.65, 4 + flap * 3, -1),
                elbow + V(sx * span * 0.7, -6 + flap * 2, -1.5),
                elbow + V(sx * span * 0.5, -14 + flap, -2),
                root + V(sx * span * 0.35, -16, -2.5)]
        for i in range(len(tips) - 1):
            mid = (tips[i] + tips[i + 1]) / 2 + V(-sx * 1.5, 2.0, 0)   # 톱니: 가장자리가 안으로 파인다
            cv.triangle(T(elbow), T(tips[i]), T(mid), membrane, bump=0.18, seed=i)
            cv.triangle(T(elbow), T(mid), T(tips[i + 1]), membrane, bump=0.18, seed=i + 5)
        cv.triangle(T(root), T(elbow), T(tips[-1]), membrane, bump=0.18, seed=9)
        limb(cv, root, elbow, 1.6, 1.2, mat, bump=0.1)
        for tip in tips[:3]:
            limb(cv, elbow, tip, 1.1, 0.35, mat, bump=0.1)
        limb(cv, elbow, elbow + V(sx * 0.5, 3, 0), 0.9, 0.2, "bone")   # 날개 발톱


def flame(cv, base, height, width, flick, seed=0):
    """위로 솟는 불꽃 혀: 바깥 어두운 불 → 밝은 속불."""
    rng = np.random.default_rng(seed + flick * 13)
    for layer, (col, k) in enumerate((("fire_dark", 1.0), ("fire", 0.72), ("fire_hot", 0.45), ("fire_core", 0.22))):
        n = 6
        for i in range(n):
            t = i / (n - 1)
            sway = math.sin(t * 3 + flick * 2 + seed) * width * 0.35 * t
            r = width * k * (1 - t * 0.85) + 0.3
            cv.ellipsoid(T(base + V(sway + (rng.random() - 0.5) * 0.6, t * height * (0.6 + 0.4 * k), 0.4 * layer)),
                         (r, r * 1.2, 0.5), "glow:" + col)


# ── 사람형 보스 ──────────────────────────────────────────────────────────────

def azazel(anim, f):
    d = Dims(hip_y=21, spine=14, neck=5.5, head_fwd=3, shoulder_w=10, upper_arm=9, fore_arm=8.5, thigh=10.5, shin=10.5)
    bp = Pose(pitch=0.12, l_abduct=0.4, r_abduct=0.4, l_elbow=0.6, r_elbow=0.6, l_knee=0.3, r_knee=0.3, l_leg=0.15, r_leg=0.15)
    att = lambda b, f: replace(b, l_swing=[1.0, 1.6, 1.7, 1.2][f], r_swing=[1.0, 1.6, 1.7, 1.2][f],
                               l_abduct=[1.3, 0.8, 0.5, 0.6][f], r_abduct=[1.3, 0.8, 0.5, 0.6][f], l_elbow=0.4, r_elbow=0.4)
    p = humanoid_pose(anim, f, d, bp, att)
    cv = Canvas(scale=1.1)
    d.cx = cv.mid
    J = skeleton(d, p)
    flap = cycle(anim, f) if anim in ("IDLE", "MOVE") else (0.6 if anim == "ATTACK" else -0.5)
    if anim != "DEATH" or f < 3:
        wings(cv, J, 16, "red", flap, membrane="dark")
    boss_death_fx(cv, J, anim, f, ("red", "blood", "bone", "dark"), 10)
    for s in ("l", "r"):
        leg(cv, J, s, 3.4, 2.6, "dark", foot_mat="bone", foot_r=2.4, bump=0.25, seed=2)
    torso(cv, J, 9.0, 7.2, 6.8, "red", bump=0.22, seed=3)
    for i in range(3):
        c = along(J, "belly", "spine_rot", [0, 3 - i * 2.5, 4.6])
        cv.ellipsoid(T(c), (3.4, 1.1, 1.0), "red", bright=-0.1, bump=0.1)
    for s, sx in (("l", -1), ("r", 1)):
        cv.ellipsoid(T(J[s + "_shoulder"] + V(sx * 0.5, 1.4, 0)), (3.4, 2.4, 3.2), "dark", bump=0.2)
        limb(cv, J[s + "_shoulder"] + V(sx * 1, 2.5, 0), J[s + "_shoulder"] + V(sx * 3, 6, -1), 1.0, 0.2, "bone")
        arm(cv, J, s, 2.7, 2.3, "red", hand_r=2.1, bump=0.22, seed=5)
        claws(cv, J[s + "_hand"])
    H = J["head"]
    cv.ellipsoid(T(H), (3.7, 4.0, 3.6), "red", bump=0.2, seed=8)
    mouth(cv, H + V(0, -2.0, 3.0), 1.8, 0.5 + (1.2 if anim == "ATTACK" else 0))
    horn(cv, H + V(-2.4, 2.6, 0), (-0.5, 1.0, -0.2), 8.0, 1.4, curl=-0.35)
    horn(cv, H + V(2.4, 2.6, 0), (0.5, 1.0, -0.2), 8.0, 1.4, curl=-0.35)
    if anim != "DEATH" or f < 3:
        cv.glow_dot(H[0] - 1.4, H[1] + 0.6, "eye_yellow", r=0.75)
        cv.glow_dot(H[0] + 1.4, H[1] + 0.6, "eye_yellow", r=0.75)
    if anim == "ATTACK":
        for s in ("l", "r"):
            r = [1.5, 3.0, 3.6, 2.0][f]
            cv.sphere(T(J[s + "_hand"] + V(0, 1.5, 2.5)), r, "glow:fire")
            cv.sphere(T(J[s + "_hand"] + V(0, 1.8, 4.0)), r * 0.5, "glow:fire_core")
    return cv.render()


def behemoth(anim, f):
    d = Dims(hip_y=17, spine=13, neck=3.5, head_fwd=9.0, shoulder_w=12.5, hip_w=5.5, upper_arm=9.5, fore_arm=9, thigh=9, shin=8.5)
    bp = Pose(pitch=0.35, head_pitch=-0.2, l_abduct=0.35, r_abduct=0.35, l_elbow=0.4, r_elbow=0.4, l_swing=0.3, r_swing=0.3,
              l_leg_ab=0.15, r_leg_ab=0.15, l_knee=0.3, r_knee=0.3, l_leg=0.15, r_leg=0.15)
    att = lambda b, f: replace(b, l_swing=[2.6, 2.9, 0.9, 0.5][f], r_swing=[2.6, 2.9, 0.9, 0.5][f],
                               l_elbow=[0.8, 0.6, 0.1, 0.3][f], r_elbow=[0.8, 0.6, 0.1, 0.3][f],
                               l_abduct=0.2, r_abduct=0.2, pitch=[0.1, 0.0, 0.6, 0.45][f])
    p = humanoid_pose(anim, f, d, bp, att)
    cv = Canvas(scale=1.25)
    d.cx = cv.mid
    J = skeleton(d, p)
    boss_death_fx(cv, J, anim, f, ("stone", "blood", "stone", "rust"), 20)
    for s in ("l", "r"):
        leg(cv, J, s, 4.4, 4.0, "stone", foot_mat="stone", foot_r=3.6, bump=0.3, seed=2)
    torso(cv, J, 13.0, 11.0, 9.0, "stone", bump=0.3, seed=3)
    # 용암이 비치는 갈라진 틈
    if anim != "DEATH" or f < 4:
        rng = np.random.default_rng(3)
        for i in range(7):
            c = along(J, "chest", "spine_rot", [(rng.random() - 0.5) * 12, -rng.random() * 13, 7.2])
            cv.ellipsoid(T(c), (0.7 + rng.random() * 1.4, 0.5, 0.3), "glow:lava")
        core = along(J, "chest", "spine_rot", [0, -4, 7.5])
        cv.ellipsoid(T(core), (2.4, 2.4, 0.5), "glow:fire")
        cv.ellipsoid(T(core + V(0, 0, 0.3)), (1.2, 1.2, 0.5), "glow:fire_core")
    for s, sx in (("l", -1), ("r", 1)):
        cv.ellipsoid(T(J[s + "_shoulder"] + V(sx * 1, 2, 0)), (5.2, 4.0, 4.5), "rust", bump=0.25, seed=11)
        arm(cv, J, s, 4.0, 4.2, "stone", hand_r=4.0, bump=0.3, seed=5)
    H = J["head"]
    cv.ellipsoid(T(H), (4.4, 3.8, 3.8), "stone", bump=0.3, seed=8)
    mouth(cv, H + V(0, -1.4, 3.0), 1.6, 0.4 + (0.8 if anim == "ATTACK" else 0))
    horn(cv, H + V(-2.8, 1.2, 0), (-1.0, 0.2, 0.3), 5.0, 1.4, curl=0.5)
    horn(cv, H + V(2.8, 1.2, 0), (1.0, 0.2, 0.3), 5.0, 1.4, curl=0.5)
    if anim != "DEATH" or f < 3:
        cv.glow_dot(H[0] - 1.3, H[1] + 0.6, "eye_red", r=0.7)
        cv.glow_dot(H[0] + 1.3, H[1] + 0.6, "eye_red", r=0.7)
    if anim == "ATTACK" and f == 2:
        for i in range(8):
            a = i / 8 * math.pi
            cv.sphere((J["pelvis"][0] + math.cos(a) * 18, 1.5 + math.sin(a) * 2, 12), 1.6, "glow:fire_dark")
    return cv.render()


def agaures(anim, f):
    d = Dims(hip_y=25, spine=13, neck=6, head_fwd=4, shoulder_w=8, upper_arm=10, fore_arm=9.5, thigh=12, shin=13)
    bp = Pose(pitch=0.4, head_pitch=-0.25, l_abduct=0.5, r_abduct=0.5, l_elbow=0.9, r_elbow=0.9, l_swing=0.5, r_swing=0.5,
              l_leg=0.45, r_leg=0.45, l_knee=1.0, r_knee=1.0, l_leg_ab=0.1, r_leg_ab=0.1)
    att = lambda b, f: replace(b, r_swing=[2.4, 1.6, 0.6, 0.9][f], r_abduct=[1.2, 0.6, 0.1, 0.4][f], r_elbow=[1.4, 0.4, 0.1, 0.6][f],
                               l_swing=[0.6, 1.4, 2.0, 1.0][f], l_abduct=[0.4, 0.9, 1.2, 0.6][f], pitch=[0.3, 0.55, 0.6, 0.45][f])
    p = humanoid_pose(anim, f, d, bp, att)
    if anim == "MOVE":
        p = walk_pose(bp, f, stride=0.7, arm=0.6, bob=2.0)
    cv = Canvas(scale=1.1)
    d.cx = cv.mid
    J = skeleton(d, p)
    boss_death_fx(cv, J, anim, f, ("purple", "blood", "bone"), 30)
    # 악어 꼬리(뒤에서 옆으로 휜다)
    sw = cycle(anim, f) * 4 if anim != "DEATH" else 0
    P = J["pelvis"]
    tail = [P + V(0, -1, -3), P + V(6 + sw, -6, -6), P + V(12 + sw * 1.5, -11, -7), P + V(17 + sw * 2, -14, -6)]
    for i in range(3):
        limb(cv, tail[i], tail[i + 1], 3.0 - i * 0.9, 2.1 - i * 0.8, "olive", bump=0.3, seed=i)
    for s in ("l", "r"):
        leg(cv, J, s, 2.8, 1.8, "purple", foot_mat="dark", foot_r=1.8, bump=0.2, seed=2)
    torso(cv, J, 7.6, 5.4, 5.0, "purple", bump=0.25, seed=3)
    for i in range(4):
        c = along(J, "chest", "spine_rot", [0, -1 - i * 2.5, 3.8])
        cv.ellipsoid(T(c), (3.6 - i * 0.3, 0.7, 0.8), "bone", bright=-0.05)
    for s in ("l", "r"):
        arm(cv, J, s, 2.0, 1.7, "purple", hand_r=1.6, bump=0.2, seed=5)
        h = J[s + "_hand"]
        blade = h + J[s + "_fore_rot"] @ V(0, -9, 0)
        limb(cv, h, blade, 1.2, 0.2, "steel", bright=0.15)
    H = J["head"]
    cv.ellipsoid(T(H + V(0, 0, 1)), (3.0, 3.0, 4.2), "purple", bump=0.2, seed=8)
    cv.ellipsoid(T(H + V(0, -1.6, 4.0)), (2.0, 1.4, 2.4), "purple", bump=0.15)
    mouth(cv, H + V(0, -1.8, 5.6), 1.4, 0.4 + (1.0 if anim == "ATTACK" else 0))
    horn(cv, H + V(-1.8, 2.2, -0.5), (-0.6, 1.0, -0.6), 9.0, 1.2, curl=-0.3)
    horn(cv, H + V(1.8, 2.2, -0.5), (0.6, 1.0, -0.6), 9.0, 1.2, curl=-0.3)
    if anim != "DEATH" or f < 3:
        cv.glow_dot(H[0] - 1.2, H[1] + 0.8, "eye_green", r=0.65)
        cv.glow_dot(H[0] + 1.2, H[1] + 0.8, "eye_green", r=0.65)
    return cv.render()


def abaddon(anim, f):
    d = Dims(hip_y=22, spine=14, neck=5, head_fwd=3, shoulder_w=9.5, upper_arm=9, fore_arm=9, thigh=11, shin=11)
    bp = Pose(pitch=0.15, l_abduct=0.5, r_abduct=0.5, l_elbow=0.8, r_elbow=0.8, l_swing=0.4, r_swing=0.4, l_knee=0.4, r_knee=0.4, l_leg=0.2, r_leg=0.2)
    att = lambda b, f: replace(b, l_swing=[0.4, 1.4, 1.6, 0.8][f], r_swing=[0.4, 1.4, 1.6, 0.8][f],
                               l_abduct=[1.4, 0.6, 0.2, 0.5][f], r_abduct=[1.4, 0.6, 0.2, 0.5][f])
    p = humanoid_pose(anim, f, d, bp, att)
    cv = Canvas(scale=1.12)
    d.cx = cv.mid
    J = skeleton(d, p)
    buzz = (f % 2) * 2 - 1
    if anim != "DEATH" or f < 2:
        C = J["chest"]
        for sx in (-1, 1):
            for k, (ang, ln) in enumerate(((0.9, 17), (0.35, 15))):
                root = C + V(sx * 2.5, 1, -5)
                tip = root + V(sx * math.cos(ang + buzz * 0.12) * ln, math.sin(ang + buzz * 0.12) * ln, -2)
                for t in np.linspace(0.1, 1, 8):
                    p2 = root + (tip - root) * t
                    r = 3.2 * math.sin(t * math.pi) + 0.6
                    cv.ellipsoid(T(p2 + V(0, 0, -2 - k)), (r, r, 0.5), "teal", bump=0.25, seed=k, bright=0.1)
    boss_death_fx(cv, J, anim, f, ("chitin", "slime", "chitin"), 40)
    for s in ("l", "r"):
        leg(cv, J, s, 2.8, 2.0, "chitin", foot_mat="chitin", foot_r=2.0, bump=0.2, seed=2)
        cv.sphere(T(J[s + "_knee"]), 2.6, "chitin", bump=0.15)
    torso(cv, J, 9.0, 7.0, 6.0, "chitin", bump=0.2, seed=3)
    for i in range(5):
        c = along(J, "chest", "spine_rot", [0, 0.5 - i * 2.8, 4.6])
        cv.ellipsoid(T(c), (4.4 - i * 0.4, 1.1, 1.2), "olive", bump=0.1, seed=50 + i)
    for s in ("l", "r"):
        arm(cv, J, s, 2.2, 1.8, "chitin", hand_r=1.8, bump=0.2, seed=5)
        claws(cv, J[s + "_hand"], mat="olive")
    H = J["head"]
    cv.ellipsoid(T(H), (3.8, 3.4, 3.6), "chitin", bump=0.15, seed=8)
    # 겹눈과 큰 턱
    for sx in (-1, 1):
        cv.ellipsoid((H[0] + sx * 2.2, H[1] + 0.8, H[2] + 2.4), (1.6, 1.9, 1.2), "glow:eye_green" if (anim != "DEATH" or f < 3) else "dark")
        m_open = 0.6 if anim == "ATTACK" and f in (1, 2) else 0.2
        limb(cv, H + V(sx * 1.5, -2, 2.5), H + V(sx * (0.6 - m_open * 2), -5, 4), 1.0, 0.3, "bone", bright=0.1)
    # 왕관 가시
    for k in range(5):
        a = (k - 2) * 0.35
        limb(cv, H + V(math.sin(a) * 3, 2.5, -0.5), H + V(math.sin(a) * 5, 7 - abs(k - 2), -1), 0.9, 0.2, "orange", bright=0.1)
    return cv.render()


def afrit(anim, f):
    d = Dims(hip_y=23, spine=14, neck=5, head_fwd=3, shoulder_w=9.5, upper_arm=9, fore_arm=8.5, thigh=11, shin=11)
    bp = Pose(pitch=0.1, l_abduct=0.55, r_abduct=0.55, l_elbow=0.7, r_elbow=0.7, l_swing=0.6, r_swing=0.6)
    att = lambda b, f: replace(b, r_swing=[2.5, 2.8, 1.2, 0.8][f], r_elbow=[1.3, 1.5, 0.2, 0.5][f], r_abduct=0.4, pitch=[0, -0.05, 0.3, 0.2][f])
    p = humanoid_pose(anim, f, d, bp, att)
    cv = Canvas(scale=1.12)
    d.cx = cv.mid
    J = skeleton(d, p)
    flick = f % 3
    alive = anim != "DEATH" or f < 3
    if alive:
        C = J["chest"]
        for sx in (-1, 1):
            for k in range(4):
                b = C + V(sx * (5 + k * 4), 1 - k * 2.5, -7 - k)
                flame(cv, b, 14 - k * 2, 3.2 - k * 0.4, flick, seed=k * 3 + (sx > 0))
    boss_death_fx(cv, J, anim, f, ("dark", "dark", "rust"), 50)
    for s in ("l", "r"):
        leg(cv, J, s, 3.0, 2.4, "stone", foot_mat="dark", foot_r=2.2, bump=0.3, seed=2)
    torso(cv, J, 9.0, 7.0, 6.4, "stone", bump=0.3, seed=3)
    for s in ("l", "r"):
        arm(cv, J, s, 2.5, 2.2, "stone", hand_r=2.0, bump=0.3, seed=5)
        claws(cv, J[s + "_hand"], mat="rust")
    # 몸의 용암 금
    rng = np.random.default_rng(4)
    for key in ("chest", "belly", "l_knee", "r_knee", "l_elbow", "r_elbow"):
        if alive:
            c = J[key] + V((rng.random() - 0.5) * 3, (rng.random() - 0.5) * 3, 5)
            cv.ellipsoid(T(c), (1.4, 0.5, 0.3), "glow:lava")
    H = J["head"]
    cv.ellipsoid(T(H), (3.2, 3.6, 3.2), "bone", bump=0.12, seed=8)
    mouth(cv, H + V(0, -2.2, 2.6), 1.6, 0.5 + (0.9 if anim == "ATTACK" else 0))
    horn(cv, H + V(-2.2, 2.2, 0), (-1, 0.6, 0), 6, 1.2, mat="dark", curl=0.4)
    horn(cv, H + V(2.2, 2.2, 0), (1, 0.6, 0), 6, 1.2, mat="dark", curl=0.4)
    if alive:
        flame(cv, H + V(0, 2.5, -1.5), 10, 3.4, flick, seed=40)
        for sx in (-1.3, 1.3):
            cv.ellipsoid((H[0] + sx, H[1] + 0.6, H[2] + 3.0), (1.0, 1.1, 0.4), "glow:fire_hot")
    if anim == "ATTACK":
        r = [2.5, 3.6, 3.0, 1.6][f]
        cv.sphere(T(J["r_hand"] + V(0, 2, 3)), r, "glow:fire")
        cv.sphere(T(J["r_hand"] + V(0, 2.3, 4.5)), r * 0.5, "glow:fire_core")
    return cv.render()


def agatho(anim, f):
    d = Dims(hip_y=21, spine=13.5, neck=6, head_fwd=5, shoulder_w=9.5, upper_arm=9, fore_arm=8.5, thigh=10.5, shin=11)
    bp = Pose(pitch=0.2, l_abduct=0.4, r_abduct=0.4, l_elbow=0.7, r_elbow=0.7, l_knee=0.7, r_knee=0.7, l_leg=0.35, r_leg=0.35)
    att = lambda b, f: replace(b, l_swing=[2.0, 2.6, 1.4, 1.0][f], l_elbow=[1.0, 1.2, 0.2, 0.5][f], l_abduct=0.5,
                               r_swing=[0.4, 0.6, 1.2, 0.8][f], pitch=[0.1, 0.0, 0.3, 0.2][f])
    p = humanoid_pose(anim, f, d, bp, att)
    cv = Canvas(scale=1.12)
    d.cx = cv.mid
    J = skeleton(d, p)
    flap = cycle(anim, f) if anim in ("IDLE", "MOVE") else 0.3
    if anim != "DEATH" or f < 3:
        wings(cv, J, 15, "brown", flap, membrane="rust")
    boss_death_fx(cv, J, anim, f, ("purple", "blood", "bone"), 60)
    for s in ("l", "r"):
        leg(cv, J, s, 3.2, 2.0, "brown", foot_mat="dark", foot_r=2.0, bump=0.35, seed=2)   # 염소 다리
    torso(cv, J, 9.0, 7.0, 6.4, "purple", bump=0.22, seed=3)
    for i in range(3):
        c = along(J, "belly", "spine_rot", [0, 3 - i * 2.6, 4.4])
        cv.ellipsoid(T(c), (3.0, 1.0, 1.0), "purple", bright=-0.12)
    for s in ("l", "r"):
        arm(cv, J, s, 2.6, 2.2, "purple", hand_r=2.0, bump=0.22, seed=5)
        claws(cv, J[s + "_hand"])
    # 염소 머리
    H = J["head"]
    cv.ellipsoid(T(H), (3.4, 3.8, 3.4), "purple", bump=0.2, seed=8)
    cv.ellipsoid(T(H + V(0, -2.4, 2.4)), (2.0, 2.4, 2.2), "purple", bump=0.15)
    mouth(cv, H + V(0, -3.6, 4.2), 1.2, 0.3 + (0.8 if anim == "ATTACK" else 0), teeth=False)
    for sx in (-1, 1):
        horn(cv, H + V(sx * 2.0, 2.6, -0.5), (sx * 0.6, 0.8, -0.8), 7.5, 1.3, curl=-0.6)
        limb(cv, H + V(sx * 3, 1, 0), H + V(sx * 5, 0, -0.5), 0.9, 0.4, "purple")   # 귀
    if anim != "DEATH" or f < 3:
        cv.glow_dot(H[0] - 1.3, H[1] + 0.8, "eye_red", r=0.65)
        cv.glow_dot(H[0] + 1.3, H[1] + 0.8, "eye_red", r=0.65)
    if anim == "ATTACK" and f in (0, 1, 2):
        r = [2.0, 3.2, 2.6][f]
        cv.sphere(T(J["l_hand"] + V(0, 2, 2.5)), r, "glow:eye_green")
        cv.sphere(T(J["l_hand"] + V(0, 2.3, 4)), r * 0.5, "glow:white")
    return cv.render()


def annihilator(anim, f):
    d = Dims(hip_y=22, spine=15, neck=4.5, head_fwd=3.5, shoulder_w=11, hip_w=5, upper_arm=10, fore_arm=9.5, thigh=11, shin=11)
    bp = Pose(pitch=0.1, l_abduct=0.45, r_abduct=0.3, l_elbow=0.9, l_swing=0.5, r_elbow=0.3, l_knee=0.25, r_knee=0.25, l_leg=0.1, r_leg=0.1)
    att = lambda b, f: replace(b, l_swing=[1.3, 1.45, 1.35, 1.0][f], l_elbow=0.1, l_abduct=0.25, pitch=[0.05, -0.05, 0.0, 0.05][f])
    p = humanoid_pose(anim, f, d, bp, att)
    cv = Canvas(scale=1.1)
    d.cx = cv.mid
    J = skeleton(d, p)
    boss_death_fx(cv, J, anim, f, ("brown", "blood", "steel", "bone"), 70)
    leg(cv, J, "l", 3.6, 3.0, "brown", foot_mat="brown", foot_r=3.0, bump=0.25, seed=2)
    leg(cv, J, "r", 3.4, 2.6, "steel", foot_mat="steel", foot_r=3.0, bump=0.06, seed=2)   # 기계 다리
    cv.sphere(T(J["r_knee"]), 2.6, "steel", bright=0.1)
    torso(cv, J, 11.0, 9.0, 7.5, "brown", bump=0.22, seed=3)
    for i in range(3):
        c = along(J, "chest", "spine_rot", [0, -0.5 - i * 3.5, 6.2])
        cv.ellipsoid(T(c + V(-2.6, 0, 0)), (2.6, 1.6, 1.0), "brown", bright=0.05, bump=0.2)
        cv.ellipsoid(T(c + V(2.6, 0, 0)), (2.6, 1.6, 1.0), "brown", bright=0.05, bump=0.2)
    # 가슴의 금속 판과 배선
    cv.box(T(along(J, "belly", "spine_rot", [3.5, -1, 6.0])), (2.0, 3.0, 0.6), "steel", bevel=0.4)
    for s, sx in (("l", -1), ("r", 1)):
        cv.ellipsoid(T(J[s + "_shoulder"] + V(sx * 0.5, 1.5, 0)), (4.0, 3.0, 3.6), "brown", bump=0.25)
    # 오른팔(화면 오른쪽)은 살, 왼팔은 로켓 발사기
    arm(cv, J, "r", 3.0, 2.6, "brown", hand_r=2.4, bump=0.22, seed=5)
    claws(cv, J["r_hand"])
    limb(cv, J["l_shoulder"], J["l_elbow"], 3.2, 3.0, "brown", bump=0.22)
    fr = J["l_fore_rot"]
    gun_end = J["l_elbow"] + fr @ V(0, -11.5, 0)
    limb(cv, J["l_elbow"], gun_end, 3.6, 3.0, "steel", bump=0.05)
    for t in (0.3, 0.6):
        cv.sphere(T(J["l_elbow"] + fr @ V(0, -11.5 * t, 0)), 3.9, "gray", bump=0.05)
    cv.sphere(T(gun_end + fr @ V(0, -0.5, 0)), 2.0, "dark")
    if anim == "ATTACK" and f in (1, 2):
        muzzle_flash(cv, gun_end[0], gun_end[1], gun_end[2], size=1.6 if f == 1 else 1.0)
    H = J["head"]
    cv.ellipsoid(T(H), (3.6, 3.8, 3.5), "brown", bump=0.2, seed=8)
    mouth(cv, H + V(0, -2.0, 3.0), 1.8, 0.5)
    horn(cv, H + V(-2.6, 2.0, 0), (-1, 0.5, 0.3), 7, 1.5, curl=0.7)
    horn(cv, H + V(2.6, 2.0, 0), (1, 0.5, 0.3), 7, 1.5, curl=0.7)
    if anim != "DEATH" or f < 3:
        cv.glow_dot(H[0] - 1.4, H[1] + 0.7, "eye_red", r=0.7)
        cv.glow_dot(H[0] + 1.4, H[1] + 0.7, "eye_red", r=0.7)
    return cv.render()


# ── 거미형 ──────────────────────────────────────────────────────────────────

def spider_legs(cv, body, n_pairs, reach, height, phase, mat, r=1.8, mech=False, death_k=0.0, seed=0):
    """몸 양쪽으로 다리를 뻗는다. phase로 번갈아 든다."""
    for i in range(n_pairs):
        zf = (i / max(1, n_pairs - 1) - 0.5) * 2          # 앞(+)·뒤(-)
        for sx in (-1, 1):
            lift = max(0.0, math.sin(phase + i * math.pi / 2 + (0 if sx < 0 else math.pi))) * 4
            hip = body + V(sx * 4, 0, zf * 4)
            spread = reach * (0.75 + 0.25 * (1 - abs(zf)))
            foot = V(body[0] + sx * spread, lift * (1 - death_k), body[2] + zf * 9)
            knee = (hip + foot) / 2 + V(sx * 2, height * (1 - death_k * 0.8), 0)
            if death_k:
                foot = foot + V(0, 0, 0)
                knee = knee + V(-sx * death_k * 4, -death_k * height * 0.6, 0)
            limb(cv, hip, knee, r * 1.1, r * 0.9, mat, bump=0.15 if not mech else 0.04, seed=seed + i)
            limb(cv, knee, foot, r * 0.9, r * 0.4, mat, bump=0.15 if not mech else 0.04, seed=seed + i + 9)
            if mech:
                cv.sphere(T(knee), r * 1.3, "gray", bright=0.1)


def arachnocortex(anim, f):
    cv = Canvas(scale=1.0)
    phase = f / ANIMS[anim] * 2 * math.pi
    k = 0.0 if anim != "DEATH" else min(1, f / 4)
    bob = math.sin(phase) * 0.8 if anim in ("IDLE", "MOVE") else 0
    body = V(cv.mid, 25 + bob - k * 17, 0)
    boss_death_fx(cv, {"pelvis": body, "chest": body + V(0, 8, 0)}, anim, f, ("pink", "blood", "steel"), 80, width=34)
    spider_legs(cv, body, 3, 28, 20, phase if anim == "MOVE" else 0, "steel", r=2.0, mech=True, death_k=k, seed=1)
    # 기계 차대
    cv.ellipsoid(T(body), (12, 5.5, 9), "steel", bump=0.05, seed=2)
    cv.ellipsoid(T(body + V(0, -2, 4)), (10, 3, 6), "gray", bump=0.05)
    # 뇌
    if anim != "DEATH" or f < 4:
        brain = body + V(0, 9.5, 0)
        cv.ellipsoid(T(brain), (12.5, 9, 10), "pink", bump=0.35, seed=3)
        for i in range(5):
            x = brain[0] - 9 + i * 4.5
            cv.ellipsoid((x, brain[1] + 1 + (i % 2) * 2, brain[2] + 8.5), (0.6, 6.5 - abs(i - 2) * 1.4, 0.6), "red", bright=-0.2)
        for sx in (-1, 1):
            cv.glow_dot(body[0] + sx * 4, body[1] + 1.5, "eye_red", r=1.0, z=20)
    # 아래쪽 체인건
    gun = body + V(0, -4.5, 8)
    limb(cv, gun + V(0, 0, -3), gun + V(0, -1, 4), 3.0, 2.6, "dark", bump=0.05)
    for a in range(6):
        ang = a / 6 * 2 * math.pi + (phase if anim == "ATTACK" else 0)
        cv.sphere(T(gun + V(math.cos(ang) * 1.8, -1 + math.sin(ang) * 1.8, 5)), 0.9, "gray", bright=0.15)
    if anim == "ATTACK" and f % 2 == 1:
        muzzle_flash(cv, gun[0], gun[1] - 1, gun[2] + 5, size=1.5)
    return cv.render()


def arachnobaron(anim, f):
    cv = Canvas(scale=1.0)
    phase = f / ANIMS[anim] * 2 * math.pi
    k = 0.0 if anim != "DEATH" else min(1, f / 4)
    bob = math.sin(phase) * 0.8 if anim in ("IDLE", "MOVE") else 0
    body = V(cv.mid, 18 + bob - k * 12, 0)
    boss_death_fx(cv, {"pelvis": body, "chest": body + V(0, 12, 0)}, anim, f, ("chitin", "blood", "red"), 90, width=34)
    spider_legs(cv, body, 3, 27, 16, phase if anim == "MOVE" else 0, "chitin", r=2.0, death_k=k, seed=1)
    cv.ellipsoid(T(body + V(0, 0, -4)), (11, 6.5, 10), "chitin", bump=0.2, seed=2)
    # 바론 상체
    d = Dims(cx=body[0], hip_y=body[1] + 3, spine=12, neck=5, head_fwd=2.5, shoulder_w=8.5, upper_arm=8, fore_arm=8)
    att = anim == "ATTACK"
    p = Pose(pitch=0.05 if anim != "DEATH" else -0.6 * k, l_abduct=0.45, r_abduct=0.45, l_elbow=0.7, r_elbow=0.7,
             l_swing=[2.4, 2.7, 1.2, 0.8][f] if att else 0.4, r_swing=[2.4, 2.7, 1.2, 0.8][f] if att else 0.4,
             extra={"free": True})
    J = skeleton(d, p)
    torso(cv, J, 8.6, 7.0, 7.4, "tan", bump=0.2, seed=3)
    for s in ("l", "r"):
        arm(cv, J, s, 2.4, 2.1, "tan", hand_r=1.9, bump=0.2, seed=5)
        claws(cv, J[s + "_hand"])
    H = J["head"]
    cv.ellipsoid(T(H), (3.4, 3.6, 3.2), "tan", bump=0.2, seed=8)
    mouth(cv, H + V(0, -1.8, 2.8), 1.6, 0.5 + (1.0 if att else 0))
    horn(cv, H + V(-2.2, 2.2, 0), (-1, 0.5, 0), 6, 1.2, curl=0.7)
    horn(cv, H + V(2.2, 2.2, 0), (1, 0.5, 0), 6, 1.2, curl=0.7)
    if anim != "DEATH" or f < 3:
        cv.glow_dot(H[0] - 1.3, H[1] + 0.6, "eye_green", r=0.65)
        cv.glow_dot(H[0] + 1.3, H[1] + 0.6, "eye_green", r=0.65)
    if att and f in (1, 2):
        for s in ("l", "r"):
            cv.sphere(T(J[s + "_hand"] + V(0, 2, 2)), 2.8, "glow:eye_green")
            cv.sphere(T(J[s + "_hand"] + V(0, 2.2, 3.5)), 1.3, "glow:white")
    return cv.render()


def arachnophyte(anim, f):
    cv = Canvas(scale=1.0)
    phase = f / ANIMS[anim] * 2 * math.pi
    k = 0.0 if anim != "DEATH" else min(1, f / 4)
    bob = math.sin(phase) * 0.8 if anim in ("IDLE", "MOVE") else 0
    body = V(cv.mid, 22 + bob - k * 15, 0)
    boss_death_fx(cv, {"pelvis": body, "chest": body + V(0, 10, 0)}, anim, f, ("green", "slime", "red"), 100, width=34)
    spider_legs(cv, body, 4, 27, 18, phase if anim == "MOVE" else 0, "green", r=1.5, death_k=k, seed=1)
    cv.ellipsoid(T(body), (11, 8, 10), "green", bump=0.3, seed=2)
    # 꽃잎과 입
    if anim != "DEATH" or f < 4:
        open_k = [0.6, 1.0, 1.2, 0.8][f] if anim == "ATTACK" else 0.5 + 0.1 * math.sin(phase)
        head = body + V(0, 10, 3)
        for i in range(7):
            a = i / 7 * 2 * math.pi + 0.2
            tip = head + V(math.cos(a) * (7 + 4 * open_k), math.sin(a) * (6 + 4 * open_k), 2)
            limb(cv, head, tip, 2.6, 1.0, "red", bump=0.2, seed=i, bright=0.05)
        cv.ellipsoid(T(head + V(0, 0, 3.5)), (3.5 + open_k * 1.5, 3.5 + open_k * 1.5, 1), "blood", bright=-0.3)
        for i in range(8):
            a = i / 8 * 2 * math.pi
            cv.sphere(T(head + V(math.cos(a) * (2.8 + open_k), math.sin(a) * (2.8 + open_k), 4.5)), 0.6, "bone", bright=0.2)
        cv.sphere(T(head + V(0, 0, 4.6)), 1.4, "glow:slime_glow")
    # 덩굴 줄기
    for i in range(4):
        a = (i - 1.5) * 0.5
        limb(cv, body + V(i * 3 - 4.5, 4, -3), body + V(math.sin(a) * 12, 18 + i, -6), 1.0, 0.4, "olive", bump=0.2)
    if anim == "ATTACK" and f in (1, 2, 3):
        for i in range(6):
            a = i * 1.1 + f
            cv.sphere(T(body + V(math.cos(a) * (6 + f * 4), 12 + math.sin(a) * (5 + f * 3), 14)), 1.2, "glow:slime_glow")
    return cv.render()


def aracnorb(anim, f):
    cv = Canvas(scale=1.0)
    phase = f / ANIMS[anim] * 2 * math.pi
    k = 0.0 if anim != "DEATH" else min(1, f / 4)
    float_y = 32 + math.sin(phase) * 2.0 - k * 20
    body = V(cv.mid, float_y, 0)
    boss_death_fx(cv, {"pelvis": V(cv.mid, 4, 0), "chest": body}, anim, f, ("purple", "blood", "bone"), 110, width=30)
    # 늘어진 다리
    for i in range(6):
        a = (i - 2.5) * 0.42
        sway = math.sin(phase + i) * 2
        p0 = body + V(math.sin(a) * 9, -7, math.cos(a) * 2)
        p1 = p0 + V(math.sin(a) * 6 + sway, -8 + k * 6, 2)
        p2 = p1 + V(math.sin(a) * 2 - sway * 0.5, -8 + k * 6, 1)
        limb(cv, p0, p1, 1.6, 1.1, "dark", bump=0.15)
        limb(cv, p1, p2, 1.1, 0.3, "dark", bump=0.15)
    cv.ellipsoid(T(body), (13, 12, 12), "purple", bump=0.35, seed=3)
    # 왕관 가시
    for i in range(7):
        a = (i - 3) * 0.32
        limb(cv, body + V(math.sin(a) * 9, 9, -1), body + V(math.sin(a) * 13, 17 - abs(i - 3) * 1.5, -2), 1.4, 0.2, "bone", bright=0.1)
    # 여러 개의 눈
    alive = anim != "DEATH" or f < 3
    eyes = [(0, 1, 2.6), (-6, 4, 1.5), (6, 4, 1.5), (-7, -3, 1.3), (7, -3, 1.3), (-3, -6, 1.1), (3, -6, 1.1), (0, 7, 1.2)]
    for ex, ey, er in eyes:
        c = body + V(ex, ey, math.sqrt(max(1, 144 - ex * ex - ey * ey)) + 0.3)
        cv.ellipsoid(T(c), (er + 0.6, er + 0.6, 0.6), "bone", bright=0.15)
        if alive:
            col = "eye_yellow" if not (anim == "ATTACK" and f in (1, 2)) else "fire_core"
            cv.ellipsoid(T(c + V(0, 0, 0.5)), (er * 0.6, er * 0.75, 0.2), "glow:" + col)
            cv.ellipsoid(T(c + V(0, 0, 0.8)), (er * 0.22, er * 0.5, 0.1), "glow:eye_red" if col == "eye_yellow" else "glow:white")
    if anim == "ATTACK" and f in (1, 2):
        c = body + V(0, 1, 16)
        cv.ellipsoid(T(c), (4.5, 4.5, 0.3), "glow:fire")
        cv.ellipsoid(T(c + V(0, 0, 0.2)), (2.5, 2.5, 0.3), "glow:fire_core")
    return cv.render()


DRAW = {
    "azazel": azazel, "behemoth": behemoth, "agaures": agaures, "abaddon": abaddon, "afrit": afrit,
    "agatho_demon": agatho, "annihilator": annihilator, "arachnocortex": arachnocortex,
    "arachnobaron": arachnobaron, "arachnophyte": arachnophyte, "aracnorb_queen": aracnorb,
}


def frames(name):
    fn = DRAW[name]
    return {a: [fn(a, f) for f in range(n)] for a, n in ANIMS.items()}
