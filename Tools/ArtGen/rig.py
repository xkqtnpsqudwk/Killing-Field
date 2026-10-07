"""사람 모양 골격과 기본 동작(대기·걷기·공격·죽음).

골격은 각도로 정한 자세에서 관절 위치를 계산한다(순운동학). 각 몬스터는 관절 위치를 받아
자기 몸을 그린다.
"""

import math
from dataclasses import dataclass, field, replace
import numpy as np


def rx(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[1, 0, 0], [0, c, -s], [0, s, c]], np.float32)


def ry(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]], np.float32)


def rz(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]], np.float32)


DOWN = np.array([0, -1, 0], np.float32)
UP = np.array([0, 1, 0], np.float32)


@dataclass
class Dims:
    """몸 치수(픽셀). 기본값은 64px 칸에 맞는 보통 사람 크기."""
    hip_y: float = 24.0        # 골반 높이
    spine: float = 15.0        # 골반 → 목
    neck: float = 4.0
    shoulder_w: float = 8.5    # 어깨 반폭
    hip_w: float = 4.0
    upper_arm: float = 9.0
    fore_arm: float = 8.5
    thigh: float = 11.5
    shin: float = 11.5
    cx: float = 32.0
    head_fwd: float = 0.8      # 머리를 가슴보다 앞(z)으로 내미는 정도


@dataclass
class Pose:
    x: float = 0.0             # 몸 전체 좌우 이동
    y: float = 0.0             # 몸 전체 높이 이동(골반)
    z: float = 0.0
    pitch: float = 0.0         # 상체 앞(+)/뒤(-) 기울기
    roll: float = 0.0          # 상체 옆 기울기(+면 오른쪽(화면 왼쪽)으로)
    body_roll: float = 0.0     # 몸 전체 옆 기울기(쓰러질 때)
    head_pitch: float = 0.0
    head_roll: float = 0.0
    # 팔: swing(+면 앞으로 듦), abduct(+면 바깥으로 벌림), elbow(+면 앞으로 굽힘)
    l_swing: float = 0.0
    l_abduct: float = 0.15
    l_elbow: float = 0.25
    r_swing: float = 0.0
    r_abduct: float = 0.15
    r_elbow: float = 0.25
    # 다리: swing(+면 앞으로), abduct, knee(+면 무릎 굽힘: 정강이가 뒤로)
    l_leg: float = 0.0
    l_leg_ab: float = 0.04
    l_knee: float = 0.05
    r_leg: float = 0.0
    r_leg_ab: float = 0.04
    r_knee: float = 0.05
    extra: dict = field(default_factory=dict)


