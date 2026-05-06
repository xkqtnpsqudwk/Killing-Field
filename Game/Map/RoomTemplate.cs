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

        /// <summary>휴식 방 여부. 전투 대신 아이템 3개 중 하나를 고르는 룸이다.</summary>
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

        private const int NormalRoomMinSize = 16;
        private const int NormalRoomMaxSize = 22;

        private static readonly RoomTemplate[] s_restTemplates;
        private static readonly RoomTemplate[] s_bossTemplates;

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

        // 층 단계별 적 아이디 풀 (중복 항목으로 확률 조정)
        private static readonly string[][] s_enemyTierPools =
        {
            // 1~10층: 초반 3종 위주 (ZombieScientist 가중치 높음)
            new[] { "gunner", "gunner", "elite", "ruined_gunner" },
            // 11~20층: 신규 2종 추가
            new[] { "gunner", "elite", "ruined_gunner", "blood_ghost", "beam_revenant" },
            // 21층~: 6종 전부 등장
            new[] { "gunner", "elite", "ruined_gunner", "blood_ghost", "beam_revenant", "hellion" },
        };

        static RoomTemplateLibrary()
        {
            s_restTemplates = BuildRestTemplates();
            s_bossTemplates = BuildBossTemplates();
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
        /// <param name="bossClearGrowthCount">누적 보스 클리어 수. 스폰 상한을 높이는 데 사용된다.</param>
        /// <param name="allowRestRoom">false이면 휴식 방을 선택 풀에서 제외한다.</param>
        public static RoomTemplate SelectForFloor(int floor, Random rng, int bossClearGrowthCount = 0, bool allowRestRoom = true)
        {
            // 보스 방: 20층마다, 크기만 소폭 랜덤
            if (floor > 0 && floor % 20 == 0)
            {
                RoomTemplate bossBase = s_bossTemplates[rng.Next(s_bossTemplates.Length)];
                int bossSize = 29 + rng.Next(5);  // 29~33
                return new RoomTemplate
                {
                    TemplateId = bossBase.TemplateId,
                    RoomWidth = bossSize,
                    RoomHeight = bossSize,
                    IsBossRoom = true,
                    LayoutVariant = RoomLayoutVariant.Open,
                    ObjectiveKind = RoomObjectiveKind.EliminateAll,
                    HazardKind = RoomHazardKind.None,
                    Spawns = bossBase.Spawns
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
            StageSpawnPoint[] spawns = GenerateRandomSpawns(floor, isElite, roomW, roomH, spawnCap, rng);
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

        /// <summary>
        /// 층 번호·방 종류·크기에 맞춰 스폰 포인트 배열을 동적으로 생성한다.
        /// 적 종류는 층 단계 풀에서 무작위로 선택되며, 스폰 위치는 방 내부에 고르게 분산된다.
        /// </summary>
        private static StageSpawnPoint[] GenerateRandomSpawns(
            int floor, bool isElite, int roomW, int roomH, int spawnCap, Random rng)
        {
            // 층에 따라 적 수 증가 (3 → spawnCap 상한)
            int baseCount = 3 + (floor - 1) / 8;
            if (isElite) baseCount++;
            int count = Math.Min(spawnCap, Math.Max(3, baseCount));

            string[] pool = GetEnemyPool(floor);
            float halfW = (roomW - 4) * 0.5f;
            float halfH = (roomH - 4) * 0.5f;

            List<(float x, float y)> positions = GenerateSpreadPositions(count, halfW, halfH, rng);

            StageSpawnPoint[] spawns = new StageSpawnPoint[positions.Count];
            for (int i = 0; i < positions.Count; i++)
            {
                string assetId = pool[rng.Next(pool.Length)];
                (float x, float y) = positions[i];
                StageSpawnPoint sp = SpawnEnemy(assetId, x, y);
                if (isElite)
                {
                    sp.HealthMultiplier = EliteStatMultiplier;
                    sp.DamageMultiplier = EliteStatMultiplier;
                }
                spawns[i] = sp;
            }

            return spawns;
        }

        private static void MarkKeyTargetSpawn(StageSpawnPoint[] spawns, Random rng)
        {
            if (spawns == null || spawns.Length == 0)
            {
                return;
            }

            int index = rng.Next(spawns.Length);
            StageSpawnPoint spawn = spawns[index];
            if (spawn == null)
            {
                return;
            }

            spawn.IsObjectiveTarget = true;
            spawn.HealthMultiplier *= GameConfig.KeyTargetHealthMultiplier;
            spawn.ScaleMultiplier *= GameConfig.KeyTargetScaleMultiplier;
        }

        private static string[] GetEnemyPool(int floor)
        {
            if (floor <= 10) return s_enemyTierPools[0];
            if (floor <= 20) return s_enemyTierPools[1];
            return s_enemyTierPools[2];
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

        private static RoomTemplate[] BuildBossTemplates()
        {
            return new[]
            {
                BossRoom("Boss-FeralAlpha",       "feral_alpha"),
                BossRoom("Boss-BulwarkColossus",  "bulwark_colossus"),
                BossRoom("Boss-AshenArtillerist", "ashen_artillerist"),
                BossRoom("Boss-RiftStrider",      "rift_strider"),
            };
        }

        private static RoomTemplate BossRoom(string id, string assetId)
        {
            return new RoomTemplate
            {
                TemplateId = id,
                RoomWidth = 31,
                RoomHeight = 31,
                IsBossRoom = true,
                LayoutVariant = RoomLayoutVariant.Open,
                ObjectiveKind = RoomObjectiveKind.EliminateAll,
                HazardKind = RoomHazardKind.None,
                Spawns = new[] { SpawnEnemy(assetId, 0f, 0f) }
            };
        }

        private static StageSpawnPoint SpawnEnemy(string assetId, float x, float y)
        {
            EnemyArchetype archetype = EnemyCatalog.Get(assetId);
            EnemyBehaviorPattern[] patternPool = archetype.BehaviorPatternPool.Length > 0
                ? (EnemyBehaviorPattern[])archetype.BehaviorPatternPool.Clone()
                : null;
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
