using System;
using System.Collections.Generic;
using System.Drawing;
using My2DEngine.Engine.Rendering;
using My2DEngine.Engine.Rendering.Abstractions;
using My2DEngine.Game.Map;
using My2DEngine.Game.Systems;
using My2DEngine.Rendering.WorldData;

namespace My2DEngine.Game.Rendering
{
    /// <summary>
    /// 레이캐스팅 기반 3D 뷰를 생성하고 HUD·오버레이와 합성하는 최종 렌더러이다.
    /// 현재 기준선은 DX11 GPU 경로이며, 월드 커맨드와 오버레이 명령을 만들어 렌더 백엔드에 제출한다.
    /// partial 클래스로 분리되어 있으며, 프레임 상태(Frame), HUD(Hud),
    /// 스프라이트(Sprites), 월드(World) partial이 각각 역할을 담당한다.
    /// </summary>
    public partial class RaycastRenderer : IDisposable
    {
        /// <summary>각 화면 열(column)에서 가장 가까운 벽까지의 투영 거리를 저장하는 깊이 버퍼.</summary>
        private double[] zBuffer;

        /// <summary>
        /// RenderEnemies 처리 중 각 화면 열(column)에서 가장 가까운 스프라이트의 깊이를 저장하는 버퍼.
        /// FlushWorldOverlayQueue에서 체력 바·텔레그래프 등 오버레이가 가까운 스프라이트에 가려지는지
        /// 판정할 때 사용한다. 프레임마다 RenderEnemies 시작 시 float.MaxValue로 초기화된다.
        /// </summary>
        private float[] spriteDepthBuffer;

        /// <summary>현재 프레임 버퍼의 너비(픽셀).</summary>
        private int frameW;

        /// <summary>현재 프레임 버퍼의 높이(픽셀).</summary>
        private int frameH;

        /// <summary>맵 데이터 및 문(door) 상태를 관리하는 매니저.</summary>
        private readonly MapManager mapManager;

        /// <summary>벽 텍스처, 무기 이미지 등 모든 텍스처 리소스를 보유하는 매니저.</summary>
        private readonly TextureManager textureManager;

        /// <summary>적 인스턴스 및 투사체 목록을 관리하는 매니저.</summary>
        private readonly EnemyManager enemyManager;

        /// <summary>GPU 렌더 커맨드 데이터를 조립하는 빌더.</summary>
        private readonly WorldRenderDataBuilder worldRenderDataBuilder;

        /// <summary>스프라이트 원근 정렬에 사용하는 거리(음수 거리²) 키 배열.</summary>
        private double[] enemySortKeys;

        /// <summary>스프라이트 정렬 결과로 재배열된 적 인덱스 배열.</summary>
        private int[] enemyOrder;

        /// <summary>GPU 오버레이 경로에서 깊이 판정 후 일괄 렌더링할 월드 공간 사각형 큐.</summary>
        private readonly List<WorldOverlayRect> worldOverlayRects = new List<WorldOverlayRect>(128);

        /// <summary>직전 프레임에 GPU 월드 렌더링이 성공했는지 여부.</summary>
        private bool lastFrameUsedGpuWorld;

        /// <summary>GPU 월드 렌더링 상태를 설명하는 최신 진단 문자열.</summary>
        private string lastGpuWorldStatus = "GPU world not attempted yet.";

        /// <summary>레이저 빔 렌더링 경로 상태를 설명하는 최신 진단 문자열.</summary>
        private string lastLaserRenderStatus = "Laser path not evaluated yet.";

        /// <summary>스프라이트 투영 디버그 외곽선 표시 여부.</summary>
        private bool showSpriteProjectionDebug;

        /// <summary>Dispose가 이미 호출되었는지 추적하는 플래그.</summary>
        private bool disposed;

        /// <summary>
        /// RaycastRenderer를 초기화한다.
        /// </summary>
        /// <param name="mapManager">맵 데이터 및 문 상태를 제공하는 매니저.</param>
        /// <param name="textureManager">텍스처와 이미지 리소스를 제공하는 매니저.</param>
        /// <param name="enemyManager">적 및 투사체 목록을 제공하는 매니저.</param>
        public RaycastRenderer(MapManager mapManager, TextureManager textureManager, EnemyManager enemyManager)
        {
            this.mapManager = mapManager;
            this.textureManager = textureManager;
            this.enemyManager = enemyManager;
            worldRenderDataBuilder = new WorldRenderDataBuilder(mapManager, textureManager, enemyManager);
        }

