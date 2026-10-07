using System;
using System.Drawing;
using My2DEngine.Engine.Rendering;
using My2DEngine.Game.Config;
using My2DEngine.Game.Core;

namespace My2DEngine.Game.Rendering.Ui.Screens
{
    /// <summary>P 스탯 화면이 그리는 데 필요한 값. GameLogic이 만들어 넘긴다.</summary>
    public sealed class PlayerStatsView
    {
        public Player Player;
        public Weapon Weapon;
        public PermanentProgressionData Permanent;

        /// <summary>카드로 모은 스탯 누적량(StatType 순서, 상한 적용 전).</summary>
        public float[] CardTotals;

        /// <summary>스탯 카드 획득 횟수(StatType 순서).</summary>
        public int[] CardCounts;

        /// <summary>지금 방 기준 흡혈 비율.</summary>
        public float LifeStealNow;

        /// <summary>독안개 방 기준 흡혈 비율.</summary>
        public float LifeStealToxic;

        public float ShieldRegenRate;
        public float ShieldRegenDelay;

        /// <summary>카드로 모은 스탯 누적량(상한 적용).</summary>
        public float Bonus(StatType stat) => StatCardCatalog.Get(stat).Clamp(CardTotals[(int)stat]);

        public float Total(StatType stat) => CardTotals[(int)stat];

        public int Count(StatType stat) => CardCounts[(int)stat];
    }

