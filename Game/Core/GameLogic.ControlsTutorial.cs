using System;
using System.Drawing;
using My2DEngine.Engine.Rendering;
using My2DEngine.Game.Config;
using My2DEngine.Game.Rendering.Ui;

namespace My2DEngine.Game.Core
{
    /// <summary>
    /// 첫 런 진입 시 짧게 표시하는 조작 안내 오버레이.
    /// HasSeenControls 플래그가 false인 상태에서 첫 방이 활성화될 때 한 번만 표시된다.
    /// </summary>
    public partial class GameLogic
    {
        private const float ControlsTutorialFadeTime = 1.0f;

        private void DrawControlsTutorialOverlay(Renderer r)
        {
            if (controlsTutorialTimer <= 0f || player == null || player.IsDead)
                return;

            float alpha = controlsTutorialTimer > ControlsTutorialFadeTime
                ? 1f
                : controlsTutorialTimer / ControlsTutorialFadeTime;

            float sw = RenderConfig.GpuWorldMaxRenderWidth;
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
            float keyX = cx - 60f;
            float descX = cx - 20f;
            r.DrawText(key, keyX, y, keyCol, 6f);
            r.DrawText(desc, descX, y, descCol, 6f);
        }
    }
}
