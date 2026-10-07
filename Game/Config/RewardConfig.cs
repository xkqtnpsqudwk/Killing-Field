namespace My2DEngine.Game.Config
{
    /// <summary>
    /// 조건부 스탯 카드 상한·발동 조건, 처치 드롭 확률, 휴식 방 상점 가격.
    /// 값을 바꾸면 게임 밸런스에 바로 영향을 주므로 의도적으로 수정할 때만 바꾼다.
    /// </summary>
    public static class RewardConfig
    {
        /// <summary>보호막이 남아 있을 때 적용되는 조건부 피해 증가 카드의 최대 누적값.</summary>
        public const float ShieldedDamageBonusCap = 0.50f;

        /// <summary>대시 직후 피해 증가 카드의 최대 누적값.</summary>
        public const float DashStrikeDamageBonusCap = 0.50f;

        /// <summary>대시 직후 피해 증가 카드가 발동하는 시간 창(초).</summary>
        public const float DashStrikeDamageWindow = 1.2f;

        /// <summary>저체력 분노 카드의 최대 누적값.</summary>
        public const float LowHealthRageBonusCap = 0.40f;

        /// <summary>저체력 분노 카드가 발동하는 체력 비율 임계값 (30% 미만).</summary>
        public const float LowHealthRageThreshold = 0.30f;

        /// <summary>처치 연계 카드의 최대 누적값.</summary>
        public const float KillChainBonusCap = 0.40f;

        /// <summary>처치 연계 카드의 발동 시간 창(초). 이 시간 내 다음 발사에 보너스가 적용된다.</summary>
        public const float KillChainWindow = 1.5f;

        /// <summary>폭발 전문가 카드의 최대 누적값 (AutoCannon 장착 시 발동).</summary>
        public const float ExplosiveSpecialistBonusCap = 0.40f;

        /// <summary>탄창 분노 카드의 최대 누적값.</summary>
        public const float LowAmmoRageBonusCap = 0.40f;

        /// <summary>탄창 분노 카드가 발동하는 잔탄 비율 임계값 (15% 이하).</summary>
        public const float LowAmmoRageThreshold = 0.15f;

        /// <summary>연사 가속 카드의 최대 누적값.</summary>
        public const float RapidFireChainBonusCap = 0.40f;

        /// <summary>연사 가속 카드가 발동하는 최소 연속 명중 수.</summary>
        public const int RapidFireChainMinStreak = 3;

        /// <summary>연사 가속 카드의 연속 명중 스트릭이 이 시간(초) 내에 갱신되지 않으면 초기화된다.</summary>
        public const float RapidFireChainStreakDecayTime = 2.0f;

        /// <summary>일반 적 처치 시 탄약 드롭 확률. 탄 드랍 확률 카드의 상한도 1 - 이 값이다.</summary>
        public const float NormalEnemyAmmoDropChance = 0.28f;

        /// <summary>미니보스 처치 시 탄약 드롭 확률.</summary>
        public const float MiniBossAmmoDropChance = 0.65f;

        /// <summary>일반 적 처치 시 코인 드롭 확률.</summary>
        public const float NormalEnemyCoinDropChance = 0.10f;

        /// <summary>미니보스 처치 시 코인 드롭 확률.</summary>
        public const float MiniBossCoinDropChance = 0.25f;

        /// <summary>운 레벨당 코인 드롭 확률 보정 최대값 (10레벨 = +20%).</summary>
        public const float LuckCoinDropBonusMax = 0.20f;

        /// <summary>휴식 상점에서 카드 구매 시 기본 코인 비용.</summary>
        public const int RestShopCardBaseCost = 2;

        /// <summary>휴식 상점 카드 등급 1단계당 추가되는 코인 비용.</summary>
        public const int RestShopCardCostPerGrade = 1;

        /// <summary>보스를 1회 클리어할 때마다 휴식 상점 가격에 더해지는 코인 수.</summary>
        public const int RestShopCostIncreasePerBossClear = 1;
    }
}
