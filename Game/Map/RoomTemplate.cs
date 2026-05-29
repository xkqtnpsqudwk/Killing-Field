using System;
using System.Collections.Generic;
using My2DEngine.Game;
using My2DEngine.Game.Config;

namespace My2DEngine.Game.Map
{
    /// <summary>
    /// 로그라이크 모드에서 방 하나를 구성하는 정적 설계 데이터 템플릿.
    /// 방 크기, 레이아웃 변형, 보스/정예 여부, 적 스폰 오프셋(방 중심 기준)을 담는다.
    /// 실제 맵 타일 생성은 <see cref="MapManager.LoadRoomFromTemplate"/>이 수행한다.
    /// </summary>
    public class RoomTemplate
    {
        /// <summary>템플릿 고유 식별자. 디버그 및 로그 출력에 사용된다.</summary>
        public string TemplateId { get; set; }

        /// <summary>방 가로 크기(타일). 외곽 벽 포함 전체 너비.</summary>
        public int RoomWidth { get; set; } = 16;

        /// <summary>방 세로 크기(타일). 외곽 벽 포함 전체 높이.</summary>
        public int RoomHeight { get; set; } = 16;

        /// <summary>방 내부 구조물 배치 변형.</summary>
        public RoomLayoutVariant LayoutVariant { get; set; } = RoomLayoutVariant.Open;

        /// <summary>보스 방 여부.</summary>
        public bool IsBossRoom { get; set; }

        /// <summary>정예 방 여부. 내부 플래그 명은 기존 구조 호환성을 위해 유지한다.</summary>
        public bool IsMiniBossRoom { get; set; }

        /// <summary>휴식 방 여부. 전투 대신 Luck 기반 카드 상점을 이용하는 룸이다.</summary>
        public bool IsRestRoom { get; set; }

        /// <summary>전투 방의 클리어 목표 종류다.</summary>
        public RoomObjectiveKind ObjectiveKind { get; set; } = RoomObjectiveKind.EliminateAll;

        /// <summary>시간 목표가 있을 때 사용하는 목표 지속 시간(초).</summary>
        public float ObjectiveDuration { get; set; }

        /// <summary>전투 방에 적용되는 위험 modifier 종류다.</summary>
        public RoomHazardKind HazardKind { get; set; } = RoomHazardKind.None;

        /// <summary>
        /// 적 스폰 포인트 배열. X/Y는 방 중심(MapCenter)을 원점으로 하는 타일 오프셋.
        /// <see cref="MapManager.LoadRoomFromTemplate"/>이 절대 좌표로 변환한다.
        /// </summary>
        public StageSpawnPoint[] Spawns { get; set; } = Array.Empty<StageSpawnPoint>();
    }

    /// <summary>
    /// 층 번호에 따라 적절한 <see cref="RoomTemplate"/>을 선택해 반환하는 정적 라이브러리.
    /// <para>선택 규칙:</para>
    /// <list type="bullet">
    ///   <item>floor % 20 == 0 → 보스 템플릿 (크기 소폭 랜덤)</item>
    ///   <item>15% → 휴식 방 (크기 랜덤)</item>
    ///   <item>25% → 정예 방 (크기·레이아웃·적 구성 랜덤, 스탯 1.5×)</item>
    ///   <item>60% → 일반 방 (크기·레이아웃·적 구성 랜덤)</item>
    /// </list>
    /// </summary>
    public static class RoomTemplateLibrary
    {
        private const float RestRoomChance = 0.15f;
        private const float EliteRoomChance = 0.25f;
        private const float EliteStatMultiplier = 1.5f;
        private const float SurvivalRoomChance = 0.25f;
        private const float KeyTargetRoomChance = 0.25f;
        private const float ToxicMistRoomChance = 0.24f;
        private const float SupplyShortageRoomChance = 0.20f;
        public const int HighRoomRiskScore = 4;
        public const int ExtremeRoomRiskScore = 5;

        private const int NormalRoomMinSize = 16;
        private const int NormalRoomMaxSize = 22;

        private static readonly RoomTemplate[] s_restTemplates;

