using System;
using My2DEngine.Engine.Math;
using My2DEngine.Game;
using My2DEngine.Game.Config;

namespace My2DEngine.Game.Systems
{
    public partial class EnemyManager
    {
        private bool TryQueueBossPatternAction(Enemy enemy, PerceptionContext perception)
        {
            if (enemy == null ||
                !enemy.IsBoss ||
                string.IsNullOrWhiteSpace(enemy.AssetId) ||
                !bossPatterns.TryGetValue(enemy.AssetId, out IEnemyBossPattern pattern) ||
                pattern == null)
            {
                return false;
            }

            if (!pattern.TryBuildActionPlan(
                    enemy,
                    perception.CanSeePlayer,
                    perception.Distance,
                    perception.FacingDot,
                    perception.PlayerX,
                    perception.PlayerY,
                    perception.DirX,
                    perception.DirY,
                    rng,
                    out EnemyBossActionPlan actionPlan))
            {
                return false;
            }

            QueueBossPatternAction(enemy, perception, actionPlan);
            return true;
        }

        private void QueueBossPatternAction(Enemy enemy, PerceptionContext perception, EnemyBossActionPlan actionPlan)
        {
            EnemyPendingAction pendingAction = MapBossActionToPendingAction(actionPlan.ActionKind);
            if (pendingAction == EnemyPendingAction.None)
            {
                return;
            }

            bool isUltimate = IsUltimateBossAction(actionPlan.ActionKind);
            float windupBase = isUltimate ? enemy.AiProfile.UltimateWindupDuration : enemy.AiProfile.SpecialWindupDuration;
            float windupDuration = Math.Max(0.05f, windupBase * actionPlan.WindupScale * GetBossPhaseWindupMultiplier(enemy));
            float cooldownMultiplier = GetBossPhaseCooldownMultiplier(enemy);

            QueueAction(
                enemy,
                EnemyAiState.AttackWindup,
                pendingAction,
                windupDuration,
                perception.DirX,
                perception.DirY,
                perception.PlayerX,
                perception.PlayerY);

            if (isUltimate)
            {
                enemy.UltimateCooldown = Math.Max(enemy.UltimateCooldown, enemy.AiProfile.UltimateCooldownDuration * cooldownMultiplier);
            }
            else
            {
                enemy.SpecialCooldown = Math.Max(enemy.SpecialCooldown, enemy.AiProfile.SpecialCooldownDuration * cooldownMultiplier);
            }
        }

        private static EnemyPendingAction MapBossActionToPendingAction(EnemyBossActionKind actionKind)
        {
            switch (actionKind)
            {
                case EnemyBossActionKind.AzazelInfernoDash:
                    return EnemyPendingAction.AzazelInfernoDash;
                case EnemyBossActionKind.AzazelHellfireBarrage:
                    return EnemyPendingAction.AzazelHellfireBarrage;
                case EnemyBossActionKind.BehemothShockwave:
                    return EnemyPendingAction.BehemothShockwave;
                case EnemyBossActionKind.BehemothSiegeBurst:
                    return EnemyPendingAction.BehemothSiegeBurst;
                case EnemyBossActionKind.ArachnocortexPlasmaFan:
                    return EnemyPendingAction.ArachnocortexPlasmaFan;
                case EnemyBossActionKind.ArachnocortexSuppressionGrid:
                    return EnemyPendingAction.ArachnocortexSuppressionGrid;
                case EnemyBossActionKind.AgauresBlinkClaw:
                    return EnemyPendingAction.AgauresBlinkClaw;
                case EnemyBossActionKind.AgauresRiftCrossfire:
                    return EnemyPendingAction.AgauresRiftCrossfire;
                default:
                    return EnemyPendingAction.None;
            }
        }

        private static bool IsUltimateBossAction(EnemyBossActionKind actionKind)
        {
            switch (actionKind)
            {
                case EnemyBossActionKind.AzazelHellfireBarrage:
                case EnemyBossActionKind.BehemothSiegeBurst:
                case EnemyBossActionKind.ArachnocortexSuppressionGrid:
                case EnemyBossActionKind.AgauresRiftCrossfire:
                    return true;
                default:
                    return false;
            }
        }

