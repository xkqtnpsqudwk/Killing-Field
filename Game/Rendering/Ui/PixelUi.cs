using System;
using System.Drawing;
using My2DEngine.Engine.Rendering;

namespace My2DEngine.Game.Rendering.Ui
{
    /// <summary>
    /// 레트로 픽셀 UI 색 팔레트. 검은 바탕에 핏빛 빨강, 강조는 황동색.
    /// </summary>
    public static class PixelPalette
    {
        public static readonly Color Ink = Color.FromArgb(255, 0, 0, 0);
        public static readonly Color Void = Color.FromArgb(255, 9, 8, 11);
        public static readonly Color Panel = Color.FromArgb(244, 20, 18, 23);
        public static readonly Color PanelRaised = Color.FromArgb(250, 30, 27, 33);
        public static readonly Color PanelDeep = Color.FromArgb(250, 12, 11, 14);
        public static readonly Color Edge = Color.FromArgb(255, 74, 22, 22);
        public static readonly Color EdgeDim = Color.FromArgb(255, 46, 40, 46);
        public static readonly Color Blood = Color.FromArgb(255, 150, 24, 24);
        public static readonly Color Accent = Color.FromArgb(255, 214, 40, 36);
        public static readonly Color AccentHot = Color.FromArgb(255, 255, 92, 60);
        public static readonly Color Brass = Color.FromArgb(255, 232, 178, 64);
        public static readonly Color Text = Color.FromArgb(255, 236, 228, 212);
        public static readonly Color TextDim = Color.FromArgb(255, 150, 142, 128);
        public static readonly Color TextMuted = Color.FromArgb(255, 92, 86, 80);
        public static readonly Color Health = Color.FromArgb(255, 222, 48, 44);
        public static readonly Color Shield = Color.FromArgb(255, 72, 162, 255);
        public static readonly Color Stamina = Color.FromArgb(255, 244, 146, 40);
        public static readonly Color Good = Color.FromArgb(255, 104, 214, 96);
        public static readonly Color Info = Color.FromArgb(255, 120, 186, 255);

        /// <summary>색을 밝게 한다(0~1).</summary>
        public static Color Lighten(Color c, float amount)
        {
            return Color.FromArgb(c.A,
                c.R + (int)((255 - c.R) * amount),
                c.G + (int)((255 - c.G) * amount),
                c.B + (int)((255 - c.B) * amount));
        }

        /// <summary>색을 어둡게 한다(0~1).</summary>
        public static Color Darken(Color c, float amount)
        {
            float k = 1f - amount;
            return Color.FromArgb(c.A, (int)(c.R * k), (int)(c.G * k), (int)(c.B * k));
        }

