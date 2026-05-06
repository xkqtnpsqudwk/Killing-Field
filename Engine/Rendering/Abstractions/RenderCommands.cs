using System.Drawing;

namespace My2DEngine.Engine.Rendering.Abstractions
{
    /// <summary>
    /// 색상 채워진 사각형 하나를 그리는 데 필요한 파라미터 묶음.
    /// </summary>
    public struct RenderRectCommand
    {
        /// <summary>사각형 왼쪽 위 X 좌표(픽셀).</summary>
        public float X;
        /// <summary>사각형 왼쪽 위 Y 좌표(픽셀).</summary>
        public float Y;
        /// <summary>사각형 너비(픽셀).</summary>
        public float Width;
        /// <summary>사각형 높이(픽셀).</summary>
        public float Height;
        /// <summary>채울 색상. 알파가 포함된 경우 블렌딩이 적용된다.</summary>
        public Color Color;
    }

    /// <summary>
    /// 이미지(비트맵) 하나를 그리는 데 필요한 파라미터 묶음.
    /// </summary>
    public struct RenderImageCommand
    {
        /// <summary>그릴 GDI+ Image 객체.</summary>
        public Image Image;
        /// <summary>화면상 목적지 왼쪽 위 X 좌표(픽셀).</summary>
        public float X;
        /// <summary>화면상 목적지 왼쪽 위 Y 좌표(픽셀).</summary>
        public float Y;
        /// <summary>그릴 목적지 너비(픽셀). 원본과 다르면 스케일링된다.</summary>
        public float Width;
        /// <summary>그릴 목적지 높이(픽셀). 원본과 다르면 스케일링된다.</summary>
        public float Height;
    }

    /// <summary>
    /// 텍스트 한 줄을 그리는 데 필요한 파라미터 묶음.
    /// </summary>
    public struct RenderTextCommand
    {
        /// <summary>출력할 문자열.</summary>
        public string Text;
        /// <summary>텍스트 왼쪽 위 X 좌표(픽셀).</summary>
        public float X;
        /// <summary>텍스트 왼쪽 위 Y 좌표(픽셀).</summary>
        public float Y;
        /// <summary>텍스트 색상.</summary>
        public Color Color;
        /// <summary>폰트 크기(픽셀 단위).</summary>
        public float Size;
    }

    /// <summary>
    /// 월드에 표시되는 스프라이트의 종류를 구분한다.
    /// GPU 스프라이트 패스에서 적·투사체·아이템을 서로 다른 방식으로 처리할 때 사용한다.
    /// </summary>
    public enum WorldSpriteKind
    {
        /// <summary>적 캐릭터 스프라이트.</summary>
        Enemy,
        /// <summary>적 투사체(총알, 로켓 등) 스프라이트.</summary>
        Projectile,
        /// <summary>바닥에 떨어진 보상 아이템 스프라이트.</summary>
        Pickup
    }

    /// <summary>
    /// GPU 월드 렌더링에 필요한 플레이어 카메라 정보.
    /// 레이캐스팅 계산에서 카메라 위치·방향·시야면(plane)을 셰이더로 전달할 때 사용한다.
    /// </summary>
    public struct WorldCameraData
    {
        /// <summary>카메라(플레이어) 월드 X 좌표.</summary>
        public float PositionX;
        /// <summary>카메라(플레이어) 월드 Y 좌표.</summary>
        public float PositionY;
        /// <summary>카메라가 바라보는 방향 벡터 X 성분(정규화).</summary>
        public float DirectionX;
        /// <summary>카메라가 바라보는 방향 벡터 Y 성분(정규화).</summary>
        public float DirectionY;
        /// <summary>시야면(camera plane) X 성분. FOV 폭을 결정한다.</summary>
        public float PlaneX;
        /// <summary>시야면(camera plane) Y 성분. FOV 폭을 결정한다.</summary>
        public float PlaneY;
    }

