using System;
using System.Drawing;
using System.Windows.Forms;
using My2DEngine.Engine.Input;
using My2DEngine.Engine.Rendering;
using My2DEngine.Game.Config;

namespace My2DEngine.Game.Core
{
    /// <summary>
    /// GameLogic의 플레이어 스탯 오버레이 partial.
    /// 현재 플레이어 상태와 카드 누적 효과를 P 키로 확인할 수 있게 한다.
    /// </summary>
    public partial class GameLogic
    {
        private bool playerStatsOverlayActive;
        private bool playerStatsToggleHeld;

        private void HandlePlayerStatsOverlayInput()
        {
            bool toggleHeld = Input.GetKey(Keys.P);
            if (endingSequenceActive)
            {
                playerStatsToggleHeld = toggleHeld;
                return;
            }

            if (toggleHeld &&
                !playerStatsToggleHeld &&
                !CardRewardRevealPending &&
                !cardRewardActive &&
                !branchSelectionActive &&
                !permanentStatsUiActive)
            {
                playerStatsOverlayActive = !playerStatsOverlayActive;
                if (playerStatsOverlayActive)
                {
                    OnPlayerStatsOverlayOpened();
                }
            }

            playerStatsToggleHeld = toggleHeld;
        }

        private void OnPlayerStatsOverlayOpened()
        {
            weapon.PendingShot = false;
            weapon.FireButtonHeld = false;
            StopLoopingWeaponEffects();
            interactPromptText = null;
        }

        private void ResetPlayerStatsOverlayState()
        {
            playerStatsOverlayActive = false;
            playerStatsToggleHeld = false;
        }

