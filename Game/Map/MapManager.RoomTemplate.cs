using System.Drawing;
using My2DEngine.Engine.Math;
using My2DEngine.Game.Config;

namespace My2DEngine.Game.Map
{
    /// <summary>
    /// 시작실과 전투실을 함께 생성하는 로그라이크 룸 템플릿 로드 partial 파일.
    /// 플레이어는 남쪽 시작실에서 출발해 문을 열고 전투실로 진입하며,
    /// 전투실 북쪽 벽에는 다음 층으로 가는 출구 문(TargetRoomId = -1)이 배치된다.
    /// </summary>
    public partial class MapManager
    {
        /// <summary>로그라이크 시작실의 고정 StageRoom ID. 0은 시작 방 예약값으로 사용한다.</summary>
        private const int RoguelikeStartRoomId = 0;

        /// <summary>로그라이크 전투실의 고정 StageRoom ID.</summary>
        private const int RoguelikeRoomId = 1;

        /// <summary>
        /// 지정된 <see cref="RoomTemplate"/>을 맵 중앙에 로드한다.
        /// <para>처리 순서:</para>
        /// <list type="number">
        ///   <item>맵 전체를 벽으로 초기화한다.</item>
        ///   <item>전투실과 시작실 내부를 파낸다(CarveRoom).</item>
        ///   <item>레이아웃 구조물(기둥·엄폐물)을 배치한다.</item>
        ///   <item>시작실 입구 문과 전투실 출구 문을 배치한다.</item>
        ///   <item>스폰 포인트를 전투실 중심 상대 좌표에서 절대 좌표로 변환한다.</item>
        ///   <item>stageRooms, hasStageFlow, playerStart 필드를 갱신한다.</item>
        /// </list>
        /// </summary>
        /// <param name="template">로드할 룸 템플릿.</param>
        /// <param name="startPosition">플레이어가 배치될 시작 위치(맵 중심).</param>
        /// <param name="startDirection">플레이어 초기 방향(북쪽: (0, -1)).</param>
        public void LoadRoomFromTemplate(RoomTemplate template,
            out Vector2 startPosition, out Vector2 startDirection)
        {
            int roomW = template.RoomWidth;
            int roomH = template.RoomHeight;
            int left = MapCenter - roomW / 2;
            int top = MapCenter - roomH / 2;
            Rectangle roomBounds = new Rectangle(left, top, roomW, roomH);
            int startRoomWidth = System.Math.Max(7, System.Math.Min(11, roomW - 4));
            int startRoomHeight = 7;
            int startRoomLeft = MapCenter - startRoomWidth / 2;
            int startRoomTop = roomBounds.Bottom - 1;
            Rectangle startRoomBounds = new Rectangle(startRoomLeft, startRoomTop, startRoomWidth, startRoomHeight);

            // 1. 맵 전체 벽으로 초기화
            EnsureGeneratedMapBuffers();
            floorHeights = null;
            ceilHeights = null;
            doorProgress.Clear();
            FillSolidMap(GeneratedMapSize, GeneratedMapSize);

            // 2. 시작실 / 전투실 내부 파내기
            CarveRoom(startRoomBounds);
            CarveRoom(roomBounds);

            // 3. StageRoom 생성 (레이아웃 적용에 사용)
            StageRoom startRoom = new StageRoom
            {
                Id = RoguelikeStartRoomId,
                Name = "Start",
                Bounds = startRoomBounds,
                LayoutVariant = RoomLayoutVariant.Open
            };

            StageRoom room = new StageRoom
            {
                Id = RoguelikeRoomId,
                Name = template.TemplateId ?? "RogueRoom",
                Bounds = roomBounds,
                IsBossRoom = template.IsBossRoom,
                IsMiniBossRoom = template.IsMiniBossRoom,
                IsRestRoom = template.IsRestRoom,
                LayoutVariant = template.LayoutVariant,
                ObjectiveKind = template.ObjectiveKind,
                ObjectiveDuration = template.ObjectiveDuration,
                HazardKind = template.HazardKind
            };

            // 4. 레이아웃 구조물 배치 (EnsureRoomTraversal이 북쪽 벽을 비울 수 있으므로 먼저 실행)
            ApplyRoomLayouts(new[] { startRoom, room });

            int entryDoorX = MapCenter;
            int entryDoorY = roomBounds.Bottom - 1;

            // 5. 시작실 입구 문 / 전투실 출구 문 배치 (ApplyRoomLayouts 이후 덮어쓰기)
            SetTile(entryDoorX, entryDoorY, GameConfig.DoorTileType, GameConfig.DoorTextureId);
            int exitDoorX = MapCenter;
            int exitDoorY = roomBounds.Top;
            SetTile(exitDoorX, exitDoorY, GameConfig.DoorTileType, GameConfig.DoorTextureId);

            startRoom.HasExitDoor = true;
            startRoom.ExitDoor = new Point(entryDoorX, entryDoorY);
            startRoom.Connections = new[]
            {
                new StageDoorConnection
                {
                    Door = new Point(entryDoorX, entryDoorY),
                    TargetRoomId = RoguelikeRoomId
                }
            };

            room.HasEntryDoor = true;
            room.EntryDoor = new Point(entryDoorX, entryDoorY);
            room.HasExitDoor = true;
            room.ExitDoor = new Point(exitDoorX, exitDoorY);

            // 6. 출구 문 연결 (TargetRoomId = -1 → 층 이동 신호)
            room.Connections = new[]
            {
                new StageDoorConnection
                {
                    Door = new Point(exitDoorX, exitDoorY),
                    TargetRoomId = -1
                }
            };

            // 7. 스폰 포인트 절대 좌표 변환 (방 중심 상대 오프셋 → 맵 절대 좌표)
            if (template.Spawns != null && template.Spawns.Length > 0)
            {
                StageSpawnPoint[] abs = new StageSpawnPoint[template.Spawns.Length];
                for (int i = 0; i < template.Spawns.Length; i++)
                {
                    StageSpawnPoint s = template.Spawns[i];
                    abs[i] = new StageSpawnPoint
                    {
                        EnemyAssetId = s.EnemyAssetId,
                        Type = s.Type,
                        Rank = s.Rank,
                        BehaviorPattern = s.BehaviorPattern,
                        BehaviorPatternPool = s.BehaviorPatternPool == null ? null : (EnemyBehaviorPattern[])s.BehaviorPatternPool.Clone(),
                        X = MapCenter + s.X,
                        Y = MapCenter + s.Y,
                        HealthMultiplier = s.HealthMultiplier,
                        DamageMultiplier = s.DamageMultiplier,
                        MoveSpeedMultiplier = s.MoveSpeedMultiplier,
                        AttackRangeMultiplier = s.AttackRangeMultiplier,
                        ScaleMultiplier = s.ScaleMultiplier,
                        IsBoss = s.IsBoss,
                        DisplayName = s.DisplayName,
                        SpriteVariantKey = s.SpriteVariantKey,
                        IsObjectiveTarget = s.IsObjectiveTarget
                    };
                }

                room.Spawns = abs;
            }

            // 8. 상태 갱신
            stageRooms = new[] { startRoom, room };
            hasStageFlow = true;

            // 플레이어는 시작실 북쪽 문 바로 앞에서 출발한다.
            float spawnX = MapCenter + 0.5f;
            float spawnY = startRoomBounds.Top + 1.5f;
            playerStartPosition = new Vector2(spawnX, spawnY);
            playerStartDirection = new Vector2(0f, -1f);
            playerStartFov = GameConfig.DefaultFovDegrees;

            startPosition = playerStartPosition;
            startDirection = playerStartDirection;
        }

        /// <summary>
        /// 고정 크기 로그라이크 생성 맵 버퍼를 재사용한다.
        /// 201x201 배열은 LOH에 올라가므로 층마다 새로 만들지 않고 비워서 다시 쓴다.
        /// </summary>
        private void EnsureGeneratedMapBuffers()
        {
            if (map == null || map.GetLength(0) != GeneratedMapSize || map.GetLength(1) != GeneratedMapSize)
            {
                map = new int[GeneratedMapSize, GeneratedMapSize];
            }
            else
            {
                System.Array.Clear(map, 0, map.Length);
            }

            if (textureIds == null || textureIds.GetLength(0) != GeneratedMapSize || textureIds.GetLength(1) != GeneratedMapSize)
            {
                textureIds = new int[GeneratedMapSize, GeneratedMapSize];
            }
            else
            {
                System.Array.Clear(textureIds, 0, textureIds.Length);
            }
        }
    }
}
