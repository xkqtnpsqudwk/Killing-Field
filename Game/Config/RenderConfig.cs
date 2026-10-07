using System.Drawing;

namespace My2DEngine.Game.Config
{
    /// <summary>
    /// 텍스처 해상도, GPU 월드 렌더 크기, 근평면, 바닥·천장 색.
    /// 값을 바꾸면 게임 밸런스에 바로 영향을 주므로 의도적으로 수정할 때만 바꾼다.
    /// </summary>
    public static class RenderConfig
    {
        /// <summary>벽 텍스처 한 장의 크기(픽셀). 정사각형으로 가정한다.
        /// 셰이더·아틀라스·레이캐스트 texX·스프라이트 셀이 모두 이 값을 참조하므로,
        /// 월드 벽/바닥/천장/문 텍스처 해상도는 별도 <see cref="WorldTextureSize"/>로 분리해 올린다.</summary>
        public const int TextureSize = 64;

        /// <summary>
        /// 월드 벽/바닥/천장/문 텍스처를 로드할 때 리샘플하는 해상도(픽셀). TextureSize의 정수 배여야 한다.
        /// 이 값만큼 아틀라스가 커지지만 스프라이트(적/무기/픽업)에는 영향을 주지 않는다.
        /// </summary>
        public const int WorldTextureSize = 256;

        /// <summary>레이캐스팅 근거리 클리핑 거리(타일). 카메라에 너무 가까운 벽을 처리한다.</summary>
        public const float NearPlane = 0.2f;

        /// <summary>근거리 클리핑 부드러움 계수. 클수록 클리핑 경계가 부드럽게 처리된다.</summary>
        public const float NearPlaneSoftness = 0.03f;

        /// <summary>GPU 월드 렌더 타깃 최대 너비(픽셀). 이보다 크면 이 값으로 제한된다.</summary>
        public const int GpuWorldMaxRenderWidth = 640;

        /// <summary>GPU 월드 렌더 타깃 최대 높이(픽셀).</summary>
        public const int GpuWorldMaxRenderHeight = 360;

        /// <summary>true이면 바닥에 텍스처를 입힌다. false이면 단색 FloorColor로 표시된다.</summary>
        public const bool GpuWorldUseTexturedFloor = true;

        /// <summary>true이면 천장에 텍스처를 입힌다. false이면 단색 CeilingColor로 표시된다.</summary>
        public const bool GpuWorldUseTexturedCeiling = true;

        /// <summary>천장 색상(기본값: 검정). 텍스처와 블렌딩하거나 단색 폴백으로 사용된다.</summary>
        public static readonly Color CeilingColor = Color.Black;

        /// <summary>바닥 단색(기본값: 어두운 회색).</summary>
        public static readonly Color FloorColor = Color.FromArgb(60, 60, 60);
    }
}