        private static int GetBossPhaseIndex(Enemy enemy)
        {
            float healthRatio = GetBossHealthRatio(enemy);
            if (healthRatio <= EnemyConfig.BossPhaseThreeHealthRatio)
            {
                return 2;
            }

            if (healthRatio <= EnemyConfig.BossPhaseTwoHealthRatio)
            {
                return 1;
            }

            return 0;
        }

        private static float GetBossHealthRatio(Enemy enemy)
        {
            if (enemy == null || enemy.MaxHealth <= 0.001f)
            {
                return 1f;
            }

            return Math.Max(0f, Math.Min(1f, enemy.Health / enemy.MaxHealth));
        }

        private static float GetBossPhaseWindupMultiplier(Enemy enemy)
        {
            int phase = GetBossPhaseIndex(enemy);
            if (phase >= 2)
            {
                return EnemyConfig.BossPhaseThreeWindupMultiplier;
            }

            if (phase == 1)
            {
                return EnemyConfig.BossPhaseTwoWindupMultiplier;
            }

            return 1f;
        }

        private static float GetBossPhaseCooldownMultiplier(Enemy enemy)
        {
            int phase = GetBossPhaseIndex(enemy);
            if (phase >= 2)
            {
                return EnemyConfig.BossPhaseThreeCooldownMultiplier;
            }

            if (phase == 1)
            {
                return EnemyConfig.BossPhaseTwoCooldownMultiplier;
            }

            return 1f;
        }

        private static float GetBossPhasePowerScale(Enemy enemy)
        {
            return 1f + GetBossPhaseIndex(enemy) * EnemyConfig.BossPhasePowerBonusPerPhase;
        }

        private static float GetBossPhaseProjectileSpeedScale(Enemy enemy)
        {
            return GetBossPhaseIndex(enemy) * EnemyConfig.BossPhaseProjectileSpeedBonusPerPhase;
        }

        private static float GetBossPhaseProjectileRadiusScale(Enemy enemy)
        {
            return GetBossPhaseIndex(enemy) * EnemyConfig.BossPhaseProjectileRadiusBonusPerPhase;
        }

        private static float GetBossPhaseDashDistanceScale(Enemy enemy)
        {
            return 1f + GetBossPhaseIndex(enemy) * EnemyConfig.BossPhaseDashDistanceBonusPerPhase;
        }

        private static int GetBossPhaseProjectileBonus(Enemy enemy)
        {
            return GetBossPhaseIndex(enemy) * EnemyConfig.BossPhaseProjectileBonusPerPhase;
        }

        private static int GetBossPhaseMinorProjectileBonus(Enemy enemy)
        {
            return GetBossPhaseIndex(enemy) * EnemyConfig.BossPhaseMinorProjectileBonusPerPhase;
        }

        private void ExecuteAzazelInfernoDash(
            Enemy enemy,
            PerceptionContext perception,
            CollisionSystem collision,
            Vector2 playerPosition,
            Action<float, float, float> onPlayerDamaged)
        {
            PerformEnemyAttack(enemy, EnemySoundCueType.Special);
            GetAimDirection(enemy, perception, out float aimX, out float aimY);
            float phasePower = GetBossPhasePowerScale(enemy);
            float dashDistance = Math.Max(1.1f,
                enemy.AiProfile.ChargeDuration *
                enemy.AiProfile.ChargeSpeedMultiplier *
                1.35f *
                GetBossPhaseDashDistanceScale(enemy));
            PerformDashMotion(enemy, aimX, aimY, dashDistance, collision, playerPosition);
            TryDamagePlayerWithMelee(enemy, playerPosition, onPlayerDamaged, enemy.AiProfile.SpecialDamageMultiplier * 1.1f * phasePower, collision);
            EnterRecoverState(enemy, EnemyAiState.AttackRecover, enemy.AiProfile.SpecialRecoverDuration);
        }

