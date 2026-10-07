using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using My2DEngine.Game.Config;

namespace My2DEngine.Game.Rendering
{
    /// <summary>
    /// 적/보스 절차적 스프라이트와 픽업 스프라이트를 구성하는 partial 클래스다.
    /// </summary>
    public partial class TextureManager
    {
        private readonly struct ProceduralVariantDescriptor
        {
            public ProceduralVariantDescriptor(EnemyBehaviorPattern pattern, bool bossPool, float scaleBias)
            {
                Pattern = pattern;
                BossPool = bossPool;
                ScaleBias = scaleBias;
            }

            public EnemyBehaviorPattern Pattern { get; }
            public bool BossPool { get; }
            public float ScaleBias { get; }
        }

        // ── 적/보스 절차적 스프라이트 ─────────────────────────────────────────

        private void LoadEnemySpritesFromFiles()
        {
            RegisterImageBackedVariantsForCatalog(loadBossVariants: false);
            RegisterProceduralVariantsForCatalog(loadBossVariants: false);
        }

        private void LoadBossSpriteSheets()
        {
            RegisterImageBackedVariantsForCatalog(loadBossVariants: true);
            RegisterProceduralVariantsForCatalog(loadBossVariants: true);
        }

        private void RegisterImageBackedVariantsForCatalog(bool loadBossVariants)
        {
            string imageGroup = loadBossVariants ? "Bosses" : "Enemy";
            EnemyArchetype[] archetypes = EnemyCatalog.GetAllArchetypes();
            for (int i = 0; i < archetypes.Length; i++)
            {
                EnemyArchetype archetype = archetypes[i];
                if (archetype == null)
                {
                    continue;
                }

                bool bossPool = archetype.Rank == EnemyRank.Boss;
                if (bossPool != loadBossVariants)
                {
                    continue;
                }

                // 그림은 Game/Images/<Enemy|Bosses>/<이름>/{IDLE,MOVE,ATTACK,DEATH}/*.png (Tools/ArtGen이 만든다).
                // 폴더가 없으면 건너뛰고, 뒤이어 등록하는 절차적 스프라이트가 대신 그린다.
                string entityDir = ResolveEntityImageDirectory(imageGroup, archetype);
                if (string.IsNullOrWhiteSpace(entityDir))
                {
                    continue;
                }

                BossSpriteSheet sheet = TryBuildSheetFromSubfolders(entityDir, bossPool);
                Color[] idleSprite = sheet?.GetFrame(BossAnimationKind.Idle, 0f);
                if (idleSprite != null && idleSprite.Length > 0)
                {
                    RegisterAllAliases(archetype, bossPool, idleSprite, sheet);
                }
            }
        }

        private void RegisterAllAliases(EnemyArchetype archetype, bool bossPool, Color[] sprite, BossSpriteSheet sheet)
        {
            RegisterLoadedSpriteVariant(archetype.SpriteVariantKey, bossPool, sprite, sheet);
            RegisterLoadedSpriteVariant(archetype.AssetId, bossPool, sprite, sheet);

            string[] variantPool = archetype.SpriteVariantKeyPool;
            if (variantPool == null)
            {
                return;
            }

            for (int v = 0; v < variantPool.Length; v++)
            {
                RegisterLoadedSpriteVariant(variantPool[v], bossPool, sprite, sheet);
            }
        }

        private string ResolveEntityImageDirectory(string imageGroup, EnemyArchetype archetype)
        {
            var searchKeys = new List<string>();
            AddUniqueSearchKey(searchKeys, archetype.SpriteVariantKey);
            AddUniqueSearchKey(searchKeys, archetype.AssetId);

            string[] variantPool = archetype.SpriteVariantKeyPool;
            if (variantPool != null)
            {
                for (int i = 0; i < variantPool.Length; i++)
                {
                    AddUniqueSearchKey(searchKeys, variantPool[i]);
                }
            }

            for (int i = 0; i < searchKeys.Count; i++)
            {
                foreach (string candidate in EnumerateVariantSearchKeys(searchKeys[i]))
                {
                    string dir = ResolveImageDirectory(imageGroup, candidate);
                    if (!string.IsNullOrWhiteSpace(dir))
                    {
                        return dir;
                    }
                }
            }

            return null;
        }

