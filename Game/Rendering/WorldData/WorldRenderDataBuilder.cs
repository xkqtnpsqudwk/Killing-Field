using System;
using System.Collections.Generic;
using System.Drawing;
using My2DEngine.Engine.Rendering.Abstractions;
using My2DEngine.Game;
using My2DEngine.Game.Config;
using My2DEngine.Game.Map;
using My2DEngine.Game.Rendering;
using My2DEngine.Game.Systems;

namespace My2DEngine.Rendering.WorldData
{
    /// <summary>
    /// GPU 스프라이트 투영 디버그 출력용 구조체.
    /// 각 스프라이트의 화면 좌표, 크기, 깊이, 아틀라스 슬롯을 담는다.
    /// </summary>
    internal struct SpriteDebugProjection
    {
        /// <summary>스프라이트의 월드 오브젝트 종류(적, 투사체, 픽업 등).</summary>
        public WorldSpriteKind Kind;

        /// <summary>스프라이트 중심의 화면 X 좌표(픽셀).</summary>
        public float X;

        /// <summary>스프라이트 중심의 화면 Y 좌표(픽셀).</summary>
        public float Y;

        /// <summary>스프라이트의 화면 너비(픽셀).</summary>
        public float Width;

        /// <summary>스프라이트의 화면 높이(픽셀).</summary>
        public float Height;

        /// <summary>스프라이트의 월드 공간 깊이(투영 거리).</summary>
        public float Depth;

        /// <summary>이 스프라이트가 할당된 아틀라스 슬롯 인덱스.</summary>
        public int AtlasSlot;
    }

    /// <summary>
    /// 스프라이트 패스 빌드 결과를 담는 구조체.
    /// GPU 렌더러에 전달할 스프라이트 인스턴스 배열과 아틀라스 정보를 포함한다.
    /// </summary>
    internal struct SpritePassBuildResult
    {
        /// <summary>이번 패스에서 렌더링할 스프라이트 인스턴스 배열.</summary>
        public WorldSpriteInstance[] Sprites;

        /// <summary>유효한 스프라이트 인스턴스 수.</summary>
        public int SpriteCount;

        /// <summary>스프라이트 텍스처 아틀라스의 ARGB 픽셀 배열.</summary>
        public int[] AtlasPixels;

        /// <summary>아틀라스 이미지의 너비(픽셀).</summary>
        public int AtlasWidth;

        /// <summary>아틀라스 이미지의 높이(픽셀).</summary>
        public int AtlasHeight;

        /// <summary>아틀라스 전체를 GPU에 재업로드해야 하면 true. 레이아웃이 변경된 경우 설정된다.</summary>
        public bool UploadFullAtlas;

        /// <summary>변경된 아틀라스 셀 영역을 나타내는 정수 배열(x, y, width, height 순으로 4개씩).</summary>
        public int[] DirtyRects;

        /// <summary>DirtyRects에 기록된 더티 사각형의 수.</summary>
        public int DirtyRectCount;
    }

    /// <summary>
    /// 현재 월드 상태를 GPU world presenter가 이해할 수 있는 RenderWorldCommand 데이터로 변환한다.
    /// 벽 열(column) 지오메트리, 깊이 버퍼, 적·투사체·픽업 스프라이트 아틀라스, 레이저 빔 인스턴스를
    /// 매 프레임 조립하여 TryBuild로 반환한다.
    /// </summary>
    internal sealed class WorldRenderDataBuilder
    {
        /// <summary>스프라이트 셀(텍스처) 크기(픽셀). RenderConfig.TextureSize와 동일하다.</summary>
        private const int SpriteCellSize = SpriteAtlasCache.CellSize;

        /// <summary>아틀라스 셀 주변에 추가하는 패딩 픽셀 수. 경계 블리딩 방지용.</summary>
        private const int SpriteAtlasPadding = SpriteAtlasCache.Padding;

        /// <summary>아틀라스 내 각 셀의 실제 스트라이드(SpriteCellSize + 패딩 * 2).</summary>
        private const int SpriteAtlasCellStride = SpriteAtlasCache.CellStride;

        /// <summary>맵 데이터 및 문 상태를 제공하는 매니저.</summary>
        private readonly MapManager mapManager;

        /// <summary>텍스처 및 스프라이트 픽셀 데이터를 제공하는 매니저.</summary>
        private readonly TextureManager textureManager;

        /// <summary>적 및 투사체 목록을 제공하는 매니저.</summary>
        private readonly EnemyManager enemyManager;

        /// <summary>이번 프레임의 적 스프라이트 빌드 항목 목록. 매 프레임 재사용된다.</summary>
        private readonly List<SpriteBuildEntry> enemySpriteEntries = new(48);

        /// <summary>이번 프레임의 기타 스프라이트(투사체·픽업) 빌드 항목 목록. 매 프레임 재사용된다.</summary>
        private readonly List<SpriteBuildEntry> miscSpriteEntries = new(64);

        /// <summary>이번 프레임의 레이저 빔 빌드 항목 목록. 매 프레임 재사용된다.</summary>
        private readonly List<BeamBuildEntry> beamEntries = new(16);

        /// <summary>ComputeVisibleSegments 재사용 버퍼. 매 호출마다 Clear 후 사용한다.</summary>
        private readonly List<(int start, int end)> _segmentBuffer = new(8);

        /// <summary>벽 기둥과 깊이 버퍼.</summary>
        private readonly WallColumnBuilder walls;

        /// <summary>이미지가 없는 투사체의 동그란 스프라이트.</summary>
        private readonly ProjectileSpriteFactory projectileSprites = new();

        /// <summary>적 스프라이트 인스턴스 배열. BuildSpritePass에서 재사용된다.</summary>
        private WorldSpriteInstance[] enemySpriteBuffer;

        /// <summary>기타 스프라이트 인스턴스 배열. BuildSpritePass에서 재사용된다.</summary>
        private WorldSpriteInstance[] miscSpriteBuffer;

        /// <summary>레이저 빔 인스턴스 배열. BuildBeamBuffer에서 재사용된다.</summary>
        private WorldBeamInstance[] beamBuffer;

        /// <summary>적 스프라이트 아틀라스 캐시. 슬롯 할당과 픽셀 더티 여부를 추적한다.</summary>
        private readonly SpriteAtlasCache enemySpriteAtlasCache = new();

        /// <summary>기타 스프라이트 아틀라스 캐시. 슬롯 할당과 픽셀 더티 여부를 추적한다.</summary>
        private readonly SpriteAtlasCache miscSpriteAtlasCache = new();

        /// <summary>스프라이트 디버그 투영 정보 배열. DebugSprites 프로퍼티를 통해 외부에 노출된다.</summary>
        private SpriteDebugProjection[] spriteDebugBuffer;

        /// <summary>이번 프레임에 기록된 스프라이트 디버그 항목 수.</summary>
        private int spriteDebugCount;

        /// <summary>
        /// WorldRenderDataBuilder를 초기화한다.
        /// </summary>
        /// <param name="mapManager">맵 데이터 및 문 상태를 제공하는 매니저.</param>
        /// <param name="textureManager">텍스처 및 스프라이트 픽셀을 제공하는 매니저.</param>
        /// <param name="enemyManager">적 및 투사체 목록을 제공하는 매니저.</param>
        public WorldRenderDataBuilder(MapManager mapManager, TextureManager textureManager, EnemyManager enemyManager)
        {
            this.mapManager = mapManager;
            this.textureManager = textureManager;
            this.enemyManager = enemyManager;
            walls = new WallColumnBuilder(mapManager, textureManager);
        }