        private void ExecuteAzazelHellfireBarrage(Enemy enemy, PerceptionContext perception)
        {
            PerformEnemyAttack(enemy, EnemySoundCueType.Ultimate);
            GetAimDirection(enemy, perception, out float aimX, out float aimY);
            int projectileBonus = GetBossPhaseProjectileBonus(enemy);
            int minorBonus = GetBossPhaseMinorProjectileBonus(enemy);
            float phasePower = GetBossPhasePowerScale(enemy);
            float speedBonus = GetBossPhaseProjectileSpeedScale(enemy);
            float radiusBonus = GetBossPhaseProjectileRadiusScale(enemy);
            FireArcVolley(enemy, aimX, aimY, 7 + projectileBonus, 52f, enemy.AiProfile.UltimateDamageMultiplier * 0.42f * phasePower, 1.05f + speedBonus, 1.18f + radiusBonus, 0.9f);
            FireArcVolley(enemy, aimX, aimY, 5 + minorBonus, 28f, enemy.AiProfile.UltimateDamageMultiplier * 0.36f * phasePower, 1.22f + speedBonus, 1.06f + radiusBonus, 0.82f);
            EnterRecoverState(enemy, EnemyAiState.AttackRecover, enemy.AiProfile.UltimateRecoverDuration);
        }

        private void ExecuteBehemothShockwave(
            Enemy enemy,
            Vector2 playerPosition,
            CollisionSystem collision,
            Action<float, float, float> onPlayerDamaged)
        {
            PerformEnemyAttack(enemy, EnemySoundCueType.Special);
            int projectileBonus = GetBossPhaseProjectileBonus(enemy);
            float phasePower = GetBossPhasePowerScale(enemy);
            float speedBonus = GetBossPhaseProjectileSpeedScale(enemy);
            float radiusBonus = GetBossPhaseProjectileRadiusScale(enemy);
            FireRadialBurst(enemy, 10 + projectileBonus, enemy.AiProfile.SpecialDamageMultiplier * 0.44f * phasePower, 0.62f + speedBonus, 1.35f + radiusBonus, 0.8f);
            TryDamagePlayerInRadius(
                enemy,
                playerPosition,
                onPlayerDamaged,
                collision,
                radius: 2.45f * phasePower,
                damage: enemy.AttackDamage * enemy.AiProfile.SpecialDamageMultiplier * phasePower);
            EnterRecoverState(enemy, EnemyAiState.AttackRecover, enemy.AiProfile.SpecialRecoverDuration);
        }

        private void ExecuteBehemothSiegeBurst(Enemy enemy, PerceptionContext perception)
        {
            PerformEnemyAttack(enemy, EnemySoundCueType.Ultimate);
            GetAimDirection(enemy, perception, out float aimX, out float aimY);
            int projectileBonus = GetBossPhaseProjectileBonus(enemy);
            int minorBonus = GetBossPhaseMinorProjectileBonus(enemy);
            float phasePower = GetBossPhasePowerScale(enemy);
            float speedBonus = GetBossPhaseProjectileSpeedScale(enemy);
            float radiusBonus = GetBossPhaseProjectileRadiusScale(enemy);
            FireArcVolley(enemy, aimX, aimY, 9 + projectileBonus, 60f, enemy.AiProfile.UltimateDamageMultiplier * 0.38f * phasePower, 0.74f + speedBonus, 1.45f + radiusBonus, 1.1f);
            FireRadialBurst(enemy, 6 + minorBonus, enemy.AiProfile.UltimateDamageMultiplier * 0.26f * phasePower, 0.58f + speedBonus, 1.2f + radiusBonus, 0.9f);
            EnterRecoverState(enemy, EnemyAiState.AttackRecover, enemy.AiProfile.UltimateRecoverDuration);
        }

        private void ExecuteArachnocortexPlasmaFan(Enemy enemy, PerceptionContext perception)
        {
            PerformEnemyAttack(enemy, EnemySoundCueType.Special);
            GetAimDirection(enemy, perception, out float aimX, out float aimY);
            int projectileBonus = GetBossPhaseProjectileBonus(enemy);
            float phasePower = GetBossPhasePowerScale(enemy);
            float speedBonus = GetBossPhaseProjectileSpeedScale(enemy);
            float radiusBonus = GetBossPhaseProjectileRadiusScale(enemy);
            FireArcVolley(enemy, aimX, aimY, 6 + projectileBonus, 48f, enemy.AiProfile.SpecialDamageMultiplier * 0.38f * phasePower, 1.15f + speedBonus, 0.95f + radiusBonus, 0.88f);
            EnterRecoverState(enemy, EnemyAiState.AttackRecover, enemy.AiProfile.SpecialRecoverDuration);
        }

