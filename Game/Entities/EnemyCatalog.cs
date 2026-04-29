using System;
using System.Collections.Generic;
using My2DEngine.Game.Config;

namespace My2DEngine.Game
{
    /// <summary>
    /// 적 하나를 구성하는 런타임 기준 아키타입 데이터다.
    /// 스탯, 표시 이름, 스프라이트, AI, 사운드를 한곳에서 관리한다.
    /// </summary>
    public sealed class EnemyArchetype
    {
        private readonly EnemyBehaviorPattern[] behaviorPatternPool;
        private readonly string[] spriteVariantKeyPool;

        public EnemyArchetype(
            string assetId,
            string displayName,
            string roomLabel,
            string spriteVariantKey,
            EnemyDefinition definition,
            EnemyRank rank = EnemyRank.Normal,
            EnemyAiProfile aiProfile = null,
            EnemySoundProfile soundProfile = null,
            EnemyBehaviorPattern[] behaviorPatternPool = null,
            string[] spriteVariantKeyPool = null)
        {
            AssetId = assetId ?? string.Empty;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? roomLabel : displayName;
            RoomLabel = string.IsNullOrWhiteSpace(roomLabel) ? DisplayName : roomLabel;
            SpriteVariantKey = string.IsNullOrWhiteSpace(spriteVariantKey) ? assetId : spriteVariantKey;
            Definition = definition?.Clone() ?? new EnemyDefinition();
            Rank = rank;
            AiProfile = aiProfile ?? new EnemyAiProfile(EnemyBehaviorPattern.Default);
            SoundProfile = soundProfile ?? EnemySoundProfile.Silent;

            if (behaviorPatternPool == null || behaviorPatternPool.Length == 0)
            {
                this.behaviorPatternPool = Array.Empty<EnemyBehaviorPattern>();
            }
            else
            {
                this.behaviorPatternPool = new EnemyBehaviorPattern[behaviorPatternPool.Length];
                Array.Copy(behaviorPatternPool, this.behaviorPatternPool, behaviorPatternPool.Length);
            }

            if (spriteVariantKeyPool == null || spriteVariantKeyPool.Length == 0)
            {
                this.spriteVariantKeyPool = Array.Empty<string>();
            }
            else
            {
                this.spriteVariantKeyPool = new string[spriteVariantKeyPool.Length];
                Array.Copy(spriteVariantKeyPool, this.spriteVariantKeyPool, spriteVariantKeyPool.Length);
            }
        }

        public string AssetId { get; }
        public string DisplayName { get; }
        public string RoomLabel { get; }
        public string SpriteVariantKey { get; }
        public EnemyDefinition Definition { get; }
        public EnemyRank Rank { get; }
        public EnemyAiProfile AiProfile { get; }
        public EnemySoundProfile SoundProfile { get; }
        public EnemyBehaviorPattern DefaultBehaviorPattern => AiProfile.MovementPattern;
        public EnemyBehaviorPattern[] BehaviorPatternPool => behaviorPatternPool;
        public string[] SpriteVariantKeyPool => spriteVariantKeyPool;

        public EnemyDefinition CreateDefinition() => Definition.Clone();

        public EnemyAiProfile CreateAiProfile(EnemyBehaviorPattern movementPattern)
            => AiProfile.WithMovementPattern(movementPattern);
    }

    /// <summary>
    /// 게임 내 모든 적/보스 에셋 카탈로그다.
    /// 일반 적 3종 + 신규 적 3종, 주력 보스 4종 + 신규 보스 7종을 정의한다.
    /// </summary>
    public static class EnemyCatalog
    {
        // ── 일반 적 ──────────────────────────────────────────────────────────

        private static readonly EnemyArchetype GunnerEnemy = new EnemyArchetype(
            "gunner",
            "Zombie Scientist",
            "Zombie Scientist",
            "zombie_scientist_pack",
            new EnemyDefinition
            {
                Type = EnemyType.Gunner,
                Scale = 0.82f,
                MaxHealth = 60f,
                MoveSpeed = 1.05f,
                AttackRange = 7.6f,
                AttackDamage = 10f,
                AttackCooldownDuration = 0.68f,
                Radius = 0.22f
            },
            EnemyRank.Normal,
            BuildGunnerProfile(),
            EnemySoundProfile.Silent,
            new[]
            {
                EnemyBehaviorPattern.Kite,
                EnemyBehaviorPattern.Strafe,
                EnemyBehaviorPattern.Default
            },
            new[]
            {
                "zombie_scientist_pack",
                "zombie_scientist_plasma",
                "zombie_scientist_freeze",
                "zombie_scientist_uzi",
                "zombie_scientist_halon",
                "zombie_scientist_axe",
                "zombie_scientist_cleaver",
                "zombie_scientist_crowbar",
                "zombie_scientist_hammer",
                "zombie_scientist_knife",
                "zombie_scientist_syringe",
                "zombie_scientist_wrench",
                "zombie_scientist_grenade"
            });

        private static readonly EnemyArchetype EliteEnemy = new EnemyArchetype(
            "elite",
            "Blind Pinky",
            "Blind Pinky",
            "blind_pinky",
            new EnemyDefinition
            {
                Type = EnemyType.Elite,
                Scale = 1.18f,
                MaxHealth = 120f,
                MoveSpeed = 0.92f,
                AttackRange = 1.02f,
                AttackDamage = 18f,
                AttackCooldownDuration = 1f,
                Radius = 0.34f
            },
            EnemyRank.Normal,
            BuildEliteProfile(),
            EnemySoundProfile.Silent,
            new[]
            {
                EnemyBehaviorPattern.Juggernaut,
                EnemyBehaviorPattern.Default,
                EnemyBehaviorPattern.Rushdown
            });

        private static readonly EnemyArchetype RuinedGunnerEnemy = new EnemyArchetype(
            "ruined_gunner",
            "Slime Imp",
            "Slime Imp",
            "slime_imp",
            new EnemyDefinition
            {
                Type = EnemyType.RuinedGunner,
                Scale = 0.82f,
                MaxHealth = 72f,
                MoveSpeed = 1f,
                AttackRange = 6.4f,
                AttackDamage = 8f,
                AttackCooldownDuration = 1.25f,
                Radius = 0.22f
            },
            EnemyRank.Normal,
            BuildRuinedGunnerProfile(),
            EnemySoundProfile.Silent,
            new[]
            {
                EnemyBehaviorPattern.Skirmisher,
                EnemyBehaviorPattern.Pouncer,
                EnemyBehaviorPattern.Rushdown
            });

