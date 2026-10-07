using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using My2DEngine.Engine.Rendering.Abstractions;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DirectWrite;
using Vortice.DXGI;
using Color4 = Vortice.Mathematics.Color4;

namespace My2DEngine.Engine.Rendering.Backends.D3D11
{
    /// <summary>
    /// 사각형, 이미지, 텍스트 같은 UI 오버레이를 Direct3D11 쿼드(삼각형 2개)로 렌더링하는 presenter입니다.
    /// DirectWrite와 Direct2D를 활용한 글리프 아틀라스 캐시를 통해 텍스트를 GPU 텍스처로 출력합니다.
    /// </summary>
    internal sealed class D3D11OverlayPresenter : IDisposable
    {
        /// <summary>LRU 글리프 캐시에 보관할 수 있는 최대 항목 수입니다.</summary>
        private const int MaxCachedGlyphEntries = 1024;

        /// <summary>LRU 이미지 텍스처 캐시에 보관할 수 있는 최대 항목 수입니다.</summary>
        private const int MaxCachedImageEntries = 64;

        /// <summary>
        /// 굵은 글꼴을 쓰는 글리프의 크기 값에 더하는 오프셋입니다.
        /// 글리프 캐시·텍스트 형식 캐시가 크기 하나를 키로 쓰므로, 굵기를 크기 값에 함께 담아 구분합니다.
        /// </summary>
        private const float BoldSizeOffset = 1000f;
        /// <summary>생성자 호출 시 텍스트 형식/줄 높이만 미리 준비할 폰트 크기 목록입니다.</summary>
        private static readonly float[] PreloadedGlyphSizes = { 9f, 10f, 11f, 12f, 13f, 14f, 18f, 19f, 20f, 24f, 26f, 44f };
        /// <summary>
        /// 오버레이 쿼드를 위한 HLSL 버텍스 셰이더 소스 코드입니다.
        /// NDC 좌표와 UV, 색상을 픽셀 셰이더로 그대로 전달합니다.
        /// </summary>
        private const string VertexShaderSource = @"
struct VSInput
{
    float2 position : POSITION;
    float2 texcoord : TEXCOORD0;
    float4 color : COLOR0;
};

struct PSInput
{
    float4 position : SV_POSITION;
    float2 texcoord : TEXCOORD0;
    float4 color : COLOR0;
};

PSInput VSMain(VSInput input)
{
    PSInput output;
    output.position = float4(input.position, 0.0f, 1.0f);
    output.texcoord = input.texcoord;
    output.color = input.color;
    return output;
}";

        /// <summary>
        /// 오버레이 쿼드를 위한 HLSL 픽셀 셰이더 소스 코드입니다.
        /// 텍스처를 샘플링한 뒤 버텍스 색상(틴트)을 곱하여 최종 색상을 출력합니다.
        /// </summary>
        private const string PixelShaderSource = @"
Texture2D overlayTexture : register(t0);
SamplerState overlaySampler : register(s0);

float4 PSMain(float4 position : SV_POSITION, float2 texcoord : TEXCOORD0, float4 color : COLOR0) : SV_TARGET
{
    return overlayTexture.Sample(overlaySampler, texcoord) * color;
}";

        /// <summary>GPU 자원 생성에 사용되는 Direct3D11 논리 장치입니다.</summary>
        private readonly ID3D11Device device;

        /// <summary>이미지 → 텍스처 LRU 캐시 본체입니다.</summary>
        private readonly Dictionary<Image, CachedTexture> imageCache = new Dictionary<Image, CachedTexture>();

        /// <summary>LRU 순서 관리를 위한 이미지 캐시 연결 리스트 노드 맵입니다.</summary>
        private readonly Dictionary<Image, LinkedListNode<Image>> imageCacheNodes = new Dictionary<Image, LinkedListNode<Image>>();

        /// <summary>이미지 캐시의 LRU 접근 순서를 추적하는 연결 리스트입니다.</summary>
        private readonly LinkedList<Image> imageCacheUsage = new LinkedList<Image>();

        /// <summary>글리프(문자+크기) → 비트맵 메타데이터 LRU 캐시 본체입니다.</summary>
        private readonly Dictionary<GlyphCacheKey, CachedGlyphBitmap> glyphCache = new Dictionary<GlyphCacheKey, CachedGlyphBitmap>();

        /// <summary>LRU 순서 관리를 위한 글리프 캐시 연결 리스트 노드 맵입니다.</summary>
        private readonly Dictionary<GlyphCacheKey, LinkedListNode<GlyphCacheKey>> glyphCacheNodes = new Dictionary<GlyphCacheKey, LinkedListNode<GlyphCacheKey>>();

        /// <summary>글리프 캐시의 LRU 접근 순서를 추적하는 연결 리스트입니다.</summary>
        private readonly LinkedList<GlyphCacheKey> glyphCacheUsage = new LinkedList<GlyphCacheKey>();

        /// <summary>크기 키 → 글리프 아틀라스 페이지 목록 맵입니다. 같은 크기의 글리프는 같은 아틀라스 세트에 패킹됩니다.</summary>
        private readonly Dictionary<int, List<GlyphAtlasPage>> glyphAtlasPages = new Dictionary<int, List<GlyphAtlasPage>>();

        /// <summary>크기 키 → DirectWrite 텍스트 형식 캐시입니다. 같은 크기 요청 시 재사용합니다.</summary>
        private readonly Dictionary<int, IDWriteTextFormat> textFormatCache = new Dictionary<int, IDWriteTextFormat>();

        /// <summary>크기 키 → 줄 높이 캐시입니다. DirectWrite 레이아웃 측정 결과를 재사용합니다.</summary>
        private readonly Dictionary<int, float> lineHeightCache = new Dictionary<int, float>();

        /// <summary>글리프 아틀라스 페이지에 텍스처를 생성할 때 사용하는 Direct2D 팩토리입니다.</summary>
        private readonly ID2D1Factory d2dFactory;

        /// <summary>글리프 메트릭 측정과 텍스트 형식 생성에 사용하는 DirectWrite 팩토리입니다.</summary>
        private readonly IDWriteFactory dwriteFactory;

        /// <summary>게임에 포함된 글꼴 파일로 만든 전용 글꼴 모음입니다. null이면 시스템 글꼴을 씁니다.</summary>
        private IDWriteFontCollection privateFontCollection;

        /// <summary>보통 굵기 글자에 쓸 패밀리 이름입니다.</summary>
        private string regularFamilyName = OverlayFontSettings.FallbackFamilyName;

        /// <summary>굵은 글자에 쓸 패밀리 이름입니다. 굵은 면이 같은 패밀리에 있으면 보통 패밀리와 같습니다.</summary>
        private string boldFamilyName = OverlayFontSettings.FallbackFamilyName;

        /// <summary>픽셀 글꼴 격자 크기입니다. 0이면 크기 맞춤과 앨리어싱 렌더링을 하지 않습니다.</summary>
        private int pixelGridSize;

        /// <summary>오버레이 쿼드를 위한 컴파일된 버텍스 셰이더입니다.</summary>
        private ID3D11VertexShader vertexShader;

        /// <summary>오버레이 쿼드를 위한 컴파일된 픽셀 셰이더입니다.</summary>
        private ID3D11PixelShader pixelShader;

        /// <summary>버텍스 버퍼의 레이아웃(위치, UV, 색상)을 설명하는 입력 레이아웃입니다.</summary>
        private ID3D11InputLayout inputLayout;

        /// <summary>쿼드 버텍스 데이터를 GPU에 업로드하기 위한 동적 버텍스 버퍼입니다.</summary>
        private ID3D11Buffer vertexBuffer;

        /// <summary>텍스처 샘플링에 사용하는 선형 필터 샘플러 상태입니다.</summary>
        private ID3D11SamplerState samplerState;

        /// <summary>알파 혼합(프리멀티플라이드 알파)을 위한 블렌드 상태입니다.</summary>
        private ID3D11BlendState blendState;

        /// <summary>단색(흰색) 사각형 렌더링에 재사용할 1×1 흰색 텍스처입니다.</summary>
        private CachedTexture whiteTexture;

        /// <summary>이 객체의 Dispose 여부를 나타냅니다.</summary>
        private bool disposed;

