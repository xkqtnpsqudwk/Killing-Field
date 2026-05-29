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
    /// GPU ?�프?�이???�영 ?�버�?출력??구조�?
    /// �??�프?�이?�의 ?�면 좌표, ?�기, 깊이, ?��??�스 ?�롯???�는??
    /// </summary>
    internal struct SpriteDebugProjection
    {
        /// <summary>?�프?�이?�의 ?�드 ?�브?�트 종류(?? ?�사�? ?�업 ??.</summary>
        public WorldSpriteKind Kind;

        /// <summary>?�프?�이??중심???�면 X 좌표(?��?).</summary>
        public float X;

        /// <summary>?�프?�이??중심???�면 Y 좌표(?��?).</summary>
        public float Y;

        /// <summary>?�프?�이?�의 ?�면 ?�비(?��?).</summary>
        public float Width;

        /// <summary>?�프?�이?�의 ?�면 ?�이(?��?).</summary>
        public float Height;

        /// <summary>?�프?�이?�의 ?�드 공간 깊이(?�영 거리).</summary>
        public float Depth;

        /// <summary>???�프?�이?��? ?�당???��??�스 ?�롯 ?�덱??</summary>
        public int AtlasSlot;
    }

    /// <summary>
    /// ?�프?�이???�스 빌드 결과�??�는 구조�?
    /// GPU ?�더?�에 ?�달???�프?�이???�스?�스 배열�??��??�스 ?�보�??�함?�다.
    /// </summary>
    internal struct SpritePassBuildResult
    {
        /// <summary>?�번 ?�스?�서 ?�더링할 ?�프?�이???�스?�스 배열.</summary>
        public WorldSpriteInstance[] Sprites;

        /// <summary>?�효???�프?�이???�스?�스 ??</summary>
        public int SpriteCount;

        /// <summary>?�프?�이???�스�??��??�스??ARGB ?��? 배열.</summary>
        public int[] AtlasPixels;

        /// <summary>?��??�스 ?��?지???�비(?��?).</summary>
        public int AtlasWidth;

        /// <summary>?��??�스 ?��?지???�이(?��?).</summary>
        public int AtlasHeight;

        /// <summary>?��??�스 ?�체�?GPU???�업로드?�야 ?�면 true. ?�이?�웃??변경된 경우 ?�정?�다.</summary>
        public bool UploadFullAtlas;

        /// <summary>변경된 ?��??�스 ?� ?�역???��??�는 ?�수 배열(x, y, width, height ?�으�?4개씩).</summary>
        public int[] DirtyRects;

        /// <summary>DirtyRects??기록???�티 ?�각?�의 ??</summary>
        public int DirtyRectCount;
    }

    /// <summary>
    /// ?�재 ?�드 ?�태�?GPU world presenter가 ?�해?????�는 RenderWorldCommand ?�이?�로 변?�한??
    /// �???column) 지?�메?�리, 깊이 버퍼, ?�·투?�체·?�업 ?�프?�이???��??�스, ?�이?� �??�스?�스�?
    /// �??�레??조립?�여 TryBuild�?반환?�다.
    /// </summary>
    internal sealed class WorldRenderDataBuilder
    {
        /// <summary>?�프?�이???�(?�스�? ?�기(?��?). GameConfig.TextureSize?� ?�일?�다.</summary>
        private const int SpriteCellSize = GameConfig.TextureSize;

        /// <summary>?��??�스 ?� 주�???추�??�는 ?�딩 ?��? ?? 경계 블리??방�???</summary>
        private const int SpriteAtlasPadding = 1;

        /// <summary>?��??�스 ??�??�???�제 ?�트?�이??SpriteCellSize + ?�딩 * 2).</summary>
        private const int SpriteAtlasCellStride = SpriteCellSize + (SpriteAtlasPadding * 2);

        /// <summary>�??�이??�?�??�태�??�공?�는 매니?�.</summary>
        private readonly MapManager mapManager;

        /// <summary>?�스�?�??�프?�이???��? ?�이?��? ?�공?�는 매니?�.</summary>
        private readonly TextureManager textureManager;

        /// <summary>??�??�사�?목록???�공?�는 매니?�.</summary>
        private readonly EnemyManager enemyManager;

        /// <summary>?�번 ?�레?�의 ???�프?�이??빌드 ??�� 목록. �??�레???�사?�된??</summary>
        private readonly List<SpriteBuildEntry> enemySpriteEntries = new(48);

        /// <summary>?�번 ?�레?�의 기�? ?�프?�이???�사체·픽?? 빌드 ??�� 목록. �??�레???�사?�된??</summary>
        private readonly List<SpriteBuildEntry> miscSpriteEntries = new(64);

        /// <summary>?�번 ?�레?�의 ?�이?� �?빌드 ??�� 목록. �??�레???�사?�된??</summary>
        private readonly List<BeamBuildEntry> beamEntries = new(16);

        /// <summary>ComputeVisibleSegments ?�사??버퍼. �??�출마다 Clear ???�용?�다.</summary>
        private readonly List<(int start, int end)> _segmentBuffer = new(8);

        /// <summary>GPU�??�송?�는 ?�별 ?�시 깊이 버퍼(float, renderWidth ?�기).</summary>
        private float[] depthBufferData;

        /// <summary>CPU ±3 min-filter가 ?�용??GPU ?�용 깊이 버퍼. ?�프?�이??차폐 ?�정???�용?�다.</summary>
        private float[] gpuDepthBufferData;

        /// <summary>
        /// �??�면 ?�의 �?지?�메?�리�??�는 버퍼.
        /// ?�당 4개의 float(perpWallDist, fullDrawStart, drawStart, drawEnd)?�로 구성?�다.
        /// </summary>
        private float[] wallColumnGeometryBuffer;

        /// <summary>
        /// �??�면 ?�의 �??�질???�는 버퍼.
        /// ?�당 4개의 int(textureId, texX, side, tileType)?�로 구성?�다.
        /// </summary>
        private int[] wallColumnMaterialBuffer;

        /// <summary>�??�면 ?�의 �?door) 개방 진행?��? ?�는 버퍼(float, renderWidth ?�기).</summary>
        private float[] wallColumnDoorProgressBuffer;

        /// <summary>???�프?�이???�스?�스 배열. BuildSpritePass?�서 ?�사?�된??</summary>
        private WorldSpriteInstance[] enemySpriteBuffer;

        /// <summary>기�? ?�프?�이???�스?�스 배열. BuildSpritePass?�서 ?�사?�된??</summary>
        private WorldSpriteInstance[] miscSpriteBuffer;

        /// <summary>?�이?� �??�스?�스 배열. BuildBeamBuffer?�서 ?�사?�된??</summary>
        private WorldBeamInstance[] beamBuffer;

        /// <summary>???�프?�이???��??�스 캐시. ?�롯 ?�당�??��? ?�티 ?��?�?추적?�다.</summary>
        private readonly SpriteAtlasCache enemySpriteAtlasCache = new();

        /// <summary>기�? ?�프?�이???��??�스 캐시. ?�롯 ?�당�??��? ?�티 ?��?�?추적?�다.</summary>
        private readonly SpriteAtlasCache miscSpriteAtlasCache = new();

        /// <summary>?�프?�이???�버�??�영 ?�보 배열. DebugSprites ?�로?�티�??�해 ?��????�출?�다.</summary>
        private SpriteDebugProjection[] spriteDebugBuffer;

        /// <summary>?�번 ?�레?�에 기록???�프?�이???�버�???�� ??</summary>
        private int spriteDebugCount;

        /// <summary>?�성 구체(AcidGlob) ?�사�??�프?�이???��? 배열. 최초 ?�청 ???�성?�다.</summary>
        private Color[] acidGlobSprite;

        /// <summary>?�성 ?�덩??AcidPool) ?�사�??�프?�이???��? 배열. 최초 ?�청 ???�성?�다.</summary>
        private Color[] acidPoolSprite;

        /// <summary>?�반 ???�환(EnemyShot) ?�프?�이???��? 배열. 최초 ?�청 ???�성?�다.</summary>
        private Color[] enemyShotSprite;

        /// <summary>보스 로켓(BossRocket) ?�사�??�프?�이???��? 배열. 최초 ?�청 ???�성?�다.</summary>
        private Color[] bossRocketSprite;

        /// <summary>?�레?�어 로켓 ??��(PlayerRocketExplosion) ?�프?�이???��? 배열. 최초 ?�청 ???�성?�다.</summary>
        private Color[] playerRocketExplosionSprite;

        /// <summary>
        /// WorldRenderDataBuilder�?초기?�한??
        /// </summary>
        /// <param name="mapManager">�??�이??�?�??�태�??�공?�는 매니?�.</param>
        /// <param name="textureManager">?�스�?�??�프?�이???��????�공?�는 매니?�.</param>
        /// <param name="enemyManager">??�??�사�?목록???�공?�는 매니?�.</param>
        public WorldRenderDataBuilder(MapManager mapManager, TextureManager textureManager, EnemyManager enemyManager)
        {
            this.mapManager = mapManager;
            this.textureManager = textureManager;
            this.enemyManager = enemyManager;
        }

        /// <summary>
        /// ?�재 ?�레?�어 카메?��? ?�드 ?�태�?기반?�로 GPU ?�더 커맨?��? 조립?�다.
        /// �???지?�메?�리 빌드 ??깊이 버퍼 ?�무????CPU ?�버?�이??깊이 복사 ??
        /// ?�프?�이???�이??빌드 ?�으�?처리????RenderWorldCommand�?반환?�다.
        /// </summary>
        /// <param name="player">카메???�치·방향·?�영 ?�면???�공?�는 ?�레?�어 ?�태.</param>
        /// <param name="depthBuffer">CPU ?�버?�이가 차폐 ?�정???�용?�는 깊이 버퍼(double[]). ???�수?�서 갱신?�다.</param>
        /// <param name="rewardPickups">?�드??배치??보상 ?�업 목록.</param>
        /// <param name="playerProjectiles">?�드??배치???�레?�어 ?�사�?목록.</param>
        /// <param name="renderWidth">?�더 ?�???�비(?��?).</param>
        /// <param name="renderHeight">?�더 ?�???�이(?��?).</param>
        /// <param name="command">빌드??GPU ?�더 커맨?? ?�패 ??기본�?</param>
        /// <returns>커맨?��? ?�공?�으�?빌드?�면 true, ?�수 ?�이?��? ?�거??맵이 비어 ?�으�?false.</returns>
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
                    DepthBuffer = gpuDepthBufferData,   // ±2 min-filter ?�용�?
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
                    DepthBuffer = gpuDepthBufferData,   // ±2 min-filter ?�용�?
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

        /// <summary>?�번 ?�레?�에 기록???�프?�이???�버�???�� ?��? 반환?�다.</summary>
        public int DebugSpriteCount => spriteDebugCount;

        /// <summary>?�번 ?�레?�의 ?�프?�이???�버�??�영 ?�보 배열??반환?�다. DebugSpriteCount만큼�??�효?�다.</summary>
        public SpriteDebugProjection[] DebugSprites => spriteDebugBuffer;

        /// <summary>
        /// �?�??�환 ???�드 ?�더 ?�시 캐시�?비운??
        /// ???��??�스/?�스?�스 버퍼가 ?�전 �?최�?치로 ?�는 것을 막기 ?�한 리셋?�다.
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
        /// DDA ?�이캐스?�으�?�??�면 ?�의 �?지?�메?�리, ?�질, �?개방 진행?? 깊이�?계산?�여
        /// wallColumnGeometryBuffer, wallColumnMaterialBuffer, wallColumnDoorProgressBuffer, depthBufferData??기록?�다.
        /// </summary>
        /// <param name="player">카메???�치·방향·?�영 ?�면???�공?�는 ?�레?�어 ?�태.</param>
        /// <param name="map">�??�???�??배열.</param>
        /// <param name="textureIds">�??�?�별 ?�스�?ID 배열.</param>
        /// <param name="mapWidth">�?가�??�기(?�??.</param>
        /// <param name="mapHeight">�??�로 ?�기(?�??.</param>
        /// <param name="renderWidth">?�더 ?�???�비(????.</param>
        /// <param name="renderHeight">?�더 ?�???�이(?��?).</param>
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

                // 계단 �?riser) 감�???변??
                float eyeZpre = player.FloorZ + 0.5f;
                float prevTileFloor = player.FloorZ;
                bool stepFaceFound = false;
                float stepFaceDist = 0f;
                float stepFaceTop = 0f;
                float stepFaceBottom = 0f;
                float stepFaceUpperFloor = 0f; // 계단 ???�?�의 바닥 ?�이 (?�평�??�더???�용)

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

                    // 바닥 ?�이 ?�승 구간 = 계단 �?riser) 감�?
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
                                    stepFaceUpperFloor = tileFloor; // ?�이?��? ?�평�?계산???�용
                                    stepFaceFound = true;
                                }
                            }
                        }
                        prevTileFloor = tileFloor;
                    }
                }

                int geometryIndex = x * 4;
                int materialIndex = x * 4;
                int stepIndex = renderWidth * 4 + x * 4; // Row 1: 계단 �??�이??
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

                // ?�이 기반 drawStart/drawEnd 계산 (Doom ?��????�터 ?�이)
                float eyeZ = player.FloorZ + 0.5f;
                float hitFloor = mapManager.GetFloorHeight(mapX, mapY);
                float hitCeil = mapManager.GetCeilHeight(mapX, mapY);
                int fullDrawStart = (int)(renderHeight * 0.5 - (hitCeil - eyeZ) * scale);
                int drawStart = fullDrawStart;
                int drawEnd = (int)(renderHeight * 0.5 + (eyeZ - hitFloor) * scale);

                // ?�위 ?�환: 기본 ?�이(floor=0, ceil=1)????기존�??�일??결과
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

                // Row 1: 계단 �?riser) ?�이??(stepDepth=0?�면 계단 ?�음)
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
        /// ?�레?�어 카메??기�??�로 ?? ?�사�? ?�업, ?�이?� 빔을 ?�면???�영?�고
        /// ?�프?�이???��??�스 �?�?버퍼�?조립?�다.
        /// </summary>
        /// <param name="player">카메???�치·방향·?�영 ?�면???�공?�는 ?�레?�어 ?�태.</param>
        /// <param name="rewardPickups">?�면???�영??보상 ?�업 목록.</param>
        /// <param name="playerProjectiles">?�면???�영???�레?�어 ?�사�?목록.</param>
        /// <param name="renderWidth">?�더 ?�???�비(?��?).</param>
        /// <param name="renderHeight">?�더 ?�???�이(?��?).</param>
        /// <param name="enemySpritePass">???�프?�이???�스 빌드 결과(?�스?�스 배열 + ?��??�스 ?�보).</param>
        /// <param name="miscSpritePass">기�? ?�프?�이???�사체·픽?? ?�스 빌드 결과.</param>
        /// <param name="beamCount">빌드???�이?� �??�스?�스 ??</param>
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
        /// ?�프?�이??빌드 ??�� 목록???��??�스???�렬·?�로?�하�?
        /// WorldSpriteInstance 배열??채워 SpritePassBuildResult�?반환?�다.
        /// </summary>
        /// <param name="entries">?�번 ?�스?�서 ?�더링할 ?�프?�이??빌드 ??�� 목록.</param>
        /// <param name="atlasCache">?�롯 ?�당�??��? ?�티 ?�태�?관리하???��??�스 캐시.</param>
        /// <param name="spriteBuffer">?�스?�스�?기록??버퍼. ?�기가 부족하�??�할?�된??</param>
        /// <param name="forceRefreshAllCells">true?�면 변�??��??� 관계없??모든 ?�???��??�스???�복?�한??</param>
        /// <returns>?�스?�스 배열, ?��??�스 ?��?, ?�티 ?�각???�보�??��? 빌드 결과.</returns>
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
        /// beamEntries�?깊이 ?�름차순?�로 ?�렬????beamBuffer??WorldBeamInstance�?기록?�다.
        /// </summary>
        /// <returns>빌드???�이?� �??�스?�스 ?? beamEntries가 비어 ?�으�?0.</returns>
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
        /// ?��??�스 캐시??DirtyRects 배열??변경된 ?� ?�역??추�??�다.
        /// 배열 ?�량??부족하�???배로 ?�장?�다.
        /// </summary>
        /// <param name="atlasCache">?�티 ?�각?�을 추�????��??�스 캐시.</param>
        /// <param name="x">?�티 ?�역???�쪽 X 좌표(?��??�스 ?��? 기�?).</param>
        /// <param name="y">?�티 ?�역???�쪽 Y 좌표(?��??�스 ?��? 기�?).</param>
        /// <param name="width">?�티 ?�역???�비(?��?).</param>
        /// <param name="height">?�티 ?�역???�이(?��?).</param>
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
        /// ?�프?�이???�에 ?�라 ?��??�스???�·행 ?��? 결정?�고 ?��? ?�기�?반환?�다.
        /// 가?�한 ???�사각형??가까운 ?�이?�웃???�용?�다.
        /// </summary>
        /// <param name="spriteCount">?��??�스??배치???�프?�이????</param>
        /// <param name="width">계산???��??�스 ?�비(?��?). spriteCount가 0 ?�하?�면 0.</param>
        /// <param name="height">계산???��??�스 ?�이(?��?). spriteCount가 0 ?�하?�면 0.</param>
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
        /// ?�의 ?�재 ?�니메이???�레???��? 배열??반환?�다.
        /// ?�니메이???�레?�이 ?�으�??�적 ?�프?�이?��? ?�용?�고, 그것???�으�?null??반환?�다.
        /// </summary>
        /// <param name="enemy">?��???가?�올 ???�스?�스.</param>
        /// <returns>?�재 ?�레?�의 ?��? 배열(Color[]). ?�효???�프?�이?��? ?�으�?null.</returns>
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
        /// ?�사�?종류???�라 미리 ?�성???�형 ?�프?�이???��? 배열??반환?�다.
        /// LaserBeam?� 별도 경로?�서 처리?��?�????�수?�서??null??반환?�다.
        /// </summary>
        /// <param name="projectile">?��???가?�올 ?�사�??�스?�스.</param>
        /// <returns>?�사�??�프?�이???��? 배열. 지?�하지 ?�는 종류?�면 null.</returns>
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
        /// ?�직 ?�성?��? ?�았?�면 BuildCircularSprite�?초기?�한??
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
        /// ?�이?� �??�사체�? 코어 ?�이?��? 글로우 ?�이????개의 BeamBuildEntry�?분해?�여 beamEntries??추�??�다.
        /// �?길이가 ?�무 짧으�?추�??��? ?�는??
        /// </summary>
        /// <param name="projectile">?�이?� �??�사�??�스?�스.</param>
        /// <param name="playerX">?�레?�어 ?�드 X 좌표.</param>
        /// <param name="playerY">?�레?�어 ?�드 Y 좌표.</param>
        /// <param name="dirX">카메??방향 벡터 X ?�분.</param>
        /// <param name="dirY">카메??방향 벡터 Y ?�분.</param>
        /// <param name="planeX">?�영 ?�면 벡터 X ?�분.</param>
        /// <param name="planeY">?�영 ?�면 벡터 Y ?�분.</param>
        /// <param name="invDet">?�영 ?�렬 ??��?�의 ?�렬????��.</param>
        /// <param name="renderWidth">?�더 ?�???�비(?��?).</param>
        /// <param name="renderHeight">?�더 ?�???�이(?��?).</param>
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
        /// ?�이?� 빔의 ?�드 좌표 구간???�면 좌표�??�영?�고, ?�나??WorldBeamInstance�?beamEntries??추�??�다.
        /// ?�면 밖으�??�전??벗어??경우 추�??��? ?�는??
        /// </summary>
        /// <param name="startWorldX">�??�작???�드 X 좌표.</param>
        /// <param name="startWorldY">�??�작???�드 Y 좌표.</param>
        /// <param name="endWorldX">�??�점 ?�드 X 좌표.</param>
        /// <param name="endWorldY">�??�점 ?�드 Y 좌표.</param>
        /// <param name="projectile">?�유 ?�사�??�께·?�업 ?�태 참조??.</param>
        /// <param name="playerX">?�레?�어 ?�드 X 좌표.</param>
        /// <param name="playerY">?�레?�어 ?�드 Y 좌표.</param>
        /// <param name="dirX">카메??방향 벡터 X ?�분.</param>
        /// <param name="dirY">카메??방향 벡터 Y ?�분.</param>
        /// <param name="planeX">?�영 ?�면 벡터 X ?�분.</param>
        /// <param name="planeY">?�영 ?�면 벡터 Y ?�분.</param>
        /// <param name="invDet">?�영 ?�렬 ??��?�의 ?�렬????��.</param>
        /// <param name="renderWidth">?�더 ?�???�비(?��?).</param>
        /// <param name="renderHeight">?�더 ?�???�이(?��?).</param>
        /// <param name="thicknessMultiplier">기본 ?�께??곱할 배율. 글로우 ?�이?�는 1보다 ?�게 ?�정?�다.</param>
        /// <param name="depthBias">깊이 ?�정 바이?�스. ?�을?�록 빔이 ?�으�??�온??</param>
        /// <param name="tintOverride">�??�조 ?�정?? null?�면 ?�색???�용?�다.</param>
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
        /// ?�차?�으�??�형 ?�프?�이???��? 배열???�성?�다.
        /// 중심부??가?�자리까지 innerColor?�서 outerColor�??�형 보간?�며, 바깥쪽�? ?�명?�다.
        /// </summary>
        /// <param name="outerColor">?�프?�이???�곽 ?�상.</param>
        /// <param name="innerColor">?�프?�이??중심 ?�상.</param>
        /// <param name="outerRadius">?�곽 반�?�??�규?? 0~1 범위). ??�?바깥?� ?�명?�다.</param>
        /// <param name="innerRadius">?��? ?�색 반�?�??�규??. ??�??�쪽?� innerColor�?채워진다.</param>
        /// <returns>GameConfig.TextureSize × TextureSize ?�기??Color[] ?��? 배열.</returns>
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
        /// ??Color 값을 ?�형 보간?�다. t=0?�면 a, t=1?�면 b�?반환?�다.
        /// </summary>
        /// <param name="a">?�작 ?�상.</param>
        /// <param name="b">???�상.</param>
        /// <param name="t">보간 비율(0~1). 범위�?벗어?�면 ?�램?�된??</param>
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
        /// ?�프?�이???��? 배열???��??�스 버퍼??지???�??복사?�다.
        /// ?�딩 ?��??�는 가?�자�??�을 복제?�여 ?��?/?�전 ???�웃 ?�???�이지 ?�도�??�다.
        /// tintHitFlash가 true?�면 불투�??��???R 채널??255�??�려 ?�격 ?�과�??�현?�다.
        /// </summary>
        /// <param name="atlasPixels">?�???��??�스 ARGB ?��? 배열.</param>
        /// <param name="spritePixels">복사???�스 ?�프?�이??Color[] ?��? 배열.</param>
        /// <param name="atlasCellX">?��??�스 ???�???�쪽 X 좌표(?�딩 ?�함).</param>
        /// <param name="atlasCellY">?��??�스 ???�???�쪽 Y 좌표(?�딩 ?�함).</param>
        /// <param name="atlasWidth">?��??�스 ?��?지???�비(?��?).</param>
        /// <param name="tintHitFlash">true?�면 불투�??��????�격 빨강 ?�조�??�용?�다.</param>
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

            // ?� 바깥 1?��???가?�자�??�을 복제???�면 ?��?/?�전 중에??
            // ?�웃 atlas ?�롯 ?�이 ?�이지 ?�아 ?�프?�이??깨짐??줄어?�다.
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
        /// ?�브?�트가 ?�면???�영??가?�성???�는지 빠르�??�별?�다.
        /// 카메???�에 ?�거???�무 ?�거???�면 밖으�??�전??벗어??경우 false�?반환?�다.
        /// </summary>
        /// <param name="dx">?�브?�트 - ?�레?�어???�드 X 차이.</param>
        /// <param name="dy">?�브?�트 - ?�레?�어???�드 Y 차이.</param>
        /// <param name="scale">?�브?�트???�드 공간 ?�기(?��???.</param>
        /// <param name="renderWidth">?�더 ?�???�비(?��?).</param>
        /// <param name="renderHeight">?�더 ?�???�이(?��?).</param>
        /// <param name="dirX">카메??방향 벡터 X ?�분.</param>
        /// <param name="dirY">카메??방향 벡터 Y ?�분.</param>
        /// <param name="planeX">?�영 ?�면 벡터 X ?�분.</param>
        /// <param name="planeY">?�영 ?�면 벡터 Y ?�분.</param>
        /// <param name="invDet">?�영 ?�렬 ??��?�의 ?�렬????��.</param>
        /// <returns>?�면???��??�도 ?�영??가?�성???�으�?true, ?�실??보이지 ?�으�?false.</returns>
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
        /// ?�브?�트???�드 ?��? 좌표�?카메??좌표계의 깊이(transformY)�?변?�한??
        /// 반환값이 0보다 ?�면 카메???�에 ?�는 것이??
        /// </summary>
        /// <param name="dx">?�브?�트 - ?�레?�어???�드 X 차이.</param>
        /// <param name="dy">?�브?�트 - ?�레?�어???�드 Y 차이.</param>
        /// <param name="planeX">?�영 ?�면 벡터 X ?�분.</param>
        /// <param name="planeY">?�영 ?�면 벡터 Y ?�분.</param>
        /// <param name="invDet">?�영 ?�렬 ??��?�의 ?�렬????��.</param>
        /// <returns>카메??좌표계에???�브?�트까�????�영 깊이(?�수 = 카메????.</returns>
        private static float GetProjectedDepth(float dx, float dy, float planeX, float planeY, double invDet)
        {
            return (float)(invDet * ((-planeY * dx) + (planeX * dy)));
        }

        /// <summary>
        /// ?�브?�트???�드 ?��? 좌표�??�면 X 좌표�?변?�한??
        /// </summary>
        /// <param name="dx">?�브?�트 - ?�레?�어???�드 X 차이.</param>
        /// <param name="dy">?�브?�트 - ?�레?�어???�드 Y 차이.</param>
        /// <param name="targetWidth">?�더 ?�???�비(?��?).</param>
        /// <param name="dirX">카메??방향 벡터 X ?�분.</param>
        /// <param name="dirY">카메??방향 벡터 Y ?�분.</param>
        /// <param name="planeX">?�영 ?�면 벡터 X ?�분.</param>
        /// <param name="planeY">?�영 ?�면 벡터 Y ?�분.</param>
        /// <param name="invDet">?�영 ?�렬 ??��?�의 ?�렬????��.</param>
        /// <returns>?�면 X 좌표(?��?). ?�면 중앙??targetWidth * 0.5???�당?�다.</returns>
        private static float GetProjectedScreenX(float dx, float dy, int targetWidth, float dirX, float dirY, float planeX, float planeY, double invDet)
        {
            double transformX = invDet * ((dirY * dx) - (dirX * dy));
            double transformY = invDet * ((-planeY * dx) + (planeX * dy));
            return (float)((targetWidth * 0.5) * (1.0 + (transformX / transformY)));
        }

        /// <summary>
        /// ?�브?�트???�영 깊이?� ?��??�을 기반?�로 ?�면??그릴 ?�이(?��?)�?계산?�다.
        /// </summary>
        /// <param name="targetHeight">?�더 ?�???�이(?��?).</param>
        /// <param name="depth">?�브?�트???�영 깊이.</param>
        /// <param name="scale">?�브?�트???�드 공간 ?�기(?��???.</param>
        /// <returns>?�면??그릴 ?�프?�이???�이(?��?). ??�� ?�수.</returns>
        private static float GetProjectedHeight(int targetHeight, float depth, float scale)
        {
            return Math.Abs((targetHeight / Math.Max(0.0001f, depth)) * scale);
        }

        /// <summary>
        /// ?�브?�트???�영 깊이?� ?��??�을 기반?�로 ?�면 Y 중심 좌표�?계산?�다.
        /// ?�재???�면 ?�직 중앙(targetHeight * 0.5)??기�??�로 ?�다.
        /// </summary>
        /// <param name="targetHeight">?�더 ?�???�이(?��?).</param>
        /// <param name="depth">?�브?�트???�영 깊이.</param>
        /// <param name="scale">?�브?�트???�드 공간 ?�기(?��???.</param>
        /// <returns>?�프?�이?�의 ?�면 Y 중심 좌표(?��?).</returns>
        private static float GetProjectedCenterY(int targetHeight, float depth, float scale)
        {
            float spriteHeight = GetProjectedHeight(targetHeight, depth, scale);
            return (targetHeight * 0.5f) + (spriteHeight * 0f);
        }

        /// <summary>
        /// ?�프?�이?�의 ?�면 X 범위?�서 gpuDepthBufferData�?검?�하??
        /// ?�어???�나???�에???�프?�이?��? �??�에 ?�는지(depth &lt; �?깊이) ?�정?�다.
        /// </summary>
        /// <param name="depth">?�프?�이?�의 ?�영 깊이.</param>
        /// <param name="drawStartX">?�프?�이???�더 ?�역???�쪽 X 좌표.</param>
        /// <param name="drawEndX">?�프?�이???�더 ?�역???�른�?X 좌표.</param>
        /// <returns>가???�이 ?�나 ?�상?�면 true, ?�전??차폐?�면 false.</returns>
        private bool IsSpriteVisibleAgainstDepth(float depth, int drawStartX, int drawEndX)
        {
            // gpuDepthBufferData(±3 min-filter ?�용�?�??�용?�다.
            // CPU RenderEnemies??zBuffer??같�? 버퍼?�서 복사?��?�?
            // GPU ?�출 ?�정�?CPU enemyVisible ?�정???�일??depth 기�??�로 ?�치?�다.
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
        /// ?�업 ?�브?�트???�면 Y 중심 좌표�?계산?�다.
        /// 바닥 근처???�치?�며 PulseTimer???�라 ?�인 곡선?�로 부??bob) ?�과�??�용?�다.
        /// </summary>
        /// <param name="targetHeight">?�더 ?�???�이(?��?).</param>
        /// <param name="depth">?�업???�영 깊이.</param>
        /// <param name="pulseTimer">부???�니메이???�?�머(�?.</param>
        /// <param name="scale">?�업???�드 공간 ?�기(?��???.</param>
        /// <returns>?�업 ?�프?�이?�의 ?�면 Y 중심 좌표(?��?).</returns>
        private static float GetProjectedPickupCenterY(int targetHeight, float depth, float pulseTimer, float scale)
        {
            float bob = (float)Math.Sin(pulseTimer * 3.6f) * 0.08f;
            float spriteHeight = GetProjectedHeight(targetHeight, depth, scale);
            float groundY = targetHeight / 2f + ((targetHeight * 0.34f) / Math.Max(0.0001f, depth)) - (bob * targetHeight * 0.2f);
            return groundY - (spriteHeight * 0.5f);
        }

        /// <summary>
        /// ?�프?�이???�면 X 중심 좌표�??��? 경계???�렬?�다.
        /// CPU RenderEnemies?�??좌표 ?�치�??�해 floor + 0.5 방식???�용?�다.
        /// </summary>
        /// <param name="value">?�렬???�면 X 좌표(?��?, float).</param>
        /// <returns>?��? 경계???�렬??X 좌표.</returns>
        private static float SnapSpriteCenterX(float value)
        {
            return (float)Math.Floor(value) + 0.5f;
        }

        /// <summary>
        /// ?�프?�이???�면 Y 중심 좌표�?반올림하??CPU RenderEnemies???�수 ?�눗??결과?� ?�치?�킨??
        /// Math.Floor + 0.5 방식?� ?��??�다????1?��? ?�프?�을 발생?�키므�?Math.Round�??�용?�다.
        /// </summary>
        /// <param name="value">?�렬???�면 Y 좌표(?��?, float).</param>
        /// <returns>반올림된 Y 좌표(float).</returns>
        private static float SnapSpriteCenterY(float value)
        {
            // CPU RenderEnemies: drawStartY = -spriteHeight/2 + frameH/2 ???�프?�이??중심 = frameH/2 (?�수 ?�눗??
            // Math.Round???�수 반올림으�?CPU???�수 ?�눗??결과?� ?�치?�다.
            // ?�전??Math.Floor(value)+0.5f???��??�다????1?��? ?�프?�을 발생?�켰??
            return (float)Math.Round(value);
        }

        /// <summary>
        /// ?�프?�이???�영 ?�이�?반올림하??CPU RenderEnemies???�수 결과?� ?�치?�킨??
        /// 최솟값�? 1?��??�다.
        /// </summary>
        /// <param name="value">반올림할 ?�영 ?�이(?��?, float).</param>
        /// <returns>반올림된 ?�영 ?�이. 최솟�?1.</returns>
        private static float SnapSpriteScale(float value)
        {
            return Math.Max(1f, (float)Math.Round(value));
        }

        /// <summary>
        /// ?�더 ?�상?��? GPU ?�드 최�? ?�상?��? 초과?�는 경우 비율???��??�면???��??�다?�한??
        /// ?�???�상?�는 짝수�??�림?�다.
        /// </summary>
        /// <param name="renderWidth">?�력 ?�더 ?�비(?��?).</param>
        /// <param name="renderHeight">?�력 ?�더 ?�이(?��?).</param>
        /// <param name="targetWidth">GPU ?�드 ?�더 ?�???�비(?��?). 최솟�?2.</param>
        /// <param name="targetHeight">GPU ?�드 ?�더 ?�???�이(?��?). 최솟�?2.</param>
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
        /// GPU 경로?�서 계산??gpuDepthBufferData(±3 min-filter ?�용�?�?
        /// CPU ?�버?�이??depthBuffer??복사?�다.
        /// ?�상?��? ?��? ?�는 min-filter 리샘?�링?�로 축소?�다.
        /// </summary>
        /// <param name="depthBuffer">CPU ?�버?�이가 참조??깊이 버퍼(double[]). ???�수?�서 갱신?�다.</param>
        /// <param name="renderWidth">CPU ?�레?�버???�비(?��?).</param>
        /// <param name="worldTargetWidth">GPU ?�드 ?�더 ?�???�비(?��?).</param>
        private void CopyDepthBufferForCpuOverlays(double[] depthBuffer, int renderWidth, int worldTargetWidth)
        {
            // gpuDepthBufferData??BuildSmoothedDepthBuffer(Radius=3)�?±3 min-filter??버퍼??
            // GPU ?�프?�이???�이?�도 ?�일 버퍼�?참조?��?�? CPU ?�버?�이(체력 �? ?�이?� �?가
            // ??버퍼�?기�??�로 차폐 ?�정?�면 GPU/CPU �??�정 불일치�? ?�거?�다.
            if (depthBuffer == null || gpuDepthBufferData == null || renderWidth <= 0 || worldTargetWidth <= 0)
            {
                return;
            }

            int copyLength = Math.Min(depthBuffer.Length, renderWidth);

            if (renderWidth == worldTargetWidth)
            {
                // ?�상???�치: 1:1 직접 복사
                for (int x = 0; x < copyLength; x++)
                {
                    depthBuffer[x] = gpuDepthBufferData[x];
                }
                return;
            }

            // ?�상??불일�???min-filter 리샘?�링.
            // gpuDepthBufferData???��? ±3 ?�무?�된 ?�태?��?�?추�? min-filter??
            // ?��??�다??매핑 ?�차�?보정?�다.
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
        /// 벽까지???�직 ?�영 거리�??�더�?거리�?변?�한??
        /// NearPlane보다 가까운 거리?�는 ?�프???�리?�을 ?�용?�다.
        /// </summary>
        /// <param name="wallDistance">?�이캐스?�로 계산??벽까지???�직 ?�영 거리.</param>
        /// <returns>?�프???�리?�이 ?�용???�더�?거리. 최솟값�? 0.0001.</returns>
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
        /// ?�프?�이?��? ?�유?�는 ?�면 ??[drawStartX, drawEndX]�?gpuDepthBufferData�??�캔??
        /// ?�속??가??visible) 구간 목록??segments??채운??
        /// ??X??spriteDepth &lt; gpuDepthBufferData[X] ????가?�로 ?�정?�다(open-space ?�함).
        /// </summary>
        /// <param name="spriteDepth">?�프?�이?�의 ?�영 깊이.</param>
        /// <param name="drawStartX">?�프?�이???�영 ?�작 ??</param>
        /// <param name="drawEndX">?�프?�이???�영 ?????�함).</param>
        /// <param name="segments">결과�?채울 리스?? ?�출 ?�에 Clear?�다.</param>
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
        /// depthBufferData??±Radius(3) �?min-filter�??�용?�여 gpuDepthBufferData???�?�한??
        /// GPU ?�이?��? ?�일 룩업?�로 ??버퍼�?참조?�면, �?경계 근처?�서???�프?�이?��? ?�바르게 차폐?�다.
        /// ?�린 공간(float.MaxValue) ?��? ?�접 벽의 깊이�??�파받�? ?�는??
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
                // open-space ??�??�음)?� ?�접 �?depth�??�파받�? ?�는??
                // min-filter�??�용?�면 float.MaxValue가 ?�접 �?depth�??�체되??
                // ?�이?��? ?�당 ?�을 벽으�??�못 ?�정?�고 ?�프?�이?��? discard?�다.
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
        /// �???버퍼(depthBufferData, wallColumnGeometryBuffer, wallColumnMaterialBuffer,
        /// wallColumnDoorProgressBuffer)가 columnCount??맞게 ?�당?�었?��? ?�인?�고 ?�요?�면 ?�할?�한??
        /// </summary>
        /// <param name="columnCount">?�더 ?�??????= ?�더 ?�비).</param>
        private void EnsureWallColumnBuffers(int columnCount)
        {
            if (depthBufferData == null || depthBufferData.Length != columnCount)
            {
                depthBufferData = new float[columnCount];
            }

            int packedLength = columnCount * 8; // Row 0: �?지?�메?�리, Row 1: 계단 �?riser) 지?�메?�리
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
        /// ?�프?�이???�스?�스 버퍼가 spriteCount ?�상???�기�?가지�??�는지 ?�인?�고 ?�요?�면 ?�할?�한??
        /// </summary>
        /// <param name="spriteBuffer">?�인??버퍼 참조. ?�기가 부족하�??�로 ?�당?�다.</param>
        /// <param name="spriteCount">?�요??최소 ?�스?�스 ??</param>
        private void EnsureSpriteBuffers(ref WorldSpriteInstance[] spriteBuffer, int spriteCount)
        {
            if (spriteBuffer == null || spriteBuffer.Length < spriteCount)
            {
                spriteBuffer = new WorldSpriteInstance[spriteCount];
            }
        }

        /// <summary>
        /// ?�프?�이???�버�??�영 버퍼가 spriteCount ?�상???�기�?가지�??�는지 ?�인?�고 ?�요?�면 ?�할?�한??
        /// </summary>
        /// <param name="spriteCount">?�요??최소 ??�� ??</param>
        private void EnsureDebugSpriteBuffer(int spriteCount)
        {
            if (spriteDebugBuffer == null || spriteDebugBuffer.Length < spriteCount)
            {
                spriteDebugBuffer = new SpriteDebugProjection[spriteCount];
            }
        }

        /// <summary>
        /// ???�레???�작 ???��??�스 캐시??PreviousUsed 배열??초기?�한??
        /// ReleaseUnusedSpriteAtlasSlots?�서 ???�레?�에 ?�용?��? ?��? ?�롯???�제????기�????�다.
        /// </summary>
        /// <param name="atlasCache">초기?�할 ?��??�스 캐시.</param>
        private void BeginSpriteAtlasFrame(SpriteAtlasCache atlasCache)
        {
            if (atlasCache.PreviousUsed != null && atlasCache.PreviousSpriteCount > 0)
            {
                Array.Clear(atlasCache.PreviousUsed, 0, atlasCache.PreviousSpriteCount);
            }
        }

        /// <summary>
        /// ?�번 ?�레?�에 ?�용?��? ?��? ?��??�스 ?�롯???�유??참조�??�제?�고 free slot?�로 반환?�다.
        /// </summary>
        /// <param name="atlasCache">?�롯???�제???��??�스 캐시.</param>
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
        /// ?�롯 배열???�쪽???�아 ?�는 �??�역???�라???�음 ?�레???��??�스 ?�기가 줄어?????�게 ?�다.
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
        /// owner ?�브?�트???�???��??�스 ?�롯??반환?�거???�로 ?�당?�다.
        /// 같�? owner가 ?��? ?�롯??가지�??�으�?기존 ?�롯??반환?�다.
        /// �??�롯???�으�??�사?�하�? ?�으�??�역 카운?��? 증�??�켜 ???�롯???�당?�다.
        /// </summary>
        /// <param name="atlasCache">?�롯??관리하???��??�스 캐시.</param>
        /// <param name="owner">?�롯 ?�유???? ?�사�? ?�업 ?�의 ?�브?�트 참조).</param>
        /// <returns>?�당???��??�스 ?�롯 ?�덱??</returns>
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
        /// ?��??�스 캐시???�롯 관??배열??requiredSlots ?�상???�기�?가지?�록 ?�장?�다.
        /// 부족하�???배씩 ?�장?�다.
        /// </summary>
        /// <param name="atlasCache">?�장???��??�스 캐시.</param>
        /// <param name="requiredSlots">?�요??최소 ?�롯 ??</param>
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
        /// ?�나???�프?�이?��? ?��??�스??배치?�고 GPU???�출?�기 ?�한 중간 빌드 ??��.
        /// ?�렬 ?? ?�유??참조, ?��??�스 ?�롯, ?��? ?�스, ?�격 ?�조 ?�래그�? ?�함?�다.
        /// </summary>
        private struct SpriteBuildEntry
        {
            /// <summary>?�근 ?�렬 ?? ?�수 깊이�??�용?�여 가까운 ?�프?�이?��? ?�에 ?�치?�다.</summary>
            public double SortKey;

            /// <summary>?�롯 ?�당 �??�제???�용?�는 ?�유???�브?�트 참조(?? ?�사�? ?�업 ??.</summary>
            public object Owner;

            /// <summary>????��???�당???��??�스 ?�롯 ?�덱??</summary>
            public int AtlasSlot;

            /// <summary>?��??�스??복사???�프?�이???��? 배열(Color[]).</summary>
            public Color[] SpritePixels;

            /// <summary>true?�면 ?��??�스 복사 ???�격 빨강 ?�조�??�용?�다.</summary>
            public bool TintHitFlash;

            /// <summary>
            /// ?�스�?가�??�롭 ?�작 비율 (0..1). UVCropRight가 0?�면 ?�롭 ?�음(?�체).
            /// BuildSpritePass?�서 U0???�용?�다.
            /// </summary>
            public float UVCropLeft;

            /// <summary>
            /// ?�스�?가�??�롭 ??비율 (0..1). 0?�면 ?�롭 ?�음(?�체 = 1�?처리).
            /// BuildSpritePass?�서 U1???�용?�다.
            /// </summary>
            public float UVCropRight;

            /// <summary>GPU???�출??WorldSpriteInstance ?�이??</summary>
            public WorldSpriteInstance Instance;
        }

        /// <summary>
        /// ?�나???�이?� �??�그먼트�?GPU???�출?�기 ?�한 중간 빌드 ??��.
        /// ?�렬 ?��? WorldBeamInstance�??�함?�다.
        /// </summary>
        private struct BeamBuildEntry
        {
            /// <summary>깊이 ?�렬 ?? ?�수 깊이�??�용?�여 가까운 빔이 ?�에 ?�치?�다.</summary>
            public double SortKey;

            /// <summary>GPU???�출??WorldBeamInstance ?�이??</summary>
            public WorldBeamInstance Instance;
        }

        /// <summary>
        /// ?��??�스 ?�롯 ?�당�??��? ?�티 ?�태�?추적?�는 캐시.
        /// �??�레???�용???�롯�?변경된 ?��?�?GPU???�로?�하???�??��???�감?�다.
        /// </summary>
        private sealed class SpriteAtlasCache
        {
            /// <summary>?��??�스 ?�체 ARGB ?��? 배열.</summary>
            public int[] Pixels;

            /// <summary>변경된 ?� ?�역??기록?�는 ?�수 배열(x, y, w, h ?�으�?4개씩).</summary>
            public int[] DirtyRects;

            /// <summary>DirtyRects??기록???�티 ?�각?�의 ??</summary>
            public int DirtyRectCount;

            /// <summary>?��??�스 ?�체 ?�업로드가 ?�요?�면 true. ?�이?�웃??변경된 경우 ?�정?�다.</summary>
            public bool UploadFullAtlas;

            /// <summary>?�번 ?�레?�에???�용???�롯??최�? ?�덱??+ 1. ?��??�스 ?�기 계산???�용?�다.</summary>
            public int ActiveSlotSpan;

            /// <summary>�??�롯???�유???�브?�트 참조. null?�면 ?�당 ?�롯??비어 ?�음???��??�다.</summary>
            public object[] PreviousOwners;

            /// <summary>?�번 ?�레?�에 �??�롯???�용?�었?��? ?��?. BeginSpriteAtlasFrame?�서 초기?�된??</summary>
            public bool[] PreviousUsed;

            /// <summary>�??�롯???�전 ?�레???�프?�이???��? ?�스. 변�?감�????�용?�다.</summary>
            public Color[][] PreviousSpriteSources;

            /// <summary>�??�롯???�전 ?�레???�격 ?�조 ?�래�? 변�?감�????�용?�다.</summary>
            public bool[] PreviousSpriteTintFlags;

            /// <summary>?�효???�롯 ???�롯 ?�덱?�의 ?�한). ?��??�스 치수 계산???�용?�다.</summary>
            public int PreviousSpriteCount;

            /// <summary>마�?�??��??�스 빌드 ?�의 ?�비(?��?). ?�이?�웃 변�?감�????�용?�다.</summary>
            public int PreviousAtlasWidth;

            /// <summary>마�?�??��??�스 빌드 ?�의 ?�이(?��?). ?�이?�웃 변�?감�????�용?�다.</summary>
            public int PreviousAtlasHeight;

            /// <summary>?�유???�브?�트 참조�??�롯 ?�덱?�로 매핑?�다.</summary>
            public readonly Dictionary<object, int> Slots;

            /// <summary>?�제???�롯 ?�덱?��? 보�??�는 ?�역 free-list ?�택?�다.</summary>
            public readonly Stack<int> FreeSlots;

            /// <summary>???�롯 ?�당 ???�용???�음 ?�역 ?�롯 ?�덱?�다.</summary>
            public int NextSlot;

            /// <summary>
            /// SpriteAtlasCache�?초기?�한??
            /// ?�롯 맵과 free-list�?초기?�한??
            /// </summary>
            public SpriteAtlasCache()
            {
                Slots = new Dictionary<object, int>(ReferenceComparer.Instance);
                FreeSlots = new Stack<int>();
            }
        }

        /// <summary>
        /// 참조 ?�일??ReferenceEquals)�?비교?�는 IEqualityComparer 구현.
        /// SpriteAtlasCache???�롯 �??�셔?�리?�서 ?�브?�트 참조�??�로 ?�용?????�요?�다.
        /// </summary>
        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            /// <summary>?��????�스?�스.</summary>
            public static readonly ReferenceComparer Instance = new();

            /// <summary>
            /// ???�브?�트 참조가 ?�일?��?(같�? 메모�?주소) ?�인?�다.
            /// </summary>
            /// <param name="x">비교??�?번째 ?�브?�트.</param>
            /// <param name="y">비교????번째 ?�브?�트.</param>
            /// <returns>??참조가 ?�일?�면 true.</returns>
            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            /// <summary>
            /// RuntimeHelpers.GetHashCode�??�용?�여 참조 기반 ?�시�?반환?�다.
            /// </summary>
            /// <param name="obj">?�시 코드�?계산???�브?�트.</param>
            /// <returns>참조 기반 ?�시 코드.</returns>
            public int GetHashCode(object obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }

        /// <summary>
        /// ?��??�스 캐시??Pixels 배열??atlasWidth × atlasHeight ?�상???�기�?가지?�록 ?�장?�다.
        /// ?�기가 부족하�??�로 ?�당?�고 UploadFullAtlas�?true�??�정?�다.
        /// </summary>
        /// <param name="atlasCache">?��? 배열???�장???��??�스 캐시.</param>
        /// <param name="atlasWidth">?�요???��??�스 ?�비(?��?).</param>
        /// <param name="atlasHeight">?�요???��??�스 ?�이(?��?).</param>
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
        /// �??�환 ???��??�스 캐시 ?�체�?비워 ??버퍼�??�제?�다.
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
        /// ?�프?�이???�버�??�영 ?�보�?spriteDebugBuffer??기록?�고 spriteDebugCount�?증�??�킨??
        /// </summary>
        /// <param name="instance">?�영 ?�보�?가?�올 WorldSpriteInstance.</param>
        /// <param name="atlasSlot">???�프?�이?�에 ?�당???��??�스 ?�롯 ?�덱??</param>
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
