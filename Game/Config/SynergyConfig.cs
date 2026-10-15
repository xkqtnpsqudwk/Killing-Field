namespace My2DEngine.Game.Config
{
    /// <summary>
    /// 카드 계열 시너지와 카드 고르기 화면의 다시 뽑기·건너뛰기 수치.
    /// </summary>
    public static class SynergyConfig
    {
        /// <summary>시너지 1단계에 필요한 같은 계열 카드 수.</summary>
        public const int Tier1Cards = 3;

        /// <summary>시너지 2단계에 필요한 같은 계열 카드 수.</summary>
        public const int Tier2Cards = 6;

        // ── 화력 ──
        public const float FirepowerFireRateBonus = 0.10f;
        public const float FirepowerDamageMult = 1.20f;

        // ── 대시 ──
        /// <summary>대시가 끝난 뒤에도 피해를 받지 않는 시간(초).</summary>
        public const float DashGraceSeconds = 0.5f;
        public const float DashCooldownBonus = 0.25f;

        // ── 보호막 ──
        public const float ShieldMaxMult = 1.25f;
        public const float ShieldDelayMult = 0.6f;

        // ── 처치 ──
        public const float SlayerKillShield = 5f;
        public const float SlayerKillHealth = 3f;

        // ── 생존 ──
        public const float SurvivalMaxHealthMult = 1.15f;
        public const float SurvivalLowHealthThreshold = 0.30f;
        public const float SurvivalLowHealthIncomingMult = 0.70f;

        // ── 다시 뽑기·건너뛰기 ──
        /// <summary>보상 한 번에서 첫 다시 뽑기 비용(코인).</summary>
        public const int RerollBaseCost = 4;

        /// <summary>같은 보상에서 다시 뽑을 때마다 늘어나는 비용.</summary>
        public const int RerollCostStep = 3;

        /// <summary>카드를 받지 않고 건너뛸 때 받는 코인.</summary>
        public const int SkipCoinReward = 3;
    }
}
