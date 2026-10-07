using System;

namespace My2DEngine.Game.Core
{
    /// <summary>
    /// 화면 위쪽에 잠깐 띄우는 상태 메시지("라운드 시작", "[E] 다음 층으로" 등).
    /// 새 메시지는 이전 메시지를 덮어쓰고, 끝나기 직전 구간에서 흐려진다.
    /// </summary>
    internal sealed class StageStatusMessage
    {
        /// <summary>사라지기 전 마지막 구간에서 흐려지는 시간(초).</summary>
        private const float FadeOutSeconds = 0.7f;

        /// <summary>지금 메시지. 없으면 null.</summary>
        public string Text { get; private set; }

        /// <summary>남은 표시 시간(초).</summary>
        public float TimeLeft { get; private set; }

        /// <summary>표시 강도 0~1. 남은 시간이 흐려지는 구간보다 길면 1.</summary>
        public float Alpha => TimeLeft <= 0f ? 0f : Math.Min(1f, TimeLeft / FadeOutSeconds);

        public void Show(string text, float duration)
        {
            Text = text;
            TimeLeft = duration;
        }

        /// <summary>
        /// 같은 메시지가 이미 떠 있으면 다시 띄우지 않는다(사라지기 직전이면 다시 띄운다).
        /// 매 프레임 같은 경고를 보낼 때 깜박이지 않게 한다.
        /// </summary>
        public void ShowIfNew(string text, float duration, float refreshBelow)
        {
            if (!string.IsNullOrWhiteSpace(text) &&
                (!string.Equals(Text, text, StringComparison.Ordinal) || TimeLeft <= refreshBelow))
            {
                Show(text, duration);
            }
        }

        public void Update(float dt)
        {
            if (TimeLeft <= 0f)
            {
                return;
            }

            TimeLeft -= dt;
            if (TimeLeft <= 0f)
            {
                Clear();
            }
        }

        public void Clear()
        {
            Text = null;
            TimeLeft = 0f;
        }
    }
}
