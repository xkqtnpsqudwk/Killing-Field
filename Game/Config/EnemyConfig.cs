namespace My2DEngine.Game.Config
{
    /// <summary>
    /// 적 성장 배율, 피격 반응, 보스 등장·페이즈 전환 값.
    /// 값을 바꾸면 게임 밸런스에 바로 영향을 주므로 의도적으로 수정할 때만 바꾼다.
    /// </summary>
    public static class EnemyConfig
    {
        /// <summary>보스 등장 연출(인트로) 지속 시간(초). 이 시간 동안 화면에 보스 이름이 표시된다.</summary>
        public const float BossIntroDuration = 2.4f;

        /// <summary>보스가 2페이즈로 전환되는 체력 비율이다.</summary>
        public const float BossPhaseTwoHealthRatio = 0.66f;

        /// <summary>보스가 3페이즈로 전환되는 체력 비율이다.</summary>
        public const float BossPhaseThreeHealthRatio = 0.33f;

        /// <summary>보스 페이즈가 하나 올라갈 때마다 특수 패턴 피해/범위 계수에 더하는 값이다.</summary>
        public const float BossPhasePowerBonusPerPhase = 0.12f;

        /// <summary>보스 2페이즈에서 특수 패턴 예비 동작 시간에 곱하는 값이다.</summary>
        public const float BossPhaseTwoWindupMultiplier = 0.9f;

        /// <summary>보스 3페이즈에서 특수 패턴 예비 동작 시간에 곱하는 값이다.</summary>
        public const float BossPhaseThreeWindupMultiplier = 0.78f;

        /// <summary>보스 2페이즈에서 특수/궁극 패턴 재사용 대기시간에 곱하는 값이다.</summary>
        public const float BossPhaseTwoCooldownMultiplier = 0.88f;

        /// <summary>보스 3페이즈에서 특수/궁극 패턴 재사용 대기시간에 곱하는 값이다.</summary>
        public const float BossPhaseThreeCooldownMultiplier = 0.72f;

        /// <summary>보스 2페이즈에서 공격 전조 아우라 강도에 더하는 값이다.</summary>
        public const float BossPhaseTwoTelegraphIntensityBonus = 0.08f;

        /// <summary>보스 3페이즈에서 공격 전조 아우라 강도에 더하는 값이다.</summary>
        public const float BossPhaseThreeTelegraphIntensityBonus = 0.16f;

        /// <summary>보스 페이즈가 하나 올라갈 때마다 주력 탄막 수에 더하는 값이다.</summary>
        public const int BossPhaseProjectileBonusPerPhase = 2;

        /// <summary>보스 페이즈가 하나 올라갈 때마다 보조 탄막 수에 더하는 값이다.</summary>
        public const int BossPhaseMinorProjectileBonusPerPhase = 1;

        /// <summary>보스 페이즈가 하나 올라갈 때마다 투사체 속도 계수에 더하는 값이다.</summary>
        public const float BossPhaseProjectileSpeedBonusPerPhase = 0.08f;

        /// <summary>보스 페이즈가 하나 올라갈 때마다 투사체 반경 계수에 더하는 값이다.</summary>
        public const float BossPhaseProjectileRadiusBonusPerPhase = 0.04f;

        /// <summary>보스 페이즈가 하나 올라갈 때마다 돌진 거리에 더하는 비율이다.</summary>
        public const float BossPhaseDashDistanceBonusPerPhase = 0.18f;

        /// <summary>전투 층(휴식 제외) 1회 클리어당 이후 적 최대 체력에 더해지는 성장 배율이다.</summary>
        public const float EnemyHealthGrowthPerClearedCombatFloor = 0.004f;

        /// <summary>전투 층(휴식 제외) 1회 클리어당 이후 적 공격력에 더해지는 성장 배율이다.</summary>
        public const float EnemyDamageGrowthPerClearedCombatFloor = 0.003f;

        /// <summary>전투 층(휴식 제외) 1회 클리어당 이후 적 이동 속도에 더해지는 성장 배율이다.</summary>
        public const float EnemyMoveSpeedGrowthPerClearedCombatFloor = 0.005f;

        /// <summary>보스 1회 클리어당 이후 적 최대 체력에 더해지는 성장 배율이다.</summary>
        public const float EnemyHealthGrowthPerBossClear = 0.04f;

        /// <summary>보스 1회 클리어당 이후 적 공격력에 더해지는 성장 배율이다.</summary>
        public const float EnemyDamageGrowthPerBossClear = 0.03f;

        /// <summary>보스 1회 클리어당 이후 적 이동 속도에 더해지는 성장 배율이다.</summary>
        public const float EnemyMoveSpeedGrowthPerBossClear = 0.015f;

        /// <summary>적 피격 플래시가 유지되는 시간(초).</summary>
        public const float EnemyHitFlashDuration = 0.18f;

        /// <summary>적이 피격 직후 움찔거리는 반응이 유지되는 시간(초).</summary>
        public const float EnemyHitReactDuration = 0.16f;

        /// <summary>일반 적이 피격 반응 중 이동 속도에 곱하는 배율.</summary>
        public const float EnemyHitReactMoveMultiplier = 0.42f;

        /// <summary>보스급 적이 피격 반응 중 이동 속도에 곱하는 배율.</summary>
        public const float BossHitReactMoveMultiplier = 0.72f;

        /// <summary>적 피격 반응 중 렌더 스케일에 더하는 최대 펄스 배율.</summary>
        public const float EnemyHitReactScalePulse = 0.08f;

        /// <summary>보스 처치 시 탄약 드롭 확률.</summary>
        public const float BossAmmoDropChance = 0.9f;

        /// <summary>보스 처치 시 코인 드롭 확률.</summary>
        public const float BossCoinDropChance = 0.50f;
    }
}
