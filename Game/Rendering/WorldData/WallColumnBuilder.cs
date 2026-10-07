using System;
using My2DEngine.Game;
using My2DEngine.Game.Config;
using My2DEngine.Game.Map;
using My2DEngine.Game.Rendering;
using My2DEngine.Game.Systems;

namespace My2DEngine.Rendering.WorldData
{
    /// <summary>
    /// 화면 열마다 DDA 레이캐스트로 벽을 찾아 GPU에 넘길 열 데이터(지오메트리·재질·문 진행도)와 깊이 버퍼를 만든다.
    /// 깊이 버퍼는 원본과, 스프라이트 차폐 판정용으로 ±3 최솟값 필터를 건 것 두 가지다.
    /// </summary>
    internal sealed class WallColumnBuilder
    {
        private readonly MapManager mapManager;
        private readonly TextureManager textureManager;

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


        public WallColumnBuilder(MapManager mapManager, TextureManager textureManager)
        {
            this.mapManager = mapManager;
            this.textureManager = textureManager;
        }

        /// <summary>열당 4개 float(perpWallDist, fullDrawStart, drawStart, drawEnd).</summary>
        public float[] Geometry => wallColumnGeometryBuffer;

        /// <summary>열당 4개 int(textureId, texX, side, tileType).</summary>
        public int[] Material => wallColumnMaterialBuffer;

        /// <summary>열마다 문 개방 진행도.</summary>
        public float[] DoorProgress => wallColumnDoorProgressBuffer;

        /// <summary>열마다 원본 벽 깊이.</summary>
        public float[] Depth => depthBufferData;

        /// <summary>±3 최솟값 필터를 건 깊이. GPU 스프라이트와 CPU 오버레이가 같은 기준으로 가린다.</summary>
        public float[] SmoothedDepth => gpuDepthBufferData;

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
        public void Build(Player player, int[,] map, int[,] textureIds, int mapWidth, int mapHeight, int renderWidth, int renderHeight)
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
                                double sScale = renderHeight / WorldProjection.GetRenderDistance(sd);
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

                double renderDist = WorldProjection.GetRenderDistance(perpWallDist);
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
                if (tileType == WorldConfig.DoorTileType)
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
                int texX = (int)(wallX * RenderConfig.WorldTextureSize);
                if (side == 0 && rayDirX > 0) texX = RenderConfig.WorldTextureSize - texX - 1;
                if (side == 1 && rayDirY < 0) texX = RenderConfig.WorldTextureSize - texX - 1;
                if (texX < 0) texX = 0;
                if (texX >= RenderConfig.WorldTextureSize) texX = RenderConfig.WorldTextureSize - 1;

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
        /// depthBufferData에 ±Radius(3) 칸 min-filter를 적용하여 gpuDepthBufferData에 저장한다.
        /// GPU 셰이더가 단일 룩업으로 이 버퍼를 참조하면, 벽 경계 근처에서도 스프라이트가 올바르게 차폐된다.
        /// 열린 공간(float.MaxValue) 열은 인접 벽의 깊이를 전파받지 않는다.
        /// </summary>
        public void BuildSmoothedDepth()
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
        /// GPU 경로에서 계산된 gpuDepthBufferData(±3 min-filter 적용본)를
        /// CPU 오버레이용 depthBuffer에 복사한다.
        /// 해상도가 다를 때는 min-filter 리샘플링으로 축소한다.
        /// </summary>
        /// <param name="depthBuffer">CPU 오버레이가 참조할 깊이 버퍼(double[]). 이 함수에서 갱신된다.</param>
        /// <param name="renderWidth">CPU 프레임버퍼 너비(픽셀).</param>
        /// <param name="worldTargetWidth">GPU 월드 렌더 대상 너비(픽셀).</param>
        public void CopyDepthForCpuOverlays(double[] depthBuffer, int renderWidth, int worldTargetWidth)
        {
            // gpuDepthBufferData는 BuildSmoothedDepth(Radius=3)로 ±3 min-filter된 버퍼다.
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
    }
}
