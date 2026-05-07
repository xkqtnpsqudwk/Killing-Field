using System;
using System.Collections.Generic;
using System.Drawing;
using My2DEngine.Engine.Math;
using My2DEngine.Game;
using My2DEngine.Game.Config;
using My2DEngine.Game.Map;

namespace My2DEngine.Game.Systems
{
    /// <summary>
    /// 적 스폰과 아키타입 해석을 담당하는 partial 클래스다.
    /// </summary>
    public partial class EnemyManager
    {
        private bool TrySpawnStageEnemy(List<Enemy> list, StageRoom room, StageSpawnPoint spawn, Vector2 playerPosition, CollisionSystem collision, bool allowNearPlayer = false)
        {
            EnemyArchetype archetype = ResolveArchetype(spawn);
            EnemyDefinition adjusted = BuildAdjustedDefinition(spawn, archetype);
            EnemyRank rank = GetSpawnRank(spawn, archetype);
            EnemyAiProfile aiProfile = ResolveAiProfile(spawn, archetype, rank);
            string spriteVariantKey = ResolveSpriteVariantKey(spawn, archetype, rank);
            string displayName = string.IsNullOrWhiteSpace(spawn.DisplayName)
                ? archetype.DisplayName
                : spawn.DisplayName;
            Color[] sprite = GetEnemySprite(adjusted.Type, spriteVariantKey);
            RectangleF leash = BuildRoomLeashBounds(room);

            float jitterX = spawn.X + (float)(rng.NextDouble() * 3.0 - 1.5);
            float jitterY = spawn.Y + (float)(rng.NextDouble() * 3.0 - 1.5);
            if (TrySpawnEnemy(list, adjusted, sprite, jitterX, jitterY,
                playerPosition, allowNearPlayer, collision, rank, displayName, aiProfile, leash, spriteVariantKey,
                false, archetype.AssetId, archetype.SoundProfile, spawn.IsObjectiveTarget))
            {
                return true;
            }

            for (int i = 0; i < StageSpawnOffsets.Length; i++)
            {
                PointF offset = StageSpawnOffsets[i];
                if (TrySpawnEnemy(list, adjusted, sprite, spawn.X + offset.X, spawn.Y + offset.Y,
                    playerPosition, allowNearPlayer, collision, rank, displayName, aiProfile, leash, spriteVariantKey,
                    false, archetype.AssetId, archetype.SoundProfile, spawn.IsObjectiveTarget))
                {
                    return true;
                }
            }

            return false;
        }

        private EnemyBehaviorPattern PickBehaviorPattern(StageSpawnPoint spawn, EnemyArchetype archetype)
        {
            if (spawn.BehaviorPatternPool != null && spawn.BehaviorPatternPool.Length > 0)
            {
                return spawn.BehaviorPatternPool[rng.Next(spawn.BehaviorPatternPool.Length)];
            }

            if (spawn.BehaviorPatternPool != null)
            {
                return spawn.BehaviorPattern;
            }

            if (archetype != null &&
                archetype.BehaviorPatternPool != null &&
                archetype.BehaviorPatternPool.Length > 0)
            {
                return archetype.BehaviorPatternPool[rng.Next(archetype.BehaviorPatternPool.Length)];
            }

            if (spawn.BehaviorPattern != EnemyBehaviorPattern.Default ||
                archetype == null ||
                archetype.DefaultBehaviorPattern == EnemyBehaviorPattern.Default)
            {
                return spawn.BehaviorPattern;
            }

            return archetype.DefaultBehaviorPattern;
        }

        private bool TrySpawnEnemy(
            List<Enemy> list,
            EnemyType type,
            float x,
            float y,
            Vector2 playerPosition,
            bool allowNearPlayer,
            CollisionSystem collision,
            RectangleF? leashBounds = null)
        {
            EnemyArchetype archetype = EnemyCatalog.GetDefaultForType(type);
            string spriteVariantKey = ResolveSpriteVariantKey(null, archetype, EnemyRank.Normal);
            return TrySpawnEnemy(
                list,
                ApplyDifficultyMultipliers(archetype.CreateDefinition()),
                GetEnemySprite(archetype.Definition.Type, spriteVariantKey),
                x,
                y,
                playerPosition,
                allowNearPlayer,
                collision,
                EnemyRank.Normal,
                null,
                archetype.CreateAiProfile(archetype.DefaultBehaviorPattern),
                leashBounds,
                spriteVariantKey,
                false,
                archetype.AssetId,
                archetype.SoundProfile);
        }