        private void ExecuteArachnocortexSuppressionGrid(Enemy enemy, PerceptionContext perception)
        {
            PerformEnemyAttack(enemy, EnemySoundCueType.Ultimate);
            GetAimDirection(enemy, perception, out float aimX, out float aimY);
            int projectileBonus = GetBossPhaseProjectileBonus(enemy);
            int minorBonus = GetBossPhaseMinorProjectileBonus(enemy);
            float phasePower = GetBossPhasePowerScale(enemy);
            float speedBonus = GetBossPhaseProjectileSpeedScale(enemy);
            float radiusBonus = GetBossPhaseProjectileRadiusScale(enemy);
            FireArcVolley(enemy, aimX, aimY, 7 + projectileBonus, 34f, enemy.AiProfile.UltimateDamageMultiplier * 0.34f * phasePower, 1.3f + speedBonus, 1f + radiusBonus, 0.95f);

            RotateVector(aimX, aimY, 18f, out float leftX, out float leftY);
            RotateVector(aimX, aimY, -18f, out float rightX, out float rightY);
            FireArcVolley(enemy, leftX, leftY, 5 + minorBonus, 22f, enemy.AiProfile.UltimateDamageMultiplier * 0.28f * phasePower, 1.2f + speedBonus, 0.95f + radiusBonus, 0.85f);
            FireArcVolley(enemy, rightX, rightY, 5 + minorBonus, 22f, enemy.AiProfile.UltimateDamageMultiplier * 0.28f * phasePower, 1.2f + speedBonus, 0.95f + radiusBonus, 0.85f);
            EnterRecoverState(enemy, EnemyAiState.AttackRecover, enemy.AiProfile.UltimateRecoverDuration);
        }

        private void ExecuteAgauresBlinkClaw(
            Enemy enemy,
            PerceptionContext perception,
            CollisionSystem collision,
            Vector2 playerPosition,
            Action<float, float, float> onPlayerDamaged)
        {
            PerformEnemyAttack(enemy, EnemySoundCueType.Special);
            TryBlinkNearPlayer(enemy, playerPosition, collision);
            GetAimDirection(enemy, perception, out float aimX, out float aimY);
            int minorBonus = GetBossPhaseMinorProjectileBonus(enemy);
            float phasePower = GetBossPhasePowerScale(enemy);
            float speedBonus = GetBossPhaseProjectileSpeedScale(enemy);
            float radiusBonus = GetBossPhaseProjectileRadiusScale(enemy);
            PerformDashMotion(enemy, aimX, aimY,
                Math.Max(0.9f, enemy.AiProfile.ChargeDuration * enemy.AiProfile.ChargeSpeedMultiplier * 0.9f * GetBossPhaseDashDistanceScale(enemy)),
                collision,
                playerPosition);
            TryDamagePlayerWithMelee(enemy, playerPosition, onPlayerDamaged, enemy.AiProfile.SpecialDamageMultiplier * 1.2f * phasePower, collision);
            FireArcVolley(enemy, aimX, aimY, 3 + minorBonus, 20f, enemy.AiProfile.SpecialDamageMultiplier * 0.2f * phasePower, 1.1f + speedBonus, 0.9f + radiusBonus, 0.72f);
            EnterRecoverState(enemy, EnemyAiState.AttackRecover, enemy.AiProfile.SpecialRecoverDuration);
        }

        private void ExecuteAgauresRiftCrossfire(Enemy enemy, PerceptionContext perception, CollisionSystem collision, Vector2 playerPosition)
        {
            PerformEnemyAttack(enemy, EnemySoundCueType.Ultimate);
            TryBlinkNearPlayer(enemy, playerPosition, collision);
            GetAimDirection(enemy, perception, out float aimX, out float aimY);
            int projectileBonus = GetBossPhaseProjectileBonus(enemy);
            int minorBonus = GetBossPhaseMinorProjectileBonus(enemy);
            float phasePower = GetBossPhasePowerScale(enemy);
            float speedBonus = GetBossPhaseProjectileSpeedScale(enemy);
            float radiusBonus = GetBossPhaseProjectileRadiusScale(enemy);
            FireArcVolley(enemy, aimX, aimY, 5 + projectileBonus, 36f, enemy.AiProfile.UltimateDamageMultiplier * 0.32f * phasePower, 1.2f + speedBonus, 1f + radiusBonus, 0.88f);
            FireRadialBurst(enemy, 8 + minorBonus, enemy.AiProfile.UltimateDamageMultiplier * 0.26f * phasePower, 0.95f + speedBonus, 1.05f + radiusBonus, 0.82f);
            EnterRecoverState(enemy, EnemyAiState.AttackRecover, enemy.AiProfile.UltimateRecoverDuration);
        }