        /// <summary>직전 프레임에 GPU 월드 렌더링이 사용되었는지 여부를 반환한다.</summary>
        public bool LastFrameUsedGpuWorld => lastFrameUsedGpuWorld;

        /// <summary>GPU 월드 렌더링의 최신 상태 메시지를 반환한다. 성공/실패/비활성 이유를 포함한다.</summary>
        public string LastGpuWorldStatus => lastGpuWorldStatus;

        /// <summary>레이저 빔 렌더 경로의 최신 상태 메시지를 반환한다.</summary>
        public string LastLaserRenderStatus => lastLaserRenderStatus;

        /// <summary>
        /// 스프라이트 투영 디버그 외곽선 표시를 켜거나 끈다.
        /// </summary>
        /// <param name="enabled">true이면 각 스프라이트의 투영 사각형을 화면에 그린다.</param>
        public void SetShowSpriteProjectionDebug(bool enabled) => showSpriteProjectionDebug = enabled;

        /// <summary>
        /// 방/층 전환 시 월드 렌더 임시 버퍼를 비운다.
        /// 이전 전투 최대치에 맞춰 커진 캐시가 그대로 유지되는 것을 막는다.
        /// </summary>
        public void ResetTransientCaches()
        {
            worldRenderDataBuilder.ResetTransientCaches();
            enemySortKeys = null;
            enemyOrder = null;
            worldOverlayRects.Clear();
        }

