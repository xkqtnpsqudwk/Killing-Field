using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using My2DEngine.Game.Config;
using My2DEngine.Game;

namespace My2DEngine.Game.Rendering
{
    /// <summary>
    /// 텍스처, 스프라이트, 아이템 이미지를 로드하고 런타임 캐시를 제공하는 자원 관리자다.
    /// 벽 텍스처, 적 스프라이트, 무기 프레임, UI 이미지 등 렌더링에 필요한
    /// 모든 픽셀 데이터를 초기화하고 조회 API를 제공한다.
    /// partial 클래스로 분리되어 있으며 Animations, Assets, Loading 파일에 기능이 나뉜다.
    /// <see cref="IDisposable"/>을 구현하므로 사용 후 반드시 <see cref="Dispose"/>를 호출해야 한다.
    /// </summary>
    public partial class TextureManager : IDisposable
    {
        /// <summary>
        /// 벽 텍스처 배열. 각 원소는 TextureSize × TextureSize 크기의 색상 배열이다.
        /// 인덱스 0~7까지 8종의 벽 텍스처를 보관한다.
        /// </summary>
        private readonly Color[][] wallTextures;

        /// <summary>
        /// 기본 적(enemy) 스프라이트 배열. <see cref="EnemyType"/> 열거값 순서로 저장된다.
        /// 각 원소는 TextureSize × TextureSize 크기의 색상 배열이다.
        /// </summary>
        private readonly Color[][] enemySprites;

        /// <summary>
        /// 이름(variant key) 기반으로 적 스프라이트를 조회하는 딕셔너리 (대소문자 무시).
        /// 기본 타입 스프라이트와 색조 변형 스프라이트가 함께 등록된다.
        /// </summary>
        private readonly Dictionary<string, Color[]> enemySpriteVariants;

        /// <summary>
        /// 보스 및 특수 적의 애니메이션 스프라이트 시트를 이름으로 조회하는 딕셔너리 (대소문자 무시).
        /// 각 시트는 Idle, Move, Attack, Special, Death 애니메이션 프레임을 포함한다.
        /// </summary>
        private readonly Dictionary<string, BossSpriteSheet> bossSpriteSheets;

        /// <summary>
        /// 일반 적 절차적 variant 키 목록이다.
        /// 스폰 시 무작위 시각 변형 선택에 사용한다.
        /// </summary>
        private readonly List<string> normalEnemyVariantKeys;

        /// <summary>
        /// 보스 절차적 variant 키 목록이다.
        /// 스폰 시 무작위 시각 변형 선택에 사용한다.
        /// </summary>
        private readonly List<string> bossEnemyVariantKeys;

        /// <summary>
        /// 절차적 variant 키별 생성 메타데이터를 저장한다.
        /// 실제 시트/프레임은 최초 사용 시 생성된다.
        /// </summary>
        private readonly Dictionary<string, ProceduralVariantDescriptor> proceduralVariantDescriptors;

        /// <summary>
        /// 보상 아이템 종류(<see cref="RewardPickupKind"/>)별로 픽업 스프라이트를 저장하는 딕셔너리.
        /// </summary>
        private readonly Dictionary<RewardPickupKind, Color[]> pickupSprites;

        /// <summary>
        /// 무기 종류별 발사 프레임 이미지 배열.
        /// 외부 키: (int)WeaponType. 내부 배열: [0..2]=Idle, [3..7]=Fire1..5.
        /// </summary>
        private readonly Image[][] weaponTypeFireImages;

        /// <summary>
        /// 열린 문의 텍스처 색상 배열 (TextureSize × TextureSize 크기).
        /// </summary>
        private readonly Color[] doorOpenTexture;

        /// <summary>
        /// 파일 이름을 절대 경로로 변환한 결과를 저장하는 캐시 (대소문자 무시).
        /// 중복 파일 시스템 탐색을 방지한다.
        /// </summary>
        private readonly Dictionary<string, string> resolvedAssetPathCache;

        /// <summary>
        /// 디렉터리 세그먼트 조합을 실제 디렉터리 절대 경로로 변환한 결과를 저장하는 캐시 (대소문자 무시).
        /// </summary>
        private readonly Dictionary<string, string> resolvedAssetDirectoryCache;

