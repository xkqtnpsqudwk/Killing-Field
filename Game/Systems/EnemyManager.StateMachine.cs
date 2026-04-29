using System;
using My2DEngine.Engine.Math;
using My2DEngine.Game;
using My2DEngine.Game.Config;

namespace My2DEngine.Game.Systems
{
    /// <summary>
    /// 적 상태 머신(기본 이동/기본 공격 + 선택적 보스 패턴)을 담당하는 partial 클래스다.
    /// </summary>
    public partial class EnemyManager
    {
        private void UpdateEnemies(
            float dt,
            Vector2 playerPosition,
            bool playerDead,
            CollisionSystem collision,
            Action<float, float, float> onPlayerDamaged)
        {
            if (enemies == null)
            {
                return;
            }

            for (int i = 0; i < enemies.Length; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy == null)
                {
                    continue;
                }

                enemy.UpdateTimers(dt);

                if (enemy.IsSpawning || !enemy.Alive)
                {
                    continue;
                }

                PerceptionContext perception = BuildPerceptionContext(enemy, playerPosition, playerDead, collision);
                UpdateEnemyAwareness(enemy, perception);

                if (enemy.StunTimer > 0f)
                {
                    UpdateStunnedState(enemy, dt);
                    continue;
                }

                if (enemy.State == EnemyAiState.Stunned)
                {
                    enemy.State = enemy.HasLastKnownPlayer ? EnemyAiState.Search : EnemyAiState.Patrol;
                    enemy.StateTimer = enemy.State == EnemyAiState.Search ? enemy.AiProfile.SearchDuration : 0f;
                }

                if (IsWindupState(enemy.State))
                {
                    UpdateWindupState(enemy, perception, collision, playerPosition, playerDead, onPlayerDamaged);
                    continue;
                }

                if (IsRecoverState(enemy.State))
                {
                    UpdateRecoverState(enemy, perception);
                    continue;
                }

                switch (enemy.State)
                {
                    case EnemyAiState.Investigate:
                        UpdateInvestigateState(enemy, dt, perception, collision, playerPosition);
                        break;
                    case EnemyAiState.Search:
                        UpdateSearchState(enemy, dt, perception, collision, playerPosition);
                        break;
                    case EnemyAiState.Combat:
                        UpdateCombatState(enemy, dt, perception, collision, playerPosition, playerDead);
                        break;
                    case EnemyAiState.Patrol:
                    default:
                        UpdatePatrolState(enemy, dt, perception, collision, playerPosition);
                        break;
                }
            }
        }

        private void UpdateEnemyAwareness(Enemy enemy, PerceptionContext perception)
        {
            if (perception.CanSeePlayer)
            {
                RememberTarget(enemy, perception.PlayerX, perception.PlayerY, perception.DirX, perception.DirY, true);
                return;
            }

            if (perception.HeardSound)
            {
                float dirX = perception.HeardDirX;
                float dirY = perception.HeardDirY;
                if ((dirX * dirX) + (dirY * dirY) <= 0.001f)
                {
                    dirX = perception.HeardX - enemy.X;
                    dirY = perception.HeardY - enemy.Y;
                }

                RememberTarget(enemy, perception.HeardX, perception.HeardY, dirX, dirY, false);
            }
        }

        private void RememberTarget(Enemy enemy, float targetX, float targetY, float dirX, float dirY, bool fromSight)
        {
            float travelX = targetX - enemy.LastKnownPlayerX;
            float travelY = targetY - enemy.LastKnownPlayerY;
            if ((travelX * travelX) + (travelY * travelY) > 0.01f)
            {
                dirX = travelX;
                dirY = travelY;
            }

            Normalize(ref dirX, ref dirY);
            if ((dirX * dirX) + (dirY * dirY) <= 0.001f)
            {
                dirX = targetX - enemy.X;
                dirY = targetY - enemy.Y;
                Normalize(ref dirX, ref dirY);
            }

            if ((dirX * dirX) + (dirY * dirY) > 0.001f)
            {
                float memoryDirX = enemy.LastKnownDirX;
                float memoryDirY = enemy.LastKnownDirY;
                if ((memoryDirX * memoryDirX) + (memoryDirY * memoryDirY) > 0.001f)
                {
                    dirX = (memoryDirX * 0.35f) + (dirX * 0.65f);
                    dirY = (memoryDirY * 0.35f) + (dirY * 0.65f);
                    Normalize(ref dirX, ref dirY);
                }

                enemy.LastKnownDirX = dirX;
                enemy.LastKnownDirY = dirY;
            }

            if (fromSight)
            {
                enemy.LastSeenPlayerX = targetX;
                enemy.LastSeenPlayerY = targetY;
            }
            else
            {
                enemy.LastHeardPlayerX = targetX;
                enemy.LastHeardPlayerY = targetY;
            }

            enemy.LastKnownPlayerX = targetX;
            enemy.LastKnownPlayerY = targetY;
            enemy.HasLastKnownPlayer = true;
            enemy.MemoryTimer = enemy.AiProfile.MemoryDuration;
            enemy.LastStimulusWasSound = !fromSight;
        }