        // ── 신규 일반 적 ─────────────────────────────────────────────────────

        private static readonly EnemyArchetype BloodGhostEnemy = new EnemyArchetype(
            "blood_ghost",
            "Blood Ghost",
            "Blood Ghost",
            "blood_ghost",
            new EnemyDefinition
            {
                Type = EnemyType.BloodGhost,
                Scale = 1.1f,
                MaxHealth = 100f,
                MoveSpeed = 1.3f,
                AttackRange = 1.1f,
                AttackDamage = 16f,
                AttackCooldownDuration = 0.85f,
                Radius = 0.30f
            },
            EnemyRank.Normal,
            BuildBloodGhostProfile(),
            EnemySoundProfile.Silent,
            new[]
            {
                EnemyBehaviorPattern.Juggernaut,
                EnemyBehaviorPattern.Rushdown
            });

        private static readonly EnemyArchetype BeamRevenantEnemy = new EnemyArchetype(
            "beam_revenant",
            "Beam Revenant",
            "Revenant",
            "beam_revenant",
            new EnemyDefinition
            {
                Type = EnemyType.BeamRevenant,
                Scale = 0.9f,
                MaxHealth = 80f,
                MoveSpeed = 0.95f,
                AttackRange = 9f,
                AttackDamage = 12f,
                AttackCooldownDuration = 1.1f,
                Radius = 0.24f
            },
            EnemyRank.Normal,
            BuildBeamRevenantProfile(),
            EnemySoundProfile.Silent,
            new[]
            {
                EnemyBehaviorPattern.Kite,
                EnemyBehaviorPattern.Strafe
            });

        private static readonly EnemyArchetype HellionEnemy = new EnemyArchetype(
            "hellion",
            "Hellion",
            "Hellion",
            "hellion",
            new EnemyDefinition
            {
                Type = EnemyType.Hellion,
                Scale = 0.78f,
                MaxHealth = 55f,
                MoveSpeed = 1.4f,
                AttackRange = 5.5f,
                AttackDamage = 7f,
                AttackCooldownDuration = 1.0f,
                Radius = 0.20f
            },
            EnemyRank.Normal,
            BuildHellionProfile(),
            EnemySoundProfile.Silent,
            new[]
            {
                EnemyBehaviorPattern.Pouncer,
                EnemyBehaviorPattern.Skirmisher
            });

        // ── 주력 보스 (폴더 기반) ─────────────────────────────────────────────

        private static readonly EnemyArchetype FeralAlpha = new EnemyArchetype(
            "feral_alpha",
            "Azazel",
            "Azazel",
            "azazel",
            new EnemyDefinition
            {
                Type = EnemyType.Gunner,
                Scale = 1.23f,
                MaxHealth = 210f,
                MoveSpeed = 1.26f,
                AttackRange = 1.18f,
                AttackDamage = 22f,
                AttackCooldownDuration = 0.74f,
                Radius = 0.297f
            },
            EnemyRank.Boss,
            BuildFeralAlphaProfile(),
            EnemySoundProfile.Silent);

        private static readonly EnemyArchetype BulwarkColossus = new EnemyArchetype(
            "bulwark_colossus",
            "Behemoth",
            "Behemoth",
            "behemoth",
            new EnemyDefinition
            {
                Type = EnemyType.Elite,
                Scale = 2.124f,
                MaxHealth = 600f,
                MoveSpeed = 0.82f,
                AttackRange = 1.2f,
                AttackDamage = 27f,
                AttackCooldownDuration = 1f,
                Radius = 0.5508f
            },
            EnemyRank.Boss,
            BuildBulwarkColossusProfile(),
            EnemySoundProfile.Silent);

        private static readonly EnemyArchetype AshenArtillerist = new EnemyArchetype(
            "ashen_artillerist",
            "Arachnocortex",
            "Arachnocortex",
            "arachnocortex",
            new EnemyDefinition
            {
                Type = EnemyType.Gunner,
                Scale = 1.066f,
                MaxHealth = 240f,
                MoveSpeed = 0.945f,
                AttackRange = 8.1f,
                AttackDamage = 20f,
                AttackCooldownDuration = 0.8f,
                Radius = 0.2574f
            },
            EnemyRank.Boss,
            BuildAshenArtilleristProfile(),
            EnemySoundProfile.Silent);

        private static readonly EnemyArchetype RiftStrider = new EnemyArchetype(
            "rift_strider",
            "Agaures",
            "Agaures",
            "agaures",
            new EnemyDefinition
            {
                Type = EnemyType.Gunner,
                Scale = 0.984f,
                MaxHealth = 180f,
                MoveSpeed = 1.47f,
                AttackRange = 1.1f,
                AttackDamage = 16f,
                AttackCooldownDuration = 0.66f,
                Radius = 0.2376f
            },
            EnemyRank.Boss,
            BuildRiftStriderProfile(),
            EnemySoundProfile.Silent);

        // ── 신규 보스 (ZIP 기반) ─────────────────────────────────────────────

        private static readonly EnemyArchetype Abaddon = new EnemyArchetype(
            "abaddon",
            "Abaddon",
            "Abaddon",
            "abaddon",
            new EnemyDefinition
            {
                Type = EnemyType.Gunner,
                Scale = 1.32f,
                MaxHealth = 250f,
                MoveSpeed = 1.18f,
                AttackRange = 1.22f,
                AttackDamage = 24f,
                AttackCooldownDuration = 0.78f,
                Radius = 0.31f
            },
            EnemyRank.Boss,
            BuildAbaddonProfile(),
            EnemySoundProfile.Silent);

        private static readonly EnemyArchetype Afrit = new EnemyArchetype(
            "afrit",
            "Afrit",
            "Afrit",
            "afrit",
            new EnemyDefinition
            {
                Type = EnemyType.Gunner,
                Scale = 1.1f,
                MaxHealth = 260f,
                MoveSpeed = 0.98f,
                AttackRange = 8.6f,
                AttackDamage = 22f,
                AttackCooldownDuration = 0.82f,
                Radius = 0.266f
            },
            EnemyRank.Boss,
            BuildAfritProfile(),
            EnemySoundProfile.Silent);

