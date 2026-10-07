using System;
using My2DEngine.Game.Config;

namespace My2DEngine.Rendering.WorldData
{
    /// <summary>
    /// 레이캐스트 화면 투영 계산. 카메라 기준 상대 좌표를 깊이·화면 x·높이로 바꾸고,
    /// 픽셀 떨림을 줄이도록 정수 픽셀에 맞춘다. 상태가 없는 순수 계산이다.
    /// </summary>
    internal static class WorldProjection
    {
        /// <summary>
        /// 오브젝트가 화면에 투영될 가능성이 있는지 빠르게 판별한다.
        /// 카메라 뒤에 있거나 너무 작거나 화면 밖으로 완전히 벗어난 경우 false를 반환한다.
        /// </summary>
        /// <param name="dx">오브젝트 - 플레이어의 월드 X 차이.</param>
        /// <param name="dy">오브젝트 - 플레이어의 월드 Y 차이.</param>
        /// <param name="scale">오브젝트의 월드 공간 크기(스케일).</param>
        /// <param name="renderWidth">렌더 대상 너비(픽셀).</param>
        /// <param name="renderHeight">렌더 대상 높이(픽셀).</param>
        /// <param name="dirX">카메라 방향 벡터 X 성분.</param>
        /// <param name="dirY">카메라 방향 벡터 Y 성분.</param>
        /// <param name="planeX">투영 평면 벡터 X 성분.</param>
        /// <param name="planeY">투영 평면 벡터 Y 성분.</param>
        /// <param name="invDet">투영 행렬 역행렬의 행렬식 역수.</param>
        /// <returns>화면에 일부라도 투영될 가능성이 있으면 true, 확실히 보이지 않으면 false.</returns>
        public static bool IsPotentiallyVisible(float dx, float dy, float scale, int renderWidth, int renderHeight,
            float dirX, float dirY, float planeX, float planeY, double invDet)
        {
            double transformX = invDet * ((dirY * dx) - (dirX * dy));
            double transformY = invDet * ((-planeY * dx) + (planeX * dy));
            if (transformY <= 0.05)
            {
                return false;
            }

            double spriteScreenX = (renderWidth * 0.5) * (1.0 + (transformX / transformY));
            double spriteHeight = Math.Abs((renderHeight / transformY) * scale);
            if (spriteHeight <= 4.0)
            {
                return false;
            }

            double spriteWidth = spriteHeight;
            double drawStartX = spriteScreenX - (spriteWidth * 0.5);
            double drawEndX = spriteScreenX + (spriteWidth * 0.5);
            return drawEndX >= -16.0 && drawStartX <= renderWidth + 16.0;
        }

        /// <summary>
        /// 오브젝트의 월드 상대 좌표를 카메라 좌표계의 깊이(transformY)로 변환한다.
        /// 반환값이 0보다 크면 카메라 앞에 있는 것이다.
        /// </summary>
        /// <param name="dx">오브젝트 - 플레이어의 월드 X 차이.</param>
        /// <param name="dy">오브젝트 - 플레이어의 월드 Y 차이.</param>
        /// <param name="planeX">투영 평면 벡터 X 성분.</param>
        /// <param name="planeY">투영 평면 벡터 Y 성분.</param>
        /// <param name="invDet">투영 행렬 역행렬의 행렬식 역수.</param>
        /// <returns>카메라 좌표계에서 오브젝트까지의 투영 깊이(양수 = 카메라 앞).</returns>
        public static float GetProjectedDepth(float dx, float dy, float planeX, float planeY, double invDet)
        {
            return (float)(invDet * ((-planeY * dx) + (planeX * dy)));
        }