        /// <summary>
        /// 현재 플레이어 카메라와 월드 상태를 기반으로 GPU 렌더 커맨드를 조립한다.
        /// 벽 열 지오메트리 빌드 → 깊이 버퍼 스무딩 → CPU 오버레이용 깊이 복사 →
        /// 스프라이트 데이터 빌드 순으로 처리한 뒤 RenderWorldCommand를 반환한다.
        /// </summary>
        /// <param name="player">카메라 위치·방향·투영 평면을 제공하는 플레이어 상태.</param>
        /// <param name="depthBuffer">CPU 오버레이가 차폐 판정에 사용하는 깊이 버퍼(double[]). 이 함수에서 갱신된다.</param>
        /// <param name="rewardPickups">월드에 배치된 보상 픽업 목록.</param>
        /// <param name="playerProjectiles">월드에 배치된 플레이어 투사체 목록.</param>
        /// <param name="renderWidth">렌더 대상 너비(픽셀).</param>
        /// <param name="renderHeight">렌더 대상 높이(픽셀).</param>
        /// <param name="command">빌드된 GPU 렌더 커맨드. 실패 시 기본값.</param>
        /// <returns>커맨드가 성공적으로 빌드되면 true, 필수 데이터가 없거나 맵이 비어 있으면 false.</returns>
        public bool TryBuild(Player player, double[] depthBuffer, IList<RewardPickup> rewardPickups, IList<EnemyProjectile> playerProjectiles, int renderWidth, int renderHeight, out RenderWorldCommand command)
        {
            command = default;
            spriteDebugCount = 0;
            if (player == null || mapManager == null || textureManager == null)
            {
                return false;
            }

            int[,] map = mapManager.Map;
            int[,] textureIds = mapManager.TextureIds;
            if (map == null || textureIds == null)
            {
                return false;
            }

            int mapWidth = map.GetLength(0);
            int mapHeight = map.GetLength(1);
            if (mapWidth <= 0 || mapHeight <= 0)
            {
                return false;
            }

            WorldProjection.GetGpuWorldTargetSize(renderWidth, renderHeight, out int worldTargetWidth, out int worldTargetHeight);
            walls.Build(player, map, textureIds, mapWidth, mapHeight, worldTargetWidth, worldTargetHeight);
            walls.BuildSmoothedDepth();
            walls.CopyDepthForCpuOverlays(depthBuffer, renderWidth, worldTargetWidth);
            int[] wallAtlasPixels = textureManager.GetWallTextureAtlasPixels(out int wallAtlasWidth, out int wallAtlasHeight);
            int[] doorPixels = textureManager.GetDoorOpenTexturePixels(out int doorWidth, out int doorHeight);
            BuildSpriteData(
                player,
                rewardPickups,
                playerProjectiles,
                worldTargetWidth,
                worldTargetHeight,
                out SpritePassBuildResult enemySpritePass,
                out SpritePassBuildResult miscSpritePass,
                out int beamCount);

            command = new RenderWorldCommand
            {
                Camera = new WorldCameraData
                {
                    PositionX = player.Position.X,
                    PositionY = player.Position.Y,
                    DirectionX = player.Direction.X,
                    DirectionY = player.Direction.Y,
                    PlaneX = player.Plane.X,
                    PlaneY = player.Plane.Y
                },
                PlayerEyeZ = player.FloorZ + 0.5f,
                TargetWidth = worldTargetWidth,
                TargetHeight = worldTargetHeight,
                ColumnCount = worldTargetWidth,
                WallColumnGeometry = walls.Geometry,
                WallColumnMaterial = walls.Material,
                WallColumnDoorProgress = walls.DoorProgress,
                DepthBufferLength = walls.Depth?.Length ?? 0,
                DepthBuffer = walls.Depth,
                TextureSize = RenderConfig.WorldTextureSize,
                WallTextureCount = textureManager.WallTextureCount,
                WallTextureAtlasPixels = wallAtlasPixels,
                WallTextureAtlasWidth = wallAtlasWidth,
                WallTextureAtlasHeight = wallAtlasHeight,
                DoorOpenTexturePixels = doorPixels,
                DoorOpenTextureWidth = doorWidth,
                DoorOpenTextureHeight = doorHeight,
                EnemySpritePass = new RenderWorldSpritePassCommand
                {
                    TargetWidth = worldTargetWidth,
                    TargetHeight = worldTargetHeight,
                    DepthBufferLength = walls.SmoothedDepth?.Length ?? 0,
                    DepthBuffer = walls.SmoothedDepth,   // ±2 min-filter 적용본
                    FogDensity = 0.15f,
                    SpriteAtlasPixels = enemySpritePass.SpriteCount > 0 ? enemySpritePass.AtlasPixels : null,
                    SpriteAtlasWidth = enemySpritePass.AtlasWidth,
                    SpriteAtlasHeight = enemySpritePass.AtlasHeight,
                    UploadFullSpriteAtlas = enemySpritePass.UploadFullAtlas,
                    DirtySpriteAtlasRects = enemySpritePass.DirtyRects,
                    DirtySpriteAtlasRectCount = enemySpritePass.DirtyRectCount,
                    Sprites = enemySpritePass.Sprites,
                    SpriteCount = enemySpritePass.SpriteCount
                },
                MiscSpritePass = new RenderWorldSpritePassCommand
                {
                    TargetWidth = worldTargetWidth,
                    TargetHeight = worldTargetHeight,
                    DepthBufferLength = walls.SmoothedDepth?.Length ?? 0,
                    DepthBuffer = walls.SmoothedDepth,   // ±2 min-filter 적용본
                    FogDensity = 0.15f,
                    SpriteAtlasPixels = miscSpritePass.SpriteCount > 0 ? miscSpritePass.AtlasPixels : null,
                    SpriteAtlasWidth = miscSpritePass.AtlasWidth,
                    SpriteAtlasHeight = miscSpritePass.AtlasHeight,
                    UploadFullSpriteAtlas = miscSpritePass.UploadFullAtlas,
                    DirtySpriteAtlasRects = miscSpritePass.DirtyRects,
                    DirtySpriteAtlasRectCount = miscSpritePass.DirtyRectCount,
                    Sprites = miscSpritePass.Sprites,
                    SpriteCount = miscSpritePass.SpriteCount
                },
                DoorTileType = WorldConfig.DoorTileType,
                FloorTextureIndex = 3,
                UseTexturedFloor = RenderConfig.GpuWorldUseTexturedFloor,
                CeilingTextureIndex = 0,
                UseTexturedCeiling = RenderConfig.GpuWorldUseTexturedCeiling,
                CeilingBlend = 0.22f,
                NearPlane = RenderConfig.NearPlane,
                NearPlaneSoftness = RenderConfig.NearPlaneSoftness,
                FloorBlend = 0.22f,
                FogDensity = 0.15f,
                FloorColor = RenderConfig.FloorColor,
                CeilingColor = RenderConfig.CeilingColor,
                Beams = beamCount > 0 ? beamBuffer : null,
                BeamCount = beamCount
            };
            return true;
        }

