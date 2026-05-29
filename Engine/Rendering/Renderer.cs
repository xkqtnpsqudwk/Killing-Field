using My2DEngine.Engine.Rendering.Abstractions;
using System.Drawing;

namespace My2DEngine.Engine.Rendering
{
    /// <summary>
    /// 게임 코드가 렌더 백엔드(GDI, D3D11 등)의 구체적인 구현을 몰라도
    /// 그리기 명령을 내릴 수 있도록 해주는 얇은 퍼사드(Facade) 클래스.
    /// 화면 오프셋, 프레젠테이션 스케일 적용을 담당하며
    /// 실제 드로우 호출은 내부 <see cref="IRenderBackend"/> 구현에 위임한다.
    /// </summary>
    public class Renderer
    {
        /// <summary>실제 그리기를 수행하는 렌더 백엔드 구현체.</summary>
        private readonly IRenderBackend backend;

        /// <summary>화면 오프셋 X 값 (프레젠테이션 스케일 적용 전 원시값).</summary>
        private float screenOffsetX;

        /// <summary>화면 오프셋 Y 값 (프레젠테이션 스케일 적용 전 원시값).</summary>
        private float screenOffsetY;

        /// <summary>내부 렌더 해상도와 실제 창 너비의 비율. 기본값 1.</summary>
        private float presentationScaleX = 1f;

        /// <summary>내부 렌더 해상도와 실제 창 높이의 비율. 기본값 1.</summary>
        private float presentationScaleY = 1f;

        /// <summary>
        /// 지정된 렌더 백엔드로 <see cref="Renderer"/>를 초기화한다.
        /// </summary>
        /// <param name="backend">드로우 명령을 처리할 렌더 백엔드 구현체.</param>
        public Renderer(IRenderBackend backend)
        {
            this.backend = backend;
        }

        /// <summary>백엔드가 월드 렌더링을 지원하는지 여부.</summary>
        public bool SupportsWorldRendering => backend.SupportsWorldRendering;

        /// <summary>백엔드의 현재 상태 메시지 (초기화 오류, 활성 경로 등).</summary>
        public string BackendStatusMessage => backend.StatusMessage;

        /// <summary>프레젠테이션 스케일이 적용된 실제 화면 X 오프셋 픽셀값.</summary>
        public float ScreenOffsetX => screenOffsetX * presentationScaleX;

        /// <summary>프레젠테이션 스케일이 적용된 실제 화면 Y 오프셋 픽셀값.</summary>
        public float ScreenOffsetY => screenOffsetY * presentationScaleY;

        /// <summary>
        /// 층 전환 등 레벨 경계에서 호출한다.
        /// 백엔드의 일시적 VRAM 캐시(글리프 아틀라스 등)를 비운다.
        /// </summary>
        public void OnLevelTransition()
        {
            backend.OnLevelTransition();
        }

        /// <summary>
        /// 새 프레임을 시작하고, 화면을 지정한 색으로 초기화한다.
        /// </summary>
        /// <param name="clearColor">화면을 채울 배경색.</param>
        public void BeginFrame(Color clearColor)
        {
            backend.BeginFrame(clearColor);
        }

        /// <summary>
        /// 현재 프레임의 렌더링을 완료하고 화면에 출력(Present)한다.
        /// </summary>
        public void EndFrame()
        {
            backend.EndFrame();
        }

        /// <summary>
        /// 월드 렌더 명령을 백엔드에 전달하여 3D 월드를 그린다.
        /// 백엔드가 월드 렌더링을 지원하지 않으면 <c>false</c>를 반환한다.
        /// </summary>
        /// <param name="command">렌더링에 필요한 월드 데이터와 설정이 담긴 명령 구조체.</param>
        /// <returns>렌더링에 성공하면 <c>true</c>, 백엔드가 지원하지 않으면 <c>false</c>.</returns>
        public bool TryDrawWorld(RenderWorldCommand command)
        {
            if (!backend.SupportsWorldRendering)
            {
                return false;
            }

            return backend.DrawWorld(command);
        }

