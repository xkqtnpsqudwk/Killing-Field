using System;
using System.Diagnostics;
using System.Drawing;
using My2DEngine.Engine.Rendering.Abstractions;
using My2DEngine.Engine.Rendering.Backends.D3D11.World;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using static Vortice.Direct3D11.D3D11;
using static Vortice.DXGI.DXGI;

namespace My2DEngine.Engine.Rendering.Backends.D3D11
{
    /// <summary>
    /// Direct3D11 장치, 스왑체인, 렌더 타깃, 각종 presenter의 수명주기를 한곳에 모은 핵심 자원 관리자입니다.
    /// 초기화/해제/리사이즈는 <c>D3D11DeviceResources.Lifecycle.cs</c>에,
    /// presenter 지연 초기화는 <c>D3D11DeviceResources.Presenters.cs</c>에 분리되어 있습니다.
    /// </summary>
    internal sealed partial class D3D11DeviceResources : IDisposable
    {
        /// <summary>
        /// Direct3D11 장치 생성 시 시도할 기능 수준 목록입니다. 높은 수준부터 순서대로 시도합니다.
        /// </summary>
        private static readonly FeatureLevel[] preferredFeatureLevels =
        {
            FeatureLevel.Level_11_1,
            FeatureLevel.Level_11_0,
            FeatureLevel.Level_10_1,
            FeatureLevel.Level_10_0
        };

        /// <summary>렌더링 대상 창 핸들과 표면 크기 정보입니다.</summary>
        private RenderSurfaceInfo surfaceInfo;

        /// <summary>이 객체의 Dispose 여부를 나타냅니다.</summary>
        private bool disposed;

        /// <summary>현재 BeginFrame~EndFrame 구간 내에 있는지 여부입니다.</summary>
        private bool frameActive;

        /// <summary>DXGI 팩토리입니다. 스왑체인 생성에 사용됩니다.</summary>
        private IDXGIFactory2 dxgiFactory;

        /// <summary>Direct3D11 논리 장치입니다. GPU 자원 생성에 사용됩니다.</summary>
        private ID3D11Device d3dDevice;

        /// <summary>Direct3D11 즉시 실행 컨텍스트입니다. 렌더링 명령을 GPU에 제출합니다.</summary>
        private ID3D11DeviceContext d3dContext;

        /// <summary>화면 출력을 위한 DXGI 스왑체인입니다.</summary>
        private IDXGISwapChain1 swapChain;

        /// <summary>스왑체인의 백 버퍼 텍스처입니다.</summary>
        private ID3D11Texture2D backBuffer;

        /// <summary>백 버퍼에 대한 렌더 타깃 뷰입니다. 모든 presenter가 이 뷰에 그립니다.</summary>
        private ID3D11RenderTargetView renderTargetView;

        /// <summary>레이캐스트 기반 GPU 월드 렌더링을 담당하는 presenter입니다.</summary>
        private D3D11WorldRaycastPresenter worldPresenter;

        /// <summary>적 스프라이트를 GPU로 합성하는 presenter입니다.</summary>
        private D3D11WorldSpritePresenter enemyWorldSpritePresenter;

        /// <summary>기타 스프라이트(아이템, 파티클 등)를 GPU로 합성하는 presenter입니다.</summary>
        private D3D11WorldSpritePresenter miscWorldSpritePresenter;

        /// <summary>레이저 빔 효과를 GPU로 렌더링하는 presenter입니다.</summary>
        private D3D11WorldBeamPresenter worldBeamPresenter;

        /// <summary>UI 오버레이(사각형, 이미지, 텍스트)를 GPU 쿼드로 그리는 presenter입니다.</summary>
        private D3D11OverlayPresenter overlayPresenter;

        /// <summary>GPU 월드 렌더링 경로가 활성화되어 있는지 여부입니다. 런타임 실패 시 <c>false</c>로 전환됩니다.</summary>
        private bool worldRenderingEnabled = true;

        /// <summary>현재 프레임에서 CPU 업로드에 소요된 시간(밀리초)입니다.</summary>
        private float currentCpuUploadMs;

        /// <summary>현재 프레임에서 월드 렌더링 패스에 소요된 시간(밀리초)입니다.</summary>
        private float currentWorldMs;