    /// <summary>P 키로 여는 현재 스탯 화면: 왼쪽은 현재 상태, 오른쪽은 카드 누적 내역.</summary>
    public static class PlayerStatsScreen
    {
        public static void Draw(Renderer r, PlayerStatsView v)
        {
            PermanentProgressionData data = v.Permanent;
            Player player = v.Player;
            Weapon weapon = v.Weapon;

            float fw = OverlayPanels.ScreenWidth;
            float fh = OverlayPanels.ScreenHeight;
            float panelW = 496f;
            float panelH = 344f;
            float panelX = (fw - panelW) * 0.5f;
            float panelY = (fh - panelH) * 0.5f;
            float panelCenterX = panelX + panelW * 0.5f;

            OverlayPanels.Dim(r, 178);
            OverlayPanels.Panel(r, panelX, panelY, panelW, panelH,
                Color.FromArgb(232, 18, 24, 34), Color.FromArgb(210, 140, 205, 255));

            OverlayPanels.Header(r, "현재 스탯", panelCenterX, panelY + 18f);
            r.DrawTextCenteredShadow("P로 닫기", panelCenterX, panelY + 38f,
                Color.FromArgb(205, 188, 188, 188), 6f);

            float leftX = panelX + 16f;
            float rightX = panelX + 258f;
            float headerY = panelY + 58f;
            float lineStep = 13.5f;
            float y0 = headerY + 24f;

            r.DrawText("현재 상태", leftX, headerY, Color.FromArgb(255, 255, 220, 150), 6f);
            r.DrawText("카드 누적", rightX, headerY, Color.FromArgb(255, 255, 220, 150), 6f);

            float dashCooldown = PlayerConfig.DashCooldownDuration * player.DashCooldownMult;
            string[] left =
            {
                $"체력 {player.Health:0} / {player.MaxHealth:0}",
                $"보호막 {player.Shield:0} / {player.MaxShield:0}",
                $"스태미나 {player.Stamina:0} / {player.MaxStamina:0}",
                $"이동 속도 {player.MoveSpeed:0.00}",
                $"대시 쿨다운 {dashCooldown:0.00}s",
                $"현재 무기 {CardText.WeaponName(weapon.CurrentType)}",
                $"탄약 {weapon.CurrentAmmo} / {weapon.MagazineSize}",
                $"현재 공격력 {weapon.CurrentDamage:0.00}",
                $"피해 감소 {v.Bonus(StatType.DamageReduction) * 100f:0}%",
                $"치명타 {v.Bonus(StatType.CriticalChance) * 100f:0}%",
                $"흡혈 {v.LifeStealNow * 100f:0}%",
                $"코인 {player.CoinCount}",
            };

            float ammoDrop = Math.Min(1f, RewardConfig.NormalEnemyAmmoDropChance + v.Total(StatType.AmmoDropChance));
            float coinDrop = Math.Min(1f, RewardConfig.NormalEnemyCoinDropChance + v.Total(StatType.CoinDropChance));
            string[] right =
            {
                BuildStatBreakdown("최대 체력", PlayerConfig.PlayerHealthMax, data.GetHealthBonus(), v.Total(StatType.MaxHealth), player.MaxHealth),
                BuildStatBreakdown("이동 속도", PlayerConfig.MoveSpeed, data.GetMoveSpeedBonus(), v.Total(StatType.MoveSpeed), player.MoveSpeed),
                $"공격력 배율  기본 1.00x / 카드 +{v.Total(StatType.Damage) * 100f:0}% / 최종 x{weapon.StatDamageMult:0.00}",
                $"탄 드랍 확률  기본 {RewardConfig.NormalEnemyAmmoDropChance * 100f:0}% / 카드 +{v.Total(StatType.AmmoDropChance) * 100f:0}% / 누적 {ammoDrop * 100f:0}%",
                $"대시 쿨다운  기본 {PlayerConfig.DashCooldownDuration:0.00}s / 카드 -{v.Total(StatType.DashCooldown) * 100f:0}% / 최종 {dashCooldown:0.00}s",
                $"코인 드랍(일반 적)  기본 {RewardConfig.NormalEnemyCoinDropChance * 100f:0}% / 카드 +{v.Total(StatType.CoinDropChance) * 100f:0}% / 누적 {coinDrop * 100f:0}%",
                $"흡혈  기본 0% / 카드 +{v.Total(StatType.LifeSteal) * 100f:0}% / 독안개 {v.LifeStealToxic * 100f:0}%",
                $"피해 감소 {v.Count(StatType.DamageReduction)}회 / 누적 {v.Total(StatType.DamageReduction) * 100f:0}%",
                $"상점 할인 {v.Count(StatType.ShopDiscount)}회 / 누적 {v.Total(StatType.ShopDiscount) * 100f:0}%",
                $"처치 회복 {v.Total(StatType.KillHeal) * 100f:0}% / 대시 환급 {v.Total(StatType.KillDashCooldownRefund) * 100f:0}%",
                $"치명타 {v.Total(StatType.CriticalChance) * 100f:0}% / 선택지 +{(v.Bonus(StatType.CardChoiceBonus) >= 1f ? 1 : 0)}",
                $"보호막 회복 {v.ShieldRegenRate:0.#}/s / 지연 {v.ShieldRegenDelay:0.#}s",
                $"조건부 피해 보호막 +{v.Total(StatType.ShieldedDamage) * 100f:0}% / 대시 +{v.Total(StatType.DashStrikeDamage) * 100f:0}%",
                $"   저체력 +{v.Total(StatType.LowHealthRage) * 100f:0}% 처치 +{v.Total(StatType.KillChain) * 100f:0}% 폭발 +{v.Total(StatType.ExplosiveSpecialist) * 100f:0}% 저탄 +{v.Total(StatType.LowAmmoRage) * 100f:0}% 연사 +{v.Total(StatType.RapidFireChain) * 100f:0}%",
                $"카드 횟수 체력 {v.Count(StatType.MaxHealth)} 속도 {v.Count(StatType.MoveSpeed)} 공격 {v.Count(StatType.Damage)} 흡혈 {v.Count(StatType.LifeSteal)}",
            };

            for (int i = 0; i < left.Length; i++)
            {
                DrawLine(r, leftX, y0 + lineStep * i, left[i]);
            }

            for (int i = 0; i < right.Length; i++)
            {
                DrawLine(r, rightX, y0 + lineStep * i, right[i]);
            }

            r.DrawRectangle(panelX + 18f, panelY + panelH - 56f, panelW - 36f, 1f, Color.FromArgb(110, 180, 180, 180));
            r.DrawTextCenteredShadow(
                $"영구 스탯: 감각 Lv {data.GetSenseTier()}  행운 Lv {data.GetLuckLevel()}  {WeaponPresentation.GetDisplayName(WeaponType.AMPistol)} +{data.GetPistolDamageBonus() * 100f:0}%",
                panelCenterX, panelY + panelH - 40f,
                Color.FromArgb(215, 185, 185, 185), 6f);
        }

        private static void DrawLine(Renderer r, float x, float y, string text)
        {
            r.DrawText(text, x, y, Color.FromArgb(235, 220, 220, 220), 6f);
        }

        private static string BuildStatBreakdown(string label, float baseValue, float permanentBonus, float cardBonus, float finalValue)
        {
            return $"{label}  기본 {baseValue:0.##} / 영구 +{permanentBonus * 100f:0}% / 카드 +{cardBonus * 100f:0}% / 최종 {finalValue:0.##}";
        }
    }
}
