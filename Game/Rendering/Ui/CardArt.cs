using System;
using System.Drawing;
using My2DEngine.Engine.Rendering;

namespace My2DEngine.Game.Rendering.Ui
{
    /// <summary>카드 한 장을 그릴 때의 상태. 기본값은 가만히 놓인 카드.</summary>
    public struct CardLook
    {
        /// <summary>마우스를 올린 정도(0~1).</summary>
        public float Hover;

        /// <summary>전체 불투명도(0~1). 0이면 1로 본다.</summary>
        public float Opacity;

        /// <summary>
        /// 장식 단계(0~4). 카드 등급(White~Red) 또는 방 유형에서 온다.
        /// 1 보석, 2 옆 칼날·빛 훑기, 3 악마 뿔·은은한 빛·반짝임, 4 불꽃 왕관·불씨. 위 단계는 아래 단계 장식을 모두 가진다.
        /// </summary>
        public int Tier;

        /// <summary>이 Y보다 아래는 그리지 않는다(타서 사라지는 카드). 0이면 자르지 않는다.</summary>
        public float ClipBottom;

        /// <summary>테두리 그림에 곱할 색(타는 동안 달아오름). 비어 있으면 흰색.</summary>
        public Color FrameTint;

        internal float EffectiveOpacity => Opacity <= 0f ? 1f : Opacity;

        internal float EffectiveClip => ClipBottom <= 0f ? float.MaxValue : ClipBottom;

        internal bool Clipped => ClipBottom > 0f;
    }

    /// <summary>
    /// 둠풍 카드 그리기(게임 해상도, 한 칸 = 1). 몸통 x,y,w,h 바깥으로 뿔·해골·칼날·불꽃이 조금 나간다.
    /// 안쪽은 위가 검고 아래로 갈수록 강조 색이 끓어오른다. 테두리 홈과 해골 눈, 보석은 강조 색을 받는다.
    /// </summary>
    public static class CardArt
    {
        /// <summary>안쪽 채움이 테두리 안으로 들어가는 정도.</summary>
        private const float FillInset = 4f;

