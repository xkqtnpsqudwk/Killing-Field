namespace My2DEngine.Game.Config
{
    /// <summary>
    /// 규칙 카드와 악마의 계약 수치. 카드 설명 문구(RunPerkCatalog)도 이 값으로 만든다.
    /// </summary>
    public static class PerkConfig
    {
        // ── 등장 ──────────────────────────────────────────────

        /// <summary>규칙 카드가 등장하는 최소 등급(Purple).</summary>
        public const int RuleMinGrade = 3;

        /// <summary>Purple 이상 스탯 카드 한 칸이 규칙 카드로 바뀔 확률.</summary>
        public const float RuleOfferChance = 0.40f;

        /// <summary>악마의 계약이 등장하기 시작하는 층.</summary>
        public const int ContractMinFloor = 8;

        /// <summary>일반 카드 보상 한 번에 계약 카드가 한 칸 끼어들 확률.</summary>
        public const float ContractOfferChance = 0.18f;

        // ── 규칙 카드 ─────────────────────────────────────────

        /// <summary>관통탄: 뒤에 있는 두 번째 적이 받는 피해 비율.</summary>
        public const float PierceDamageRatio = 0.60f;

        /// <summary>시체 폭발: 쓰러진 적 최대 체력 대비 폭발 피해.</summary>
        public const float CorpseBlastHealthRatio = 0.25f;

        /// <summary>시체 폭발 반지름(타일).</summary>
        public const float CorpseBlastRadius = 1.8f;

        /// <summary>이중 대시: 더해지는 대시 충전 수.</summary>
        public const int ExtraDashCharges = 1;

        /// <summary>대시 충격파: 현재 무기 피해 대비 배율.</summary>
        public const float DashShockDamageMult = 1.5f;

        /// <summary>대시 충격파 반지름(타일).</summary>
        public const float DashShockRadius = 1.8f;

        /// <summary>대시 충격파 기절 시간(초).</summary>
        public const float DashShockStun = 0.8f;

        /// <summary>보호막 파열: 최대 보호막 대비 폭발 피해.</summary>
        public const float ShieldBurstShieldRatio = 1.0f;

        /// <summary>보호막 파열 반지름(타일).</summary>
        public const float ShieldBurstRadius = 2.6f;

        /// <summary>보호막 파열 기절 시간(초).</summary>
        public const float ShieldBurstStun = 1.2f;

        // ── 악마의 계약 ───────────────────────────────────────

        /// <summary>피의 계약: 주는 피해 배율.</summary>
        public const float BloodPactDamageMult = 1.8f;

        /// <summary>피의 계약: 최대 체력 배율.</summary>
        public const float BloodPactMaxHealthMult = 0.5f;

        /// <summary>유리 대포: 더해지는 치명타 확률.</summary>
        public const float GlassCannonCritBonus = 0.25f;

        /// <summary>유리 대포: 치명타 피해 배율(기본 2배).</summary>
        public const float GlassCannonCritMult = 3.5f;

        /// <summary>기본 치명타 피해 배율.</summary>
        public const float BaseCritMult = 2f;

        /// <summary>광기의 계약: 더해지는 발사 속도(쿨타임 감소).</summary>
        public const float FrenzyFireRateBonus = 0.35f;

        /// <summary>광기의 계약: 이동 속도 배율.</summary>
        public const float FrenzyMoveSpeedMult = 1.25f;

        /// <summary>광기의 계약: 받는 피해 배율.</summary>
        public const float FrenzyIncomingMult = 1.6f;

        /// <summary>학살의 계약: 처치 한 번마다 쌓이는 피해 증가.</summary>
        public const float SlaughterDamagePerStack = 0.06f;

        /// <summary>학살의 계약: 최대 누적 수.</summary>
        public const int SlaughterMaxStacks = 20;

        /// <summary>학살의 계약: 이 시간(초) 안에 다시 처치하지 못하면 누적이 사라지고 체력이 깎이기 시작한다.</summary>
        public const float SlaughterWindow = 3f;

        /// <summary>학살의 계약: 굶주림 상태에서 초당 잃는 최대 체력 비율. 체력 1 아래로는 깎지 않는다.</summary>
        public const float SlaughterDrainPerSecond = 0.04f;

        /// <summary>탐욕의 계약: 일반 적 코인 드랍 확률 배율과 코인 개수 배율.</summary>
        public const float GreedCoinChanceMult = 2f;
        public const int GreedCoinAmountMult = 2;

        /// <summary>탐욕의 계약: 상점 가격 배율.</summary>
        public const float GreedShopPriceMult = 0.6f;

        /// <summary>탐욕의 계약: 주는 피해 배율.</summary>
        public const float GreedDamageMult = 0.7f;
    }
}
