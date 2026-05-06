using System;
using System.Collections.Generic;
using System.Drawing;
using My2DEngine.Engine.Rendering;
using My2DEngine.Engine.Rendering.Abstractions;
using My2DEngine.Game.Config;
using My2DEngine.Game.Systems;

namespace My2DEngine.Game.Rendering
{
    /// <summary>
    /// GPU 월드 렌더링 커맨드 제출, 월드 오버레이 큐 관리, zBuffer 갱신 보조 경로를 담당하는 partial 클래스.
    /// GPU 경로가 성공하면 WorldRenderDataBuilder가 생성한 RenderWorldCommand를 렌더러 백엔드로 전달하고,
    /// 실패하면 상태 메시지만 남긴다.
    /// </summary>
    public partial class RaycastRenderer
    {
        /// <summary>
        /// GPU 월드 렌더링을 시도한다.
        /// 렌더러 지원 여부와 데이터 빌드 결과를 확인한 뒤,
        /// 성공 시 렌더러 백엔드에 RenderWorldCommand를 제출하고 true를 반환한다.
        /// </summary>
        /// <param name="renderer">월드 렌더링 커맨드를 수신할 렌더러 백엔드.</param>
        /// <param name="player">카메라 상태를 제공하는 플레이어.</param>
        /// <param name="rewardPickups">월드에 배치된 보상 픽업 목록.</param>
        /// <param name="playerProjectiles">월드에 배치된 플레이어 투사체 목록.</param>
        /// <param name="compositeRotationDegrees">최종 합성 시 적용할 화면 회전 각도(도).</param>
        /// <param name="compositeScale">최종 합성 시 적용할 화면 스케일 배율.</param>
        /// <param name="compositeOffsetY">최종 합성 시 적용할 수직 오프셋(픽셀).</param>
        /// <param name="toxicMistAlpha">독성 안개 방의 월드 안개 강도. 0이면 기본 월드 안개를 사용한다.</param>
        /// <returns>GPU 월드 렌더링에 성공하면 true, 그렇지 않으면 false.</returns>
        private bool TryRenderWorld(Renderer renderer, Player player, IList<RewardPickup> rewardPickups, IList<EnemyProjectile> playerProjectiles, float compositeRotationDegrees, float compositeScale, float compositeOffsetY, float toxicMistAlpha)
        {
            if (renderer == null ||
                player == null ||
                !renderer.SupportsWorldRendering)
            {
                if (renderer == null)
                {
                    lastGpuWorldStatus = "GPU world unavailable because the renderer is null.";
                }
                else if (player == null)
                {
                    lastGpuWorldStatus = "GPU world unavailable because the player state is missing.";
                }
                else
                {
                    lastGpuWorldStatus = "GPU world unavailable: " + (renderer.BackendStatusMessage ?? "the active backend does not support world rendering.");
                }
                return false;
            }

            if (!worldRenderDataBuilder.TryBuild(player, zBuffer, rewardPickups, playerProjectiles, frameW, frameH, out RenderWorldCommand command))
            {
                lastGpuWorldStatus = "GPU world build failed before issuing the render command.";
                return false;
            }

            command.ScreenOffsetX = renderer.ScreenOffsetX;
            command.ScreenOffsetY = renderer.ScreenOffsetY;
            command.CompositeRotationDegrees = compositeRotationDegrees;
            command.CompositeScale = compositeScale;
            command.CompositeOffsetY = compositeOffsetY;
            ApplyToxicMistWorldFog(ref command, toxicMistAlpha);

            bool drewWorld = renderer.TryDrawWorld(command);
            if (drewWorld)
            {
                lastGpuWorldStatus = "GPU world rendered successfully.";
            }
            else
            {
                lastGpuWorldStatus = "GPU world draw was attempted, but the backend rejected the command.";
            }

            return drewWorld;
        }

        private static void ApplyToxicMistWorldFog(ref RenderWorldCommand command, float toxicMistAlpha)
        {
            if (toxicMistAlpha <= 0f)
            {
                return;
            }

            float intensity = toxicMistAlpha / 0.28f;
            if (intensity < 0f)
            {
                intensity = 0f;
            }
            else if (intensity > 1f)
            {
                intensity = 1f;
            }

            command.FogDensity = Math.Max(command.FogDensity, 0.18f + (0.06f * intensity));
            command.FogColor = Color.FromArgb(255, 72, 130, 64);
            command.FogTintStrength = 0.18f + (0.14f * intensity);

            command.EnemySpritePass.FogDensity = command.FogDensity;
            command.MiscSpritePass.FogDensity = command.FogDensity;
        }