        private static readonly EnemyArchetype AgathoDemon = new EnemyArchetype(
            "agatho_demon",
            "Agatho Demon",
            "Agatho Demon",
            "agatho_demon",
            new EnemyDefinition
            {
                Type = EnemyType.Gunner,
                Scale = 1.0f,
                MaxHealth = 190f,
                MoveSpeed = 1.52f,
                AttackRange = 1.12f,
                AttackDamage = 17f,
                AttackCooldownDuration = 0.64f,
                Radius = 0.242f
            },
            EnemyRank.Boss,
            BuildAgathoDemonProfile(),
            EnemySoundProfile.Silent);

        private static readonly EnemyArchetype Annihilator = new EnemyArchetype(
            "annihilator",
            "Annihilator",
            "Annihilator",
            "annihilator",
            new EnemyDefinition
            {
                Type = EnemyType.Elite,
                Scale = 2.0f,
                MaxHealth = 680f,
                MoveSpeed = 0.78f,
                AttackRange = 1.25f,
                AttackDamage = 30f,
                AttackCooldownDuration = 1.05f,
                Radius = 0.52f
            },
            EnemyRank.Boss,
            BuildAnnihilatorProfile(),
            EnemySoundProfile.Silent);

        private static readonly EnemyArchetype Arachnobaron = new EnemyArchetype(
            "arachnobaron",
            "Arachnobaron",
            "Arachnobaron",
            "arachnobaron",
            new EnemyDefinition
            {
                Type = EnemyType.Gunner,
                Scale = 1.14f,
                MaxHealth = 280f,
                MoveSpeed = 0.92f,
                AttackRange = 8.8f,
                AttackDamage = 21f,
                AttackCooldownDuration = 0.85f,
                Radius = 0.275f
            },
            EnemyRank.Boss,
            BuildArachnobaronProfile(),
            EnemySoundProfile.Silent);

        private static readonly EnemyArchetype Arachnophyte = new EnemyArchetype(
            "arachnophyte",
            "Arachnophyte",
            "Arachnophyte",
            "arachnophyte",
            new EnemyDefinition
            {
                Type = EnemyType.Gunner,
                Scale = 0.96f,
                MaxHealth = 200f,
                MoveSpeed = 1.44f,
                AttackRange = 1.15f,
                AttackDamage = 15f,
                AttackCooldownDuration = 0.62f,
                Radius = 0.232f
            },
            EnemyRank.Boss,
            BuildArachnophyteProfile(),
            EnemySoundProfile.Silent);

        private static readonly EnemyArchetype AracnorbQueen = new EnemyArchetype(
            "aracnorb_queen",
            "Aracnorb Queen",
            "Aracnorb Queen",
            "aracnorb_queen",
            new EnemyDefinition
            {
                Type = EnemyType.Elite,
                Scale = 2.2f,
                MaxHealth = 720f,
                MoveSpeed = 0.76f,
                AttackRange = 1.3f,
                AttackDamage = 32f,
                AttackCooldownDuration = 1.1f,
                Radius = 0.572f
            },
            EnemyRank.Boss,
            BuildAracnorbQueenProfile(),
            EnemySoundProfile.Silent);

        // ── 카탈로그 인덱스 ───────────────────────────────────────────────────

        private static readonly EnemyArchetype[] s_archetypes =
        {
            GunnerEnemy,
            EliteEnemy,
            RuinedGunnerEnemy,
            BloodGhostEnemy,
            BeamRevenantEnemy,
            HellionEnemy,
            FeralAlpha,
            BulwarkColossus,
            AshenArtillerist,
            RiftStrider,
            Abaddon,
            Afrit,
            AgathoDemon,
            Annihilator,
            Arachnobaron,
            Arachnophyte,
            AracnorbQueen
        };

        private static readonly Dictionary<string, EnemyArchetype> s_byAssetId =
            new Dictionary<string, EnemyArchetype>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<EnemyType, EnemyArchetype> s_defaultByType =
            new Dictionary<EnemyType, EnemyArchetype>
            {
                { EnemyType.Gunner,       GunnerEnemy      },
                { EnemyType.Elite,        EliteEnemy       },
                { EnemyType.RuinedGunner, RuinedGunnerEnemy },
                { EnemyType.BloodGhost,   BloodGhostEnemy  },
                { EnemyType.BeamRevenant, BeamRevenantEnemy },
                { EnemyType.Hellion,      HellionEnemy     },
            };

        static EnemyCatalog()
        {
            Register(GunnerEnemy, "zombie_scientist", "zombie_scientist_pack");
            Register(EliteEnemy, "blind_pinky");
            Register(RuinedGunnerEnemy, "slime_imp");
            Register(BloodGhostEnemy);
            Register(BeamRevenantEnemy);
            Register(HellionEnemy);
            Register(FeralAlpha, "boss_beast", "azazel");
            Register(BulwarkColossus, "boss_tanker", "behemoth");
            Register(AshenArtillerist, "boss_artillery", "arachnocortex");
            Register(RiftStrider, "boss_runner", "agaures");
            Register(Abaddon);
            Register(Afrit);
            Register(AgathoDemon);
            Register(Annihilator);
            Register(Arachnobaron);
            Register(Arachnophyte);
            Register(AracnorbQueen);
        }

        // ── 공개 API ─────────────────────────────────────────────────────────

        public static EnemyArchetype[] GetAllArchetypes()
        {
            EnemyArchetype[] copy = new EnemyArchetype[s_archetypes.Length];
            Array.Copy(s_archetypes, copy, s_archetypes.Length);
            return copy;
        }

        public static EnemyArchetype Resolve(string assetId, EnemyType fallbackType)
        {
            if (!string.IsNullOrWhiteSpace(assetId) &&
                s_byAssetId.TryGetValue(assetId, out EnemyArchetype archetype))
            {
                return archetype;
            }

            return GetDefaultForType(fallbackType);
        }

        public static EnemyArchetype Get(string assetId)
        {
            if (string.IsNullOrWhiteSpace(assetId))
                throw new ArgumentException("Enemy asset id is required.", nameof(assetId));

            if (s_byAssetId.TryGetValue(assetId, out EnemyArchetype archetype))
                return archetype;

            throw new ArgumentException("Unknown enemy asset id: " + assetId, nameof(assetId));
        }

        public static EnemyArchetype GetDefaultForType(EnemyType type)
        {
            if (s_defaultByType.TryGetValue(type, out EnemyArchetype archetype))
                return archetype;

            return GunnerEnemy;
        }

