using System.Collections.Generic;
using System.Windows.Forms;

namespace My2DEngine.Engine.Input
{
    /// <summary>
    /// WinForms 키보드 이벤트에서 수집한 키 상태를 프레임 폴링 방식으로 읽기 위한 정적 저장소다.
    /// Form의 KeyDown/KeyUp 이벤트가 발생할 때 각각 <see cref="KeyDown"/>/<see cref="KeyUp"/>을
    /// 호출하면, 게임 루프는 매 프레임 <see cref="GetKey"/>로 현재 눌린 키를 조회할 수 있다.
    /// </summary>
    public static class Input
    {
        /// <summary>
        /// 현재 눌려 있는 키를 추적하는 집합.
        /// HashSet은 중복 삽입을 자동으로 무시하므로 KeyDown이 여러 번 와도 안전하다.
        /// </summary>
        private static readonly HashSet<Keys> keys = new HashSet<Keys>();

        /// <summary>
        /// 키가 눌렸을 때 Form의 KeyDown 핸들러에서 호출한다.
        /// 이미 집합에 있으면 무시되어 상태가 중복 등록되지 않는다.
        /// </summary>
        /// <param name="key">눌린 키 코드</param>
        public static void KeyDown(Keys key)
        {
            keys.Add(key);
        }

        /// <summary>
        /// 키가 떼어졌을 때 Form의 KeyUp 핸들러에서 호출한다.
        /// 집합에 없으면 아무 효과가 없어 안전하게 호출할 수 있다.
        /// </summary>
        /// <param name="key">떼어진 키 코드</param>
        public static void KeyUp(Keys key)
        {
            keys.Remove(key);
        }

        /// <summary>
        /// 지정한 키가 현재 눌려 있는지 반환한다.
        /// 게임 루프에서 매 프레임 호출해 이동·발사·대시 등의 입력을 확인한다.
        /// </summary>
        /// <param name="key">조회할 키 코드</param>
        /// <returns>현재 눌려 있으면 true</returns>
        public static bool GetKey(Keys key)
        {
            return keys.Contains(key);
        }

        /// <summary>
        /// 모든 키 상태를 지운다.
        /// 창 포커스를 잃거나 게임을 재시작할 때 호출해 이전 키 상태가 남지 않게 한다.
        /// </summary>
        public static void Reset()
        {
            keys.Clear();
        }
    }
}
