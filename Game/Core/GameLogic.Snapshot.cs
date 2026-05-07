using My2DEngine.Engine.Math;
using My2DEngine.Game.Config;
using My2DEngine.Game.Map;

namespace My2DEngine.Game.Core
{
    /// <summary>
    /// 스모크 테스트가 GameLogic의 최소 상태를 읽을 수 있도록 노출하는 내부 스냅샷 클래스.
    /// 게임 로직 내부를 직접 참조하지 않고 테스트 어설션에 필요한 값만 담아 반환한다.
    /// </summary>
    internal sealed class GameLogicSmokeSnapshot
    {
        /// <summary>맵 타일 배열. 0은 빈 공간, 0이 아니면 벽 텍스처 인덱스를 나타낸다.</summary>
        public int[,] Map { get; set; }

        /// <summary>충돌 시스템(<see cref="CollisionSystem"/>)이 초기화되었는지 여부.</summary>
        public bool CollisionInitialized { get; set; }

        /// <summary>로드된 벽 텍스처의 수. 0이면 텍스처 로딩에 실패한 것이다.</summary>
        public int WallTextureCount { get; set; }

        /// <summary>현재 맵에 스테이지 진행 흐름(방·연결·보스)이 정의되어 있는지 여부.</summary>
        public bool HasStageFlow { get; set; }

        /// <summary>현재 맵에 있는 스테이지 방의 수.</summary>
        public int StageRoomCount { get; set; }

        /// <summary>스냅샷 생성 시점의 플레이어 월드 위치.</summary>
        public Vector2 PlayerPosition { get; set; }

        /// <summary>스냅샷 생성 시점의 플레이어 바라보는 방향(단위 벡터).</summary>
        public Vector2 PlayerDirection { get; set; }

        /// <summary>스냅샷 생성 시점의 플레이어 시야각(도).</summary>
        public float PlayerFovDegrees { get; set; }

        /// <summary>스냅샷 생성 시점의 활성 플레이어 투사체 수.</summary>
        public int PlayerProjectileCount { get; set; }

        /// <summary>첫 활성 플레이어 투사체의 종류. 투사체가 없으면 기본값이다.</summary>
        public EnemyProjectileKind FirstPlayerProjectileKind { get; set; }

        /// <summary>첫 활성 플레이어 투사체의 속도 제곱. 투사체가 없으면 0이다.</summary>
        public float FirstPlayerProjectileSpeedSquared { get; set; }

        /// <summary>첫 활성 플레이어 투사체의 폭발 반경. 투사체가 없으면 0이다.</summary>
        public float FirstPlayerProjectileExplosionRadius { get; set; }
    }

    internal sealed class PlayerDamageSmokeResult
    {
        public float Health { get; set; }
        public float Shield { get; set; }
        public float ShieldRegenDelayTimer { get; set; }
    }

    internal sealed class KillBonusSmokeResult
    {
        public float Health { get; set; }
        public float DashCooldownTimer { get; set; }
    }

    internal sealed class ShieldSettingsSmokeResult
    {
        public float RegenRate { get; set; }
        public float RegenDelayDuration { get; set; }
    }

