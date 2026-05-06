using System;
using My2DEngine.Engine.Math;
using My2DEngine.Game;
using My2DEngine.Game.Config;

namespace My2DEngine.Game.Systems
{
    /// <summary>
    /// 적 감지와 이동 휴리스틱을 담당하는 partial 클래스다.
    /// </summary>
    public partial class EnemyManager
    {
        private PerceptionContext BuildPerceptionContext(Enemy enemy, Vector2 playerPosition, bool playerDead, CollisionSystem collision)
        {
            var perception = new PerceptionContext
            {
                PlayerX = playerPosition.X,
                PlayerY = playerPosition.Y
            };

            float dx = playerPosition.X - enemy.X;
            float dy = playerPosition.Y - enemy.Y;
            float distSq = (dx * dx) + (dy * dy);
            float dist = distSq > 0.000001f ? (float)Math.Sqrt(distSq) : 0f;
            perception.Distance = dist;

            if (dist > 0.001f)
            {
                perception.DirX = dx / dist;
                perception.DirY = dy / dist;
            }
            else
            {
                perception.DirX = enemy.FacingDirX;
                perception.DirY = enemy.FacingDirY;
            }

            float facingX = enemy.FacingDirX;
            float facingY = enemy.FacingDirY;
            if ((facingX * facingX) + (facingY * facingY) <= 0.001f)
            {
                facingX = enemy.MoveDirX;
                facingY = enemy.MoveDirY;
            }
            if ((facingX * facingX) + (facingY * facingY) <= 0.001f)
            {
                facingX = enemy.LastKnownDirX;
                facingY = enemy.LastKnownDirY;
            }

            Normalize(ref facingX, ref facingY);
            perception.FacingDot = (facingX * perception.DirX) + (facingY * perception.DirY);

            EnemyAiProfile profile = enemy.AiProfile;
            float sightDotThreshold = enemy.State == EnemyAiState.Patrol && enemy.MemoryTimer <= 0f
                ? profile.PatrolSightDot
                : profile.AlertSightDot;
            if (dist > 0f && dist < 1.35f)
            {
                sightDotThreshold = -1f;
            }

            perception.CanSeePlayer = !playerDead &&
                dist > 0f &&
                dist <= profile.SightRange &&
                enemy.IsInsideLeash(playerPosition.X, playerPosition.Y) &&
                perception.FacingDot >= sightDotThreshold &&
                (collision == null || collision.HasLineOfSight(enemy.X, enemy.Y, playerPosition.X, playerPosition.Y, dist));

            PlayerSoundStimulus soundStimulus = playerDead ? null : FindBestAudibleStimulus(enemy);
            if (soundStimulus != null)
            {
                perception.HeardSound = true;
                perception.HeardX = soundStimulus.X;
                perception.HeardY = soundStimulus.Y;
                perception.HeardDirX = soundStimulus.DirX;
                perception.HeardDirY = soundStimulus.DirY;
            }

            return perception;
        }

        private PlayerSoundStimulus FindBestAudibleStimulus(Enemy enemy)
        {
            EnemyAiProfile profile = enemy?.AiProfile;
            if (enemy == null || profile == null || playerSoundStimuli.Count == 0)
            {
                return null;
            }

            float bestScore = float.MinValue;
            PlayerSoundStimulus best = null;

            for (int i = 0; i < playerSoundStimuli.Count; i++)
            {
                PlayerSoundStimulus stimulus = playerSoundStimuli[i];
                float audibleRange = Math.Min(stimulus.Radius, profile.HearingRange);
                if (audibleRange <= 0.001f)
                {
                    continue;
                }

                if (enemy.HasLeashBounds && !enemy.IsInsideLeash(stimulus.X, stimulus.Y))
                {
                    continue;
                }

                float dx = stimulus.X - enemy.X;
                float dy = stimulus.Y - enemy.Y;
                float distSq = (dx * dx) + (dy * dy);
                if (distSq > audibleRange * audibleRange)
                {
                    continue;
                }

                float dist = (float)Math.Sqrt(distSq);
                float ageWeight = (PlayerSoundStimulusLifetime - stimulus.Age) / PlayerSoundStimulusLifetime;
                float distanceWeight = 1f - (dist / audibleRange);
                float score = (ageWeight * 1.6f) + distanceWeight;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = stimulus;
                }
            }

