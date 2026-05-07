using System;
using My2DEngine.Game.Config;

namespace My2DEngine.Game
{
    /// <summary>
    /// 적 상태 머신의 상위 상태다.
    /// </summary>
    public enum EnemyAiState
    {
        Patrol,
        Investigate,
        Search,
        Combat,
        AttackWindup,
        AttackRecover,
        Stunned
    }

    /// <summary>
    /// 비전투 시 배회 규칙을 분기하는 에셋 기반 프로필 분류다.
    /// </summary>
    public enum EnemyWanderStyle
    {
        Standard,
        MiniBoss,
        BossSentinel,
        BossRunner,
        BossArtillery
    }

    /// <summary>
    /// 일반 원거리 적이 어떤 발사 방식을 사용하는지 정의한다.
    /// </summary>
    public enum EnemyRangedAttackStyle
    {
        None,
        SingleShot,
        SpreadTriplet,
        BurstPair,
        WideFan
    }

    /// <summary>
    /// 보스 전투 로직의 메인 핸들러 분류다.
    /// </summary>
    public enum EnemyBossCombatStyle
    {
        None,
        Beast,
        Tanker,
        Runner,
        Artillery
    }

    /// <summary>
    /// 보스 궁극기 로직의 분류다.
    /// </summary>
    public enum EnemyBossUltimateStyle
    {
        None,
        BeastCharge,
        TankerRocketRain,
        RunnerRiftSummon,
        ArtillerySweep
    }

    /// <summary>
    /// 적 사운드 큐의 런타임 이벤트 종류다.
    /// </summary>
    public enum EnemySoundCueType
    {
        Spawn,
        Attack,
        Special,
        Ultimate,
        Death
    }

    /// <summary>
    /// 적 AI의 이동, 감지, 전투, 액션 튜닝을 담는 에셋 프로필이다.
    /// </summary>
    public sealed class EnemyAiProfile
    {
        public EnemyAiProfile(
            EnemyBehaviorPattern movementPattern,
            EnemyWanderStyle wanderStyle = EnemyWanderStyle.Standard,
            EnemyRangedAttackStyle rangedAttackStyle = EnemyRangedAttackStyle.None,
            EnemyBossCombatStyle bossCombatStyle = EnemyBossCombatStyle.None,
            EnemyBossUltimateStyle bossUltimateStyle = EnemyBossUltimateStyle.None)
        {
            MovementPattern = movementPattern;
            WanderStyle = wanderStyle;
            RangedAttackStyle = rangedAttackStyle;
            BossCombatStyle = bossCombatStyle;
            BossUltimateStyle = bossUltimateStyle;

            bool meleeUnit = rangedAttackStyle == EnemyRangedAttackStyle.None &&
                bossCombatStyle != EnemyBossCombatStyle.Tanker &&
                bossCombatStyle != EnemyBossCombatStyle.Artillery;

            SightRange = bossCombatStyle == EnemyBossCombatStyle.None ? 11f : 13.5f;
            HearingRange = bossCombatStyle == EnemyBossCombatStyle.None ? 10.5f : 14f;
            PatrolSightDot = 0.18f;
            AlertSightDot = -0.28f;
            MemoryDuration = 3.2f;
            InvestigateDuration = 2.6f;
            SearchDuration = 3.4f;
            SearchRetargetInterval = 0.8f;
            SearchRadius = bossCombatStyle == EnemyBossCombatStyle.None ? 2.1f : 2.8f;
            SearchSpreadDegrees = 70f;
            DirectionalSearchDistance = bossCombatStyle == EnemyBossCombatStyle.None ? 1.8f : 2.5f;
            PredictionLeadSeconds = rangedAttackStyle == EnemyRangedAttackStyle.None ? 0.16f : 0.32f;
            PreferredCombatDistance = meleeUnit ? 1.05f : 5.8f;
            PreferredDistanceTolerance = meleeUnit ? 0.4f : 1.15f;
            AttackWindupDuration = meleeUnit ? 0.1f : 0.18f;
            AttackRecoverDuration = meleeUnit ? 0.18f : 0.22f;
            SpecialWindupDuration = 0.32f;
            SpecialRecoverDuration = 0.4f;
            UltimateWindupDuration = 0.7f;
            UltimateRecoverDuration = 0.55f;
            ChaseSpeedScale = meleeUnit ? 1.02f : 0.92f;
            InvestigateSpeedScale = 0.86f;
            SearchSpeedScale = 0.72f;
            StrafeSpeedScale = rangedAttackStyle == EnemyRangedAttackStyle.None ? 0.55f : 0.88f;
            TurnResponse = bossCombatStyle == EnemyBossCombatStyle.None ? 6.4f : 8.6f;
            DirectionInfluence = 0.7f;
            PreferredAttackFacingDot = 0.42f;
            UltimateHealthThreshold = 0.5f;
            SpecialMinDistance = meleeUnit ? 1.6f : 3.6f;
            SpecialMaxDistance = meleeUnit ? 5.8f : 8.6f;

            PatrolIdleChance = bossCombatStyle == EnemyBossCombatStyle.None ? 0.14f : 0.08f;
            PatrolIdleDurationMin = 0.2f;
            PatrolIdleDurationMax = 0.7f;
            PatrolMoveDurationMin = 1.2f;
            PatrolMoveDurationMax = 2.8f;

            MeleeDamageMultiplier = 1f;
            SpecialDamageMultiplier = 1.15f;
            UltimateDamageMultiplier = 1.45f;
            SpecialCooldownDuration = 1.8f;
            UltimateCooldownDuration = 26f;
            ProjectileSpeed = 9.4f;
            ProjectileLifetime = 3.8f;
            ProjectileSpreadOffset = 0.18f;
            ExplosionRadius = 1f;
            ChargeDuration = 0.35f;
            ChargeSpeedMultiplier = 2.8f;
            ChargeDamageMultiplier = 1.35f;
            BeamDuration = 0.28f;
            BeamWidth = 0.18f;
            BeamTickInterval = 0.18f;
            BeamWarmupDuration = 0.2f;
            BeamRotateSpeedDegrees = 88f;
            UltimateDuration = 5f;
            UltimateWidth = 0.34f;
            SummonOffsetDistance = 1.4f;
        }

