using System;
using System.Drawing;
using My2DEngine.Game.Config;

namespace My2DEngine.Game
{
    /// <summary>
    /// 상태 머신이 다음 실행할 전투 액션 종류다.
    /// </summary>
    public enum EnemyPendingAction
    {
        None,
        MeleeAttack,
        RangedAttack,
        AzazelInfernoDash,
        AzazelHellfireBarrage,
        BehemothShockwave,
        BehemothSiegeBurst,
        ArachnocortexPlasmaFan,
        ArachnocortexSuppressionGrid,
        AgauresBlinkClaw,
        AgauresRiftCrossfire
    }

    /// <summary>
    /// 실제 전투 중 존재하는 적 인스턴스의 모든 런타임 상태를 담는 엔티티다.
    /// 정의(EnemyDefinition)에서 스탯을 읽어 초기화하고, EnemyManager가 프레임마다 갱신한다.
    /// </summary>
    public class Enemy
    {
        public float X { get; set; }

        public float Y { get; set; }

        public EnemyType Type { get; private set; }

        public string AssetId { get; private set; }

        public EnemyRank Rank { get; private set; }

        public EnemyBehaviorPattern BehaviorPattern { get; private set; }

        public EnemyAiProfile AiProfile { get; private set; }

        public EnemySoundProfile SoundProfile { get; private set; }

        public string SpriteVariantKey { get; private set; }

        public Color[] Sprite { get; private set; }

        public float Scale { get; private set; }

        public float Health { get; set; }

        public float MaxHealth { get; private set; }

        public bool Alive { get; set; }

        public bool IsBoss => Rank == EnemyRank.Boss;

        public bool IsMiniBoss => Rank == EnemyRank.MiniBoss;

        public bool IsObjectiveTarget { get; set; }

        public string DisplayName { get; private set; }

        public float MoveSpeed { get; private set; }

        public float AttackRange { get; private set; }

        public float AttackDamage { get; private set; }

        public float AttackCooldownDuration { get; private set; }

        public float Radius { get; private set; }

        public float AttackCooldown { get; set; }

        public float StunTimer { get; set; }

        public float HitFlash { get; set; }

        public float HitReactTimer { get; set; }

        public float WanderDirX { get; private set; }

        public float WanderDirY { get; private set; }

        public float WanderTimer { get; set; }

        public float IdleTimer { get; set; }

        public float BehaviorTimer { get; set; }

        public float BehaviorPhase { get; set; }

        public float MoveDirX { get; set; }

        public float MoveDirY { get; set; }

        public float SpecialCooldown { get; set; }

        public float UltimateCooldown { get; set; }

        public float UltimateTimer { get; set; }

        public float TelegraphTimer { get; set; }

        public float ChargeTimer { get; set; }

        public float ChargeDirX { get; set; }

        public float ChargeDirY { get; set; }

        public EnemyAiState State { get; set; }

        public float StateTimer { get; set; }

        public EnemyPendingAction PendingAction { get; set; }

        public float FacingDirX { get; set; }

        public float FacingDirY { get; set; }

        public float MemoryTimer { get; set; }

        public float SearchRetargetTimer { get; set; }

        public float LastSeenPlayerX { get; set; }

        public float LastSeenPlayerY { get; set; }

        public float LastHeardPlayerX { get; set; }

        public float LastHeardPlayerY { get; set; }

        public float LastKnownPlayerX { get; set; }

        public float LastKnownPlayerY { get; set; }

        public float LastKnownDirX { get; set; }

        public float LastKnownDirY { get; set; }

        public float SearchTargetX { get; set; }

        public float SearchTargetY { get; set; }

        public bool HasLastKnownPlayer { get; set; }

        public bool LastStimulusWasSound { get; set; }

        public float PendingDirX { get; set; }

        public float PendingDirY { get; set; }

        public float PendingTargetX { get; set; }

        public float PendingTargetY { get; set; }

        public float AnimationTime { get; set; }

        public float SpawnAnimationTime { get; set; }

        public float SpawnAnimationTimer { get; set; }

        public float RecentMovementTime { get; set; }

        public float AttackAnimationTimer { get; set; }

        public float DeathAnimationTime { get; set; }

        public float DeathAnimationTimer { get; set; }

        public RectangleF LeashBounds { get; private set; }

        public bool IsIllusion { get; private set; }

        public bool HasLeashBounds => LeashBounds.Width > 0f && LeashBounds.Height > 0f;

        public bool IsSpawning => Alive && SpawnAnimationTimer > 0f;

        public bool IsDeathAnimating => !Alive && DeathAnimationTimer > 0f;

        public bool IsRenderable => Alive || IsDeathAnimating;

        public Action<Enemy> DeathCallback { get; set; }