        private BossSpriteSheet TryBuildSheetFromSubfolders(string entityDir, bool bossPool)
        {
            string[] folderNames = { "IDLE", "MOVE", "ATTACK", "DEATH" };
            BossAnimationKind[] kinds = { BossAnimationKind.Idle, BossAnimationKind.Move, BossAnimationKind.Attack, BossAnimationKind.Death };

            bool hasAny = false;
            for (int i = 0; i < folderNames.Length; i++)
            {
                if (Directory.Exists(Path.Combine(entityDir, folderNames[i])))
                {
                    hasAny = true;
                    break;
                }
            }

            if (!hasAny)
            {
                return null;
            }

            float baseFps = bossPool ? 5.0f : 6.5f;
            float[] defaultFps =
            {
                baseFps,
                baseFps + 1.5f,
                baseFps + 3.5f,
                Math.Max(3.0f, baseFps - 1.5f)
            };

            var animations = new Dictionary<BossAnimationKind, Color[][]>();
            var fpsMap = new Dictionary<BossAnimationKind, float>();

            for (int i = 0; i < folderNames.Length; i++)
            {
                string subDir = Path.Combine(entityDir, folderNames[i]);
                if (!Directory.Exists(subDir))
                {
                    continue;
                }

                string[] pngFiles = Directory.GetFiles(subDir, "*.png", SearchOption.TopDirectoryOnly);
                if (pngFiles.Length == 0)
                {
                    continue;
                }

                Array.Sort(pngFiles, StringComparer.OrdinalIgnoreCase);

                var frames = new List<Color[]>();
                for (int f = 0; f < pngFiles.Length; f++)
                {
                    Color[] frame = LoadTextureFromPath(pngFiles[f]);
                    if (frame != null && frame.Length > 0)
                    {
                        frames.Add(frame);
                    }
                }

                if (frames.Count > 0)
                {
                    animations[kinds[i]] = frames.ToArray();
                    fpsMap[kinds[i]] = defaultFps[i];
                }
            }

            if (animations.Count == 0)
            {
                return null;
            }

            if (animations.TryGetValue(BossAnimationKind.Death, out Color[][] deathFrames))
            {
                animations[BossAnimationKind.Spawn] = deathFrames;
                fpsMap[BossAnimationKind.Spawn] = fpsMap[BossAnimationKind.Death];
            }

            if (!animations.ContainsKey(BossAnimationKind.Special) &&
                animations.TryGetValue(BossAnimationKind.Attack, out Color[][] attackFrames))
            {
                animations[BossAnimationKind.Special] = attackFrames;
                fpsMap[BossAnimationKind.Special] = fpsMap[BossAnimationKind.Attack];
            }

            return new BossSpriteSheet(animations, fpsMap);
        }