        /// <summary>
        /// GPU 업로드용으로 사전 패킹된 벽 텍스처 아틀라스 픽셀 배열 (ARGB int[]).
        /// null이면 아직 빌드되지 않은 상태이며, <see cref="GetWallTextureAtlasPixels"/> 호출 시 생성된다.
        /// </summary>
        private int[] wallTextureAtlasPixels;

        /// <summary>벽 텍스처 아틀라스의 너비(픽셀). TextureSize × 텍스처 개수.</summary>
        private int wallTextureAtlasWidth;

        /// <summary>벽 텍스처 아틀라스의 높이(픽셀). TextureSize와 동일.</summary>
        private int wallTextureAtlasHeight;

        /// <summary>
        /// GPU 업로드용으로 사전 패킹된 열린 문 텍스처 픽셀 배열 (ARGB int[]).
        /// null이면 아직 빌드되지 않은 상태이며, <see cref="GetDoorOpenTexturePixels"/> 호출 시 생성된다.
        /// </summary>
        private int[] doorOpenTexturePixels;

        /// <summary>이 인스턴스가 이미 Dispose되었는지 나타내는 플래그</summary>
        private bool disposed;

        /// <summary>벽 텍스처 배열을 읽기 전용으로 노출한다. 인덱스 0~7의 8종 텍스처를 담는다.</summary>
        public Color[][] WallTextures => wallTextures;

        /// <summary>기본 적 스프라이트 배열을 읽기 전용으로 노출한다. <see cref="EnemyType"/> 순서이다.</summary>
        public Color[][] EnemySprites => enemySprites;

        /// <summary>열린 문 텍스처 색상 배열을 읽기 전용으로 노출한다.</summary>
        public Color[] DoorOpenTexture => doorOpenTexture;

        /// <summary>등록된 벽 텍스처의 총 개수를 반환한다.</summary>
        public int WallTextureCount => wallTextures.Length;

        /// <summary>
        /// <see cref="TextureManager"/>를 초기화한다.
        /// 벽 텍스처(8종), 적 스프라이트(3종), 무기 프레임(4종), UI 이미지 배열 등
        /// 모든 내부 버퍼를 빈 상태로 할당한다.
        /// 실제 데이터 로딩은 <see cref="LoadAllTextures"/>를 호출해야 시작된다.
        /// </summary>
        public TextureManager()
        {
            wallTextures = new Color[8][];
            for (int i = 0; i < wallTextures.Length; i++)
            {
                wallTextures[i] = new Color[GameConfig.TextureSize * GameConfig.TextureSize];
            }

            enemySprites = new Color[3][];
            for (int i = 0; i < enemySprites.Length; i++)
            {
                enemySprites[i] = new Color[GameConfig.TextureSize * GameConfig.TextureSize];
            }

            enemySpriteVariants = new Dictionary<string, Color[]>(StringComparer.OrdinalIgnoreCase);
            bossSpriteSheets = new Dictionary<string, BossSpriteSheet>(StringComparer.OrdinalIgnoreCase);
            normalEnemyVariantKeys = [];
            bossEnemyVariantKeys = [];
            proceduralVariantDescriptors = new Dictionary<string, ProceduralVariantDescriptor>(StringComparer.OrdinalIgnoreCase);
            pickupSprites = [];

            int weaponCount = System.Enum.GetValues(typeof(WeaponType)).Length;
            weaponTypeFireImages   = new Image[weaponCount][];
            for (int i = 0; i < weaponCount; i++)
            {
                weaponTypeFireImages[i]   = new Image[GameConfig.WeaponOverlayFrameCount];
            }

            doorOpenTexture = new Color[GameConfig.TextureSize * GameConfig.TextureSize];
            resolvedAssetPathCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            resolvedAssetDirectoryCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 보유 중인 모든 GDI+ 이미지 리소스를 해제하고 GPU 캐시 배열을 초기화한다.
        /// <see cref="IDisposable"/> 구현. 중복 호출은 안전하게 무시된다.
        /// 색상(Color[]) 배열은 관리형 메모리이므로 별도 해제가 필요 없다.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 리소스 해제를 수행한다.
        /// </summary>
        /// <param name="disposing">true이면 관리 리소스를 함께 해제한다.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                if (weaponTypeFireImages != null)
                {
                    for (int t = 0; t < weaponTypeFireImages.Length; t++)
                    {
                        if (weaponTypeFireImages[t] == null) continue;
                        for (int f = 0; f < weaponTypeFireImages[t].Length; f++)
                        {
                            weaponTypeFireImages[t][f]?.Dispose();
                            weaponTypeFireImages[t][f] = null;
                        }
                    }
                }

                hudCrosshair?.Dispose();
                hudCrosshair = null;
                hudHitMarker?.Dispose();
                hudHitMarker = null;
                hudKillMarker?.Dispose();
                hudKillMarker = null;
                hudMessagePanel?.Dispose();
                hudMessagePanel = null;
                hudHealthBarFrame?.Dispose();
                hudHealthBarFrame = null;
                hudShieldBarFrame?.Dispose();
                hudShieldBarFrame = null;
                hudStaminaBarFrame?.Dispose();
                hudStaminaBarFrame = null;
                DisposeImageArray(hudHealthBarFrames);
                hudHealthBarFrames = null;
                DisposeImageArray(hudShieldBarFrames);
                hudShieldBarFrames = null;
                DisposeImageArray(hudStaminaBarFrames);
                hudStaminaBarFrames = null;
                hudCoinPanel?.Dispose();
                hudCoinPanel = null;
                hudAmmoPanel?.Dispose();
                hudAmmoPanel = null;

                resolvedAssetPathCache.Clear();
                resolvedAssetDirectoryCache.Clear();
                enemySpriteVariants.Clear();
                bossSpriteSheets.Clear();
                normalEnemyVariantKeys.Clear();
                bossEnemyVariantKeys.Clear();
                proceduralVariantDescriptors.Clear();
            }

