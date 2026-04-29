using System;

namespace My2DEngine.Engine.Rendering.Backends.D3D11
{
    /// <summary>
    /// D3D11 런타임 의존 어셈블리(Vortice.Direct3D11, Vortice.DXGI)가
    /// 실제로 로드 가능한지 가볍게 탐지하는 정적 프로브다.
    /// 실제 장치 생성을 시도하지 않고 어셈블리 로드 성공 여부만 확인하므로 빠르게 실패할 수 있다.
    /// GameHost의 ResolveBackend가 실제 backend 생성 전에 이 프로브를 호출해
    /// 의존 DLL이 없는 환경에서 불필요한 예외를 피한다.
    /// </summary>
    internal static class D3D11BackendProbe
    {
        /// <summary>
        /// D3D11 런타임 의존성이 모두 사용 가능하면 true를 반환한다.
        /// </summary>
        public static bool IsRuntimeAvailable()
        {
            return GetMissingDependency() == null;
        }

        /// <summary>
        /// 누락된 의존 어셈블리 이름을 반환한다.
        /// 모두 사용 가능하면 null을 반환한다.
        /// 예외가 발생하면 Vortice 패키지 중 하나 이상이 없는 것으로 간주한다.
        /// </summary>
        public static string GetMissingDependency()
        {
            try
            {
                CheckDependencies();
                return null;
            }
            catch (Exception)
            {
                return "Vortice.Direct3D11 or Vortice.DXGI";
            }
        }

        /// <summary>
        /// Vortice 의존 타입을 직접 참조해 어셈블리 로드를 강제로 시도한다.
        /// NoInlining을 적용해 이 메서드 진입 시점에 의존 DLL 로드가 발생하게 하여
        /// 외부 try-catch가 TypeLoadException 등을 잡을 수 있게 한다.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void CheckDependencies()
        {
            var type1 = typeof(Vortice.Direct3D11.ID3D11Device);
            var type2 = typeof(Vortice.DXGI.IDXGISwapChain);
        }
    }
}