using System;
using My2DEngine.Engine.Math;
using My2DEngine.Game.Config;

namespace My2DEngine.Game.Systems
{
    /// <summary>
    /// 맵 타일 기준 충돌·이동·시야 판정을 담당하는 시스템이다.
    /// 플레이어와 적 모두 이 클래스를 통해 이동을 시도하며, 결과에 따라 위치가 갱신된다.
    /// 맵 배열을 읽기 전용으로 보유하므로 생성 후 맵이 바뀌면 새 인스턴스를 만들어야 한다.
    /// </summary>
    public class CollisionSystem
    {
        /// <summary>충돌 검사에 사용하는 맵 타일 유형 2D 배열(읽기 전용 참조).</summary>
        private readonly int[,] map;

        /// <summary>맵 가로 크기(타일). 범위 검사에 사용한다.</summary>
        private readonly int mapWidth;

        /// <summary>맵 세로 크기(타일). 범위 검사에 사용한다.</summary>
        private readonly int mapHeight;

        /// <summary>
        /// 타일별 바닥 높이 배열. null이면 모든 타일의 바닥을 0.0으로 간주한다.
        /// 계단 이동 판정에 사용된다.
        /// </summary>
        private readonly float[,] floorHeights;

        /// <summary>
        /// 주어진 맵 배열로 CollisionSystem을 초기화한다.
        /// null이면 모든 위치를 벽으로 처리한다.
        /// </summary>
        /// <param name="map">타일 유형 2D 배열</param>
        public CollisionSystem(int[,] map) : this(map, null)
        {
        }

        /// <summary>
        /// 주어진 맵 배열과 바닥 높이 배열로 CollisionSystem을 초기화한다.
        /// </summary>
        /// <param name="map">타일 유형 2D 배열</param>
        /// <param name="floorHeights">타일별 바닥 높이 2D 배열. null이면 모든 바닥을 0.0으로 간주한다.</param>
        public CollisionSystem(int[,] map, float[,] floorHeights)
        {
            this.map = map;
            this.floorHeights = floorHeights;
            mapWidth = map == null ? 0 : map.GetLength(0);
            mapHeight = map == null ? 0 : map.GetLength(1);
        }

        /// <summary>
        /// 월드 좌표의 바닥 높이를 반환한다. 범위 밖이거나 floorHeights가 null이면 0.0.
        /// </summary>
        public float GetFloorHeightAt(float x, float y)
        {
            if (floorHeights == null) return 0f;
            int mapX = (int)x;
            int mapY = (int)y;
            if (mapX < 0 || mapY < 0 || mapX >= floorHeights.GetLength(0) || mapY >= floorHeights.GetLength(1)) return 0f;
            return floorHeights[mapX, mapY];
        }

        /// <summary>
        /// 현재 바닥 높이에서 목적지 바닥 높이로 올라갈 수 있는지 확인한다.
        /// 올라가는 단차가 MaxStepHeight 이하일 때만 허용한다. 내려가는 것은 항상 허용.
        /// </summary>
        private bool CanStep(float currentFloor, float destX, float destY)
        {
            float destFloor = GetFloorHeightAt(destX, destY);
            return (destFloor - currentFloor) <= GameConfig.MaxStepHeight;
        }

        /// <summary>
        /// 월드 좌표가 고체(벽) 타일 위에 있는지 확인한다.
        /// 맵 범위 밖이면 벽으로 간주해 true를 반환한다.
        /// </summary>
        /// <param name="x">월드 X 좌표(타일 단위)</param>
        /// <param name="y">월드 Y 좌표(타일 단위)</param>
        /// <returns>해당 위치가 고체 타일이면 true</returns>
        public bool IsWall(float x, float y)
        {
            if (map == null || mapWidth <= 0 || mapHeight <= 0)
            {
                return true;
            }

            int mapX = (int)x;
            int mapY = (int)y;

            if (mapX < 0 || mapY < 0 || mapX >= mapWidth || mapY >= mapHeight)
            {
                return true;
            }

            return IsSolidType(map[mapX, mapY]);
        }

        /// <summary>
        /// 타일 유형 번호가 고체(이동 불가) 타입인지 반환한다.
        /// 1~8번(일반 벽)과 9번(문 타일)이 고체로 처리된다.
        /// </summary>
        /// <param name="type">타일 유형 번호</param>
        /// <returns>고체 타입이면 true</returns>
        public static bool IsSolidType(int type)
        {
            return (type >= 1 && type <= 8) || type == GameConfig.DoorTileType;
        }

