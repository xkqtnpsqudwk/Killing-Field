using System;
using System.Drawing;

namespace My2DEngine.Engine.Rendering.Abstractions
{
    /// <summary>
    /// 게임 코드가 GDI·D3D11 구현을 직접 알지 못해도 렌더링할 수 있게 하는 공용 backend 계약이다.
    /// Renderer 클래스는 이 인터페이스에만 의존하므로 백엔드를 교체해도 게임 코드를 바꿀 필요가 없다.
    /// </summary>
    public interface IRenderBackend : IDisposable
    {
        /// <summary>이 백엔드가 레이캐스트 월드 렌더링을 GPU에서 처리할 수 있는지 여부.</summary>
        bool SupportsWorldRendering { get; }

        /// <summary>백엔드의 현재 상태나 초기화 오류 메시지. 디버그 HUD에 표시된다.</summary>
        string StatusMessage { get; }

        /// <summary>
        /// 프레임 렌더링을 시작한다. 렌더 타깃을 지정된 색으로 초기화(클리어)한다.
        /// </summary>
        /// <param name="clearColor">배경 초기화 색</param>
        void BeginFrame(Color clearColor);

        /// <summary>
        /// 프레임 렌더링을 마치고 화면에 제출(Present)한다.
        /// </summary>
        void EndFrame();

        /// <summary>
        /// 뷰포트 크기가 바뀔 때 호출해 렌더 타깃 등 크기 종속 리소스를 재구성한다.
        /// </summary>
        /// <param name="width">새 너비(픽셀)</param>
        /// <param name="height">새 높이(픽셀)</param>
        void Resize(int width, int height);

        /// <summary>
        /// 레이캐스트 월드(벽/바닥/천장/스프라이트 등)를 한 번에 그린다.
        /// GPU 경로를 지원하지 않으면 false를 반환해 상위에서 CPU 대체 경로를 선택하게 한다.
        /// </summary>
        /// <param name="command">렌더에 필요한 카메라·지오메트리·텍스처 데이터 묶음</param>
        /// <returns>GPU 경로로 성공적으로 그렸으면 true</returns>
        bool DrawWorld(RenderWorldCommand command);

        /// <summary>
        /// 색상으로 채워진 사각형을 오버레이에 그린다.
        /// </summary>
        void DrawRectangle(RenderRectCommand command);

        /// <summary>
        /// 이미지(비트맵)를 지정 위치·크기로 오버레이에 그린다.
        /// </summary>
        void DrawImage(RenderImageCommand command);

        /// <summary>
        /// 텍스트를 지정 위치·색·크기로 오버레이에 그린다.
        /// </summary>
        void DrawText(RenderTextCommand command);

        /// <summary>
        /// 지정 크기로 렌더링했을 때 텍스트가 차지할 픽셀 크기를 반환한다.
        /// DrawTextCentered처럼 텍스트를 중앙 정렬하기 전에 미리 크기를 알아야 할 때 사용한다.
        /// </summary>
        /// <param name="text">측정할 문자열</param>
        /// <param name="size">폰트 크기(픽셀)</param>
        /// <returns>텍스트의 너비·높이</returns>
        SizeF MeasureText(string text, float size);

        /// <summary>
        /// 층 전환 등 레벨 경계에서 호출된다.
        /// 백엔드는 이 시점에 VRAM을 많이 사용하는 일시적 캐시(글리프 아틀라스 등)를 비울 수 있다.
        /// </summary>
        void OnLevelTransition();
    }
}
