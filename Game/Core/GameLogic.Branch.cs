using System;
using System.Windows.Forms;
using My2DEngine.Engine.Input;
using My2DEngine.Game.Rendering.Ui.Screens;
using My2DEngine.Game.Map;

namespace My2DEngine.Game.Core
{
    /// <summary>
    /// GameLogic의 로그라이크 분기 선택 UI partial.
    /// 룸 클리어 후 두 갈래 다음 방 선택 카드를 표시하고,
    /// 플레이어의 키 입력(1/2)에 따라 선택된 템플릿으로 다음 층을 로드한다.
    /// </summary>
    public partial class GameLogic
    {
        /// <summary>분기 선택 UI가 현재 활성화되어 있는지 여부.</summary>
        private bool branchSelectionActive;

        /// <summary>분기 선택지 A (왼쪽 카드, 1번 키)로 제시될 룸 템플릿.</summary>
        private RoomTemplate branchOptionA;

        /// <summary>분기 선택지 B (오른쪽 카드, 2번 키)로 제시될 룸 템플릿.</summary>
        private RoomTemplate branchOptionB;

        /// <summary>
        /// 직전 방 생성 기회에서 카드 상점이 실제 방 또는 분기 후보로 노출됐는지 여부.
        /// true이면 다음 생성 기회에서는 상점을 제외하여 연속 노출을 막는다.
        /// </summary>
        private bool restRoomOpportunityCooldownActive;

        /// <summary>이전 프레임에 1번 키가 눌려 있었는지 여부. 엣지 트리거 처리에 사용.</summary>
        private bool branch1KeyHeld;

        /// <summary>이전 프레임에 2번 키가 눌려 있었는지 여부. 엣지 트리거 처리에 사용.</summary>
        private bool branch2KeyHeld;

        /// <summary>마우스 클릭으로 분기 카드를 선택할 때 처리할 게임 좌표 X (-1이면 미처리).</summary>
        private float pendingBranchClickX = -1f;

        /// <summary>마우스 클릭으로 분기 카드를 선택할 때 처리할 게임 좌표 Y.</summary>
        private float pendingBranchClickY = -1f;

        /// <summary>
        /// 영구 스탯 감각 단계(0~5). 분기 카드에 공개되는 정보 범위를 결정한다.
        /// </summary>
        private int senseLevel;

        /// <summary>
        /// 다음 층이 일반 층일 때 호출하여 분기 선택 UI를 활성화한다.
        /// 서로 다른 두 개의 룸 템플릿을 무작위로 선택하여 카드로 제시한다.
        /// </summary>
        private void ShowBranchSelection()
        {
            int nextFloor = currentFloor + 1;
            bool allowRestRoom = ShouldAllowRestRoomForNextSelection();
            branchOptionA = SelectBranchOption(nextFloor, allowRestRoom, null);
            bool allowSecondRestRoom = allowRestRoom && branchOptionA?.IsRestRoom != true;

            branchOptionB = SelectBranchOption(nextFloor, allowSecondRestRoom, branchOptionA);
            if (branchOptionB == null)
            {
                branchOptionB = SelectBranchOption(nextFloor, allowSecondRestRoom, branchOptionA);
            }

            restRoomOpportunityCooldownActive = BranchIncludesRestRoom(branchOptionA, branchOptionB);
            branchSelectionActive = true;
            pendingBranchClickX = -1f;
            pendingBranchClickY = -1f;
        }

        private RoomTemplate SelectBranchOption(int nextFloor, bool allowRestRoom, RoomTemplate otherOption)
        {
            RoomTemplate fallback = null;
            for (int attempt = 0; attempt < 16; attempt++)
            {
                RoomTemplate candidate = RoomTemplateLibrary.SelectForFloor(nextFloor, templateRandom, bossClearGrowthCount, allowRestRoom);
                if (fallback == null)
                {
                    fallback = candidate;
                }

                if (otherOption == null || AreBranchOptionsMeaningfullyDifferent(candidate, otherOption))
                {
                    return candidate;
                }
            }

            return fallback;
        }

