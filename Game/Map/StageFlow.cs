using System;
using System.Drawing;
using My2DEngine.Game.Config;

namespace My2DEngine.Game.Map
{
    /// <summary>
    /// 방 내부의 장식 및 구조물(기둥, 벽 등) 배치 방식을 나타내는 레이아웃 변형 열거형이다.
    /// 스테이지 생성 시 각 방의 시각적 구조를 결정하는 데 사용된다.
    /// </summary>
    public enum RoomLayoutVariant
    {
        /// <summary>장애물이 없는 완전히 열린 방 레이아웃이다.</summary>
        Open = 0,
        /// <summary>방 중앙에 기둥 하나가 배치된 레이아웃이다.</summary>
        CenterPillar = 1,
        /// <summary>방 안에 두 개의 기둥이 나란히 배치된 레이아웃이다.</summary>
        TwinPillars = 2,
        /// <summary>방의 네 모서리에 기둥이 배치된 레이아웃이다.</summary>
        CornerPillars = 3,
        /// <summary>방을 두 개의 이동 경로로 나누는 레이아웃이다.</summary>
        SplitLanes = 4,
        /// <summary>방 내부 대각선 4방향에 기둥이 배치된 레이아웃이다.</summary>
        DiagonalPillars = 5,
        /// <summary>방 내부 중앙 링 위치에 4개의 기둥이 배치된 레이아웃이다.</summary>
        InnerRing = 6,
        /// <summary>방 중앙 상하로 짧은 가로 장벽 2개가 배치된 레이아웃이다.</summary>
        CenterWall = 7,
    }

    /// <summary>
    /// 전투 방의 클리어 목표 종류다.
    /// </summary>
    public enum RoomObjectiveKind
    {
        /// <summary>모든 적을 제거하면 클리어된다.</summary>
        EliminateAll = 0,

        /// <summary>제한 시간 동안 생존하면 클리어된다.</summary>
        Survive = 1,

        /// <summary>지정된 표적 적을 제거하면 클리어된다.</summary>
        KeyTarget = 2,
    }

    /// <summary>
    /// 전투 방에 추가로 붙는 위험 modifier 종류다.
    /// </summary>
    public enum RoomHazardKind
    {
        /// <summary>추가 위험 없음.</summary>
        None = 0,

        /// <summary>독성 안개가 주기적으로 플레이어에게 피해를 준다.</summary>
        ToxicMist = 1,

        /// <summary>방 안에서 적 처치 드롭이 없지만 클리어 보상 품질이 오른다.</summary>
        SupplyShortage = 2,
    }

    /// <summary>
    /// 한 방 안에서 어떤 종류의 적이 어느 위치에, 어떤 속성으로 생성될지를 정의하는 스폰 포인트 데이터 클래스다.
    /// 스테이지 설계 단계에서 각 방의 적 배치를 지정하기 위해 사용된다.
    /// </summary>
    public class StageSpawnPoint
    {
        /// <summary>
        /// 생성할 적 에셋 ID다.
        /// 비어 있으면 <see cref="Type"/> 기반 기본 아키타입으로 폴백한다.
        /// </summary>
        public string EnemyAssetId { get; set; }

        /// <summary>
        /// 생성할 적의 전투 분류다.
        /// 에셋 기반 스폰에서는 폴백/호환용으로만 사용된다.
        /// </summary>
        public EnemyType Type { get; set; }

        /// <summary>
        /// 생성할 적의 등급(일반, 미니보스, 보스 등)을 나타낸다.
        /// 기본값은 <see cref="EnemyRank.Normal"/>이다.
        /// </summary>
        public EnemyRank Rank { get; set; } = EnemyRank.Normal;

        /// <summary>
        /// 생성할 적의 행동 패턴을 나타낸다.
        /// 기본값은 <see cref="EnemyBehaviorPattern.Default"/>이다.
        /// </summary>
        public EnemyBehaviorPattern BehaviorPattern { get; set; } = EnemyBehaviorPattern.Default;

        /// <summary>
        /// 스폰 시 랜덤하게 고를 행동 패턴 풀이다.
        /// null이 아니고 비어 있지 않으면 <see cref="BehaviorPattern"/> 대신 이 목록에서 무작위로 하나를 선택한다.
        /// 같은 방 템플릿이라도 매번 다른 전술로 적이 행동하게 된다.
        /// </summary>
        public EnemyBehaviorPattern[] BehaviorPatternPool { get; set; }

