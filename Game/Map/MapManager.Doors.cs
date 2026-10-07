using System.Drawing;
using My2DEngine.Game.Config;

namespace My2DEngine.Game.Map
{
    /// <summary>
    /// 문 개폐 상태·충돌·애니메이션 진행을 담당하는 MapManager partial 파일이다.
    /// 문 타일은 BeginDoorOpening()으로 열리기 시작해 UpdateDoorAnimations()에서 진행되다가
    /// 진행도가 1이 되면 OpenDoor()에서 빈 공간 타일로 교체된다.
    /// </summary>
    public partial class MapManager
    {
        /// <summary>
        /// 문을 즉시 열어 빈 공간 타일로 교체하고 애니메이션 진행도를 제거한다.
        /// UpdateDoorAnimations에서 진행도가 1에 도달하면 자동으로 호출된다.
        /// </summary>
        /// <param name="door">열 문의 타일 좌표</param>
        public void OpenDoor(Point door)
        {
            doorProgress.Remove(GetDoorKey(door.X, door.Y));
            SetTile(door.X, door.Y, 0, 0);
        }

        /// <summary>
        /// 열려 있는 타일을 다시 문 타일로 교체하고 진행도를 0으로 초기화한다.
        /// 스테이지 재시작 시 문을 닫힌 상태로 되돌릴 때 사용한다.
        /// </summary>
        /// <param name="door">닫을 문의 타일 좌표</param>
        public void CloseDoor(Point door)
        {
            SetTile(door.X, door.Y, WorldConfig.DoorTileType, WorldConfig.DoorTextureId);
            doorProgress[GetDoorKey(door.X, door.Y)] = 0f;
        }

        /// <summary>
        /// 문 열림 애니메이션을 시작한다.
        /// 이미 열리는 중이면 false를 반환하고 중복 시작을 방지한다.
        /// </summary>
        /// <param name="door">열기 시작할 문의 타일 좌표</param>
        /// <returns>새로 열림이 시작됐으면 true</returns>
        public bool BeginDoorOpening(Point door)
        {
            if (map == null || door.X < 0 || door.Y < 0 || door.X >= map.GetLength(0) || door.Y >= map.GetLength(1))
            {
                return false;
            }

            if (map[door.X, door.Y] != WorldConfig.DoorTileType)
            {
                return false;
            }

            int key = GetDoorKey(door.X, door.Y);
            if (!doorProgress.TryGetValue(key, out float progress) || progress <= 0f)
            {
                doorProgress[key] = 0.001f;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 열리는 중인 모든 문의 애니메이션 진행도를 dt만큼 전진시킨다.
        /// 진행도가 1에 도달하면 해당 문을 실제로 열어(OpenDoor) 타일을 교체한다.
        /// 딕셔너리를 직접 순회하면서 수정하는 문제를 doorKeysBuffer로 우회한다.
        /// </summary>
        /// <param name="dt">이번 프레임 DeltaTime(초)</param>
        public void UpdateDoorAnimations(float dt)
        {
            if (doorProgress.Count == 0)
            {
                return;
            }

            doorKeysBuffer.Clear();
            foreach (int key in doorProgress.Keys)
            {
                doorKeysBuffer.Add(key);
            }

            for (int i = 0; i < doorKeysBuffer.Count; i++)
            {
                int key = doorKeysBuffer[i];
                float progress = doorProgress[key];
                if (progress <= 0f)
                {
                    continue;
                }

                progress += dt / WorldConfig.DoorOpenDuration;
                int x = key & 0xFFFF;
                int y = key >> 16;

                if (progress >= 1f)
                {
                    OpenDoor(new Point(x, y));
                }
                else
                {
                    doorProgress[key] = progress;
                }
            }
        }

        public float GetDoorOpenProgress(int x, int y)
        {
            int key = GetDoorKey(x, y);
            if (doorProgress.TryGetValue(key, out float progress))
            {
                return progress;
            }

            return 0f;
        }

        public bool IsDoorClosed(Point door)
        {
            if (map == null || door.X < 0 || door.Y < 0 || door.X >= map.GetLength(0) || door.Y >= map.GetLength(1))
            {
                return false;
            }

            return map[door.X, door.Y] == WorldConfig.DoorTileType;
        }

        /// <summary>
        /// 지정 좌표의 타일 유형과 텍스처 ID를 동시에 변경한다.
        /// 문 타일이 아닌 값으로 바꾸면 해당 좌표의 doorProgress 항목도 자동으로 제거한다.
        /// </summary>
        /// <param name="x">타일 X 좌표</param>
        /// <param name="y">타일 Y 좌표</param>
        /// <param name="tileType">설정할 타일 유형</param>
        /// <param name="textureId">설정할 텍스처 ID</param>
        private void SetTile(int x, int y, int tileType, int textureId)
        {
            if (map == null || textureIds == null)
            {
                return;
            }

            if (x < 0 || y < 0 || x >= map.GetLength(0) || y >= map.GetLength(1))
            {
                return;
            }

            map[x, y] = tileType;
            textureIds[x, y] = textureId;

            if (tileType != WorldConfig.DoorTileType)
            {
                doorProgress.Remove(GetDoorKey(x, y));
            }
        }

        /// <summary>
        /// 타일 좌표 (x, y)를 doorProgress 딕셔너리 키로 변환한다.
        /// y를 상위 16비트, x를 하위 16비트에 패킹해 단일 정수 키를 만든다.
        /// 맵 크기는 최대 65535 × 65535까지 지원된다.
        /// </summary>
        /// <param name="x">타일 X 좌표(0~65535)</param>
        /// <param name="y">타일 Y 좌표(0~65535)</param>
        private int GetDoorKey(int x, int y)
        {
            return (y << 16) | (x & 0xFFFF);
        }
    }
}