        /// <summary>
        /// 한 프레임 전체를 렌더링한다. 월드(벽/바닥/스프라이트)를 먼저 그린 뒤
        /// HUD, 오버레이, 사망 연출 순으로 합성하여 렌더러에 제출한다.
        /// </summary>
        /// <param name="r">화면에 그릴 렌더러 백엔드.</param>
        /// <param name="screenWidth">렌더 대상 너비(픽셀).</param>
        /// <param name="screenHeight">렌더 대상 높이(픽셀).</param>
        /// <param name="player">카메라 위치·방향을 제공하는 플레이어 상태.</param>
        /// <param name="weapon">현재 장착 무기. HUD 탄약 카운터와 무기 오버레이에 사용된다.</param>
        /// <param name="rewardPickups">화면에 표시할 보상 픽업 목록.</param>
        /// <param name="playerProjectiles">화면에 표시할 플레이어 투사체 목록.</param>
        /// <param name="bossEnemy">보스 적 인스턴스. null이면 보스 HUD를 그리지 않는다.</param>
        /// <param name="bossIntroTimer">보스 등장 연출 남은 시간(초). 0 이하이면 연출을 표시하지 않는다.</param>
        /// <param name="stageStatusMessage">화면 상단에 표시할 스테이지 상태 메시지.</param>
        /// <param name="interactPromptText">플레이어 근처의 상호작용 힌트 텍스트.</param>
        /// <param name="victory">스테이지 클리어 여부. true이면 클리어 UI를 표시한다.</param>
        /// <param name="playerDamageFlash">피격 화면 번쩍임 강도(0~1).</param>
        /// <param name="damageDirX">피격 방향 벡터의 X 성분(월드 공간).</param>
        /// <param name="damageDirY">피격 방향 벡터의 Y 성분(월드 공간).</param>
        /// <param name="playerDamageShakeTimer">피격 화면 흔들림 남은 시간(초).</param>
        /// <param name="playerDamageShakePower">피격 화면 흔들림 세기(0~1).</param>
        /// <param name="playerRecoilShakeTimer">반동 화면 흔들림 남은 시간(초).</param>
        /// <param name="playerRecoilShakePower">반동 화면 흔들림 세기(0~1).</param>
        /// <param name="deathPresentationProgress">사망 연출 진행 비율(0=시작, 1=완료).</param>
        /// <param name="deathRollDirection">사망 시 화면이 기울어지는 방향(-1 또는 1).</param>
        /// <param name="hitMarkerAlpha">명중 히트마커 표시 강도(0~1).</param>
        /// <param name="killMarkerAlpha">처치 히트마커 표시 강도(0~1).</param>
        /// <param name="weaponStatusText">발사 불가 상태를 설명하는 짧은 HUD 문구.</param>
        /// <param name="weaponStatusAlpha">발사 불가 HUD 문구 표시 강도(0~1).</param>
        /// <param name="pickupToastText">보상 드롭/획득을 설명하는 짧은 HUD 토스트 문구.</param>
        /// <param name="pickupToastAlpha">보상 드롭/획득 HUD 토스트 표시 강도(0~1).</param>
        public void Render(Renderer r, int screenWidth, int screenHeight, Player player, Weapon weapon,
            IList<RewardPickup> rewardPickups, IList<EnemyProjectile> playerProjectiles, Enemy bossEnemy, float bossIntroTimer, string stageStatusMessage,
            string interactPromptText, bool victory, float playerDamageFlash, float damageDirX, float damageDirY,
            float playerDamageShakeTimer, float playerDamageShakePower,
            float playerRecoilShakeTimer, float playerRecoilShakePower,
            float deathPresentationProgress, float deathRollDirection,
            float toxicMistAlpha,
            float hitMarkerAlpha, float killMarkerAlpha,
            string weaponStatusText, float weaponStatusAlpha,
            string pickupToastText, float pickupToastAlpha)
        {
            EnsureFrame(screenWidth, screenHeight);
            if (zBuffer == null)
            {
                return;
            }

            ClearWorldOverlayQueue();

            GetDamageShakeOffset(player, playerDamageShakeTimer, playerDamageShakePower, damageDirX, damageDirY,
                out float damageShakeX, out float damageShakeY);
            GetRecoilShakeOffset(playerRecoilShakeTimer, playerRecoilShakePower, out float recoilShakeX, out float recoilShakeY);
            float shakeOffsetX = damageShakeX + recoilShakeX;
            float shakeOffsetY = damageShakeY + recoilShakeY;
            r.SetScreenOffset(shakeOffsetX, shakeOffsetY);

            float deathEased = EaseOutCubic(deathPresentationProgress);
            float deathRotation = deathRollDirection * 24f * deathEased;
            float deathScale = 1f + (deathEased * 0.09f);
            float deathOffsetY = 8f * deathEased * deathEased;

            bool canTryGpuWorld = r.SupportsWorldRendering;
            bool usedGpuWorld = false;
            if (canTryGpuWorld)
            {
                usedGpuWorld = TryRenderWorld(r, player, rewardPickups, playerProjectiles, deathRotation, deathScale, deathOffsetY, toxicMistAlpha);
            }
            else
            {
                lastGpuWorldStatus = "GPU world skipped: " + (r.BackendStatusMessage ?? "the active backend does not support world rendering.");
            }

            lastFrameUsedGpuWorld = usedGpuWorld;
            lastLaserRenderStatus = usedGpuWorld
                ? "GPU beam pass active."
                : "GPU beam pass unavailable because GPU world rendering is unavailable.";

            if (usedGpuWorld)
            {
                RenderEnemies(player);
                RenderRewardPickups(player, rewardPickups);
                FlushWorldOverlayQueue(r);
            }

            if (player.IsDead)
            {
                DrawDeathOverlay(r, deathPresentationProgress);
            }
            else
            {
                DrawBossHudBackdrop(r, bossEnemy);
                DrawStageBackdrop(r, stageStatusMessage, interactPromptText, bossIntroTimer, bossEnemy, victory);
                DrawMiniMap(r, player);
                DrawAmmoCounterBackdrop(r, weapon);
                DrawCoinHudBackdrop(r);
                DrawHealthBar(r, player);
                DrawShieldBar(r, player);
                DrawStaminaBar(r, player);
                DrawCrosshair(r);
                DrawPlayerDamageOverlay(r, player, playerDamageFlash, damageDirX, damageDirY);
                DrawHitMarker(r, hitMarkerAlpha, killMarkerAlpha);
                DrawWeaponStatusFeedback(r, weaponStatusText, weaponStatusAlpha);
                DrawPickupToast(r, pickupToastText, pickupToastAlpha);
                DrawWeaponOverlay(r, weapon);

                DrawBossHud(r, bossEnemy);
                DrawStageOverlay(r, stageStatusMessage, interactPromptText, bossIntroTimer, bossEnemy, victory);
                DrawAmmoCounter(r, weapon);
                DrawCoinHud(r, player);
                DrawRewardPickupLabels(r, player, rewardPickups);
                DrawSpriteProjectionDebug(r);

                if (player.Stamina <= 0f)
                {
                    r.DrawTextCenteredShadow("Not Enough Stamina", frameW * 0.5f, frameH * 0.5f, Color.White, 18f);
                }
            }

            r.SetScreenOffset(0f, 0f);
        }

