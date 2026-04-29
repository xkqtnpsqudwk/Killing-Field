using System;
using My2DEngine.Game.Config;
using My2DEngine.Game.Core;
using My2DEngine.Game.Rendering;
using My2DEngine.Game;

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
                RunBootSmokeCheck();
                RunEnemyVariantSmokeCheck();
                RunRoguelikeStartSmokeCheck(GameLogic.DifficultyPreset.Easy, "roguelike start easy");
                RunRoguelikeStartSmokeCheck(GameLogic.DifficultyPreset.Normal, "roguelike start normal");
                RunRoguelikeStartSmokeCheck(GameLogic.DifficultyPreset.Hard, "roguelike start hard");
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
            int actualDoorCount = CountTiles(snapshot.Map, GameConfig.DoorTileType);
            if (actualDoorCount != expectedDoorCount)
            {
                throw new InvalidOperationException("Smoke check failed for " + scenarioName + ": expected " + expectedDoorCount + " doors, got " + actualDoorCount + ".");
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
