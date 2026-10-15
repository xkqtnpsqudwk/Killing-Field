using System;
using My2DEngine.Game.Config;

namespace My2DEngine.Game.Core
{
    /// <summary>스탯 카드 값을 화면에 표시하는 방식.</summary>
    internal enum StatValueFormat
    {
        /// <summary>+N% (증가).</summary>
        Percent,
        /// <summary>-N% (감소: 쿨타임, 가격, 지연).</summary>
        ReductionPercent,
        /// <summary>+1처럼 정수 개수.</summary>
        Count,
        /// <summary>+N/s (초당 회복량).</summary>
        PerSecond
    }

    /// <summary>
    /// 스탯 카드 한 종류의 정의. 이름·표시 형식·상한·기본 보너스·발동 조건을 한곳에 모은다.
    /// </summary>
    internal sealed class StatCardDefinition
    {
        private readonly Func<float> cap;
        private readonly Func<CardGrade, float> baseBonus;
        private readonly Func<string> condition;
        private readonly Func<CardGrade, float> linkedBonus;

        internal StatCardDefinition(
            StatType stat,
            string name,
            string shortName,
            StatValueFormat format,
            Func<float> cap = null,
            Func<CardGrade, float> baseBonus = null,
            Func<string> condition = null,
            bool inRewardPool = true,
            StatType? linked = null,
            Func<CardGrade, float> linkedBonus = null)
        {
            Stat = stat;
            Name = name;
            ShortName = shortName;
            Format = format;
            InRewardPool = inRewardPool;
            Linked = linked;
            this.cap = cap;
            this.baseBonus = baseBonus;
            this.condition = condition;
            this.linkedBonus = linkedBonus;
        }

        /// <summary>
        /// 이 카드를 고르면 함께 오르는 스탯(합친 카드). 예: 약탈 = 코인 드랍 + 상점 할인.
        /// 함께 오르는 스탯은 따로 보상 풀에 나오지 않는다.
        /// </summary>
        internal StatType? Linked { get; }

        /// <summary>함께 오르는 스탯의 등급별 값. 정의가 없으면 등급 공통 값.</summary>
        internal float GetLinkedBonus(CardGrade grade)
        {
            return linkedBonus == null ? CardGradeHelper.GetBonusValue(grade) : linkedBonus(grade);
        }

        internal StatType Stat { get; }

        /// <summary>카드·오버레이·사망 화면에 쓰는 이름.</summary>
        internal string Name { get; }

        /// <summary>월드 라벨처럼 좁은 곳에 쓰는 짧은 이름.</summary>
        internal string ShortName { get; }

        internal StatValueFormat Format { get; }

        /// <summary>카드 보상 후보 풀에 들어가는지 여부.</summary>
        internal bool InRewardPool { get; }

        /// <summary>런 동안 누적할 수 있는 최대 보너스. 상한이 없으면 양의 무한대.</summary>
        internal float Cap => cap == null ? float.PositiveInfinity : cap();

        /// <summary>조건부 카드의 발동 조건 설명. 상시 카드면 null.</summary>
        internal string Condition => condition?.Invoke();

        /// <summary>등급별 기본 보너스. 정의가 없으면 등급 공통 값(White 3% ~ Red 18%)을 쓴다.</summary>
        internal float GetBaseBonus(CardGrade grade)
        {
            return baseBonus == null ? CardGradeHelper.GetBonusValue(grade) : baseBonus(grade);
        }

        /// <summary>누적 값을 상한 안으로 자른다. 음수와 NaN은 0으로 본다.</summary>
        internal float Clamp(float value)
        {
            if (float.IsNaN(value) || value < 0f)
            {
                return 0f;
            }

            return Math.Min(value, Cap);
        }

        /// <summary>카드 한 장의 값을 표시 문자열로 만든다.</summary>
        internal string FormatOfferValue(float value)
        {
            return Format == StatValueFormat.Count ? "+1" : FormatValue(value);
        }

        /// <summary>누적 값을 표시 문자열로 만든다.</summary>
        internal string FormatTotalValue(float value)
        {
            return Format == StatValueFormat.Count ? (value >= 1f ? "+1" : "+0") : FormatValue(value);
        }

        private string FormatValue(float value)
        {
            int pct = (int)Math.Round(value * 100f);
            switch (Format)
            {
                case StatValueFormat.ReductionPercent:
                    return $"-{pct}%";
                case StatValueFormat.PerSecond:
                    return $"+{value:0.#}/s";
                default:
                    return $"+{pct}%";
            }
        }
    }

    /// <summary>
    /// 모든 스탯 카드 정의 표. <see cref="StatType"/> 값마다 정의가 정확히 하나 있어야 하며,
    /// 빠진 값이 있으면 타입 초기화 시점에 예외를 던진다.
    /// 새 스탯 카드는 enum 값을 추가하고 여기에 정의 한 줄을 넣은 뒤,
    /// 효과(ApplyCombinedProgressionStats 등)와 저장 필드만 연결하면 된다.
    /// </summary>
    internal static class StatCardCatalog
    {
        private static readonly StatCardDefinition[] definitions = Build();

