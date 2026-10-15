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

        public static void Draw(Renderer r, RoomTemplate optionA, RoomTemplate optionB, int senseLevel, CardDeckMotion motion = null)
        {
            float fw = OverlayPanels.ScreenWidth;
            float fh = OverlayPanels.ScreenHeight;

            OverlayPanels.Dim(r, 170);
            OverlayPanels.Header(r, "다음 룸 선택", fw * 0.5f, fh * 0.10f);

            // 마우스를 올린 카드는 빛 번짐이 가리지 않게 나중에 그린다.
            bool rightOnTop = motion != null && motion.Hover(1) > motion.Hover(0);
            if (rightOnTop)
            {
                DrawSlot(r, 0, optionA, "[1]", senseLevel, motion);
                DrawSlot(r, 1, optionB, "[2]", senseLevel, motion);
            }
            else
            {
                DrawSlot(r, 1, optionB, "[2]", senseLevel, motion);
                DrawSlot(r, 0, optionA, "[1]", senseLevel, motion);
            }
        }

        /// <summary>고르지 않아 타서 사라지는 카드. 분기 화면이 닫힌 뒤 GameLogic이 부른다.</summary>
        public static void DrawBurning(Renderer r, CardDeckMotion motion, int senseLevel)
        {
            int count = motion?.BurnCount ?? 0;
            for (int i = 0; i < count; i++)
            {
                motion.GetBurn(i, out RectangleF rect, out Color accent, out int tier, out object item, out float progress, out float lineY);
                if (progress >= 1f || !(item is RoomTemplate template))
                {
                    continue;
                }

                CardLook look = new CardLook { Tier = tier, ClipBottom = lineY, FrameTint = CardDeckMotion.BurnTint(progress) };
                DrawCard(r, template, rect.X, rect.Y, rect.Width, rect.Height, null, senseLevel, look, landed: true);
                CardArt.DrawBurnEdge(r, rect.X, rect.Width, lineY, i + 7);
            }
        }

        /// <summary>고른 카드는 번쩍이고 다른 카드는 불태우도록 움직임에 알린다.</summary>
        public static void BeginPick(CardDeckMotion motion, int pickedIndex, RoomTemplate picked, RoomTemplate other)
        {
            if (motion == null) return;

            RectangleF pickedRect = GetCardRect(pickedIndex);
            pickedRect.Offset(0f, motion.OffsetY(pickedIndex));
            motion.Pick(pickedRect, GetAccent(picked), GetTier(picked));
            if (other != null)
            {
                int otherIndex = 1 - pickedIndex;
                RectangleF otherRect = GetCardRect(otherIndex);
                otherRect.Offset(0f, (float)Math.Round(motion.OffsetY(otherIndex)));
                motion.Burn(otherRect, GetAccent(other), GetTier(other), other);
            }
        }

        /// <summary>방 유형별 장식 단계: 보스 4(불꽃), 정예 3(뿔), 상점 1(보석), 일반 0.</summary>
        public static int GetTier(RoomTemplate template)
        {
            return template.IsBossRoom ? 4 : (template.IsMiniBossRoom ? 3 : (template.IsRestRoom ? 1 : 0));
        }

        /// <summary>카드 번호(0 = 왼쪽, 1 = 오른쪽)의 카드 몸통 자리(움직임 없이).</summary>
        public static RectangleF GetCardRect(int index)
        {
            GetCardX(index, out float x);
            return new RectangleF(x, (float)Math.Round(OverlayPanels.ScreenHeight * CardYRatio), CardWidth, CardHeight);
        }

        /// <summary>방 유형 색(테두리 홈·해골 눈·빛 번짐에 쓴다).</summary>
        public static Color GetAccent(RoomTemplate template)
        {
            return template.IsBossRoom
                ? Color.FromArgb(255, 200, 80, 50)
                : (template.IsRestRoom
                    ? Color.FromArgb(255, 110, 210, 170)
                    : (template.IsMiniBossRoom
                    ? Color.FromArgb(255, 160, 100, 220)
                    : Color.FromArgb(255, 100, 130, 180)));
        }

        private static void DrawSlot(Renderer r, int index, RoomTemplate template, string keyLabel, int senseLevel, CardDeckMotion motion)
        {
            if (motion != null && !motion.Visible(index)) return;

            RectangleF rect = GetCardRect(index);
            float y = (float)Math.Round(rect.Y + (motion?.OffsetY(index) ?? 0f));
            CardLook look = new CardLook { Hover = motion?.Hover(index) ?? 0f, Tier = GetTier(template) };
            bool landed = motion == null || motion.Drop(index) >= 1f;
            DrawCard(r, template, rect.X, y, rect.Width, rect.Height, keyLabel, senseLevel, look, landed);
            motion?.DrawCardOverlay(r, index, rect.X, y, rect.Width, rect.Height);
        }

        /// <summary>게임 좌표 클릭이 닿은 카드 번호(0 = 왼쪽, 1 = 오른쪽). 없으면 -1.</summary>
        public static int HitTest(float x, float y)
        {
            for (int i = 0; i < 2; i++)
            {
                RectangleF rect = GetCardRect(i);
                if (OverlayPanels.Contains(x, y, rect.X, rect.Y, rect.Width, rect.Height))
                {
                    return i;
                }
            }

            return -1;
        }

        private static void GetCardX(int index, out float x)
        {
            float center = OverlayPanels.ScreenWidth * 0.5f;
            x = (float)Math.Round(index == 0 ? center - CardWidth - CardGap * 0.5f : center + CardGap * 0.5f);
        }

        /// <summary>카드 한 장: 방 유형 색 테두리, 유형 제목, 정보 줄, 선택 키 안내(keyLabel이 null이면 생략).</summary>
        private static void DrawCard(Renderer r, RoomTemplate template, float x, float y, float w, float h, string keyLabel, int senseLevel, CardLook look, bool landed)
        {
            OverlayPanels.CardPanel(r, x, y, w, h, GetAccent(template), look);
            if (!landed)
            {
                return;
            }

            float clip = look.ClipBottom > 0f ? look.ClipBottom : float.MaxValue;

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

            OverlayPanels.TextClipped(r, header, cx, y + 27f, headerColor, 12f, clip, true);
            if (y + 40f < clip)
            {
                r.DrawRectangle(x + 14f, y + 39f, w - 28f, 1f, Color.FromArgb(120, 180, 180, 180));
            }

            float lineY = y + 52f;
            foreach (InfoLine line in BuildInfoLines(template, senseLevel))
            {
                OverlayPanels.TextClipped(r, line.Text, cx, lineY, line.Color, 6f, clip);
                lineY += 14.5f;
            }

            if (keyLabel == null)
            {
                return;
            }

            r.DrawRectangle(x + 14f, y + h - 31f, w - 28f, 1f, Color.FromArgb(120, 180, 180, 180));
            bool hot = look.Hover > 0.5f;
            Color prompt = hot ? PixelPalette.Lighten(PixelPalette.Brass, 0.35f) : Color.FromArgb(255, 255, 225, 100);
            r.DrawTextCenteredShadow(hot ? "▶ " + keyLabel + " 선택 ◀" : keyLabel + " 선택", cx, y + h - 20f, prompt, 6f);
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
