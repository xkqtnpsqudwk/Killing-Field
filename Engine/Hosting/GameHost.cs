using System;
using My2DEngine.Engine.Rendering;
using My2DEngine.Engine.Rendering.Abstractions;
using My2DEngine.Engine.Rendering.Backends.D3D11;

namespace My2DEngine.Engine.Hosting
{
    /// <summary>
    /// WinForms 폼과 D3D11 렌더 백엔드 사이를 중재하는 호스트 클래스.
    /// 렌더 백엔드의 생성, 재생성, 크기 변경, 실패 복구를 담당하며
    /// 폼은 이 클래스를 통해 <see cref="Renderer"/>를 얻어 그리기를 수행한다.
    /// 현재 기준선은 D3D11 전용이며, 실패 시 GDI로 내려가지 않고
    /// 상태를 기록한 뒤 일정 시간(backoff) 후 재시도한다.
    /// </summary>
    public sealed class GameHost : IDisposable
    {
        /// <summary>
        /// 백엔드 초기화 실패 후 다음 재시도까지 기다리는 최소 시간.
        /// 매 프레임 재시도 시 발생하는 생성/해제 루프를 방지한다.
        /// </summary>
        private static readonly TimeSpan D3D11RetryBackoff = TimeSpan.FromMilliseconds(3000);

        /// <summary>현재 렌더링 대상 창 핸들과 해상도를 담은 서피스 정보.</summary>
        private RenderSurfaceInfo surfaceInfo;

        /// <summary>현재 유지 중인 렌더 백엔드 인스턴스. 실패 시 null.</summary>
        private D3D11RenderBackend persistentBackend;

        /// <summary>persistentBackend가 생성될 때 사용된 창 핸들. 핸들 변경 감지에 사용.</summary>
        private IntPtr backendWindowHandle;

        /// <summary>D3D11 재시도를 허용할 다음 UTC 시각. backoff 기간 동안 재시도를 막는다.</summary>
        private DateTime nextD3D11RetryTimeUtc;

        /// <summary>Dispose가 호출된 이후 true가 된다. 이후 모든 작업을 차단한다.</summary>
        private bool disposed;

        /// <summary>
        /// DX11 전용 <see cref="GameHost"/>를 초기화한다.
        /// </summary>
        public GameHost()
        {
            BackendStatusMessage = "Initializing Direct3D11 backend.";
        }

        /// <summary>백엔드의 현재 상태를 설명하는 메시지. 초기화 오류나 활성 렌더 경로를 나타낸다.</summary>
        public string BackendStatusMessage { get; private set; }

        /// <summary>
        /// 렌더링 대상 창 핸들이나 해상도가 변경되었을 때 호출하여 서피스 정보를 갱신한다.
        /// 이전과 동일한 핸들/크기 조합이면 아무 작업도 하지 않는다.
        /// Dispose 이후에는 무시된다.
        /// </summary>
        /// <param name="windowHandle">렌더링 대상 창의 Win32 핸들.</param>
        /// <param name="width">창의 클라이언트 영역 너비 (픽셀).</param>
        /// <param name="height">창의 클라이언트 영역 높이 (픽셀).</param>
        public void UpdateSurface(IntPtr windowHandle, int width, int height)
        {
            if (disposed)
            {
                return;
            }

            if (surfaceInfo.WindowHandle == windowHandle &&
                surfaceInfo.Width == width &&
                surfaceInfo.Height == height)
            {
                return;
            }

            surfaceInfo = new RenderSurfaceInfo
            {
                WindowHandle = windowHandle,
                Width = width,
                Height = height
            };

            ResolveBackend(true);
        }

