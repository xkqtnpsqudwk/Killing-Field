using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using My2DEngine.Game.Config;
using My2DEngine.Game.Core;
using My2DEngine.Game.Rendering;
using My2DEngine.Game;
using My2DEngine.Game.Map;

namespace My2DEngine.SmokeTests
{
    /// <summary>
    /// 구조 리팩터링 뒤에도 최소 실행/로그라이크 시작 경로가 살아 있는지 확인하는 스모크 테스트 진입점이다.
    /// <para>
    /// 기본 상태와 로그라이크 런 시작 경로를 난이도별로 검증한다.
    /// <list type="bullet">
    ///   <item><description>부팅 직후 기본 맵 초기화 상태를 검증한다.</description></item>
        ///   <item><description>Easy/Normal/Hard 세 난이도로 로그라이크 런 시작 시 시작실+전투실 구조가 정상 생성되는지 검증한다.</description></item>
    /// </list>
    /// </para>
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// 스모크 테스트 진입점. 기본 상태와 로그라이크 시작 상태를 순서대로 검증한다.
        /// </summary>
        /// <param name="args">사용되지 않는 커맨드라인 인자 배열.</param>
        /// <returns>성공 시 0, 예외 발생 시 1을 반환한다.</returns>
        private static int Main(string[] args)
        {
            try
            {
                if (args != null &&
                    args.Length > 0 &&
                    string.Equals(args[0], "--balance-snapshot", StringComparison.OrdinalIgnoreCase))
                {
                    // --balance-snapshot [--csv <경로>]
                    var rows = BalanceSnapshotReporter.Write(Console.Out);
                    if (args.Length >= 3 && string.Equals(args[1], "--csv", StringComparison.OrdinalIgnoreCase))
                    {
                        BalanceSnapshotReporter.SaveCsv(args[2], rows);
                        Console.WriteLine("CSV saved: " + Path.GetFullPath(args[2]) + " (" + rows.Count + " rows)");
                    }

                    return 0;
                }

                if (args != null &&
                    args.Length >= 3 &&
                    string.Equals(args[0], "--balance-compare", StringComparison.OrdinalIgnoreCase))
                {
                    // --balance-compare <이전.csv> <이후.csv>. 차이가 없으면 0, 있으면 2를 돌려준다.
                    return BalanceSnapshotReporter.Compare(Console.Out, args[1], args[2]) ? 2 : 0;
                }

                RunBootSmokeCheck();
                RunEnemyVariantSmokeCheck();
                RunRemovedRecoveryPickupSmokeCheck();
                RunRoguelikeStartSmokeCheck(GameLogic.DifficultyPreset.Easy, "roguelike start easy");
                RunRoguelikeStartSmokeCheck(GameLogic.DifficultyPreset.Normal, "roguelike start normal");
                RunRoguelikeStartSmokeCheck(GameLogic.DifficultyPreset.Hard, "roguelike start hard");
                RunSaveContinueSmokeCheck();
                RunRoomObjectiveTemplateSmokeCheck();
                RunKeyTargetRevealSmokeCheck();
                RunRoomDifficultyRewardSmokeCheck();
                RunEnemyProgressionPoolSmokeCheck();
                RunBossTemplateUnlockSmokeCheck();
                RunRestShopCardOfferSmokeCheck();
                RunRestRoomBranchCooldownSmokeCheck();
                RunLifeStealStatSmokeCheck();
                RunExpandedStatCardSmokeCheck();
                RunDeathSummarySmokeCheck();
                RunAutoCannonProjectileSmokeCheck();
                RunNewSynergyCardsSmokeCheck();
                RunLuckCoinBonusSmokeCheck();
                RunMoveSpeedDashSynergySmokeCheck();
                RunLayoutBehaviorChecks();
                RunSeedDeterminismSmokeCheck();
                RunStatCardCatalogSmokeCheck();
                RunEnemyCatalogDataSmokeCheck();
                Console.WriteLine("Smoke checks passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        /// <summary>
        /// Game/Data/enemies.json이 모두 읽히고, 보스 패턴과 해금 풀이 가리키는 ID가 모두 있는지,
        /// 잘못된 데이터는 어느 적인지 알려 주며 거부되는지 검증한다.
        /// </summary>
        private static void RunEnemyCatalogDataSmokeCheck()
        {
            EnemyArchetype[] archetypes = EnemyCatalog.GetAllArchetypes();
            if (archetypes.Length != 21)
            {
                throw new InvalidOperationException("Smoke check failed for enemy catalog: expected 21 archetypes, got " + archetypes.Length + ".");
            }

            // 보스 패턴 등록과 방 템플릿 해금 풀은 코드에 ID 문자열로 남아 있으므로 JSON과 어긋나면 여기서 잡는다.
            string[] referencedIds =
            {
                "feral_alpha", "azazel", "abaddon", "bulwark_colossus", "behemoth", "annihilator", "aracnorb_queen",
                "ashen_artillerist", "arachnocortex", "afrit", "arachnobaron", "rift_strider", "agaures",
                "agatho_demon", "arachnophyte", "gunner", "uzi_trooper", "lab_butcher", "elite", "ruined_gunner",
                "plasma_tech", "grenadier_scientist", "blood_ghost", "beam_revenant", "hellion"
            };
            foreach (string id in referencedIds)
            {
                EnemyCatalog.Get(id);
            }

            string valid = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Game", "Data", "enemies.json"));
            if (EnemyCatalog.ValidateJson(valid) != archetypes.Length)
            {
                throw new InvalidOperationException("Smoke check failed for enemy catalog: re-reading enemies.json gave a different count.");
            }

            AssertEnemyCatalogRejects(valid.Replace("\"SightRange\": 11.5", "\"SightRangeTypo\": 11.5"), "SightRangeTypo", "unknown AI override");
            AssertEnemyCatalogRejects(valid.Replace("\"movementPattern\": \"ZombieGunner\"", "\"movementPattern\": \"Zombie\""), "\"Zombie\"", "bad enum value");
            AssertEnemyCatalogRejects(valid.Replace("\"defaultForType\": \"Gunner\",", string.Empty), "Gunner", "missing default archetype");
        }

        private static void AssertEnemyCatalogRejects(string json, string expectedInMessage, string scenario)
        {
            try
            {
                EnemyCatalog.ValidateJson(json);
            }
            catch (InvalidDataException ex)
            {
                if (!ex.Message.Contains(expectedInMessage))
                {
                    throw new InvalidOperationException(
                        "Smoke check failed for enemy catalog (" + scenario + "): error message does not mention '" + expectedInMessage + "': " + ex.Message);
                }

                return;
            }

            throw new InvalidOperationException("Smoke check failed for enemy catalog (" + scenario + "): invalid data was accepted.");
        }

        /// <summary>
        /// 모든 StatType에 이름·짧은 이름·표시 형식이 있고, 조건부 카드에는 발동 조건이 있는지 검증한다.
        /// 새 스탯 카드를 넣고 정의를 빠뜨리면 여기서 걸린다.
        /// </summary>
        private static void RunStatCardCatalogSmokeCheck()
        {
            var shortNames = new HashSet<string>();
            foreach (StatType stat in Enum.GetValues(typeof(StatType)))
            {
                StatCardDefinition definition = StatCardCatalog.Get(stat);
                if (definition.Stat != stat ||
                    string.IsNullOrWhiteSpace(definition.Name) ||
                    string.IsNullOrWhiteSpace(definition.ShortName))
                {
                    throw new InvalidOperationException("Smoke check failed for stat card catalog: " + stat + " has an incomplete definition.");
                }

                if (!shortNames.Add(definition.ShortName))
                {
                    throw new InvalidOperationException("Smoke check failed for stat card catalog: short name '" + definition.ShortName + "' is used twice.");
                }

                bool conditional = stat >= StatType.ShieldedDamage;
                if (conditional != (definition.Condition != null))
                {
                    throw new InvalidOperationException(
                        "Smoke check failed for stat card catalog: " + stat + (conditional ? " needs" : " must not have") + " a trigger condition.");
                }

                float cap = definition.Cap;
                if (!float.IsPositiveInfinity(cap) && Math.Abs(definition.Clamp(cap + 1f) - cap) > 0.0001f)
                {
                    throw new InvalidOperationException("Smoke check failed for stat card catalog: " + stat + " clamp does not respect its cap.");
                }
            }
        }

        /// <summary>
        /// 같은 런 시드면 상점 카드와 첫 층 맵이 똑같이 나오는지 검증한다.
        /// 밸런스 스냅샷 비교와 버그 재현이 이 성질에 기대므로 깨지면 바로 알려야 한다.
        /// </summary>
        private static void RunSeedDeterminismSmokeCheck()
        {
            const int seed = 4242;
            var first = new GameLogic(seed);
            var second = new GameLogic(seed);
            if (first.RunSeed != seed || second.RunSeed != seed)
            {
                throw new InvalidOperationException("Smoke check failed for run seed: fixed seed was not applied.");
            }

            string firstOffers = DescribeOffers(first.CreateRestShopCardOfferSmokeSnapshot(5f));
            string secondOffers = DescribeOffers(second.CreateRestShopCardOfferSmokeSnapshot(5f));
            if (firstOffers != secondOffers)
            {
                throw new InvalidOperationException(
                    "Smoke check failed for run seed: shop offers differ (" + firstOffers + " vs " + secondOffers + ").");
            }

            first.StartRoguelikeRun();
            second.StartRoguelikeRun();
            if (first.RunSeed != seed || !MapsEqual(first.CreateSmokeSnapshot().Map, second.CreateSmokeSnapshot().Map))
            {
                throw new InvalidOperationException("Smoke check failed for run seed: first floor maps differ for the same seed.");
            }
        }

        private static string DescribeOffers(RewardCardOffer[] offers)
        {
            if (offers == null)
            {
                return "none";
            }

            var parts = new List<string>();
            for (int i = 0; i < offers.Length; i++)
            {
                parts.Add(offers[i] == null ? "null" : offers[i].Grade + " " + offers[i].StatType);
            }

            return string.Join(", ", parts);
        }

        private static bool MapsEqual(int[,] a, int[,] b)
        {
            if (a == null || b == null || a.GetLength(0) != b.GetLength(0) || a.GetLength(1) != b.GetLength(1))
            {
                return false;
            }

            for (int x = 0; x < a.GetLength(0); x++)
            {
                for (int y = 0; y < a.GetLength(1); y++)
                {
                    if (a[x, y] != b[x, y])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// 기본 부팅 상태를 검증한다.
        /// 시작 시점에는 기본 맵이 로드되어 있어야 하지만 스테이지 흐름은 비활성 상태여야 한다.
        /// </summary>
        private static void RunBootSmokeCheck()
        {
            var world = new GameLogic();
            GameLogicSmokeValidator.Validate(world, "boot");

            GameLogicSmokeSnapshot snapshot = world.CreateSmokeSnapshot();
            if (snapshot.HasStageFlow)
            {
                throw new InvalidOperationException("Smoke check failed for boot: stage flow should be disabled.");
            }

            if (snapshot.StageRoomCount != 0)
            {
                throw new InvalidOperationException("Smoke check failed for boot: expected 0 rooms, got " + snapshot.StageRoomCount + ".");
            }
        }

        /// <summary>
        /// 적 카탈로그에 선언된 기본 variant 및 variant pool이 모두 실제 스프라이트로 등록되는지 검증한다.
        /// </summary>
        private static void RunEnemyVariantSmokeCheck()
        {
            using (var textures = new TextureManager())
            {
                textures.LoadAllTextures();

                EnemyArchetype[] archetypes = EnemyCatalog.GetAllArchetypes();
                for (int i = 0; i < archetypes.Length; i++)
                {
                    EnemyArchetype archetype = archetypes[i];
                    EnsureVariantLoaded(textures, archetype.AssetId, archetype.SpriteVariantKey);

                    string[] variantPool = archetype.SpriteVariantKeyPool;
                    for (int variantIndex = 0; variantIndex < variantPool.Length; variantIndex++)
                    {
                        EnsureVariantLoaded(textures, archetype.AssetId, variantPool[variantIndex]);
                    }
                }

                EnsureImageBackedEnemyVariant(textures, "uzi_trooper", "zombie_scientist_uzi", 6, 6, 4, 3);
                EnsureImageBackedEnemyVariant(textures, "plasma_tech", "zombie_scientist_plasma", 6, 6, 4, 3);
                EnsureImageBackedEnemyVariant(textures, "grenadier_scientist", "zombie_scientist_grenade", 6, 6, 4, 3);
                EnsureImageBackedEnemyVariant(textures, "lab_butcher", "zombie_scientist_cleaver", 6, 6, 4, 3);
            }
        }

        private static void EnsureVariantLoaded(TextureManager textures, string assetId, string spriteVariantKey)
        {
            if (string.IsNullOrWhiteSpace(spriteVariantKey))
            {
                return;
            }

            if (!textures.HasEnemySpriteVariant(spriteVariantKey))
            {
                throw new InvalidOperationException(
                    "Smoke check failed for enemy variants: asset '" + assetId +
                    "' is missing sprite variant '" + spriteVariantKey + "'.");
            }
        }

        private static void EnsureImageBackedEnemyVariant(
            TextureManager textures,
            string assetId,
            string spriteVariantKey,
            int idleFrames,
            int moveFrames,
            int attackFrames,
            int deathFrames)
        {
            EnemyArchetype archetype = EnemyCatalog.Get(assetId);
            EnsureVariantLoaded(textures, assetId, assetId);
            EnsureVariantLoaded(textures, assetId, spriteVariantKey);

            Color[] sprite = textures.GetEnemySprite(assetId, archetype.Definition.Type);
            if (sprite == null || sprite.Length == 0)
            {
                throw new InvalidOperationException(
                    "Smoke check failed for image-backed enemy variants: asset '" +
                    assetId + "' did not return a loaded image sprite.");
            }

            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Game", "Images", "Enemy", assetId);
            if (!Directory.Exists(root))
            {
                throw new InvalidOperationException(
                    "Smoke check failed for image-backed enemy variants: copied image folder is missing for asset '" +
                    assetId + "' at '" + root + "'.");
            }

            EnsureFrameCount(root, assetId, "IDLE", idleFrames);
            EnsureFrameCount(root, assetId, "MOVE", moveFrames);
            EnsureFrameCount(root, assetId, "ATTACK", attackFrames);
            EnsureFrameCount(root, assetId, "DEATH", deathFrames);
        }

        private static void EnsureFrameCount(string root, string assetId, string action, int expectedCount)
        {
            string directory = Path.Combine(root, action);
            int actualCount = Directory.Exists(directory)
                ? Directory.GetFiles(directory, "*.png", SearchOption.TopDirectoryOnly).Length
                : 0;
            if (actualCount != expectedCount)
            {
                throw new InvalidOperationException(
                    "Smoke check failed for image-backed enemy variants: asset '" + assetId +
                    "' expected " + expectedCount + " " + action + " frames, got " + actualCount + ".");
            }
        }

        /// <summary>
        /// 체력팩/스팀팩 픽업 종류가 보상 시스템에서 제거됐고, 남은 픽업 스프라이트가 로드되는지 검증한다.
        /// </summary>
        private static void RunRemovedRecoveryPickupSmokeCheck()
        {
            string[] names = Enum.GetNames(typeof(RewardPickupKind));
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], "HealthPack", StringComparison.Ordinal) ||
                    string.Equals(names[i], "StimPack", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Smoke check failed for removed recovery pickups: " + names[i] + " is still present.");
                }
            }

            using (var textures = new TextureManager())
            {
                textures.LoadAllTextures();
                if (textures.GetPickupSprite(RewardPickupKind.AmmoPack) == null ||
                    textures.GetPickupSprite(RewardPickupKind.Coin) == null ||
                    textures.GetPickupSprite(RewardPickupKind.Card) == null)
                {
                    throw new InvalidOperationException("Smoke check failed for removed recovery pickups: remaining pickup sprites did not load.");
                }
            }
        }

        /// <summary>
        /// 지정된 난이도 프리셋으로 로그라이크 런 시작 경로를 검증한다.
        /// 기본 불변식 검증 외에 스테이지 흐름 활성화 여부, 방 개수(시작실+전투실 2개), 문 개수(입구+출구 2개)를 확인한다.
        /// 조건을 만족하지 못하면 <see cref="InvalidOperationException"/>을 발생시킨다.
        /// </summary>
        /// <param name="preset">검사에 사용할 난이도 프리셋.</param>
        /// <param name="scenarioName">오류 메시지에 포함될 시나리오 이름.</param>
        private static void RunRoguelikeStartSmokeCheck(GameLogic.DifficultyPreset preset, string scenarioName)
        {
            var world = new GameLogic();
            world.SetDifficultyPreset(preset);
            world.StartRoguelikeRun();
            GameLogicSmokeValidator.Validate(world, scenarioName);

            GameLogicSmokeSnapshot snapshot = world.CreateSmokeSnapshot();
            if (!snapshot.HasStageFlow)
            {
                throw new InvalidOperationException("Smoke check failed for " + scenarioName + ": stage flow is disabled.");
            }

            const int expectedRoomCount = 2;
            if (snapshot.StageRoomCount != expectedRoomCount)
            {
                throw new InvalidOperationException("Smoke check failed for " + scenarioName + ": expected " + expectedRoomCount + " rooms, got " + snapshot.StageRoomCount + ".");
            }

            const int expectedDoorCount = 2;
            int actualDoorCount = CountTiles(snapshot.Map, WorldConfig.DoorTileType);
            if (actualDoorCount != expectedDoorCount)
            {
                throw new InvalidOperationException("Smoke check failed for " + scenarioName + ": expected " + expectedDoorCount + " doors, got " + actualDoorCount + ".");
            }
        }

        /// <summary>
        /// 로그라이크 전투 템플릿 풀에 생존/열쇠/위험 방이 실제로 포함되는지 검증한다.
        /// </summary>
        private static void RunRoomObjectiveTemplateSmokeCheck()
        {
            var rng = new Random(3817);
            bool sawSurvival = false;
            bool sawKeyTarget = false;
            bool sawToxicMist = false;
            bool sawSupplyShortage = false;

            for (int i = 0; i < 600; i++)
            {
                RoomTemplate template = RoomTemplateLibrary.SelectForFloor(5 + i % 12, rng, bossClearGrowthCount: 1);
                if (template.IsRestRoom || template.IsBossRoom)
                {
                    continue;
                }

                if (template.ObjectiveKind == RoomObjectiveKind.Survive)
                {
                    sawSurvival = true;
                    if (template.ObjectiveDuration <= 0f)
                    {
                        throw new InvalidOperationException("Smoke check failed for room objectives: survival room has no duration.");
                    }
                }

                if (template.ObjectiveKind == RoomObjectiveKind.KeyTarget)
                {
                    sawKeyTarget = true;
                    StageSpawnPoint targetSpawn = FindObjectiveTargetSpawn(template.Spawns);
                    if (targetSpawn == null)
                    {
                        throw new InvalidOperationException("Smoke check failed for room objectives: key room has no key target spawn.");
                    }

                    AssertKeyTargetSpawnReworked(targetSpawn);
                }

                if (template.HazardKind == RoomHazardKind.ToxicMist)
                {
                    sawToxicMist = true;
                }

                if (template.HazardKind == RoomHazardKind.SupplyShortage)
                {
                    sawSupplyShortage = true;
                }
            }

            if (!sawSurvival || !sawKeyTarget || !sawToxicMist || !sawSupplyShortage)
            {
                throw new InvalidOperationException(
                    "Smoke check failed for room objectives: expected survival, key target, toxic mist, and supply shortage rooms in generated templates.");
            }

            for (int i = 0; i < 80; i++)
            {
                RoomTemplate template = RoomTemplateLibrary.SelectForFloor(5 + i % 12, rng, bossClearGrowthCount: 1, allowRestRoom: false);
                if (template.IsRestRoom)
                {
                    throw new InvalidOperationException("Smoke check failed for room objectives: rest room was selected while rest rooms were disallowed.");
                }
            }
        }

        private static StageSpawnPoint FindObjectiveTargetSpawn(StageSpawnPoint[] spawns)
        {
            if (spawns == null)
            {
                return null;
            }

            for (int i = 0; i < spawns.Length; i++)
            {
                if (spawns[i]?.IsObjectiveTarget == true)
                {
                    return spawns[i];
                }
            }

            return null;
        }

        private static void AssertKeyTargetSpawnReworked(StageSpawnPoint targetSpawn)
        {
            if (targetSpawn.HealthMultiplier < RoomConfig.KeyTargetHealthMultiplier)
            {
                throw new InvalidOperationException(
                    "Smoke check failed for room objectives: key target health multiplier is too low.");
            }

            if (Math.Abs(targetSpawn.ScaleMultiplier - RoomConfig.KeyTargetScaleMultiplier) > 0.001f)
            {
                throw new InvalidOperationException(
                    "Smoke check failed for room objectives: key target should not use scale as an obvious visual marker.");
            }
        }

        /// <summary>
        /// 런 저장/이어하기 경로가 현재 카드 스탯 직렬화 스키마와 함께 동작하는지 검증한다.
        /// </summary>
        private static void RunSaveContinueSmokeCheck()
        {
            var world = new GameLogic();
            try
            {
                world.StartRoguelikeRun();
                world.SaveRunProgress();
                if (!world.HasRunSave())
                {
                    throw new InvalidOperationException("Smoke check failed for run save: saved run was not detected.");
                }

                world.ContinueRun();
                GameLogicSmokeValidator.Validate(world, "continue saved run");
            }
            finally
            {
                world.DeleteRunProgress();
            }
        }

        private static void RunKeyTargetRevealSmokeCheck()
        {
            var enemy = new Enemy(
                0f,
                0f,
                new EnemyDefinition
                {
                    Type = EnemyType.Gunner,
                    Scale = 1f,
                    MaxHealth = 100f,
                    MoveSpeed = 1f,
                    AttackRange = 1f,
                    AttackDamage = 1f,
                    AttackCooldownDuration = 1f,
                    Radius = 0.2f
                },
                new[] { Color.Red });

            enemy.IsObjectiveTarget = true;
            if (enemy.ObjectiveTargetRevealed)
            {
                throw new InvalidOperationException("Smoke check failed for key target reveal: target started revealed.");
            }

            enemy.TakeDamage(19f);
            if (enemy.ObjectiveTargetRevealed)
            {
                throw new InvalidOperationException("Smoke check failed for key target reveal: target revealed before threshold.");
            }

            enemy.TakeDamage(1.1f);
            if (!enemy.ObjectiveTargetRevealed)
            {
                throw new InvalidOperationException("Smoke check failed for key target reveal: target did not reveal after threshold.");
            }

            enemy.Reset();
            if (enemy.ObjectiveTargetRevealed)
            {
                throw new InvalidOperationException("Smoke check failed for key target reveal: reset target stayed revealed.");
            }
        }

        /// <summary>
        /// 방 레이아웃/목표/위험도에 비례해 카드 보상 등급 보정이 붙고, 극한 방은 최고 가능 등급+1을 보장하는지 검증한다.
        /// </summary>
        private static void RunRoomDifficultyRewardSmokeCheck()
        {
            var world = new GameLogic();

            RoomTemplate lowRisk = new RoomTemplate
            {
                LayoutVariant = RoomLayoutVariant.Open,
                ObjectiveKind = RoomObjectiveKind.EliminateAll,
                HazardKind = RoomHazardKind.None,
                Spawns = new[] { new StageSpawnPoint(), new StageSpawnPoint(), new StageSpawnPoint() }
            };

            if (RoomTemplateLibrary.GetRoomRiskScore(lowRisk) >= RoomTemplateLibrary.HighRoomRiskScore ||
                world.GetRoomRewardGradeBoostSmokeSnapshot(lowRisk) != 0 ||
                world.GetExtremeRoomMinimumRewardGradeSmokeSnapshot(lowRisk, 0f) != -1)
            {
                throw new InvalidOperationException("Smoke check failed for room difficulty rewards: low-risk room received a grade reward.");
            }

            RoomTemplate highRisk = new RoomTemplate
            {
                LayoutVariant = RoomLayoutVariant.SplitLanes,
                ObjectiveKind = RoomObjectiveKind.Survive,
                HazardKind = RoomHazardKind.None,
                ObjectiveDuration = 20f,
                Spawns = new[] { new StageSpawnPoint(), new StageSpawnPoint(), new StageSpawnPoint() }
            };

            if (RoomTemplateLibrary.GetRoomRiskScore(highRisk) != RoomTemplateLibrary.HighRoomRiskScore ||
                world.GetRoomRewardGradeBoostSmokeSnapshot(highRisk) != 1 ||
                RoomTemplateLibrary.IsExtremeRewardRoom(highRisk))
            {
                throw new InvalidOperationException("Smoke check failed for room difficulty rewards: high-risk room grade boost is incorrect.");
            }

            RoomTemplate extremeRisk = new RoomTemplate
            {
                LayoutVariant = RoomLayoutVariant.CenterWall,
                ObjectiveKind = RoomObjectiveKind.Survive,
                HazardKind = RoomHazardKind.ToxicMist,
                ObjectiveDuration = 24f,
                Spawns = new[] { new StageSpawnPoint(), new StageSpawnPoint(), new StageSpawnPoint() }
            };

            if (!RoomTemplateLibrary.IsExtremeRewardRoom(extremeRisk))
            {
                throw new InvalidOperationException("Smoke check failed for room difficulty rewards: extreme room was not classified as extreme.");
            }

            int minimumGrade = world.GetExtremeRoomMinimumRewardGradeSmokeSnapshot(extremeRisk, 0f);
            if (minimumGrade != (int)CardGrade.Green)
            {
                throw new InvalidOperationException("Smoke check failed for room difficulty rewards: expected extreme room minimum grade Green, got " + (CardGrade)minimumGrade + ".");
            }
        }

        /// <summary>
        /// 보스 클리어 전에는 신규 적이 잠겨 있고, 보스 클리어 단계가 오를수록 중후반 적이 실제 템플릿에 등장하는지 검증한다.
        /// </summary>
        private static void RunEnemyProgressionPoolSmokeCheck()
        {
            var rng = new Random(9207);
            var lockedBeforeFirstBoss = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "plasma_tech",
                "grenadier_scientist",
                "blood_ghost",
                "beam_revenant",
                "hellion"
            };
            var earlySeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < 180; i++)
            {
                RoomTemplate template = RoomTemplateLibrary.SelectForFloor(15, rng, bossClearGrowthCount: 0, allowRestRoom: false);
                AddSpawnAssets(template.Spawns, earlySeen);
                if (ContainsAnySpawnAsset(template.Spawns, lockedBeforeFirstBoss))
                {
                    throw new InvalidOperationException("Smoke check failed for enemy progression: post-boss enemies appeared before the first boss clear.");
                }
            }

            if (!earlySeen.Contains("uzi_trooper") || !earlySeen.Contains("lab_butcher"))
            {
                throw new InvalidOperationException("Smoke check failed for enemy progression: early expanded pool did not produce Uzi Trooper and Lab Butcher.");
            }

            var tierOneSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < 360; i++)
            {
                RoomTemplate template = RoomTemplateLibrary.SelectForFloor(21 + i % 12, rng, bossClearGrowthCount: 1, allowRestRoom: false);
                AddSpawnAssets(template.Spawns, tierOneSeen);
            }

            if (!tierOneSeen.Contains("plasma_tech") ||
                !tierOneSeen.Contains("grenadier_scientist") ||
                !tierOneSeen.Contains("blood_ghost") ||
                !tierOneSeen.Contains("beam_revenant"))
            {
                throw new InvalidOperationException("Smoke check failed for enemy progression: first boss unlock tier did not produce Plasma Tech, Grenadier, Blood Ghost, and Beam Revenant.");
            }

            var tierTwoSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < 360; i++)
            {
                RoomTemplate template = RoomTemplateLibrary.SelectForFloor(41 + i % 15, rng, bossClearGrowthCount: 2, allowRestRoom: false);
                AddSpawnAssets(template.Spawns, tierTwoSeen);
            }