        /// <summary>이번 프레임에 기록된 스프라이트 디버그 항목 수를 반환한다.</summary>
        public int DebugSpriteCount => spriteDebugCount;

        /// <summary>이번 프레임의 스프라이트 디버그 투영 정보 배열을 반환한다. DebugSpriteCount만큼만 유효하다.</summary>
        public SpriteDebugProjection[] DebugSprites => spriteDebugBuffer;

        /// <summary>
        /// 방/층 전환 시 월드 렌더 임시 캐시를 비운다.
        /// 큰 아틀라스/인스턴스 버퍼가 이전 방 최대치로 남는 것을 막기 위한 리셋이다.
        /// </summary>
        public void ResetTransientCaches()
        {
            enemySpriteEntries.Clear();
            miscSpriteEntries.Clear();
            beamEntries.Clear();
            _segmentBuffer.Clear();

            enemySpriteBuffer = null;
            miscSpriteBuffer = null;
            beamBuffer = null;
            spriteDebugBuffer = null;
            spriteDebugCount = 0;

            enemySpriteAtlasCache.Reset();
            miscSpriteAtlasCache.Reset();
        }

        /// <summary>
        /// 플레이어 카메라 기준으로 적, 투사체, 픽업, 레이저 빔을 화면에 투영하고
        /// 스프라이트 아틀라스 및 빔 버퍼를 조립한다.
        /// </summary>
        /// <param name="player">카메라 위치·방향·투영 평면을 제공하는 플레이어 상태.</param>
        /// <param name="rewardPickups">화면에 투영할 보상 픽업 목록.</param>
        /// <param name="playerProjectiles">화면에 투영할 플레이어 투사체 목록.</param>
        /// <param name="renderWidth">렌더 대상 너비(픽셀).</param>
        /// <param name="renderHeight">렌더 대상 높이(픽셀).</param>
        /// <param name="enemySpritePass">적 스프라이트 패스 빌드 결과(인스턴스 배열 + 아틀라스 정보).</param>
        /// <param name="miscSpritePass">기타 스프라이트(투사체·픽업) 패스 빌드 결과.</param>
        /// <param name="beamCount">빌드된 레이저 빔 인스턴스 수.</param>
        private void BuildSpriteData(
            Player player,
            IList<RewardPickup> rewardPickups,
            IList<EnemyProjectile> playerProjectiles,
            int renderWidth,
            int renderHeight,
            out SpritePassBuildResult enemySpritePass,
            out SpritePassBuildResult miscSpritePass,
            out int beamCount)
        {
            enemySpriteEntries.Clear();
            miscSpriteEntries.Clear();
            beamEntries.Clear();
            beamCount = 0;
            enemySpriteAtlasCache.BeginFrame();
            miscSpriteAtlasCache.BeginFrame();
            if (player == null || renderWidth <= 0 || renderHeight <= 0)
            {
                enemySpriteAtlasCache.ReleaseUnusedSlots();
                miscSpriteAtlasCache.ReleaseUnusedSlots();
                enemySpritePass = default;
                miscSpritePass = default;
                return;
            }

            float playerX = player.Position.X;
            float playerY = player.Position.Y;
            float dirX = player.Direction.X;
            float dirY = player.Direction.Y;
            float planeX = player.Plane.X;
            float planeY = player.Plane.Y;
            double invDet = 1.0 / ((planeX * dirY) - (dirX * planeY));

            Enemy[] enemies = enemyManager?.Enemies;
            if (enemies != null)
            {
                for (int i = 0; i < enemies.Length; i++)
                {
                    Enemy enemy = enemies[i];
                    if (enemy == null || !enemy.IsRenderable)
                    {
                        continue;
                    }

                    Color[] spritePixels = GetEnemySpritePixels(enemy);
                    if (spritePixels == null)
                    {
                        continue;
                    }

                    float dx = enemy.X - playerX;
                    float dy = enemy.Y - playerY;
                    float hitReactRatio = 0f;
                    if (enemy.HitReactTimer > 0f && EnemyConfig.EnemyHitReactDuration > 0f)
                    {
                        hitReactRatio = Math.Min(1f, enemy.HitReactTimer / EnemyConfig.EnemyHitReactDuration);
                    }

                    float enemyRenderScale = enemy.Scale * (1f + (EnemyConfig.EnemyHitReactScalePulse * hitReactRatio));
                    if (!WorldProjection.IsPotentiallyVisible(dx, dy, enemyRenderScale, renderWidth, renderHeight, dirX, dirY, planeX, planeY, invDet))
                    {
                        continue;
                    }

                    float enemyDepth = WorldProjection.GetProjectedDepth(dx, dy, planeX, planeY, invDet);
                    float enemyScreenX = WorldProjection.GetProjectedScreenX(dx, dy, targetWidth: renderWidth, dirX, dirY, planeX, planeY, invDet);
                    float enemyProjectedHeight = Math.Max(6f, WorldProjection.SnapSpriteScale(WorldProjection.GetProjectedHeight(renderHeight, enemyDepth, enemyRenderScale)));
                    float hitReactLift = enemyProjectedHeight * 0.025f * hitReactRatio;
                    float enemyProjectedCenterY = WorldProjection.SnapSpriteCenterY(WorldProjection.GetProjectedCenterY(renderHeight, enemyDepth, enemyRenderScale) - hitReactLift);
                    float enemyProjectedCenterX = WorldProjection.SnapSpriteCenterX(enemyScreenX);
                    int enemySpriteHeight = Math.Abs((int)enemyProjectedHeight);
                    if (enemySpriteHeight <= 0)
                    {
                        continue;
                    }

                    int enemySpriteWidth = enemySpriteHeight;
                    int enemyDrawStartX = (int)(enemyProjectedCenterX - (enemySpriteWidth * 0.5f));
                    int enemyDrawEndX = enemyDrawStartX + enemySpriteWidth - 1;
                    if (!IsSpriteVisibleAgainstDepth(enemyDepth, enemyDrawStartX, enemyDrawEndX))
                    {
                        continue;
                    }

                    ComputeVisibleSegments(enemyDepth, enemyDrawStartX, enemyDrawEndX, _segmentBuffer);
                    int totalEnemyCols = enemyDrawEndX - enemyDrawStartX + 1;
                    bool enemyFirstSegment = true;
                    foreach ((int segStart, int segEnd) in _segmentBuffer)
                    {
                        int segWidth = segEnd - segStart + 1;
                        float tLeft  = (float)(segStart - enemyDrawStartX) / totalEnemyCols;
                        float tRight = (float)(segEnd - enemyDrawStartX + 1) / totalEnemyCols;
                        enemySpriteEntries.Add(new SpriteBuildEntry
                        {
                            SortKey = -enemyDepth,
                            Owner = enemy,
                            SpritePixels = spritePixels,
                            TintHitFlash = enemy.HitFlash > 0f,
                            UVCropLeft = tLeft,
                            UVCropRight = tRight,
                            Instance = new WorldSpriteInstance
                            {
                                Kind = WorldSpriteKind.Enemy,
                                PositionX = segStart + segWidth * 0.5f,
                                PositionY = enemyProjectedCenterY,
                                Scale = enemyProjectedHeight,
                                WidthScale = (float)segWidth / enemyProjectedHeight,
                                VerticalOffsetFactor = enemyDepth,
                                DepthBias = 0.01f,
                                RenderMode = 2f,
                                TintArgb = Color.White.ToArgb(),
                                ShowHealthBar = enemyFirstSegment && enemy.Alive,
                                ShowTelegraph = enemyFirstSegment && (enemy.IsBoss || enemy.IsMiniBoss) && enemy.TelegraphTimer > 0f,
                                MinWallDepth = float.MaxValue,
                            }
                        });
                        enemyFirstSegment = false;
                    }
                }
            }

            IList<EnemyProjectile> projectiles = enemyManager?.EnemyProjectiles;
            if (projectiles != null)
            {
                for (int i = 0; i < projectiles.Count; i++)
                {
                    EnemyProjectile projectile = projectiles[i];
                    if (projectile == null || !projectile.Active)
                    {
                        continue;
                    }

                    if (projectile.Kind == EnemyProjectileKind.LaserBeam)
                    {
                        AddLaserBeamEntries(projectile, playerX, playerY, dirX, dirY, planeX, planeY, invDet, renderWidth, renderHeight);
                        continue;
                    }

                    Color[] spritePixels = projectileSprites.Get(projectile);
                    if (spritePixels == null)
                    {
                        continue;
                    }

                    float dx = projectile.X - playerX;
                    float dy = projectile.Y - playerY;
                    bool isAcidPool = projectile.Kind == EnemyProjectileKind.AcidGlob
                        && Math.Abs(projectile.VelocityX) < 0.001f
                        && Math.Abs(projectile.VelocityY) < 0.001f;
                    float projectileScale = Math.Max(0.18f, projectile.Radius * 0.8f);
                    if (!WorldProjection.IsPotentiallyVisible(dx, dy, projectileScale, renderWidth, renderHeight, dirX, dirY, planeX, planeY, invDet))
                    {
                        continue;
                    }

                    float projectileDepth = WorldProjection.GetProjectedDepth(dx, dy, planeX, planeY, invDet);
                    float projectileScreenX = WorldProjection.GetProjectedScreenX(dx, dy, targetWidth: renderWidth, dirX, dirY, planeX, planeY, invDet);
                    float projectileProjectedHeight = WorldProjection.SnapSpriteScale(WorldProjection.GetProjectedHeight(renderHeight, projectileDepth, projectileScale));
                    float projectileProjectedCenterY;
                    if (isAcidPool)
                    {
                        float safeDepth = Math.Max(0.01f, projectileDepth);
                        projectileProjectedCenterY = WorldProjection.SnapSpriteCenterY(renderHeight * 0.5f + (renderHeight * 0.34f) / safeDepth);
                    }
                    else
                    {
                        projectileProjectedCenterY = WorldProjection.SnapSpriteCenterY(WorldProjection.GetProjectedCenterY(renderHeight, projectileDepth, projectileScale));
                    }
                    float projectileProjectedCenterX = WorldProjection.SnapSpriteCenterX(projectileScreenX);
                    int projectileSpriteHeight = Math.Abs((int)projectileProjectedHeight);
                    if (projectileSpriteHeight <= 4)
                    {
                        continue;
                    }

                    int projectileSpriteWidth = isAcidPool ? (int)(projectileSpriteHeight * 1.8f) : projectileSpriteHeight;
                    int projectileDrawStartX = (int)(projectileProjectedCenterX - (projectileSpriteWidth * 0.5f));
                    int projectileDrawEndX = projectileDrawStartX + projectileSpriteWidth - 1;
                    if (!IsSpriteVisibleAgainstDepth(projectileDepth, projectileDrawStartX, projectileDrawEndX))
                    {
                        continue;
                    }

                    ComputeVisibleSegments(projectileDepth, projectileDrawStartX, projectileDrawEndX, _segmentBuffer);
                    int totalProjectileCols = projectileDrawEndX - projectileDrawStartX + 1;
                    foreach ((int segStart, int segEnd) in _segmentBuffer)
                    {
                        int segWidth = segEnd - segStart + 1;
                        float tLeft  = (float)(segStart - projectileDrawStartX) / totalProjectileCols;
                        float tRight = (float)(segEnd - projectileDrawStartX + 1) / totalProjectileCols;
                        miscSpriteEntries.Add(new SpriteBuildEntry
                        {
                            SortKey = -projectileDepth,
                            Owner = projectile,
                            SpritePixels = spritePixels,
                            UVCropLeft = tLeft,
                            UVCropRight = tRight,
                            Instance = new WorldSpriteInstance
                            {
                                Kind = WorldSpriteKind.Projectile,
                                PositionX = segStart + segWidth * 0.5f,
                                PositionY = projectileProjectedCenterY,
                                Scale = projectileProjectedHeight,
                                WidthScale = (float)segWidth / projectileProjectedHeight,
                                VerticalOffsetFactor = projectileDepth,
                                DepthBias = 0.12f,
                                TintArgb = Color.White.ToArgb(),
                                MinWallDepth = float.MaxValue,
                            }
                        });
                    }
                }
            }

            if (playerProjectiles != null)
            {
                for (int i = 0; i < playerProjectiles.Count; i++)
                {
                    EnemyProjectile projectile = playerProjectiles[i];
                    if (projectile == null || !projectile.Active)
                    {
                        continue;
                    }

                    Color[] spritePixels = projectileSprites.Get(projectile);
                    if (spritePixels == null)
                    {
                        continue;
                    }

                    float dx = projectile.X - playerX;
                    float dy = projectile.Y - playerY;
                    float projectileScale = Math.Max(0.18f, projectile.Radius * 0.8f);
                    if (!WorldProjection.IsPotentiallyVisible(dx, dy, projectileScale, renderWidth, renderHeight, dirX, dirY, planeX, planeY, invDet))
                    {
                        continue;
                    }

                    float projectileDepth = WorldProjection.GetProjectedDepth(dx, dy, planeX, planeY, invDet);
                    float projectileScreenX = WorldProjection.GetProjectedScreenX(dx, dy, targetWidth: renderWidth, dirX, dirY, planeX, planeY, invDet);
                    float projectileProjectedHeight = WorldProjection.SnapSpriteScale(WorldProjection.GetProjectedHeight(renderHeight, projectileDepth, projectileScale));
                    float projectileProjectedCenterY = WorldProjection.SnapSpriteCenterY(WorldProjection.GetProjectedCenterY(renderHeight, projectileDepth, projectileScale));
                    float projectileProjectedCenterX = WorldProjection.SnapSpriteCenterX(projectileScreenX);
                    int projectileSpriteHeight = Math.Abs((int)projectileProjectedHeight);
                    if (projectileSpriteHeight <= 4)
                    {
                        continue;
                    }

                    int projectileSpriteWidth = projectileSpriteHeight;
                    int projectileDrawStartX = (int)(projectileProjectedCenterX - (projectileSpriteWidth * 0.5f));
                    int projectileDrawEndX = projectileDrawStartX + projectileSpriteWidth - 1;
                    if (!IsSpriteVisibleAgainstDepth(projectileDepth, projectileDrawStartX, projectileDrawEndX))
                    {
                        continue;
                    }

                    ComputeVisibleSegments(projectileDepth, projectileDrawStartX, projectileDrawEndX, _segmentBuffer);
                    int totalProjectileCols = projectileDrawEndX - projectileDrawStartX + 1;
                    foreach ((int segStart, int segEnd) in _segmentBuffer)
                    {
                        int segWidth = segEnd - segStart + 1;
                        float tLeft = (float)(segStart - projectileDrawStartX) / totalProjectileCols;
                        float tRight = (float)(segEnd - projectileDrawStartX + 1) / totalProjectileCols;
                        miscSpriteEntries.Add(new SpriteBuildEntry
                        {
                            SortKey = -projectileDepth,
                            Owner = projectile,
                            SpritePixels = spritePixels,
                            UVCropLeft = tLeft,
                            UVCropRight = tRight,
                            Instance = new WorldSpriteInstance
                            {
                                Kind = WorldSpriteKind.Projectile,
                                PositionX = segStart + segWidth * 0.5f,
                                PositionY = projectileProjectedCenterY,
                                Scale = projectileProjectedHeight,
                                WidthScale = (float)segWidth / projectileProjectedHeight,
                                VerticalOffsetFactor = projectileDepth,
                                DepthBias = 0.12f,
                                TintArgb = Color.White.ToArgb(),
                                MinWallDepth = float.MaxValue,
                            }
                        });
                    }
                }
            }

            if (rewardPickups != null)
            {
                for (int i = 0; i < rewardPickups.Count; i++)
                {
                    RewardPickup pickup = rewardPickups[i];
                    if (pickup == null || !pickup.Active)
                    {
                        continue;
                    }

                    Color[] spritePixels = textureManager.GetPickupSprite(pickup.Kind);
                    if (spritePixels == null)
                    {
                        continue;
                    }

                    float dx = pickup.X - playerX;
                    float dy = pickup.Y - playerY;
                    if (!WorldProjection.IsPotentiallyVisible(dx, dy, 0.42f, renderWidth, renderHeight, dirX, dirY, planeX, planeY, invDet))
                    {
                        continue;
                    }

                    float pickupDepth = WorldProjection.GetProjectedDepth(dx, dy, planeX, planeY, invDet);
                    float pickupScreenX = WorldProjection.GetProjectedScreenX(dx, dy, targetWidth: renderWidth, dirX, dirY, planeX, planeY, invDet);
                    float pickupProjectedHeight = WorldProjection.SnapSpriteScale(WorldProjection.GetProjectedHeight(renderHeight, pickupDepth, 0.42f));
                    float pickupProjectedCenterY = WorldProjection.SnapSpriteCenterY(WorldProjection.GetProjectedPickupCenterY(renderHeight, pickupDepth, pickup.PulseTimer, 0.42f));
                    float pickupProjectedCenterX = WorldProjection.SnapSpriteCenterX(pickupScreenX);
                    int pickupSpriteHeight = Math.Abs((int)pickupProjectedHeight);
                    if (pickupSpriteHeight <= 8)
                    {
                        continue;
                    }

                    int pickupSpriteWidth = Math.Max(12, (int)(pickupSpriteHeight * 0.82f));
                    int pickupDrawStartX = (int)(pickupProjectedCenterX - (pickupSpriteWidth * 0.5f));
                    int pickupDrawEndX = pickupDrawStartX + pickupSpriteWidth - 1;
                    if (!IsSpriteVisibleAgainstDepth(pickupDepth, pickupDrawStartX, pickupDrawEndX))
                    {
                        continue;
                    }

                    ComputeVisibleSegments(pickupDepth, pickupDrawStartX, pickupDrawEndX, _segmentBuffer);
                    int totalPickupCols = pickupDrawEndX - pickupDrawStartX + 1;
                    foreach ((int segStart, int segEnd) in _segmentBuffer)
                    {
                        int segWidth = segEnd - segStart + 1;
                        float tLeft  = (float)(segStart - pickupDrawStartX) / totalPickupCols;
                        float tRight = (float)(segEnd - pickupDrawStartX + 1) / totalPickupCols;
                        miscSpriteEntries.Add(new SpriteBuildEntry
                        {
                            SortKey = -pickupDepth,
                            Owner = pickup,
                            SpritePixels = spritePixels,
                            UVCropLeft = tLeft,
                            UVCropRight = tRight,
                            Instance = new WorldSpriteInstance
                            {
                                Kind = WorldSpriteKind.Pickup,
                                PositionX = segStart + segWidth * 0.5f,
                                PositionY = pickupProjectedCenterY,
                                Scale = pickupProjectedHeight,
                                WidthScale = (float)segWidth / pickupProjectedHeight,
                                VerticalOffsetFactor = pickupDepth,
                                DepthBias = 0.2f,
                                TintArgb = Color.White.ToArgb(),
                                MinWallDepth = float.MaxValue,
                            }
                        });
                    }
                }
            }

            beamCount = BuildBeamBuffer();

            EnsureDebugSpriteBuffer(enemySpriteEntries.Count + miscSpriteEntries.Count);
            spriteDebugCount = 0;
            enemySpritePass = BuildSpritePass(
                enemySpriteEntries,
                enemySpriteAtlasCache,
                ref enemySpriteBuffer,
                true);
            miscSpritePass = BuildSpritePass(
                miscSpriteEntries,
                miscSpriteAtlasCache,
                ref miscSpriteBuffer,
                false);
        }

