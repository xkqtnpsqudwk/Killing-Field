using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using My2DEngine.Engine.Input;
using My2DEngine.Engine.Rendering;
using My2DEngine.Game;
using My2DEngine.Game.Config;
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
            branchOptionA = RoomTemplateLibrary.SelectForFloor(nextFloor, templateRandom, bossClearGrowthCount);

            // 두 번째 템플릿은 첫 번째와 다른 ID가 되도록 최대 8회 재시도한다.
            for (int attempt = 0; attempt < 8; attempt++)
            {
                branchOptionB = RoomTemplateLibrary.SelectForFloor(nextFloor, templateRandom, bossClearGrowthCount);
                if (branchOptionB.TemplateId != branchOptionA.TemplateId)
                {
                    break;
                }
            }

            branchSelectionActive = true;
            pendingBranchClickX = -1f;
            pendingBranchClickY = -1f;
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

                float fw = GameConfig.GpuWorldMaxRenderWidth;
                float fh = GameConfig.GpuWorldMaxRenderHeight;
                float cardW = 205f;
                float cardH = 165f;
                float cardY = fh * 0.20f;
                float gap = 18f;
                float cardAx = fw * 0.5f - cardW - gap * 0.5f;
                float cardBx = fw * 0.5f + gap * 0.5f;

                if (branchOptionA != null &&
                    clickX >= cardAx && clickX < cardAx + cardW &&
                    clickY >= cardY && clickY < cardY + cardH)
                {
                    ConfirmBranchSelection(branchOptionA);
                    return;
                }

                if (branchOptionB != null &&
                    clickX >= cardBx && clickX < cardBx + cardW &&
                    clickY >= cardY && clickY < cardY + cardH)
                {
                    ConfirmBranchSelection(branchOptionB);
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

        /// <summary>
        /// 분기 선택 UI를 화면에 그린다.
        /// branchSelectionActive가 false이면 아무것도 그리지 않는다.
        /// </summary>
        /// <param name="r">렌더러.</param>
        private void DrawBranchSelectionUI(Renderer r)
        {
            if (!branchSelectionActive || branchOptionA == null || branchOptionB == null)
            {
                return;
            }

            float fw = GameConfig.GpuWorldMaxRenderWidth;
            float fh = GameConfig.GpuWorldMaxRenderHeight;

            // 화면 전체 반투명 어둠 처리
            r.DrawRectangle(0f, 0f, fw, fh, Color.FromArgb(170, 0, 0, 0));

            // 제목
            r.DrawTextCenteredShadow("[ 다음 룸 선택 ]", fw * 0.5f, fh * 0.10f,
                Color.FromArgb(255, 255, 235, 150), 14f);

            // 카드 배치: 왼쪽 / 오른쪽
            float cardW = 205f;
            float cardH = 165f;
            float cardY = fh * 0.20f;
            float gap = 18f;
            float cardAx = fw * 0.5f - cardW - gap * 0.5f;
            float cardBx = fw * 0.5f + gap * 0.5f;

            DrawBranchCard(r, branchOptionA, cardAx, cardY, cardW, cardH, "[1]");
            DrawBranchCard(r, branchOptionB, cardBx, cardY, cardW, cardH, "[2]");
        }

        /// <summary>
        /// 개별 분기 선택 카드를 그린다.
        /// 카드 배경·테두리·구역 유형 헤더·적 정보·선택 키 힌트를 포함한다.
        /// </summary>
        private void DrawBranchCard(Renderer r, RoomTemplate template, float x, float y, float w, float h, string keyLabel)
        {
            // 카드 배경
            r.DrawRectangle(x, y, w, h, Color.FromArgb(210, 25, 32, 44));

            // 테두리 (4변을 1px 사각형으로 처리)
            Color borderColor = template.IsBossRoom
                ? Color.FromArgb(220, 200, 80, 50)
                : (template.IsRestRoom
                    ? Color.FromArgb(220, 110, 210, 170)
                    : (template.IsMiniBossRoom
                    ? Color.FromArgb(220, 160, 100, 220)
                    : Color.FromArgb(200, 100, 130, 180)));

            r.DrawRectangle(x,         y,         w, 1f, borderColor);
            r.DrawRectangle(x,         y + h - 1, w, 1f, borderColor);
            r.DrawRectangle(x,         y,         1f, h,  borderColor);
            r.DrawRectangle(x + w - 1, y,         1f, h,  borderColor);

            float cx = x + w * 0.5f;

            // 구역 유형 헤더
            string header;
            Color headerColor;
            if (template.IsBossRoom)
            {
                header = "★ 보스 구역";
                headerColor = Color.FromArgb(255, 255, 110, 70);
            }
            else if (template.IsRestRoom)
            {
                header = "● 휴식 구역";
                headerColor = Color.FromArgb(255, 120, 230, 190);
            }
            else if (template.IsMiniBossRoom)
            {
                header = "◆ 정예 구역";
                headerColor = Color.FromArgb(255, 200, 145, 255);
            }
            else
            {
                header = "■ 일반 구역";
                headerColor = Color.FromArgb(255, 170, 205, 255);
            }

            r.DrawTextCenteredShadow(header, cx, y + 14f, headerColor, 10f);

            // 구분선
            r.DrawRectangle(x + 8f, y + 25f, w - 16f, 1f, Color.FromArgb(120, 180, 180, 180));

            // 적 정보 (감각 단계에 따라 공개 범위 결정)
            string[] infoLines = BuildRoomInfoLines(template);
            float lineY = y + 38f;
            foreach (string line in infoLines)
            {
                r.DrawTextCenteredShadow(line, cx, lineY, Color.FromArgb(255, 205, 205, 205), 9f);
                lineY += 16f;
            }

            // 구분선
            r.DrawRectangle(x + 8f, y + h - 28f, w - 16f, 1f, Color.FromArgb(120, 180, 180, 180));

            // 선택 키 힌트
            r.DrawTextCenteredShadow(keyLabel + " 선택", cx, y + h - 14f,
                Color.FromArgb(255, 255, 225, 100), 10f);
        }

        /// <summary>
        /// 감각(senseLevel) 단계에 따라 카드에 표시할 적 정보 문자열 배열을 생성한다.
        /// </summary>
        private string[] BuildRoomInfoLines(RoomTemplate template)
        {
            if (template.IsRestRoom)
            {
                return new[] { "적: 없음", "보상: 코인 상점 (회복 / 스팀 / 탄약)" };
            }

            if (senseLevel == 0)
            {
                return new[] { "적: ???", "보상: ???" };
            }

            List<string> orderedLabels = new List<string>();
            Dictionary<string, int> counts = new Dictionary<string, int>();

            if (template.Spawns != null)
            {
                foreach (StageSpawnPoint spawn in template.Spawns)
                {
                    string label = GetBranchEnemyLabel(spawn);
                    if (string.IsNullOrWhiteSpace(label))
                    {
                        continue;
                    }

                    if (!counts.ContainsKey(label))
                    {
                        counts[label] = 0;
                        orderedLabels.Add(label);
                    }

                    counts[label]++;
                }
            }

            // 적 정보 줄 구성
            string enemyLine;
            if (senseLevel >= 2)
            {
                List<string> parts = new List<string>();
                for (int i = 0; i < orderedLabels.Count; i++)
                {
                    string label = orderedLabels[i];
                    parts.Add(label + "×" + counts[label]);
                }
                enemyLine = "적: " + (parts.Count > 0 ? string.Join(", ", parts) : "없음");
            }
            else
            {
                List<string> types = new List<string>();
                for (int i = 0; i < orderedLabels.Count; i++)
                {
                    types.Add(orderedLabels[i]);
                }
                enemyLine = "적: " + (types.Count > 0 ? string.Join(", ", types) : "없음");
            }

            if (senseLevel >= 3)
            {
                string rewardLine = "보상: 스탯 카드";
                return new[] { enemyLine, rewardLine };
            }

            return new[] { enemyLine, "보상: ???" };
        }

        private static string GetBranchEnemyLabel(StageSpawnPoint spawn)
        {
            if (spawn == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(spawn.DisplayName))
            {
                return spawn.DisplayName;
            }

            EnemyArchetype archetype = EnemyCatalog.Resolve(spawn.EnemyAssetId, spawn.Type);
            if (!string.IsNullOrWhiteSpace(archetype?.RoomLabel))
            {
                return archetype.RoomLabel;
            }

            return archetype?.DisplayName;
        }
    }
}