        /// <summary>
        /// 모든 드로우 호출에 공통으로 더해질 화면 오프셋을 설정한다.
        /// 주로 게임 뷰포트를 창 안의 특정 영역에 배치할 때 사용한다.
        /// </summary>
        /// <param name="x">X축 오프셋 (픽셀, 프레젠테이션 스케일 적용 전).</param>
        /// <param name="y">Y축 오프셋 (픽셀, 프레젠테이션 스케일 적용 전).</param>
        public void SetScreenOffset(float x, float y)
        {
            screenOffsetX = x;
            screenOffsetY = y;
        }

        /// <summary>
        /// 내부 렌더 해상도와 실제 창 크기의 비율을 설정한다.
        /// UI/오버레이 좌표가 내부 해상도 기준으로 지정되었을 때
        /// 실제 창 픽셀 좌표로 변환하는 데 사용된다.
        /// 0 이하의 값이 전달되면 해당 축은 1로 고정된다.
        /// </summary>
        /// <param name="scaleX">X축 스케일 (창 너비 / 내부 렌더 너비).</param>
        /// <param name="scaleY">Y축 스케일 (창 높이 / 내부 렌더 높이).</param>
        public void SetPresentationScale(float scaleX, float scaleY)
        {
            presentationScaleX = scaleX <= 0f ? 1f : scaleX;
            presentationScaleY = scaleY <= 0f ? 1f : scaleY;
        }

        /// <summary>
        /// 지정한 색으로 채워진 사각형을 그린다.
        /// 화면 오프셋, 프레젠테이션 스케일이 순서대로 적용된다.
        /// </summary>
        /// <param name="x">사각형 왼쪽 상단의 X 좌표 (내부 렌더 해상도 기준).</param>
        /// <param name="y">사각형 왼쪽 상단의 Y 좌표 (내부 렌더 해상도 기준).</param>
        /// <param name="w">사각형의 너비.</param>
        /// <param name="h">사각형의 높이.</param>
        /// <param name="color">사각형을 채울 색.</param>
        public void DrawRectangle(float x, float y, float w, float h, Color color)
        {
            float drawW = w;
            float drawH = h;

            // Renderer의 공통 좌표 규칙은 "게임 내부 좌표에 오프셋을 더한 뒤 최종 창 스케일을 적용"이다.
            // 이 순서를 유지해야 내부 640x360 기준 UI와 실제 창 좌표계 UI가 같은 API를 공유할 수 있다.
            x += screenOffsetX;
            y += screenOffsetY;
            x *= presentationScaleX;
            y *= presentationScaleY;
            drawW *= presentationScaleX;
            drawH *= presentationScaleY;

            backend.DrawRectangle(new RenderRectCommand
            {
                X = x,
                Y = y,
                Width = drawW,
                Height = drawH,
                Color = color
            });
        }

        /// <summary>
        /// 이미지를 지정한 크기로 늘려서 그린다.
        /// img가 null이면 아무것도 그리지 않는다.
        /// </summary>
        /// <param name="img">그릴 이미지. null이면 무시된다.</param>
        /// <param name="x">이미지 왼쪽 상단의 X 좌표 (내부 렌더 해상도 기준).</param>
        /// <param name="y">이미지 왼쪽 상단의 Y 좌표 (내부 렌더 해상도 기준).</param>
        /// <param name="w">출력할 너비.</param>
        /// <param name="h">출력할 높이.</param>
        public void DrawImage(Image img, float x, float y, float w, float h)
        {
            DrawImageRegion(img, x, y, w, h, 0f, 0f, 1f, 1f, Color.White);
        }

        /// <summary>
        /// 이미지를 색조/불투명도(tint)와 함께 그린다. 흰색 불투명이면 원본 그대로,
        /// 알파를 낮추면 페이드, 색을 주면 색조가 적용된다(히트마커 페이드 등).
        /// </summary>
        public void DrawImage(Image img, float x, float y, float w, float h, Color tint)
        {
            DrawImageRegion(img, x, y, w, h, 0f, 0f, 1f, 1f, tint);
        }

        /// <summary>
        /// 이미지의 일부 영역(UV 0~1)만 지정한 목적지에 늘려 그린다.
        /// 9-slice 등 부분 샘플링이 필요한 경우에 사용한다.
        /// </summary>
        public void DrawImageRegion(Image img, float x, float y, float w, float h, float u0, float v0, float u1, float v1)
        {
            DrawImageRegion(img, x, y, w, h, u0, v0, u1, v1, Color.White);
        }