    /// <summary>
    /// GPU 스프라이트 인스턴스 하나의 렌더 파라미터.
    /// 인스턴드 드로우를 위해 배열로 묶여 한 번에 GPU에 업로드된다.
    /// </summary>
    public struct WorldSpriteInstance
    {
        /// <summary>스프라이트 종류(적·투사체·픽업).</summary>
        public WorldSpriteKind Kind;
        /// <summary>스프라이트 월드 X 좌표.</summary>
        public float PositionX;
        /// <summary>스프라이트 월드 Y 좌표.</summary>
        public float PositionY;
        /// <summary>스프라이트 전체 크기 배율.</summary>
        public float Scale;
        /// <summary>스프라이트 가로 크기 배율(Scale에 곱해진다).</summary>
        public float WidthScale;
        /// <summary>스프라이트를 수직으로 얼마나 올릴지 결정하는 오프셋 비율.</summary>
        public float VerticalOffsetFactor;
        /// <summary>깊이 테스트에서 벽보다 앞/뒤에 그려지게 미세 조정하는 깊이 편향.</summary>
        public float DepthBias;
        /// <summary>스프라이트 회전 각도(도 단위).</summary>
        public float RotationDegrees;
        /// <summary>렌더 모드 플래그(0=일반, 1=히트 플래시 등).</summary>
        public float RenderMode;
        /// <summary>스프라이트 투영 컬럼 범위 내 최소 벽 깊이 (CPU 산출). 벽에 가려지는지 판단한다.</summary>
        public float MinWallDepth;
        /// <summary>텍스처 아틀라스 인덱스. 어느 스프라이트 시트를 사용할지 결정한다.</summary>
        public int AtlasIndex;
        /// <summary>UV 좌표 좌측 U (0~1 정규화).</summary>
        public float U0;
        /// <summary>UV 좌표 상단 V (0~1 정규화).</summary>
        public float V0;
        /// <summary>UV 좌표 우측 U (0~1 정규화).</summary>
        public float U1;
        /// <summary>UV 좌표 하단 V (0~1 정규화).</summary>
        public float V1;
        /// <summary>스프라이트에 곱할 색상(ARGB 정수 형태).</summary>
        public int TintArgb;
        /// <summary>거리에 따른 안개 블렌딩 비율(0=안개 최대, 1=안개 없음).</summary>
        public float FogFactor;
        /// <summary>true이면 스프라이트 위에 체력 바를 렌더링한다.</summary>
        public bool ShowHealthBar;
        /// <summary>true이면 보스 특수기 예고 시각 효과를 렌더링한다.</summary>
        public bool ShowTelegraph;
    }

    /// <summary>
    /// GPU 레이저 빔 인스턴스 하나의 렌더 파라미터.
    /// 보스·특수 공격 빔을 월드 공간에서 쿼드로 그리는 데 사용한다.
    /// </summary>
    public struct WorldBeamInstance
    {
        /// <summary>빔 시작 월드 X 좌표.</summary>
        public float PositionX;
        /// <summary>빔 시작 월드 Y 좌표.</summary>
        public float PositionY;
        /// <summary>빔 전체 크기 배율.</summary>
        public float Scale;
        /// <summary>빔 가로 크기 배율(Scale에 곱해진다).</summary>
        public float WidthScale;
        /// <summary>원본 소스 텍스처 너비(UV 계산에 사용).</summary>
        public float SourceWidth;
        /// <summary>원본 소스 텍스처 높이(UV 계산에 사용).</summary>
        public float SourceHeight;
        /// <summary>빔의 원근 깊이값(카메라 거리).</summary>
        public float Depth;
        /// <summary>깊이 테스트 편향. 벽과의 z-fighting을 방지한다.</summary>
        public float DepthBias;
        /// <summary>빔 회전 각도(도 단위).</summary>
        public float RotationDegrees;
        /// <summary>빔에 곱할 색상(ARGB 정수 형태).</summary>
        public int TintArgb;
    }

