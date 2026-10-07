using System;
using System.Collections.Generic;
using System.Drawing;
using My2DEngine.Engine.Math;
using My2DEngine.Game;
using My2DEngine.Game.Audio;
using My2DEngine.Game.Config;
using My2DEngine.Game.Map;
using My2DEngine.Game.Rendering;

namespace My2DEngine.Game.Systems
{
    /// <summary>
    /// 적 생성, 상태 머신 AI, 보스 액션, 적 투사체를 관리하는 전투 시스템이다.
    /// </summary>
    public partial class EnemyManager
    {
        private const float PlayerSoundStimulusLifetime = 2.4f;

        private static readonly PointF[] StageSpawnOffsets =
        {
            new PointF(0f, 0f),
            new PointF(0.5f, 0f),
            new PointF(-0.5f, 0f),
            new PointF(0f, 0.5f),
            new PointF(0f, -0.5f)
        };

        private static readonly float[] RuinedGunnerSpreads = { -0.18f, 0f, 0.18f };
        private static readonly float[] BurstPairSpreads = { -0.08f, 0.08f };
        private static readonly float[] WideFanSpreads = { -0.34f, -0.18f, 0f, 0.18f, 0.34f };

        private sealed class PlayerSoundStimulus
        {
            public float X;
            public float Y;
            public float Radius;
            public float Age;
            public float DirX;
            public float DirY;
        }

        private struct PerceptionContext
        {
            public float PlayerX;
            public float PlayerY;
            public float DirX;
            public float DirY;
            public float Distance;
            public float FacingDot;
            public bool CanSeePlayer;
            public bool HeardSound;
            public float HeardX;
            public float HeardY;
            public float HeardDirX;
            public float HeardDirY;
        }

        private Enemy[] enemies;
        private readonly List<EnemyProjectile> enemyProjectiles;
        private readonly List<Enemy> enemyBuildBuffer;
        private readonly List<PlayerSoundStimulus> playerSoundStimuli;
        private readonly TextureManager textureManager;
        private readonly EffectSoundManager sfxChannel;
        private Random rng;
        private readonly Dictionary<string, IEnemyBossPattern> bossPatterns;
        private float enemyHealthMultiplier;
        private float enemyDamageMultiplier;
        private float enemyMoveSpeedMultiplier;
        private Enemy cachedBossEnemy;

        public Enemy[] Enemies => enemies;

        public IList<EnemyProjectile> EnemyProjectiles => enemyProjectiles;

        public Action<Enemy> EnemyDeathCallback { get; set; }

        /// <summary>적 스폰·AI 난수를 런 시드에서 파생한 값으로 다시 만든다.</summary>
        internal void Reseed(int seed)
        {
            rng = new Random(seed);
        }

        internal EnemyManager(TextureManager textureManager, EffectSoundManager sfxChannel = null)
        {
            this.textureManager = textureManager;
            this.sfxChannel = sfxChannel;
            rng = new Random();
            bossPatterns = new Dictionary<string, IEnemyBossPattern>(StringComparer.OrdinalIgnoreCase);
            enemyProjectiles = new List<EnemyProjectile>();
            enemyBuildBuffer = new List<Enemy>();
            playerSoundStimuli = new List<PlayerSoundStimulus>();
            enemies = Array.Empty<Enemy>();
            enemyHealthMultiplier = 1f;
            enemyDamageMultiplier = 1f;
            enemyMoveSpeedMultiplier = 1f;
            cachedBossEnemy = null;
            RegisterBossPatterns();
        }

        public void ConfigureDifficulty(float enemyHealthMultiplier, float enemyDamageMultiplier, float enemyMoveSpeedMultiplier)
        {
            this.enemyHealthMultiplier = Math.Max(0.5f, enemyHealthMultiplier);
            this.enemyDamageMultiplier = Math.Max(0.5f, enemyDamageMultiplier);
            this.enemyMoveSpeedMultiplier = Math.Max(0.5f, enemyMoveSpeedMultiplier);
        }

        public void BuildFromMap(int[,] map, Vector2 playerPosition, CollisionSystem collision)
        {
            enemies = Array.Empty<Enemy>();
            enemyProjectiles.Clear();
            playerSoundStimuli.Clear();
            cachedBossEnemy = null;
        }

