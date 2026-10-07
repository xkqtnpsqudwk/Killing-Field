using System;
using My2DEngine.Game.Config;
using My2DEngine.Game.Core;
using My2DEngine.Game.Systems;

namespace My2DEngine.SmokeTests
{
    /// <summary>
    /// <see cref="GameLogic"/> 스냅샷을 읽어 생성·진행의 핵심 불변식을 검증하는 스모크 검증기.
    /// <para>
    /// 다음 항목을 순서대로 확인한다:
    /// <list type="number">
    ///   <item><description>맵이 null이 아니며 최소 3×3 이상인지</description></item>
    ///   <item><description>충돌 시스템이 초기화되었는지</description></item>
    ///   <item><description>벽 텍스처가 하나 이상 로드되었는지</description></item>
    ///   <item><description>플레이어 시작 위치가 맵 범위 안에 있는지</description></item>
    ///   <item><description>플레이어가 솔리드 타일 위에서 스폰되지 않는지</description></item>
    ///   <item><description>플레이어 방향 벡터가 단위 벡터인지 (길이 ≈ 1)</description></item>
    ///   <item><description>플레이어 시야각(FOV)이 허용 범위 내인지</description></item>
    /// </list>
    /// </para>
    /// </summary>
    internal static class GameLogicSmokeValidator
    {
        /// <summary>
        /// 주어진 <see cref="GameLogic"/> 인스턴스의 스냅샷을 기반으로 핵심 불변식을 검증한다.
        /// 검증에 실패하면 실패 원인을 설명하는 메시지와 함께 <see cref="InvalidOperationException"/>을 발생시킨다.
        /// </summary>
        /// <param name="world">검증 대상 게임 로직 인스턴스. null이면 <see cref="ArgumentNullException"/>이 발생한다.</param>
        /// <param name="scenarioName">
        /// 오류 메시지에 포함될 시나리오 이름. null이거나 공백인 경우 "unknown"으로 대체된다.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="world"/>가 null인 경우.</exception>
        /// <exception cref="InvalidOperationException">하나 이상의 불변식 검증에 실패한 경우.</exception>
        public static void Validate(GameLogic world, string scenarioName)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (string.IsNullOrWhiteSpace(scenarioName))
            {
                scenarioName = "unknown";
            }

            GameLogicSmokeSnapshot snapshot = world.CreateSmokeSnapshot();
            int[,] map = snapshot.Map ?? throw new InvalidOperationException("Smoke check failed for " + scenarioName + ": map is null.");
            if (map.GetLength(0) < 3 || map.GetLength(1) < 3)
            {
                throw new InvalidOperationException("Smoke check failed for " + scenarioName + ": map is too small.");
            }

            if (!snapshot.CollisionInitialized)
            {
                throw new InvalidOperationException("Smoke check failed for " + scenarioName + ": collision system is not initialized.");
            }

            if (snapshot.WallTextureCount <= 0)
            {
                throw new InvalidOperationException("Smoke check failed for " + scenarioName + ": textures are not loaded.");
            }

            int playerTileX = (int)snapshot.PlayerPosition.X;
            int playerTileY = (int)snapshot.PlayerPosition.Y;
            if (playerTileX < 0 || playerTileY < 0 || playerTileX >= map.GetLength(0) || playerTileY >= map.GetLength(1))
            {
                throw new InvalidOperationException("Smoke check failed for " + scenarioName + ": player start is outside the map.");
            }

            if (CollisionSystem.IsSolidType(map[playerTileX, playerTileY]))
            {
                throw new InvalidOperationException("Smoke check failed for " + scenarioName + ": player spawned inside a solid tile.");
            }

            float dirLenSq = (snapshot.PlayerDirection.X * snapshot.PlayerDirection.X) + (snapshot.PlayerDirection.Y * snapshot.PlayerDirection.Y);
            if (dirLenSq < 0.9f || dirLenSq > 1.1f)
            {
                throw new InvalidOperationException("Smoke check failed for " + scenarioName + ": player direction is not normalized.");
            }

            if (snapshot.PlayerFovDegrees < PlayerConfig.MinFovDegrees || snapshot.PlayerFovDegrees > PlayerConfig.MaxFovDegrees)
            {
                throw new InvalidOperationException("Smoke check failed for " + scenarioName + ": FOV is out of range.");
            }
        }
    }
}