    /// <summary>
    /// 한 프레임에서 스프라이트 패스(적 또는 기타 스프라이트) 하나를 GPU에 제출하는 데이터 묶음.
    /// 적 패스와 기타(투사체·픽업) 패스를 분리해 각각 독립적으로 관리한다.
    /// </summary>
    public struct RenderWorldSpritePassCommand
    {
        /// <summary>렌더 타깃 너비(픽셀).</summary>
        public int TargetWidth;
        /// <summary>렌더 타깃 높이(픽셀).</summary>
        public int TargetHeight;
        /// <summary>DepthBuffer 배열 유효 길이.</summary>
        public int DepthBufferLength;
        /// <summary>CPU가 레이캐스팅으로 계산한 컬럼별 벽 깊이 배열. 스프라이트 가림 판정에 사용.</summary>
        public float[] DepthBuffer;
        /// <summary>거리 안개 밀도 계수.</summary>
        public float FogDensity;
        /// <summary>스프라이트 텍스처 아틀라스 픽셀 데이터(ARGB 정수 배열).</summary>
        public int[] SpriteAtlasPixels;
        /// <summary>스프라이트 아틀라스 너비(픽셀).</summary>
        public int SpriteAtlasWidth;
        /// <summary>스프라이트 아틀라스 높이(픽셀).</summary>
        public int SpriteAtlasHeight;
        /// <summary>true이면 아틀라스 전체를 GPU에 다시 업로드한다. false이면 더티 영역만 부분 업로드.</summary>
        public bool UploadFullSpriteAtlas;
        /// <summary>부분 업로드할 더티 사각형 목록(x, y, width, height 4개씩 연속 저장).</summary>
        public int[] DirtySpriteAtlasRects;
        /// <summary>DirtySpriteAtlasRects에 저장된 사각형 수.</summary>
        public int DirtySpriteAtlasRectCount;
        /// <summary>이번 패스에서 그릴 스프라이트 인스턴스 배열.</summary>
        public WorldSpriteInstance[] Sprites;
        /// <summary>Sprites 배열에서 실제로 유효한 인스턴스 수.</summary>
        public int SpriteCount;
    }

