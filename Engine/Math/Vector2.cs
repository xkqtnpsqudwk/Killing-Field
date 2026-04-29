namespace My2DEngine.Engine.Math
{
    /// <summary>
    /// 엔진과 게임 전반에서 공용으로 쓰는 최소 2D 벡터 타입이다.
    /// 위치, 방향, 속도처럼 X·Y 두 축으로 표현하는 모든 값에 사용된다.
    /// 구조체(struct)이므로 복사 비용이 낮고 힙 할당이 없다.
    /// </summary>
    public struct Vector2
    {
        /// <summary>벡터의 수평 성분(오른쪽이 양의 방향).</summary>
        public float X;

        /// <summary>벡터의 수직 성분(아래쪽이 양의 방향, 화면 좌표계 기준).</summary>
        public float Y;

        /// <summary>
        /// 주어진 X·Y 값으로 벡터를 생성한다.
        /// </summary>
        /// <param name="x">수평 성분</param>
        /// <param name="y">수직 성분</param>
        public Vector2(float x, float y)
        {
            X = x;
            Y = y;
        }
    }
}
