using System;
using System.Windows.Forms;
using My2DEngine.Engine.Input;

namespace My2DEngine.Game.Core
{
    /// <summary>
    /// GameLogic의 플레이어 스탯 오버레이 partial.
    /// 현재 플레이어 상태와 카드 누적 효과를 P 키로 확인할 수 있게 한다.
    /// </summary>
    public partial class GameLogic
    {
        private bool playerStatsToggleHeld;

        private void HandlePlayerStatsOverlayInput()
        {
            bool toggleHeld = Input.GetKey(Keys.P);
            if (EndingOpen)
            {
                playerStatsToggleHeld = toggleHeld;
                return;
            }

            if (toggleHeld &&
                !playerStatsToggleHeld &&
                !CardRewardRevealPending &&
                !CardRewardOpen &&
                !BranchSelectionOpen &&
                !PermanentStatsOpen)
            {
                ToggleOverlay(GameplayOverlay.PlayerStats);
                if (PlayerStatsOpen)
                {
                    OnPlayerStatsOverlayOpened();
                }
            }

            playerStatsToggleHeld = toggleHeld;
        }

        private void OnPlayerStatsOverlayOpened()
        {
            weapon.PendingShot = false;
            weapon.FireButtonHeld = false;
            StopLoopingWeaponEffects();
            interactPromptText = null;
        }

        private void ResetPlayerStatsOverlayState()
        {
            CloseOverlay(GameplayOverlay.PlayerStats);
            playerStatsToggleHeld = false;
        }
    }
}