        /// <summary>UV 영역과 tint를 함께 지정해 이미지를 그린다.</summary>
        public void DrawImageRegion(Image img, float x, float y, float w, float h, float u0, float v0, float u1, float v1, Color tint)
        {
            if (img == null)
            {
                return;
            }

            float drawW = w;
            float drawH = h;

            // 이미지도 사각형과 같은 좌표 변환 규칙을 따른다.
            // backend는 이미 최종 픽셀 좌표만 받으므로 여기서 모든 Form/Game 좌표계를 정리한다.
            x += screenOffsetX;
            y += screenOffsetY;
            x *= presentationScaleX;
            y *= presentationScaleY;
            drawW *= presentationScaleX;
            drawH *= presentationScaleY;
            backend.DrawImage(new RenderImageCommand
            {
                Image = img,
                X = x,
                Y = y,
                Width = drawW,
                Height = drawH,
                U0 = u0,
                V0 = v0,
                U1 = u1,
                V1 = v1,
                Tint = tint
            });
        }

        /// <summary>
        /// 9-slice 방식으로 이미지를 그린다. 네 모서리는 원본 크기를 유지하고
        /// 가장자리와 중앙만 늘어나, 패널·버튼 배경을 임의 크기로 왜곡 없이 표현한다.
        /// </summary>
        /// <param name="img">9-slice 소스 이미지.</param>
        /// <param name="x">목적지 왼쪽 상단 X (내부 렌더 해상도 기준).</param>
        /// <param name="y">목적지 왼쪽 상단 Y.</param>
        /// <param name="w">목적지 너비.</param>
        /// <param name="h">목적지 높이.</param>
        /// <param name="srcBorderPx">소스 이미지에서 모서리로 취급할 테두리 두께(픽셀).</param>
        /// <param name="dstBorder">목적지에서 모서리가 차지할 두께(내부 렌더 해상도 기준 픽셀).</param>
        public void DrawImageNineSlice(Image img, float x, float y, float w, float h, float srcBorderPx, float dstBorder)
        {
            DrawImageNineSlice(img, x, y, w, h, srcBorderPx, dstBorder, Color.White);
        }

