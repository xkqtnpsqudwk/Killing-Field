using System.Drawing;
using My2DEngine.Game.Config;

namespace My2DEngine.Game.Map
{
    /// <summary>
    /// MapManager의 장식(Decor) 파셜 클래스입니다.
    /// 방 내부의 기둥, 엄폐물 등 전투 구조물을 배치하고,
    /// 모든 방이 가로·세로 방향으로 이동 가능하도록 통로를 보정합니다.
    /// </summary>
    public partial class MapManager
    {
        /// <summary>
        /// 모든 방에 레이아웃 변형(<see cref="RoomLayoutVariant"/>)에 맞는 내부 구조물을 배치합니다.
        /// <para>
        /// 보스 방은 건너뛰고, 시작 방이 아닌 전투 방에는 공통 구조물 배치 함수를 호출합니다.
        /// 이후 레이아웃 변형에 따라 추가 기둥(pillar)을 배치하고,
        /// 마지막으로 모든 방에 대해 통로 보정을 수행합니다.
        /// </para>
        /// </summary>
        /// <param name="rooms">
        /// 구조물을 배치할 모든 방 배열입니다. null인 경우 즉시 반환합니다.
        /// </param>
        private void ApplyRoomLayouts(StageRoom[] rooms)
        {
            if (rooms == null)
            {
                return;
            }

            for (int i = 0; i < rooms.Length; i++)
            {
                StageRoom room = rooms[i];
                if (room == null || room.IsBossRoom)
                {
                    continue;
                }

                Rectangle rect = room.Bounds;
                if (room.Name != "Start" && !room.IsRestRoom)
                {
                    BuildGeneralRoomStructures(room);
                }

                switch (room.LayoutVariant)
                {
                    case RoomLayoutVariant.CenterPillar:
                        BuildPillar(rect.Left + rect.Width / 2 - 1, rect.Top + rect.Height / 2 - 1, 2, 2);
                        break;
                    case RoomLayoutVariant.TwinPillars:
                        BuildPillar(rect.Left + 1, rect.Top + rect.Height / 2 - 1, 2, 2);
                        BuildPillar(rect.Right - 3, rect.Top + rect.Height / 2 - 1, 2, 2);
                        break;
                    case RoomLayoutVariant.CornerPillars:
                        BuildPillar(rect.Left + 1, rect.Top + 1, 2, 2);
                        BuildPillar(rect.Right - 3, rect.Top + 1, 2, 2);
                        BuildPillar(rect.Left + 1, rect.Bottom - 3, 2, 2);
                        BuildPillar(rect.Right - 3, rect.Bottom - 3, 2, 2);
                        break;
                    case RoomLayoutVariant.SplitLanes:
                        BuildHorizontalCover(rect.Left + 1, rect.Right - 2, rect.Top + rect.Height / 2, rect.Left + rect.Width / 2 - 1, 2);
                        break;
                    case RoomLayoutVariant.DiagonalPillars:
                        // 좌상→우하 대각선 기둥 2쌍 (4개)
                        BuildPillar(rect.Left  + rect.Width  / 4 - 1, rect.Top    + rect.Height / 4 - 1, 2, 2);
                        BuildPillar(rect.Right - rect.Width  / 4 - 1, rect.Bottom - rect.Height / 4 - 1, 2, 2);
                        BuildPillar(rect.Right - rect.Width  / 4 - 1, rect.Top    + rect.Height / 4 - 1, 2, 2);
                        BuildPillar(rect.Left  + rect.Width  / 4 - 1, rect.Bottom - rect.Height / 4 - 1, 2, 2);
                        break;
                    case RoomLayoutVariant.InnerRing:
                        // 방 내부 중앙 링 위치에 4개 기둥 (CornerPillars보다 안쪽)
                        {
                            int rx = rect.Width  / 3;
                            int ry = rect.Height / 3;
                            BuildPillar(rect.Left + rx - 1,     rect.Top  + ry - 1,     2, 2);
                            BuildPillar(rect.Right - rx - 1,    rect.Top  + ry - 1,     2, 2);
                            BuildPillar(rect.Left + rx - 1,     rect.Bottom - ry - 1,   2, 2);
                            BuildPillar(rect.Right - rx - 1,    rect.Bottom - ry - 1,   2, 2);
                        }
                        break;
                    case RoomLayoutVariant.CenterWall:
                        // 중앙 상하에 가로 장벽 2개 (중앙 통로 사이에 갭)
                        {
                            int wallOffset = System.Math.Max(2, rect.Height / 5);
                            int cy = rect.Top + rect.Height / 2;
                            int gx  = rect.Left + rect.Width / 2 - 1;
                            BuildHorizontalCover(rect.Left + 1, rect.Right - 2, cy - wallOffset, gx, 2);
                            BuildHorizontalCover(rect.Left + 1, rect.Right - 2, cy + wallOffset, gx, 2);
                        }
                        break;
                }

                EnsureRoomTraversal(room);
            }
        }