            wallTextureAtlasPixels = null;
            wallTextureAtlasWidth = 0;
            wallTextureAtlasHeight = 0;
            doorOpenTexturePixels = null;
            disposed = true;
        }

        /// <summary>
        /// 게임에 필요한 모든 텍스처와 스프라이트를 로드한다.
        /// 벽/무기/UI는 기존 로딩 정책을 유지하고,
        /// 적/보스는 절차적으로 생성된 애니메이션 시트를 등록한다.
        /// </summary>
        public void LoadAllTextures()
        {
            BuildFallbackWallTextures();
            LoadWallTexturesFromFiles();

            enemySpriteVariants.Clear();
            bossSpriteSheets.Clear();
            normalEnemyVariantKeys.Clear();
            bossEnemyVariantKeys.Clear();
            proceduralVariantDescriptors.Clear();
            LoadEnemySpritesFromFiles();
            LoadBossSpriteSheets();
            EnsureEnemyVariantPoolsLoaded();
            LoadPickupSprites();

            LoadWeaponTypesFromGunFolders();
            LoadHudTextures();
            InvalidateGpuTextureCaches();
        }

        /// <summary>HUD 조준점/히트마커 텍스처. Game/Images/ui 폴더에서 로드하며 없으면 null(코드 기본 그리기로 대체).</summary>
        private Image hudCrosshair;
        private Image hudHitMarker;
        private Image hudKillMarker;
        private Image hudMessagePanel;

        /// <summary>
        /// 체력/보호막/스태미나 게이지의 "빈 틀" 이미지와 코인/탄약 패널 틀 이미지.
        /// 게이지는 틀만 제공하고 동적 채움은 코드가 그린다. 없으면 null(코드 단색 fallback).
        /// </summary>
        private Image hudHealthBarFrame;
        private Image hudShieldBarFrame;
        private Image hudStaminaBarFrame;
        private Image hudCoinPanel;
        private Image hudAmmoPanel;

        /// <summary>
        /// 체력/보호막/스태미나 게이지의 채움 단계별 프레임 시퀀스.
        /// 인덱스 0=빈(0%) … 마지막=가득(100%). ratio로 한 장을 골라 그대로 그린다.
        /// 시퀀스가 없으면 null이며, 이 경우 단일 틀(hud*BarFrame) + 코드 채움으로 폴백한다.
        /// </summary>
        private Image[] hudHealthBarFrames;
        private Image[] hudShieldBarFrames;
        private Image[] hudStaminaBarFrames;

        /// <summary>HUD 게이지/패널 틀 이미지를 로드할 때 줄여 보관할 목표 너비(픽셀). 원본 2508px를 메모리 절약 위해 축소한다.</summary>
        private const int HudFrameTargetWidth = 512;