        private void PerformDashMotion(
            Enemy enemy,
            float dirX,
            float dirY,
            float distance,
            CollisionSystem collision,
            Vector2 playerPosition)
        {
            Normalize(ref dirX, ref dirY);
            if ((dirX * dirX) + (dirY * dirY) <= 0.001f || distance <= 0.01f)
            {
                return;
            }

            int steps = Math.Max(1, (int)Math.Ceiling(distance / 0.08f));
            float stepX = (dirX * distance) / steps;
            float stepY = (dirY * distance) / steps;

            float startX = enemy.X;
            float startY = enemy.Y;
            for (int i = 0; i < steps; i++)
            {
                if (collision != null)
                {
                    collision.TryMoveEnemy(enemy, stepX, stepY, playerPosition);
                }
                else
                {
                    enemy.X += stepX;
                    enemy.Y += stepY;
                }

                enemy.ClampToLeash();
            }

            enemy.SetFacingDirection(dirX, dirY);
            enemy.MarkMovement(enemy.X - startX, enemy.Y - startY);
        }

        private bool TryBlinkNearPlayer(Enemy enemy, Vector2 playerPosition, CollisionSystem collision)
        {
            float toPlayerX = playerPosition.X - enemy.X;
            float toPlayerY = playerPosition.Y - enemy.Y;
            Normalize(ref toPlayerX, ref toPlayerY);
            if ((toPlayerX * toPlayerX) + (toPlayerY * toPlayerY) <= 0.001f)
            {
                toPlayerX = enemy.FacingDirX;
                toPlayerY = enemy.FacingDirY;
                Normalize(ref toPlayerX, ref toPlayerY);
            }

            float sideX = -toPlayerY;
            float sideY = toPlayerX;
            Vector2[] candidateOffsets =
            {
                new Vector2((-toPlayerX * 1.7f) + (sideX * 1.1f), (-toPlayerY * 1.7f) + (sideY * 1.1f)),
                new Vector2((-toPlayerX * 1.7f) - (sideX * 1.1f), (-toPlayerY * 1.7f) - (sideY * 1.1f)),
                new Vector2(-toPlayerX * 2.05f, -toPlayerY * 2.05f),
                new Vector2((-toPlayerX * 1.25f) + (sideX * 1.55f), (-toPlayerY * 1.25f) + (sideY * 1.55f)),
                new Vector2((-toPlayerX * 1.25f) - (sideX * 1.55f), (-toPlayerY * 1.25f) - (sideY * 1.55f))
            };

            int start = rng.Next(candidateOffsets.Length);
            float minPlayerDistance = enemy.Radius + PlayerConfig.PlayerRadius + 0.16f;
            for (int i = 0; i < candidateOffsets.Length; i++)
            {
                Vector2 offset = candidateOffsets[(start + i) % candidateOffsets.Length];
                float blinkX = playerPosition.X + offset.X;
                float blinkY = playerPosition.Y + offset.Y;
                if (enemy.HasLeashBounds && !enemy.IsInsideLeash(blinkX, blinkY))
                {
                    continue;
                }

                float dxPlayer = blinkX - playerPosition.X;
                float dyPlayer = blinkY - playerPosition.Y;
                if ((dxPlayer * dxPlayer) + (dyPlayer * dyPlayer) < minPlayerDistance * minPlayerDistance)
                {
                    continue;
                }

                if (collision != null && collision.IsWallRadius(blinkX, blinkY, enemy.Radius, 0.05f))
                {
                    continue;
                }

                enemy.X = blinkX;
                enemy.Y = blinkY;
                enemy.ClampToLeash();
                enemy.SetFacingDirection(playerPosition.X - enemy.X, playerPosition.Y - enemy.Y);
                return true;
            }

            return false;
        }

