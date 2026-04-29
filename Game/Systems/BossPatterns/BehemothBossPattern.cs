using System;
using My2DEngine.Game;

namespace My2DEngine.Game.Systems
{
    internal sealed class BehemothBossPattern : IEnemyBossPattern
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
                (healthRatio <= enemy.AiProfile.UltimateHealthThreshold + 0.08f ||
                 distance >= enemy.AiProfile.SpecialMinDistance * 1.15f) &&
                distance <= enemy.AiProfile.SpecialMaxDistance + 2.5f)
            {
                actionPlan = new EnemyBossActionPlan(EnemyBossActionKind.BehemothSiegeBurst, 1.04f);
                return true;
            }

            if (enemy.SpecialCooldown <= 0f &&
                distance <= enemy.AiProfile.SpecialMaxDistance + 0.6f &&
                facingDot >= -0.35f)
            {
                actionPlan = new EnemyBossActionPlan(EnemyBossActionKind.BehemothShockwave, 0.95f);
                return true;
            }

            return false;
        }
    }
}