        /// <summary>
        /// worldOverlayRects 큐를 비워 이번 프레임의 오버레이 목록을 초기화한다.
        /// 매 프레임 시작 시 호출된다.
        /// </summary>
        private void ClearWorldOverlayQueue()
        {
            if (worldOverlayRects.Count > 0)
            {
                worldOverlayRects.Clear();
            }
        }

        /// <summary>
        /// GPU 경로에서 화면에 그릴 월드 오버레이 사각형을 큐에 추가한다.
        /// depth가 0보다 크면 FlushWorldOverlayQueue에서 zBuffer 차폐 판정을 수행한다.
        /// 크기나 알파가 0 이하이면 추가하지 않는다.
        /// </summary>
        /// <param name="x">사각형의 화면 X 좌표(픽셀).</param>
        /// <param name="y">사각형의 화면 Y 좌표(픽셀).</param>
        /// <param name="width">사각형의 너비(픽셀). 0 이하이면 무시된다.</param>
        /// <param name="height">사각형의 높이(픽셀). 0 이하이면 무시된다.</param>
        /// <param name="color">채울 색상. 알파가 0 이하이면 무시된다.</param>
        /// <param name="depth">월드 공간 깊이. 0 이하이면 차폐 판정 없이 항상 렌더링한다.</param>
        private void QueueWorldOverlayRect(float x, float y, float width, float height, Color color, float depth = 0f)
        {
            if (width <= 0f || height <= 0f || color.A <= 0)
            {
                return;
            }

            worldOverlayRects.Add(new WorldOverlayRect
            {
                X = x,
                Y = y,
                Width = width,
                Height = height,
                Color = color,
                Depth = depth
            });
        }

        /// <summary>
        /// worldOverlayRects 큐의 모든 사각형을 렌더러로 제출한다.
        /// depth가 있는 사각형은 열(column)별로 zBuffer 차폐 여부를 검사하여
        /// 가시 열만 연속된 DrawRectangle 호출로 묶어 제출한다.
        /// GPU 경로가 활성화되지 않았거나 큐가 비어 있으면 아무것도 하지 않는다.
        /// </summary>
        /// <param name="renderer">사각형을 제출할 렌더러 백엔드.</param>
        private void FlushWorldOverlayQueue(Renderer renderer)
        {
            if (!lastFrameUsedGpuWorld || renderer == null || worldOverlayRects.Count == 0)
            {
                return;
            }

            for (int i = 0; i < worldOverlayRects.Count; i++)
            {
                WorldOverlayRect rect = worldOverlayRects[i];
                if (rect.Depth <= 0f || zBuffer == null)
                {
                    renderer.DrawRectangle(rect.X, rect.Y, rect.Width, rect.Height, rect.Color);
                    continue;
                }

                int left = (int)Math.Floor(rect.X);
                int right = (int)Math.Ceiling(rect.X + rect.Width) - 1;
                int runStart = -1;
                for (int x = left; x <= right + 1; x++)
                {
                    bool visible = x <= right && IsOverlayColumnVisible(x, rect.Depth);
                    if (visible)
                    {
                        if (runStart < 0) runStart = x;
                    }
                    else if (runStart >= 0)
                    {
                        renderer.DrawRectangle(runStart, rect.Y, x - runStart, rect.Height, rect.Color);
                        runStart = -1;
                    }
                }
            }
        }

        /// <summary>
        /// 지정된 화면 열 x에서 오버레이가 벽 또는 가까운 스프라이트에 가려지지 않는지 판정한다.
        /// depth가 해당 열의 zBuffer(벽 거리)보다 작아야 가시이며,
        /// spriteDepthBuffer에 더 가까운 스프라이트가 있으면 차폐로 판정한다.
        /// </summary>
        /// <param name="x">검사할 화면 열(column) 인덱스.</param>
        /// <param name="depth">오버레이의 월드 공간 깊이.</param>
        /// <returns>오버레이가 가시이면 true, 차폐되면 false.</returns>
        private bool IsOverlayColumnVisible(int x, float depth)
        {
            if (x < 0 || x >= frameW) return false;
            if ((double)depth >= zBuffer[x]) return false;
            if (spriteDepthBuffer != null && depth > spriteDepthBuffer[x]) return false;
            return true;
        }

    }
}
