using System.Drawing;
using System.Windows.Forms;
using My2DEngine.Engine.Input;

namespace My2DEngine
{
    /// <summary>
    /// Form1의 입력 처리 partial 클래스.
    /// WinForms 키보드/마우스 이벤트를 게임 상태 전환과 엔진 입력 저장소(<see cref="Input"/>)에 연결한다.
    /// </summary>
    public partial class Form1
    {
        /// <summary>
        /// 키를 눌렀을 때 호출되는 이벤트 핸들러.
        /// 엔진 입력 저장소에 키 상태를 기록하고, 특수 키(F3, ESC)에 대한 게임 상태 전환을 처리한다.
        /// 이미 눌린 상태인 키의 반복 입력은 상태 전환 로직에서 무시된다.
        /// </summary>
        /// <param name="sender">이벤트 발생원 (사용되지 않음).</param>
        /// <param name="e">눌린 키 정보가 담긴 이벤트 인수.</param>
        private void OnGameKeyDown(object sender, KeyEventArgs e)
        {
            bool wasAlreadyDown = Input.GetKey(e.KeyCode);
            Input.KeyDown(e.KeyCode);

            if (wasAlreadyDown)
            {
                return;
            }

            if (e.KeyCode == Keys.F3)
            {
                showDebugHud = !showDebugHud;
                RequestPaint(force: true);
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                currentStateHandler.HandleEscape();
                RequestPaint(force: true);
            }
        }

        /// <summary>
        /// 키를 뗐을 때 호출되는 이벤트 핸들러.
        /// 엔진 입력 저장소에서 해당 키의 눌림 상태를 해제한다.
        /// </summary>
        /// <param name="sender">이벤트 발생원 (사용되지 않음).</param>
        /// <param name="e">뗀 키 정보가 담긴 이벤트 인수.</param>
        private void OnGameKeyUp(object sender, KeyEventArgs e)
        {
            Input.KeyUp(e.KeyCode);
        }

        /// <summary>
        /// 마우스 버튼을 눌렀을 때 호출되는 이벤트 핸들러.
        /// 왼쪽 버튼 클릭만 처리하며, 현재 게임 상태에 따라 메뉴 클릭, 발사, 마우스 캡처 등을 수행한다.
        /// </summary>
        /// <param name="sender">이벤트 발생원 (사용되지 않음).</param>
        /// <param name="e">마우스 버튼과 위치 정보가 담긴 이벤트 인수.</param>
        private void OnGameMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            currentStateHandler.HandleLeftClick(e.Location);
        }

        /// <summary>
        /// 마우스 버튼을 뗐을 때 호출되는 이벤트 핸들러.
        /// 왼쪽 버튼을 떼면 설정 화면의 FOV/감도 슬라이더 드래그 상태를 해제한다.
        /// </summary>
        /// <param name="sender">이벤트 발생원 (사용되지 않음).</param>
        /// <param name="e">마우스 버튼과 위치 정보가 담긴 이벤트 인수.</param>
        private void OnGameMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                draggingFov = false;
                draggingSensitivity = false;
                draggingBgm = false;
                draggingSfx = false;

                currentStateHandler.HandleLeftMouseUp();
            }
        }

        /// <summary>
        /// 마우스가 이동했을 때 호출되는 이벤트 핸들러.
        /// 설정 화면에서는 슬라이더 드래그를 처리하고,
        /// 게임 플레이 중 마우스 캡처 상태에서는 중심 대비 이동량을 계산하여 시점 회전에 사용한다.
        /// 마우스를 중앙으로 강제 복귀시킨 직후의 이벤트는 suppressMouseMove 플래그로 무시된다.
        /// </summary>
        /// <param name="sender">이벤트 발생원 (사용되지 않음).</param>
        /// <param name="e">마우스 위치 정보가 담긴 이벤트 인수.</param>
        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            lastMousePosition = e.Location;

            currentStateHandler.HandleMouseMove(e);
        }

        /// <summary>
        /// 마우스 커서를 숨기고 클라이언트 영역 중앙에 고정하여 게임 플레이용 마우스 캡처를 시작한다.
        /// 이미 캡처 중이면 아무 작업도 하지 않는다.
        /// </summary>
        private void CaptureMouse()
        {
            if (mouseCaptured)
            {
                return;
            }

            mouseCaptured = true;
            Cursor.Hide();
            CenterMouse();
        }

        /// <summary>
        /// 마우스 캡처를 해제하고 커서를 다시 표시한다.
        /// 이미 해제된 상태이면 아무 작업도 하지 않는다.
        /// </summary>
        private void ReleaseMouse()
        {
            if (!mouseCaptured)
            {
                return;
            }

            mouseCaptured = false;
            Cursor.Show();
        }

        /// <summary>
        /// 마우스 커서를 클라이언트 영역 중앙으로 강제 이동시킨다.
        /// 이동 후 발생하는 MouseMove 이벤트를 무시하도록 suppressMouseMove를 true로 설정한다.
        /// 클라이언트 크기가 0 이하이면 아무 작업도 하지 않는다.
        /// </summary>
        private void CenterMouse()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
            {
                return;
            }

            suppressMouseMove = true;
            Cursor.Position = PointToScreen(GetClientCenter());
        }

        /// <summary>
        /// 클라이언트 영역의 중심 좌표를 반환한다.
        /// 마우스를 중앙에 고정할 때 기준점으로 사용된다.
        /// </summary>
        /// <returns>클라이언트 영역 중심 좌표 (클라이언트 좌표계).</returns>
        private Point GetClientCenter()
        {
            return new Point(ClientSize.Width / 2, ClientSize.Height / 2);
        }

        /// <summary>
        /// 자유 마우스 입력이 필요한 게임 오버레이 UI에 클릭 좌표를 전달한다.
        /// 좌표는 내부 게임 렌더 해상도(640x360) 기준으로 변환된다.
        /// </summary>
        private void NotifyWorldUiMouseClick(Point clientLocation)
        {
            TryHandleWorldOverlayClick(clientLocation);
        }
    }
}