        private void FireArcVolley(
            Enemy enemy,
            float centerDirX,
            float centerDirY,
            int shotCount,
            float arcDegrees,
            float damageScale,
            float speedScale,
            float radiusScale,
            float lifetimeScale)
        {
            Normalize(ref centerDirX, ref centerDirY);
            if ((centerDirX * centerDirX) + (centerDirY * centerDirY) <= 0.001f || shotCount <= 0)
            {
                return;
            }

            if (shotCount == 1)
            {
                SpawnPatternShot(enemy, centerDirX, centerDirY, damageScale, speedScale, radiusScale, lifetimeScale);
                return;
            }

            for (int i = 0; i < shotCount; i++)
            {
                float t = i / (float)(shotCount - 1);
                float angle = (t - 0.5f) * arcDegrees;
                RotateVector(centerDirX, centerDirY, angle, out float dirX, out float dirY);
                SpawnPatternShot(enemy, dirX, dirY, damageScale, speedScale, radiusScale, lifetimeScale);
            }
        }

        private void FireRadialBurst(
            Enemy enemy,
            int shotCount,
            float damageScale,
            float speedScale,
            float radiusScale,
            float lifetimeScale)
        {
            if (shotCount <= 0)
            {
                return;
            }

            float baseAngle = (float)(rng.NextDouble() * Math.PI * 2.0);
            for (int i = 0; i < shotCount; i++)
            {
                float angle = baseAngle + (float)(Math.PI * 2.0 * (i / (float)shotCount));
                float dirX = (float)Math.Cos(angle);
                float dirY = (float)Math.Sin(angle);
                SpawnPatternShot(enemy, dirX, dirY, damageScale, speedScale, radiusScale, lifetimeScale);
            }
        }

        private void SpawnPatternShot(
            Enemy enemy,
            float dirX,
            float dirY,
            float damageScale,
            float speedScale,
            float radiusScale,
            float lifetimeScale)
        {
            Normalize(ref dirX, ref dirY);
            if ((dirX * dirX) + (dirY * dirY) <= 0.001f)
            {
                return;
            }

            float radius = Math.Max(0.1f, 0.12f * Math.Max(0.5f, radiusScale));
            float speed = Math.Max(2.6f, enemy.AiProfile.ProjectileSpeed * Math.Max(0.25f, speedScale));
            float lifetime = Math.Max(0.35f, enemy.AiProfile.ProjectileLifetime * Math.Max(0.3f, lifetimeScale));
            enemyProjectiles.Add(new EnemyProjectile
            {
                Kind = EnemyProjectileKind.EnemyShot,
                X = enemy.X + dirX * (enemy.Radius + radius + 0.08f),
                Y = enemy.Y + dirY * (enemy.Radius + radius + 0.08f),
                VelocityX = dirX * speed,
                VelocityY = dirY * speed,
                Radius = radius,
                Damage = enemy.AttackDamage * Math.Max(0.08f, damageScale),
                Lifetime = lifetime,
                Active = true
            });
        }

        private static void RotateVector(float x, float y, float angleDegrees, out float outX, out float outY)
        {
            float rad = angleDegrees * ((float)Math.PI / 180f);
            float cos = (float)Math.Cos(rad);
            float sin = (float)Math.Sin(rad);
            outX = (x * cos) - (y * sin);
            outY = (x * sin) + (y * cos);
        }

        private void TryDamagePlayerInRadius(
            Enemy enemy,
            Vector2 playerPosition,
            Action<float, float, float> onPlayerDamaged,
            CollisionSystem collision,
            float radius,
            float damage)
        {
            float dx = playerPosition.X - enemy.X;
            float dy = playerPosition.Y - enemy.Y;
            float distSq = (dx * dx) + (dy * dy);
            float hitRadius = radius + PlayerConfig.PlayerRadius;
            if (distSq > hitRadius * hitRadius)
            {
                return;
            }

            float dist = distSq <= 0.0001f ? 0f : (float)Math.Sqrt(distSq);
            if (collision != null &&
                dist > 0.001f &&
                !collision.HasLineOfSight(enemy.X, enemy.Y, playerPosition.X, playerPosition.Y, dist))
            {
                return;
            }

            onPlayerDamaged?.Invoke(damage, enemy.X, enemy.Y);
        }
    }
}