        /// <summary>
        /// 스프라이트 빌드 항목 목록을 아틀라스에 정렬·업로드하고
        /// WorldSpriteInstance 배열을 채워 SpritePassBuildResult로 반환한다.
        /// </summary>
        /// <param name="entries">이번 패스에서 렌더링할 스프라이트 빌드 항목 목록.</param>
        /// <param name="atlasCache">슬롯 할당과 픽셀 더티 상태를 관리하는 아틀라스 캐시.</param>
        /// <param name="spriteBuffer">인스턴스를 기록할 버퍼. 크기가 부족하면 재할당된다.</param>
        /// <param name="forceRefreshAllCells">true이면 변경 여부와 관계없이 모든 셀을 아틀라스에 재복사한다.</param>
        /// <returns>인스턴스 배열, 아틀라스 픽셀, 더티 사각형 정보를 담은 빌드 결과.</returns>
        private SpritePassBuildResult BuildSpritePass(
            List<SpriteBuildEntry> entries,
            SpriteAtlasCache atlasCache,
            ref WorldSpriteInstance[] spriteBuffer,
            bool forceRefreshAllCells)
        {
            if (entries.Count == 0)
            {
                atlasCache.UploadFullAtlas = false;
                atlasCache.DirtyRectCount = 0;
                atlasCache.ReleaseUnusedSlots();
                atlasCache.TrimUnusedTail();
                return default;
            }

            atlasCache.ActiveSlotSpan = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                SpriteBuildEntry entry = entries[i];
                entry.AtlasSlot = atlasCache.GetOrCreateSlot(entry.Owner);
                atlasCache.PreviousUsed[entry.AtlasSlot] = true;
                atlasCache.ActiveSlotSpan = Math.Max(atlasCache.ActiveSlotSpan, entry.AtlasSlot + 1);
                entries[i] = entry;
            }