        /// <summary>
        /// 현재 상태에 맞는 <see cref="Renderer"/>를 생성하여 반환한다.
        /// 창 핸들이 변경되었거나 백엔드가 무효화된 경우 자동으로 재초기화를 시도한다.
        /// D3D11 백엔드가 사용 불가능한 경우 <see cref="InvalidOperationException"/>을 던진다.
        /// </summary>
        /// <returns>현재 프레임을 그릴 수 있는 <see cref="Renderer"/> 인스턴스.</returns>
        /// <exception cref="ObjectDisposedException">GameHost가 이미 Dispose된 경우.</exception>
        /// <exception cref="InvalidOperationException">D3D11 백엔드를 사용할 수 없는 경우.</exception>
        public Renderer CreateRenderer()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(GameHost));
            }

            if (persistentBackend == null ||
                backendWindowHandle != surfaceInfo.WindowHandle ||
                !persistentBackend.IsInitialized)
            {
                ResolveBackend(false);
            }

            if (persistentBackend != null && persistentBackend.IsInitialized)
            {
                return new Renderer(persistentBackend);
            }

            throw new InvalidOperationException(BackendStatusMessage ?? "Direct3D11 backend is unavailable.");
        }

        /// <summary>
        /// 보유 중인 렌더 백엔드를 해제하고 GameHost를 비활성화한다.
        /// 이후 모든 메서드 호출은 무시되거나 예외를 발생시킨다.
        /// </summary>
        public void Dispose()
        {
            persistentBackend?.Dispose();
            persistentBackend = null;
            disposed = true;
        }

        /// <summary>
        /// 프레임 렌더링 중 발생한 예외를 보고한다.
        /// 폼을 즉시 종료하는 대신 백엔드를 비우고 재시도 backoff를 적용한다.
        /// Dispose된 상태이면 무시된다.
        /// </summary>
        /// <param name="message">실패 원인을 설명하는 메시지 (예: 예외 메시지).</param>
        public void ReportBackendFailure(string message)
        {
            if (disposed)
            {
                return;
            }

            ResetToDx11Retry("Direct3D11 frame failure. " + message, true);
        }

        /// <summary>
        /// D3D11 백엔드의 현재 진단 정보를 스냅샷으로 캡처하여 반환한다.
        /// 백엔드가 초기화되지 않았으면 null을 반환한다.
        /// </summary>
        /// <returns>
        /// D3D11 렌더 진단 정보 스냅샷.
        /// 백엔드가 없거나 초기화되지 않은 경우 null.
        /// </returns>
        public D3D11RenderDiagnostics CaptureD3D11Diagnostics()
        {
            if (persistentBackend == null || !persistentBackend.IsInitialized)
            {
                return null;
            }

            return persistentBackend.CaptureDiagnostics();
        }

        /// <summary>
        /// 현재 서피스 정보에 맞게 D3D11 백엔드를 해결(생성/재사용/크기조정)한다.
        /// </summary>
        /// <param name="force">
        /// true이면 backoff 시간을 무시하고 즉시 재시도한다.
        /// false이면 backoff 기간 중에는 재시도를 건너뛴다.
        /// </param>
        private void ResolveBackend(bool force)
        {
            DateTime nowUtc = DateTime.UtcNow;
            if (!force && nowUtc < nextD3D11RetryTimeUtc)
            {
                if (string.IsNullOrWhiteSpace(BackendStatusMessage))
                {
                    BackendStatusMessage = "Direct3D11 retry is temporarily delayed.";
                }
                return;
            }

            var d3d11Backend = persistentBackend;
            bool needsRecreate = d3d11Backend == null || backendWindowHandle != surfaceInfo.WindowHandle;
            if (needsRecreate)
            {
                try
                {
                    d3d11Backend = RenderBackendFactory.CreateD3D11(surfaceInfo);
                    ReplacePersistentBackend(d3d11Backend, surfaceInfo.WindowHandle);
                }
                catch (Exception ex)
                {
                    ResetToDx11Retry("Direct3D11 creation failed. " + ex.Message, true);
                    return;
                }
            }
            else
            {
                try
                {
                    d3d11Backend.Resize(surfaceInfo.Width, surfaceInfo.Height);
                }
                catch (Exception ex)
                {
                    ResetToDx11Retry("Direct3D11 resize failed. " + ex.Message, true);
                    return;
                }
            }

            if (d3d11Backend == null || !d3d11Backend.IsInitialized)
            {
                ResetToDx11Retry(
                    d3d11Backend?.InitializationError ?? "Direct3D11 backend failed to initialize.",
                    true);
                return;
            }

            BackendStatusMessage = d3d11Backend.StatusMessage;
            nextD3D11RetryTimeUtc = DateTime.MinValue;
        }

        /// <summary>
        /// 기존 persistentBackend를 해제하고 새 백엔드로 교체한다.
        /// 동일한 인스턴스가 전달되면 아무 작업도 하지 않는다.
        /// </summary>
        /// <param name="newBackend">새로 사용할 백엔드. null이면 백엔드를 비운다.</param>
        /// <param name="windowHandle">새 백엔드가 사용하는 창 핸들.</param>
        private void ReplacePersistentBackend(D3D11RenderBackend newBackend, IntPtr windowHandle)
        {
            if (ReferenceEquals(persistentBackend, newBackend))
            {
                return;
            }

            persistentBackend?.Dispose();
            persistentBackend = newBackend;
            backendWindowHandle = windowHandle;
        }

        /// <summary>
        /// 백엔드를 완전히 비우고, 상태 메시지를 기록하며, 필요시 재시도 backoff를 설정한다.
        /// 실제 재시도는 backoff가 지난 뒤 다음 <see cref="ResolveBackend"/> 호출에서만 일어난다.
        /// </summary>
        /// <param name="statusMessage">현재 실패 상태를 설명하는 메시지.</param>
        /// <param name="applyRetryBackoff">
        /// true이면 <see cref="D3D11RetryBackoff"/> 후에 재시도하도록 타임스탬프를 설정한다.
        /// false이면 타임스탬프를 변경하지 않는다.
        /// </param>
        private void ResetToDx11Retry(string statusMessage, bool applyRetryBackoff)
        {
            ReplacePersistentBackend(null, IntPtr.Zero);
            BackendStatusMessage = statusMessage;
            if (applyRetryBackoff)
            {
                nextD3D11RetryTimeUtc = DateTime.UtcNow + D3D11RetryBackoff;
            }
        }
    }
}
