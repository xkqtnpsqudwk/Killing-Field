using System;
using My2DEngine.Game.Config;

namespace My2DEngine.Game.Core
{
    /// <summary>
    /// GameLogic의 플레이어 투사체 처리 partial.
    /// 로켓 생성, 이동, 충돌, 폭발 피해, 비행 루프 사운드를 담당한다.
    /// </summary>
    public partial class GameLogic
    {
        private const float TrackingRocketRetargetRange = 12f;

        /// <summary>
        /// 플레이어 전방으로 Auto Cannon 로켓 투사체를 생성한다.
        /// </summary>
        private void SpawnPlayerRocket(float damage, float splashRadius)
        {
            float dirX = player.Direction.X;
            float dirY = player.Direction.Y;
            float dirLength = (float)Math.Sqrt((dirX * dirX) + (dirY * dirY));
            if (dirLength <= 0.001f)
            {
                return;
            }

            dirX /= dirLength;
            dirY /= dirLength;

            float radius = GameConfig.RocketProjectileRadius;
            FindRocketSpawnPosition(dirX, dirY, radius, out float startX, out float startY);

            float speed = Math.Max(0.1f, GameConfig.RocketProjectileSpeed);
            playerProjectiles.Add(new EnemyProjectile
            {
                Kind = EnemyProjectileKind.PlayerRocket,
                X = startX,
                Y = startY,
                VelocityX = dirX * speed,
                VelocityY = dirY * speed,
                Radius = radius,
                ExplosionRadius = Math.Max(radius, splashRadius),
                Damage = damage,
                Lifetime = Math.Min(GameConfig.RocketProjectileLifetime, weapon.Range / speed),
                Active = true
            });

            UpdateRocketFlyLoop();
        }

        /// <summary>
        /// 로켓이 플레이어 몸이나 벽에 겹쳐 시작하지 않도록 전방 후보 위치를 고른다.
        /// </summary>
        private void FindRocketSpawnPosition(float dirX, float dirY, float radius, out float startX, out float startY)
        {
            float[] offsets = { 0.42f, 0.34f, 0.26f, 0.18f, 0.10f };
            for (int i = 0; i < offsets.Length; i++)
            {
                float candidateX = player.Position.X + (dirX * offsets[i]);
                float candidateY = player.Position.Y + (dirY * offsets[i]);
                if (collision == null || !collision.IsWallRadius(candidateX, candidateY, radius, 0.02f))
                {
                    startX = candidateX;
                    startY = candidateY;
                    return;
                }
            }

            startX = player.Position.X;
            startY = player.Position.Y;
        }

        /// <summary>
        /// 활성 플레이어 로켓과 폭발 시각 효과를 한 프레임 갱신한다.
        /// </summary>
        /// <param name="dt">이번 프레임의 경과 시간(초).</param>
        private void UpdatePlayerProjectiles(float dt)
        {
            if (playerProjectiles.Count == 0)
            {
                UpdateRocketFlyLoop();
                return;
            }

            float dtClamped = Math.Max(0f, dt);
            for (int i = playerProjectiles.Count - 1; i >= 0; i--)
            {
                EnemyProjectile projectile = playerProjectiles[i];
                if (projectile == null || !projectile.Active)
                {
                    playerProjectiles.RemoveAt(i);
                    continue;
                }

                projectile.Lifetime -= dtClamped;

                if (projectile.Kind == EnemyProjectileKind.PlayerRocketExplosion)
                {
                    if (projectile.Lifetime <= 0f)
                    {
                        playerProjectiles.RemoveAt(i);
                    }

                    continue;
                }

                if (projectile.Lifetime <= 0f)
                {
                    ExplodePlayerRocket(projectile);
                    if (projectile.Kind != EnemyProjectileKind.PlayerRocketExplosion || projectile.Lifetime <= 0f)
                    {
                        playerProjectiles.RemoveAt(i);
                    }

                    continue;
                }

                MovePlayerRocket(projectile, dtClamped);
                if (!projectile.Active)
                {
                    playerProjectiles.RemoveAt(i);
                }
            }

            UpdateRocketFlyLoop();
        }