        // ── 일반 적 AI 프로필 ──────────────────────────────────────────────────

        private static EnemyAiProfile BuildGunnerProfile()
        {
            return new EnemyAiProfile(
                EnemyBehaviorPattern.ZombieGunner,
                EnemyWanderStyle.Standard,
                EnemyRangedAttackStyle.SingleShot)
            {
                SightRange = 11.5f,
                HearingRange = 11f,
                MemoryDuration = 3.4f,
                PreferredCombatDistance = 6.1f,
                PreferredDistanceTolerance = 1f,
                AttackWindupDuration = 0.14f,
                AttackRecoverDuration = 0.2f,
                ChaseSpeedScale = 0.9f,
                InvestigateSpeedScale = 0.9f,
                SearchSpeedScale = 0.78f,
                StrafeSpeedScale = 0.9f,
                TurnResponse = 7f,
                PredictionLeadSeconds = 0.32f,
                DirectionalSearchDistance = 1.9f,
                PatrolMoveDurationMin = 1.4f,
                PatrolMoveDurationMax = 3f,
                PatrolIdleChance = 0.12f
            };
        }

        private static EnemyAiProfile BuildEliteProfile()
        {
            return new EnemyAiProfile(
                EnemyBehaviorPattern.BlindCharger,
                EnemyWanderStyle.Standard)
            {
                SightRange = 9.8f,
                HearingRange = 12.2f,
                PatrolSightDot = 0.28f,
                AlertSightDot = -0.42f,
                MemoryDuration = 4f,
                SearchDuration = 4.2f,
                SearchRadius = 2.4f,
                SearchSpreadDegrees = 90f,
                DirectionalSearchDistance = 2.4f,
                PreferredCombatDistance = 1.15f,
                PreferredDistanceTolerance = 0.28f,
                AttackWindupDuration = 0.18f,
                AttackRecoverDuration = 0.28f,
                ChaseSpeedScale = 1.08f,
                InvestigateSpeedScale = 0.94f,
                SearchSpeedScale = 0.82f,
                TurnResponse = 8.5f,
                DirectionInfluence = 0.55f,
                PreferredAttackFacingDot = 0.25f,
                PatrolIdleChance = 0.1f,
                PatrolMoveDurationMin = 1.6f,
                PatrolMoveDurationMax = 3.2f
            };
        }

        private static EnemyAiProfile BuildRuinedGunnerProfile()
        {
            return new EnemyAiProfile(
                EnemyBehaviorPattern.SlimeLobber,
                EnemyWanderStyle.Standard,
                EnemyRangedAttackStyle.SpreadTriplet)
            {
                SightRange = 10.6f,
                HearingRange = 10.8f,
                MemoryDuration = 3f,
                SearchDuration = 3.2f,
                DirectionalSearchDistance = 1.7f,
                PreferredCombatDistance = 4.9f,
                PreferredDistanceTolerance = 1.3f,
                AttackWindupDuration = 0.16f,
                AttackRecoverDuration = 0.3f,
                ChaseSpeedScale = 1.02f,
                InvestigateSpeedScale = 0.88f,
                SearchSpeedScale = 0.75f,
                StrafeSpeedScale = 0.95f,
                PredictionLeadSeconds = 0.28f,
                ProjectileSpreadOffset = 0.18f,
                PatrolIdleChance = 0.14f,
                PatrolMoveDurationMin = 1.25f,
                PatrolMoveDurationMax = 2.6f
            };
        }

        // ── 신규 일반 적 AI 프로필 ────────────────────────────────────────────

        private static EnemyAiProfile BuildBloodGhostProfile()
        {
            // 유령형: 시야 제한 없이 소리에 민감하고 빠르게 돌진
            return new EnemyAiProfile(
                EnemyBehaviorPattern.BloodPhantom,
                EnemyWanderStyle.Standard)
            {
                SightRange = 8.5f,
                HearingRange = 14f,
                PatrolSightDot = -0.5f,
                AlertSightDot = -0.7f,
                MemoryDuration = 5f,
                SearchDuration = 4.8f,
                SearchRadius = 2.8f,
                SearchSpreadDegrees = 120f,
                DirectionalSearchDistance = 2.6f,
                PreferredCombatDistance = 1.0f,
                PreferredDistanceTolerance = 0.22f,
                AttackWindupDuration = 0.14f,
                AttackRecoverDuration = 0.24f,
                ChaseSpeedScale = 1.18f,
                InvestigateSpeedScale = 1.0f,
                SearchSpeedScale = 0.88f,
                TurnResponse = 9f,
                DirectionInfluence = 0.48f,
                PreferredAttackFacingDot = 0.2f,
                PatrolIdleChance = 0.06f,
                PatrolMoveDurationMin = 1.2f,
                PatrolMoveDurationMax = 2.8f
            };
        }

        private static EnemyAiProfile BuildBeamRevenantProfile()
        {
            // 빔 원거리 공격형: 사거리 길고 안전 거리 유지
            return new EnemyAiProfile(
                EnemyBehaviorPattern.BeamSniper,
                EnemyWanderStyle.Standard,
                EnemyRangedAttackStyle.SingleShot)
            {
                SightRange = 13f,
                HearingRange = 10f,
                MemoryDuration = 3.8f,
                PreferredCombatDistance = 7.5f,
                PreferredDistanceTolerance = 1.2f,
                AttackWindupDuration = 0.22f,
                AttackRecoverDuration = 0.35f,
                ChaseSpeedScale = 0.85f,
                InvestigateSpeedScale = 0.82f,
                SearchSpeedScale = 0.72f,
                StrafeSpeedScale = 0.88f,
                TurnResponse = 6.5f,
                PredictionLeadSeconds = 0.24f,
                DirectionalSearchDistance = 2.1f,
                PatrolMoveDurationMin = 1.5f,
                PatrolMoveDurationMax = 3.2f,
                PatrolIdleChance = 0.16f
            };
        }