        /// <summary>현재 프레임에서 스프라이트 패스에 소요된 시간(밀리초)입니다.</summary>
        private float currentSpriteMs;

        /// <summary>현재 프레임에서 오버레이 패스에 소요된 시간(밀리초)입니다.</summary>
        private float currentOverlayMs;

        /// <summary>지수 평활된 CPU 업로드 소요 시간(밀리초)입니다. HUD 표시에 사용됩니다.</summary>
        private float smoothedCpuUploadMs;

        /// <summary>지수 평활된 월드 패스 소요 시간(밀리초)입니다.</summary>
        private float smoothedWorldMs;

        /// <summary>지수 평활된 스프라이트 패스 소요 시간(밀리초)입니다.</summary>
        private float smoothedSpriteMs;

        /// <summary>지수 평활된 오버레이 패스 소요 시간(밀리초)입니다.</summary>
        private float smoothedOverlayMs;

        /// <summary>지수 평활된 스왑체인 Present 소요 시간(밀리초)입니다.</summary>
        private float smoothedPresentMs;

        /// <summary>마지막으로 요청된 월드 렌더 타깃 너비(픽셀)입니다. 진단 정보에 포함됩니다.</summary>
        private int lastWorldTargetWidth;

        /// <summary>마지막으로 요청된 월드 렌더 타깃 높이(픽셀)입니다. 진단 정보에 포함됩니다.</summary>
        private int lastWorldTargetHeight;

        /// <summary>마지막으로 렌더링된 월드의 컬럼(열) 수입니다. 진단 정보에 포함됩니다.</summary>
        private int lastColumnCount;

        /// <summary>마지막 프레임의 적 스프라이트 수입니다. 진단 정보에 포함됩니다.</summary>
        private int lastEnemySpriteCount;

        /// <summary>마지막 프레임의 기타 스프라이트 수입니다. 진단 정보에 포함됩니다.</summary>
        private int lastMiscSpriteCount;

        /// <summary>
        /// 지정된 표면 정보로 <see cref="D3D11DeviceResources"/>를 초기화하고 장치를 생성합니다.
        /// </summary>
        /// <param name="surfaceInfo">렌더링 대상 창 핸들과 표면 크기 정보입니다.</param>
        public D3D11DeviceResources(RenderSurfaceInfo surfaceInfo)
        {
            this.surfaceInfo = surfaceInfo;
            InitializeOrInvalidate();
        }

        /// <summary>Direct3D11 장치가 정상적으로 초기화되었는지 여부입니다.</summary>
        public bool IsInitialized { get; private set; }

        /// <summary>초기화 실패 시 그 이유를 설명하는 오류 메시지입니다. 성공 시 <c>null</c>입니다.</summary>
        public string InitializationError { get; private set; }

        /// <summary>현재 백엔드 상태를 설명하는 사람이 읽기 쉬운 메시지입니다.</summary>
        public string StatusMessage { get; private set; }

        /// <summary>
        /// GPU 월드 렌더링을 사용할 수 있는지 여부입니다.
        /// 초기화 완료와 월드 렌더링 활성화 상태가 모두 충족되어야 합니다.
        /// </summary>
        public bool SupportsWorldRendering => IsInitialized && worldRenderingEnabled;
        /// <summary>
        /// 새 프레임을 시작합니다. 렌더 타깃을 바인딩하고 뷰포트를 설정한 뒤 지정된 색으로 클리어합니다.
        /// 모든 presenter는 동일한 RTV/viewport를 공유하므로 프레임 시작 시점에 한 번만 초기화합니다.
        /// </summary>
        /// <param name="clearColor">렌더 타깃을 채울 배경색입니다.</param>
        public void BeginFrame(Color clearColor)
        {
            // 모든 presenter는 동일한 RTV/viewport를 공유하므로
            // 프레임 시작 시점에 출력 타깃과 진단 누적값을 한 번만 초기화한다.
            EnsureReady("begin a frame");
            if (frameActive)
            {
                return;
            }

            d3dContext.OMSetRenderTargets(renderTargetView, null);
            d3dContext.RSSetViewport(0f, 0f, surfaceInfo.Width, surfaceInfo.Height, 0f, 1f);
            d3dContext.ClearRenderTargetView(renderTargetView, clearColor);
            currentCpuUploadMs = 0f;
            currentWorldMs = 0f;
            currentSpriteMs = 0f;
            currentOverlayMs = 0f;
            frameActive = true;
        }

