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
        private bool playerStatsOverlayActive;
        private bool playerStatsToggleHeld;

        private void HandlePlayerStatsOverlayInput()
        {
            bool toggleHeld = Input.GetKey(Keys.P);
            if (endingSequenceActive)
            {
                playerStatsToggleHeld = toggleHeld;
                return;
            }

            if (toggleHeld &&
                !playerStatsToggleHeld &&
                !CardRewardRevealPending &&
                !cardRewardActive &&
                !branchSelectionActive &&
                !permanentStatsUiActive)
            {
                playerStatsOverlayActive = !playerStatsOverlayActive;
                if (playerStatsOverlayActive)
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
            playerStatsOverlayActive = false;
            playerStatsToggleHeld = false;
        }
    }
}