        public Enemy(
            float x,
            float y,
            EnemyDefinition definition,
            Color[] sprite,
            EnemyRank rank = EnemyRank.Normal,
            string displayName = null,
            EnemyBehaviorPattern behaviorPattern = EnemyBehaviorPattern.Default,
            string spriteVariantKey = null,
            bool isIllusion = false,
            string assetId = null,
            EnemyAiProfile aiProfile = null,
            EnemySoundProfile soundProfile = null)
        {
            EnemyAiProfile resolvedAiProfile = aiProfile ?? new EnemyAiProfile(behaviorPattern);

            X = x;
            Y = y;
            Type = definition.Type;
            AssetId = assetId;
            Rank = rank;
            AiProfile = resolvedAiProfile;
            BehaviorPattern = resolvedAiProfile.MovementPattern;
            SoundProfile = soundProfile ?? EnemySoundProfile.Silent;
            SpriteVariantKey = spriteVariantKey;
            IsIllusion = isIllusion;
            Sprite = sprite;
            Scale = definition.Scale;
            Health = definition.MaxHealth;
            MaxHealth = definition.MaxHealth;
            MoveSpeed = definition.MoveSpeed;
            AttackRange = definition.AttackRange;
            AttackDamage = definition.AttackDamage;
            AttackCooldownDuration = definition.AttackCooldownDuration;
            Radius = definition.Radius;
            Alive = true;
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? (rank == EnemyRank.Boss ? "Arena Warden" : rank == EnemyRank.MiniBoss ? "Mini Boss" : null)
                : displayName;

            ResetRuntimeState();
        }

        public void Reset()
        {
            Health = MaxHealth;
            Alive = true;
            ResetRuntimeState();
        }

        public void TakeDamage(float damage)
        {
            if (!Alive)
            {
                return;
            }

            Health -= damage;
            HitFlash = Math.Max(HitFlash, GameConfig.EnemyHitFlashDuration);
            HitReactTimer = Math.Max(HitReactTimer, GameConfig.EnemyHitReactDuration);

            if (Health > 0f)
            {
                return;
            }

            Health = 0f;
            Alive = false;
            WanderDirX = 0f;
            WanderDirY = 0f;
            WanderTimer = 0f;
            IdleTimer = 0f;
            BehaviorTimer = 0f;
            BehaviorPhase = 1f;
            MoveDirX = 0f;
            MoveDirY = 0f;
            SpecialCooldown = 0f;
            UltimateCooldown = 0f;
            UltimateTimer = 0f;
            TelegraphTimer = 0f;
            ChargeTimer = 0f;
            ChargeDirX = 0f;
            ChargeDirY = 0f;
            State = EnemyAiState.Patrol;
            StateTimer = 0f;
            PendingAction = EnemyPendingAction.None;
            MemoryTimer = 0f;
            SearchRetargetTimer = 0f;
            HasLastKnownPlayer = false;
            LastStimulusWasSound = false;
            PendingDirX = 0f;
            PendingDirY = 0f;
            PendingTargetX = 0f;
            PendingTargetY = 0f;
            SpawnAnimationTime = 0f;
            SpawnAnimationTimer = 0f;
            RecentMovementTime = 0f;
            AttackAnimationTimer = 0f;
            DeathAnimationTime = 0f;
            DeathAnimationTimer = 1.45f;
            DeathCallback?.Invoke(this);
        }

        public void UpdateTimers(float dt)
        {
            if (dt <= 0f)
            {
                return;
            }

            if (!Alive)
            {
                TickDeathTimers(dt);
                return;
            }

            if (SpawnAnimationTimer > 0f)
            {
                SpawnAnimationTime += dt;
                SpawnAnimationTimer = Math.Max(0f, SpawnAnimationTimer - dt);
                HitFlash = Math.Max(0f, HitFlash - dt);
                HitReactTimer = Math.Max(0f, HitReactTimer - dt);
                return;
            }

            AnimationTime += dt;
            AttackCooldown = Math.Max(0f, AttackCooldown - dt);
            HitFlash = Math.Max(0f, HitFlash - dt);
            HitReactTimer = Math.Max(0f, HitReactTimer - dt);
            WanderTimer = Math.Max(0f, WanderTimer - dt);
            IdleTimer = Math.Max(0f, IdleTimer - dt);
            StunTimer = Math.Max(0f, StunTimer - dt);
            SpecialCooldown = Math.Max(0f, SpecialCooldown - dt);
            UltimateCooldown = Math.Max(0f, UltimateCooldown - dt);
            UltimateTimer = Math.Max(0f, UltimateTimer - dt);
            TelegraphTimer = Math.Max(0f, TelegraphTimer - dt);
            ChargeTimer = Math.Max(0f, ChargeTimer - dt);
            RecentMovementTime = Math.Max(0f, RecentMovementTime - dt);
            AttackAnimationTimer = Math.Max(0f, AttackAnimationTimer - dt);
            StateTimer = Math.Max(0f, StateTimer - dt);
            SearchRetargetTimer = Math.Max(0f, SearchRetargetTimer - dt);
            MemoryTimer = Math.Max(0f, MemoryTimer - dt);

            if (MemoryTimer <= 0f)
            {
                HasLastKnownPlayer = false;
                LastStimulusWasSound = false;
            }
        }

        public bool CanAttack()
        {
            return AttackCooldown <= 0f;
        }

        public void PerformAttack()
        {
            AttackCooldown = AttackCooldownDuration;
            AttackAnimationTimer = 0.42f;
        }

