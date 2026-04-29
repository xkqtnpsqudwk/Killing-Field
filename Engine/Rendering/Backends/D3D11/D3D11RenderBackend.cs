using System;
using System.Drawing;
using My2DEngine.Engine.Rendering.Abstractions;

namespace My2DEngine.Engine.Rendering.Backends.D3D11
{
    /// <summary>
    /// Direct3D11 렌더 백엔드의 공개 진입점 래퍼 클래스입니다.
    /// "Direct3D11로 그릴 수 있다"는 사실만 외부에 노출하고,
    /// 장치 상태 변화와 세부 presenter 구성은 <see cref="D3D11DeviceResources"/>에 위임합니다.
    /// </summary>
    public sealed class D3D11RenderBackend : IRenderBackend
    {
        /// <summary>
        /// D3D11 장치, 스왑체인, 렌더 타깃 등 저수준 자원을 관리하는 내부 객체입니다.
        /// 실제 렌더 실패는 여기서 기록되고, 상위 GameHost가 다음 프레임에 재시도 여부를 결정합니다.
        /// </summary>
        private readonly D3D11DeviceResources deviceResources;

        /// <summary>이 객체의 Dispose 여부를 나타냅니다.</summary>
        private bool disposed;

        /// <summary>
        /// 지정된 렌더 표면 정보로 <see cref="D3D11RenderBackend"/>를 초기화합니다.
        /// </summary>
        /// <param name="surfaceInfo">렌더링 대상 창 핸들과 크기 정보입니다.</param>
        public D3D11RenderBackend(RenderSurfaceInfo surfaceInfo)
        {
            deviceResources = new D3D11DeviceResources(surfaceInfo);
        }

        /// <summary>GPU 월드 렌더링을 지원하는지 여부입니다. 장치 초기화 상태와 런타임 월드 경로 상태에 따라 결정됩니다.</summary>
        public bool SupportsWorldRendering => deviceResources.SupportsWorldRendering;

        /// <summary>Direct3D11 장치가 정상적으로 초기화되었는지 여부입니다.</summary>
        public bool IsInitialized => deviceResources.IsInitialized;

        /// <summary>초기화 실패 시 그 이유를 설명하는 오류 메시지입니다. 초기화에 성공했으면 <c>null</c>입니다.</summary>
        public string InitializationError => deviceResources.InitializationError;

        /// <summary>현재 백엔드 상태를 설명하는 사람이 읽기 쉬운 메시지입니다.</summary>
        public string StatusMessage => deviceResources.StatusMessage;

        /// <summary>
        /// 새 프레임을 시작하고 렌더 타깃을 지정된 색으로 지웁니다.
        /// 프레임 경계에서 실패하면 상위 레이어가 다음 프레임에 이 백엔드를 포기할 수 있습니다.
        /// </summary>
        /// <param name="clearColor">프레임 시작 시 렌더 타깃을 채울 배경색입니다.</param>
        public void BeginFrame(Color clearColor)
        {
            // 프레임 경계에서 실패하면 상위는 이 backend를 다음 프레임에 포기할 수 있다.
            try
            {
                EnsureAvailable();
                deviceResources.BeginFrame(clearColor);
            }
            catch (Exception ex)
            {
                HandleRuntimeFailure("begin a frame", ex);
            }
        }

        /// <summary>
        /// 현재 프레임을 종료하고 스왑체인을 통해 화면에 출력합니다.
        /// 지수 평활된 진단 수치를 갱신합니다.
        /// </summary>
        public void EndFrame()
        {
            try
            {
                EnsureAvailable();
                deviceResources.EndFrame();
            }
            catch (Exception ex)
            {
                HandleRuntimeFailure("present the frame", ex);
            }
        }

        /// <summary>
        /// 렌더 표면의 크기를 변경합니다. 스왑체인 버퍼를 새 크기에 맞게 재구성합니다.
        /// </summary>
        /// <param name="width">새 표면 너비(픽셀)입니다.</param>
        /// <param name="height">새 표면 높이(픽셀)입니다.</param>
        public void Resize(int width, int height)
        {
            try
            {
                EnsureAvailable();
                deviceResources.Resize(width, height);
            }
            catch (Exception ex)
            {
                HandleRuntimeFailure("resize the render surface", ex);
                throw;
            }
        }

        /// <summary>
        /// GPU 월드 렌더링 패스를 실행합니다.
        /// world draw는 성공 여부를 bool로 반환하여 호출자가 상태를 기록할 수 있게 합니다.
        /// </summary>
        /// <param name="command">월드 렌더링에 필요한 레이캐스트, 스프라이트, 빔 데이터가 담긴 명령입니다.</param>
        /// <returns>GPU 월드 렌더링이 성공하면 <c>true</c>, 실패하거나 비활성화된 경우 <c>false</c>입니다.</returns>
        public bool DrawWorld(RenderWorldCommand command)
        {
            // world draw는 성공 여부를 bool로 돌려줘서 호출자가 상태를 기록할 수 있게 한다.
            try
            {
                EnsureAvailable();
                return deviceResources.DrawWorld(command);
            }
            catch (Exception ex)
            {
                HandleRuntimeFailure("draw the world", ex);
                return false;
            }
        }

        /// <summary>
        /// 지정된 사각형을 GPU 오버레이 패스로 그립니다.
        /// </summary>
        /// <param name="command">위치, 크기, 색상 정보가 담긴 사각형 렌더 명령입니다.</param>
        public void DrawRectangle(RenderRectCommand command)
        {
            try
            {
                EnsureAvailable();
                deviceResources.DrawRectangle(command);
            }
            catch (Exception ex)
            {
                HandleRuntimeFailure("draw a rectangle", ex);
            }
        }

