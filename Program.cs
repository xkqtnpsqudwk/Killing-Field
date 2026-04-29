using System;
using System.Windows.Forms;

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
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
