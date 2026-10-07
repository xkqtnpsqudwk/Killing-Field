using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using My2DEngine.Game.Config;

namespace My2DEngine.Game.Rendering
{
    /// <summary>
    /// 비트맵 디코딩, 픽셀 배열 변환, 폴백 텍스처 생성을 담당하는 partial 클래스.
    /// 파일에서 이미지를 읽어 TextureSize × TextureSize 크기로 리샘플링하고
    /// <see cref="Color"/>[] 또는 <see cref="Image"/> 형태로 반환하는 모든 로딩 로직을 포함한다.
    /// </summary>
    public partial class TextureManager
    {
        /// <summary>
        /// 에셋 파일이 없을 때 사용할 기본 벽 텍스처 8종을 절차적으로 생성한다.
        /// 각 인덱스는 서로 다른 패턴(체커, 벽돌, 석재, 나무, 줄무늬, 문, 녹색 대각선, 직물)을 가진다.
        /// 인덱스 5(문 닫힘)를 생성할 때 doorOpenTexture(문 열림 텍스처)도 함께 초기화된다.
        /// </summary>
        private void BuildFallbackWallTextures()
        {
            // wallTextures/doorOpenTexture 슬롯은 WorldTextureSize² 크기이므로 폴백도 같은 해상도로 채운다.
            // 문 패널 테두리는 64px 기준 절대 좌표였으므로 해상도 비율(scale)에 맞춰 환산한다.
            int size = RenderConfig.WorldTextureSize;
            float scale = size / 64f;
            int doorBorderX = (int)(6 * scale);
            int doorBorderTop = (int)(4 * scale);
            int doorCenter = size / 2;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int idx = y * size + x;

                    bool checker = ((x / 8 + y / 8) % 2) == 0;
                    wallTextures[0][idx] = checker ? Color.DarkRed : Color.Red;

                    bool brickLine = (y % 16) == 0;
                    bool brickGap = ((x + (y / 16) * 8) % 32) == 0;
                    wallTextures[1][idx] = (brickLine || brickGap)
                        ? Color.FromArgb(120, 60, 40)
                        : Color.FromArgb(170, 80, 60);

                    int noise = (x * 13 + y * 7) % 30;
                    wallTextures[2][idx] = Color.FromArgb(80 + noise, 80 + noise, 80 + noise);

                    int grain = (x * 5 + y * 11) % 40;
                    wallTextures[3][idx] = Color.FromArgb(120 + grain, 80 + grain / 2, 50);

                    bool stripes = (x / 6) % 2 == 0;
                    wallTextures[4][idx] = stripes
                        ? Color.FromArgb(50, 90, 140)
                        : Color.FromArgb(30, 60, 110);

                    bool doorPanel = x < doorBorderX || x > size - 1 - doorBorderX
                        || y < doorBorderTop || y > size - 1 - doorBorderTop
                        || x == doorCenter || x == doorCenter - 1;
                    wallTextures[5][idx] = doorPanel
                        ? Color.FromArgb(170, 138, 118, 78)
                        : Color.FromArgb(105, 74, 60, 36);
                    doorOpenTexture[idx] = Color.FromArgb(120, 84, 120, 150);

                    int diag = (x + y) % 20;
                    wallTextures[6][idx] = diag < 10
                        ? Color.FromArgb(60, 120, 60)
                        : Color.FromArgb(40, 90, 40);

                    bool weave = ((x / 4 + (y / 4) * 3) % 2) == 0;
                    wallTextures[7][idx] = weave
                        ? Color.FromArgb(140, 100, 60)
                        : Color.FromArgb(110, 80, 50);
                }
            }
        }

        /// <summary>
        /// Game/Images/World/ 폴더의 wall.png, floor.png, ceiling.png, Door.png 를 로드하여
        /// 벽 텍스처 슬롯, 바닥·천장·문 텍스처를 덮어쓴다.
        /// World 폴더에 파일이 없으면 기존 파일명으로 전체 Images 폴더를 검색한다.
        ///
        /// 슬롯 배분:
        ///   wallTextures[0] = ceiling.png (CeilingTextureIndex=0)
        ///   wallTextures[1..7] = wall.png (그다음 2·4·6·7 슬롯을 벽 종류 텍스처로 덮어씀)
        ///   wallTextures[3] = floor.png (FloorTextureIndex=3, wall 이후 덮어씀)
        ///   wallTextures[DoorTextureId] = Door.png (wall 이후 덮어씀)
        ///   doorOpenTexture = Door.png (별도 열린 문 텍스처; DoorOpen.png 우선)
        /// </summary>
        private void LoadWallTexturesFromFiles()
        {
            string worldDir = ResolveImageDirectory("World");

            Color[] wallTexture    = LoadWorldTexture(worldDir, "wall.png");
            Color[] floorTexture   = LoadWorldTexture(worldDir, "floor.png");
            Color[] ceilingTexture = LoadWorldTexture(worldDir, "ceiling.png");
            Color[] doorTexture    = LoadWorldTexture(worldDir, "Door.png");

            // 문 닫힘 폴백: World 폴더에 Door.png 가 없으면 DoorClose.png 를 검색
            if (doorTexture == null)
            {
                doorTexture = LoadWorldTextureFromFile("DoorClose.png");
            }

            // 문 열림 텍스처: DoorOpen.png 우선, 없으면 닫힘 텍스처를 재사용
            Color[] doorOpenData = LoadWorldTextureFromFile("DoorOpen.png");
            if (doorOpenData == null)
            {
                doorOpenData = doorTexture;
            }

            // wall.png → 슬롯 1..7 모두 덮어쓰기
            if (wallTexture != null)
            {
                for (int i = 1; i < wallTextures.Length; i++)
                {
                    Array.Copy(wallTexture, wallTextures[i], wallTexture.Length);
                }
            }

            // floor.png → 슬롯 3 (FloorTextureIndex = 3)
            if (floorTexture != null)
            {
                Array.Copy(floorTexture, wallTextures[3], floorTexture.Length);
            }

            // ceiling.png → 슬롯 0 (CeilingTextureIndex = 0)
            if (ceilingTexture != null)
            {
                Array.Copy(ceilingTexture, wallTextures[0], ceilingTexture.Length);
            }

            // Door.png → 슬롯 DoorTextureId (닫힌 문 텍스처)
            if (doorTexture != null)
            {
                Array.Copy(doorTexture, wallTextures[WorldConfig.DoorTextureId], doorTexture.Length);
            }

            // 벽 종류별 텍스처 → 각 슬롯 (없으면 wall.png 그대로)
            LoadWallVariant(worldDir, "wall_support.png", WorldConfig.WallTextureSupport);
            LoadWallVariant(worldDir, "wall_hell.png", WorldConfig.WallTextureHell);
            LoadWallVariant(worldDir, "wall_lab.png", WorldConfig.WallTextureLab);
            LoadWallVariant(worldDir, "wall_cover.png", WorldConfig.WallTextureCover);

            // 열린 문 텍스처 갱신
            if (doorOpenData != null)
            {
                Array.Copy(doorOpenData, doorOpenTexture, doorOpenData.Length);
            }
        }

        /// <summary>벽 종류 텍스처 한 장을 지정 슬롯에 덮어쓴다. 파일이 없으면 슬롯을 그대로 둔다.</summary>
        private void LoadWallVariant(string worldDir, string fileName, int slot)
        {
            Color[] texture = LoadWorldTexture(worldDir, fileName);
            if (texture != null && slot >= 0 && slot < wallTextures.Length)
            {
                Array.Copy(texture, wallTextures[slot], texture.Length);
            }
        }

        /// <summary>
        /// World 서브 디렉터리에서 먼저 파일을 찾고 없으면 Images 전체에서 검색한다.
        /// </summary>
        private Color[] LoadWorldTexture(string worldDir, string fileName)
        {
            if (worldDir != null)
            {
                string fullPath = Path.Combine(worldDir, fileName);
                if (File.Exists(fullPath))
                {
                    return LoadWorldTextureFromPath(fullPath);
                }
            }

            return LoadWorldTextureFromFile(fileName);
        }

        /// <summary>
        /// 무기 타입별 발사/재장전 스프라이트를 로드한다.
        /// Game/Images/Gun/ 하위 PNG를 우선 사용하고, 누락된 경우 절차적 폴백을 사용한다.
        /// </summary>
        private void LoadWeaponTypesFromGunFolders()
        {
            WeaponType[] weaponTypes = (WeaponType[])Enum.GetValues(typeof(WeaponType));
            for (int i = 0; i < weaponTypes.Length; i++)
            {
                if (!TryLoadWeaponTypeSpritesFromFiles(weaponTypes[i]))
                {
                    BuildProceduralWeaponTypeSprites(weaponTypes[i]);
                }
            }
        }

        private bool TryLoadWeaponTypeSpritesFromFiles(WeaponType type)
        {
            string sequenceSheetPath = ResolveWeaponSequenceSheetPath(type);
            if (sequenceSheetPath != null && TryLoadWeaponFireSheet(sequenceSheetPath, type))
            {
                return true;
            }

            string spritePath = ResolveWeaponSpritePath(type);
            if (string.IsNullOrWhiteSpace(spritePath))
            {
                return false;
            }

            // 단일 스프라이트 폴백
            Color[] spritePixels = LoadTextureFromPath(spritePath);
            if (spritePixels == null || spritePixels.Length == 0)
            {
                return false;
            }

            int typeIndex = (int)type;
            for (int frame = 0; frame < WeaponConfig.WeaponOverlayFrameCount; frame++)
            {
                SetWeaponOverlayFrame(typeIndex, frame, spritePixels);
            }

            return true;
        }

        private string ResolveWeaponSequenceSheetPath(WeaponType type)
        {
            string folderName = GetWeaponFolderName(type);
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return null;
            }

            string gunDirectory = ResolveImageDirectory("Gun", folderName);
            if (string.IsNullOrWhiteSpace(gunDirectory))
            {
                return null;
            }

            string[] candidates =
            {
                Path.Combine(gunDirectory, folderName + "_sprite_sheet.png")
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                if (File.Exists(candidates[i]))
                {
                    return candidates[i];
                }
            }

            return null;
        }

        private string ResolveWeaponSpritePath(WeaponType type)
        {
            string folderName = GetWeaponFolderName(type);
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return null;
            }

            string gunDirectory = ResolveImageDirectory("Gun", folderName);
            if (!string.IsNullOrWhiteSpace(gunDirectory))
            {
                string preferred = Path.Combine(gunDirectory, folderName + ".png");
                if (File.Exists(preferred))
                {
                    return preferred;
                }

                string[] candidates = Directory.GetFiles(gunDirectory, "*.png", SearchOption.TopDirectoryOnly);
                if (candidates.Length > 0)
                {
                    for (int i = 0; i < candidates.Length; i++)
                    {
                        string candidateName = Path.GetFileName(candidates[i]);
                        if (!candidateName.EndsWith("_sprite_sheet.png", StringComparison.OrdinalIgnoreCase) &&
                            !candidateName.EndsWith("_fire.png", StringComparison.OrdinalIgnoreCase))
                        {
                            return candidates[i];
                        }
                    }
                }
            }

            return ResolveImagePath(folderName + ".png");
        }

        private static string GetWeaponFolderName(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.AMPistol:
                    return "amp_pistol";
                case WeaponType.BearKiller:
                    return "bear_killer";
                case WeaponType.HChainGun:
                    return "h_chaingun";
                case WeaponType.AutoCannon:
                    return "auto_cannon";
                case WeaponType.DuelBerettas:
                    return "dual_berettas";
                default:
                    return null;
            }
        }

        private void BuildProceduralWeaponTypeSprites(WeaponType type)
        {
            int typeIndex = (int)type;
            (Color baseColor, Color accentColor) = GetWeaponPalette(type);
            int seed = (typeIndex + 3) * 97;

            for (int frame = 0; frame < WeaponConfig.WeaponOverlayFrameCount; frame++)
            {
                int fireFrameIndex = frame < WeaponConfig.WeaponIdleFrameCount
                    ? 0
                    : (frame - WeaponConfig.WeaponIdleFrameCount) + 1;
                Color[] pixels = BuildProceduralWeaponFrame(baseColor, accentColor, seed, fireFrameIndex, reloadPhase: -1f);
                SetWeaponOverlayFrame(typeIndex, frame, pixels);
            }
        }

        private void SetWeaponOverlayFrame(int typeIndex, int frameIndex, Color[] pixels)
        {
            weaponTypeFireImages[typeIndex][frameIndex]?.Dispose();
            weaponTypeFireImages[typeIndex][frameIndex] = BuildImageFromPixels(pixels);
        }

        private (Color baseColor, Color accentColor) GetWeaponPalette(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.AMPistol:
                    return (Color.FromArgb(58, 62, 60), Color.FromArgb(138, 136, 118));
                case WeaponType.BearKiller:
                    return (Color.FromArgb(72, 44, 24), Color.FromArgb(148, 108, 64));
                case WeaponType.HChainGun:
                    return (Color.FromArgb(56, 62, 36), Color.FromArgb(124, 138, 68));
                case WeaponType.AutoCannon:
                    return (Color.FromArgb(82, 70, 48), Color.FromArgb(172, 138, 84));
                case WeaponType.DuelBerettas:
                    return (Color.FromArgb(44, 48, 52), Color.FromArgb(104, 112, 122));
                default:
                    return (Color.FromArgb(58, 58, 56), Color.FromArgb(148, 138, 104));
            }
        }

        // Posterize step: 5~6 discrete levels per channel (simulate Doom's 256-color palette)
        private Color[] BuildProceduralWeaponFrame(
            Color baseColor,
            Color accentColor,
            int seed,
            int fireFrameIndex,
            float reloadPhase)
        {
            int size = RenderConfig.TextureSize;
            var pixels = new Color[size * size];

            float fireKick = fireFrameIndex == 0 ? 0f : 0.02f * fireFrameIndex;
            float flash = fireFrameIndex == 0 ? 0f : 0.25f + fireFrameIndex * 0.2f;
            float reloadShift = reloadPhase < 0f ? 0f : (reloadPhase - 0.5f) * 0.22f;
            float reloadLift = reloadPhase < 0f ? 0f : (float)Math.Sin(reloadPhase * Math.PI) * 0.10f;
            float randomTilt = ((seed % 17) - 8) * 0.0018f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int idx = y * size + x;
                    float nx = ((x / (float)(size - 1)) - 0.5f) * 2f;
                    float ny = ((y / (float)(size - 1)) - 0.5f) * 2f;

                    float px = nx + reloadShift + fireKick * 0.8f;
                    float py = ny - reloadLift + randomTilt * nx * 22f;

                    bool body = px > -0.24f && px < 0.24f && py > -0.12f && py < 0.90f;
                    bool barrel = px > -0.06f && px < 0.06f && py > -0.82f + fireKick && py < -0.10f;
                    bool grip = px > 0.04f && px < 0.20f && py > 0.20f && py < 0.96f;
                    bool detail = body && py > 0.08f && py < 0.16f;
                    bool muzzleFlash = flash > 0f &&
                        px > -(0.11f + flash * 0.16f) && px < (0.11f + flash * 0.16f) &&
                        py > -0.98f && py < (-0.84f + flash * 0.05f);

                    if (muzzleFlash)
                    {
                        int alpha = 210 + (int)(40 * Math.Min(1f, flash));
                        Color flashColor = Color.FromArgb(255, 255, 240, 180);
                        pixels[idx] = Color.FromArgb(alpha, flashColor);
                    }
                    else if (barrel || detail)
                    {
                        pixels[idx] = Color.FromArgb(255,
                            ClampToByte(baseColor.R * 1.08f),
                            ClampToByte(baseColor.G * 1.08f),
                            ClampToByte(baseColor.B * 1.08f));
                    }
                    else if (grip)
                    {
                        pixels[idx] = Color.FromArgb(255,
                            ClampToByte(accentColor.R * 0.9f),
                            ClampToByte(accentColor.G * 0.9f),
                            ClampToByte(accentColor.B * 0.9f));
                    }
                    else if (body)
                    {
                        float shade = 0.82f + (0.18f * (1f - Math.Abs(px) / 0.24f));
                        pixels[idx] = Color.FromArgb(255,
                            ClampToByte(baseColor.R * shade),
                            ClampToByte(baseColor.G * shade),
                            ClampToByte(baseColor.B * shade));
                    }
                    else
                    {
                        pixels[idx] = Color.Transparent;
                    }
                }
            }

            return pixels;
        }

        private Image BuildImageFromPixels(Color[] pixels)
        {
            if (pixels == null || pixels.Length == 0)
            {
                return null;
            }

            int size = RenderConfig.TextureSize;
            var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bitmap.SetPixel(x, y, pixels[y * size + x]);
                }
            }

            return bitmap;
        }

        /// <summary>
        /// 지정한 절대 경로의 이미지를 로드하고 TextureSize × TextureSize로 리샘플링하여
        /// 색상 배열로 반환한다. 픽셀 아트가 번지지 않게 최근접 보간을 사용한다.
        /// 경로가 null이거나 파일이 없으면 null을 반환한다.
        /// </summary>
        /// <param name="path">로드할 이미지의 절대 경로</param>
        /// <returns>TextureSize × TextureSize 크기로 리샘플링된 색상 배열. 실패 시 null.</returns>
        private Color[] LoadTextureFromPath(string path)
        {
            // 기본은 스프라이트/무기용 TextureSize. 월드 텍스처는 WorldTextureSize 오버로드를 쓴다.
            return LoadTextureFromPath(path, RenderConfig.TextureSize);
        }

        private Color[] LoadTextureFromPath(string path, int size)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            using (Bitmap source = new Bitmap(path))
            using (Bitmap scaled = new Bitmap(size, size))
            using (Graphics g = Graphics.FromImage(scaled))
            {
                g.Clear(Color.Transparent);
                // 그림은 모두 픽셀 아트라 최근접 보간으로 칸을 그대로 옮긴다.
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(source, 0, 0, size, size);
                return BitmapToColorArray(scaled);
            }
        }

        /// <summary>월드 벽/바닥/천장/문 텍스처를 WorldTextureSize 해상도로 로드한다.</summary>
        private Color[] LoadWorldTextureFromPath(string path)
        {
            return LoadTextureFromPath(path, RenderConfig.WorldTextureSize);
        }

        /// <summary>파일 이름을 해석해 월드 텍스처를 WorldTextureSize 해상도로 로드한다.</summary>
        private Color[] LoadWorldTextureFromFile(string fileName)
        {
            string resolvedPath = ResolveImagePath(fileName);
            return LoadWorldTextureFromPath(resolvedPath);
        }

        // ── 스프라이트 시트 로딩 ──────────────────────────────────────────────

        private static Color[] ExtractFrameFromSheet(Bitmap bmp, int col, int row, int cellSize, int outputSize)
        {
            int startX = col * cellSize;
            int startY = row * cellSize;
            Color[] pixels = new Color[outputSize * outputSize];

            using (Bitmap cell = new Bitmap(outputSize, outputSize, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(cell))
            {
                g.Clear(Color.Transparent);
                // 그림은 모두 픽셀 아트라 최근접 보간으로 칸을 그대로 옮긴다.
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(bmp,
                    new Rectangle(0, 0, outputSize, outputSize),
                    new Rectangle(startX, startY, cellSize, cellSize),
                    GraphicsUnit.Pixel);

                BitmapData bd = cell.LockBits(
                    new Rectangle(0, 0, outputSize, outputSize),
                    ImageLockMode.ReadOnly,
                    PixelFormat.Format32bppArgb);
                try
                {
                    byte[] raw = new byte[Math.Abs(bd.Stride) * outputSize];
                    Marshal.Copy(bd.Scan0, raw, 0, raw.Length);
                    for (int y = 0; y < outputSize; y++)
                    {
                        int rowStart = y * bd.Stride;
                        for (int x = 0; x < outputSize; x++)
                        {
                            int src = rowStart + x * 4;
                            pixels[y * outputSize + x] = Color.FromArgb(raw[src + 3], raw[src + 2], raw[src + 1], raw[src]);
                        }
                    }
                }
                finally
                {
                    cell.UnlockBits(bd);
                }
            }

            return pixels;
        }

        // ── 무기 파이어 시트 로딩 ────────────────────────────────────────────

        private bool TryLoadWeaponFireSheet(string path, WeaponType type)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            return TryLoadWeaponOverlaySequenceSheet(path, type);
        }

        private bool TryLoadWeaponOverlaySequenceSheet(string path, WeaponType type)
        {
            Bitmap bmp;
            try { bmp = new Bitmap(path); }
            catch { return false; }

            using (bmp)
            {
                int sourceFrameSize = bmp.Height;
                if (sourceFrameSize <= 0 ||
                    bmp.Width < sourceFrameSize * WeaponConfig.WeaponOverlayFrameCount)
                {
                    return false;
                }

                int typeIndex = (int)type;
                for (int frame = 0; frame < WeaponConfig.WeaponOverlayFrameCount; frame++)
                {
                    Color[] pixels = ExtractFrameFromSheet(bmp, frame, 0, sourceFrameSize, RenderConfig.TextureSize);
                    SetWeaponOverlayFrame(typeIndex, frame, pixels);
                }

                return true;
            }
        }

        /// <summary>
        /// <see cref="Bitmap"/> 객체에서 픽셀 데이터를 읽어 <see cref="Color"/>[] 배열로 변환한다.
        /// LockBits를 사용하여 비관리 메모리에서 직접 픽셀 바이트를 읽음으로써
        /// GetPixel 방식 대비 성능을 크게 향상시킨다.
        /// 입력 비트맵은 내부적으로 Format32bppArgb로 변환된 후 처리된다.
        /// </summary>
        /// <param name="bitmap">픽셀 데이터를 읽을 비트맵. null이면 빈 배열을 반환한다.</param>
        /// <returns>TextureSize × TextureSize 크기의 ARGB 색상 배열. bitmap이 null이면 빈 배열.</returns>
        private Color[] BitmapToColorArray(Bitmap bitmap)
        {
            if (bitmap == null)
            {
                return Array.Empty<Color>();
            }

            using (Bitmap argbBitmap = CreateArgbBitmapCopy(bitmap))
            {
                // 비트맵 실제 크기를 따라 읽는다. 스프라이트는 64², 월드 텍스처는 WorldTextureSize²로
                // 호출되므로 특정 크기를 가정하면(과거 64 하드코딩) 큰 비트맵의 일부만 읽혀
                // 나머지 슬롯이 폴백 패턴(빨간 체커보드 등)으로 남는다.
                int width = argbBitmap.Width;
                int height = argbBitmap.Height;
                Color[] data = new Color[width * height];
                Rectangle rect = new Rectangle(0, 0, width, height);
                BitmapData bitmapData = argbBitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

                try
                {
                    int byteCount = Math.Abs(bitmapData.Stride) * height;
                    byte[] raw = new byte[byteCount];
                    Marshal.Copy(bitmapData.Scan0, raw, 0, byteCount);

                    for (int y = 0; y < height; y++)
                    {
                        int rowStart = y * bitmapData.Stride;
                        for (int x = 0; x < width; x++)
                        {
                            int src = rowStart + (x * 4);
                            byte b = raw[src];
                            byte g = raw[src + 1];
                            byte r = raw[src + 2];
                            byte a = raw[src + 3];
                            data[y * width + x] = Color.FromArgb(a, r, g, b);
                        }
                    }
                }
                finally
                {
                    argbBitmap.UnlockBits(bitmapData);
                }

                return data;
            }
        }

        /// <summary>
        /// 원본 비트맵을 Format32bppArgb 형식의 새 비트맵으로 복사한다.
        /// <see cref="BitmapToColorArray"/>에서 LockBits 전에 포맷을 통일하기 위해 사용한다.
        /// 투명 배경에 원본을 그려 알파 채널도 올바르게 처리된다.
        /// </summary>
        /// <param name="source">복사할 원본 비트맵</param>
        /// <returns>Format32bppArgb 형식으로 변환된 새 <see cref="Bitmap"/> 인스턴스</returns>
        private static Bitmap CreateArgbBitmapCopy(Bitmap source)
        {
            var copy = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(copy))
            {
                g.Clear(Color.Transparent);
                g.DrawImage(source, 0, 0, source.Width, source.Height);
            }

            return copy;
        }
    }
}