        /// <summary>알파만 바꾼다.</summary>
        public static Color WithAlpha(Color c, int alpha)
        {
            return Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), c.R, c.G, c.B);
        }
    }

    /// <summary>버튼 상태.</summary>
    public enum PixelButtonState
    {
        Normal,
        Hover,
        Disabled
    }

    /// <summary>
    /// 이미지 없이 사각형과 픽셀 글꼴만으로 그리는 레트로 UI 도구.
    /// 모든 크기는 픽셀 한 칸(u) 단위다. 메뉴는 <see cref="UnitFor"/>로 u를 정하고, 640x360 HUD는 u=1을 쓴다.
    /// </summary>
    public static class PixelUi
    {
        /// <summary>글꼴 기준 크기(갈무리11 = 12px). 글자 크기는 이 배수로 그려진다.</summary>
        public const float FontBase = 12f;

        /// <summary>메뉴 UI 배율에서 픽셀 한 칸 크기를 구한다(1280x720에서 2px).</summary>
        public static float UnitFor(float uiScale)
        {
            return Math.Max(1f, (float)Math.Round(2f * uiScale));
        }

        /// <summary>
        /// 픽셀 테두리 상자. 바깥 검은 선은 모서리 한 칸을 비워 둥글게 보이고,
        /// 안쪽에 색 테두리, 채움, 위·왼쪽 밝은 선과 아래·오른쪽 어두운 선으로 입체감을 준다.
        /// </summary>
        public static void Frame(Renderer r, float x, float y, float w, float h, float u, Color fill, Color edge, bool raised = true, float opacity = 1f)
        {
            if (opacity <= 0f)
            {
                return;
            }

            if (opacity < 1f)
            {
                fill = Fade(fill, opacity);
                edge = Fade(edge, opacity);
            }

            if (w < u * 4f || h < u * 4f)
            {
                r.DrawRectangle(x, y, w, h, fill);
                return;
            }

            // 바깥선(모서리 제외)
            Color ink = Fade(PixelPalette.Ink, opacity);
            r.DrawRectangle(x + u, y, w - u * 2f, u, ink);
            r.DrawRectangle(x + u, y + h - u, w - u * 2f, u, ink);
            r.DrawRectangle(x, y + u, u, h - u * 2f, ink);
            r.DrawRectangle(x + w - u, y + u, u, h - u * 2f, ink);

            // 색 테두리(고리)와 채움. 채움이 반투명이어도 테두리 색이 비치지 않게 고리를 따로 그린다.
            r.DrawRectangle(x + u, y + u, w - u * 2f, u, edge);
            r.DrawRectangle(x + u, y + h - u * 2f, w - u * 2f, u, edge);
            r.DrawRectangle(x + u, y + u * 2f, u, h - u * 4f, edge);
            r.DrawRectangle(x + w - u * 2f, y + u * 2f, u, h - u * 4f, edge);
            r.DrawRectangle(x + u * 2f, y + u * 2f, w - u * 4f, h - u * 4f, fill);

            Color hi = PixelPalette.WithAlpha(PixelPalette.Lighten(fill, 0.16f), fill.A);
            Color lo = PixelPalette.WithAlpha(PixelPalette.Darken(fill, 0.45f), fill.A);
            Color top = raised ? hi : lo;
            Color bottom = raised ? lo : hi;
            r.DrawRectangle(x + u * 2f, y + u * 2f, w - u * 4f, u, top);
            r.DrawRectangle(x + u * 2f, y + u * 3f, u, h - u * 5f, top);
            r.DrawRectangle(x + u * 2f, y + h - u * 3f, w - u * 4f, u, bottom);
            r.DrawRectangle(x + w - u * 3f, y + u * 3f, u, h - u * 5f, bottom);
        }

        /// <summary>
        /// 패널. 픽셀 테두리 상자에 모서리 리벳과, 제목이 있으면 머리띠를 붙인다.
        /// </summary>
        /// <returns>제목 띠 아래 내용 영역의 Y 좌표.</returns>
        public static float Panel(Renderer r, float x, float y, float w, float h, float u, string title = null, Color? accent = null)
        {
            Color edge = accent ?? PixelPalette.Edge;
            Frame(r, x, y, w, h, u, PixelPalette.Panel, edge);

            // 모서리 리벳
            Color rivet = PixelPalette.Lighten(edge, 0.25f);
            float inset = u * 4f;
            r.DrawRectangle(x + inset, y + inset, u * 2f, u * 2f, rivet);
            r.DrawRectangle(x + w - inset - u * 2f, y + inset, u * 2f, u * 2f, rivet);
            r.DrawRectangle(x + inset, y + h - inset - u * 2f, u * 2f, u * 2f, rivet);
            r.DrawRectangle(x + w - inset - u * 2f, y + h - inset - u * 2f, u * 2f, u * 2f, rivet);

            if (string.IsNullOrEmpty(title))
            {
                return y + u * 8f;
            }

            float textSize = FontBase * u;
            float bandH = textSize + u * 8f;
            float bandY = y + u * 8f;
            float bandX = x + u * 10f;
            float bandW = w - u * 20f;
            r.DrawRectangle(bandX, bandY, bandW, bandH, PixelPalette.Darken(edge, 0.62f));
            r.DrawRectangle(bandX, bandY + bandH - u, bandW, u, edge);
            r.DrawRectangle(bandX, bandY, u * 2f, bandH, PixelPalette.Lighten(edge, 0.2f));
            TextCentered(r, title, x + w * 0.5f, bandY + bandH * 0.5f, PixelPalette.Text, textSize, u, bold: true);
            return bandY + bandH + u * 6f;
        }

        /// <summary>
        /// 버튼. 보통은 어두운 상자, 마우스를 올리면 빨간 테두리와 왼쪽 화살표, 비활성은 흐린 회색.
        /// sub가 있으면 제목 아래 설명을 작게 쓴다.
        /// </summary>
        public static void Button(Renderer r, RectangleF rect, float u, string label, string sub, PixelButtonState state, Color? accent = null)
        {
            Color accentColor = accent ?? PixelPalette.Accent;
            Color fill;
            Color edge;
            Color labelColor;
            Color subColor;
            switch (state)
            {
                case PixelButtonState.Hover:
                    fill = PixelPalette.WithAlpha(PixelPalette.Darken(accentColor, 0.72f), 250);
                    edge = accentColor;
                    labelColor = PixelPalette.Text;
                    subColor = PixelPalette.Lighten(PixelPalette.TextDim, 0.3f);
                    break;
                case PixelButtonState.Disabled:
                    fill = PixelPalette.PanelDeep;
                    edge = PixelPalette.EdgeDim;
                    labelColor = PixelPalette.TextMuted;
                    subColor = PixelPalette.TextMuted;
                    break;
                default:
                    fill = PixelPalette.PanelRaised;
                    edge = PixelPalette.EdgeDim;
                    labelColor = PixelPalette.Text;
                    subColor = PixelPalette.TextDim;
                    break;
            }

            Frame(r, rect.X, rect.Y, rect.Width, rect.Height, u, fill, edge, raised: state != PixelButtonState.Disabled);

            // 왼쪽 색 표식: 버튼 종류를 구분한다.
            Color stripe = state == PixelButtonState.Disabled ? PixelPalette.EdgeDim : accentColor;
            r.DrawRectangle(rect.X + u * 4f, rect.Y + u * 4f, u * 2f, rect.Height - u * 8f, stripe);

            float labelSize = LabelSizeFor(rect.Height, u, sub != null);
            if (string.IsNullOrEmpty(sub))
            {
                TextCentered(r, label, rect.X + rect.Width * 0.5f, rect.Y + rect.Height * 0.5f, labelColor, labelSize, u, bold: true);
            }
            else
            {
                float subSize = FontBase * u * 0.5f;
                float textX = rect.X + u * 12f;
                float blockH = labelSize + subSize + u * 3f;
                float top = rect.Y + (rect.Height - blockH) * 0.5f;
                Text(r, label, textX, top, labelColor, labelSize, u, bold: true);
                Text(r, sub, textX, top + labelSize + u * 3f, subColor, subSize, u);
            }

            if (state == PixelButtonState.Hover)
            {
                // 오른쪽 끝 화살표(픽셀 삼각형)
                float ah = u * 6f;
                float ax = rect.Right - u * 12f;
                float ay = rect.Y + rect.Height * 0.5f - ah * 0.5f;
                for (int i = 0; i < 3; i++)
                {
                    float col = ax + i * u * 2f;
                    float span = ah - i * u * 4f;
                    if (span <= 0f) break;
                    r.DrawRectangle(col, ay + i * u * 2f, u * 2f, span, accentColor);
                }
            }
        }

        /// <summary>
        /// 칸으로 나뉜 게이지. 채워진 칸은 위 밝은 선과 아래 어두운 선을 가지고, 마지막 칸은 남은 비율만큼 채운다.
        /// </summary>
        public static void SegmentBar(Renderer r, float x, float y, float w, float h, float u, float ratio, Color color, int segments)
        {
            ratio = Math.Max(0f, Math.Min(1f, ratio));
            segments = Math.Max(1, segments);
            Frame(r, x, y, w, h, u, PixelPalette.PanelDeep, PixelPalette.Darken(color, 0.65f), raised: false);

            float innerX = x + u * 2f;
            float innerY = y + u * 2f;
            float innerW = w - u * 4f;
            float innerH = h - u * 4f;
            float gap = u;
            float cellW = (innerW - gap * (segments + 1)) / segments;
            if (cellW < u)
            {
                // 칸이 너무 좁으면 통짜 막대로 그린다.
                r.DrawRectangle(innerX + gap, innerY + gap, (innerW - gap * 2f) * ratio, innerH - gap * 2f, color);
                return;
            }

            float filled = ratio * segments;
            Color hi = PixelPalette.Lighten(color, 0.35f);
            Color lo = PixelPalette.Darken(color, 0.35f);
            Color empty = PixelPalette.WithAlpha(PixelPalette.Darken(color, 0.82f), 255);
            for (int i = 0; i < segments; i++)
            {
                float cx = innerX + gap + i * (cellW + gap);
                float cy = innerY + gap;
                float ch = innerH - gap * 2f;
                r.DrawRectangle(cx, cy, cellW, ch, empty);

                float amount = Math.Max(0f, Math.Min(1f, filled - i));
                if (amount <= 0f)
                {
                    continue;
                }

                float fw = cellW * amount;
                r.DrawRectangle(cx, cy, fw, ch, color);
                r.DrawRectangle(cx, cy, fw, u, hi);
                if (ch > u * 2f)
                {
                    r.DrawRectangle(cx, cy + ch - u, fw, u, lo);
                }
            }
        }

        /// <summary>가로 슬라이더. 트랙, 채움, 픽셀 손잡이.</summary>
        public static void Slider(Renderer r, RectangleF track, float u, float ratio, bool hover)
        {
            ratio = Math.Max(0f, Math.Min(1f, ratio));
            Frame(r, track.X, track.Y, track.Width, track.Height, u, PixelPalette.PanelDeep, PixelPalette.EdgeDim, raised: false);
            float fillW = (track.Width - u * 4f) * ratio;
            if (fillW > 0f)
            {
                r.DrawRectangle(track.X + u * 2f, track.Y + u * 2f, fillW, track.Height - u * 4f, PixelPalette.Blood);
                r.DrawRectangle(track.X + u * 2f, track.Y + u * 2f, fillW, u, PixelPalette.Accent);
            }

            float knobW = u * 6f;
            float knobH = track.Height + u * 6f;
            float knobX = track.X + u * 2f + fillW - knobW * 0.5f;
            float knobY = track.Y - u * 3f;
            Frame(r, knobX, knobY, knobW, knobH, u, hover ? PixelPalette.Brass : PixelPalette.Text, PixelPalette.Ink);
        }

        /// <summary>왼쪽 정렬 텍스트. 오른쪽 아래로 한 칸 검은 그림자를 둔다.</summary>
        public static void Text(Renderer r, string text, float x, float y, Color color, float size, float u, bool bold = false)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            float shadow = Math.Max(1f, ShadowFor(size, u));
            r.DrawText(text, x + shadow, y + shadow, PixelPalette.WithAlpha(PixelPalette.Ink, Math.Min((int)color.A, 230)), size, bold);
            r.DrawText(text, x, y, color, size, bold);
        }

        /// <summary>오른쪽 정렬 텍스트.</summary>
        public static void TextRight(Renderer r, string text, float right, float y, Color color, float size, float u, bool bold = false)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            SizeF m = r.MeasureText(text, size, bold);
            Text(r, text, right - m.Width, y, color, size, u, bold);
        }

        /// <summary>가운데 정렬 텍스트.</summary>
        public static void TextCentered(Renderer r, string text, float cx, float cy, Color color, float size, float u, bool bold = false)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            SizeF m = r.MeasureText(text, size, bold);
            Text(r, text, cx - m.Width * 0.5f, cy - m.Height * 0.5f, color, size, u, bold);
        }

        /// <summary>
        /// 굵은 외곽선 제목. 네 방향 검은 외곽선, 아래쪽 어두운 핏빛 그림자, 본문 순으로 그린다.
        /// </summary>
        public static void Title(Renderer r, string text, float cx, float cy, Color color, float size, float u)
        {
            SizeF m = r.MeasureText(text, size, true);
            float x = cx - m.Width * 0.5f;
            float y = cy - m.Height * 0.5f;
            float o = Math.Max(1f, ShadowFor(size, u));
            Color dark = PixelPalette.Darken(color, 0.7f);
            r.DrawText(text, x, y + o * 2f, dark, size, true);
            r.DrawText(text, x + o, y + o * 2f, dark, size, true);
            r.DrawText(text, x - o, y, PixelPalette.Ink, size, true);
            r.DrawText(text, x + o, y, PixelPalette.Ink, size, true);
            r.DrawText(text, x, y - o, PixelPalette.Ink, size, true);
            r.DrawText(text, x, y + o, PixelPalette.Ink, size, true);
            r.DrawText(text, x, y, color, size, true);
        }

        /// <summary>점선처럼 끊긴 구분선.</summary>
        public static void Divider(Renderer r, float x, float y, float w, float u, Color color)
        {
            float step = u * 4f;
            for (float px = x; px < x + w; px += step)
            {
                r.DrawRectangle(px, y, Math.Min(u * 2f, x + w - px), u, color);
            }
        }

        /// <summary>
        /// 메뉴 배경. 위는 검고 아래로 갈수록 붉어지는 띠, 원근 바닥선, 떠오르는 불씨, 주사선, 가장자리 비네트.
        /// </summary>
        public static void Backdrop(Renderer r, float width, float height, float u, float time)
        {
            r.DrawRectangle(0, 0, width, height, PixelPalette.Void);

            // 세로 그라데이션(띠)
            int bands = 24;
            float bandH = height / bands;
            for (int i = 0; i < bands; i++)
            {
                float t = i / (float)(bands - 1);
                int red = (int)(10 + t * t * 46);
                int other = (int)(8 + t * 4);
                r.DrawRectangle(0, i * bandH, width, bandH + 1f, Color.FromArgb(255, red, other, other + 2));
            }

            // 지평선 아래 원근 바닥선: 지평선에 가까울수록 촘촘하다.
            float horizon = height * 0.64f;
            r.DrawRectangle(0, horizon, width, u, PixelPalette.WithAlpha(PixelPalette.Accent, 90));
            float scroll = (time * 0.25f) % 1f;
            for (int i = 0; i < 14; i++)
            {
                float k = (i + scroll) / 14f;
                float ly = horizon + (height - horizon) * k * k;
                int alpha = (int)(20 + 70 * k);
                r.DrawRectangle(0, ly, width, u, PixelPalette.WithAlpha(PixelPalette.Blood, alpha));
            }

            // 떠오르는 불씨
            for (int i = 0; i < 36; i++)
            {
                float seed = Hash(i);
                float speed = 0.04f + Hash(i + 101) * 0.08f;
                float life = (time * speed + seed) % 1f;
                float px = Hash(i + 37) * width + (float)Math.Sin(time * 0.7f + i) * u * 6f;
                float py = height - life * height * 0.9f;
                float size = Hash(i + 59) > 0.75f ? u * 2f : u;
                int alpha = (int)(220 * Math.Sin(life * Math.PI));
                Color ember = Hash(i + 83) > 0.6f ? PixelPalette.Brass : PixelPalette.AccentHot;
                r.DrawRectangle(px, py, size, size, PixelPalette.WithAlpha(ember, alpha));
            }

            // 주사선
            float lineStep = u * 3f;
            for (float ly = 0; ly < height; ly += lineStep)
            {
                r.DrawRectangle(0, ly, width, Math.Max(1f, u * 0.5f), Color.FromArgb(34, 0, 0, 0));
            }

            // 좌우 비네트
            int steps = 8;
            float edgeW = width * 0.18f / steps;
            for (int i = 0; i < steps; i++)
            {
                int alpha = (int)(150 * (1f - i / (float)steps));
                r.DrawRectangle(i * edgeW, 0, edgeW + 1f, height, Color.FromArgb(alpha, 0, 0, 0));
                r.DrawRectangle(width - (i + 1) * edgeW, 0, edgeW + 1f, height, Color.FromArgb(alpha, 0, 0, 0));
            }
        }

        /// <summary>게임 화면 위에 덮는 어두운 막과 주사선.</summary>
        public static void Dim(Renderer r, float width, float height, float u, int alpha)
        {
            // 순수 검정만 쓴다. 색조를 넣으면 일부 GPU에서 월드 위에 빈 점이 생긴다.
            r.DrawRectangle(0, 0, width, height, Color.FromArgb(alpha, 0, 0, 0));
            float lineStep = u * 3f;
            for (float ly = 0; ly < height; ly += lineStep)
            {
                r.DrawRectangle(0, ly, width, Math.Max(1f, u * 0.5f), Color.FromArgb(28, 0, 0, 0));
            }
        }

        /// <summary>색의 알파에 불투명도(0~1)를 곱한다.</summary>
        public static Color Fade(Color c, float opacity)
        {
            if (opacity >= 1f)
            {
                return c;
            }

            return Color.FromArgb(Math.Max(0, Math.Min(255, (int)(c.A * Math.Max(0f, opacity)))), c.R, c.G, c.B);
        }

        /// <summary>버튼 높이에 맞는 제목 글자 크기(12px 배수).</summary>
        private static float LabelSizeFor(float height, float u, bool withSub)
        {
            float target = withSub ? height * 0.40f : height * 0.55f;
            float size = FontBase * u * 0.5f;
            while (size + FontBase * u * 0.5f <= target)
            {
                size += FontBase * u * 0.5f;
            }

            return size;
        }

        private static float ShadowFor(float size, float u)
        {
            return Math.Max(u * 0.5f, (float)Math.Round(size / FontBase) * u * 0.5f);
        }

        /// <summary>정수 하나를 0~1 사이 고정 난수로 바꾼다(배경 장식용).</summary>
        private static float Hash(int n)
        {
            unchecked
            {
                uint x = (uint)n * 747796405u + 2891336453u;
                x = ((x >> (int)((x >> 28) + 4u)) ^ x) * 277803737u;
                x = (x >> 22) ^ x;
                return (x & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }
    }
}