        public static void Draw(Renderer r, float x, float y, float w, float h, Color accent, CardLook look)
        {
            float opacity = look.EffectiveOpacity;
            float clip = look.EffectiveClip;
            if (clip <= y - 30f)
            {
                return;
            }

            accent = PixelPalette.WithAlpha(accent, 255);
            if (UiArt.Card == null)
            {
                PixelUi.Frame(r, x, y, w, h, 1f, PixelPalette.Panel, accent, raised: true, opacity: opacity);
                return;
            }

            float hover = look.Hover;
            int tier = Math.Max(0, Math.Min(4, look.Tier));
            double time = PixelUi.Clock;
            float pulse = 0.5f + 0.5f * (float)Math.Sin(time * 6.0);
            Color frameTint = look.FrameTint.A == 0 ? Color.White : look.FrameTint;
            frameTint = Color.FromArgb((int)(255 * opacity), frameTint.R, frameTint.G, frameTint.B);

            // ── 뒤쪽: 빛 번짐, 불꽃 왕관, 옆 칼날 ──
            if (!look.Clipped)
            {
                if (hover > 0f)
                {
                    PixelUi.GlowRect(r, x, y, w, h, 14f, PixelPalette.WithAlpha(accent, (int)(hover * (140 + 70 * pulse))), opacity);
                }

                if (tier >= 3)
                {
                    float slow = 0.5f + 0.5f * (float)Math.Sin(time * 2.4 + x * 0.05);
                    int alpha = tier >= 4 ? (int)(70 + 70 * slow) : (int)(35 + 45 * slow);
                    PixelUi.GlowRect(r, x, y, w, h, tier >= 4 ? 16f : 10f, PixelPalette.WithAlpha(accent, alpha), opacity);
                }
            }

            if (tier >= 4 && clip > y + h * 0.5f)
            {
                DrawFlameCrown(r, x, y, w, time, opacity);
            }

            float m = UiArt.CardMargin;
            if (tier >= 2 && UiArt.SpikeLeft != null)
            {
                float sy = (float)Math.Round(y + h * 0.5f - UiArt.SpikeHeight * 0.5f);
                ImageClipped(r, UiArt.SpikeLeft, x - 12f, sy, UiArt.SpikeWidth, UiArt.SpikeHeight, frameTint, clip);
                ImageClipped(r, UiArt.SpikeRight, x + w - 4f, sy, UiArt.SpikeWidth, UiArt.SpikeHeight, frameTint, clip);
            }

            // ── 안쪽 채움 ──
            const int bands = 12;
            float bandH = (h - FillInset * 2f) / bands;
            for (int i = 0; i < bands; i++)
            {
                float by = y + FillInset + i * bandH;
                float bh = Math.Min(bandH + 1f, clip - by);
                if (bh <= 0f) break;
                float t = i / (float)(bands - 1);
                Color c = PixelUi.Mix(PixelPalette.PanelDeep, accent, t * t * (0.20f + 0.14f * hover + 0.03f * tier));
                r.DrawRectangle(x + FillInset, by, w - FillInset * 2f, bh, PixelUi.Fade(PixelPalette.WithAlpha(c, 252), opacity));
            }

            if (tier >= 2)
            {
                DrawShimmer(r, x, y, w, h, accent, time, opacity, clip);
            }

            // ── 테두리 ──
            NineSliceClipped(r, UiArt.Card, x - m, y - m, w + m * 2f, h + m * 2f, UiArt.CardBorder, UiArt.CardBorder, frameTint, clip);
            Color trim = PixelPalette.Lighten(accent, 0.1f + 0.4f * hover * pulse);
            NineSliceClipped(r, UiArt.CardTrim, x - m, y - m, w + m * 2f, h + m * 2f, UiArt.CardBorder, UiArt.CardBorder, PixelUi.Fade(trim, opacity), clip);

            if (tier >= 1 && UiArt.Gem != null)
            {
                // 모서리 황동판의 볼트 자리(card.png 기준)
                float gemPulse = 0.75f + 0.25f * (float)Math.Sin(time * 3.0 + x * 0.1);
                Color gem = PixelPalette.Lighten(accent, 0.15f * gemPulse + 0.25f * hover);
                ImageClipped(r, UiArt.Gem, x + 3f, y + 3f, UiArt.GemSize, UiArt.GemSize, PixelUi.Fade(gem, opacity), clip);
                ImageClipped(r, UiArt.Gem, x + w - 13f, y + 3f, UiArt.GemSize, UiArt.GemSize, PixelUi.Fade(gem, opacity), clip);
                ImageClipped(r, UiArt.Gem, x + 3f, y + h - 13f, UiArt.GemSize, UiArt.GemSize, PixelUi.Fade(gem, opacity), clip);
                ImageClipped(r, UiArt.Gem, x + w - 13f, y + h - 13f, UiArt.GemSize, UiArt.GemSize, PixelUi.Fade(gem, opacity), clip);
            }

            // ── 해골(보라 이상은 뿔) ──
            if (UiArt.Skull != null)
            {
                float sx = (float)Math.Round(x + w * 0.5f - UiArt.SkullWidth * 0.5f);
                float sy = y - 12f;
                if (tier >= 3 && UiArt.Horns != null)
                {
                    float hx = (float)Math.Round(x + w * 0.5f - UiArt.HornsWidth * 0.5f);
                    ImageClipped(r, UiArt.Horns, hx, sy - 9f, UiArt.HornsWidth, UiArt.HornsHeight, frameTint, clip);
                }

                float eyeHeat = Math.Max(hover, tier >= 4 ? 0.7f : (tier >= 3 ? 0.35f : 0f));
                if (eyeHeat > 0f && !look.Clipped)
                {
                    PixelUi.GlowRect(r, sx + 8f, sy + 9f, 16f, 6f, 6f, PixelPalette.WithAlpha(accent, (int)(eyeHeat * (120 + 120 * pulse))), opacity);
                }

                ImageClipped(r, UiArt.Skull, sx, sy, UiArt.SkullWidth, UiArt.SkullHeight, frameTint, clip);
                Color eyes = PixelPalette.Lighten(accent, 0.15f + 0.65f * eyeHeat * pulse);
                ImageClipped(r, UiArt.SkullEyes, sx, sy, UiArt.SkullWidth, UiArt.SkullHeight, PixelUi.Fade(eyes, opacity * (0.6f + 0.4f * eyeHeat)), clip);
            }

            // ── 앞쪽: 반짝임, 불씨 ──
            if (!look.Clipped)
            {
                if (tier >= 3)
                {
                    DrawSparkles(r, x, y, w, h, accent, time, opacity);
                }

                if (tier >= 4)
                {
                    DrawEmbers(r, x, y - 6f, w, 36f, 12, time * 0.55, x, opacity);
                }
            }
        }