        private void LoadHudTextures()
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Game", "Images", "ui");
            hudCrosshair = LoadOptionalHudImage(Path.Combine(dir, "Crosshair.png"));
            hudHitMarker = LoadOptionalHudImage(Path.Combine(dir, "HitMarker.png"));
            hudKillMarker = LoadOptionalHudImage(Path.Combine(dir, "KillMarker.png"));
            // 진행 중 알림 메시지 배경에 쓰는 패널 틀. 호버 버튼 프레임을 재사용한다(이미 누끼된 32bpp).
            hudMessagePanel = LoadOptionalHudImage(Path.Combine(dir, "ButtonFrameHover.png"));

            // 게이지/패널 틀은 GPT 원본이 흰 배경(누끼 전)일 수 있어 로드 시 자동 누끼 + 축소한다.
            // 게이지는 채움 단계별 시퀀스(_0.._N)가 있으면 우선 사용하고, 없으면 단일 틀로 폴백한다.
            hudHealthBarFrames = LoadCutoutHudSequence(dir, "HealthBar", HudFrameTargetWidth);
            hudShieldBarFrames = LoadCutoutHudSequence(dir, "ShieldBar", HudFrameTargetWidth);
            hudStaminaBarFrames = LoadCutoutHudSequence(dir, "StaminaBar", HudFrameTargetWidth);
            hudHealthBarFrame = LoadCutoutHudImage(Path.Combine(dir, "HealthBar.png"), HudFrameTargetWidth);
            hudShieldBarFrame = LoadCutoutHudImage(Path.Combine(dir, "ShieldBar.png"), HudFrameTargetWidth);
            hudStaminaBarFrame = LoadCutoutHudImage(Path.Combine(dir, "StaminaBar.png"), HudFrameTargetWidth);
            hudCoinPanel = LoadCutoutHudImage(Path.Combine(dir, "CoinPanel.png"), HudFrameTargetWidth);
            hudAmmoPanel = LoadCutoutHudImage(Path.Combine(dir, "AmmoPanel.png"), HudFrameTargetWidth);
        }

        private static Image LoadOptionalHudImage(string path)
        {
            return File.Exists(path) ? Image.FromFile(path) : null;
        }

        /// <summary>
        /// {baseName}_0.png 부터 번호가 끊길 때까지 순서대로 누끼+축소 로드해 프레임 배열로 반환한다.
        /// 첫 프레임(_0)이 없으면 null을 반환한다(시퀀스 미사용으로 폴백).
        /// </summary>
        private static Image[] LoadCutoutHudSequence(string dir, string baseName, int targetWidth)
        {
            var frames = new List<Image>();
            for (int i = 0; ; i++)
            {
                string path = Path.Combine(dir, baseName + "_" + i + ".png");
                if (!File.Exists(path))
                {
                    break;
                }

                Image img = LoadCutoutHudImage(path, targetWidth);
                if (img == null)
                {
                    break;
                }

                frames.Add(img);
            }

            return frames.Count > 0 ? frames.ToArray() : null;
        }

        /// <summary>이미지 배열의 모든 원소를 안전하게 해제한다. null 배열/원소는 무시한다.</summary>
        private static void DisposeImageArray(Image[] images)
        {
            if (images == null)
            {
                return;
            }

            for (int i = 0; i < images.Length; i++)
            {
                images[i]?.Dispose();
                images[i] = null;
            }
        }

        /// <summary>
        /// HUD 틀 이미지를 로드한다. 목표 너비로 비율 유지 축소한 뒤,
        /// 가장자리에 연결된 밝은 무채색(흰 배경)을 flood-fill로 투명화(누끼)한다.
        /// 이미 투명한 PNG면 누끼 단계가 사실상 무시되므로 누끼 여부와 무관하게 안전하다.
        /// 파일이 없으면 null을 반환한다.
        /// </summary>
        private static Image LoadCutoutHudImage(string path, int targetWidth)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            using (Bitmap src = new Bitmap(path))
            {
                int tw = (targetWidth > 0 && src.Width > targetWidth) ? targetWidth : src.Width;
                int th = Math.Max(1, (int)Math.Round(src.Height * (tw / (float)src.Width)));
                Bitmap bmp = new Bitmap(tw, th, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    g.DrawImage(src, new Rectangle(0, 0, tw, th));
                }

                CutoutBackground(bmp);
                return bmp;
            }
        }

        /// <summary>
        /// 네 모서리에서 시작하는 flood-fill로 가장자리에 연결된 밝은 무채색 픽셀(흰 배경)의
        /// 알파를 0으로 만들어 투명화한다. 내부의 채색 세그먼트/아이콘과 어두운 금속 틀은 보존된다.
        /// </summary>
        private static void CutoutBackground(Bitmap bmp)
        {
            int w = bmp.Width;
            int h = bmp.Height;
            Rectangle rect = new Rectangle(0, 0, w, h);
            BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                int total = stride * h;
                byte[] buf = new byte[total];
                Marshal.Copy(data.Scan0, buf, 0, total);

                bool[] visited = new bool[w * h];
                Stack<int> stack = new Stack<int>((w + h) * 2);

                for (int x = 0; x < w; x++)
                {
                    SeedBackgroundPixel(buf, visited, stack, stride, w, h, x, 0);
                    SeedBackgroundPixel(buf, visited, stack, stride, w, h, x, h - 1);
                }
                for (int y = 0; y < h; y++)
                {
                    SeedBackgroundPixel(buf, visited, stack, stride, w, h, 0, y);
                    SeedBackgroundPixel(buf, visited, stack, stride, w, h, w - 1, y);
                }

                while (stack.Count > 0)
                {
                    int idx = stack.Pop();
                    int x = idx % w;
                    int y = idx / w;
                    buf[(y * stride) + (x * 4) + 3] = 0;
                    SeedBackgroundPixel(buf, visited, stack, stride, w, h, x - 1, y);
                    SeedBackgroundPixel(buf, visited, stack, stride, w, h, x + 1, y);
                    SeedBackgroundPixel(buf, visited, stack, stride, w, h, x, y - 1);
                    SeedBackgroundPixel(buf, visited, stack, stride, w, h, x, y + 1);
                }

                Marshal.Copy(buf, 0, data.Scan0, total);
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }

        /// <summary>flood-fill 후보 픽셀을 검사해 배경이면 방문 표시 후 스택에 넣는다.</summary>
        private static void SeedBackgroundPixel(byte[] buf, bool[] visited, Stack<int> stack, int stride, int w, int h, int x, int y)
        {
            if (x < 0 || y < 0 || x >= w || y >= h)
            {
                return;
            }

            int idx = (y * w) + x;
            if (visited[idx])
            {
                return;
            }

            int p = (y * stride) + (x * 4);
            // 메모리 바이트 순서는 BGRA.
            if (IsBackgroundColor(buf[p + 2], buf[p + 1], buf[p + 0], buf[p + 3]))
            {
                visited[idx] = true;
                stack.Push(idx);
            }
        }

        /// <summary>밝고 무채색(흰 배경)이거나 이미 투명한 픽셀을 배경으로 판정한다.</summary>
        private static bool IsBackgroundColor(byte r, byte g, byte b, byte a)
        {
            if (a < 16)
            {
                return true;
            }

            int mn = Math.Min(r, Math.Min(g, b));
            int mx = Math.Max(r, Math.Max(g, b));
            return mn >= 190 && (mx - mn) <= 30;
        }

        /// <summary>HUD 조준점 텍스처. 없으면 null.</summary>
        public Image GetCrosshair() => hudCrosshair;

        /// <summary>HUD 명중 마커 텍스처. 없으면 null.</summary>
        public Image GetHitMarker() => hudHitMarker;

        /// <summary>HUD 처치 마커 텍스처. 없으면 null.</summary>
        public Image GetKillMarker() => hudKillMarker;

        /// <summary>진행 중 알림 메시지 배경용 패널 틀(호버 버튼 프레임 재사용). 없으면 null(단색 fallback).</summary>
        public Image GetMessagePanel() => hudMessagePanel;

        /// <summary>체력 게이지 빈 틀 이미지(시퀀스 없을 때 폴백). 없으면 null(코드 단색 fallback).</summary>
        public Image GetHealthBarFrame() => hudHealthBarFrame;

        /// <summary>보호막 게이지 빈 틀 이미지(시퀀스 없을 때 폴백). 없으면 null.</summary>
        public Image GetShieldBarFrame() => hudShieldBarFrame;

        /// <summary>스태미나 게이지 빈 틀 이미지(시퀀스 없을 때 폴백). 없으면 null.</summary>
        public Image GetStaminaBarFrame() => hudStaminaBarFrame;

        /// <summary>체력 게이지 채움 단계 시퀀스(0=빈 … 마지막=가득). 없으면 null.</summary>
        public Image[] GetHealthBarFrames() => hudHealthBarFrames;

        /// <summary>보호막 게이지 채움 단계 시퀀스. 없으면 null.</summary>
        public Image[] GetShieldBarFrames() => hudShieldBarFrames;

        /// <summary>스태미나 게이지 채움 단계 시퀀스. 없으면 null.</summary>
        public Image[] GetStaminaBarFrames() => hudStaminaBarFrames;

        /// <summary>코인 패널 틀 이미지. 없으면 null.</summary>
        public Image GetCoinPanel() => hudCoinPanel;

        /// <summary>탄약 패널 틀 이미지. 없으면 null.</summary>
        public Image GetAmmoPanel() => hudAmmoPanel;

        /// <summary>
        /// 지정 무기 종류의 발사 프레임 이미지를 반환한다.
        /// frameIndex: 0~2=Idle, 3~7=Fire1..5.
        /// 해당 이미지가 없으면 null.
        /// </summary>
        public Image GetWeaponFireImage(WeaponType type, int frameIndex)
        {
            int t = (int)type;
            if (t < 0 || t >= weaponTypeFireImages.Length) return null;
            var arr = weaponTypeFireImages[t];
            if (arr == null || frameIndex < 0 || frameIndex >= arr.Length) return null;
            return arr[frameIndex];
        }

        /// <summary>
        /// 적 스프라이트를 반환한다.
        /// 적/보스 렌더링은 variant 키가 필수이며,
        /// 유효한 variant가 없으면 즉시 예외를 던진다.
        /// </summary>
        /// <param name="spriteVariantKey">조회할 스프라이트 variant 이름</param>
        /// <param name="fallbackType">오류 메시지에 함께 남길 적 타입</param>
        /// <returns>해당 적의 색상 배열 (TextureSize × TextureSize)</returns>
        public Color[] GetEnemySprite(string spriteVariantKey, EnemyType fallbackType)
        {
            if (string.IsNullOrWhiteSpace(spriteVariantKey))
            {
                throw new InvalidOperationException(
                    "Enemy sprite variant key is required for procedural enemy rendering. EnemyType=" +
                    fallbackType + ".");
            }

            if (enemySpriteVariants.TryGetValue(spriteVariantKey, out Color[] sprite) && sprite != null)
            {
                return sprite;
            }

            if (EnsureProceduralVariantLoaded(spriteVariantKey) &&
                enemySpriteVariants.TryGetValue(spriteVariantKey, out sprite) &&
                sprite != null)
            {
                return sprite;
            }

            throw new InvalidOperationException(
                "Enemy sprite variant is not registered: " +
                spriteVariantKey + " (EnemyType=" + fallbackType + ").");
        }

        /// <summary>
        /// 지정한 적 sprite variant가 현재 로드된 에셋 집합에 존재하는지 반환한다.
        /// 현재 등록된 절차적 적 스프라이트 variant 상태 검증에 사용한다.
        /// </summary>
        public bool HasEnemySpriteVariant(string spriteVariantKey)
        {
            if (string.IsNullOrWhiteSpace(spriteVariantKey))
            {
                return false;
            }

            if (enemySpriteVariants.TryGetValue(spriteVariantKey, out Color[] sprite) && sprite != null)
            {
                return true;
            }

            return proceduralVariantDescriptors.ContainsKey(spriteVariantKey);
        }

        /// <summary>
        /// 주어진 적 인스턴스의 현재 상태에 맞는 애니메이션 프레임 색상 배열을 반환한다.
        /// 보스 스프라이트 시트가 있는 경우 현재 애니메이션 종류와 시간에 따라
        /// 적절한 프레임을 추출하고, 그렇지 않으면 적의 현재 스프라이트를 그대로 반환한다.
        /// </summary>
        /// <param name="enemy">프레임을 가져올 적 인스턴스. null이면 null을 반환한다.</param>
        /// <returns>현재 재생해야 할 색상 배열 (TextureSize × TextureSize), 또는 null.</returns>
        public Color[] GetAnimatedEnemySprite(Enemy enemy)
        {
            if (enemy == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(enemy.SpriteVariantKey))
            {
                EnsureProceduralVariantLoaded(enemy.SpriteVariantKey);
                if (bossSpriteSheets.TryGetValue(enemy.SpriteVariantKey, out BossSpriteSheet sheet) && sheet != null)
                {
                    BossAnimationKind kind = GetEnemyAnimationKind(enemy);
                    float animationTime = kind == BossAnimationKind.Death
                        ? enemy.DeathAnimationTime
                        : kind == BossAnimationKind.Spawn
                            ? enemy.SpawnAnimationTime
                            : enemy.AnimationTime;
                    Color[] frame = sheet.GetFrame(kind, animationTime);
                    if (frame != null)
                    {
                        return frame;
                    }
                }
            }

            return enemy.Sprite;
        }

        /// <summary>
        /// 지정한 적 variant의 스폰 연출 길이를 반환한다.
        /// 애니메이션 시트가 없으면 0을 반환한다.
        /// </summary>
        /// <param name="spriteVariantKey">조회할 적 sprite variant 키.</param>
        /// <returns>스폰 연출 길이(초).</returns>
        public float GetEnemySpawnAnimationDuration(string spriteVariantKey)
        {
            if (string.IsNullOrWhiteSpace(spriteVariantKey))
            {
                return 0f;
            }

            EnsureProceduralVariantLoaded(spriteVariantKey);
            if (bossSpriteSheets.TryGetValue(spriteVariantKey, out BossSpriteSheet sheet) && sheet != null)
            {
                return sheet.GetAnimationDuration(BossAnimationKind.Spawn);
            }

            return 0f;
        }

        /// <summary>
        /// 일반 적/보스 구분에 따라 등록된 variant 풀에서 무작위 키를 하나 반환한다.
        /// 등록된 variant가 없으면 null을 반환한다.
        /// </summary>
        public string PickRandomEnemyVariantKey(Random rng, EnemyRank rank)
        {
            if (rng == null)
            {
                return null;
            }

            List<string> pool = rank == EnemyRank.Boss ? bossEnemyVariantKeys : normalEnemyVariantKeys;
            if (pool == null || pool.Count == 0)
            {
                return null;
            }

            return pool[rng.Next(pool.Count)];
        }

        /// <summary>
        /// 지정한 보상 아이템 종류에 해당하는 픽업 스프라이트 색상 배열을 반환한다.
        /// 해당 종류의 스프라이트가 로드되지 않았으면 null을 반환한다.
        /// </summary>
        /// <param name="kind">스프라이트를 가져올 보상 아이템 종류</param>
        /// <returns>픽업 아이템의 색상 배열 (TextureSize × TextureSize), 없으면 null.</returns>
        public Color[] GetPickupSprite(RewardPickupKind kind)
        {
            if (pickupSprites.TryGetValue(kind, out Color[] sprite) && sprite != null)
            {
                return sprite;
            }

            return null;
        }

        /// <summary>
        /// 색상을 지정한 배율(factor)로 밝게 만든다.
        /// 각 채널(R, G, B)에 factor를 곱하고 0~255 범위로 클램프한다.
        /// 알파 채널은 변경하지 않는다.
        /// </summary>
        /// <param name="color">원본 색상</param>
        /// <param name="factor">밝기 배율 (1.0이면 원본 유지, 1.2이면 20% 밝게)</param>
        /// <returns>밝기가 조정된 새 색상</returns>
        private Color Lighten(Color color, float factor)
        {
            int r = ClampToByte(color.R * factor);
            int g = ClampToByte(color.G * factor);
            int b = ClampToByte(color.B * factor);
            return Color.FromArgb(color.A, r, g, b);
        }

        /// <summary>
        /// 부동소수점 값을 0~255 범위의 정수로 클램프한다.
        /// 색상 채널 계산 후 byte 범위를 벗어나는 값을 안전하게 처리하기 위해 사용한다.
        /// </summary>
        /// <param name="value">클램프할 부동소수점 값</param>
        /// <returns>0~255 범위로 제한된 정수 값</returns>
        private int ClampToByte(float value)
        {
            if (value < 0f)
            {
                return 0;
            }

            if (value > 255f)
            {
                return 255;
            }

            return (int)value;
        }

        /// <summary>
        /// GPU 렌더러에서 사용할 벽 텍스처 아틀라스를 ARGB int[] 형태로 반환한다.
        /// 최초 호출 시 모든 벽 텍스처를 가로로 이어 붙인 아틀라스를 빌드하며,
        /// 이후 텍스처가 변경되기 전까지는 캐시된 배열을 즉시 반환한다.
        /// </summary>
        /// <param name="width">아틀라스의 너비(픽셀)를 out으로 반환한다.</param>
        /// <param name="height">아틀라스의 높이(픽셀)를 out으로 반환한다.</param>
        /// <returns>ARGB 형식으로 패킹된 아틀라스 픽셀 배열</returns>
        public int[] GetWallTextureAtlasPixels(out int width, out int height)
        {
            // GPU world path는 wall atlas를 int[] ARGB 형태로 한 번에 올리므로
            // 필요한 순간까지 lazy-build 하고, 이후에는 변경 전까지 재사용한다.
            if (wallTextureAtlasPixels == null)
            {
                BuildWallTextureAtlasPixels();
            }

            width = wallTextureAtlasWidth;
            height = wallTextureAtlasHeight;
            return wallTextureAtlasPixels;
        }

        /// <summary>
        /// GPU 렌더러에서 사용할 열린 문 텍스처를 ARGB int[] 형태로 반환한다.
        /// 최초 호출 시 <see cref="doorOpenTexture"/> 색상 배열을 int[]로 변환하며,
        /// 이후에는 캐시된 배열을 반환한다.
        /// </summary>
        /// <param name="width">텍스처 너비(픽셀)를 out으로 반환한다.</param>
        /// <param name="height">텍스처 높이(픽셀)를 out으로 반환한다.</param>
        /// <returns>ARGB 형식으로 패킹된 열린 문 텍스처 픽셀 배열</returns>
        public int[] GetDoorOpenTexturePixels(out int width, out int height)
        {
            if (doorOpenTexturePixels == null)
            {
                int pixelCount = GameConfig.TextureSize * GameConfig.TextureSize;
                doorOpenTexturePixels = new int[pixelCount];
                for (int i = 0; i < pixelCount; i++)
                {
                    doorOpenTexturePixels[i] = doorOpenTexture[i].ToArgb();
                }
            }

            width = GameConfig.TextureSize;
            height = GameConfig.TextureSize;
            return doorOpenTexturePixels;
        }

        /// <summary>
        /// GPU 업로드용으로 캐시된 packed pixel 배열을 무효화한다.
        /// 텍스처 데이터가 변경된 후 호출하여 다음 <see cref="GetWallTextureAtlasPixels"/> 호출 시
        /// 아틀라스가 새로 빌드되도록 한다.
        /// CPU 색상 배열(<see cref="wallTextures"/>, <see cref="doorOpenTexture"/>)은 그대로 유지된다.
        /// </summary>
        private void InvalidateGpuTextureCaches()
        {
            wallTextureAtlasPixels = null;
            wallTextureAtlasWidth = 0;
            wallTextureAtlasHeight = 0;
            doorOpenTexturePixels = null;
        }

        /// <summary>
        /// 모든 벽 텍스처를 가로로 이어 붙인 아틀라스를 빌드하여
        /// <see cref="wallTextureAtlasPixels"/>에 ARGB int[] 형태로 저장한다.
        /// 아틀라스 너비 = TextureSize × 텍스처 개수, 높이 = TextureSize.
        /// </summary>
        private void BuildWallTextureAtlasPixels()
        {
            wallTextureAtlasWidth = GameConfig.TextureSize * wallTextures.Length;
            wallTextureAtlasHeight = GameConfig.TextureSize;
            wallTextureAtlasPixels = new int[wallTextureAtlasWidth * wallTextureAtlasHeight];

            for (int textureIndex = 0; textureIndex < wallTextures.Length; textureIndex++)
            {
                Color[] sourceTexture = wallTextures[textureIndex];
                int atlasOffsetX = textureIndex * GameConfig.TextureSize;
                for (int y = 0; y < GameConfig.TextureSize; y++)
                {
                    int sourceRow = y * GameConfig.TextureSize;
                    int atlasRow = y * wallTextureAtlasWidth;
                    for (int x = 0; x < GameConfig.TextureSize; x++)
                    {
                        wallTextureAtlasPixels[atlasRow + atlasOffsetX + x] = sourceTexture[sourceRow + x].ToArgb();
                    }
                }
            }
        }
    }
}
