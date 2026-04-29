using My2DEngine.Engine.Rendering.Abstractions;
using My2DEngine.Engine.Rendering.Backends.D3D11;

namespace My2DEngine.Engine.Rendering
{
    /// <summary>
    /// 렌더 백엔드 인스턴스 생성을 담당하는 정적 팩토리 클래스.
    /// 백엔드 생성 로직을 한 곳에 집중시켜 호출 측 코드를 단순하게 유지한다.
    /// </summary>
    public static class RenderBackendFactory
    {
        /// <summary>
        /// D3D11 렌더 백엔드 인스턴스를 생성하여 반환한다.
        /// presenter, 월드 렌더러, 오버레이는 내부에서 지연 초기화(lazy-init)된다.
        /// </summary>
        /// <param name="surfaceInfo">렌더링 대상 창 핸들과 해상도 정보.</param>
        /// <returns>초기화된 <see cref="D3D11RenderBackend"/> 인스턴스.</returns>
        public static D3D11RenderBackend CreateD3D11(RenderSurfaceInfo surfaceInfo)
        {
            return new D3D11RenderBackend(surfaceInfo);
        }
    }
}
