using System;
using System.Drawing;
using System.Windows.Forms;
using My2DEngine.Engine.Input;
using My2DEngine.Engine.Rendering;
using My2DEngine.Game.Config;
using My2DEngine.Game.Rendering.Ui;

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

            float fw = RenderConfig.GpuWorldMaxRenderWidth;
            float fh = RenderConfig.GpuWorldMaxRenderHeight;
            float panelW = 496f;
            float panelH = 344f;
            float panelX = (fw - panelW) * 0.5f;
            float panelY = (fh - panelH) * 0.5f;
            float panelCenterX = panelX + panelW * 0.5f;

            r.DrawRectangle(0f, 0f, fw, fh, Color.FromArgb(178, 0, 0, 0));
            DrawUiOverlayPanel(r, panelX, panelY, panelW, panelH,
                Color.FromArgb(232, 18, 24, 34), Color.FromArgb(210, 140, 205, 255));

            DrawOverlayHeader(r, "현재 스탯", panelCenterX, panelY + 18f);
            r.DrawTextCenteredShadow("P로 닫기", panelCenterX, panelY + 38f,
                Color.FromArgb(205, 188, 188, 188), 6f);

            float leftX = panelX + 16f;
            float rightX = panelX + 258f;
            float headerY = panelY + 58f;
            float lineStep = 13.5f;

            r.DrawText("현재 상태", leftX, headerY, Color.FromArgb(255, 255, 220, 150), 6f);
            r.DrawText("카드 누적", rightX, headerY, Color.FromArgb(255, 255, 220, 150), 6f);

            float currentWeaponDamage = weapon.CurrentDamage;
            string currentWeaponName = GetWeaponName(weapon.CurrentType);
            string ammoText = weapon.CurrentAmmo + " / " + weapon.MagazineSize;

            DrawPlayerStatsLine(r, leftX, headerY + 24f, $"체력 {player.Health:0} / {player.MaxHealth:0}");
            DrawPlayerStatsLine(r, leftX, headerY + 24f + lineStep, $"보호막 {player.Shield:0} / {player.MaxShield:0}");
            DrawPlayerStatsLine(r, leftX, headerY + 24f + lineStep * 2f, $"스태미나 {player.Stamina:0} / {player.MaxStamina:0}");
            DrawPlayerStatsLine(r, leftX, headerY + 24f + lineStep * 3f, $"이동 속도 {player.MoveSpeed:0.00}");
            DrawPlayerStatsLine(r, leftX, headerY + 24f + lineStep * 4f, $"대시 쿨다운 {PlayerConfig.DashCooldownDuration * player.DashCooldownMult:0.00}s");
            DrawPlayerStatsLine(r, leftX, headerY + 24f + lineStep * 5f, $"현재 무기 {currentWeaponName}");
            DrawPlayerStatsLine(r, leftX, headerY + 24f + lineStep * 6f, $"탄약 {ammoText}");
            DrawPlayerStatsLine(r, leftX, headerY + 24f + lineStep * 7f, $"현재 공격력 {currentWeaponDamage:0.00}");
            DrawPlayerStatsLine(r, leftX, headerY + 24f + lineStep * 8f, $"피해 감소 {GetRunStatBonus(StatType.DamageReduction) * 100f:0}%");
            DrawPlayerStatsLine(r, leftX, headerY + 24f + lineStep * 9f, $"치명타 {GetRunStatBonus(StatType.CriticalChance) * 100f:0}%");
            DrawPlayerStatsLine(r, leftX, headerY + 24f + lineStep * 10f, $"흡혈 {GetEffectiveLifeStealRatio(IsActiveToxicMistRoom()) * 100f:0}%");
            DrawPlayerStatsLine(r, leftX, headerY + 24f + lineStep * 11f, $"코인 {player.CoinCount}");

            DrawPlayerStatsLine(r, rightX, headerY + 24f,
                BuildStatBreakdown("최대 체력", PlayerConfig.PlayerHealthMax, data.GetHealthBonus(), runStatBonusTotals[(int)StatType.MaxHealth], player.MaxHealth, suffix: string.Empty));
            DrawPlayerStatsLine(r, rightX, headerY + 24f + lineStep,
                BuildStatBreakdown("이동 속도", PlayerConfig.MoveSpeed, data.GetMoveSpeedBonus(), runStatBonusTotals[(int)StatType.MoveSpeed], player.MoveSpeed, suffix: string.Empty));
            DrawPlayerStatsLine(r, rightX, headerY + 24f + lineStep * 2f,
                $"공격력 배율  기본 1.00x / 카드 +{runStatBonusTotals[(int)StatType.Damage] * 100f:0}% / 최종 x{weapon.StatDamageMult:0.00}");
            DrawPlayerStatsLine(r, rightX, headerY + 24f + lineStep * 3f,
                $"탄 드랍 확률  기본 {RewardConfig.NormalEnemyAmmoDropChance * 100f:0}% / 카드 +{runStatBonusTotals[(int)StatType.AmmoDropChance] * 100f:0}% / 누적 {Math.Min(1f, RewardConfig.NormalEnemyAmmoDropChance + runStatBonusTotals[(int)StatType.AmmoDropChance]) * 100f:0}%");
            DrawPlayerStatsLine(r, rightX, headerY + 24f + lineStep * 4f,
                $"대시 쿨다운  기본 {PlayerConfig.DashCooldownDuration:0.00}s / 카드 -{runStatBonusTotals[(int)StatType.DashCooldown] * 100f:0}% / 최종 {PlayerConfig.DashCooldownDuration * player.DashCooldownMult:0.00}s");
            DrawPlayerStatsLine(r, rightX, headerY + 24f + lineStep * 5f,
                $"코인 드랍(일반 적)  기본 {RewardConfig.NormalEnemyCoinDropChance * 100f:0}% / 카드 +{runStatBonusTotals[(int)StatType.CoinDropChance] * 100f:0}% / 누적 {Math.Min(1f, RewardConfig.NormalEnemyCoinDropChance + runStatBonusTotals[(int)StatType.CoinDropChance]) * 100f:0}%");
            DrawPlayerStatsLine(r, rightX, headerY + 24f + lineStep * 6f,
                $"흡혈  기본 0% / 카드 +{runStatBonusTotals[(int)StatType.LifeSteal] * 100f:0}% / 독안개 {GetEffectiveLifeStealRatio(true) * 100f:0}%");
            DrawPlayerStatsLine(r, rightX, headerY + 24f + lineStep * 7f,
                $"피해 감소 {runStatPickupCount[(int)StatType.DamageReduction]}회 / 누적 {runStatBonusTotals[(int)StatType.DamageReduction] * 100f:0}%");
            DrawPlayerStatsLine(r, rightX, headerY + 24f + lineStep * 8f,
                $"상점 할인 {runStatPickupCount[(int)StatType.ShopDiscount]}회 / 누적 {runStatBonusTotals[(int)StatType.ShopDiscount] * 100f:0}%");
            DrawPlayerStatsLine(r, rightX, headerY + 24f + lineStep * 9f,
                $"처치 회복 {runStatBonusTotals[(int)StatType.KillHeal] * 100f:0}% / 대시 환급 {runStatBonusTotals[(int)StatType.KillDashCooldownRefund] * 100f:0}%");
            DrawPlayerStatsLine(r, rightX, headerY + 24f + lineStep * 10f,
                $"치명타 {runStatBonusTotals[(int)StatType.CriticalChance] * 100f:0}% / 선택지 +{(GetRunStatBonus(StatType.CardChoiceBonus) >= 1f ? 1 : 0)}");
            DrawPlayerStatsLine(r, rightX, headerY + 24f + lineStep * 11f,
                $"보호막 회복 {GetEffectiveShieldRegenRate():0.#}/s / 지연 {GetEffectiveShieldRegenDelayDuration():0.#}s");
            DrawPlayerStatsLine(r, rightX, headerY + 24f + lineStep * 12f,
                $"조건부 피해 보호막 +{runStatBonusTotals[(int)StatType.ShieldedDamage] * 100f:0}% / 대시 +{runStatBonusTotals[(int)StatType.DashStrikeDamage] * 100f:0}%");
            DrawPlayerStatsLine(r, rightX, headerY + 24f + lineStep * 13f,
                $"   저체력 +{runStatBonusTotals[(int)StatType.LowHealthRage] * 100f:0}% 처치 +{runStatBonusTotals[(int)StatType.KillChain] * 100f:0}% 폭발 +{runStatBonusTotals[(int)StatType.ExplosiveSpecialist] * 100f:0}% 저탄 +{runStatBonusTotals[(int)StatType.LowAmmoRage] * 100f:0}% 연사 +{runStatBonusTotals[(int)StatType.RapidFireChain] * 100f:0}%");
            DrawPlayerStatsLine(r, rightX, headerY + 24f + lineStep * 14f,
                $"카드 횟수 체력 {runStatPickupCount[(int)StatType.MaxHealth]} 속도 {runStatPickupCount[(int)StatType.MoveSpeed]} 공격 {runStatPickupCount[(int)StatType.Damage]} 흡혈 {runStatPickupCount[(int)StatType.LifeSteal]}");

            r.DrawRectangle(panelX + 18f, panelY + panelH - 56f, panelW - 36f, 1f, Color.FromArgb(110, 180, 180, 180));
            r.DrawTextCenteredShadow(
                $"영구 스탯: 감각 Lv {data.GetSenseTier()}  행운 Lv {data.GetLuckLevel()}  {WeaponPresentation.GetDisplayName(WeaponType.AMPistol)} +{data.GetPistolDamageBonus() * 100f:0}%",
                panelCenterX, panelY + panelH - 40f,
                Color.FromArgb(215, 185, 185, 185), 6f);
        }

        private static void DrawPlayerStatsLine(Renderer r, float x, float y, string text)
        {
            r.DrawText(text, x, y, Color.FromArgb(235, 220, 220, 220), 6f);
        }

        private static string BuildStatBreakdown(string label, float baseValue, float permanentBonus, float cardBonus, float finalValue, string suffix)
        {
            string valueSuffix = string.IsNullOrEmpty(suffix) ? string.Empty : suffix;
            return $"{label}  기본 {baseValue:0.##}{valueSuffix} / 영구 +{permanentBonus * 100f:0}% / 카드 +{cardBonus * 100f:0}% / 최종 {finalValue:0.##}{valueSuffix}";
        }
    }
}
