"""벽·바닥·천장·문 텍스처. 128×128로 그린 뒤 2배로 키워 256×256으로 저장한다.

모두 위아래·좌우로 이어 붙여도 이음매가 없게 만든다(문 제외). 분위기는 Doom의 기지 벽:
녹슨 갈색 금속 판, 리벳, 굵은 이음새, 배관, 경고 줄무늬.
"""

import math
import numpy as np
from core import RAMPS, BAYER4

N = 128


def value_noise(n, cells, seed):
    """이어 붙여지는 값 잡음 [0,1)."""
    rng = np.random.default_rng(seed)
    g = rng.random((cells, cells)).astype(np.float32)
    xs = np.arange(n, dtype=np.float32) * cells / n
    i0 = np.floor(xs).astype(int)
    t = xs - i0
    t = t * t * (3 - 2 * t)
    i1 = (i0 + 1) % cells
    a = g[i0][:, i0] * (1 - t)[None, :] + g[i0][:, i1] * t[None, :]
    b = g[i1][:, i0] * (1 - t)[None, :] + g[i1][:, i1] * t[None, :]
    return a * (1 - t)[:, None] + b * t[:, None]


def fbm(n, seed, octaves=((4, 0.5), (8, 0.3), (16, 0.15), (32, 0.08))):
    out = np.zeros((n, n), np.float32)
    tot = 0
    for i, (c, w) in enumerate(octaves):
        out += value_noise(n, c, seed + i * 17) * w
        tot += w
    return out / tot


