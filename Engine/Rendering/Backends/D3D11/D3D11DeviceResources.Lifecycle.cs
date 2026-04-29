using System;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using static Vortice.Direct3D11.D3D11;
using static Vortice.DXGI.DXGI;

namespace My2DEngine.Engine.Rendering.Backends.D3D11
{
    /// <summary>
    /// <see cref="D3D11DeviceResources"/>의 초기화, 리사이즈, 해제 경로를 담당하는 부분 클래스입니다.
    /// presenter draw 코드와 장치 수명주기 코드를 독립적으로 읽고 수정할 수 있도록 분리되어 있습니다.
    /// </summary>
    internal sealed partial class D3D11DeviceResources
    {
        /// <summary>
        /// 런타임 조건을 확인한 뒤 Direct3D11 장치를 초기화하거나, 조건이 불충족이면 무효화합니다.
        /// 창 핸들 누락, 렌더 크기 불량, 런타임 DLL 누락, Dispose 상태 등을 사전에 확인합니다.
        /// </summary>
        private void InitializeOrInvalidate()
        {
            if (disposed)
            {
                ReleasePresenterResources();
                IsInitialized = false;
                InitializationError = "Direct3D11 device resources were disposed.";
                StatusMessage = InitializationError;
                return;
            }

            if (!D3D11BackendProbe.IsRuntimeAvailable())
            {
                ReleasePresenterResources();
                IsInitialized = false;
                InitializationError = "Direct3D11 backend dependencies are unavailable. Missing assembly: " +
                    D3D11BackendProbe.GetMissingDependency();
                StatusMessage = InitializationError;
                return;
            }

            if (surfaceInfo.WindowHandle == IntPtr.Zero)
            {
                ReleasePresenterResources();
                IsInitialized = false;
                InitializationError = "Direct3D11 device resources require a valid window handle.";
                StatusMessage = InitializationError;
                return;
            }

            if (surfaceInfo.Width <= 0 || surfaceInfo.Height <= 0)
            {
                ReleasePresenterResources();
                IsInitialized = false;
                InitializationError = "Direct3D11 device resources require a valid render size.";
                StatusMessage = InitializationError;
                return;
            }

            try
            {
                RebuildPresenterResources();
                IsInitialized = true;
                worldRenderingEnabled = true;
                InitializationError = null;
                StatusMessage =
                    "Direct3D11 device resources were initialized. Device, swap chain, and render target are active and " +
                    "ready for a Vortice-backed presenter.";
            }
            catch (Exception ex)
            {
                ReleasePresenterResources();
                IsInitialized = false;
                InitializationError = "Direct3D11 initialization failed: " + ex.Message;
                StatusMessage = InitializationError;
            }
        }

        /// <summary>
        /// 기존 presenter 자원을 모두 해제하고 장치, 스왑체인, 렌더 타깃을 처음부터 다시 생성합니다.
        /// 부분 교체보다 전체 재구성을 선택하여 미세한 상태 누수를 방지합니다.
        /// </summary>
        private void RebuildPresenterResources()
        {
            // 리빌드는 부분 교체보다 전체 재구성을 택한다.
            // DX11 초기화 문제는 미세한 상태 누수보다 완전 재생성이 더 안전하다.
            frameActive = false;
            ReleasePresenterResources();
            CreateDevice();
            CreateSwapChain();
            CreateRenderTarget();
        }

        /// <summary>
        /// Direct3D11 하드웨어 장치와 즉시 실행 컨텍스트를 생성합니다.
        /// </summary>
        private void CreateDevice()
        {
            D3D11CreateDevice(
                IntPtr.Zero,
                DriverType.Hardware,
                DeviceCreationFlags.BgraSupport,
                preferredFeatureLevels,
                out d3dDevice,
                out _,
                out d3dContext).CheckError();
        }