        private static void AddUniqueSearchKey(List<string> searchKeys, string key)
        {
            if (searchKeys == null || string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            for (int i = 0; i < searchKeys.Count; i++)
            {
                if (string.Equals(searchKeys[i], key, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            searchKeys.Add(key);
        }

        private static IEnumerable<string> EnumerateVariantSearchKeys(string variantKey)
        {
            if (string.IsNullOrWhiteSpace(variantKey))
            {
                yield break;
            }

            string current = variantKey.Trim();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (!string.IsNullOrWhiteSpace(current))
            {
                if (visited.Add(current))
                {
                    yield return current;
                }

                int splitIndex = current.LastIndexOf('_');
                if (splitIndex <= 0)
                {
                    break;
                }

                current = current.Substring(0, splitIndex);
            }
        }

        private void RegisterLoadedSpriteVariant(string variantKey, bool bossPool, Color[] sprite, BossSpriteSheet sheet)
        {
            if (string.IsNullOrWhiteSpace(variantKey) || sprite == null)
            {
                return;
            }

            if (!enemySpriteVariants.ContainsKey(variantKey))
            {
                enemySpriteVariants[variantKey] = sprite;
            }

            if (sheet != null && !bossSpriteSheets.ContainsKey(variantKey))
            {
                bossSpriteSheets[variantKey] = sheet;
            }

            RegisterVariantPoolKey(variantKey, bossPool);
        }

        private void EnsureEnemyVariantPoolsLoaded()
        {
            if (normalEnemyVariantKeys.Count == 0)
            {
                RegisterProceduralVariant("procedural_normal_default", false, EnemyBehaviorPattern.Default, 1f);
            }

            if (bossEnemyVariantKeys.Count == 0)
            {
                RegisterProceduralVariant("procedural_boss_default", true, EnemyBehaviorPattern.BossTanker, 1.35f);
            }
        }

        private void RegisterProceduralVariantsForCatalog(bool loadBossVariants)
        {
            EnemyArchetype[] archetypes = EnemyCatalog.GetAllArchetypes();
            for (int i = 0; i < archetypes.Length; i++)
            {
                EnemyArchetype archetype = archetypes[i];
                if (archetype == null)
                {
                    continue;
                }

                bool bossPool = archetype.Rank == EnemyRank.Boss;
                if (bossPool != loadBossVariants)
                {
                    continue;
                }

                EnemyBehaviorPattern pattern = archetype.DefaultBehaviorPattern;
                float scaleBias = archetype.Definition?.Scale ?? 1f;
                RegisterProceduralVariant(archetype.SpriteVariantKey, bossPool, pattern, scaleBias);

                string[] variantPool = archetype.SpriteVariantKeyPool;
                if (variantPool == null)
                {
                    continue;
                }

                for (int v = 0; v < variantPool.Length; v++)
                {
                    RegisterProceduralVariant(variantPool[v], bossPool, pattern, scaleBias);
                }
            }
        }

        private void RegisterProceduralVariant(
            string variantKey,
            bool bossPool,
            EnemyBehaviorPattern pattern,
            float scaleBias)
        {
            string normalizedVariantKey = NormalizeVariantKey(variantKey, bossPool, pattern);
            RegisterProceduralVariantDescriptor(normalizedVariantKey, bossPool, pattern, scaleBias);
            RegisterVariantPoolKey(normalizedVariantKey, bossPool);
        }

        private static string NormalizeVariantKey(string variantKey, bool bossPool, EnemyBehaviorPattern pattern)
        {
            if (!string.IsNullOrWhiteSpace(variantKey))
            {
                return variantKey;
            }

            return (bossPool ? "boss_" : "enemy_") + pattern.ToString().ToLowerInvariant();
        }

        private void RegisterProceduralVariantDescriptor(
            string variantKey,
            bool bossPool,
            EnemyBehaviorPattern pattern,
            float scaleBias)
        {
            if (string.IsNullOrWhiteSpace(variantKey))
            {
                return;
            }

            if (proceduralVariantDescriptors.ContainsKey(variantKey))
            {
                return;
            }

            proceduralVariantDescriptors[variantKey] = new ProceduralVariantDescriptor(pattern, bossPool, scaleBias);
        }

        private bool EnsureProceduralVariantLoaded(string variantKey)
        {
            if (string.IsNullOrWhiteSpace(variantKey))
            {
                return false;
            }

            if (bossSpriteSheets.TryGetValue(variantKey, out BossSpriteSheet existingSheet) && existingSheet != null)
            {
                if (!enemySpriteVariants.TryGetValue(variantKey, out Color[] cachedIdle) || cachedIdle == null)
                {
                    cachedIdle = existingSheet.GetFrame(BossAnimationKind.Idle, 0f);
                    if (cachedIdle != null)
                    {
                        enemySpriteVariants[variantKey] = cachedIdle;
                    }
                }

                return true;
            }

            if (!proceduralVariantDescriptors.TryGetValue(variantKey, out ProceduralVariantDescriptor descriptor))
            {
                return false;
            }

            BossSpriteSheet sheet = BuildProceduralSpriteSheet(
                descriptor.Pattern,
                descriptor.BossPool,
                descriptor.ScaleBias,
                variantKey);
            return RegisterAnimationSet(variantKey, sheet);
        }

        private BossSpriteSheet BuildProceduralSpriteSheet(
            EnemyBehaviorPattern pattern,
            bool bossPool,
            float scaleBias,
            string variantKey)
        {
            int seed = BuildSeedFromKey(variantKey);
            float baseFps = bossPool ? 5.2f : 6.4f;

            if (pattern == EnemyBehaviorPattern.BossRunner ||
                pattern == EnemyBehaviorPattern.BossRiftBlitz ||
                pattern == EnemyBehaviorPattern.BossArachnoFang ||
                pattern == EnemyBehaviorPattern.HellionSwarm)
            {
                baseFps += 1.2f;
            }
            else if (pattern == EnemyBehaviorPattern.BossTanker ||
                pattern == EnemyBehaviorPattern.BossBehemoth ||
                pattern == EnemyBehaviorPattern.BossAnnihilator ||
                pattern == EnemyBehaviorPattern.BossSpiderQueen)
            {
                baseFps -= 1f;
            }

            Color[][] idleFrames = BuildProceduralFrames(pattern, bossPool, BossAnimationKind.Idle, 4, scaleBias, seed);
            Color[][] moveFrames = BuildProceduralFrames(pattern, bossPool, BossAnimationKind.Move, 6, scaleBias, seed);
            Color[][] attackFrames = BuildProceduralFrames(pattern, bossPool, BossAnimationKind.Attack, 4, scaleBias, seed);
            Color[][] specialFrames = BuildProceduralFrames(pattern, bossPool, BossAnimationKind.Special, 5, scaleBias, seed);
            Color[][] deathFrames = BuildProceduralFrames(pattern, bossPool, BossAnimationKind.Death, 6, scaleBias, seed);

            var animations = new Dictionary<BossAnimationKind, Color[][]>
            {
                [BossAnimationKind.Idle] = idleFrames,
                [BossAnimationKind.Move] = moveFrames,
                [BossAnimationKind.Attack] = attackFrames,
                [BossAnimationKind.Special] = specialFrames,
                [BossAnimationKind.Death] = deathFrames,
                [BossAnimationKind.Spawn] = deathFrames
            };

            var fpsMap = new Dictionary<BossAnimationKind, float>
            {
                [BossAnimationKind.Idle] = baseFps,
                [BossAnimationKind.Move] = baseFps + 1.1f,
                [BossAnimationKind.Attack] = baseFps + 2f,
                [BossAnimationKind.Special] = baseFps + 1.3f,
                [BossAnimationKind.Death] = Math.Max(3.2f, baseFps - 1.2f),
                [BossAnimationKind.Spawn] = Math.Max(3.2f, baseFps - 1.2f)
            };

            return new BossSpriteSheet(animations, fpsMap);
        }

        private Color[][] BuildProceduralFrames(
            EnemyBehaviorPattern pattern,
            bool bossPool,
            BossAnimationKind kind,
            int frameCount,
            float scaleBias,
            int seed)
        {
            if (frameCount <= 0)
            {
                return Array.Empty<Color[]>();
            }

            var frames = new Color[frameCount][];
            for (int i = 0; i < frameCount; i++)
            {
                frames[i] = BuildProceduralFrame(pattern, bossPool, kind, i, frameCount, scaleBias, seed);
            }

            return frames;
        }

        private Color[] BuildProceduralFrame(
            EnemyBehaviorPattern pattern,
            bool bossPool,
            BossAnimationKind kind,
            int frameIndex,
            int frameCount,
            float scaleBias,
            int seed)
        {
            int size = RenderConfig.TextureSize;
            var pixels = new Color[size * size];

            (Color primary, Color accent) = GetPatternPalette(pattern, bossPool, seed);
            float phase      = frameCount <= 1 ? 0f : frameIndex / (float)(frameCount - 1);
            float basePulse  = (float)Math.Sin((frameIndex + (seed % 11)) * (Math.PI / 3.0));
            float moveBob    = kind == BossAnimationKind.Move    ? basePulse * 0.055f : 0f;
            float sway       = kind == BossAnimationKind.Move    ? basePulse * 0.07f  : 0f;
            float attackBurst  = kind == BossAnimationKind.Attack  ? (float)Math.Sin((phase + 0.08f) * Math.PI) * 0.12f : 0f;
            float specialBurst = kind == BossAnimationKind.Special ? (float)Math.Sin((phase + 0.05f) * Math.PI) * 0.15f : 0f;
            float deathFade  = kind == BossAnimationKind.Death   ? Math.Max(0.05f, 1f - phase) : 1f;

            float normalizedScale = Math.Max(0.72f, Math.Min(1.55f, scaleBias <= 0f ? 1f : scaleBias));
            float bodyRadiusX = (bossPool ? 0.34f : 0.27f) * normalizedScale;
            float bodyRadiusY = (bossPool ? 0.46f : 0.36f) * normalizedScale;
            float headRadiusX = bodyRadiusX * 0.66f;
            float headRadiusY = bodyRadiusY * 0.52f;
            float auraSize    = attackBurst + specialBurst;

            float eyeLineY   = bossPool ? -0.20f : -0.16f;
            float eyeWidth   = bossPool ? 0.09f  : 0.07f;
            float eyeOffsetX = bossPool ? 0.15f  : 0.11f;

            // ── Pass 1: Build zone mask ─────────────────────────────────────
            // 0=transparent  1=body  2=head  3=horn  4=eye  5=aura
            var zoneMask = new byte[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int idx = y * size + x;
                    float nx = ((x / (float)(size - 1)) - 0.5f) * 2f;
                    float ny = ((y / (float)(size - 1)) - 0.5f) * 2f;
                    float px = nx - sway;
                    float py = ny - moveBob;

                    float bodyEq = (px * px) / (bodyRadiusX * bodyRadiusX)
                                 + (py * py) / (bodyRadiusY * bodyRadiusY);
                    float hx = px;
                    float hy = py + bodyRadiusY * 0.62f;
                    float headEq = (hx * hx) / (headRadiusX * headRadiusX)
                                 + (hy * hy) / (headRadiusY * headRadiusY);

                    bool insideBody = bodyEq <= 1f;
                    bool insideHead = headEq <= 1f;
                    bool isHorn = bossPool && py < -0.08f && py > -0.44f &&
                        ((px > 0.14f && px < 0.32f) || (px < -0.14f && px > -0.32f));
                    float auraEq = (px * px) / ((bodyRadiusX + auraSize + 0.03f) * (bodyRadiusX + auraSize + 0.03f))
                                 + (py * py) / ((bodyRadiusY + auraSize + 0.03f) * (bodyRadiusY + auraSize + 0.03f));
                    bool inAura = auraSize > 0.01f && auraEq <= 1f && !insideBody && !insideHead && !isHorn;

                    bool leftEye  = (insideHead || insideBody) &&
                        py > eyeLineY - 0.03f && py < eyeLineY + 0.05f &&
                        px > -(eyeOffsetX + eyeWidth) && px < -(eyeOffsetX - eyeWidth * 0.2f);
                    bool rightEye = (insideHead || insideBody) &&
                        py > eyeLineY - 0.03f && py < eyeLineY + 0.05f &&
                        px > (eyeOffsetX - eyeWidth * 0.2f) && px < (eyeOffsetX + eyeWidth);

                    if      (leftEye || rightEye) zoneMask[idx] = 4;
                    else if (isHorn)              zoneMask[idx] = 3;
                    else if (insideHead)          zoneMask[idx] = 2;
                    else if (insideBody)          zoneMask[idx] = 1;
                    else if (inAura)              zoneMask[idx] = 5;
                }
            }

            // ── Pass 2: Color with Doom-style flat shading + dark outlines ──
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int idx  = y * size + x;
                    byte zone = zoneMask[idx];

                    if (zone == 0) { pixels[idx] = Color.Transparent; continue; }

                    int alphaInt = (int)(255f * deathFade);
                    float nx = ((x / (float)(size - 1)) - 0.5f) * 2f;
                    float ny = ((y / (float)(size - 1)) - 0.5f) * 2f;
                    float px = nx - sway;

                    // Aura glow
                    if (zone == 5)
                    {
                        Color glow = Blend(accent, Color.White, 0.35f + specialBurst * 0.6f);
                        pixels[idx] = Color.FromArgb((int)(82f * deathFade), glow);
                        continue;
                    }

                    // Glowing eyes — classic Doom yellow-orange
                    if (zone == 4)
                    {
                        Color eyeColor = kind == BossAnimationKind.Death
                            ? Blend(Color.FromArgb(220, 60, 20), Color.Black, phase * 0.85f)
                            : Color.FromArgb(255, 200 + (int)(attackBurst * 55f), 60);
                        pixels[idx] = Color.FromArgb(alphaInt, eyeColor);
                        continue;
                    }

                    // Dark outline: any solid pixel touching transparency
                    bool onEdge = (x > 0        && zoneMask[idx - 1]    == 0)
                               || (x < size - 1 && zoneMask[idx + 1]    == 0)
                               || (y > 0        && zoneMask[idx - size]  == 0)
                               || (y < size - 1 && zoneMask[idx + size]  == 0);
                    if (onEdge)
                    {
                        pixels[idx] = Color.FromArgb(alphaInt,
                            ClampChannel((int)(primary.R * 0.13f + 3)),
                            ClampChannel((int)(primary.G * 0.11f + 2)),
                            ClampChannel((int)(primary.B * 0.09f + 2)));
                        continue;
                    }

                    // Flat directional shading (light from upper-left, Doom convention)
                    // px > 0 → right/shadow side; px < 0 → left/highlight side
                    float rawShade = 0.75f - px * 0.40f - (ny - moveBob) * 0.12f;

                    // Pixel noise — simulates hand-painted Doom texture
                    int noise = ((x * 7 + y * 13 + (seed % 97) * 3) % 23) - 11;
                    rawShade += noise * 0.013f;

                    // Quantize to 4 flat levels (Doom's limited-palette feel)
                    float shadeLevel;
                    if      (rawShade > 0.74f) shadeLevel = 1.00f;   // highlight
                    else if (rawShade > 0.48f) shadeLevel = 0.64f;   // mid-light
                    else if (rawShade > 0.24f) shadeLevel = 0.36f;   // mid-shadow
                    else                       shadeLevel = 0.16f;   // deep shadow

                    // Horns: slightly brighter than body
                    if (zone == 3) shadeLevel = Math.Min(1f, shadeLevel + 0.14f);

                    // Accent blending: highlights pick up accent colour, shadows stay primary
                    float accentMix = shadeLevel >= 1.0f ? 0.58f
                                    : shadeLevel >= 0.64f ? 0.28f
                                    : 0.06f;
                    accentMix += attackBurst * 0.45f + specialBurst * 0.65f;
                    Color bodyColor = Blend(primary, accent, Math.Min(1f, accentMix));

                    int r = ClampChannel((int)(bodyColor.R * shadeLevel));
                    int g = ClampChannel((int)(bodyColor.G * shadeLevel));
                    int b = ClampChannel((int)(bodyColor.B * shadeLevel));

                    if (kind == BossAnimationKind.Death)
                    {
                        float deathGray = r * 0.299f + g * 0.587f + b * 0.114f;
                        float dm = 0.44f + phase * 0.46f;
                        r = ClampChannel((int)(r * (1f - dm) + deathGray * 0.28f * dm));
                        g = ClampChannel((int)(g * (1f - dm) + deathGray * 0.28f * dm));
                        b = ClampChannel((int)(b * (1f - dm) + deathGray * 0.28f * dm));
                    }

                    pixels[idx] = Color.FromArgb(alphaInt, r, g, b);
                }
            }

            return pixels;
        }

        private static int BuildSeedFromKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return 17;
            }