        private static bool AreBranchOptionsMeaningfullyDifferent(RoomTemplate a, RoomTemplate b)
        {
            if (a == null || b == null)
            {
                return true;
            }

            return a.IsRestRoom != b.IsRestRoom ||
                a.IsBossRoom != b.IsBossRoom ||
                a.IsMiniBossRoom != b.IsMiniBossRoom ||
                a.ObjectiveKind != b.ObjectiveKind ||
                a.HazardKind != b.HazardKind ||
                a.LayoutVariant != b.LayoutVariant ||
                a.Spawns?.Length != b.Spawns?.Length ||
                !string.Equals(a.TemplateId, b.TemplateId, StringComparison.Ordinal);
        }

        private bool ShouldAllowRestRoomAfterCurrentFloor()
        {
            StageRoom encounterRoom = GetCurrentEncounterRoom();
            return encounterRoom == null || !encounterRoom.IsRestRoom;
        }

        private bool ShouldAllowRestRoomForNextSelection()
        {
            return ShouldAllowRestRoomForNextSelection(
                ShouldAllowRestRoomAfterCurrentFloor(),
                restRoomOpportunityCooldownActive);
        }

        private static bool ShouldAllowRestRoomForNextSelection(
            bool currentFloorAllowsRestRoom,
            bool restRoomCooldownActive)
        {
            return currentFloorAllowsRestRoom && !restRoomCooldownActive;
        }

        private static bool BranchIncludesRestRoom(RoomTemplate optionA, RoomTemplate optionB)
        {
            return optionA?.IsRestRoom == true || optionB?.IsRestRoom == true;
        }

        /// <summary>
        /// 분기 선택 UI가 활성화되어 있을 때 1/2 키 입력을 처리한다.
        /// 선택 즉시 해당 템플릿으로 다음 층을 로드한다.
        /// </summary>
        private void HandleBranchInput()
        {
            if (!branchSelectionActive)
            {
                pendingBranchClickX = -1f;
                pendingBranchClickY = -1f;
                return;
            }

            if (pendingBranchClickX >= 0f)
            {
                float clickX = pendingBranchClickX;
                float clickY = pendingBranchClickY;
                pendingBranchClickX = -1f;
                pendingBranchClickY = -1f;

                int index = BranchSelectionScreen.HitTest(clickX, clickY);
                RoomTemplate clicked = index == 0 ? branchOptionA : (index == 1 ? branchOptionB : null);
                if (clicked != null)
                {
                    ConfirmBranchSelection(clicked);
                    return;
                }
            }

            bool key1 = Input.GetKey(Keys.D1);
            bool key2 = Input.GetKey(Keys.D2);

            if (key1 && !branch1KeyHeld && branchOptionA != null)
            {
                ConfirmBranchSelection(branchOptionA);
            }
            else if (key2 && !branch2KeyHeld && branchOptionB != null)
            {
                ConfirmBranchSelection(branchOptionB);
            }

            branch1KeyHeld = key1;
            branch2KeyHeld = key2;
        }

        /// <summary>
        /// 선택된 템플릿으로 분기 상태를 해제하고 다음 층으로 전환한다.
        /// </summary>
        /// <param name="selected">플레이어가 선택한 룸 템플릿.</param>
        private void ConfirmBranchSelection(RoomTemplate selected)
        {
            branchSelectionActive = false;
            branchOptionA = null;
            branchOptionB = null;
            branch1KeyHeld = false;
            branch2KeyHeld = false;
            pendingBranchClickX = -1f;
            pendingBranchClickY = -1f;
            TransitionToNextFloor(selected);
        }

        /// <summary>
        /// 분기 선택 UI가 열려 있을 때 마우스 클릭 좌표를 다음 프레임 입력 처리로 전달한다.
        /// </summary>
        public void NotifyBranchMouseClick(float gameX, float gameY)
        {
            pendingBranchClickX = gameX;
            pendingBranchClickY = gameY;
        }

        /// <summary>분기 선택 UI가 현재 열려 있는지 여부.</summary>
        public bool BranchSelectionActive => branchSelectionActive;
    }
}