        /// <summary>
        /// 플레이어 로켓을 소형 스텝으로 전진시키며 벽/적 충돌을 검사한다.
        /// </summary>
        /// <param name="projectile">이동시킬 플레이어 로켓.</param>
        /// <param name="dt">이번 프레임의 경과 시간(초).</param>
        private void MovePlayerRocket(EnemyProjectile projectile, float dt)
        {
            UpdateTrackingRocket(projectile, dt);

            float moveX = projectile.VelocityX * dt;
            float moveY = projectile.VelocityY * dt;
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(moveX), Math.Abs(moveY)) / 0.08f));
            float stepX = moveX / steps;
            float stepY = moveY / steps;

            for (int step = 0; step < steps; step++)
            {
                projectile.X += stepX;
                projectile.Y += stepY;

                if (collision != null && collision.IsWallRadius(projectile.X, projectile.Y, projectile.Radius, 0.02f))
                {
                    ExplodePlayerRocket(projectile);
                    return;
                }

                Enemy hitEnemy = FindRocketHitEnemy(projectile);
                if (hitEnemy != null)
                {
                    projectile.X = hitEnemy.X;
                    projectile.Y = hitEnemy.Y;
                    ExplodePlayerRocket(projectile);
                    return;
                }
            }
        }

        /// <summary>
        /// 추적 로켓이면 현재 표적 방향으로 천천히 조향한다.
        /// </summary>
        private void UpdateTrackingRocket(EnemyProjectile projectile, float dt)
        {
            if (projectile == null || projectile.TurnRate <= 0f)
            {
                return;
            }

            Enemy target = projectile.TrackingTarget;
            if (target == null || !target.Alive)
            {
                target = FindNearestAliveEnemyFromPoint(projectile.X, projectile.Y, TrackingRocketRetargetRange);
                projectile.TrackingTarget = target;
            }

            if (target == null)
            {
                return;
            }

            float desiredX = target.X - projectile.X;
            float desiredY = target.Y - projectile.Y;
            float desiredLength = (float)Math.Sqrt((desiredX * desiredX) + (desiredY * desiredY));
            if (desiredLength <= 0.001f)
            {
                return;
            }

            desiredX /= desiredLength;
            desiredY /= desiredLength;

            float speed = (float)Math.Sqrt((projectile.VelocityX * projectile.VelocityX) + (projectile.VelocityY * projectile.VelocityY));
            if (speed <= 0.001f)
            {
                speed = GameConfig.RocketProjectileSpeed;
            }

            float currentX = projectile.VelocityX / speed;
            float currentY = projectile.VelocityY / speed;
            float blend = Math.Min(1f, projectile.TurnRate * dt);
            float newDirX = currentX + ((desiredX - currentX) * blend);
            float newDirY = currentY + ((desiredY - currentY) * blend);
            float newLength = (float)Math.Sqrt((newDirX * newDirX) + (newDirY * newDirY));
            if (newLength <= 0.001f)
            {
                return;
            }

            projectile.VelocityX = (newDirX / newLength) * speed;
            projectile.VelocityY = (newDirY / newLength) * speed;
        }

        /// <summary>
        /// 지정 좌표 기준으로 가장 가까운 생존 적을 찾는다.
        /// </summary>
        private Enemy FindNearestAliveEnemyFromPoint(float x, float y, float maxRange)
        {
            Enemy[] enemies = enemyManager.Enemies;
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

        /// <summary>
        /// 현재 로켓과 직접 충돌한 적을 찾는다.
        /// </summary>
        /// <param name="projectile">충돌 검사를 수행할 플레이어 로켓.</param>
        /// <returns>충돌한 적이 있으면 그 적, 없으면 null.</returns>
        private Enemy FindRocketHitEnemy(EnemyProjectile projectile)
        {
            Enemy[] enemies = enemyManager.Enemies;
            if (projectile == null || enemies == null)
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

                float dx = enemy.X - projectile.X;
                float dy = enemy.Y - projectile.Y;
                float hitRadius = enemy.Radius + projectile.Radius;
                if ((dx * dx) + (dy * dy) <= hitRadius * hitRadius)
                {
                    return enemy;
                }
            }

            return null;
        }

        /// <summary>
        /// 플레이어 로켓을 폭발 상태로 전환하고 범위 내 적에게 거리 감쇠 피해를 준다.
        /// </summary>
        /// <param name="projectile">폭발시킬 플레이어 로켓.</param>
        private void ExplodePlayerRocket(EnemyProjectile projectile)
        {
            if (projectile == null || !projectile.Active)
            {
                return;
            }

            float splashRadius = Math.Max(projectile.ExplosionRadius, projectile.Radius);
            Enemy[] enemies = enemyManager.Enemies;
            if (enemies != null)
            {
                for (int i = 0; i < enemies.Length; i++)
                {
                    Enemy enemy = enemies[i];
                    if (enemy == null || !enemy.Alive)
                    {
                        continue;
                    }

                    float dx = enemy.X - projectile.X;
                    float dy = enemy.Y - projectile.Y;
                    float centerDistance = (float)Math.Sqrt((dx * dx) + (dy * dy));
                    float effectiveDistance = Math.Max(0f, centerDistance - enemy.Radius);
                    if (effectiveDistance > splashRadius)
                    {
                        continue;
                    }

                    if (collision != null &&
                        centerDistance > 0.05f &&
                        !collision.HasLineOfSight(projectile.X, projectile.Y, enemy.X, enemy.Y, centerDistance))
                    {
                        continue;
                    }

                    float falloff = 1f - ((effectiveDistance / Math.Max(0.001f, splashRadius)) * 0.7f);
                    DamageEnemy(enemy, projectile.Damage * falloff);
                }
            }

            PlayWeaponEffectSound(GameConfig.RocketBoomSoundAlias, false);
            EmitEnemyAlertSound(projectile.X, projectile.Y, 15.5f);

            projectile.Kind = EnemyProjectileKind.PlayerRocketExplosion;
            projectile.VelocityX = 0f;
            projectile.VelocityY = 0f;
            projectile.Radius = Math.Min(Math.Max(0.42f, splashRadius * 0.42f), 1.35f);
            projectile.Damage = 0f;
            projectile.Lifetime = GameConfig.RocketExplosionVisualDuration;
            projectile.Active = true;
        }

        /// <summary>
        /// 플레이어 로켓이 하나라도 비행 중이면 Fly 루프를 유지하고, 없으면 정지한다.
        /// </summary>
        private void UpdateRocketFlyLoop()
        {
            bool hasFlyingRocket = false;
            for (int i = 0; i < playerProjectiles.Count; i++)
            {
                EnemyProjectile projectile = playerProjectiles[i];
                if (projectile != null &&
                    projectile.Active &&
                    projectile.Kind == EnemyProjectileKind.PlayerRocket)
                {
                    hasFlyingRocket = true;
                    break;
                }
            }

            if (hasFlyingRocket)
            {
                if (!rocketFlyLoopActive)
                {
                    PlayWeaponEffectSound(GameConfig.RocketFlySoundAlias, true, true);
                    rocketFlyLoopActive = true;
                }
            }
            else if (rocketFlyLoopActive)
            {
                StopWeaponEffectSound(GameConfig.RocketFlySoundAlias);
                rocketFlyLoopActive = false;
            }
        }
    }
}