        // 일반/정예 방에서 랜덤 선택되는 레이아웃 풀
        private static readonly RoomLayoutVariant[] s_combatLayouts =
        {
            RoomLayoutVariant.Open,
            RoomLayoutVariant.CenterPillar,
            RoomLayoutVariant.TwinPillars,
            RoomLayoutVariant.CornerPillars,
            RoomLayoutVariant.SplitLanes,
            RoomLayoutVariant.DiagonalPillars,
            RoomLayoutVariant.InnerRing,
            RoomLayoutVariant.CenterWall,
        };

        // 보스 클리어 단계별 일반 적 아이디 풀 (중복 항목으로 확률 조정).
        // 0회: 기본 적 + 실험실 변형, 1회: 중거리/저격 신규 적, 2회 이상: 무리 압박형 Hellion까지 해금.
        private static readonly string[][] s_enemyTierWeightedPools =
        {
            new[] { "gunner", "gunner", "uzi_trooper", "uzi_trooper", "lab_butcher", "elite", "ruined_gunner", "ruined_gunner" },
            new[] { "gunner", "uzi_trooper", "lab_butcher", "ruined_gunner", "plasma_tech", "plasma_tech", "grenadier_scientist", "blood_ghost", "blood_ghost", "beam_revenant", "beam_revenant" },
            new[] { "uzi_trooper", "lab_butcher", "plasma_tech", "grenadier_scientist", "blood_ghost", "beam_revenant", "hellion", "hellion", "hellion" },
        };

        // 방마다 최소 1개 이상 우선 배치되는 역할 후보.
        // 압박형/저격형/돌진형/방해형/지역 장악형이 순수 랜덤에 묻히지 않도록 한다.
        private static readonly string[][] s_enemyTierRolePools =
        {
            new[] { "lab_butcher", "uzi_trooper", "ruined_gunner", "elite" },
            new[] { "blood_ghost", "beam_revenant", "plasma_tech", "grenadier_scientist" },
            new[] { "hellion", "beam_revenant", "grenadier_scientist", "blood_ghost", "plasma_tech" },
        };

        // 보스 클리어 단계별 보스 후보. 신규 보스는 이전 보스를 처치한 뒤부터 실제 런에 등장한다.
        private static readonly string[][] s_bossTierPools =
        {
            new[] { "feral_alpha", "bulwark_colossus", "ashen_artillerist", "rift_strider" },
            new[] { "feral_alpha", "bulwark_colossus", "ashen_artillerist", "rift_strider", "abaddon", "afrit", "agatho_demon", "arachnobaron" },
            new[] { "feral_alpha", "bulwark_colossus", "ashen_artillerist", "rift_strider", "abaddon", "afrit", "agatho_demon", "annihilator", "arachnobaron", "arachnophyte", "aracnorb_queen" },
        };

        static RoomTemplateLibrary()
        {
            s_restTemplates = BuildRestTemplates();
        }

        /// <summary>동시 스폰 제한 기본값. 보스 클리어마다 1씩 증가한다.</summary>
        private const int BaseSpawnCap = 5;

        /// <summary>보스 클리어로 늘어날 수 있는 스폰 제한 최대값.</summary>
        private const int MaxSpawnCap = 12;