        /// <summary>
        /// 카드가 타 들어가는 가장자리. lineY 아래는 이미 타서 없고(카드를 그 높이에서 자른다),
        /// 그 위로 검게 그을린 들쭉날쭉한 띠와 너울거리는 불꽃, 위로 날리는 불씨를 그린다.
        /// </summary>
        public static void DrawBurnEdge(Renderer r, float x, float w, float lineY, float seed)
        {
            double time = PixelUi.Clock;
            int frame = (int)(time * 14.0);
            PixelUi.GlowRect(r, x, lineY - 6f, w, 6f, 10f, Color.FromArgb(150, 255, 120, 30));

            for (float cx = x - 8f; cx < x + w + 8f; cx += 2f)
            {
                int col = (int)((cx - x) / 2f) + (int)(seed * 97f);

                // 타 들어가는 앞머리는 물결처럼 들쭉날쭉하다. 그을음 띠가 직선으로 잘린 카드 끝을 덮는다.
                float wave = 4f + 3f * (float)Math.Sin(col * 0.31 + seed * 1.7) + 2f * (float)Math.Sin(col * 0.83 + seed * 3.1);
                float charH = 2f + Math.Max(0f, wave) + 3f * PixelUi.Hash(col * 7 + 3);
                float flame = 3f + 12f * (0.5f * PixelUi.Hash(col * 13 + frame) + 0.5f * PixelUi.Hash(col * 13 + frame + 1))
                    * (0.55f + 0.45f * (float)Math.Sin(time * 9.0 + col * 0.8));
                float top = lineY - charH;
                r.DrawRectangle(cx, top, 2f, charH, Color.FromArgb(255, 28, 12, 6));
                r.DrawRectangle(cx, top - flame, 2f, flame * 0.45f, Color.FromArgb(215, 200, 40, 16));
                r.DrawRectangle(cx, top - flame * 0.55f, 2f, flame * 0.35f, Color.FromArgb(240, 255, 130, 30));
                r.DrawRectangle(cx, top - flame * 0.2f, 2f, flame * 0.2f + 1f, Color.FromArgb(255, 255, 228, 120));
            }

            DrawEmbers(r, x, lineY - 4f, w, 28f, 16, time * 1.3, seed * 31f, 1f);
        }

        /// <summary>빨강 등급 카드 위 테두리를 따라 타오르는 불꽃. 테두리보다 먼저 그려 밑동이 가려진다.</summary>
        private static void DrawFlameCrown(Renderer r, float x, float y, float w, double time, float opacity)
        {
            float baseY = y + 3f;
            for (float cx = x + 2f; cx < x + w - 2f; cx += 2f)
            {
                int col = (int)((cx - x) / 2f);
                float edge = Math.Min(cx - x, x + w - cx) / (w * 0.5f);   // 가운데가 높다
                float hgt = (8f + 14f * edge) * (0.6f + 0.4f * (float)Math.Sin(time * 7.0 + col * 0.9))
                    * (0.75f + 0.25f * (float)Math.Sin(time * 13.0 + col * 2.3));
                if (hgt < 1f) continue;
                r.DrawRectangle(cx, baseY - hgt, 2f, hgt, PixelUi.Fade(Color.FromArgb(200, 190, 30, 14), opacity));
                r.DrawRectangle(cx, baseY - hgt * 0.65f, 2f, hgt * 0.65f, PixelUi.Fade(Color.FromArgb(235, 255, 120, 30), opacity));
                r.DrawRectangle(cx, baseY - hgt * 0.3f, 2f, hgt * 0.3f, PixelUi.Fade(Color.FromArgb(255, 255, 222, 110), opacity));
            }
        }

        /// <summary>위로 날리며 사라지는 불씨. 같은 seed면 같은 자리에서 나온다.</summary>
        private static void DrawEmbers(Renderer r, float x, float baseY, float w, float rise, int count, double time, float seed, float opacity)
        {
            for (int i = 0; i < count; i++)
            {
                float h0 = PixelUi.Hash(i * 17 + (int)seed);
                float life = (float)((time + h0) % 1.0);
                float px = x + PixelUi.Hash(i * 31 + 5 + (int)seed) * w + (float)Math.Sin(time * 6.0 + i) * 2f;
                float py = baseY - life * rise;
                int alpha = (int)(240 * (1f - life) * opacity);
                if (alpha <= 0) continue;
                Color c = PixelUi.Hash(i * 7 + 11) > 0.5f ? Color.FromArgb(alpha, 255, 214, 90) : Color.FromArgb(alpha, 255, 120, 30);
                float size = PixelUi.Hash(i * 3 + 1) > 0.7f ? 2f : 1f;
                r.DrawRectangle((float)Math.Round(px), (float)Math.Round(py), size, size, c);
            }
        }