        private void UpdatePatrolState(Enemy enemy, float dt, PerceptionContext perception, CollisionSystem collision, Vector2 playerPosition)
        {
            if (perception.CanSeePlayer)
            {
                EnterCombatState(enemy);
                return;
            }

            if (perception.HeardSound)
            {
                EnterInvestigateState(enemy, perception.HeardX, perception.HeardY);
                return;
            }

            UpdatePatrolMovement(enemy, dt, collision, playerPosition);
        }

        private void UpdateInvestigateState(Enemy enemy, float dt, PerceptionContext perception, CollisionSystem collision, Vector2 playerPosition)
        {
            if (perception.CanSeePlayer)
            {
                EnterCombatState(enemy);
                return;
            }

            if (perception.HeardSound)
            {
                float heardX = perception.HeardX;
                float heardY = perception.HeardY;
                ClampPointToLeash(enemy, ref heardX, ref heardY);
                enemy.SearchTargetX = heardX;
                enemy.SearchTargetY = heardY;
                enemy.StateTimer = Math.Max(enemy.StateTimer, enemy.AiProfile.InvestigateDuration * 0.5f);
            }

            bool moved = MoveTowardPoint(
                enemy,
                enemy.SearchTargetX,
                enemy.SearchTargetY,
                enemy.AiProfile.InvestigateSpeedScale,
                dt,
                collision,
                playerPosition,
                0.35f);
            BlendFacingTowards(enemy, enemy.SearchTargetX - enemy.X, enemy.SearchTargetY - enemy.Y, dt, 0.8f);

            if (!moved || enemy.StateTimer <= 0f)
            {
                EnterSearchState(enemy);
            }
        }

        private void UpdateSearchState(Enemy enemy, float dt, PerceptionContext perception, CollisionSystem collision, Vector2 playerPosition)
        {
            if (perception.CanSeePlayer)
            {
                EnterCombatState(enemy);
                return;
            }

            if (perception.HeardSound)
            {
                EnterInvestigateState(enemy, perception.HeardX, perception.HeardY);
                return;
            }

            if (!enemy.HasLastKnownPlayer || enemy.MemoryTimer <= 0f || enemy.StateTimer <= 0f)
            {
                EnterPatrolState(enemy);
                return;
            }

            float targetDx = enemy.SearchTargetX - enemy.X;
            float targetDy = enemy.SearchTargetY - enemy.Y;
            float targetDistSq = (targetDx * targetDx) + (targetDy * targetDy);
            if (enemy.SearchRetargetTimer <= 0f || targetDistSq <= 0.16f)
            {
                ChooseNextSearchTarget(enemy);
            }

            bool moved = MoveTowardPoint(
                enemy,
                enemy.SearchTargetX,
                enemy.SearchTargetY,
                enemy.AiProfile.SearchSpeedScale,
                dt,
                collision,
                playerPosition,
                0.32f);
            BlendFacingTowards(enemy, enemy.SearchTargetX - enemy.X, enemy.SearchTargetY - enemy.Y, dt, 0.7f);

            if (!moved && enemy.SearchRetargetTimer <= 0f)
            {
                ChooseNextSearchTarget(enemy);
            }
        }

        private void UpdateCombatState(Enemy enemy, float dt, PerceptionContext perception, CollisionSystem collision, Vector2 playerPosition, bool playerDead)
        {
            if (!perception.CanSeePlayer)
            {
                if (perception.HeardSound)
                {
                    EnterInvestigateState(enemy, perception.HeardX, perception.HeardY);
                    return;
                }

                if (!enemy.HasLastKnownPlayer || enemy.MemoryTimer <= 0f)
                {
                    EnterPatrolState(enemy);
                    return;
                }

                EnterSearchState(enemy);
                UpdateSearchState(enemy, dt, perception, collision, playerPosition);
                return;
            }

            BlendFacingTowards(enemy, perception.DirX, perception.DirY, dt);

            if (!playerDead && TryQueueCombatAction(enemy, perception))
            {
                return;
            }

            UpdateCombatMovement(enemy, dt, collision, playerPosition, perception.PlayerX, perception.PlayerY);
        }