        /// <summary>
        /// 지정된 이미지를 GPU 오버레이 패스로 그립니다.
        /// </summary>
        /// <param name="command">이미지, 위치, 크기 정보가 담긴 이미지 렌더 명령입니다.</param>
        public void DrawImage(RenderImageCommand command)
        {
            try
            {
                EnsureAvailable();
                deviceResources.DrawImage(command);
            }
            catch (Exception ex)
            {
                HandleRuntimeFailure("draw an image", ex);
            }
        }

        /// <summary>
        /// 지정된 텍스트를 GPU 오버레이 패스로 그립니다.
        /// 글리프 아틀라스 캐시를 활용하여 렌더링합니다.
        /// </summary>
        /// <param name="command">텍스트 내용, 위치, 크기, 색상 정보가 담긴 텍스트 렌더 명령입니다.</param>
        public void DrawText(RenderTextCommand command)
        {
            try
            {
                EnsureAvailable();
                deviceResources.DrawText(command);
            }
            catch (Exception ex)
            {
                HandleRuntimeFailure("draw text", ex);
            }
        }

        /// <summary>
        /// 지정된 텍스트를 렌더링했을 때의 크기를 측정합니다.
        /// </summary>
        /// <param name="text">크기를 측정할 텍스트입니다.</param>
        /// <param name="size">텍스트의 폰트 크기(포인트)입니다.</param>
        /// <returns>측정된 텍스트의 너비와 높이입니다.</returns>
        public SizeF MeasureText(string text, float size)
        {
            try
            {
                EnsureAvailable();
                return deviceResources.MeasureText(text, size);
            }
            catch (Exception ex)
            {
                HandleRuntimeFailure("measure text", ex);
                return SizeF.Empty;
            }
        }

        /// <summary>
        /// 글리프 아틀라스 GPU 텍스처를 비워 VRAM을 회수합니다.
        /// 층 전환 시 호출되며, 다음 렌더에서 글리프는 자동으로 재패킹됩니다.
        /// </summary>
        public void OnLevelTransition()
        {
            if (disposed || deviceResources == null)
            {
                return;
            }

            deviceResources.FlushGlyphAtlas();
        }

        /// <summary>
        /// 이 백엔드와 모든 D3D11 자원을 해제합니다.
        /// </summary>
        public void Dispose()
        {
            deviceResources.Dispose();
            disposed = true;
        }

        /// <summary>
        /// 현재 활성화된 렌더 경로(GPU 월드/스프라이트/오버레이 조합)를 한 줄 문자열로 설명합니다.
        /// F3 HUD와 상태 메시지에서 이 문자열을 직접 표시합니다.
        /// </summary>
        /// <returns>현재 렌더 경로를 설명하는 문자열입니다.</returns>
        public string DescribeActivePath()
        {
            // F3 HUD와 상태 메시지는 이 설명 문자열을 그대로 사용하므로
            // 현재 DX11 경로가 한 줄로 읽히게 유지한다.
            if (!IsInitialized)
            {
                return InitializationError ?? "Direct3D11 backend is not initialized.";
            }

            string world = deviceResources.SupportsWorldRendering ? "GPU world" : "GPU world disabled";
            return "Using Direct3D11 with " + world + ", GPU sprites, GPU beams, and GPU overlay.";
        }

        /// <summary>
        /// 현재 프레임의 렌더링 진단 정보를 캡처하여 반환합니다.
        /// 각 패스별 평활 소요 시간, 스프라이트 수, 렌더 타깃 크기 등이 포함됩니다.
        /// </summary>
        /// <returns>현재 렌더 상태와 성능 수치가 담긴 진단 구조체입니다.</returns>
        public D3D11RenderDiagnostics CaptureDiagnostics()
        {
            return deviceResources.CaptureDiagnostics(DescribeActivePath());
        }

        /// <summary>
        /// 이 백엔드를 사용할 수 있는 상태인지 검증합니다.
        /// Dispose 이후 또는 초기화가 실패한 상태에서 호출하면 예외를 던집니다.
        /// </summary>
        /// <exception cref="ObjectDisposedException">이 객체가 이미 Dispose된 경우 발생합니다.</exception>
        /// <exception cref="InvalidOperationException">장치 자원이 없거나 초기화되지 않은 경우 발생합니다.</exception>
        private void EnsureAvailable()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(D3D11RenderBackend));
            }

            if (deviceResources == null)
            {
                throw new InvalidOperationException("Direct3D11 device resources are unavailable.");
            }

            if (!IsInitialized)
            {
                throw new InvalidOperationException(InitializationError ?? "Direct3D11 backend is not initialized.");
            }
        }

        /// <summary>
        /// 런타임 렌더 실패를 처리합니다.
        /// 오류 메시지 박스를 표시한 뒤 장치 자원에 실패를 기록하여
        /// 다음 프레임에 안전하게 재시도할 수 있도록 합니다.
        /// </summary>
        /// <param name="action">실패한 작업을 설명하는 짧은 문자열입니다.</param>
        /// <param name="ex">발생한 예외입니다.</param>
        private void HandleRuntimeFailure(string action, Exception ex)
        {
            if (disposed)
            {
                return;
            }

            // 에러를 조용히 덮지 않고, 정확한 원인을 확인하기 위해 팝업을 띄웁니다.
            System.Windows.Forms.MessageBox.Show(
                $"DX11 크래시 발생 (작업: {action}):\n{ex.Message}",
                "DX11 Rendering Error",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Error
            );

            deviceResources.InvalidateRuntimeFailure(action, ex);
        }
    }
}