        /// <summary>
        /// 지정된 Direct3D11 장치로 <see cref="D3D11OverlayPresenter"/>를 초기화합니다.
        /// 셰이더 파이프라인 자원을 생성하고 기본 글리프 아틀라스를 미리 채웁니다.
        /// </summary>
        /// <param name="device">GPU 자원 생성에 사용할 Direct3D11 장치입니다.</param>
        /// <exception cref="ArgumentNullException"><paramref name="device"/>가 <c>null</c>인 경우 발생합니다.</exception>
        public D3D11OverlayPresenter(ID3D11Device device)
        {
            this.device = device ?? throw new ArgumentNullException(nameof(device));
            d2dFactory = D2D1.D2D1CreateFactory<ID2D1Factory>(Vortice.Direct2D1.FactoryType.SingleThreaded);
            dwriteFactory = DWrite.DWriteCreateFactory<IDWriteFactory>(Vortice.DirectWrite.FactoryType.Shared);
            LoadOverlayFonts();
            CreatePipelineResources();
            PrewarmDefaultGlyphAtlases();
        }

        /// <summary>
        /// 지정된 사각형을 흰색 1×1 텍스처 쿼드로 렌더링합니다.
        /// 너비, 높이, 알파가 유효하지 않으면 아무 동작도 하지 않습니다.
        /// </summary>
        /// <param name="context">렌더링 명령을 제출할 D3D11 즉시 실행 컨텍스트입니다.</param>
        /// <param name="renderTargetView">출력 대상 렌더 타깃 뷰입니다.</param>
        /// <param name="surfaceWidth">렌더 표면 너비(픽셀)입니다. NDC 변환에 사용됩니다.</param>
        /// <param name="surfaceHeight">렌더 표면 높이(픽셀)입니다. NDC 변환에 사용됩니다.</param>
        /// <param name="command">위치, 크기, 색상 정보가 담긴 사각형 렌더 명령입니다.</param>
        public void DrawRectangle(ID3D11DeviceContext context, ID3D11RenderTargetView renderTargetView, int surfaceWidth, int surfaceHeight, RenderRectCommand command)
        {
            if (command.Width <= 0f || command.Height <= 0f || command.Color.A <= 0)
            {
                return;
            }

            // 블렌드 상태가 premultiplied alpha(SourceBlend=One)이므로 색도 알파를 곱해 넘긴다.
            // 곱하지 않으면 반투명 색 사각형이 밝게 더해지고, 밝은 바탕에서는 흰 점으로 넘친다.
            Color c = command.Color;
            int a = c.A;
            Color premultiplied = a >= 255 ? c : Color.FromArgb(a, c.R * a / 255, c.G * a / 255, c.B * a / 255);
            CachedTexture texture = EnsureWhiteTexture(context);
            DrawQuad(context, renderTargetView, surfaceWidth, surfaceHeight, texture, command.X, command.Y, command.Width, command.Height, 0f, new RectangleF(0f, 0f, 1f, 1f), premultiplied);
        }

        /// <summary>
        /// 지정된 이미지를 GPU 텍스처 쿼드로 렌더링합니다.
        /// </summary>
        /// <param name="context">렌더링 명령을 제출할 D3D11 즉시 실행 컨텍스트입니다.</param>
        /// <param name="renderTargetView">출력 대상 렌더 타깃 뷰입니다.</param>
        /// <param name="surfaceWidth">렌더 표면 너비(픽셀)입니다.</param>
        /// <param name="surfaceHeight">렌더 표면 높이(픽셀)입니다.</param>
        /// <param name="command">이미지, 위치, 크기 정보가 담긴 이미지 렌더 명령입니다.</param>
        public void DrawImage(ID3D11DeviceContext context, ID3D11RenderTargetView renderTargetView, int surfaceWidth, int surfaceHeight, RenderImageCommand command)
        {
            if (command.Image == null || command.Width <= 0f || command.Height <= 0f)
            {
                return;
            }

            CachedTexture texture = GetOrCreateImageTexture(context, command.Image);
            if (texture == null)
            {
                return;
            }

            // UV가 비어 있으면(기본 0,0,0,0) 전체 이미지를 그린다. 9-slice 등은 부분 UV를 지정한다.
            float u0 = command.U0, v0 = command.V0, u1 = command.U1, v1 = command.V1;
            if (u1 <= u0 || v1 <= v0)
            {
                u0 = 0f; v0 = 0f; u1 = 1f; v1 = 1f;
            }

            // 텍스처가 premultiplied alpha이므로 tint도 premultiply해서 정점 색으로 넘긴다.
            // 흰색 불투명이면 (255,255,255,255) 그대로라 원본을 변형 없이 그린다.
            Color t = command.Tint;
            if (t.A == 0 && t.R == 0 && t.G == 0 && t.B == 0)
            {
                t = Color.White;
            }
            int ta = t.A;
            Color premultTint = Color.FromArgb(ta, t.R * ta / 255, t.G * ta / 255, t.B * ta / 255);

            DrawQuad(
                context,
                renderTargetView,
                surfaceWidth,
                surfaceHeight,
                texture,
                command.X,
                command.Y,
                command.Width,
                command.Height,
                0f,
                new RectangleF(u0, v0, u1 - u0, v1 - v0),
                premultTint);
        }

        /// <summary>
        /// 지정된 텍스트를 글리프 아틀라스 캐시를 이용하여 렌더링합니다.
        /// 줄바꿈(\n)을 지원하며 각 글자를 아틀라스 텍스처 쿼드로 개별 출력합니다.
        /// </summary>
        /// <param name="context">렌더링 명령을 제출할 D3D11 즉시 실행 컨텍스트입니다.</param>
        /// <param name="renderTargetView">출력 대상 렌더 타깃 뷰입니다.</param>
        /// <param name="surfaceWidth">렌더 표면 너비(픽셀)입니다.</param>
        /// <param name="surfaceHeight">렌더 표면 높이(픽셀)입니다.</param>
        /// <param name="command">텍스트 내용, 위치, 크기, 색상 정보가 담긴 텍스트 렌더 명령입니다.</param>
        public void DrawText(ID3D11DeviceContext context, ID3D11RenderTargetView renderTargetView, int surfaceWidth, int surfaceHeight, RenderTextCommand command)
        {
            if (string.IsNullOrEmpty(command.Text))
            {
                return;
            }

            float fontSpec = ResolveFontSpec(command.Size, command.Bold);
            bool snapToPixels = pixelGridSize > 0;
            float cursorX = snapToPixels ? (float)System.Math.Round(command.X) : command.X;
            float cursorY = snapToPixels ? (float)System.Math.Round(command.Y) : command.Y;
            float lineStartX = cursorX;
            float lineHeight = GetLineHeight(fontSpec);
            float maxLineHeight = lineHeight;
            string text = command.Text;

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch == '\r')
                {
                    continue;
                }

                if (ch == '\n')
                {
                    cursorX = lineStartX;
                    cursorY += maxLineHeight;
                    maxLineHeight = lineHeight;
                    continue;
                }

                CachedGlyphBitmap glyph = GetOrCreateGlyphBitmap(ch, fontSpec);
                if (glyph == null)
                {
                    continue;
                }

                GlyphAtlasPage atlasPage = EnsureGlyphAtlasTexture(context, glyph);
                if (atlasPage != null && atlasPage.Texture != null && glyph.Width > 0 && glyph.Height > 0)
                {
                    // DrawOffsetY는 atlas 슬롯 안에서 실제 글자 픽셀이 시작되는 Y 위치다.
                    // quad를 cursorY - DrawOffsetY에 배치해야 모든 글자가 동일한 기준선에 정렬된다.
                    DrawQuad(
                        context,
                        renderTargetView,
                        surfaceWidth,
                        surfaceHeight,
                        atlasPage.Texture,
                        cursorX,
                        cursorY - glyph.DrawOffsetY,
                        glyph.Width,
                        glyph.Height,
                        0f,
                        glyph.UvRect,
                        command.Color);
                }

