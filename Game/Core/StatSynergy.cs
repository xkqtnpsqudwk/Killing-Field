using System.Drawing;
using My2DEngine.Game.Config;

namespace My2DEngine.Game
{
    /// <summary>스탯 카드 계열. 같은 계열 카드를 모으면 시너지 보너스가 붙는다.</summary>
    public enum StatFamily
    {
        None = 0,
        Firepower,
        Dash,
        Shield,
        Slayer,
        Survival
    }

    /// <summary>계열 하나의 이름·색·단계별 보너스 문구.</summary>
    public sealed class StatFamilyDefinition
    {
        internal StatFamilyDefinition(StatFamily family, string name, Color color, string tier1, string tier2)
        {
            Family = family;
            Name = name;
            Color = color;
            Tier1 = tier1;
            Tier2 = tier2;
        }

        public StatFamily Family { get; }

        public string Name { get; }

        public Color Color { get; }

        /// <summary>3장 보너스 설명.</summary>
        public string Tier1 { get; }

        /// <summary>6장 보너스 설명.</summary>
        public string Tier2 { get; }
    }

    /// <summary>계열 정의 표. 어느 스탯이 어느 계열인지는 StatCardCatalog 정의(family)에 있다.</summary>
    public static class StatSynergyCatalog
    {
        public static readonly StatFamily[] All =
        {
            StatFamily.Firepower, StatFamily.Dash, StatFamily.Shield, StatFamily.Slayer, StatFamily.Survival
        };

        private static readonly StatFamilyDefinition[] definitions =
        {
            null,
            new(StatFamily.Firepower, "화력", Color.FromArgb(255, 255, 140, 60),
                $"발사 속도 +{SynergyConfig.FirepowerFireRateBonus * 100f:0}%",
                $"주는 피해 +{(SynergyConfig.FirepowerDamageMult - 1f) * 100f:0}%"),
            new(StatFamily.Dash, "대시", Color.FromArgb(255, 110, 220, 255),
                $"대시 뒤 {SynergyConfig.DashGraceSeconds:0.#}초 무적",
                $"대시 쿨타임 -{SynergyConfig.DashCooldownBonus * 100f:0}%"),
            new(StatFamily.Shield, "보호막", Color.FromArgb(255, 90, 160, 255),
                $"최대 보호막 +{(SynergyConfig.ShieldMaxMult - 1f) * 100f:0}%",
                $"보호막 회복 지연 -{(1f - SynergyConfig.ShieldDelayMult) * 100f:0}%"),
            new(StatFamily.Slayer, "처치", Color.FromArgb(255, 235, 80, 90),
                $"처치 시 보호막 +{SynergyConfig.SlayerKillShield:0}",
                $"처치 시 체력 +{SynergyConfig.SlayerKillHealth:0}"),
            new(StatFamily.Survival, "생존", Color.FromArgb(255, 120, 220, 120),
                $"최대 체력 +{(SynergyConfig.SurvivalMaxHealthMult - 1f) * 100f:0}%",
                $"체력 {SynergyConfig.SurvivalLowHealthThreshold * 100f:0}% 미만일 때 받는 피해 -{(1f - SynergyConfig.SurvivalLowHealthIncomingMult) * 100f:0}%"),
        };

        public static StatFamilyDefinition Get(StatFamily family)
        {
            return definitions[(int)family];
        }

        /// <summary>계열 카드 수로 정한 시너지 단계(0, 1, 2).</summary>
        public static int TierFor(int cards)
        {
            return cards >= SynergyConfig.Tier2Cards ? 2 : (cards >= SynergyConfig.Tier1Cards ? 1 : 0);
        }

        /// <summary>다음 단계까지 필요한 카드 수(이미 2단계면 Tier2Cards).</summary>
        public static int NextThreshold(int cards)
        {
            return cards < SynergyConfig.Tier1Cards ? SynergyConfig.Tier1Cards : SynergyConfig.Tier2Cards;
        }
    }
}
