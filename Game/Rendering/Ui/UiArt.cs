using System;
using System.Drawing;
using System.IO;

namespace My2DEngine.Game.Rendering.Ui
{
    /// <summary>
    /// UI 그림 조각(Game/Images/Ui). Tools/ArtGen/ui.py가 만든다.
    /// 파일이 없으면 null이고, 그리는 쪽은 예전 사각형 테두리로 돌아간다.
    /// 숫자는 ui.py의 조각 크기와 맞아야 한다.
    /// </summary>
    public static class UiArt
    {
        /// <summary>패널 테두리(48×48). 9칸 테두리 14, 안쪽 10칸부터 내용.</summary>
        public static readonly Image Panel = Load("panel.png");
        public const float PanelBorder = 14f;
        public const float PanelInset = 10f;

        /// <summary>카드 테두리(80×80). 바깥 7칸은 뿔 자리, 9칸 테두리 28, 몸통 안쪽 11칸부터 내용.</summary>
        public static readonly Image Card = Load("card.png");
        public static readonly Image CardTrim = Load("card_trim.png");
        public const float CardBorder = 28f;
        public const float CardMargin = 7f;
        public const float CardInset = 11f;

        /// <summary>버튼 테두리(24×24). 9칸 테두리 6, 안쪽 4칸부터 내용. 홈은 trim으로 빛난다.</summary>
        public static readonly Image Button = Load("button.png");
        public static readonly Image ButtonTrim = Load("button_trim.png");
        public const float ButtonBorder = 6f;
        public const float ButtonInset = 4f;

        /// <summary>HUD 상자 테두리(16×16). 9칸 테두리 5, 안쪽 4칸부터 내용.</summary>
        public static readonly Image Hud = Load("hud.png");
        public const float HudBorder = 5f;
        public const float HudInset = 4f;

        /// <summary>제목 명판(64×22). 양 끝 18칸은 그대로, 가운데만 늘린다.</summary>
        public static readonly Image Plaque = Load("plaque.png");
        public const float PlaqueCap = 18f;
        public const float PlaqueHeight = 22f;

        /// <summary>카드 위 해골 장식(32×26)과 눈(흰색, 강조 색을 곱한다).</summary>
        public static readonly Image Skull = Load("skull.png");
        public static readonly Image SkullEyes = Load("skull_eyes.png");
        public const float SkullWidth = 32f;
        public const float SkullHeight = 26f;

        /// <summary>등급 보석(9×9, 흰색 면). 초록 등급 이상 카드의 모서리 볼트 자리에 박는다.</summary>
        public static readonly Image Gem = Load("gem.png");
        public const float GemSize = 9f;

        /// <summary>악마 뿔(60×24). 보라 등급 이상 카드의 해골 뒤에 붙인다. 뿌리는 그림 (30±8, 17).</summary>
        public static readonly Image Horns = Load("horns.png");
        public const float HornsWidth = 60f;
        public const float HornsHeight = 24f;

        /// <summary>옆구리 칼날(16×26). 파랑 등급 이상 카드 양옆에 박는다.</summary>
        public static readonly Image SpikeLeft = Load("spike_l.png");
        public static readonly Image SpikeRight = Load("spike_r.png");
        public const float SpikeWidth = 16f;
        public const float SpikeHeight = 26f;

        /// <summary>빛 번짐(32×32, 흰색). 9칸 테두리 15.</summary>
        public static readonly Image Glow = Load("glow.png");
        public const float GlowBorder = 15f;

        private static Image Load(string fileName)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Game", "Images", "Ui", fileName);
                if (!File.Exists(path))
                {
                    return null;
                }

                // 파일을 잠그지 않도록 메모리로 복사해 둔다.
                using (FileStream stream = File.OpenRead(path))
                using (Image source = Image.FromStream(stream))
                {
                    return new Bitmap(source);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
