using System;
using My2DEngine.Game.Config;

namespace My2DEngine.Game.Rendering
{
    /// <summary>
    /// GPU 월드 렌더링에서 공유하는 프레임 상태와 거리/색상 유틸리티를 담당하는 partial 클래스.
    /// CPU 프레임버퍼 합성 경로는 제거되었으며, 현재는 화면 크기와 zBuffer만 유지한다.
    /// </summary>
    public partial class RaycastRenderer
    {
        /// <summary>
        /// 벽까지의 수직 투영 거리를 렌더링 거리로 변환한다.
        /// NearPlane보다 가까운 거리에서는 소프트 클리핑을 적용해 벽이 갑자기 잘리지 않도록 한다.
        /// </summary>
        /// <param name="wallDistance">레이캐스트로 계산한 벽까지의 수직 투영 거리.</param>
        /// <returns>소프트 클리핑이 적용된 렌더링 거리. 최솟값은 0.0001이다.</returns>
        private double GetRenderDistance(double wallDistance)
        {
            if (wallDistance <= 0.0001)
            {
                return 0.0001;
            }

            if (wallDistance >= RenderConfig.NearPlane)
            {
                return wallDistance;
            }

            double delta = RenderConfig.NearPlane - wallDistance;
            double blend = Math.Sqrt(delta * delta + RenderConfig.NearPlaneSoftness * RenderConfig.NearPlaneSoftness);
            return RenderConfig.NearPlane - 0.5 * (delta + RenderConfig.NearPlaneSoftness - blend);
        }

        /// <summary>
        /// 거리에 따른 안개 감쇠 계수를 계산한다.
        /// 반환값이 작을수록 더 어둡게 렌더링된다. 최솟값은 0.2이다.
        /// </summary>
        /// <param name="distance">물체까지의 거리.</param>
        /// <returns>0.2~1.0 범위의 안개 계수.</returns>
        private float Fog(float distance)
        {
            float factor = 1f / (1f + distance * 0.15f);
            if (factor < 0.2f)
            {
                factor = 0.2f;
            }

            return factor;
        }

        /// <summary>
        /// ARGB 정수 색상에 밝기 계수를 곱해 어둡게 만든다. 알파는 255로 고정된다.
        /// </summary>
        /// <param name="color">원본 ARGB 정수 색상.</param>
        /// <param name="factor">밝기 배율(0.0~1.0).</param>
        /// <returns>밝기가 조정된 ARGB 정수 색상.</returns>
        private int DarkenFast(int color, float factor)
        {
            int rr = (int)(((color >> 16) & 255) * factor);
            int gg = (int)(((color >> 8) & 255) * factor);
            int bb = (int)((color & 255) * factor);
            return (255 << 24) | (rr << 16) | (gg << 8) | bb;
        }

        /// <summary>
        /// 요청된 해상도에 맞게 프레임 상태를 초기화한다.
        /// GPU 경로에서는 화면 크기와 zBuffer만 관리한다.
        /// </summary>
        /// <param name="width">새 프레임 너비(픽셀).</param>
        /// <param name="height">새 프레임 높이(픽셀).</param>
        private void EnsureFrame(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return;
            }

            if (width == frameW && height == frameH && zBuffer != null)
            {
                return;
            }

            frameW = width;
            frameH = height;
            zBuffer = new double[frameW];
        }
    }
}
