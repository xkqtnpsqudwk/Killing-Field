namespace My2DEngine.Game.Core
{
    /// <summary>
    /// 오버레이 화면 상태. 예전에는 화면마다 bool 플래그가 따로 있어 둘이 함께 켜질 수 있었다.
    /// 이제 <see cref="activeOverlay"/> 하나(<see cref="GameplayOverlay"/>)만 있고, 열고 닫기는
    /// OpenOverlay/CloseOverlay로만 한다. 다른 화면이 열려 있을 때 새 화면을 열면 앞 화면은 닫힌다
    /// (화면 쪽 입력 처리는 이미 서로 막는다). CardRewardReveal은 상태가 아니라 카드가 뜨기 전 대기 시간에서 나온다.
    /// </summary>
    public partial class GameLogic
    {
        private GameplayOverlay activeOverlay = GameplayOverlay.None;

        private bool CardRewardOpen => activeOverlay == GameplayOverlay.CardReward;

        private bool BranchSelectionOpen => activeOverlay == GameplayOverlay.BranchSelection;

        private bool PlayerStatsOpen => activeOverlay == GameplayOverlay.PlayerStats;

        private bool PermanentStatsOpen => activeOverlay == GameplayOverlay.PermanentStats;

        private bool EndingOpen => activeOverlay == GameplayOverlay.Ending;

        private void OpenOverlay(GameplayOverlay screen)
        {
            activeOverlay = screen;
        }

        /// <summary>그 화면이 열려 있을 때만 닫는다(다른 화면을 실수로 닫지 않는다).</summary>
        private void CloseOverlay(GameplayOverlay screen)
        {
            if (activeOverlay == screen)
            {
                activeOverlay = GameplayOverlay.None;
            }
        }

        private void ToggleOverlay(GameplayOverlay screen)
        {
            if (activeOverlay == screen)
            {
                activeOverlay = GameplayOverlay.None;
            }
            else
            {
                activeOverlay = screen;
            }
        }
    }
}