        /// <summary>파랑 등급 이상: 몇 초마다 카드 안을 비스듬히 훑고 지나가는 빛줄기.</summary>
        private static void DrawShimmer(Renderer r, float x, float y, float w, float h, Color accent, double time, float opacity, float clip)
        {
            const double period = 3.2;
            const double sweep = 0.7;
            double phase = (time + x * 0.013) % period;
            if (phase > sweep)
            {
                return;
            }

            float s = (float)(phase / sweep);
            float left = x + FillInset;
            float right = x + w - FillInset;
            float start = x - h * 0.5f + s * (w + h);
            // 가운데가 진하고 양옆이 옅은 넓은 띠
            Color soft = PixelUi.Fade(PixelPalette.WithAlpha(PixelPalette.Lighten(accent, 0.6f), 22), opacity);
            Color core = PixelUi.Fade(PixelPalette.WithAlpha(PixelPalette.Lighten(accent, 0.7f), 34), opacity);
            for (float yy = y + FillInset; yy < y + h - FillInset && yy < clip; yy += 2f)
            {
                float bx = start - (yy - y) * 0.5f;
                Span(r, bx, bx + 22f, yy, left, right, soft);
                Span(r, bx + 7f, bx + 15f, yy, left, right, core);
            }
        }

        /// <summary>가로 한 줄(높이 2)을 [left, right] 안으로 잘라 그린다.</summary>
        private static void Span(Renderer r, float x0, float x1, float y, float left, float right, Color c)
        {
            x0 = Math.Max(x0, left);
            x1 = Math.Min(x1, right);
            if (x1 > x0)
            {
                r.DrawRectangle(x0, y, x1 - x0, 2f, c);
            }
        }

        /// <summary>보라 등급 이상: 테두리를 따라 깜박이는 십자 반짝임.</summary>
        private static void DrawSparkles(Renderer r, float x, float y, float w, float h, Color accent, double time, float opacity)
        {
            Color c = PixelPalette.Lighten(accent, 0.55f);
            float perimeter = (w + h) * 2f;
            for (int i = 0; i < 7; i++)
            {
                float twinkle = (float)Math.Sin(time * 2.6 + i * 1.9 + x * 0.02);
                if (twinkle < 0.55f) continue;
                float a = (twinkle - 0.55f) / 0.45f;
                float d = PixelUi.Hash(i * 23 + (int)x) * perimeter;
                float px, py;
                if (d < w) { px = x + d; py = y; }
                else if (d < w + h) { px = x + w; py = y + d - w; }
                else if (d < w * 2f + h) { px = x + w - (d - w - h); py = y + h; }
                else { px = x; py = y + h - (d - w * 2f - h); }
                px = (float)Math.Round(px);
                py = (float)Math.Round(py);
                Color cc = PixelUi.Fade(c, a * opacity);
                r.DrawRectangle(px - 2f, py, 5f, 1f, cc);
                r.DrawRectangle(px, py - 2f, 1f, 5f, cc);
                r.DrawRectangle(px, py, 1f, 1f, PixelUi.Fade(Color.White, a * opacity));
            }
        }

        /// <summary>그림 전체를 그리되 clip 아래는 잘라 낸다.</summary>
        private static void ImageClipped(Renderer r, Image img, float x, float y, float w, float h, Color tint, float clip)
        {
            RegionClipped(r, img, x, y, w, h, 0f, 0f, 1f, 1f, tint, clip);
        }

        private static void RegionClipped(Renderer r, Image img, float x, float y, float w, float h, float u0, float v0, float u1, float v1, Color tint, float clip)
        {
            if (img == null || w <= 0f || h <= 0f || y >= clip)
            {
                return;
            }

            if (y + h > clip)
            {
                float k = (clip - y) / h;
                v1 = v0 + (v1 - v0) * k;
                h = clip - y;
            }

            r.DrawImageRegion(img, x, y, w, h, u0, v0, u1, v1, tint);
        }

        /// <summary>Renderer.DrawImageNineSlice와 같지만 clip 아래를 잘라 낸다.</summary>
        private static void NineSliceClipped(Renderer r, Image img, float x, float y, float w, float h, float srcBorder, float dstBorder, Color tint, float clip)
        {
            if (img == null)
            {
                return;
            }

            if (clip >= y + h)
            {
                r.DrawImageNineSlice(img, x, y, w, h, srcBorder, dstBorder, tint);
                return;
            }

            float db = Math.Min(dstBorder, Math.Min(w, h) * 0.5f);
            float su = Math.Min(0.5f, srcBorder / img.Width);
            float sv = Math.Min(0.5f, srcBorder / img.Height);
            float[] u = { 0f, su, 1f - su, 1f };
            float[] v = { 0f, sv, 1f - sv, 1f };
            float[] dx = { x, x + db, x + w - db, x + w };
            float[] dy = { y, y + db, y + h - db, y + h };
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    RegionClipped(r, img, dx[col], dy[row], dx[col + 1] - dx[col], dy[row + 1] - dy[row],
                        u[col], v[row], u[col + 1], v[row + 1], tint, clip);
                }
            }
        }
    }
}
