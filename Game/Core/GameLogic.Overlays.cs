using My2DEngine.Engine.Rendering;
using My2DEngine.Game.Config;
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

        // 고르기 화면 카드의 움직임(떨어짐·떠오름·고른 카드 번쩍임). 그리기 전용이다.
        private readonly CardDeckMotion cardMotion = new CardDeckMotion();
        private readonly CardDeckMotion branchMotion = new CardDeckMotion();

        // 마우스 위치(게임 해상도 좌표). 오버레이가 열려 있을 때 Form1이 매 프레임 넘긴다.
        private float uiPointerX = -1f;
        private float uiPointerY = -1f;

        /// <summary>마우스 위치를 게임 해상도 좌표로 받는다. 카드에 마우스를 올린 반응에 쓴다.</summary>
        public void SetUiPointer(float gameX, float gameY)
        {
            uiPointerX = gameX;
            uiPointerY = gameY;
        }

        /// <summary>고르기 화면 카드의 움직임을 진행한다. 화면이 닫혀 있어도 고른 카드 번쩍임·불탐은 진행된다.</summary>
        private void UpdateOverlayMotion(float dt)
        {
            int cardHover = cardRewardActive ? CardRewardScreen.HitTest(BuildCardRewardView(), uiPointerX, uiPointerY) : -1;
            cardMotion.Update(dt, cardRewardActive, cardHover);

            bool branchOpen = branchSelectionActive && branchOptionA != null && branchOptionB != null;
            int branchHover = branchOpen ? BranchSelectionScreen.HitTest(uiPointerX, uiPointerY) : -1;
            if (branchMotion.Update(dt, branchOpen, branchHover) && cardMotion.BurnRemaining > 0f)
            {
                // 카드 보상에서 고르지 않은 카드가 타는 동안은 다음 룸 카드를 내려보내지 않는다.
                branchMotion.Hold(cardMotion.BurnRemaining * 0.85f);
            }
        }

        /// <summary>월드 위에 열린 오버레이 화면을 그린다.</summary>
        private void DrawOverlayScreens(Renderer r)
        {
            if (cardRewardActive)
            {
                CardRewardScreen.Draw(r, BuildCardRewardView());
            }

            if (branchSelectionActive && branchOptionA != null && branchOptionB != null)
            {
                BranchSelectionScreen.Draw(r, branchOptionA, branchOptionB, senseLevel, branchMotion);
            }

            // 고르지 않은 카드는 타서 사라지고, 고른 카드는 그 위에서 번쩍이며 사라진다. 화면이 닫힌 뒤에도 그린다.
            CardRewardScreen.DrawBurning(r, BuildCardRewardView(), drawDim: !branchSelectionActive && !cardRewardActive);
            BranchSelectionScreen.DrawBurning(r, branchMotion, senseLevel);
            cardMotion.DrawPick(r);
            branchMotion.DrawPick(r);

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
                    Motion = cardMotion,
                };
            }

            cardRewardView.SlotCount = GetCardRewardOfferSlotCount();
            cardRewardView.WasBossRoom = cardRewardWasBossRoom;
            cardRewardView.FamilyCounts = BuildFamilyCounts();
            cardRewardView.ShowButtons = CardRewardAllowsRerollAndSkip;
            cardRewardView.Coins = player.CoinCount;
            cardRewardView.RerollCost = GetCardRerollCost();
            cardRewardView.SkipReward = SynergyConfig.SkipCoinReward;
            cardRewardView.PointerX = uiPointerX;
            cardRewardView.PointerY = uiPointerY;
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
            playerStatsView.Perks = GetOwnedPerks();
            playerStatsView.FamilyCounts = BuildFamilyCounts();
            playerStatsView.SlaughterStacks = modifiers.SlaughterStacks;
            return playerStatsView;
        }
    }
}