        /// <summary>
        /// 렌더러가 보유하는 GDI Bitmap 및 픽셀 배열 등 비관리 리소스를 해제한다.
        /// 이후 이 인스턴스를 사용해서는 안 된다.
        /// </summary>
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            zBuffer = null;
            enemySortKeys = null;
            enemyOrder = null;
            disposed = true;
        }

        /// <summary>
        /// Ease-Out Cubic 보간 함수. 0~1 범위 입력을 받아 가속 후 감속하는 곡선 출력을 반환한다.
        /// 사망 연출의 회전·스케일 애니메이션에 사용된다.
        /// </summary>
        /// <param name="value">0에서 1 사이의 선형 진행 값.</param>
        /// <returns>Ease-Out Cubic 보간된 0~1 사이의 값.</returns>
        private float EaseOutCubic(float value)
        {
            if (value <= 0f)
            {
                return 0f;
            }

            if (value >= 1f)
            {
                return 1f;
            }

            float inv = 1f - value;
            return 1f - (inv * inv * inv);
        }

        /// <summary>
        /// GPU 오버레이 경로에서 일괄 처리할 화면 공간 사각형 정보.
        /// Depth가 0보다 크면 zBuffer와 비교해 벽 차폐 여부를 판정한다.
        /// </summary>
        private struct WorldOverlayRect
        {
            /// <summary>사각형의 화면 X 좌표(픽셀).</summary>
            public float X;

            /// <summary>사각형의 화면 Y 좌표(픽셀).</summary>
            public float Y;

            /// <summary>사각형의 너비(픽셀).</summary>
            public float Width;

            /// <summary>사각형의 높이(픽셀).</summary>
            public float Height;

            /// <summary>채울 색상(ARGB).</summary>
            public Color Color;

            /// <summary>
            /// 월드 공간 깊이(transformY). 0 이하이면 깊이 판정 없이 무조건 렌더링한다.
            /// </summary>
            public float Depth;
        }

        /// <summary>
        /// showSpriteProjectionDebug가 활성화되어 있고 GPU 경로가 사용 중일 때,
        /// 각 스프라이트의 투영 사각형 외곽선과 종류·아틀라스 슬롯 텍스트를 화면에 그린다.
        /// </summary>
        /// <param name="renderer">외곽선과 텍스트를 그릴 렌더러.</param>
        private void DrawSpriteProjectionDebug(Renderer renderer)
        {
            if (!showSpriteProjectionDebug || renderer == null || !lastFrameUsedGpuWorld)
            {
                return;
            }

            SpriteDebugProjection[] debugSprites = worldRenderDataBuilder.DebugSprites;
            int count = worldRenderDataBuilder.DebugSpriteCount;
            if (debugSprites == null || count <= 0)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                SpriteDebugProjection projection = debugSprites[i];
                float width = Math.Max(2f, projection.Width);
                float height = Math.Max(2f, projection.Height);
                float x = projection.X - (width * 0.5f);
                float y = projection.Y - (height * 0.5f);
                Color color = projection.Kind == WorldSpriteKind.Enemy
                    ? Color.FromArgb(190, 255, 90, 90)
                    : projection.Kind == WorldSpriteKind.Projectile
                        ? Color.FromArgb(190, 255, 220, 90)
                        : Color.FromArgb(190, 90, 220, 255);
                renderer.DrawRectangle(x, y, width, 1f, color);
                renderer.DrawRectangle(x, y + height - 1f, width, 1f, color);
                renderer.DrawRectangle(x, y, 1f, height, color);
                renderer.DrawRectangle(x + width - 1f, y, 1f, height, color);
                renderer.DrawText(projection.Kind.ToString() + " #" + projection.AtlasSlot, x, Math.Max(0f, y - 10f), color, 8f);
            }
        }
    }
}