        public int SpawnStageEncounter(StageRoom room, CollisionSystem collision, Vector2 playerPosition)
        {
            if (room == null)
            {
                return 0;
            }

            StageSpawnPoint[] spawns = room.Blueprint.Spawns;
            if (spawns == null || spawns.Length == 0)
            {
                return 0;
            }

            List<Enemy> list = PrepareEnemyBuildBuffer();
            int spawned = 0;

            foreach (StageSpawnPoint spawn in spawns)
            {
                if (TrySpawnStageEnemy(list, room, spawn, playerPosition, collision))
                {
                    spawned++;
                }
            }

            if (room.ObjectiveKind == RoomObjectiveKind.KeyTarget && CountAliveObjectiveTargets(list) <= 0)
            {
                if (TrySpawnObjectiveTargetFallback(list, room, collision, playerPosition))
                {
                    spawned++;
                }
            }

            CommitEnemyBuildBuffer();
            RefreshBossCache();
            return spawned;
        }

        public int SpawnStageReinforcements(StageRoom room, CollisionSystem collision, Vector2 playerPosition, int maxCount)
        {
            if (room == null || maxCount <= 0)
            {
                return 0;
            }

            StageSpawnPoint[] spawns = room.Blueprint.Spawns;
            if (spawns == null || spawns.Length == 0)
            {
                return 0;
            }

            List<Enemy> list = PrepareEnemyBuildBuffer();
            int spawned = 0;
            int start = rng.Next(spawns.Length);
            int attempts = spawns.Length * 2;
            for (int i = 0; i < attempts && spawned < maxCount; i++)
            {
                StageSpawnPoint source = spawns[(start + i) % spawns.Length];
                if (source == null || source.IsObjectiveTarget)
                {
                    continue;
                }

                if (TrySpawnStageEnemy(list, room, source, playerPosition, collision, allowNearPlayer: i >= spawns.Length))
                {
                    spawned++;
                }
            }

            CommitEnemyBuildBuffer();
            RefreshBossCache();
            return spawned;
        }

        public Enemy GetBossEnemy()
        {
            if (cachedBossEnemy != null && cachedBossEnemy.Alive && cachedBossEnemy.IsBoss)
            {
                return cachedBossEnemy;
            }

            if (enemies == null)
            {
                return null;
            }

            for (int i = 0; i < enemies.Length; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy != null && enemy.Alive && enemy.IsBoss)
                {
                    cachedBossEnemy = enemy;
                    return enemy;
                }
            }

            cachedBossEnemy = null;
            return null;
        }

        public void ResetAllEnemies()
        {
            if (enemies == null)
            {
                return;
            }

            for (int i = 0; i < enemies.Length; i++)
            {
                enemies[i]?.Reset();
            }

            enemyProjectiles.Clear();
            playerSoundStimuli.Clear();
            RefreshBossCache();
        }

        public void EmitPlayerSound(float x, float y, float radius, float dirX = 0f, float dirY = 0f)
        {
            if (radius <= 0f)
            {
                return;
            }

            Normalize(ref dirX, ref dirY);
            playerSoundStimuli.Add(new PlayerSoundStimulus
            {
                X = x,
                Y = y,
                Radius = radius,
                Age = 0f,
                DirX = dirX,
                DirY = dirY
            });

            if (playerSoundStimuli.Count > 48)
            {
                playerSoundStimuli.RemoveAt(0);
            }
        }

        public void Update(
            float dt,
            Vector2 playerPosition,
            bool playerDead,
            CollisionSystem collision,
            Action<float, float, float> onPlayerDamaged)
        {
            UpdatePlayerSoundStimuli(dt);
            UpdateEnemies(dt, playerPosition, playerDead, collision, onPlayerDamaged);
            RevealIsolatedObjectiveTargets();
            UpdateEnemyProjectiles(dt, collision, playerPosition, onPlayerDamaged);
            RemoveExpiredDeadEnemies();
        }

        public int CountAliveEnemies()
        {
            if (enemies == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < enemies.Length; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy != null && enemy.Alive)
                {
                    count++;
                }
            }

