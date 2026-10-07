using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
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
        /// <summary>스프라이트 셀(텍스처) 크기(픽셀). GameConfig.TextureSize와 동일하다.</summary>
        private const int SpriteCellSize = GameConfig.TextureSize;

        /// <summary>아틀라스 셀 주변에 추가하는 패딩 픽셀 수. 경계 블리딩 방지용.</summary>
        private const int SpriteAtlasPadding = 1;

        /// <summary>아틀라스 내 각 셀의 실제 스트라이드(SpriteCellSize + 패딩 * 2).</summary>
        private const int SpriteAtlasCellStride = SpriteCellSize + (SpriteAtlasPadding * 2);

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

        /// <summary>GPU로 전송하는 열별 원시 깊이 버퍼(float, renderWidth 크기).</summary>
        private float[] depthBufferData;

        /// <summary>CPU ±3 min-filter가 적용된 GPU 전용 깊이 버퍼. 스프라이트 차폐 판정에 사용된다.</summary>
        private float[] gpuDepthBufferData;

        /// <summary>
        /// 각 화면 열의 벽 지오메트리를 담는 버퍼.
        /// 열당 4개의 float(perpWallDist, fullDrawStart, drawStart, drawEnd)으로 구성된다.
        /// </summary>
        private float[] wallColumnGeometryBuffer;

        /// <summary>
        /// 각 화면 열의 벽 재질을 담는 버퍼.
        /// 열당 4개의 int(textureId, texX, side, tileType)으로 구성된다.
        /// </summary>
        private int[] wallColumnMaterialBuffer;

        /// <summary>각 화면 열의 문(door) 개방 진행도를 담는 버퍼(float, renderWidth 크기).</summary>
        private float[] wallColumnDoorProgressBuffer;

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

        /// <summary>산성 구체(AcidGlob) 투사체 스프라이트 픽셀 배열. 최초 요청 시 생성된다.</summary>
        private Color[] acidGlobSprite;

        /// <summary>산성 웅덩이(AcidPool) 투사체 스프라이트 픽셀 배열. 최초 요청 시 생성된다.</summary>
        private Color[] acidPoolSprite;

        /// <summary>일반 적 탄환(EnemyShot) 스프라이트 픽셀 배열. 최초 요청 시 생성된다.</summary>
        private Color[] enemyShotSprite;

        /// <summary>보스 로켓(BossRocket) 투사체 스프라이트 픽셀 배열. 최초 요청 시 생성된다.</summary>
        private Color[] bossRocketSprite;

        /// <summary>플레이어 로켓 폭발(PlayerRocketExplosion) 스프라이트 픽셀 배열. 최초 요청 시 생성된다.</summary>
        private Color[] playerRocketExplosionSprite;

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

            GetGpuWorldTargetSize(renderWidth, renderHeight, out int worldTargetWidth, out int worldTargetHeight);
            BuildWallColumns(player, map, textureIds, mapWidth, mapHeight, worldTargetWidth, worldTargetHeight);
            BuildSmoothedDepthBuffer();
            CopyDepthBufferForCpuOverlays(depthBuffer, renderWidth, worldTargetWidth);
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
                WallColumnGeometry = wallColumnGeometryBuffer,
                WallColumnMaterial = wallColumnMaterialBuffer,
                WallColumnDoorProgress = wallColumnDoorProgressBuffer,
                DepthBufferLength = depthBufferData?.Length ?? 0,
                DepthBuffer = depthBufferData,
                TextureSize = GameConfig.WorldTextureSize,
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
                    DepthBufferLength = gpuDepthBufferData?.Length ?? 0,
                    DepthBuffer = gpuDepthBufferData,   // ±2 min-filter 적용본
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
                    DepthBufferLength = gpuDepthBufferData?.Length ?? 0,
                    DepthBuffer = gpuDepthBufferData,   // ±2 min-filter 적용본
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
                DoorTileType = GameConfig.DoorTileType,
                FloorTextureIndex = 3,
                UseTexturedFloor = GameConfig.GpuWorldUseTexturedFloor,
                CeilingTextureIndex = 0,
                UseTexturedCeiling = GameConfig.GpuWorldUseTexturedCeiling,
                CeilingBlend = 0.22f,
                NearPlane = GameConfig.NearPlane,
                NearPlaneSoftness = GameConfig.NearPlaneSoftness,
                FloorBlend = 0.22f,
                FogDensity = 0.15f,
                FloorColor = GameConfig.FloorColor,
                CeilingColor = GameConfig.CeilingColor,
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

            ResetSpriteAtlasCache(enemySpriteAtlasCache);
            ResetSpriteAtlasCache(miscSpriteAtlasCache);
        }

        /// <summary>
        /// DDA 레이캐스팅으로 각 화면 열의 벽 지오메트리, 재질, 문 개방 진행도, 깊이를 계산하여
        /// wallColumnGeometryBuffer, wallColumnMaterialBuffer, wallColumnDoorProgressBuffer, depthBufferData에 기록한다.
        /// </summary>
        /// <param name="player">카메라 위치·방향·투영 평면을 제공하는 플레이어 상태.</param>
        /// <param name="map">맵 타일 타입 배열.</param>
        /// <param name="textureIds">맵 타일별 텍스처 ID 배열.</param>
        /// <param name="mapWidth">맵 가로 크기(타일).</param>
        /// <param name="mapHeight">맵 세로 크기(타일).</param>
        /// <param name="renderWidth">렌더 대상 너비(열 수).</param>
        /// <param name="renderHeight">렌더 대상 높이(픽셀).</param>
        private void BuildWallColumns(Player player, int[,] map, int[,] textureIds, int mapWidth, int mapHeight, int renderWidth, int renderHeight)
        {
            EnsureWallColumnBuffers(renderWidth);
            int wallTextureCount = Math.Max(1, textureManager.WallTextureCount);

            for (int x = 0; x < renderWidth; x++)
            {
                double cameraX = (2.0 * (x + 0.5) / renderWidth) - 1.0;
                double rayDirX = player.Direction.X + player.Plane.X * cameraX;
                double rayDirY = player.Direction.Y + player.Plane.Y * cameraX;

                int mapX = (int)player.Position.X;
                int mapY = (int)player.Position.Y;

                double deltaDistX = rayDirX == 0 ? 1e30 : Math.Abs(1.0 / rayDirX);
                double deltaDistY = rayDirY == 0 ? 1e30 : Math.Abs(1.0 / rayDirY);

                int stepX;
                int stepY;
                double sideDistX;
                double sideDistY;

                if (rayDirX < 0)
                {
                    stepX = -1;
                    sideDistX = (player.Position.X - mapX) * deltaDistX;
                }
                else
                {
                    stepX = 1;
                    sideDistX = (mapX + 1.0 - player.Position.X) * deltaDistX;
                }

                if (rayDirY < 0)
                {
                    stepY = -1;
                    sideDistY = (player.Position.Y - mapY) * deltaDistY;
                }
                else
                {
                    stepY = 1;
                    sideDistY = (mapY + 1.0 - player.Position.Y) * deltaDistY;
                }

                // 계단 면(riser) 감지용 변수
                float eyeZpre = player.FloorZ + 0.5f;
                float prevTileFloor = player.FloorZ;
                bool stepFaceFound = false;
                float stepFaceDist = 0f;
                float stepFaceTop = 0f;
                float stepFaceBottom = 0f;
                float stepFaceUpperFloor = 0f; // 계단 위 타일의 바닥 높이 (수평면 렌더에 사용)

                bool hit = false;
                int side = 0;
                while (!hit)
                {
                    if (sideDistX < sideDistY)
                    {
                        sideDistX += deltaDistX;
                        mapX += stepX;
                        side = 0;
                    }
                    else
                    {
                        sideDistY += deltaDistY;
                        mapY += stepY;
                        side = 1;
                    }

                    if (mapX < 0 || mapY < 0 || mapX >= mapWidth || mapY >= mapHeight)
                    {
                        break;
                    }

                    if (CollisionSystem.IsSolidType(map[mapX, mapY]))
                    {
                        hit = true;
                        break;
                    }

                    // 바닥 높이 상승 구간 = 계단 면(riser) 감지
                    if (!stepFaceFound)
                    {
                        float tileFloor = mapManager.GetFloorHeight(mapX, mapY);
                        if (tileFloor > prevTileFloor + 0.01f)
                        {
                            double sd = side == 0
                                ? (mapX - player.Position.X + (1 - stepX) / 2.0) / rayDirX
                                : (mapY - player.Position.Y + (1 - stepY) / 2.0) / rayDirY;
                            if (sd > 0.0001)
                            {
                                double sScale = renderHeight / GetRenderDistance(sd);
                                float fTop = (float)(renderHeight * 0.5 + (eyeZpre - tileFloor) * sScale);
                                float fBot = (float)(renderHeight * 0.5 + (eyeZpre - prevTileFloor) * sScale);
                                if (fBot > fTop)
                                {
                                    stepFaceDist = (float)sd;
                                    stepFaceTop = fTop;
                                    stepFaceBottom = fBot;
                                    stepFaceUpperFloor = tileFloor; // 셰이더가 수평면 계산에 사용
                                    stepFaceFound = true;
                                }
                            }
                        }
                        prevTileFloor = tileFloor;
                    }
                }

                int geometryIndex = x * 4;
                int materialIndex = x * 4;
                int stepIndex = renderWidth * 4 + x * 4; // Row 1: 계단 면 데이터
                if (!hit)
                {
                    wallColumnGeometryBuffer[geometryIndex + 0] = -1f;
                    wallColumnGeometryBuffer[geometryIndex + 1] = 0f;
                    wallColumnGeometryBuffer[geometryIndex + 2] = 0f;
                    wallColumnGeometryBuffer[geometryIndex + 3] = 0f;
                    wallColumnGeometryBuffer[stepIndex + 0] = 0f;
                    wallColumnGeometryBuffer[stepIndex + 1] = 0f;
                    wallColumnGeometryBuffer[stepIndex + 2] = 0f;
                    wallColumnGeometryBuffer[stepIndex + 3] = 0f;
                    wallColumnMaterialBuffer[materialIndex + 0] = 0;
                    wallColumnMaterialBuffer[materialIndex + 1] = 0;
                    wallColumnMaterialBuffer[materialIndex + 2] = 0;
                    wallColumnMaterialBuffer[materialIndex + 3] = 0;
                    wallColumnDoorProgressBuffer[x] = 0f;
                    depthBufferData[x] = float.MaxValue;
                    continue;
                }

                double perpWallDist = side == 0
                    ? (mapX - player.Position.X + (1 - stepX) / 2.0) / rayDirX
                    : (mapY - player.Position.Y + (1 - stepY) / 2.0) / rayDirY;

                if (perpWallDist < 0.0001)
                {
                    perpWallDist = 0.0001;
                }

                double renderDist = GetRenderDistance(perpWallDist);
                double scale = renderHeight / renderDist;

                // 높이 기반 drawStart/drawEnd 계산 (Doom 스타일 섹터 높이)
                float eyeZ = player.FloorZ + 0.5f;
                float hitFloor = mapManager.GetFloorHeight(mapX, mapY);
                float hitCeil = mapManager.GetCeilHeight(mapX, mapY);
                int fullDrawStart = (int)(renderHeight * 0.5 - (hitCeil - eyeZ) * scale);
                int drawStart = fullDrawStart;
                int drawEnd = (int)(renderHeight * 0.5 + (eyeZ - hitFloor) * scale);

                // 하위 호환: 기본 높이(floor=0, ceil=1)일 때 기존과 동일한 결과
                int lineHeight = drawEnd - fullDrawStart;

                int tileType = map[mapX, mapY];
                float doorProgress = 0f;
                if (tileType == GameConfig.DoorTileType)
                {
                    doorProgress = mapManager.GetDoorOpenProgress(mapX, mapY);
                    if (doorProgress > 0f)
                    {
                        int visibleHeight = (int)Math.Round(lineHeight * Math.Max(0f, 1f - doorProgress));
                        if (visibleHeight <= 0)
                        {
                            wallColumnGeometryBuffer[geometryIndex + 0] = -1f;
                            wallColumnGeometryBuffer[geometryIndex + 1] = 0f;
                            wallColumnGeometryBuffer[geometryIndex + 2] = 0f;
                            wallColumnGeometryBuffer[geometryIndex + 3] = 0f;
                            wallColumnMaterialBuffer[materialIndex + 0] = 0;
                            wallColumnMaterialBuffer[materialIndex + 1] = 0;
                            wallColumnMaterialBuffer[materialIndex + 2] = 0;
                            wallColumnMaterialBuffer[materialIndex + 3] = 0;
                            wallColumnDoorProgressBuffer[x] = 0f;
                            depthBufferData[x] = float.MaxValue;
                            continue;
                        }

                        drawEnd = fullDrawStart + visibleHeight - 1;
                    }
                }

                double wallX = side == 0
                    ? player.Position.Y + perpWallDist * rayDirY
                    : player.Position.X + perpWallDist * rayDirX;
                wallX -= Math.Floor(wallX);

                // 벽 텍셀 X는 월드 텍스처 해상도(WorldTextureSize) 기준으로 계산한다(아틀라스 셀 크기와 일치).
                int texX = (int)(wallX * GameConfig.WorldTextureSize);
                if (side == 0 && rayDirX > 0) texX = GameConfig.WorldTextureSize - texX - 1;
                if (side == 1 && rayDirY < 0) texX = GameConfig.WorldTextureSize - texX - 1;
                if (texX < 0) texX = 0;
                if (texX >= GameConfig.WorldTextureSize) texX = GameConfig.WorldTextureSize - 1;

                int textureId = textureIds[mapX, mapY] % wallTextureCount;
                if (textureId < 0)
                {
                    textureId += wallTextureCount;
                }

                wallColumnGeometryBuffer[geometryIndex + 0] = (float)perpWallDist;
                wallColumnGeometryBuffer[geometryIndex + 1] = fullDrawStart;
                wallColumnGeometryBuffer[geometryIndex + 2] = drawStart;
                wallColumnGeometryBuffer[geometryIndex + 3] = drawEnd;

                // Row 1: 계단 면(riser) 데이터 (stepDepth=0이면 계단 없음)
                if (stepFaceFound)
                {
                    wallColumnGeometryBuffer[stepIndex + 0] = stepFaceDist;
                    wallColumnGeometryBuffer[stepIndex + 1] = stepFaceTop;
                    wallColumnGeometryBuffer[stepIndex + 2] = stepFaceBottom;
                    wallColumnGeometryBuffer[stepIndex + 3] = stepFaceUpperFloor;
                }
                else
                {
                    wallColumnGeometryBuffer[stepIndex + 0] = 0f;
                    wallColumnGeometryBuffer[stepIndex + 1] = 0f;
                    wallColumnGeometryBuffer[stepIndex + 2] = 0f;
                    wallColumnGeometryBuffer[stepIndex + 3] = 0f;
                }

                wallColumnMaterialBuffer[materialIndex + 0] = textureId;
                wallColumnMaterialBuffer[materialIndex + 1] = texX;
                wallColumnMaterialBuffer[materialIndex + 2] = side;
                wallColumnMaterialBuffer[materialIndex + 3] = tileType;
                wallColumnDoorProgressBuffer[x] = doorProgress;
                depthBufferData[x] = (float)perpWallDist;
            }
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
            BeginSpriteAtlasFrame(enemySpriteAtlasCache);
            BeginSpriteAtlasFrame(miscSpriteAtlasCache);
            if (player == null || renderWidth <= 0 || renderHeight <= 0)
            {
                ReleaseUnusedSpriteAtlasSlots(enemySpriteAtlasCache);
                ReleaseUnusedSpriteAtlasSlots(miscSpriteAtlasCache);
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
                    if (enemy.HitReactTimer > 0f && GameConfig.EnemyHitReactDuration > 0f)
                    {
                        hitReactRatio = Math.Min(1f, enemy.HitReactTimer / GameConfig.EnemyHitReactDuration);
                    }

                    float enemyRenderScale = enemy.Scale * (1f + (GameConfig.EnemyHitReactScalePulse * hitReactRatio));
                    if (!IsPotentiallyVisible(dx, dy, enemyRenderScale, renderWidth, renderHeight, dirX, dirY, planeX, planeY, invDet))
                    {
                        continue;
                    }

                    float enemyDepth = GetProjectedDepth(dx, dy, planeX, planeY, invDet);
                    float enemyScreenX = GetProjectedScreenX(dx, dy, targetWidth: renderWidth, dirX, dirY, planeX, planeY, invDet);
                    float enemyProjectedHeight = Math.Max(6f, SnapSpriteScale(GetProjectedHeight(renderHeight, enemyDepth, enemyRenderScale)));
                    float hitReactLift = enemyProjectedHeight * 0.025f * hitReactRatio;
                    float enemyProjectedCenterY = SnapSpriteCenterY(GetProjectedCenterY(renderHeight, enemyDepth, enemyRenderScale) - hitReactLift);
                    float enemyProjectedCenterX = SnapSpriteCenterX(enemyScreenX);
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

                    Color[] spritePixels = GetProjectileSpritePixels(projectile);
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
                    if (!IsPotentiallyVisible(dx, dy, projectileScale, renderWidth, renderHeight, dirX, dirY, planeX, planeY, invDet))
                    {
                        continue;
                    }

                    float projectileDepth = GetProjectedDepth(dx, dy, planeX, planeY, invDet);
                    float projectileScreenX = GetProjectedScreenX(dx, dy, targetWidth: renderWidth, dirX, dirY, planeX, planeY, invDet);
                    float projectileProjectedHeight = SnapSpriteScale(GetProjectedHeight(renderHeight, projectileDepth, projectileScale));
                    float projectileProjectedCenterY;
                    if (isAcidPool)
                    {
                        float safeDepth = Math.Max(0.01f, projectileDepth);
                        projectileProjectedCenterY = SnapSpriteCenterY(renderHeight * 0.5f + (renderHeight * 0.34f) / safeDepth);
                    }
                    else
                    {
                        projectileProjectedCenterY = SnapSpriteCenterY(GetProjectedCenterY(renderHeight, projectileDepth, projectileScale));
                    }
                    float projectileProjectedCenterX = SnapSpriteCenterX(projectileScreenX);
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

                    Color[] spritePixels = GetProjectileSpritePixels(projectile);
                    if (spritePixels == null)
                    {
                        continue;
                    }

                    float dx = projectile.X - playerX;
                    float dy = projectile.Y - playerY;
                    float projectileScale = Math.Max(0.18f, projectile.Radius * 0.8f);
                    if (!IsPotentiallyVisible(dx, dy, projectileScale, renderWidth, renderHeight, dirX, dirY, planeX, planeY, invDet))
                    {
                        continue;
                    }

                    float projectileDepth = GetProjectedDepth(dx, dy, planeX, planeY, invDet);
                    float projectileScreenX = GetProjectedScreenX(dx, dy, targetWidth: renderWidth, dirX, dirY, planeX, planeY, invDet);
                    float projectileProjectedHeight = SnapSpriteScale(GetProjectedHeight(renderHeight, projectileDepth, projectileScale));
                    float projectileProjectedCenterY = SnapSpriteCenterY(GetProjectedCenterY(renderHeight, projectileDepth, projectileScale));
                    float projectileProjectedCenterX = SnapSpriteCenterX(projectileScreenX);
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
                    if (!IsPotentiallyVisible(dx, dy, 0.42f, renderWidth, renderHeight, dirX, dirY, planeX, planeY, invDet))
                    {
                        continue;
                    }

                    float pickupDepth = GetProjectedDepth(dx, dy, planeX, planeY, invDet);
                    float pickupScreenX = GetProjectedScreenX(dx, dy, targetWidth: renderWidth, dirX, dirY, planeX, planeY, invDet);
                    float pickupProjectedHeight = SnapSpriteScale(GetProjectedHeight(renderHeight, pickupDepth, 0.42f));
                    float pickupProjectedCenterY = SnapSpriteCenterY(GetProjectedPickupCenterY(renderHeight, pickupDepth, pickup.PulseTimer, 0.42f));
                    float pickupProjectedCenterX = SnapSpriteCenterX(pickupScreenX);
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
                ReleaseUnusedSpriteAtlasSlots(atlasCache);
                TrimUnusedSpriteAtlasTail(atlasCache);
                return default;
            }

            atlasCache.ActiveSlotSpan = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                SpriteBuildEntry entry = entries[i];
                entry.AtlasSlot = GetOrCreateSpriteAtlasSlot(atlasCache, entry.Owner);
                atlasCache.PreviousUsed[entry.AtlasSlot] = true;
                atlasCache.ActiveSlotSpan = Math.Max(atlasCache.ActiveSlotSpan, entry.AtlasSlot + 1);
                entries[i] = entry;
            }

            ReleaseUnusedSpriteAtlasSlots(atlasCache);
            TrimUnusedSpriteAtlasTail(atlasCache);

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
            GetSpriteAtlasDimensions(atlasCache.PreviousSpriteCount, out int atlasWidth, out int atlasHeight);
            bool atlasLayoutChanged = atlasWidth != atlasCache.PreviousAtlasWidth || atlasHeight != atlasCache.PreviousAtlasHeight;
            atlasCache.UploadFullAtlas = atlasLayoutChanged;
            atlasCache.DirtyRectCount = 0;
            EnsureSpriteAtlasPixelCapacity(atlasCache, atlasWidth, atlasHeight);
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
                    CopySpriteToAtlas(atlasCache.Pixels, entry.SpritePixels, atlasCellX, atlasCellY, atlasWidth, entry.TintHitFlash);
                    if (!atlasCache.UploadFullAtlas)
                    {
                        AddDirtySpriteAtlasRect(atlasCache, atlasCellX, atlasCellY, SpriteAtlasCellStride, SpriteAtlasCellStride);
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
        /// 아틀라스 캐시의 DirtyRects 배열에 변경된 셀 영역을 추가한다.
        /// 배열 용량이 부족하면 두 배로 확장한다.
        /// </summary>
        /// <param name="atlasCache">더티 사각형을 추가할 아틀라스 캐시.</param>
        /// <param name="x">더티 영역의 왼쪽 X 좌표(아틀라스 픽셀 기준).</param>
        /// <param name="y">더티 영역의 위쪽 Y 좌표(아틀라스 픽셀 기준).</param>
        /// <param name="width">더티 영역의 너비(픽셀).</param>
        /// <param name="height">더티 영역의 높이(픽셀).</param>
        private void AddDirtySpriteAtlasRect(SpriteAtlasCache atlasCache, int x, int y, int width, int height)
        {
            int requiredLength = (atlasCache.DirtyRectCount + 1) * 4;
            if (atlasCache.DirtyRects == null || atlasCache.DirtyRects.Length < requiredLength)
            {
                int newLength = atlasCache.DirtyRects == null ? 16 : atlasCache.DirtyRects.Length * 2;
                while (newLength < requiredLength)
                {
                    newLength *= 2;
                }

                Array.Resize(ref atlasCache.DirtyRects, newLength);
            }

            int baseIndex = atlasCache.DirtyRectCount * 4;
            atlasCache.DirtyRects[baseIndex + 0] = x;
            atlasCache.DirtyRects[baseIndex + 1] = y;
            atlasCache.DirtyRects[baseIndex + 2] = width;
            atlasCache.DirtyRects[baseIndex + 3] = height;
            atlasCache.DirtyRectCount++;
        }

        /// <summary>
        /// 스프라이트 수에 따라 아틀라스의 열·행 수를 결정하고 픽셀 크기를 반환한다.
        /// 가능한 한 정사각형에 가까운 레이아웃을 사용한다.
        /// </summary>
        /// <param name="spriteCount">아틀라스에 배치할 스프라이트 수.</param>
        /// <param name="width">계산된 아틀라스 너비(픽셀). spriteCount가 0 이하이면 0.</param>
        /// <param name="height">계산된 아틀라스 높이(픽셀). spriteCount가 0 이하이면 0.</param>
        private void GetSpriteAtlasDimensions(int spriteCount, out int width, out int height)
        {
            if (spriteCount <= 0)
            {
                width = 0;
                height = 0;
                return;
            }

            int columns = (int)Math.Ceiling(Math.Sqrt(spriteCount));
            int rows = (spriteCount + columns - 1) / columns;
            width = columns * SpriteAtlasCellStride;
            height = rows * SpriteAtlasCellStride;
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
        /// 투사체 종류에 따라 미리 생성된 원형 스프라이트 픽셀 배열을 반환한다.
        /// LaserBeam은 별도 경로에서 처리되므로 이 함수에서는 null을 반환한다.
        /// </summary>
        /// <param name="projectile">픽셀을 가져올 투사체 인스턴스.</param>
        /// <returns>투사체 스프라이트 픽셀 배열. 지원하지 않는 종류이면 null.</returns>
        private Color[] GetProjectileSpritePixels(EnemyProjectile projectile)
        {
            EnsureProjectileSprites();

            switch (projectile.Kind)
            {
                case EnemyProjectileKind.AcidGlob:
                    return Math.Abs(projectile.VelocityX) < 0.001f && Math.Abs(projectile.VelocityY) < 0.001f
                        ? acidPoolSprite
                        : acidGlobSprite;
                case EnemyProjectileKind.EnemyShot:
                    return enemyShotSprite;
                case EnemyProjectileKind.BossRocket:
                case EnemyProjectileKind.PlayerRocket:
                    return bossRocketSprite;
                case EnemyProjectileKind.PlayerRocketExplosion:
                    return playerRocketExplosionSprite;
                default:
                    return null;
            }
        }

        /// <summary>
        /// acidGlobSprite, acidPoolSprite, enemyShotSprite, bossRocketSprite가
        /// 아직 생성되지 않았으면 BuildCircularSprite로 초기화한다.
        /// </summary>
        private void EnsureProjectileSprites()
        {
            acidGlobSprite ??= BuildCircularSprite(
                    Color.FromArgb(255, 95, 255, 115),
                    Color.FromArgb(255, 25, 120, 35),
                    0.82f,
                    0.52f);

            acidPoolSprite ??= BuildCircularSprite(
                    Color.FromArgb(235, 105, 255, 135),
                    Color.FromArgb(210, 30, 120, 35),
                    0.94f,
                    0.7f);

            enemyShotSprite ??= BuildCircularSprite(
                    Color.FromArgb(255, 255, 215, 100),
                    Color.FromArgb(255, 175, 80, 25),
                    0.78f,
                    0.5f);

            bossRocketSprite ??= BuildCircularSprite(
                    Color.FromArgb(255, 255, 165, 70),
                    Color.FromArgb(255, 170, 45, 20),
                    0.86f,
                    0.42f);

            playerRocketExplosionSprite ??= BuildCircularSprite(
                    Color.FromArgb(235, 255, 235, 150),
                    Color.FromArgb(255, 255, 120, 45),
                    0.95f,
                    0.24f);

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
            float startDepth = GetProjectedDepth(startDx, startDy, planeX, planeY, invDet);
            float endDepth = GetProjectedDepth(endDx, endDy, planeX, planeY, invDet);
            if (startDepth <= 0.08f && endDepth <= 0.08f)
            {
                return;
            }

            float avgDepth = Math.Max(0.08f, (startDepth + endDepth) * 0.5f);
            float startScreenX = GetProjectedScreenX(startDx, startDy, renderWidth, dirX, dirY, planeX, planeY, invDet);
            float endScreenX = GetProjectedScreenX(endDx, endDy, renderWidth, dirX, dirY, planeX, planeY, invDet);
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

            float spriteHeight = Math.Max(3f, GetProjectedHeight(renderHeight, avgDepth, thicknessScale));
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
        /// 절차적으로 원형 스프라이트 픽셀 배열을 생성한다.
        /// 중심부터 가장자리까지 innerColor에서 outerColor로 선형 보간되며, 바깥쪽은 투명하다.
        /// </summary>
        /// <param name="outerColor">스프라이트 외곽 색상.</param>
        /// <param name="innerColor">스프라이트 중심 색상.</param>
        /// <param name="outerRadius">외곽 반지름(정규화, 0~1 범위). 이 값 바깥은 투명하다.</param>
        /// <param name="innerRadius">내부 단색 반지름(정규화). 이 값 안쪽은 innerColor로 채워진다.</param>
        /// <returns>GameConfig.TextureSize × TextureSize 크기의 Color[] 픽셀 배열.</returns>
        private Color[] BuildCircularSprite(Color outerColor, Color innerColor, float outerRadius, float innerRadius)
        {
            var pixels = new Color[SpriteCellSize * SpriteCellSize];
            float center = (SpriteCellSize - 1) * 0.5f;
            float invRadius = 1f / center;

            for (int y = 0; y < SpriteCellSize; y++)
            {
                for (int x = 0; x < SpriteCellSize; x++)
                {
                    float nx = (x - center) * invRadius;
                    float ny = (y - center) * invRadius;
                    float dist = (float)Math.Sqrt((nx * nx) + (ny * ny));
                    int index = (y * SpriteCellSize) + x;

                    if (dist > outerRadius)
                    {
                        pixels[index] = Color.Transparent;
                    }
                    else if (dist <= innerRadius)
                    {
                        pixels[index] = innerColor;
                    }
                    else
                    {
                        float blend = (dist - innerRadius) / Math.Max(0.001f, outerRadius - innerRadius);
                        pixels[index] = LerpColor(innerColor, outerColor, blend);
                    }
                }
            }

            return pixels;
        }

        /// <summary>
        /// 두 Color 값을 선형 보간한다. t=0이면 a, t=1이면 b를 반환한다.
        /// </summary>
        /// <param name="a">시작 색상.</param>
        /// <param name="b">끝 색상.</param>
        /// <param name="t">보간 비율(0~1). 범위를 벗어나면 클램핑된다.</param>
        /// <returns>보간??Color.</returns>
        private static Color LerpColor(Color a, Color b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            int aa = (int)(a.A + ((b.A - a.A) * t));
            int rr = (int)(a.R + ((b.R - a.R) * t));
            int gg = (int)(a.G + ((b.G - a.G) * t));
            int bb = (int)(a.B + ((b.B - a.B) * t));
            return Color.FromArgb(aa, rr, gg, bb);
        }

        /// <summary>
        /// 스프라이트 픽셀 배열을 아틀라스 버퍼의 지정 셀에 복사한다.
        /// 패딩 픽셀에는 가장자리 색을 복제하여 확대/회전 시 이웃 셀이 섞이지 않도록 한다.
        /// tintHitFlash가 true이면 불투명 픽셀의 R 채널을 255로 올려 피격 효과를 표현한다.
        /// </summary>
        /// <param name="atlasPixels">대상 아틀라스 ARGB 픽셀 배열.</param>
        /// <param name="spritePixels">복사할 소스 스프라이트 Color[] 픽셀 배열.</param>
        /// <param name="atlasCellX">아틀라스 내 셀의 왼쪽 X 좌표(패딩 포함).</param>
        /// <param name="atlasCellY">아틀라스 내 셀의 위쪽 Y 좌표(패딩 포함).</param>
        /// <param name="atlasWidth">아틀라스 이미지의 너비(픽셀).</param>
        /// <param name="tintHitFlash">true이면 불투명 픽셀에 피격 빨강 색조를 적용한다.</param>
        private void CopySpriteToAtlas(int[] atlasPixels, Color[] spritePixels, int atlasCellX, int atlasCellY, int atlasWidth, bool tintHitFlash)
        {
            if (spritePixels == null)
            {
                return;
            }

            int atlasX = atlasCellX + SpriteAtlasPadding;
            int atlasY = atlasCellY + SpriteAtlasPadding;
            for (int y = 0; y < SpriteCellSize; y++)
            {
                int sourceRow = y * SpriteCellSize;
                int atlasRow = (atlasY + y) * atlasWidth;
                for (int x = 0; x < SpriteCellSize; x++)
                {
                    Color color = spritePixels[sourceRow + x];
                    if (tintHitFlash && color.A > 0)
                    {
                        color = Color.FromArgb(color.A, 255, Math.Min(255, color.G + 40), Math.Min(255, color.B + 40));
                    }

                    atlasPixels[atlasRow + atlasX + x] = color.ToArgb();
                }
            }

            // 셀 바깥 1픽셀에 가장자리 색을 복제해 두면 확대/회전 중에도
            // 이웃 atlas 슬롯 색이 섞이지 않아 스프라이트 깨짐이 줄어든다.
            for (int y = 0; y < SpriteCellSize; y++)
            {
                int row = atlasY + y;
                int baseIndex = row * atlasWidth;
                atlasPixels[baseIndex + atlasCellX] = atlasPixels[baseIndex + atlasX];
                atlasPixels[baseIndex + atlasX + SpriteCellSize] = atlasPixels[baseIndex + atlasX + SpriteCellSize - 1];
            }

            int topRow = atlasY * atlasWidth;
            int bottomRow = (atlasY + SpriteCellSize - 1) * atlasWidth;
            int paddingTopRow = atlasCellY * atlasWidth;
            int paddingBottomRow = (atlasY + SpriteCellSize) * atlasWidth;
            for (int x = 0; x < SpriteCellSize; x++)
            {
                atlasPixels[paddingTopRow + atlasX + x] = atlasPixels[topRow + atlasX + x];
                atlasPixels[paddingBottomRow + atlasX + x] = atlasPixels[bottomRow + atlasX + x];
            }

            atlasPixels[(atlasCellY * atlasWidth) + atlasCellX] = atlasPixels[(atlasY * atlasWidth) + atlasX];
            atlasPixels[(atlasCellY * atlasWidth) + atlasX + SpriteCellSize] = atlasPixels[(atlasY * atlasWidth) + atlasX + SpriteCellSize - 1];
            atlasPixels[((atlasY + SpriteCellSize) * atlasWidth) + atlasCellX] = atlasPixels[((atlasY + SpriteCellSize - 1) * atlasWidth) + atlasX];
            atlasPixels[((atlasY + SpriteCellSize) * atlasWidth) + atlasX + SpriteCellSize] = atlasPixels[((atlasY + SpriteCellSize - 1) * atlasWidth) + atlasX + SpriteCellSize - 1];
        }

        /// <summary>
        /// 오브젝트가 화면에 투영될 가능성이 있는지 빠르게 판별한다.
        /// 카메라 뒤에 있거나 너무 작거나 화면 밖으로 완전히 벗어난 경우 false를 반환한다.
        /// </summary>
        /// <param name="dx">오브젝트 - 플레이어의 월드 X 차이.</param>
        /// <param name="dy">오브젝트 - 플레이어의 월드 Y 차이.</param>
        /// <param name="scale">오브젝트의 월드 공간 크기(스케일).</param>
        /// <param name="renderWidth">렌더 대상 너비(픽셀).</param>
        /// <param name="renderHeight">렌더 대상 높이(픽셀).</param>
        /// <param name="dirX">카메라 방향 벡터 X 성분.</param>
        /// <param name="dirY">카메라 방향 벡터 Y 성분.</param>
        /// <param name="planeX">투영 평면 벡터 X 성분.</param>
        /// <param name="planeY">투영 평면 벡터 Y 성분.</param>
        /// <param name="invDet">투영 행렬 역행렬의 행렬식 역수.</param>
        /// <returns>화면에 일부라도 투영될 가능성이 있으면 true, 확실히 보이지 않으면 false.</returns>
        private bool IsPotentiallyVisible(float dx, float dy, float scale, int renderWidth, int renderHeight,
            float dirX, float dirY, float planeX, float planeY, double invDet)
        {
            double transformX = invDet * ((dirY * dx) - (dirX * dy));
            double transformY = invDet * ((-planeY * dx) + (planeX * dy));
            if (transformY <= 0.05)
            {
                return false;
            }

            double spriteScreenX = (renderWidth * 0.5) * (1.0 + (transformX / transformY));
            double spriteHeight = Math.Abs((renderHeight / transformY) * scale);
            if (spriteHeight <= 4.0)
            {
                return false;
            }

            double spriteWidth = spriteHeight;
            double drawStartX = spriteScreenX - (spriteWidth * 0.5);
            double drawEndX = spriteScreenX + (spriteWidth * 0.5);
            return drawEndX >= -16.0 && drawStartX <= renderWidth + 16.0;
        }

        /// <summary>
        /// 오브젝트의 월드 상대 좌표를 카메라 좌표계의 깊이(transformY)로 변환한다.
        /// 반환값이 0보다 크면 카메라 앞에 있는 것이다.
        /// </summary>
        /// <param name="dx">오브젝트 - 플레이어의 월드 X 차이.</param>
        /// <param name="dy">오브젝트 - 플레이어의 월드 Y 차이.</param>
        /// <param name="planeX">투영 평면 벡터 X 성분.</param>
        /// <param name="planeY">투영 평면 벡터 Y 성분.</param>
        /// <param name="invDet">투영 행렬 역행렬의 행렬식 역수.</param>
        /// <returns>카메라 좌표계에서 오브젝트까지의 투영 깊이(양수 = 카메라 앞).</returns>
        private static float GetProjectedDepth(float dx, float dy, float planeX, float planeY, double invDet)
        {
            return (float)(invDet * ((-planeY * dx) + (planeX * dy)));
        }

        /// <summary>
        /// 오브젝트의 월드 상대 좌표를 화면 X 좌표로 변환한다.
        /// </summary>
        /// <param name="dx">오브젝트 - 플레이어의 월드 X 차이.</param>
        /// <param name="dy">오브젝트 - 플레이어의 월드 Y 차이.</param>
        /// <param name="targetWidth">렌더 대상 너비(픽셀).</param>
        /// <param name="dirX">카메라 방향 벡터 X 성분.</param>
        /// <param name="dirY">카메라 방향 벡터 Y 성분.</param>
        /// <param name="planeX">투영 평면 벡터 X 성분.</param>
        /// <param name="planeY">투영 평면 벡터 Y 성분.</param>
        /// <param name="invDet">투영 행렬 역행렬의 행렬식 역수.</param>
        /// <returns>화면 X 좌표(픽셀). 화면 중앙이 targetWidth * 0.5에 해당한다.</returns>
        private static float GetProjectedScreenX(float dx, float dy, int targetWidth, float dirX, float dirY, float planeX, float planeY, double invDet)
        {
            double transformX = invDet * ((dirY * dx) - (dirX * dy));
            double transformY = invDet * ((-planeY * dx) + (planeX * dy));
            return (float)((targetWidth * 0.5) * (1.0 + (transformX / transformY)));
        }

        /// <summary>
        /// 오브젝트의 투영 깊이와 스케일을 기반으로 화면에 그릴 높이(픽셀)를 계산한다.
        /// </summary>
        /// <param name="targetHeight">렌더 대상 높이(픽셀).</param>
        /// <param name="depth">오브젝트의 투영 깊이.</param>
        /// <param name="scale">오브젝트의 월드 공간 크기(스케일).</param>
        /// <returns>화면에 그릴 스프라이트 높이(픽셀). 항상 양수.</returns>
        private static float GetProjectedHeight(int targetHeight, float depth, float scale)
        {
            return Math.Abs((targetHeight / Math.Max(0.0001f, depth)) * scale);
        }

        /// <summary>
        /// 오브젝트의 투영 깊이와 스케일을 기반으로 화면 Y 중심 좌표를 계산한다.
        /// 현재는 화면 수직 중앙(targetHeight * 0.5)을 기준으로 한다.
        /// </summary>
        /// <param name="targetHeight">렌더 대상 높이(픽셀).</param>
        /// <param name="depth">오브젝트의 투영 깊이.</param>
        /// <param name="scale">오브젝트의 월드 공간 크기(스케일).</param>
        /// <returns>스프라이트의 화면 Y 중심 좌표(픽셀).</returns>
        private static float GetProjectedCenterY(int targetHeight, float depth, float scale)
        {
            float spriteHeight = GetProjectedHeight(targetHeight, depth, scale);
            return (targetHeight * 0.5f) + (spriteHeight * 0f);
        }

        /// <summary>
        /// 스프라이트의 화면 X 범위에서 gpuDepthBufferData를 검사하여
        /// 적어도 하나의 열에서 스프라이트가 벽 앞에 있는지(depth &lt; 벽 깊이) 판정한다.
        /// </summary>
        /// <param name="depth">스프라이트의 투영 깊이.</param>
        /// <param name="drawStartX">스프라이트 렌더 영역의 왼쪽 X 좌표.</param>
        /// <param name="drawEndX">스프라이트 렌더 영역의 오른쪽 X 좌표.</param>
        /// <returns>가시 열이 하나 이상이면 true, 완전히 차폐되면 false.</returns>
        private bool IsSpriteVisibleAgainstDepth(float depth, int drawStartX, int drawEndX)
        {
            // gpuDepthBufferData(±3 min-filter 적용본)를 사용한다.
            // CPU RenderEnemies의 zBuffer도 같은 버퍼에서 복사하므로,
            // GPU 제출 판정과 CPU enemyVisible 판정이 동일한 depth 기준으로 일치한다.
            if (gpuDepthBufferData == null || gpuDepthBufferData.Length == 0 || drawEndX < drawStartX)
            {
                return false;
            }

            int start = Math.Max(0, drawStartX);
            int end = Math.Min(gpuDepthBufferData.Length - 1, drawEndX);
            for (int x = start; x <= end; x++)
            {
                if (depth < gpuDepthBufferData[x])
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 픽업 오브젝트의 화면 Y 중심 좌표를 계산한다.
        /// 바닥 근처에 위치하며 PulseTimer에 따라 사인 곡선으로 부유(bob) 효과를 적용한다.
        /// </summary>
        /// <param name="targetHeight">렌더 대상 높이(픽셀).</param>
        /// <param name="depth">픽업의 투영 깊이.</param>
        /// <param name="pulseTimer">부유 애니메이션 타이머(초).</param>
        /// <param name="scale">픽업의 월드 공간 크기(스케일).</param>
        /// <returns>픽업 스프라이트의 화면 Y 중심 좌표(픽셀).</returns>
        private static float GetProjectedPickupCenterY(int targetHeight, float depth, float pulseTimer, float scale)
        {
            float bob = (float)Math.Sin(pulseTimer * 3.6f) * 0.08f;
            float spriteHeight = GetProjectedHeight(targetHeight, depth, scale);
            float groundY = targetHeight / 2f + ((targetHeight * 0.34f) / Math.Max(0.0001f, depth)) - (bob * targetHeight * 0.2f);
            return groundY - (spriteHeight * 0.5f);
        }

        /// <summary>
        /// 스프라이트 화면 X 중심 좌표를 픽셀 경계에 정렬한다.
        /// CPU RenderEnemies와의 좌표 일치를 위해 floor + 0.5 방식을 사용한다.
        /// </summary>
        /// <param name="value">정렬할 화면 X 좌표(픽셀, float).</param>
        /// <returns>픽셀 경계에 정렬된 X 좌표.</returns>
        private static float SnapSpriteCenterX(float value)
        {
            return (float)Math.Floor(value) + 0.5f;
        }

        /// <summary>
        /// 스프라이트 화면 Y 중심 좌표를 반올림하여 CPU RenderEnemies의 정수 나눗셈 결과와 일치시킨다.
        /// Math.Floor + 0.5 방식은 스케일다운 시 1픽셀 오프셋을 발생시키므로 Math.Round를 사용한다.
        /// </summary>
        /// <param name="value">정렬할 화면 Y 좌표(픽셀, float).</param>
        /// <returns>반올림된 Y 좌표(float).</returns>
        private static float SnapSpriteCenterY(float value)
        {
            // CPU RenderEnemies: drawStartY = -spriteHeight/2 + frameH/2 → 스프라이트 중심 = frameH/2 (정수 나눗셈)
            // Math.Round는 정수 반올림으로 CPU의 정수 나눗셈 결과와 일치한다.
            // 이전의 Math.Floor(value)+0.5f는 스케일다운 시 1픽셀 오프셋을 발생시켰다.
            return (float)Math.Round(value);
        }

        /// <summary>
        /// 스프라이트 투영 높이를 반올림하여 CPU RenderEnemies의 정수 결과와 일치시킨다.
        /// 최솟값은 1픽셀이다.
        /// </summary>
        /// <param name="value">반올림할 투영 높이(픽셀, float).</param>
        /// <returns>반올림된 투영 높이. 최솟값 1.</returns>
        private static float SnapSpriteScale(float value)
        {
            return Math.Max(1f, (float)Math.Round(value));
        }

        /// <summary>
        /// 렌더 해상도가 GPU 월드 최대 해상도를 초과하는 경우 비율을 유지하면서 스케일다운한다.
        /// 홀수 해상도는 짝수로 내림한다.
        /// </summary>
        /// <param name="renderWidth">입력 렌더 너비(픽셀).</param>
        /// <param name="renderHeight">입력 렌더 높이(픽셀).</param>
        /// <param name="targetWidth">GPU 월드 렌더 대상 너비(픽셀). 최솟값 2.</param>
        /// <param name="targetHeight">GPU 월드 렌더 대상 높이(픽셀). 최솟값 2.</param>
        private void GetGpuWorldTargetSize(int renderWidth, int renderHeight, out int targetWidth, out int targetHeight)
        {
            if (renderWidth <= 0 || renderHeight <= 0)
            {
                targetWidth = 2;
                targetHeight = 2;
                return;
            }

            double widthScale = renderWidth > GameConfig.GpuWorldMaxRenderWidth
                ? GameConfig.GpuWorldMaxRenderWidth / (double)renderWidth
                : 1.0;
            double heightScale = renderHeight > GameConfig.GpuWorldMaxRenderHeight
                ? GameConfig.GpuWorldMaxRenderHeight / (double)renderHeight
                : 1.0;
            double scale = Math.Min(1.0, Math.Min(widthScale, heightScale));

            targetWidth = Math.Max(2, (int)Math.Round(renderWidth * scale));
            targetHeight = Math.Max(2, (int)Math.Round(renderHeight * scale));
            if ((targetWidth & 1) != 0)
            {
                targetWidth--;
            }

            if ((targetHeight & 1) != 0)
            {
                targetHeight--;
            }

            if (targetWidth <= 0) targetWidth = 2;
            if (targetHeight <= 0) targetHeight = 2;
        }

        /// <summary>
        /// GPU 경로에서 계산된 gpuDepthBufferData(±3 min-filter 적용본)를
        /// CPU 오버레이용 depthBuffer에 복사한다.
        /// 해상도가 다를 때는 min-filter 리샘플링으로 축소한다.
        /// </summary>
        /// <param name="depthBuffer">CPU 오버레이가 참조할 깊이 버퍼(double[]). 이 함수에서 갱신된다.</param>
        /// <param name="renderWidth">CPU 프레임버퍼 너비(픽셀).</param>
        /// <param name="worldTargetWidth">GPU 월드 렌더 대상 너비(픽셀).</param>
        private void CopyDepthBufferForCpuOverlays(double[] depthBuffer, int renderWidth, int worldTargetWidth)
        {
            // gpuDepthBufferData는 BuildSmoothedDepthBuffer(Radius=3)로 ±3 min-filter된 버퍼다.
            // GPU 스프라이트 셰이더도 동일 버퍼를 참조하므로, CPU 오버레이(체력 바, 레이저 빔)가
            // 이 버퍼를 기준으로 차폐 판정하면 GPU/CPU 간 판정 불일치가 제거된다.
            if (depthBuffer == null || gpuDepthBufferData == null || renderWidth <= 0 || worldTargetWidth <= 0)
            {
                return;
            }

            int copyLength = Math.Min(depthBuffer.Length, renderWidth);

            if (renderWidth == worldTargetWidth)
            {
                // 해상도 일치: 1:1 직접 복사
                for (int x = 0; x < copyLength; x++)
                {
                    depthBuffer[x] = gpuDepthBufferData[x];
                }
                return;
            }

            // 해상도 불일치 시 min-filter 리샘플링.
            // gpuDepthBufferData는 이미 ±3 스무딩된 상태이므로 추가 min-filter는
            // 스케일다운 매핑 오차만 보정한다.
            float scale = (float)worldTargetWidth / renderWidth;
            for (int x = 0; x < copyLength; x++)
            {
                int srcStart = (int)(x * scale);
                int srcEnd = (int)((x + 1) * scale);
                if (srcStart < 0) srcStart = 0;
                if (srcEnd >= worldTargetWidth) srcEnd = worldTargetWidth - 1;

                double minDepth = gpuDepthBufferData[srcStart];
                for (int s = srcStart + 1; s <= srcEnd; s++)
                {
                    if (gpuDepthBufferData[s] < minDepth)
                    {
                        minDepth = gpuDepthBufferData[s];
                    }
                }
                depthBuffer[x] = minDepth;
            }
        }

        /// <summary>
        /// 벽까지의 수직 투영 거리를 렌더링 거리로 변환한다.
        /// NearPlane보다 가까운 거리에는 소프트 클리핑을 적용한다.
        /// </summary>
        /// <param name="wallDistance">레이캐스트로 계산한 벽까지의 수직 투영 거리.</param>
        /// <returns>소프트 클리핑이 적용된 렌더링 거리. 최솟값은 0.0001.</returns>
        private static double GetRenderDistance(double wallDistance)
        {
            if (wallDistance <= 0.0001)
            {
                return 0.0001;
            }

            if (wallDistance >= GameConfig.NearPlane)
            {
                return wallDistance;
            }

            double delta = GameConfig.NearPlane - wallDistance;
            double blend = Math.Sqrt(delta * delta + GameConfig.NearPlaneSoftness * GameConfig.NearPlaneSoftness);
            return GameConfig.NearPlane - 0.5 * (delta + GameConfig.NearPlaneSoftness - blend);
        }

        /// <summary>
        /// 스프라이트가 점유하는 화면 열 [drawStartX, drawEndX]를 gpuDepthBufferData로 스캔해
        /// 연속된 가시(visible) 구간 목록을 segments에 채운다.
        /// 열 X는 spriteDepth &lt; gpuDepthBufferData[X] 일 때 가시로 판정한다(open-space 포함).
        /// </summary>
        /// <param name="spriteDepth">스프라이트의 투영 깊이.</param>
        /// <param name="drawStartX">스프라이트 투영 시작 열.</param>
        /// <param name="drawEndX">스프라이트 투영 끝 열(포함).</param>
        /// <param name="segments">결과를 채울 리스트. 호출 전에 Clear된다.</param>
        private void ComputeVisibleSegments(float spriteDepth, int drawStartX, int drawEndX, List<(int start, int end)> segments)
        {
            segments.Clear();
            if (gpuDepthBufferData == null || gpuDepthBufferData.Length == 0)
            {
                return;
            }

            int bufLen = gpuDepthBufferData.Length;
            int xStart = Math.Max(0, drawStartX);
            int xEnd   = Math.Min(bufLen - 1, drawEndX);

            int runStart = -1;
            for (int x = xStart; x <= xEnd; x++)
            {
                bool visible = spriteDepth < gpuDepthBufferData[x];
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
        /// depthBufferData에 ±Radius(3) 칸 min-filter를 적용하여 gpuDepthBufferData에 저장한다.
        /// GPU 셰이더가 단일 룩업으로 이 버퍼를 참조하면, 벽 경계 근처에서도 스프라이트가 올바르게 차폐된다.
        /// 열린 공간(float.MaxValue) 열은 인접 벽의 깊이를 전파받지 않는다.
        /// </summary>
        private void BuildSmoothedDepthBuffer()
        {
            if (depthBufferData == null)
            {
                return;
            }

            int n = depthBufferData.Length;
            if (gpuDepthBufferData == null || gpuDepthBufferData.Length != n)
            {
                gpuDepthBufferData = new float[n];
            }

            const int Radius = 3;
            for (int x = 0; x < n; x++)
            {
                // open-space 열(벽 없음)은 인접 벽 depth를 전파받지 않는다.
                // min-filter를 적용하면 float.MaxValue가 인접 벽 depth로 대체되어
                // 셰이더가 해당 열을 벽으로 잘못 판정하고 스프라이트를 discard한다.
                if (depthBufferData[x] >= float.MaxValue)
                {
                    gpuDepthBufferData[x] = float.MaxValue;
                    continue;
                }

                float minD = depthBufferData[x];
                for (int k = 1; k <= Radius; k++)
                {
                    if (x - k >= 0 && depthBufferData[x - k] < minD)
                    {
                        minD = depthBufferData[x - k];
                    }

                    if (x + k < n && depthBufferData[x + k] < minD)
                    {
                        minD = depthBufferData[x + k];
                    }
                }

                gpuDepthBufferData[x] = minD;
            }
        }

        /// <summary>
        /// 벽 열 버퍼(depthBufferData, wallColumnGeometryBuffer, wallColumnMaterialBuffer,
        /// wallColumnDoorProgressBuffer)가 columnCount에 맞게 할당되었는지 확인하고 필요하면 재할당한다.
        /// </summary>
        /// <param name="columnCount">렌더 대상 열 수(= 렌더 너비).</param>
        private void EnsureWallColumnBuffers(int columnCount)
        {
            if (depthBufferData == null || depthBufferData.Length != columnCount)
            {
                depthBufferData = new float[columnCount];
            }

            int packedLength = columnCount * 8; // Row 0: 벽 지오메트리, Row 1: 계단 면(riser) 지오메트리
            if (wallColumnGeometryBuffer == null || wallColumnGeometryBuffer.Length != packedLength)
            {
                wallColumnGeometryBuffer = new float[packedLength];
            }

            if (wallColumnMaterialBuffer == null || wallColumnMaterialBuffer.Length != packedLength)
            {
                wallColumnMaterialBuffer = new int[packedLength];
            }

            if (wallColumnDoorProgressBuffer == null || wallColumnDoorProgressBuffer.Length != columnCount)
            {
                wallColumnDoorProgressBuffer = new float[columnCount];
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
        /// 새 프레임 시작 시 아틀라스 캐시의 PreviousUsed 배열을 초기화한다.
        /// ReleaseUnusedSpriteAtlasSlots에서 이 프레임에 사용되지 않은 슬롯을 해제할 때 기준이 된다.
        /// </summary>
        /// <param name="atlasCache">초기화할 아틀라스 캐시.</param>
        private void BeginSpriteAtlasFrame(SpriteAtlasCache atlasCache)
        {
            if (atlasCache.PreviousUsed != null && atlasCache.PreviousSpriteCount > 0)
            {
                Array.Clear(atlasCache.PreviousUsed, 0, atlasCache.PreviousSpriteCount);
            }
        }

        /// <summary>
        /// 이번 프레임에 사용되지 않은 아틀라스 슬롯의 소유자 참조를 해제하고 free slot으로 반환한다.
        /// </summary>
        /// <param name="atlasCache">슬롯을 해제할 아틀라스 캐시.</param>
        private void ReleaseUnusedSpriteAtlasSlots(SpriteAtlasCache atlasCache)
        {
            for (int i = 0; i < atlasCache.PreviousSpriteCount; i++)
            {
                if (atlasCache.PreviousOwners[i] != null && !atlasCache.PreviousUsed[i])
                {
                    atlasCache.Slots.Remove(atlasCache.PreviousOwners[i]);
                    atlasCache.PreviousOwners[i] = null;
                    atlasCache.PreviousSpriteSources[i] = null;
                    atlasCache.PreviousSpriteTintFlags[i] = false;
                    atlasCache.FreeSlots.Push(i);
                }
            }
        }

        /// <summary>
        /// 슬롯 배열의 뒤쪽에 남아 있는 빈 영역을 잘라내 다음 프레임 아틀라스 크기가 줄어들 수 있게 한다.
        /// </summary>
        private void TrimUnusedSpriteAtlasTail(SpriteAtlasCache atlasCache)
        {
            if (atlasCache.PreviousOwners == null || atlasCache.PreviousSpriteCount <= 0)
            {
                atlasCache.PreviousSpriteCount = 0;
                return;
            }

            int trimmedCount = atlasCache.PreviousSpriteCount;
            while (trimmedCount > 0 && atlasCache.PreviousOwners[trimmedCount - 1] == null)
            {
                trimmedCount--;
            }

            atlasCache.PreviousSpriteCount = trimmedCount;
        }

        /// <summary>
        /// owner 오브젝트에 대한 아틀라스 슬롯을 반환하거나 새로 할당한다.
        /// 같은 owner가 이미 슬롯을 가지고 있으면 기존 슬롯을 반환한다.
        /// 빈 슬롯이 있으면 재사용하고, 없으면 전역 카운터를 증가시켜 새 슬롯을 할당한다.
        /// </summary>
        /// <param name="atlasCache">슬롯을 관리하는 아틀라스 캐시.</param>
        /// <param name="owner">슬롯 소유자(적, 투사체, 픽업 등의 오브젝트 참조).</param>
        /// <returns>할당된 아틀라스 슬롯 인덱스.</returns>
        private int GetOrCreateSpriteAtlasSlot(SpriteAtlasCache atlasCache, object owner)
        {
            if (owner == null)
            {
                return 0;
            }

            if (atlasCache.Slots.TryGetValue(owner, out int existingSlot))
            {
                return existingSlot;
            }

            int slot;
            if (atlasCache.FreeSlots.Count > 0)
            {
                slot = atlasCache.FreeSlots.Pop();
            }
            else
            {
                slot = atlasCache.NextSlot++;
            }

            EnsureSpriteAtlasSlotCapacity(atlasCache, slot + 1);
            if (slot >= atlasCache.PreviousSpriteCount)
            {
                atlasCache.PreviousSpriteCount = slot + 1;
            }

            atlasCache.PreviousOwners[slot] = owner;
            atlasCache.Slots[owner] = slot;
            return slot;
        }

        /// <summary>
        /// 아틀라스 캐시의 슬롯 관련 배열이 requiredSlots 이상의 크기를 가지도록 확장한다.
        /// 부족하면 두 배씩 확장한다.
        /// </summary>
        /// <param name="atlasCache">확장할 아틀라스 캐시.</param>
        /// <param name="requiredSlots">필요한 최소 슬롯 수.</param>
        private void EnsureSpriteAtlasSlotCapacity(SpriteAtlasCache atlasCache, int requiredSlots)
        {
            if (atlasCache.PreviousOwners == null || atlasCache.PreviousOwners.Length < requiredSlots)
            {
                int newLength = atlasCache.PreviousOwners == null ? 16 : atlasCache.PreviousOwners.Length * 2;
                while (newLength < requiredSlots)
                {
                    newLength *= 2;
                }

                Array.Resize(ref atlasCache.PreviousOwners, newLength);
                Array.Resize(ref atlasCache.PreviousUsed, newLength);
                Array.Resize(ref atlasCache.PreviousSpriteSources, newLength);
                Array.Resize(ref atlasCache.PreviousSpriteTintFlags, newLength);
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
        /// 아틀라스 슬롯 할당과 픽셀 더티 상태를 추적하는 캐시.
        /// 매 프레임 사용된 슬롯과 변경된 픽셀만 GPU에 업로드하여 대역폭을 절감한다.
        /// </summary>
        private sealed class SpriteAtlasCache
        {
            /// <summary>아틀라스 전체 ARGB 픽셀 배열.</summary>
            public int[] Pixels;

            /// <summary>변경된 셀 영역을 기록하는 정수 배열(x, y, w, h 순으로 4개씩).</summary>
            public int[] DirtyRects;

            /// <summary>DirtyRects에 기록된 더티 사각형의 수.</summary>
            public int DirtyRectCount;

            /// <summary>아틀라스 전체 재업로드가 필요하면 true. 레이아웃이 변경된 경우 설정된다.</summary>
            public bool UploadFullAtlas;

            /// <summary>이번 프레임에서 사용된 슬롯의 최대 인덱스 + 1. 아틀라스 크기 계산에 사용된다.</summary>
            public int ActiveSlotSpan;

            /// <summary>각 슬롯의 소유자 오브젝트 참조. null이면 해당 슬롯이 비어 있음을 나타낸다.</summary>
            public object[] PreviousOwners;

            /// <summary>이번 프레임에 각 슬롯이 사용되었는지 여부. BeginSpriteAtlasFrame에서 초기화된다.</summary>
            public bool[] PreviousUsed;

            /// <summary>각 슬롯의 이전 프레임 스프라이트 픽셀 소스. 변경 감지에 사용된다.</summary>
            public Color[][] PreviousSpriteSources;

            /// <summary>각 슬롯의 이전 프레임 피격 색조 플래그. 변경 감지에 사용된다.</summary>
            public bool[] PreviousSpriteTintFlags;

            /// <summary>유효한 슬롯 수(슬롯 인덱스의 상한). 아틀라스 치수 계산에 사용된다.</summary>
            public int PreviousSpriteCount;

            /// <summary>마지막 아틀라스 빌드 시의 너비(픽셀). 레이아웃 변경 감지에 사용된다.</summary>
            public int PreviousAtlasWidth;

            /// <summary>마지막 아틀라스 빌드 시의 높이(픽셀). 레이아웃 변경 감지에 사용된다.</summary>
            public int PreviousAtlasHeight;

            /// <summary>소유자 오브젝트 참조를 슬롯 인덱스로 매핑한다.</summary>
            public readonly Dictionary<object, int> Slots;

            /// <summary>해제된 슬롯 인덱스를 보관하는 전역 free-list 스택이다.</summary>
            public readonly Stack<int> FreeSlots;

            /// <summary>새 슬롯 할당 시 사용할 다음 전역 슬롯 인덱스다.</summary>
            public int NextSlot;

            /// <summary>
            /// SpriteAtlasCache를 초기화한다.
            /// 슬롯 맵과 free-list를 초기화한다.
            /// </summary>
            public SpriteAtlasCache()
            {
                Slots = new Dictionary<object, int>(ReferenceComparer.Instance);
                FreeSlots = new Stack<int>();
            }
        }

        /// <summary>
        /// 참조 동일성(ReferenceEquals)만 비교하는 IEqualityComparer 구현.
        /// SpriteAtlasCache의 슬롯 맵 딕셔너리에서 오브젝트 참조를 키로 사용할 때 필요하다.
        /// </summary>
        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            /// <summary>싱글톤 인스턴스.</summary>
            public static readonly ReferenceComparer Instance = new();

            /// <summary>
            /// 두 오브젝트 참조가 동일한지(같은 메모리 주소) 확인한다.
            /// </summary>
            /// <param name="x">비교할 첫 번째 오브젝트.</param>
            /// <param name="y">비교할 두 번째 오브젝트.</param>
            /// <returns>두 참조가 동일하면 true.</returns>
            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            /// <summary>
            /// RuntimeHelpers.GetHashCode를 사용하여 참조 기반 해시를 반환한다.
            /// </summary>
            /// <param name="obj">해시 코드를 계산할 오브젝트.</param>
            /// <returns>참조 기반 해시 코드.</returns>
            public int GetHashCode(object obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }

        /// <summary>
        /// 아틀라스 캐시의 Pixels 배열이 atlasWidth × atlasHeight 이상의 크기를 가지도록 확장한다.
        /// 크기가 부족하면 새로 할당하고 UploadFullAtlas를 true로 설정한다.
        /// </summary>
        /// <param name="atlasCache">픽셀 배열을 확장할 아틀라스 캐시.</param>
        /// <param name="atlasWidth">필요한 아틀라스 너비(픽셀).</param>
        /// <param name="atlasHeight">필요한 아틀라스 높이(픽셀).</param>
        private void EnsureSpriteAtlasPixelCapacity(SpriteAtlasCache atlasCache, int atlasWidth, int atlasHeight)
        {
            int atlasPixelCount = atlasWidth * atlasHeight;
            if (atlasCache.Pixels == null || atlasCache.Pixels.Length < atlasPixelCount)
            {
                atlasCache.Pixels = new int[atlasPixelCount];
                atlasCache.PreviousAtlasWidth = atlasWidth;
                atlasCache.PreviousAtlasHeight = atlasHeight;
                atlasCache.UploadFullAtlas = true;
            }
        }

        /// <summary>
        /// 방 전환 시 아틀라스 캐시 전체를 비워 큰 버퍼를 해제한다.
        /// </summary>
        private static void ResetSpriteAtlasCache(SpriteAtlasCache atlasCache)
        {
            if (atlasCache == null)
            {
                return;
            }

            atlasCache.Pixels = null;
            atlasCache.DirtyRects = null;
            atlasCache.DirtyRectCount = 0;
            atlasCache.UploadFullAtlas = false;
            atlasCache.ActiveSlotSpan = 0;
            atlasCache.PreviousOwners = null;
            atlasCache.PreviousUsed = null;
            atlasCache.PreviousSpriteSources = null;
            atlasCache.PreviousSpriteTintFlags = null;
            atlasCache.PreviousSpriteCount = 0;
            atlasCache.PreviousAtlasWidth = 0;
            atlasCache.PreviousAtlasHeight = 0;
            atlasCache.Slots.Clear();
            atlasCache.FreeSlots.Clear();
            atlasCache.NextSlot = 0;
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