def skeleton(d: Dims, p: Pose):
    """관절 위치 사전. 왼쪽(l)은 화면 왼쪽이다."""
    J = {}
    root_rot = rz(p.body_roll)
    pelvis = np.array([d.cx + p.x, d.hip_y + p.y, p.z], np.float32)
    J["pelvis"] = pelvis
    spine_rot = root_rot @ rz(p.roll) @ rx(p.pitch)
    chest = pelvis + spine_rot @ (UP * d.spine)
    J["chest"] = chest
    J["belly"] = pelvis + spine_rot @ (UP * d.spine * 0.45)
    J["spine_rot"] = spine_rot
    head_rot = spine_rot @ rz(p.head_roll) @ rx(p.head_pitch)
    J["neck"] = chest + spine_rot @ (UP * d.neck * 0.4)
    J["head"] = chest + head_rot @ (UP * d.neck) + spine_rot @ np.array([0, 0, d.head_fwd], np.float32)
    J["head_rot"] = head_rot
    for side, sx in (("l", -1), ("r", 1)):
        sh = chest + spine_rot @ np.array([sx * d.shoulder_w, -1.0, 0], np.float32)
        swing = getattr(p, side + "_swing")
        ab = getattr(p, side + "_abduct")
        el = getattr(p, side + "_elbow")
        arm_rot = spine_rot @ rz(-sx * ab) @ rx(-swing)
        elbow = sh + arm_rot @ (DOWN * d.upper_arm)
        fore_rot = arm_rot @ rx(-el)
        hand = elbow + fore_rot @ (DOWN * d.fore_arm)
        J[side + "_shoulder"], J[side + "_elbow"], J[side + "_hand"] = sh, elbow, hand
        J[side + "_fore_rot"] = fore_rot
        hip = pelvis + root_rot @ np.array([sx * d.hip_w, 0, 0], np.float32)
        lg = getattr(p, side + "_leg")
        lab = getattr(p, side + "_leg_ab")
        kn = getattr(p, side + "_knee")
        leg_rot = root_rot @ rz(-sx * lab) @ rx(-lg)
        knee = hip + leg_rot @ (DOWN * d.thigh)
        shin_rot = leg_rot @ rx(kn)
        foot = knee + shin_rot @ (DOWN * d.shin)
        J[side + "_hip"], J[side + "_knee"], J[side + "_foot"] = hip, knee, foot
        J[side + "_shin_rot"] = shin_rot
    # 발이 바닥 아래로 내려가지 않게 몸 전체를 올린다(쓰러진 자세는 제외)
    if not p.extra.get("free"):
        low = min(J["l_foot"][1], J["r_foot"][1]) - 1.5
        if low < 0:
            for k, v in list(J.items()):
                if isinstance(v, np.ndarray) and v.shape == (3,):
                    J[k] = v + np.array([0, -low, 0], np.float32)
    return J


def T(v):
    return tuple(float(a) for a in v)


# ── 기본 동작 ────────────────────────────────────────────────────────────────

def idle_pose(base: Pose, f, n=4, amount=1.0):
    t = f / n * 2 * math.pi
    b = math.sin(t) * amount
    return replace(base, y=base.y + b * 0.6, pitch=base.pitch + b * 0.03,
                   l_swing=base.l_swing + b * 0.05, r_swing=base.r_swing - b * 0.05,
                   head_pitch=base.head_pitch - b * 0.04)


def walk_pose(base: Pose, f, n=6, stride=0.55, arm=0.45, bob=1.0):
    t = f / n * 2 * math.pi
    s = math.sin(t)
    c = math.cos(t)
    return replace(base,
                   y=base.y - abs(c) * bob + 0.4,
                   x=base.x + s * 0.6,
                   roll=base.roll + s * 0.05,
                   l_leg=base.l_leg + s * stride, r_leg=base.r_leg - s * stride,
                   l_knee=base.l_knee + max(0, c) * 0.9, r_knee=base.r_knee + max(0, -c) * 0.9,
                   l_swing=base.l_swing - s * arm, r_swing=base.r_swing + s * arm)


def death_pose(base: Pose, f, d: Dims, fall_side=1):
    """6프레임 죽음: 맞고 젖힘 → 무릎 꺾임 → 옆으로 쓰러짐 → 바닥."""
    k = [0.0, 0.22, 0.48, 0.74, 0.92, 1.0][f]
    p = replace(base,
                pitch=base.pitch - 0.35 - 0.5 * k,
                head_pitch=-0.4 - 0.4 * k,
                l_swing=0.9 - 0.6 * k, l_abduct=0.6 + 0.6 * k, l_elbow=0.6,
                r_swing=0.9 - 0.6 * k, r_abduct=0.6 + 0.6 * k, r_elbow=0.6,
                l_knee=0.3 + 1.2 * k, r_knee=0.3 + 1.0 * k,
                l_leg=0.4 * k, r_leg=0.3 * k,
                body_roll=fall_side * 1.35 * max(0, k - 0.25) / 0.75,
                y=base.y - (d.hip_y - 5) * k)
    if k > 0.25:
        p.extra = dict(p.extra, free=True)
    return p
