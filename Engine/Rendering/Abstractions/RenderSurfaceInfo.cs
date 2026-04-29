using System;

namespace My2DEngine.Engine.Rendering.Abstractions
{
    /// <summary>
    /// backend가 스왑체인·렌더 타깃을 생성할 때 필요한 창 핸들과 크기 정보다.
    /// GameHost가 Form 크기 변경을 감지하면 이 값을 업데이트하고 backend에 전달한다.
    /// </summary>
    public struct RenderSurfaceInfo
    {
        /// <summary>
        /// 렌더 대상 WinForms 창의 네이티브 핸들(HWND).
        /// DXGI 스왑체인 생성 시 이 핸들에 연결된다.
        /// IntPtr.Zero이면 backend 초기화가 거부된다.
        /// </summary>
        public IntPtr WindowHandle { get; set; }

        /// <summary>렌더 대상 너비(픽셀). 0 이하이면 backend 초기화가 거부된다.</summary>
        public int Width { get; set; }

        /// <summary>렌더 대상 높이(픽셀). 0 이하이면 backend 초기화가 거부된다.</summary>
        public int Height { get; set; }
    }
}