        /// <summary>적이 생성될 월드 공간의 X 좌표다.</summary>
        public float X { get; set; }

        /// <summary>적이 생성될 월드 공간의 Y 좌표다.</summary>
        public float Y { get; set; }

        /// <summary>
        /// 기본 체력에 곱해지는 배율이다. 1.0이면 기본값 그대로이고,
        /// 값이 클수록 더 많은 체력을 가진 적이 생성된다.
        /// </summary>
        public float HealthMultiplier { get; set; } = 1f;

        /// <summary>
        /// 기본 공격 데미지에 곱해지는 배율이다. 1.0이면 기본값 그대로이고,
        /// 값이 클수록 더 높은 데미지를 가진 적이 생성된다.
        /// </summary>
        public float DamageMultiplier { get; set; } = 1f;

        /// <summary>
        /// 기본 이동 속도에 곱해지는 배율이다. 1.0이면 기본값 그대로이고,
        /// 값이 클수록 더 빠르게 이동하는 적이 생성된다.
        /// </summary>
        public float MoveSpeedMultiplier { get; set; } = 1f;

        /// <summary>
        /// 기본 공격 사거리에 곱해지는 배율이다. 1.0이면 기본값 그대로이고,
        /// 값이 클수록 더 넓은 공격 범위를 가진 적이 생성된다.
        /// </summary>
        public float AttackRangeMultiplier { get; set; } = 1f;

        /// <summary>
        /// 적의 스케일(크기)에 곱해지는 배율이다. 1.0이면 기본 크기이고,
        /// 크기가 커지면 충돌 반지름도 함께 조정된다.
        /// </summary>
        public float ScaleMultiplier { get; set; } = 1f;

        /// <summary>이 스폰 포인트가 보스 적을 생성하는지 여부를 나타낸다.</summary>
        public bool IsBoss { get; set; }

        /// <summary>
        /// 게임 내 UI나 로그에 표시될 적의 커스텀 이름 오버라이드다.
        /// null이면 에셋 카탈로그의 기본 이름이 사용된다.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 적에게 적용할 스프라이트 키 오버라이드다.
        /// null이거나 빈 문자열이면 에셋 카탈로그의 기본 스프라이트 키가 사용된다.
        /// </summary>
        public string SpriteVariantKey { get; set; }

        /// <summary>열쇠 방 목표 표적으로 지정된 스폰인지 여부다.</summary>
        public bool IsObjectiveTarget { get; set; }
    }

    /// <summary>
    /// 방과 다른 방 사이의 문 연결 관계를 명시적으로 표현하는 데이터 클래스다.
    /// 어느 문 타일에서 어느 방으로 이동할 수 있는지를 기술한다.
    /// </summary>
    public sealed class StageDoorConnection
    {
        /// <summary>연결이 시작되는 문의 타일 좌표다(그리드 단위).</summary>
        public Point Door { get; set; }

        /// <summary>이 문을 통해 이동할 목적지 방의 ID다.</summary>
        public int TargetRoomId { get; set; }
    }

    /// <summary>
    /// 방의 위치, 경계 영역, 입출구 문, 적 스폰 목록, 방 연결 같은
    /// 변경되지 않는 정적 설계 데이터를 담는 블루프린트 클래스다.
    /// 런타임 진행 상태는 <see cref="StageRoomState"/>가 별도로 관리한다.
    /// </summary>
    public sealed class StageRoomBlueprint
    {
        /// <summary>방의 고유 식별자다. 스테이지 내에서 중복되지 않아야 한다.</summary>
        public int Id { get; set; }

        /// <summary>방의 이름 또는 레이블이다. 디버그 및 설계 용도로 사용된다.</summary>
        public string Name { get; set; }

        /// <summary>방이 차지하는 월드 공간의 직사각형 경계 영역이다(타일 단위).</summary>
        public Rectangle Bounds { get; set; }

        /// <summary>
        /// 이 방의 부모 방 ID다. 분기 구조에서 이전 방을 참조할 때 사용된다.
        /// -1이면 부모가 없는 최상위 방을 나타낸다.
        /// </summary>
        public int ParentRoomId { get; set; } = -1;

        /// <summary>플레이어가 이 방에 진입하는 입구 문의 타일 좌표다.</summary>
        public Point EntryDoor { get; set; }

        /// <summary>이 방에 입구 문이 존재하는지 여부를 나타낸다.</summary>
        public bool HasEntryDoor { get; set; }

        /// <summary>플레이어가 이 방을 나가는 출구 문의 타일 좌표다.</summary>
        public Point ExitDoor { get; set; }

