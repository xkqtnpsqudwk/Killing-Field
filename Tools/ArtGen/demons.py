"""일반 악마 5종: 슬라임 임프, 블라인드 핑키, 헬리온, 블러드 고스트, 빔 레버넌트."""

import math
from dataclasses import replace
import numpy as np
from core import Canvas
from rig import Dims, Pose, skeleton, T, idle_pose, walk_pose, death_pose
from parts import arm, leg, torso, limb, along, blood_pool, gibs, muzzle_flash

ANIMS = {"IDLE": 4, "MOVE": 6, "ATTACK": 4, "DEATH": 6}
V = lambda *a: np.array(a, np.float32)


def mouth(cv, center, w, open_amt, teeth=True, mat_teeth="bone"):
    """벌린 입: 어두운 구멍 + 위아래 이빨."""
    cv.ellipsoid(T(center), (w, 0.8 + open_amt, 0.8), "blood", bright=-0.35)
    if teeth:
        n = max(2, int(w * 1.1))
        for i in range(n):
            x = center[0] - w * 0.8 + (2 * w * 0.8) * i / max(1, n - 1)
            cv.ellipsoid((x, center[1] + 0.5 + open_amt * 0.6, center[2] + 0.6), (0.45, 0.8, 0.4), mat_teeth, bright=0.2)
            cv.ellipsoid((x + 0.4, center[1] - 0.5 - open_amt * 0.6, center[2] + 0.6), (0.45, 0.7, 0.4), mat_teeth, bright=0.1)


def horn(cv, base, direction, length, r0, mat="bone", curl=0.0):
    pts = [base]
    d = V(*direction)
    d /= np.linalg.norm(d)
    for i in range(1, 4):
        d = d + V(0, curl, 0) * 0.5
        d /= np.linalg.norm(d)
        pts.append(pts[-1] + d * length / 3)
    radii = [r0, r0 * 0.7, r0 * 0.45, r0 * 0.2]
    for i in range(3):
        limb(cv, pts[i], pts[i + 1], radii[i], radii[i + 1], mat, bump=0.1)


def claws(cv, hand, mat="bone", spread=1.0):
    for k in (-1, 0, 1):
        limb(cv, hand, hand + V(k * 1.2 * spread, -2.2, 0.8), 0.6, 0.25, mat, bright=0.1)


# ── 슬라임 임프 ──────────────────────────────────────────────────────────────

