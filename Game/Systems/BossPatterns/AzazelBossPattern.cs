using System;
using My2DEngine.Game;

namespace My2DEngine.Game.Systems
{
    internal sealed class AzazelBossPattern : IEnemyBossPattern
    {
        public bool TryBuildActionPlan(
            Enemy enemy,
            bool canSeePlayer,
            float distance,
            float facingDot,
            float playerX,
            float playerY,
            float dirX,
            float dirY,
            Random rng,
            out EnemyBossActionPlan actionPlan)
        {
            actionPlan = default;
            if (enemy == null || !canSeePlayer || distance <= 0f)
            {
                return false;
            }

            float healthRatio = enemy.MaxHealth > 0.001f ? enemy.Health / enemy.MaxHealth : 1f;
            if (enemy.UltimateCooldown <= 0f &&
                healthRatio <= Math.Max(0.18f, enemy.AiProfile.UltimateHealthThreshold) &&
                distance <= enemy.AiProfile.SpecialMaxDistance + 1.8f)
            {
                actionPlan = new EnemyBossActionPlan(EnemyBossActionKind.AzazelHellfireBarrage, 1.08f);
                return true;
            }

            float dashMinDistance = Math.Max(1.9f, enemy.AiProfile.SpecialMinDistance * 0.78f);
            float dashMaxDistance = enemy.AiProfile.SpecialMaxDistance + 0.9f;
            if (enemy.SpecialCooldown <= 0f &&
                distance >= dashMinDistance &&
                distance <= dashMaxDistance &&
                facingDot >= -0.25f)
            {
                actionPlan = new EnemyBossActionPlan(EnemyBossActionKind.AzazelInfernoDash, 0.82f);
                return true;
            }

            if (enemy.SpecialCooldown <= 0f &&
                distance > enemy.AttackRange + 1.25f &&
                rng.NextDouble() < 0.12)
            {
                actionPlan = new EnemyBossActionPlan(EnemyBossActionKind.AzazelInfernoDash, 0.78f);
                return true;
            }

            return false;
        }
    }
}