        private static EnemyAiProfile BuildHellionProfile()
        {
            // 도약 기습형: 빠르고 예측 불가한 달려들기
            return new EnemyAiProfile(
                EnemyBehaviorPattern.HellionSwarm,
                EnemyWanderStyle.Standard,
                EnemyRangedAttackStyle.SpreadTriplet)
            {
                SightRange = 11f,
                HearingRange = 9.5f,
                MemoryDuration = 2.8f,
                SearchDuration = 3f,
                DirectionalSearchDistance = 1.6f,
                PreferredCombatDistance = 4.2f,
                PreferredDistanceTolerance = 1.4f,
                AttackWindupDuration = 0.12f,
                AttackRecoverDuration = 0.26f,
                ChaseSpeedScale = 1.15f,
                InvestigateSpeedScale = 0.95f,
                SearchSpeedScale = 0.82f,
                StrafeSpeedScale = 1.0f,
                PredictionLeadSeconds = 0.22f,
                ProjectileSpreadOffset = 0.22f,
                PatrolIdleChance = 0.10f,
                PatrolMoveDurationMin = 1.0f,
                PatrolMoveDurationMax = 2.2f
            };
        }

        // ── 주력 보스 AI 프로필 ───────────────────────────────────────────────

        private static EnemyAiProfile BuildFeralAlphaProfile()
        {
            return new EnemyAiProfile(
                EnemyBehaviorPattern.BossFlameLord,
                EnemyWanderStyle.BossSentinel,
                EnemyRangedAttackStyle.None,
                EnemyBossCombatStyle.Beast,
                EnemyBossUltimateStyle.BeastCharge)
            {
                SightRange = 13.8f,
                HearingRange = 16f,
                MemoryDuration = 4.8f,
                InvestigateDuration = 3f,
                SearchDuration = 4.5f,
                SearchRadius = 2.9f,
                SearchSpreadDegrees = 95f,
                DirectionalSearchDistance = 3f,
                PreferredCombatDistance = 1.55f,
                PreferredDistanceTolerance = 0.35f,
                AttackWindupDuration = 0.2f,
                AttackRecoverDuration = 0.24f,
                SpecialWindupDuration = 0.28f,
                SpecialRecoverDuration = 0.36f,
                UltimateWindupDuration = 0.72f,
                UltimateRecoverDuration = 0.52f,
                ChaseSpeedScale = 1.22f,
                InvestigateSpeedScale = 0.98f,
                SearchSpeedScale = 0.86f,
                TurnResponse = 10f,
                DirectionInfluence = 0.5f,
                PreferredAttackFacingDot = 0.2f,
                UltimateHealthThreshold = 0.58f,
                SpecialMinDistance = 2.1f,
                SpecialMaxDistance = 5.2f,
                PatrolIdleChance = 0.04f,
                PatrolMoveDurationMin = 1.5f,
                PatrolMoveDurationMax = 2.2f,
                MeleeDamageMultiplier = 1.12f,
                SpecialCooldownDuration = 2.1f,
                UltimateCooldownDuration = 26f,
                ChargeDuration = 0.48f,
                ChargeSpeedMultiplier = 3.35f,
                ChargeDamageMultiplier = 1.55f,
                UltimateDuration = 0.72f,
                UltimateDamageMultiplier = 2.45f
            };
        }

        private static EnemyAiProfile BuildBulwarkColossusProfile()
        {
            return new EnemyAiProfile(
                EnemyBehaviorPattern.BossBehemoth,
                EnemyWanderStyle.BossSentinel,
                EnemyRangedAttackStyle.None,
                EnemyBossCombatStyle.Tanker,
                EnemyBossUltimateStyle.TankerRocketRain)
            {
                SightRange = 14.5f,
                HearingRange = 16f,
                MemoryDuration = 4.4f,
                SearchDuration = 4.2f,
                SearchRadius = 3.1f,
                SearchSpreadDegrees = 80f,
                DirectionalSearchDistance = 2.8f,
                PreferredCombatDistance = 6.2f,
                PreferredDistanceTolerance = 1.05f,
                AttackWindupDuration = 0.18f,
                AttackRecoverDuration = 0.3f,
                SpecialWindupDuration = 0.34f,
                SpecialRecoverDuration = 0.46f,
                UltimateWindupDuration = 0.85f,
                UltimateRecoverDuration = 0.62f,
                ChaseSpeedScale = 0.86f,
                InvestigateSpeedScale = 0.76f,
                SearchSpeedScale = 0.7f,
                StrafeSpeedScale = 0.55f,
                TurnResponse = 7.2f,
                UltimateHealthThreshold = 0.55f,
                SpecialMinDistance = 3.4f,
                SpecialMaxDistance = 8.8f,
                PatrolIdleChance = 0.08f,
                PatrolIdleDurationMin = 0.18f,
                PatrolIdleDurationMax = 0.34f,
                PatrolMoveDurationMin = 1.2f,
                PatrolMoveDurationMax = 2f,
                MeleeDamageMultiplier = 1.1f,
                SpecialDamageMultiplier = 1.2f,
                UltimateDamageMultiplier = 1.08f,
                SpecialCooldownDuration = 2.8f,
                UltimateCooldownDuration = 28f,
                ProjectileSpeed = 6.1f,
                ProjectileLifetime = 4.2f,
                ExplosionRadius = 1.05f,
                UltimateDuration = 0.85f,
                UltimateWidth = 1.2f
            };
        }

        private static EnemyAiProfile BuildAshenArtilleristProfile()
        {
            return new EnemyAiProfile(
                EnemyBehaviorPattern.BossCortex,
                EnemyWanderStyle.BossArtillery,
                EnemyRangedAttackStyle.None,
                EnemyBossCombatStyle.Artillery,
                EnemyBossUltimateStyle.ArtillerySweep)
            {
                SightRange = 14.2f,
                HearingRange = 15.6f,
                MemoryDuration = 4.6f,
                SearchDuration = 4f,
                SearchRadius = 2.7f,
                SearchSpreadDegrees = 75f,
                DirectionalSearchDistance = 2.6f,
                PreferredCombatDistance = 6.7f,
                PreferredDistanceTolerance = 1.1f,
                AttackWindupDuration = 0.18f,
                AttackRecoverDuration = 0.22f,
                SpecialWindupDuration = 0.34f,
                SpecialRecoverDuration = 0.38f,
                UltimateWindupDuration = 0.7f,
                UltimateRecoverDuration = 0.58f,
                ChaseSpeedScale = 0.82f,
                InvestigateSpeedScale = 0.84f,
                SearchSpeedScale = 0.72f,
                StrafeSpeedScale = 0.86f,
                TurnResponse = 9.1f,
                PreferredAttackFacingDot = 0.35f,
                UltimateHealthThreshold = 0.52f,
                SpecialMinDistance = 4.5f,
                SpecialMaxDistance = 10f,
                PatrolIdleChance = 0.16f,
                PatrolIdleDurationMin = 0.14f,
                PatrolIdleDurationMax = 0.28f,
                PatrolMoveDurationMin = 1.1f,
                PatrolMoveDurationMax = 1.9f,
                SpecialDamageMultiplier = 0.92f,
                UltimateDamageMultiplier = 1.55f,
                SpecialCooldownDuration = 1.95f,
                UltimateCooldownDuration = 25f,
                BeamDuration = 0.26f,
                BeamWidth = 0.18f,
                BeamTickInterval = 0.22f,
                BeamWarmupDuration = 0.34f,
                BeamRotateSpeedDegrees = 88f,
                UltimateDuration = 5f,
                UltimateWidth = 0.36f
            };
        }

