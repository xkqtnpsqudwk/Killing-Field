using System;
using My2DEngine.Engine.Math;
using My2DEngine.Game;
using My2DEngine.Game.Config;

namespace My2DEngine.Game.Systems
{
    /// <summary>
    /// 적 투사체(기본 원거리 탄환)를 담당하는 partial 클래스다.
    /// 보스 패턴 전용 투사체(산성/로켓/레이저)는 사용하지 않는다.
    /// </summary>
    public partial class EnemyManager
    {
        private void FireRangedVolley(Enemy enemy, float dirX, float dirY)
        {
            EnemyRangedAttackStyle attackStyle = enemy?.AiProfile?.RangedAttackStyle ?? EnemyRangedAttackStyle.None;
            if (enemy == null || attackStyle == EnemyRangedAttackStyle.None)
            {
                return;
            }

            PerformEnemyAttack(enemy);
            Normalize(ref dirX, ref dirY);
            if ((dirX * dirX) + (dirY * dirY) <= 0.001f)
            {
                return;
            }

            if (attackStyle == EnemyRangedAttackStyle.SpreadTriplet)
            {
                float spread = Math.Max(0.08f, enemy.AiProfile.ProjectileSpreadOffset);
                for (int i = 0; i < RuinedGunnerSpreads.Length; i++)
                {
                    float offset = RuinedGunnerSpreads[i] / 0.18f * spread;
                    FireEnemySpreadShot(enemy, dirX, dirY, offset, 0.62f);
                }

                return;
            }

            if (attackStyle == EnemyRangedAttackStyle.BurstPair)
            {
                float spread = Math.Max(0.04f, enemy.AiProfile.ProjectileSpreadOffset);
                for (int i = 0; i < BurstPairSpreads.Length; i++)
                {
                    float offset = BurstPairSpreads[i] / 0.08f * spread;
                    FireEnemySpreadShot(enemy, dirX, dirY, offset, 0.58f);
                }

                return;
            }

            if (attackStyle == EnemyRangedAttackStyle.WideFan)
            {
                float spread = Math.Max(0.14f, enemy.AiProfile.ProjectileSpreadOffset);
                for (int i = 0; i < WideFanSpreads.Length; i++)
                {
                    float offset = WideFanSpreads[i] / 0.34f * spread;
                    FireEnemySpreadShot(enemy, dirX, dirY, offset, 0.44f);
                }

                return;
            }

            float aimJitter = ((float)rng.NextDouble() - 0.5f) * 0.04f;
            FireEnemySpreadShot(enemy, dirX, dirY, aimJitter, 0.9f);
        }

        private void FireEnemyShot(Enemy enemy, float dirX, float dirY, float damageScale)
        {
            enemyProjectiles.Add(new EnemyProjectile
            {
                Kind = EnemyProjectileKind.EnemyShot,
                X = enemy.X + dirX * (enemy.Radius + 0.2f),
                Y = enemy.Y + dirY * (enemy.Radius + 0.2f),
                VelocityX = dirX * enemy.AiProfile.ProjectileSpeed,
                VelocityY = dirY * enemy.AiProfile.ProjectileSpeed,
                Radius = 0.12f,
                Damage = enemy.AttackDamage * damageScale,
                Lifetime = enemy.AiProfile.ProjectileLifetime,
                Active = true
            });
        }

        private void FireEnemySpreadShot(Enemy enemy, float dirX, float dirY, float sideOffset, float damageScale)
        {
            float sideX = -dirY;
            float sideY = dirX;
            float shotX = dirX + sideX * sideOffset;
            float shotY = dirY + sideY * sideOffset;
            Normalize(ref shotX, ref shotY);
            if ((shotX * shotX) + (shotY * shotY) <= 0.001f)
            {
                return;
            }

            FireEnemyShot(enemy, shotX, shotY, damageScale);
        }

        private void UpdateEnemyProjectiles(float dt, CollisionSystem collision, Vector2 playerPosition, Action<float, float, float> onPlayerDamaged)
        {
            float dtClamped = Math.Max(0f, dt);
            float playerX = playerPosition.X;
            float playerY = playerPosition.Y;
            float playerRadius = GameConfig.PlayerRadius;

            for (int i = enemyProjectiles.Count - 1; i >= 0; i--)
            {
                EnemyProjectile projectile = enemyProjectiles[i];
                if (projectile == null || !projectile.Active)
                {
                    enemyProjectiles.RemoveAt(i);
                    continue;
                }

                projectile.Lifetime -= dtClamped;
                if (projectile.Lifetime <= 0f)
                {
                    enemyProjectiles.RemoveAt(i);
                    continue;
                }

                projectile.X += projectile.VelocityX * dtClamped;
                projectile.Y += projectile.VelocityY * dtClamped;

                if (collision == null || collision.IsWallRadius(projectile.X, projectile.Y, projectile.Radius, 0.02f))
                {
                    enemyProjectiles.RemoveAt(i);
                    continue;
                }

                float dx = projectile.X - playerX;
                float dy = projectile.Y - playerY;
                float hitRadius = projectile.Radius + playerRadius;
                if ((dx * dx) + (dy * dy) <= hitRadius * hitRadius)
                {
                    onPlayerDamaged?.Invoke(projectile.Damage, projectile.X, projectile.Y);
                    enemyProjectiles.RemoveAt(i);
                }
            }
        }
    }
}