        /// <summary>
        /// 현재 프레임을 종료하고 스왑체인을 통해 화면에 출력합니다.
        /// 진단 수치는 프레임마다 튀지 않도록 지수 평활을 적용한 뒤 HUD에 노출합니다.
        /// </summary>
        public void EndFrame()
        {
            // 진단 수치는 프레임마다 튀지 않도록 지수 평활 후 HUD에 노출한다.
            EnsureReady("end a frame");
            if (!frameActive)
            {
                return;
            }
            long presentStart = Stopwatch.GetTimestamp();
            swapChain.Present(1, PresentFlags.None).CheckError();
            float presentMs = ElapsedMilliseconds(presentStart);
            UpdateSmoothedMetric(ref smoothedCpuUploadMs, currentCpuUploadMs);
            UpdateSmoothedMetric(ref smoothedWorldMs, currentWorldMs);
            UpdateSmoothedMetric(ref smoothedSpriteMs, currentSpriteMs);
            UpdateSmoothedMetric(ref smoothedOverlayMs, currentOverlayMs);
            UpdateSmoothedMetric(ref smoothedPresentMs, presentMs);
            frameActive = false;
        }

        /// <summary>
        /// 프레임 진행 중에 발생한 런타임 실패를 기록하고 장치를 무효화합니다.
        /// 초기화 실패와 달리 "한 번은 성공했지만 프레임 도중 깨진 상황"을 처리합니다.
        /// presenter 자원을 즉시 해제하여 다음 재초기화 시도에서 안전하게 재생성할 수 있도록 합니다.
        /// </summary>
        /// <param name="action">실패한 작업을 설명하는 짧은 문자열입니다.</param>
        /// <param name="ex">발생한 예외입니다.</param>
        public void InvalidateRuntimeFailure(string action, Exception ex)
        {
            // runtime failure는 초기화 실패와 달리 "한 번은 됐지만 프레임 중 깨진 상황"이다.
            // presenter 자원은 즉시 내려서 다음 Resolve에서 안전하게 재생성할 수 있게 한다.
            frameActive = false;
            ReleasePresenterResources();
            IsInitialized = false;
            InitializationError = "Direct3D11 runtime failure while trying to " + action + ": " + ex.Message;
            StatusMessage = InitializationError;
        }