            unchecked
            {
                int hash = 23;
                string lower = key.ToLowerInvariant();
                for (int i = 0; i < lower.Length; i++)
                {
                    hash = (hash * 31) + lower[i];
                }

                return Math.Abs(hash);
            }
        }

        private static (Color primary, Color accent) GetPatternPalette(EnemyBehaviorPattern pattern, bool bossPool, int seed)
        {
            Color baseColor;
            Color accentColor;

            switch (pattern)
            {
                // Zombie soldier — olive military green
                case EnemyBehaviorPattern.ZombieGunner:
                case EnemyBehaviorPattern.Kite:
                case EnemyBehaviorPattern.Strafe:
                    baseColor = Color.FromArgb(72, 88, 44);
                    accentColor = Color.FromArgb(138, 148, 76);
                    break;
                // Pinky / Spectre — fleshy brownish-pink
                case EnemyBehaviorPattern.BlindCharger:
                case EnemyBehaviorPattern.BloodPhantom:
                    baseColor = Color.FromArgb(168, 92, 64);
                    accentColor = Color.FromArgb(220, 148, 98);
                    break;
                // Cacodemon / heavy ranged — blood red
                case EnemyBehaviorPattern.BeamSniper:
                case EnemyBehaviorPattern.BossCortex:
                case EnemyBehaviorPattern.BossArackBaron:
                    baseColor = Color.FromArgb(148, 36, 28);
                    accentColor = Color.FromArgb(220, 72, 44);
                    break;
                // Imp / swarm — burnt sienna
                case EnemyBehaviorPattern.SlimeLobber:
                case EnemyBehaviorPattern.HellionSwarm:
                    baseColor = Color.FromArgb(122, 70, 32);
                    accentColor = Color.FromArgb(198, 128, 56);
                    break;
                // Baron of Hell — dark olive + hellfire
                case EnemyBehaviorPattern.BossFlameLord:
                case EnemyBehaviorPattern.BossDarkLord:
                case EnemyBehaviorPattern.BossBeast:
                    baseColor = Color.FromArgb(76, 36, 20);
                    accentColor = Color.FromArgb(208, 58, 24);
                    break;
                // Cyberdemon — iron gray + deep crimson
                case EnemyBehaviorPattern.BossAfritBomber:
                case EnemyBehaviorPattern.BossRiftBlitz:
                    baseColor = Color.FromArgb(52, 48, 44);
                    accentColor = Color.FromArgb(196, 36, 28);
                    break;
                // Spider Mastermind / heavy tanker — dark gunmetal
                case EnemyBehaviorPattern.BossBehemoth:
                case EnemyBehaviorPattern.BossAnnihilator:
                case EnemyBehaviorPattern.BossSpiderQueen:
                case EnemyBehaviorPattern.BossTanker:
                    baseColor = Color.FromArgb(56, 54, 48);
                    accentColor = Color.FromArgb(144, 130, 106);
                    break;
                // Arch-vile / fast boss — pale bone
                case EnemyBehaviorPattern.BossArachnoFang:
                case EnemyBehaviorPattern.BossAgathoDemon:
                case EnemyBehaviorPattern.BossRunner:
                    baseColor = Color.FromArgb(176, 150, 112);
                    accentColor = Color.FromArgb(238, 216, 168);
                    break;
                default:
                    baseColor = bossPool
                        ? Color.FromArgb(96, 58, 36)
                        : Color.FromArgb(80, 70, 42);
                    accentColor = bossPool
                        ? Color.FromArgb(200, 108, 56)
                        : Color.FromArgb(158, 144, 88);
                    break;
            }

            int tintRange = bossPool ? 18 : 24;
            baseColor = ApplyVariantTint(baseColor, seed, tintRange);
            accentColor = ApplyVariantTint(accentColor, seed / 3, tintRange + 6);
            return (baseColor, accentColor);
        }

