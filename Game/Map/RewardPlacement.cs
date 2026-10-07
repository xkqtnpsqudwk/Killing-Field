using System;
using System.Drawing;

namespace My2DEngine.Game.Map
{
    /// <summary>
    /// 방 안에 보상 아이템을 놓을 자리를 찾는다. 맵 배열만 보고 계산한다.
    /// 놓을 수 있는 칸은 빈 칸(0)이면서 방 경계에서 2칸 이상 안쪽이다.
    /// 돌려주는 좌표는 칸 중심(칸 좌표 + 0.5)이다.
    /// </summary>
    public static class RewardPlacement
    {
        /// <summary>
        /// 방 중심에서 바깥으로 넓혀 가며 자리를 찾는다.
        /// 1) 주변 8칸까지 비어 있는 안전한 칸 → 2) 그냥 빈 칸 → 3) 방 중심.
        /// </summary>
        public static PointF FindRoomSpot(int[,] map, Rectangle bounds)
        {
            int centerX = bounds.Left + bounds.Width / 2;
            int centerY = bounds.Top + bounds.Height / 2;
            int maxRadius = Math.Max(bounds.Width, bounds.Height);

            if (TrySpiral(centerX, centerY, maxRadius, (x, y) => IsSafeTile(x, y, bounds, map), out PointF safe))
            {
                return safe;
            }

            if (TrySpiral(centerX, centerY, maxRadius, (x, y) => IsWalkableTile(x, y, bounds, map), out PointF walkable))
            {
                return walkable;
            }

            return new PointF(centerX + 0.5f, centerY + 0.5f);
        }

        /// <summary>카드 상점 3지선다가 놓일 왼쪽·가운데·오른쪽 자리. 막혀 있으면 가까운 빈 칸으로 옮긴다.</summary>
        public static PointF[] FindShopSpots(int[,] map, Rectangle bounds)
        {
            int centerX = bounds.Left + bounds.Width / 2;
            int centerY = bounds.Top + bounds.Height / 2;
            int spacing = Math.Max(2, bounds.Width / 6);

            return new[]
            {
                FindNearestSpot(map, centerX - spacing, centerY, bounds),
                FindNearestSpot(map, centerX, centerY, bounds),
                FindNearestSpot(map, centerX + spacing, centerY, bounds)
            };
        }

        /// <summary>원하는 칸 주변에서 가장 가까운 빈 칸. 맵이 없으면 원하는 칸 그대로.</summary>
        public static PointF FindNearestSpot(int[,] map, int preferredX, int preferredY, Rectangle bounds)
        {
            if (map == null)
            {
                return new PointF(preferredX + 0.5f, preferredY + 0.5f);
            }

            int maxRadius = Math.Max(bounds.Width, bounds.Height);
            if (TrySpiral(preferredX, preferredY, maxRadius, (x, y) => IsWalkableTile(x, y, bounds, map), out PointF spot))
            {
                return spot;
            }

            return FindRoomSpot(map, bounds);
        }

        /// <summary>중심에서 반경을 하나씩 늘리며 테두리 칸만 검사한다.</summary>
        private static bool TrySpiral(int centerX, int centerY, int maxRadius, Func<int, int, bool> accept, out PointF spot)
        {
            for (int radius = 0; radius <= maxRadius; radius++)
            {
                for (int y = centerY - radius; y <= centerY + radius; y++)
                {
                    for (int x = centerX - radius; x <= centerX + radius; x++)
                    {
                        if (Math.Abs(x - centerX) != radius && Math.Abs(y - centerY) != radius)
                        {
                            continue;
                        }

                        if (accept(x, y))
                        {
                            spot = new PointF(x + 0.5f, y + 0.5f);
                            return true;
                        }
                    }
                }
            }

            spot = default;
            return false;
        }

        /// <summary>칸 자체와 주변 8칸이 모두 놓을 수 있는 칸인지.</summary>
        private static bool IsSafeTile(int x, int y, Rectangle bounds, int[,] map)
        {
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if (!IsWalkableTile(x + offsetX, y + offsetY, bounds, map))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsWalkableTile(int x, int y, Rectangle bounds, int[,] map)
        {
            if (map == null)
            {
                return false;
            }

            if (x <= bounds.Left + 1 || x >= bounds.Right - 2 || y <= bounds.Top + 1 || y >= bounds.Bottom - 2)
            {
                return false;
            }

            if (x < 0 || y < 0 || x >= map.GetLength(0) || y >= map.GetLength(1))
            {
                return false;
            }

            return map[x, y] == 0;
        }
    }
}