        public EnemyBehaviorPattern MovementPattern { get; set; }

        public EnemyWanderStyle WanderStyle { get; set; }

        public EnemyRangedAttackStyle RangedAttackStyle { get; set; }

        public EnemyBossCombatStyle BossCombatStyle { get; set; }

        public EnemyBossUltimateStyle BossUltimateStyle { get; set; }

        public float SightRange { get; set; }

        public float HearingRange { get; set; }

        public float PatrolSightDot { get; set; }

        public float AlertSightDot { get; set; }

        public float MemoryDuration { get; set; }

        public float InvestigateDuration { get; set; }

        public float SearchDuration { get; set; }

        public float SearchRetargetInterval { get; set; }

        public float SearchRadius { get; set; }

        public float SearchSpreadDegrees { get; set; }

        public float DirectionalSearchDistance { get; set; }

        public float PredictionLeadSeconds { get; set; }

        public float PreferredCombatDistance { get; set; }

        public float PreferredDistanceTolerance { get; set; }

        public float AttackWindupDuration { get; set; }

        public float AttackRecoverDuration { get; set; }

        public float SpecialWindupDuration { get; set; }

        public float SpecialRecoverDuration { get; set; }

        public float UltimateWindupDuration { get; set; }

        public float UltimateRecoverDuration { get; set; }

        public float ChaseSpeedScale { get; set; }

        public float InvestigateSpeedScale { get; set; }

        public float SearchSpeedScale { get; set; }

        public float StrafeSpeedScale { get; set; }

        public float TurnResponse { get; set; }

        public float DirectionInfluence { get; set; }

        public float PreferredAttackFacingDot { get; set; }

        public float UltimateHealthThreshold { get; set; }

        public float SpecialMinDistance { get; set; }

        public float SpecialMaxDistance { get; set; }

        public float PatrolIdleChance { get; set; }

        public float PatrolIdleDurationMin { get; set; }

