using System;
using System.Drawing;
using My2DEngine.Engine.Rendering;

namespace My2DEngine.Game.Rendering.Ui.Screens
{
    /// <summary>첫 런에서 잠깐 보이는 조작 안내 상자.</summary>
    public static class ControlsTutorialScreen
    {
        /// <param name="alpha">0~1 불투명도(사라질 때 줄어든다).</param>
        public static void Draw(Renderer r, float alpha)
        {
            float sw = OverlayPanels.ScreenWidth;
            float cx = sw * 0.5f;
            float panelW = Math.Min(200f, sw - 40f);
            float panelH = 86f;
            float panelX = cx - panelW * 0.5f;
            float panelY = 70f;

            PixelUi.Frame(r, panelX, panelY, panelW, panelH, 1f, PixelPalette.Panel, PixelPalette.Info, raised: true, opacity: alpha);

            float ty = panelY + 10f;
            float lineH = 15f;
            Color headerCol = PixelUi.Fade(PixelPalette.Info, alpha);
            Color keyCol = PixelUi.Fade(PixelPalette.Brass, alpha);
            Color descCol = PixelUi.Fade(PixelPalette.Text, alpha);

            PixelUi.TextCentered(r, "조작 안내", cx, ty, headerCol, PixelUi.FontBase * 0.5f, 1f, bold: true);
            ty += lineH;

            DrawHintLine(r, cx, ty, keyCol, descCol, "WASD", "이동");
            ty += lineH;
            DrawHintLine(r, cx, ty, keyCol, descCol, "Shift", "달리기");
            ty += lineH;
            DrawHintLine(r, cx, ty, keyCol, descCol, "Ctrl", "대시");
            ty += lineH;
            DrawHintLine(r, cx, ty, keyCol, descCol, "LMB", "발사   1~5 무기 전환");
        }

        private static void DrawHintLine(Renderer r, float cx, float y, Color keyCol, Color descCol, string key, string desc)
        {
            r.DrawText(key, cx - 60f, y, keyCol, 6f);
            r.DrawText(desc, cx - 20f, y, descCol, 6f);
        }
    }
}