        /// <summary>
        /// 일반 방의 레이아웃 변형에 따른 엄폐물 구조물을 배치합니다.
        /// <para>
        /// 각 변형별 배치 규칙:
        /// <list type="bullet">
        ///   <item><see cref="RoomLayoutVariant.Open"/>: 좌상단과 우하단에 짧은 수평 엄폐물을 배치합니다.</item>
        ///   <item><see cref="RoomLayoutVariant.CenterPillar"/>: 좌측과 우측에 수직 엄폐물을 배치합니다.</item>
        ///   <item><see cref="RoomLayoutVariant.TwinPillars"/>: 중앙을 기준으로 좌상·우하에 수평 엄폐물을 배치합니다.</item>
        ///   <item><see cref="RoomLayoutVariant.CornerPillars"/>: 좌우에 세로 방향 중앙 엄폐물을 배치합니다.</item>
        ///   <item><see cref="RoomLayoutVariant.SplitLanes"/>: 중앙 X를 기준으로 좌상·우하에 수직 엄폐물을 배치합니다.</item>
        /// </list>
        /// 보스 방과 시작 방(Start)에는 적용되지 않습니다.
        /// </para>
        /// </summary>
        /// <param name="room">구조물을 배치할 일반 방입니다.</param>
        private void BuildGeneralRoomStructures(StageRoom room)
        {
            if (room == null || room.IsBossRoom || room.IsRestRoom || room.Name == "Start")
            {
                return;
            }

            Rectangle rect = room.Bounds;
            int coverLength = System.Math.Max(4, rect.Width / 4);
            int sideInset = 2;
            int topY = rect.Top + 3;
            int bottomY = rect.Bottom - 4;
            int leftX = rect.Left + 3;
            int rightX = rect.Right - 4;
            int centerX = rect.Left + (rect.Width / 2);
            int centerY = rect.Top + (rect.Height / 2);

            switch (room.LayoutVariant)
            {
                case RoomLayoutVariant.Open:
                    BuildHorizontalCover(rect.Left + sideInset, rect.Left + sideInset + coverLength, topY, rect.Left + sideInset + 1, 1);
                    BuildHorizontalCover(rect.Right - sideInset - coverLength - 1, rect.Right - sideInset - 1, bottomY, rect.Right - sideInset - 2, 1);
                    break;
                case RoomLayoutVariant.CenterPillar:
                    BuildVerticalCover(leftX, rect.Top + 2, rect.Top + 2 + coverLength, rect.Top + 4, 1);
                    BuildVerticalCover(rightX, rect.Bottom - 3 - coverLength, rect.Bottom - 3, rect.Bottom - 5, 1);
                    break;
                case RoomLayoutVariant.TwinPillars:
                    BuildHorizontalCover(rect.Left + 2, centerX - 3, topY, centerX - 5, 2);
                    BuildHorizontalCover(centerX + 3, rect.Right - 3, bottomY, centerX + 4, 2);
                    break;
                case RoomLayoutVariant.CornerPillars:
                    BuildVerticalCover(rect.Left + 4, centerY - coverLength / 2, centerY + coverLength / 2, centerY - 1, 2);
                    BuildVerticalCover(rect.Right - 5, centerY - coverLength / 2, centerY + coverLength / 2, centerY - 1, 2);
                    break;
                case RoomLayoutVariant.SplitLanes:
                    BuildVerticalCover(centerX - 4, rect.Top + 2, centerY - 3, rect.Top + 4, 1);
                    BuildVerticalCover(centerX + 4, centerY + 3, rect.Bottom - 3, rect.Bottom - 5, 1);
                    break;
                case RoomLayoutVariant.DiagonalPillars:
                    // 대각선 사이 짧은 수평 엄폐물
                    BuildHorizontalCover(leftX, centerX - 2, topY, leftX + 1, 1);
                    BuildHorizontalCover(centerX + 2, rightX, bottomY, rightX - 1, 1);
                    break;
                case RoomLayoutVariant.InnerRing:
                    // 링 기둥 사이 수직 엄폐물
                    BuildVerticalCover(leftX,  topY,  centerY - 2, topY + 1, 1);
                    BuildVerticalCover(rightX, centerY + 2, bottomY, bottomY - 1, 1);
                    break;
                case RoomLayoutVariant.CenterWall:
                    // 양 옆 세로 방향 소형 엄폐물
                    BuildVerticalCover(leftX,  topY, centerY - 2, topY + 1, 1);
                    BuildVerticalCover(rightX, centerY + 2, bottomY, bottomY - 1, 1);
                    break;
            }
        }

        /// <summary>
        /// 지정한 위치에 직사각형 기둥을 솔리드 타일(타입 1, 골조 텍스처)로 채워서 생성합니다.
        /// </summary>
        /// <param name="startX">기둥 좌측 상단의 X 타일 좌표입니다.</param>
        /// <param name="startY">기둥 좌측 상단의 Y 타일 좌표입니다.</param>
        /// <param name="width">기둥의 가로 타일 수입니다.</param>
        /// <param name="height">기둥의 세로 타일 수입니다.</param>
        private void BuildPillar(int startX, int startY, int width, int height)
        {
            for (int y = startY; y < startY + height; y++)
            {
                for (int x = startX; x < startX + width; x++)
                {
                    SetTile(x, y, 1, WorldConfig.WallTextureSupport);
                }
            }
        }