        private static EnemyAiProfile BuildRiftStriderProfile()
        {
            return new EnemyAiProfile(
                EnemyBehaviorPattern.BossRiftBlitz,
                EnemyWanderStyle.BossRunner,
                EnemyRangedAttackStyle.None,
                EnemyBossCombatStyle.Runner,
                EnemyBossUltimateStyle.RunnerRiftSummon)
            {
                SightRange = 14.2f,
                HearingRange = 15.5f,
                MemoryDuration = 4.2f,
                InvestigateDuration = 2.8f,
                SearchDuration = 4.1f,
                SearchRadius = 3f,
                SearchSpreadDegrees = 100f,
                DirectionalSearchDistance = 3.2f,
                PreferredCombatDistance = 1.8f,
                PreferredDistanceTolerance = 0.48f,
                AttackWindupDuration = 0.14f,
                AttackRecoverDuration = 0.18f,
                SpecialWindupDuration = 0.18f,
                SpecialRecoverDuration = 0.3f,
                UltimateWindupDuration = 0.7f,
                UltimateRecoverDuration = 0.42f,
                ChaseSpeedScale = 1.24f,
                InvestigateSpeedScale = 1f,
                SearchSpeedScale = 0.9f,
                TurnResponse = 10.4f,
                DirectionInfluence = 0.45f,
                PreferredAttackFacingDot = 0.18f,
                UltimateHealthThreshold = 0.55f,
                SpecialMinDistance = 2.8f,
                SpecialMaxDistance = 5.8f,
                PatrolIdleChance = 0.05f,
                PatrolIdleDurationMin = 0.08f,
                PatrolIdleDurationMax = 0.18f,
                PatrolMoveDurationMin = 1.2f,
                PatrolMoveDurationMax = 2.1f,
                MeleeDamageMultiplier = 1.04f,
                SpecialDamageMultiplier = 1.12f,
                SpecialCooldownDuration = 1.45f,
                UltimateCooldownDuration = 24f,
                ChargeDuration = 0.26f,
                ChargeSpeedMultiplier = 3.15f,
                ChargeDamageMultiplier = 1.12f,
                SummonOffsetDistance = 1.4f
            };
        }

        // ── 신규 보스 AI 프로필 ───────────────────────────────────────────────

        private static EnemyAiProfile BuildAbaddonProfile()
        {
            // 어비스 야수형: FeralAlpha보다 강하고 빠름
            return new EnemyAiProfile(
                EnemyBehaviorPattern.BossDarkLord,
                EnemyWanderStyle.BossSentinel,
                EnemyRangedAttackStyle.None,
                EnemyBossCombatStyle.Beast,
                EnemyBossUltimateStyle.BeastCharge)
            {
                SightRange = 14.5f,
                HearingRange = 17f,
                MemoryDuration = 5.2f,
                InvestigateDuration = 3.2f,
                SearchDuration = 4.8f,
                SearchRadius = 3.1f,
                SearchSpreadDegrees = 100f,
                DirectionalSearchDistance = 3.2f,
                PreferredCombatDistance = 1.4f,
                PreferredDistanceTolerance = 0.3f,
                AttackWindupDuration = 0.18f,
                AttackRecoverDuration = 0.22f,
                SpecialWindupDuration = 0.24f,
                SpecialRecoverDuration = 0.32f,
                UltimateWindupDuration = 0.68f,
                UltimateRecoverDuration = 0.48f,
                ChaseSpeedScale = 1.3f,
                InvestigateSpeedScale = 1.02f,
                SearchSpeedScale = 0.9f,
                TurnResponse = 10.8f,
                DirectionInfluence = 0.46f,
                PreferredAttackFacingDot = 0.18f,
                UltimateHealthThreshold = 0.60f,
                SpecialMinDistance = 1.8f,
                SpecialMaxDistance = 5.5f,
                PatrolIdleChance = 0.03f,
                PatrolMoveDurationMin = 1.4f,
                PatrolMoveDurationMax = 2.0f,
                MeleeDamageMultiplier = 1.18f,
                SpecialCooldownDuration = 1.9f,
                UltimateCooldownDuration = 24f,
                ChargeDuration = 0.44f,
                ChargeSpeedMultiplier = 3.6f,
                ChargeDamageMultiplier = 1.65f,
                UltimateDuration = 0.68f,
                UltimateDamageMultiplier = 2.6f
            };
        }

        private static EnemyAiProfile BuildAfritProfile()
        {
            // 화염 포병형: AshenArtillerist의 변형, 더 공격적인 빔 회전
            return new EnemyAiProfile(
                EnemyBehaviorPattern.BossAfritBomber,
                EnemyWanderStyle.BossArtillery,
                EnemyRangedAttackStyle.None,
                EnemyBossCombatStyle.Artillery,
                EnemyBossUltimateStyle.ArtillerySweep)
            {
                SightRange = 15f,
                HearingRange = 16f,
                MemoryDuration = 4.8f,
                SearchDuration = 4.2f,
                SearchRadius = 2.9f,
                SearchSpreadDegrees = 80f,
                DirectionalSearchDistance = 2.8f,
                PreferredCombatDistance = 7.2f,
                PreferredDistanceTolerance = 1.2f,
                AttackWindupDuration = 0.16f,
                AttackRecoverDuration = 0.2f,
                SpecialWindupDuration = 0.3f,
                SpecialRecoverDuration = 0.34f,
                UltimateWindupDuration = 0.65f,
                UltimateRecoverDuration = 0.54f,
                ChaseSpeedScale = 0.86f,
                InvestigateSpeedScale = 0.88f,
                SearchSpeedScale = 0.75f,
                StrafeSpeedScale = 0.9f,
                TurnResponse = 9.6f,
                PreferredAttackFacingDot = 0.3f,
                UltimateHealthThreshold = 0.54f,
                SpecialMinDistance = 4.2f,
                SpecialMaxDistance = 10.5f,
                PatrolIdleChance = 0.14f,
                PatrolIdleDurationMin = 0.12f,
                PatrolIdleDurationMax = 0.26f,
                PatrolMoveDurationMin = 1.0f,
                PatrolMoveDurationMax = 1.8f,
                SpecialDamageMultiplier = 1.0f,
                UltimateDamageMultiplier = 1.65f,
                SpecialCooldownDuration = 1.8f,
                UltimateCooldownDuration = 23f,
                BeamDuration = 0.28f,
                BeamWidth = 0.20f,
                BeamTickInterval = 0.20f,
                BeamWarmupDuration = 0.30f,
                BeamRotateSpeedDegrees = 96f,
                UltimateDuration = 5.5f,
                UltimateWidth = 0.40f
            };
        }

