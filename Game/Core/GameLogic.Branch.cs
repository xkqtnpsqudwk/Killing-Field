using System;
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

        private const float BranchCardWidth = 230f;
        private const float BranchCardHeight = 196f;
        private const float BranchCardGap = 18f;
        private const float BranchCardYRatio = 0.18f;

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

                float fw = RenderConfig.GpuWorldMaxRenderWidth;
                float fh = RenderConfig.GpuWorldMaxRenderHeight;
                float cardW = BranchCardWidth;
                float cardH = BranchCardHeight;
                float cardY = fh * BranchCardYRatio;
                float gap = BranchCardGap;
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

            float fw = RenderConfig.GpuWorldMaxRenderWidth;
            float fh = RenderConfig.GpuWorldMaxRenderHeight;

            // 화면 전체 반투명 어둠 처리
            r.DrawRectangle(0f, 0f, fw, fh, Color.FromArgb(170, 0, 0, 0));

            // 제목
            r.DrawTextCenteredShadow("[ 다음 룸 선택 ]", fw * 0.5f, fh * 0.10f,
                Color.FromArgb(255, 255, 235, 150), 14f);

            // 카드 배치: 왼쪽 / 오른쪽
            float cardW = BranchCardWidth;
            float cardH = BranchCardHeight;
            float cardY = fh * BranchCardYRatio;
            float gap = BranchCardGap;
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
            // 방 유형별 테두리 색 (보스/휴식/미니보스/일반)
            Color borderColor = template.IsBossRoom
                ? Color.FromArgb(220, 200, 80, 50)
                : (template.IsRestRoom
                    ? Color.FromArgb(220, 110, 210, 170)
                    : (template.IsMiniBossRoom
                    ? Color.FromArgb(220, 160, 100, 220)
                    : Color.FromArgb(200, 100, 130, 180)));

            // 카드 배경 + 유형 색 테두리 (패널 프레임이 있으면 9-slice 금속 틀 사용)
            DrawCardPanelBackground(r, x, y, w, h, borderColor);

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
                header = "● 카드 상점";
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
            BranchInfoLine[] infoLines = BuildRoomInfoLines(template);
            float lineY = y + 38f;
            foreach (BranchInfoLine line in infoLines)
            {
                r.DrawTextCenteredShadow(line.Text, cx, lineY, line.Color, 8.6f);
                lineY += 14.5f;
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
        private BranchInfoLine[] BuildRoomInfoLines(RoomTemplate template)
        {
            if (template.IsRestRoom)
            {
                return new[]
                {
                    BranchInfoLine.Neutral("목표: 상점"),
                    BranchInfoLine.Safe("위험도: 없음"),
                    BranchInfoLine.Reward("보상: 고등급 카드 구매"),
                    BranchInfoLine.Neutral("선택지: 카드 3장")
                };
            }

            List<BranchInfoLine> lines = new List<BranchInfoLine>
            {
                BranchInfoLine.Neutral("목표: " + GetRoomObjectiveLabel(template)),
                new BranchInfoLine("위험도: " + GetRoomRiskLabel(template), GetRoomRiskColor(template)),
                BranchInfoLine.Reward("보상: " + GetRoomRewardLabel(template))
            };

            if (template.HazardKind != RoomHazardKind.None)
            {
                lines.Add(BranchInfoLine.Warning("변수: " + GetRoomHazardLabel(template)));
            }

            if (senseLevel == 0)
            {
                lines.Add(BranchInfoLine.Hidden("적: ???"));
                return lines.ToArray();
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

            lines.Add(BranchInfoLine.Neutral(enemyLine));

            if (senseLevel >= 3)
            {
                lines.Add(BranchInfoLine.Detail("규모: " + template.Spawns.Length + "체 / " + template.RoomWidth + "x" + template.RoomHeight));
            }

            return lines.ToArray();
        }

        private static string GetRoomObjectiveLabel(RoomTemplate template)
        {
            switch (template.ObjectiveKind)
            {
                case RoomObjectiveKind.Survive:
                    return Math.Ceiling(Math.Max(1f, template.ObjectiveDuration)) + "초 생존";
                case RoomObjectiveKind.KeyTarget:
                    return "은닉 표적 추적";
                default:
                    return "전멸";
            }
        }

        private static string GetRoomHazardLabel(RoomTemplate template)
        {
            switch (template.HazardKind)
            {
                case RoomHazardKind.ToxicMist:
                    return "독성 안개";
                case RoomHazardKind.SupplyShortage:
                    return "보급 없음";
                default:
                    return "없음";
            }
        }

        private static string GetRoomRewardLabel(RoomTemplate template)
        {
            if (template.IsBossRoom)
            {
                return "무기 카드 + 영구 포인트";
            }

            if (RoomTemplateLibrary.IsExtremeRewardRoom(template))
            {
                return "스탯 카드 (최고등급 +1)";
            }

            if (RoomTemplateLibrary.GetCardRewardGradeBoost(template) > 0)
            {
                return "스탯 카드 +1등급";
            }

            return template.IsMiniBossRoom ? "스탯 카드 (정예)" : "스탯 카드";
        }

        private static string GetRoomRiskLabel(RoomTemplate template)
        {
            int score = GetRoomRiskScore(template);
            if (score >= RoomTemplateLibrary.ExtremeRoomRiskScore) return "극한";
            if (score >= RoomTemplateLibrary.HighRoomRiskScore) return "높음";
            if (score >= 2) return "보통";
            return "낮음";
        }

        private static Color GetRoomRiskColor(RoomTemplate template)
        {
            int score = GetRoomRiskScore(template);
            if (score >= RoomTemplateLibrary.ExtremeRoomRiskScore) return Color.FromArgb(255, 255, 105, 85);
            if (score >= RoomTemplateLibrary.HighRoomRiskScore) return Color.FromArgb(255, 255, 165, 80);
            if (score >= 2) return Color.FromArgb(255, 235, 210, 120);
            return Color.FromArgb(255, 135, 230, 170);
        }

        private static int GetRoomRiskScore(RoomTemplate template)
        {
            return RoomTemplateLibrary.GetRoomRiskScore(template);
        }

        private readonly struct BranchInfoLine
        {
            public readonly string Text;
            public readonly Color Color;

            public BranchInfoLine(string text, Color color)
            {
                Text = text;
                Color = color;
            }

            public static BranchInfoLine Neutral(string text)
            {
                return new BranchInfoLine(text, Color.FromArgb(255, 205, 205, 205));
            }

            public static BranchInfoLine Safe(string text)
            {
                return new BranchInfoLine(text, Color.FromArgb(255, 135, 230, 170));
            }

            public static BranchInfoLine Reward(string text)
            {
                return new BranchInfoLine(text, Color.FromArgb(255, 255, 225, 115));
            }

            public static BranchInfoLine Warning(string text)
            {
                return new BranchInfoLine(text, Color.FromArgb(255, 255, 175, 95));
            }

            public static BranchInfoLine Hidden(string text)
            {
                return new BranchInfoLine(text, Color.FromArgb(255, 170, 170, 170));
            }

            public static BranchInfoLine Detail(string text)
            {
                return new BranchInfoLine(text, Color.FromArgb(255, 170, 205, 235));
            }
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