        /// <summary>
        /// 9-slice 방식으로 이미지를 tint(색조/불투명도)와 함께 그린다.
        /// tint 알파를 낮추면 프레임 전체가 균일하게 페이드된다(메시지 패널 페이드아웃 등).
        /// </summary>
        public void DrawImageNineSlice(Image img, float x, float y, float w, float h, float srcBorderPx, float dstBorder, Color tint)
        {
            if (img == null)
            {
                return;
            }

            float iw = img.Width;
            float ih = img.Height;
            if (iw <= 0f || ih <= 0f)
            {
                return;
            }

            // 목적지가 양쪽 테두리 합보다 작으면 테두리를 비례 축소해 겹침을 막는다.
            float db = dstBorder;
            if (db * 2f > w) db = w * 0.5f;
            if (db * 2f > h) db = h * 0.5f;

            // 소스 UV 분할점
            float su = System.Math.Min(0.5f, srcBorderPx / iw);
            float sv = System.Math.Min(0.5f, srcBorderPx / ih);
            float[] u = { 0f, su, 1f - su, 1f };
            float[] v = { 0f, sv, 1f - sv, 1f };

            // 목적지 분할점
            float[] dx = { x, x + db, x + w - db, x + w };
            float[] dy = { y, y + db, y + h - db, y + h };

            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    float cellW = dx[col + 1] - dx[col];
                    float cellH = dy[row + 1] - dy[row];
                    if (cellW <= 0f || cellH <= 0f)
                    {
                        continue;
                    }

                    DrawImageRegion(img, dx[col], dy[row], cellW, cellH,
                        u[col], v[row], u[col + 1], v[row + 1], tint);
                }
            }
        }

        /// <summary>
        /// 지정한 위치에 텍스트를 왼쪽 정렬로 그린다.
        /// text가 null이거나 빈 문자열이면 아무것도 그리지 않는다.
        /// </summary>
        /// <param name="text">그릴 문자열.</param>
        /// <param name="x">텍스트 왼쪽 상단의 X 좌표 (내부 렌더 해상도 기준).</param>
        /// <param name="y">텍스트 왼쪽 상단의 Y 좌표 (내부 렌더 해상도 기준).</param>
        /// <param name="color">텍스트 색.</param>
        /// <param name="size">폰트 크기 (내부 렌더 해상도 기준 포인트).</param>
        public void DrawText(string text, float x, float y, Color color, float size)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            x = (x + screenOffsetX) * presentationScaleX;
            y = (y + screenOffsetY) * presentationScaleY;
            backend.DrawText(new RenderTextCommand
            {
                Text = text,
                X = x,
                Y = y,
                Color = color,
                // 폰트 크기는 세로 픽셀 밀도 기준으로만 스케일한다.
                // X/Y 스케일이 달라져도 글자 폭을 별도로 늘리지 않아 텍스트 왜곡을 피한다.
                Size = size * presentationScaleY
            });
        }

        /// <summary>
        /// 지정한 중심 좌표를 기준으로 텍스트를 가로/세로 중앙 정렬로 그린다.
        /// text가 null이거나 빈 문자열이면 아무것도 그리지 않는다.
        /// </summary>
        /// <param name="text">그릴 문자열.</param>
        /// <param name="centerX">텍스트 중심의 X 좌표 (내부 렌더 해상도 기준).</param>
        /// <param name="centerY">텍스트 중심의 Y 좌표 (내부 렌더 해상도 기준).</param>
        /// <param name="color">텍스트 색.</param>
        /// <param name="size">폰트 크기 (내부 렌더 해상도 기준 포인트).</param>
        public void DrawTextCentered(string text, float centerX, float centerY, Color color, float size)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            centerX = (centerX + screenOffsetX) * presentationScaleX;
            centerY = (centerY + screenOffsetY) * presentationScaleY;
            float scaledSize = size * presentationScaleY;

            // 중앙 정렬은 backend의 실제 텍스트 측정값에 의존한다.
            // DirectWrite/GDI 계열 백엔드마다 글리프 폭이 다를 수 있으므로 추정식으로 맞추지 않는다.
            SizeF measure = backend.MeasureText(text, scaledSize);
            float x = centerX - (measure.Width * 0.5f);
            float y = centerY - (measure.Height * 0.5f);
            backend.DrawText(new RenderTextCommand
            {
                Text = text,
                X = x,
                Y = y,
                Color = color,
                Size = scaledSize
            });
        }

        /// <summary>
        /// 지정한 중심 좌표를 기준으로 텍스트를 가로/세로 중앙 정렬로 그리되,
        /// 오른쪽 아래 2픽셀 offset의 반투명 검은 그림자를 먼저 그린 뒤 본 텍스트를 그린다.
        /// text가 null이거나 빈 문자열이면 아무것도 그리지 않는다.
        /// </summary>
        /// <param name="text">그릴 문자열.</param>
        /// <param name="centerX">텍스트 중심의 X 좌표 (내부 렌더 해상도 기준).</param>
        /// <param name="centerY">텍스트 중심의 Y 좌표 (내부 렌더 해상도 기준).</param>
        /// <param name="color">본 텍스트 색. 그림자는 반투명 검은색으로 고정.</param>
        /// <param name="size">폰트 크기 (내부 렌더 해상도 기준 포인트).</param>
        public void DrawTextCenteredShadow(string text, float centerX, float centerY, Color color, float size)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            centerX = (centerX + screenOffsetX) * presentationScaleX;
            centerY = (centerY + screenOffsetY) * presentationScaleY;
            float scaledSize = size * presentationScaleY;
            SizeF measure = backend.MeasureText(text, scaledSize);
            float x = centerX - (measure.Width * 0.5f);
            float y = centerY - (measure.Height * 0.5f);

            // 그림자는 별도 DrawText 명령 두 번으로 만든다.
            // backend에 텍스트 스타일 개념을 추가하지 않고도 모든 렌더 경로에서 동일한 효과를 낼 수 있다.
            backend.DrawText(new RenderTextCommand
            {
                Text = text,
                X = x + 2,
                Y = y + 2,
                Color = Color.FromArgb(180, 0, 0, 0),
                Size = scaledSize
            });
            backend.DrawText(new RenderTextCommand
            {
                Text = text,
                X = x,
                Y = y,
                Color = color,
                Size = scaledSize
            });
        }
    }
}