def imp(anim, f):
    d = Dims(hip_y=21, spine=13.5, neck=4.0, head_fwd=2.5, shoulder_w=7.5, upper_arm=8.5, fore_arm=8, thigh=10.5, shin=10.5)
    bp = Pose(pitch=0.3, head_pitch=-0.15, l_abduct=0.35, r_abduct=0.35, l_elbow=0.9, r_elbow=0.9,
              l_swing=0.3, r_swing=0.3, l_knee=0.35, r_knee=0.35, l_leg=0.2, r_leg=0.2, l_leg_ab=0.12, r_leg_ab=0.12)
    if anim == "IDLE":
        p = idle_pose(bp, f, amount=1.3)
    elif anim == "MOVE":
        p = walk_pose(bp, f, stride=0.6, arm=0.5)
    elif anim == "ATTACK":
        # 오른손을 뒤로 젖혔다가 던진다
        sw = [-0.6, 0.2, 1.5, 0.6][f]
        p = replace(bp, r_swing=sw, r_abduct=[0.9, 0.7, 0.3, 0.4][f], r_elbow=[1.6, 1.3, 0.2, 0.6][f],
                    pitch=[0.1, 0.2, 0.5, 0.35][f], l_swing=0.6)
    else:
        p = death_pose(bp, f, d)
    cv = Canvas(scale=1.32)
    d.cx = cv.mid
    J = skeleton(d, p)
    skin = "slime"
    dead = anim == "DEATH"
    if dead and f >= 2:
        blood_pool(cv, J["pelvis"][0], 16 + f * 2, 0.5 + 0.12 * f, seed=f)
    for s in ("l", "r"):
        leg(cv, J, s, 2.6, 2.0, skin, foot_mat="green", foot_r=2.2, bump=0.25, seed=2)
    torso(cv, J, 7.0, 5.6, 5.4, skin, bump=0.3, seed=3)
    # 등뼈 가시
    for i in range(4):
        b = along(J, "chest", "spine_rot", [0, -i * 3.0, -3.5])
        limb(cv, b, b + V(0, 2.5, -2.5), 1.1, 0.2, "bone")
    # 어깨 가시
    for s, sx in (("l", -1), ("r", 1)):
        sh = J[s + "_shoulder"]
        limb(cv, sh + V(0, 1, 0), sh + V(sx * 2.5, 4.5, -0.5), 1.4, 0.2, "bone")
        limb(cv, sh + V(sx * 0.8, 0.5, 0), sh + V(sx * 4.0, 2.5, 0.5), 1.1, 0.2, "bone")
    # 배의 근육선
    for i in range(3):
        c = along(J, "belly", "spine_rot", [0, 2 - i * 2.6, 4.2])
        cv.ellipsoid(T(c), (2.6, 1.0, 1.0), "green", bump=0.2, seed=40 + i)
    for s in ("l", "r"):
        arm(cv, J, s, 2.1, 1.8, skin, hand_r=1.7, bump=0.25, seed=5)
        claws(cv, J[s + "_hand"])
    H = J["head"]
    limb(cv, J["neck"], H, 2.0, 2.2, skin, bump=0.2)
    cv.ellipsoid(T(H), (3.8, 3.6, 3.6), skin, bump=0.3, seed=8)
    cv.ellipsoid(T(H + V(0, 1.6, 1.5)), (3.4, 1.2, 2.0), "green", bump=0.2)   # 이마 뼈
    open_amt = 1.4 if anim == "ATTACK" and f in (1, 2) else 0.6
    mouth(cv, H + V(0, -1.6, 3.2), 2.0, open_amt)
    horn(cv, H + V(-2.2, 2.4, 0), (-0.8, 1.0, -0.3), 4.5, 1.0, curl=0.2)
    horn(cv, H + V(2.2, 2.4, 0), (0.8, 1.0, -0.3), 4.5, 1.0, curl=0.2)
    if not (dead and f >= 3):
        cv.glow_dot(H[0] - 1.5, H[1] + 0.5, "eye_yellow", r=0.8)
        cv.glow_dot(H[0] + 1.5, H[1] + 0.5, "eye_yellow", r=0.8)
    if anim == "ATTACK" and f in (0, 1, 2):
        hnd = J["r_hand"]
        rr = [1.8, 2.6, 3.0][f]
        cv.sphere(T(hnd + V(0, 2.0, 2)), rr, "glow:slime_glow")
        cv.sphere(T(hnd + V(-0.4, 2.4, 3)), rr * 0.5, "glow:white")
    if dead and f >= 4:
        gibs(cv, J["pelvis"][0], 3, 18, 6 + f, seed=200 + f, mats=("slime", "blood", "bone", "green"))
    return cv.render()


# ── 블라인드 핑키 ────────────────────────────────────────────────────────────