        private static EnemyAiProfile BuildAgathoDemonProfile()
        {
            // 기민한 악마형: RiftStrider의 변형, 더 빠른 소환
            return new EnemyAiProfile(
                EnemyBehaviorPattern.BossAgathoDemon,
                EnemyWanderStyle.BossRunner,
                EnemyRangedAttackStyle.None,
                EnemyBossCombatStyle.Runner,
                EnemyBossUltimateStyle.RunnerRiftSummon)
            {
                SightRange = 14.8f,
                HearingRange = 16f,
                MemoryDuration = 4.5f,
                InvestigateDuration = 2.6f,
                SearchDuration = 4.3f,
                SearchRadius = 3.2f,
                SearchSpreadDegrees = 105f,
                DirectionalSearchDistance = 3.4f,
                PreferredCombatDistance = 1.6f,
                PreferredDistanceTolerance = 0.44f,
                AttackWindupDuration = 0.12f,
                AttackRecoverDuration = 0.16f,
                SpecialWindupDuration = 0.16f,
                SpecialRecoverDuration = 0.28f,
                UltimateWindupDuration = 0.65f,
                UltimateRecoverDuration = 0.38f,
                ChaseSpeedScale = 1.3f,
                InvestigateSpeedScale = 1.04f,
                SearchSpeedScale = 0.92f,
                TurnResponse = 10.8f,
                DirectionInfluence = 0.42f,
                PreferredAttackFacingDot = 0.15f,
                UltimateHealthThreshold = 0.58f,
                SpecialMinDistance = 2.6f,
                SpecialMaxDistance = 6.0f,
                PatrolIdleChance = 0.04f,
                PatrolIdleDurationMin = 0.06f,
                PatrolIdleDurationMax = 0.16f,
                PatrolMoveDurationMin = 1.1f,
                PatrolMoveDurationMax = 2.0f,
                MeleeDamageMultiplier = 1.06f,
                SpecialDamageMultiplier = 1.15f,
                SpecialCooldownDuration = 1.35f,
                UltimateCooldownDuration = 22f,
                ChargeDuration = 0.24f,
                ChargeSpeedMultiplier = 3.3f,
                ChargeDamageMultiplier = 1.18f,
                SummonOffsetDistance = 1.5f
            };
        }

        private static EnemyAiProfile BuildAnnihilatorProfile()
        {
            // 초중형 탱커: BulwarkColossus보다 더 많은 체력과 공격력
            return new EnemyAiProfile(
                EnemyBehaviorPattern.BossAnnihilator,
                EnemyWanderStyle.BossSentinel,
                EnemyRangedAttackStyle.None,
                EnemyBossCombatStyle.Tanker,
                EnemyBossUltimateStyle.TankerRocketRain)
            {
                SightRange = 15f,
                HearingRange = 17f,
                MemoryDuration = 4.8f,
                SearchDuration = 4.5f,
                SearchRadius = 3.3f,
                SearchSpreadDegrees = 85f,
                DirectionalSearchDistance = 3.0f,
                PreferredCombatDistance = 6.5f,
                PreferredDistanceTolerance = 1.1f,
                AttackWindupDuration = 0.16f,
                AttackRecoverDuration = 0.28f,
                SpecialWindupDuration = 0.32f,
                SpecialRecoverDuration = 0.44f,
                UltimateWindupDuration = 0.80f,
                UltimateRecoverDuration = 0.58f,
                ChaseSpeedScale = 0.82f,
                InvestigateSpeedScale = 0.72f,
                SearchSpeedScale = 0.66f,
                StrafeSpeedScale = 0.52f,
                TurnResponse = 6.8f,
                UltimateHealthThreshold = 0.52f,
                SpecialMinDistance = 3.6f,
                SpecialMaxDistance = 9.2f,
                PatrolIdleChance = 0.06f,
                PatrolIdleDurationMin = 0.16f,
                PatrolIdleDurationMax = 0.30f,
                PatrolMoveDurationMin = 1.1f,
                PatrolMoveDurationMax = 1.9f,
                MeleeDamageMultiplier = 1.15f,
                SpecialDamageMultiplier = 1.25f,
                UltimateDamageMultiplier = 1.12f,
                SpecialCooldownDuration = 2.6f,
                UltimateCooldownDuration = 26f,
                ProjectileSpeed = 6.4f,
                ProjectileLifetime = 4.5f,
                ExplosionRadius = 1.1f,
                UltimateDuration = 0.9f,
                UltimateWidth = 1.3f
            };
        }