    /// <summary>
    /// 레이캐스트 월드 렌더링 전체(벽·바닥·천장·스프라이트·빔)를 한 번에 GPU에 제출하는 명령 묶음.
    /// CPU에서 계산한 컬럼 지오메트리와 카메라 정보를 포함해 셰이더가 픽셀 단위로 그릴 수 있게 한다.
    /// </summary>
    public struct RenderWorldCommand
    {
        /// <summary>레이캐스팅 계산에 사용하는 카메라 위치·방향·시야면 데이터.</summary>
        public WorldCameraData Camera;
        /// <summary>플레이어 눈 높이 (playerFloorZ + 0.5). 계단 평면 렌더링에 사용.</summary>
        public float PlayerEyeZ;
        /// <summary>렌더 타깃 너비(픽셀). GpuWorldMaxRenderWidth 이하로 제한될 수 있다.</summary>
        public int TargetWidth;
        /// <summary>렌더 타깃 높이(픽셀).</summary>
        public int TargetHeight;
        /// <summary>렌더 결과를 화면에 합성할 때 적용할 X 오프셋(픽셀).</summary>
        public float ScreenOffsetX;
        /// <summary>렌더 결과를 화면에 합성할 때 적용할 Y 오프셋(픽셀).</summary>
        public float ScreenOffsetY;
        /// <summary>합성 단계에서 이미지를 회전할 각도(도 단위). 카메라 흔들림 효과에 사용.</summary>
        public float CompositeRotationDegrees;
        /// <summary>합성 단계에서 적용할 확대/축소 배율.</summary>
        public float CompositeScale;
        /// <summary>합성 단계에서 이미지를 수직으로 이동할 픽셀 오프셋.</summary>
        public float CompositeOffsetY;
        /// <summary>화면 가로 레이 개수. 보통 TargetWidth와 같다.</summary>
        public int ColumnCount;
        /// <summary>
        /// 컬럼별 벽 지오메트리 배열.
        /// 각 컬럼마다 (벽 거리, 벽 시작 Y, 벽 끝 Y) 등의 데이터가 연속 저장된다.
        /// </summary>
        public float[] WallColumnGeometry;
        /// <summary>컬럼별 벽 재질(텍스처 인덱스·면 구분) 정보 배열.</summary>
        public int[] WallColumnMaterial;
        /// <summary>컬럼별 문 열림 진행도(0=닫힘, 1=완전 열림) 배열.</summary>
        public float[] WallColumnDoorProgress;
        /// <summary>DepthBuffer 배열 유효 길이(= ColumnCount).</summary>
        public int DepthBufferLength;
        /// <summary>컬럼별 최소 벽 깊이 배열. 스프라이트 가림 판정에 사용된다.</summary>
        public float[] DepthBuffer;
        /// <summary>벽 텍스처 한 장의 크기(픽셀). 정사각형으로 가정한다.</summary>
        public int TextureSize;
        /// <summary>벽 텍스처 아틀라스에 포함된 텍스처 수.</summary>
        public int WallTextureCount;
        /// <summary>벽 텍스처 아틀라스 픽셀 데이터(ARGB 정수 배열).</summary>
        public int[] WallTextureAtlasPixels;
        /// <summary>벽 텍스처 아틀라스 너비(픽셀).</summary>
        public int WallTextureAtlasWidth;
        /// <summary>벽 텍스처 아틀라스 높이(픽셀).</summary>
        public int WallTextureAtlasHeight;
        /// <summary>문이 열리는 애니메이션에 쓸 문 열림 텍스처 픽셀 데이터.</summary>
        public int[] DoorOpenTexturePixels;
        /// <summary>문 열림 텍스처 너비(픽셀).</summary>
        public int DoorOpenTextureWidth;
        /// <summary>문 열림 텍스처 높이(픽셀).</summary>
        public int DoorOpenTextureHeight;
        /// <summary>적 스프라이트 렌더 패스 데이터(적 캐릭터 전용).</summary>
        public RenderWorldSpritePassCommand EnemySpritePass;
        /// <summary>기타 스프라이트 렌더 패스 데이터(투사체·픽업 등).</summary>
        public RenderWorldSpritePassCommand MiscSpritePass;
        /// <summary>문 타일 타입 번호(= GameConfig.DoorTileType). 셰이더가 문과 일반 벽을 구분한다.</summary>
        public int DoorTileType;
        /// <summary>바닥 텍스처로 사용할 아틀라스 인덱스.</summary>
        public int FloorTextureIndex;
        /// <summary>true이면 바닥에 텍스처를 입힌다. false이면 단색(FloorColor)으로 채운다.</summary>
        public bool UseTexturedFloor;
        /// <summary>천장 텍스처로 사용할 아틀라스 인덱스.</summary>
        public int CeilingTextureIndex;
        /// <summary>true이면 천장에 텍스처를 입힌다. false이면 단색(CeilingColor)으로 채운다.</summary>
        public bool UseTexturedCeiling;
        /// <summary>천장 색상과 텍스처 간의 블렌딩 비율(0=텍스처 전용, 1=단색 전용).</summary>
        public float CeilingBlend;
        /// <summary>근거리 클리핑 평면 거리. 카메라에 너무 가까운 벽을 부드럽게 처리한다.</summary>
        public float NearPlane;
        /// <summary>근거리 클리핑 평면 부드러움 계수. 값이 클수록 전환이 매끄럽다.</summary>
        public float NearPlaneSoftness;
        /// <summary>바닥 색상과 텍스처 간의 블렌딩 비율(0=단색, 1=텍스처 전용).</summary>
        public float FloorBlend;
        /// <summary>거리 안개 밀도. 클수록 멀리 있는 물체가 빠르게 안개에 가려진다.</summary>
        public float FogDensity;
        /// <summary>거리 안개가 섞일 색상. FogTintStrength가 0이면 사용되지 않는다.</summary>
        public Color FogColor;
        /// <summary>거리 안개 색상 블렌딩 강도. 0이면 기존 검은 거리 감쇠만 사용한다.</summary>
        public float FogTintStrength;
        /// <summary>바닥 단색. UseTexturedFloor=false 또는 FloorBlend&lt;1일 때 사용된다.</summary>
        public Color FloorColor;
        /// <summary>천장 단색.</summary>
        public Color CeilingColor;
        /// <summary>이번 프레임에 그릴 레이저 빔 인스턴스 배열.</summary>
        public WorldBeamInstance[] Beams;
        /// <summary>Beams 배열에서 실제로 유효한 빔 수.</summary>
        public int BeamCount;
    }
}
