using System;
using System.Runtime.InteropServices;
using My2DEngine.Engine.Rendering.Abstractions;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace My2DEngine.Engine.Rendering.Backends.D3D11.World
{
    /// <summary>
    /// 적·투사체·전리품 등 월드 공간에 배치되는 스프라이트를 인스턴스드 쿼드로 렌더링하는 프레젠터다.
    /// <para>
    /// 각 스프라이트는 하나의 인스턴스 데이터로 표현되며, 버텍스 셰이더에서 깊이·크기·UV·틴트를
    /// 계산한 뒤 픽셀 셰이더에서 벽 깊이 버퍼를 참조하여 차폐(occlusion)를 처리한다.
    /// </para>
    /// <para>
    /// 반투명 합성을 위해 알파 블렌딩이 활성화되며, 정렬 순서를 보장하기 위해
    /// 한 번의 대형 인스턴스드 드로우 대신 스프라이트별 개별 드로우를 사용한다.
    /// </para>
    /// </summary>
    internal sealed class D3D11WorldSpritePresenter : IDisposable
    {
        private const string VertexShaderSource = @"
cbuffer SpriteConstants : register(b0)
{
    float2 SurfaceSize;
    float2 SourceSize;
    float DepthBufferLength;
    float FogDensity;
    float2 Padding;
};

struct VSInput
{
    float2 corner : POSITION;
    float2 texcoord : TEXCOORD0;
    float4 worldScale : TEXCOORD1;
    float4 uvRect : TEXCOORD2;
    float4 tint : COLOR0;
    float4 spriteParams : TEXCOORD3;
    float4 extraParams : TEXCOORD4; // x=minWallDepth, y=showHealthBar(0/1), z=showTelegraph(0/1), w=unused
};

struct PSInput
{
    float4 position : SV_POSITION;
    float2 texcoord : TEXCOORD0;
    float2 localTexcoord : TEXCOORD1;
    float4 tint : COLOR0;
    float depth : TEXCOORD2;
    float fog : TEXCOORD3;
    float screenX : TEXCOORD4;
    float valid : TEXCOORD5;
    float depthBias : TEXCOORD6;
    float renderMode : TEXCOORD7;
    float minWallDepth : TEXCOORD8;
};

PSInput VSMain(VSInput input)
{
    PSInput output;

    float spriteDepth = input.spriteParams.x;
    if (spriteDepth <= 0.05)
    {
        output.position = float4(-2.0, -2.0, 0.0, 1.0);
        output.texcoord = input.uvRect.xy;
        output.localTexcoord = input.texcoord;
        output.tint = input.tint;
        output.depth = 0.0;
        output.fog = 1.0;
        output.screenX = 0.0;
        output.valid = 0.0;
        output.minWallDepth = 3.402823466e+38;
        return output;
    }

    float spriteScreenX = input.worldScale.x;
    float spriteCenterY = input.worldScale.y;
    float spriteHeight = abs(input.worldScale.z);
    float spriteWidth = spriteHeight * max(0.01, input.worldScale.w);
    float radians = input.spriteParams.z * 0.01745329252;
    float s = sin(radians);
    float c = cos(radians);
    float2 localPosition = float2(input.corner.x * (spriteWidth * 0.5), input.corner.y * (spriteHeight * 0.5));
    float2 rotatedPosition = float2(
        localPosition.x * c - localPosition.y * s,
        localPosition.x * s + localPosition.y * c);
    float2 screenPosition = float2(
        spriteScreenX + rotatedPosition.x,
        spriteCenterY + rotatedPosition.y);

    float2 scaleToSurface = float2(
        SurfaceSize.x / max(1.0, SourceSize.x),
        SurfaceSize.y / max(1.0, SourceSize.y));
    float2 targetPosition = screenPosition * scaleToSurface;
    targetPosition = floor(targetPosition) + 0.5;
    output.position = float4((targetPosition.x / SurfaceSize.x) * 2.0 - 1.0, 1.0 - (targetPosition.y / SurfaceSize.y) * 2.0, 0.0, 1.0);
    output.texcoord = float2(
        lerp(input.uvRect.x, input.uvRect.z, input.texcoord.x),
        lerp(input.uvRect.y, input.uvRect.w, input.texcoord.y));
    output.localTexcoord = input.texcoord;
    output.tint = input.tint;
    output.depth = spriteDepth;
    output.fog = max(0.2, 1.0 / (1.0 + spriteDepth * FogDensity));
    output.screenX = spriteScreenX;
    output.valid = 1.0;
    output.depthBias = input.spriteParams.y;
    output.renderMode = input.spriteParams.w;
    output.minWallDepth = input.extraParams.x;
    return output;
}";

        private const string PixelShaderSource = @"
cbuffer SpriteConstants : register(b0)
{
    float2 SurfaceSize;
    float2 SourceSize;
    float DepthBufferLength;
    float FogDensity;
    float2 Padding;
};

Texture2D SpriteAtlasTexture : register(t0);
Texture2D<float> DepthBufferTexture : register(t1);
SamplerState PointSampler : register(s0);
SamplerState LinearSampler : register(s1);

float4 PSMain(float4 position : SV_POSITION, float2 texcoord : TEXCOORD0, float2 localTexcoord : TEXCOORD1, float4 tint : COLOR0, float depth : TEXCOORD2, float fog : TEXCOORD3, float screenX : TEXCOORD4, float valid : TEXCOORD5, float depthBias : TEXCOORD6, float renderMode : TEXCOORD7, float minWallDepth : TEXCOORD8) : SV_TARGET
{
    if (valid < 0.5)
    {
        discard;
    }

    // 1차 차폐: 현재 픽셀 컬럼의 벽 깊이로 per-column 판정.
    // DepthBufferTexture = gpuDepthBufferData (벽 컬럼만 ±3 min-filter, open 컬럼은 MaxValue 유지).
    float sourceX = (position.x / max(1.0, SurfaceSize.x)) * SourceSize.x;
    int depthIndex = clamp((int)floor(sourceX), 0, (int)DepthBufferLength - 1);
    float colWallDepth = DepthBufferTexture.Load(int3(depthIndex, 0, 0));
    if (colWallDepth > 0.0001 && colWallDepth < 3.402823466e+38 && depth > colWallDepth + depthBias)
    {
        discard;
    }


    if (renderMode > 0.5 && renderMode < 1.5)
    {
        float centeredY = abs((localTexcoord.y * 2.0) - 1.0);
        float core = 1.0 - smoothstep(0.10, 0.24, centeredY);
        float glow = 1.0 - smoothstep(0.22, 0.95, centeredY);
        float tip = saturate(min(localTexcoord.x, 1.0 - localTexcoord.x) / 0.08);
        float alpha = max(core, glow * 0.62) * tip * tint.a;
        if (alpha <= 0.01)
        {
            discard;
        }

        float3 beamColor = tint.rgb * (0.58 + (core * 0.65) + (glow * 0.22));
        return float4(beamColor * fog, alpha);
    }

    float4 color;
    if (renderMode > 1.5)
    {
        float2 atlasSize;
        SpriteAtlasTexture.GetDimensions(atlasSize.x, atlasSize.y);
        float2 atlasUv = (texcoord + 0.5) / max(float2(1.0, 1.0), atlasSize);
        color = SpriteAtlasTexture.SampleLevel(PointSampler, atlasUv, 0.0); // 픽셀 아트라 점 샘플링
    }
    else
    {
        int sampleX = (int)floor(texcoord.x + 0.5);
        int sampleY = (int)floor(texcoord.y + 0.5);
        color = SpriteAtlasTexture.Load(int3(sampleX, sampleY, 0));
    }
    float alphaCutoff = renderMode > 1.5 ? 0.001 : 0.01;
    if (color.a <= alphaCutoff)
    {
        discard;
    }

    color *= tint;
    color.rgb *= fog;
    return color;
}";

        /// <summary>
        /// 스프라이트 버텍스/픽셀 셰이더 공통 상수 버퍼 구조체다.
        /// cbuffer SpriteConstants(b0) 레이아웃과 정확히 대응한다.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct SpriteConstants
        {
            /// <summary>최종 출력 서피스의 가로 픽셀 수다.</summary>
            public float SurfaceWidth;
            /// <summary>최종 출력 서피스의 세로 픽셀 수다.</summary>
            public float SurfaceHeight;
            /// <summary>레이캐스트 소스(씬) 텍스처의 가로 픽셀 수다. 스프라이트 스케일 기준으로 사용된다.</summary>
            public float SourceWidth;
            /// <summary>레이캐스트 소스(씬) 텍스처의 세로 픽셀 수다.</summary>
            public float SourceHeight;
            /// <summary>깊이 버퍼 텍스처의 열 수다. 픽셀 셰이더에서 깊이 인덱스를 계산하는 데 쓰인다.</summary>
            public float DepthBufferLength;
            /// <summary>거리에 따른 안개 감쇠 밀도다.</summary>
            public float FogDensity;
            /// <summary>16바이트 정렬을 위한 패딩 필드다.</summary>
            public float Padding0;
            /// <summary>16바이트 정렬을 위한 패딩 필드다.</summary>
            public float Padding1;
        }

        /// <summary>D3D11 디바이스 참조다. 모든 GPU 리소스 생성에 사용된다.</summary>
        private readonly ID3D11Device device;
        /// <summary>인스턴스 데이터를 처리하는 버텍스 셰이더다.</summary>
        private ID3D11VertexShader vertexShader;
        /// <summary>텍스처 샘플링·차폐·알파 컷오프를 처리하는 픽셀 셰이더다.</summary>
        private ID3D11PixelShader pixelShader;
        /// <summary>버텍스·인스턴스 버퍼의 시맨틱 레이아웃을 정의하는 입력 레이아웃이다.</summary>
        private ID3D11InputLayout inputLayout;
        /// <summary>스프라이트 쿼드의 로컬 코너(-1~+1) 및 UV(0~1) 데이터를 담는 정적 버텍스 버퍼다.</summary>
        private ID3D11Buffer quadVertexBuffer;
        /// <summary>프레임마다 갱신되는 스프라이트 인스턴스 데이터 버퍼다(동적, WriteDiscard).</summary>
        private ID3D11Buffer instanceBuffer;
        /// <summary>상수 버퍼(<see cref="SpriteConstants"/>)다.</summary>
        private ID3D11Buffer constantBuffer;
        /// <summary>포인트 필터링 샘플러 스테이트다. 픽셀 아트 스타일의 선명한 텍셀을 위해 사용된다.</summary>
        private ID3D11SamplerState pointSampler;
        /// <summary>선형 필터링 샘플러 스테이트다. renderMode &gt; 1.5 인 스프라이트(부드러운 렌더링)에 사용된다.</summary>
        private ID3D11SamplerState linearSampler;
        /// <summary>스트레이트 알파 블렌딩 스테이트다. 반투명 스프라이트 합성에 사용된다.</summary>
        private ID3D11BlendState blendState;
        /// <summary>모든 스프라이트 프레임을 하나로 모은 아틀라스 텍스처다.</summary>
        private ID3D11Texture2D spriteAtlasTexture;
        /// <summary><see cref="spriteAtlasTexture"/>에 대한 셰이더 리소스 뷰다.</summary>
        private ID3D11ShaderResourceView spriteAtlasView;
        /// <summary>레이캐스트 결과에서 전달받은 벽 깊이 데이터를 저장하는 1D float 텍스처다.</summary>
        private ID3D11Texture2D depthBufferTexture;
        /// <summary><see cref="depthBufferTexture"/>에 대한 셰이더 리소스 뷰다.</summary>
        private ID3D11ShaderResourceView depthBufferView;
        /// <summary>현재 할당된 스프라이트 아틀라스 텍스처의 가로 픽셀 수다.</summary>
        private int spriteAtlasWidth;
        /// <summary>현재 할당된 스프라이트 아틀라스 텍스처의 세로 픽셀 수다.</summary>
        private int spriteAtlasHeight;
        /// <summary>현재 할당된 깊이 버퍼 텍스처의 열 수다.</summary>
        private int depthBufferLength;
        /// <summary>현재 인스턴스 버퍼가 수용할 수 있는 최대 스프라이트 수다.</summary>
        private int instanceCapacity;
        /// <summary>인스턴스 버퍼에 복사하기 전 데이터를 조립하는 CPU 측 임시 버퍼다.</summary>
        private float[] instanceDataBuffer;
        /// <summary>이 객체가 이미 해제되었는지 나타내는 플래그다.</summary>
        private bool disposed;

        /// <summary>
        /// <see cref="D3D11WorldSpritePresenter"/>의 새 인스턴스를 초기화한다.
        /// 셰이더 컴파일, 입력 레이아웃, 버퍼, 샘플러, 블렌드 스테이트를 생성한다.
        /// </summary>
        /// <param name="device">D3D11 디바이스다. null이면 <see cref="ArgumentNullException"/>이 발생한다.</param>
        public D3D11WorldSpritePresenter(ID3D11Device device)
        {
            this.device = device ?? throw new ArgumentNullException(nameof(device));
            CreatePipelineResources();
        }

        /// <summary>
        /// 이번 프레임의 모든 월드 스프라이트를 렌더링한다.
        /// 아틀라스·깊이 버퍼·인스턴스 데이터를 GPU에 업로드한 뒤
        /// 스프라이트별 개별 인스턴스드 드로우로 합성 순서를 보장한다.
        /// </summary>
        /// <param name="context">렌더링 명령을 제출할 D3D11 디바이스 컨텍스트다.</param>
        /// <param name="renderTargetView">스프라이트를 출력할 렌더 타깃 뷰다.</param>
        /// <param name="surfaceWidth">최종 출력 서피스의 가로 픽셀 수다.</param>
        /// <param name="surfaceHeight">최종 출력 서피스의 세로 픽셀 수다.</param>
        /// <param name="command">스프라이트 인스턴스·아틀라스·깊이 버퍼 등이 담긴 명령 객체다.</param>
        public void Present(ID3D11DeviceContext context, ID3D11RenderTargetView renderTargetView, int surfaceWidth, int surfaceHeight, RenderWorldSpritePassCommand command)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(D3D11WorldSpritePresenter));
            }

            if (context == null || renderTargetView == null || command.Sprites == null || command.SpriteCount <= 0 ||
                command.SpriteAtlasPixels == null || command.SpriteAtlasWidth <= 0 || command.SpriteAtlasHeight <= 0 ||
                command.DepthBuffer == null || command.DepthBufferLength <= 0)
            {
                return;
            }

            EnsureTextures(command);
            EnsureInstanceBuffer(command.SpriteCount);
            UpdateInstanceBuffer(context, command);
            UpdateConstants(context, surfaceWidth, surfaceHeight, command);

            UpdateSpriteAtlas(context, command);
            context.UpdateSubresource(command.DepthBuffer, depthBufferTexture, 0, command.DepthBufferLength * sizeof(float), command.DepthBufferLength * sizeof(float), null);

            context.OMSetRenderTargets(renderTargetView, null);
            context.RSSetViewport(0f, 0f, surfaceWidth, surfaceHeight, 0f, 1f);
            context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
            context.IASetInputLayout(inputLayout);
            context.IASetVertexBuffers(
                0,
                2,
                new[] { quadVertexBuffer, instanceBuffer },
                new[] { sizeof(float) * 4, sizeof(float) * 20 },
                new[] { 0, 0 });
            context.VSSetShader(vertexShader);
            context.VSSetConstantBuffers(0, 1, new[] { constantBuffer });
            context.PSSetShader(pixelShader);
            context.PSSetShaderResources(0, 2, new[] { spriteAtlasView, depthBufferView });
            context.PSSetSamplers(0, 2, new[] { pointSampler, linearSampler });
            context.OMSetBlendState(blendState);
            // 반투명 스프라이트는 draw 순서가 바로 최종 합성 결과가 되므로,
            // 한 번의 대형 instanced draw보다 정렬된 순서대로 개별 draw를 보내는 편이 안정적이다.
            for (int i = 0; i < command.SpriteCount; i++)
            {
                context.DrawInstanced(6, 1, 0, i);
            }
            context.PSSetShaderResources(0, 2, new ID3D11ShaderResourceView[] { null, null });
        }

        /// <summary>
        /// 이 프레젠터가 보유한 모든 GPU 리소스를 해제한다.
        /// 중복 호출은 무시된다.
        /// </summary>
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            depthBufferView?.Dispose();
            depthBufferView = null;
            depthBufferTexture?.Dispose();
            depthBufferTexture = null;
            spriteAtlasView?.Dispose();
            spriteAtlasView = null;
            spriteAtlasTexture?.Dispose();
            spriteAtlasTexture = null;
            pointSampler?.Dispose();
            pointSampler = null;
            linearSampler?.Dispose();
            linearSampler = null;
            blendState?.Dispose();
            blendState = null;
            constantBuffer?.Dispose();
            constantBuffer = null;
            instanceBuffer?.Dispose();
            instanceBuffer = null;
            quadVertexBuffer?.Dispose();
            quadVertexBuffer = null;
            inputLayout?.Dispose();
            inputLayout = null;
            pixelShader?.Dispose();
            pixelShader = null;
            vertexShader?.Dispose();
            vertexShader = null;
            disposed = true;
        }

        /// <summary>
        /// 현재 방에서 더 이상 스프라이트를 그리지 않을 때 큰 아틀라스/깊이/인스턴스 버퍼를 즉시 내려놓는다.
        /// presenter 객체 자체는 유지하므로 다음 방에서 필요해지면 리소스만 다시 생성된다.
        /// </summary>
        public void ReleaseTransientResources()
        {
            if (disposed)
            {
                return;
            }

            depthBufferView?.Dispose();
            depthBufferView = null;
            depthBufferTexture?.Dispose();
            depthBufferTexture = null;
            depthBufferLength = 0;

            spriteAtlasView?.Dispose();
            spriteAtlasView = null;
            spriteAtlasTexture?.Dispose();
            spriteAtlasTexture = null;
            spriteAtlasWidth = 0;
            spriteAtlasHeight = 0;

            instanceBuffer?.Dispose();
            instanceBuffer = null;
            instanceCapacity = 0;
            instanceDataBuffer = null;
        }

        /// <summary>
        /// 스프라이트 렌더링에 필요한 모든 GPU 파이프라인 리소스를 생성한다.
        /// 셰이더 컴파일, 입력 레이아웃 정의, 쿼드 버텍스 버퍼, 상수 버퍼,
        /// 샘플러 스테이트, 알파 블렌드 스테이트를 순서대로 초기화한다.
        /// </summary>
        private void CreatePipelineResources()
        {
            Blob vertexShaderBlob = D3D11ShaderCompiler.Compile(VertexShaderSource, "VSMain", "vs_4_0");
            Blob pixelShaderBlob = D3D11ShaderCompiler.Compile(PixelShaderSource, "PSMain", "ps_4_0");
            try
            {
                vertexShader = device.CreateVertexShader(vertexShaderBlob.BufferPointer, vertexShaderBlob.BufferSize);
                pixelShader = device.CreatePixelShader(pixelShaderBlob.BufferPointer, pixelShaderBlob.BufferSize);
                inputLayout = device.CreateInputLayout(
                    new[]
                    {
                        new InputElementDescription("POSITION", 0, Format.R32G32_Float, 0, 0, InputClassification.PerVertexData, 0),
                        new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 8, 0, InputClassification.PerVertexData, 0),
                        new InputElementDescription("TEXCOORD", 1, Format.R32G32B32A32_Float, 0, 1, InputClassification.PerInstanceData, 1),
                        new InputElementDescription("TEXCOORD", 2, Format.R32G32B32A32_Float, 16, 1, InputClassification.PerInstanceData, 1),
                        new InputElementDescription("COLOR", 0, Format.R32G32B32A32_Float, 32, 1, InputClassification.PerInstanceData, 1),
                        new InputElementDescription("TEXCOORD", 3, Format.R32G32B32A32_Float, 48, 1, InputClassification.PerInstanceData, 1),
                        new InputElementDescription("TEXCOORD", 4, Format.R32G32B32A32_Float, 64, 1, InputClassification.PerInstanceData, 1)
                    },
                    vertexShaderBlob);

                quadVertexBuffer = device.CreateBuffer(
                    new BufferDescription(
                        sizeof(float) * 4 * 6,
                        ResourceUsage.Dynamic,
                        BindFlags.VertexBuffer,
                        CpuAccessFlags.Write,
                        ResourceOptionFlags.None,
                        0),
                    null);

                constantBuffer = device.CreateBuffer(
                    new BufferDescription(
                        Marshal.SizeOf<SpriteConstants>(),
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

                linearSampler = device.CreateSamplerState(new SamplerDescription(
                    Filter.MinMagMipLinear,
                    TextureAddressMode.Clamp,
                    TextureAddressMode.Clamp,
                    TextureAddressMode.Clamp,
                    0f,
                    1,
                    ComparisonFunction.Never,
                    System.Drawing.Color.Black,
                    0f,
                    float.MaxValue));

                var straightAlphaBlend = new BlendDescription
                {
                    AlphaToCoverageEnable = false,
                    IndependentBlendEnable = false
                };
                straightAlphaBlend.RenderTarget[0] = new RenderTargetBlendDescription
                {
                    IsBlendEnabled = true,
                    SourceBlend = Blend.SourceAlpha,
                    DestinationBlend = Blend.InverseSourceAlpha,
                    BlendOperation = BlendOperation.Add,
                    SourceBlendAlpha = Blend.One,
                    DestinationBlendAlpha = Blend.InverseSourceAlpha,
                    BlendOperationAlpha = BlendOperation.Add,
                    RenderTargetWriteMask = ColorWriteEnable.All
                };
                blendState = device.CreateBlendState(straightAlphaBlend);
            }
            finally
            {
                vertexShaderBlob?.Dispose();
                pixelShaderBlob?.Dispose();
            }

            InitializeQuadVertices();
        }

        /// <summary>
        /// 스프라이트 아틀라스 텍스처와 깊이 버퍼 텍스처가 현재 명령 데이터와 크기가 일치하는지 확인한다.
        /// 크기가 다르면 기존 리소스를 해제하고 새로 생성한다.
        /// </summary>
        /// <param name="command">텍스처 크기 정보를 포함하는 명령 객체다.</param>
        private void EnsureTextures(RenderWorldSpritePassCommand command)
        {
            if (spriteAtlasTexture == null || spriteAtlasWidth != command.SpriteAtlasWidth || spriteAtlasHeight != command.SpriteAtlasHeight)
            {
                spriteAtlasView?.Dispose();
                spriteAtlasTexture?.Dispose();
                spriteAtlasTexture = CreateColorTexture(command.SpriteAtlasWidth, command.SpriteAtlasHeight);
                spriteAtlasView = device.CreateShaderResourceView(spriteAtlasTexture, null);
                spriteAtlasWidth = command.SpriteAtlasWidth;
                spriteAtlasHeight = command.SpriteAtlasHeight;
            }

            if (depthBufferTexture == null || depthBufferLength != command.DepthBufferLength)
            {
                depthBufferView?.Dispose();
                depthBufferTexture?.Dispose();
                depthBufferTexture = device.CreateTexture2D(
                    new Texture2DDescription(
                        Format.R32_Float,
                        command.DepthBufferLength,
                        1,
                        1,
                        1,
                        BindFlags.ShaderResource,
                        ResourceUsage.Default,
                        CpuAccessFlags.None,
                        1,
                        0,
                        ResourceOptionFlags.None),
                    null);
                depthBufferView = device.CreateShaderResourceView(depthBufferTexture, null);
                depthBufferLength = command.DepthBufferLength;
            }
        }


        /// <summary>
        /// 스프라이트 아틀라스 텍스처를 GPU에 업로드한다.
        /// <see cref="RenderWorldSpritePassCommand.UploadFullSpriteAtlas"/>가 true이거나
        /// 더티 렉트 정보가 없으면 전체 아틀라스를 업로드한다.
        /// 그렇지 않으면 변경된 영역(<see cref="RenderWorldSpritePassCommand.DirtySpriteAtlasRects"/>)만 부분 업데이트한다.
        /// </summary>
        /// <param name="context">서브리소스 업데이트에 사용할 디바이스 컨텍스트다.</param>
        /// <param name="command">아틀라스 픽셀 데이터 및 더티 렉트 정보를 포함하는 명령 객체다.</param>
        private void UpdateSpriteAtlas(ID3D11DeviceContext context, RenderWorldSpritePassCommand command)
        {
            if (command.UploadFullSpriteAtlas || command.DirtySpriteAtlasRectCount <= 0 || command.DirtySpriteAtlasRects == null)
            {
                context.UpdateSubresource(command.SpriteAtlasPixels, spriteAtlasTexture, 0, command.SpriteAtlasWidth * sizeof(int), command.SpriteAtlasPixels.Length * sizeof(int), null);
                return;
            }

            GCHandle pinnedPixels = GCHandle.Alloc(command.SpriteAtlasPixels, GCHandleType.Pinned);
            try
            {
                IntPtr basePointer = pinnedPixels.AddrOfPinnedObject();
                for (int i = 0; i < command.DirtySpriteAtlasRectCount; i++)
                {
                    int baseIndex = i * 4;
                    int rectX = command.DirtySpriteAtlasRects[baseIndex + 0];
                    int rectY = command.DirtySpriteAtlasRects[baseIndex + 1];
                    int rectWidth = command.DirtySpriteAtlasRects[baseIndex + 2];
                    int rectHeight = command.DirtySpriteAtlasRects[baseIndex + 3];
                    if (rectWidth <= 0 || rectHeight <= 0)
                    {
                        continue;
                    }

                    int sourceOffset = ((rectY * command.SpriteAtlasWidth) + rectX) * sizeof(int);
                    var targetBox = new Box(rectX, rectY, 0, rectX + rectWidth, rectY + rectHeight, 1);
                    // UpdateSubresource의 row pitch는 전체 아틀라스 한 줄의 바이트 수다.
                    // dirty rect 시작 주소만 이동시키고 pitch는 원본 배열의 전체 너비를 유지해야 각 행이 올바르게 건너뛴다.
                    context.UpdateSubresource(
                        spriteAtlasTexture,
                        0,
                        targetBox,
                        IntPtr.Add(basePointer, sourceOffset),
                        command.SpriteAtlasWidth * sizeof(int),
                        rectHeight * command.SpriteAtlasWidth * sizeof(int));
                }
            }
            finally
            {
                pinnedPixels.Free();
            }
        }

        /// <summary>
        /// 쿼드 버텍스 버퍼를 초기화한다.
        /// 로컬 코너 좌표(-1~+1)와 UV 좌표(0~1)로 구성된 6개의 버텍스(삼각형 2개)를 GPU에 업로드한다.
        /// 파이프라인 초기화 시 한 번만 호출된다.
        /// </summary>
        private void InitializeQuadVertices()
        {
            float[] quadVertices =
            {
                -1f, -1f, 0f, 0f,
                 1f, -1f, 1f, 0f,
                 1f,  1f, 1f, 1f,
                -1f, -1f, 0f, 0f,
                 1f,  1f, 1f, 1f,
                -1f,  1f, 0f, 1f
            };

            ID3D11DeviceContext context = device.ImmediateContext;
            if (context == null)
            {
                return;
            }

            MappedSubresource mapped = context.Map(quadVertexBuffer, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
            try
            {
                Marshal.Copy(quadVertices, 0, mapped.DataPointer, quadVertices.Length);
            }
            finally
            {
                context.Unmap(quadVertexBuffer, 0);
            }
        }

        /// <summary>
        /// 인스턴스 버퍼가 요청된 스프라이트 수를 수용할 수 있는지 확인한다.
        /// 용량이 부족하면 현재 용량의 두 배 혹은 최솟값 32 중 큰 값으로 재할당한다.
        /// </summary>
        /// <param name="spriteCount">이번 프레임에 렌더링할 스프라이트 수다.</param>
        private void EnsureInstanceBuffer(int spriteCount)
        {
            if (instanceBuffer != null && instanceCapacity >= spriteCount)
            {
                return;
            }

            instanceBuffer?.Dispose();
            instanceCapacity = System.Math.Max(spriteCount, System.Math.Max(32, instanceCapacity * 2));
            instanceBuffer = device.CreateBuffer(
                new BufferDescription(
                    sizeof(float) * 20 * instanceCapacity,
                    ResourceUsage.Dynamic,
                    BindFlags.VertexBuffer,
                    CpuAccessFlags.Write,
                    ResourceOptionFlags.None,
                    0),
                null);
        }

        /// <summary>
        /// 스프라이트 인스턴스 데이터를 CPU 임시 버퍼에 조립한 뒤 GPU 인스턴스 버퍼에 복사한다.
        /// 각 인스턴스는 float 20개(위치·스케일·UV·틴트·파라미터)로 구성된다.
        /// </summary>
        /// <param name="context">맵·언맵 작업에 사용할 디바이스 컨텍스트다.</param>
        /// <param name="command">스프라이트 인스턴스 배열을 포함하는 명령 객체다.</param>
        private void UpdateInstanceBuffer(ID3D11DeviceContext context, RenderWorldSpritePassCommand command)
        {
            int requiredLength = command.SpriteCount * 20;
            if (instanceDataBuffer == null || instanceDataBuffer.Length < requiredLength)
            {
                instanceDataBuffer = new float[requiredLength];
            }

            for (int i = 0; i < command.SpriteCount; i++)
            {
                WorldSpriteInstance instance = command.Sprites[i];
                int baseIndex = i * 20;

                // 이 배열 레이아웃은 HLSL VSInput의 인스턴스 시맨틱 순서와 1:1로 대응한다.
                // 필드 순서를 바꾸면 inputLayout과 shader input도 같이 바꿔야 한다.
                instanceDataBuffer[baseIndex + 0] = instance.PositionX;
                instanceDataBuffer[baseIndex + 1] = instance.PositionY;
                instanceDataBuffer[baseIndex + 2] = instance.Scale;
                instanceDataBuffer[baseIndex + 3] = instance.WidthScale <= 0f ? 1f : instance.WidthScale;
                instanceDataBuffer[baseIndex + 4] = instance.U0;
                instanceDataBuffer[baseIndex + 5] = instance.V0;
                instanceDataBuffer[baseIndex + 6] = instance.U1;
                instanceDataBuffer[baseIndex + 7] = instance.V1;
                instanceDataBuffer[baseIndex + 8] = ((instance.TintArgb >> 16) & 0xFF) / 255f;
                instanceDataBuffer[baseIndex + 9] = ((instance.TintArgb >> 8) & 0xFF) / 255f;
                instanceDataBuffer[baseIndex + 10] = (instance.TintArgb & 0xFF) / 255f;
                instanceDataBuffer[baseIndex + 11] = ((instance.TintArgb >> 24) & 0xFF) / 255f;
                instanceDataBuffer[baseIndex + 12] = instance.VerticalOffsetFactor;
                instanceDataBuffer[baseIndex + 13] = instance.DepthBias;
                instanceDataBuffer[baseIndex + 14] = instance.RotationDegrees;
                instanceDataBuffer[baseIndex + 15] = instance.RenderMode;
                instanceDataBuffer[baseIndex + 16] = instance.MinWallDepth;
                instanceDataBuffer[baseIndex + 17] = instance.ShowHealthBar ? 1f : 0f;
                instanceDataBuffer[baseIndex + 18] = instance.ShowTelegraph ? 1f : 0f;
                instanceDataBuffer[baseIndex + 19] = 0f;
            }

            MappedSubresource mapped = context.Map(instanceBuffer, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
            try
            {
                Marshal.Copy(instanceDataBuffer, 0, mapped.DataPointer, requiredLength);
            }
            finally
            {
                context.Unmap(instanceBuffer, 0);
            }
        }

        /// <summary>
        /// 상수 버퍼(<see cref="SpriteConstants"/>)를 현재 파라미터로 갱신한다.
        /// </summary>
        /// <param name="context">맵·언맵 작업에 사용할 디바이스 컨텍스트다.</param>
        /// <param name="surfaceWidth">최종 출력 서피스의 가로 픽셀 수다.</param>
        /// <param name="surfaceHeight">최종 출력 서피스의 세로 픽셀 수다.</param>
        /// <param name="command">소스 크기·깊이 버퍼 길이·안개 밀도를 포함하는 명령 객체다.</param>
        private void UpdateConstants(ID3D11DeviceContext context, int surfaceWidth, int surfaceHeight, RenderWorldSpritePassCommand command)
        {
            var constants = new SpriteConstants
            {
                SurfaceWidth = surfaceWidth,
                SurfaceHeight = surfaceHeight,
                SourceWidth = command.TargetWidth,
                SourceHeight = command.TargetHeight,
                DepthBufferLength = command.DepthBufferLength,
                FogDensity = command.FogDensity
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
        /// B8G8R8A8_UNorm 포맷의 셰이더 리소스 전용 2D 컬러 텍스처를 생성한다.
        /// 스프라이트 아틀라스 저장에 사용된다.
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
    }
}