        private static EnemyAiProfile BuildArachnobaronProfile()
        {
            // 거미 남작형: 원거리 산성/거미줄 공격
            return new EnemyAiProfile(
                EnemyBehaviorPattern.BossArackBaron,
                EnemyWanderStyle.BossArtillery,
                EnemyRangedAttackStyle.None,
                EnemyBossCombatStyle.Artillery,
                EnemyBossUltimateStyle.ArtillerySweep)
            {
                SightRange = 13.8f,
                HearingRange = 15f,
                MemoryDuration = 4.4f,
                SearchDuration = 3.8f,
                SearchRadius = 2.6f,
                SearchSpreadDegrees = 72f,
                DirectionalSearchDistance = 2.5f,
                PreferredCombatDistance = 6.4f,
                PreferredDistanceTolerance = 1.05f,
                AttackWindupDuration = 0.20f,
                AttackRecoverDuration = 0.24f,
                SpecialWindupDuration = 0.36f,
                SpecialRecoverDuration = 0.40f,
                UltimateWindupDuration = 0.72f,
                UltimateRecoverDuration = 0.60f,
                ChaseSpeedScale = 0.80f,
                InvestigateSpeedScale = 0.82f,
                SearchSpeedScale = 0.70f,
                StrafeSpeedScale = 0.84f,
                TurnResponse = 8.8f,
                PreferredAttackFacingDot = 0.38f,
                UltimateHealthThreshold = 0.50f,
                SpecialMinDistance = 4.8f,
                SpecialMaxDistance = 10.2f,
                PatrolIdleChance = 0.18f,
                PatrolIdleDurationMin = 0.16f,
                PatrolIdleDurationMax = 0.30f,
                PatrolMoveDurationMin = 1.2f,
                PatrolMoveDurationMax = 2.0f,
                SpecialDamageMultiplier = 0.95f,
                UltimateDamageMultiplier = 1.5f,
                SpecialCooldownDuration = 2.0f,
                UltimateCooldownDuration = 26f,
                BeamDuration = 0.24f,
                BeamWidth = 0.16f,
                BeamTickInterval = 0.24f,
                BeamWarmupDuration = 0.36f,
                BeamRotateSpeedDegrees = 82f,
                UltimateDuration = 4.8f,
                UltimateWidth = 0.34f
            };
        }

        private static EnemyAiProfile BuildArachnophyteProfile()
        {
            // 거미 유충형: 빠른 소환 특화 러너
            return new EnemyAiProfile(
                EnemyBehaviorPattern.BossArachnoFang,
                EnemyWanderStyle.BossRunner,
                EnemyRangedAttackStyle.None,
                EnemyBossCombatStyle.Runner,
                EnemyBossUltimateStyle.RunnerRiftSummon)
            {
                SightRange = 13.5f,
                HearingRange = 14.8f,
                MemoryDuration = 4.0f,
                InvestigateDuration = 2.5f,
                SearchDuration = 3.8f,
                SearchRadius = 2.8f,
                SearchSpreadDegrees = 98f,
                DirectionalSearchDistance = 3.0f,
                PreferredCombatDistance = 1.9f,
                PreferredDistanceTolerance = 0.50f,
                AttackWindupDuration = 0.15f,
                AttackRecoverDuration = 0.20f,
                SpecialWindupDuration = 0.20f,
                SpecialRecoverDuration = 0.32f,
                UltimateWindupDuration = 0.72f,
                UltimateRecoverDuration = 0.44f,
                ChaseSpeedScale = 1.20f,
                InvestigateSpeedScale = 0.98f,
                SearchSpeedScale = 0.88f,
                TurnResponse = 10f,
                DirectionInfluence = 0.48f,
                PreferredAttackFacingDot = 0.20f,
                UltimateHealthThreshold = 0.52f,
                SpecialMinDistance = 2.9f,
                SpecialMaxDistance = 5.6f,
                PatrolIdleChance = 0.05f,
                PatrolIdleDurationMin = 0.08f,
                PatrolIdleDurationMax = 0.20f,
                PatrolMoveDurationMin = 1.2f,
                PatrolMoveDurationMax = 2.2f,
                MeleeDamageMultiplier = 1.02f,
                SpecialDamageMultiplier = 1.10f,
                SpecialCooldownDuration = 1.50f,
                UltimateCooldownDuration = 25f,
                ChargeDuration = 0.28f,
                ChargeSpeedMultiplier = 3.0f,
                ChargeDamageMultiplier = 1.10f,
                SummonOffsetDistance = 1.45f
            };
        }

        private static EnemyAiProfile BuildAracnorbQueenProfile()
        {
            // 거미 여왕형: 최강의 탱커, 대량 로켓 강화
            return new EnemyAiProfile(
                EnemyBehaviorPattern.BossSpiderQueen,
                EnemyWanderStyle.BossSentinel,
                EnemyRangedAttackStyle.None,
                EnemyBossCombatStyle.Tanker,
                EnemyBossUltimateStyle.TankerRocketRain)
            {
                SightRange = 15.5f,
                HearingRange = 18f,
                MemoryDuration = 5.0f,
                SearchDuration = 4.8f,
                SearchRadius = 3.5f,
                SearchSpreadDegrees = 90f,
                DirectionalSearchDistance = 3.2f,
                PreferredCombatDistance = 6.8f,
                PreferredDistanceTolerance = 1.15f,
                AttackWindupDuration = 0.15f,
                AttackRecoverDuration = 0.26f,
                SpecialWindupDuration = 0.30f,
                SpecialRecoverDuration = 0.42f,
                UltimateWindupDuration = 0.90f,
                UltimateRecoverDuration = 0.65f,
                ChaseSpeedScale = 0.80f,
                InvestigateSpeedScale = 0.70f,
                SearchSpeedScale = 0.64f,
                StrafeSpeedScale = 0.50f,
                TurnResponse = 6.5f,
                UltimateHealthThreshold = 0.50f,
                SpecialMinDistance = 3.8f,
                SpecialMaxDistance = 9.5f,
                PatrolIdleChance = 0.05f,
                PatrolIdleDurationMin = 0.14f,
                PatrolIdleDurationMax = 0.28f,
                PatrolMoveDurationMin = 1.0f,
                PatrolMoveDurationMax = 1.8f,
                MeleeDamageMultiplier = 1.18f,
                SpecialDamageMultiplier = 1.30f,
                UltimateDamageMultiplier = 1.15f,
                SpecialCooldownDuration = 2.5f,
                UltimateCooldownDuration = 25f,
                ProjectileSpeed = 6.6f,
                ProjectileLifetime = 4.6f,
                ExplosionRadius = 1.15f,
                UltimateDuration = 0.95f,
                UltimateWidth = 1.4f
            };
        }

        // ── 내부 등록 헬퍼 ────────────────────────────────────────────────────

        private static void Register(EnemyArchetype archetype, params string[] aliases)
        {
            if (archetype == null || string.IsNullOrWhiteSpace(archetype.AssetId))
                return;

            s_byAssetId[archetype.AssetId] = archetype;

            if (aliases == null)
                return;

            for (int i = 0; i < aliases.Length; i++)
            {
                string alias = aliases[i];
                if (!string.IsNullOrWhiteSpace(alias))
                    s_byAssetId[alias] = archetype;
            }
        }
    }
}
