using My2DEngine.Engine.Rendering.Backends.D3D11.World;

namespace My2DEngine.Engine.Rendering.Backends.D3D11
{
    /// <summary>
    /// <see cref="D3D11DeviceResources"/>의 presenter 지연 초기화 책임을 담당하는 부분 클래스입니다.
    /// 어떤 draw 요청이 어떤 presenter를 생성했는지 추적하기 쉽도록 별도 파일로 분리되어 있습니다.
    /// </summary>
    internal sealed partial class D3D11DeviceResources
    {
        /// <summary>
        /// 오버레이 presenter를 폐기한다.
        /// 텍스트 캐시나 D2D 렌더 타깃이 깨졌을 때 해당 계층만 국소적으로 재생성하기 위해 사용한다.
        /// </summary>
        private void ResetOverlayPresenter()
        {
            overlayPresenter?.Dispose();
            overlayPresenter = null;
        }

        /// <summary>
        /// 장치는 유지한 채 모든 presenter 인스턴스를 폐기한다.
        /// 리사이즈 후에는 presenter 내부의 크기 의존 텍스처와 D2D 타깃을 새 표면 기준으로 다시 만드는 편이 안전하다.
        /// </summary>
        private void ResetAllPresenters()
        {
            ResetOverlayPresenter();

            worldBeamPresenter?.Dispose();
            worldBeamPresenter = null;

            enemyWorldSpritePresenter?.Dispose();
            enemyWorldSpritePresenter = null;

            miscWorldSpritePresenter?.Dispose();
            miscWorldSpritePresenter = null;

            worldPresenter?.Dispose();
            worldPresenter = null;
        }

        /// <summary>
        /// UI 오버레이 presenter가 없으면 새로 생성합니다.
        /// 사각형, 이미지, 텍스트 오버레이가 처음 요청될 때 호출됩니다.
        /// </summary>
        private void EnsureOverlayPresenter()
        {
            if (overlayPresenter == null)
            {
                overlayPresenter = new D3D11OverlayPresenter(d3dDevice);
            }
        }

        /// <summary>
        /// GPU 레이캐스트 월드 presenter가 없으면 새로 생성합니다.
        /// GPU 월드 렌더링이 처음 요청될 때 호출됩니다.
        /// </summary>
        private void EnsureWorldPresenters()
        {
            if (worldPresenter == null)
            {
                worldPresenter = new D3D11WorldRaycastPresenter(d3dDevice);
            }
        }

        /// <summary>
        /// 적 스프라이트 presenter가 없으면 새로 생성합니다.
        /// 적 스프라이트 패스가 처음 요청될 때 호출됩니다.
        /// </summary>
        private void EnsureEnemyWorldSpritePresenter()
        {
            if (enemyWorldSpritePresenter == null)
            {
                enemyWorldSpritePresenter = new D3D11WorldSpritePresenter(d3dDevice);
            }
        }

        /// <summary>
        /// 기타 스프라이트 presenter가 없으면 새로 생성합니다.
        /// 기타 스프라이트 패스(아이템, 파티클 등)가 처음 요청될 때 호출됩니다.
        /// </summary>
        private void EnsureMiscWorldSpritePresenter()
        {
            if (miscWorldSpritePresenter == null)
            {
                miscWorldSpritePresenter = new D3D11WorldSpritePresenter(d3dDevice);
            }
        }

        /// <summary>
        /// 빔 효과 presenter가 없으면 새로 생성합니다.
        /// 레이저 빔 패스가 처음 요청될 때 호출됩니다.
        /// </summary>
        private void EnsureWorldBeamPresenter()
        {
            if (worldBeamPresenter == null)
            {
                worldBeamPresenter = new D3D11WorldBeamPresenter(d3dDevice);
            }
        }
    }
}