            return best;
        }

        private void BlendFacingTowards(Enemy enemy, float dirX, float dirY, float dt, float scale = 1f)
        {
            if (enemy == null)
            {
                return;
            }

            Normalize(ref dirX, ref dirY);
            if ((dirX * dirX) + (dirY * dirY) <= 0.001f)
            {
                return;
            }

            float currentX = enemy.FacingDirX;
            float currentY = enemy.FacingDirY;
            if ((currentX * currentX) + (currentY * currentY) <= 0.001f)
            {
                enemy.FacingDirX = dirX;
                enemy.FacingDirY = dirY;
                return;
            }

            float blend = Clamp(dt * enemy.AiProfile.TurnResponse * scale, 0f, 1f);
            currentX += (dirX - currentX) * blend;
            currentY += (dirY - currentY) * blend;
            Normalize(ref currentX, ref currentY);
            enemy.FacingDirX = currentX;
            enemy.FacingDirY = currentY;
        }

        private bool MoveTowardPoint(
            Enemy enemy,
            float targetX,
            float targetY,
            float speedScale,
            float dt,
            CollisionSystem collision,
            Vector2 playerPosition,
            float stopDistance = 0.28f)
        {
            float dx = targetX - enemy.X;
            float dy = targetY - enemy.Y;
            float distSq = (dx * dx) + (dy * dy);
            if (distSq <= stopDistance * stopDistance)
            {
                enemy.MoveDirX *= 0.7f;
                enemy.MoveDirY *= 0.7f;
                BlendFacingTowards(enemy, dx, dy, dt, 0.7f);
                return false;
            }

            MoveEnemyWithSteering(
                enemy,
                dx,
                dy,
                enemy.MoveSpeed * speedScale,
                dt,
                collision,
                playerPosition,
                enemy.AiProfile.TurnResponse);
            return true;
        }

        private void UpdatePatrolMovement(Enemy enemy, float dt, CollisionSystem collision, Vector2 playerPosition)
        {
            if (enemy.IdleTimer > 0f)
            {
                enemy.MoveDirX *= 0.78f;
                enemy.MoveDirY *= 0.78f;
                return;
            }

            if (enemy.WanderTimer <= 0f)
            {
                ChooseNextPatrol(enemy);
                if (enemy.IdleTimer > 0f || enemy.WanderTimer <= 0f)
                {
                    return;
                }
            }

            float oldX = enemy.X;
            float oldY = enemy.Y;
            MoveEnemyWithSteering(
                enemy,
                enemy.WanderDirX,
                enemy.WanderDirY,
                enemy.MoveSpeed * 0.56f,
                dt,
                collision,
                playerPosition,
                enemy.AiProfile.TurnResponse * 0.58f);

            float movedX = enemy.X - oldX;
            float movedY = enemy.Y - oldY;
            enemy.MarkMovement(movedX, movedY);
            if ((movedX * movedX) + (movedY * movedY) < 0.0004f)
            {
                enemy.WanderTimer = 0f;
            }
        }

        private void ChooseNextPatrol(Enemy enemy)
        {
            EnemyAiProfile profile = enemy.AiProfile;
            if (rng.NextDouble() < profile.PatrolIdleChance)
            {
                float idleDuration = profile.PatrolIdleDurationMin +
                    (float)rng.NextDouble() * Math.Max(0.05f, profile.PatrolIdleDurationMax - profile.PatrolIdleDurationMin);
                enemy.SetIdle(idleDuration);
                return;
            }

            float angle = (float)(rng.NextDouble() * Math.PI * 2.0);
            float duration = profile.PatrolMoveDurationMin +
                (float)rng.NextDouble() * Math.Max(0.05f, profile.PatrolMoveDurationMax - profile.PatrolMoveDurationMin);

            switch (profile.WanderStyle)
            {
                case EnemyWanderStyle.BossSentinel:
                    duration *= 0.8f;
                    break;
                case EnemyWanderStyle.BossRunner:
                    duration *= 0.72f;
                    break;
                case EnemyWanderStyle.BossArtillery:
                    duration *= 0.68f;
                    break;
                case EnemyWanderStyle.MiniBoss:
                    duration *= 1.12f;
                    break;
            }

            enemy.SetWanderDirection((float)Math.Cos(angle), (float)Math.Sin(angle), duration);
        }

