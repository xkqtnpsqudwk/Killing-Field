using System.Drawing;
using My2DEngine.Game.Config;

namespace My2DEngine.Game.Map
{
    /// <summary>
    /// 벽 종류를 정하는 규칙. 벽이 난잡해지지 않도록 방마다 쓰는 종류를 셋으로 묶는다.
    /// <list type="number">
    ///   <item>바탕 벽은 방 종류로 정한다: 시작실·카드 상점은 연구소, 보스방은 지옥, 나머지는 기지.</item>
    ///   <item>구조를 이루는 벽은 골조로 통일한다: 두 방이 맞닿은 벽, 문 양옆, 방 안 기둥.</item>
    ///   <item>방 안 엄폐 벽은 모두 상자 더미로 칠한다.</item>
    /// </list>
    /// 무작위는 쓰지 않는다. 같은 방 종류는 언제나 같은 모습이라 방 종류를 벽으로 알아볼 수 있다.
    /// 텍스처는 모두 같은 높이에 가로 띠가 있어 종류가 바뀌어도 이어져 보인다(Tools/ArtGen/world.py).
    /// </summary>
    public partial class MapManager
    {
        /// <summary>
        /// 시작실과 전투실의 바깥 벽, 맞닿은 벽, 문 양옆에 벽 종류를 칠한다.
        /// 방 안 구조물(기둥·엄폐물)은 만들 때 이미 종류가 정해진다.
        /// 문과 레이아웃 배치가 끝난 뒤에 호출해야 한다.
        /// </summary>
        private void ApplyWallThemes(StageRoom startRoom, StageRoom room)
        {
            PaintRoomBoundary(startRoom.Bounds, GetRoomWallTexture(startRoom));
            PaintRoomBoundary(room.Bounds, GetRoomWallTexture(room));
            PaintSharedWall(startRoom.Bounds, room.Bounds);

            if (room.HasEntryDoor)
            {
                PaintDoorFrame(room.EntryDoor);
            }

            if (room.HasExitDoor)
            {
                PaintDoorFrame(room.ExitDoor);
            }
        }

        /// <summary>방 종류에 맞는 바탕 벽 텍스처.</summary>
        private static int GetRoomWallTexture(StageRoom room)
        {
            if (room.IsBossRoom)
            {
                return WorldConfig.WallTextureHell;
            }

            if (room.Name == "Start" || room.IsRestRoom)
            {
                return WorldConfig.WallTextureLab;
            }

            return WorldConfig.WallTextureBase;
        }

        /// <summary>
        /// 방 사각형의 테두리 한 줄 중 막힌 벽 타일만 칠한다(문은 건드리지 않는다).
        /// 중앙 통로가 테두리를 뚫어 생긴 한 칸 홈은 안쪽 벽까지 같은 종류로 칠한다.
        /// </summary>
        private void PaintRoomBoundary(Rectangle rect, int textureId)
        {
            for (int x = rect.Left; x < rect.Right; x++)
            {
                PaintBoundaryTile(x, rect.Top, 0, -1, textureId);
                PaintBoundaryTile(x, rect.Bottom - 1, 0, 1, textureId);
            }

            for (int y = rect.Top + 1; y < rect.Bottom - 1; y++)
            {
                PaintBoundaryTile(rect.Left, y, -1, 0, textureId);
                PaintBoundaryTile(rect.Right - 1, y, 1, 0, textureId);
            }
        }

        /// <summary>테두리 한 칸을 칠한다. 뚫린 칸(문 제외)이면 바깥쪽(outX, outY) 벽을 대신 칠한다.</summary>
        private void PaintBoundaryTile(int x, int y, int outX, int outY, int textureId)
        {
            if (IsSolidWall(x, y))
            {
                textureIds[x, y] = textureId;
            }
            else if (map[x, y] != WorldConfig.DoorTileType)
            {
                // 홈 안쪽 세 면(뒤와 비스듬히 보이는 양옆 뒤)
                PaintSolidWall(x + outX, y + outY, textureId);
                PaintSolidWall(x + outX + outY, y + outY + outX, textureId);
                PaintSolidWall(x + outX - outY, y + outY - outX, textureId);
            }
        }

        /// <summary>두 방이 겹치는 테두리 타일을 골조로 칠한다. 방 경계가 어느 쪽에서 봐도 같아진다.</summary>
        private void PaintSharedWall(Rectangle a, Rectangle b)
        {
            Rectangle overlap = Rectangle.Intersect(a, b);
            for (int y = overlap.Top; y < overlap.Bottom; y++)
            {
                for (int x = overlap.Left; x < overlap.Right; x++)
                {
                    PaintSolidWall(x, y, WorldConfig.WallTextureSupport);
                }
            }
        }

        /// <summary>문 양옆(문이 놓인 벽 방향) 한 칸씩을 골조 기둥으로 칠한다.</summary>
        private void PaintDoorFrame(Point door)
        {
            bool horizontalWall = IsSolidWall(door.X - 1, door.Y) || IsSolidWall(door.X + 1, door.Y);
            if (horizontalWall)
            {
                PaintSolidWall(door.X - 1, door.Y, WorldConfig.WallTextureSupport);
                PaintSolidWall(door.X + 1, door.Y, WorldConfig.WallTextureSupport);
            }
            else
            {
                PaintSolidWall(door.X, door.Y - 1, WorldConfig.WallTextureSupport);
                PaintSolidWall(door.X, door.Y + 1, WorldConfig.WallTextureSupport);
            }
        }

        private bool IsSolidWall(int x, int y)
        {
            return map != null && x >= 0 && y >= 0 && x < map.GetLength(0) && y < map.GetLength(1) && map[x, y] == 1;
        }

        private void PaintSolidWall(int x, int y, int textureId)
        {
            if (IsSolidWall(x, y))
            {
                textureIds[x, y] = textureId;
            }
        }
    }
}
