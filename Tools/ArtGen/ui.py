"""UI 그림 조각. 패널·카드·버튼·HUD 테두리, 제목 명판, 해골 장식, 빛 번짐.

모든 조각은 높이 지도(height map)로 만든다. 높이의 기울기로 면 방향을 구하고 왼쪽 위에서 빛을 비춘 뒤,
재질별 색 단계(ramp)와 Bayer 디더링으로 줄인다. 그래서 테두리의 경사·리벳·뿔이 입체로 보인다.

9칸 조각(nine-slice)으로 늘려 쓰는 테두리는 가장자리 가운데가 길이 방향으로 한결같아야 한다.
그래서 잡음은 넣지 않고, 리벳·판 같은 장식은 모서리 칸 안에만 둔다.

강조 색(카드 등급, 버튼 색)은 게임이 칠한다. 흰색으로 그린 *_trim, *_eyes 그림을 색조로 곱해 쓴다.
"""

import numpy as np
from core import RAMPS, BAYER4, _ramp

# 황동(카드 모서리판·명판 장식). core의 재질 목록에는 없어 여기서 더한다.
RAMPS.setdefault("brass", _ramp("1e1205", "3a2408", "5a3a0e", "7c5414", "a0701c", "c08f2a", "dcb046", "f0d278"))

LIGHT = np.array([-0.62, -0.62, 0.48], np.float32)
LIGHT /= np.linalg.norm(LIGHT)


