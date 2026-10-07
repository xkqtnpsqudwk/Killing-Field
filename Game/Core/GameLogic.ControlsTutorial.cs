using System;
using System.Drawing;
using My2DEngine.Engine.Rendering;
using My2DEngine.Game.Config;

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

            int a = (int)(alpha * 200f);
            int ta = (int)(alpha * 255f);

            float sw = RenderConfig.GpuWorldMaxRenderWidth;
            float sh = RenderConfig.GpuWorldMaxRenderHeight;
            float cx = sw * 0.5f;
            float panelW = Math.Min(320f, sw - 40f);
            float panelH = 150f;
            float panelX = cx - panelW * 0.5f;
            float panelY = sh * 0.62f;

            r.DrawRectangle(panelX, panelY, panelW, panelH, Color.FromArgb(Math.Min(a, 200), 10, 14, 22));
            r.DrawRectangle(panelX, panelY, panelW, 1f, Color.FromArgb(Math.Min(a, 180), 120, 160, 220));
            r.DrawRectangle(panelX, panelY + panelH - 1f, panelW, 1f, Color.FromArgb(Math.Min(a, 180), 120, 160, 220));

            float ty = panelY + 12f;
            float lineH = 22f;
            Color headerCol = Color.FromArgb(ta, 180, 210, 255);
            Color keyCol = Color.FromArgb(ta, 255, 220, 100);
            Color descCol = Color.FromArgb(ta, 210, 210, 210);

            r.DrawTextCenteredShadow("[ 조작 안내 ]", cx, ty, headerCol, 10f);
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
            r.DrawText(key, keyX, y, keyCol, 9f);
            r.DrawText(desc, descX, y, descCol, 9f);
        }
    }
}
