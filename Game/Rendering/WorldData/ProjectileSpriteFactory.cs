using System;
using System.Drawing;
using My2DEngine.Game;

namespace My2DEngine.Rendering.WorldData
{
    /// <summary>
    /// 이미지가 없는 투사체(산성 덩어리·웅덩이, 적 탄, 로켓, 폭발)의 동그란 스프라이트를 처음 쓸 때 만든다.
    /// </summary>
    internal sealed class ProjectileSpriteFactory
    {
        /// <summary>산성 구체(AcidGlob) 투사체 스프라이트 픽셀 배열. 최초 요청 시 생성된다.</summary>
        private Color[] acidGlobSprite;

        /// <summary>산성 웅덩이(AcidPool) 투사체 스프라이트 픽셀 배열. 최초 요청 시 생성된다.</summary>
        private Color[] acidPoolSprite;

        /// <summary>일반 적 탄환(EnemyShot) 스프라이트 픽셀 배열. 최초 요청 시 생성된다.</summary>
        private Color[] enemyShotSprite;

        /// <summary>보스 로켓(BossRocket) 투사체 스프라이트 픽셀 배열. 최초 요청 시 생성된다.</summary>
        private Color[] bossRocketSprite;

        /// <summary>플레이어 로켓 폭발(PlayerRocketExplosion) 스프라이트 픽셀 배열. 최초 요청 시 생성된다.</summary>
        private Color[] playerRocketExplosionSprite;


        /// <summary>
        /// 투사체 종류에 따라 미리 생성된 원형 스프라이트 픽셀 배열을 반환한다.
        /// LaserBeam은 별도 경로에서 처리되므로 이 함수에서는 null을 반환한다.
        /// </summary>
        /// <param name="projectile">픽셀을 가져올 투사체 인스턴스.</param>
        /// <returns>투사체 스프라이트 픽셀 배열. 지원하지 않는 종류이면 null.</returns>
        public Color[] Get(EnemyProjectile projectile)
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
        /// 아직 생성되지 않았으면 BuildCircularSprite로 초기화한다.
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
        /// 절차적으로 원형 스프라이트 픽셀 배열을 생성한다.
        /// 중심부터 가장자리까지 innerColor에서 outerColor로 선형 보간되며, 바깥쪽은 투명하다.
        /// </summary>
        /// <param name="outerColor">스프라이트 외곽 색상.</param>
        /// <param name="innerColor">스프라이트 중심 색상.</param>
        /// <param name="outerRadius">외곽 반지름(정규화, 0~1 범위). 이 값 바깥은 투명하다.</param>
        /// <param name="innerRadius">내부 단색 반지름(정규화). 이 값 안쪽은 innerColor로 채워진다.</param>
        /// <returns>RenderConfig.TextureSize × TextureSize 크기의 Color[] 픽셀 배열.</returns>
        private Color[] BuildCircularSprite(Color outerColor, Color innerColor, float outerRadius, float innerRadius)
        {
            var pixels = new Color[SpriteAtlasCache.CellSize * SpriteAtlasCache.CellSize];
            float center = (SpriteAtlasCache.CellSize - 1) * 0.5f;
            float invRadius = 1f / center;

            for (int y = 0; y < SpriteAtlasCache.CellSize; y++)
            {
                for (int x = 0; x < SpriteAtlasCache.CellSize; x++)
                {
                    float nx = (x - center) * invRadius;
                    float ny = (y - center) * invRadius;
                    float dist = (float)Math.Sqrt((nx * nx) + (ny * ny));
                    int index = (y * SpriteAtlasCache.CellSize) + x;

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
        /// 두 Color 값을 선형 보간한다. t=0이면 a, t=1이면 b를 반환한다.
        /// </summary>
        /// <param name="a">시작 색상.</param>
        /// <param name="b">끝 색상.</param>
        /// <param name="t">보간 비율(0~1). 범위를 벗어나면 클램핑된다.</param>
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
    }
}
