using System;
using System.Drawing;
using My2DEngine.Engine.Rendering;
using My2DEngine.Game.Config;

namespace My2DEngine.Game.Rendering.Ui.Screens
{
    /// <summary>
    /// 게임 오버레이 화면들이 함께 쓰는 상자와 제목. 좌표는 모두 게임 해상도(640×360) 기준이다.
    /// </summary>
    public static class OverlayPanels
    {
        public static float ScreenWidth => RenderConfig.GpuWorldMaxRenderWidth;
        public static float ScreenHeight => RenderConfig.GpuWorldMaxRenderHeight;

        /// <summary>화면 전체를 덮는 검은 막.</summary>
        public static void Dim(Renderer r, int alpha)
        {
            r.DrawRectangle(0f, 0f, ScreenWidth, ScreenHeight, Color.FromArgb(alpha, 0, 0, 0));
        }

        /// <summary>
        /// 공통 패널 배경. 강조 색은 테두리 고리에 써서 화면별 정체성(파랑/금색 등)을 보존한다.
        /// 월드 위 반투명 색 채움 문제를 피하려고 채움은 거의 불투명하게 올린다.
        /// </summary>
        public static void Panel(Renderer r, float x, float y, float w, float h, Color fill, Color accent)
        {
            Color solidFill = Color.FromArgb(Math.Max((int)fill.A, 250), fill.R, fill.G, fill.B);
            PixelUi.Frame(r, x, y, w, h, 1f, solidFill, accent);
        }

        /// <summary>오버레이 제목. 굵은 외곽선 황동색 글자.</summary>
        public static void Header(Renderer r, string text, float cx, float cy)
        {
            PixelUi.Title(r, text, cx, cy, PixelPalette.Brass, PixelUi.FontBase, 1f);
        }

        /// <summary>카드/분기 패널 배경. 테두리와 윗줄을 등급(또는 방 유형) 색으로 칠한다.</summary>
        public static void CardPanel(Renderer r, float x, float y, float w, float h, Color gradeColor)
        {
            PixelUi.Frame(r, x, y, w, h, 1f, PixelPalette.Panel, gradeColor);
            r.DrawRectangle(x + 3f, y + 3f, w - 6f, 2f, PixelUi.Fade(gradeColor, 0.8f));
        }

        public static bool Contains(float x, float y, float left, float top, float width, float height)
        {
            return x >= left && x < left + width && y >= top && y < top + height;
        }
    }
}
