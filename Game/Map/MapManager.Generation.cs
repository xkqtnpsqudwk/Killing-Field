using System.Drawing;

namespace My2DEngine.Game.Map
{
    /// <summary>
    /// 로그라이크 룸 템플릿 생성에 공통으로 사용하는 맵 표면 초기화 헬퍼들이다.
    /// </summary>
    public partial class MapManager
    {
        /// <summary>로그라이크 룸을 배치할 런타임 맵의 전체 크기(타일)다.</summary>
        private const int GeneratedMapSize = 201;

        /// <summary>로그라이크 룸을 배치할 맵 중심 타일 좌표다.</summary>
        private const int MapCenter = GeneratedMapSize / 2;

        /// <summary>
        /// 지정된 크기의 맵 배열 전체를 솔리드 벽 타일(타입 1)로 채운다.
        /// 이후 필요한 셀만 바닥(0)이나 문 타일로 덮어쓴다.
        /// </summary>
        private void FillSolidMap(int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    map[x, y] = 1;
                    textureIds[x, y] = 1;
                }
            }
        }

        /// <summary>
        /// 주어진 사각형 영역의 내부 타일을 바닥(타입 0)으로 파낸다.
        /// 외곽 1타일 테두리는 벽으로 남겨 둔다.
        /// </summary>
        private void CarveRoom(Rectangle rect)
        {
            for (int y = rect.Top + 1; y < rect.Bottom - 1; y++)
            {
                for (int x = rect.Left + 1; x < rect.Right - 1; x++)
                {
                    SetTile(x, y, 0, 0);
                }
            }
        }
    }
}