    /// <summary>
    /// GameLogic의 테스트용 관찰 API partial.
    /// 내부 상태를 <see cref="GameLogicSmokeSnapshot"/>으로 노출하여
    /// 외부 테스트 코드가 구현 세부 사항 없이 핵심 초기화 상태를 검증할 수 있게 한다.
    /// </summary>
    public partial class GameLogic
    {
        /// <summary>
        /// 현재 게임 로직의 핵심 상태를 스모크 테스트용 스냅샷으로 캡처하여 반환한다.
        /// 맵·충돌·텍스처·스테이지 방·플레이어 상태를 한 번에 읽을 수 있다.
        /// </summary>
        /// <returns>현재 상태를 담은 <see cref="GameLogicSmokeSnapshot"/> 인스턴스.</returns>
        internal GameLogicSmokeSnapshot CreateSmokeSnapshot()
        {
            GameLogicSmokeSnapshot snapshot = new GameLogicSmokeSnapshot
            {
                Map = mapManager.Map,
                CollisionInitialized = collision != null,
                WallTextureCount = textureManager.WallTextures == null ? 0 : textureManager.WallTextures.Length,
                HasStageFlow = mapManager.HasStageFlow,
                StageRoomCount = mapManager.StageRooms == null ? 0 : mapManager.StageRooms.Length,
                PlayerPosition = player.Position,
                PlayerDirection = player.Direction,
                PlayerFovDegrees = player.FovDegrees,
                PlayerProjectileCount = playerProjectiles == null ? 0 : playerProjectiles.Count
            };

            if (playerProjectiles != null && playerProjectiles.Count > 0)
            {
                EnemyProjectile projectile = playerProjectiles[0];
                if (projectile != null)
                {
                    snapshot.FirstPlayerProjectileKind = projectile.Kind;
                    snapshot.FirstPlayerProjectileSpeedSquared =
                        (projectile.VelocityX * projectile.VelocityX) +
                        (projectile.VelocityY * projectile.VelocityY);
                    snapshot.FirstPlayerProjectileExplosionRadius = projectile.ExplosionRadius;
                }
            }

            return snapshot;
        }

        internal RewardCardOffer[] CreateRestShopCardOfferSmokeSnapshot(float luckValue)
        {
            PermanentProgressionData previousProgression = permanentProgression;
            try
            {
                permanentProgression = PermanentProgressionData.CreateDefault();
                permanentProgression.LuckValue = luckValue;
                permanentProgression.Sanitize();
                return GenerateRestShopCardOffers(3);
            }
            finally
            {
                permanentProgression = previousProgression;
            }
        }

        internal int GetRestShopCardCostSmokeSnapshot(CardGrade grade)
        {
            return GetRestShopCardCost(grade);
        }

        internal int GetRoomRewardGradeBoostSmokeSnapshot(RoomTemplate template)
        {
            return RoomTemplateLibrary.GetCardRewardGradeBoost(template);
        }

        internal int GetExtremeRoomMinimumRewardGradeSmokeSnapshot(RoomTemplate template, float luckValue)
        {
            PermanentProgressionData previousProgression = permanentProgression;
            try
            {
                permanentProgression = PermanentProgressionData.CreateDefault();
                permanentProgression.LuckValue = luckValue;
                permanentProgression.Sanitize();
                return RoomTemplateLibrary.IsExtremeRewardRoom(template)
                    ? (int)GetOneGradeAboveHighestLuckAvailableStatOfferGrade()
                    : -1;
            }
            finally
            {
                permanentProgression = previousProgression;
            }
        }

        internal int GetRestShopCardCostSmokeSnapshot(CardGrade grade, float shopDiscount)
        {
            float previous = runStatBonusTotals[(int)StatType.ShopDiscount];
            try
            {
                runStatBonusTotals[(int)StatType.ShopDiscount] = shopDiscount;
                return GetRestShopCardCost(grade);
            }
            finally
            {
                runStatBonusTotals[(int)StatType.ShopDiscount] = previous;
            }
        }

        internal bool CanOfferRestRoomForNextSelectionSmokeSnapshot(
            bool currentFloorAllowsRestRoom,
            bool restRoomCooldownActive)
        {
            return ShouldAllowRestRoomForNextSelection(currentFloorAllowsRestRoom, restRoomCooldownActive);
        }

        internal bool BranchIncludesRestRoomSmokeSnapshot(bool optionAIsRestRoom, bool optionBIsRestRoom)
        {
            RoomTemplate optionA = new RoomTemplate { IsRestRoom = optionAIsRestRoom };
            RoomTemplate optionB = new RoomTemplate { IsRestRoom = optionBIsRestRoom };
            return BranchIncludesRestRoom(optionA, optionB);
        }