        /// <summary>카드 보상 후보 풀. 정의 순서(= enum 순서)를 따른다.</summary>
        internal static readonly StatType[] RewardPool = BuildRewardPool();

        internal static int Count => definitions.Length;

        internal static StatCardDefinition Get(StatType stat)
        {
            int index = (int)stat;
            if (index < 0 || index >= definitions.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(stat), stat, "Unknown stat card.");
            }

            return definitions[index];
        }

        private static StatCardDefinition[] Build()
        {
            // 값 규칙(2026-10-15 개편):
            // - 상시 스탯은 등급 공통 값 3/5/8/12/18%.
            // - 상한에 빨리 닿는 방어·회복 계열은 카드마다 작은 값을 따로 둔다.
            // - 조건부 피해는 조건이 붙는 만큼 공통 값의 1.5배.
            // - 겹치거나 쓸모가 좁던 카드는 풀에서 뺐다(inRewardPool: false). 저장된 런의 값은 그대로 효과가 있다.
            StatCardDefinition[] list =
            {
                new(StatType.MaxHealth, "최대 체력", "체력", StatValueFormat.Percent),
                new(StatType.MoveSpeed, "이동 속도", "속도", StatValueFormat.Percent,
                    cap: () => 0.60f),
                new(StatType.DashCooldown, "대시 쿨타임", "대시", StatValueFormat.ReductionPercent,
                    cap: () => 1f - (PlayerConfig.DashCooldownMinDuration / PlayerConfig.DashCooldownDuration),
                    baseBonus: grade => Pick(grade, 0.03f, 0.05f, 0.07f, 0.10f, 0.14f)),
                new(StatType.AmmoDropChance, "탄 드랍 확률", "탄드랍", StatValueFormat.Percent,
                    cap: () => 1f - RewardConfig.NormalEnemyAmmoDropChance),
                new(StatType.Damage, "공격력", "공격", StatValueFormat.Percent),
                new(StatType.CoinDropChance, "약탈", "약탈", StatValueFormat.Percent,
                    cap: () => 0.50f,
                    linked: StatType.ShopDiscount,
                    linkedBonus: grade => Pick(grade, 0.02f, 0.03f, 0.05f, 0.07f, 0.10f)),
                new(StatType.LifeSteal, "모든 피해 흡혈", "흡혈", StatValueFormat.Percent,
                    cap: () => 0.25f,
                    baseBonus: grade => Pick(grade, 0.01f, 0.02f, 0.03f, 0.04f, 0.06f)),
                new(StatType.DamageReduction, "피해 감소", "피감", StatValueFormat.Percent,
                    cap: () => 0.65f,
                    baseBonus: grade => Pick(grade, 0.02f, 0.03f, 0.05f, 0.07f, 0.10f)),
                new(StatType.ShopDiscount, "상점 할인", "할인", StatValueFormat.ReductionPercent,
                    cap: () => 0.50f,
                    inRewardPool: false),
                new(StatType.KillHeal, "처치 시 회복", "처치회복", StatValueFormat.Percent,
                    cap: () => 0.10f,
                    baseBonus: grade => Pick(grade, 0.005f, 0.01f, 0.015f, 0.02f, 0.03f)),
                new(StatType.KillDashCooldownRefund, "처치 시 대시 환급", "대시환급", StatValueFormat.Percent,
                    cap: () => 0.25f,
                    baseBonus: grade => Pick(grade, 0.02f, 0.03f, 0.05f, 0.07f, 0.10f)),
                new(StatType.CriticalChance, "치명타 확률", "치명", StatValueFormat.Percent,
                    cap: () => 0.60f),
                new(StatType.CardChoiceBonus, "카드 선택지 증가", "선택+", StatValueFormat.Count,
                    cap: () => 1f,
                    baseBonus: _ => 1f),
                new(StatType.ShieldRegenRate, "보호막 재생", "방패재생", StatValueFormat.PerSecond,
                    cap: () => Math.Max(0f, PlayerConfig.PlayerShieldMaxRegenRate - PlayerConfig.PlayerShieldBaseRegenRate),
                    baseBonus: grade => 1f + Math.Max(0, (int)grade),
                    linked: StatType.ShieldRegenDelayReduction),
                new(StatType.ShieldRegenDelayReduction, "보호막 회복 지연", "방패지연", StatValueFormat.ReductionPercent,
                    cap: () => 1f - (PlayerConfig.PlayerShieldMinRegenDelay / PlayerConfig.PlayerShieldBaseRegenDelay),
                    inRewardPool: false),
                new(StatType.ShieldedDamage, "보호막 피해 증폭", "방패공격", StatValueFormat.Percent,
                    cap: () => RewardConfig.ShieldedDamageBonusCap,
                    baseBonus: Conditional,
                    condition: () => "보호막이 남아 있을 때"),
                new(StatType.DashStrikeDamage, "대시 후 피해", "대시공격", StatValueFormat.Percent,
                    cap: () => RewardConfig.DashStrikeDamageBonusCap,
                    baseBonus: Conditional,
                    condition: () => $"대시 직후 {RewardConfig.DashStrikeDamageWindow:0.#}초간"),
                new(StatType.LowHealthRage, "저체력 분노", "분노", StatValueFormat.Percent,
                    cap: () => RewardConfig.LowHealthRageBonusCap,
                    baseBonus: Conditional,
                    condition: () => $"체력 {RewardConfig.LowHealthRageThreshold * 100f:0}% 미만일 때"),
                new(StatType.KillChain, "처치 연계", "연계", StatValueFormat.Percent,
                    cap: () => RewardConfig.KillChainBonusCap,
                    baseBonus: Conditional,
                    condition: () => $"처치 직후 {RewardConfig.KillChainWindow:0.#}초간"),
                // 무기 카드(AutoCannon 피해·폭발)와 겹치고, 그 무기가 없으면 효과가 없어 뺐다.
                new(StatType.ExplosiveSpecialist, "폭발 전문가", "폭발", StatValueFormat.Percent,
                    cap: () => RewardConfig.ExplosiveSpecialistBonusCap,
                    condition: () => "AutoCannon 장착 중",
                    inRewardPool: false),
                // 일부러 탄을 비워야 발동해 플레이와 어긋나서 뺐다.
                new(StatType.LowAmmoRage, "탄창 분노", "탄창분노", StatValueFormat.Percent,
                    cap: () => RewardConfig.LowAmmoRageBonusCap,
                    condition: () => $"잔탄 {RewardConfig.LowAmmoRageThreshold * 100f:0}% 이하일 때",
                    inRewardPool: false),
                // 처치 연계와 손맛이 같아 처치 연계 하나로 남겼다.
                new(StatType.RapidFireChain, "연사 가속", "연사", StatValueFormat.Percent,
                    cap: () => RewardConfig.RapidFireChainBonusCap,
                    condition: () => $"연속 명중 {RewardConfig.RapidFireChainMinStreak}회 이상",
                    inRewardPool: false),
                // 2026-10-15 추가: 빠져 있던 기본 스탯
                new(StatType.MaxShield, "최대 보호막", "방패", StatValueFormat.Percent,
                    cap: () => 1.00f),
                new(StatType.AmmoCapacity, "최대 탄약", "탄약", StatValueFormat.Percent,
                    cap: () => 1.00f,
                    baseBonus: grade => Pick(grade, 0.05f, 0.08f, 0.12f, 0.18f, 0.25f)),
                new(StatType.FireRate, "발사 속도", "속사", StatValueFormat.Percent,
                    cap: () => 0.40f,
                    baseBonus: grade => Pick(grade, 0.02f, 0.03f, 0.05f, 0.07f, 0.10f)),
            };

            Array values = Enum.GetValues(typeof(StatType));
            if (list.Length != values.Length)
            {
                throw new InvalidOperationException(
                    "StatCardCatalog has " + list.Length + " definitions but StatType has " + values.Length + " values.");
            }

            for (int i = 0; i < list.Length; i++)
            {
                if ((int)list[i].Stat != i)
                {
                    throw new InvalidOperationException(
                        "StatCardCatalog entry " + i + " is " + list[i].Stat + "; definitions must follow StatType order.");
                }
            }

            return list;
        }

        /// <summary>등급(White~Red) 순서의 값 다섯 개 중 하나를 고른다.</summary>
        private static float Pick(CardGrade grade, float white, float green, float blue, float purple, float red)
        {
            switch (grade)
            {
                case CardGrade.Green: return green;
                case CardGrade.Blue: return blue;
                case CardGrade.Purple: return purple;
                case CardGrade.Red: return red;
                default: return white;
            }
        }

        /// <summary>조건부 피해 카드 값: 조건이 붙는 만큼 공통 값의 1.5배(4.5% ~ 27%).</summary>
        private static float Conditional(CardGrade grade)
        {
            return CardGradeHelper.GetBonusValue(grade) * 1.5f;
        }

        private static StatType[] BuildRewardPool()
        {
            int count = 0;
            for (int i = 0; i < definitions.Length; i++)
            {
                if (definitions[i].InRewardPool)
                {
                    count++;
                }
            }

            var pool = new StatType[count];
            int next = 0;
            for (int i = 0; i < definitions.Length; i++)
            {
                if (definitions[i].InRewardPool)
                {
                    pool[next++] = definitions[i].Stat;
                }
            }

            return pool;
        }
    }
}