        private void UpdateWindupState(
            Enemy enemy,
            PerceptionContext perception,
            CollisionSystem collision,
            Vector2 playerPosition,
            bool playerDead,
            Action<float, float, float> onPlayerDamaged)
        {
            RefreshPendingAim(enemy, perception);
            BlendFacingTowards(enemy, enemy.PendingDirX, enemy.PendingDirY, 1f / 60f, 1.1f);
            enemy.MoveDirX *= 0.7f;
            enemy.MoveDirY *= 0.7f;

            if (enemy.StateTimer > 0f)
            {
                return;
            }

            ExecutePendingAction(enemy, perception, collision, playerPosition, playerDead, onPlayerDamaged);
        }

        private void UpdateRecoverState(Enemy enemy, PerceptionContext perception)
        {
            enemy.MoveDirX *= 0.8f;
            enemy.MoveDirY *= 0.8f;

            if (enemy.StateTimer > 0f)
            {
                return;
            }

            if (perception.CanSeePlayer)
            {
                EnterCombatState(enemy);
            }
            else if (perception.HeardSound)
            {
                EnterInvestigateState(enemy, perception.HeardX, perception.HeardY);
            }
            else if (enemy.HasLastKnownPlayer && enemy.MemoryTimer > 0f)
            {
                EnterSearchState(enemy);
            }
            else
            {
                EnterPatrolState(enemy);
            }
        }

        private void UpdateStunnedState(Enemy enemy, float dt)
        {
            enemy.State = EnemyAiState.Stunned;
            enemy.StateTimer = Math.Max(enemy.StateTimer, enemy.StunTimer);
            enemy.PendingAction = EnemyPendingAction.None;
            enemy.TelegraphTimer = 0f;
            enemy.MoveDirX *= Math.Max(0f, 1f - (dt * 8f));
            enemy.MoveDirY *= Math.Max(0f, 1f - (dt * 8f));
        }

        private void EnterPatrolState(Enemy enemy)
        {
            enemy.State = EnemyAiState.Patrol;
            enemy.StateTimer = 0f;
            enemy.PendingAction = EnemyPendingAction.None;
            enemy.TelegraphTimer = 0f;
            enemy.SearchRetargetTimer = 0f;
        }

        private void EnterInvestigateState(Enemy enemy, float targetX, float targetY)
        {
            enemy.State = EnemyAiState.Investigate;
            enemy.StateTimer = enemy.AiProfile.InvestigateDuration;
            enemy.PendingAction = EnemyPendingAction.None;
            float clampedX = targetX;
            float clampedY = targetY;
            ClampPointToLeash(enemy, ref clampedX, ref clampedY);
            enemy.SearchTargetX = clampedX;
            enemy.SearchTargetY = clampedY;
            BlendFacingTowards(enemy, targetX - enemy.X, targetY - enemy.Y, 1f / 30f, 1.2f);
        }

        private void EnterSearchState(Enemy enemy)
        {
            enemy.State = EnemyAiState.Search;
            enemy.StateTimer = enemy.AiProfile.SearchDuration;
            enemy.PendingAction = EnemyPendingAction.None;
            enemy.SearchRetargetTimer = 0f;
            if (!enemy.HasLastKnownPlayer)
            {
                enemy.SearchTargetX = enemy.X;
                enemy.SearchTargetY = enemy.Y;
            }
        }

        private void EnterCombatState(Enemy enemy)
        {
            bool wasInCombat = enemy.State == EnemyAiState.Combat ||
                               enemy.State == EnemyAiState.AttackWindup ||
                               enemy.State == EnemyAiState.AttackRecover;
            enemy.State = EnemyAiState.Combat;
            enemy.StateTimer = 0f;
            enemy.SearchRetargetTimer = 0f;

            if (!wasInCombat)
            {
                PlayEnemySound(enemy, EnemySoundCueType.Spawn);
            }
        }

        private void EnterRecoverState(Enemy enemy, EnemyAiState state, float duration)
        {
            enemy.State = state;
            enemy.StateTimer = duration;
            enemy.TelegraphTimer = 0f;
            enemy.PendingDirX = 0f;
            enemy.PendingDirY = 0f;
            enemy.PendingTargetX = 0f;
            enemy.PendingTargetY = 0f;
            enemy.PendingAction = EnemyPendingAction.None;
        }

