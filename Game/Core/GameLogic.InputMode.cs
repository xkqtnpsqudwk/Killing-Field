namespace My2DEngine.Game.Core
{
    /// <summary>플레이 화면 위를 덮고 있는 오버레이. 여러 개가 켜져 있으면 위에 그려지는 것 하나만 대표로 고른다.</summary>
    public enum GameplayOverlay
    {
        None,
        /// <summary>P 키 플레이어 스탯 오버레이. 월드가 멈춘다.</summary>
        PlayerStats,
        /// <summary>영구 스탯 배분 UI. 월드가 멈춘다.</summary>
        PermanentStats,
        /// <summary>666층 엔딩 연출. 월드가 멈춘다.</summary>
        Ending,
        /// <summary>방 클리어 직후 카드 보상이 나타나기 전 연출.</summary>
        CardRewardReveal,
        /// <summary>카드 보상 선택. 숫자키·마우스가 선택에 쓰인다.</summary>
        CardReward,
        /// <summary>다음 방 분기 선택. 숫자키·마우스가 선택에 쓰인다.</summary>
        BranchSelection
    }

    /// <summary>
    /// GameLogic의 입력 모드 규칙 partial.
    /// 오버레이 플래그 조합으로 흩어져 있던 "지금 이 입력을 받아도 되는가"를 이름 붙은 규칙으로 모은다.
    /// 새 오버레이를 추가하면 <see cref="ActiveOverlay"/>와 아래 규칙만 고치면 된다.
    /// </summary>
    public partial class GameLogic
    {
        /// <summary>현재 대표 오버레이. 화면을 통째로 멈추는 것이 선택 UI보다 우선한다.</summary>
        public GameplayOverlay ActiveOverlay
        {
            get
            {
                if (playerStatsOverlayActive) return GameplayOverlay.PlayerStats;
                if (permanentStatsUiActive) return GameplayOverlay.PermanentStats;
                if (endingSequenceActive) return GameplayOverlay.Ending;
                if (cardRewardActive) return GameplayOverlay.CardReward;
                if (branchSelectionActive) return GameplayOverlay.BranchSelection;
                if (CardRewardRevealPending) return GameplayOverlay.CardRewardReveal;
                return GameplayOverlay.None;
            }
        }

        /// <summary>월드를 멈추는 전체 화면 오버레이(스탯·영구 스탯·엔딩)가 켜져 있다.</summary>
        private bool IsWorldPausedByOverlay =>
            playerStatsOverlayActive || permanentStatsUiActive || endingSequenceActive;

        /// <summary>카드·분기 선택 UI가 숫자키와 F키를 쓰고 있다.</summary>
        private bool IsSelectionUiActive => cardRewardActive || branchSelectionActive;

        /// <summary>카드 보상 연출부터 분기 선택까지, 방 클리어 후 보상 흐름이 진행 중이다.</summary>
        private bool IsRewardFlowActive => CardRewardRevealPending || IsSelectionUiActive;

        /// <summary>방 진행과 상호작용 안내를 멈춰야 한다(보상 흐름, 영구 스탯 UI, 엔딩).</summary>
        private bool IsStageFlowPaused => IsRewardFlowActive || permanentStatsUiActive || endingSequenceActive;

        /// <summary>E 키 상호작용을 막아야 한다(보상 흐름, 엔딩).</summary>
        private bool IsInteractBlocked => IsRewardFlowActive || endingSequenceActive;

        /// <summary>발사·무기 전환을 받을 수 있다. 승리·사망·전체 화면 오버레이 중에는 막는다.</summary>
        private bool CanUseCombatInput => !victory && !player.IsDead && !IsWorldPausedByOverlay;

        /// <summary>마우스 시야 회전을 받을 수 있다. 승리 후에는 둘러볼 수 있게 허용한다.</summary>
        private bool CanUseLookInput => !player.IsDead && !IsWorldPausedByOverlay;
    }
}