            if (!tierTwoSeen.Contains("hellion"))
            {
                throw new InvalidOperationException("Smoke check failed for enemy progression: second boss unlock tier did not produce Hellion.");
            }
        }

        /// <summary>
        /// 보스 방 템플릿이 보스 클리어 단계에 따라 기본 보스에서 신규 보스 풀까지 확장되는지 검증한다.
        /// </summary>
        private static void RunBossTemplateUnlockSmokeCheck()
        {
            var rng = new Random(7071);
            var baseBosses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "feral_alpha",
                "bulwark_colossus",
                "ashen_artillerist",
                "rift_strider"
            };
            var tierOneBosses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "abaddon",
                "afrit",
                "agatho_demon",
                "arachnobaron"
            };
            var tierTwoBosses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "annihilator",
                "arachnophyte",
                "aracnorb_queen"
            };

            for (int i = 0; i < 80; i++)
            {
                string assetId = GetFirstSpawnAsset(RoomTemplateLibrary.SelectForFloor(20, rng, bossClearGrowthCount: 0).Spawns);
                if (!baseBosses.Contains(assetId))
                {
                    throw new InvalidOperationException("Smoke check failed for boss unlocks: locked boss '" + assetId + "' appeared in the first boss pool.");
                }
            }

            bool sawTierOneBoss = false;
            for (int i = 0; i < 160; i++)
            {
                string assetId = GetFirstSpawnAsset(RoomTemplateLibrary.SelectForFloor(40, rng, bossClearGrowthCount: 1).Spawns);
                sawTierOneBoss |= tierOneBosses.Contains(assetId);
                if (tierTwoBosses.Contains(assetId))
                {
                    throw new InvalidOperationException("Smoke check failed for boss unlocks: late boss '" + assetId + "' appeared after only one boss clear.");
                }
            }

            if (!sawTierOneBoss)
            {
                throw new InvalidOperationException("Smoke check failed for boss unlocks: first boss unlock tier did not produce any newly unlocked boss.");
            }

            bool sawTierTwoBoss = false;
            for (int i = 0; i < 200; i++)
            {
                string assetId = GetFirstSpawnAsset(RoomTemplateLibrary.SelectForFloor(60, rng, bossClearGrowthCount: 2).Spawns);
                sawTierTwoBoss |= tierTwoBosses.Contains(assetId);
            }

            if (!sawTierTwoBoss)
            {
                throw new InvalidOperationException("Smoke check failed for boss unlocks: second boss unlock tier did not produce any late boss.");
            }
        }

        /// <summary>
        /// 휴식 상점이 Luck으로 현재 출현 가능한 최고 등급보다 한 단계 높은 카드 3장을 중복 없이 생성하는지 검증한다.
        /// </summary>
        private static void RunRestShopCardOfferSmokeCheck()
        {
            AssertRestShopCardOffers(0f, CardGrade.Green, "luck level 0");
            AssertRestShopCardOffers(5.0f, CardGrade.Purple, "luck level 4");
            AssertRestShopCardOffers(PlayerConfig.PermanentLuckMax, CardGrade.Red, "max luck");

            var world = new GameLogic();
            int greenCost = world.GetRestShopCardCostSmokeSnapshot(CardGrade.Green);
            int redCost = world.GetRestShopCardCostSmokeSnapshot(CardGrade.Red);
            if (greenCost != RewardConfig.RestShopCardBaseCost + (int)CardGrade.Green * RewardConfig.RestShopCardCostPerGrade ||
                redCost != RewardConfig.RestShopCardBaseCost + (int)CardGrade.Red * RewardConfig.RestShopCardCostPerGrade)
            {
                throw new InvalidOperationException("Smoke check failed for rest shop card offers: card costs do not match grade scaling.");
            }
        }

        private static void AssertRestShopCardOffers(float luckValue, CardGrade expectedGrade, string scenarioName)
        {
            var world = new GameLogic();
            RewardCardOffer[] offers = world.CreateRestShopCardOfferSmokeSnapshot(luckValue);
            if (offers == null || offers.Length != 3)
            {
                throw new InvalidOperationException(
                    "Smoke check failed for rest shop card offers (" + scenarioName +
                    "): expected 3 offers, got " + (offers == null ? 0 : offers.Length) + ".");
            }

            var seenStats = new HashSet<StatType>();
            for (int i = 0; i < offers.Length; i++)
            {
                RewardCardOffer offer = offers[i];
                if (offer == null || offer.IsWeaponCard)
                {
                    throw new InvalidOperationException(
                        "Smoke check failed for rest shop card offers (" + scenarioName +
                        "): shop offer " + i + " is not a stat card.");
                }

                if (offer.Grade != expectedGrade)
                {
                    throw new InvalidOperationException(
                        "Smoke check failed for rest shop card offers (" + scenarioName +
                        "): expected " + expectedGrade + ", got " + offer.Grade + ".");
                }

                if (!seenStats.Add(offer.StatType))
                {
                    throw new InvalidOperationException(
                        "Smoke check failed for rest shop card offers (" + scenarioName +
                        "): duplicate stat card " + offer.StatType + ".");
                }
            }
        }

        /// <summary>
        /// 카드 상점이 실제 입장뿐 아니라 분기 후보 노출만으로도 다음 생성 기회에서 제외되는지 검증한다.
        /// </summary>
        private static void RunRestRoomBranchCooldownSmokeCheck()
        {
            var world = new GameLogic();
            if (!world.CanOfferRestRoomForNextSelectionSmokeSnapshot(currentFloorAllowsRestRoom: true, restRoomCooldownActive: false))
            {
                throw new InvalidOperationException("Smoke check failed for rest room branch cooldown: rest room should be allowed without cooldown.");
            }

            if (world.CanOfferRestRoomForNextSelectionSmokeSnapshot(currentFloorAllowsRestRoom: true, restRoomCooldownActive: true))
            {
                throw new InvalidOperationException("Smoke check failed for rest room branch cooldown: previous branch rest offer did not block the next rest room.");
            }

            if (world.CanOfferRestRoomForNextSelectionSmokeSnapshot(currentFloorAllowsRestRoom: false, restRoomCooldownActive: false))
            {
                throw new InvalidOperationException("Smoke check failed for rest room branch cooldown: rest room was allowed directly after a rest room.");
            }

            if (!world.BranchIncludesRestRoomSmokeSnapshot(optionAIsRestRoom: true, optionBIsRestRoom: false) ||
                !world.BranchIncludesRestRoomSmokeSnapshot(optionAIsRestRoom: false, optionBIsRestRoom: true) ||
                world.BranchIncludesRestRoomSmokeSnapshot(optionAIsRestRoom: false, optionBIsRestRoom: false))
            {
                throw new InvalidOperationException("Smoke check failed for rest room branch cooldown: branch rest-room detection is incorrect.");
            }
        }

        /// <summary>
        /// 모든 피해 흡혈 스탯이 0~100%로 고정되고 독안개에서 절반으로 감소하는지 검증한다.
        /// </summary>
        private static void RunLifeStealStatSmokeCheck()
        {
            var world = new GameLogic();

            if (!world.IsStatInRewardPoolSmokeSnapshot(StatType.LifeSteal))
            {
                throw new InvalidOperationException("Smoke check failed for life steal: stat is missing from the reward card pool.");
            }

            if (world.ClampRunStatBonusSmokeSnapshot(StatType.LifeSteal, -0.25f) != 0f)
            {
                throw new InvalidOperationException("Smoke check failed for life steal: negative value was not clamped to zero.");
            }

            if (world.ClampRunStatBonusSmokeSnapshot(StatType.LifeSteal, 1.25f) != 1f)
            {
                throw new InvalidOperationException("Smoke check failed for life steal: value above 100% was not capped.");
            }

            if (Math.Abs(world.GetLifeStealRatioSmokeSnapshot(0.40f, toxicMistPenaltyActive: false) - 0.40f) > 0.0001f)
            {
                throw new InvalidOperationException("Smoke check failed for life steal: normal ratio is incorrect.");
            }

            if (Math.Abs(world.GetLifeStealRatioSmokeSnapshot(0.40f, toxicMistPenaltyActive: true) - 0.20f) > 0.0001f)
            {
                throw new InvalidOperationException("Smoke check failed for life steal: toxic mist penalty did not halve the ratio.");
            }

            float healed = world.ApplyLifeStealSmokeSnapshot(
                startingHealth: 40f,
                maxHealth: 100f,
                lifeStealBonus: 0.25f,
                dealtDamage: 80f,
                toxicMistPenaltyActive: false);
            if (Math.Abs(healed - 60f) > 0.0001f)
            {
                throw new InvalidOperationException("Smoke check failed for life steal: expected health 60, got " + healed + ".");
            }

            float toxicHealed = world.ApplyLifeStealSmokeSnapshot(
                startingHealth: 40f,
                maxHealth: 100f,
                lifeStealBonus: 0.25f,
                dealtDamage: 80f,
                toxicMistPenaltyActive: true);
            if (Math.Abs(toxicHealed - 50f) > 0.0001f)
            {
                throw new InvalidOperationException("Smoke check failed for life steal: expected toxic health 50, got " + toxicHealed + ".");
            }
        }

        /// <summary>
        /// 확장 스탯 카드와 기본 보호막이 전투, 상점, 카드 선택지 계산에 연결되는지 검증한다.
        /// </summary>
        private static void RunExpandedStatCardSmokeCheck()
        {
            var world = new GameLogic();

            StatType[] expectedStats =
            {
                StatType.DamageReduction,
                StatType.ShopDiscount,
                StatType.KillHeal,
                StatType.KillDashCooldownRefund,
                StatType.CriticalChance,
                StatType.CardChoiceBonus,
                StatType.ShieldRegenRate,
                StatType.ShieldRegenDelayReduction,
                StatType.ShieldedDamage,
                StatType.DashStrikeDamage
            };

            for (int i = 0; i < expectedStats.Length; i++)
            {
                if (!world.IsStatInRewardPoolSmokeSnapshot(expectedStats[i]))
                {
                    throw new InvalidOperationException("Smoke check failed for expanded stat cards: " + expectedStats[i] + " is missing from the reward pool.");
                }
            }

            AssertNearlyEqual(0.65f, world.ClampRunStatBonusSmokeSnapshot(StatType.DamageReduction, 1f), "damage reduction cap");
            AssertNearlyEqual(0.50f, world.ClampRunStatBonusSmokeSnapshot(StatType.ShopDiscount, 1f), "shop discount cap");
            AssertNearlyEqual(0.10f, world.ClampRunStatBonusSmokeSnapshot(StatType.KillHeal, 1f), "kill heal cap");
            AssertNearlyEqual(0.25f, world.ClampRunStatBonusSmokeSnapshot(StatType.KillDashCooldownRefund, 1f), "kill dash refund cap");
            AssertNearlyEqual(1f, world.ClampRunStatBonusSmokeSnapshot(StatType.CriticalChance, 1.5f), "critical chance cap");
            AssertNearlyEqual(1f, world.ClampRunStatBonusSmokeSnapshot(StatType.CardChoiceBonus, 2f), "card choice cap");
            AssertNearlyEqual(19f, world.ClampRunStatBonusSmokeSnapshot(StatType.ShieldRegenRate, 99f), "shield regen rate cap");
            AssertNearlyEqual(0.80f, world.ClampRunStatBonusSmokeSnapshot(StatType.ShieldRegenDelayReduction, 1f), "shield regen delay cap");
            AssertNearlyEqual(RewardConfig.ShieldedDamageBonusCap, world.ClampRunStatBonusSmokeSnapshot(StatType.ShieldedDamage, 1f), "shielded damage cap");
            AssertNearlyEqual(RewardConfig.DashStrikeDamageBonusCap, world.ClampRunStatBonusSmokeSnapshot(StatType.DashStrikeDamage, 1f), "dash strike damage cap");

            if (world.IsStatOfferAvailableSmokeSnapshot(StatType.CardChoiceBonus, floor: 5, cardChoiceOffered: false, currentBonus: 0f))
            {
                throw new InvalidOperationException("Smoke check failed for expanded stat cards: card choice bonus appeared on a low floor.");
            }

            if (!world.IsStatOfferAvailableSmokeSnapshot(StatType.CardChoiceBonus, floor: 10, cardChoiceOffered: false, currentBonus: 0f))
            {
                throw new InvalidOperationException("Smoke check failed for expanded stat cards: card choice bonus did not become available after the low-floor gate.");
            }

            if (world.IsStatOfferAvailableSmokeSnapshot(StatType.CardChoiceBonus, floor: 10, cardChoiceOffered: true, currentBonus: 0f))
            {
                throw new InvalidOperationException("Smoke check failed for expanded stat cards: card choice bonus reappeared after being offered.");
            }

            if (world.GetCardRewardOfferSlotCountSmokeSnapshot(0f) != 3 ||
                world.GetCardRewardOfferSlotCountSmokeSnapshot(1f) != 4)
            {
                throw new InvalidOperationException("Smoke check failed for expanded stat cards: card reward slot count is incorrect.");
            }

            int redCost = world.GetRestShopCardCostSmokeSnapshot(CardGrade.Red);
            int discountedRedCost = world.GetRestShopCardCostSmokeSnapshot(CardGrade.Red, 0.50f);
            if (discountedRedCost != Math.Max(1, (int)Math.Ceiling(redCost * 0.5f)))
            {
                throw new InvalidOperationException("Smoke check failed for expanded stat cards: shop discount did not apply to card cost.");
            }

            PlayerDamageSmokeResult shieldOnly = world.ApplyIncomingDamageSmokeSnapshot(
                startingHealth: 100f,
                maxHealth: 100f,
                startingShield: 100f,
                damage: 50f,
                damageReduction: 0f);
            AssertNearlyEqual(100f, shieldOnly.Health, "shield absorbs health damage");
            AssertNearlyEqual(50f, shieldOnly.Shield, "shield remaining after damage");

            PlayerDamageSmokeResult reducedDamage = world.ApplyIncomingDamageSmokeSnapshot(
                startingHealth: 100f,
                maxHealth: 100f,
                startingShield: 100f,
                damage: 50f,
                damageReduction: 0.50f);
            AssertNearlyEqual(75f, reducedDamage.Shield, "damage reduction before shield");

            PlayerDamageSmokeResult shieldBreak = world.ApplyIncomingDamageSmokeSnapshot(
                startingHealth: 100f,
                maxHealth: 100f,
                startingShield: 20f,
                damage: 50f,
                damageReduction: 0f);
            AssertNearlyEqual(70f, shieldBreak.Health, "leftover shield damage reaches health");
            AssertNearlyEqual(0f, shieldBreak.Shield, "shield breaks to zero");

            KillBonusSmokeResult killBonus = world.ApplyKillBonusesSmokeSnapshot(
                startingHealth: 40f,
                maxHealth: 100f,
                dashCooldownTimer: 8f,
                killHealBonus: 0.10f,
                killDashRefundBonus: 0.25f);
            AssertNearlyEqual(46f, killBonus.Health, "kill heal missing-health ratio");
            AssertNearlyEqual(6f, killBonus.DashCooldownTimer, "kill dash cooldown refund");

            ShieldSettingsSmokeResult shieldSettings = world.GetShieldSettingsSmokeSnapshot(
                regenRateBonus: 99f,
                regenDelayReduction: 0.90f);
            AssertNearlyEqual(PlayerConfig.PlayerShieldMaxRegenRate, shieldSettings.RegenRate, "shield regen rate max");
            AssertNearlyEqual(PlayerConfig.PlayerShieldMinRegenDelay, shieldSettings.RegenDelayDuration, "shield regen delay min");

            AssertNearlyEqual(100f, world.GetOutgoingDamageSmokeSnapshot(
                baseDamage: 100f,
                shield: 0f,
                shieldedDamageBonus: 0.20f,
                dashStrikeDamageBonus: 0f,
                dashStrikeWindowActive: false), "shielded damage inactive without shield");

            AssertNearlyEqual(120f, world.GetOutgoingDamageSmokeSnapshot(
                baseDamage: 100f,
                shield: 50f,
                shieldedDamageBonus: 0.20f,
                dashStrikeDamageBonus: 0f,
                dashStrikeWindowActive: false), "shielded damage active");

            AssertNearlyEqual(115f, world.GetOutgoingDamageSmokeSnapshot(
                baseDamage: 100f,
                shield: 0f,
                shieldedDamageBonus: 0f,
                dashStrikeDamageBonus: 0.15f,
                dashStrikeWindowActive: true), "dash strike damage active");

            AssertNearlyEqual(135f, world.GetOutgoingDamageSmokeSnapshot(
                baseDamage: 100f,
                shield: 50f,
                shieldedDamageBonus: 0.20f,
                dashStrikeDamageBonus: 0.15f,
                dashStrikeWindowActive: true), "conditional damage stacks additively");
        }

        private static void AssertNearlyEqual(float expected, float actual, string scenarioName)
        {
            if (Math.Abs(expected - actual) > 0.0001f)
            {
                throw new InvalidOperationException(
                    "Smoke check failed for " + scenarioName + ": expected " + expected + ", got " + actual + ".");
            }
        }

        private static void RunDeathSummarySmokeCheck()
        {
            var world = new GameLogic();
            RunSummarySnapshot summary = world.CreateRunSummarySmokeSnapshot(7, 23, 2, 125);
            if (summary.FloorReached != 7 ||
                summary.EnemiesKilled != 23 ||
                summary.BossesKilled != 2 ||
                summary.DurationSeconds < 124 ||
                summary.DurationSeconds > 126)
            {
                throw new InvalidOperationException("Smoke check failed for death summary: summary values are incorrect.");
            }

            RunSummarySnapshot clamped = world.CreateRunSummarySmokeSnapshot(-3, -5, -1, -20);
            if (clamped.FloorReached != 0 ||
                clamped.EnemiesKilled != 0 ||
                clamped.BossesKilled != 0 ||
                clamped.DurationSeconds != 0)
            {
                throw new InvalidOperationException("Smoke check failed for death summary: negative values were not clamped.");
            }

            RunSummarySnapshot ended = world.CreateEndedRunSummarySmokeSnapshot(8, 30, 3, 140, 600);
            if (ended.DurationSeconds != 140)
            {
                throw new InvalidOperationException("Smoke check failed for death summary: death-screen wait time changed run duration.");
            }
        }

        private static void AddSpawnAssets(StageSpawnPoint[] spawns, HashSet<string> destination)
        {
            if (spawns == null || destination == null)
            {
                return;
            }

            for (int i = 0; i < spawns.Length; i++)
            {
                string assetId = spawns[i]?.EnemyAssetId;
                if (!string.IsNullOrWhiteSpace(assetId))
                {
                    destination.Add(assetId);
                }
            }
        }

        private static bool ContainsAnySpawnAsset(StageSpawnPoint[] spawns, HashSet<string> candidates)
        {
            if (spawns == null || candidates == null || candidates.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < spawns.Length; i++)
            {
                string assetId = spawns[i]?.EnemyAssetId;
                if (!string.IsNullOrWhiteSpace(assetId) && candidates.Contains(assetId))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetFirstSpawnAsset(StageSpawnPoint[] spawns)
        {
            if (spawns == null || spawns.Length == 0 || string.IsNullOrWhiteSpace(spawns[0]?.EnemyAssetId))
            {
                throw new InvalidOperationException("Smoke check failed for boss unlocks: boss template has no spawn asset.");
            }

            return spawns[0].EnemyAssetId;
        }

        /// <summary>
        /// AutoCannon 기본 발사가 즉발 폭발이 아니라 플레이어 로켓 투사체를 생성하는지 검증한다.
        /// </summary>
        private static void RunAutoCannonProjectileSmokeCheck()
        {
            var world = new GameLogic();
            world.StartRoguelikeRun();
            world.SwitchWeapon(WeaponType.AutoCannon);
            world.FireButtonDown();
            world.Update();

            GameLogicSmokeSnapshot snapshot = world.CreateSmokeSnapshot();
            if (snapshot.PlayerProjectileCount != 1)
            {
                throw new InvalidOperationException(
                    "Smoke check failed for autocannon projectile: expected 1 player projectile, got " +
                    snapshot.PlayerProjectileCount + ".");
            }

            if (snapshot.FirstPlayerProjectileKind != EnemyProjectileKind.PlayerRocket)
            {
                throw new InvalidOperationException(
                    "Smoke check failed for autocannon projectile: expected PlayerRocket, got " +
                    snapshot.FirstPlayerProjectileKind + ".");
            }

            if (snapshot.FirstPlayerProjectileSpeedSquared <= 0f)
            {
                throw new InvalidOperationException("Smoke check failed for autocannon projectile: rocket velocity is zero.");
            }

            if (snapshot.FirstPlayerProjectileExplosionRadius <= WeaponConfig.RocketProjectileRadius)
            {
                throw new InvalidOperationException("Smoke check failed for autocannon projectile: explosion radius was not set.");
            }
        }

        /// <summary>
        /// 5종 신규 조건부 피해 카드가 카드 풀에 등록되고 조건별로 정확히 발동하는지 검증한다.
        /// </summary>
        private static void RunNewSynergyCardsSmokeCheck()
        {
            var world = new GameLogic();

            StatType[] newStats =
            {
                StatType.LowHealthRage,
                StatType.KillChain,
                StatType.ExplosiveSpecialist,
                StatType.LowAmmoRage,
                StatType.RapidFireChain
            };

            foreach (var stat in newStats)
            {
                if (!world.IsStatInRewardPoolSmokeSnapshot(stat))
                    throw new InvalidOperationException("Smoke check failed for new synergy cards: " + stat + " is missing from the reward pool.");
            }

            // LowHealthRage: 체력 29% — 발동, 체력 31% — 미발동
            AssertNearlyEqual(120f, world.GetNewConditionalDamageSmokeSnapshot(
                baseDamage: 100f, healthRatio: 0.29f, lowHealthRageBonus: 0.20f,
                killChainWindowActive: false, killChainBonus: 0f,
                currentWeaponType: WeaponType.AMPistol, explosiveSpecialistBonus: 0f,
                ammoRatio: 0.5f, lowAmmoRageBonus: 0f,
                hitStreak: 0, rapidFireChainBonus: 0f), "low health rage active");

            AssertNearlyEqual(100f, world.GetNewConditionalDamageSmokeSnapshot(
                baseDamage: 100f, healthRatio: 0.31f, lowHealthRageBonus: 0.20f,
                killChainWindowActive: false, killChainBonus: 0f,
                currentWeaponType: WeaponType.AMPistol, explosiveSpecialistBonus: 0f,
                ammoRatio: 0.5f, lowAmmoRageBonus: 0f,
                hitStreak: 0, rapidFireChainBonus: 0f), "low health rage inactive above threshold");

            // KillChain: 창 활성 — 발동, 창 비활성 — 미발동
            AssertNearlyEqual(115f, world.GetNewConditionalDamageSmokeSnapshot(
                baseDamage: 100f, healthRatio: 1f, lowHealthRageBonus: 0f,
                killChainWindowActive: true, killChainBonus: 0.15f,
                currentWeaponType: WeaponType.AMPistol, explosiveSpecialistBonus: 0f,
                ammoRatio: 0.5f, lowAmmoRageBonus: 0f,
                hitStreak: 0, rapidFireChainBonus: 0f), "kill chain active");

            AssertNearlyEqual(100f, world.GetNewConditionalDamageSmokeSnapshot(
                baseDamage: 100f, healthRatio: 1f, lowHealthRageBonus: 0f,
                killChainWindowActive: false, killChainBonus: 0.15f,
                currentWeaponType: WeaponType.AMPistol, explosiveSpecialistBonus: 0f,
                ammoRatio: 0.5f, lowAmmoRageBonus: 0f,
                hitStreak: 0, rapidFireChainBonus: 0f), "kill chain inactive without window");

            // ExplosiveSpecialist: AutoCannon — 발동, AMPistol — 미발동
            AssertNearlyEqual(125f, world.GetNewConditionalDamageSmokeSnapshot(
                baseDamage: 100f, healthRatio: 1f, lowHealthRageBonus: 0f,
                killChainWindowActive: false, killChainBonus: 0f,
                currentWeaponType: WeaponType.AutoCannon, explosiveSpecialistBonus: 0.25f,
                ammoRatio: 0.5f, lowAmmoRageBonus: 0f,
                hitStreak: 0, rapidFireChainBonus: 0f), "explosive specialist on autocannon");

            AssertNearlyEqual(100f, world.GetNewConditionalDamageSmokeSnapshot(
                baseDamage: 100f, healthRatio: 1f, lowHealthRageBonus: 0f,
                killChainWindowActive: false, killChainBonus: 0f,
                currentWeaponType: WeaponType.AMPistol, explosiveSpecialistBonus: 0.25f,
                ammoRatio: 0.5f, lowAmmoRageBonus: 0f,
                hitStreak: 0, rapidFireChainBonus: 0f), "explosive specialist inactive on non-explosive");

            // LowAmmoRage: 잔탄 10% — 발동, 잔탄 50% — 미발동
            AssertNearlyEqual(130f, world.GetNewConditionalDamageSmokeSnapshot(
                baseDamage: 100f, healthRatio: 1f, lowHealthRageBonus: 0f,
                killChainWindowActive: false, killChainBonus: 0f,
                currentWeaponType: WeaponType.AMPistol, explosiveSpecialistBonus: 0f,
                ammoRatio: 0.05f, lowAmmoRageBonus: 0.30f,
                hitStreak: 0, rapidFireChainBonus: 0f), "low ammo rage active");

            AssertNearlyEqual(100f, world.GetNewConditionalDamageSmokeSnapshot(
                baseDamage: 100f, healthRatio: 1f, lowHealthRageBonus: 0f,
                killChainWindowActive: false, killChainBonus: 0f,
                currentWeaponType: WeaponType.AMPistol, explosiveSpecialistBonus: 0f,
                ammoRatio: 0.50f, lowAmmoRageBonus: 0.30f,
                hitStreak: 0, rapidFireChainBonus: 0f), "low ammo rage inactive with sufficient ammo");

            // RapidFireChain: 스트릭 3 — 발동, 스트릭 2 — 미발동
            AssertNearlyEqual(110f, world.GetNewConditionalDamageSmokeSnapshot(
                baseDamage: 100f, healthRatio: 1f, lowHealthRageBonus: 0f,
                killChainWindowActive: false, killChainBonus: 0f,
                currentWeaponType: WeaponType.AMPistol, explosiveSpecialistBonus: 0f,
                ammoRatio: 0.5f, lowAmmoRageBonus: 0f,
                hitStreak: 3, rapidFireChainBonus: 0.10f), "rapid fire chain at min streak");

            AssertNearlyEqual(100f, world.GetNewConditionalDamageSmokeSnapshot(
                baseDamage: 100f, healthRatio: 1f, lowHealthRageBonus: 0f,
                killChainWindowActive: false, killChainBonus: 0f,
                currentWeaponType: WeaponType.AMPistol, explosiveSpecialistBonus: 0f,
                ammoRatio: 0.5f, lowAmmoRageBonus: 0f,
                hitStreak: 2, rapidFireChainBonus: 0.10f), "rapid fire chain below min streak");

            // 모든 조건 동시 발동: 합산 확인
            float allActive = world.GetNewConditionalDamageSmokeSnapshot(
                baseDamage: 100f, healthRatio: 0.20f, lowHealthRageBonus: 0.10f,
                killChainWindowActive: true, killChainBonus: 0.10f,
                currentWeaponType: WeaponType.AutoCannon, explosiveSpecialistBonus: 0.10f,
                ammoRatio: 0.05f, lowAmmoRageBonus: 0.10f,
                hitStreak: 5, rapidFireChainBonus: 0.10f);
            if (allActive < 149f || allActive > 151f)
                throw new InvalidOperationException("Smoke check failed for new synergy cards: all-active stacking expected ~150, got " + allActive + ".");

            // 카드 UI에 발동 조건 설명이 모든 조건부 카드에 채워지는지 검증한다.
            StatType[] conditionalStats =
            {
                StatType.ShieldedDamage, StatType.DashStrikeDamage,
                StatType.LowHealthRage, StatType.KillChain,
                StatType.ExplosiveSpecialist, StatType.LowAmmoRage, StatType.RapidFireChain
            };
            foreach (var stat in conditionalStats)
            {
                string desc = world.GetStatConditionTextSmokeSnapshot(stat);
                if (string.IsNullOrWhiteSpace(desc))
                    throw new InvalidOperationException("Smoke check failed for new synergy cards: " + stat + " is missing a condition description on the card UI.");
            }

            // 상시 발동 스탯은 조건 설명이 없어야 한다(불필요한 안내 방지).
            if (world.GetStatConditionTextSmokeSnapshot(StatType.MaxHealth) != null)
                throw new InvalidOperationException("Smoke check failed for new synergy cards: a non-conditional stat unexpectedly has a condition description.");
        }

        /// <summary>운 레벨에 따라 코인 드롭 확률 보너스가 선형으로 증가하는지 검증한다.</summary>
        private static void RunLuckCoinBonusSmokeCheck()
        {
            var world = new GameLogic();

            float lv0 = world.GetLuckCoinDropBonusSmokeSnapshot(0);
            float lv10 = world.GetLuckCoinDropBonusSmokeSnapshot(10);

            if (lv0 != 0f)
                throw new InvalidOperationException("Smoke check failed for luck coin bonus: lv0 should be 0, got " + lv0 + ".");

            if (Math.Abs(lv10 - RewardConfig.LuckCoinDropBonusMax) > 0.0001f)
                throw new InvalidOperationException("Smoke check failed for luck coin bonus: lv10 should be " + RewardConfig.LuckCoinDropBonusMax + ", got " + lv10 + ".");

            float lv5 = world.GetLuckCoinDropBonusSmokeSnapshot(5);
            if (Math.Abs(lv5 - RewardConfig.LuckCoinDropBonusMax * 0.5f) > 0.0001f)
                throw new InvalidOperationException("Smoke check failed for luck coin bonus: lv5 should be " + (RewardConfig.LuckCoinDropBonusMax * 0.5f) + ", got " + lv5 + ".");
        }

        /// <summary>이동 속도 영구 스탯 3포인트 이상일 때 대시 쿨다운 시너지가 활성화되는지 검증한다.</summary>
        private static void RunMoveSpeedDashSynergySmokeCheck()
        {
            var world = new GameLogic();

            if (world.GetMoveSpeedDashSynergySmokeSnapshot(2))
                throw new InvalidOperationException("Smoke check failed for move speed dash synergy: should not activate at 2 points.");

            if (!world.GetMoveSpeedDashSynergySmokeSnapshot(3))
                throw new InvalidOperationException("Smoke check failed for move speed dash synergy: should activate at 3 points.");

            if (!world.GetMoveSpeedDashSynergySmokeSnapshot(5))
                throw new InvalidOperationException("Smoke check failed for move speed dash synergy: should activate at 5 points.");
        }

        /// <summary>
        /// 방 레이아웃 기반 행동 패턴 편향이 적 아키타입이 지원하는 패턴 범위를 절대 벗어나지 않는지 검증한다.
        /// 모든 전투 방 스폰의 BehaviorPatternPool은 해당 아키타입 풀의 부분집합이어야 하며,
        /// 적어도 한 번은 레이아웃 편향으로 풀이 실제로 좁혀져야 한다(연계가 무력화되지 않았음을 보장).
        /// </summary>
        private static void RunLayoutBehaviorChecks()
        {
            bool sawNarrowedPool = false;

            for (int floor = 1; floor <= 60; floor++)
            {
                for (int seed = 0; seed < 8; seed++)
                {
                    RoomTemplate template = RoomTemplateLibrary.SelectForFloor(floor, new Random(floor * 131 + seed * 17 + 3));
                    if (template.IsBossRoom || template.IsRestRoom || template.Spawns == null)
                    {
                        continue;
                    }

                    foreach (StageSpawnPoint spawn in template.Spawns)
                    {
                        if (spawn == null || spawn.BehaviorPatternPool == null || spawn.BehaviorPatternPool.Length == 0)
                        {
                            continue;
                        }

                        EnemyArchetype archetype = EnemyCatalog.Resolve(spawn.EnemyAssetId, spawn.Type);
                        EnemyBehaviorPattern[] archetypePool = archetype.BehaviorPatternPool;
                        if (archetypePool == null || archetypePool.Length == 0)
                        {
                            throw new InvalidOperationException(
                                "Smoke check failed for layout behavior bias: spawn '" + spawn.EnemyAssetId +
                                "' has a behavior pool but its archetype has none.");
                        }

                        foreach (EnemyBehaviorPattern p in spawn.BehaviorPatternPool)
                        {
                            if (Array.IndexOf(archetypePool, p) < 0)
                            {
                                throw new InvalidOperationException(
                                    "Smoke check failed for layout behavior bias: pattern '" + p +
                                    "' is not supported by archetype '" + spawn.EnemyAssetId + "'.");
                            }
                        }

                        if (spawn.BehaviorPatternPool.Length < archetypePool.Length)
                        {
                            sawNarrowedPool = true;
                        }
                    }
                }
            }

            if (!sawNarrowedPool)
            {
                throw new InvalidOperationException(
                    "Smoke check failed for layout behavior bias: no spawn pool was ever narrowed — the wiring is inert.");
            }
        }

        /// <summary>
        /// 2차원 맵 배열에서 특정 타일 타입의 개수를 세어 반환한다.
        /// </summary>
        /// <param name="map">검색 대상 맵 배열. null이면 0을 반환한다.</param>
        /// <param name="tileType">개수를 셀 타일의 정수 타입 값.</param>
        /// <returns>맵 안에서 <paramref name="tileType"/>과 일치하는 타일의 총 개수.</returns>
        private static int CountTiles(int[,] map, int tileType)
        {
            if (map == null)
            {
                return 0;
            }

            int count = 0;
            for (int y = 0; y < map.GetLength(1); y++)
            {
                for (int x = 0; x < map.GetLength(0); x++)
                {
                    if (map[x, y] == tileType)
                    {
                        count++;
                    }
                }
            }

            return count;
        }
    }
}