        private bool TrySpawnEnemy(
            List<Enemy> list,
            EnemyDefinition definition,
            Color[] sprite,
            float x,
            float y,
            Vector2 playerPosition,
            bool allowNearPlayer,
            CollisionSystem collision,
            EnemyRank rank,
            string displayName,
            EnemyAiProfile aiProfile = null,
            RectangleF? leashBounds = null,
            string spriteVariantKey = null,
            bool isIllusion = false,
            string assetId = null,
            EnemySoundProfile soundProfile = null,
            bool isObjectiveTarget = false)
        {
            if (collision == null || collision.IsWallRadius(x, y, definition.Radius, 0.08f))
            {
                return false;
            }

            float dxPlayer = x - playerPosition.X;
            float dyPlayer = y - playerPosition.Y;
            float minPlayerDistanceSq = allowNearPlayer ? 4f : 9f;
            if ((dxPlayer * dxPlayer) + (dyPlayer * dyPlayer) < minPlayerDistanceSq)
            {
                return false;
            }

            for (int i = 0; i < list.Count; i++)
            {
                Enemy existing = list[i];
                if (existing == null || !existing.Alive)
                {
                    continue;
                }

                float dx = x - existing.X;
                float dy = y - existing.Y;
                float minDist = definition.Radius + existing.Radius + 0.45f;
                if ((dx * dx) + (dy * dy) < minDist * minDist)
                {
                    return false;
                }
            }

            Enemy enemy = new Enemy(
                x,
                y,
                definition,
                sprite,
                rank,
                displayName,
                aiProfile?.MovementPattern ?? EnemyBehaviorPattern.Default,
                spriteVariantKey,
                isIllusion,
                assetId,
                aiProfile,
                soundProfile);
            enemy.IsObjectiveTarget = isObjectiveTarget;
            enemy.DeathCallback = HandleEnemyDeath;

            if (leashBounds.HasValue)
            {
                enemy.SetLeashBounds(leashBounds.Value);
            }

            float facingAngle = (float)(rng.NextDouble() * Math.PI * 2.0);
            enemy.SetFacingDirection((float)Math.Cos(facingAngle), (float)Math.Sin(facingAngle));
            enemy.SearchTargetX = enemy.X;
            enemy.SearchTargetY = enemy.Y;

            float spawnAnimationDuration = textureManager.GetEnemySpawnAnimationDuration(enemy.SpriteVariantKey);
            if (spawnAnimationDuration > 0f)
            {
                enemy.BeginSpawnAnimation(spawnAnimationDuration);
            }

            if (enemy.IsBoss)
            {
                cachedBossEnemy = enemy;
            }

            list.Add(enemy);
            PlayEnemySound(enemy, EnemySoundCueType.Spawn);
            return true;
        }

        private EnemyDefinition BuildAdjustedDefinition(StageSpawnPoint spawn, EnemyArchetype archetype)
        {
            EnemyDefinition baseDefinition = ApplyDifficultyMultipliers(archetype.CreateDefinition());

            float healthVariance = 1f;
            float speedVariance = 1f;
            if (GetSpawnRank(spawn, archetype) == EnemyRank.Normal)
            {
                healthVariance = 0.8f + (float)(rng.NextDouble() * 0.4);
                speedVariance = 0.85f + (float)(rng.NextDouble() * 0.3);
            }

            return new EnemyDefinition
            {
                Type = baseDefinition.Type,
                Scale = baseDefinition.Scale * spawn.ScaleMultiplier,
                MaxHealth = baseDefinition.MaxHealth * spawn.HealthMultiplier * healthVariance,
                MoveSpeed = baseDefinition.MoveSpeed * spawn.MoveSpeedMultiplier * speedVariance,
                AttackRange = baseDefinition.AttackRange * spawn.AttackRangeMultiplier,
                AttackDamage = baseDefinition.AttackDamage * spawn.DamageMultiplier,
                AttackCooldownDuration = baseDefinition.AttackCooldownDuration,
                Radius = baseDefinition.Radius * Math.Max(1f, spawn.ScaleMultiplier * 0.9f)
            };
        }

        private EnemyDefinition ApplyDifficultyMultipliers(EnemyDefinition baseDefinition)
        {
            return new EnemyDefinition
            {
                Type = baseDefinition.Type,
                Scale = baseDefinition.Scale,
                MaxHealth = baseDefinition.MaxHealth * enemyHealthMultiplier,
                MoveSpeed = baseDefinition.MoveSpeed * enemyMoveSpeedMultiplier,
                AttackRange = baseDefinition.AttackRange,
                AttackDamage = baseDefinition.AttackDamage * enemyDamageMultiplier,
                AttackCooldownDuration = baseDefinition.AttackCooldownDuration,
                Radius = baseDefinition.Radius
            };
        }

        private Color[] GetEnemySprite(EnemyType type, string spriteVariantKey)
        {
            return textureManager.GetEnemySprite(spriteVariantKey, type);
        }

        private RectangleF BuildRoomLeashBounds(StageRoom room)
        {
            if (room == null)
            {
                return RectangleF.Empty;
            }

            Rectangle bounds = room.Bounds;
            return RectangleF.FromLTRB(
                bounds.Left + 1.2f,
                bounds.Top + 1.2f,
                bounds.Right - 1.2f,
                bounds.Bottom - 1.2f);
        }

        private static EnemyRank GetSpawnRank(StageSpawnPoint spawn, EnemyArchetype archetype)
        {
            if (spawn == null)
            {
                return archetype?.Rank ?? EnemyRank.Normal;
            }

            if (spawn.Rank != EnemyRank.Normal)
            {
                return spawn.Rank;
            }

            if (spawn.IsBoss)
            {
                return EnemyRank.Boss;
            }

            return archetype?.Rank ?? EnemyRank.Normal;
        }