        private void UpdateCombatMovement(
            Enemy enemy,
            float dt,
            CollisionSystem collision,
            Vector2 playerPosition,
            float targetX,
            float targetY)
        {
            float dx = targetX - enemy.X;
            float dy = targetY - enemy.Y;
            float distSq = (dx * dx) + (dy * dy);
            if (distSq <= 0.0001f)
            {
                return;
            }

            float dist = (float)Math.Sqrt(distSq);
            float moveX = dx / dist;
            float moveY = dy / dist;
            float sideX = -moveY;
            float sideY = moveX;
            float speedScale = enemy.AiProfile.ChaseSpeedScale;
            float desiredDistance = enemy.AiProfile.PreferredCombatDistance;
            float tolerance = enemy.AiProfile.PreferredDistanceTolerance;

            switch (enemy.AiProfile.MovementPattern)
            {
                case EnemyBehaviorPattern.Juggernaut:
                    speedScale = dist > desiredDistance + tolerance ? 1.05f : 0.34f;
                    break;
                case EnemyBehaviorPattern.Rushdown:
                    speedScale = dist > desiredDistance + tolerance ? 1.28f : 1.06f;
                    break;
                case EnemyBehaviorPattern.Strafe:
                    RefreshBehaviorPhase(enemy, dt, 0.95f, 1.75f);
                    if (dist < desiredDistance - tolerance)
                    {
                        moveX = -(dx / dist) + sideX * enemy.BehaviorPhase * 0.62f;
                        moveY = -(dy / dist) + sideY * enemy.BehaviorPhase * 0.62f;
                    }
                    else if (dist > desiredDistance + tolerance)
                    {
                        moveX = (dx / dist) + sideX * enemy.BehaviorPhase * 0.42f;
                        moveY = (dy / dist) + sideY * enemy.BehaviorPhase * 0.42f;
                    }
                    else
                    {
                        moveX = sideX * enemy.BehaviorPhase;
                        moveY = sideY * enemy.BehaviorPhase;
                    }
                    speedScale = Math.Max(speedScale, enemy.AiProfile.StrafeSpeedScale);
                    break;
                case EnemyBehaviorPattern.Kite:
                    RefreshBehaviorPhase(enemy, dt, 1.15f, 2.2f);
                    if (dist < desiredDistance - tolerance)
                    {
                        moveX = -(dx / dist) + sideX * enemy.BehaviorPhase * 0.55f;
                        moveY = -(dy / dist) + sideY * enemy.BehaviorPhase * 0.55f;
                    }
                    else if (dist > desiredDistance + tolerance)
                    {
                        moveX = (dx / dist) + sideX * enemy.BehaviorPhase * 0.35f;
                        moveY = (dy / dist) + sideY * enemy.BehaviorPhase * 0.35f;
                    }
                    else
                    {
                        moveX = sideX * enemy.BehaviorPhase;
                        moveY = sideY * enemy.BehaviorPhase;
                    }
                    speedScale = Math.Max(speedScale, enemy.AiProfile.StrafeSpeedScale);
                    break;
                case EnemyBehaviorPattern.Pouncer:
                    UpdatePouncerState(enemy, dt);
                    speedScale = enemy.BehaviorPhase > 0f ? Math.Max(speedScale, 1.35f) : 0.28f;
                    break;
                case EnemyBehaviorPattern.Skirmisher:
                    RefreshBehaviorPhase(enemy, dt, 0.85f, 1.6f);
                    if (dist < desiredDistance - tolerance)
                    {
                        moveX = -(dx / dist) + sideX * enemy.BehaviorPhase * 0.72f;
                        moveY = -(dy / dist) + sideY * enemy.BehaviorPhase * 0.72f;
                    }
                    else if (dist > desiredDistance + tolerance)
                    {
                        moveX += sideX * enemy.BehaviorPhase * 0.52f;
                        moveY += sideY * enemy.BehaviorPhase * 0.52f;
                    }
                    else
                    {
                        moveX = sideX * enemy.BehaviorPhase * 0.95f;
                        moveY = sideY * enemy.BehaviorPhase * 0.95f;
                    }
                    speedScale = Math.Max(speedScale, 1.04f);
                    break;
                case EnemyBehaviorPattern.BossTanker:
                    moveX = dist < desiredDistance - tolerance ? -(dx / dist) : (dx / dist);
                    moveY = dist < desiredDistance - tolerance ? -(dy / dist) : (dy / dist);
                    moveX += sideX * 0.08f;
                    moveY += sideY * 0.08f;
                    speedScale = 0.8f;
                    break;
                case EnemyBehaviorPattern.BossBeast:
                    RefreshBehaviorPhase(enemy, dt, 0.7f, 1.25f);
                    moveX += sideX * enemy.BehaviorPhase * 0.38f;
                    moveY += sideY * enemy.BehaviorPhase * 0.38f;
                    speedScale = Math.Max(speedScale, 1.14f);
                    break;
                case EnemyBehaviorPattern.BossRunner:
                    RefreshBehaviorPhase(enemy, dt, 0.52f, 1.05f);
                    moveX = sideX * enemy.BehaviorPhase * 1.12f + (dx / dist) * 0.48f;
                    moveY = sideY * enemy.BehaviorPhase * 1.12f + (dy / dist) * 0.48f;
                    if (dist < desiredDistance)
                    {
                        moveX -= (dx / dist) * 1.08f;
                        moveY -= (dy / dist) * 1.08f;
                    }
                    speedScale = Math.Max(speedScale, 1.28f);
                    break;
                case EnemyBehaviorPattern.BossArtillery:
                    RefreshBehaviorPhase(enemy, dt, 0.9f, 1.45f);
                    if (dist < desiredDistance - tolerance)
                    {
                        moveX = -(dx / dist) + sideX * enemy.BehaviorPhase * 0.6f;
                        moveY = -(dy / dist) + sideY * enemy.BehaviorPhase * 0.6f;
                    }
                    else if (dist > desiredDistance + tolerance)
                    {
                        moveX = (dx / dist) + sideX * enemy.BehaviorPhase * 0.28f;
                        moveY = (dy / dist) + sideY * enemy.BehaviorPhase * 0.28f;
                    }
                    else
                    {
                        moveX = sideX * enemy.BehaviorPhase;
                        moveY = sideY * enemy.BehaviorPhase;
                    }
                    speedScale = Math.Max(speedScale, enemy.AiProfile.StrafeSpeedScale);
                    break;
                // ── 일반 적 신규 패턴 ─────────────────────────────────────────
                case EnemyBehaviorPattern.ZombieGunner:
                    // 사거리 유지 + 소량 횡이동 (Kite 동일)
                    RefreshBehaviorPhase(enemy, dt, 1.15f, 2.2f);
                    if (dist < desiredDistance - tolerance)
                    {
                        moveX = -(dx / dist) + sideX * enemy.BehaviorPhase * 0.55f;
                        moveY = -(dy / dist) + sideY * enemy.BehaviorPhase * 0.55f;
                    }
                    else if (dist > desiredDistance + tolerance)
                    {
                        moveX = (dx / dist) + sideX * enemy.BehaviorPhase * 0.35f;
                        moveY = (dy / dist) + sideY * enemy.BehaviorPhase * 0.35f;
                    }
                    else
                    {
                        moveX = sideX * enemy.BehaviorPhase;
                        moveY = sideY * enemy.BehaviorPhase;
                    }
                    speedScale = Math.Max(speedScale, enemy.AiProfile.StrafeSpeedScale);
                    break;
                case EnemyBehaviorPattern.BlindCharger:
                    // 청각 탐지 후 목표 방향 직선 전력 질주 (도중 회피 없음)
                    speedScale = dist > desiredDistance + tolerance ? 1.12f : 0.36f;
                    break;
                case EnemyBehaviorPattern.BloodPhantom:
                    // 고속 직진 + 피격 후 이속 보너스 (Rushdown 강화)
                    speedScale = dist > desiredDistance + tolerance ? 1.38f : 1.12f;
                    break;
                case EnemyBehaviorPattern.BeamSniper:
                    // 장거리 유지 + 넓은 횡이동 (Kite 변형)
                    RefreshBehaviorPhase(enemy, dt, 1.0f, 2.0f);
                    if (dist < desiredDistance - tolerance)
                    {
                        moveX = -(dx / dist) + sideX * enemy.BehaviorPhase * 0.68f;
                        moveY = -(dy / dist) + sideY * enemy.BehaviorPhase * 0.68f;
                    }
                    else if (dist > desiredDistance + tolerance)
                    {
                        moveX = (dx / dist) + sideX * enemy.BehaviorPhase * 0.42f;
                        moveY = (dy / dist) + sideY * enemy.BehaviorPhase * 0.42f;
                    }
                    else
                    {
                        moveX = sideX * enemy.BehaviorPhase * 1.1f;
                        moveY = sideY * enemy.BehaviorPhase * 1.1f;
                    }
                    speedScale = Math.Max(speedScale, enemy.AiProfile.StrafeSpeedScale);
                    break;
                case EnemyBehaviorPattern.SlimeLobber:
                    // 중거리 유지 + 압박 시 옆으로 이탈 (Skirmisher 유사)
                    RefreshBehaviorPhase(enemy, dt, 0.85f, 1.6f);
                    if (dist < desiredDistance - tolerance)
                    {
                        moveX = -(dx / dist) + sideX * enemy.BehaviorPhase * 0.72f;
                        moveY = -(dy / dist) + sideY * enemy.BehaviorPhase * 0.72f;
                    }
                    else if (dist > desiredDistance + tolerance)
                    {
                        moveX += sideX * enemy.BehaviorPhase * 0.52f;
                        moveY += sideY * enemy.BehaviorPhase * 0.52f;
                    }
                    else
                    {
                        moveX = sideX * enemy.BehaviorPhase * 0.95f;
                        moveY = sideY * enemy.BehaviorPhase * 0.95f;
                    }
                    speedScale = Math.Max(speedScale, 1.04f);
                    break;
                case EnemyBehaviorPattern.HellionSwarm:
                    // 초고속 공전 + 순간 돌진 (Pouncer 강화)
                    UpdatePouncerState(enemy, dt);
                    speedScale = enemy.BehaviorPhase > 0f ? Math.Max(speedScale, 1.52f) : 0.32f;
                    break;

                // ── 보스 신규 패턴 ─────────────────────────────────────────────
                case EnemyBehaviorPattern.BossFlameLord:
                    // 공격적 돌진형 (BossBeast 동일)
                    RefreshBehaviorPhase(enemy, dt, 0.7f, 1.25f);
                    moveX += sideX * enemy.BehaviorPhase * 0.38f;
                    moveY += sideY * enemy.BehaviorPhase * 0.38f;
                    speedScale = Math.Max(speedScale, 1.14f);
                    break;
                case EnemyBehaviorPattern.BossBehemoth:
                    // 극저속 압박 (BossTanker 유사, 더 느림)
                    moveX = dist < desiredDistance - tolerance ? -(dx / dist) : (dx / dist);
                    moveY = dist < desiredDistance - tolerance ? -(dy / dist) : (dy / dist);
                    moveX += sideX * 0.05f;
                    moveY += sideY * 0.05f;
                    speedScale = 0.68f;
                    break;
                case EnemyBehaviorPattern.BossCortex:
                    // 원거리 유지 + 횡이동 (BossArtillery 동일)
                    RefreshBehaviorPhase(enemy, dt, 0.9f, 1.45f);
                    if (dist < desiredDistance - tolerance)
                    {
                        moveX = -(dx / dist) + sideX * enemy.BehaviorPhase * 0.6f;
                        moveY = -(dy / dist) + sideY * enemy.BehaviorPhase * 0.6f;
                    }
                    else if (dist > desiredDistance + tolerance)
                    {
                        moveX = (dx / dist) + sideX * enemy.BehaviorPhase * 0.28f;
                        moveY = (dy / dist) + sideY * enemy.BehaviorPhase * 0.28f;
                    }
                    else
                    {
                        moveX = sideX * enemy.BehaviorPhase;
                        moveY = sideY * enemy.BehaviorPhase;
                    }
                    speedScale = Math.Max(speedScale, enemy.AiProfile.StrafeSpeedScale);
                    break;
                case EnemyBehaviorPattern.BossRiftBlitz:
                    // 정지 없는 빠른 공전 (BossRunner 강화)
                    RefreshBehaviorPhase(enemy, dt, 0.48f, 0.95f);
                    moveX = sideX * enemy.BehaviorPhase * 1.22f + (dx / dist) * 0.44f;
                    moveY = sideY * enemy.BehaviorPhase * 1.22f + (dy / dist) * 0.44f;
                    if (dist < desiredDistance)
                    {
                        moveX -= (dx / dist) * 1.12f;
                        moveY -= (dy / dist) * 1.12f;
                    }
                    speedScale = Math.Max(speedScale, 1.36f);
                    break;
                case EnemyBehaviorPattern.BossDarkLord:
                    // 강한 근접 돌진 (BossBeast 유사)
                    RefreshBehaviorPhase(enemy, dt, 0.65f, 1.2f);
                    moveX += sideX * enemy.BehaviorPhase * 0.32f;
                    moveY += sideY * enemy.BehaviorPhase * 0.32f;
                    speedScale = Math.Max(speedScale, 1.18f);
                    break;
                case EnemyBehaviorPattern.BossAfritBomber:
                    // 원거리 유지 + 넓은 횡이동 (BossArtillery 변형)
                    RefreshBehaviorPhase(enemy, dt, 0.85f, 1.4f);
                    if (dist < desiredDistance - tolerance)
                    {
                        moveX = -(dx / dist) + sideX * enemy.BehaviorPhase * 0.65f;
                        moveY = -(dy / dist) + sideY * enemy.BehaviorPhase * 0.65f;
                    }
                    else if (dist > desiredDistance + tolerance)
                    {
                        moveX = (dx / dist) + sideX * enemy.BehaviorPhase * 0.30f;
                        moveY = (dy / dist) + sideY * enemy.BehaviorPhase * 0.30f;
                    }
                    else
                    {
                        moveX = sideX * enemy.BehaviorPhase * 1.05f;
                        moveY = sideY * enemy.BehaviorPhase * 1.05f;
                    }
                    speedScale = Math.Max(speedScale, enemy.AiProfile.StrafeSpeedScale);
                    break;
                case EnemyBehaviorPattern.BossAgathoDemon:
                    // 공격 후 즉시 측면 이탈 (BossRunner 변형)
                    RefreshBehaviorPhase(enemy, dt, 0.50f, 1.0f);
                    moveX = sideX * enemy.BehaviorPhase * 1.15f + (dx / dist) * 0.46f;
                    moveY = sideY * enemy.BehaviorPhase * 1.15f + (dy / dist) * 0.46f;
                    if (dist < desiredDistance)
                    {
                        moveX -= (dx / dist) * 1.1f;
                        moveY -= (dy / dist) * 1.1f;
                    }
                    speedScale = Math.Max(speedScale, 1.3f);
                    break;
                case EnemyBehaviorPattern.BossAnnihilator:
                    // 극저속 압박 (BossTanker보다 더 느림)
                    moveX = dist < desiredDistance - tolerance ? -(dx / dist) : (dx / dist);
                    moveY = dist < desiredDistance - tolerance ? -(dy / dist) : (dy / dist);
                    moveX += sideX * 0.04f;
                    moveY += sideY * 0.04f;
                    speedScale = 0.60f;
                    break;
                case EnemyBehaviorPattern.BossArackBaron:
                    // 원거리 유지 + 측면 기동 (BossArtillery 변형)
                    RefreshBehaviorPhase(enemy, dt, 0.92f, 1.5f);
                    if (dist < desiredDistance - tolerance)
                    {
                        moveX = -(dx / dist) + sideX * enemy.BehaviorPhase * 0.58f;
                        moveY = -(dy / dist) + sideY * enemy.BehaviorPhase * 0.58f;
                    }
                    else if (dist > desiredDistance + tolerance)
                    {
                        moveX = (dx / dist) + sideX * enemy.BehaviorPhase * 0.26f;
                        moveY = (dy / dist) + sideY * enemy.BehaviorPhase * 0.26f;
                    }
                    else
                    {
                        moveX = sideX * enemy.BehaviorPhase;
                        moveY = sideY * enemy.BehaviorPhase;
                    }
                    speedScale = Math.Max(speedScale, enemy.AiProfile.StrafeSpeedScale);
                    break;
                case EnemyBehaviorPattern.BossArachnoFang:
                    // 빠른 근접 + 측면 기동 (BossRunner 변형)
                    RefreshBehaviorPhase(enemy, dt, 0.55f, 1.08f);
                    moveX = sideX * enemy.BehaviorPhase * 1.08f + (dx / dist) * 0.50f;
                    moveY = sideY * enemy.BehaviorPhase * 1.08f + (dy / dist) * 0.50f;
                    if (dist < desiredDistance)
                    {
                        moveX -= (dx / dist) * 1.05f;
                        moveY -= (dy / dist) * 1.05f;
                    }
                    speedScale = Math.Max(speedScale, 1.22f);
                    break;
                case EnemyBehaviorPattern.BossSpiderQueen:
                    // 방 중앙 고정 경향, 극저속 (BossTanker 유사)
                    moveX = dist < desiredDistance - tolerance ? -(dx / dist) : (dx / dist);
                    moveY = dist < desiredDistance - tolerance ? -(dy / dist) : (dy / dist);
                    moveX += sideX * 0.06f;
                    moveY += sideY * 0.06f;
                    speedScale = 0.72f;
                    break;

                case EnemyBehaviorPattern.Default:
                default:
                    if (dist < desiredDistance - tolerance)
                    {
                        moveX = -(dx / dist);
                        moveY = -(dy / dist);
                    }
                    else if (dist <= desiredDistance + tolerance)
                    {
                        RefreshBehaviorPhase(enemy, dt, 0.9f, 1.6f);
                        moveX = sideX * enemy.BehaviorPhase * 0.55f;
                        moveY = sideY * enemy.BehaviorPhase * 0.55f;
                        speedScale = Math.Max(speedScale, enemy.AiProfile.StrafeSpeedScale * 0.85f);
                    }
                    break;
            }

            MoveEnemyWithSteering(
                enemy,
                moveX,
                moveY,
                enemy.MoveSpeed * speedScale,
                dt,
                collision,
                playerPosition,
                enemy.AiProfile.TurnResponse);
        }

