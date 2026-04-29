using System;
using My2DEngine.Game;

namespace My2DEngine.Game.Systems
{
    internal sealed class ArachnocortexBossPattern : IEnemyBossPattern
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
                (healthRatio <= enemy.AiProfile.UltimateHealthThreshold ||
                 distance >= enemy.AiProfile.PreferredCombatDistance - 0.4f))
            {
                actionPlan = new EnemyBossActionPlan(EnemyBossActionKind.ArachnocortexSuppressionGrid, 1.12f);
                return true;
            }

            float minDistance = Math.Max(2.8f, enemy.AiProfile.SpecialMinDistance * 0.85f);
            float maxDistance = enemy.AiProfile.SpecialMaxDistance + 2f;
            if (enemy.SpecialCooldown <= 0f &&
                distance >= minDistance &&
                distance <= maxDistance &&
                facingDot >= -0.45f)
            {
                actionPlan = new EnemyBossActionPlan(EnemyBossActionKind.ArachnocortexPlasmaFan, 0.9f);
                return true;
            }

            if (enemy.SpecialCooldown <= 0f &&
                distance > enemy.AttackRange + 2f &&
                rng.NextDouble() < 0.14)
            {
                actionPlan = new EnemyBossActionPlan(EnemyBossActionKind.ArachnocortexPlasmaFan, 0.86f);
                return true;
            }

            return false;
        }
    }
}