        private static bool IsRangedNormalEnemy(Enemy enemy)
        {
            if (enemy == null)
            {
                return false;
            }

            return (enemy.AiProfile?.RangedAttackStyle ?? EnemyRangedAttackStyle.None) != EnemyRangedAttackStyle.None;
        }

        private EnemyArchetype ResolveArchetype(StageSpawnPoint spawn)
        {
            if (spawn == null)
            {
                return EnemyCatalog.GetDefaultForType(EnemyType.Gunner);
            }

            return EnemyCatalog.Resolve(spawn.EnemyAssetId, spawn.Type);
        }

        private EnemyAiProfile ResolveAiProfile(StageSpawnPoint spawn, EnemyArchetype archetype, EnemyRank rank)
        {
            EnemyArchetype resolvedArchetype = archetype ?? EnemyCatalog.GetDefaultForType(EnemyType.Gunner);
            EnemyBehaviorPattern movementPattern = PickBehaviorPattern(spawn, resolvedArchetype);
            EnemyAiProfile profile = resolvedArchetype.CreateAiProfile(movementPattern);
            if (ShouldUseDedicatedBossPattern(resolvedArchetype, rank))
            {
                return profile;
            }

            return StripBossPatternProfile(profile, rank);
        }

        private static bool ShouldUseDedicatedBossPattern(EnemyArchetype archetype, EnemyRank rank)
        {
            if (archetype == null || rank != EnemyRank.Boss || string.IsNullOrWhiteSpace(archetype.AssetId))
            {
                return false;
            }

            return archetype.Rank == EnemyRank.Boss;
        }

        private static EnemyAiProfile StripBossPatternProfile(EnemyAiProfile profile, EnemyRank rank)
        {
            if (profile == null || (rank != EnemyRank.Boss && rank != EnemyRank.MiniBoss))
            {
                return profile;
            }

            EnemyAiProfile stripped = profile.Clone();
            stripped.MovementPattern = EnemyBehaviorPattern.Default;
            stripped.WanderStyle = EnemyWanderStyle.Standard;
            stripped.BossCombatStyle = EnemyBossCombatStyle.None;
            stripped.BossUltimateStyle = EnemyBossUltimateStyle.None;
            stripped.SpecialCooldownDuration = float.MaxValue;
            stripped.UltimateCooldownDuration = float.MaxValue;
            stripped.SpecialWindupDuration = 0f;
            stripped.SpecialRecoverDuration = 0f;
            stripped.UltimateWindupDuration = 0f;
            stripped.UltimateRecoverDuration = 0f;
            stripped.SpecialMinDistance = float.MaxValue;
            stripped.SpecialMaxDistance = 0f;
            stripped.UltimateHealthThreshold = 0f;

            if (stripped.RangedAttackStyle == EnemyRangedAttackStyle.None)
            {
                stripped.PreferredCombatDistance = 1.3f;
                stripped.PreferredDistanceTolerance = 0.45f;
                stripped.ChaseSpeedScale = Math.Max(stripped.ChaseSpeedScale, 1f);
                stripped.PreferredAttackFacingDot = Math.Min(stripped.PreferredAttackFacingDot, 0.18f);
            }

            return stripped;
        }

        private string ResolveSpriteVariantKey(StageSpawnPoint spawn, EnemyArchetype archetype, EnemyRank rank)
        {
            if (IsLoadedEnemyVariant(spawn?.SpriteVariantKey))
                return spawn.SpriteVariantKey;

            // 아키타입 고유 variant pool이 있으면 그 안에서만 랜덤 선택
            string[] variantPool = archetype?.SpriteVariantKeyPool;
            string loadedPoolVariant = PickLoadedVariantFromPool(variantPool);
            if (!string.IsNullOrWhiteSpace(loadedPoolVariant))
                return loadedPoolVariant;

            if (IsLoadedEnemyVariant(archetype?.SpriteVariantKey))
                return archetype.SpriteVariantKey;

            string randomVariantKey = textureManager.PickRandomEnemyVariantKey(rng, rank);
            if (IsLoadedEnemyVariant(randomVariantKey))
                return randomVariantKey;

            if (!string.IsNullOrWhiteSpace(spawn?.SpriteVariantKey))
                return spawn.SpriteVariantKey;

            if (variantPool != null && variantPool.Length > 0)
                return variantPool[0];

            return archetype?.SpriteVariantKey;
        }

        private bool IsLoadedEnemyVariant(string spriteVariantKey)
        {
            return !string.IsNullOrWhiteSpace(spriteVariantKey) &&
                textureManager.HasEnemySpriteVariant(spriteVariantKey);
        }

        private string PickLoadedVariantFromPool(string[] variantPool)
        {
            if (variantPool == null || variantPool.Length == 0)
            {
                return null;
            }

            int startIndex = rng.Next(variantPool.Length);
            for (int i = 0; i < variantPool.Length; i++)
            {
                string candidate = variantPool[(startIndex + i) % variantPool.Length];
                if (IsLoadedEnemyVariant(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