            atlasCache.ReleaseUnusedSlots();
            atlasCache.TrimUnusedTail();

            entries.Sort((a, b) =>
            {
                double diff = a.SortKey - b.SortKey;
                if (Math.Abs(diff) > 0.001)
                {
                    return diff < 0 ? -1 : 1;
                }

                return a.AtlasSlot.CompareTo(b.AtlasSlot);
            });

            EnsureSpriteBuffers(ref spriteBuffer, entries.Count);
            SpriteAtlasCache.GetDimensions(atlasCache.PreviousSpriteCount, out int atlasWidth, out int atlasHeight);
            bool atlasLayoutChanged = atlasWidth != atlasCache.PreviousAtlasWidth || atlasHeight != atlasCache.PreviousAtlasHeight;
            atlasCache.UploadFullAtlas = atlasLayoutChanged;
            atlasCache.DirtyRectCount = 0;
            atlasCache.EnsurePixelCapacity(atlasWidth, atlasHeight);
            if (atlasLayoutChanged)
            {
                Array.Clear(atlasCache.Pixels, 0, atlasWidth * atlasHeight);
                atlasCache.PreviousAtlasWidth = atlasWidth;
                atlasCache.PreviousAtlasHeight = atlasHeight;
            }

            int columns = Math.Max(1, atlasWidth / SpriteAtlasCellStride);
            for (int i = 0; i < entries.Count; i++)
            {
                SpriteBuildEntry entry = entries[i];
                int cellX = entry.AtlasSlot % columns;
                int cellY = entry.AtlasSlot / columns;
                int atlasCellX = cellX * SpriteAtlasCellStride;
                int atlasCellY = cellY * SpriteAtlasCellStride;
                int atlasPixelX = atlasCellX + SpriteAtlasPadding;
                int atlasPixelY = atlasCellY + SpriteAtlasPadding;

                if (forceRefreshAllCells ||
                    atlasLayoutChanged ||
                    atlasCache.PreviousSpriteSources[entry.AtlasSlot] != entry.SpritePixels ||
                    atlasCache.PreviousSpriteTintFlags[entry.AtlasSlot] != entry.TintHitFlash)
                {
                    SpriteAtlasCache.CopySprite(atlasCache.Pixels, entry.SpritePixels, atlasCellX, atlasCellY, atlasWidth, entry.TintHitFlash);
                    if (!atlasCache.UploadFullAtlas)
                    {
                        atlasCache.AddDirtyRect(atlasCellX, atlasCellY, SpriteAtlasCellStride, SpriteAtlasCellStride);
                    }

                    atlasCache.PreviousSpriteSources[entry.AtlasSlot] = entry.SpritePixels;
                    atlasCache.PreviousSpriteTintFlags[entry.AtlasSlot] = entry.TintHitFlash;
                }

                WorldSpriteInstance instance = entry.Instance;
                instance.AtlasIndex = entry.AtlasSlot;
                float cellU0 = atlasPixelX;
                float cellU1 = atlasPixelX + SpriteCellSize - 1;
                if (entry.UVCropRight > 0f)
                {
                    float cellUWidth = cellU1 - cellU0;
                    instance.U0 = cellU0 + entry.UVCropLeft * cellUWidth;
                    instance.U1 = cellU0 + entry.UVCropRight * cellUWidth;
                }
                else
                {
                    instance.U0 = cellU0;
                    instance.U1 = cellU1;
                }

                instance.V0 = atlasPixelY;
                instance.V1 = atlasPixelY + SpriteCellSize - 1;
                spriteBuffer[i] = instance;
                AddSpriteDebugProjection(instance, entry.AtlasSlot);
            }
            return new SpritePassBuildResult
            {
                Sprites = spriteBuffer,
                SpriteCount = entries.Count,
                AtlasPixels = atlasCache.Pixels,
                AtlasWidth = atlasWidth,
                AtlasHeight = atlasHeight,
                UploadFullAtlas = atlasCache.UploadFullAtlas,
                DirtyRects = atlasCache.DirtyRects,
                DirtyRectCount = atlasCache.DirtyRectCount
            };
        }

