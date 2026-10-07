using System.Drawing;
using My2DEngine.Engine.Rendering;
using My2DEngine.Engine.Rendering.Backends.D3D11;

namespace My2DEngine
{
    public partial class Form1
    {
        /// <summary>
        /// 렌더 백엔드에서 수집한 프레임 단위 GPU/업로드 진단 정보.
        /// D3D11 구현 타입을 UI 코드에 직접 퍼뜨리지 않기 위한 폼 전용 DTO다.
        /// </summary>
        private readonly record struct RenderDebugSnapshot(
            bool IsAvailable,
            string PathDescription,
            float CpuUploadMs,
            float WorldMs,
            float SpriteMs,
            float OverlayMs,
            float PresentMs,
            int WorldTargetWidth,
            int WorldTargetHeight,
            int ColumnCount,
            int SpriteCount);

        /// <summary>
        /// 디버그 HUD가 실제로 그릴 문자열 묶음.
        /// 렌더 루프 안에서는 문자열 조합과 백엔드 조회가 흩어지지 않도록 한 번에 캡처한다.
        /// </summary>
        private readonly record struct DebugHudSnapshot(
            string BackendText,
            string PathText,
            string WorldText,
            string WorldStatusText,
            string LaserStatusText,
            string WindowText,
            string RenderText,
            string GpuRenderText,
            string GpuCostText,
            string PresentCostText,
            string FpsText,
            string StatusText);

        /// <summary>
        /// F3 키로 활성화되는 디버그 HUD를 화면 왼쪽 상단에 그린다.
        /// HUD에 필요한 데이터 수집은 snapshot 생성 경로에서 끝내고, 이 메서드는 그리기만 담당한다.
        /// </summary>
        /// <param name="renderer">디버그 텍스트를 그릴 렌더러 인스턴스.</param>
        private void DrawDebugHud(Renderer renderer)
        {
            DebugHudSnapshot snapshot = CaptureDebugHudSnapshot();
            int panelX = 12;
            int panelY = 12;
            int panelW = 540;
            int panelH = 222;

            renderer.DrawRectangle(panelX, panelY, panelW, panelH, Color.FromArgb(170, 8, 8, 8));
            renderer.DrawRectangle(panelX + 8, panelY + 8, panelW - 16, 2, Color.FromArgb(180, 80, 180, 255));
            renderer.DrawText(snapshot.BackendText, panelX + 12, panelY + 16, Color.White, 12f);
            renderer.DrawText(snapshot.PathText, panelX + 12, panelY + 34, Color.FromArgb(220, 190, 230, 255), 10f);
            renderer.DrawText(snapshot.WorldText, panelX + 12, panelY + 52, Color.FromArgb(220, 190, 230, 255), 11f);
            renderer.DrawText(snapshot.WorldStatusText, panelX + 12, panelY + 68, Color.FromArgb(220, 235, 210, 170), 9f);
            renderer.DrawText(snapshot.LaserStatusText, panelX + 12, panelY + 84, Color.FromArgb(220, 255, 225, 190), 9f);
            renderer.DrawText(snapshot.WindowText, panelX + 12, panelY + 102, Color.Gainsboro, 11f);
            renderer.DrawText(snapshot.RenderText, panelX + 12, panelY + 120, Color.Gainsboro, 11f);
            renderer.DrawText(snapshot.GpuRenderText, panelX + 12, panelY + 138, Color.FromArgb(220, 220, 240, 255), 10f);
            renderer.DrawText(snapshot.GpuCostText, panelX + 12, panelY + 156, Color.FromArgb(220, 255, 220, 190), 10f);
            renderer.DrawText(snapshot.PresentCostText, panelX + 12, panelY + 172, Color.FromArgb(220, 255, 220, 190), 10f);
            renderer.DrawText(snapshot.FpsText, panelX + 12, panelY + 188, Color.FromArgb(220, 190, 255, 190), 11f);
            if (!string.IsNullOrWhiteSpace(snapshot.StatusText))
            {
                renderer.DrawText(snapshot.StatusText, panelX + 240, panelY + 16, Color.FromArgb(220, 190, 210, 255), 9f);
            }
        }

        /// <summary>
        /// HUD에 표시할 모든 디버그 데이터를 한 번에 캡처해 문자열 스냅샷으로 정리한다.
        /// </summary>
        private DebugHudSnapshot CaptureDebugHudSnapshot()
        {
            WorldDebugSnapshot worldDebug = CaptureWorldDebugSnapshot();
            RenderDebugSnapshot renderDebug = CaptureRenderDebugSnapshot();

            string pathText = renderDebug.IsAvailable
                ? renderDebug.PathDescription
                : "Path: Direct3D11 path unavailable.";
            string gpuRenderText = renderDebug.IsAvailable
                ? "GPU Target: " + renderDebug.WorldTargetWidth + "x" + renderDebug.WorldTargetHeight +
                  "  Cols: " + renderDebug.ColumnCount +
                  "  Sprites: " + renderDebug.SpriteCount
                : "GPU Target: n/a";
            string gpuCostText = renderDebug.IsAvailable
                ? "GPU ms  World " + renderDebug.WorldMs.ToString("0.00") +
                  "  Sprites " + renderDebug.SpriteMs.ToString("0.00") +
                  "  Overlay " + renderDebug.OverlayMs.ToString("0.00")
                : "GPU ms  World n/a  Sprites n/a  Overlay n/a";
            string presentCostText = renderDebug.IsAvailable
                ? "Upload " + renderDebug.CpuUploadMs.ToString("0.00") +
                  "  Present " + renderDebug.PresentMs.ToString("0.00")
                : "Upload n/a  Present n/a";

            return new DebugHudSnapshot(
                "Backend: Direct3D11",
                pathText,
                "World: " + (worldDebug.UsesGpuWorldRendering ? "GPU" : "Unavailable") +
                    "  Seed: " + world.RunSeed + (world.FixedRunSeed.HasValue ? " (fixed)" : string.Empty),
                "World Status: " + worldDebug.WorldStatus,
                "Laser Path: " + worldDebug.LaserStatus,
                "Window: " + ClientSize.Width + "x" + ClientSize.Height,
                "Render: " + lastWorldRenderSize.Width + "x" + lastWorldRenderSize.Height,
                gpuRenderText,
                gpuCostText,
                presentCostText,
                "FPS: " + smoothedFps.ToString("0.0") + "  (" + smoothedFrameMs.ToString("0.0") + " ms)",
                gameHost.BackendStatusMessage ?? string.Empty);
        }

        /// <summary>
        /// D3D11 렌더 백엔드의 현재 진단 정보를 폼 전용 값 객체로 복사한다.
        /// </summary>
        private RenderDebugSnapshot CaptureRenderDebugSnapshot()
        {
            D3D11RenderDiagnostics diagnostics = gameHost.CaptureD3D11Diagnostics();
            if (diagnostics == null)
            {
                return new RenderDebugSnapshot(
                    false,
                    string.Empty,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0,
                    0,
                    0,
                    0);
            }

            return new RenderDebugSnapshot(
                true,
                diagnostics.PathDescription ?? "Path: Direct3D11 path unavailable.",
                diagnostics.CpuUploadMs,
                diagnostics.WorldMs,
                diagnostics.SpriteMs,
                diagnostics.OverlayMs,
                diagnostics.PresentMs,
                diagnostics.WorldTargetWidth,
                diagnostics.WorldTargetHeight,
                diagnostics.ColumnCount,
                diagnostics.SpriteCount);
        }
    }
}
