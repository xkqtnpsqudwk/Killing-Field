using System;
using System.IO;
using System.Windows.Forms;
using My2DEngine.Engine.Rendering;

namespace My2DEngine
{
    /// <summary>
    /// WinForms 애플리케이션의 진입점 클래스.
    /// 실제 게임 초기화는 <see cref="Form1"/>이, 게임 상태 초기화는 GameLogic이 담당한다.
    /// 진입점을 최대한 얇게 유지하여 시작 실패 지점을 추적하기 쉽도록 구성되어 있다.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// WinForms 초기화는 한 번만 수행하고, 이후의 수명주기 제어는 Form1에 넘긴다.
        /// 별도 bootstrapper를 두지 않는 대신 진입점을 최대한 얇게 유지해
        /// 시작 실패 지점을 추적하기 쉽도록 구성했다.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ConfigureOverlayFont();
            Application.Run(new Form1(ParseSeedArgument(args)));
        }

        /// <summary>UI 글꼴로 게임에 포함된 갈무리11(12px 픽셀 글꼴)을 쓴다. 파일이 없으면 엔진이 맑은 고딕으로 대체한다.</summary>
        private static void ConfigureOverlayFont()
        {
            string fontDir = Path.Combine(AppContext.BaseDirectory, "Game", "Fonts");
            OverlayFontSettings.UsePixelFont(
                "Galmuri11",
                12,
                Path.Combine(fontDir, "Galmuri11.ttf"),
                Path.Combine(fontDir, "Galmuri11-Bold.ttf"));
        }

        /// <summary>`--seed N` 인자가 있으면 그 값을, 없거나 잘못됐으면 null을 돌려준다.</summary>
        private static int? ParseSeedArgument(string[] args)
        {
            if (args == null)
            {
                return null;
            }

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "--seed", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(args[i + 1], out int seed))
                {
                    return seed;
                }
            }

            return null;
        }
    }
}