        private void MoveEnemyWithSteering(
            Enemy enemy,
            float desiredX,
            float desiredY,
            float speed,
            float dt,
            CollisionSystem collision,
            Vector2 playerPosition,
            float turnResponse)
        {
            if (enemy == null || collision == null || speed <= 0f || dt <= 0f)
            {
                return;
            }

            float desiredLenSq = (desiredX * desiredX) + (desiredY * desiredY);
            if (desiredLenSq <= 0.0001f)
            {
                enemy.MoveDirX *= 0.82f;
                enemy.MoveDirY *= 0.82f;
                return;
            }

            float desiredLen = (float)Math.Sqrt(desiredLenSq);
            desiredX /= desiredLen;
            desiredY /= desiredLen;

            float currentLenSq = (enemy.MoveDirX * enemy.MoveDirX) + (enemy.MoveDirY * enemy.MoveDirY);
            if (currentLenSq <= 0.0001f)
            {
                enemy.MoveDirX = desiredX;
                enemy.MoveDirY = desiredY;
            }
            else
            {
                float blend = Clamp(dt * turnResponse, 0f, 1f);
                float blendedX = enemy.MoveDirX + ((desiredX - enemy.MoveDirX) * blend);
                float blendedY = enemy.MoveDirY + ((desiredY - enemy.MoveDirY) * blend);
                Normalize(ref blendedX, ref blendedY);
                enemy.MoveDirX = blendedX;
                enemy.MoveDirY = blendedY;
            }

            if (enemy.HitReactTimer > 0f)
            {
                float hitReactSpeed = enemy.IsBoss
                    ? GameConfig.BossHitReactMoveMultiplier
                    : GameConfig.EnemyHitReactMoveMultiplier;
                speed *= Math.Max(0.05f, hitReactSpeed);
            }

            float step = speed * dt;
            float oldX = enemy.X;
            float oldY = enemy.Y;
            collision.TryMoveEnemy(enemy, enemy.MoveDirX * step, enemy.MoveDirY * step, playerPosition);
            enemy.ClampToLeash();
            enemy.MarkMovement(enemy.X - oldX, enemy.Y - oldY);
            BlendFacingTowards(enemy, enemy.MoveDirX, enemy.MoveDirY, dt, 0.9f);

            float movedX = enemy.X - oldX;
            float movedY = enemy.Y - oldY;
            if ((movedX * movedX) + (movedY * movedY) < 0.0001f)
            {
                enemy.MoveDirX = desiredX;
                enemy.MoveDirY = desiredY;
            }
        }

