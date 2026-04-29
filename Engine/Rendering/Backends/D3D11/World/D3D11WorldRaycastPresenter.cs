using System;
using System.Runtime.InteropServices;
using My2DEngine.Engine.Rendering.Abstractions;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace My2DEngine.Engine.Rendering.Backends.D3D11.World
{
    /// <summary>
    /// CPU가 열(column) 단위로 미리 계산한 벽 레이캐스트 결과를 GPU로 전달하여
    /// 벽·바닥·천장을 한 번의 풀스크린 패스로 렌더링하는 프레젠터다.
    /// <para>
    /// 핵심 설계 목표는 픽셀 셰이더 내부에서 DDA 알고리즘을 수행하지 않도록
    /// 열 단위 지오메트리·머티리얼·도어 진행 데이터를 텍스처로 업로드한 뒤
    /// 픽셀 셰이더가 단순 룩업만 하도록 하여 픽셀당 연산 비용을 크게 낮추는 것이다.
    /// </para>
    /// <para>
    /// 렌더링은 두 단계로 나뉜다:
    /// 1. <see cref="RenderScene"/> — 내부 씬 텍스처에 벽/바닥/천장을 그린다.
    /// 2. <see cref="CompositeScene"/> — 씬 텍스처를 최종 렌더 타깃에 합성한다.
    /// </para>
    /// </summary>
    internal sealed class D3D11WorldRaycastPresenter : IDisposable
    {
        private const string VertexShaderSource = @"
struct VSOutput
{
    float4 position : SV_POSITION;
    float2 uv : TEXCOORD0;
};

VSOutput VSMain(uint vertexId : SV_VertexID)
{
    VSOutput output;

    float2 positions[3];
    positions[0] = float2(-1.0, -1.0);
    positions[1] = float2(-1.0, 3.0);
    positions[2] = float2(3.0, -1.0);

    float2 uvs[3];
    uvs[0] = float2(0.0, 1.0);
    uvs[1] = float2(0.0, -1.0);
    uvs[2] = float2(2.0, 1.0);

    output.position = float4(positions[vertexId], 0.0, 1.0);
    output.uv = uvs[vertexId];
    return output;
}";

        private const string PixelShaderSource = @"
cbuffer WorldConstants : register(b0)
{
    float2 PlayerPosition;
    float2 PlayerDirection;
    float2 PlayerPlane;
    float2 SurfaceSize;
    int ColumnCount;
    int TextureSize;
    int WallTextureCount;
    int DoorTileType;
    int FloorTextureIndex;
    int UseTexturedFloor;
    float PlayerEyeZ;
    int Padding1;
    float NearPlane;
    float NearPlaneSoftness;
    float FloorBlend;
    float FogDensity;
    float4 FloorColor;
    float4 CeilingColor;
    int CeilingTextureIndex;
    int UseTexturedCeiling;
    float CeilingBlend;
    int Padding2;
};

Texture2D<float4> ColumnGeometryTexture : register(t0);
Texture2D<int4> ColumnMaterialTexture : register(t1);
Texture2D<float> ColumnDoorProgressTexture : register(t2);
Texture2D WallAtlasTexture : register(t3);
Texture2D DoorOpenTexture : register(t4);
SamplerState PointSampler : register(s0);

float GetFogFactor(float distanceValue)
{
    float fog = 1.0 / (1.0 + distanceValue * FogDensity);
    return max(0.2, fog);
}

float GetRenderDistance(float wallDistance)
{
    if (wallDistance <= 0.0001)
    {
        return 0.0001;
    }

    if (wallDistance >= NearPlane)
    {
        return wallDistance;
    }

    float delta = NearPlane - wallDistance;
    float blend = sqrt(delta * delta + NearPlaneSoftness * NearPlaneSoftness);
    return NearPlane - 0.5 * (delta + NearPlaneSoftness - blend);
}

float4 SampleWallColor(int textureId, int texX, int texY)
{
    int safeTextureId = textureId;
    if (safeTextureId < 0)
    {
        safeTextureId = 0;
    }

    if (WallTextureCount > 0)
    {
        safeTextureId = safeTextureId % WallTextureCount;
    }

    float atlasWidth = TextureSize * WallTextureCount;
    float2 uv = float2(((safeTextureId * TextureSize) + texX + 0.5) / atlasWidth, (texY + 0.5) / TextureSize);
    return WallAtlasTexture.Sample(PointSampler, uv);
}

float4 ComputeFloorOrCeiling(float screenX, float screenY)
{
    float centerY = SurfaceSize.y * 0.5;
    if (screenY < centerY)
    {
        float p = centerY - screenY + 0.5;
        float posZ = 0.5 * SurfaceSize.y;
        float rowDist = posZ / max(p, 0.0001);
        float fog = GetFogFactor(rowDist);

        if (UseTexturedCeiling == 0)
        {
            float4 flatCeiling = CeilingColor;
            flatCeiling.rgb *= fog;
            flatCeiling.a = 1.0;
            return flatCeiling;
        }

        float cameraXc = ((screenX + 0.5) / SurfaceSize.x) * 2.0 - 1.0;
        float2 rayDirC = PlayerDirection + PlayerPlane * cameraXc;
        float2 ceilPos = PlayerPosition + rayDirC * rowDist;
        float2 fracPosC = frac(ceilPos);
        float4 sampledCeiling = SampleWallColor(CeilingTextureIndex, (int)(fracPosC.x * TextureSize), (int)(fracPosC.y * TextureSize));
        float4 blendedCeiling = lerp(sampledCeiling, CeilingColor, CeilingBlend);
        blendedCeiling.rgb *= fog;
        blendedCeiling.a = 1.0;
        return blendedCeiling;
    }

    float p = screenY - centerY + 0.5;
    float posZ = 0.5 * SurfaceSize.y;
    float rowDist = posZ / max(p, 0.0001);
    float fog = GetFogFactor(rowDist);
    if (UseTexturedFloor == 0)
    {
        float4 flatFloor = FloorColor;
        flatFloor.rgb *= fog;
        flatFloor.a = 1.0;
        return flatFloor;
    }

    float cameraX = ((screenX + 0.5) / SurfaceSize.x) * 2.0 - 1.0;
    float2 rayDir = PlayerDirection + PlayerPlane * cameraX;
    float2 floorPos = PlayerPosition + rayDir * rowDist;
    float2 fracPos = frac(floorPos);
    float4 sampledFloor = SampleWallColor(FloorTextureIndex, (int)(fracPos.x * TextureSize), (int)(fracPos.y * TextureSize));
    float4 blendedFloor = lerp(sampledFloor, FloorColor, FloorBlend);
    blendedFloor.rgb *= fog;
    blendedFloor.a = 1.0;
    return blendedFloor;
}

float4 PSMain(float4 position : SV_POSITION, float2 uv : TEXCOORD0) : SV_TARGET
{
    float screenX = position.x;
    float screenY = position.y;
    int column = clamp((int)(((screenX + 0.5) / max(1.0, SurfaceSize.x)) * ColumnCount), 0, ColumnCount - 1);

    // Row 1: 계단 데이터 (stepDepth=0 이면 없음)
    float4 stepGeo = ColumnGeometryTexture.Load(int3(column, 1, 0));
    float stepDepth      = stepGeo.x;
    float stepFaceTop    = stepGeo.y;  // riser 위쪽 픽셀
    float stepFaceBottom = stepGeo.z;  // riser 아래쪽 픽셀
    float upperFloor     = stepGeo.w;  // 계단 위 바닥 높이

    if (stepDepth > 0.0)
    {
        float cameraXs = ((screenX + 0.5) / SurfaceSize.x) * 2.0 - 1.0;
        float2 rayDirs = PlayerDirection + PlayerPlane * cameraXs;
        float centerY = SurfaceSize.y * 0.5;

        // ── 수직면(riser): stepFaceTop ~ stepFaceBottom ──────────────────
        if (screenY >= stepFaceTop && screenY <= stepFaceBottom)
        {
            // 타일 경계 교점에서 텍스처 X 산출
            float2 stepPos = PlayerPosition + rayDirs * stepDepth;
            float wallX = (abs(rayDirs.x) > abs(rayDirs.y)) ? frac(stepPos.y) : frac(stepPos.x);
            int texX = clamp((int)(wallX * TextureSize), 0, TextureSize - 1);

            float stepH = max(stepFaceBottom - stepFaceTop, 0.0001);
            float texPos = (screenY - stepFaceTop) / stepH;
            int texY = clamp((int)(texPos * TextureSize), 0, TextureSize - 1);

            float4 color = SampleWallColor(FloorTextureIndex, texX, texY);
            color.rgb *= GetFogFactor(stepDepth);
            color.a = 1.0;
            return color;
        }

        // ── 수평면(tread): centerY ~ stepFaceTop 사이의 바닥 픽셀 ────────
        if (screenY >= centerY && screenY < stepFaceTop)
        {
            float p = screenY - centerY + 0.5;
            float eyeDiff = PlayerEyeZ - upperFloor;
            if (eyeDiff > 0.0)
            {
                float rowDist = eyeDiff * SurfaceSize.y / max(p, 0.0001);
                float2 floorPos = PlayerPosition + rayDirs * rowDist;
                float2 fracPos = frac(floorPos);
                int ftx = clamp((int)(fracPos.x * TextureSize), 0, TextureSize - 1);
                int fty = clamp((int)(fracPos.y * TextureSize), 0, TextureSize - 1);
                float4 color = SampleWallColor(FloorTextureIndex, ftx, fty);
                color.rgb *= GetFogFactor(rowDist);
                color.a = 1.0;
                return color;
            }
        }
    }

    float4 geometry = ColumnGeometryTexture.Load(int3(column, 0, 0));
    float depth = geometry.x;
    if (depth <= 0.0)
    {
        return ComputeFloorOrCeiling(screenX, screenY);
    }

    float fullDrawStart = geometry.y;
    float drawStart = geometry.z;
    float drawEnd = geometry.w;
    if (screenY < drawStart || screenY > drawEnd)
    {
        return ComputeFloorOrCeiling(screenX, screenY);
    }

    int4 material = ColumnMaterialTexture.Load(int3(column, 0, 0));
    int textureId = material.x;
    int texX = clamp(material.y, 0, TextureSize - 1);
    int side = material.z;
    int tileType = material.w;
    float doorProgress = ColumnDoorProgressTexture.Load(int3(column, 0, 0));

    float renderDist = GetRenderDistance(depth);
    float lineHeight = SurfaceSize.y / renderDist;
    float texPos = (screenY - fullDrawStart) / max(lineHeight, 0.0001);
    int texY = clamp((int)(texPos * TextureSize), 0, TextureSize - 1);

    float4 color = SampleWallColor(textureId, texX, texY);
    if (tileType == DoorTileType)
    {
        float2 doorUv = float2((texX + 0.5) / TextureSize, (texY + 0.5) / TextureSize);
        float4 openColor = DoorOpenTexture.Sample(PointSampler, doorUv);
        color = lerp(color, openColor, doorProgress);
    }

    if (side == 1)
    {
        color.rgb *= 0.7;
    }

    color.rgb *= GetFogFactor(depth);
    color.a = 1.0;
    return color;
}";

        private const string CompositePixelShaderSource = @"
cbuffer CompositeConstants : register(b0)
{
    float2 SurfaceSize;
    float2 ScreenOffset;
    float CompositeRotationDegrees;
    float CompositeScale;
    float CompositeOffsetY;
    float CompositePadding0;
};

Texture2D sceneTexture : register(t0);
SamplerState sceneSampler : register(s0);

float4 PSMain(float4 position : SV_POSITION, float2 uv : TEXCOORD0) : SV_TARGET
{
    float2 shiftedUv = uv - (ScreenOffset / max(float2(1.0, 1.0), SurfaceSize));
    if (shiftedUv.x < 0.0 || shiftedUv.x > 1.0 || shiftedUv.y < 0.0 || shiftedUv.y > 1.0)
    {
        return float4(0.0, 0.0, 0.0, 1.0);
    }

    return sceneTexture.Sample(sceneSampler, shiftedUv);
}";

        /// <summary>
        /// 픽셀 셰이더에 전달되는 월드 렌더링 상수 버퍼 구조체다.
        /// 카메라 변환, 서피스 크기, 텍스처 파라미터, 안개·바닥 혼합 값 등
        /// 한 프레임에 필요한 모든 상수를 포함한다.
        /// cbuffer 레이아웃(b0)과 정확히 대응하도록 순서를 유지해야 한다.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct WorldConstants
        {
            /// <summary>플레이어(카메라) 월드 위치의 X 좌표다.</summary>
            /// <summary>플레이어(카메라) 월드 위치의 X 좌표다.</summary>
            public float PlayerPositionX;
            /// <summary>플레이어(카메라) 월드 위치의 Y 좌표다.</summary>
            public float PlayerPositionY;
            /// <summary>카메라 시선 방향 벡터의 X 성분이다.</summary>
            public float PlayerDirectionX;
            /// <summary>카메라 시선 방향 벡터의 Y 성분이다.</summary>
            public float PlayerDirectionY;
            /// <summary>카메라 뷰 플레인(투사 평면) 벡터의 X 성분이다.</summary>
            public float PlayerPlaneX;
            /// <summary>카메라 뷰 플레인(투사 평면) 벡터의 Y 성분이다.</summary>
            public float PlayerPlaneY;
            /// <summary>렌더링 대상 서피스의 가로 픽셀 수다.</summary>
            public float SurfaceWidth;
            /// <summary>렌더링 대상 서피스의 세로 픽셀 수다.</summary>
            public float SurfaceHeight;
            /// <summary>레이캐스트 열(column)의 총 개수다.</summary>
            public int ColumnCount;
            /// <summary>벽 텍스처 한 장의 가로/세로 픽셀 크기다(정방형을 가정).</summary>
            public int TextureSize;
            /// <summary>아틀라스에 포함된 벽 텍스처의 총 개수다.</summary>
            public int WallTextureCount;
            /// <summary>도어 타일로 분류되는 타일 타입 ID다.</summary>
            public int DoorTileType;
            /// <summary>아틀라스 내 바닥 텍스처의 인덱스다.</summary>
            public int FloorTextureIndex;
            /// <summary>텍스처 바닥을 사용할 경우 1, 단색 바닥일 경우 0이다.</summary>
            public int UseTexturedFloor;
            /// <summary>플레이어 눈 높이 (playerFloorZ + 0.5). 계단 수평면 렌더링에 사용.</summary>
            public float PlayerEyeZ;
            /// <summary>16바이트 정렬을 맞추기 위한 패딩 필드다.</summary>
            public int Padding1;
            /// <summary>근거리 클리핑 보정이 시작되는 거리 임계값이다.</summary>
            public float NearPlane;
            /// <summary>근거리 클리핑 보정의 부드러움 정도(소프트니스)다.</summary>
            public float NearPlaneSoftness;
            /// <summary>텍스처 바닥과 단색 바닥 사이의 블렌딩 비율이다(0=텍스처, 1=단색).</summary>
            public float FloorBlend;
            /// <summary>거리에 따른 안개(Fog) 감쇠 밀도다. 값이 클수록 안개가 짙다.</summary>
            public float FogDensity;
            /// <summary>단색 바닥 색상의 R 채널 값이다(0~1 범위).</summary>
            public float FloorColorR;
            /// <summary>단색 바닥 색상의 G 채널 값이다(0~1 범위).</summary>
            public float FloorColorG;
            /// <summary>단색 바닥 색상의 B 채널 값이다(0~1 범위).</summary>
            public float FloorColorB;
            /// <summary>단색 바닥 색상의 A 채널 값이다(0~1 범위).</summary>
            public float FloorColorA;
            /// <summary>천장 색상의 R 채널 값이다(0~1 범위).</summary>
            public float CeilingColorR;
            /// <summary>천장 색상의 G 채널 값이다(0~1 범위).</summary>
            public float CeilingColorG;
            /// <summary>천장 색상의 B 채널 값이다(0~1 범위).</summary>
            public float CeilingColorB;
            /// <summary>천장 색상의 A 채널 값이다(0~1 범위).</summary>
            public float CeilingColorA;
            /// <summary>아틀라스 내 천장 텍스처의 인덱스다.</summary>
            public int CeilingTextureIndex;
            /// <summary>텍스처 천장을 사용할 경우 1, 단색 천장일 경우 0이다.</summary>
            public int UseTexturedCeiling;
            /// <summary>텍스처 천장과 단색 천장 사이의 블렌딩 비율이다(0=텍스처, 1=단색).</summary>
            public float CeilingBlend;
            /// <summary>16바이트 정렬을 맞추기 위한 패딩 필드다.</summary>
            public int Padding2;
        }

        /// <summary>
        /// 합성(Composite) 패스 픽셀 셰이더에 전달되는 상수 버퍼 구조체다.
        /// 씬 텍스처를 최종 렌더 타깃에 붙여넣을 때 적용되는
        /// 오프셋·회전·스케일 파라미터를 포함한다.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct CompositeConstants
        {
            /// <summary>최종 출력 서피스의 가로 픽셀 수다.</summary>
            public float SurfaceWidth;
            /// <summary>최종 출력 서피스의 세로 픽셀 수다.</summary>
            public float SurfaceHeight;
            /// <summary>씬 텍스처를 화면에 붙일 때 적용하는 수평 픽셀 오프셋이다.</summary>
            public float ScreenOffsetX;
            /// <summary>씬 텍스처를 화면에 붙일 때 적용하는 수직 픽셀 오프셋이다.</summary>
            public float ScreenOffsetY;
            /// <summary>합성 시 씬 텍스처에 적용하는 회전 각도(도 단위)다.</summary>
            public float CompositeRotationDegrees;
            /// <summary>합성 시 씬 텍스처에 적용하는 스케일 배율이다.</summary>
            public float CompositeScale;
            /// <summary>합성 시 씬 텍스처의 수직 오프셋 보정값이다.</summary>
            public float CompositeOffsetY;
            /// <summary>16바이트 정렬을 맞추기 위한 패딩 필드다.</summary>
            public float CompositePadding0;
        }

        /// <summary>렌더링 디바이스 참조다. 셰이더·버퍼·텍스처 생성에 사용된다.</summary>
        private readonly ID3D11Device device;
        /// <summary>풀스크린 삼각형을 출력하는 버텍스 셰이더다.</summary>
        private ID3D11VertexShader vertexShader;
        /// <summary>벽·바닥·천장을 픽셀 단위로 계산하는 메인 픽셀 셰이더다.</summary>
        private ID3D11PixelShader pixelShader;
        /// <summary>메인 패스 상수 버퍼(<see cref="WorldConstants"/>)다.</summary>
        private ID3D11Buffer constantBuffer;
        /// <summary>합성 패스 상수 버퍼(<see cref="CompositeConstants"/>)다.</summary>
        private ID3D11Buffer compositeConstantBuffer;
        /// <summary>포인트 필터링 샘플러 스테이트다. 텍셀 경계를 선명하게 유지한다.</summary>
        private ID3D11SamplerState pointSampler;
        /// <summary>열(column)별 지오메트리 데이터(깊이·드로우 범위)를 저장하는 1D 텍스처다.</summary>
        private ID3D11Texture2D columnGeometryTexture;
        /// <summary><see cref="columnGeometryTexture"/>에 대한 셰이더 리소스 뷰다.</summary>
        private ID3D11ShaderResourceView columnGeometryView;
        /// <summary>열별 머티리얼 데이터(텍스처 ID·텍셀 X·면 방향·타일 타입)를 저장하는 1D 텍스처다.</summary>
        private ID3D11Texture2D columnMaterialTexture;
        /// <summary><see cref="columnMaterialTexture"/>에 대한 셰이더 리소스 뷰다.</summary>
        private ID3D11ShaderResourceView columnMaterialView;
        /// <summary>열별 도어 열림 진행도(0~1)를 저장하는 1D float 텍스처다.</summary>
        private ID3D11Texture2D columnDoorProgressTexture;
        /// <summary><see cref="columnDoorProgressTexture"/>에 대한 셰이더 리소스 뷰다.</summary>
        private ID3D11ShaderResourceView columnDoorProgressView;
        /// <summary>모든 벽 텍스처를 가로로 이어 붙인 아틀라스 텍스처다.</summary>
        private ID3D11Texture2D wallAtlasTexture;
        /// <summary><see cref="wallAtlasTexture"/>에 대한 셰이더 리소스 뷰다.</summary>
        private ID3D11ShaderResourceView wallAtlasView;
        /// <summary>열린 문 상태를 표현하는 텍스처다.</summary>
        private ID3D11Texture2D doorOpenTexture;
        /// <summary><see cref="doorOpenTexture"/>에 대한 셰이더 리소스 뷰다.</summary>
        private ID3D11ShaderResourceView doorOpenView;
        /// <summary>중간 씬 렌더 타깃 텍스처다. 합성 전에 결과가 여기에 기록된다.</summary>
        private ID3D11Texture2D sceneTexture;
        /// <summary><see cref="sceneTexture"/>에 대한 렌더 타깃 뷰다.</summary>
        private ID3D11RenderTargetView sceneRenderTargetView;
        /// <summary><see cref="sceneTexture"/>에 대한 셰이더 리소스 뷰다(합성 패스 입력용).</summary>
        private ID3D11ShaderResourceView sceneTextureView;
        /// <summary>씬 텍스처를 최종 화면에 합성하는 픽셀 셰이더다.</summary>
        private ID3D11PixelShader compositePixelShader;
        /// <summary>현재 할당된 씬 텍스처의 가로 픽셀 수다.</summary>
        private int sceneTextureWidth;
        /// <summary>현재 할당된 씬 텍스처의 세로 픽셀 수다.</summary>
        private int sceneTextureHeight;
        /// <summary>현재 할당된 열 텍스처의 열 수다. 변경 시 재생성 여부를 판단하는 데 쓰인다.</summary>
        private int columnTextureWidth;
        /// <summary>현재 할당된 벽 아틀라스 텍스처의 가로 픽셀 수다.</summary>
        private int wallAtlasWidth;
        /// <summary>현재 할당된 벽 아틀라스 텍스처의 세로 픽셀 수다.</summary>
        private int wallAtlasHeight;
        /// <summary>현재 할당된 도어 텍스처의 가로 픽셀 수다.</summary>
        private int doorTextureWidth;
        /// <summary>현재 할당된 도어 텍스처의 세로 픽셀 수다.</summary>
        private int doorTextureHeight;
        /// <summary>마지막으로 업로드한 벽 아틀라스 픽셀 데이터의 해시값이다. 변경 여부를 빠르게 비교한다.</summary>
        private int wallAtlasHash;
        /// <summary>마지막으로 업로드한 도어 텍스처 픽셀 데이터의 해시값이다.</summary>
        private int doorTextureHash;
        /// <summary>이 객체가 이미 해제되었는지 나타내는 플래그다.</summary>
        private bool disposed;

        /// <summary>
        /// <see cref="D3D11WorldRaycastPresenter"/>의 새 인스턴스를 초기화한다.
        /// 셰이더 컴파일 및 GPU 파이프라인 리소스 생성을 수행한다.
        /// </summary>
        /// <param name="device">D3D11 디바이스다. null이면 <see cref="ArgumentNullException"/>이 발생한다.</param>
        public D3D11WorldRaycastPresenter(ID3D11Device device)
        {
            this.device = device ?? throw new ArgumentNullException(nameof(device));
            CreatePipelineResources();
        }

        /// <summary>
        /// 내부 씬 텍스처에 벽·바닥·천장을 렌더링한다.
        /// CPU 레이캐스트 결과(열 지오메트리·머티리얼·도어 진행도)를 GPU에 업로드한 뒤
        /// 풀스크린 삼각형 하나로 전체 씬을 그린다.
        /// </summary>
        /// <param name="context">렌더링 명령을 제출할 D3D11 디바이스 컨텍스트다.</param>
        /// <param name="command">레이캐스트 결과 및 렌더링 파라미터가 담긴 명령 객체다.</param>
        /// <exception cref="ObjectDisposedException">이 인스턴스가 이미 해제된 경우 발생한다.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="context"/>가 null인 경우 발생한다.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="command"/>의 필수 데이터가 없거나 유효하지 않은 경우 발생한다.</exception>
        public void RenderScene(ID3D11DeviceContext context, RenderWorldCommand command)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(D3D11WorldRaycastPresenter));
            }

            if (context == null)
            {
                throw new ArgumentNullException();
            }

            if (!IsValid(command))
            {
                throw new InvalidOperationException("World render command is incomplete.");
            }

            int targetWidth = command.TargetWidth;
            int targetHeight = command.TargetHeight;

            EnsureSceneRenderTarget(targetWidth, targetHeight);
            EnsureSceneTextures(context, command);
            UpdateConstants(context, command);

            context.OMSetRenderTargets(sceneRenderTargetView, null);
            context.RSSetViewport(0f, 0f, targetWidth, targetHeight, 0f, 1f);
            context.ClearRenderTargetView(sceneRenderTargetView, System.Drawing.Color.Black);
            context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
            context.IASetInputLayout(null);
            context.VSSetShader(vertexShader);
            context.PSSetShader(pixelShader);
            context.PSSetConstantBuffers(0, 1, new[] { constantBuffer });
            context.PSSetShaderResources(0, 1, new[] { columnGeometryView });
            context.PSSetShaderResources(1, 1, new[] { columnMaterialView });
            context.PSSetShaderResources(2, 1, new[] { columnDoorProgressView });
            context.PSSetShaderResources(3, 1, new[] { wallAtlasView });
            context.PSSetShaderResources(4, 1, new[] { doorOpenView });
            context.PSSetSamplers(0, 1, new[] { pointSampler });
            context.Draw(3, 0);
            context.PSSetShaderResources(0, 5, new ID3D11ShaderResourceView[] { null, null, null, null, null });
        }

        /// <summary>
        /// <see cref="RenderScene"/>에서 그린 내부 씬 텍스처를 지정한 렌더 타깃에 합성한다.
        /// 오프셋·스케일·회전 파라미터를 적용하여 최종 화면에 출력한다.
        /// </summary>
        /// <param name="context">렌더링 명령을 제출할 D3D11 디바이스 컨텍스트다.</param>
        /// <param name="renderTargetView">결과를 출력할 최종 렌더 타깃 뷰다.</param>
        /// <param name="surfaceWidth">최종 출력 서피스의 가로 픽셀 수다.</param>
        /// <param name="surfaceHeight">최종 출력 서피스의 세로 픽셀 수다.</param>
        /// <param name="command">합성 파라미터(오프셋·스케일·회전 등)가 담긴 명령 객체다.</param>
        /// <exception cref="ObjectDisposedException">이 인스턴스가 이미 해제된 경우 발생한다.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> 또는 <paramref name="renderTargetView"/>가 null인 경우 발생한다.</exception>
        public void CompositeScene(ID3D11DeviceContext context, ID3D11RenderTargetView renderTargetView, int surfaceWidth, int surfaceHeight, RenderWorldCommand command)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(D3D11WorldRaycastPresenter));
            }

            if (context == null || renderTargetView == null)
            {
                throw new ArgumentNullException();
            }

            EnsureSceneRenderTarget(command.TargetWidth, command.TargetHeight);
            context.OMSetRenderTargets(renderTargetView, null);
            context.RSSetViewport(0f, 0f, surfaceWidth, surfaceHeight, 0f, 1f);
            context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
            context.IASetInputLayout(null);
            context.VSSetShader(vertexShader);
            context.PSSetShader(compositePixelShader);
            UpdateCompositeConstants(context, surfaceWidth, surfaceHeight, command);
            context.PSSetConstantBuffers(0, 1, new[] { compositeConstantBuffer });
            context.PSSetShaderResources(0, 1, new[] { sceneTextureView });
            context.PSSetSamplers(0, 1, new[] { pointSampler });
            context.Draw(3, 0);
            context.PSSetShaderResources(0, 1, new ID3D11ShaderResourceView[] { null });
        }

        /// <summary>
        /// 내부 씬 렌더 타깃 뷰를 반환한다.
        /// 필요 시 씬 텍스처를 재생성하여 크기를 맞춘다.
        /// 외부 시스템이 씬 텍스처에 직접 그려야 할 때 사용한다.
        /// </summary>
        /// <param name="command">렌더 타깃 크기를 결정하는 명령 객체다.</param>
        /// <returns>현재 씬 렌더 타깃 뷰를 반환한다.</returns>
        /// <exception cref="ObjectDisposedException">이 인스턴스가 이미 해제된 경우 발생한다.</exception>
        public ID3D11RenderTargetView GetSceneRenderTargetView(RenderWorldCommand command)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(D3D11WorldRaycastPresenter));
            }

            EnsureSceneRenderTarget(command.TargetWidth, command.TargetHeight);
            return sceneRenderTargetView;
        }

        /// <summary>
        /// 이 프레젠터가 보유한 모든 GPU 리소스(셰이더·버퍼·텍스처·뷰)를 해제한다.
        /// 중복 호출은 무시된다.
        /// </summary>
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            doorOpenView?.Dispose();
            doorOpenView = null;
            doorOpenTexture?.Dispose();
            doorOpenTexture = null;
            wallAtlasView?.Dispose();
            wallAtlasView = null;
            wallAtlasTexture?.Dispose();
            wallAtlasTexture = null;
            columnDoorProgressView?.Dispose();
            columnDoorProgressView = null;
            columnDoorProgressTexture?.Dispose();
            columnDoorProgressTexture = null;
            columnMaterialView?.Dispose();
            columnMaterialView = null;
            columnMaterialTexture?.Dispose();
            columnMaterialTexture = null;
            columnGeometryView?.Dispose();
            columnGeometryView = null;
            columnGeometryTexture?.Dispose();
            columnGeometryTexture = null;
            sceneTextureView?.Dispose();
            sceneTextureView = null;
            sceneRenderTargetView?.Dispose();
            sceneRenderTargetView = null;
            sceneTexture?.Dispose();
            sceneTexture = null;
            pointSampler?.Dispose();
            pointSampler = null;
            compositeConstantBuffer?.Dispose();
            compositeConstantBuffer = null;
            constantBuffer?.Dispose();
            constantBuffer = null;
            compositePixelShader?.Dispose();
            compositePixelShader = null;
            pixelShader?.Dispose();
            pixelShader = null;
            vertexShader?.Dispose();
            vertexShader = null;
            sceneTextureWidth = 0;
            sceneTextureHeight = 0;
            columnTextureWidth = 0;
            wallAtlasHash = 0;
            doorTextureHash = 0;
            disposed = true;
        }

        /// <summary>
        /// 버텍스 셰이더·픽셀 셰이더·합성 픽셀 셰이더를 컴파일하고,
        /// 상수 버퍼 및 샘플러 스테이트를 생성하는 초기화 메서드다.
        /// 생성자에서 한 번 호출된다.
        /// </summary>
        private void CreatePipelineResources()
        {
            Blob vertexShaderBlob = D3D11ShaderCompiler.Compile(VertexShaderSource, "VSMain", "vs_4_0");
            Blob pixelShaderBlob = D3D11ShaderCompiler.Compile(PixelShaderSource, "PSMain", "ps_4_0");
            Blob compositePixelShaderBlob = D3D11ShaderCompiler.Compile(CompositePixelShaderSource, "PSMain", "ps_4_0");
            try
            {
                vertexShader = device.CreateVertexShader(vertexShaderBlob.BufferPointer, vertexShaderBlob.BufferSize);
                pixelShader = device.CreatePixelShader(pixelShaderBlob.BufferPointer, pixelShaderBlob.BufferSize);
                compositePixelShader = device.CreatePixelShader(compositePixelShaderBlob.BufferPointer, compositePixelShaderBlob.BufferSize);
                constantBuffer = device.CreateBuffer(
                    new BufferDescription(
                        Marshal.SizeOf(typeof(WorldConstants)),
                        ResourceUsage.Dynamic,
                        BindFlags.ConstantBuffer,
                        CpuAccessFlags.Write,
                        ResourceOptionFlags.None,
                        0),
                    null);
                compositeConstantBuffer = device.CreateBuffer(
                    new BufferDescription(
                        Marshal.SizeOf(typeof(CompositeConstants)),
                        ResourceUsage.Dynamic,
                        BindFlags.ConstantBuffer,
                        CpuAccessFlags.Write,
                        ResourceOptionFlags.None,
                        0),
                    null);
                pointSampler = device.CreateSamplerState(new SamplerDescription(
                    Filter.MinMagMipPoint,
                    TextureAddressMode.Clamp,
                    TextureAddressMode.Clamp,
                    TextureAddressMode.Clamp,
                    0f,
                    1,
                    ComparisonFunction.Never,
                    System.Drawing.Color.Black,
                    0f,
                    float.MaxValue));
            }
            finally
            {
                vertexShaderBlob?.Dispose();
                pixelShaderBlob?.Dispose();
                compositePixelShaderBlob?.Dispose();
            }
        }

        /// <summary>
        /// 씬 렌더 타깃 텍스처가 요청된 크기와 일치하는지 확인하고,
        /// 크기가 다를 경우 기존 리소스를 해제하고 새로 생성한다.
        /// </summary>
        /// <param name="width">필요한 씬 텍스처의 가로 픽셀 수다.</param>
        /// <param name="height">필요한 씬 텍스처의 세로 픽셀 수다.</param>
        private void EnsureSceneRenderTarget(int width, int height)
        {
            if (sceneTexture != null && sceneTextureWidth == width && sceneTextureHeight == height)
            {
                return;
            }

            sceneTextureView?.Dispose();
            sceneTextureView = null;
            sceneRenderTargetView?.Dispose();
            sceneRenderTargetView = null;
            sceneTexture?.Dispose();
            sceneTexture = null;

            sceneTexture = device.CreateTexture2D(
                new Texture2DDescription(
                    Format.B8G8R8A8_UNorm,
                    width,
                    height,
                    1,
                    1,
                    BindFlags.RenderTarget | BindFlags.ShaderResource,
                    ResourceUsage.Default,
                    CpuAccessFlags.None,
                    1,
                    0,
                    ResourceOptionFlags.None),
                null);
            sceneRenderTargetView = device.CreateRenderTargetView(sceneTexture, null);
            sceneTextureView = device.CreateShaderResourceView(sceneTexture, null);
            sceneTextureWidth = width;
            sceneTextureHeight = height;
        }

        /// <summary>
        /// 씬 렌더링에 필요한 모든 보조 텍스처(열 텍스처·아틀라스 텍스처)를
        /// 최신 상태로 유지한다.
        /// </summary>
        /// <param name="context">서브리소스 업데이트에 사용할 디바이스 컨텍스트다.</param>
        /// <param name="command">텍스처 데이터를 포함하는 명령 객체다.</param>
        private void EnsureSceneTextures(ID3D11DeviceContext context, RenderWorldCommand command)
        {
            EnsureColumnTextures(context, command);
            EnsureAtlasTextures(context, command);
        }

        /// <summary>
        /// 열 지오메트리·머티리얼·도어 진행도 텍스처가 현재 열 수와 일치하는지 확인한다.
        /// 열 수가 변경되면 텍스처를 재생성하고, 항상 최신 CPU 데이터로 GPU를 업데이트한다.
        /// </summary>
        /// <param name="context">서브리소스 업데이트에 사용할 디바이스 컨텍스트다.</param>
        /// <param name="command">열 데이터를 포함하는 명령 객체다.</param>
        private void EnsureColumnTextures(ID3D11DeviceContext context, RenderWorldCommand command)
        {
            if (columnGeometryTexture == null || columnTextureWidth != command.ColumnCount)
            {
                columnGeometryView?.Dispose();
                columnGeometryView = null;
                columnGeometryTexture?.Dispose();
                columnGeometryTexture = CreateFloat4Texture(command.ColumnCount, 2); // Row 0: 벽, Row 1: 계단 면
                columnGeometryView = device.CreateShaderResourceView(columnGeometryTexture, null);

                columnMaterialView?.Dispose();
                columnMaterialView = null;
                columnMaterialTexture?.Dispose();
                columnMaterialTexture = CreateInt4Texture(command.ColumnCount, 1);
                columnMaterialView = device.CreateShaderResourceView(columnMaterialTexture, null);

                columnDoorProgressView?.Dispose();
                columnDoorProgressView = null;
                columnDoorProgressTexture?.Dispose();
                columnDoorProgressTexture = CreateFloatTexture(command.ColumnCount, 1);
                columnDoorProgressView = device.CreateShaderResourceView(columnDoorProgressTexture, null);

                columnTextureWidth = command.ColumnCount;
            }

            context.UpdateSubresource(command.WallColumnGeometry, columnGeometryTexture, 0, command.ColumnCount * sizeof(float) * 4, command.WallColumnGeometry.Length * sizeof(float), null);
            context.UpdateSubresource(command.WallColumnMaterial, columnMaterialTexture, 0, command.ColumnCount * sizeof(int) * 4, command.WallColumnMaterial.Length * sizeof(int), null);
            context.UpdateSubresource(command.WallColumnDoorProgress, columnDoorProgressTexture, 0, command.ColumnCount * sizeof(float), command.WallColumnDoorProgress.Length * sizeof(float), null);
        }

        /// <summary>
        /// 벽 아틀라스 텍스처와 도어 텍스처가 현재 크기와 일치하는지 확인한다.
        /// 크기가 변경되면 텍스처를 재생성하고, 픽셀 데이터 해시가 달라진 경우에만 GPU 업로드를 수행한다.
        /// </summary>
        /// <param name="context">서브리소스 업데이트에 사용할 디바이스 컨텍스트다.</param>
        /// <param name="command">아틀라스 픽셀 데이터를 포함하는 명령 객체다.</param>
        private void EnsureAtlasTextures(ID3D11DeviceContext context, RenderWorldCommand command)
        {
            if (wallAtlasTexture == null || wallAtlasWidth != command.WallTextureAtlasWidth || wallAtlasHeight != command.WallTextureAtlasHeight)
            {
                wallAtlasView?.Dispose();
                wallAtlasView = null;
                wallAtlasTexture?.Dispose();
                wallAtlasTexture = CreateColorTexture(command.WallTextureAtlasWidth, command.WallTextureAtlasHeight);
                wallAtlasView = device.CreateShaderResourceView(wallAtlasTexture, null);
                wallAtlasWidth = command.WallTextureAtlasWidth;
                wallAtlasHeight = command.WallTextureAtlasHeight;
            }

            if (doorOpenTexture == null || doorTextureWidth != command.DoorOpenTextureWidth || doorTextureHeight != command.DoorOpenTextureHeight)
            {
                doorOpenView?.Dispose();
                doorOpenView = null;
                doorOpenTexture?.Dispose();
                doorOpenTexture = CreateColorTexture(command.DoorOpenTextureWidth, command.DoorOpenTextureHeight);
                doorOpenView = device.CreateShaderResourceView(doorOpenTexture, null);
                doorTextureWidth = command.DoorOpenTextureWidth;
                doorTextureHeight = command.DoorOpenTextureHeight;
            }

            int nextWallAtlasHash = ComputeHash(command.WallTextureAtlasPixels);
            if (nextWallAtlasHash != wallAtlasHash)
            {
                context.UpdateSubresource(command.WallTextureAtlasPixels, wallAtlasTexture, 0, command.WallTextureAtlasWidth * sizeof(int), command.WallTextureAtlasPixels.Length * sizeof(int), null);
                wallAtlasHash = nextWallAtlasHash;
            }

            int nextDoorTextureHash = ComputeHash(command.DoorOpenTexturePixels);
            if (nextDoorTextureHash != doorTextureHash)
            {
                context.UpdateSubresource(command.DoorOpenTexturePixels, doorOpenTexture, 0, command.DoorOpenTextureWidth * sizeof(int), command.DoorOpenTexturePixels.Length * sizeof(int), null);
                doorTextureHash = nextDoorTextureHash;
            }
        }

        /// <summary>
        /// 메인 패스 상수 버퍼(<see cref="WorldConstants"/>)를 현재 명령 데이터로 갱신한다.
        /// MapMode.WriteDiscard를 사용하여 GPU 동기화 없이 빠르게 덮어쓴다.
        /// </summary>
        /// <param name="context">맵·언맵 작업에 사용할 디바이스 컨텍스트다.</param>
        /// <param name="command">카메라·텍스처·안개 등 상수 값을 포함하는 명령 객체다.</param>
        private void UpdateConstants(ID3D11DeviceContext context, RenderWorldCommand command)
        {
            var constants = new WorldConstants
            {
                PlayerPositionX = command.Camera.PositionX,
                PlayerPositionY = command.Camera.PositionY,
                PlayerDirectionX = command.Camera.DirectionX,
                PlayerDirectionY = command.Camera.DirectionY,
                PlayerPlaneX = command.Camera.PlaneX,
                PlayerPlaneY = command.Camera.PlaneY,
                SurfaceWidth = command.TargetWidth,
                SurfaceHeight = command.TargetHeight,
                ColumnCount = command.ColumnCount,
                TextureSize = command.TextureSize,
                WallTextureCount = command.WallTextureCount,
                DoorTileType = command.DoorTileType,
                FloorTextureIndex = command.FloorTextureIndex,
                UseTexturedFloor = command.UseTexturedFloor ? 1 : 0,
                PlayerEyeZ = command.PlayerEyeZ,
                Padding1 = 0,
                NearPlane = command.NearPlane,
                NearPlaneSoftness = command.NearPlaneSoftness,
                FloorBlend = command.FloorBlend,
                FogDensity = command.FogDensity,
                FloorColorR = command.FloorColor.R / 255f,
                FloorColorG = command.FloorColor.G / 255f,
                FloorColorB = command.FloorColor.B / 255f,
                FloorColorA = command.FloorColor.A / 255f,
                CeilingColorR = command.CeilingColor.R / 255f,
                CeilingColorG = command.CeilingColor.G / 255f,
                CeilingColorB = command.CeilingColor.B / 255f,
                CeilingColorA = command.CeilingColor.A / 255f,
                CeilingTextureIndex = command.CeilingTextureIndex,
                UseTexturedCeiling = command.UseTexturedCeiling ? 1 : 0,
                CeilingBlend = command.CeilingBlend,
                Padding2 = 0
            };

            MappedSubresource mapped = context.Map(constantBuffer, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
            try
            {
                Marshal.StructureToPtr(constants, mapped.DataPointer, false);
            }
            finally
            {
                context.Unmap(constantBuffer, 0);
            }
        }

        /// <summary>
        /// 합성 패스 상수 버퍼(<see cref="CompositeConstants"/>)를 현재 파라미터로 갱신한다.
        /// CompositeOffsetY는 씬 텍스처와 출력 서피스의 높이 비율에 따라 스케일된다.
        /// </summary>
        /// <param name="context">맵·언맵 작업에 사용할 디바이스 컨텍스트다.</param>
        /// <param name="surfaceWidth">최종 출력 서피스의 가로 픽셀 수다.</param>
        /// <param name="surfaceHeight">최종 출력 서피스의 세로 픽셀 수다.</param>
        /// <param name="command">오프셋·스케일·회전 파라미터를 포함하는 명령 객체다.</param>
        private void UpdateCompositeConstants(ID3D11DeviceContext context, int surfaceWidth, int surfaceHeight, RenderWorldCommand command)
        {
            var constants = new CompositeConstants
            {
                SurfaceWidth = surfaceWidth,
                SurfaceHeight = surfaceHeight,
                ScreenOffsetX = command.ScreenOffsetX,
                ScreenOffsetY = command.ScreenOffsetY,
                CompositeRotationDegrees = command.CompositeRotationDegrees,
                CompositeScale = command.CompositeScale <= 0.001f ? 1f : command.CompositeScale,
                CompositeOffsetY = command.TargetHeight > 0 ? command.CompositeOffsetY * (surfaceHeight / (float)command.TargetHeight) : command.CompositeOffsetY
            };

            MappedSubresource mapped = context.Map(compositeConstantBuffer, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
            try
            {
                Marshal.StructureToPtr(constants, mapped.DataPointer, false);
            }
            finally
            {
                context.Unmap(compositeConstantBuffer, 0);
            }
        }

        /// <summary>
        /// R32G32B32A32_Float 포맷의 셰이더 리소스 전용 2D 텍스처를 생성한다.
        /// 열 지오메트리 데이터 저장에 사용된다.
        /// </summary>
        /// <param name="width">텍스처의 가로 픽셀 수다.</param>
        /// <param name="height">텍스처의 세로 픽셀 수다.</param>
        /// <returns>생성된 2D 텍스처를 반환한다.</returns>
        private ID3D11Texture2D CreateFloat4Texture(int width, int height)
        {
            return device.CreateTexture2D(
                new Texture2DDescription(
                    Format.R32G32B32A32_Float,
                    width,
                    height,
                    1,
                    1,
                    BindFlags.ShaderResource,
                    ResourceUsage.Default,
                    CpuAccessFlags.None,
                    1,
                    0,
                    ResourceOptionFlags.None),
                null);
        }

        /// <summary>
        /// R32G32B32A32_SInt 포맷의 셰이더 리소스 전용 2D 텍스처를 생성한다.
        /// 열 머티리얼 데이터(정수 4채널) 저장에 사용된다.
        /// </summary>
        /// <param name="width">텍스처의 가로 픽셀 수다.</param>
        /// <param name="height">텍스처의 세로 픽셀 수다.</param>
        /// <returns>생성된 2D 텍스처를 반환한다.</returns>
        private ID3D11Texture2D CreateInt4Texture(int width, int height)
        {
            return device.CreateTexture2D(
                new Texture2DDescription(
                    Format.R32G32B32A32_SInt,
                    width,
                    height,
                    1,
                    1,
                    BindFlags.ShaderResource,
                    ResourceUsage.Default,
                    CpuAccessFlags.None,
                    1,
                    0,
                    ResourceOptionFlags.None),
                null);
        }

        /// <summary>
        /// R32_Float 포맷의 셰이더 리소스 전용 2D 텍스처를 생성한다.
        /// 열별 도어 진행도(단일 float) 저장에 사용된다.
        /// </summary>
        /// <param name="width">텍스처의 가로 픽셀 수다.</param>
        /// <param name="height">텍스처의 세로 픽셀 수다.</param>
        /// <returns>생성된 2D 텍스처를 반환한다.</returns>
        private ID3D11Texture2D CreateFloatTexture(int width, int height)
        {
            return device.CreateTexture2D(
                new Texture2DDescription(
                    Format.R32_Float,
                    width,
                    height,
                    1,
                    1,
                    BindFlags.ShaderResource,
                    ResourceUsage.Default,
                    CpuAccessFlags.None,
                    1,
                    0,
                    ResourceOptionFlags.None),
                null);
        }

        /// <summary>
        /// B8G8R8A8_UNorm 포맷의 셰이더 리소스 전용 2D 컬러 텍스처를 생성한다.
        /// 벽 아틀라스·도어 텍스처 저장에 사용된다.
        /// </summary>
        /// <param name="width">텍스처의 가로 픽셀 수다.</param>
        /// <param name="height">텍스처의 세로 픽셀 수다.</param>
        /// <returns>생성된 2D 텍스처를 반환한다.</returns>
        private ID3D11Texture2D CreateColorTexture(int width, int height)
        {
            return device.CreateTexture2D(
                new Texture2DDescription(
                    Format.B8G8R8A8_UNorm,
                    width,
                    height,
                    1,
                    1,
                    BindFlags.ShaderResource,
                    ResourceUsage.Default,
                    CpuAccessFlags.None,
                    1,
                    0,
                    ResourceOptionFlags.None),
                null);
        }

        /// <summary>
        /// 정수 배열의 간이 해시 값을 계산한다.
        /// 전체 요소 대신 일정 간격으로 샘플링하여 성능을 유지하면서도
        /// 변경 여부를 빠르게 감지한다. 마지막 요소는 항상 포함한다.
        /// </summary>
        /// <param name="values">해시를 계산할 정수 배열이다. null이거나 비어 있으면 0을 반환한다.</param>
        /// <returns>계산된 해시 값을 반환한다.</returns>
        private static int ComputeHash(int[] values)
        {
            if (values == null || values.Length == 0)
            {
                return 0;
            }

            unchecked
            {
                int hash = 17;
                int step = System.Math.Max(1, values.Length / 32);
                for (int i = 0; i < values.Length; i += step)
                {
                    hash = (hash * 31) + values[i];
                }

                hash = (hash * 31) + values[values.Length - 1];
                return hash;
            }
        }

        /// <summary>
        /// 렌더 명령이 GPU 렌더링을 수행하기에 충분한 데이터를 보유하고 있는지 검사한다.
        /// 열 수·지오메트리·머티리얼·도어·아틀라스·텍스처 크기 등 모든 필수 조건을 확인한다.
        /// </summary>
        /// <param name="command">검사할 렌더 명령 객체다.</param>
        /// <returns>모든 필수 데이터가 유효하면 <see langword="true"/>를 반환한다.</returns>
        private static bool IsValid(RenderWorldCommand command)
        {
            return command.ColumnCount > 0 &&
                   command.WallColumnGeometry != null &&
                   command.WallColumnGeometry.Length >= command.ColumnCount * 4 &&
                   command.WallColumnMaterial != null &&
                   command.WallColumnMaterial.Length >= command.ColumnCount * 4 &&
                   command.WallColumnDoorProgress != null &&
                   command.WallColumnDoorProgress.Length >= command.ColumnCount &&
                   command.WallTextureAtlasPixels != null &&
                   command.WallTextureAtlasWidth > 0 &&
                   command.WallTextureAtlasHeight > 0 &&
                   command.DoorOpenTexturePixels != null &&
                   command.DoorOpenTextureWidth > 0 &&
                   command.DoorOpenTextureHeight > 0 &&
                   command.TextureSize > 0;
        }
    }
}