            return count;
        }

        public int CountAliveObjectiveTargets()
        {
            if (enemies == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < enemies.Length; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy != null && enemy.Alive && enemy.IsObjectiveTarget)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountAliveObjectiveTargets(List<Enemy> list)
        {
            if (list == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < list.Count; i++)
            {
                Enemy enemy = list[i];
                if (enemy != null && enemy.Alive && enemy.IsObjectiveTarget)
                {
                    count++;
                }
            }

            return count;
        }

        private bool TrySpawnObjectiveTargetFallback(List<Enemy> list, StageRoom room, CollisionSystem collision, Vector2 playerPosition)
        {
            StageSpawnPoint source = FindObjectiveTargetSpawn(room) ?? FindFirstSpawn(room);
            if (source == null)
            {
                return false;
            }

            StageSpawnPoint fallback = CloneSpawnPoint(source);
            fallback.IsObjectiveTarget = true;
            fallback.HealthMultiplier = Math.Max(fallback.HealthMultiplier, GameConfig.KeyTargetHealthMultiplier);
            fallback.ScaleMultiplier *= GameConfig.KeyTargetScaleMultiplier;

            PointF[] fallbackPositions =
            {
                new PointF(room.Bounds.Left + room.Bounds.Width * 0.5f, room.Bounds.Top + room.Bounds.Height * 0.5f),
                new PointF(room.Bounds.Left + 2.5f, room.Bounds.Top + 2.5f),
                new PointF(room.Bounds.Right - 2.5f, room.Bounds.Top + 2.5f),
                new PointF(room.Bounds.Left + 2.5f, room.Bounds.Bottom - 2.5f),
                new PointF(room.Bounds.Right - 2.5f, room.Bounds.Bottom - 2.5f)
            };

            for (int i = 0; i < fallbackPositions.Length; i++)
            {
                fallback.X = fallbackPositions[i].X;
                fallback.Y = fallbackPositions[i].Y;
                if (TrySpawnStageEnemy(list, room, fallback, playerPosition, collision, allowNearPlayer: true))
                {
                    return true;
                }
            }

            return false;
        }

        private void RevealIsolatedObjectiveTargets()
        {
            if (enemies == null || enemies.Length == 0)
            {
                return;
            }

            bool hasAliveObjectiveTarget = false;
            bool hasAliveNonObjectiveEnemy = false;
            for (int i = 0; i < enemies.Length; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy == null || !enemy.Alive)
                {
                    continue;
                }

                if (enemy.IsObjectiveTarget)
                {
                    hasAliveObjectiveTarget = true;
                }
                else
                {
                    hasAliveNonObjectiveEnemy = true;
                }
            }

            if (!hasAliveObjectiveTarget || hasAliveNonObjectiveEnemy)
            {
                return;
            }

            for (int i = 0; i < enemies.Length; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy != null && enemy.Alive && enemy.IsObjectiveTarget)
                {
                    enemy.RevealObjectiveTarget();
                }
            }
        }

        private static StageSpawnPoint FindObjectiveTargetSpawn(StageRoom room)
        {
            StageSpawnPoint[] spawns = room?.Blueprint.Spawns;
            if (spawns == null)
            {
                return null;
            }

            for (int i = 0; i < spawns.Length; i++)
            {
                if (spawns[i]?.IsObjectiveTarget == true)
                {
                    return spawns[i];
                }
            }

            return null;
        }

        private static StageSpawnPoint FindFirstSpawn(StageRoom room)
        {
            StageSpawnPoint[] spawns = room?.Blueprint.Spawns;
            if (spawns == null || spawns.Length == 0)
            {
                return null;
            }

            return spawns[0];
        }

        private static StageSpawnPoint CloneSpawnPoint(StageSpawnPoint source)
        {
            return new StageSpawnPoint
            {
                EnemyAssetId = source.EnemyAssetId,
                Type = source.Type,
                Rank = source.Rank,
                BehaviorPattern = source.BehaviorPattern,
                BehaviorPatternPool = source.BehaviorPatternPool == null ? null : (EnemyBehaviorPattern[])source.BehaviorPatternPool.Clone(),
                X = source.X,
                Y = source.Y,
                HealthMultiplier = source.HealthMultiplier,
                DamageMultiplier = source.DamageMultiplier,
                MoveSpeedMultiplier = source.MoveSpeedMultiplier,
                AttackRangeMultiplier = source.AttackRangeMultiplier,
                ScaleMultiplier = source.ScaleMultiplier,
                IsBoss = source.IsBoss,
                DisplayName = source.DisplayName,
                SpriteVariantKey = source.SpriteVariantKey,
                IsObjectiveTarget = source.IsObjectiveTarget
            };
        }

        public void ClearActiveEncounter()
        {
            enemies = Array.Empty<Enemy>();
            enemyProjectiles.Clear();
            playerSoundStimuli.Clear();
            cachedBossEnemy = null;
        }

        private void UpdatePlayerSoundStimuli(float dt)
        {
            float dtClamped = Math.Max(0f, dt);
            for (int i = playerSoundStimuli.Count - 1; i >= 0; i--)
            {
                PlayerSoundStimulus stimulus = playerSoundStimuli[i];
                stimulus.Age += dtClamped;
                if (stimulus.Age > PlayerSoundStimulusLifetime)
                {
                    playerSoundStimuli.RemoveAt(i);
                }
            }
        }

        private void PerformEnemyAttack(Enemy enemy, EnemySoundCueType cueType = EnemySoundCueType.Attack)
        {
            if (enemy == null)
            {
                return;
            }

            enemy.PerformAttack();
            PlayEnemySound(enemy, cueType);
        }

        private void PlayEnemySound(Enemy enemy, EnemySoundCueType cueType, bool stopFirst = false, bool loop = false)
        {
            string alias = ResolveEnemySoundAlias(enemy, cueType);
            if (!string.IsNullOrWhiteSpace(alias))
            {
                sfxChannel?.PlaySound(alias, stopFirst, loop);
            }
        }

        private string ResolveEnemySoundAlias(Enemy enemy, EnemySoundCueType cueType)
        {
            if (enemy == null || sfxChannel == null)
            {
                return null;
            }

            EnemySoundCue cue = enemy.SoundProfile?.GetCue(cueType);
            if (cue != null)
            {
                if (!string.IsNullOrWhiteSpace(cue.Alias) && sfxChannel.HasSound(cue.Alias))
                    return cue.Alias;
                if (!string.IsNullOrWhiteSpace(cue.FallbackAlias) && sfxChannel.HasSound(cue.FallbackAlias))
                    return cue.FallbackAlias;
            }

            // SoundProfile에 큐가 없을 때: 자동 탐색으로 등록된 alias 패턴으로 폴백
            if (!string.IsNullOrWhiteSpace(enemy.AssetId))
            {
                string autoAlias = "enemy." + enemy.AssetId.ToLowerInvariant() + "." + cueType.ToString().ToLowerInvariant();
                if (sfxChannel.HasSound(autoAlias))
                    return autoAlias;
            }

            return null;
        }

        private void HandleEnemyDeath(Enemy enemy)
        {
            PlayEnemySound(enemy, EnemySoundCueType.Death);
            EnemyDeathCallback?.Invoke(enemy);
        }

        private void RemoveExpiredDeadEnemies()
        {
            if (enemies == null || enemies.Length == 0)
            {
                return;
            }

            int writeIdx = 0;
            for (int i = 0; i < enemies.Length; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy != null && enemy.IsRenderable)
                {
                    enemies[writeIdx++] = enemy;
                }
            }

            if (writeIdx == enemies.Length)
            {
                return;
            }

            if (writeIdx == 0)
            {
                enemies = Array.Empty<Enemy>();
                RefreshBossCache();
                return;
            }

            Array.Resize(ref enemies, writeIdx);
            RefreshBossCache();
        }

        private void RefreshBossCache()
        {
            cachedBossEnemy = null;
            if (enemies == null)
            {
                return;
            }

            for (int i = 0; i < enemies.Length; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy != null && enemy.Alive && enemy.IsBoss)
                {
                    cachedBossEnemy = enemy;
                    return;
                }
            }
        }

        private List<Enemy> PrepareEnemyBuildBuffer()
        {
            enemyBuildBuffer.Clear();
            if (enemies != null && enemies.Length > 0)
            {
                enemyBuildBuffer.AddRange(enemies);
            }

            return enemyBuildBuffer;
        }

        private void CommitEnemyBuildBuffer()
        {
            enemies = enemyBuildBuffer.ToArray();
            enemyBuildBuffer.Clear();
            if (enemyBuildBuffer.Capacity > 64)
            {
                enemyBuildBuffer.Capacity = 64;
            }
        }

        private static bool IsRecoverState(EnemyAiState state)
        {
            return state == EnemyAiState.AttackRecover;
        }

        private static bool IsWindupState(EnemyAiState state)
        {
            return state == EnemyAiState.AttackWindup;
        }

        private static void Normalize(ref float x, ref float y)
        {
            float len = (float)Math.Sqrt((x * x) + (y * y));
            if (len <= 0.001f)
            {
                x = 0f;
                y = 0f;
                return;
            }

            x /= len;
            y /= len;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        private void RegisterBossPatterns()
        {
            RegisterBossPattern(new AzazelBossPattern(), "feral_alpha", "azazel", "abaddon");
            RegisterBossPattern(new BehemothBossPattern(), "bulwark_colossus", "behemoth", "annihilator", "aracnorb_queen");
            RegisterBossPattern(new ArachnocortexBossPattern(), "ashen_artillerist", "arachnocortex", "afrit", "arachnobaron");
            RegisterBossPattern(new AgauresBossPattern(), "rift_strider", "agaures", "agatho_demon", "arachnophyte");
        }

        private void RegisterBossPattern(IEnemyBossPattern pattern, params string[] assetIds)
        {
            if (pattern == null || assetIds == null)
            {
                return;
            }

            for (int i = 0; i < assetIds.Length; i++)
            {
                string assetId = assetIds[i];
                if (string.IsNullOrWhiteSpace(assetId))
                {
                    continue;
                }

                bossPatterns[assetId] = pattern;
            }
        }
    }
}