        private bool TryQueueCombatAction(Enemy enemy, PerceptionContext perception)
        {
            if (TryQueueBossPatternAction(enemy, perception))
            {
                return true;
            }

            if (enemy.CanAttack() && ShouldUseBasicAttack(enemy, perception))
            {
                QueueBasicAttack(enemy, perception);
                return true;
            }

            return false;
        }

        private bool ShouldUseBasicAttack(Enemy enemy, PerceptionContext perception)
        {
            if (enemy == null || !perception.CanSeePlayer)
            {
                return false;
            }

            if (IsRangedNormalEnemy(enemy))
            {
                return perception.Distance <= enemy.AttackRange &&
                    perception.FacingDot >= enemy.AiProfile.PreferredAttackFacingDot;
            }

            float meleeWindow = enemy.AttackRange +
                enemy.AiProfile.AttackWindowBonus +
                enemy.Radius +
                GameConfig.PlayerRadius;
            return perception.Distance <= meleeWindow &&
                perception.FacingDot >= Math.Min(0.18f, enemy.AiProfile.PreferredAttackFacingDot);
        }

        private void QueueBasicAttack(Enemy enemy, PerceptionContext perception)
        {
            QueueAction(
                enemy,
                EnemyAiState.AttackWindup,
                IsRangedNormalEnemy(enemy) ? EnemyPendingAction.RangedAttack : EnemyPendingAction.MeleeAttack,
                enemy.AiProfile.AttackWindupDuration,
                perception.DirX,
                perception.DirY,
                perception.PlayerX,
                perception.PlayerY);
        }

        private void QueueAction(
            Enemy enemy,
            EnemyAiState state,
            EnemyPendingAction pendingAction,
            float duration,
            float dirX,
            float dirY,
            float targetX,
            float targetY)
        {
            enemy.State = state;
            enemy.StateTimer = duration;
            enemy.PendingAction = pendingAction;
            enemy.PendingDirX = dirX;
            enemy.PendingDirY = dirY;
            enemy.PendingTargetX = targetX;
            enemy.PendingTargetY = targetY;
            enemy.TelegraphTimer = (enemy.IsBoss || enemy.IsMiniBoss) && state == EnemyAiState.AttackWindup
                ? duration
                : 0f;
            BlendFacingTowards(enemy, dirX, dirY, 1f / 30f, 1.3f);
        }

        private void RefreshPendingAim(Enemy enemy, PerceptionContext perception)
        {
            switch (enemy.PendingAction)
            {
                case EnemyPendingAction.RangedAttack:
                case EnemyPendingAction.AzazelHellfireBarrage:
                case EnemyPendingAction.BehemothSiegeBurst:
                case EnemyPendingAction.ArachnocortexPlasmaFan:
                case EnemyPendingAction.ArachnocortexSuppressionGrid:
                case EnemyPendingAction.AgauresRiftCrossfire:
                    GetPredictedTarget(enemy, perception, out float predictedX, out float predictedY);
                    float predictedDirX = predictedX - enemy.X;
                    float predictedDirY = predictedY - enemy.Y;
                    Normalize(ref predictedDirX, ref predictedDirY);
                    enemy.PendingTargetX = predictedX;
                    enemy.PendingTargetY = predictedY;
                    enemy.PendingDirX = predictedDirX;
                    enemy.PendingDirY = predictedDirY;
                    break;

                case EnemyPendingAction.MeleeAttack:
                case EnemyPendingAction.AzazelInfernoDash:
                case EnemyPendingAction.BehemothShockwave:
                case EnemyPendingAction.AgauresBlinkClaw:
                    enemy.PendingDirX = perception.DirX;
                    enemy.PendingDirY = perception.DirY;
                    enemy.PendingTargetX = perception.PlayerX;
                    enemy.PendingTargetY = perception.PlayerY;
                    break;
            }
        }

