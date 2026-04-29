using System.Collections.Generic;
using System.Drawing;

namespace My2DEngine.Game.Rendering
{
    /// <summary>
    /// 보스 스프라이트 시트에서 사용할 애니메이션 종류를 정의한다.
    /// </summary>
    public enum BossAnimationKind
    {
        /// <summary>대기(기본) 애니메이션.</summary>
        Idle,

        /// <summary>등장 애니메이션. Death 프레임을 역순으로 재생한다.</summary>
        Spawn,

        /// <summary>이동 중 애니메이션.</summary>
        Move,

        /// <summary>일반 공격 애니메이션.</summary>
        Attack,

        /// <summary>특수 공격 또는 스킬 애니메이션.</summary>
        Special,

        /// <summary>사망 애니메이션. 마지막 프레임에서 멈춘다.</summary>
        Death
    }

    /// <summary>
    /// 보스 스프라이트 시트에서 애니메이션 종류와 재생 시간을 기반으로
    /// 현재 표시할 프레임을 반환하는 헬퍼 클래스.
    /// 존재하지 않는 애니메이션 종류는 Idle로 폴백한다.
    /// </summary>
    public sealed class BossSpriteSheet
    {
        private const float DefaultFps = 5f;

        private readonly Dictionary<BossAnimationKind, Color[][]> animations;
        private readonly Dictionary<BossAnimationKind, float> fpsMap;

        public BossSpriteSheet(
            Dictionary<BossAnimationKind, Color[][]> animations,
            Dictionary<BossAnimationKind, float> fpsMap = null)
        {
            this.animations = animations ?? new Dictionary<BossAnimationKind, Color[][]>();
            this.fpsMap = fpsMap ?? new Dictionary<BossAnimationKind, float>();
        }

        private float GetFps(BossAnimationKind kind)
        {
            if (fpsMap.TryGetValue(kind, out float fps) && fps > 0f)
                return fps;
            return DefaultFps;
        }

        /// <summary>
        /// 지정된 애니메이션 종류와 재생 시간에 해당하는 스프라이트 프레임을 반환한다.
        /// 요청한 종류의 애니메이션이 없으면 Idle 애니메이션으로 폴백한다.
        /// Death 애니메이션은 마지막 프레임에서 멈추고, 나머지는 반복 재생된다.
        /// </summary>
        /// <param name="kind">재생할 애니메이션 종류.</param>
        /// <param name="animationTime">애니메이션 재생 시간(초). 0 미만이면 0으로 처리된다.</param>
        /// <returns>현재 시간에 해당하는 스프라이트 프레임 픽셀 배열.
        /// 재생 가능한 프레임이 없으면 null을 반환한다.</returns>
        public Color[] GetFrame(BossAnimationKind kind, float animationTime)
        {
            if (!animations.TryGetValue(kind, out Color[][] frames) || frames == null || frames.Length == 0)
            {
                if (!animations.TryGetValue(BossAnimationKind.Idle, out frames) || frames == null || frames.Length == 0)
                {
                    return null;
                }
            }

            int index = 0;
            if (frames.Length > 1)
            {
                if (animationTime < 0f)
                {
                    animationTime = 0f;
                }

                float fps = GetFps(kind);
                if (kind == BossAnimationKind.Death)
                {
                    index = (int)(animationTime * fps);
                    if (index >= frames.Length)
                    {
                        index = frames.Length - 1;
                    }
                }
                else if (kind == BossAnimationKind.Spawn)
                {
                    index = (int)(animationTime * fps);
                    if (index >= frames.Length)
                    {
                        index = frames.Length - 1;
                    }

                    index = (frames.Length - 1) - index;
                }
                else
                {
                    index = (int)(animationTime * fps) % frames.Length;
                }
            }

            return frames[index];
        }

        /// <summary>
        /// 지정한 애니메이션 종류의 전체 재생 시간을 반환한다.
        /// 프레임이 없으면 0을 반환한다.
        /// </summary>
        /// <param name="kind">조회할 애니메이션 종류.</param>
        /// <returns>전체 애니메이션 길이(초).</returns>
        public float GetAnimationDuration(BossAnimationKind kind)
        {
            if (!animations.TryGetValue(kind, out Color[][] frames) || frames == null || frames.Length == 0)
            {
                if (!animations.TryGetValue(BossAnimationKind.Idle, out frames) || frames == null || frames.Length == 0)
                {
                    return 0f;
                }
            }

            return frames.Length / GetFps(kind);
        }
    }
}