        private void RefreshBehaviorPhase(Enemy enemy, float dt, float minDuration, float maxDuration)
        {
            enemy.BehaviorTimer -= Math.Max(0f, dt);
            if (enemy.BehaviorTimer > 0f)
            {
                return;
            }

            enemy.BehaviorTimer = minDuration +
                (float)rng.NextDouble() * Math.Max(0.05f, maxDuration - minDuration);
            enemy.BehaviorPhase = rng.Next(2) == 0 ? -1f : 1f;
        }

        private void UpdatePouncerState(Enemy enemy, float dt)
        {
            enemy.BehaviorTimer -= Math.Max(0f, dt);
            if (enemy.BehaviorTimer > 0f)
            {
                return;
            }

            if (enemy.BehaviorPhase > 0f)
            {
                enemy.BehaviorPhase = -1f;
                enemy.BehaviorTimer = 0.4f + (float)rng.NextDouble() * 0.45f;
            }
            else
            {
                enemy.BehaviorPhase = 1f;
                enemy.BehaviorTimer = 0.7f + (float)rng.NextDouble() * 0.5f;
            }
        }

        private void ClampPointToLeash(Enemy enemy, ref float x, ref float y, float padding = 0.45f)
        {
            if (enemy == null || !enemy.HasLeashBounds)
            {
                return;
            }

            float minX = enemy.LeashBounds.Left + padding;
            float maxX = enemy.LeashBounds.Right - padding;
            float minY = enemy.LeashBounds.Top + padding;
            float maxY = enemy.LeashBounds.Bottom - padding;

            x = Clamp(x, minX, maxX);
            y = Clamp(y, minY, maxY);
        }

