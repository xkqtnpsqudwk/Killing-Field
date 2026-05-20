using System;
using System.Runtime.InteropServices;
using My2DEngine.Engine.Rendering.Abstractions;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace My2DEngine.Engine.Rendering.Backends.D3D11.World
{
    /// <summary>
    /// 레이저 빔·에너지 빔 등 월드 공간의 빔 이펙트를 인스턴스드 쿼드로 렌더링하는 프레젠터다.
    /// <para>
    /// 각 빔은 하나의 인스턴스로 표현되며, 버텍스 셰이더에서 위치·회전·크기를 계산한 뒤
    /// 픽셀 셰이더에서 벽 깊이 버퍼를 참조하여 차폐를 처리하고
    /// smoothstep 기반 글로우 효과를 적용한다.
    /// </para>
    /// <para>
    /// 알파 블렌딩이 활성화되어 있으며, 모든 빔은 단일 인스턴스드 드로우로 한 번에 처리된다.
    /// </para>
    /// </summary>
    internal sealed class D3D11WorldBeamPresenter : IDisposable
    {
        private const string VertexShaderSource = @"
cbuffer BeamConstants : register(b0)
{
    float2 SurfaceSize;
    float DepthBufferLength;
    float FogDensity;
    float2 Padding;
};

struct VSInput
{
    float2 corner : POSITION;
    float4 beamTransform : TEXCOORD0;
    float4 tint : COLOR0;
    float4 beamParams : TEXCOORD1;
    float4 beamSource : TEXCOORD2;
};

struct PSInput
{
    float4 position : SV_POSITION;
    float2 localTexcoord : TEXCOORD0;
    float4 tint : COLOR0;
    float depth : TEXCOORD1;
    float fog : TEXCOORD2;
    float screenX : TEXCOORD3;
    float depthBias : TEXCOORD4;
};

PSInput VSMain(VSInput input)
{
    PSInput output;
    float radians = input.beamParams.z * 0.01745329252;
    float s = sin(radians);
    float c = cos(radians);
    float scaleX = SurfaceSize.x / max(1.0, input.beamSource.x);
    float scaleY = SurfaceSize.y / max(1.0, input.beamSource.y);
    float beamCenterX = input.beamTransform.x * scaleX;
    float beamCenterY = input.beamTransform.y * scaleY;
    float beamHeight = abs(input.beamTransform.z) * scaleY;
    float beamWidth = beamHeight * max(0.01, input.beamTransform.w) * (scaleX / max(0.001, scaleY));
    float2 localPosition = float2(input.corner.x * (beamWidth * 0.5), input.corner.y * (beamHeight * 0.5));
    float2 rotatedPosition = float2(
        localPosition.x * c - localPosition.y * s,
        localPosition.x * s + localPosition.y * c);
    float2 screenPosition = float2(beamCenterX, beamCenterY) + rotatedPosition;

    output.position = float4((screenPosition.x / SurfaceSize.x) * 2.0 - 1.0, 1.0 - (screenPosition.y / SurfaceSize.y) * 2.0, 0.0, 1.0);
    output.localTexcoord = float2((input.corner.x + 1.0) * 0.5, (input.corner.y + 1.0) * 0.5);
    output.tint = input.tint;
    output.depth = input.beamParams.x;
    output.fog = max(0.2, 1.0 / (1.0 + input.beamParams.x * FogDensity));
    output.screenX = screenPosition.x;
    output.depthBias = input.beamParams.y;
    return output;
}";

        private const string PixelShaderSource = @"
cbuffer BeamConstants : register(b0)
{
    float2 SurfaceSize;
    float DepthBufferLength;
    float FogDensity;
    float2 Padding;
};

Texture2D<float> DepthBufferTexture : register(t0);

float4 PSMain(float4 position : SV_POSITION, float2 localTexcoord : TEXCOORD0, float4 tint : COLOR0, float depth : TEXCOORD1, float fog : TEXCOORD2, float screenX : TEXCOORD3, float depthBias : TEXCOORD4) : SV_TARGET
{
    int depthIndex = clamp((int)(((position.x + 0.5) / max(1.0, SurfaceSize.x)) * DepthBufferLength), 0, (int)DepthBufferLength - 1);
    float wallDepth = DepthBufferTexture.Load(int3(depthIndex, 0, 0));
    if (wallDepth > 0.0001 && wallDepth < 1000000.0 && depth > wallDepth + depthBias)
    {
        discard;
    }

    float centeredY = abs((localTexcoord.y * 2.0) - 1.0);
    float core = 1.0 - smoothstep(0.015, 0.075, centeredY);
    float innerGlow = 1.0 - smoothstep(0.07, 0.24, centeredY);
    float outerGlow = 1.0 - smoothstep(0.22, 0.88, centeredY);
    float tip = saturate(0.7 + min(localTexcoord.x, 1.0 - localTexcoord.x) / 0.04);
    float alpha = max(core, max(innerGlow * 0.72, outerGlow * 0.38)) * tip * tint.a;
    if (alpha <= 0.01)
    {
        discard;
    }

    float3 beamColor = tint.rgb * (0.48 + (core * 1.18) + (innerGlow * 0.34) + (outerGlow * 0.12));
    return float4(beamColor * fog, alpha);
}";

        /// <summary>
        /// 빔 버텍스/픽셀 셰이더 공통 상수 버퍼 구조체다.
        /// cbuffer BeamConstants(b0) 레이아웃과 정확히 대응한다.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct BeamConstants
        {
            /// <summary>최종 출력 서피스의 가로 픽셀 수다.</summary>
            public float SurfaceWidth;
            /// <summary>최종 출력 서피스의 세로 픽셀 수다.</summary>
            public float SurfaceHeight;
            /// <summary>깊이 버퍼 텍스처의 열 수다.</summary>
            public float DepthBufferLength;
            /// <summary>거리에 따른 안개 감쇠 밀도다.</summary>
            public float FogDensity;
            /// <summary>16바이트 정렬을 위한 패딩 필드다.</summary>
            public float Padding0;
            /// <summary>16바이트 정렬을 위한 패딩 필드다.</summary>
            public float Padding1;
            /// <summary>16바이트 정렬을 위한 패딩 필드다.</summary>
            public float Padding2;
            /// <summary>16바이트 정렬을 위한 패딩 필드다.</summary>
            public float Padding3;
        }

        /// <summary>D3D11 디바이스 참조다. 모든 GPU 리소스 생성에 사용된다.</summary>
        private readonly ID3D11Device device;
        /// <summary>빔 인스턴스 데이터를 처리하는 버텍스 셰이더다.</summary>
        private ID3D11VertexShader vertexShader;
        /// <summary>깊이 차폐와 smoothstep 글로우를 처리하는 픽셀 셰이더다.</summary>
        private ID3D11PixelShader pixelShader;
        /// <summary>버텍스·인스턴스 버퍼의 시맨틱 레이아웃을 정의하는 입력 레이아웃이다.</summary>
        private ID3D11InputLayout inputLayout;
        /// <summary>빔 쿼드의 로컬 코너(-1~+1) 좌표를 담는 정적 버텍스 버퍼다.</summary>
        private ID3D11Buffer quadVertexBuffer;
        /// <summary>프레임마다 갱신되는 빔 인스턴스 데이터 버퍼다(동적, WriteDiscard).</summary>
        private ID3D11Buffer instanceBuffer;
        /// <summary>상수 버퍼(<see cref="BeamConstants"/>)다. VS·PS 모두에 바인딩된다.</summary>
        private ID3D11Buffer constantBuffer;
        /// <summary>빔 반투명 합성에 사용하는 알파 블렌드 스테이트다.</summary>
        private ID3D11BlendState blendState;
        /// <summary>레이캐스트 결과에서 전달받은 벽 깊이 데이터를 저장하는 1D float 텍스처다.</summary>
        private ID3D11Texture2D depthBufferTexture;
        /// <summary><see cref="depthBufferTexture"/>에 대한 셰이더 리소스 뷰다.</summary>
        private ID3D11ShaderResourceView depthBufferView;
        /// <summary>현재 할당된 깊이 버퍼 텍스처의 열 수다.</summary>
        private int depthBufferLength;
        /// <summary>현재 인스턴스 버퍼가 수용할 수 있는 최대 빔 수다.</summary>
        private int instanceCapacity;
        /// <summary>인스턴스 버퍼에 복사하기 전 데이터를 조립하는 CPU 측 임시 버퍼다.</summary>
        private float[] instanceDataBuffer;
        /// <summary>이 객체가 이미 해제되었는지 나타내는 플래그다.</summary>
        private bool disposed;

        /// <summary>
        /// <see cref="D3D11WorldBeamPresenter"/>의 새 인스턴스를 초기화한다.
        /// 셰이더 컴파일, 입력 레이아웃, 버퍼, 블렌드 스테이트를 생성한다.
        /// </summary>
        /// <param name="device">D3D11 디바이스다. null이면 <see cref="ArgumentNullException"/>이 발생한다.</param>
        public D3D11WorldBeamPresenter(ID3D11Device device)
        {
            this.device = device ?? throw new ArgumentNullException(nameof(device));
            CreatePipelineResources();
        }

        /// <summary>
        /// 이번 프레임의 모든 빔 이펙트를 렌더링한다.
        /// 깊이 버퍼·인스턴스 데이터를 GPU에 업로드한 뒤
        /// 단일 인스턴스드 드로우(6 버텍스 × 빔 수)로 모든 빔을 한 번에 그린다.
        /// </summary>
        /// <param name="context">렌더링 명령을 제출할 D3D11 디바이스 컨텍스트다.</param>
        /// <param name="renderTargetView">빔을 출력할 렌더 타깃 뷰다.</param>
        /// <param name="surfaceWidth">최종 출력 서피스의 가로 픽셀 수다.</param>
        /// <param name="surfaceHeight">최종 출력 서피스의 세로 픽셀 수다.</param>
        /// <param name="command">빔 인스턴스 배열·깊이 버퍼 등이 담긴 명령 객체다.</param>
        public void Present(ID3D11DeviceContext context, ID3D11RenderTargetView renderTargetView, int surfaceWidth, int surfaceHeight, RenderWorldCommand command)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(D3D11WorldBeamPresenter));
            }

            if (context == null || renderTargetView == null || command.Beams == null || command.BeamCount <= 0 || command.DepthBuffer == null || command.DepthBufferLength <= 0)
            {
                return;
            }

            EnsureDepthTexture(command);
            EnsureInstanceBuffer(command.BeamCount);
            UpdateInstanceBuffer(context, command);
            UpdateConstants(context, command.TargetWidth, command.TargetHeight, command);
            context.UpdateSubresource(command.DepthBuffer, depthBufferTexture, 0, command.DepthBufferLength * sizeof(float), command.DepthBufferLength * sizeof(float), null);

            context.OMSetRenderTargets(renderTargetView, null);
            context.RSSetViewport(0f, 0f, surfaceWidth, surfaceHeight, 0f, 1f);
            context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
            context.IASetInputLayout(inputLayout);
            context.IASetVertexBuffers(
                0,
                2,
                new[] { quadVertexBuffer, instanceBuffer },
                new[] { sizeof(float) * 2, sizeof(float) * 16 },
                new[] { 0, 0 });
            context.VSSetShader(vertexShader);
            context.VSSetConstantBuffers(0, 1, new[] { constantBuffer });
            context.PSSetShader(pixelShader);
            context.PSSetConstantBuffers(0, 1, new[] { constantBuffer });
            context.PSSetShaderResources(0, 1, new[] { depthBufferView });
            context.OMSetBlendState(blendState);
            context.DrawInstanced(6, command.BeamCount, 0, 0);
            context.PSSetShaderResources(0, 1, new ID3D11ShaderResourceView[] { null });
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
        /// 빔이 없는 구간에서는 깊이/인스턴스 버퍼를 바로 해제해 이전 전투 최대치가 유지되지 않게 한다.
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

            instanceBuffer?.Dispose();
            instanceBuffer = null;
            instanceCapacity = 0;
            instanceDataBuffer = null;
        }

        /// <summary>
        /// 빔 렌더링에 필요한 모든 GPU 파이프라인 리소스를 생성한다.
        /// 셰이더 컴파일, 입력 레이아웃 정의, 쿼드 버텍스 버퍼, 상수 버퍼,
        /// 알파 블렌드 스테이트를 순서대로 초기화한다.
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
                        new InputElementDescription("TEXCOORD", 0, Format.R32G32B32A32_Float, 0, 1, InputClassification.PerInstanceData, 1),
                        new InputElementDescription("COLOR", 0, Format.R32G32B32A32_Float, 16, 1, InputClassification.PerInstanceData, 1),
                        new InputElementDescription("TEXCOORD", 1, Format.R32G32B32A32_Float, 32, 1, InputClassification.PerInstanceData, 1),
                        new InputElementDescription("TEXCOORD", 2, Format.R32G32B32A32_Float, 48, 1, InputClassification.PerInstanceData, 1)
                    },
                    vertexShaderBlob);

                quadVertexBuffer = device.CreateBuffer(
                    new BufferDescription(sizeof(float) * 2 * 6, ResourceUsage.Dynamic, BindFlags.VertexBuffer, CpuAccessFlags.Write, ResourceOptionFlags.None, 0),
                    null);

                constantBuffer = device.CreateBuffer(
                    new BufferDescription(Marshal.SizeOf<BeamConstants>(), ResourceUsage.Dynamic, BindFlags.ConstantBuffer, CpuAccessFlags.Write, ResourceOptionFlags.None, 0),
                    null);

                blendState = device.CreateBlendState(BlendDescription.AlphaBlend);
            }
            finally
            {
                vertexShaderBlob?.Dispose();
                pixelShaderBlob?.Dispose();
            }

            InitializeQuadVertices();
        }

        /// <summary>
        /// 깊이 버퍼 텍스처가 현재 명령 데이터의 열 수와 일치하는지 확인한다.
        /// 열 수가 변경되면 기존 리소스를 해제하고 새로 생성한다.
        /// </summary>
        /// <param name="command">깊이 버퍼 열 수를 포함하는 명령 객체다.</param>
        private void EnsureDepthTexture(RenderWorldCommand command)
        {
            if (depthBufferTexture == null || depthBufferLength != command.DepthBufferLength)
            {
                depthBufferView?.Dispose();
                depthBufferTexture?.Dispose();
                depthBufferTexture = device.CreateTexture2D(
                    new Texture2DDescription(Format.R32_Float, command.DepthBufferLength, 1, 1, 1, BindFlags.ShaderResource, ResourceUsage.Default, CpuAccessFlags.None, 1, 0, ResourceOptionFlags.None),
                    null);
                depthBufferView = device.CreateShaderResourceView(depthBufferTexture, null);
                depthBufferLength = command.DepthBufferLength;
            }
        }

        /// <summary>
        /// 쿼드 버텍스 버퍼를 초기화한다.
        /// 로컬 코너 좌표(-1~+1)만으로 구성된 6개의 버텍스(삼각형 2개)를 GPU에 업로드한다.
        /// 파이프라인 초기화 시 한 번만 호출된다.
        /// </summary>
        private void InitializeQuadVertices()
        {
            float[] quadVertices =
            {
                -1f, -1f,
                 1f, -1f,
                 1f,  1f,
                -1f, -1f,
                 1f,  1f,
                -1f,  1f
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
        /// 인스턴스 버퍼가 요청된 빔 수를 수용할 수 있는지 확인한다.
        /// 용량이 부족하면 현재 용량의 두 배 혹은 최솟값 16 중 큰 값으로 재할당한다.
        /// </summary>
        /// <param name="beamCount">이번 프레임에 렌더링할 빔 수다.</param>
        private void EnsureInstanceBuffer(int beamCount)
        {
            if (instanceBuffer != null && instanceCapacity >= beamCount)
            {
                return;
            }

            instanceBuffer?.Dispose();
            instanceCapacity = System.Math.Max(beamCount, System.Math.Max(16, instanceCapacity * 2));
            instanceBuffer = device.CreateBuffer(
                new BufferDescription(sizeof(float) * 16 * instanceCapacity, ResourceUsage.Dynamic, BindFlags.VertexBuffer, CpuAccessFlags.Write, ResourceOptionFlags.None, 0),
                null);
        }

        /// <summary>
        /// 빔 인스턴스 데이터를 CPU 임시 버퍼에 조립한 뒤 GPU 인스턴스 버퍼에 복사한다.
        /// 각 인스턴스는 float 16개(위치·스케일·틴트·깊이·회전·소스 크기)로 구성된다.
        /// </summary>
        /// <param name="context">맵·언맵 작업에 사용할 디바이스 컨텍스트다.</param>
        /// <param name="command">빔 인스턴스 배열을 포함하는 명령 객체다.</param>
        private void UpdateInstanceBuffer(ID3D11DeviceContext context, RenderWorldCommand command)
        {
            int requiredLength = command.BeamCount * 16;
            if (instanceDataBuffer == null || instanceDataBuffer.Length < requiredLength)
            {
                instanceDataBuffer = new float[requiredLength];
            }

            for (int i = 0; i < command.BeamCount; i++)
            {
                WorldBeamInstance instance = command.Beams[i];
                int baseIndex = i * 16;

                // 인스턴스 데이터는 BeamConstants가 아니라 VSInput 인스턴스 버퍼 레이아웃에 맞춘다.
                // transform/tint/params/source 네 개 float4 슬롯이 inputLayout에 순서대로 바인딩된다.
                instanceDataBuffer[baseIndex + 0] = instance.PositionX;
                instanceDataBuffer[baseIndex + 1] = instance.PositionY;
                instanceDataBuffer[baseIndex + 2] = instance.Scale;
                instanceDataBuffer[baseIndex + 3] = instance.WidthScale <= 0f ? 1f : instance.WidthScale;
                instanceDataBuffer[baseIndex + 4] = ((instance.TintArgb >> 16) & 0xFF) / 255f;
                instanceDataBuffer[baseIndex + 5] = ((instance.TintArgb >> 8) & 0xFF) / 255f;
                instanceDataBuffer[baseIndex + 6] = (instance.TintArgb & 0xFF) / 255f;
                instanceDataBuffer[baseIndex + 7] = ((instance.TintArgb >> 24) & 0xFF) / 255f;
                instanceDataBuffer[baseIndex + 8] = instance.Depth;
                instanceDataBuffer[baseIndex + 9] = instance.DepthBias;
                instanceDataBuffer[baseIndex + 10] = instance.RotationDegrees;
                instanceDataBuffer[baseIndex + 11] = 0f;
                instanceDataBuffer[baseIndex + 12] = instance.SourceWidth;
                instanceDataBuffer[baseIndex + 13] = instance.SourceHeight;
                instanceDataBuffer[baseIndex + 14] = 0f;
                instanceDataBuffer[baseIndex + 15] = 0f;
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
        /// 상수 버퍼(<see cref="BeamConstants"/>)를 현재 파라미터로 갱신한다.
        /// VS·PS 양쪽에 동일한 상수 버퍼가 바인딩된다.
        /// </summary>
        /// <param name="context">맵·언맵 작업에 사용할 디바이스 컨텍스트다.</param>
        /// <param name="surfaceWidth">최종 출력 서피스의 가로 픽셀 수다.</param>
        /// <param name="surfaceHeight">최종 출력 서피스의 세로 픽셀 수다.</param>
        /// <param name="command">깊이 버퍼 길이·안개 밀도를 포함하는 명령 객체다.</param>
        private void UpdateConstants(ID3D11DeviceContext context, int surfaceWidth, int surfaceHeight, RenderWorldCommand command)
        {
            var constants = new BeamConstants
            {
                SurfaceWidth = surfaceWidth,
                SurfaceHeight = surfaceHeight,
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
    }
}
