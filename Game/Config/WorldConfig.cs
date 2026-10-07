namespace My2DEngine.Game.Config
{
    /// <summary>
    /// 맵 타일·문·단차·충돌 스텝·픽업 거리.
    /// 값을 바꾸면 게임 밸런스에 바로 영향을 주므로 의도적으로 수정할 때만 바꾼다.
    /// </summary>
    public static class WorldConfig
    {
        /// <summary>문(door) 타일 타입 번호. 맵 배열에서 이 값이면 문으로 처리된다.</summary>
        public const int DoorTileType = 9;

        /// <summary>문 텍스처 ID. TextureIds 배열에서 이 값을 가진 타일에 문 텍스처를 그린다.</summary>
        public const int DoorTextureId = 5;

        /// <summary>문 한 짝이 완전히 열리는 데 걸리는 시간(초).</summary>
        public const float DoorOpenDuration = 0.85f;

        /// <summary>플레이어가 문과 상호작용할 수 있는 최대 거리(타일).</summary>
        public const float DoorInteractDistance = 1.5f;

        /// <summary>전리품 아이템 수거 판정 거리(타일). 이 거리 이내로 접근하면 자동 획득된다.</summary>
        public const float PickupRadius = 0.7f;

        /// <summary>
        /// 충돌 검사 1회 최대 이동 거리(타일).
        /// 큰 DeltaTime에서 벽 통과를 방지하기 위해 이동을 여러 단계로 분할한다.
        /// </summary>
        public const float CollisionStepSize = 0.025f;

        /// <summary>플레이어가 자동으로 올라설 수 있는 최대 바닥 단차(타일). Doom의 24/64 ≈ 0.375와 동일.</summary>
        public const float MaxStepHeight = 0.375f;
    }
}
