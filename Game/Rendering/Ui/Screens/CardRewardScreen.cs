using System;
using System.Drawing;
using My2DEngine.Engine.Rendering;
using My2DEngine.Game.Config;
using My2DEngine.Game.Core;

namespace My2DEngine.Game.Rendering.Ui.Screens
{
    /// <summary>카드 보상 화면이 그리는 데 필요한 값. GameLogic이 만들어 넘긴다.</summary>
    public sealed class CardRewardView
    {
        /// <summary>제시된 카드. 길이는 최대 칸 수이고 빈 칸은 null.</summary>
        public RewardCardOffer[] Offers;

        /// <summary>이번에 보여 줄 칸 수(3 또는 4).</summary>
        public int SlotCount;

        /// <summary>보스방 보상인지(문구와 카드 높이가 다르다).</summary>
        public bool WasBossRoom;

        /// <summary>스탯별 보유 최고 등급(StatType 순서, 없으면 -1).</summary>
        public int[] StatGrades;

        /// <summary>스탯별 획득 횟수.</summary>
        public int[] StatCounts;

        /// <summary>스탯별 누적량.</summary>
        public float[] StatTotals;

        /// <summary>무기별 보유 여부(WeaponType 순서).</summary>
        public bool[] OwnedWeapons;

        /// <summary>카드 움직임(떨어짐·떠오름). null이면 가만히 그린다.</summary>
        public CardDeckMotion Motion;

        /// <summary>계열별로 모은 스탯 카드 수(StatFamily 순서).</summary>
        public int[] FamilyCounts;

        /// <summary>다시 뽑기·건너뛰기 버튼을 보일지(보스 보상은 숨긴다).</summary>
        public bool ShowButtons;

        public int Coins;
        public int RerollCost;
        public int SkipReward;

        /// <summary>마우스 위치(게임 좌표). 버튼 올림 표시에 쓴다.</summary>
        public float PointerX;
        public float PointerY;
    }

    /// <summary>카드 보상 화면 아래 버튼.</summary>
    public enum CardRewardButton
    {
        None,
        Reroll,
        Skip
    }

    /// <summary>방 클리어 뒤 스탯·무기 카드 중 하나를 고르는 화면. 그리기와 클릭 판정이 같은 배치를 쓴다.</summary>
    public static class CardRewardScreen
    {
        /// <summary>기본 제시 칸 수. 이보다 많으면 카드를 좁힌다.</summary>
        public const int BaseSlotCount = 3;

        public static void Draw(Renderer r, CardRewardView v)
        {
            float fw = OverlayPanels.ScreenWidth;
            float fh = OverlayPanels.ScreenHeight;

            OverlayPanels.Dim(r, 185);
            OverlayPanels.Header(r, "카드 선택", fw * 0.5f, fh * 0.065f);

            if (v.WasBossRoom)
            {
                r.DrawTextCenteredShadow("보스 보상: 영구 스탯 포인트 +1 획득", fw * 0.5f, fh * 0.145f,
                    Color.FromArgb(255, 140, 220, 155), 6f);
            }

            // 마우스를 올린 카드는 빛 번짐이 이웃 카드에 가리지 않게 마지막에 그린다.
            int top = TopSlot(v);
            for (int i = 0; i < v.SlotCount; i++)
            {
                if (i != top) DrawSlot(r, v, i);
            }

            if (top >= 0) DrawSlot(r, v, top);

            if (v.ShowButtons)
            {
                DrawButtons(r, v);
            }
        }

        // ── 다시 뽑기·건너뛰기 버튼 ──

        private const float ButtonW = 128f;
        private const float ButtonH = 20f;
        private const float ButtonGap = 14f;

