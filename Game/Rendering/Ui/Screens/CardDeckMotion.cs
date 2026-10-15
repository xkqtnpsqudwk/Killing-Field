using System;
using System.Drawing;
using My2DEngine.Engine.Rendering;

namespace My2DEngine.Game.Rendering.Ui.Screens
{
    /// <summary>
    /// 고르기 화면(카드 보상, 다음 룸 선택) 카드의 움직임.
    /// 화면이 열리면 카드가 한 장씩 위에서 떨어져 꽂히고, 마우스를 올린 카드는 떠오르며 빛나고,
    /// 고른 카드는 화면이 닫힌 뒤에도 잠깐 번쩍이며 부풀었다 사라진다. 게임 규칙에는 영향을 주지 않는다.
    /// </summary>
    public sealed class CardDeckMotion
    {
        public const int MaxSlots = 4;

        /// <summary>카드마다 떨어지기 시작하는 간격(초).</summary>
        private const float DropStagger = 0.07f;

        /// <summary>카드 한 장이 떨어져 꽂히는 시간(초).</summary>
        private const float DropDuration = 0.30f;

        /// <summary>떨어지기 시작하는 높이(게임 픽셀).</summary>
        private const float DropHeight = 44f;

        /// <summary>꽂힌 직후 번쩍임이 사라지는 시간(초).</summary>
        private const float LandFlashDuration = 0.18f;

        /// <summary>마우스를 올렸을 때 떠오르는 높이(게임 픽셀).</summary>
        private const float HoverLift = 5f;

        /// <summary>떠오름이 목표에 다가가는 빠르기(1/초).</summary>
        private const float HoverSpeed = 14f;

        /// <summary>고른 카드가 번쩍이며 사라지는 시간(초).</summary>
        private const float PickDuration = 0.4f;

        private readonly float[] hover = new float[MaxSlots];
        private float openTime;
        private bool wasOpen;
        private RectangleF pickRect;
        private Color pickColor;
        private float pickTimer;

        /// <summary>
        /// 매 프레임 부른다. 화면이 새로 열리면 처음부터 다시 떨어뜨린다.
        /// hoveredSlot은 마우스 아래 카드 번호(없으면 -1).
        /// </summary>
        public void Update(float dt, bool open, int hoveredSlot)
        {
            if (open && !wasOpen)
            {
                openTime = 0f;
                Array.Clear(hover);
            }

            wasOpen = open;
            pickTimer = Math.Max(0f, pickTimer - dt);
            if (!open)
            {
                return;
            }

            openTime += dt;
            float k = Math.Min(1f, dt * HoverSpeed);
            for (int i = 0; i < MaxSlots; i++)
            {
                // 아직 떨어지는 중인 카드는 떠오르지 않는다.
                float target = i == hoveredSlot && Drop(i) >= 1f ? 1f : 0f;
                hover[i] += (target - hover[i]) * k;
            }
        }

        /// <summary>고른 카드의 번쩍임을 시작한다. rect는 카드 몸통.</summary>
        public void Pick(RectangleF rect, Color accent)
        {
            pickRect = rect;
            pickColor = accent;
            pickTimer = PickDuration;
        }

        /// <summary>마우스를 올린 정도(0~1).</summary>
        public float Hover(int slot)
        {
            return slot >= 0 && slot < MaxSlots ? hover[slot] : 0f;
        }

        /// <summary>가장 많이 떠오른 카드의 떠오름. 다른 카드를 어둡게 할 때 쓴다.</summary>
        public float MaxHover()
        {
            float m = 0f;
            for (int i = 0; i < MaxSlots; i++)
            {
                m = Math.Max(m, hover[i]);
            }

            return m;
        }

        /// <summary>떨어짐 진행도(0~1). 1이면 자리에 꽂혔다.</summary>
        public float Drop(int slot)
        {
            return Math.Max(0f, Math.Min(1f, (openTime - slot * DropStagger) / DropDuration));
        }

        /// <summary>지금 그릴 카드 위치의 세로 이동(떨어짐 + 떠오름).</summary>
        public float OffsetY(int slot)
        {
            float t = Drop(slot);

            // 살짝 지나쳤다 돌아오는 감속(ease-out-back)
            const float overshoot = 1.9f;
            float s = t - 1f;
            float eased = 1f + (overshoot + 1f) * s * s * s + overshoot * s * s;
            return -(1f - eased) * DropHeight - Hover(slot) * HoverLift;
        }

        /// <summary>꽂힌 직후 번쩍임 세기(0~1).</summary>
        public float LandFlash(int slot)
        {
            float since = openTime - slot * DropStagger - DropDuration;
            if (since < 0f || since >= LandFlashDuration)
            {
                return 0f;
            }

            return 1f - since / LandFlashDuration;
        }

        /// <summary>
        /// 카드 몸통 위에 덮는 효과: 꽂힐 때의 흰 번쩍임과, 다른 카드에 마우스가 있을 때의 어둡게 함.
        /// 내용을 그린 뒤에 부른다.
        /// </summary>
        public void DrawCardOverlay(Renderer r, int slot, float x, float y, float w, float h)
        {
            float flash = LandFlash(slot);
            if (flash > 0f)
            {
                r.DrawRectangle(x + 4f, y + 4f, w - 8f, h - 8f, Color.FromArgb((int)(170 * flash * flash), 255, 240, 220));
            }

            float dim = MaxHover() - Hover(slot);
            if (dim > 0.01f)
            {
                r.DrawRectangle(x + 4f, y + 4f, w - 8f, h - 8f, Color.FromArgb((int)(80 * dim), 0, 0, 0));
            }
        }

        /// <summary>고른 카드가 번쩍이며 부풀었다 사라지는 모습. 화면이 닫힌 뒤에도 그린다.</summary>
        public void DrawPick(Renderer r)
        {
            if (pickTimer <= 0f)
            {
                return;
            }

            float k = 1f - pickTimer / PickDuration;
            float fade = 1f - k;
            float grow = k * 12f;
            float x = pickRect.X - grow;
            float y = pickRect.Y - grow - k * 6f;
            float w = pickRect.Width + grow * 2f;
            float h = pickRect.Height + grow * 2f;
            PixelUi.GlowRect(r, x, y, w, h, 18f, PixelPalette.WithAlpha(pickColor, (int)(230 * fade)));
            PixelUi.DoomCard(r, x, y, w, h, pickColor, 1f, fade);
            r.DrawRectangle(x + 4f, y + 4f, w - 8f, h - 8f, Color.FromArgb((int)(220 * fade * fade), 255, 244, 225));
        }
    }
}
