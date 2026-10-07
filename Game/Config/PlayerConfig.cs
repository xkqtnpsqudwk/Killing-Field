namespace My2DEngine.Game.Config
{
    /// <summary>
    /// 플레이어 이동·스태미나·대시·체력·보호막·시야와 영구 스탯 포인트당 효과.
    /// 값을 바꾸면 게임 밸런스에 바로 영향을 주므로 의도적으로 수정할 때만 바꾼다.
    /// </summary>
    public static class PlayerConfig
    {
        /// <summary>이동 속도 영구 스탯이 이 포인트 이상이면 대시 쿨다운 추가 감소가 활성화된다.</summary>
        public const int MoveSpeedDashSynergyThreshold = 3;

        /// <summary>이동 속도 시너지 활성화 시 추가 대시 쿨다운 감소 비율.</summary>
        public const float MoveSpeedDashSynergyBonus = 0.08f;

        /// <summary>플레이어 기본 이동 속도(타일/초).</summary>
        public const float MoveSpeed = 2.5f;

        /// <summary>달리기(스프린트) 시 이동 속도에 곱하는 배율.</summary>
        public const float SprintMultiplier = 1.5f;

        /// <summary>스태미나가 완전히 고갈됐을 때 이동 속도에 곱하는 배율(반속).</summary>
        public const float ExhaustedSpeedMultiplier = 0.5f;

        /// <summary>마우스 수평 이동 1픽셀당 회전 각도(라디안).</summary>
        public const float MouseSensitivity = 0.003f;

        /// <summary>플레이어 충돌 원 반지름(타일). 작을수록 좁은 공간을 통과하기 쉽다.</summary>
        public const float PlayerRadius = 0.1f;

        /// <summary>스태미나 최대값.</summary>
        public const float StaminaMax = 100f;

        /// <summary>달리는 동안 초당 소모되는 스태미나량.</summary>
        public const float StaminaDrainPerSec = 15f;

        /// <summary>스태미나가 회복될 때 초당 회복량.</summary>
        public const float StaminaRecoverPerSec = 15f;

        /// <summary>스태미나 소모를 멈춘 뒤 회복이 시작되기까지의 대기 시간(초).</summary>
        public const float StaminaRecoverDelay = 1.35f;

        /// <summary>대시 1회당 소모되는 스태미나량.</summary>
        public const float DashStaminaCost = 20f;

        /// <summary>대시 후 다시 대시할 수 있게 되기까지의 쿨다운 시간(초).</summary>
        public const float DashCooldownDuration = 5.0f;

        /// <summary>카드 보너스 적용 후 대시 쿨다운이 더 이상 줄어들지 않는 최소 시간(초).</summary>
        public const float DashCooldownMinDuration = 0.5f;

        /// <summary>대시로 이동하는 총 거리(타일). DashDuration 동안 이 거리를 이동한다.</summary>
        public const float DashDistance = 2.0f;

        /// <summary>대시가 지속되는 시간(초). 짧을수록 순간적인 돌진 느낌이 강해진다.</summary>
        public const float DashDuration = 0.20f;

        /// <summary>플레이어 최대 체력.</summary>
        public const float PlayerHealthMax = 100f;

        /// <summary>플레이어 기본 보호막 최대값.</summary>
        public const float PlayerShieldMax = 100f;

        /// <summary>피해를 입은 뒤 보호막 회복이 시작되기까지의 기본 대기 시간(초).</summary>
        public const float PlayerShieldBaseRegenDelay = 10f;

        /// <summary>보호막 회복 지연 시간이 더 이상 줄어들지 않는 최소 시간(초).</summary>
        public const float PlayerShieldMinRegenDelay = 2f;

        /// <summary>보호막 기본 초당 회복량.</summary>
        public const float PlayerShieldBaseRegenRate = 1f;

        /// <summary>카드 보너스 적용 후 보호막 초당 회복량 상한.</summary>
        public const float PlayerShieldMaxRegenRate = 20f;

        /// <summary>영구 스탯 체력 1포인트당 최대 체력 증가율.</summary>
        public const float PermanentHealthPerPoint = 0.10f;

        /// <summary>영구 스탯 속도 1포인트당 이동 속도 증가율.</summary>
        public const float PermanentMoveSpeedPerPoint = 0.05f;

        /// <summary>영구 스탯 Pistol 데미지 1포인트당 피해 증가율.</summary>
        public const float PermanentPistolDamagePerPoint = 0.10f;

        /// <summary>영구 스탯 감각 1포인트당 증가량.</summary>
        public const float PermanentSensePerPoint = 0.10f;

        /// <summary>영구 스탯 감각 최대값 (5레벨 달성 누적값).</summary>
        public const float PermanentSenseMax = 7.5f;

        /// <summary>영구 스탯 행운 1포인트당 증가량.</summary>
        public const float PermanentLuckPerPoint = 0.10f;

        /// <summary>영구 스탯 행운 최대값 (10레벨 달성 누적값).</summary>
        public const float PermanentLuckMax = 27.5f;

        /// <summary>기본 시야각(도 단위).</summary>
        public const float DefaultFovDegrees = 80f;

        /// <summary>설정에서 선택 가능한 최소 시야각(도 단위).</summary>
        public const float MinFovDegrees = 60f;

        /// <summary>설정에서 선택 가능한 최대 시야각(도 단위).</summary>
        public const float MaxFovDegrees = 90f;
    }
}
