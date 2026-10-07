"""Doom 스타일 그림 생성기의 공통 부분.

Doom의 적 스프라이트는 점토 모형을 찍어 팔레트로 줄인 그림이다. 여기서도 같은 방식을 쓴다.
구·타원체·캡슐로 만든 입체 모형을 정사영으로 비추고, 재질마다 정해진 색 단계(ramp)로 줄인다.

좌표: x 오른쪽, y 위, z 보는 사람 쪽. 단위는 출력 픽셀. y=0이 바닥(그림의 맨 아래 줄)이다.
"""

import math
import numpy as np
from PIL import Image

# ── 팔레트 ──────────────────────────────────────────────────────────────────
# 재질마다 어두움 → 밝음 순서의 색 단계. Doom PLAYPAL의 계열을 흉내 냈다.

def _ramp(*hexes):
    return np.array([[int(h[i:i + 2], 16) for i in (0, 2, 4)] for h in hexes], dtype=np.float32)

RAMPS = {
    "flesh":   _ramp("2a0f07", "4a1d0e", "6e3018", "924726", "b06236", "c9804c", "dc9e68", "ecc08e"),
    "pink":    _ramp("2e0d0c", "551a18", "7c2c27", "a3423a", "c25c4e", "d97b68", "eb9c88", "f7c2ad"),
    "red":     _ramp("1c0303", "3a0606", "5c0b08", "7f120c", "a01c10", "c22a16", "de4524", "f06a3c"),
    "blood":   _ramp("1a0000", "330000", "520202", "6e0606", "8c0c0a", "a8160f"),
    "brown":   _ramp("1b1007", "2e1d0e", "443017", "5c4321", "76582d", "8f6e3c", "a8874f", "c2a266"),
    "tan":     _ramp("2b2213", "463922", "625233", "7f6c46", "9b875a", "b5a171", "cdbb8b", "e2d4a8"),
    "zskin":   _ramp("1a1d12", "2c3120", "424830", "5a6142", "727a55", "8b936a", "a6ad84", "c3c8a2"),
    "green":   _ramp("061206", "0d230b", "163a12", "21531a", "2f6e22", "40892c", "58a43c", "7cc25a"),
    "slime":   _ramp("0b1406", "17240b", "243512", "334719", "435a20", "566e2a", "6e8636", "8ea248"),
    "gray":    _ramp("101012", "1f1f23", "303036", "44444b", "5b5b63", "74747d", "8f8f98", "adadb5"),
    "steel":   _ramp("0c0e12", "181c22", "262c35", "36404b", "4a5562", "61707e", "7d8d9b", "a3b2bf"),
    "dark":    _ramp("070707", "0f0e0e", "181616", "221f1e", "2d2927", "3a3532", "48423e"),
    "bone":    _ramp("2b2519", "4a4231", "6b614a", "8c8264", "aba27f", "c6be9c", "ddd7ba", "f1edd8"),
    "coat":    _ramp("23231f", "393934", "4f4e47", "66655b", "7e7c70", "959385", "aba898", "c2bfad"),
    "olive":   _ramp("15170a", "252912", "373d1b", "4b5225", "606830", "777f3e", "8f974f", "aab066"),
    "teal":    _ramp("041416", "08262a", "0f3b41", "175259", "216a72", "2d848c", "3f9fa6", "5cbcc0"),
    "orange":  _ramp("2a1002", "4a1e04", "6e2e06", "93420a", "b5590e", "d27416", "e8952a", "f6b84c"),
    "purple":  _ramp("150816", "261029", "3a1a3e", "502454", "68306b", "823f84", "9c539c", "b86fb4"),
    "chitin":  _ramp("0a0d08", "141a10", "1f2818", "2b3721", "39472b", "4a5a37", "5f7046"),
    "rust":    _ramp("1d0d05", "34170a", "4e2410", "693217", "84421f", "9e5529", "b56c38"),
    "stone":   _ramp("141210", "24201c", "35302a", "47413a", "5a534b", "6e665d", "847b70", "9c9386"),
    "wood":    _ramp("1a0e05", "2d1a0a", "432811", "5b3818", "744920", "8c5c2b", "a47139"),
}

# 스스로 빛나는 색(빛 계산을 하지 않는다)
GLOW = {
    "eye_red":    (255, 60, 30),
    "eye_yellow": (255, 220, 60),
    "eye_green":  (150, 255, 90),
    "eye_cyan":   (110, 240, 255),
    "fire_core":  (255, 244, 170),
    "fire":       (255, 168, 40),
    "fire_hot":   (255, 214, 90),
    "fire_dark":  (214, 72, 16),
    "plasma":     (120, 230, 255),
    "plasma_core": (225, 252, 255),
    "slime_glow": (170, 255, 80),
    "lava":       (255, 110, 20),
    "white":      (250, 250, 240),
}

