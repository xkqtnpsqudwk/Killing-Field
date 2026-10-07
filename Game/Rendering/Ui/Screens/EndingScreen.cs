using System.Drawing;
using My2DEngine.Engine.Rendering;

namespace My2DEngine.Game.Rendering.Ui.Screens
{
    /// <summary>666층 돌파 엔딩 연출.</summary>
    public static class EndingScreen
    {
        /// <param name="progress">연출 진행도 0~1.</param>
        /// <param name="unlockedNow">이번에 무한 모드를 처음 해금했는지.</param>
        public static void Draw(Renderer r, float progress, bool unlockedNow)
        {
            float fw = OverlayPanels.ScreenWidth;
            float fh = OverlayPanels.ScreenHeight;
            if (progress < 0f) progress = 0f;
            if (progress > 1f) progress = 1f;

            OverlayPanels.Dim(r, (int)(160f + progress * 55f));
            PixelUi.Frame(r, fw * 0.5f - 170f, fh * 0.24f, 340f, fh * 0.34f, 1f, PixelPalette.PanelDeep, PixelPalette.Brass);

            PixelUi.Title(r, "666층 돌파", fw * 0.5f, fh * 0.33f, PixelPalette.Brass, PixelUi.FontBase * 2f, 1f);
            r.DrawTextCenteredShadow("최종 보스를 격파했습니다", fw * 0.5f, fh * 0.405f,
                Color.FromArgb(235, 245, 240, 230), 12f);

            string unlockText = unlockedNow ? "무한 모드 해금" : "무한 모드 유지";
            Color unlockColor = unlockedNow
                ? Color.FromArgb(255, 125, 220, 150)
                : Color.FromArgb(255, 175, 205, 255);
            r.DrawTextCenteredShadow(unlockText, fw * 0.5f, fh * 0.47f, unlockColor, 12f);
            r.DrawTextCenteredShadow("잠시 후 끝없는 층으로 진입할 수 있습니다", fw * 0.5f, fh * 0.53f,
                Color.FromArgb(225, 225, 225, 225), 6f);
        }
    }
}