def pinky(anim, f):
    d = Dims(hip_y=17, spine=13, neck=1.0, head_fwd=7.5, shoulder_w=10.5, hip_w=6, upper_arm=8.5, fore_arm=8.0, thigh=9, shin=9)
    bp = Pose(pitch=0.55, head_pitch=-0.35, l_abduct=0.35, r_abduct=0.35, l_elbow=0.5, r_elbow=0.5,
              l_swing=0.6, r_swing=0.6, l_knee=0.5, r_knee=0.5, l_leg=0.35, r_leg=0.35, l_leg_ab=0.18, r_leg_ab=0.18)
    if anim == "IDLE":
        p = idle_pose(bp, f, amount=1.0)
    elif anim == "MOVE":
        p = walk_pose(bp, f, stride=0.5, arm=0.5, bob=1.5)
    elif anim == "ATTACK":
        p = replace(bp, pitch=[0.4, 0.75, 0.85, 0.6][f], head_pitch=[-0.6, -0.1, 0.1, -0.3][f],
                    l_swing=[0.3, 1.1, 1.2, 0.8][f], r_swing=[0.3, 1.1, 1.2, 0.8][f])
    else:
        p = death_pose(bp, f, d, fall_side=-1)
    cv = Canvas(scale=1.42)
    d.cx = cv.mid
    J = skeleton(d, p)
    skin = "pink"
    dead = anim == "DEATH"
    if dead and f >= 2:
        blood_pool(cv, J["pelvis"][0], 20 + f * 2, 0.6 + 0.12 * f, seed=f)
    for s in ("l", "r"):
        leg(cv, J, s, 4.0, 3.2, skin, foot_mat="brown", foot_r=3.0, bump=0.2, seed=2)
    torso(cv, J, 12.5, 10.5, 8.0, skin, bump=0.25, seed=3)
    # 등의 갈비 튀어나옴
    for i in range(3):
        c = along(J, "chest", "spine_rot", [0, -1 - i * 3.2, 6.5])
        cv.ellipsoid(T(c), (6.5 - i, 1.0, 1.0), "red", bump=0.15, seed=60 + i)
    for s in ("l", "r"):
        arm(cv, J, s, 3.4, 3.0, skin, hand_r=2.6, bump=0.2, seed=5)
        claws(cv, J[s + "_hand"], spread=1.3)
    # 머리: 큰 턱이 앞으로
    H = J["head"]
    hr = J["head_rot"]
    cv.ellipsoid(T(H + V(0, 1.0, -1.0)), (5.6, 4.6, 5.0), skin, bump=0.25, seed=8)
    open_amt = [0.6, 2.8, 3.4, 1.2][f] if anim == "ATTACK" else (0.9 + 0.4 * math.sin(f) if anim != "DEATH" else 2.0)
    upper = H + hr @ V(0, -0.4, 4.4)
    lower = H + hr @ V(0, -3.2 - open_amt, 3.6)
    cv.ellipsoid(T(upper), (5.4, 2.4, 2.4), skin, bump=0.2, seed=9)
    cv.ellipsoid(T(lower), (5.0, 2.0, 2.4), skin, bump=0.2, seed=10)
    gap = (upper + lower) / 2
    cv.ellipsoid(T(gap + V(0, 0, 1.2)), (4.4, 0.6 + open_amt * 0.6, 0.6), "blood", bright=-0.4)
    for i in range(7):
        x = gap[0] - 4 + i * 1.33
        cv.ellipsoid((x, upper[1] - 1.6, upper[2] + 2.0), (0.55, 1.2, 0.4), "bone", bright=0.2)
        cv.ellipsoid((x + 0.6, lower[1] + 1.4, lower[2] + 2.0), (0.55, 1.1, 0.4), "bone", bright=0.15)
    # 눈 없는 자리: 꿰맨 흉터
    for sx in (-1, 1):
        c = H + V(sx * 2.4, 2.4, 3.6)
        cv.ellipsoid(T(c), (1.5, 0.5, 0.4), "red", bright=-0.25)
    if dead and f >= 4:
        gibs(cv, J["pelvis"][0], 3, 22, 8 + f, seed=300 + f, mats=("pink", "blood", "bone"))
    return cv.render()


# ── 헬리온(헬 나이트 계열) ───────────────────────────────────────────────────

