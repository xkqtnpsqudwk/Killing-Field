namespace My2DEngine.Game.Core
{
    internal sealed class RunSaveData
    {
        public int Floor { get; set; }
        public float PlayerHealth { get; set; }
        public float BonusMaxHealth { get; set; }
        public float BonusMoveSpeed { get; set; }
        public float BonusDamage { get; set; }
        public float BonusAmmoDropChance { get; set; }
        public float BonusDashCooldown { get; set; }
        public float BonusCoinDropChance { get; set; }
        public float BonusLifeSteal { get; set; }
        public float BonusDamageReduction { get; set; }
        public float BonusShopDiscount { get; set; }
        public float BonusKillHeal { get; set; }
        public float BonusKillDashCooldownRefund { get; set; }
        public float BonusCriticalChance { get; set; }
        public float BonusCardChoiceBonus { get; set; }
        public float BonusShieldRegenRate { get; set; }
        public float BonusShieldRegenDelayReduction { get; set; }
        public float PlayerShield { get; set; }
        public float ShieldRegenDelayTimer { get; set; }
        public bool CardChoiceBonusOffered { get; set; }
        public int ClearedCombatFloorCount { get; set; }
        public int BossClearGrowthCount { get; set; }
        public int OwnedWeaponsMask { get; set; }
        public int CoinCount { get; set; }
        public int WeaponCardPoolCount { get; set; }
        public bool RestRoomOpportunityCooldownActive { get; set; }
        public int CurrentWeaponType { get; set; }
        public string WeaponAmmoState { get; set; }
        public string WeaponUpgradeState { get; set; }
        public string RunStatGradeState { get; set; }
        public string RunStatPickupState { get; set; }
    }
}