        private void DrawPlayerStatsOverlay(Renderer r)
        {
            if (!playerStatsOverlayActive)
            {
                return;
            }

            PermanentProgressionData data = permanentProgression ?? PermanentProgressionData.CreateDefault();
            data.Sanitize();

            float fw = GameConfig.GpuWorldMaxRenderWidth;
            float fh = GameConfig.GpuWorldMaxRenderHeight;
            float panelW = 496f;
            float panelH = 300f;
            float panelX = (fw - panelW) * 0.5f;
            float panelY = (fh - panelH) * 0.5f;
            float panelCenterX = panelX + panelW * 0.5f;

            r.DrawRectangle(0f, 0f, fw, fh, Color.FromArgb(178, 0, 0, 0));
            r.DrawRectangle(panelX, panelY, panelW, panelH, Color.FromArgb(232, 18, 24, 34));
            r.DrawRectangle(panelX, panelY, panelW, 1f, Color.FromArgb(210, 140, 205, 255));
            r.DrawRectangle(panelX, panelY + panelH - 1f, panelW, 1f, Color.FromArgb(210, 140, 205, 255));
            r.DrawRectangle(panelX, panelY, 1f, panelH, Color.FromArgb(210, 140, 205, 255));
            r.DrawRectangle(panelX + panelW - 1f, panelY, 1f, panelH, Color.FromArgb(210, 140, 205, 255));

            r.DrawTextCenteredShadow("[ 현재 스탯 ]", panelCenterX, panelY + 18f,
                Color.FromArgb(255, 235, 240, 255), 14f);
            r.DrawTextCenteredShadow("P로 닫기", panelCenterX, panelY + 38f,
                Color.FromArgb(205, 188, 188, 188), 8.5f);

            float leftX = panelX + 16f;
            float rightX = panelX + 258f;
            float headerY = panelY + 58f;
            float lineStep = 15f;

            r.DrawText("현재 상태", leftX, headerY, Color.FromArgb(255, 255, 220, 150), 10f);
            r.DrawText("카드 누적", rightX, headerY, Color.FromArgb(255, 255, 220, 150), 10f);

            float currentWeaponDamage = weapon.CurrentDamage;
            string currentWeaponName = GetWeaponName(weapon.CurrentType);
            string ammoText = weapon.CurrentAmmo + " / " + weapon.MagazineSize;

            DrawPlayerStatsLine(r, leftX, headerY + 24f, $"체력 {player.Health:0} / {player.MaxHealth:0}");
            DrawPlayerStatsLine(r, leftX, headerY + 24f + lineStep, $"스태미나 {player.Stamina:0} / {player.MaxStamina:0}");
            DrawPlayerStatsLine(r, leftX, headerY + 24f + lineStep * 2f, $"이동 속도 {player.MoveSpeed:0.00}");
            DrawPlayerStatsLine(r, leftX, headerY + 24f + lineStep * 3f, $"대시 쿨다운 {GameConfig.DashCooldownDuration * player.DashCooldownMult:0.00}s");
            DrawPlayerStatsLine(r, leftX, headerY + 24f + lineStep * 4f, $"현재 무기 {currentWeaponName}");
            DrawPlayerStatsLine(r, leftX, headerY + 24f + lineStep * 5f, $"탄약 {ammoText}");
            DrawPlayerStatsLine(r, leftX, headerY + 24f + lineStep * 6f, $"현재 공격력 {currentWeaponDamage:0.00}");
            DrawPlayerStatsLine(r, leftX, headerY + 24f + lineStep * 7f, $"탄 드랍 확률 +{GetRunStatBonus(StatType.AmmoDropChance) * 100f:0}%");
            DrawPlayerStatsLine(r, leftX, headerY + 24f + lineStep * 8f, $"코인 {player.CoinCount}");

            DrawPlayerStatsLine(r, rightX, headerY + 24f,
                BuildStatBreakdown("최대 체력", GameConfig.PlayerHealthMax, data.GetHealthBonus(), runStatBonusTotals[(int)StatType.MaxHealth], player.MaxHealth, suffix: string.Empty));
            DrawPlayerStatsLine(r, rightX, headerY + 24f + lineStep,
                BuildStatBreakdown("이동 속도", GameConfig.MoveSpeed, data.GetMoveSpeedBonus(), runStatBonusTotals[(int)StatType.MoveSpeed], player.MoveSpeed, suffix: string.Empty));
            DrawPlayerStatsLine(r, rightX, headerY + 24f + lineStep * 2f,
                $"공격력 배율  기본 1.00x / 카드 +{runStatBonusTotals[(int)StatType.Damage] * 100f:0}% / 최종 x{weapon.StatDamageMult:0.00}");
            DrawPlayerStatsLine(r, rightX, headerY + 24f + lineStep * 3f,
                $"탄 드랍 확률  기본 {NormalEnemyAmmoDropChance * 100f:0}% / 카드 +{runStatBonusTotals[(int)StatType.AmmoDropChance] * 100f:0}% / 누적 {Math.Min(1f, NormalEnemyAmmoDropChance + runStatBonusTotals[(int)StatType.AmmoDropChance]) * 100f:0}%");
            DrawPlayerStatsLine(r, rightX, headerY + 24f + lineStep * 4f,
                $"대시 쿨다운  기본 {GameConfig.DashCooldownDuration:0.00}s / 카드 -{runStatBonusTotals[(int)StatType.DashCooldown] * 100f:0}% / 최종 {GameConfig.DashCooldownDuration * player.DashCooldownMult:0.00}s");
            DrawPlayerStatsLine(r, rightX, headerY + 24f + lineStep * 5f,
                $"코인 드랍(일반 적)  기본 {NormalEnemyCoinDropChance * 100f:0}% / 카드 +{runStatBonusTotals[(int)StatType.CoinDropChance] * 100f:0}% / 누적 {Math.Min(1f, NormalEnemyCoinDropChance + runStatBonusTotals[(int)StatType.CoinDropChance]) * 100f:0}%");
            DrawPlayerStatsLine(r, rightX, headerY + 24f + lineStep * 6f,
                $"체력 카드 {runStatPickupCount[(int)StatType.MaxHealth]}회 / 누적 +{runStatBonusTotals[(int)StatType.MaxHealth] * 100f:0}%");
            DrawPlayerStatsLine(r, rightX, headerY + 24f + lineStep * 7f,
                $"속도 카드 {runStatPickupCount[(int)StatType.MoveSpeed]}회 / 누적 +{runStatBonusTotals[(int)StatType.MoveSpeed] * 100f:0}%");
            DrawPlayerStatsLine(r, rightX, headerY + 24f + lineStep * 8f,
                $"공격력 {runStatPickupCount[(int)StatType.Damage]}회  탄드랍 {runStatPickupCount[(int)StatType.AmmoDropChance]}회  대시 {runStatPickupCount[(int)StatType.DashCooldown]}회  코인 {runStatPickupCount[(int)StatType.CoinDropChance]}회");

            r.DrawRectangle(panelX + 18f, panelY + panelH - 46f, panelW - 36f, 1f, Color.FromArgb(110, 180, 180, 180));
            r.DrawTextCenteredShadow(
                $"영구 스탯: 감각 Lv {data.GetSenseTier()}  행운 Lv {data.GetLuckLevel()}  {WeaponPresentation.GetDisplayName(WeaponType.AMPistol)} +{data.GetPistolDamageBonus() * 100f:0}%",
                panelCenterX, panelY + panelH - 28f,
                Color.FromArgb(215, 185, 185, 185), 8.2f);
        }

        private static void DrawPlayerStatsLine(Renderer r, float x, float y, string text)
        {
            r.DrawText(text, x, y, Color.FromArgb(235, 220, 220, 220), 8.1f);
        }

        private static string BuildStatBreakdown(string label, float baseValue, float permanentBonus, float cardBonus, float finalValue, string suffix)
        {
            string valueSuffix = string.IsNullOrEmpty(suffix) ? string.Empty : suffix;
            return $"{label}  기본 {baseValue:0.##}{valueSuffix} / 영구 +{permanentBonus * 100f:0}% / 카드 +{cardBonus * 100f:0}% / 최종 {finalValue:0.##}{valueSuffix}";
        }
    }
}
