using My2DEngine.Game.Audio;
using My2DEngine.Game.Map;

namespace My2DEngine.Game.Core
{
    /// <summary>
    /// GameLogic의 맥락(Context) 파악 partial.
    /// 플레이어가 현재 어느 스테이지 방에 있는지 판단하고,
    /// 그에 맞는 배경 음악 카테고리를 결정하여 BGM 시스템에 전달한다.
    /// </summary>
    public partial class GameLogic
    {
        /// <summary>
        /// 매 프레임 BGM 상태를 갱신한다.
        /// 승리 상태이면 BGM을 즉시 정지하고,
        /// 그렇지 않으면 현재 방 상태에 따라 적절한 카테고리로 BGM을 전환한다.
        /// </summary>
        /// <param name="dt">이번 프레임의 경과 시간(초). BGM 크로스페이드 계산에 사용된다.</param>
        private void UpdateBackgroundMusicState(float dt)
        {
            if (victory || endingSequenceActive)
            {
                audio.SilenceMusic();
                return;
            }

            audio.UpdateMusic(GetBackgroundMusicCategory(), dt);
        }

        /// <summary>
        /// 플레이어가 위치한 방의 상태를 기반으로 재생해야 할 BGM 카테고리를 결정한다.
        /// <list type="bullet">
        ///   <item>클리어되지 않은 보스 방: <see cref="BackgroundMusicCategory.Boss"/></item>
        ///   <item>클리어되지 않은 미니 보스 방: <see cref="BackgroundMusicCategory.MiniBoss"/></item>
        ///   <item>그 외(복도·일반 방·클리어된 방): <see cref="BackgroundMusicCategory.Normal"/></item>
        /// </list>
        /// </summary>
        /// <returns>현재 상황에 맞는 BGM 카테고리.</returns>
        private BackgroundMusicCategory GetBackgroundMusicCategory()
        {
            StageRoom room = roomLookup.FindCurrent(player.Position.X, player.Position.Y);
            if (room != null && room.State.Activated)
            {
                if (room.IsBossRoom)
                {
                    return BackgroundMusicCategory.Boss;
                }

                if (room.IsMiniBossRoom)
                {
                    return BackgroundMusicCategory.MiniBoss;
                }
            }

            return BackgroundMusicCategory.Normal;
        }
    }
}
