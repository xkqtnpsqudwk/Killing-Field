namespace My2DEngine.Game.Core
{
    internal sealed class RunSaveData
    {
        public int Floor { get; set; }
        public float PlayerHealth { get; set; }

        /// <summary>
        /// 스탯 카드 누적 보너스(StatType 순서, 쉼표로 구분, 소수는 InvariantCulture).
        /// 새 스탯을 넣어도 DB 열을 늘리지 않는다.
        /// </summary>
        public string RunStatBonusState { get; set; }

        /// <summary>
        /// 예전 저장의 스탯별 열 값(StatType 0~24 순서). RunStatBonusState가 비어 있을 때만 쓴다.
        /// </summary>
        public float[] LegacyStatBonuses { get; set; }

        /// <summary>얻은 규칙 카드·악마의 계약 비트 묶음(RunPerk 값 = 비트 위치).</summary>
        public int RunPerkMask { get; set; }
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
