using System;
using My2DEngine.Game.Config;

namespace My2DEngine.Game
{
    /// <summary>
    /// 런 동안 한 번만 얻는 특성. 숫자가 아니라 규칙을 바꾼다.
    /// 값은 저장 비트 위치이므로 바꾸지 않는다(새 특성은 뒤에 붙인다).
    /// </summary>
    public enum RunPerk
    {
        // 규칙 카드 (Purple 이상)
        Piercing = 0,
        CorpseBlast = 1,
        DoubleDash = 2,
        DashShock = 3,
        ShieldBurst = 4,

        // 악마의 계약 (8층부터, 하이리스크 하이리턴)
        BloodPact = 5,
        GlassCannon = 6,
        Frenzy = 7,
        Slaughter = 8,
        Greed = 9
    }

    public enum RunPerkKind
    {
        /// <summary>규칙 카드: 대가 없이 규칙을 바꾼다. Purple 이상에서만 나온다.</summary>
        Rule,

        /// <summary>악마의 계약: 큰 이득과 큰 대가를 함께 준다.</summary>
        Contract
    }

    /// <summary>특성 하나의 이름과 카드 문구.</summary>
    public sealed class RunPerkDefinition
    {
        internal RunPerkDefinition(RunPerk perk, RunPerkKind kind, string name, string[] gains, string[] costs)
        {
            Perk = perk;
            Kind = kind;
            Name = name;
            Gains = gains;
            Costs = costs;
        }

        public RunPerk Perk { get; }

        public RunPerkKind Kind { get; }

        public string Name { get; }

        /// <summary>얻는 것(카드에 초록으로).</summary>
        public string[] Gains { get; }

        /// <summary>대가(카드에 빨강으로). 규칙 카드는 비어 있다.</summary>
        public string[] Costs { get; }
    }

    /// <summary>모든 특성 정의 표. 순서는 <see cref="RunPerk"/> 값과 같다.</summary>
    public static class RunPerkCatalog
    {
        private static readonly RunPerkDefinition[] definitions = Build();

        public static int Count => definitions.Length;

        public static RunPerkDefinition Get(RunPerk perk)
        {
            return definitions[(int)perk];
        }

        public static int Bit(RunPerk perk)
        {
            return 1 << (int)perk;
        }

        private static string Pct(float ratio)
        {
            return $"{ratio * 100f:0}%";
        }

        private static RunPerkDefinition[] Build()
        {
            string[] none = Array.Empty<string>();
            RunPerkDefinition[] list =
            {
                // 카드 폭이 좁아(4칸이면 140) 한 줄은 15자 안팎으로 쓴다.
                new(RunPerk.Piercing, RunPerkKind.Rule, "관통탄",
                    new[] { "총알이 적 하나를 관통", $"뒤의 적에게 피해 {Pct(PerkConfig.PierceDamageRatio)}" }, none),
                new(RunPerk.CorpseBlast, RunPerkKind.Rule, "시체 폭발",
                    new[] { "쓰러진 적이 폭발", $"그 적 최대 체력 {Pct(PerkConfig.CorpseBlastHealthRatio)}", "만큼 범위 피해" }, none),
                new(RunPerk.DoubleDash, RunPerkKind.Rule, "이중 대시",
                    new[] { "대시를 2번 모아 둔다", "충전은 따로 회복" }, none),
                new(RunPerk.DashShock, RunPerkKind.Rule, "대시 충격파",
                    new[] { "대시할 때 주변에 충격파", $"무기 피해 x{PerkConfig.DashShockDamageMult:0.#}", $"{PerkConfig.DashShockStun:0.#}초 기절" }, none),
                new(RunPerk.ShieldBurst, RunPerkKind.Rule, "보호막 파열",
                    new[] { "보호막이 깨지면 폭발", "최대 보호막만큼 피해", $"{PerkConfig.ShieldBurstStun:0.#}초 기절" }, none),

                new(RunPerk.BloodPact, RunPerkKind.Contract, "피의 계약",
                    new[] { $"주는 피해 +{Pct(PerkConfig.BloodPactDamageMult - 1f)}" },
                    new[] { $"최대 체력 -{Pct(1f - PerkConfig.BloodPactMaxHealthMult)}" }),
                new(RunPerk.GlassCannon, RunPerkKind.Contract, "유리 대포",
                    new[] { $"치명타 확률 +{Pct(PerkConfig.GlassCannonCritBonus)}", $"치명타 피해 x{PerkConfig.GlassCannonCritMult:0.#}" },
                    new[] { "보호막을 잃는다" }),
                new(RunPerk.Frenzy, RunPerkKind.Contract, "광기의 계약",
                    new[] { $"발사 속도 +{Pct(PerkConfig.FrenzyFireRateBonus)}", $"이동 속도 +{Pct(PerkConfig.FrenzyMoveSpeedMult - 1f)}" },
                    new[] { $"받는 피해 +{Pct(PerkConfig.FrenzyIncomingMult - 1f)}" }),
                new(RunPerk.Slaughter, RunPerkKind.Contract, "학살의 계약",
                    new[] { $"처치마다 피해 +{Pct(PerkConfig.SlaughterDamagePerStack)}", $"(최대 +{Pct(PerkConfig.SlaughterDamagePerStack * PerkConfig.SlaughterMaxStacks)})" },
                    new[] { $"{PerkConfig.SlaughterWindow:0}초 못 죽이면 초기화", $"전투 중 체력 초당 -{Pct(PerkConfig.SlaughterDrainPerSecond)}" }),
                new(RunPerk.Greed, RunPerkKind.Contract, "탐욕의 계약",
                    new[] { $"코인 드랍 x{PerkConfig.GreedCoinChanceMult:0}, 개수 x{PerkConfig.GreedCoinAmountMult}", $"상점 가격 -{Pct(1f - PerkConfig.GreedShopPriceMult)}" },
                    new[] { $"주는 피해 -{Pct(1f - PerkConfig.GreedDamageMult)}" }),
            };

            Array values = Enum.GetValues(typeof(RunPerk));
            if (list.Length != values.Length)
            {
                throw new InvalidOperationException("RunPerkCatalog has " + list.Length + " definitions but RunPerk has " + values.Length + " values.");
            }

            for (int i = 0; i < list.Length; i++)
            {
                if ((int)list[i].Perk != i)
                {
                    throw new InvalidOperationException("RunPerkCatalog entry " + i + " is " + list[i].Perk + "; definitions must follow RunPerk order.");
                }
            }

            return list;
        }
    }
}