def hellion(anim, f):
    d = Dims(hip_y=23, spine=14, neck=6.5, head_fwd=3.5, shoulder_w=9.5, upper_arm=9.5, fore_arm=9, thigh=12, shin=12)
    bp = Pose(pitch=0.15, l_abduct=0.3, r_abduct=0.3, l_elbow=0.5, r_elbow=0.5, l_knee=0.25, r_knee=0.25,
              l_leg=0.15, r_leg=0.15, l_leg_ab=0.08, r_leg_ab=0.08)
    if anim == "IDLE":
        p = idle_pose(bp, f)
    elif anim == "MOVE":
        p = walk_pose(bp, f, stride=0.55, arm=0.4)
    elif anim == "ATTACK":
        p = replace(bp, l_swing=[2.4, 2.8, 1.0, 0.6][f], l_elbow=[1.0, 1.4, 0.2, 0.4][f], l_abduct=[0.5, 0.4, 0.3, 0.3][f],
                    pitch=[0.0, -0.05, 0.4, 0.25][f])
    else:
        p = death_pose(bp, f, d)
    cv = Canvas(scale=1.2)
    d.cx = cv.mid
    J = skeleton(d, p)
    skin = "red"
    dead = anim == "DEATH"
    if dead and f >= 2:
        blood_pool(cv, J["pelvis"][0], 18 + f * 2, 0.5 + 0.12 * f, seed=f)
    for s in ("l", "r"):
        leg(cv, J, s, 3.6, 2.6, "brown", foot_mat="dark", foot_r=2.4, bump=0.3, seed=2)  # 털 난 다리
    torso(cv, J, 8.6, 7.0, 6.6, skin, bump=0.22, seed=3)
    for i in range(2):
        c = along(J, "chest", "spine_rot", [0, -1 - i * 4.0, 5.5])
        cv.ellipsoid(T(c + V(-2.4, 0, 0)), (2.6, 2.0, 1.2), "red", bump=0.2, seed=70 + i, bright=0.05)
        cv.ellipsoid(T(c + V(2.4, 0, 0)), (2.6, 2.0, 1.2), "red", bump=0.2, seed=72 + i, bright=0.05)
    # 어깨 갑주(뼈 판)
    for s, sx in (("l", -1), ("r", 1)):
        cv.ellipsoid(T(J[s + "_shoulder"] + V(sx * 0.5, 1.2, 0)), (3.6, 2.6, 3.4), "bone", bump=0.15, seed=11)
    for s in ("l", "r"):
        arm(cv, J, s, 2.8, 2.4, skin, hand_r=2.2, bump=0.22, seed=5)
        claws(cv, J[s + "_hand"])
    H = J["head"]
    cv.ellipsoid(T(H), (3.9, 4.2, 3.8), skin, bump=0.2, seed=8)
    mouth(cv, H + V(0, -2.0, 3.0), 1.9, 0.6 + (1.0 if anim == "ATTACK" else 0))
    horn(cv, H + V(-2.6, 2.2, 0), (-1.0, 0.3, 0), 6.0, 1.3, curl=0.6)
    horn(cv, H + V(2.6, 2.2, 0), (1.0, 0.3, 0), 6.0, 1.3, curl=0.6)
    if not (dead and f >= 3):
        cv.glow_dot(H[0] - 1.5, H[1] + 0.6, "eye_red", r=0.8)
        cv.glow_dot(H[0] + 1.5, H[1] + 0.6, "eye_red", r=0.8)
    if anim == "ATTACK" and f in (0, 1, 2):
        hnd = J["l_hand"]
        rr = [3.0, 4.0, 3.0][f]
        cv.sphere(T(hnd + V(0, 1.5, 2)), rr, "glow:fire")
        cv.sphere(T(hnd + V(0, 1.9, 3.5)), rr * 0.55, "glow:fire_core")
    if dead and f >= 4:
        gibs(cv, J["pelvis"][0], 3, 22, 7 + f, seed=400 + f, mats=("red", "blood", "bone"))
    return cv.render()


# ── 블러드 고스트 ────────────────────────────────────────────────────────────