        /// <summary>
        /// 층 번호에 맞는 <see cref="RoomTemplate"/>을 반환한다.
        /// 보스·휴식 방을 제외한 일반/정예 방은 크기·레이아웃·적 구성이 매번 무작위로 결정된다.
        /// </summary>
        /// <param name="floor">현재 층 번호.</param>
        /// <param name="rng">무작위 선택에 사용할 난수 생성기.</param>
        /// <param name="bossClearGrowthCount">누적 보스 클리어 수. 스폰 상한과 적/보스 해금 단계에 사용된다.</param>
        /// <param name="allowRestRoom">false이면 휴식 방을 선택 풀에서 제외한다.</param>
        public static RoomTemplate SelectForFloor(int floor, Random rng, int bossClearGrowthCount = 0, bool allowRestRoom = true)
        {
            // 보스 방: 20층마다, 크기만 소폭 랜덤
            if (floor > 0 && floor % 20 == 0)
            {
                string bossAssetId = PickBossAssetId(rng, bossClearGrowthCount);
                int bossSize = 29 + rng.Next(5);  // 29~33
                return new RoomTemplate
                {
                    TemplateId = BuildBossTemplateId(bossAssetId),
                    RoomWidth = bossSize,
                    RoomHeight = bossSize,
                    IsBossRoom = true,
                    LayoutVariant = RoomLayoutVariant.Open,
                    ObjectiveKind = RoomObjectiveKind.EliminateAll,
                    HazardKind = RoomHazardKind.None,
                    Spawns = new[] { SpawnEnemy(bossAssetId, 0f, 0f) }
                };
            }

            double roll = rng.NextDouble();

            // 휴식 방: 크기 고정 (3종 중 랜덤 선택)
            if (allowRestRoom && roll < RestRoomChance)
            {
                return s_restTemplates[rng.Next(s_restTemplates.Length)];
            }

            bool isElite = allowRestRoom
                ? roll < RestRoomChance + EliteRoomChance
                : rng.NextDouble() < EliteRoomChance;

            // 방 크기 랜덤 (16~22)
            int roomW = NormalRoomMinSize + rng.Next(NormalRoomMaxSize - NormalRoomMinSize + 1);
            int roomH = NormalRoomMinSize + rng.Next(NormalRoomMaxSize - NormalRoomMinSize + 1);

            // 레이아웃 랜덤
            RoomLayoutVariant layout = s_combatLayouts[rng.Next(s_combatLayouts.Length)];

            int spawnCap = Math.Min(MaxSpawnCap, BaseSpawnCap + bossClearGrowthCount);
            RoomObjectiveKind objective = RollCombatObjective(rng);
            RoomHazardKind hazard = RollCombatHazard(rng);
            float objectiveDuration = objective == RoomObjectiveKind.Survive
                ? GetSurvivalDuration(floor, isElite)
                : 0f;
            StageSpawnPoint[] spawns = GenerateRandomSpawns(floor, bossClearGrowthCount, isElite, roomW, roomH, spawnCap, rng, layout);
            if (objective == RoomObjectiveKind.KeyTarget)
            {
                MarkKeyTargetSpawn(spawns, rng);
            }

            return new RoomTemplate
            {
                TemplateId = BuildCombatTemplateId(isElite, objective, hazard),
                RoomWidth = roomW,
                RoomHeight = roomH,
                LayoutVariant = layout,
                IsMiniBossRoom = isElite,
                ObjectiveKind = objective,
                ObjectiveDuration = objectiveDuration,
                HazardKind = hazard,
                Spawns = spawns
            };
        }

        private static RoomObjectiveKind RollCombatObjective(Random rng)
        {
            double roll = rng.NextDouble();
            if (roll < SurvivalRoomChance)
            {
                return RoomObjectiveKind.Survive;
            }

            if (roll < SurvivalRoomChance + KeyTargetRoomChance)
            {
                return RoomObjectiveKind.KeyTarget;
            }

            return RoomObjectiveKind.EliminateAll;
        }

        private static RoomHazardKind RollCombatHazard(Random rng)
        {
            double roll = rng.NextDouble();
            if (roll < ToxicMistRoomChance)
            {
                return RoomHazardKind.ToxicMist;
            }

            if (roll < ToxicMistRoomChance + SupplyShortageRoomChance)
            {
                return RoomHazardKind.SupplyShortage;
            }

            return RoomHazardKind.None;
        }

        private static float GetSurvivalDuration(int floor, bool isElite)
        {
            float duration = GameConfig.SurvivalRoomBaseDuration + Math.Max(0, floor - 1) * GameConfig.SurvivalRoomDurationPerFloor;
            if (isElite)
            {
                duration += GameConfig.SurvivalRoomEliteExtraDuration;
            }

            return Math.Min(GameConfig.SurvivalRoomMaxDuration, duration);
        }

        private static string BuildCombatTemplateId(bool isElite, RoomObjectiveKind objective, RoomHazardKind hazard)
        {
            string prefix = isElite ? "Elite" : "Normal";
            string objectiveId = objective == RoomObjectiveKind.Survive
                ? "Survive"
                : objective == RoomObjectiveKind.KeyTarget
                    ? "Key"
                    : "Eliminate";
            string hazardId = hazard == RoomHazardKind.ToxicMist
                ? "-Toxic"
                : hazard == RoomHazardKind.SupplyShortage
                    ? "-LowSupply"
                    : string.Empty;
            return prefix + "-" + objectiveId + hazardId + "-Random";
        }