                cursorX += glyph.AdvanceWidth;
                if (glyph.Height > maxLineHeight)
                {
                    maxLineHeight = glyph.Height;
                }
            }
        }

        /// <summary>
        /// 지정된 텍스트를 렌더링했을 때의 크기를 픽셀 단위로 측정합니다.
        /// 줄바꿈(\n)을 고려하여 멀티라인 텍스트의 최대 너비와 전체 높이를 계산합니다.
        /// </summary>
        /// <param name="text">크기를 측정할 텍스트입니다.</param>
        /// <param name="size">텍스트의 폰트 크기(포인트)입니다.</param>
        /// <returns>측정된 텍스트의 너비와 높이(픽셀)입니다. 빈 문자열이면 <see cref="SizeF.Empty"/>를 반환합니다.</returns>
        public SizeF MeasureText(string text, float size, bool bold = false)
        {
            if (string.IsNullOrEmpty(text))
            {
                return SizeF.Empty;
            }

            size = ResolveFontSpec(size, bold);
            float width = 0f;
            float lineWidth = 0f;
            float lineHeight = GetLineHeight(size);
            float height = lineHeight;

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch == '\r')
                {
                    continue;
                }

                if (ch == '\n')
                {
                    if (lineWidth > width)
                    {
                        width = lineWidth;
                    }

                    lineWidth = 0f;
                    height += lineHeight;
                    continue;
                }

                CachedGlyphBitmap glyph = GetOrCreateGlyphBitmap(ch, size);
                if (glyph == null)
                {
                    continue;
                }

                lineWidth += glyph.AdvanceWidth;
                if (glyph.Height > lineHeight)
                {
                    lineHeight = glyph.Height;
                }
            }

            if (lineWidth > width)
            {
                width = lineWidth;
            }

            return new SizeF(width, height);
        }

        /// <summary>
        /// 글리프 아틀라스 GPU 텍스처를 모두 해제하고 패킹 상태를 초기화합니다.
        /// 글리프 치수 메타데이터(너비·높이·어드밴스)는 유지하므로 다음 렌더 시 재측정 없이 재패킹됩니다.
        /// 층 전환처럼 VRAM을 일괄 회수해야 할 때 호출하십시오.
        /// </summary>
        public void FlushGlyphAtlas()
        {
            if (disposed)
            {
                return;
            }

            foreach (List<GlyphAtlasPage> pages in glyphAtlasPages.Values)
            {
                foreach (GlyphAtlasPage page in pages)
                {
                    page.Dispose();
                }
            }

            glyphAtlasPages.Clear();

            foreach (CachedGlyphBitmap glyph in glyphCache.Values)
            {
                if (glyph == null)
                {
                    continue;
                }

                glyph.AtlasPage = null;
                glyph.IsPacked = false;
            }
        }

        /// <summary>
        /// 이 presenter가 보유한 모든 GPU 자원(셰이더, 버퍼, 캐시, 아틀라스)을 해제합니다.
        /// 이미 Dispose된 경우에는 아무 동작도 하지 않습니다.
        /// </summary>
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            foreach (CachedTexture texture in imageCache.Values)
            {
                texture.Dispose();
            }

            imageCache.Clear();
            imageCacheNodes.Clear();
            imageCacheUsage.Clear();

            foreach (CachedGlyphBitmap glyphBitmap in glyphCache.Values)
            {
                glyphBitmap.Dispose();
            }

            glyphCache.Clear();
            glyphCacheNodes.Clear();
            glyphCacheUsage.Clear();
            foreach (List<GlyphAtlasPage> pages in glyphAtlasPages.Values)
            {
                foreach (GlyphAtlasPage glyphAtlasPage in pages)
                {
                    glyphAtlasPage.Dispose();
                }
            }
            glyphAtlasPages.Clear();
            foreach (IDWriteTextFormat textFormat in textFormatCache.Values)
            {
                textFormat?.Dispose();
            }
            textFormatCache.Clear();
            lineHeightCache.Clear();
            dwriteFactory?.Dispose();
            d2dFactory?.Dispose();
            whiteTexture?.Dispose();
            whiteTexture = null;
            blendState?.Dispose();
            blendState = null;
            samplerState?.Dispose();
            samplerState = null;
            vertexBuffer?.Dispose();
            vertexBuffer = null;
            inputLayout?.Dispose();
            inputLayout = null;
            pixelShader?.Dispose();
            pixelShader = null;
            vertexShader?.Dispose();
            vertexShader = null;
            disposed = true;
            privateFontCollection?.Dispose();
            privateFontCollection = null;
        }

        /// <summary>
        /// 버텍스 셰이더, 픽셀 셰이더, 입력 레이아웃, 버텍스 버퍼, 샘플러, 블렌드 상태를 생성합니다.
        /// 생성 후 셰이더 바이너리 Blob은 즉시 해제합니다.
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
                        new Vortice.Direct3D11.InputElementDescription("POSITION", 0, Format.R32G32_Float, 0, 0),
                        new Vortice.Direct3D11.InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 8, 0),
                        new Vortice.Direct3D11.InputElementDescription("COLOR", 0, Format.R32G32B32A32_Float, 16, 0)
                    },
                    vertexShaderBlob);

                vertexBuffer = device.CreateBuffer(
                    new BufferDescription(
                        sizeof(float) * 8 * 6,
                        ResourceUsage.Dynamic,
                        BindFlags.VertexBuffer,
                        CpuAccessFlags.Write,
                        ResourceOptionFlags.None,
                        0),
                    null);

                // 무기 그림 같은 픽셀 아트를 키워 그리므로 번지지 않게 점 샘플링을 쓴다.
                samplerState = device.CreateSamplerState(new SamplerDescription(
                    Vortice.Direct3D11.Filter.MinMagMipPoint,
                    TextureAddressMode.Clamp,
                    TextureAddressMode.Clamp,
                    TextureAddressMode.Clamp,
                    0f,
                    1,
                    ComparisonFunction.Never,
                    new Color4(0f, 0f, 0f, 1f),
                    0f,
                    float.MaxValue));

                //blendState = device.CreateBlendState(Vortice.Direct3D11.BlendDescription.AlphaBlend);
                var blendDesc = new Vortice.Direct3D11.BlendDescription();
                ref var rt = ref blendDesc.RenderTarget[0];
                rt.IsBlendEnabled = true;
                rt.SourceBlend = Vortice.Direct3D11.Blend.One;
                rt.DestinationBlend = Vortice.Direct3D11.Blend.InverseSourceAlpha;
                rt.BlendOperation = Vortice.Direct3D11.BlendOperation.Add;
                rt.SourceBlendAlpha = Vortice.Direct3D11.Blend.One;
                rt.DestinationBlendAlpha = Vortice.Direct3D11.Blend.InverseSourceAlpha;
                rt.BlendOperationAlpha = Vortice.Direct3D11.BlendOperation.Add;
                rt.RenderTargetWriteMask = Vortice.Direct3D11.ColorWriteEnable.All;
                blendState = device.CreateBlendState(blendDesc);
            }
            finally
            {
                vertexShaderBlob?.Dispose();
                pixelShaderBlob?.Dispose();
            }
        }

        /// <summary>
        /// 지정된 텍스처를 사용하여 회전 가능한 쿼드(삼각형 2개)를 렌더링합니다.
        /// 버텍스 좌표를 NDC로 변환하고 동적 버텍스 버퍼에 업로드한 뒤 Draw를 호출합니다.
        /// </summary>
        /// <param name="context">렌더링 명령을 제출할 D3D11 즉시 실행 컨텍스트입니다.</param>
        /// <param name="renderTargetView">출력 대상 렌더 타깃 뷰입니다.</param>
        /// <param name="surfaceWidth">렌더 표면 너비(픽셀)입니다.</param>
        /// <param name="surfaceHeight">렌더 표면 높이(픽셀)입니다.</param>
        /// <param name="texture">쿼드에 적용할 텍스처입니다.</param>
        /// <param name="x">쿼드 왼쪽 상단 X 좌표(픽셀)입니다.</param>
        /// <param name="y">쿼드 왼쪽 상단 Y 좌표(픽셀)입니다.</param>
        /// <param name="width">쿼드 너비(픽셀)입니다.</param>
        /// <param name="height">쿼드 높이(픽셀)입니다.</param>
        /// <param name="rotationDegrees">쿼드 중심 기준 회전 각도(도)입니다.</param>
        /// <param name="uvRect">텍스처에서 샘플링할 UV 영역입니다.</param>
        /// <param name="tint">버텍스 색상(틴트)입니다. 텍스처 색상에 곱해집니다.</param>
        private void DrawQuad(
            ID3D11DeviceContext context,
            ID3D11RenderTargetView renderTargetView,
            int surfaceWidth,
            int surfaceHeight,
            CachedTexture texture,
            float x,
            float y,
            float width,
            float height,
            float rotationDegrees,
            RectangleF uvRect,
            Color tint)
        {
            if (texture == null || texture.View == null || width <= 0.001f || height <= 0.001f)
            {
                return;
            }

            float centerX = x + width * 0.5f;
            float centerY = y + height * 0.5f;
            float halfW = width * 0.5f;
            float halfH = height * 0.5f;
            double radians = rotationDegrees * (System.Math.PI / 180.0);
            float cos = (float)System.Math.Cos(radians);
            float sin = (float)System.Math.Sin(radians);
            float r = tint.R / 255f;
            float g = tint.G / 255f;
            float b = tint.B / 255f;
            float a = tint.A / 255f;
            float u0 = uvRect.X;
            float v0 = uvRect.Y;
            float u1 = uvRect.X + uvRect.Width;
            float v1 = uvRect.Y + uvRect.Height;

            float[] vertices =
            {
                ToClipX(centerX + RotateX(-halfW, -halfH, cos, sin), surfaceWidth), ToClipY(centerY + RotateY(-halfW, -halfH, cos, sin), surfaceHeight), u0, v0, r, g, b, a,
                ToClipX(centerX + RotateX( halfW, -halfH, cos, sin), surfaceWidth), ToClipY(centerY + RotateY( halfW, -halfH, cos, sin), surfaceHeight), u1, v0, r, g, b, a,
                ToClipX(centerX + RotateX( halfW,  halfH, cos, sin), surfaceWidth), ToClipY(centerY + RotateY( halfW,  halfH, cos, sin), surfaceHeight), u1, v1, r, g, b, a,

                ToClipX(centerX + RotateX(-halfW, -halfH, cos, sin), surfaceWidth), ToClipY(centerY + RotateY(-halfW, -halfH, cos, sin), surfaceHeight), u0, v0, r, g, b, a,
                ToClipX(centerX + RotateX( halfW,  halfH, cos, sin), surfaceWidth), ToClipY(centerY + RotateY( halfW,  halfH, cos, sin), surfaceHeight), u1, v1, r, g, b, a,
                ToClipX(centerX + RotateX(-halfW,  halfH, cos, sin), surfaceWidth), ToClipY(centerY + RotateY(-halfW,  halfH, cos, sin), surfaceHeight), u0, v1, r, g, b, a
            };

            MappedSubresource mapped = context.Map(vertexBuffer, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
            try
            {
                Marshal.Copy(vertices, 0, mapped.DataPointer, vertices.Length);
            }
            finally
            {
                context.Unmap(vertexBuffer, 0);
            }

            context.OMSetRenderTargets(renderTargetView, null);
            context.RSSetViewport(0f, 0f, surfaceWidth, surfaceHeight, 0f, 1f);
            context.OMSetBlendState(blendState);
            context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
            context.IASetInputLayout(inputLayout);
            context.IASetVertexBuffers(0, 1, new[] { vertexBuffer }, new[] { sizeof(float) * 8 }, new[] { 0 });
            context.VSSetShader(vertexShader);
            context.PSSetShader(pixelShader);
            context.PSSetShaderResources(0, 1, new[] { texture.View });
            context.PSSetSamplers(0, 1, new[] { samplerState });
            context.Draw(6, 0);
            context.PSSetShaderResources(0, 1, new ID3D11ShaderResourceView[] { null });
        }

        /// <summary>
        /// 단색 사각형 렌더링에 사용할 1×1 흰색 텍스처를 반환합니다.
        /// 아직 생성되지 않았으면 새로 만들고 캐시합니다.
        /// </summary>
        /// <param name="context">텍스처 데이터 업로드에 사용할 컨텍스트입니다.</param>
        /// <returns>1×1 흰색 텍스처 캐시 항목입니다.</returns>
        private CachedTexture EnsureWhiteTexture(ID3D11DeviceContext context)
        {
            if (whiteTexture != null)
            {
                return whiteTexture;
            }

            whiteTexture = CreateTexture(context, new[] { unchecked((int)0xFFFFFFFF) }, 1, 1);
            return whiteTexture;
        }

        /// <summary>
        /// 이미지에 대응하는 GPU 텍스처를 LRU 캐시에서 조회하거나, 없으면 새로 생성합니다.
        /// </summary>
        /// <param name="context">텍스처 업로드에 사용할 컨텍스트입니다.</param>
        /// <param name="image">GPU 텍스처로 변환할 GDI+ 이미지입니다.</param>
        /// <returns>캐시된 또는 새로 생성된 텍스처입니다. 이미지가 유효하지 않으면 <c>null</c>을 반환합니다.</returns>
        private CachedTexture GetOrCreateImageTexture(ID3D11DeviceContext context, Image image)
        {
            if (imageCache.TryGetValue(image, out CachedTexture cachedTexture) && cachedTexture != null)
            {
                TouchImageCacheKey(image);
                return cachedTexture;
            }

            int[] pixels = ExtractImagePixels(image, out int width, out int height);
            if (pixels == null)
            {
                return null;
            }

            cachedTexture = CreateTexture(context, pixels, width, height);
            imageCache[image] = cachedTexture;
            TouchImageCacheKey(image);
            TrimImageCache();
            return cachedTexture;
        }

        /// <summary>
        /// 이미지 LRU 캐시에서 지정된 키를 최근 사용으로 갱신합니다.
        /// </summary>
        /// <param name="image">갱신할 이미지 키입니다.</param>
        private void TouchImageCacheKey(Image image)
        {
            if (imageCacheNodes.TryGetValue(image, out LinkedListNode<Image> existingNode))
            {
                imageCacheUsage.Remove(existingNode);
                imageCacheUsage.AddFirst(existingNode);
                return;
            }

            LinkedListNode<Image> node = imageCacheUsage.AddFirst(image);
            imageCacheNodes[image] = node;
        }

        /// <summary>
        /// 이미지 캐시 항목 수가 <see cref="MaxCachedImageEntries"/>를 초과하면
        /// LRU 순서에 따라 가장 오래된 항목을 제거합니다.
        /// </summary>
        private void TrimImageCache()
        {
            while (imageCache.Count > MaxCachedImageEntries)
            {
                LinkedListNode<Image> lastNode = imageCacheUsage.Last;
                if (lastNode == null)
                {
                    break;
                }

                Image key = lastNode.Value;
                imageCacheUsage.RemoveLast();
                imageCacheNodes.Remove(key);
                if (imageCache.TryGetValue(key, out CachedTexture cachedTexture) && cachedTexture != null)
                {
                    cachedTexture.Dispose();
                }

                imageCache.Remove(key);
            }
        }

        /// <summary>
        /// 문자와 크기에 대응하는 글리프 비트맵 메타데이터를 LRU 캐시에서 조회하거나, 없으면 새로 생성합니다.
        /// </summary>
        /// <param name="ch">렌더링할 문자입니다.</param>
        /// <param name="size">폰트 크기(포인트)입니다.</param>
        /// <returns>캐시된 또는 새로 생성된 글리프 비트맵 메타데이터입니다.</returns>
        private CachedGlyphBitmap GetOrCreateGlyphBitmap(char ch, float size)
        {
            var key = new GlyphCacheKey(ch, size);
            if (glyphCache.TryGetValue(key, out CachedGlyphBitmap cachedGlyph) && cachedGlyph != null)
            {
                TouchGlyphCacheKey(key);
                return cachedGlyph;
            }

            cachedGlyph = CreateGlyphBitmap(ch, size);
            cachedGlyph.AtlasPageKey = key.SizeKey;
            glyphCache[key] = cachedGlyph;
            TouchGlyphCacheKey(key);
            TrimGlyphCache();
            return cachedGlyph;
        }

        /// <summary>
        /// 글리프를 아틀라스 페이지에 패킹하고 해당 GPU 텍스처가 생성되어 있는지 보장합니다.
        /// 기존 페이지에 공간이 없으면 새 1024×1024 아틀라스 페이지를 만듭니다.
        /// </summary>
        /// <param name="context">텍스처 업로드에 사용할 컨텍스트입니다(현재 미사용, 향후 확장용).</param>
        /// <param name="glyph">아틀라스에 패킹할 글리프 메타데이터입니다.</param>
        /// <returns>글리프가 패킹된 아틀라스 페이지입니다. 글리프가 유효하지 않으면 <c>null</c>입니다.</returns>
        private GlyphAtlasPage EnsureGlyphAtlasTexture(ID3D11DeviceContext context, CachedGlyphBitmap glyph)
        {
            if (glyph == null || glyph.Width <= 0 || glyph.Height <= 0)
            {
                return null;
            }

            // 이미 아틀라스에 패킹된 글리프는 해당 페이지를 그대로 반환한다.
            if (glyph.IsPacked && glyph.AtlasPage != null)
            {
                if (glyph.AtlasPage.Texture == null)
                {
                    CreateGlyphAtlasPageTexture(glyph.AtlasPage);
                }
                return glyph.AtlasPage;
            }

            if (!glyphAtlasPages.TryGetValue(glyph.AtlasPageKey, out List<GlyphAtlasPage> pages))
            {
                pages = new List<GlyphAtlasPage>();
                glyphAtlasPages[glyph.AtlasPageKey] = pages;
            }

            // 기존 페이지 중 공간이 남은 곳에 패킹을 시도한다.
            foreach (GlyphAtlasPage existingPage in pages)
            {
                if (TryPackGlyphIntoAtlas(existingPage, glyph))
                {
                    if (existingPage.Texture == null)
                    {
                        CreateGlyphAtlasPageTexture(existingPage);
                    }
                    return existingPage;
                }
            }

            // 모든 기존 페이지가 꽉 찼으면 새 페이지를 만든다.
            GlyphAtlasPage newPage = new GlyphAtlasPage(1024, 1024);
            pages.Add(newPage);
            TryPackGlyphIntoAtlas(newPage, glyph);
            CreateGlyphAtlasPageTexture(newPage);
            return newPage;
        }

        /// <summary>
        /// 자주 쓰는 폰트 크기의 DirectWrite 포맷과 줄 높이만 미리 계산한다.
        /// 실제 글리프 아틀라스 텍스처는 첫 렌더 시점에 생성해 시작 시 GPU 메모리 선점량을 줄인다.
        /// </summary>
        private void PrewarmDefaultGlyphAtlases()
        {
            foreach (float size in PreloadedGlyphSizes)
            {
                float spec = ResolveFontSpec(size, bold: false);
                GetOrCreateTextFormat(GetSizeKey(spec));
                GetLineHeight(spec);
            }
        }

        /// <summary>
        /// 글리프를 지정된 아틀라스 페이지의 현재 커서 위치에 패킹합니다.
        /// 현재 행이 꽉 차면 다음 행으로 이동하고, 페이지 전체가 꽉 찼으면 <c>false</c>를 반환합니다.
        /// </summary>
        /// <param name="atlasPage">글리프를 패킹할 대상 아틀라스 페이지입니다.</param>
        /// <param name="glyph">패킹할 글리프 메타데이터입니다.</param>
        /// <returns>패킹에 성공하면 <c>true</c>, 페이지가 꽉 찼으면 <c>false</c>입니다.</returns>
        private bool TryPackGlyphIntoAtlas(GlyphAtlasPage atlasPage, CachedGlyphBitmap glyph)
        {
            const int padding = 1;
            if (atlasPage.CursorX + glyph.Width + padding > atlasPage.Width)
            {
                atlasPage.CursorX = padding;
                atlasPage.CursorY += atlasPage.RowHeight + padding;
                atlasPage.RowHeight = 0;
            }

            if (atlasPage.CursorY + glyph.Height + padding > atlasPage.Height)
            {
                // 이 페이지는 꽉 찼다. 호출자가 새 페이지를 만들도록 false를 반환한다.
                return false;
            }

            int dstX = atlasPage.CursorX;
            int dstY = atlasPage.CursorY;
            DrawGlyphIntoAtlas(atlasPage, glyph, dstX, dstY);

            // UV는 RectangleF의 Width/Height가 "우하단 좌표"가 아니라 "영역 크기"인 형태로 저장된다.
            // DrawQuad 쪽에서 x/y + width/height로 실제 u1/v1을 계산한다.
            glyph.UvRect = new RectangleF(
                dstX / (float)atlasPage.Width,
                dstY / (float)atlasPage.Height,
                glyph.Width / (float)atlasPage.Width,
                glyph.Height / (float)atlasPage.Height);
            glyph.IsPacked = true;
            glyph.AtlasPage = atlasPage;
            atlasPage.CursorX += glyph.Width + padding;
            if (glyph.Height > atlasPage.RowHeight)
            {
                atlasPage.RowHeight = glyph.Height;
            }
            return true;
        }

        /// <summary>
        /// 글리프 LRU 캐시에서 지정된 키를 최근 사용으로 갱신합니다.
        /// </summary>
        /// <param name="key">갱신할 글리프 캐시 키입니다.</param>
        private void TouchGlyphCacheKey(GlyphCacheKey key)
        {
            if (glyphCacheNodes.TryGetValue(key, out LinkedListNode<GlyphCacheKey> existingNode))
            {
                glyphCacheUsage.Remove(existingNode);
                glyphCacheUsage.AddFirst(existingNode);
                return;
            }

            LinkedListNode<GlyphCacheKey> node = glyphCacheUsage.AddFirst(key);
            glyphCacheNodes[key] = node;
        }

        /// <summary>
        /// 글리프 캐시 항목 수가 <see cref="MaxCachedGlyphEntries"/>를 초과하면
        /// 아직 아틀라스에 패킹되지 않은 항목부터 LRU 순서대로 제거합니다.
        /// 아틀라스에 패킹된 글리프는 evict하지 않습니다.
        /// </summary>
        private void TrimGlyphCache()
        {
            if (glyphCache.Count <= MaxCachedGlyphEntries)
            {
                return;
            }

            // 아틀라스에 이미 패킹된 글리프는 evict하지 않는다.
            // evict 해도 아틀라스 공간은 회수되지 않고, 재요청 시 새로 패킹하면 공간만 낭비된다.
            // 패킹되지 않은 글리프(공백 문자 등)만 LRU 순서대로 제거한다.
            LinkedListNode<GlyphCacheKey> node = glyphCacheUsage.Last;
            while (node != null && glyphCache.Count > MaxCachedGlyphEntries)
            {
                LinkedListNode<GlyphCacheKey> prev = node.Previous;
                GlyphCacheKey key = node.Value;
                if (glyphCache.TryGetValue(key, out CachedGlyphBitmap cachedGlyph) && (cachedGlyph == null || !cachedGlyph.IsPacked))
                {
                    glyphCacheUsage.Remove(node);
                    glyphCacheNodes.Remove(key);
                    cachedGlyph?.Dispose();
                    glyphCache.Remove(key);
                }
                node = prev;
            }
        }

        /// <summary>
        /// 픽셀 배열로부터 <c>B8G8R8A8_UNorm</c> 형식의 GPU 텍스처와 셰이더 리소스 뷰를 생성합니다.
        /// </summary>
        /// <param name="context">텍스처 데이터 업로드에 사용할 컨텍스트입니다.</param>
        /// <param name="pixels">BGRA 픽셀 데이터 배열입니다.</param>
        /// <param name="width">텍스처 너비(픽셀)입니다.</param>
        /// <param name="height">텍스처 높이(픽셀)입니다.</param>
        /// <returns>생성된 텍스처 캐시 항목입니다. 입력이 유효하지 않으면 <c>null</c>입니다.</returns>
        private CachedTexture CreateTexture(ID3D11DeviceContext context, int[] pixels, int width, int height)
        {
            if (pixels == null || width <= 0 || height <= 0)
            {
                return null;
            }

            ID3D11Texture2D texture = device.CreateTexture2D(
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
            ID3D11ShaderResourceView view = device.CreateShaderResourceView(texture, null);
            context.UpdateSubresource(pixels, texture, 0, width * 4, width * height * 4, null);
            return new CachedTexture(texture, view, width, height);
        }

        /// <summary>
        /// GDI+ <see cref="Image"/>에서 BGRA 픽셀 배열을 추출합니다.
        /// 소스 이미지가 <c>Format32bppArgb</c>가 아니면 임시 비트맵으로 변환합니다.
        /// </summary>
        /// <param name="image">픽셀을 추출할 GDI+ 이미지입니다.</param>
        /// <param name="width">추출된 이미지의 너비(픽셀)입니다.</param>
        /// <param name="height">추출된 이미지의 높이(픽셀)입니다.</param>
        /// <returns>BGRA 픽셀 데이터 배열입니다. 이미지가 <c>null</c>이면 <c>null</c>입니다.</returns>
        private static int[] ExtractImagePixels(Image image, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (image == null)
            {
                return null;
            }

            Bitmap converted = null;
            try
            {
                Bitmap sourceBitmap = image as Bitmap;
                if (sourceBitmap != null && sourceBitmap.PixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppArgb)
                {
                    converted = sourceBitmap;
                }
                else
                {
                    converted = new Bitmap(image.Width, image.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(converted))
                    {
                        g.DrawImage(image, 0, 0, image.Width, image.Height);
                    }
                }

                Rectangle rect = new Rectangle(0, 0, converted.Width, converted.Height);
                BitmapData data = converted.LockBits(rect, ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try
                {
                    int[] pixels = new int[converted.Width * converted.Height];
                    Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                    width = converted.Width;
                    height = converted.Height;

                    // 오버레이 블렌드 스테이트가 premultiplied alpha(SourceBlend=One)이므로
                    // straight-alpha 픽셀의 RGB에 알파를 미리 곱해준다. 이렇게 하지 않으면
                    // 투명 영역(α=0)의 RGB가 그대로 더해져 PNG 배경이 비쳐 보인다.
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        int p = pixels[i];
                        int a = (p >> 24) & 0xFF;
                        if (a == 255)
                        {
                            continue;
                        }

                        if (a == 0)
                        {
                            pixels[i] = 0;
                            continue;
                        }

                        int r = (p >> 16) & 0xFF;
                        int g = (p >> 8) & 0xFF;
                        int b = p & 0xFF;
                        r = (r * a + 127) / 255;
                        g = (g * a + 127) / 255;
                        b = (b * a + 127) / 255;
                        pixels[i] = (a << 24) | (r << 16) | (g << 8) | b;
                    }

                    return pixels;
                }
                finally
                {
                    converted.UnlockBits(data);
                }
            }
            finally
            {
                if (converted != null && !ReferenceEquals(converted, image))
                {
                    converted.Dispose();
                }
            }
        }

        /// <summary>
        /// DirectWrite로 문자의 레이아웃 메트릭을 측정하여 <see cref="CachedGlyphBitmap"/> 메타데이터를 생성합니다.
        /// 공백 문자는 너비/높이 0으로 생성하고 어드밴스 너비만 설정합니다.
        /// </summary>
        /// <param name="ch">메타데이터를 생성할 문자입니다.</param>
        /// <param name="size">폰트 크기(포인트)입니다.</param>
        /// <returns>생성된 글리프 비트맵 메타데이터입니다.</returns>
        private CachedGlyphBitmap CreateGlyphBitmap(char ch, float size)
        {
            int sizeKey = GetSizeKey(size);
            float lineHeight = GetLineHeight(size);
            int advanceWidth = GetWhitespaceAdvance(ch, sizeKey);
            if (char.IsWhiteSpace(ch))
            {
                return new CachedGlyphBitmap(ch, size, 0, System.Math.Max(1, (int)System.Math.Ceiling(lineHeight)), advanceWidth);
            }

            string glyphText = ch.ToString();
            IDWriteTextFormat textFormat = GetOrCreateTextFormat(sizeKey);
            using (IDWriteTextLayout textLayout = dwriteFactory.CreateTextLayout(glyphText, textFormat, GetActualSize(size) * 4f, lineHeight * 2f))
            {
                TextMetrics metrics = textLayout.Metrics;
                OverhangMetrics overhang = textLayout.OverhangMetrics;
                advanceWidth = System.Math.Max(1, (int)System.Math.Ceiling(metrics.WidthIncludingTrailingWhitespace));
                int width = System.Math.Max(1, (int)System.Math.Ceiling(metrics.WidthIncludingTrailingWhitespace + System.Math.Abs(overhang.Left) + System.Math.Abs(overhang.Right) + 2f));
                int height = System.Math.Max(1, (int)System.Math.Ceiling(System.Math.Max(metrics.Height, lineHeight) + System.Math.Abs(overhang.Top) + System.Math.Abs(overhang.Bottom) + 2f));
                return new CachedGlyphBitmap(
                    ch,
                    size,
                    width,
                    height,
                    advanceWidth,
                    1f - overhang.Left,
                    1f - overhang.Top);
            }
        }

        /// <summary>
        /// 지정된 폰트 크기에 대한 줄 높이를 반환합니다.
        /// 캐시에 없으면 DirectWrite 레이아웃으로 측정하고 캐시합니다.
        /// </summary>
        /// <param name="size">폰트 크기(포인트)입니다.</param>
        /// <returns>줄 높이(픽셀)입니다.</returns>
        private float GetLineHeight(float size)
        {
            int sizeKey = GetSizeKey(size);
            if (lineHeightCache.TryGetValue(sizeKey, out float cachedHeight))
            {
                return cachedHeight;
            }

            IDWriteTextFormat textFormat = GetOrCreateTextFormat(sizeKey);
            using (IDWriteTextLayout textLayout = dwriteFactory.CreateTextLayout("Hg", textFormat, GetActualSize(size) * 8f, GetActualSize(size) * 4f))
            {
                float lineHeight = System.Math.Max(1f, (float)System.Math.Ceiling(textLayout.Metrics.Height + 2f));
                lineHeightCache[sizeKey] = lineHeight;
                return lineHeight;
            }
        }

        /// <summary>
        /// 지정된 크기 키에 해당하는 DirectWrite 텍스트 형식을 캐시에서 조회하거나, 없으면 새로 생성합니다.
        /// </summary>
        /// <param name="sizeKey">크기를 정수화한 키입니다(<see cref="GetSizeKey"/> 참조).</param>
        /// <returns>캐시된 또는 새로 생성된 <see cref="IDWriteTextFormat"/>입니다.</returns>
        private IDWriteTextFormat GetOrCreateTextFormat(int sizeKey)
        {
            if (textFormatCache.TryGetValue(sizeKey, out IDWriteTextFormat cachedTextFormat))
            {
                return cachedTextFormat;
            }

            float spec = sizeKey / 100f;
            bool bold = IsBoldSpec(spec);
            IDWriteTextFormat textFormat = dwriteFactory.CreateTextFormat(
                bold ? boldFamilyName : regularFamilyName,
                privateFontCollection,
                bold || privateFontCollection == null ? FontWeight.Bold : FontWeight.Normal,
                Vortice.DirectWrite.FontStyle.Normal,
                FontStretch.Normal,
                GetActualSize(spec),
                "ko-KR");
            textFormatCache[sizeKey] = textFormat;
            return textFormat;
        }

        /// <summary>
        /// 글리프 아틀라스 페이지의 GPU 텍스처와 Direct2D 렌더 타깃을 생성합니다.
        /// 이미 텍스처가 있는 경우에는 아무 동작도 하지 않습니다.
        /// </summary>
        /// <param name="atlasPage">텍스처를 생성할 아틀라스 페이지입니다.</param>
        private void CreateGlyphAtlasPageTexture(GlyphAtlasPage atlasPage)
        {
            if (atlasPage == null || atlasPage.Texture != null)
            {
                return;
            }

            ID3D11Texture2D texture = device.CreateTexture2D(
                new Texture2DDescription(
                    Format.B8G8R8A8_UNorm,
                    atlasPage.Width,
                    atlasPage.Height,
                    1,
                    1,
                    BindFlags.ShaderResource | BindFlags.RenderTarget,
                    ResourceUsage.Default,
                    CpuAccessFlags.None,
                    1,
                    0,
                    ResourceOptionFlags.None),
                null);
            ID3D11ShaderResourceView view = device.CreateShaderResourceView(texture, null);
            IDXGISurface surface = texture.QueryInterface<IDXGISurface>();
            ID2D1RenderTarget renderTarget = d2dFactory.CreateDxgiSurfaceRenderTarget(
                surface,
                new RenderTargetProperties(
                    RenderTargetType.Default,
                    new Vortice.DCommon.PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                    96f,
                    96f,
                    RenderTargetUsage.None,
                    Vortice.Direct2D1.FeatureLevel.Default));
            ID2D1SolidColorBrush brush = renderTarget.CreateSolidColorBrush(new Color4(1f, 1f, 1f, 1f));
            if (pixelGridSize > 0)
            {
                // 픽셀 글꼴은 안티앨리어싱 없이 그려야 가장자리가 번지지 않는다.
                renderTarget.TextAntialiasMode = Vortice.Direct2D1.TextAntialiasMode.Aliased;
            }

            renderTarget.BeginDraw();
            renderTarget.Clear(new Color4(0f, 0f, 0f, 0f));
            renderTarget.EndDraw();

            atlasPage.Texture = new CachedTexture(texture, view, atlasPage.Width, atlasPage.Height);
            atlasPage.Surface = surface;
            atlasPage.RenderTarget = renderTarget;
            atlasPage.Brush = brush;
        }

        /// <summary>
        /// Direct2D 렌더 타깃을 이용하여 아틀라스 페이지의 지정된 위치에 글리프를 그립니다.
        /// DrawOffsetX/Y를 적용하여 실제 문자 픽셀이 슬롯 내에 정확히 위치하도록 합니다.
        /// </summary>
        /// <param name="atlasPage">글리프를 그릴 대상 아틀라스 페이지입니다.</param>
        /// <param name="glyph">그릴 글리프 메타데이터입니다.</param>
        /// <param name="dstX">아틀라스 페이지 내 대상 X 좌표입니다.</param>
        /// <param name="dstY">아틀라스 페이지 내 대상 Y 좌표입니다.</param>
        private void DrawGlyphIntoAtlas(GlyphAtlasPage atlasPage, CachedGlyphBitmap glyph, int dstX, int dstY)
        {
            if (atlasPage == null || glyph == null || glyph.Width <= 0 || glyph.Height <= 0)
            {
                return;
            }

            CreateGlyphAtlasPageTexture(atlasPage);
            if (atlasPage.RenderTarget == null || atlasPage.Brush == null)
            {
                return;
            }

            IDWriteTextFormat textFormat = GetOrCreateTextFormat(GetSizeKey(glyph.Size));
            RectangleF layoutRect = new RectangleF(dstX + glyph.DrawOffsetX, dstY + glyph.DrawOffsetY, glyph.Width, glyph.Height);
            atlasPage.RenderTarget.BeginDraw();
            atlasPage.RenderTarget.DrawText(glyph.GlyphChar.ToString(), textFormat, layoutRect, atlasPage.Brush, DrawTextOptions.None, MeasuringMode.Natural);
            atlasPage.RenderTarget.EndDraw();
        }

        /// <summary>
        /// 부동소수점 폰트 크기를 캐시 키로 사용할 정수로 변환합니다.
        /// </summary>
        /// <param name="size">폰트 크기(포인트)입니다.</param>
        /// <returns>100을 곱하고 반올림한 정수 키입니다. 0 이하이면 1000을 반환합니다.</returns>
        private static int GetSizeKey(float size)
        {
            int key = (int)System.Math.Round(size * 100f);
            return key <= 0 ? 1000 : key;
        }

        /// <summary>
        /// 요청 크기와 굵기를 글리프 캐시용 크기 값으로 바꾼다.
        /// 픽셀 글꼴이면 크기를 격자 배수로 맞추고, 굵은 글자는 <see cref="BoldSizeOffset"/>를 더해 구분한다.
        /// </summary>
        private float ResolveFontSpec(float size, bool bold)
        {
            if (size <= 0f)
            {
                size = 10f;
            }

            if (pixelGridSize > 0)
            {
                int steps = System.Math.Max(1, (int)System.Math.Round(size / pixelGridSize));
                size = steps * pixelGridSize;
            }

            return bold ? size + BoldSizeOffset : size;
        }

        /// <summary>글리프 캐시용 크기 값에서 실제 글꼴 크기를 꺼낸다.</summary>
        private static float GetActualSize(float spec)
        {
            return spec >= BoldSizeOffset ? spec - BoldSizeOffset : spec;
        }

        private static bool IsBoldSpec(float spec)
        {
            return spec >= BoldSizeOffset;
        }

        /// <summary>
        /// <see cref="OverlayFontSettings"/>의 글꼴 파일로 전용 글꼴 모음을 만든다.
        /// 실패하면 설정한 이름의 시스템 글꼴, 그것도 없으면 맑은 고딕을 쓰고 픽셀 격자 맞춤을 끈다.
        /// </summary>
        private void LoadOverlayFonts()
        {
            string family = OverlayFontSettings.FamilyName;
            regularFamilyName = family;
            boldFamilyName = family;
            pixelGridSize = 0;

            if (OverlayFontSettings.FontFiles.Count == 0)
            {
                return;
            }

            try
            {
                IDWriteFactory5 factory5 = dwriteFactory.QueryInterface<IDWriteFactory5>();
                IDWriteFontSetBuilder1 builder = factory5.CreateFontSetBuilder();
                foreach (string file in OverlayFontSettings.FontFiles)
                {
                    using (IDWriteFontFile fontFile = factory5.CreateFontFileReference(file, null))
                    {
                        builder.AddFontFile(fontFile);
                    }
                }

                IDWriteFontSet fontSet = builder.CreateFontSet();
                IDWriteFontCollection1 collection = factory5.CreateFontCollectionFromFontSet(fontSet);
                fontSet.Dispose();
                builder.Dispose();
                factory5.Dispose();

                string regular = FindFamily(collection, family, preferBold: false);
                if (regular == null)
                {
                    collection.Dispose();
                    return;
                }

                privateFontCollection = collection;
                regularFamilyName = regular;
                boldFamilyName = FindFamily(collection, family, preferBold: true) ?? regular;
                pixelGridSize = OverlayFontSettings.PixelGridSize;
            }
            catch (SharpGen.Runtime.SharpGenException)
            {
                privateFontCollection = null;
                regularFamilyName = family;
                boldFamilyName = family;
                pixelGridSize = 0;
            }
        }

        /// <summary>
        /// 글꼴 모음에서 패밀리 이름을 찾는다. 정확히 같은 이름이 있으면 그것을, 없으면 이름으로 시작하는 패밀리를 고른다.
        /// preferBold면 이름에 Bold가 들어간 패밀리를 먼저 찾는다(굵은 면이 별도 패밀리로 등록된 글꼴 대응).
        /// </summary>
        private static string FindFamily(IDWriteFontCollection collection, string family, bool preferBold)
        {
            string exact = null;
            string prefixed = null;
            string bold = null;
            int count = (int)collection.FontFamilyCount;
            for (int i = 0; i < count; i++)
            {
                using (IDWriteFontFamily fontFamily = collection.GetFontFamily(i))
                using (IDWriteLocalizedStrings names = fontFamily.FamilyNames)
                {
                    for (int n = 0; n < (int)names.Count; n++)
                    {
                        string name = names.GetString(n);
                        if (string.Equals(name, family, StringComparison.OrdinalIgnoreCase))
                        {
                            exact ??= name;
                        }
                        else if (name.StartsWith(family, StringComparison.OrdinalIgnoreCase))
                        {
                            if (name.IndexOf("Bold", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                bold ??= name;
                            }
                            else
                            {
                                prefixed ??= name;
                            }
                        }
                    }
                }
            }

            if (preferBold)
            {
                return bold;
            }

            return exact ?? prefixed;
        }

        /// <summary>
        /// 공백 문자의 어드밴스 너비를 반환합니다. 탭 문자는 공백 4칸에 해당하는 너비를 반환합니다.
        /// </summary>
        /// <param name="ch">공백 여부를 판단할 문자입니다.</param>
        /// <param name="sizeKey">폰트 크기 키입니다.</param>
        /// <returns>공백 어드밴스 너비(픽셀)입니다.</returns>
        private static int GetWhitespaceAdvance(char ch, int sizeKey)
        {
            float size = GetActualSize(sizeKey / 100f);
            if (ch == '\t')
            {
                return System.Math.Max(1, (int)System.Math.Ceiling(size * 0.42f) * 4);
            }

            return System.Math.Max(1, (int)System.Math.Ceiling(size * 0.42f));
        }

        /// <summary>
        /// 2D 회전 변환의 X 성분을 계산합니다.
        /// </summary>
        /// <param name="x">원본 X 오프셋입니다.</param>
        /// <param name="y">원본 Y 오프셋입니다.</param>
        /// <param name="cos">회전 각도의 코사인 값입니다.</param>
        /// <param name="sin">회전 각도의 사인 값입니다.</param>
        /// <returns>회전 후 X 오프셋입니다.</returns>
        private static float RotateX(float x, float y, float cos, float sin)
        {
            return (cos * x) - (sin * y);
        }

        /// <summary>
        /// 2D 회전 변환의 Y 성분을 계산합니다.
        /// </summary>
        /// <param name="x">원본 X 오프셋입니다.</param>
        /// <param name="y">원본 Y 오프셋입니다.</param>
        /// <param name="cos">회전 각도의 코사인 값입니다.</param>
        /// <param name="sin">회전 각도의 사인 값입니다.</param>
        /// <returns>회전 후 Y 오프셋입니다.</returns>
        private static float RotateY(float x, float y, float cos, float sin)
        {
            return (sin * x) + (cos * y);
        }

        /// <summary>
        /// 스크린 X 좌표를 NDC(Normalized Device Coordinates) X 좌표로 변환합니다.
        /// </summary>
        /// <param name="x">스크린 X 좌표(픽셀)입니다.</param>
        /// <param name="width">렌더 표면 너비(픽셀)입니다.</param>
        /// <returns>NDC X 좌표 [-1, 1] 범위입니다.</returns>
        private static float ToClipX(float x, int width)
        {
            return ((x / width) * 2f) - 1f;
        }

        /// <summary>
        /// 스크린 Y 좌표를 NDC(Normalized Device Coordinates) Y 좌표로 변환합니다.
        /// Direct3D는 Y축이 위쪽이 양수이므로 좌표를 반전합니다.
        /// </summary>
        /// <param name="y">스크린 Y 좌표(픽셀)입니다.</param>
        /// <param name="height">렌더 표면 높이(픽셀)입니다.</param>
        /// <returns>NDC Y 좌표 [-1, 1] 범위입니다.</returns>
        private static float ToClipY(float y, int height)
        {
            return 1f - ((y / height) * 2f);
        }

        /// <summary>
        /// D3D11 텍스처와 셰이더 리소스 뷰를 함께 보관하는 캐시 항목입니다.
        /// </summary>
        private sealed class CachedTexture : IDisposable
        {
            /// <summary>
            /// 지정된 텍스처, 뷰, 크기로 캐시 항목을 초기화합니다.
            /// </summary>
            /// <param name="texture">GPU 텍스처 객체입니다.</param>
            /// <param name="view">셰이더에서 텍스처에 접근하기 위한 셰이더 리소스 뷰입니다.</param>
            /// <param name="width">텍스처 너비(픽셀)입니다.</param>
            /// <param name="height">텍스처 높이(픽셀)입니다.</param>
            public CachedTexture(ID3D11Texture2D texture, ID3D11ShaderResourceView view, int width, int height)
            {
                Texture = texture;
                View = view;
                Width = width;
                Height = height;
            }

            /// <summary>GPU 텍스처 객체입니다.</summary>
            public ID3D11Texture2D Texture { get; }

            /// <summary>픽셀 셰이더에서 이 텍스처를 샘플링하기 위한 셰이더 리소스 뷰입니다.</summary>
            public ID3D11ShaderResourceView View { get; }

            /// <summary>텍스처 너비(픽셀)입니다.</summary>
            public int Width { get; }

            /// <summary>텍스처 높이(픽셀)입니다.</summary>
            public int Height { get; }

            /// <summary>이 캐시 항목이 보유한 GPU 자원을 해제합니다.</summary>
            public void Dispose()
            {
                View?.Dispose();
                Texture?.Dispose();
            }
        }

        /// <summary>
        /// DirectWrite로 측정된 글리프의 크기, 어드밴스, 드로우 오프셋 및 아틀라스 패킹 정보를 담는 캐시 항목입니다.
        /// 실제 픽셀 데이터는 아틀라스 페이지에 보관되며, 이 클래스는 메타데이터만 유지합니다.
        /// </summary>
        private sealed class CachedGlyphBitmap : IDisposable
        {
            /// <summary>
            /// 지정된 메트릭으로 글리프 비트맵 메타데이터를 초기화합니다.
            /// </summary>
            /// <param name="glyphChar">이 메타데이터가 나타내는 문자입니다.</param>
            /// <param name="size">폰트 크기(포인트)입니다.</param>
            /// <param name="width">글리프 슬롯 너비(픽셀)입니다.</param>
            /// <param name="height">글리프 슬롯 높이(픽셀)입니다.</param>
            /// <param name="advanceWidth">다음 글리프까지 커서를 이동할 너비(픽셀)입니다.</param>
            /// <param name="drawOffsetX">슬롯 내 실제 픽셀 시작 X 오프셋입니다.</param>
            /// <param name="drawOffsetY">슬롯 내 실제 픽셀 시작 Y 오프셋입니다. 기준선 정렬에 사용됩니다.</param>
            public CachedGlyphBitmap(char glyphChar, float size, int width, int height, int advanceWidth, float drawOffsetX = 0f, float drawOffsetY = 0f)
            {
                GlyphChar = glyphChar;
                Size = size;
                Width = width;
                Height = height;
                AdvanceWidth = advanceWidth;
                DrawOffsetX = drawOffsetX;
                DrawOffsetY = drawOffsetY;
            }

            /// <summary>이 메타데이터가 나타내는 문자입니다.</summary>
            public char GlyphChar { get; }

            /// <summary>폰트 크기(포인트)입니다.</summary>
            public float Size { get; }

            /// <summary>아틀라스 슬롯 너비(픽셀)입니다.</summary>
            public int Width { get; }

            /// <summary>아틀라스 슬롯 높이(픽셀)입니다.</summary>
            public int Height { get; }

            /// <summary>다음 글리프까지 커서를 이동할 너비(픽셀)입니다.</summary>
            public int AdvanceWidth { get; }

            /// <summary>슬롯 내 실제 문자 픽셀 시작 X 오프셋입니다.</summary>
            public float DrawOffsetX { get; }

            /// <summary>슬롯 내 실제 문자 픽셀 시작 Y 오프셋입니다. 기준선 정렬에 사용됩니다.</summary>
            public float DrawOffsetY { get; }

            /// <summary>이 글리프가 속하는 아틀라스 페이지의 크기 키입니다.</summary>
            public int AtlasPageKey { get; set; }

            /// <summary>이 글리프가 패킹된 아틀라스 페이지입니다. 패킹 전에는 <c>null</c>입니다.</summary>
            public GlyphAtlasPage AtlasPage { get; set; }

            /// <summary>아틀라스 텍스처 내에서 이 글리프의 UV 영역입니다.</summary>
            public RectangleF UvRect { get; set; }

            /// <summary>이 글리프가 아틀라스에 패킹되었는지 여부입니다.</summary>
            public bool IsPacked { get; set; }

            /// <summary>
            /// 글리프 픽셀 데이터는 CPU에 보관하지 않으므로 별도 해제 작업이 없습니다.
            /// </summary>
            public void Dispose()
            {
                // 글리프 픽셀은 더 이상 CPU 쪽에 보관하지 않는다.
            }
        }

        /// <summary>
        /// 여러 글리프를 하나의 텍스처에 패킹하는 글리프 아틀라스 페이지입니다.
        /// Direct2D 렌더 타깃을 통해 글리프를 텍스처에 직접 그립니다.
        /// </summary>
        private sealed class GlyphAtlasPage : IDisposable
        {
            /// <summary>
            /// 지정된 크기로 아틀라스 페이지를 초기화합니다.
            /// 커서는 패딩을 고려하여 (1, 1)에서 시작합니다.
            /// </summary>
            /// <param name="width">아틀라스 페이지 너비(픽셀)입니다.</param>
            /// <param name="height">아틀라스 페이지 높이(픽셀)입니다.</param>
            public GlyphAtlasPage(int width, int height)
            {
                Width = width;
                Height = height;
                CursorX = 1;
                CursorY = 1;
                RowHeight = 0;
            }

            /// <summary>아틀라스 페이지 너비(픽셀)입니다.</summary>
            public int Width { get; }

            /// <summary>아틀라스 페이지 높이(픽셀)입니다.</summary>
            public int Height { get; }

            /// <summary>이 아틀라스 페이지의 GPU 텍스처입니다. 첫 글리프가 패킹될 때 생성됩니다.</summary>
            public CachedTexture Texture { get; set; }

            /// <summary>Direct2D 렌더 타깃 생성에 사용하는 DXGI 표면입니다.</summary>
            public IDXGISurface Surface { get; set; }

            /// <summary>아틀라스 텍스처에 글리프를 그리기 위한 Direct2D 렌더 타깃입니다.</summary>
            public ID2D1RenderTarget RenderTarget { get; set; }

            /// <summary>글리프를 흰색으로 그리기 위한 Direct2D 단색 브러시입니다.</summary>
            public ID2D1SolidColorBrush Brush { get; set; }

            /// <summary>다음 글리프를 패킹할 현재 행의 X 커서 위치입니다.</summary>
            public int CursorX { get; set; }

            /// <summary>다음 글리프를 패킹할 현재 행의 Y 커서 위치입니다.</summary>
            public int CursorY { get; set; }

            /// <summary>현재 행에서 가장 높은 글리프의 높이입니다. 다음 행 시작 위치 계산에 사용됩니다.</summary>
            public int RowHeight { get; set; }

            /// <summary>이 아틀라스 페이지가 보유한 모든 GPU 및 Direct2D 자원을 해제합니다.</summary>
            public void Dispose()
            {
                Brush?.Dispose();
                Brush = null;
                RenderTarget?.Dispose();
                RenderTarget = null;
                Surface?.Dispose();
                Surface = null;
                Texture?.Dispose();
                Texture = null;
            }
        }

        /// <summary>
        /// 글리프 캐시의 딕셔너리 키로 사용되는 불변 값 타입입니다.
        /// 문자와 폰트 크기(100배 정수)를 조합하여 유일한 키를 만듭니다.
        /// </summary>
        private readonly struct GlyphCacheKey : IEquatable<GlyphCacheKey>
        {
            /// <summary>캐싱 대상 문자입니다.</summary>
            private readonly char glyph;

            /// <summary>폰트 크기를 100배 반올림한 정수 키입니다.</summary>
            private readonly int sizeKey;

            /// <summary>
            /// 문자와 폰트 크기로 글리프 캐시 키를 초기화합니다.
            /// </summary>
            /// <param name="glyph">캐싱 대상 문자입니다.</param>
            /// <param name="size">폰트 크기(포인트)입니다.</param>
            public GlyphCacheKey(char glyph, float size)
            {
                this.glyph = glyph;
                sizeKey = (int)System.Math.Round(size * 100f);
            }

            /// <summary>폰트 크기를 100배 반올림한 정수 키입니다. 아틀라스 페이지 그룹 식별에 사용됩니다.</summary>
            public int SizeKey => sizeKey;

            /// <summary>다른 <see cref="GlyphCacheKey"/>와 동등성을 비교합니다.</summary>
            /// <param name="other">비교할 대상 키입니다.</param>
            /// <returns>문자와 크기 키가 모두 같으면 <c>true</c>입니다.</returns>
            public bool Equals(GlyphCacheKey other)
            {
                return sizeKey == other.sizeKey && glyph == other.glyph;
            }

            /// <inheritdoc/>
            public override bool Equals(object obj)
            {
                return obj is GlyphCacheKey other && Equals(other);
            }

            /// <inheritdoc/>
            public override int GetHashCode()
            {
                unchecked
                {
                    return (glyph.GetHashCode() * 397) ^ sizeKey;
                }
            }
        }
    }
}