        private static RectangleF GetButtonRect(CardRewardView v, CardRewardButton button)
        {
            GetLayout(v.SlotCount, v.WasBossRoom, out _, out float cardH, out _, out _, out float cardY);
            float y = (float)Math.Round(cardY + cardH + 12f);
            float cx = OverlayPanels.ScreenWidth * 0.5f;
            float x = button == CardRewardButton.Reroll ? cx - ButtonGap * 0.5f - ButtonW : cx + ButtonGap * 0.5f;
            return new RectangleF((float)Math.Round(x), y, ButtonW, ButtonH);
        }

        /// <summary>게임 좌표가 닿은 버튼. 버튼을 보이지 않을 때는 None.</summary>
        public static CardRewardButton HitTestButton(CardRewardView v, float x, float y)
        {
            if (!v.ShowButtons)
            {
                return CardRewardButton.None;
            }

            foreach (CardRewardButton button in new[] { CardRewardButton.Reroll, CardRewardButton.Skip })
            {
                RectangleF rect = GetButtonRect(v, button);
                if (OverlayPanels.Contains(x, y, rect.X, rect.Y, rect.Width, rect.Height))
                {
                    return button;
                }
            }

            return CardRewardButton.None;
        }

        private static void DrawButtons(Renderer r, CardRewardView v)
        {
            RectangleF reroll = GetButtonRect(v, CardRewardButton.Reroll);
            RectangleF skip = GetButtonRect(v, CardRewardButton.Skip);
            bool canReroll = v.Coins >= v.RerollCost;

            PixelButtonState rerollState = !canReroll
                ? PixelButtonState.Disabled
                : (reroll.Contains(v.PointerX, v.PointerY) ? PixelButtonState.Hover : PixelButtonState.Normal);
            PixelButtonState skipState = skip.Contains(v.PointerX, v.PointerY) ? PixelButtonState.Hover : PixelButtonState.Normal;

            PixelUi.Button(r, reroll, 1f, $"다시 뽑기  -{v.RerollCost} 코인", null, rerollState, PixelPalette.Brass);
            PixelUi.Button(r, skip, 1f, $"건너뛰기  +{v.SkipReward} 코인", null, skipState, PixelPalette.AccentHot);
            r.DrawTextCenteredShadow($"보유 코인 {v.Coins}", OverlayPanels.ScreenWidth * 0.5f, reroll.Bottom + 9f,
                canReroll ? PixelPalette.Brass : PixelPalette.TextDim, 6f);
        }

        /// <summary>지금 보이는 카드를 모두 태운다(다시 뽑기·건너뛰기). 카드 칸이 바뀌기 전에 부른다.</summary>
        public static void BeginBurnAll(CardRewardView v)
        {
            CardDeckMotion motion = v.Motion;
            if (motion == null) return;

            motion.BeginBurn();
            for (int i = 0; i < v.SlotCount; i++)
            {
                RewardCardOffer offer = v.Offers[i];
                if (offer == null || !motion.Visible(i)) continue;
                RectangleF rect = GetCardRect(v, i);
                rect.Offset(0f, (float)Math.Round(motion.OffsetY(i)));
                motion.Burn(rect, GetAccent(offer), GetTier(offer), offer);
            }
        }

        /// <summary>
        /// 고르지 않아 타서 사라지는 카드들. 카드 화면이 닫힌 뒤 GameLogic이 부른다.
        /// drawDim이면 화면을 덮던 검은 막도 타는 동안 서서히 걷힌다.
        /// </summary>
        public static void DrawBurning(Renderer r, CardRewardView v, bool drawDim)
        {
            CardDeckMotion motion = v.Motion;
            int count = motion?.BurnCount ?? 0;
            if (count == 0)
            {
                return;
            }

            if (drawDim)
            {
                OverlayPanels.Dim(r, (int)(185 * Math.Min(1f, motion.BurnRemaining / 0.9f)));
            }

            for (int i = 0; i < count; i++)
            {
                motion.GetBurn(i, out RectangleF rect, out Color accent, out int tier, out object item, out float progress, out float lineY);
                if (progress >= 1f || !(item is RewardCardOffer offer))
                {
                    continue;
                }

                CardLook look = new CardLook { Tier = tier, ClipBottom = lineY, FrameTint = CardDeckMotion.BurnTint(progress) };
                DrawCard(r, v, offer, rect.X, rect.Y, rect.Width, rect.Height, look, landed: true);
                CardArt.DrawBurnEdge(r, rect.X, rect.Width, lineY, i + 1);
            }
        }

