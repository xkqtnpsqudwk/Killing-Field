using System.Diagnostics;

namespace My2DEngine.Engine.Core
{
    /// <summary>
    /// 프레임 기반 시간값을 한곳에서 계산해 게임 전체가 같은 DeltaTime을 공유하게 한다.
    /// 매 프레임 <see cref="Update"/>를 호출하면 DeltaTime이 갱신된다.
    /// Stopwatch를 사용하므로 OS 타이머보다 훨씬 높은 정밀도(나노초 수준)를 제공한다.
    /// </summary>
    public static class Time
    {
        /// <summary>
        /// 직전 Update 호출 시점의 Stopwatch 틱 값.
        /// 첫 호출을 감지하기 위해 0을 초기값으로 사용한다.
        /// </summary>
        private static long lastTicks;

        /// <summary>
        /// 이번 프레임과 직전 프레임 사이의 경과 시간(초 단위).
        /// 이동, 물리, 애니메이션 등 거의 모든 프레임 갱신에서 이 값을 곱해 속도를 결정한다.
        /// </summary>
        public static float DeltaTime { get; private set; }

        /// <summary>
        /// 매 프레임 한 번 호출해 DeltaTime을 갱신한다.
        /// 첫 호출 시에는 기준 틱만 저장하고 DeltaTime을 0으로 두어
        /// 초기 프레임에 비정상적으로 큰 dt가 입력되는 현상을 방지한다.
        /// </summary>
        public static void Update()
        {
            long now = Stopwatch.GetTimestamp();

            if (lastTicks == 0)
            {
                lastTicks = now;
                DeltaTime = 0f;
                return;
            }

            long diff = now - lastTicks;
            lastTicks = now;

            float dt = (float)diff / Stopwatch.Frequency;

            if (dt < 0f)
            {
                dt = 0f;
            }

            DeltaTime = dt;
        }
    }
}