        public float PatrolIdleDurationMax { get; set; }

        public float PatrolMoveDurationMin { get; set; }

        public float PatrolMoveDurationMax { get; set; }

        public float MeleeDamageMultiplier { get; set; }

        public float SpecialDamageMultiplier { get; set; }

        public float UltimateDamageMultiplier { get; set; }

        public float SpecialCooldownDuration { get; set; }

        public float UltimateCooldownDuration { get; set; }

        public float ProjectileSpeed { get; set; }

        public float ProjectileLifetime { get; set; }

        public float ProjectileSpreadOffset { get; set; }

        public float ExplosionRadius { get; set; }

        public float ChargeDuration { get; set; }

        public float ChargeSpeedMultiplier { get; set; }

        public float ChargeDamageMultiplier { get; set; }

        public float BeamDuration { get; set; }

        public float BeamWidth { get; set; }

        public float BeamTickInterval { get; set; }

        public float BeamWarmupDuration { get; set; }

        public float BeamRotateSpeedDegrees { get; set; }

        public float UltimateDuration { get; set; }

        public float UltimateWidth { get; set; }

        public float SummonOffsetDistance { get; set; }

        public float AttackWindowBonus => GetAttackWindowBonus(MovementPattern);

        public EnemyAiProfile Clone()
        {
            return new EnemyAiProfile(MovementPattern, WanderStyle, RangedAttackStyle, BossCombatStyle, BossUltimateStyle)
            {
                SightRange = SightRange,
                HearingRange = HearingRange,
                PatrolSightDot = PatrolSightDot,
                AlertSightDot = AlertSightDot,
                MemoryDuration = MemoryDuration,
                InvestigateDuration = InvestigateDuration,
                SearchDuration = SearchDuration,
                SearchRetargetInterval = SearchRetargetInterval,
                SearchRadius = SearchRadius,
                SearchSpreadDegrees = SearchSpreadDegrees,
                DirectionalSearchDistance = DirectionalSearchDistance,
                PredictionLeadSeconds = PredictionLeadSeconds,
                PreferredCombatDistance = PreferredCombatDistance,
                PreferredDistanceTolerance = PreferredDistanceTolerance,
                AttackWindupDuration = AttackWindupDuration,
                AttackRecoverDuration = AttackRecoverDuration,
                SpecialWindupDuration = SpecialWindupDuration,
                SpecialRecoverDuration = SpecialRecoverDuration,
                UltimateWindupDuration = UltimateWindupDuration,
                UltimateRecoverDuration = UltimateRecoverDuration,
                ChaseSpeedScale = ChaseSpeedScale,
                InvestigateSpeedScale = InvestigateSpeedScale,
                SearchSpeedScale = SearchSpeedScale,
                StrafeSpeedScale = StrafeSpeedScale,
                TurnResponse = TurnResponse,
                DirectionInfluence = DirectionInfluence,
                PreferredAttackFacingDot = PreferredAttackFacingDot,
                UltimateHealthThreshold = UltimateHealthThreshold,
                SpecialMinDistance = SpecialMinDistance,
                SpecialMaxDistance = SpecialMaxDistance,
                PatrolIdleChance = PatrolIdleChance,
                PatrolIdleDurationMin = PatrolIdleDurationMin,
                PatrolIdleDurationMax = PatrolIdleDurationMax,
                PatrolMoveDurationMin = PatrolMoveDurationMin,
                PatrolMoveDurationMax = PatrolMoveDurationMax,
                MeleeDamageMultiplier = MeleeDamageMultiplier,
                SpecialDamageMultiplier = SpecialDamageMultiplier,
                UltimateDamageMultiplier = UltimateDamageMultiplier,
                SpecialCooldownDuration = SpecialCooldownDuration,
                UltimateCooldownDuration = UltimateCooldownDuration,
                ProjectileSpeed = ProjectileSpeed,
                ProjectileLifetime = ProjectileLifetime,
                ProjectileSpreadOffset = ProjectileSpreadOffset,
                ExplosionRadius = ExplosionRadius,
                ChargeDuration = ChargeDuration,
                ChargeSpeedMultiplier = ChargeSpeedMultiplier,
                ChargeDamageMultiplier = ChargeDamageMultiplier,
                BeamDuration = BeamDuration,
                BeamWidth = BeamWidth,
                BeamTickInterval = BeamTickInterval,
                BeamWarmupDuration = BeamWarmupDuration,
                BeamRotateSpeedDegrees = BeamRotateSpeedDegrees,
                UltimateDuration = UltimateDuration,
                UltimateWidth = UltimateWidth,
                SummonOffsetDistance = SummonOffsetDistance
            };
        }

