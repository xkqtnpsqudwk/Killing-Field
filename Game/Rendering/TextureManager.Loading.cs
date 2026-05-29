using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using My2DEngine.Game.Config;
using My2DEngine.Game;

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
            for (int y = 0; y < GameConfig.TextureSize; y++)
            {
                for (int x = 0; x < GameConfig.TextureSize; x++)
                {
                    int idx = y * GameConfig.TextureSize + x;

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

                    bool doorPanel = x < 6 || x > 57 || y < 4 || y > 59 || x == 31 || x == 32;
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
        ///   wallTextures[1..7] = wall.png
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
                Array.Copy(doorTexture, wallTextures[GameConfig.DoorTextureId], doorTexture.Length);
            }

            // 열린 문 텍스처 갱신
            if (doorOpenData != null)
            {
                Array.Copy(doorOpenData, doorOpenTexture, doorOpenData.Length);
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
            for (int frame = 0; frame < GameConfig.WeaponOverlayFrameCount; frame++)
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

            for (int frame = 0; frame < GameConfig.WeaponOverlayFrameCount; frame++)
            {
                int fireFrameIndex = frame < GameConfig.WeaponIdleFrameCount
                    ? 0
                    : (frame - GameConfig.WeaponIdleFrameCount) + 1;
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

        private enum DoomSpriteKind { Enemy, Boss, Weapon }

        // Posterize step: 5~6 discrete levels per channel (simulate Doom's 256-color palette)
        private const int DoomPosterizeStep = 52;

        private Color[] ApplyDoomStyleFilter(Color[] pixels, DoomSpriteKind kind)
        {
            if (pixels == null || pixels.Length == 0) return pixels;

            int size = (int)Math.Round(Math.Sqrt(pixels.Length));

            float satMix   = kind == DoomSpriteKind.Weapon ? 0.62f
                           : kind == DoomSpriteKind.Boss   ? 0.68f
                           : 0.74f;
            float contrast = kind == DoomSpriteKind.Boss   ? 1.45f
                           : kind == DoomSpriteKind.Weapon ? 1.28f
                           : 1.36f;
            int rShift = kind == DoomSpriteKind.Boss   ? 26
                       : kind == DoomSpriteKind.Enemy  ? 18
                       : -10;
            int gShift = kind == DoomSpriteKind.Boss   ? -10
                       : kind == DoomSpriteKind.Enemy  ?  -4
                       :  -6;
            int bShift = kind == DoomSpriteKind.Boss   ? -22
                       : kind == DoomSpriteKind.Enemy  ? -16
                       :   8;

            // Pass 1: opaque mask for edge detection
            bool[] opaque = new bool[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
                opaque[i] = pixels[i].A >= 20;

            var result = new Color[pixels.Length];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x;
                    Color c = pixels[i];
                    if (!opaque[i]) { result[i] = c; continue; }

                    // Edge → dark Doom outline
                    bool onEdge = (x > 0        && !opaque[i - 1])
                               || (x < size - 1 && !opaque[i + 1])
                               || (y > 0        && !opaque[i - size])
                               || (y < size - 1 && !opaque[i + size]);
                    if (onEdge)
                    {
                        result[i] = Color.FromArgb(c.A,
                            ClampToByte(c.R * 0.12f),
                            ClampToByte(c.G * 0.10f),
                            ClampToByte(c.B * 0.10f));
                        continue;
                    }

                    // Color grading
                    float gray = c.R * 0.299f + c.G * 0.587f + c.B * 0.114f;
                    float r = gray + (c.R - gray) * satMix + rShift;
                    float g = gray + (c.G - gray) * satMix + gShift;
                    float b = gray + (c.B - gray) * satMix + bShift;

                    r = (r - 128f) * contrast + 128f;
                    g = (g - 128f) * contrast + 128f;
                    b = (b - 128f) * contrast + 128f;

                    // Posterize → flat color blocks like Doom's 256-color palette
                    r = (float)Math.Round(r / DoomPosterizeStep) * DoomPosterizeStep;
                    g = (float)Math.Round(g / DoomPosterizeStep) * DoomPosterizeStep;
                    b = (float)Math.Round(b / DoomPosterizeStep) * DoomPosterizeStep;

                    result[i] = Color.FromArgb(c.A, ClampToByte(r), ClampToByte(g), ClampToByte(b));
                }
            }

            return result;
        }

        private void ApplyDoomFilterToAnimations(Dictionary<BossAnimationKind, Color[][]> animations, bool isBoss)
        {
            if (animations == null) return;
            DoomSpriteKind kind = isBoss ? DoomSpriteKind.Boss : DoomSpriteKind.Enemy;
            foreach (var kvp in animations)
            {
                Color[][] frames = kvp.Value;
                if (frames == null) continue;
                for (int i = 0; i < frames.Length; i++)
                    frames[i] = ApplyDoomStyleFilter(frames[i], kind);
            }
        }

        private Color[] BuildProceduralWeaponFrame(
            Color baseColor,
            Color accentColor,
            int seed,
            int fireFrameIndex,
            float reloadPhase)
        {
            int size = GameConfig.TextureSize;
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

            int size = GameConfig.TextureSize;
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
        /// 지정한 파일 이름으로 이미지를 로드하고 TextureSize × TextureSize로 리샘플링하여
        /// 색상 배열로 반환한다. 최근접 보간(NearestNeighbor)을 사용하여 픽셀 아트 스타일을 유지한다.
        /// 파일을 찾지 못하면 null을 반환한다.
        /// </summary>
        /// <param name="fileName">로드할 이미지 파일 이름 (예: "wall.png")</param>
        /// <returns>TextureSize × TextureSize 크기로 리샘플링된 색상 배열. 실패 시 null.</returns>
        private Color[] LoadTextureFromFile(string fileName)
        {
            string path = ResolveImagePath(fileName);
            if (path == null)
            {
                return null;
            }

            int size = GameConfig.TextureSize;
            using (Bitmap source = new Bitmap(path))
            using (Bitmap scaled = new Bitmap(size, size))
            using (Graphics g = Graphics.FromImage(scaled))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(source, 0, 0, size, size);
                return BitmapToColorArray(scaled);
            }
        }

        /// <summary>
        /// 지정한 파일 이름의 이미지를 로드하여 대상 색상 배열 버퍼에 복사한다.
        /// 로드 실패 시 또는 크기가 맞지 않으면 버퍼를 수정하지 않는다.
        /// </summary>
        /// <param name="fileName">로드할 이미지 파일 이름 (예: "Enemy01.png")</param>
        /// <param name="target">픽셀 데이터를 복사할 대상 색상 배열. TextureSize × TextureSize 크기여야 한다.</param>
        private void LoadSpriteIntoBuffer(string fileName, Color[] target)
        {
            Color[] loaded = LoadTextureFromFile(fileName);
            if (loaded == null || target == null || target.Length != loaded.Length)
            {
                return;
            }

            Array.Copy(loaded, target, loaded.Length);
        }

        /// <summary>
        /// 지정한 절대 경로의 이미지를 로드하고 TextureSize × TextureSize로 리샘플링하여
        /// 색상 배열로 반환한다. 고품질 바이큐빅 보간을 사용하여 스프라이트 품질을 유지한다.
        /// 경로가 null이거나 파일이 없으면 null을 반환한다.
        /// </summary>
        /// <param name="path">로드할 이미지의 절대 경로</param>
        /// <returns>TextureSize × TextureSize 크기로 리샘플링된 색상 배열. 실패 시 null.</returns>
        private Color[] LoadTextureFromPath(string path)
        {
            // 기본은 스프라이트/무기용 TextureSize. 월드 텍스처는 WorldTextureSize 오버로드를 쓴다.
            return LoadTextureFromPath(path, GameConfig.TextureSize);
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
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(source, 0, 0, size, size);
                return BitmapToColorArray(scaled);
            }
        }

        /// <summary>월드 벽/바닥/천장/문 텍스처를 WorldTextureSize 해상도로 로드한다.</summary>
        private Color[] LoadWorldTextureFromPath(string path)
        {
            return LoadTextureFromPath(path, GameConfig.WorldTextureSize);
        }

        /// <summary>파일 이름을 해석해 월드 텍스처를 WorldTextureSize 해상도로 로드한다.</summary>
        private Color[] LoadWorldTextureFromFile(string fileName)
        {
            string resolvedPath = ResolveImagePath(fileName);
            return LoadWorldTextureFromPath(resolvedPath);
        }

        // ── 스프라이트 시트 로딩 ──────────────────────────────────────────────

        private bool TryLoadAnimationSheet(string path, out Dictionary<BossAnimationKind, Color[][]> animations)
        {
            animations = null;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            int sz = GameConfig.TextureSize;
            Bitmap bmp;
            try { bmp = new Bitmap(path); }
            catch { return false; }

            using (bmp)
            {
                if (!TryInferAnimationSheetGrid(bmp, out int cols, out int rows, out int cellSize))
                {
                    return false;
                }

                animations = new Dictionary<BossAnimationKind, Color[][]>();
                Color[][][] gridFrames = new Color[rows][][];
                int[][] gridOccupancy = new int[rows][];
                for (int row = 0; row < rows; row++)
                {
                    Color[][] rowFrames = new Color[cols][];
                    int[] rowOccupancy = new int[cols];
                    for (int col = 0; col < cols; col++)
                    {
                        Color[] frame = ExtractFrameFromSheet(bmp, col, row, cellSize, sz);
                        rowFrames[col] = frame;
                        rowOccupancy[col] = CountOpaquePixels(frame);
                    }

                    gridFrames[row] = rowFrames;
                    gridOccupancy[row] = rowOccupancy;
                }

                PopulateAnimationRowsFromGrid(animations, gridFrames, gridOccupancy);

                if (animations.TryGetValue(BossAnimationKind.Death, out Color[][] deathFrames) &&
                    deathFrames != null &&
                    deathFrames.Length > 0)
                {
                    animations[BossAnimationKind.Spawn] = deathFrames;
                }

                if (!animations.ContainsKey(BossAnimationKind.Special) &&
                    animations.TryGetValue(BossAnimationKind.Attack, out Color[][] attackFrames) &&
                    attackFrames != null &&
                    attackFrames.Length > 0)
                {
                    animations[BossAnimationKind.Special] = attackFrames;
                }

                return animations.Count > 0;
            }
        }

        private static bool TryInferAnimationSheetGrid(Bitmap bmp, out int cols, out int rows, out int cellSize)
        {
            cols = 0;
            rows = 0;
            cellSize = 0;
            if (bmp == null || bmp.Width <= 0 || bmp.Height <= 0)
            {
                return false;
            }

            int[] candidateCellSizes = { 512, 384, 320, 256, 192, 160, 128, 96, 80, 64 };
            for (int i = 0; i < candidateCellSizes.Length; i++)
            {
                int candidate = candidateCellSizes[i];
                if (candidate <= 0 || bmp.Width % candidate != 0 || bmp.Height % candidate != 0)
                {
                    continue;
                }

                int candidateCols = bmp.Width / candidate;
                int candidateRows = bmp.Height / candidate;
                if (candidateCols >= 4 && candidateCols <= 6 && candidateRows >= 4 && candidateRows <= 6)
                {
                    cols = candidateCols;
                    rows = candidateRows;
                    cellSize = candidate;
                    return true;
                }
            }

            if (bmp.Width % GameConfig.TextureSize == 0 && bmp.Height % GameConfig.TextureSize == 0)
            {
                cols = Math.Max(1, bmp.Width / GameConfig.TextureSize);
                rows = Math.Max(1, bmp.Height / GameConfig.TextureSize);
                cellSize = GameConfig.TextureSize;
                return true;
            }

            return false;
        }

        private static void PopulateAnimationRowsFromGrid(
            Dictionary<BossAnimationKind, Color[][]> animations,
            Color[][][] gridFrames,
            int[][] gridOccupancy)
        {
            if (animations == null || gridFrames == null || gridOccupancy == null)
            {
                return;
            }

            int rowCount = Math.Min(gridFrames.Length, gridOccupancy.Length);
            if (rowCount <= 0)
            {
                return;
            }

            if (rowCount >= 6)
            {
                SetAnimationRow(animations, BossAnimationKind.Idle, gridFrames, gridOccupancy, 0, false);
                SetAnimationRow(animations, BossAnimationKind.Move, gridFrames, gridOccupancy, 1, false);
                SetAnimationRow(animations, BossAnimationKind.Attack, gridFrames, gridOccupancy, 2, false);
                SetAnimationRow(animations, BossAnimationKind.Special, gridFrames, gridOccupancy, 3, false);
                SetAnimationRowsCombined(animations, BossAnimationKind.Death, gridFrames, gridOccupancy, 4, rowCount - 1, true);
                return;
            }

            if (rowCount == 5)
            {
                SetAnimationRow(animations, BossAnimationKind.Idle, gridFrames, gridOccupancy, 0, false);
                SetAnimationRow(animations, BossAnimationKind.Move, gridFrames, gridOccupancy, 1, false);
                SetAnimationRow(animations, BossAnimationKind.Attack, gridFrames, gridOccupancy, 2, false);
                SetAnimationRow(animations, BossAnimationKind.Special, gridFrames, gridOccupancy, 3, false);
                SetAnimationRow(animations, BossAnimationKind.Death, gridFrames, gridOccupancy, 4, true);
                return;
            }

            SetAnimationRow(animations, BossAnimationKind.Idle, gridFrames, gridOccupancy, 0, false);
            SetAnimationRow(animations, BossAnimationKind.Move, gridFrames, gridOccupancy, Math.Min(1, rowCount - 1), false);
            SetAnimationRow(animations, BossAnimationKind.Attack, gridFrames, gridOccupancy, Math.Min(2, rowCount - 1), false);
            SetAnimationRow(animations, BossAnimationKind.Death, gridFrames, gridOccupancy, rowCount - 1, true);
        }

        private static void SetAnimationRow(
            Dictionary<BossAnimationKind, Color[][]> animations,
            BossAnimationKind kind,
            Color[][][] gridFrames,
            int[][] gridOccupancy,
            int rowIndex,
            bool isDeathRow)
        {
            if (rowIndex < 0 || rowIndex >= gridFrames.Length || rowIndex >= gridOccupancy.Length)
            {
                return;
            }

            Color[][] frames = TrimAnimationFrames(gridFrames[rowIndex], gridOccupancy[rowIndex], isDeathRow);
            if (frames.Length > 0)
            {
                animations[kind] = frames;
            }
        }

        private static void SetAnimationRowsCombined(
            Dictionary<BossAnimationKind, Color[][]> animations,
            BossAnimationKind kind,
            Color[][][] gridFrames,
            int[][] gridOccupancy,
            int startRow,
            int endRow,
            bool isDeathRow)
        {
            if (animations == null || gridFrames == null || gridOccupancy == null)
            {
                return;
            }

            startRow = Math.Max(0, startRow);
            endRow = Math.Min(Math.Min(gridFrames.Length, gridOccupancy.Length) - 1, endRow);
            if (startRow > endRow)
            {
                return;
            }

            var frames = new List<Color[]>();
            var occupancy = new List<int>();
            for (int row = startRow; row <= endRow; row++)
            {
                if (gridFrames[row] == null || gridOccupancy[row] == null)
                {
                    continue;
                }

                frames.AddRange(gridFrames[row]);
                occupancy.AddRange(gridOccupancy[row]);
            }

            Color[][] combined = TrimAnimationFrames(frames.ToArray(), occupancy.ToArray(), isDeathRow);
            if (combined.Length > 0)
            {
                animations[kind] = combined;
            }
        }

        private static Color[][] TrimAnimationFrames(Color[][] frames, int[] occupancy, bool isDeathRow)
        {
            if (frames == null || occupancy == null || frames.Length == 0 || occupancy.Length == 0)
            {
                return Array.Empty<Color[]>();
            }

            int usableLength = Math.Min(frames.Length, occupancy.Length);
            int maxOccupancy = 0;
            for (int i = 0; i < usableLength; i++)
            {
                if (occupancy[i] > maxOccupancy)
                {
                    maxOccupancy = occupancy[i];
                }
            }

            if (maxOccupancy <= 0)
            {
                return Array.Empty<Color[]>();
            }

            int threshold = isDeathRow
                ? Math.Max(8, (int)Math.Round(maxOccupancy * 0.05f))
                : Math.Max(16, (int)Math.Round(maxOccupancy * 0.25f));

            int lastActiveIndex = -1;
            for (int i = 0; i < usableLength; i++)
            {
                if (occupancy[i] >= threshold)
                {
                    lastActiveIndex = i;
                }
            }

            if (lastActiveIndex < 0)
            {
                lastActiveIndex = 0;
            }

            var trimmed = new List<Color[]>();
            for (int i = 0; i <= lastActiveIndex; i++)
            {
                if (frames[i] != null && frames[i].Length > 0)
                {
                    trimmed.Add(frames[i]);
                }
            }

            return trimmed.ToArray();
        }

        private static int CountOpaquePixels(Color[] pixels)
        {
            if (pixels == null || pixels.Length == 0)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].A > 8)
                {
                    count++;
                }
            }

            return count;
        }

        private static Color[] ExtractFrameFromSheet(Bitmap bmp, int col, int row, int cellSize, int outputSize)
        {
            int startX = col * cellSize;
            int startY = row * cellSize;
            Color[] pixels = new Color[outputSize * outputSize];

            using (Bitmap cell = new Bitmap(outputSize, outputSize, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(cell))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
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
                    bmp.Width < sourceFrameSize * GameConfig.WeaponOverlayFrameCount)
                {
                    return false;
                }

                int typeIndex = (int)type;
                for (int frame = 0; frame < GameConfig.WeaponOverlayFrameCount; frame++)
                {
                    Color[] pixels = ExtractFrameFromSheet(bmp, frame, 0, sourceFrameSize, GameConfig.TextureSize);
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
                int size = GameConfig.TextureSize;
                Color[] data = new Color[size * size];
                Rectangle rect = new Rectangle(0, 0, size, size);
                BitmapData bitmapData = argbBitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

                try
                {
                    int byteCount = Math.Abs(bitmapData.Stride) * size;
                    byte[] raw = new byte[byteCount];
                    Marshal.Copy(bitmapData.Scan0, raw, 0, byteCount);

                    for (int y = 0; y < size; y++)
                    {
                        int rowStart = y * bitmapData.Stride;
                        for (int x = 0; x < size; x++)
                        {
                            int src = rowStart + (x * 4);
                            byte b = raw[src];
                            byte g = raw[src + 1];
                            byte r = raw[src + 2];
                            byte a = raw[src + 3];
                            data[y * size + x] = Color.FromArgb(a, r, g, b);
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

        /// <summary>
        /// 지정한 경로의 이미지 파일을 <see cref="FileStream"/>으로 열어
        /// GDI+ <see cref="Image"/>로 로드한다.
        /// <see cref="FileShare.ReadWrite"/>를 지정하여 다른 프로세스와의 파일 잠금 충돌을 방지한다.
        /// 반환된 이미지는 스트림이 닫힌 후에도 독립적으로 사용 가능한 복사본이다.
        /// </summary>
        /// <param name="path">로드할 이미지 파일의 절대 경로</param>
        /// <returns>파일에서 로드된 <see cref="Image"/> 인스턴스 (호출자가 Dispose해야 함)</returns>
        private static Image LoadStandaloneImage(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (Image source = Image.FromStream(stream))
            {
                return new Bitmap(source);
            }
        }
    }
}