        public void BeginSpawnAnimation(float duration)
        {
            if (duration <= 0f)
            {
                SpawnAnimationTime = 0f;
                SpawnAnimationTimer = 0f;
                return;
            }

            SpawnAnimationTime = 0f;
            SpawnAnimationTimer = duration;
            AnimationTime = 0f;
            RecentMovementTime = 0f;
            AttackAnimationTimer = 0f;
        }

        public void MarkMovement(float movedX, float movedY)
        {
            if ((movedX * movedX) + (movedY * movedY) >= 0.0004f)
            {
                RecentMovementTime = 0.18f;
                SetFacingDirection(movedX, movedY);
            }
        }

        public void SetFacingDirection(float dirX, float dirY)
        {
            float len = (float)Math.Sqrt((dirX * dirX) + (dirY * dirY));
            if (len <= 0.001f)
            {
                return;
            }

            FacingDirX = dirX / len;
            FacingDirY = dirY / len;
        }

        public void SetWanderDirection(float dirX, float dirY, float duration)
        {
            float len = (float)Math.Sqrt((dirX * dirX) + (dirY * dirY));
            if (len <= 0.001f)
            {
                WanderDirX = 0f;
                WanderDirY = 0f;
                WanderTimer = 0f;
                return;
            }

            WanderDirX = dirX / len;
            WanderDirY = dirY / len;
            WanderTimer = duration;
            IdleTimer = 0f;
            SetFacingDirection(WanderDirX, WanderDirY);
        }

        public void SetIdle(float duration)
        {
            WanderDirX = 0f;
            WanderDirY = 0f;
            WanderTimer = 0f;
            IdleTimer = duration;
        }

        public void SetLeashBounds(RectangleF bounds)
        {
            LeashBounds = bounds;
            ClampToLeash();
        }

        public bool IsInsideLeash(float x, float y)
        {
            if (!HasLeashBounds)
            {
                return true;
            }

            return x >= LeashBounds.Left &&
                x <= LeashBounds.Right &&
                y >= LeashBounds.Top &&
                y <= LeashBounds.Bottom;
        }

        public void ClampToLeash()
        {
            if (!HasLeashBounds)
            {
                return;
            }

            float minX = LeashBounds.Left + Radius;
            float maxX = LeashBounds.Right - Radius;
            float minY = LeashBounds.Top + Radius;
            float maxY = LeashBounds.Bottom - Radius;

            if (minX > maxX)
            {
                float centerX = (LeashBounds.Left + LeashBounds.Right) * 0.5f;
                minX = centerX;
                maxX = centerX;
            }

            if (minY > maxY)
            {
                float centerY = (LeashBounds.Top + LeashBounds.Bottom) * 0.5f;
                minY = centerY;
                maxY = centerY;
            }

            if (X < minX) X = minX;
            if (X > maxX) X = maxX;
            if (Y < minY) Y = minY;
            if (Y > maxY) Y = maxY;
        }

        private void ResetRuntimeState()
        {
            AttackCooldown = 0f;
            StunTimer = 0f;
            HitFlash = 0f;
            HitReactTimer = 0f;
            WanderDirX = 0f;
            WanderDirY = 0f;
            WanderTimer = 0f;
            IdleTimer = 0f;
            BehaviorTimer = 0f;
            BehaviorPhase = 1f;
            MoveDirX = 0f;
            MoveDirY = 0f;
            SpecialCooldown = 0f;
            UltimateCooldown = 0f;
            UltimateTimer = 0f;
            TelegraphTimer = 0f;
            ChargeTimer = 0f;
            ChargeDirX = 0f;
            ChargeDirY = 0f;
            State = EnemyAiState.Patrol;
            StateTimer = 0f;
            PendingAction = EnemyPendingAction.None;
            FacingDirX = 0f;
            FacingDirY = -1f;
            MemoryTimer = 0f;
            SearchRetargetTimer = 0f;
            LastSeenPlayerX = X;
            LastSeenPlayerY = Y;
            LastHeardPlayerX = X;
            LastHeardPlayerY = Y;
            LastKnownPlayerX = X;
            LastKnownPlayerY = Y;
            LastKnownDirX = 0f;
            LastKnownDirY = -1f;
            SearchTargetX = X;
            SearchTargetY = Y;
            HasLastKnownPlayer = false;
            LastStimulusWasSound = false;
            PendingDirX = 0f;
            PendingDirY = 0f;
            PendingTargetX = 0f;
            PendingTargetY = 0f;
            AnimationTime = 0f;
            SpawnAnimationTime = 0f;
            SpawnAnimationTimer = 0f;
            RecentMovementTime = 0f;
            AttackAnimationTimer = 0f;
            DeathAnimationTime = 0f;
            DeathAnimationTimer = 0f;
        }

        private void TickDeathTimers(float dt)
        {
            if (DeathAnimationTimer > 0f)
            {
                DeathAnimationTime += dt;
                DeathAnimationTimer = Math.Max(0f, DeathAnimationTimer - dt);
            }

            HitFlash = Math.Max(0f, HitFlash - dt);
            HitReactTimer = Math.Max(0f, HitReactTimer - dt);
        }
    }
}
