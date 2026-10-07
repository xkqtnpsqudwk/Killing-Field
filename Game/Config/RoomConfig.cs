namespace My2DEngine.Game.Config
{
    /// <summary>
    /// 방 목표(생존·열쇠)와 위험 요소(독성 안개) 값.
    /// 값을 바꾸면 게임 밸런스에 바로 영향을 주므로 의도적으로 수정할 때만 바꾼다.
    /// </summary>
    public static class RoomConfig
    {
        /// <summary>생존 방의 기본 생존 목표 시간(초).</summary>
        public const float SurvivalRoomBaseDuration = 18f;

        /// <summary>생존 방 목표 시간이 층마다 늘어나는 양(초).</summary>
        public const float SurvivalRoomDurationPerFloor = 0.08f;

        /// <summary>정예 생존 방에 추가되는 목표 시간(초).</summary>
        public const float SurvivalRoomEliteExtraDuration = 4f;

        /// <summary>생존 방 목표 시간 상한(초).</summary>
        public const float SurvivalRoomMaxDuration = 32f;

        /// <summary>생존 방 증원 생성 간격(초).</summary>
        public const float SurvivalRoomReinforcementInterval = 3.4f;

        /// <summary>생존 방에서 유지하려는 일반 활성 적 수.</summary>
        public const int SurvivalRoomTargetAliveEnemies = 4;

        /// <summary>정예 생존 방에서 추가로 유지하려는 활성 적 수.</summary>
        public const int SurvivalRoomEliteTargetAliveBonus = 1;

        /// <summary>생존 방 증원 1회당 최대 생성 수.</summary>
        public const int SurvivalRoomReinforcementCount = 2;

        /// <summary>열쇠 방 표적 적의 체력 배율. 은닉 목표라 일반 적보다 훨씬 오래 버티게 한다.</summary>
        public const float KeyTargetHealthMultiplier = 3.5f;

        /// <summary>열쇠 방 표적 적의 스케일 배율. 1.0을 유지해 외형만으로 표적을 알 수 없게 한다.</summary>
        public const float KeyTargetScaleMultiplier = 1.0f;

        /// <summary>열쇠 방 표적이 이 체력 비율 이하로 내려가면 HUD에서 정체가 드러난다.</summary>
        public const float KeyTargetRevealHealthRatio = 0.80f;

        /// <summary>독성 안개 위험 방의 피해 간격(초).</summary>
        public const float ToxicMistDamageInterval = 1.15f;

        /// <summary>독성 안개가 한 번에 주는 피해량.</summary>
        public const float ToxicMistDamage = 3f;
    }
}
