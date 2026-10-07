using System;
using System.Collections.Generic;
using System.Drawing;
using My2DEngine.Engine.Rendering;
using My2DEngine.Game.Map;

namespace My2DEngine.Game.Rendering.Ui.Screens
{
    /// <summary>
    /// 방 클리어 뒤 다음 방 두 개 중 하나를 고르는 화면. 그리기와 클릭 판정이 같은 배치를 쓴다.
    /// 카드에 공개되는 적 정보 범위는 영구 스탯 감각 단계(senseLevel)가 정한다.
    /// </summary>
    public static class BranchSelectionScreen
    {
        private const float CardWidth = 230f;
        private const float CardHeight = 196f;
        private const float CardGap = 18f;
        private const float CardYRatio = 0.18f;

        public static void Draw(Renderer r, RoomTemplate optionA, RoomTemplate optionB, int senseLevel)
        {
            float fw = OverlayPanels.ScreenWidth;
            float fh = OverlayPanels.ScreenHeight;

            OverlayPanels.Dim(r, 170);
            OverlayPanels.Header(r, "다음 룸 선택", fw * 0.5f, fh * 0.10f);

            GetCardX(0, out float ax);
            GetCardX(1, out float bx);
            float cardY = fh * CardYRatio;
            DrawCard(r, optionA, ax, cardY, CardWidth, CardHeight, "[1]", senseLevel);
            DrawCard(r, optionB, bx, cardY, CardWidth, CardHeight, "[2]", senseLevel);
        }

        /// <summary>게임 좌표 클릭이 닿은 카드 번호(0 = 왼쪽, 1 = 오른쪽). 없으면 -1.</summary>
        public static int HitTest(float x, float y)
        {
            float cardY = OverlayPanels.ScreenHeight * CardYRatio;
            for (int i = 0; i < 2; i++)
            {
                GetCardX(i, out float cardX);
                if (OverlayPanels.Contains(x, y, cardX, cardY, CardWidth, CardHeight))
                {
                    return i;
                }
            }

            return -1;
        }

        private static void GetCardX(int index, out float x)
        {
            float center = OverlayPanels.ScreenWidth * 0.5f;
            x = index == 0 ? center - CardWidth - CardGap * 0.5f : center + CardGap * 0.5f;
        }

        /// <summary>카드 한 장: 방 유형 색 테두리, 유형 제목, 정보 줄, 선택 키 안내.</summary>
        private static void DrawCard(Renderer r, RoomTemplate template, float x, float y, float w, float h, string keyLabel, int senseLevel)
        {
            Color borderColor = template.IsBossRoom
                ? Color.FromArgb(220, 200, 80, 50)
                : (template.IsRestRoom
                    ? Color.FromArgb(220, 110, 210, 170)
                    : (template.IsMiniBossRoom
                    ? Color.FromArgb(220, 160, 100, 220)
                    : Color.FromArgb(200, 100, 130, 180)));

            OverlayPanels.CardPanel(r, x, y, w, h, borderColor);

            float cx = x + w * 0.5f;

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

            r.DrawTextCenteredShadow(header, cx, y + 14f, headerColor, 12f, true);
            r.DrawRectangle(x + 8f, y + 25f, w - 16f, 1f, Color.FromArgb(120, 180, 180, 180));

            float lineY = y + 38f;
            foreach (InfoLine line in BuildInfoLines(template, senseLevel))
            {
                r.DrawTextCenteredShadow(line.Text, cx, lineY, line.Color, 6f);
                lineY += 14.5f;
            }

            r.DrawRectangle(x + 8f, y + h - 28f, w - 16f, 1f, Color.FromArgb(120, 180, 180, 180));
            r.DrawTextCenteredShadow(keyLabel + " 선택", cx, y + h - 14f,
                Color.FromArgb(255, 255, 225, 100), 6f);
        }