def ghost(anim, f):
    d = Dims(hip_y=26, spine=14, neck=4.0, shoulder_w=8, upper_arm=9, fore_arm=9, thigh=8, shin=8)
    bob = math.sin(f / (6 if anim == "MOVE" else 4) * 2 * math.pi) * 1.4
    bp = Pose(y=bob, pitch=0.2, head_pitch=0.1, l_abduct=0.5, r_abduct=0.5, l_swing=0.9, r_swing=0.9,
              l_elbow=0.6, r_elbow=0.6)
    if anim == "ATTACK":
        bp = replace(bp, l_swing=[1.6, 2.0, 1.2, 1.0][f], r_swing=[1.6, 2.0, 1.2, 1.0][f],
                     l_abduct=[1.1, 0.5, 0.2, 0.4][f], r_abduct=[1.1, 0.5, 0.2, 0.4][f], pitch=[0.0, 0.3, 0.45, 0.3][f])
    k = 0.0
    if anim == "DEATH":
        k = f / 5
        bp = replace(bp, y=-k * 18, l_abduct=0.5 + k, r_abduct=0.5 + k, head_pitch=-0.5 * k, pitch=-0.3 * k)
    cv = Canvas(scale=1.25)
    d.cx = cv.mid
    J = skeleton(d, replace(bp, extra={"free": True}))
    # 넝마 같은 몸과 꼬리
    P = J["pelvis"]
    rng = np.random.default_rng(5)
    for i in range(7):
        sx = (i - 3) * 1.6
        sway = math.sin(f * 1.3 + i) * 1.5
        top = P + V(sx * 0.8, 6, 0)
        low = P + V(sx * 1.25 + sway, -16 - rng.random() * 5 + k * 10, -1)
        limb(cv, top, low, 3.0, 0.6, "red", bump=0.3, seed=i)
    torso(cv, J, 9.0, 7.5, 7.0, "red", bump=0.3, seed=3)
    for s in ("l", "r"):
        arm(cv, J, s, 2.0, 1.6, "red", hand_r=1.4, hand_mat="bone", bump=0.3, seed=5)
        claws(cv, J[s + "_hand"], spread=1.2)
    # 두건 + 해골
    H = J["head"]
    cv.ellipsoid(T(H + V(0, 1.2, -1.2)), (5.0, 5.2, 4.4), "red", bump=0.3, seed=9)
    cv.ellipsoid(T(H + V(0, 0, 1.2)), (3.2, 3.6, 3.2), "bone", bump=0.15, seed=10)
    cv.ellipsoid(T(H + V(0, -2.4, 3.2)), (2.0, 1.2, 1.2), "bone", bump=0.1)
    mouth(cv, H + V(0, -2.6, 4.2), 1.6, 0.5 + (1.4 if anim == "ATTACK" else 0))
    for sx in (-1.3, 1.3):
        cv.ellipsoid((H[0] + sx, H[1] + 0.8, H[2] + 4.0), (1.1, 1.2, 0.5), "dark", bright=-0.3)
        if anim != "DEATH" or f < 4:
            cv.glow_dot(H[0] + sx, H[1] + 0.8, "eye_red", r=0.6)
    if anim == "ATTACK" and f in (1, 2):
        for s in ("l", "r"):
            cv.sphere(T(J[s + "_hand"] + V(0, 0, 2)), 2.0, "glow:eye_red")
    if anim == "DEATH" and f >= 3:
        blood_pool(cv, P[0], 20, 0.6 + 0.15 * f, seed=f)
        gibs(cv, P[0], 2, 20, 4 + f, seed=500 + f, mats=("red", "blood", "bone"))
    img = cv.render()
    # 반투명 유령
    a = img[:, :, 3].astype(np.float32)
    alpha_scale = 0.88 if anim != "DEATH" else max(0.35, 0.88 - f * 0.1)
    img[:, :, 3] = (a * alpha_scale).astype(np.uint8)
    return img


# ── 빔 레버넌트 ──────────────────────────────────────────────────────────────

