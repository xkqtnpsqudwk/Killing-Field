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

    /// <summary>
    /// P 키로 여는 현재 스탯 화면. 생존·공격·기동과 보상 세 칸으로 나누고,
    /// 한 줄에 이름(왼쪽)과 최종 값(오른쪽)만 둔다. 카드로 오른 항목만 초록 꼬리표("카드 N장")를 달고,
    /// 아직 0인 항목은 흐리게 써서 눈이 가야 할 값이 먼저 보이게 한다.
    /// </summary>
    public static class PlayerStatsScreen
    {
        private const float PanelW = 612f;
        private const float PanelH = 344f;
        private const float ColumnGap = 8f;
        private const float RowH = 16f;
        private const float GaugeRowH = 27f;
        private const float TextSize = 6f;

        private static readonly Color Label = Color.FromArgb(255, 196, 190, 178);
        private static readonly Color Value = Color.FromArgb(255, 244, 238, 224);
        private static readonly Color Zero = Color.FromArgb(255, 96, 92, 88);
        private static readonly Color Boosted = Color.FromArgb(255, 132, 226, 120);
        private static readonly Color Tag = Color.FromArgb(255, 108, 196, 100);
        private static readonly Color Stripe = Color.FromArgb(18, 255, 255, 255);

        public static void Draw(Renderer r, PlayerStatsView v)
        {
            PermanentProgressionData data = v.Permanent;
            Player player = v.Player;
            Weapon weapon = v.Weapon;

            float fw = OverlayPanels.ScreenWidth;
            float fh = OverlayPanels.ScreenHeight;
            float panelX = (float)Math.Round((fw - PanelW) * 0.5f);
            float panelY = (float)Math.Round((fh - PanelH) * 0.5f);
            float panelCenterX = panelX + PanelW * 0.5f;

            OverlayPanels.Dim(r, 190);
            OverlayPanels.Panel(r, panelX, panelY, PanelW, PanelH,
                Color.FromArgb(250, 16, 18, 24), Color.FromArgb(210, 140, 205, 255));
            OverlayPanels.Header(r, "현재 스탯", panelCenterX, panelY + 13f);

            float colTop = panelY + 34f;
            float colH = PanelH - 34f - 34f;
            float colW = (float)Math.Floor((PanelW - 28f - ColumnGap * 2f) / 3f);
            float colX0 = panelX + 14f;

            DrawSurvival(r, v, player, colX0, colTop, colW, colH);
            DrawOffense(r, v, weapon, colX0 + colW + ColumnGap, colTop, colW, colH);
            DrawMobility(r, v, player, colX0 + (colW + ColumnGap) * 2f, colTop, colW, colH);

            // 아래 줄: 영구 스탯과 닫기 안내
            float footY = panelY + PanelH - 25f;
            string permanent = $"영구 스탯   감각 Lv {data.GetSenseTier()}   행운 Lv {data.GetLuckLevel()}   " +
                $"{WeaponPresentation.GetDisplayName(WeaponType.AMPistol)} +{data.GetPistolDamageBonus() * 100f:0}%   " +
                $"체력 +{data.GetHealthBonus() * 100f:0}%   속도 +{data.GetMoveSpeedBonus() * 100f:0}%";
            PixelUi.Text(r, permanent, colX0 + 4f, footY, PixelPalette.Brass, TextSize, 1f);
            PixelUi.TextRight(r, "P 닫기", panelX + PanelW - 18f, footY, PixelPalette.TextDim, TextSize, 1f);
        }

        // ── 세 칸 ────────────────────────────────────────────────────────────

        private static void DrawSurvival(Renderer r, PlayerStatsView v, Player player, float x, float y, float w, float h)
        {
            float rowY = Column(r, "생존", PixelPalette.Health, x, y, w, h);

            float maxBonus = v.Permanent.GetHealthBonus() + v.Total(StatType.MaxHealth);
            rowY = Gauge(r, x, rowY, w, "체력", $"{player.Health:0} / {player.MaxHealth:0}",
                player.MaxHealth > 0f ? player.Health / player.MaxHealth : 0f, PixelPalette.Health,
                maxBonus > 0f ? $"최대 +{maxBonus * 100f:0}%" : null);
            float shieldBonus = v.Total(StatType.MaxShield);
            rowY = Gauge(r, x, rowY, w, "보호막", $"{player.Shield:0} / {player.MaxShield:0}",
                player.MaxShield > 0f ? player.Shield / player.MaxShield : 0f, PixelPalette.Shield,
                shieldBonus > 0f ? $"최대 +{shieldBonus * 100f:0}%" : null);
            rowY = Gauge(r, x, rowY, w, "스태미나", $"{player.Stamina:0} / {player.MaxStamina:0}",
                player.MaxStamina > 0f ? player.Stamina / player.MaxStamina : 0f, PixelPalette.Stamina, null);

            rowY += 4f;
            int row = 0;
            rowY = PercentRow(r, x, rowY, w, row++, "피해 감소", v.Bonus(StatType.DamageReduction), v.Count(StatType.DamageReduction));

            string toxic = v.LifeStealToxic > v.LifeStealNow + 0.001f ? $"독안개 {v.LifeStealToxic * 100f:0}%" : null;
            rowY = Row(r, x, rowY, w, row++, "흡혈", Pct(v.LifeStealNow), v.LifeStealNow > 0f ? Boosted : Zero,
                toxic ?? CardTag(v.Count(StatType.LifeSteal)));
            rowY = PercentRow(r, x, rowY, w, row++, "처치 회복", v.Total(StatType.KillHeal), v.Count(StatType.KillHeal));
            rowY = Row(r, x, rowY, w, row++, "보호막 회복", $"{v.ShieldRegenRate:0.#}/s", Value, CardTag(v.Count(StatType.ShieldRegenRate)));
            Row(r, x, rowY, w, row, "회복 지연", $"{v.ShieldRegenDelay:0.#}s", Value, CardTag(v.Count(StatType.ShieldRegenRate)));
        }

        private static void DrawOffense(Renderer r, PlayerStatsView v, Weapon weapon, float x, float y, float w, float h)
        {
            float rowY = Column(r, "공격", PixelPalette.AccentHot, x, y, w, h);

            int row = 0;
            rowY = Row(r, x, rowY, w, row++, "무기", CardText.WeaponName(weapon.CurrentType), PixelPalette.Brass, null);
            rowY = Row(r, x, rowY, w, row++, "탄약", $"{weapon.CurrentAmmo} / {weapon.MagazineSize}",
                v.Total(StatType.AmmoCapacity) > 0f ? Boosted : Value,
                v.Total(StatType.AmmoCapacity) > 0f ? $"최대 +{v.Total(StatType.AmmoCapacity) * 100f:0}%" : null);
            rowY = Row(r, x, rowY, w, row++, "공격력", $"{weapon.CurrentDamage:0.0}", Value, null);
            rowY = Row(r, x, rowY, w, row++, "공격 배율", $"x{weapon.StatDamageMult:0.00}",
                weapon.StatDamageMult > 1.001f ? Boosted : Value, CardTag(v.Count(StatType.Damage)));
            rowY = PercentRow(r, x, rowY, w, row++, "발사 속도", v.Bonus(StatType.FireRate), v.Count(StatType.FireRate), plus: true);
            rowY = PercentRow(r, x, rowY, w, row++, "치명타", v.Bonus(StatType.CriticalChance), v.Count(StatType.CriticalChance));

            rowY += 3f;
            PixelUi.Text(r, "조건부 추가 피해", x + 8f, rowY, PixelPalette.TextDim, TextSize, 1f);
            r.DrawRectangle(x + 8f, rowY + 10f, w - 16f, 1f, PixelPalette.WithAlpha(PixelPalette.AccentHot, 70));
            rowY += 14f;

            row = 0;
            rowY = PercentRow(r, x, rowY, w, row++, "보호막 있을 때", v.Total(StatType.ShieldedDamage), v.Count(StatType.ShieldedDamage), plus: true);
            rowY = PercentRow(r, x, rowY, w, row++, "대시 직후", v.Total(StatType.DashStrikeDamage), v.Count(StatType.DashStrikeDamage), plus: true);
            rowY = PercentRow(r, x, rowY, w, row++, "저체력", v.Total(StatType.LowHealthRage), v.Count(StatType.LowHealthRage), plus: true);
            rowY = PercentRow(r, x, rowY, w, row++, "연속 처치", v.Total(StatType.KillChain), v.Count(StatType.KillChain), plus: true);

            // 보상 풀에서 뺀 카드는 예전 저장으로 값이 남아 있을 때만 보인다.
            rowY = RetiredRow(r, x, rowY, w, ref row, "폭발", v, StatType.ExplosiveSpecialist);
            rowY = RetiredRow(r, x, rowY, w, ref row, "탄약 부족", v, StatType.LowAmmoRage);
            RetiredRow(r, x, rowY, w, ref row, "연사", v, StatType.RapidFireChain);
        }

        private static float RetiredRow(Renderer r, float x, float y, float w, ref int row, string label, PlayerStatsView v, StatType stat)
        {
            if (v.Total(stat) <= 0.0001f)
            {
                return y;
            }

            return PercentRow(r, x, y, w, row++, label, v.Total(stat), v.Count(stat), plus: true);
        }

        private static void DrawMobility(Renderer r, PlayerStatsView v, Player player, float x, float y, float w, float h)
        {
            float rowY = Column(r, "기동 · 보상", PixelPalette.Info, x, y, w, h);

            float speedBonus = v.Permanent.GetMoveSpeedBonus() + v.Total(StatType.MoveSpeed);
            float dashCooldown = PlayerConfig.DashCooldownDuration * player.DashCooldownMult;
            float ammoDrop = Math.Min(1f, RewardConfig.NormalEnemyAmmoDropChance + v.Total(StatType.AmmoDropChance));
            float coinDrop = Math.Min(1f, RewardConfig.NormalEnemyCoinDropChance + v.Total(StatType.CoinDropChance));
            int choiceBonus = v.Bonus(StatType.CardChoiceBonus) >= 1f ? 1 : 0;

            int row = 0;
            rowY = Row(r, x, rowY, w, row++, "이동 속도", $"{player.MoveSpeed:0.00}", speedBonus > 0f ? Boosted : Value,
                speedBonus > 0f ? $"+{speedBonus * 100f:0}%" : null);
            rowY = Row(r, x, rowY, w, row++, "대시 쿨다운", $"{dashCooldown:0.00}s",
                dashCooldown < PlayerConfig.DashCooldownDuration - 0.001f ? Boosted : Value, CardTag(v.Count(StatType.DashCooldown)));
            rowY = PercentRow(r, x, rowY, w, row++, "처치 시 대시 환급", v.Total(StatType.KillDashCooldownRefund), v.Count(StatType.KillDashCooldownRefund));

            rowY += 6f;
            rowY = Row(r, x, rowY, w, row++, "탄약 드랍", Pct(ammoDrop),
                v.Total(StatType.AmmoDropChance) > 0f ? Boosted : Value, CardTag(v.Count(StatType.AmmoDropChance)));
            rowY = Row(r, x, rowY, w, row++, "코인 드랍", Pct(coinDrop),
                v.Total(StatType.CoinDropChance) > 0f ? Boosted : Value, CardTag(v.Count(StatType.CoinDropChance)));
            rowY = PercentRow(r, x, rowY, w, row++, "상점 할인", v.Total(StatType.ShopDiscount), v.Count(StatType.CoinDropChance));
            rowY = Row(r, x, rowY, w, row++, "카드 선택지", choiceBonus > 0 ? $"+{choiceBonus}" : "0", choiceBonus > 0 ? Boosted : Zero, null);

            rowY += 6f;
            Row(r, x, rowY, w, row, "코인", player.CoinCount.ToString(), PixelPalette.Brass, null);
        }

        // ── 칸·줄 그리기 ─────────────────────────────────────────────────────

        /// <summary>얇은 금속 칸과 색 제목. 첫 줄의 Y를 돌려준다.</summary>
        private static float Column(Renderer r, string title, Color accent, float x, float y, float w, float h)
        {
            PixelUi.HudFrame(r, x, y, w, h, 1f, Color.FromArgb(250, 12, 13, 18), accent);
            r.DrawRectangle(x + 5f, y + 5f, w - 10f, 16f, PixelPalette.WithAlpha(PixelPalette.Darken(accent, 0.7f), 200));
            r.DrawRectangle(x + 5f, y + 20f, w - 10f, 1f, PixelPalette.WithAlpha(accent, 180));
            r.DrawRectangle(x + 5f, y + 5f, 2f, 16f, accent);
            PixelUi.Text(r, title, x + 12f, y + 7f, PixelPalette.Lighten(accent, 0.35f), TextSize, 1f, bold: true);
            return y + 26f;
        }

        /// <summary>이름·값 한 줄에 게이지 막대. tag가 있으면 이름 뒤에 초록 꼬리표.</summary>
        private static float Gauge(Renderer r, float x, float y, float w, string label, string value, float ratio, Color color, string tag)
        {
            float left = x + 8f;
            float right = x + w - 8f;
            PixelUi.Text(r, label, left, y, Label, TextSize, 1f);
            DrawTag(r, label, tag, left, y);
            PixelUi.TextRight(r, value, right, y, Value, TextSize, 1f);
            PixelUi.SegmentBar(r, left, y + 10f, right - left, 8f, 1f, ratio, color, 16);
            return y + GaugeRowH;
        }

        /// <summary>이름(왼쪽)과 값(오른쪽) 한 줄. 짝수 줄에는 옅은 줄무늬를 깐다.</summary>
        private static float Row(Renderer r, float x, float y, float w, int index, string label, string value, Color valueColor, string tag)
        {
            float left = x + 8f;
            float right = x + w - 8f;
            if (index % 2 == 0)
            {
                r.DrawRectangle(x + 5f, y - 3f, w - 10f, RowH, Stripe);
            }

            PixelUi.Text(r, label, left, y, valueColor == Zero ? Zero : Label, TextSize, 1f);
            DrawTag(r, label, tag, left, y);
            PixelUi.TextRight(r, value, right, y, valueColor, TextSize, 1f);
            return y + RowH;
        }

        /// <summary>비율 스탯 한 줄. 0이면 흐리게, 오르면 초록 값과 "카드 N장" 꼬리표.</summary>
        private static float PercentRow(Renderer r, float x, float y, float w, int index, string label, float amount, int cards, bool plus = false)
        {
            bool any = amount > 0.0001f;
            string text = any ? (plus ? "+" : "") + Pct(amount) : "-";
            return Row(r, x, y, w, index, label, text, any ? Boosted : Zero, CardTag(cards));
        }

        private static void DrawTag(Renderer r, string label, string tag, float left, float y)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return;
            }

            float labelW = r.MeasureText(label, TextSize).Width;
            PixelUi.Text(r, tag, left + labelW + 5f, y, Tag, TextSize, 1f);
        }

        private static string CardTag(int count)
        {
            return count > 0 ? $"카드 {count}장" : null;
        }

        private static string Pct(float ratio)
        {
            return $"{ratio * 100f:0}%";
        }
    }
}