        private void ChooseNextSearchTarget(Enemy enemy)
        {
            EnemyAiProfile profile = enemy.AiProfile;
            float baseDirX = enemy.LastKnownDirX;
            float baseDirY = enemy.LastKnownDirY;
            if ((baseDirX * baseDirX) + (baseDirY * baseDirY) <= 0.001f)
            {
                baseDirX = enemy.LastKnownPlayerX - enemy.X;
                baseDirY = enemy.LastKnownPlayerY - enemy.Y;
            }

            Normalize(ref baseDirX, ref baseDirY);
            if ((baseDirX * baseDirX) + (baseDirY * baseDirY) <= 0.001f)
            {
                baseDirX = enemy.FacingDirX;
                baseDirY = enemy.FacingDirY;
            }

            float searchLead = profile.DirectionalSearchDistance * (enemy.LastStimulusWasSound ? 0.7f : 1f);
            float centerX = enemy.LastKnownPlayerX + (baseDirX * searchLead);
            float centerY = enemy.LastKnownPlayerY + (baseDirY * searchLead);

            float spread = profile.SearchSpreadDegrees * ((float)rng.NextDouble() * 2f - 1f);
            float radians = spread * ((float)Math.PI / 180f);
            float cos = (float)Math.Cos(radians);
            float sin = (float)Math.Sin(radians);
            float rotatedX = (baseDirX * cos) - (baseDirY * sin);
            float rotatedY = (baseDirX * sin) + (baseDirY * cos);
            Normalize(ref rotatedX, ref rotatedY);

            float radius = 0.2f + (float)rng.NextDouble() * profile.SearchRadius;
            float targetX = centerX + (rotatedX * radius);
            float targetY = centerY + (rotatedY * radius);
            ClampPointToLeash(enemy, ref targetX, ref targetY);

            enemy.SearchTargetX = targetX;
            enemy.SearchTargetY = targetY;
            enemy.SearchRetargetTimer = profile.SearchRetargetInterval;
        }
    }
}
