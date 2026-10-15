using System;
using System.Drawing;
using My2DEngine.Engine.Rendering;
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
            RectangleF rect = GetCardRect(v, i);
            float y = (float)Math.Round(rect.Y + (motion?.OffsetY(i) ?? 0f));
            float hover = motion?.Hover(i) ?? 0f;
            bool landed = motion == null || motion.Drop(i) >= 1f;
            DrawCard(r, v, offer, rect.X, y, rect.Width, rect.Height, hover, landed);
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

        /// <summary>카드의 등급 색(테두리 홈·해골 눈·빛 번짐에 쓴다).</summary>
        public static Color GetAccent(RewardCardOffer offer)
        {
            return offer.IsWeaponCard
                ? CardGradeHelper.GetGradeColor(offer.WeaponGrade)
                : CardGradeHelper.GetGradeColor(offer.Grade);
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

        private static void DrawCard(Renderer r, CardRewardView v, RewardCardOffer offer, float x, float y, float w, float h, float hover, bool landed)
        {
            OverlayPanels.CardPanel(r, x, y, w, h, GetAccent(offer), hover);
            if (!landed)
            {
                // 떨어지는 동안은 테두리만 보인다. 꽂히는 순간 번쩍이며 내용이 드러난다.
                return;
            }

            float cx = x + w * 0.5f;
            if (offer.IsWeaponCard)
                DrawWeaponContent(r, v, offer, cx, y, w);
            else
                DrawStatContent(r, v, offer, cx, y, w);

            DrawOrnamentDivider(r, cx, y + h - 32f, w * 0.78f, PixelPalette.EdgeDim);
            Color prompt = hover > 0.5f ? PixelPalette.Lighten(PixelPalette.Brass, 0.35f) : Color.FromArgb(255, 255, 225, 100);
            r.DrawTextCenteredShadow(hover > 0.5f ? "▶ 클릭하여 선택 ◀" : "클릭하여 선택", cx, y + h - 20f, prompt, 6f);
        }

        /// <summary>가운데 마름모가 박힌 구분선.</summary>
        private static void DrawOrnamentDivider(Renderer r, float cx, float y, float w, Color color)
        {
            float half = (float)Math.Round(w * 0.5f);
            r.DrawRectangle(cx - half, y, half - 5f, 1f, color);
            r.DrawRectangle(cx + 5f, y, half - 5f, 1f, color);
            r.DrawRectangle(cx - 1f, y - 2f, 2f, 5f, color);
            r.DrawRectangle(cx - 2f, y - 1f, 4f, 3f, color);
        }

        private static void DrawStatContent(Renderer r, CardRewardView v, RewardCardOffer offer, float cx, float y, float w)
        {
            Color gradeColor = CardGradeHelper.GetGradeColor(offer.Grade);
            string gradeName = CardGradeHelper.GetGradeName(offer.Grade);
            string statName = CardText.StatName(offer.StatType);
            string valueText = CardText.StatValue(offer.StatType, offer.StatBonusValue);
            float nameFontSize = w < 160f ? 10f : 12f;
            float valueFontSize = w < 160f ? 13f : 15f;

            r.DrawTextCenteredShadow("스탯 카드", cx, y + 28f, Color.FromArgb(200, 170, 170, 170), 6f);
            r.DrawTextCenteredShadow(gradeName, cx, y + 46f, gradeColor, 12f);
            DrawOrnamentDivider(r, cx, y + 60f, w * 0.7f, PixelUi.Fade(gradeColor, 0.6f));
            r.DrawTextCenteredShadow(statName, cx, y + 82f, Color.FromArgb(255, 235, 225, 200), nameFontSize, true);
            r.DrawTextCenteredShadow(valueText, cx, y + 108f, gradeColor, valueFontSize);

            // 조건부 카드는 발동 조건을 적어 단순 수치 카드와 구분한다.
            string conditionText = CardText.StatCondition(offer.StatType);
            if (!string.IsNullOrEmpty(conditionText))
            {
                r.DrawTextCenteredShadow(conditionText, cx, y + 128f, Color.FromArgb(235, 255, 200, 110), 6f);
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
            r.DrawTextCenteredShadow(owned, cx, y + 148f, ownedColor, 6f);
        }

        private static void DrawWeaponContent(Renderer r, CardRewardView v, RewardCardOffer offer, float cx, float y, float w)
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
            r.DrawTextCenteredShadow(headerText, cx, y + 28f, headerColor, 6f);
            r.DrawTextCenteredShadow(gradeName, cx, y + 46f, gradeColor, 12f);
            DrawOrnamentDivider(r, cx, y + 60f, w * 0.7f, PixelUi.Fade(gradeColor, 0.6f));
            r.DrawTextCenteredShadow(weaponName, cx, y + 82f, Color.FromArgb(255, 235, 225, 200), 12f);
            r.DrawTextCenteredShadow(catName, cx, y + 106f, gradeColor, 12f);
            r.DrawTextCenteredShadow(effectDesc, cx, y + 148f, Color.FromArgb(200, 210, 210, 210), 6f);
        }
    }
}