        /// <summary>이 방에 출구 문이 존재하는지 여부를 나타낸다.</summary>
        public bool HasExitDoor { get; set; }

        /// <summary>이 방이 보스 방인지 여부를 나타낸다. 보스 방에서는 특수 연출이 발생할 수 있다.</summary>
        public bool IsBossRoom { get; set; }

        /// <summary>이 방이 미니 보스 방인지 여부를 나타낸다.</summary>
        public bool IsMiniBossRoom { get; set; }

        /// <summary>이 방이 휴식 방인지 여부를 나타낸다.</summary>
        public bool IsRestRoom { get; set; }

        /// <summary>방 내부의 구조물 배치 방식을 나타내는 레이아웃 변형이다.</summary>
        public RoomLayoutVariant LayoutVariant { get; set; }

        /// <summary>이 방의 클리어 목표 종류다.</summary>
        public RoomObjectiveKind ObjectiveKind { get; set; } = RoomObjectiveKind.EliminateAll;

        /// <summary>목표에 시간 제한이 있을 때 사용하는 목표 시간(초).</summary>
        public float ObjectiveDuration { get; set; }

        /// <summary>이 방에 적용되는 위험 modifier 종류다.</summary>
        public RoomHazardKind HazardKind { get; set; } = RoomHazardKind.None;

        /// <summary>
        /// 이 방에서 다른 방으로 연결되는 문 연결 목록이다.
        /// 기본값은 빈 배열이다.
        /// </summary>
        public StageDoorConnection[] Connections { get; set; } = Array.Empty<StageDoorConnection>();

        /// <summary>
        /// 이 방에 배치된 적 스폰 포인트 목록이다.
        /// 기본값은 빈 배열이다.
        /// </summary>
        public StageSpawnPoint[] Spawns { get; set; } = Array.Empty<StageSpawnPoint>();

        /// <summary>
        /// 주어진 월드 좌표가 이 방의 경계 영역 안에 포함되는지 확인한다.
        /// 경계에서 0.1f 안쪽 여백을 두어 경계선 바로 위의 좌표는 포함되지 않는 것으로 처리한다.
        /// </summary>
        /// <param name="x">확인할 월드 공간의 X 좌표다.</param>
        /// <param name="y">확인할 월드 공간의 Y 좌표다.</param>
        /// <returns>좌표가 방 내부에 있으면 <c>true</c>, 그렇지 않으면 <c>false</c>를 반환한다.</returns>
        public bool Contains(float x, float y)
        {
            return x >= Bounds.Left + 0.1f &&
                x < Bounds.Right - 0.1f &&
                y >= Bounds.Top + 0.1f &&
                y < Bounds.Bottom - 0.1f;
        }
    }

    /// <summary>
    /// 방의 활성화 여부, 클리어 여부, 보상 지급 여부처럼
    /// 게임 플레이 중 변경되는 런타임 진행 상태를 담는 클래스다.
    /// 정적 설계 데이터는 <see cref="StageRoomBlueprint"/>가 관리한다.
    /// </summary>
    public sealed class StageRoomState
    {
        /// <summary>방이 플레이어에 의해 활성화(진입 또는 잠금 해제)되었는지 여부를 나타낸다.</summary>
        public bool Activated { get; set; }

        /// <summary>방 내 모든 적을 처치하거나 조건을 달성하여 방이 클리어되었는지 여부를 나타낸다.</summary>
        public bool Cleared { get; set; }

        /// <summary>방 클리어 보상이 이미 지급되었는지 여부를 나타낸다. 중복 지급을 방지하는 데 사용된다.</summary>
        public bool RewardGranted { get; set; }

        /// <summary>활성화된 목표의 남은 시간(초). 시간 목표가 아니면 0이다.</summary>
        public float ObjectiveTimer { get; set; }

        /// <summary>위험 modifier의 다음 피해/효과까지 남은 시간(초).</summary>
        public float HazardTickTimer { get; set; }

        /// <summary>생존 방 증원 생성까지 남은 시간(초).</summary>
        public float ReinforcementTimer { get; set; }