        /// <summary>
        /// 수평 방향으로 엄폐물 타일을 배치합니다.
        /// 지정된 간격(gap) 구간의 타일은 건너뛰어 통로를 남겨 둡니다.
        /// </summary>
        /// <param name="startX">엄폐물 시작 X 타일 좌표입니다(포함).</param>
        /// <param name="endX">엄폐물 끝 X 타일 좌표입니다(포함).</param>
        /// <param name="y">엄폐물이 놓일 Y 타일 좌표입니다.</param>
        /// <param name="gapStart">통로 간격의 시작 X 좌표입니다.</param>
        /// <param name="gapWidth">통로 간격의 가로 타일 수입니다.</param>
        private void BuildHorizontalCover(int startX, int endX, int y, int gapStart, int gapWidth)
        {
            for (int x = startX; x <= endX; x++)
            {
                if (x >= gapStart && x < gapStart + gapWidth)
                {
                    continue;
                }

                SetTile(x, y, 1, WorldConfig.WallTextureCover);
            }
        }

        /// <summary>
        /// 수직 방향으로 엄폐물 타일을 배치합니다.
        /// 지정된 간격(gap) 구간의 타일은 건너뛰어 통로를 남겨 둡니다.
        /// </summary>
        /// <param name="x">엄폐물이 놓일 X 타일 좌표입니다.</param>
        /// <param name="startY">엄폐물 시작 Y 타일 좌표입니다(포함).</param>
        /// <param name="endY">엄폐물 끝 Y 타일 좌표입니다(포함).</param>
        /// <param name="gapStart">통로 간격의 시작 Y 좌표입니다.</param>
        /// <param name="gapHeight">통로 간격의 세로 타일 수입니다.</param>
        private void BuildVerticalCover(int x, int startY, int endY, int gapStart, int gapHeight)
        {
            for (int y = startY; y <= endY; y++)
            {
                if (y >= gapStart && y < gapStart + gapHeight)
                {
                    continue;
                }

                SetTile(x, y, 1, WorldConfig.WallTextureCover);
            }
        }

        /// <summary>
        /// 방의 중심을 지나는 가로·세로 통로를 비워서 반드시 이동 가능하도록 보정합니다.
        /// <para>
        /// 모든 전투 방은 동일하게 1타일 너비의 중앙 통로를 확보합니다.
        /// 구조물 배치 이후 호출되어 플레이어와 적이 방 내를 가로지를 수 있도록 보장합니다.
        /// </para>
        /// </summary>
        /// <param name="room">통로를 보정할 방입니다.</param>
        private void EnsureRoomTraversal(StageRoom room)
        {
            if (room == null)
            {
                return;
            }

            Rectangle rect = room.Bounds;
            int centerX = rect.Left + (rect.Width / 2);
            int centerY = rect.Top + (rect.Height / 2);
            int halfLaneWidth = 0;

            ClearVerticalLane(rect, centerX, halfLaneWidth);
            ClearHorizontalLane(rect, centerY, halfLaneWidth);
        }

        /// <summary>
        /// 방의 수직 중앙 레인을 바닥 타일(타입 0)로 비웁니다.
        /// <para>
        /// <paramref name="centerX"/> ± <paramref name="halfLaneWidth"/> 범위의 X 좌표에 대해
        /// 방의 상단부터 하단까지 모든 Y 위치를 개방합니다.
        /// </para>
        /// </summary>
        /// <param name="rect">방의 경계 사각형입니다.</param>
        /// <param name="centerX">레인의 중심 X 타일 좌표입니다.</param>
        /// <param name="halfLaneWidth">레인 너비의 절반입니다. 0이면 1타일, 1이면 3타일 너비가 됩니다.</param>
        private void ClearVerticalLane(Rectangle rect, int centerX, int halfLaneWidth)
        {
            for (int x = centerX - halfLaneWidth; x <= centerX + halfLaneWidth; x++)
            {
                for (int y = rect.Top; y < rect.Bottom; y++)
                {
                    SetTile(x, y, 0, 0);
                }
            }
        }

        /// <summary>
        /// 방의 수평 중앙 레인을 바닥 타일(타입 0)로 비웁니다.
        /// <para>
        /// <paramref name="centerY"/> ± <paramref name="halfLaneWidth"/> 범위의 Y 좌표에 대해
        /// 방의 좌측부터 우측까지 모든 X 위치를 개방합니다.
        /// </para>
        /// </summary>
        /// <param name="rect">방의 경계 사각형입니다.</param>
        /// <param name="centerY">레인의 중심 Y 타일 좌표입니다.</param>
        /// <param name="halfLaneWidth">레인 너비의 절반입니다. 0이면 1타일, 1이면 3타일 너비가 됩니다.</param>
        private void ClearHorizontalLane(Rectangle rect, int centerY, int halfLaneWidth)
        {
            for (int y = centerY - halfLaneWidth; y <= centerY + halfLaneWidth; y++)
            {
                for (int x = rect.Left; x < rect.Right; x++)
                {
                    SetTile(x, y, 0, 0);
                }
            }
        }
    }
}
