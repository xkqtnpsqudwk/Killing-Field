using My2DEngine.Engine.Rendering;
using My2DEngine.Game.Rendering.Ui.Screens;

namespace My2DEngine.Game.Core
{
    /// <summary>
    /// GameLogic과 오버레이 화면(Game/Rendering/Ui/Screens)을 잇는 partial.
    /// 화면은 그리기와 클릭 판정만 맡고, GameLogic은 여기서 상태를 보기 객체로 넘긴다.
    /// </summary>
    public partial class GameLogic
    {
        /// <summary>조작 안내가 사라지기 시작하는 남은 시간(초).</summary>
        private const float ControlsTutorialFadeTime = 1.0f;

        // 매 프레임 새로 만들지 않도록 보기 객체를 재사용한다. 배열은 GameLogic 상태를 그대로 가리킨다.
        private CardRewardView cardRewardView;
        private PlayerStatsView playerStatsView;

        /// <summary>월드 위에 열린 오버레이 화면을 그린다.</summary>
        private void DrawOverlayScreens(Renderer r)
        {
            if (cardRewardActive)
            {
                CardRewardScreen.Draw(r, BuildCardRewardView());
            }

            if (branchSelectionActive && branchOptionA != null && branchOptionB != null)
            {
                BranchSelectionScreen.Draw(r, branchOptionA, branchOptionB, senseLevel);
            }

            DrawPermanentStatsScreen(r);

            if (playerStatsOverlayActive)
            {
                PlayerStatsScreen.Draw(r, BuildPlayerStatsView());
            }

            if (endingSequenceActive)
            {
                EndingScreen.Draw(r, 1f - (endingSequenceTimer / EndingSequenceDuration), endingUnlockedNow);
            }

            if (controlsTutorialTimer > 0f && player != null && !player.IsDead)
            {
                float alpha = controlsTutorialTimer > ControlsTutorialFadeTime
                    ? 1f
                    : controlsTutorialTimer / ControlsTutorialFadeTime;
                ControlsTutorialScreen.Draw(r, alpha);
            }
        }

        /// <summary>영구 스탯 화면. 메인 메뉴에서도 쓴다.</summary>
        private void DrawPermanentStatsScreen(Renderer r)
        {
            if (permanentStatsUiActive)
            {
                PermanentStatsScreen.Draw(r, permanentProgression ?? PermanentProgressionData.CreateDefault());
            }
        }

        private CardRewardView BuildCardRewardView()
        {
            if (cardRewardView == null)
            {
                cardRewardView = new CardRewardView
                {
                    Offers = currentCardOffers,
                    StatGrades = runStatGrade,
                    StatCounts = runStatPickupCount,
                    StatTotals = runStatBonusTotals,
                    OwnedWeapons = ownedWeapons,
                };
            }

            cardRewardView.SlotCount = GetCardRewardOfferSlotCount();
            cardRewardView.WasBossRoom = cardRewardWasBossRoom;
            return cardRewardView;
        }

        private PlayerStatsView BuildPlayerStatsView()
        {
            if (playerStatsView == null)
            {
                playerStatsView = new PlayerStatsView
                {
                    CardTotals = runStatBonusTotals,
                    CardCounts = runStatPickupCount,
                };
            }

            // 예전 그리기 코드와 같게 저장된 영구 스탯 값을 화면을 열 때마다 정리한다.
            PermanentProgressionData data = permanentProgression ?? PermanentProgressionData.CreateDefault();
            data.Sanitize();

            playerStatsView.Player = player;
            playerStatsView.Weapon = weapon;
            playerStatsView.Permanent = data;
            playerStatsView.LifeStealNow = modifiers.LifeStealRatio(IsActiveToxicMistRoom());
            playerStatsView.LifeStealToxic = modifiers.LifeStealRatio(true);
            playerStatsView.ShieldRegenRate = GetEffectiveShieldRegenRate();
            playerStatsView.ShieldRegenDelay = GetEffectiveShieldRegenDelayDuration();
            return playerStatsView;
        }
    }
}