        /// <summary>
        /// GPU 월드 렌더링 패스(레이캐스트, 스프라이트, 빔)를 실행합니다.
        /// 성공/실패만 명확히 반환하고 호출부가 다음 경로를 선택하게 합니다.
        /// 런타임 실패 시 월드 GPU 경로만 비활성화합니다.
        /// </summary>
        /// <param name="command">레이캐스트, 스프라이트, 빔 데이터가 담긴 월드 렌더 명령입니다.</param>
        /// <returns>GPU 월드 렌더링이 성공하면 <c>true</c>, 비활성화되었거나 실패하면 <c>false</c>입니다.</returns>
        public bool DrawWorld(RenderWorldCommand command)
        {
            // world draw는 성공/실패만 분명히 돌려주고 호출부가 다음 경로를 선택하게 한다.
            EnsureReady("draw world");
            if (!frameActive || !worldRenderingEnabled)
            {
                return false;
            }

            try
            {
                EnsureWorldPresenters();
                lastWorldTargetWidth = command.TargetWidth;
                lastWorldTargetHeight = command.TargetHeight;
                lastColumnCount = command.ColumnCount;
                lastEnemySpriteCount = command.EnemySpritePass.SpriteCount;
                lastMiscSpriteCount = command.MiscSpritePass.SpriteCount;
                long worldStart = Stopwatch.GetTimestamp();
                worldPresenter.RenderScene(d3dContext, command);
                currentWorldMs = ElapsedMilliseconds(worldStart);
                worldPresenter.CompositeScene(d3dContext, renderTargetView, surfaceInfo.Width, surfaceInfo.Height, command);
                if (command.EnemySpritePass.Sprites != null && command.EnemySpritePass.SpriteCount > 0)
                {
                    EnsureEnemyWorldSpritePresenter();
                    long spriteStart = Stopwatch.GetTimestamp();
                    enemyWorldSpritePresenter.Present(
                        d3dContext,
                        renderTargetView,
                        surfaceInfo.Width,
                        surfaceInfo.Height,
                        command.EnemySpritePass);
                    currentSpriteMs = ElapsedMilliseconds(spriteStart);
                }
                else
                {
                    enemyWorldSpritePresenter?.ReleaseTransientResources();
                    currentSpriteMs = 0f;
                }
                if (command.MiscSpritePass.Sprites != null && command.MiscSpritePass.SpriteCount > 0)
                {
                    EnsureMiscWorldSpritePresenter();
                    long miscSpriteStart = Stopwatch.GetTimestamp();
                    miscWorldSpritePresenter.Present(
                        d3dContext,
                        renderTargetView,
                        surfaceInfo.Width,
                        surfaceInfo.Height,
                        command.MiscSpritePass);
                    currentSpriteMs += ElapsedMilliseconds(miscSpriteStart);
                }
                else
                {
                    miscWorldSpritePresenter?.ReleaseTransientResources();
                }
                if (command.Beams != null && command.BeamCount > 0)
                {
                    EnsureWorldBeamPresenter();
                    long beamStart = Stopwatch.GetTimestamp();
                    worldBeamPresenter.Present(
                        d3dContext,
                        renderTargetView,
                        surfaceInfo.Width,
                        surfaceInfo.Height,
                        command);
                    currentSpriteMs += ElapsedMilliseconds(beamStart);
                }
                else
                {
                    worldBeamPresenter?.ReleaseTransientResources();
                }
                return true;
            }
            catch (Exception ex)
            {
                // 월드 패스 하나가 실패해도 presenter 전체를 무너뜨리면 즉시 flicker 루프가 생긴다.
                // 이 경우는 월드 GPU 경로만 끄고 상태를 남긴다.
                enemyWorldSpritePresenter?.Dispose();
                enemyWorldSpritePresenter = null;
                miscWorldSpritePresenter?.Dispose();
                miscWorldSpritePresenter = null;
                worldBeamPresenter?.Dispose();
                worldBeamPresenter = null;
                worldPresenter?.Dispose();
                worldPresenter = null;
                worldRenderingEnabled = false;
                StatusMessage =
                    "Direct3D11 GPU world rendering was disabled after a runtime failure. " +
                    "Stage=" + GetWorldFailureStage(ex) + ", " +
                    "Target=" + command.TargetWidth + "x" + command.TargetHeight +
                    ", Cols=" + command.ColumnCount +
                    ", EnemySprites=" + command.EnemySpritePass.SpriteCount +
                    ", MiscSprites=" + command.MiscSpritePass.SpriteCount +
                    ", Beams=" + command.BeamCount +
                    ". " + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 월드 렌더링 실패 시 스택 트레이스를 분석하여 어느 단계에서 실패했는지 설명하는 문자열을 반환합니다.
        /// 진단 메시지에 포함되어 실패 원인을 빠르게 파악하는 데 사용됩니다.
        /// </summary>
        /// <param name="ex">분석할 예외입니다.</param>
        /// <returns>실패 단계를 설명하는 문자열(예: "render scene", "present sprites")입니다.</returns>
        private static string GetWorldFailureStage(Exception ex)
        {
            if (ex == null || string.IsNullOrEmpty(ex.StackTrace))
            {
                return "unknown";
            }

            string stack = ex.StackTrace;
            if (stack.Contains("CompositeScene"))
            {
                return "composite scene";
            }

            if (stack.Contains("D3D11WorldBeamPresenter"))
            {
                return "present beams";
            }

            if (stack.Contains("D3D11WorldSpritePresenter"))
            {
                return "present sprites";
            }

            if (stack.Contains("RenderScene"))
            {
                return "render scene";
            }

            return "unknown";
        }

        /// <summary>
        /// 지정된 사각형을 GPU 오버레이 패스로 그립니다.
        /// </summary>
        /// <param name="command">위치, 크기, 색상 정보가 담긴 사각형 렌더 명령입니다.</param>
        public void DrawRectangle(RenderRectCommand command)
        {
            EnsureReady("draw a rectangle");
            if (!frameActive)
            {
                return;
            }

            EnsureOverlayPresenter();
            long overlayStart = Stopwatch.GetTimestamp();
            overlayPresenter.DrawRectangle(d3dContext, renderTargetView, surfaceInfo.Width, surfaceInfo.Height, command);
            currentOverlayMs += ElapsedMilliseconds(overlayStart);
        }

        /// <summary>
        /// 지정된 이미지를 GPU 오버레이 패스로 그립니다.
        /// </summary>
        /// <param name="command">이미지, 위치, 크기 정보가 담긴 이미지 렌더 명령입니다.</param>
        public void DrawImage(RenderImageCommand command)
        {
            EnsureReady("draw an image");
            if (!frameActive)
            {
                return;
            }

            EnsureOverlayPresenter();
            long overlayStart = Stopwatch.GetTimestamp();
            overlayPresenter.DrawImage(d3dContext, renderTargetView, surfaceInfo.Width, surfaceInfo.Height, command);
            currentOverlayMs += ElapsedMilliseconds(overlayStart);
        }

        /// <summary>
        /// 지정된 텍스트를 GPU 오버레이 패스로 그립니다.
        /// </summary>
        /// <param name="command">텍스트 내용, 위치, 크기, 색상 정보가 담긴 텍스트 렌더 명령입니다.</param>
        public void DrawText(RenderTextCommand command)
        {
            EnsureReady("draw text");
            if (!frameActive)
            {
                return;
            }

            EnsureOverlayPresenter();
            long overlayStart = Stopwatch.GetTimestamp();
            try
            {
                overlayPresenter.DrawText(d3dContext, renderTargetView, surfaceInfo.Width, surfaceInfo.Height, command);
            }
            catch
            {
                ResetOverlayPresenter();
                EnsureOverlayPresenter();
                overlayPresenter.DrawText(d3dContext, renderTargetView, surfaceInfo.Width, surfaceInfo.Height, command);
            }
            currentOverlayMs += ElapsedMilliseconds(overlayStart);
        }

        /// <summary>
        /// 지정된 텍스트를 렌더링했을 때의 크기를 측정합니다.
        /// </summary>
        /// <param name="text">크기를 측정할 텍스트입니다.</param>
        /// <param name="size">텍스트의 폰트 크기(포인트)입니다.</param>
        /// <returns>측정된 텍스트의 너비와 높이입니다.</returns>
        public SizeF MeasureText(string text, float size)
        {
            EnsureReady("measure text");
            EnsureOverlayPresenter();
            try
            {
                return overlayPresenter.MeasureText(text, size);
            }
            catch
            {
                ResetOverlayPresenter();
                EnsureOverlayPresenter();
                return overlayPresenter.MeasureText(text, size);
            }
        }

        /// <summary>
        /// 글리프 아틀라스 GPU 텍스처를 모두 해제하고 패킹 상태를 초기화합니다.
        /// overlayPresenter가 아직 생성되지 않은 경우에는 아무 동작도 하지 않습니다.
        /// </summary>
        public void FlushGlyphAtlas()
        {
            overlayPresenter?.FlushGlyphAtlas();
        }

        /// <summary>
        /// 렌더 표면의 크기를 변경합니다.
        /// 가능하면 스왑체인 버퍼만 재구성하고, 실패 시 전체 장치를 재초기화합니다.
        /// </summary>
        /// <param name="width">새 표면 너비(픽셀)입니다.</param>
        /// <param name="height">새 표면 높이(픽셀)입니다.</param>
        public void Resize(int width, int height)
        {
            ThrowIfDisposed();
            if (surfaceInfo.Width == width && surfaceInfo.Height == height)
            {
                return;
            }

            surfaceInfo.Width = width;
            surfaceInfo.Height = height;
            if (IsInitialized && swapChain != null && d3dDevice != null && d3dContext != null && width > 0 && height > 0)
            {
                try
                {
                    ResizeSwapChainBuffers();
                    return;
                }
                catch (Exception ex)
                {
                    ReleasePresenterResources();
                    IsInitialized = false;
                    InitializationError = "Direct3D11 resize failed: " + ex.Message;
                    StatusMessage = InitializationError;
                }
            }

            InitializeOrInvalidate();
        }

        /// <summary>
        /// 이 객체가 보유한 모든 Direct3D11 자원을 해제합니다.
        /// presenter, 렌더 타깃, 스왑체인, 장치를 역순으로 해제합니다.
        /// </summary>
        public void Dispose()
        {
            ReleasePresenterResources();
            disposed = true;
            frameActive = false;
            IsInitialized = false;
            InitializationError = "Direct3D11 device resources were disposed.";
            StatusMessage = InitializationError;
        }

        /// <summary>
        /// 현재 렌더 상태와 성능 수치를 <see cref="D3D11RenderDiagnostics"/> 구조체로 캡처합니다.
        /// 각 패스별 지수 평활 소요 시간, 스프라이트 수, 렌더 타깃 크기 등이 포함됩니다.
        /// </summary>
        /// <param name="pathDescription">현재 렌더 경로를 설명하는 문자열입니다.</param>
        /// <returns>렌더 상태와 성능 수치가 담긴 진단 구조체입니다.</returns>
        public D3D11RenderDiagnostics CaptureDiagnostics(string pathDescription)
        {
            return new D3D11RenderDiagnostics
            {
                PathDescription = pathDescription,
                CpuUploadMs = smoothedCpuUploadMs,
                WorldMs = smoothedWorldMs,
                SpriteMs = smoothedSpriteMs,
                OverlayMs = smoothedOverlayMs,
                PresentMs = smoothedPresentMs,
                WorldTargetWidth = lastWorldTargetWidth,
                WorldTargetHeight = lastWorldTargetHeight,
                ColumnCount = lastColumnCount,
                SpriteCount = lastEnemySpriteCount + lastMiscSpriteCount
            };
        }

        /// <summary>
        /// 장치 자원이 사용 가능한 상태인지 검증합니다.
        /// Dispose 이후 또는 초기화가 실패한 상태에서 호출하면 예외를 던집니다.
        /// </summary>
        /// <param name="action">시도하려는 작업명입니다. 오류 메시지에 포함됩니다.</param>
        /// <exception cref="ObjectDisposedException">이 객체가 이미 Dispose된 경우 발생합니다.</exception>
        /// <exception cref="InvalidOperationException">장치가 초기화되지 않은 경우 발생합니다.</exception>
        private void EnsureReady(string action)
        {
            ThrowIfDisposed();

            if (!IsInitialized)
            {
                throw new InvalidOperationException(
                    InitializationError ??
                    $"Direct3D11 device resources are not initialized and cannot {action}.");
            }
        }

        /// <summary>
        /// 이 객체가 Dispose된 경우 <see cref="ObjectDisposedException"/>를 던집니다.
        /// </summary>
        /// <exception cref="ObjectDisposedException">이 객체가 이미 Dispose된 경우 발생합니다.</exception>
        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(D3D11DeviceResources));
            }
        }

        /// <summary>
        /// 지정된 시작 타임스탬프로부터 현재까지 경과한 시간을 밀리초 단위로 반환합니다.
        /// </summary>
        /// <param name="startTimestamp"><see cref="Stopwatch.GetTimestamp"/>로 얻은 시작 타임스탬프입니다.</param>
        /// <returns>경과 시간(밀리초)입니다.</returns>
        private static float ElapsedMilliseconds(long startTimestamp)
        {
            return (float)((Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency);
        }

        /// <summary>
        /// 지수 평활 필터를 적용하여 성능 수치를 부드럽게 갱신합니다.
        /// 기존 값이 거의 0이면 첫 샘플을 그대로 사용합니다.
        /// </summary>
        /// <param name="target">갱신할 평활 수치 필드입니다.</param>
        /// <param name="sample">이번 프레임의 측정값입니다.</param>
        private static void UpdateSmoothedMetric(ref float target, float sample)
        {
            const float smoothing = 0.18f;
            if (target <= 0.0001f)
            {
                target = sample;
                return;
            }

            target += (sample - target) * smoothing;
        }
    }
}
