using System.Collections.Generic;
using My2DEngine.Engine.Math;
using My2DEngine.Game.Config;
using My2DEngine.Game.Systems;

namespace My2DEngine.Game.Map
{
    /// <summary>
    /// 맵 타일 데이터, 문 개폐 애니메이션, 스테이지 방 메타데이터를 함께 관리하는 partial 클래스다.
    /// MapManager.Doors, MapManager.Generation, MapManager.RoomTemplate 등 partial 파일로 기능이 분리된다.
    /// </summary>
    public partial class MapManager
    {
        /// <summary>
        /// 맵 타일 유형 2D 배열. [x, y] 인덱스로 접근하며 0=빈 공간, 1~8=벽, 9=문이다.
        /// </summary>
        private int[,] map;

        /// <summary>
        /// 타일별 텍스처 ID 2D 배열. map과 같은 크기이며 렌더러가 벽 텍스처를 선택하는 데 사용한다.
        /// </summary>
        private int[,] textureIds;

        /// <summary>
        /// 타일별 바닥 높이 2D 배열 [x, y]. 값 범위 0.0~1.0. null이면 모든 타일이 기본값 0.0으로 간주된다.
        /// 맵 로드 시 채워지며, 0.0은 바닥(기본), 1.0은 최대 높이를 의미한다.
        /// </summary>
        private float[,] floorHeights;

        /// <summary>
        /// 타일별 천장 높이 2D 배열 [x, y]. 값 범위 0.0~2.0. null이면 모든 타일이 기본값 1.0으로 간주된다.
        /// 벽 타일의 경우 이 값이 1.0보다 작으면 낮은 벽(barrier)이 된다.
        /// </summary>
        private float[,] ceilHeights;

        /// <summary>
        /// 문 좌표 키 → 열림 진행도(0~1) 딕셔너리. 키는 GetDoorKey(x, y)로 생성한다.
        /// 진행도가 1에 도달하면 해당 타일을 빈 공간(0)으로 변환한다.
        /// </summary>
        private readonly Dictionary<int, float> doorProgress;

        /// <summary>
        /// UpdateDoorAnimations에서 딕셔너리를 순회하며 키를 수집하는 임시 버퍼.
        /// 순회 중 딕셔너리를 수정하는 문제를 피하기 위해 별도로 분리한다.
        /// </summary>
        private readonly List<int> doorKeysBuffer;

        /// <summary>맵 로드 후 플레이어가 시작해야 하는 월드 좌표.</summary>
        private Vector2 playerStartPosition;

        /// <summary>맵 로드 후 플레이어가 처음 바라봐야 하는 방향 단위 벡터.</summary>
        private Vector2 playerStartDirection;

        /// <summary>맵 로드 후 사용할 초기 시야각(도 단위).</summary>
        private float playerStartFov;

        /// <summary>스테이지 방 배열. 스테이지 생성 후 채워지며 빈 스테이지면 길이 0이다.</summary>
        private StageRoom[] stageRooms;

        /// <summary>true이면 stageRooms에 유효한 스테이지 플로우 데이터가 있음을 나타낸다.</summary>
        private bool hasStageFlow;

        public int[,] Map => map;
        public int[,] TextureIds => textureIds;
        public float[,] FloorHeights => floorHeights;
        public Vector2 PlayerStartPosition => playerStartPosition;
        public Vector2 PlayerStartDirection => playerStartDirection;
        public float PlayerStartFov => playerStartFov;
        public StageRoom[] StageRooms => stageRooms;
        public bool HasStageFlow => hasStageFlow;

        /// <summary>
        /// 지정된 타일 좌표의 바닥 높이를 반환한다.
        /// 범위 밖이거나 floorHeights가 null이면 기본값 0.0을 반환한다.
        /// </summary>
        public float GetFloorHeight(int x, int y)
        {
            if (floorHeights == null || x < 0 || y < 0 || x >= floorHeights.GetLength(0) || y >= floorHeights.GetLength(1))
            {
                return 0f;
            }

            return floorHeights[x, y];
        }

        /// <summary>
        /// 지정된 타일 좌표의 천장 높이를 반환한다.
        /// 범위 밖이거나 ceilHeights가 null이면 기본값 1.0을 반환한다.
        /// </summary>
        public float GetCeilHeight(int x, int y)
        {
            if (ceilHeights == null || x < 0 || y < 0 || x >= ceilHeights.GetLength(0) || y >= ceilHeights.GetLength(1))
            {
                return 1f;
            }

            return ceilHeights[x, y];
        }

        public MapManager()
        {
            doorProgress = new Dictionary<int, float>();
            doorKeysBuffer = new List<int>();
            map = BuildDefaultMap();
            textureIds = BuildDefaultTextureIds(map);
            floorHeights = null;
            ceilHeights = null;
            stageRooms = new StageRoom[0];
            hasStageFlow = false;
            ResetPlayerStartToDefaults();
        }

        /// <summary>
        /// 16x16 크기의 기본 맵 타일 배열을 반환한다.
        /// 외곽은 벽, 내부는 바닥이며 중앙 기둥 하나를 포함한다.
        /// </summary>
        /// <returns>[x, y] 인덱스 형식의 타일 타입 2차원 배열.</returns>
        public static int[,] BuildDefaultMap()
        {
            return new int[,]
            {
                {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
                {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
                {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
                {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
                {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
                {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
                {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
                {1,0,0,0,0,0,1,1,1,1,0,0,0,0,0,1},
                {1,0,0,0,0,0,1,0,0,1,0,0,0,0,0,1},
                {1,0,0,0,0,0,1,0,0,1,0,0,0,0,0,1},
                {1,0,0,0,0,0,1,1,1,1,0,0,0,0,0,1},
                {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
                {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
                {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
                {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
                {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
            };
        }

        /// <summary>
        /// 타일 타입 격자를 기반으로 기본 텍스처 ID 격자를 생성한다.
        /// 문은 문 텍스처, 솔리드 타일은 벽 텍스처, 바닥은 0을 사용한다.
        /// </summary>
        /// <param name="tileTypes">텍스처 ID를 생성할 기준 타일 타입 격자.</param>
        /// <returns>타일 타입에 대응하는 텍스처 ID 2차원 배열.</returns>
        private static int[,] BuildDefaultTextureIds(int[,] tileTypes)
        {
            if (tileTypes == null)
            {
                tileTypes = BuildDefaultMap();
            }

            int w = tileTypes.GetLength(0);
            int h = tileTypes.GetLength(1);
            int[,] ids = new int[w, h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (tileTypes[x, y] == WorldConfig.DoorTileType)
                    {
                        ids[x, y] = WorldConfig.DoorTextureId;
                    }
                    else
                    {
                        ids[x, y] = CollisionSystem.IsSolidType(tileTypes[x, y]) ? 1 : 0;
                    }
                }
            }

            return ids;
        }

        /// <summary>
        /// 플레이어 시작 위치, 방향, 시야각을 기본값으로 초기화한다.
        /// </summary>
        private void ResetPlayerStartToDefaults()
        {
            playerStartPosition = new Vector2(3.5f, 3.5f);
            playerStartDirection = new Vector2(1f, 0f);
            playerStartFov = PlayerConfig.DefaultFovDegrees;
        }
    }
}