        private void ExecutePendingAction(
            Enemy enemy,
            PerceptionContext perception,
            CollisionSystem collision,
            Vector2 playerPosition,
            bool playerDead,
            Action<float, float, float> onPlayerDamaged)
        {
            switch (enemy.PendingAction)
            {
                case EnemyPendingAction.MeleeAttack:
                    PerformEnemyAttack(enemy);
                    if (!playerDead)
                    {
                        TryDamagePlayerWithMelee(enemy, playerPosition, onPlayerDamaged, enemy.AiProfile.MeleeDamageMultiplier, collision);
                    }
                    EnterRecoverState(enemy, EnemyAiState.AttackRecover, enemy.AiProfile.AttackRecoverDuration);
                    break;

                case EnemyPendingAction.RangedAttack:
                    GetAimDirection(enemy, perception, out float rangedDirX, out float rangedDirY);
                    FireRangedVolley(enemy, rangedDirX, rangedDirY);
                    EnterRecoverState(enemy, EnemyAiState.AttackRecover, enemy.AiProfile.AttackRecoverDuration);
                    break;

                case EnemyPendingAction.AzazelInfernoDash:
                    ExecuteAzazelInfernoDash(enemy, perception, collision, playerPosition, onPlayerDamaged);
                    break;

                case EnemyPendingAction.AzazelHellfireBarrage:
                    ExecuteAzazelHellfireBarrage(enemy, perception);
                    break;

                case EnemyPendingAction.BehemothShockwave:
                    ExecuteBehemothShockwave(enemy, playerPosition, collision, onPlayerDamaged);
                    break;

                case EnemyPendingAction.BehemothSiegeBurst:
                    ExecuteBehemothSiegeBurst(enemy, perception);
                    break;

                case EnemyPendingAction.ArachnocortexPlasmaFan:
                    ExecuteArachnocortexPlasmaFan(enemy, perception);
                    break;

                case EnemyPendingAction.ArachnocortexSuppressionGrid:
                    ExecuteArachnocortexSuppressionGrid(enemy, perception);
                    break;

                case EnemyPendingAction.AgauresBlinkClaw:
                    ExecuteAgauresBlinkClaw(enemy, perception, collision, playerPosition, onPlayerDamaged);
                    break;

                case EnemyPendingAction.AgauresRiftCrossfire:
                    ExecuteAgauresRiftCrossfire(enemy, perception, collision, playerPosition);
                    break;

                default:
                    EnterCombatState(enemy);
                    break;
            }
        }

        private void TryDamagePlayerWithMelee(
            Enemy enemy,
            Vector2 playerPosition,
            Action<float, float, float> onPlayerDamaged,
            float damageMultiplier,
            CollisionSystem collision)
        {
            float dx = playerPosition.X - enemy.X;
            float dy = playerPosition.Y - enemy.Y;
            float distSq = (dx * dx) + (dy * dy);
            float hitDistance = enemy.AttackRange +
                enemy.AiProfile.AttackWindowBonus +
                enemy.Radius +
                GameConfig.PlayerRadius;
            if (distSq > hitDistance * hitDistance)
            {
                return;
            }

            float dist = distSq > 0.0001f ? (float)Math.Sqrt(distSq) : 0f;
            if (collision != null && dist > 0.001f &&
                !collision.HasLineOfSight(enemy.X, enemy.Y, playerPosition.X, playerPosition.Y, dist))
            {
                return;
            }

            onPlayerDamaged?.Invoke(enemy.AttackDamage * damageMultiplier, enemy.X, enemy.Y);
        }

        private void GetPredictedTarget(Enemy enemy, PerceptionContext perception, out float targetX, out float targetY)
        {
            targetX = perception.CanSeePlayer ? perception.PlayerX : enemy.LastKnownPlayerX;
            targetY = perception.CanSeePlayer ? perception.PlayerY : enemy.LastKnownPlayerY;

            float dirX = enemy.LastKnownDirX;
            float dirY = enemy.LastKnownDirY;
            Normalize(ref dirX, ref dirY);
            if ((dirX * dirX) + (dirY * dirY) <= 0.001f)
            {
                dirX = perception.DirX;
                dirY = perception.DirY;
            }

            float leadDistance = (1.35f + (perception.Distance * 0.08f)) * enemy.AiProfile.PredictionLeadSeconds * 2.2f;
            targetX += dirX * leadDistance;
            targetY += dirY * leadDistance;
            ClampPointToLeash(enemy, ref targetX, ref targetY);
        }

        private void GetAimDirection(Enemy enemy, PerceptionContext perception, out float dirX, out float dirY)
        {
            GetPredictedTarget(enemy, perception, out float targetX, out float targetY);
            dirX = targetX - enemy.X;
            dirY = targetY - enemy.Y;
            Normalize(ref dirX, ref dirY);
            if ((dirX * dirX) + (dirY * dirY) <= 0.001f)
            {
                dirX = enemy.FacingDirX;
                dirY = enemy.FacingDirY;
            }
        }
    }
}