        /// <summary>
        /// 원(중심 + 반지름)이 맵의 고체 타일에 닿는지 확인한다.
        /// 4개 대각선 모서리 + 4방향 safeDist 연장 지점을 검사해 벽 파고드는 현상을 방지한다.
        /// </summary>
        /// <param name="x">원 중심 X 좌표</param>
        /// <param name="y">원 중심 Y 좌표</param>
        /// <param name="radius">원 반지름</param>
        /// <param name="safeDist">추가 안전 거리(벽과의 여유 거리)</param>
        /// <returns>벽과 충돌하면 true</returns>
        public bool IsWallRadius(float x, float y, float radius, float safeDist)
        {
            if (IsWall(x - radius, y - radius) ||
                IsWall(x + radius, y - radius) ||
                IsWall(x - radius, y + radius) ||
                IsWall(x + radius, y + radius))
            {
                return true;
            }

            if (IsWall(x - radius - safeDist, y) ||
                IsWall(x + radius + safeDist, y) ||
                IsWall(x, y - radius - safeDist) ||
                IsWall(x, y + radius + safeDist))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 플레이어를 (dx, dy)만큼 이동하면서 벽 충돌을 처리한다.
        /// 이동량이 크면 여러 단계로 분할해 빠른 이동에서도 벽 통과를 방지한다.
        /// X축과 Y축을 독립적으로 검사해 코너 슬라이딩이 자연스럽게 동작한다.
        /// </summary>
        /// <param name="player">이동할 플레이어 객체. Position이 직접 수정된다.</param>
        /// <param name="dx">이번 프레임 X 이동량(타일)</param>
        /// <param name="dy">이번 프레임 Y 이동량(타일)</param>
        public void TryMovePlayer(Player player, float dx, float dy)
        {
            // 한 단계에서 이동할 수 있는 최대 거리를 CollisionStepSize로 제한해
            // 빠른 이동에서도 벽 내부로 파고드는 현상을 방지한다.
            float maxDelta = Math.Max(Math.Abs(dx), Math.Abs(dy));
            int steps = Math.Max(1, (int)Math.Ceiling(maxDelta / GameConfig.CollisionStepSize));
            float stepX = dx / steps;
            float stepY = dy / steps;

            for (int i = 0; i < steps; i++)
            {
                float newX = player.Position.X + stepX;
                float newY = player.Position.Y + stepY;

                // X축 이동 가능하면 적용한다. 계단 단차도 확인한다.
                if (!IsWallRadius(newX, player.Position.Y, player.Radius, 0.1f) &&
                    CanStep(player.FloorZ, newX, player.Position.Y))
                {
                    player.Position = new Vector2(newX, player.Position.Y);
                }
                // Y축 이동 가능하면 적용한다. X와 독립적으로 처리해 코너 슬라이딩이 된다.
                if (!IsWallRadius(player.Position.X, newY, player.Radius, 0.1f) &&
                    CanStep(player.FloorZ, player.Position.X, newY))
                {
                    player.Position = new Vector2(player.Position.X, newY);
                }
            }

            // 이동 후 현재 타일의 바닥 높이를 플레이어에 적용한다.
            player.FloorZ = GetFloorHeightAt(player.Position.X, player.Position.Y);
        }

        /// <summary>
        /// 적을 (dx, dy)만큼 이동하면서 벽·플레이어 충돌을 처리한다.
        /// 플레이어와 최소 거리를 유지해 적이 플레이어 위로 겹치는 것을 방지한다.
        /// TryMovePlayer와 동일한 단계 분할 방식을 사용한다.
        /// </summary>
        /// <param name="enemy">이동할 적 객체. X·Y가 직접 수정된다.</param>
        /// <param name="dx">X 이동량(타일)</param>
        /// <param name="dy">Y 이동량(타일)</param>
        /// <param name="playerPosition">플레이어 현재 위치(적-플레이어 간 최소 거리 유지에 사용)</param>
        public void TryMoveEnemy(Enemy enemy, float dx, float dy, Vector2 playerPosition)
        {
            float maxDelta = Math.Max(Math.Abs(dx), Math.Abs(dy));
            int steps = Math.Max(1, (int)Math.Ceiling(maxDelta / GameConfig.CollisionStepSize));
            float stepX = dx / steps;
            float stepY = dy / steps;

            for (int i = 0; i < steps; i++)
            {
                float nextX = enemy.X + stepX;
                float nextY = enemy.Y + stepY;

                if (!IsBlockedForEnemy(enemy, nextX, enemy.Y, playerPosition))
                {
                    enemy.X = nextX;
                }
                if (!IsBlockedForEnemy(enemy, enemy.X, nextY, playerPosition))
                {
                    enemy.Y = nextY;
                }
            }
        }

        private bool IsBlockedForEnemy(Enemy enemy, float x, float y, Vector2 playerPosition)
        {
            if (IsWallRadius(x, y, enemy.Radius, 0.04f))
            {
                return true;
            }

            float minPlayerDist = GameConfig.PlayerRadius + enemy.Radius + 0.08f;
            float dxPlayer = x - playerPosition.X;
            float dyPlayer = y - playerPosition.Y;
            if ((dxPlayer * dxPlayer) + (dyPlayer * dyPlayer) < minPlayerDist * minPlayerDist)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 두 월드 좌표 사이에 벽이 없는지(시야가 통하는지) 선형 샘플링으로 확인한다.
        /// 거리가 클수록 더 많은 샘플 포인트를 검사한다(0.12타일 간격).
        /// </summary>
        /// <param name="x0">시작점 X</param>
        /// <param name="y0">시작점 Y</param>
        /// <param name="x1">끝점 X</param>
        /// <param name="y1">끝점 Y</param>
        /// <param name="distance">두 점 사이의 거리(타일). 샘플 수 계산에 사용된다.</param>
        /// <returns>시야가 통하면 true</returns>
        public bool HasLineOfSight(float x0, float y0, float x1, float y1, float distance)
        {
            int steps = Math.Max(2, (int)(distance / 0.12f));
            for (int i = 1; i < steps; i++)
            {
                float t = i / (float)steps;
                float sx = x0 + (x1 - x0) * t;
                float sy = y0 + (y1 - y0) * t;
                if (IsWall(sx, sy))
                {
                    return false;
                }
            }

            return true;
        }

    }
}