        /// <summary>
        /// beamEntries를 깊이 오름차순으로 정렬한 뒤 beamBuffer에 WorldBeamInstance를 기록한다.
        /// </summary>
        /// <returns>빌드된 레이저 빔 인스턴스 수. beamEntries가 비어 있으면 0.</returns>
        private int BuildBeamBuffer()
        {
            if (beamEntries.Count <= 0)
            {
                return 0;
            }

            if (beamBuffer == null || beamBuffer.Length < beamEntries.Count)
            {
                beamBuffer = new WorldBeamInstance[Math.Max(beamEntries.Count, beamBuffer == null ? 8 : beamBuffer.Length * 2)];
            }

            beamEntries.Sort((a, b) => a.SortKey.CompareTo(b.SortKey));
            for (int i = 0; i < beamEntries.Count; i++)
            {
                beamBuffer[i] = beamEntries[i].Instance;
            }

            return beamEntries.Count;
        }

        /// <summary>
        /// 적의 현재 애니메이션 프레임 픽셀 배열을 반환한다.
        /// 애니메이션 프레임이 없으면 정적 스프라이트를 사용하고, 그것도 없으면 null을 반환한다.
        /// </summary>
        /// <param name="enemy">픽셀을 가져올 적 인스턴스.</param>
        /// <returns>현재 프레임의 픽셀 배열(Color[]). 유효한 스프라이트가 없으면 null.</returns>
        private Color[] GetEnemySpritePixels(Enemy enemy)
        {
            Color[] sprite = textureManager.GetAnimatedEnemySprite(enemy) ?? enemy.Sprite;
            if (sprite == null)
            {
                return null;
            }

            return sprite;
        }

        /// <summary>
        /// 레이저 빔 투사체를 코어 레이어와 글로우 레이어 두 개의 BeamBuildEntry로 분해하여 beamEntries에 추가한다.
        /// 빔 길이가 너무 짧으면 추가하지 않는다.
        /// </summary>
        /// <param name="projectile">레이저 빔 투사체 인스턴스.</param>
        /// <param name="playerX">플레이어 월드 X 좌표.</param>
        /// <param name="playerY">플레이어 월드 Y 좌표.</param>
        /// <param name="dirX">카메라 방향 벡터 X 성분.</param>
        /// <param name="dirY">카메라 방향 벡터 Y 성분.</param>
        /// <param name="planeX">투영 평면 벡터 X 성분.</param>
        /// <param name="planeY">투영 평면 벡터 Y 성분.</param>
        /// <param name="invDet">투영 행렬 역행렬의 행렬식 역수.</param>
        /// <param name="renderWidth">렌더 대상 너비(픽셀).</param>
        /// <param name="renderHeight">렌더 대상 높이(픽셀).</param>
        private void AddLaserBeamEntries(
            EnemyProjectile projectile,
            float playerX,
            float playerY,
            float dirX,
            float dirY,
            float planeX,
            float planeY,
            double invDet,
            int renderWidth,
            int renderHeight)
        {
            float dx = projectile.EndX - projectile.X;
            float dy = projectile.EndY - projectile.Y;
            float length = (float)Math.Sqrt((dx * dx) + (dy * dy));
            if (length <= 0.05f)
            {
                return;
            }

            Color coreTint = projectile.WarmupTimer > 0f || projectile.Damage <= 0f
                ? Color.FromArgb(255, 255, 232, 182)
                : Color.FromArgb(255, 255, 132, 96);
            AddLaserBeamEntry(projectile.X, projectile.Y, projectile.EndX, projectile.EndY,
                projectile, playerX, playerY, dirX, dirY, planeX, planeY, invDet, renderWidth, renderHeight, 1f, 0.03f, coreTint);

            Color glowTint = projectile.WarmupTimer > 0f || projectile.Damage <= 0f
                ? Color.FromArgb(120, 255, 235, 170)
                : Color.FromArgb(105, 255, 220, 120);
            AddLaserBeamEntry(projectile.X, projectile.Y, projectile.EndX, projectile.EndY,
                projectile, playerX, playerY, dirX, dirY, planeX, planeY, invDet, renderWidth, renderHeight, 1.65f, 0.015f, glowTint);
        }