        private static Color ApplyVariantTint(Color color, int seed, int magnitude)
        {
            int signedSeed = (seed % 97) - 48;
            int r = ClampChannel(color.R + (signedSeed * magnitude) / 96);
            int g = ClampChannel(color.G + ((signedSeed / 2) * magnitude) / 96);
            int b = ClampChannel(color.B - (signedSeed * magnitude) / 128);
            return Color.FromArgb(255, r, g, b);
        }

        private static Color Blend(Color from, Color to, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            int r = ClampChannel((int)(from.R + (to.R - from.R) * t));
            int g = ClampChannel((int)(from.G + (to.G - from.G) * t));
            int b = ClampChannel((int)(from.B + (to.B - from.B) * t));
            return Color.FromArgb(255, r, g, b);
        }

        private static int ClampChannel(int value)
        {
            if (value < 0) return 0;
            if (value > 255) return 255;
            return value;
        }

        private void RegisterVariantPoolKey(string variantKey, bool bossPool)
        {
            if (string.IsNullOrWhiteSpace(variantKey))
            {
                return;
            }

            List<string> target = bossPool ? bossEnemyVariantKeys : normalEnemyVariantKeys;
            for (int i = 0; i < target.Count; i++)
            {
                if (string.Equals(target[i], variantKey, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            target.Add(variantKey);
        }

        // ── 보상 아이템 픽업 스프라이트 ──────────────────────────────────────

        private void LoadPickupSprites()
        {
            pickupSprites.Clear();
            RegisterPickupSprite(RewardPickupKind.AmmoPack, "Item", "AmmoPack.png");
            RegisterPickupSprite(RewardPickupKind.Coin, "Item", "Coin.png");
            RegisterPickupSprite(RewardPickupKind.Card, "Item", "Card.png");
            if (!pickupSprites.ContainsKey(RewardPickupKind.AmmoPack))
            {
                pickupSprites[RewardPickupKind.AmmoPack] = BuildFallbackAmmoPickupSprite();
            }
            if (!pickupSprites.ContainsKey(RewardPickupKind.Coin))
            {
                pickupSprites[RewardPickupKind.Coin] = BuildFallbackCoinPickupSprite();
            }
            if (!pickupSprites.ContainsKey(RewardPickupKind.Card))
            {
                pickupSprites[RewardPickupKind.Card] = BuildFallbackCardPickupSprite();
            }
        }

        private void RegisterPickupSprite(RewardPickupKind kind, params string[] pathSegments)
        {
            string fullPath = null;
            string itemDirectory = ResolveImageDirectory("Item");
            if (itemDirectory != null)
            {
                string candidatePath = Path.Combine(itemDirectory, pathSegments[^1]);
                if (File.Exists(candidatePath))
                {
                    fullPath = candidatePath;
                }
            }

            Color[] sprite = LoadTextureFromPath(fullPath);
            if (sprite != null)
            {
                pickupSprites[kind] = sprite;
            }
        }

        private Color[] BuildFallbackAmmoPickupSprite()
            => BuildWeaponAmmoSprite(Color.FromArgb(255, 255, 210, 120));

        private Color[] BuildFallbackCardPickupSprite()
        {
            int size = RenderConfig.TextureSize;
            Color[] sprite = new Color[size * size];
            Color edge = Color.FromArgb(255, 255, 226, 122);
            Color face = Color.FromArgb(255, 46, 58, 84);
            Color stripe = Color.FromArgb(255, 128, 205, 255);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int idx = y * size + x;
                    float nx = ((x / (float)(size - 1)) - 0.5f) * 2f;
                    float ny = ((y / (float)(size - 1)) - 0.5f) * 2f;
                    float skewX = nx + ny * 0.16f;
                    bool body = skewX > -0.48f && skewX < 0.48f && ny > -0.62f && ny < 0.62f;
                    if (!body)
                    {
                        sprite[idx] = Color.Transparent;
                        continue;
                    }

                    bool border = skewX < -0.40f || skewX > 0.40f || ny < -0.54f || ny > 0.54f;
                    bool band = ny > -0.12f && ny < 0.06f && skewX > -0.32f && skewX < 0.32f;
                    bool pip = (skewX * skewX + (ny + 0.30f) * (ny + 0.30f)) < 0.035f;
                    sprite[idx] = border ? edge : band || pip ? stripe : face;
                }
            }

            return sprite;
        }

        private Color[] BuildFallbackCoinPickupSprite()
        {
            int size = RenderConfig.TextureSize;
            Color[] sprite = new Color[size * size];
            Color rim = Color.FromArgb(255, 255, 238, 150);
            Color fill = Color.FromArgb(255, 214, 164, 56);
            Color shine = Color.FromArgb(255, 255, 248, 210);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int idx = y * size + x;
                    float nx = ((x / (float)(size - 1)) - 0.5f) * 2f;
                    float ny = ((y / (float)(size - 1)) - 0.5f) * 2f;
                    float distSq = (nx * nx) + (ny * ny);
                    if (distSq > 0.62f)
                    {
                        sprite[idx] = Color.Transparent;
                        continue;
                    }

                    bool edge = distSq > 0.48f;
                    bool highlight = nx < -0.15f && ny < -0.1f && distSq < 0.34f;
                    sprite[idx] = highlight ? shine : edge ? rim : fill;
                }
            }