        /// <summary>
        /// 오브젝트의 월드 상대 좌표를 화면 X 좌표로 변환한다.
        /// </summary>
        /// <param name="dx">오브젝트 - 플레이어의 월드 X 차이.</param>
        /// <param name="dy">오브젝트 - 플레이어의 월드 Y 차이.</param>
        /// <param name="targetWidth">렌더 대상 너비(픽셀).</param>
        /// <param name="dirX">카메라 방향 벡터 X 성분.</param>
        /// <param name="dirY">카메라 방향 벡터 Y 성분.</param>
        /// <param name="planeX">투영 평면 벡터 X 성분.</param>
        /// <param name="planeY">투영 평면 벡터 Y 성분.</param>
        /// <param name="invDet">투영 행렬 역행렬의 행렬식 역수.</param>
        /// <returns>화면 X 좌표(픽셀). 화면 중앙이 targetWidth * 0.5에 해당한다.</returns>
        public static float GetProjectedScreenX(float dx, float dy, int targetWidth, float dirX, float dirY, float planeX, float planeY, double invDet)
        {
            double transformX = invDet * ((dirY * dx) - (dirX * dy));
            double transformY = invDet * ((-planeY * dx) + (planeX * dy));
            return (float)((targetWidth * 0.5) * (1.0 + (transformX / transformY)));
        }

        /// <summary>
        /// 오브젝트의 투영 깊이와 스케일을 기반으로 화면에 그릴 높이(픽셀)를 계산한다.
        /// </summary>
        /// <param name="targetHeight">렌더 대상 높이(픽셀).</param>
        /// <param name="depth">오브젝트의 투영 깊이.</param>
        /// <param name="scale">오브젝트의 월드 공간 크기(스케일).</param>
        /// <returns>화면에 그릴 스프라이트 높이(픽셀). 항상 양수.</returns>
        public static float GetProjectedHeight(int targetHeight, float depth, float scale)
        {
            return Math.Abs((targetHeight / Math.Max(0.0001f, depth)) * scale);
        }

        /// <summary>
        /// 오브젝트의 투영 깊이와 스케일을 기반으로 화면 Y 중심 좌표를 계산한다.
        /// 현재는 화면 수직 중앙(targetHeight * 0.5)을 기준으로 한다.
        /// </summary>
        /// <param name="targetHeight">렌더 대상 높이(픽셀).</param>
        /// <param name="depth">오브젝트의 투영 깊이.</param>
        /// <param name="scale">오브젝트의 월드 공간 크기(스케일).</param>
        /// <returns>스프라이트의 화면 Y 중심 좌표(픽셀).</returns>
        public static float GetProjectedCenterY(int targetHeight, float depth, float scale)
        {
            float spriteHeight = GetProjectedHeight(targetHeight, depth, scale);
            return (targetHeight * 0.5f) + (spriteHeight * 0f);
        }

        /// <summary>
        /// 픽업 오브젝트의 화면 Y 중심 좌표를 계산한다.
        /// 바닥 근처에 위치하며 PulseTimer에 따라 사인 곡선으로 부유(bob) 효과를 적용한다.
        /// </summary>
        /// <param name="targetHeight">렌더 대상 높이(픽셀).</param>
        /// <param name="depth">픽업의 투영 깊이.</param>
        /// <param name="pulseTimer">부유 애니메이션 타이머(초).</param>
        /// <param name="scale">픽업의 월드 공간 크기(스케일).</param>
        /// <returns>픽업 스프라이트의 화면 Y 중심 좌표(픽셀).</returns>
        public static float GetProjectedPickupCenterY(int targetHeight, float depth, float pulseTimer, float scale)
        {
            float bob = (float)Math.Sin(pulseTimer * 3.6f) * 0.08f;
            float spriteHeight = GetProjectedHeight(targetHeight, depth, scale);
            float groundY = targetHeight / 2f + ((targetHeight * 0.34f) / Math.Max(0.0001f, depth)) - (bob * targetHeight * 0.2f);
            return groundY - (spriteHeight * 0.5f);
        }

        /// <summary>
        /// 스프라이트 화면 X 중심 좌표를 픽셀 경계에 정렬한다.
        /// CPU RenderEnemies와의 좌표 일치를 위해 floor + 0.5 방식을 사용한다.
        /// </summary>
        /// <param name="value">정렬할 화면 X 좌표(픽셀, float).</param>
        /// <returns>픽셀 경계에 정렬된 X 좌표.</returns>
        public static float SnapSpriteCenterX(float value)
        {
            return (float)Math.Floor(value) + 0.5f;
        }

