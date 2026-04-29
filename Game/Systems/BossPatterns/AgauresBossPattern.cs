using System;
using My2DEngine.Game;

namespace My2DEngine.Game.Systems
{
    internal sealed class AgauresBossPattern : IEnemyBossPattern
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
                (healthRatio <= enemy.AiProfile.UltimateHealthThreshold + 0.1f ||
                 distance > enemy.AiProfile.SpecialMinDistance + 0.6f))
            {
                actionPlan = new EnemyBossActionPlan(EnemyBossActionKind.AgauresRiftCrossfire, 1f);
                return true;
            }

            if (enemy.SpecialCooldown <= 0f &&
                distance >= 1.4f &&
                distance <= enemy.AiProfile.SpecialMaxDistance + 0.8f)
            {
                actionPlan = new EnemyBossActionPlan(EnemyBossActionKind.AgauresBlinkClaw, 0.72f);
                return true;
            }

            if (enemy.SpecialCooldown <= 0f &&
                facingDot < -0.15f &&
                rng.NextDouble() < 0.2)
            {
                actionPlan = new EnemyBossActionPlan(EnemyBossActionKind.AgauresBlinkClaw, 0.68f);
                return true;
            }

            return false;
        }
    }
}