class Tex:
    """밝기(0~1) 지도와 재질 지도를 칠한 뒤 팔레트로 줄인다."""

    def __init__(self, n=N, base_mat="brown", base=0.45):
        self.n = n
        self.L = np.full((n, n), base, np.float32)
        self.M = np.full((n, n), 0, np.int32)
        self.mats = [base_mat]
        self.glow = np.zeros((n, n, 3), np.float32)
        self.gmask = np.zeros((n, n), bool)
        yy, xx = np.mgrid[0:n, 0:n]
        self.X, self.Y = xx, yy

    def mat(self, name):
        if name not in self.mats:
            self.mats.append(name)
        return self.mats.index(name)

    def rect(self, x0, y0, x1, y1, L=None, mat=None, add=None, bevel=0, light=0.18):
        """사각형 채움. bevel>0이면 위·왼쪽은 밝게, 아래·오른쪽은 어둡게."""
        m = (self.X >= x0) & (self.X < x1) & (self.Y >= y0) & (self.Y < y1)
        if mat is not None:
            self.M[m] = self.mat(mat)
        if L is not None:
            self.L[m] = L
        if add is not None:
            self.L[m] += add
        if bevel:
            top = m & (self.Y < y0 + bevel)
            left = m & (self.X < x0 + bevel)
            bot = m & (self.Y >= y1 - bevel)
            right = m & (self.X >= x1 - bevel)
            self.L[top | left] += light
            self.L[bot | right] -= light
        return m

    def circle(self, cx, cy, r, L=None, mat=None, add=None, shade=True):
        d2 = (self.X - cx) ** 2 + (self.Y - cy) ** 2
        m = d2 <= r * r
        if mat is not None:
            self.M[m] = self.mat(mat)
        if L is not None:
            self.L[m] = L
        if add is not None:
            self.L[m] += add
        if shade:
            # 왼쪽 위가 밝은 반구
            nx = (self.X - cx) / max(r, 0.5)
            ny = (self.Y - cy) / max(r, 0.5)
            self.L[m] += (-nx[m] * 0.5 - ny[m] * 0.5) * 0.25
        return m

    def glow_rect(self, x0, y0, x1, y1, rgb):
        m = (self.X >= x0) & (self.X < x1) & (self.Y >= y0) & (self.Y < y1)
        self.glow[m] = rgb
        self.gmask[m] = True

    def glow_circle(self, cx, cy, r, rgb):
        m = (self.X - cx) ** 2 + (self.Y - cy) ** 2 <= r * r
        self.glow[m] = rgb
        self.gmask[m] = True

    def grime(self, amount=0.18, seed=0):
        self.L += (fbm(self.n, seed) - 0.5) * amount
        rng = np.random.default_rng(seed + 99)
        self.L += (rng.random((self.n, self.n)).astype(np.float32) - 0.5) * amount * 0.5

    def render(self, scale=2):
        img = np.zeros((self.n, self.n, 4), np.uint8)
        dither = np.tile(BAYER4, (self.n // 4, self.n // 4))
        for i, name in enumerate(self.mats):
            m = self.M == i
            ramp = RAMPS[name]
            k = len(ramp)
            lvl = np.clip(np.floor(self.L[m] * (k - 0.01) + dither[m] * 0.5), 0, k - 1).astype(int)
            img[m, :3] = ramp[lvl]
        img[self.gmask, :3] = self.glow[self.gmask]
        img[:, :, 3] = 255
        return np.repeat(np.repeat(img, scale, 0), scale, 1)


def rivets(t, xs, ys, r=1.4):
    for x in xs:
        for y in ys:
            t.circle(x, y, r + 0.6, add=-0.25, shade=False)
            t.circle(x - 0.3, y - 0.3, r, L=0.62, mat="gray")


def hazard(t, x0, y0, x1, y1, phase=0):
    m = t.rect(x0, y0, x1, y1)
    stripe = ((t.X + t.Y + phase) // 6) % 2 == 0
    t.M[m & stripe] = t.mat("orange")
    t.L[m & stripe] = 0.8
    t.M[m & ~stripe] = t.mat("dark")
    t.L[m & ~stripe] = 0.4
    t.rect(x0, y0, x1, y0 + 1, add=0.15)
    t.rect(x0, y1 - 1, x1, y1, add=-0.25)


# ── 벽 규칙 ──
# 벽은 모두 같은 높이(BAND)에 가로 띠가 있다. 종류가 달라도 띠가 이어져 방이 한 덩어리로 보인다.
# 종류별 역할: 기지(일반 전투), 연구소(시작실·상점), 지옥(보스), 골조(방 경계·문틀·기둥), 엄폐물.
BAND = (46, 58)


def wall():
    """기지 벽: 녹슨 갈색 금속 판(일반 전투방)."""
    t = Tex(base_mat="brown", base=0.46)
    t.grime(0.22, seed=1)
    # 이음새: 가로 2단, 세로 2열, 위아래 벽이 어긋나게
    for (x0, y0, x1, y1) in ((0, 0, 64, 52), (64, 0, 128, 52), (0, 52, 40, 128), (40, 52, 104, 128), (104, 52, 128, 128)):
        t.rect(x0 + 1, y0 + 1, x1 - 1, y1 - 1, add=0.0, bevel=2, light=0.16)
        t.rect(x0, y0, x1, y0 + 1, L=0.08)
        t.rect(x0, y0, x0 + 1, y1, L=0.08)
    # 가로 띠: 강철 + 경고 줄무늬
    t.rect(0, BAND[0], 128, BAND[1], L=0.5, mat="steel", bevel=2, light=0.2)
    hazard(t, 0, BAND[0] + 3, 128, BAND[1] - 3)
    # 세로 배관
    for px in (20, 23):
        m = t.rect(px, 58, px + 3, 128, mat="steel")
        t.L[m] = 0.35 + 0.35 * np.cos((t.X[m] - px - 0.8) / 3 * math.pi)
    for y in (70, 104):
        t.rect(18, y, 28, y + 3, L=0.55, mat="gray", bevel=1)
    # 통풍구
    for i in range(5):
        t.rect(78, 14 + i * 5, 112, 16 + i * 5, L=0.12, mat="dark")
        t.rect(78, 16 + i * 5, 112, 17 + i * 5, L=0.55, mat="brown")
    t.rect(76, 12, 114, 13, L=0.6, mat="brown")
    # 작은 표시등
    t.rect(52, 84, 60, 96, L=0.3, mat="dark", bevel=1)
    t.glow_rect(54, 86, 58, 89, (255, 60, 30))
    t.glow_rect(54, 91, 58, 94, (120, 230, 120))
    rivets(t, (5, 59, 69, 123), (6, 41))
    rivets(t, (5, 35, 45, 99, 109, 123), (64, 121))
    # 녹물 자국
    rng = np.random.default_rng(7)
    for i in range(6):
        x = int(rng.integers(0, 128))
        y0 = int(rng.integers(0, 90))
        ln = int(rng.integers(8, 30))
        t.rect(x, y0, x + 1 + (i % 2), y0 + ln, add=-0.12)
        t.M[(t.X >= x) & (t.X < x + 2) & (t.Y >= y0) & (t.Y < y0 + ln)] = t.mat("rust")
    return t.render()


def wall_lab():
    """연구소 벽: 밝은 타일과 청록 띠(시작실·카드 상점)."""
    t = Tex(base_mat="coat", base=0.62)
    t.grime(0.12, seed=41)
    # 16px 타일 줄눈
    for k in range(0, 128, 16):
        t.rect(0, k, 128, k + 1, L=0.3)
        t.rect(k, 0, k + 1, 128, L=0.3)
    for x0 in range(0, 128, 16):
        for y0 in range(0, 128, 16):
            t.rect(x0 + 1, y0 + 1, x0 + 16, y0 + 16, bevel=1, light=0.08)
    t.rect(0, BAND[0], 128, BAND[1], L=0.5, mat="teal", bevel=2, light=0.18)
    t.rect(0, BAND[0] + 5, 128, BAND[0] + 7, L=0.8, mat="teal")
    # 벽걸이 조명과 깨진 타일 몇 장
    t.rect(52, 14, 76, 24, L=0.4, mat="steel", bevel=1)
    t.glow_rect(54, 16, 74, 22, (226, 240, 230))
    for (x0, y0) in ((16, 80), (96, 96), (32, 112)):
        t.rect(x0 + 2, y0 + 2, x0 + 14, y0 + 14, L=0.28, mat="gray")
    t.rect(70, 70, 71, 100, L=0.2)
    t.rect(71, 99, 80, 100, L=0.2)
    # 바닥 걸레받이
    t.rect(0, 120, 128, 128, L=0.35, mat="steel", bevel=1)
    rng = np.random.default_rng(42)
    for i in range(4):
        x = int(rng.integers(4, 124))
        t.rect(x, 60, x + 1, 60 + int(rng.integers(8, 40)), add=-0.12)
    return t.render()


def wall_hell():
    """지옥 벽: 붉은 바위와 살덩이, 용암 틈(보스방)."""
    t = Tex(base_mat="stone", base=0.42)
    n = fbm(N, 51)
    t.L = 0.16 + n * 0.36
    # 살덩이 맥: 잡음의 등고선을 붉은 재질로
    vein = np.abs(fbm(N, 53) - 0.5) < 0.022
    t.M[vein] = t.mat("red")
    t.L[vein] = 0.4
    flesh = fbm(N, 57) > 0.68
    t.M[flesh & ~vein] = t.mat("flesh")
    t.L[flesh & ~vein] -= 0.05
    # 띠: 검은 쇠 띠에 해골 장식
    t.rect(0, BAND[0], 128, BAND[1], L=0.35, mat="dark", bevel=2, light=0.2)
    for cx in (32, 96):
        t.circle(cx, BAND[0] + 6, 5, L=0.7, mat="bone")
        t.rect(cx - 3, BAND[0] + 4, cx - 1, BAND[0] + 7, L=0.05, mat="dark")
        t.rect(cx + 1, BAND[0] + 4, cx + 3, BAND[0] + 7, L=0.05, mat="dark")
        t.rect(cx - 2, BAND[0] + 9, cx + 2, BAND[0] + 10, L=0.1, mat="dark")
    # 용암 틈
    lava = (np.abs(fbm(N, 59) - 0.5) < 0.008) & (t.Y > BAND[1] + 4)
    t.glow[lava] = (255, 110, 20)
    t.gmask[lava] = True
    return t.render()


def wall_support():
    """골조 벽: 강철 I빔 기둥과 X자 보강재. 방 경계·문틀·기둥에 쓴다."""
    t = Tex(base_mat="steel", base=0.32)
    t.grime(0.14, seed=61)
    # 뒷판
    t.rect(0, 0, 128, 128, L=0.24, mat="gray")
    t.grime(0.12, seed=62)
    # X 보강재(위·아래 칸)
    for (y0, y1) in ((4, BAND[0] - 2), (BAND[1] + 2, 124)):
        h = y1 - y0
        for k in range(-1, 2):
            d1 = np.abs((t.X - 16) * h / 96 - (t.Y - y0) + k) < 1.0
            d2 = np.abs((112 - t.X) * h / 96 - (t.Y - y0) + k) < 1.0
            m = (d1 | d2) & (t.Y >= y0) & (t.Y < y1) & (t.X >= 16) & (t.X < 112)
            t.M[m] = t.mat("steel")
            t.L[m] = 0.5 + 0.1 * k
    # 양쪽 I빔 기둥(타일 경계에 걸쳐 이어지게 0과 128 근처)
    for x0 in (0, 112):
        t.rect(x0, 0, x0 + 16, 128, L=0.45, mat="steel", bevel=2, light=0.2)
        t.rect(x0 + 6, 0, x0 + 10, 128, L=0.32, mat="steel")
        for y in range(8, 128, 24):
            t.circle(x0 + 3, y, 1.3, L=0.7, mat="gray")
            t.circle(x0 + 13, y, 1.3, L=0.7, mat="gray")
    # 띠: 경고 줄무늬 (다른 벽의 띠와 같은 높이)
    t.rect(0, BAND[0], 128, BAND[1], L=0.5, mat="steel", bevel=2, light=0.2)
    hazard(t, 0, BAND[0] + 3, 128, BAND[1] - 3, phase=2)
    # 아래쪽 받침
    t.rect(0, 118, 128, 128, L=0.3, mat="dark", bevel=1)
    return t.render()


def wall_cover():
    """엄폐물: 쌓아 올린 군용 상자. 방 안 엄폐 벽에만 쓴다."""
    t = Tex(base_mat="olive", base=0.36)
    t.grime(0.14, seed=71)
    for (x0, y0, x1, y1) in ((0, 0, 64, BAND[0]), (64, 0, 128, BAND[0]), (0, BAND[1], 128, 128)):
        t.rect(x0 + 1, y0 + 1, x1 - 1, y1 - 1, bevel=3, light=0.18)
        t.rect(x0 + 6, y0 + 6, x1 - 6, y1 - 6, add=-0.06, bevel=1, light=0.1)
        t.rect(x0, y0, x1, y0 + 1, L=0.06, mat="dark")
        t.rect(x0, y0, x0 + 1, y1, L=0.06, mat="dark")
        # 모서리 쇠붙이
        for cx, cy in ((x0 + 4, y0 + 4), (x1 - 5, y0 + 4), (x0 + 4, y1 - 5), (x1 - 5, y1 - 5)):
            t.rect(cx - 2, cy - 2, cx + 3, cy + 3, L=0.55, mat="gray", bevel=1)
    # 스텐실 표시(흰 줄 세 개와 사각)
    for x0 in (20, 84):
        for k in range(3):
            t.rect(x0, 16 + k * 5, x0 + 24, 18 + k * 5, L=0.85, mat="tan")
    t.rect(48, 78, 80, 100, L=0.8, mat="tan")
    t.rect(52, 82, 76, 96, L=0.4, mat="olive")
    # 띠 높이: 상자 사이 금속 받침대
    t.rect(0, BAND[0], 128, BAND[1], L=0.4, mat="steel", bevel=2, light=0.2)
    return t.render()


def floor():
    t = Tex(base_mat="gray", base=0.4)
    t.grime(0.16, seed=11)
    # 4×4 금속 바닥판
    for i in range(2):
        for j in range(2):
            x0, y0 = i * 64, j * 64
            t.rect(x0 + 1, y0 + 1, x0 + 63, y0 + 63, bevel=2, light=0.14)
            t.rect(x0, y0, x0 + 64, y0 + 1, L=0.06, mat="dark")
            t.rect(x0, y0, x0 + 1, y0 + 64, L=0.06, mat="dark")
            if (i + j) % 2 == 0:
                # 미끄럼 방지 무늬
                for yy in range(y0 + 6, y0 + 60, 6):
                    for xx in range(x0 + 6 + ((yy // 6) % 2) * 3, x0 + 60, 6):
                        t.rect(xx, yy, xx + 3, yy + 1, add=0.2)
                        t.rect(xx, yy + 1, xx + 3, yy + 2, add=-0.15)
            else:
                # 배수 격자
                t.rect(x0 + 10, y0 + 10, x0 + 54, y0 + 54, L=0.25, mat="steel", bevel=2, light=0.15)
                for k in range(x0 + 14, x0 + 52, 5):
                    t.rect(k, y0 + 14, k + 2, y0 + 50, L=0.05, mat="dark")
            rivets(t, (x0 + 5, x0 + 59), (y0 + 5, y0 + 59), r=1.2)
    # 기름 얼룩
    n = fbm(N, 33)
    t.L[n > 0.66] -= 0.15
    return t.render()


def ceiling():
    t = Tex(base_mat="stone", base=0.42)
    t.grime(0.14, seed=21)
    for i in range(2):
        for j in range(2):
            x0, y0 = i * 64, j * 64
            t.rect(x0 + 1, y0 + 1, x0 + 63, y0 + 63, bevel=2, light=0.12)
            t.rect(x0, y0, x0 + 64, y0 + 2, L=0.1, mat="dark")
            t.rect(x0, y0, x0 + 2, y0 + 64, L=0.1, mat="dark")
            for k in range(4):
                t.rect(x0 + 8 + k * 13, y0 + 8, x0 + 10 + k * 13, y0 + 56, add=-0.06)
    # 가운데 조명
    t.rect(40, 48, 88, 80, L=0.35, mat="steel", bevel=2, light=0.2)
    t.glow_rect(44, 52, 84, 76, (226, 220, 176))
    t.glow_rect(46, 54, 82, 74, (250, 246, 214))
    for k in range(48, 82, 6):
        t.glow_rect(k, 52, k + 1, 76, (160, 150, 110))
    rivets(t, (6, 58, 70, 122), (6, 58, 70, 122), r=1.1)
    return t.render()


def door(open_=False):
    t = Tex(base_mat="steel", base=0.42)
    t.grime(0.16, seed=31)
    # 문틀
    t.rect(0, 0, 128, 128, L=0.3, mat="brown")
    t.grime(0.2, seed=32)
    t.rect(0, 0, 128, 128, bevel=3, light=0.2)
    hazard(t, 6, 4, 122, 12)
    hazard(t, 6, 116, 122, 124, phase=3)
    # 안쪽 문짝 자리
    x0, x1, y0, y1 = 16, 112, 14, 114
    t.rect(x0 - 2, y0 - 1, x1 + 2, y1 + 1, L=0.05, mat="dark")
    if open_:
        # 어두운 통로: 원근 바닥선과 멀리 희미한 불빛
        m = t.rect(x0, y0, x1, y1, L=0.06, mat="dark")
        cx, cy = 64, 66
        for k in range(1, 6):
            s = k / 6
            xa, xb = int(cx - (cx - x0) * s), int(cx + (x1 - cx) * s)
            ya, yb = int(cy - (cy - y0) * s), int(cy + (y1 - cy) * s)
            t.rect(xa, yb - 1, xb, yb, add=0.12 * s)
            t.rect(xa, ya, xa + 1, yb, add=0.08 * s)
            t.rect(xb - 1, ya, xb, yb, add=0.08 * s)
        t.glow_rect(60, 60, 68, 64, (90, 70, 40))
        # 양옆으로 밀려 들어간 문짝 끝
        for xa, xb in ((x0, x0 + 8), (x1 - 8, x1)):
            t.rect(xa, y0, xb, y1, L=0.4, mat="steel", bevel=2)
    else:
        for side, (xa, xb) in enumerate(((x0, 64), (64, x1))):
            t.rect(xa, y0, xb, y1, L=0.44, mat="steel", bevel=2, light=0.18)
            for k in range(5):
                yy = y0 + 8 + k * 19
                t.rect(xa + 5, yy, xb - 5, yy + 9, add=0.0, bevel=2, light=0.14)
                t.rect(xa + 5, yy + 9, xb - 5, yy + 10, add=-0.2)
            rivets(t, (xa + 3, xb - 4), (y0 + 3, y1 - 4), r=1.0)
        t.rect(63, y0, 65, y1, L=0.04, mat="dark")
        # 가운데 잠금 장치
        t.rect(56, 56, 72, 72, L=0.5, mat="gray", bevel=2, light=0.2)
        t.glow_rect(61, 61, 67, 67, (255, 60, 30))
    # 옆 기둥의 조작판
    t.rect(4, 56, 12, 74, L=0.25, mat="dark", bevel=1)
    t.glow_rect(6, 59, 10, 63, (255, 60, 30) if not open_ else (110, 240, 110))
    t.rect(116, 40, 124, 90, L=0.35, mat="steel", bevel=1)
    return t.render()


def all_textures():
    return {"wall.png": wall(), "wall_lab.png": wall_lab(), "wall_hell.png": wall_hell(),
            "wall_support.png": wall_support(), "wall_cover.png": wall_cover(),
            "floor.png": floor(), "ceiling.png": ceiling(),
            "Door.png": door(False), "DoorOpen.png": door(True)}