        public static int GetRoomRiskScore(RoomTemplate template)
        {
            if (template == null)
            {
                return 0;
            }

            return GetRoomRiskScore(
                template.IsBossRoom,
                template.IsMiniBossRoom,
                template.IsRestRoom,
                template.LayoutVariant,
                template.ObjectiveKind,
                template.HazardKind,
                template.Spawns == null ? 0 : template.Spawns.Length);
        }

        public static int GetRoomRiskScore(StageRoom room)
        {
            if (room == null)
            {
                return 0;
            }

            return GetRoomRiskScore(
                room.IsBossRoom,
                room.IsMiniBossRoom,
                room.IsRestRoom,
                room.LayoutVariant,
                room.ObjectiveKind,
                room.HazardKind,
                room.Spawns == null ? 0 : room.Spawns.Length);
        }

        public static bool IsExtremeRewardRoom(RoomTemplate template)
        {
            return template != null && !template.IsBossRoom && !template.IsRestRoom &&
                GetRoomRiskScore(template) >= ExtremeRoomRiskScore;
        }

        public static bool IsExtremeRewardRoom(StageRoom room)
        {
            return room != null && !room.IsBossRoom && !room.IsRestRoom &&
                GetRoomRiskScore(room) >= ExtremeRoomRiskScore;
        }

        public static int GetCardRewardGradeBoost(RoomTemplate template)
        {
            if (template == null || template.IsBossRoom || template.IsRestRoom)
            {
                return 0;
            }

            return GetRoomRiskScore(template) >= HighRoomRiskScore ? 1 : 0;
        }

        public static int GetCardRewardGradeBoost(StageRoom room)
        {
            if (room == null || room.IsBossRoom || room.IsRestRoom)
            {
                return 0;
            }

            return GetRoomRiskScore(room) >= HighRoomRiskScore ? 1 : 0;
        }

        private static int GetRoomRiskScore(
            bool isBossRoom,
            bool isMiniBossRoom,
            bool isRestRoom,
            RoomLayoutVariant layout,
            RoomObjectiveKind objective,
            RoomHazardKind hazard,
            int spawnCount)
        {
            if (isRestRoom)
            {
                return 0;
            }

            if (isBossRoom)
            {
                return ExtremeRoomRiskScore;
            }

            int score = isMiniBossRoom ? 2 : 0;
            score += GetLayoutRiskScore(layout);
            score += GetObjectiveRiskScore(objective);
            score += GetHazardRiskScore(hazard);

            if (spawnCount >= 8)
            {
                score += 2;
            }
            else if (spawnCount >= 6)
            {
                score += 1;
            }

            return score;
        }

        private static int GetLayoutRiskScore(RoomLayoutVariant layout)
        {
            switch (layout)
            {
                case RoomLayoutVariant.CenterPillar:
                case RoomLayoutVariant.TwinPillars:
                case RoomLayoutVariant.CornerPillars:
                    return 1;
                case RoomLayoutVariant.SplitLanes:
                case RoomLayoutVariant.DiagonalPillars:
                    return 2;
                case RoomLayoutVariant.InnerRing:
                case RoomLayoutVariant.CenterWall:
                    return 3;
                default:
                    return 0;
            }
        }

        private static int GetObjectiveRiskScore(RoomObjectiveKind objective)
        {
            switch (objective)
            {
                case RoomObjectiveKind.Survive:
                    return 2;
                case RoomObjectiveKind.KeyTarget:
                    return 1;
                default:
                    return 0;
            }
        }

