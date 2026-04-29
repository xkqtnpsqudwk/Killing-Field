using System;
using My2DEngine.Game;

namespace My2DEngine.Game.Systems
{
    internal enum EnemyBossActionKind
    {
        AzazelInfernoDash,
        AzazelHellfireBarrage,
        BehemothShockwave,
        BehemothSiegeBurst,
        ArachnocortexPlasmaFan,
        ArachnocortexSuppressionGrid,
        AgauresBlinkClaw,
        AgauresRiftCrossfire
    }

    internal readonly struct EnemyBossActionPlan
    {
        public EnemyBossActionPlan(EnemyBossActionKind actionKind, float windupScale = 1f)
        {
            ActionKind = actionKind;
            WindupScale = windupScale <= 0f ? 1f : windupScale;
        }

        public EnemyBossActionKind ActionKind { get; }
        public float WindupScale { get; }
    }

    internal interface IEnemyBossPattern
    {
        bool TryBuildActionPlan(
            Enemy enemy,
            bool canSeePlayer,
            float distance,
            float facingDot,
            float playerX,
            float playerY,
            float dirX,
            float dirY,
            Random rng,
            out EnemyBossActionPlan actionPlan);
    }
}
