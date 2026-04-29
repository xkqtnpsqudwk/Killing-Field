namespace My2DEngine.Engine.Rendering.Backends.D3D11
{
    /// <summary>
    /// 디버그 HUD(F3)에서 DX11 렌더 경로의 병목을 빠르게 파악할 수 있도록
    /// 지수 평활된 핵심 타이밍 지표와 상태 문자열을 한곳에 모은 스냅샷이다.
    /// D3D11RenderBackend.CaptureDiagnostics()가 매 프레임 이 객체를 새로 만들어 반환한다.
    /// </summary>
    public sealed class D3D11RenderDiagnostics
    {
        /// <summary>현재 활성 렌더 경로 설명 문자열. 예: "Using Direct3D11 with GPU world, GPU sprites, and GPU overlay."</summary>
        public string PathDescription { get; set; }

        /// <summary>CPU→GPU 텍스처 업로드에 소요된 평활화 시간(밀리초).</summary>
        public float CpuUploadMs { get; set; }

        /// <summary>레이캐스트 월드(벽·바닥·천장) 렌더 패스 소요 시간(밀리초).</summary>
        public float WorldMs { get; set; }

        /// <summary>스프라이트 렌더 패스 총 소요 시간(밀리초). 적·투사체·픽업 패스 합계.</summary>
        public float SpriteMs { get; set; }

        /// <summary>UI 오버레이(사각형·이미지·텍스트) 렌더 패스 소요 시간(밀리초).</summary>
        public float OverlayMs { get; set; }

        /// <summary>스왑체인 Present 호출 소요 시간(밀리초). VSync 대기 시간이 포함된다.</summary>
        public float PresentMs { get; set; }

        /// <summary>이번 프레임 월드 렌더 타깃 너비(픽셀).</summary>
        public int WorldTargetWidth { get; set; }

        /// <summary>이번 프레임 월드 렌더 타깃 높이(픽셀).</summary>
        public int WorldTargetHeight { get; set; }

        /// <summary>이번 프레임에 처리한 레이 컬럼 수.</summary>
        public int ColumnCount { get; set; }

        /// <summary>이번 프레임에 그린 스프라이트 총 수(적 패스 + 기타 패스).</summary>
        public int SpriteCount { get; set; }
    }
}
