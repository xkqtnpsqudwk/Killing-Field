using System.Collections.Generic;

namespace My2DEngine.Game.Map
{
    /// <summary>
    /// 지금 층의 스테이지 방 조회. 방 ID 표와 "플레이어가 있는 방" 캐시를 갖는다.
    /// 방 목록은 MapManager에서 매번 읽으므로, 맵이 바뀌면 <see cref="Rebuild"/>로 ID 표만 다시 만든다.
    /// </summary>
    public sealed class StageRoomIndex
    {
        private readonly MapManager map;
        private readonly Dictionary<int, StageRoom> byId = new Dictionary<int, StageRoom>();

        /// <summary>직전에 찾은 "현재 방". 같은 방에 머무는 동안 전체 탐색을 건너뛴다.</summary>
        private StageRoom currentCache;

        public StageRoomIndex(MapManager map)
        {
            this.map = map;
        }

        /// <summary>현재 맵의 방 목록으로 ID 표를 다시 만들고 현재 방 캐시를 지운다.</summary>
        public void Rebuild()
        {
            byId.Clear();
            currentCache = null;

            StageRoom[] rooms = map.StageRooms;
            if (rooms == null)
            {
                return;
            }

            for (int i = 0; i < rooms.Length; i++)
            {
                StageRoom room = rooms[i];
                if (room != null)
                {
                    byId[room.Id] = room;
                }
            }
        }

        /// <summary>현재 방 캐시만 지운다(플레이어를 순간이동시킬 때).</summary>
        public void ForgetCurrent()
        {
            currentCache = null;
        }

        /// <summary>방 ID로 찾는다. 없으면 null.</summary>
        public StageRoom Get(int roomId)
        {
            return byId.TryGetValue(roomId, out StageRoom room) ? room : null;
        }

        /// <summary>좌표를 포함하는 방. 없으면 null.</summary>
        public StageRoom FindAt(float x, float y)
        {
            StageRoom[] rooms = map.StageRooms;
            if (rooms == null)
            {
                return null;
            }

            for (int i = 0; i < rooms.Length; i++)
            {
                StageRoom room = rooms[i];
                if (room != null && room.Contains(x, y))
                {
                    return room;
                }
            }

            return null;
        }

        /// <summary>
        /// 플레이어 위치의 방. 직전 결과가 아직 맞으면 그대로 돌려준다.
        /// </summary>
        public StageRoom FindCurrent(float playerX, float playerY)
        {
            if (currentCache != null && currentCache.Contains(playerX, playerY))
            {
                return currentCache;
            }

            currentCache = FindAt(playerX, playerY);
            return currentCache;
        }

        /// <summary>보스 방이 하나 이상 있고 모두 클리어됐는지. 보스 방이 없으면 false(맵 데이터 오류 방어).</summary>
        public bool AreAllBossRoomsCleared()
        {
            StageRoom[] rooms = map.StageRooms;
            if (rooms == null)
            {
                return false;
            }

            bool hasBossRoom = false;
            for (int i = 0; i < rooms.Length; i++)
            {
                StageRoom room = rooms[i];
                if (room == null || !room.IsBossRoom)
                {
                    continue;
                }

                hasBossRoom = true;
                if (!room.State.Cleared)
                {
                    return false;
                }
            }

            return hasBossRoom;
        }
    }
}