        /// <summary>
        /// 방의 진행 상태를 초기 상태로 리셋한다.
        /// <paramref name="unlockedAtStart"/>가 <c>true</c>이면 처음부터 활성화·클리어·보상 지급 완료 상태로 설정된다.
        /// </summary>
        /// <param name="unlockedAtStart">
        /// 스테이지 시작 시점에 이 방이 이미 잠금 해제된 상태여야 하면 <c>true</c>,
        /// 잠긴 상태로 시작해야 하면 <c>false</c>를 전달한다.
        /// </param>
        public void Reset(bool unlockedAtStart)
        {
            Activated = unlockedAtStart;
            Cleared = unlockedAtStart;
            RewardGranted = unlockedAtStart;
            ObjectiveTimer = 0f;
            HazardTickTimer = 0f;
            ReinforcementTimer = 0f;
        }
    }

    /// <summary>
    /// 변경되지 않는 정적 설계 데이터(<see cref="StageRoomBlueprint"/>)와
    /// 런타임 진행 상태(<see cref="StageRoomState"/>)를 하나로 묶어 편리하게 다루기 위한 방 래퍼 클래스다.
    /// 모든 프로퍼티는 내부적으로 Blueprint 또는 State에 위임(delegate)된다.
    /// </summary>
    public class StageRoom
    {
        /// <summary>
        /// 새로운 <see cref="StageRoom"/> 인스턴스를 생성하고,
        /// 빈 <see cref="StageRoomBlueprint"/>와 <see cref="StageRoomState"/>를 초기화한다.
        /// </summary>
        public StageRoom()
        {
            Blueprint = new StageRoomBlueprint();
            State = new StageRoomState();
        }

        /// <summary>방의 정적 설계 데이터를 담는 블루프린트 객체다.</summary>
        public StageRoomBlueprint Blueprint { get; }

        /// <summary>방의 런타임 진행 상태를 담는 상태 객체다.</summary>
        public StageRoomState State { get; }

        /// <summary>방의 고유 식별자다. 내부적으로 <see cref="StageRoomBlueprint.Id"/>에 위임된다.</summary>
        public int Id
        {
            get => Blueprint.Id;
            set => Blueprint.Id = value;
        }

        /// <summary>방의 이름 또는 레이블이다. 내부적으로 <see cref="StageRoomBlueprint.Name"/>에 위임된다.</summary>
        public string Name
        {
            get => Blueprint.Name;
            set => Blueprint.Name = value;
        }

        /// <summary>방의 경계 직사각형이다. 내부적으로 <see cref="StageRoomBlueprint.Bounds"/>에 위임된다.</summary>
        public Rectangle Bounds
        {
            get => Blueprint.Bounds;
            set => Blueprint.Bounds = value;
        }

        /// <summary>입구 문의 타일 좌표다. 내부적으로 <see cref="StageRoomBlueprint.EntryDoor"/>에 위임된다.</summary>
        public Point EntryDoor
        {
            get => Blueprint.EntryDoor;
            set => Blueprint.EntryDoor = value;
        }

        /// <summary>부모 방 ID다. 내부적으로 <see cref="StageRoomBlueprint.ParentRoomId"/>에 위임된다.</summary>
        public int ParentRoomId
        {
            get => Blueprint.ParentRoomId;
            set => Blueprint.ParentRoomId = value;
        }

        /// <summary>입구 문 존재 여부다. 내부적으로 <see cref="StageRoomBlueprint.HasEntryDoor"/>에 위임된다.</summary>
        public bool HasEntryDoor
        {
            get => Blueprint.HasEntryDoor;
            set => Blueprint.HasEntryDoor = value;
        }

        /// <summary>출구 문의 타일 좌표다. 내부적으로 <see cref="StageRoomBlueprint.ExitDoor"/>에 위임된다.</summary>
        public Point ExitDoor
        {
            get => Blueprint.ExitDoor;
            set => Blueprint.ExitDoor = value;
        }

        /// <summary>출구 문 존재 여부다. 내부적으로 <see cref="StageRoomBlueprint.HasExitDoor"/>에 위임된다.</summary>
        public bool HasExitDoor
        {
            get => Blueprint.HasExitDoor;
            set => Blueprint.HasExitDoor = value;
        }

        /// <summary>보스 방 여부다. 내부적으로 <see cref="StageRoomBlueprint.IsBossRoom"/>에 위임된다.</summary>
        public bool IsBossRoom
        {
            get => Blueprint.IsBossRoom;
            set => Blueprint.IsBossRoom = value;
        }

        /// <summary>미니 보스 방 여부다. 내부적으로 <see cref="StageRoomBlueprint.IsMiniBossRoom"/>에 위임된다.</summary>
        public bool IsMiniBossRoom
        {
            get => Blueprint.IsMiniBossRoom;
            set => Blueprint.IsMiniBossRoom = value;
        }

