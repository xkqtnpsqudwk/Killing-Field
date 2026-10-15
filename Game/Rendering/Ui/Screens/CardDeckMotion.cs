using System;
using System.Drawing;
using My2DEngine.Engine.Rendering;

namespace My2DEngine.Game.Rendering.Ui.Screens
{
    /// <summary>
    /// 고르기 화면(카드 보상, 다음 룸 선택) 카드의 움직임.
    /// 화면이 열리면 카드가 한 장씩 위에서 떨어져 꽂히고, 마우스를 올린 카드는 떠오르며 빛나고,
    /// 고르면 그 카드는 번쩍이며 부풀었다 사라지고 나머지 카드는 아래부터 불타 사라진다.
    /// 화면이 닫힌 뒤에도 번쩍임·불탐은 이어서 그린다. 게임 규칙에는 영향을 주지 않는다.
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

        /// <summary>고르지 않은 카드 한 장이 다 타는 시간(초).</summary>
        private const float BurnDuration = 0.85f;

        /// <summary>카드마다 불이 붙는 간격(초).</summary>
        private const float BurnStagger = 0.08f;

        private readonly float[] hover = new float[MaxSlots];
        private float openTime;
        private bool wasOpen;

        private RectangleF pickRect;
        private Color pickColor;
        private int pickTier;
        private float pickTimer;

        private readonly RectangleF[] burnRects = new RectangleF[MaxSlots];
        private readonly Color[] burnColors = new Color[MaxSlots];
        private readonly int[] burnTiers = new int[MaxSlots];
        private readonly object[] burnItems = new object[MaxSlots];
        private int burnCount;
        private float burnTime = -1f;

        /// <summary>
        /// 매 프레임 부른다. 화면이 새로 열리면 처음부터 다시 떨어뜨린다.
        /// hoveredSlot은 마우스 아래 카드 번호(없으면 -1). 화면이 이번에 새로 열렸으면 true.
        /// </summary>
        public bool Update(float dt, bool open, int hoveredSlot)
        {
            bool opened = open && !wasOpen;
            if (opened)
            {
                openTime = 0f;
                Array.Clear(hover);
            }

            wasOpen = open;
            pickTimer = Math.Max(0f, pickTimer - dt);
            if (burnTime >= 0f)
            {
                burnTime += dt;
                if (burnTime > BurnTotal)
                {
                    burnTime = -1f;
                    burnCount = 0;
                    Array.Clear(burnItems);
                }
            }

            if (!open)
            {
                return opened;
            }

            openTime += dt;
            float k = Math.Min(1f, dt * HoverSpeed);
            for (int i = 0; i < MaxSlots; i++)
            {
                // 아직 떨어지는 중인 카드는 떠오르지 않는다.
                float target = i == hoveredSlot && Drop(i) >= 1f ? 1f : 0f;
                hover[i] += (target - hover[i]) * k;
            }

            return opened;
        }

        /// <summary>떨어지기 시작을 seconds만큼 미룬다(앞 화면 카드가 타는 동안 기다릴 때).</summary>
        public void Hold(float seconds)
        {
            openTime = -Math.Max(0f, seconds);
        }

        /// <summary>고른 카드의 번쩍임을 시작한다. rect는 카드 몸통.</summary>
        public void Pick(RectangleF rect, Color accent, int tier)
        {
            pickRect = rect;
            pickColor = accent;
            pickTier = tier;
            pickTimer = PickDuration;
            burnCount = 0;
            burnTime = 0f;
        }

        /// <summary>고른 카드 없이 카드를 태우기 시작한다(다시 뽑기·건너뛰기). 뒤이어 Burn으로 카드를 넣는다.</summary>
        public void BeginBurn()
        {
            burnCount = 0;
            burnTime = 0f;
        }

        /// <summary>고르지 않은 카드 한 장을 태운다. Pick 뒤에 부른다. item은 화면이 내용을 다시 그릴 때 쓴다.</summary>
        public void Burn(RectangleF rect, Color accent, int tier, object item)
        {
            if (burnCount >= MaxSlots)
            {
                return;
            }

            burnRects[burnCount] = rect;
            burnColors[burnCount] = accent;
            burnTiers[burnCount] = tier;
            burnItems[burnCount] = item;
            burnCount++;
        }

        /// <summary>타는 카드 수(다 타면 0).</summary>
        public int BurnCount => burnTime >= 0f ? burnCount : 0;

        /// <summary>모든 카드가 다 탈 때까지 남은 시간(초).</summary>
        public float BurnRemaining => burnTime >= 0f ? Math.Max(0f, BurnTotal - burnTime) : 0f;

        private float BurnTotal => BurnDuration + BurnStagger * Math.Max(0, burnCount - 1);

        /// <summary>
        /// 타는 카드 정보. progress는 0(불이 막 붙음)~1(다 탐), lineY는 이 높이 아래가 타서 없어졌다는 뜻.
        /// </summary>
        public void GetBurn(int index, out RectangleF rect, out Color accent, out int tier, out object item, out float progress, out float lineY)
        {
            rect = burnRects[index];
            accent = burnColors[index];
            tier = burnTiers[index];
            item = burnItems[index];
            float t = Math.Max(0f, Math.Min(1f, (burnTime - index * BurnStagger) / BurnDuration));
            progress = t;

            // 밑에서 시작해 점점 빨리 타오른다. 위로 삐져나온 뿔·해골까지 다 태운다.
            float eased = t * (0.6f + 0.4f * t);
            float bottom = rect.Bottom + 6f;
            float top = rect.Top - 30f;
            lineY = bottom - (bottom - top) * eased;
        }

        /// <summary>타는 카드의 테두리 색조: 불이 붙을수록 달아오른다.</summary>
        public static Color BurnTint(float progress)
        {
            return Color.FromArgb(255, 255, (int)(255 - 70 * progress), (int)(255 - 140 * progress));
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

        /// <summary>카드가 떨어지기 시작했는지. 아직이면 그리지 않는다.</summary>
        public bool Visible(int slot)
        {
            return openTime - slot * DropStagger > 0f;
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
            CardArt.Draw(r, x, y, w, h, pickColor, new CardLook { Hover = 1f, Opacity = Math.Max(0.01f, fade), Tier = pickTier });
            r.DrawRectangle(x + 4f, y + 4f, w - 8f, h - 8f, Color.FromArgb((int)(220 * fade * fade), 255, 244, 225));
        }
    }
}