        internal float ClampRunStatBonusSmokeSnapshot(StatType stat, float value)
        {
            return ClampRunStatBonusTotal(stat, value);
        }

        internal bool IsStatInRewardPoolSmokeSnapshot(StatType stat)
        {
            for (int i = 0; i < RewardStatTypes.Length; i++)
            {
                if (RewardStatTypes[i] == stat)
                {
                    return true;
                }
            }

            return false;
        }

        internal bool IsStatOfferAvailableSmokeSnapshot(StatType stat, int floor, bool cardChoiceOffered, float currentBonus)
        {
            int previousFloor = currentFloor;
            bool previousCardChoiceOffered = this.cardChoiceBonusOffered;
            float previousBonus = runStatBonusTotals[(int)stat];
            try
            {
                currentFloor = floor;
                this.cardChoiceBonusOffered = cardChoiceOffered;
                runStatBonusTotals[(int)stat] = currentBonus;
                return IsStatOfferAvailable(stat);
            }
            finally
            {
                currentFloor = previousFloor;
                this.cardChoiceBonusOffered = previousCardChoiceOffered;
                runStatBonusTotals[(int)stat] = previousBonus;
            }
        }

        internal int GetCardRewardOfferSlotCountSmokeSnapshot(float cardChoiceBonus)
        {
            float previous = runStatBonusTotals[(int)StatType.CardChoiceBonus];
            try
            {
                runStatBonusTotals[(int)StatType.CardChoiceBonus] = cardChoiceBonus;
                return GetCardRewardOfferSlotCount();
            }
            finally
            {
                runStatBonusTotals[(int)StatType.CardChoiceBonus] = previous;
            }
        }

        internal ShieldSettingsSmokeResult GetShieldSettingsSmokeSnapshot(float regenRateBonus, float regenDelayReduction)
        {
            float previousRate = runStatBonusTotals[(int)StatType.ShieldRegenRate];
            float previousDelay = runStatBonusTotals[(int)StatType.ShieldRegenDelayReduction];
            try
            {
                runStatBonusTotals[(int)StatType.ShieldRegenRate] = regenRateBonus;
                runStatBonusTotals[(int)StatType.ShieldRegenDelayReduction] = regenDelayReduction;
                return new ShieldSettingsSmokeResult
                {
                    RegenRate = GetEffectiveShieldRegenRate(),
                    RegenDelayDuration = GetEffectiveShieldRegenDelayDuration(),
                };
            }
            finally
            {
                runStatBonusTotals[(int)StatType.ShieldRegenRate] = previousRate;
                runStatBonusTotals[(int)StatType.ShieldRegenDelayReduction] = previousDelay;
            }
        }

        internal PlayerDamageSmokeResult ApplyIncomingDamageSmokeSnapshot(
            float startingHealth,
            float maxHealth,
            float startingShield,
            float damage,
            float damageReduction)
        {
            float previousHealth = player.Health;
            float previousMaxHealth = player.MaxHealth;
            bool previousIsDead = player.IsDead;
            float previousShield = player.Shield;
            float previousMaxShield = player.MaxShield;
            float previousShieldRegenRate = player.ShieldRegenRate;
            float previousShieldRegenDelay = player.ShieldRegenDelayDuration;
            float previousShieldRegenTimer = player.ShieldRegenDelayTimer;
            float previousDamageReduction = runStatBonusTotals[(int)StatType.DamageReduction];
            float previousDashTimer = player.DashTimer;
            try
            {
                player.MaxHealth = maxHealth;
                player.Health = startingHealth;
                player.IsDead = false;
                player.ConfigureShield(GameConfig.PlayerShieldMax, GameConfig.PlayerShieldBaseRegenRate, GameConfig.PlayerShieldBaseRegenDelay);
                player.RestoreShieldState(startingShield, 0f);
                player.DashTimer = 0f;
                runStatBonusTotals[(int)StatType.DamageReduction] = damageReduction;

                OnPlayerDamaged(damage, player.Position.X, player.Position.Y);
                return new PlayerDamageSmokeResult
                {
                    Health = player.Health,
                    Shield = player.Shield,
                    ShieldRegenDelayTimer = player.ShieldRegenDelayTimer,
                };
            }
            finally
            {
                player.MaxHealth = previousMaxHealth;
                player.Health = previousHealth;
                player.IsDead = previousIsDead;
                player.ConfigureShield(previousMaxShield, previousShieldRegenRate, previousShieldRegenDelay);
                player.RestoreShieldState(previousShield, previousShieldRegenTimer);
                player.DashTimer = previousDashTimer;
                runStatBonusTotals[(int)StatType.DamageReduction] = previousDamageReduction;
            }
        }