        /// <summary>휴식 방 여부다. 내부적으로 <see cref="StageRoomBlueprint.IsRestRoom"/>에 위임된다.</summary>
        public bool IsRestRoom
        {
            get => Blueprint.IsRestRoom;
            set => Blueprint.IsRestRoom = value;
        }

        /// <summary>방 레이아웃 변형이다. 내부적으로 <see cref="StageRoomBlueprint.LayoutVariant"/>에 위임된다.</summary>
        public RoomLayoutVariant LayoutVariant
        {
            get => Blueprint.LayoutVariant;
            set => Blueprint.LayoutVariant = value;
        }

        /// <summary>방의 클리어 목표 종류다. 내부적으로 <see cref="StageRoomBlueprint.ObjectiveKind"/>에 위임된다.</summary>
        public RoomObjectiveKind ObjectiveKind
        {
            get => Blueprint.ObjectiveKind;
            set => Blueprint.ObjectiveKind = value;
        }

        /// <summary>시간 목표의 기준 지속 시간(초)이다. 내부적으로 <see cref="StageRoomBlueprint.ObjectiveDuration"/>에 위임된다.</summary>
        public float ObjectiveDuration
        {
            get => Blueprint.ObjectiveDuration;
            set => Blueprint.ObjectiveDuration = value;
        }

        /// <summary>방에 적용된 위험 modifier 종류다. 내부적으로 <see cref="StageRoomBlueprint.HazardKind"/>에 위임된다.</summary>
        public RoomHazardKind HazardKind
        {
            get => Blueprint.HazardKind;
            set => Blueprint.HazardKind = value;
        }

        /// <summary>
        /// 방의 적 스폰 포인트 목록이다. 내부적으로 <see cref="StageRoomBlueprint.Spawns"/>에 위임된다.
        /// null을 설정하면 빈 배열로 대체된다.
        /// </summary>
        public StageSpawnPoint[] Spawns
        {
            get => Blueprint.Spawns;
            set => Blueprint.Spawns = value ?? Array.Empty<StageSpawnPoint>();
        }

        /// <summary>
        /// 방의 문 연결 목록이다. 내부적으로 <see cref="StageRoomBlueprint.Connections"/>에 위임된다.
        /// null을 설정하면 빈 배열로 대체된다.
        /// </summary>
        public StageDoorConnection[] Connections
        {
            get => Blueprint.Connections;
            set => Blueprint.Connections = value ?? Array.Empty<StageDoorConnection>();
        }

        /// <summary>방 활성화 여부다. 내부적으로 <see cref="StageRoomState.Activated"/>에 위임된다.</summary>
        public bool Activated
        {
            get => State.Activated;
            set => State.Activated = value;
        }

        /// <summary>방 클리어 여부다. 내부적으로 <see cref="StageRoomState.Cleared"/>에 위임된다.</summary>
        public bool Cleared
        {
            get => State.Cleared;
            set => State.Cleared = value;
        }

        /// <summary>보상 지급 완료 여부다. 내부적으로 <see cref="StageRoomState.RewardGranted"/>에 위임된다.</summary>
        public bool RewardGranted
        {
            get => State.RewardGranted;
            set => State.RewardGranted = value;
        }

        /// <summary>
        /// 주어진 월드 좌표가 이 방의 경계 영역 안에 포함되는지 확인한다.
        /// 내부적으로 <see cref="StageRoomBlueprint.Contains"/>에 위임된다.
        /// </summary>
        /// <param name="x">확인할 월드 공간의 X 좌표다.</param>
        /// <param name="y">확인할 월드 공간의 Y 좌표다.</param>
        /// <returns>좌표가 방 내부에 있으면 <c>true</c>, 그렇지 않으면 <c>false</c>를 반환한다.</returns>
        public bool Contains(float x, float y)
        {
            return Blueprint.Contains(x, y);
        }

        /// <summary>
        /// 방의 런타임 진행 상태를 초기화한다.
        /// 내부적으로 <see cref="StageRoomState.Reset"/>을 호출한다.
        /// </summary>
        /// <param name="unlockedAtStart">
        /// 스테이지 시작 시점에 이 방이 이미 잠금 해제된 상태여야 하면 <c>true</c>,
        /// 잠긴 상태로 시작해야 하면 <c>false</c>를 전달한다.
        /// </param>
        public void ResetProgress(bool unlockedAtStart)
        {
            State.Reset(unlockedAtStart);
        }
    }
}