def revenant(anim, f):
    d = Dims(hip_y=26, spine=15, neck=5.0, shoulder_w=7.5, upper_arm=10, fore_arm=9.5, thigh=13, shin=12.5)
    bp = Pose(pitch=0.08, l_abduct=0.25, r_abduct=0.25, l_elbow=0.4, r_elbow=0.4)
    if anim == "IDLE":
        p = idle_pose(bp, f, amount=0.8)
    elif anim == "MOVE":
        p = walk_pose(bp, f, stride=0.5, arm=0.5)
    elif anim == "ATTACK":
        p = replace(bp, pitch=[-0.05, -0.1, -0.12, 0.0][f], l_abduct=0.6, r_abduct=0.6, l_swing=0.4, r_swing=0.4)
    else:
        p = death_pose(bp, f, d)
    cv = Canvas(scale=1.12)
    d.cx = cv.mid
    J = skeleton(d, p)
    bone = "bone"
    dead = anim == "DEATH"
    if dead and f >= 3:
        blood_pool(cv, J["pelvis"][0], 16, 0.3 + 0.1 * f, seed=f)
    for s in ("l", "r"):
        leg(cv, J, s, 1.5, 1.3, bone, foot_mat="bone", foot_r=1.6, bump=0.15, seed=2)
        cv.sphere(T(J[s + "_knee"]), 2.0, "dark", bump=0.1)    # 무릎 보호대
    cv.ellipsoid(T(J["pelvis"]), (4.5, 2.4, 3.0), bone, bump=0.2)
    # 척추와 갈비
    limb(cv, J["pelvis"], J["chest"], 1.2, 1.2, bone)
    for i in range(5):
        c = along(J, "chest", "spine_rot", [0, -1.5 - i * 2.1, 1.5])
        w = 5.8 - i * 0.6
        cv.ellipsoid(T(c), (w, 0.9, 3.4), bone, bump=0.1, seed=30 + i)
    # 가슴 갑옷 띠와 어깨 발사기
    cv.ellipsoid(T(along(J, "chest", "spine_rot", [0, -0.5, 0.5])), (6.5, 2.2, 3.5), "steel", bump=0.1)
    for s, sx in (("l", -1), ("r", 1)):
        base = J[s + "_shoulder"] + V(sx * 1.0, 3.5, -2.0)
        limb(cv, base + V(0, -2.5, 0), base + V(0, 2.5, 1.0), 2.0, 2.0, "steel", bump=0.08)
        tip = base + V(0, 3.4, 1.4)
        cv.ellipsoid(T(tip), (1.5, 1.0, 1.2), "glow:eye_red" if anim != "ATTACK" else "glow:plasma")
    for s in ("l", "r"):
        arm(cv, J, s, 1.3, 1.1, bone, hand_r=1.3, bump=0.1, seed=5)
        claws(cv, J[s + "_hand"], spread=0.8)
    H = J["head"]
    limb(cv, J["neck"], H, 1.1, 1.1, bone)
    cv.ellipsoid(T(H + V(0, 0.5, 0)), (3.3, 3.8, 3.4), bone, bump=0.12, seed=8)
    cv.ellipsoid(T(H + V(0, -2.6, 1.4)), (2.4, 1.4, 2.0), bone, bump=0.1)
    mouth(cv, H + V(0, -2.6, 3.0), 1.7, 0.4 + (0.8 if anim == "ATTACK" else 0))
    for sx in (-1.3, 1.3):
        cv.ellipsoid((H[0] + sx, H[1] + 0.8, H[2] + 3.4), (1.1, 1.2, 0.4), "dark", bright=-0.3)
        if not (dead and f >= 3):
            cv.glow_dot(H[0] + sx, H[1] + 0.8, "eye_yellow", r=0.55)
    if anim == "ATTACK" and f in (1, 2):
        for s, sx in (("l", -1), ("r", 1)):
            tip = J[s + "_shoulder"] + V(sx * 1.0, 7.0, -0.6)
            muzzle_flash(cv, tip[0], tip[1] + 1.5, tip[2], size=1.0 if f == 1 else 0.7, kind="plasma")
    if dead and f >= 4:
        gibs(cv, J["pelvis"][0], 2, 20, 8 + f, seed=600 + f, mats=("bone", "bone", "steel", "blood"))
    return cv.render()


DRAW = {"slime_imp": imp, "blind_pinky": pinky, "hellion": hellion, "blood_ghost": ghost, "beam_revenant": revenant}


def frames(name):
    fn = DRAW[name]
    return {a: [fn(a, f) for f in range(n)] for a, n in ANIMS.items()}
