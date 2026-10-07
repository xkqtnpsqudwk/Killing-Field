using System;

namespace My2DEngine.Game.Systems
{
    /// <summary>
    /// 플레이어 공격의 대상 찾기. 적 배열과 충돌 판정만 보고 계산하며 상태를 바꾸지 않는다.
    /// </summary>
    internal static class CombatTargeting
    {
        /// <summary>
        /// 바라보는 방향 앞쪽 원뿔 안에서 보이는 가장 가까운 적.
        /// 거리가 멀수록 옆으로 벗어나도 조금 더 봐줘서 픽셀 단위 떨림으로 빗나가는 일을 줄인다.
        /// </summary>
        /// <param name="spreadRadius">허용하는 옆 거리(타일).</param>
        /// <param name="exclude">결과에서 뺄 적(다중 탄환이 같은 적을 겹쳐 맞히지 않게).</param>
        /// <param name="hitDistance">찾은 적까지 거리. 없으면 float.MaxValue.</param>
        public static Enemy FindInCone(Enemy[] enemies, CollisionSystem collision,
            float originX, float originY, float dirX, float dirY,
            float spreadRadius, float range, Enemy exclude, out float hitDistance)
        {
            hitDistance = float.MaxValue;
            if (enemies == null) return null;

            Enemy bestTarget = null;
            float bestDistance = float.MaxValue;

            foreach (Enemy enemy in enemies)
            {
                if (enemy == null || !enemy.Alive) continue;
                if (enemy == exclude) continue;

                float dx = enemy.X - originX;
                float dy = enemy.Y - originY;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);

                if (dist <= 0.001f || dist > range) continue;

                float forward = (dx * dirX + dy * dirY) / dist;
                if (forward < 0.75f) continue;

                // 옆 거리 = 바라보는 방향에 수직인 거리
                float lateral = Math.Abs(dx * dirY - dy * dirX);
                float allowed = spreadRadius + dist * 0.03f;
                if (lateral > allowed) continue;

                if (!collision.HasLineOfSight(originX, originY, enemy.X, enemy.Y, dist)) continue;

                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestTarget = enemy;
                }
            }

            hitDistance = bestDistance;
            return bestTarget;
        }

        /// <summary>점에서 maxRange 안의 가장 가까운 살아 있는 적(벽은 보지 않는다).</summary>
        public static Enemy FindNearest(Enemy[] enemies, float x, float y, float maxRange)
        {
            if (enemies == null)
            {
                return null;
            }

            Enemy nearest = null;
            float bestDistSq = maxRange * maxRange;

            for (int i = 0; i < enemies.Length; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy == null || !enemy.Alive)
                {
                    continue;
                }

                float dx = enemy.X - x;
                float dy = enemy.Y - y;
                float distSq = (dx * dx) + (dy * dy);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    nearest = enemy;
                }
            }

            return nearest;
        }

        /// <summary>원(중심 x, y, 반지름 radius)과 겹치는 첫 번째 살아 있는 적.</summary>
        public static Enemy FindTouching(Enemy[] enemies, float x, float y, float radius)
        {
            if (enemies == null)
            {
                return null;
            }

            for (int i = 0; i < enemies.Length; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy == null || !enemy.Alive)
                {
                    continue;
                }

                float dx = enemy.X - x;
                float dy = enemy.Y - y;
                float hitRadius = enemy.Radius + radius;
                if ((dx * dx) + (dy * dy) <= hitRadius * hitRadius)
                {
                    return enemy;
                }
            }

            return null;
        }
    }
}