        /// <summary>
        /// 레이저 빔의 월드 좌표 구간을 화면 좌표로 투영하고, 하나의 WorldBeamInstance를 beamEntries에 추가한다.
        /// 화면 밖으로 완전히 벗어난 경우 추가하지 않는다.
        /// </summary>
        /// <param name="startWorldX">빔 시작점 월드 X 좌표.</param>
        /// <param name="startWorldY">빔 시작점 월드 Y 좌표.</param>
        /// <param name="endWorldX">빔 끝점 월드 X 좌표.</param>
        /// <param name="endWorldY">빔 끝점 월드 Y 좌표.</param>
        /// <param name="projectile">소유 투사체(두께·웜업 상태 참조용).</param>
        /// <param name="playerX">플레이어 월드 X 좌표.</param>
        /// <param name="playerY">플레이어 월드 Y 좌표.</param>
        /// <param name="dirX">카메라 방향 벡터 X 성분.</param>
        /// <param name="dirY">카메라 방향 벡터 Y 성분.</param>
        /// <param name="planeX">투영 평면 벡터 X 성분.</param>
        /// <param name="planeY">투영 평면 벡터 Y 성분.</param>
        /// <param name="invDet">투영 행렬 역행렬의 행렬식 역수.</param>
        /// <param name="renderWidth">렌더 대상 너비(픽셀).</param>
        /// <param name="renderHeight">렌더 대상 높이(픽셀).</param>
        /// <param name="thicknessMultiplier">기본 두께에 곱할 배율. 글로우 레이어는 1보다 크게 설정한다.</param>
        /// <param name="depthBias">깊이 판정 바이어스. 작을수록 빔이 앞으로 나온다.</param>
        /// <param name="tintOverride">빔 색조 재정의. null이면 흰색을 사용한다.</param>
        private void AddLaserBeamEntry(
            float startWorldX,
            float startWorldY,
            float endWorldX,
            float endWorldY,
            EnemyProjectile projectile,
            float playerX,
            float playerY,
            float dirX,
            float dirY,
            float planeX,
            float planeY,
            double invDet,
            int renderWidth,
            int renderHeight,
            float thicknessMultiplier,
            float depthBias,
            Color? tintOverride)
        {
            float startDx = startWorldX - playerX;
            float startDy = startWorldY - playerY;
            float endDx = endWorldX - playerX;
            float endDy = endWorldY - playerY;
            float startDepth = WorldProjection.GetProjectedDepth(startDx, startDy, planeX, planeY, invDet);
            float endDepth = WorldProjection.GetProjectedDepth(endDx, endDy, planeX, planeY, invDet);
            if (startDepth <= 0.08f && endDepth <= 0.08f)
            {
                return;
            }

            float avgDepth = Math.Max(0.08f, (startDepth + endDepth) * 0.5f);
            float startScreenX = WorldProjection.GetProjectedScreenX(startDx, startDy, renderWidth, dirX, dirY, planeX, planeY, invDet);
            float endScreenX = WorldProjection.GetProjectedScreenX(endDx, endDy, renderWidth, dirX, dirY, planeX, planeY, invDet);
            float thicknessScale = Math.Max(0.12f, projectile.Radius * ((projectile.WarmupTimer > 0f || projectile.Damage <= 0f) ? 0.34f : 0.42f)) * thicknessMultiplier;
            float startEffectiveDepth = Math.Max(0.08f, startDepth <= 0.08f ? avgDepth : startDepth);
            float endEffectiveDepth = Math.Max(0.08f, endDepth <= 0.08f ? avgDepth : endDepth);
            float startScreenY = renderHeight * 0.5f + (renderHeight * 0.12f) / startEffectiveDepth;
            float endScreenY = renderHeight * 0.5f + (renderHeight * 0.12f) / endEffectiveDepth;
            const float beamViewportMargin = 32f;
            startScreenX = Math.Max(-beamViewportMargin, Math.Min(renderWidth + beamViewportMargin, startScreenX));
            endScreenX = Math.Max(-beamViewportMargin, Math.Min(renderWidth + beamViewportMargin, endScreenX));
            startScreenY = Math.Max(-beamViewportMargin, Math.Min(renderHeight + beamViewportMargin, startScreenY));
            endScreenY = Math.Max(-beamViewportMargin, Math.Min(renderHeight + beamViewportMargin, endScreenY));
            float segmentDx = endScreenX - startScreenX;
            float segmentDy = endScreenY - startScreenY;
            float segmentScreenLength = (float)Math.Sqrt((segmentDx * segmentDx) + (segmentDy * segmentDy));
            if (segmentScreenLength <= 1f)
            {
                return;
            }

            float spriteHeight = Math.Max(3f, WorldProjection.GetProjectedHeight(renderHeight, avgDepth, thicknessScale));
            float maxBeamThickness = renderHeight * 0.05f;
            if (spriteHeight > maxBeamThickness)
            {
                spriteHeight = maxBeamThickness;
            }
            float widthScale = Math.Max(1f, segmentScreenLength / Math.Max(1f, spriteHeight));
            float centerScreenX = (startScreenX + endScreenX) * 0.5f;
            float centerScreenY = (startScreenY + endScreenY) * 0.5f;
            if (centerScreenX < -64f || centerScreenX > renderWidth + 64f || centerScreenY < -64f || centerScreenY > renderHeight + 64f)
            {
                return;
            }

            beamEntries.Add(new BeamBuildEntry
            {
                SortKey = -avgDepth,
                Instance = new WorldBeamInstance
                {
                    PositionX = centerScreenX,
                    PositionY = centerScreenY,
                    Scale = spriteHeight,
                    WidthScale = widthScale,
                    SourceWidth = renderWidth,
                    SourceHeight = renderHeight,
                    Depth = avgDepth,
                    DepthBias = depthBias,
                    RotationDegrees = (float)(Math.Atan2(segmentDy, segmentDx) * (180.0 / Math.PI)),
                    TintArgb = (tintOverride ?? Color.White).ToArgb()
                }
            });
        }

