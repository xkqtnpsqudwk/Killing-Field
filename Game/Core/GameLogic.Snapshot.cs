using My2DEngine.Engine.Math;

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
            return new GameLogicSmokeSnapshot
            {
                Map = mapManager.Map,
                CollisionInitialized = collision != null,
                WallTextureCount = textureManager.WallTextures == null ? 0 : textureManager.WallTextures.Length,
                HasStageFlow = mapManager.HasStageFlow,
                StageRoomCount = mapManager.StageRooms == null ? 0 : mapManager.StageRooms.Length,
                PlayerPosition = player.Position,
                PlayerDirection = player.Direction,
                PlayerFovDegrees = player.FovDegrees
            };
        }
    }
}