        private static int GetHazardRiskScore(RoomHazardKind hazard)
        {
            switch (hazard)
            {
                case RoomHazardKind.ToxicMist:
                case RoomHazardKind.SupplyShortage:
                    return 2;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// 층 번호·방 종류·크기에 맞춰 스폰 포인트 배열을 동적으로 생성한다.
        /// 적 종류는 층 단계 풀에서 무작위로 선택되며, 스폰 위치는 방 내부에 고르게 분산된다.
        /// </summary>
        private static StageSpawnPoint[] GenerateRandomSpawns(
            int floor, int bossClearGrowthCount, bool isElite, int roomW, int roomH, int spawnCap, Random rng,
            RoomLayoutVariant layout)
        {
            // 층에 따라 적 수 증가 (3 → spawnCap 상한)
            int baseCount = 3 + (floor - 1) / 8;
            if (isElite) baseCount++;
            int count = Math.Min(spawnCap, Math.Max(3, baseCount));

            float halfW = (roomW - 4) * 0.5f;
            float halfH = (roomH - 4) * 0.5f;

            List<(float x, float y)> positions = GenerateSpreadPositions(count, halfW, halfH, rng);
            string[] roster = BuildEnemyRoster(positions.Count, bossClearGrowthCount, isElite, rng);

            // 방 레이아웃이 선호하는 행동 패턴 집합. 적 아키타입이 실제로 지원하는 패턴과
            // 교집합을 취해, 지원하지 않는 패턴을 강제하지 않으면서 전술 성향만 편향시킨다.
            EnemyBehaviorPattern[] layoutPreferred = GetLayoutPreferredPatterns(layout);

            StageSpawnPoint[] spawns = new StageSpawnPoint[positions.Count];
            for (int i = 0; i < positions.Count; i++)
            {
                string assetId = roster[i];
                (float x, float y) = positions[i];
                StageSpawnPoint sp = SpawnEnemy(assetId, x, y, layoutPreferred);
                if (isElite)
                {
                    sp.HealthMultiplier = EliteStatMultiplier;
                    sp.DamageMultiplier = EliteStatMultiplier;
                }
                spawns[i] = sp;
            }

            return spawns;
        }

        /// <summary>
        /// 방 레이아웃 변형에 어울리는 선호 행동 패턴 집합을 반환한다.
        /// 이 집합은 적 아키타입이 지원하는 패턴과 교집합으로만 적용되므로(<see cref="SpawnEnemy"/>),
        /// 적이 지원하지 않는 패턴이 강제되지 않는다. 빈 배열이면 레이아웃 편향 없음(아키타입 기본 풀 사용).
        /// </summary>
        private static EnemyBehaviorPattern[] GetLayoutPreferredPatterns(RoomLayoutVariant layout)
        {
            switch (layout)
            {
                // 개활지: 엄폐물이 없어 정면 압박이 유효하다.
                case RoomLayoutVariant.Open:
                    return new[] { EnemyBehaviorPattern.Rushdown, EnemyBehaviorPattern.Juggernaut, EnemyBehaviorPattern.Default };

                // 중앙 기둥 한 개: 기둥을 끼고 도는 횡이동/타이밍 돌진이 어울린다.
                case RoomLayoutVariant.CenterPillar:
                    return new[] { EnemyBehaviorPattern.Strafe, EnemyBehaviorPattern.Pouncer };

                // 쌍기둥: 엄폐 활용 치고빠지기.
                case RoomLayoutVariant.TwinPillars:
                    return new[] { EnemyBehaviorPattern.Skirmisher, EnemyBehaviorPattern.Strafe };

                // 네 모서리 기둥: 모서리 엄폐 저격/원거리 견제.
                case RoomLayoutVariant.CornerPillars:
                    return new[] { EnemyBehaviorPattern.Kite, EnemyBehaviorPattern.BeamSniper, EnemyBehaviorPattern.ZombieGunner };

                // 분할 통로: 통로를 따라 빠르게 들어오는 돌진/접근.
                case RoomLayoutVariant.SplitLanes:
                    return new[] { EnemyBehaviorPattern.Rushdown, EnemyBehaviorPattern.Skirmisher };

                // 대각선 기둥: 비스듬한 엄폐, 횡이동 견제.
                case RoomLayoutVariant.DiagonalPillars:
                    return new[] { EnemyBehaviorPattern.Strafe, EnemyBehaviorPattern.Kite };

                // 내부 링: 중앙을 끼고 도는 타이밍 돌진/근접.
                case RoomLayoutVariant.InnerRing:
                    return new[] { EnemyBehaviorPattern.Pouncer, EnemyBehaviorPattern.Skirmisher };

                // 중앙 가로 장벽: 장벽 너머 원거리 견제.
                case RoomLayoutVariant.CenterWall:
                    return new[] { EnemyBehaviorPattern.Kite, EnemyBehaviorPattern.BeamSniper, EnemyBehaviorPattern.Strafe };

                default:
                    return Array.Empty<EnemyBehaviorPattern>();
            }
        }

        private static void MarkKeyTargetSpawn(StageSpawnPoint[] spawns, Random rng)
        {
            if (spawns == null || spawns.Length == 0)
            {
                return;
            }

            int index = PickKeyTargetSpawnIndex(spawns, rng);
            StageSpawnPoint spawn = spawns[index];
            if (spawn == null)
            {
                return;
            }

            spawn.IsObjectiveTarget = true;
            spawn.HealthMultiplier *= GameConfig.KeyTargetHealthMultiplier;
            spawn.ScaleMultiplier *= GameConfig.KeyTargetScaleMultiplier;
        }

        private static int PickKeyTargetSpawnIndex(StageSpawnPoint[] spawns, Random rng)
        {
            int bestScore = int.MinValue;
            var candidates = new List<int>(spawns.Length);
            for (int i = 0; i < spawns.Length; i++)
            {
                StageSpawnPoint spawn = spawns[i];
                if (spawn == null)
                {
                    continue;
                }

                int score = GetKeyTargetCandidateScore(spawn);
                if (score > bestScore)
                {
                    bestScore = score;
                    candidates.Clear();
                    candidates.Add(i);
                }
                else if (score == bestScore)
                {
                    candidates.Add(i);
                }
            }

            if (candidates.Count <= 0)
            {
                return 0;
            }

            return candidates[rng.Next(candidates.Count)];
        }

        private static int GetKeyTargetCandidateScore(StageSpawnPoint spawn)
        {
            if (spawn == null)
            {
                return int.MinValue;
            }

            EnemyArchetype archetype = EnemyCatalog.Resolve(spawn.EnemyAssetId, spawn.Type);
            EnemyDefinition definition = archetype.CreateDefinition();
            int score = (int)Math.Round(definition.MaxHealth);
            if (definition.AttackRange >= 3f)
            {
                score += 12;
            }

            if (archetype.Rank == EnemyRank.MiniBoss || spawn.Rank == EnemyRank.MiniBoss)
            {
                score += 60;
            }

            return score;
        }

        private static string[] BuildEnemyRoster(int count, int bossClearGrowthCount, bool isElite, Random rng)
        {
            if (count <= 0)
            {
                return Array.Empty<string>();
            }

            string[] weightedPool = GetEnemyWeightedPool(bossClearGrowthCount);
            string[] rolePool = GetEnemyRolePool(bossClearGrowthCount);
            var roster = new List<string>(count);

            int guaranteedRoleCount = count >= 6 ? 2 : 1;
            if (isElite && count >= 4)
            {
                guaranteedRoleCount++;
            }

            guaranteedRoleCount = Math.Min(count, Math.Min(rolePool.Length, guaranteedRoleCount));
            int roleStart = rng.Next(rolePool.Length);
            for (int i = 0; i < guaranteedRoleCount; i++)
            {
                roster.Add(rolePool[(roleStart + i) % rolePool.Length]);
            }

            while (roster.Count < count)
            {
                roster.Add(weightedPool[rng.Next(weightedPool.Length)]);
            }

            ShuffleRoster(roster, rng);
            return roster.ToArray();
        }

        private static string[] GetEnemyWeightedPool(int bossClearGrowthCount)
        {
            return s_enemyTierWeightedPools[GetEnemyTierIndex(bossClearGrowthCount)];
        }

        private static string[] GetEnemyRolePool(int bossClearGrowthCount)
        {
            return s_enemyTierRolePools[GetEnemyTierIndex(bossClearGrowthCount)];
        }

        private static int GetEnemyTierIndex(int bossClearGrowthCount)
        {
            int tier = Math.Max(0, bossClearGrowthCount);
            return Math.Min(s_enemyTierWeightedPools.Length - 1, tier);
        }

        private static string PickBossAssetId(Random rng, int bossClearGrowthCount)
        {
            string[] pool = s_bossTierPools[GetBossTierIndex(bossClearGrowthCount)];
            return pool[rng.Next(pool.Length)];
        }

        private static int GetBossTierIndex(int bossClearGrowthCount)
        {
            int tier = Math.Max(0, bossClearGrowthCount);
            return Math.Min(s_bossTierPools.Length - 1, tier);
        }

        private static string BuildBossTemplateId(string assetId)
        {
            return "Boss-" + (string.IsNullOrWhiteSpace(assetId) ? "Unknown" : assetId.Replace("_", "-"));
        }

        private static void ShuffleRoster(List<string> roster, Random rng)
        {
            for (int i = roster.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                string temp = roster[i];
                roster[i] = roster[j];
                roster[j] = temp;
            }
        }

        /// <summary>
        /// 방 내부에 <paramref name="count"/>개의 스폰 위치를 서로 최소 2.2타일 이상 떨어지도록 생성한다.
        /// 시도 횟수 초과 시 확보된 위치만 반환한다.
        /// </summary>
        private static List<(float x, float y)> GenerateSpreadPositions(
            int count, float halfW, float halfH, Random rng)
        {
            var positions = new List<(float x, float y)>(count);
            const float minDist = 2.2f;
            const float minDistSq = minDist * minDist;
            int maxAttempts = count * 30;
            int attempts = 0;

            while (positions.Count < count && attempts < maxAttempts)
            {
                attempts++;
                float x = (float)((rng.NextDouble() * 2.0 - 1.0) * halfW);
                float y = (float)((rng.NextDouble() * 2.0 - 1.0) * halfH);

                bool tooClose = false;
                for (int j = 0; j < positions.Count; j++)
                {
                    float dx = positions[j].x - x;
                    float dy = positions[j].y - y;
                    if (dx * dx + dy * dy < minDistSq)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                    positions.Add((x, y));
            }

            return positions;
        }

        private static RoomTemplate[] BuildRestTemplates()
        {
            return new[]
            {
                new RoomTemplate { TemplateId = "Rest-Sanctuary-Compact", RoomWidth = 17, RoomHeight = 17, LayoutVariant = RoomLayoutVariant.Open, IsRestRoom = true },
                new RoomTemplate { TemplateId = "Rest-Sanctuary-Wide",    RoomWidth = 19, RoomHeight = 17, LayoutVariant = RoomLayoutVariant.Open, IsRestRoom = true },
                new RoomTemplate { TemplateId = "Rest-Sanctuary-Tall",    RoomWidth = 17, RoomHeight = 19, LayoutVariant = RoomLayoutVariant.Open, IsRestRoom = true },
            };
        }

        private static StageSpawnPoint SpawnEnemy(string assetId, float x, float y)
        {
            return SpawnEnemy(assetId, x, y, null);
        }

        /// <summary>
        /// 스폰 포인트를 만든다. <paramref name="layoutPreferred"/>가 주어지면 아키타입이 지원하는
        /// 패턴과의 교집합으로 행동 패턴 풀을 좁혀 방 레이아웃에 맞는 전술 성향을 부여한다.
        /// 교집합이 비면 아키타입 기본 풀을 그대로 사용한다(지원하지 않는 패턴을 강제하지 않음).
        /// </summary>
        private static StageSpawnPoint SpawnEnemy(string assetId, float x, float y, EnemyBehaviorPattern[] layoutPreferred)
        {
            EnemyArchetype archetype = EnemyCatalog.Get(assetId);
            EnemyBehaviorPattern[] patternPool = archetype.BehaviorPatternPool.Length > 0
                ? (EnemyBehaviorPattern[])archetype.BehaviorPatternPool.Clone()
                : null;

            if (patternPool != null && layoutPreferred != null && layoutPreferred.Length > 0)
            {
                var intersection = new List<EnemyBehaviorPattern>(patternPool.Length);
                for (int i = 0; i < patternPool.Length; i++)
                {
                    if (Array.IndexOf(layoutPreferred, patternPool[i]) >= 0)
                    {
                        intersection.Add(patternPool[i]);
                    }
                }

                // 교집합이 있으면 그 부분집합만 사용, 없으면 아키타입 원본 풀 유지.
                if (intersection.Count > 0)
                {
                    patternPool = intersection.ToArray();
                }
            }

            return new StageSpawnPoint
            {
                EnemyAssetId = archetype.AssetId,
                Type = archetype.Definition.Type,
                Rank = archetype.Rank,
                BehaviorPatternPool = patternPool,
                X = x,
                Y = y,
                HealthMultiplier = 1f,
                DamageMultiplier = 1f,
                MoveSpeedMultiplier = 1f,
                AttackRangeMultiplier = 1f,
                ScaleMultiplier = 1f,
                IsBoss = archetype.Rank == EnemyRank.Boss,
                IsObjectiveTarget = false
            };
        }
    }
}