        /// <summary>
        /// 스프라이트의 화면 X 범위에서 walls.SmoothedDepth(±3 최솟값 필터 깊이)를 검사하여
        /// 적어도 하나의 열에서 스프라이트가 벽 앞에 있는지(depth &lt; 벽 깊이) 판정한다.
        /// </summary>
        /// <param name="depth">스프라이트의 투영 깊이.</param>
        /// <param name="drawStartX">스프라이트 렌더 영역의 왼쪽 X 좌표.</param>
        /// <param name="drawEndX">스프라이트 렌더 영역의 오른쪽 X 좌표.</param>
        /// <returns>가시 열이 하나 이상이면 true, 완전히 차폐되면 false.</returns>
        private bool IsSpriteVisibleAgainstDepth(float depth, int drawStartX, int drawEndX)
        {
            // walls.SmoothedDepth(±3 min-filter 적용본)를 사용한다.
            // CPU RenderEnemies의 zBuffer도 같은 버퍼에서 복사하므로,
            // GPU 제출 판정과 CPU enemyVisible 판정이 동일한 depth 기준으로 일치한다.
            if (walls.SmoothedDepth == null || walls.SmoothedDepth.Length == 0 || drawEndX < drawStartX)
            {
                return false;
            }

            int start = Math.Max(0, drawStartX);
            int end = Math.Min(walls.SmoothedDepth.Length - 1, drawEndX);
            for (int x = start; x <= end; x++)
            {
                if (depth < walls.SmoothedDepth[x])
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 스프라이트가 점유하는 화면 열 [drawStartX, drawEndX]를 walls.SmoothedDepth로 스캔해
        /// 연속된 가시(visible) 구간 목록을 segments에 채운다.
        /// 열 X는 spriteDepth &lt; walls.SmoothedDepth[X] 일 때 가시로 판정한다(open-space 포함).
        /// </summary>
        /// <param name="spriteDepth">스프라이트의 투영 깊이.</param>
        /// <param name="drawStartX">스프라이트 투영 시작 열.</param>
        /// <param name="drawEndX">스프라이트 투영 끝 열(포함).</param>
        /// <param name="segments">결과를 채울 리스트. 호출 전에 Clear된다.</param>
        private void ComputeVisibleSegments(float spriteDepth, int drawStartX, int drawEndX, List<(int start, int end)> segments)
        {
            segments.Clear();
            if (walls.SmoothedDepth == null || walls.SmoothedDepth.Length == 0)
            {
                return;
            }

            int bufLen = walls.SmoothedDepth.Length;
            int xStart = Math.Max(0, drawStartX);
            int xEnd   = Math.Min(bufLen - 1, drawEndX);

            int runStart = -1;
            for (int x = xStart; x <= xEnd; x++)
            {
                bool visible = spriteDepth < walls.SmoothedDepth[x];
                if (visible)
                {
                    if (runStart < 0)
                    {
                        runStart = x;
                    }
                }
                else if (runStart >= 0)
                {
                    segments.Add((runStart, x - 1));
                    runStart = -1;
                }
            }

            if (runStart >= 0)
            {
                segments.Add((runStart, xEnd));
            }
        }

        /// <summary>
        /// 스프라이트 인스턴스 버퍼가 spriteCount 이상의 크기를 가지고 있는지 확인하고 필요하면 재할당한다.
        /// </summary>
        /// <param name="spriteBuffer">확인할 버퍼 참조. 크기가 부족하면 새로 할당된다.</param>
        /// <param name="spriteCount">필요한 최소 인스턴스 수.</param>
        private void EnsureSpriteBuffers(ref WorldSpriteInstance[] spriteBuffer, int spriteCount)
        {
            if (spriteBuffer == null || spriteBuffer.Length < spriteCount)
            {
                spriteBuffer = new WorldSpriteInstance[spriteCount];
            }
        }

        /// <summary>
        /// 스프라이트 디버그 투영 버퍼가 spriteCount 이상의 크기를 가지고 있는지 확인하고 필요하면 재할당한다.
        /// </summary>
        /// <param name="spriteCount">필요한 최소 항목 수.</param>
        private void EnsureDebugSpriteBuffer(int spriteCount)
        {
            if (spriteDebugBuffer == null || spriteDebugBuffer.Length < spriteCount)
            {
                spriteDebugBuffer = new SpriteDebugProjection[spriteCount];
            }
        }

        /// <summary>
        /// 하나의 스프라이트를 아틀라스에 배치하고 GPU에 제출하기 위한 중간 빌드 항목.
        /// 정렬 키, 소유자 참조, 아틀라스 슬롯, 픽셀 소스, 피격 색조 플래그를 포함한다.
        /// </summary>
        private struct SpriteBuildEntry
        {
            /// <summary>원근 정렬 키. 음수 깊이를 사용하여 가까운 스프라이트가 뒤에 위치한다.</summary>
            public double SortKey;

            /// <summary>슬롯 할당 및 해제에 사용하는 소유자 오브젝트 참조(적, 투사체, 픽업 등).</summary>
            public object Owner;

            /// <summary>이 항목에 할당된 아틀라스 슬롯 인덱스.</summary>
            public int AtlasSlot;

            /// <summary>아틀라스에 복사할 스프라이트 픽셀 배열(Color[]).</summary>
            public Color[] SpritePixels;

            /// <summary>true이면 아틀라스 복사 시 피격 빨강 색조를 적용한다.</summary>
            public bool TintHitFlash;

            /// <summary>
            /// 텍스처 가로 크롭 시작 비율 (0..1). UVCropRight가 0이면 크롭 없음(전체).
            /// BuildSpritePass에서 U0에 적용된다.
            /// </summary>
            public float UVCropLeft;

            /// <summary>
            /// 텍스처 가로 크롭 끝 비율 (0..1). 0이면 크롭 없음(전체 = 1로 처리).
            /// BuildSpritePass에서 U1에 적용된다.
            /// </summary>
            public float UVCropRight;

            /// <summary>GPU에 제출할 WorldSpriteInstance 데이터.</summary>
            public WorldSpriteInstance Instance;
        }

        /// <summary>
        /// 하나의 레이저 빔 세그먼트를 GPU에 제출하기 위한 중간 빌드 항목.
        /// 정렬 키와 WorldBeamInstance를 포함한다.
        /// </summary>
        private struct BeamBuildEntry
        {
            /// <summary>깊이 정렬 키. 음수 깊이를 사용하여 가까운 빔이 뒤에 위치한다.</summary>
            public double SortKey;

            /// <summary>GPU에 제출할 WorldBeamInstance 데이터.</summary>
            public WorldBeamInstance Instance;
        }

        /// <summary>
        /// 스프라이트 디버그 투영 정보를 spriteDebugBuffer에 기록하고 spriteDebugCount를 증가시킨다.
        /// </summary>
        /// <param name="instance">투영 정보를 가져올 WorldSpriteInstance.</param>
        /// <param name="atlasSlot">이 스프라이트에 할당된 아틀라스 슬롯 인덱스.</param>
        private void AddSpriteDebugProjection(WorldSpriteInstance instance, int atlasSlot)
        {
            int index = spriteDebugCount++;
            spriteDebugBuffer[index] = new SpriteDebugProjection
            {
                Kind = instance.Kind,
                X = instance.PositionX,
                Y = instance.PositionY,
                Width = instance.Scale * Math.Max(0.01f, instance.WidthScale),
                Height = instance.Scale,
                Depth = instance.VerticalOffsetFactor,
                AtlasSlot = atlasSlot
            };
        }
    }
}