        public EnemyAiProfile WithMovementPattern(EnemyBehaviorPattern movementPattern)
        {
            EnemyAiProfile clone = Clone();
            clone.MovementPattern = movementPattern;
            return clone;
        }

        private static float GetAttackWindowBonus(EnemyBehaviorPattern movementPattern)
        {
            switch (movementPattern)
            {
                case EnemyBehaviorPattern.Juggernaut:
                case EnemyBehaviorPattern.BlindCharger:
                    return 0.08f;
                case EnemyBehaviorPattern.Rushdown:
                case EnemyBehaviorPattern.Pouncer:
                case EnemyBehaviorPattern.BloodPhantom:
                case EnemyBehaviorPattern.HellionSwarm:
                    return 0.12f;
                case EnemyBehaviorPattern.BossTanker:
                case EnemyBehaviorPattern.BossBehemoth:
                case EnemyBehaviorPattern.BossAnnihilator:
                case EnemyBehaviorPattern.BossSpiderQueen:
                    return 0.24f;
                case EnemyBehaviorPattern.BossBeast:
                case EnemyBehaviorPattern.BossFlameLord:
                case EnemyBehaviorPattern.BossDarkLord:
                    return 0.18f;
                case EnemyBehaviorPattern.BossRunner:
                case EnemyBehaviorPattern.BossRiftBlitz:
                case EnemyBehaviorPattern.BossAgathoDemon:
                case EnemyBehaviorPattern.BossArachnoFang:
                    return 0.04f;
                default:
                    return 0f;
            }
        }
    }

    /// <summary>
    /// 적 사운드 한 개의 로딩 정보와 폴백 별칭을 담는다.
    /// </summary>
    public sealed class EnemySoundCue
    {
        public EnemySoundCue(
            string alias,
            string fallbackAlias = null)
        {
            Alias = alias ?? string.Empty;
            FallbackAlias = fallbackAlias ?? string.Empty;
        }

        public string Alias { get; }

        public string FallbackAlias { get; }

        public static EnemySoundCue FromAlias(string alias, string fallbackAlias = null)
            => new EnemySoundCue(alias, fallbackAlias);
    }

    /// <summary>
    /// 적의 스폰, 공격, 사망 등 이벤트별 사운드 매핑이다.
    /// </summary>
    public sealed class EnemySoundProfile
    {
        public static readonly EnemySoundProfile Silent = new EnemySoundProfile();

        public EnemySoundProfile(
            EnemySoundCue spawn = null,
            EnemySoundCue attack = null,
            EnemySoundCue special = null,
            EnemySoundCue ultimate = null,
            EnemySoundCue death = null)
        {
            Spawn = spawn;
            Attack = attack;
            Special = special;
            Ultimate = ultimate;
            Death = death;
        }

        public EnemySoundCue Spawn { get; }

        public EnemySoundCue Attack { get; }

        public EnemySoundCue Special { get; }

        public EnemySoundCue Ultimate { get; }

        public EnemySoundCue Death { get; }

        public EnemySoundCue GetCue(EnemySoundCueType cueType)
        {
            switch (cueType)
            {
                case EnemySoundCueType.Spawn:
                    return Spawn;
                case EnemySoundCueType.Attack:
                    return Attack;
                case EnemySoundCueType.Special:
                    return Special;
                case EnemySoundCueType.Ultimate:
                    return Ultimate;
                case EnemySoundCueType.Death:
                    return Death;
                default:
                    return null;
            }
        }
    }
}