        /// <summary>
        /// 스프라이트 화면 Y 중심 좌표를 반올림하여 CPU RenderEnemies의 정수 나눗셈 결과와 일치시킨다.
        /// Math.Floor + 0.5 방식은 스케일다운 시 1픽셀 오프셋을 발생시키므로 Math.Round를 사용한다.
        /// </summary>
        /// <param name="value">정렬할 화면 Y 좌표(픽셀, float).</param>
        /// <returns>반올림된 Y 좌표(float).</returns>
        public static float SnapSpriteCenterY(float value)
        {
            // CPU RenderEnemies: drawStartY = -spriteHeight/2 + frameH/2 → 스프라이트 중심 = frameH/2 (정수 나눗셈)
            // Math.Round는 정수 반올림으로 CPU의 정수 나눗셈 결과와 일치한다.
            // 이전의 Math.Floor(value)+0.5f는 스케일다운 시 1픽셀 오프셋을 발생시켰다.
            return (float)Math.Round(value);
        }

        /// <summary>
        /// 스프라이트 투영 높이를 반올림하여 CPU RenderEnemies의 정수 결과와 일치시킨다.
        /// 최솟값은 1픽셀이다.
        /// </summary>
        /// <param name="value">반올림할 투영 높이(픽셀, float).</param>
        /// <returns>반올림된 투영 높이. 최솟값 1.</returns>
        public static float SnapSpriteScale(float value)
        {
            return Math.Max(1f, (float)Math.Round(value));
        }

        /// <summary>
        /// 벽까지의 수직 투영 거리를 렌더링 거리로 변환한다.
        /// NearPlane보다 가까운 거리에는 소프트 클리핑을 적용한다.
        /// </summary>
        /// <param name="wallDistance">레이캐스트로 계산한 벽까지의 수직 투영 거리.</param>
        /// <returns>소프트 클리핑이 적용된 렌더링 거리. 최솟값은 0.0001.</returns>
        public static double GetRenderDistance(double wallDistance)
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
        /// 렌더 해상도가 GPU 월드 최대 해상도를 초과하는 경우 비율을 유지하면서 스케일다운한다.
        /// 홀수 해상도는 짝수로 내림한다.
        /// </summary>
        /// <param name="renderWidth">입력 렌더 너비(픽셀).</param>
        /// <param name="renderHeight">입력 렌더 높이(픽셀).</param>
        /// <param name="targetWidth">GPU 월드 렌더 대상 너비(픽셀). 최솟값 2.</param>
        /// <param name="targetHeight">GPU 월드 렌더 대상 높이(픽셀). 최솟값 2.</param>
        public static void GetGpuWorldTargetSize(int renderWidth, int renderHeight, out int targetWidth, out int targetHeight)
        {
            if (renderWidth <= 0 || renderHeight <= 0)
            {
                targetWidth = 2;
                targetHeight = 2;
                return;
            }

            double widthScale = renderWidth > RenderConfig.GpuWorldMaxRenderWidth
                ? RenderConfig.GpuWorldMaxRenderWidth / (double)renderWidth
                : 1.0;
            double heightScale = renderHeight > RenderConfig.GpuWorldMaxRenderHeight
                ? RenderConfig.GpuWorldMaxRenderHeight / (double)renderHeight
                : 1.0;
            double scale = Math.Min(1.0, Math.Min(widthScale, heightScale));

            targetWidth = Math.Max(2, (int)Math.Round(renderWidth * scale));
            targetHeight = Math.Max(2, (int)Math.Round(renderHeight * scale));
            if ((targetWidth & 1) != 0)
            {
                targetWidth--;
            }

            if ((targetHeight & 1) != 0)
            {
                targetHeight--;
            }

            if (targetWidth <= 0) targetWidth = 2;
            if (targetHeight <= 0) targetHeight = 2;
        }
    }
}