        /// <summary>감각 단계에 따라 카드에 표시할 정보 줄을 만든다.</summary>
        private static InfoLine[] BuildInfoLines(RoomTemplate template, int senseLevel)
        {
            if (template.IsRestRoom)
            {
                return new[]
                {
                    InfoLine.Neutral("목표: 상점"),
                    InfoLine.Safe("위험도: 없음"),
                    InfoLine.Reward("보상: 고등급 카드 구매"),
                    InfoLine.Neutral("선택지: 카드 3장")
                };
            }

            List<InfoLine> lines = new List<InfoLine>
            {
                InfoLine.Neutral("목표: " + GetObjectiveLabel(template)),
                new InfoLine("위험도: " + GetRiskLabel(template), GetRiskColor(template)),
                InfoLine.Reward("보상: " + GetRewardLabel(template))
            };

            if (template.HazardKind != RoomHazardKind.None)
            {
                lines.Add(InfoLine.Warning("변수: " + GetHazardLabel(template)));
            }

            if (senseLevel == 0)
            {
                lines.Add(InfoLine.Hidden("적: ???"));
                return lines.ToArray();
            }

            List<string> orderedLabels = new List<string>();
            Dictionary<string, int> counts = new Dictionary<string, int>();
            if (template.Spawns != null)
            {
                foreach (StageSpawnPoint spawn in template.Spawns)
                {
                    string label = GetEnemyLabel(spawn);
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

            // 감각 2단계부터 종류별 수까지 보인다
            List<string> parts = new List<string>();
            foreach (string label in orderedLabels)
            {
                parts.Add(senseLevel >= 2 ? label + "×" + counts[label] : label);
            }

            lines.Add(InfoLine.Neutral("적: " + (parts.Count > 0 ? string.Join(", ", parts) : "없음")));

            if (senseLevel >= 3)
            {
                lines.Add(InfoLine.Detail("규모: " + template.Spawns.Length + "체 / " + template.RoomWidth + "x" + template.RoomHeight));
            }

            return lines.ToArray();
        }

        private static string GetObjectiveLabel(RoomTemplate template)
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

        private static string GetHazardLabel(RoomTemplate template)
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

        private static string GetRewardLabel(RoomTemplate template)
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

        private static string GetRiskLabel(RoomTemplate template)
        {
            int score = RoomTemplateLibrary.GetRoomRiskScore(template);
            if (score >= RoomTemplateLibrary.ExtremeRoomRiskScore) return "극한";
            if (score >= RoomTemplateLibrary.HighRoomRiskScore) return "높음";
            if (score >= 2) return "보통";
            return "낮음";
        }

        private static Color GetRiskColor(RoomTemplate template)
        {
            int score = RoomTemplateLibrary.GetRoomRiskScore(template);
            if (score >= RoomTemplateLibrary.ExtremeRoomRiskScore) return Color.FromArgb(255, 255, 105, 85);
            if (score >= RoomTemplateLibrary.HighRoomRiskScore) return Color.FromArgb(255, 255, 165, 80);
            if (score >= 2) return Color.FromArgb(255, 235, 210, 120);
            return Color.FromArgb(255, 135, 230, 170);
        }

        private static string GetEnemyLabel(StageSpawnPoint spawn)
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

        private readonly struct InfoLine
        {
            public readonly string Text;
            public readonly Color Color;

            public InfoLine(string text, Color color)
            {
                Text = text;
                Color = color;
            }

            public static InfoLine Neutral(string text) => new InfoLine(text, Color.FromArgb(255, 205, 205, 205));
            public static InfoLine Safe(string text) => new InfoLine(text, Color.FromArgb(255, 135, 230, 170));
            public static InfoLine Reward(string text) => new InfoLine(text, Color.FromArgb(255, 255, 225, 115));
            public static InfoLine Warning(string text) => new InfoLine(text, Color.FromArgb(255, 255, 175, 95));
            public static InfoLine Hidden(string text) => new InfoLine(text, Color.FromArgb(255, 170, 170, 170));
            public static InfoLine Detail(string text) => new InfoLine(text, Color.FromArgb(255, 170, 205, 235));
        }
    }
}