            return sprite;
        }

        private Color[] BuildWeaponAmmoSprite(Color accentColor)
        {
            int size = RenderConfig.TextureSize;
            Color[] sprite = new Color[size * size];
            Color crateColor = Color.FromArgb(255,
                (int)(accentColor.R * 0.55f),
                (int)(accentColor.G * 0.55f),
                (int)(accentColor.B * 0.55f));

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int idx = y * size + x;
                    float u = x / (float)(size - 1);
                    float v = y / (float)(size - 1);

                    bool crate = u > 0.16f && u < 0.84f && v > 0.26f && v < 0.84f;
                    bool band = crate && v > 0.42f && v < 0.56f;
                    bool shells = v > 0.08f && v < 0.36f &&
                        ((u > 0.24f && u < 0.36f) || (u > 0.44f && u < 0.56f) || (u > 0.64f && u < 0.76f));

                    if (shells)
                    {
                        sprite[idx] = v < 0.16f ? Color.FromArgb(255, 240, 220, 150) : accentColor;
                    }
                    else if (crate)
                    {
                        sprite[idx] = band ? accentColor : crateColor;
                    }
                    else
                    {
                        sprite[idx] = Color.Transparent;
                    }
                }
            }

            return sprite;
        }

        // ── 공통 등록 로직 ────────────────────────────────────────────────────

        private bool RegisterAnimationSet(string variantKey, BossSpriteSheet sheet)
        {
            if (sheet == null || string.IsNullOrWhiteSpace(variantKey))
            {
                return false;
            }

            Color[] idleFrame = sheet.GetFrame(BossAnimationKind.Idle, 0f);
            if (idleFrame == null)
            {
                return false;
            }

            bossSpriteSheets[variantKey] = sheet;
            enemySpriteVariants[variantKey] = idleFrame;
            return true;
        }

        // ── 애니메이션 상태 판별 ──────────────────────────────────────────────

        private BossAnimationKind GetEnemyAnimationKind(Enemy enemy)
        {
            if (enemy == null)
            {
                return BossAnimationKind.Idle;
            }

            if (!enemy.Alive)
            {
                return BossAnimationKind.Death;
            }

            if (enemy.IsSpawning)
            {
                return BossAnimationKind.Spawn;
            }

            switch (enemy.State)
            {
                case EnemyAiState.Stunned:
                    return BossAnimationKind.Special;
                case EnemyAiState.AttackWindup:
                case EnemyAiState.AttackRecover:
                    return BossAnimationKind.Attack;
            }

            if (enemy.AttackAnimationTimer > 0f)
            {
                return BossAnimationKind.Attack;
            }

            if (IsMovementState(enemy) || (enemy.State == EnemyAiState.Combat && IsAggressivePattern(enemy.BehaviorPattern)))
            {
                return BossAnimationKind.Move;
            }

            return BossAnimationKind.Idle;
        }

        private static bool IsMovementState(Enemy enemy)
        {
            if (enemy == null)
            {
                return false;
            }

            if (enemy.RecentMovementTime > 0.01f || enemy.WanderTimer > 0.01f)
            {
                return true;
            }

            float dirSq = (enemy.MoveDirX * enemy.MoveDirX) + (enemy.MoveDirY * enemy.MoveDirY);
            if (dirSq > 0.0016f)
            {
                return true;
            }

            if ((enemy.State == EnemyAiState.Patrol ||
                enemy.State == EnemyAiState.Investigate ||
                enemy.State == EnemyAiState.Search) &&
                enemy.IdleTimer <= 0f)
            {
                return true;
            }

            return false;
        }

        private static bool IsAggressivePattern(EnemyBehaviorPattern pattern)
        {
            switch (pattern)
            {
                case EnemyBehaviorPattern.Juggernaut:
                case EnemyBehaviorPattern.Rushdown:
                case EnemyBehaviorPattern.Pouncer:
                case EnemyBehaviorPattern.Skirmisher:
                case EnemyBehaviorPattern.BlindCharger:
                case EnemyBehaviorPattern.BloodPhantom:
                case EnemyBehaviorPattern.HellionSwarm:
                case EnemyBehaviorPattern.BossBeast:
                case EnemyBehaviorPattern.BossRunner:
                case EnemyBehaviorPattern.BossFlameLord:
                case EnemyBehaviorPattern.BossRiftBlitz:
                case EnemyBehaviorPattern.BossDarkLord:
                case EnemyBehaviorPattern.BossArachnoFang:
                    return true;
                default:
                    return false;
            }
        }
    }
}