LIGHT = np.array([-0.72, 0.55, 0.42], dtype=np.float32)
LIGHT /= np.linalg.norm(LIGHT)

BAYER4 = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]], dtype=np.float32) / 16.0 - 0.47


def hash_noise(ix, iy, seed):
    """정수 좌표의 결정적 잡음 [0,1)."""
    h = (ix.astype(np.int64) * 374761393 + iy.astype(np.int64) * 668265263 + seed * 2147483647) & 0xFFFFFFFF
    h = (h ^ (h >> 13)) * 1274126177 & 0xFFFFFFFF
    h = h ^ (h >> 16)
    return (h & 0xFFFF).astype(np.float32) / 65536.0


class Canvas:
    """z 버퍼를 가진 정사영 그림판. 모형을 쌓은 뒤 render()로 RGBA 그림을 만든다."""

    def __init__(self, w=64, h=64, light=1.05, ambient=0.16, scale=1.0):
        """scale: 세계 단위 1이 몇 픽셀인지. 세계 폭은 w/scale, 가운데 x는 mid."""
        self.w, self.h = w, h
        self.s = scale
        self.mid = w / scale / 2
        self.z = np.full((h, w), -1e9, dtype=np.float32)
        self.nx = np.zeros((h, w), np.float32)
        self.ny = np.zeros((h, w), np.float32)
        self.nz = np.ones((h, w), np.float32)
        self.mat = np.full((h, w), -1, np.int32)      # 재질 번호
        self.bump = np.zeros((h, w), np.float32)      # 재질 잡음
        self.bright = np.zeros((h, w), np.float32)    # 밝기 보정
        self.alpha = np.ones((h, w), np.float32)
        self.mats = []
        self.mat_index = {}
        self.light = light
        self.ambient = ambient
        xs = (np.arange(w, dtype=np.float32) + 0.5) / scale
        ys = ((h - 1 - np.arange(h, dtype=np.float32)) + 0.5) / scale
        self.X, self.Y = np.meshgrid(xs, ys)
        self.decals = []
        self.edge_gap = 2.2

    def _mat(self, name):
        if name not in self.mat_index:
            self.mat_index[name] = len(self.mats)
            self.mats.append(name)
        return self.mat_index[name]

    # ── 기본 입체 ──
    def ellipsoid(self, c, r, mat, bump=0.0, seed=0, bright=0.0, alpha=1.0, zbias=0.0):
        """축 정렬 타원체. c=(x,y,z), r=(rx,ry,rz)."""
        cx, cy, cz = c
        rx, ry, rz = (r, r, r) if np.isscalar(r) else r
        S = self.s
        x0, x1 = int(max(0, math.floor((cx - rx) * S - 1))), int(min(self.w, math.ceil((cx + rx) * S + 1)))
        y0r, y1r = (cy - ry) * S - 1, (cy + ry) * S + 1
        r0, r1 = int(max(0, math.floor(self.h - 1 - y1r))), int(min(self.h, math.ceil(self.h - y0r)))
        if x0 >= x1 or r0 >= r1:
            return
        X = self.X[r0:r1, x0:x1]
        Y = self.Y[r0:r1, x0:x1]
        dx = (X - cx) / rx
        dy = (Y - cy) / ry
        d2 = dx * dx + dy * dy
        inside = d2 < 1.0
        if not inside.any():
            return
        dz = np.sqrt(np.clip(1.0 - d2, 0, 1))
        z = cz + dz * rz + zbias
        nx, ny, nz = dx / rx, dy / ry, dz / rz
        ln = np.sqrt(nx * nx + ny * ny + nz * nz) + 1e-6
        self._write(r0, r1, x0, x1, inside, z, nx / ln, ny / ln, nz / ln, mat, bump, seed, bright, alpha,
                    (X - cx), (Y - cy))

    def sphere(self, c, r, mat, **kw):
        self.ellipsoid(c, (r, r, r), mat, **kw)

    def capsule(self, a, b, ra, rb, mat, step=None, **kw):
        """두 점 사이 원뿔형 캡슐(구를 이어 붙임)."""
        a = np.array(a, np.float32)
        b = np.array(b, np.float32)
        length = float(np.linalg.norm(b - a))
        n = max(1, int(length / (step or 0.45 / self.s)))
        for i in range(n + 1):
            t = i / n
            p = a + (b - a) * t
            r = ra + (rb - ra) * t
            self.sphere(tuple(p), r, mat, **kw)

    def chain(self, pts, radii, mat, **kw):
        for i in range(len(pts) - 1):
            self.capsule(pts[i], pts[i + 1], radii[i], radii[i + 1], mat, **kw)

    def triangle(self, p0, p1, p2, mat, bump=0.0, seed=0, bright=0.0, alpha=1.0):
        """평평한 삼각형 막(날개 등). 앞뒤 어느 쪽에서 봐도 앞면으로 친다."""
        P = [np.array(p, np.float32) for p in (p0, p1, p2)]
        S = self.s
        xs = [p[0] * S for p in P]
        ys = [p[1] * S for p in P]
        x0, x1 = int(max(0, math.floor(min(xs)))), int(min(self.w, math.ceil(max(xs)) + 1))
        r0, r1 = int(max(0, math.floor(self.h - max(ys)))), int(min(self.h, math.ceil(self.h - min(ys)) + 1))
        if x0 >= x1 or r0 >= r1:
            return
        X = self.X[r0:r1, x0:x1]
        Y = self.Y[r0:r1, x0:x1]
        (ax, ay, az), (bx, by, bz), (cx, cy, cz) = P
        den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(den) < 1e-6:
            return
        w0 = ((by - cy) * (X - cx) + (cx - bx) * (Y - cy)) / den
        w1 = ((cy - ay) * (X - cx) + (ax - cx) * (Y - cy)) / den
        w2 = 1 - w0 - w1
        inside = (w0 >= 0) & (w1 >= 0) & (w2 >= 0)
        if not inside.any():
            return
        z = w0 * az + w1 * bz + w2 * cz
        n = np.cross(P[1] - P[0], P[2] - P[0])
        n = n / (np.linalg.norm(n) + 1e-6)
        if n[2] < 0:
            n = -n
        ones = np.ones_like(X)
        self._write(r0, r1, x0, x1, inside, z, ones * n[0], ones * n[1], ones * n[2], mat, bump, seed, bright, alpha,
                    X - ax, Y - ay)

    def box(self, c, half, mat, bump=0.0, seed=0, bright=0.0, bevel=0.6, alpha=1.0):
        """정면을 향한 상자(앞면 + 경사진 가장자리)."""
        cx, cy, cz = c
        hx, hy, hz = half
        S = self.s
        x0, x1 = int(max(0, math.floor((cx - hx) * S))), int(min(self.w, math.ceil((cx + hx) * S)))
        r0, r1 = int(max(0, math.floor(self.h - (cy + hy) * S))), int(min(self.h, math.ceil(self.h - (cy - hy) * S)))
        if x0 >= x1 or r0 >= r1:
            return
        X = self.X[r0:r1, x0:x1]
        Y = self.Y[r0:r1, x0:x1]
        inside = (np.abs(X - cx) <= hx) & (np.abs(Y - cy) <= hy)
        ex = np.clip((np.abs(X - cx) - (hx - bevel * 2)) / (bevel * 2), 0, 1) * np.sign(X - cx)
        ey = np.clip((np.abs(Y - cy) - (hy - bevel * 2)) / (bevel * 2), 0, 1) * np.sign(Y - cy)
        nz = np.ones_like(X)
        ln = np.sqrt(ex * ex + ey * ey + nz * nz)
        z = cz + hz - (np.abs(ex) + np.abs(ey)) * bevel
        self._write(r0, r1, x0, x1, inside, z, ex / ln, ey / ln, nz / ln, mat, bump, seed, bright, alpha,
                    X - cx, Y - cy)

    def _write(self, r0, r1, x0, x1, inside, z, nx, ny, nz, mat, bump, seed, bright, alpha, lx, ly):
        zone = self.z[r0:r1, x0:x1]
        m = inside & (z > zone)
        if not m.any():
            return
        zone[m] = z[m]
        self.nx[r0:r1, x0:x1][m] = nx[m]
        self.ny[r0:r1, x0:x1][m] = ny[m]
        self.nz[r0:r1, x0:x1][m] = nz[m]
        self.mat[r0:r1, x0:x1][m] = self._mat(mat)
        if bump:
            nn = hash_noise(np.floor(lx).astype(np.int32), np.floor(ly).astype(np.int32), seed)
            nn2 = hash_noise(np.floor(lx / 2).astype(np.int32), np.floor(ly / 2).astype(np.int32), seed + 7)
            self.bump[r0:r1, x0:x1][m] = ((nn * 0.45 + nn2 * 0.55) - 0.5)[m] * bump
        else:
            self.bump[r0:r1, x0:x1][m] = 0
        self.bright[r0:r1, x0:x1][m] = bright
        self.alpha[r0:r1, x0:x1][m] = alpha

    # ── 평면 장식(빛 계산 없이 색을 찍음) ──
    def decal(self, fn):
        """render 이후 RGBA 배열에 직접 그리는 함수 fn(img)를 예약한다."""
        self.decals.append(fn)

    def glow_dot(self, x, y, color, r=0.8, z=999):
        """빛나는 점(눈, 불꽃 등). z 버퍼 위에 그린다."""
        self.ellipsoid((x, y, z), (r, r, 0.01), "glow:" + color)

    def render(self, outline=True, outline_strength=0.35):
        img = np.zeros((self.h, self.w, 4), np.float32)
        lit = (self.nx * LIGHT[0] + self.ny * LIGHT[1] + self.nz * LIGHT[2])
        diffuse = np.clip(lit, 0, 1)
        # 정면에서 받는 약한 보조광 + 테두리 어둡게
        inten = self.ambient + (1 - self.ambient) * diffuse * self.light
        inten = inten * (0.72 + 0.28 * self.nz)
        inten = inten + self.bump + self.bright
        dither = np.tile(BAYER4, (self.h // 4 + 1, self.w // 4 + 1))[:self.h, :self.w]
        for idx, name in enumerate(self.mats):
            m = self.mat == idx
            if not m.any():
                continue
            if name.startswith("glow:"):
                img[m, :3] = GLOW[name[5:]]
                img[m, 3] = 255 * self.alpha[m]
                continue
            ramp = RAMPS[name]
            n = len(ramp)
            level = inten[m] * (n - 0.01) + dither[m] * 0.55
            li = np.clip(np.floor(level), 0, n - 1).astype(np.int32)
            img[m, :3] = ramp[li]
            img[m, 3] = 255 * self.alpha[m]
        # 깊이가 크게 끊기는 곳(뒤쪽 픽셀)을 어둡게 해 팔·다리 경계를 살린다
        zz = np.where(self.mat >= 0, self.z, -1e9)
        edge = np.zeros_like(zz, bool)
        for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
            nb = np.roll(np.roll(zz, dy, 0), dx, 1)
            edge |= (nb - zz) > self.edge_gap
        edge &= self.mat >= 0
        glow = np.zeros_like(edge)
        for idx, name in enumerate(self.mats):
            if name.startswith("glow:"):
                glow |= self.mat == idx
        edge &= ~glow
        img[edge, :3] *= 0.45
        if outline:
            img = add_outline(img, outline_strength)
        out = img.clip(0, 255).astype(np.uint8)
        for fn in self.decals:
            fn(out)
        return out


def add_outline(img, strength=0.35):
    """실루엣 바깥쪽 1픽셀을 이웃 색의 아주 어두운 버전으로 칠하고, 가장자리 안쪽을 조금 어둡게 한다."""
    a = img[:, :, 3] > 8
    h, w = a.shape
    out = img.copy()
    pad = np.pad(a, 1)
    neigh = pad[:-2, 1:-1] | pad[2:, 1:-1] | pad[1:-1, :-2] | pad[1:-1, 2:]
    ring = neigh & ~a
    # 이웃 색 평균을 어둡게
    col = np.zeros((h, w, 3), np.float32)
    cnt = np.zeros((h, w), np.float32)
    pimg = np.pad(img, ((1, 1), (1, 1), (0, 0)))
    pa = np.pad(a, 1).astype(np.float32)
    for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
        sl = pimg[1 + dy:h + 1 + dy, 1 + dx:w + 1 + dx, :3]
        sa = pa[1 + dy:h + 1 + dy, 1 + dx:w + 1 + dx]
        col += sl * sa[:, :, None]
        cnt += sa
    col /= np.maximum(cnt, 1)[:, :, None]
    out[ring, :3] = col[ring] * 0.18
    out[ring, 3] = 255
    # 안쪽 가장자리(아래·오른쪽) 어둡게
    inner_edge = a & ~(pad[2:, 1:-1] & pad[1:-1, 2:])
    out[inner_edge, :3] *= (1 - strength)
    return out


def to_image(arr):
    return Image.fromarray(arr, "RGBA")


def upscale(arr, k):
    return np.repeat(np.repeat(arr, k, axis=0), k, axis=1)


# ── 2D 도움 함수(벽 텍스처·장식용) ──────────────────────────────────────────

def put(img, x, y, rgb, a=255):
    h, w = img.shape[:2]
    x, y = int(x), int(y)
    if 0 <= x < w and 0 <= y < h:
        img[y, x, :3] = rgb
        img[y, x, 3] = a


def ramp_color(name, i):
    r = RAMPS[name]
    i = max(0, min(len(r) - 1, int(i)))
    return tuple(int(v) for v in r[i])


def lerp(a, b, t):
    return a + (b - a) * t


def ease(t):
    return 0.5 - 0.5 * math.cos(math.pi * max(0.0, min(1.0, t)))