        internal KillBonusSmokeResult ApplyKillBonusesSmokeSnapshot(
            float startingHealth,
            float maxHealth,
            float dashCooldownTimer,
            float killHealBonus,
            float killDashRefundBonus)
        {
            float previousHealth = player.Health;
            float previousMaxHealth = player.MaxHealth;
            bool previousIsDead = player.IsDead;
            float previousDashCooldown = player.DashCooldownTimer;
            float previousKillHeal = runStatBonusTotals[(int)StatType.KillHeal];
            float previousKillDash = runStatBonusTotals[(int)StatType.KillDashCooldownRefund];
            try
            {
                player.MaxHealth = maxHealth;
                player.Health = startingHealth;
                player.IsDead = false;
                player.DashCooldownTimer = dashCooldownTimer;
                runStatBonusTotals[(int)StatType.KillHeal] = killHealBonus;
                runStatBonusTotals[(int)StatType.KillDashCooldownRefund] = killDashRefundBonus;

                ApplyOnEnemyKilledRunStatBonuses();
                return new KillBonusSmokeResult
                {
                    Health = player.Health,
                    DashCooldownTimer = player.DashCooldownTimer,
                };
            }
            finally
            {
                player.MaxHealth = previousMaxHealth;
                player.Health = previousHealth;
                player.IsDead = previousIsDead;
                player.DashCooldownTimer = previousDashCooldown;
                runStatBonusTotals[(int)StatType.KillHeal] = previousKillHeal;
                runStatBonusTotals[(int)StatType.KillDashCooldownRefund] = previousKillDash;
            }
        }

        internal float GetLifeStealRatioSmokeSnapshot(float lifeStealBonus, bool toxicMistPenaltyActive)
        {
            float previous = runStatBonusTotals[(int)StatType.LifeSteal];
            try
            {
                runStatBonusTotals[(int)StatType.LifeSteal] = lifeStealBonus;
                return GetEffectiveLifeStealRatio(toxicMistPenaltyActive);
            }
            finally
            {
                runStatBonusTotals[(int)StatType.LifeSteal] = previous;
            }
        }

        internal float ApplyLifeStealSmokeSnapshot(
            float startingHealth,
            float maxHealth,
            float lifeStealBonus,
            float dealtDamage,
            bool toxicMistPenaltyActive)
        {
            float previousHealth = player.Health;
            float previousMaxHealth = player.MaxHealth;
            bool previousIsDead = player.IsDead;
            float previousLifeSteal = runStatBonusTotals[(int)StatType.LifeSteal];
            try
            {
                player.MaxHealth = maxHealth;
                player.Health = startingHealth;
                player.IsDead = false;
                runStatBonusTotals[(int)StatType.LifeSteal] = lifeStealBonus;
                ApplyPlayerLifeSteal(dealtDamage, toxicMistPenaltyActive);
                return player.Health;
            }
            finally
            {
                player.MaxHealth = previousMaxHealth;
                player.Health = previousHealth;
                player.IsDead = previousIsDead;
                runStatBonusTotals[(int)StatType.LifeSteal] = previousLifeSteal;
            }
        }
    }
}