class Plate:
    """높이·재질·밝기 지도를 칠한 뒤 빛을 계산해 RGBA로 만든다."""

    def __init__(self, w, h):
        self.w, self.h = w, h
        self.H = np.zeros((h, w), np.float32)
        self.L = np.zeros((h, w), np.float32)     # 재질 기본 밝기에 더할 값
        self.M = np.full((h, w), -1, np.int32)    # -1 = 투명
        self.mats = []
        self.base = []
        self.flat = np.zeros((h, w), bool)        # 빛 계산 없이 기본 밝기만 쓰는 칸
        yy, xx = np.mgrid[0:h, 0:w]
        self.X, self.Y = xx.astype(np.float32), yy.astype(np.float32)

    def mat(self, name, base):
        key = (name, base)
        if key not in list(zip(self.mats, self.base)):
            self.mats.append(name)
            self.base.append(base)
        return list(zip(self.mats, self.base)).index(key)

    def paint(self, mask, mat, base, height=None, add=None, flat=False):
        self.M[mask] = self.mat(mat, base)
        if height is not None:
            self.H[mask] = height[mask] if isinstance(height, np.ndarray) else height
        self.L[mask] = 0 if add is None else (add[mask] if isinstance(add, np.ndarray) else add)
        self.flat[mask] = flat

    def raise_(self, mask, height):
        """높이를 더한다(이미 칠한 칸 위에 볼록하게)."""
        self.H[mask] += height[mask] if isinstance(height, np.ndarray) else height

    def render(self, outline=True):
        gy, gx = np.gradient(self.H)
        n = np.stack([-gx, -gy, np.ones_like(gx)], -1)
        n /= np.linalg.norm(n, axis=-1, keepdims=True)
        lam = np.clip((n * LIGHT).sum(-1), 0, 1)
        # 정면(평평한 면)의 밝기가 1이 되게 맞춘다.
        lam = lam / LIGHT[2]
        img = np.zeros((self.h, self.w, 4), np.uint8)
        dither = np.tile(BAYER4, (self.h // 4 + 1, self.w // 4 + 1))[:self.h, :self.w]
        for i, (name, base) in enumerate(zip(self.mats, self.base)):
            m = self.M == i
            if not m.any():
                continue
            ramp = RAMPS[name]
            k = len(ramp)
            shade = np.where(self.flat, 1.0, lam)
            v = base * shade + self.L
            lvl = np.clip(np.floor(v * (k - 0.01) + dither * 0.45), 0, k - 1).astype(int)
            img[m, :3] = ramp[lvl[m]]
            img[m, 3] = 255
        if outline:
            a = img[:, :, 3] > 0
            pad = np.pad(a, 1)
            ring = (pad[:-2, 1:-1] | pad[2:, 1:-1] | pad[1:-1, :-2] | pad[1:-1, 2:]) & ~a
            img[ring] = (4, 3, 3, 255)
        return img


def edge_distance(p, x0, y0, x1, y1):
    """사각형 [x0,x1)×[y0,y1) 안에서 가장 가까운 변까지 거리(칸). 바깥은 음수."""
    return np.minimum(np.minimum(p.X - x0, x1 - 1 - p.X), np.minimum(p.Y - y0, y1 - 1 - p.Y))


def bevel(d, d0, d1, up=True):
    """띠 [d0,d1) 안에서 0→1(또는 1→0)로 오르는 둥근 경사."""
    t = np.clip((d - d0) / max(d1 - d0 - 1, 1), 0, 1)
    prof = np.sin(t * np.pi)            # 가운데가 가장 높은 둥근 띠
    return prof if up else 1 - prof


def rivet(p, cx, cy, r, mat="steel", base=0.62, height=2.2):
    d2 = (p.X - cx) ** 2 + (p.Y - cy) ** 2
    m = d2 <= r * r
    dome = np.sqrt(np.clip(r * r - d2, 0, None)) / max(r, 0.5) * height
    p.paint(m, mat, base)
    p.raise_(m, dome)
    # 리벳 둘레 그림자
    ring = (d2 > r * r) & (d2 <= (r + 1) ** 2) & (p.M >= 0)
    p.L[ring] -= 0.18


def corner_plates(p, x0, y0, x1, y1, size, mat, base, bolt_r, cut=None, height=1.6):
    """네 모서리에 안쪽 귀를 45도로 자른 금속판과 볼트를 붙인다."""
    cut = size // 2 if cut is None else cut
    for sx, sy in ((0, 0), (1, 0), (0, 1), (1, 1)):
        ox = x0 if sx == 0 else x1 - size
        oy = y0 if sy == 0 else y1 - size
        lx = p.X - ox if sx == 0 else ox + size - 1 - p.X     # 모서리 쪽 원점으로 접은 좌표
        ly = p.Y - oy if sy == 0 else oy + size - 1 - p.Y
        m = (lx >= 0) & (ly >= 0) & (lx < size) & (ly < size) & (lx + ly < size * 2 - 2 - cut)
        d = np.minimum(np.minimum(lx, ly), (size * 2 - 3 - cut - lx - ly) / 1.414)
        h = np.clip(d / 2.0, 0, 1) * height
        p.paint(m, mat, base, height=h + 2.0)
        cx = ox + (size * 0.42 if sx == 0 else size * 0.58 - 1)
        cy = oy + (size * 0.42 if sy == 0 else size * 0.58 - 1)
        rivet(p, cx, cy, bolt_r, height=bolt_r + 1)


# ── 9칸 테두리 ────────────────────────────────────────────────────────────────

def _frame(size, layers, plates=None, margin=0):
    """layers: (두께, 재질, 밝기, 'up'|'down'|'flat'|'groove', 높이) 를 바깥에서 안쪽 순서로."""
    p = Plate(size, size)
    x0 = y0 = margin
    x1 = y1 = size - margin
    d = edge_distance(p, x0, y0, x1, y1)
    at = 0
    for thick, mat, base, kind, hgt in layers:
        m = (d >= at) & (d < at + thick)
        if kind == "up":
            h = bevel(d, at, at + thick) * hgt + 1.0
        elif kind == "ramp_in":     # 바깥이 높고 안쪽으로 내려가는 경사
            h = (1 - np.clip((d - at) / max(thick, 1), 0, 1)) * hgt
        elif kind == "groove":
            h = np.full_like(d, -0.5)
        else:
            h = np.full_like(d, hgt)
        p.paint(m, mat, base, height=h, flat=(kind == "groove"))
        at += thick
    if plates:
        plates(p, x0, y0, x1, y1)
    return p, at


def panel_frame():
    """메뉴·오버레이 패널. 48×48, 9칸 테두리 14. 안쪽 10칸부터 내용."""
    p, inner = _frame(48, [
        (4, "steel", 0.66, "up", 2.5),
        (1, "dark", 0.15, "groove", 0),
        (3, "rust", 0.55, "ramp_in", 1.5),
        (1, "dark", 0.10, "groove", 0),
    ], plates=lambda p, x0, y0, x1, y1: corner_plates(p, x0, y0, x1, y1, 12, "steel", 0.72, 1.6))
    return p.render(outline=False), inner


def card_frame():
    """카드. 80×80, 9칸 테두리 28. 바깥 7칸은 뿔 자리, 안쪽 18칸부터 내용."""
    margin = 7

    def deco(p, x0, y0, x1, y1):
        # 모서리 뿔: 바깥쪽 대각선으로 뻗은 뼈 가시
        for sx, sy in ((0, 0), (1, 0), (0, 1), (1, 1)):
            tipx = 0 if sx == 0 else p.w - 1
            tipy = 0 if sy == 0 else p.h - 1
            bx = x0 + 6 if sx == 0 else x1 - 7
            by = y0 + 6 if sy == 0 else y1 - 7
            ax, ay = bx - tipx, by - tipy
            L = np.hypot(ax, ay)
            ux, uy = ax / L, ay / L
            rx, ry = p.X - tipx, p.Y - tipy
            along = rx * ux + ry * uy                  # 끝에서 뿌리 쪽 거리
            across = np.abs(-rx * uy + ry * ux)
            width = 0.3 + along * 0.26
            m = (along >= 0) & (along <= L) & (across <= width)
            h = (1 - across / np.maximum(width, 0.1)) * 2.5 + along * 0.08
            p.paint(m, "bone", 0.78, height=h)
        corner_plates(p, x0, y0, x1, y1, 18, "brass", 0.66, 2.2, cut=7, height=2.0)

    p, inner = _frame(80, [
        (5, "steel", 0.62, "up", 3.0),
        (1, "dark", 0.12, "groove", 0),
        (2, "dark", 0.30, "flat", 0.5),       # 등급 색 홈(trim 그림이 덮는다)
        (1, "dark", 0.12, "groove", 0),
        (2, "rust", 0.50, "ramp_in", 1.2),
    ], plates=deco, margin=margin)
    img = p.render()
    trim = _trim_mask(80, margin, 6, 2, plates_size=18)
    return img, trim, margin + inner, margin


def _trim_mask(size, margin, start, thick, plates_size):
    """흰색 홈 그림. 게임이 등급 색을 곱한다. 위·왼쪽은 밝게, 아래·오른쪽은 어둡게."""
    p = Plate(size, size)
    d = edge_distance(p, margin, margin, size - margin, size - margin)
    m = (d >= start) & (d < start + thick)
    # 모서리 판 아래는 가린다
    lx = np.minimum(p.X - margin, size - margin - 1 - p.X)
    ly = np.minimum(p.Y - margin, size - margin - 1 - p.Y)
    m &= ~((lx < plates_size) & (ly < plates_size) & (lx + ly < plates_size * 2 - 2 - 7))
    img = np.zeros((size, size, 4), np.uint8)
    inner = d == start + thick - 1
    v = np.where(inner, 170, 255)
    # 바깥에서 볼 때 위·왼쪽 변은 밝게, 아래·오른쪽 변은 어둡게
    dist_top_left = np.minimum(p.X - margin, p.Y - margin)
    dist_bot_right = np.minimum(size - margin - 1 - p.X, size - margin - 1 - p.Y)
    v = np.where(dist_bot_right < dist_top_left, v * 0.72, v)
    img[m, 0] = img[m, 1] = img[m, 2] = v[m].astype(np.uint8)
    img[m, 3] = 255
    return img


def button_frame():
    """버튼. 24×24, 9칸 테두리 6. 안쪽 4칸부터 내용. 홈(4번째 줄)은 trim으로 빛난다."""
    p, inner = _frame(24, [
        (1, "dark", 0.05, "flat", 0),
        (2, "steel", 0.60, "up", 1.8),
        (1, "dark", 0.10, "groove", 0),
    ])
    for cx, cy in ((2, 2), (21, 2), (2, 21), (21, 21)):
        p.paint((p.X == cx) & (p.Y == cy), "steel", 1.0, flat=True)
    img = p.render(outline=False)
    trim = np.zeros_like(img)
    d = edge_distance(p, 0, 0, 24, 24)
    trim[d == 3] = (255, 255, 255, 255)
    return img, trim, inner


def hud_frame():
    """HUD 상자. 16×16, 9칸 테두리 5. 안쪽 4칸부터 내용."""
    p, inner = _frame(16, [
        (1, "dark", 0.05, "flat", 0),
        (2, "steel", 0.55, "up", 1.6),
        (1, "dark", 0.10, "groove", 0),
    ])
    for cx, cy in ((1, 1), (14, 1), (1, 14), (14, 14)):
        p.paint((p.X == cx) & (p.Y == cy), "steel", 1.0, flat=True)
    return p.render(outline=False), inner


# ── 가로 세 칸 명판(제목) ──────────────────────────────────────────────────────

def plaque():
    """제목 명판. 64×22. 왼쪽·오른쪽 끝 18칸은 뾰족한 뿔 끝, 가운데는 늘어난다."""
    w, h, cap = 64, 22, 18
    p = Plate(w, h)
    cy = (h - 1) / 2
    # 몸통: 위아래 경사가 있는 띠. 끝으로 갈수록 얇아져 뾰족해진다.
    ex = np.minimum(p.X, w - 1 - p.X)                     # 가까운 끝까지 거리
    half = np.where(ex < cap, 3 + (ex / cap) * (cy - 3), cy)
    dy = np.abs(p.Y - cy)
    body = dy <= half - 0.0
    t = dy / np.maximum(half, 1)
    p.paint(body, "steel", 0.60, height=(1 - t ** 2) * 3.0)
    # 가운데 판: 어두운 홈 안에 글자가 올라간다
    inner = body & (dy <= half - 4) & (ex >= 8)
    p.paint(inner, "dark", 0.30, height=0.2, flat=True)
    groove = body & (np.abs(dy - (half - 4)) < 0.75) & (ex >= 8)
    p.paint(groove, "dark", 0.08, flat=True)
    # 끝의 뿔(위아래로 휜 가시)
    for side in (0, 1):
        for up in (-1, 1):
            tipx = 0.5 if side == 0 else w - 1.5
            tipy = cy + up * (cy - 0.5)
            rootx = 10 if side == 0 else w - 11
            ax, ay = rootx - tipx, cy - tipy
            L = np.hypot(ax, ay)
            ux, uy = ax / L, ay / L
            rx, ry = p.X - tipx, p.Y - tipy
            along = rx * ux + ry * uy
            across = np.abs(-rx * uy + ry * ux)
            width = 0.3 + along * 0.22
            m = (along >= 0) & (along <= L) & (across <= width) & ~body
            p.paint(m, "bone", 0.72, height=(1 - across / np.maximum(width, 0.1)) * 2 + along * 0.1)
    for side in (0, 1):
        rivet(p, 11 if side == 0 else w - 12, cy, 1.6, base=0.7)
    return p.render(), cap


# ── 장식 ─────────────────────────────────────────────────────────────────────

def skull():
    """카드 위 가운데에 박는 해골 장식. 32×26. 눈은 따로 흰 그림(eyes)으로 등급 색을 받는다."""
    w, h = 32, 26
    p = Plate(w, h)
    cx = (w - 1) / 2

    def ell(x, y, rx, ry):
        return ((p.X - x) / rx) ** 2 + ((p.Y - y) / ry) ** 2

    # 뒤에 깔리는 금속 받침(양옆으로 뻗은 날개)
    wing = (np.abs(p.Y - 13) <= 3 - np.abs(p.X - cx) * 0.06) & (np.abs(p.X - cx) <= 15)
    p.paint(wing, "steel", 0.55, height=1.0 + (1 - np.abs(p.Y - 13) / 3) * 1.2)
    # 머리뼈
    cran = ell(cx, 10, 9.5, 9.0)
    p.paint(cran <= 1, "bone", 0.80, height=np.sqrt(np.clip(1 - cran, 0, 1)) * 6 + 3)
    # 광대·턱
    jaw = (np.abs(p.X - cx) <= 6.5 - np.clip(p.Y - 18, 0, None) * 0.6) & (p.Y >= 14) & (p.Y <= 23)
    p.paint(jaw, "bone", 0.74, height=4 - np.abs(p.X - cx) * 0.25)
    # 이빨: 세로 홈
    teeth = jaw & (p.Y >= 18) & (p.Y <= 21) & (np.abs(p.X - cx) <= 4.5)
    p.L[teeth & (((p.X - cx + 0.5) % 2) < 1)] -= 0.35
    p.L[jaw & (p.Y == 18)] -= 0.25
    # 눈구멍과 코
    for ex in (cx - 4, cx + 4):
        eye = ell(ex, 11.5, 2.8, 2.4) <= 1
        p.paint(eye, "dark", 0.05, flat=True)
    nose = (p.Y >= 14) & (p.Y <= 16.5) & (np.abs(p.X - cx) <= (p.Y - 13) * 0.5)
    p.paint(nose, "dark", 0.08, flat=True)
    # 이마 금
    crack = (np.abs(p.X - (cx + 2 + (p.Y - 2) * 0.35)) < 0.55) & (p.Y >= 2) & (p.Y <= 6)
    p.L[crack & (p.M >= 0)] -= 0.3
    img = p.render()

    eyes = np.zeros_like(img)
    for ex in (cx - 4, cx + 4):
        e = ell(ex, 11.5, 1.7, 1.5)
        eyes[e <= 1] = (255, 255, 255, 255)
        eyes[(e > 1) & (ell(ex, 11.5, 2.6, 2.2) <= 1)] = (150, 150, 150, 200)
    return img, eyes


def gem():
    """등급 보석. 9×9 마름모, 면마다 밝기가 다른 흰 그림이라 게임이 등급 색을 곱한다. 카드 모서리 볼트 자리에 박는다."""
    n = 9
    c = 4
    img = np.zeros((n, n, 4), np.uint8)
    for y in range(n):
        for x in range(n):
            dx, dy = x - c, y - c
            d = abs(dx) + abs(dy)
            if d > 4:
                continue
            if d == 4:
                v = 40                       # 테두리(색을 곱해도 어둡게 남는다)
            elif dx <= 0 and dy <= 0:
                v = 255                      # 왼쪽 위 면: 빛을 받는다
            elif dx > 0 and dy <= 0:
                v = 205
            elif dx <= 0:
                v = 150
            else:
                v = 105
            img[y, x] = (v, v, v, 255)
    img[c - 2, c - 1] = (255, 255, 255, 255)   # 반짝이는 점
    img[c, c] = (230, 230, 230, 255)
    return img


def horns():
    """보라·빨강 등급 해골에 붙는 악마 뿔. 60×24. 해골 양옆에서 바깥 위로 휘어 오른다."""
    w, h = 60, 24
    p = Plate(w, h)
    cx = (w - 1) / 2
    for side in (-1, 1):
        # 2차 베지어 중심선: 뿌리 → 바깥 → 끝(위)
        p0 = np.array([cx + side * 8.0, 17.0])
        p1 = np.array([cx + side * 26.0, 18.0])
        p2 = np.array([cx + side * 27.0, 1.5])
        steps = 80
        for i in range(steps + 1):
            t = i / steps
            pt = (1 - t) ** 2 * p0 + 2 * (1 - t) * t * p1 + t * t * p2
            r = 3.6 * (1 - t) + 0.5 * t
            d2 = (p.X - pt[0]) ** 2 + (p.Y - pt[1]) ** 2
            m = d2 <= r * r
            dome = np.sqrt(np.clip(r * r - d2, 0, None)) / max(r, 0.5) * 2.5
            ring = (i % 9) < 2 and t < 0.8              # 마디: 몇 칸마다 어두운 고리
            base = 0.42 + 0.45 * t
            sel = m & ((p.M < 0) | (p.H < dome + t))
            p.paint(sel, "bone" if t > 0.35 else "rust", base, height=dome + t, add=-0.18 if ring else 0)
    return p.render()


def spike(side):
    """파랑 등급 이상 카드 옆구리에 박는 금속 칼날. 16×26. side=-1은 왼쪽을 향하고 1은 오른쪽을 향한다."""
    w, h = 16, 26
    p = Plate(w, h)
    cy = (h - 1) / 2
    # 왼쪽을 향하는 모양으로 계산한 뒤 오른쪽이면 x를 뒤집은 좌표로 칠한다(빛 방향은 그대로).
    X = p.X if side < 0 else (w - 1 - p.X)
    blade = (X >= 0.5) & (X <= 12) & (np.abs(p.Y - cy) <= (X - 0.5) * 0.62)
    ridge = 1 - np.abs(p.Y - cy) / np.maximum((X - 0.5) * 0.62, 0.5)
    p.paint(blade, "steel", 0.66, height=ridge * 2.4)
    mount = (X > 11) & (X <= 15) & (np.abs(p.Y - cy) <= 9)
    p.paint(mount, "brass", 0.62, height=1.5 + np.clip(np.minimum(X - 11, 15 - X), 0, 1.2))
    for yy in (cy - 6, cy + 6):
        rivet(p, (13 if side < 0 else w - 14), yy, 1.2, base=0.7, height=1.6)
    return p.render()


def glow():
    """빛 번짐. 32×32 흰색, 가장자리로 갈수록 투명. 단계 알파와 디더링으로 픽셀 느낌을 남긴다."""
    n = 32
    p = Plate(n, n)
    d = edge_distance(p, 0, 0, n, n)
    t = np.clip(d / 15.0, 0, 1)
    a = t ** 1.6
    dither = np.tile(BAYER4, (n // 4, n // 4))
    lvl = np.clip(np.floor(a * 6 + dither * 0.9), 0, 6) / 6.0
    img = np.zeros((n, n, 4), np.uint8)
    img[:, :, :3] = 255
    img[:, :, 3] = (lvl * 255).astype(np.uint8)
    return img


def all_pieces():
    """파일 이름 → RGBA 배열."""
    out = {}
    out["panel.png"], _ = panel_frame()
    out["card.png"], out["card_trim.png"], _, _ = card_frame()
    out["button.png"], out["button_trim.png"], _ = button_frame()
    out["hud.png"], _ = hud_frame()
    out["plaque.png"], _ = plaque()
    out["skull.png"], out["skull_eyes.png"] = skull()
    out["glow.png"] = glow()
    out["gem.png"] = gem()
    out["horns.png"] = horns()
    out["spike_l.png"] = spike(-1)
    out["spike_r.png"] = spike(1)
    return out


if __name__ == "__main__":
    import os
    from PIL import Image
    here = os.path.dirname(os.path.abspath(__file__))
    pv = os.path.join(here, "preview")
    os.makedirs(pv, exist_ok=True)
    pieces = all_pieces()
    # 확대 미리보기: 어두운 바탕 위에 6배
    W = sum(a.shape[1] for a in pieces.values()) + 8 * (len(pieces) + 1)
    H = max(a.shape[0] for a in pieces.values()) + 16
    sheet = Image.new("RGBA", (W, H), (40, 20, 20, 255))
    x = 8
    for name, a in pieces.items():
        sheet.alpha_composite(Image.fromarray(a, "RGBA"), (x, 8))
        x += a.shape[1] + 8
    sheet.resize((W * 5, H * 5), Image.NEAREST).save(os.path.join(pv, "ui.png"))
    print("preview ui.png")
