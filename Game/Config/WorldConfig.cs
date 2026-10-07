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

        // ── 벽 종류 ──
        // 텍스처 슬롯 0은 천장, 3은 바닥, 5는 문이라 벽은 나머지 슬롯을 쓴다.
        // 어떤 벽에 어떤 종류를 쓸지는 MapManager.WallThemes.cs의 규칙이 정한다.

        /// <summary>기지 벽(녹슨 금속 판). 일반 전투방의 바탕 벽.</summary>
        public const int WallTextureBase = 1;

        /// <summary>골조 벽(강철 I빔과 보강재). 방이 맞닿은 벽, 문 양옆 기둥, 방 안 기둥.</summary>
        public const int WallTextureSupport = 2;

        /// <summary>지옥 벽(붉은 바위와 용암 틈). 보스방의 바탕 벽.</summary>
        public const int WallTextureHell = 4;

        /// <summary>연구소 벽(밝은 타일). 시작실과 카드 상점의 바탕 벽.</summary>
        public const int WallTextureLab = 6;

        /// <summary>엄폐물(쌓은 상자). 방 안 엄폐 벽.</summary>
        public const int WallTextureCover = 7;

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