        private static int TopSlot(CardRewardView v)
        {
            if (v.Motion == null) return -1;
            int top = -1;
            float best = 0.01f;
            for (int i = 0; i < v.SlotCount; i++)
            {
                if (v.Motion.Hover(i) > best)
                {
                    best = v.Motion.Hover(i);
                    top = i;
                }
            }

            return top;
        }

        private static void DrawSlot(Renderer r, CardRewardView v, int i)
        {
            RewardCardOffer offer = v.Offers[i];
            if (offer == null) return;

            CardDeckMotion motion = v.Motion;
            if (motion != null && !motion.Visible(i)) return;

            RectangleF rect = GetCardRect(v, i);
            float y = (float)Math.Round(rect.Y + (motion?.OffsetY(i) ?? 0f));
            CardLook look = new CardLook { Hover = motion?.Hover(i) ?? 0f, Tier = GetTier(offer) };
            bool landed = motion == null || motion.Drop(i) >= 1f;
            DrawCard(r, v, offer, rect.X, y, rect.Width, rect.Height, look, landed);
            motion?.DrawCardOverlay(r, i, rect.X, y, rect.Width, rect.Height);
        }

        /// <summary>게임 좌표 클릭이 닿은 카드 칸 번호. 없으면 -1(빈 칸도 -1).</summary>
        public static int HitTest(CardRewardView v, float x, float y)
        {
            for (int i = 0; i < v.SlotCount; i++)
            {
                if (v.Offers[i] == null) continue;
                RectangleF rect = GetCardRect(v, i);
                if (OverlayPanels.Contains(x, y, rect.X, rect.Y, rect.Width, rect.Height))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>칸 번호의 카드 몸통 자리(움직임 없이).</summary>
        public static RectangleF GetCardRect(CardRewardView v, int slot)
        {
            GetLayout(v.SlotCount, v.WasBossRoom, out float cardW, out float cardH, out float gap, out float startX, out float cardY);
            return new RectangleF(startX + slot * (cardW + gap), cardY, cardW, cardH);
        }

        /// <summary>악마의 계약 카드 색(검붉은 피).</summary>
        public static readonly Color ContractColor = Color.FromArgb(255, 205, 22, 34);

        /// <summary>카드의 등급 색(테두리 홈·해골 눈·빛 번짐에 쓴다). 악마의 계약은 검붉은 색.</summary>
        public static Color GetAccent(RewardCardOffer offer)
        {
            if (IsContract(offer))
            {
                return ContractColor;
            }

            return CardGradeHelper.GetGradeColor(GetGrade(offer));
        }

        /// <summary>카드 장식 단계: 등급 그대로(White 0 ~ Red 4). 악마의 계약은 늘 4(불꽃 왕관).</summary>
        public static int GetTier(RewardCardOffer offer)
        {
            return IsContract(offer) ? 4 : (int)GetGrade(offer);
        }

        private static bool IsContract(RewardCardOffer offer)
        {
            return offer.IsPerkCard && RunPerkCatalog.Get(offer.Perk).Kind == RunPerkKind.Contract;
        }

        /// <summary>고른 카드는 번쩍이고 나머지는 불태우도록 움직임에 알린다. 카드 칸이 비기 전에 부른다.</summary>
        public static void BeginPick(CardRewardView v, int pickedSlot)
        {
            CardDeckMotion motion = v.Motion;
            if (motion == null) return;

            RectangleF picked = GetCardRect(v, pickedSlot);
            picked.Offset(0f, motion.OffsetY(pickedSlot));
            RewardCardOffer pickedOffer = v.Offers[pickedSlot];
            motion.Pick(picked, GetAccent(pickedOffer), GetTier(pickedOffer));

            for (int i = 0; i < v.SlotCount; i++)
            {
                RewardCardOffer offer = v.Offers[i];
                if (i == pickedSlot || offer == null) continue;
                RectangleF rect = GetCardRect(v, i);
                rect.Offset(0f, (float)Math.Round(motion.OffsetY(i)));
                motion.Burn(rect, GetAccent(offer), GetTier(offer), offer);
            }
        }

        private static CardGrade GetGrade(RewardCardOffer offer)
        {
            return offer.IsWeaponCard ? offer.WeaponGrade : offer.Grade;
        }

        private static void GetLayout(int slotCount, bool wasBossRoom,
            out float cardW, out float cardH, out float gap, out float startX, out float cardY)
        {
            cardW = slotCount > BaseSlotCount ? 140f : 166f;
            cardH = 206f;
            gap = slotCount > BaseSlotCount ? 16f : 22f;

            float totalW = slotCount * cardW + Math.Max(0, slotCount - 1) * gap;
            startX = (float)Math.Round((OverlayPanels.ScreenWidth - totalW) * 0.5f);
            cardY = (float)Math.Round(wasBossRoom ? OverlayPanels.ScreenHeight * 0.21f : OverlayPanels.ScreenHeight * 0.18f);
        }

        private static void DrawCard(Renderer r, CardRewardView v, RewardCardOffer offer, float x, float y, float w, float h, CardLook look, bool landed)
        {
            OverlayPanels.CardPanel(r, x, y, w, h, GetAccent(offer), look);
            if (!landed)
            {
                // 떨어지는 동안은 테두리만 보인다. 꽂히는 순간 번쩍이며 내용이 드러난다.
                return;
            }

            float clip = look.ClipBottom > 0f ? look.ClipBottom : float.MaxValue;
            float cx = x + w * 0.5f;
            if (offer.IsWeaponCard)
                DrawWeaponContent(r, v, offer, cx, y, w, clip);
            else if (offer.IsPerkCard)
                DrawPerkContent(r, offer, cx, y, w, clip);
            else
                DrawStatContent(r, v, offer, cx, y, w, clip);

            if (look.ClipBottom > 0f)
            {
                // 타는 카드에는 선택 안내를 쓰지 않는다.
                return;
            }

            DrawOrnamentDivider(r, cx, y + h - 32f, w * 0.78f, PixelPalette.EdgeDim, clip);
            bool hot = look.Hover > 0.5f;
            Color prompt = hot ? PixelPalette.Lighten(PixelPalette.Brass, 0.35f) : Color.FromArgb(255, 255, 225, 100);
            r.DrawTextCenteredShadow(hot ? "▶ 클릭하여 선택 ◀" : "클릭하여 선택", cx, y + h - 20f, prompt, 6f);
        }

        /// <summary>가운데 마름모가 박힌 구분선.</summary>
        private static void DrawOrnamentDivider(Renderer r, float cx, float y, float w, Color color, float clip)
        {
            if (y + 3f >= clip) return;
            float half = (float)Math.Round(w * 0.5f);
            r.DrawRectangle(cx - half, y, half - 5f, 1f, color);
            r.DrawRectangle(cx + 5f, y, half - 5f, 1f, color);
            r.DrawRectangle(cx - 1f, y - 2f, 2f, 5f, color);
            r.DrawRectangle(cx - 2f, y - 1f, 4f, 3f, color);
        }

        private static void DrawStatContent(Renderer r, CardRewardView v, RewardCardOffer offer, float cx, float y, float w, float clip)
        {
            Color gradeColor = CardGradeHelper.GetGradeColor(offer.Grade);
            string gradeName = CardGradeHelper.GetGradeName(offer.Grade);
            string statName = CardText.StatName(offer.StatType);
            string valueText = CardText.StatValue(offer.StatType, offer.StatBonusValue);
            float nameFontSize = w < 160f ? 10f : 12f;
            float valueFontSize = w < 160f ? 13f : 15f;

            OverlayPanels.TextClipped(r, "스탯 카드", cx, y + 28f, Color.FromArgb(200, 170, 170, 170), 6f, clip);
            OverlayPanels.TextClipped(r, gradeName, cx, y + 46f, gradeColor, 12f, clip);
            DrawOrnamentDivider(r, cx, y + 60f, w * 0.7f, PixelUi.Fade(gradeColor, 0.6f), clip);
            OverlayPanels.TextClipped(r, statName, cx, y + 82f, Color.FromArgb(255, 235, 225, 200), nameFontSize, clip, true);
            OverlayPanels.TextClipped(r, valueText, cx, y + 108f, gradeColor, valueFontSize, clip);

            // 조건부 카드는 발동 조건을 적어 단순 수치 카드와 구분한다.
            string conditionText = CardText.StatCondition(offer.StatType);
            string linkedText = CardText.StatLinked(offer.StatType, offer.Grade);
            if (!string.IsNullOrEmpty(conditionText))
            {
                OverlayPanels.TextClipped(r, conditionText, cx, y + 128f, Color.FromArgb(235, 255, 200, 110), 6f, clip);
            }
            else if (!string.IsNullOrEmpty(linkedText))
            {
                // 합친 카드: 함께 오르는 값
                OverlayPanels.TextClipped(r, "+ " + linkedText, cx, y + 128f, gradeColor, 6f, clip);
            }

            // 지금까지 모은 양
            int statIndex = (int)offer.StatType;
            int cur = v.StatGrades[statIndex];
            int pickupCount = v.StatCounts[statIndex];
            string owned = pickupCount <= 0
                ? "누적: 없음"
                : $"누적 {CardText.StatTotal(offer.StatType, v.StatTotals[statIndex])} / {pickupCount}회";
            Color ownedColor = cur < 0
                ? Color.FromArgb(170, 155, 155, 155)
                : CardGradeHelper.GetGradeColor((CardGrade)cur);
            OverlayPanels.TextClipped(r, owned, cx, y + 148f, ownedColor, 6f, clip);
            DrawFamilyLine(r, v, offer.StatType, cx, y + 164f, clip);
        }

        /// <summary>
        /// 카드의 계열과 모은 수. 이 카드로 시너지 단계가 오르면 "시너지 발동!"으로 바꿔 깜박인다.
        /// </summary>
        private static void DrawFamilyLine(Renderer r, CardRewardView v, StatType stat, float cx, float y, float clip)
        {
            StatFamily family = StatCardCatalog.FamilyOf(stat);
            if (family == StatFamily.None || v.FamilyCounts == null)
            {
                return;
            }

            StatFamilyDefinition definition = StatSynergyCatalog.Get(family);
            int have = v.FamilyCounts[(int)family];
            bool tierUp = StatSynergyCatalog.TierFor(have + 1) > StatSynergyCatalog.TierFor(have);
            if (tierUp)
            {
                float pulse = 0.6f + 0.4f * (float)Math.Sin(PixelUi.Clock * 8.0);
                OverlayPanels.TextClipped(r, $"{definition.Name} 시너지 발동!", cx, y,
                    PixelUi.Fade(PixelPalette.Lighten(definition.Color, 0.3f), pulse), 6f, clip, true);
                return;
            }

            int next = StatSynergyCatalog.NextThreshold(have);
            string text = have >= SynergyConfig.Tier2Cards
                ? $"{definition.Name} 계열 {have}장 (완성)"
                : $"{definition.Name} 계열 {have}/{next}";
            OverlayPanels.TextClipped(r, text, cx, y, PixelUi.Fade(definition.Color, 0.85f), 6f, clip);
        }

        /// <summary>
        /// 규칙 카드·악마의 계약. 이름 아래에 얻는 것을 초록으로, 계약이면 대가를 빨강으로 쓴다.
        /// </summary>
        private static void DrawPerkContent(Renderer r, RewardCardOffer offer, float cx, float y, float w, float clip)
        {
            RunPerkDefinition definition = RunPerkCatalog.Get(offer.Perk);
            bool contract = definition.Kind == RunPerkKind.Contract;
            Color accent = GetAccent(offer);

            if (contract)
            {
                OverlayPanels.TextClipped(r, "악마의 계약", cx, y + 28f, Color.FromArgb(255, 255, 96, 80), 6f, clip, true);
                OverlayPanels.TextClipped(r, "되돌릴 수 없다", cx, y + 46f, Color.FromArgb(220, 190, 70, 64), 6f, clip);
            }
            else
            {
                OverlayPanels.TextClipped(r, "규칙 카드", cx, y + 28f, Color.FromArgb(255, 255, 220, 100), 6f, clip, true);
                OverlayPanels.TextClipped(r, CardGradeHelper.GetGradeName(offer.Grade), cx, y + 46f, accent, 12f, clip);
            }

            DrawOrnamentDivider(r, cx, y + 60f, w * 0.7f, PixelUi.Fade(accent, 0.6f), clip);
            Color nameColor = contract ? Color.FromArgb(255, 255, 196, 180) : Color.FromArgb(255, 235, 225, 200);
            OverlayPanels.TextClipped(r, definition.Name, cx, y + 80f, nameColor, w < 160f ? 10f : 12f, clip, true);

            float lineY = y + 100f;
            foreach (string gain in definition.Gains)
            {
                OverlayPanels.TextClipped(r, gain, cx, lineY, Color.FromArgb(255, 140, 230, 125), 6f, clip);
                lineY += 12f;
            }

            if (definition.Costs.Length == 0)
            {
                return;
            }

            lineY += 6f;
            OverlayPanels.TextClipped(r, "- 대가 -", cx, lineY, Color.FromArgb(255, 170, 40, 40), 6f, clip, true);
            lineY += 12f;
            foreach (string cost in definition.Costs)
            {
                OverlayPanels.TextClipped(r, cost, cx, lineY, Color.FromArgb(255, 255, 90, 80), 6f, clip);
                lineY += 12f;
            }
        }

        private static void DrawWeaponContent(Renderer r, CardRewardView v, RewardCardOffer offer, float cx, float y, float w, float clip)
        {
            Color gradeColor = CardGradeHelper.GetGradeColor(offer.WeaponGrade);
            string gradeName = CardGradeHelper.GetGradeName(offer.WeaponGrade);
            string weaponName = CardText.WeaponName(offer.WeaponType);
            string catName = CardText.CategoryName(offer.WeaponCategory);
            string effectDesc = CardText.WeaponUpgradeDesc(offer.WeaponType, offer.WeaponCategory, offer.WeaponGrade);

            bool isNewWeapon = !v.OwnedWeapons[(int)offer.WeaponType];
            string headerText = isNewWeapon ? "신규 무기 해금" : "무기 업그레이드";
            Color headerColor = isNewWeapon
                ? Color.FromArgb(200, 255, 220, 100)
                : Color.FromArgb(200, 170, 170, 170);
            OverlayPanels.TextClipped(r, headerText, cx, y + 28f, headerColor, 6f, clip);
            OverlayPanels.TextClipped(r, gradeName, cx, y + 46f, gradeColor, 12f, clip);
            DrawOrnamentDivider(r, cx, y + 60f, w * 0.7f, PixelUi.Fade(gradeColor, 0.6f), clip);
            OverlayPanels.TextClipped(r, weaponName, cx, y + 82f, Color.FromArgb(255, 235, 225, 200), 12f, clip);
            OverlayPanels.TextClipped(r, catName, cx, y + 106f, gradeColor, 12f, clip);
            OverlayPanels.TextClipped(r, effectDesc, cx, y + 148f, Color.FromArgb(200, 210, 210, 210), 6f, clip);
        }
    }
}
