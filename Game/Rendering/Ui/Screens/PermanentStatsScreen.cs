using System.Drawing;
using My2DEngine.Engine.Rendering;
using My2DEngine.Game.Config;
using My2DEngine.Game.Core;

namespace My2DEngine.Game.Rendering.Ui.Screens
{
    /// <summary>
    /// I 키(메뉴에서도)로 여는 영구 스탯 배분 화면. 그리기와 클릭 판정이 같은 배치를 쓴다.
    /// 칸 번호 0~4는 [1]~[5] 키 순서(체력, 속도, 감각, 권총, 행운)다.
    /// </summary>
    public static class PermanentStatsScreen
    {
        public const int SlotCount = 5;

        private const float PanelW = 470f;
        private const float PanelH = 338f;
        private const float CardW = 205f;
        private const float CardH = 60f;
        private const float GapX = 18f;
        private const float GapY = 10f;

        public static void Draw(Renderer r, PermanentProgressionData data)
        {
            GetPanel(out float panelX, out float panelY, out float panelCenterX);

            OverlayPanels.Dim(r, 190);
            OverlayPanels.Panel(r, panelX, panelY, PanelW, PanelH,
                Color.FromArgb(228, 18, 24, 34), Color.FromArgb(200, 255, 210, 115));

            OverlayPanels.Header(r, "영구 스탯", panelCenterX, panelY + 18f);
            r.DrawTextCenteredShadow("보스 처치 시 포인트 +1", panelCenterX, panelY + 40f,
                Color.FromArgb(210, 190, 190, 190), 6f);
            r.DrawTextCenteredShadow("미사용 포인트: " + data.UnspentPoints, panelCenterX, panelY + 54f,
                data.UnspentPoints > 0 ? Color.FromArgb(255, 120, 220, 140) : Color.FromArgb(210, 175, 175, 175), 6f);
            r.DrawTextCenteredShadow("무한 모드: " + (data.EndlessModeUnlocked ? "해금됨" : "잠김"),
                panelCenterX, panelY + 68f,
                data.EndlessModeUnlocked ? Color.FromArgb(230, 145, 220, 160) : Color.FromArgb(180, 160, 160, 160), 6f);

            int senseNext = data.GetSensePointsToNextLevel();
            string senseProgress = senseNext > 0 ? $"다음 Lv까지 -{senseNext}pt" : "최대 레벨";
            int luckNext = data.GetLuckPointsToNextLevel();
            string luckProgress = luckNext > 0 ? $"다음 Lv까지 -{luckNext}pt" : "최대 레벨";

            DrawCard(r, 0, "[1] 체력",
                "Lv " + data.HealthPoints,
                $"+{data.GetHealthBonus() * 100f:0}% 최대 체력",
                Color.FromArgb(255, 210, 90, 90));

            DrawCard(r, 1, "[2] 속도",
                "Lv " + data.MoveSpeedPoints,
                $"+{data.GetMoveSpeedBonus() * 100f:0}% 이동 속도",
                Color.FromArgb(255, 90, 190, 240));

            DrawCard(r, 2, "[3] 감각",
                $"Lv {data.GetSenseTier()} / 5  ({data.SenseValue:0.0} / {PlayerConfig.PermanentSenseMax:0.0})",
                $"{GetSenseDescription(data.GetSenseTier())}  {senseProgress}",
                Color.FromArgb(255, 195, 150, 255));

            DrawCard(r, 3, "[4] " + WeaponPresentation.GetDisplayName(WeaponType.AMPistol),
                "Lv " + data.PistolDamagePoints,
                $"+{data.GetPistolDamageBonus() * 100f:0}% 피해",
                Color.FromArgb(255, 255, 170, 95));

            DrawCard(r, 4, "[5] 행운",
                $"Lv {data.GetLuckLevel()} / 10  ({data.LuckValue:0.0} / {PlayerConfig.PermanentLuckMax:0.0})",
                $"고급 카드 확률 +{data.GetLuckLevel() * 10}%  {luckProgress}",
                Color.FromArgb(255, 245, 210, 110));

            string footer = data.UnspentPoints > 0
                ? "1~5로 배분, I로 닫기"
                : "배분 가능한 포인트가 없습니다. I로 닫기";
            r.DrawTextCenteredShadow(footer, panelCenterX, panelY + PanelH - 18f,
                Color.FromArgb(220, 220, 220, 220), 6f);
        }

        /// <summary>게임 좌표 클릭이 닿은 칸 번호(0~4). 없으면 -1.</summary>
        public static int HitTest(float x, float y)
        {
            for (int slot = 0; slot < SlotCount; slot++)
            {
                GetCardRect(slot, out float cx, out float cy);
                if (OverlayPanels.Contains(x, y, cx, cy, CardW, CardH))
                {
                    return slot;
                }
            }

            return -1;
        }

        private static void GetPanel(out float panelX, out float panelY, out float panelCenterX)
        {
            panelX = (OverlayPanels.ScreenWidth - PanelW) * 0.5f;
            panelY = (OverlayPanels.ScreenHeight - PanelH) * 0.5f;
            panelCenterX = panelX + PanelW * 0.5f;
        }

        /// <summary>칸 배치: 2열 2줄 + 가운데 1칸.</summary>
        private static void GetCardRect(int slot, out float x, out float y)
        {
            GetPanel(out _, out float panelY, out float panelCenterX);
            float topY = panelY + 84f;
            float leftX = panelCenterX - CardW - GapX * 0.5f;
            float rightX = panelCenterX + GapX * 0.5f;
            int row = slot / 2;
            y = topY + (CardH + GapY) * row;
            x = slot == 4 ? panelCenterX - CardW * 0.5f : (slot % 2 == 0 ? leftX : rightX);
        }

        private static void DrawCard(Renderer r, int slot, string title, string levelText, string effectText, Color accent)
        {
            GetCardRect(slot, out float x, out float y);
            float centerX = x + CardW * 0.5f;

            OverlayPanels.Panel(r, x, y, CardW, CardH, Color.FromArgb(205, 28, 36, 50), accent);

            r.DrawTextCenteredShadow(title, centerX, y + 14f, accent, 12f, true);
            r.DrawTextCenteredShadow(levelText, centerX, y + 33f,
                Color.FromArgb(235, 230, 230, 230), 6f);
            r.DrawTextCenteredShadow(effectText, centerX, y + 47f,
                Color.FromArgb(210, 185, 185, 185), 6f);
        }

        private static string GetSenseDescription(int senseTier)
        {
            switch (senseTier)
            {
                case 0: return "분기 정보 비공개";
                case 1: return "적 종류 공개";
                case 2: return "적 수 공개";
                case 3: return "보상 종류 공개";
                case 4: return "보상 힌트 강화";
                default: return "모든 분기 정보 공개";
            }
        }
    }
}