        /// <summary>
        /// DXGI 팩토리를 생성하고 창 핸들에 연결된 스왑체인을 만듭니다.
        /// 창 모드 flicker를 줄이기 위해 FlipDiscard 기반 스왑체인을 사용합니다.
        /// Alt+Enter 전체화면 전환을 억제하기 위해 <c>IgnoreAltEnter</c>를 설정합니다.
        /// </summary>
        private void CreateSwapChain()
        {
            // 창 모드 flicker를 줄이기 위해 현재 기준선은 flip discard 기반 swap chain을 사용한다.
            dxgiFactory = CreateDXGIFactory2<IDXGIFactory2>(false);
            var swapChainDescription = new SwapChainDescription1(
                surfaceInfo.Width,
                surfaceInfo.Height,
                Format.B8G8R8A8_UNorm,
                false,
                Usage.RenderTargetOutput,
                3,
                Scaling.Stretch,
                SwapEffect.FlipDiscard,
                AlphaMode.Ignore,
                SwapChainFlags.None);

            swapChain = dxgiFactory.CreateSwapChainForHwnd(
                d3dDevice,
                surfaceInfo.WindowHandle,
                swapChainDescription,
                null,
                null);
            dxgiFactory.MakeWindowAssociation(surfaceInfo.WindowHandle, WindowAssociationFlags.IgnoreAltEnter);
        }

        /// <summary>
        /// 스왑체인의 백 버퍼를 가져와 렌더 타깃 뷰를 생성합니다.
        /// 모든 presenter는 이 뷰를 공유하여 렌더링합니다.
        /// </summary>
        private void CreateRenderTarget()
        {
            backBuffer = swapChain.GetBuffer<ID3D11Texture2D>(0);
            renderTargetView = d3dDevice.CreateRenderTargetView(backBuffer, null);
        }

        /// <summary>
        /// 스왑체인 버퍼를 새 크기에 맞게 재구성합니다.
        /// 장치와 컨텍스트는 유지하고 백 버퍼와 렌더 타깃 뷰만 교체합니다.
        /// presenter의 지연 초기화 상태는 그대로 유지되며, 다음 draw 요청 시에만 재생성됩니다.
        /// </summary>
        private void ResizeSwapChainBuffers()
        {
            // resize 이후에도 이전 presenter를 그대로 재사용하면
            // swap chain 교체 전 상태를 참조하는 텍스트/D2D 리소스가 남을 수 있다.
            // 장치는 유지하고 presenter만 내려 안전하게 다시 lazy-init한다.
            frameActive = false;
            ResetAllPresenters();
            d3dContext.OMSetRenderTargets((ID3D11RenderTargetView)null, null);
            renderTargetView?.Dispose();
            renderTargetView = null;
            backBuffer?.Dispose();
            backBuffer = null;
            swapChain.ResizeBuffers(0, surfaceInfo.Width, surfaceInfo.Height, Format.Unknown, SwapChainFlags.None).CheckError();
            CreateRenderTarget();
            IsInitialized = true;
            worldRenderingEnabled = true;
            InitializationError = null;
            StatusMessage = "Direct3D11 device resources resized successfully.";
        }

        /// <summary>
        /// presenter부터 장치까지 모든 Direct3D11 자원을 역순으로 안전하게 해제합니다.
        /// 해제 순서는 "presenter → RTV/백 버퍼 → 스왑체인 → 장치/컨텍스트 → DXGI 팩토리"입니다.
        /// 뒤쪽 자원이 앞쪽 자원을 참조하므로 역순 해제가 가장 안전합니다.
        /// </summary>
        private void ReleasePresenterResources()
        {
            // 해제 순서는 "presenter -> RTV/backbuffer -> swapchain -> device/context"를 따른다.
            // 뒤쪽 리소스가 앞쪽 리소스를 참조하는 구조라 역순 해제가 가장 안전하다.
            frameActive = false;
            overlayPresenter?.Dispose();
            overlayPresenter = null;
            worldBeamPresenter?.Dispose();
            worldBeamPresenter = null;
            enemyWorldSpritePresenter?.Dispose();
            enemyWorldSpritePresenter = null;
            miscWorldSpritePresenter?.Dispose();
            miscWorldSpritePresenter = null;
            worldPresenter?.Dispose();
            worldPresenter = null;
            renderTargetView?.Dispose();
            renderTargetView = null;
            backBuffer?.Dispose();
            backBuffer = null;
            swapChain?.Dispose();
            swapChain = null;
            d3dContext?.Dispose();
            d3dContext = null;
            d3dDevice?.Dispose();
            d3dDevice = null;
            dxgiFactory?.Dispose();
            dxgiFactory = null;
        }
    }
}
