using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using My2DEngine.Engine.Rendering.Abstractions;
using My2DEngine.Game;
using My2DEngine.Game.Config;
using My2DEngine.Game.Map;
using My2DEngine.Game.Rendering;
using My2DEngine.Game.Systems;

namespace My2DEngine.Rendering.WorldData
{
    /// <summary>
    /// GPU ?¤í”„?¼ì´???¬ì˜ ?”ë²„ê·?ì¶œë ¥??êµ¬ì¡°ì²?
    /// ê°??¤í”„?¼ì´?¸ì˜ ?”ë©´ ì¢Œí‘œ, ?¬ê¸°, ê¹Šì´, ?„í??¼ìŠ¤ ?¬ë¡¯???´ëŠ”??
    /// </summary>
    internal struct SpriteDebugProjection
    {
        /// <summary>?¤í”„?¼ì´?¸ì˜ ?”ë“œ ?¤ë¸Œ?íŠ¸ ì¢…ë¥˜(?? ?¬ì‚¬ì²? ?½ì—… ??.</summary>
        public WorldSpriteKind Kind;

        /// <summary>?¤í”„?¼ì´??ì¤‘ì‹¬???”ë©´ X ì¢Œí‘œ(?½ì?).</summary>
        public float X;

        /// <summary>?¤í”„?¼ì´??ì¤‘ì‹¬???”ë©´ Y ì¢Œí‘œ(?½ì?).</summary>
        public float Y;

        /// <summary>?¤í”„?¼ì´?¸ì˜ ?”ë©´ ?ˆë¹„(?½ì?).</summary>
        public float Width;

        /// <summary>?¤í”„?¼ì´?¸ì˜ ?”ë©´ ?’ì´(?½ì?).</summary>
        public float Height;

        /// <summary>?¤í”„?¼ì´?¸ì˜ ?”ë“œ ê³µê°„ ê¹Šì´(?¬ì˜ ê±°ë¦¬).</summary>
        public float Depth;

        /// <summary>???¤í”„?¼ì´?¸ê? ? ë‹¹???„í??¼ìŠ¤ ?¬ë¡¯ ?¸ë±??</summary>
        public int AtlasSlot;
    }

    /// <summary>
    /// ?¤í”„?¼ì´???¨ìŠ¤ ë¹Œë“œ ê²°ê³¼ë¥??´ëŠ” êµ¬ì¡°ì²?
    /// GPU ?Œë”?¬ì— ?„ë‹¬???¤í”„?¼ì´???¸ìŠ¤?´ìŠ¤ ë°°ì—´ê³??„í??¼ìŠ¤ ?•ë³´ë¥??¬í•¨?œë‹¤.
    /// </summary>
    internal struct SpritePassBuildResult
    {
        /// <summary>?´ë²ˆ ?¨ìŠ¤?ì„œ ?Œë”ë§í•  ?¤í”„?¼ì´???¸ìŠ¤?´ìŠ¤ ë°°ì—´.</summary>
        public WorldSpriteInstance[] Sprites;

        /// <summary>? íš¨???¤í”„?¼ì´???¸ìŠ¤?´ìŠ¤ ??</summary>
        public int SpriteCount;

        /// <summary>?¤í”„?¼ì´???ìŠ¤ì²??„í??¼ìŠ¤??ARGB ?½ì? ë°°ì—´.</summary>
        public int[] AtlasPixels;

        /// <summary>?„í??¼ìŠ¤ ?´ë?ì§€???ˆë¹„(?½ì?).</summary>
        public int AtlasWidth;

        /// <summary>?„í??¼ìŠ¤ ?´ë?ì§€???’ì´(?½ì?).</summary>
        public int AtlasHeight;

        /// <summary>?„í??¼ìŠ¤ ?„ì²´ë¥?GPU???¬ì—…ë¡œë“œ?´ì•¼ ?˜ë©´ true. ?ˆì´?„ì›ƒ??ë³€ê²½ëœ ê²½ìš° ?¤ì •?œë‹¤.</summary>
        public bool UploadFullAtlas;

        /// <summary>ë³€ê²½ëœ ?„í??¼ìŠ¤ ?€ ?ì—­???˜í??´ëŠ” ?•ìˆ˜ ë°°ì—´(x, y, width, height ?œìœ¼ë¡?4ê°œì”©).</summary>
        public int[] DirtyRects;

        /// <summary>DirtyRects??ê¸°ë¡???”í‹° ?¬ê°?•ì˜ ??</summary>
        public int DirtyRectCount;
    }

    /// <summary>
    /// ?„ì¬ ?”ë“œ ?íƒœë¥?GPU world presenterê°€ ?´í•´?????ˆëŠ” RenderWorldCommand ?°ì´?°ë¡œ ë³€?˜í•œ??
    /// ë²???column) ì§€?¤ë©”?¸ë¦¬, ê¹Šì´ ë²„í¼, ?Â·íˆ¬?¬ì²´Â·?½ì—… ?¤í”„?¼ì´???„í??¼ìŠ¤, ?ˆì´?€ ë¹??¸ìŠ¤?´ìŠ¤ë¥?
    /// ë§??„ë ˆ??ì¡°ë¦½?˜ì—¬ TryBuildë¡?ë°˜í™˜?œë‹¤.
    /// </summary>
    internal sealed class WorldRenderDataBuilder
    {
        /// <summary>?¤í”„?¼ì´???€(?ìŠ¤ì²? ?¬ê¸°(?½ì?). GameConfig.TextureSize?€ ?™ì¼?˜ë‹¤.</summary>
        private const int SpriteCellSize = GameConfig.TextureSize;

        /// <summary>?„í??¼ìŠ¤ ?€ ì£¼ë???ì¶”ê??˜ëŠ” ?¨ë”© ?½ì? ?? ê²½ê³„ ë¸”ë¦¬??ë°©ì???</summary>
        private const int SpriteAtlasPadding = 1;

        /// <summary>?„í??¼ìŠ¤ ??ê°??€???¤ì œ ?¤íŠ¸?¼ì´??SpriteCellSize + ?¨ë”© * 2).</summary>
        private const int SpriteAtlasCellStride = SpriteCellSize + (SpriteAtlasPadding * 2);

        /// <summary>ë§??°ì´??ë°?ë¬??íƒœë¥??œê³µ?˜ëŠ” ë§¤ë‹ˆ?€.</summary>
        private readonly MapManager mapManager;

        /// <summary>?ìŠ¤ì²?ë°??¤í”„?¼ì´???½ì? ?°ì´?°ë? ?œê³µ?˜ëŠ” ë§¤ë‹ˆ?€.</summary>
        private readonly TextureManager textureManager;

        /// <summary>??ë°??¬ì‚¬ì²?ëª©ë¡???œê³µ?˜ëŠ” ë§¤ë‹ˆ?€.</summary>
        private readonly EnemyManager enemyManager;

        /// <summary>?´ë²ˆ ?„ë ˆ?„ì˜ ???¤í”„?¼ì´??ë¹Œë“œ ??ª© ëª©ë¡. ë§??„ë ˆ???¬ì‚¬?©ëœ??</summary>
        private readonly List<SpriteBuildEntry> enemySpriteEntries = new(48);

        /// <summary>?´ë²ˆ ?„ë ˆ?„ì˜ ê¸°í? ?¤í”„?¼ì´???¬ì‚¬ì²´Â·í”½?? ë¹Œë“œ ??ª© ëª©ë¡. ë§??„ë ˆ???¬ì‚¬?©ëœ??</summary>
        private readonly List<SpriteBuildEntry> miscSpriteEntries = new(64);

        /// <summary>?´ë²ˆ ?„ë ˆ?„ì˜ ?ˆì´?€ ë¹?ë¹Œë“œ ??ª© ëª©ë¡. ë§??„ë ˆ???¬ì‚¬?©ëœ??</summary>
        private readonly List<BeamBuildEntry> beamEntries = new(16);

        /// <summary>ComputeVisibleSegments ?¬ì‚¬??ë²„í¼. ë§??¸ì¶œë§ˆë‹¤ Clear ???¬ìš©?œë‹¤.</summary>
        private readonly List<(int start, int end)> _segmentBuffer = new(8);

        /// <summary>GPUë¡??„ì†¡?˜ëŠ” ?´ë³„ ?ì‹œ ê¹Šì´ ë²„í¼(float, renderWidth ?¬ê¸°).</summary>
        private float[] depthBufferData;

        /// <summary>CPU Â±3 min-filterê°€ ?ìš©??GPU ?„ìš© ê¹Šì´ ë²„í¼. ?¤í”„?¼ì´??ì°¨í ?ì •???¬ìš©?œë‹¤.</summary>
        private float[] gpuDepthBufferData;

        /// <summary>
        /// ê°??”ë©´ ?´ì˜ ë²?ì§€?¤ë©”?¸ë¦¬ë¥??´ëŠ” ë²„í¼.
        /// ?´ë‹¹ 4ê°œì˜ float(perpWallDist, fullDrawStart, drawStart, drawEnd)?¼ë¡œ êµ¬ì„±?œë‹¤.
        /// </summary>
        private float[] wallColumnGeometryBuffer;

        /// <summary>
        /// ê°??”ë©´ ?´ì˜ ë²??¬ì§ˆ???´ëŠ” ë²„í¼.
        /// ?´ë‹¹ 4ê°œì˜ int(textureId, texX, side, tileType)?¼ë¡œ êµ¬ì„±?œë‹¤.
        /// </summary>
        private int[] wallColumnMaterialBuffer;

        /// <summary>ê°??”ë©´ ?´ì˜ ë¬?door) ê°œë°© ì§„í–‰?„ë? ?´ëŠ” ë²„í¼(float, renderWidth ?¬ê¸°).</summary>
        private float[] wallColumnDoorProgressBuffer;

        /// <summary>???¤í”„?¼ì´???¸ìŠ¤?´ìŠ¤ ë°°ì—´. BuildSpritePass?ì„œ ?¬ì‚¬?©ëœ??</summary>
        private WorldSpriteInstance[] enemySpriteBuffer;

        /// <summary>ê¸°í? ?¤í”„?¼ì´???¸ìŠ¤?´ìŠ¤ ë°°ì—´. BuildSpritePass?ì„œ ?¬ì‚¬?©ëœ??</summary>
        private WorldSpriteInstance[] miscSpriteBuffer;

        /// <summary>?ˆì´?€ ë¹??¸ìŠ¤?´ìŠ¤ ë°°ì—´. BuildBeamBuffer?ì„œ ?¬ì‚¬?©ëœ??</summary>
        private WorldBeamInstance[] beamBuffer;

        /// <summary>???¤í”„?¼ì´???„í??¼ìŠ¤ ìºì‹œ. ?¬ë¡¯ ? ë‹¹ê³??½ì? ?”í‹° ?¬ë?ë¥?ì¶”ì ?œë‹¤.</summary>
        private readonly SpriteAtlasCache enemySpriteAtlasCache = new();

        /// <summary>ê¸°í? ?¤í”„?¼ì´???„í??¼ìŠ¤ ìºì‹œ. ?¬ë¡¯ ? ë‹¹ê³??½ì? ?”í‹° ?¬ë?ë¥?ì¶”ì ?œë‹¤.</summary>
        private readonly SpriteAtlasCache miscSpriteAtlasCache = new();

        /// <summary>?¤í”„?¼ì´???”ë²„ê·??¬ì˜ ?•ë³´ ë°°ì—´. DebugSprites ?„ë¡œ?¼í‹°ë¥??µí•´ ?¸ë????¸ì¶œ?œë‹¤.</summary>
        private SpriteDebugProjection[] spriteDebugBuffer;

        /// <summary>?´ë²ˆ ?„ë ˆ?„ì— ê¸°ë¡???¤í”„?¼ì´???”ë²„ê·???ª© ??</summary>
        private int spriteDebugCount;

        /// <summary>?°ì„± êµ¬ì²´(AcidGlob) ?¬ì‚¬ì²??¤í”„?¼ì´???½ì? ë°°ì—´. ìµœì´ˆ ?”ì²­ ???ì„±?œë‹¤.</summary>
        private Color[] acidGlobSprite;

        /// <summary>?°ì„± ?…ë©??AcidPool) ?¬ì‚¬ì²??¤í”„?¼ì´???½ì? ë°°ì—´. ìµœì´ˆ ?”ì²­ ???ì„±?œë‹¤.</summary>
        private Color[] acidPoolSprite;

        /// <summary>?¼ë°˜ ???„í™˜(EnemyShot) ?¤í”„?¼ì´???½ì? ë°°ì—´. ìµœì´ˆ ?”ì²­ ???ì„±?œë‹¤.</summary>
        private Color[] enemyShotSprite;

        /// <summary>ë³´ìŠ¤ ë¡œì¼“(BossRocket) ?¬ì‚¬ì²??¤í”„?¼ì´???½ì? ë°°ì—´. ìµœì´ˆ ?”ì²­ ???ì„±?œë‹¤.</summary>
        private Color[] bossRocketSprite;

        /// <summary>?Œë ˆ?´ì–´ ë¡œì¼“ ??°œ(PlayerRocketExplosion) ?¤í”„?¼ì´???½ì? ë°°ì—´. ìµœì´ˆ ?”ì²­ ???ì„±?œë‹¤.</summary>
        private Color[] playerRocketExplosionSprite;

        /// <summary>
        /// WorldRenderDataBuilderë¥?ì´ˆê¸°?”í•œ??
        /// </summary>
        /// <param name="mapManager">ë§??°ì´??ë°?ë¬??íƒœë¥??œê³µ?˜ëŠ” ë§¤ë‹ˆ?€.</param>
        /// <param name="textureManager">?ìŠ¤ì²?ë°??¤í”„?¼ì´???½ì????œê³µ?˜ëŠ” ë§¤ë‹ˆ?€.</param>
        /// <param name="enemyManager">??ë°??¬ì‚¬ì²?ëª©ë¡???œê³µ?˜ëŠ” ë§¤ë‹ˆ?€.</param>
        public WorldRenderDataBuilder(MapManager mapManager, TextureManager textureManager, EnemyManager enemyManager)
        {
            this.mapManager = mapManager;
            this.textureManager = textureManager;
            this.enemyManager = enemyManager;
        }

        /// <summary>
        /// ?„ì¬ ?Œë ˆ?´ì–´ ì¹´ë©”?¼ì? ?”ë“œ ?íƒœë¥?ê¸°ë°˜?¼ë¡œ GPU ?Œë” ì»¤ë§¨?œë? ì¡°ë¦½?œë‹¤.
        /// ë²???ì§€?¤ë©”?¸ë¦¬ ë¹Œë“œ ??ê¹Šì´ ë²„í¼ ?¤ë¬´????CPU ?¤ë²„?ˆì´??ê¹Šì´ ë³µì‚¬ ??
        /// ?¤í”„?¼ì´???°ì´??ë¹Œë“œ ?œìœ¼ë¡?ì²˜ë¦¬????RenderWorldCommandë¥?ë°˜í™˜?œë‹¤.
        /// </summary>
        /// <param name="player">ì¹´ë©”???„ì¹˜Â·ë°©í–¥Â·?¬ì˜ ?‰ë©´???œê³µ?˜ëŠ” ?Œë ˆ?´ì–´ ?íƒœ.</param>
        /// <param name="depthBuffer">CPU ?¤ë²„?ˆì´ê°€ ì°¨í ?ì •???¬ìš©?˜ëŠ” ê¹Šì´ ë²„í¼(double[]). ???¨ìˆ˜?ì„œ ê°±ì‹ ?œë‹¤.</param>
        /// <param name="rewardPickups">?”ë“œ??ë°°ì¹˜??ë³´ìƒ ?½ì—… ëª©ë¡.</param>
        /// <param name="playerProjectiles">?”ë“œ??ë°°ì¹˜???Œë ˆ?´ì–´ ?¬ì‚¬ì²?ëª©ë¡.</param>
        /// <param name="renderWidth">?Œë” ?€???ˆë¹„(?½ì?).</param>
        /// <param name="renderHeight">?Œë” ?€???’ì´(?½ì?).</param>
        /// <param name="command">ë¹Œë“œ??GPU ?Œë” ì»¤ë§¨?? ?¤íŒ¨ ??ê¸°ë³¸ê°?</param>
        /// <returns>ì»¤ë§¨?œê? ?±ê³µ?ìœ¼ë¡?ë¹Œë“œ?˜ë©´ true, ?„ìˆ˜ ?°ì´?°ê? ?†ê±°??ë§µì´ ë¹„ì–´ ?ˆìœ¼ë©?false.</returns>
        public bool TryBuild(Player player, double[] depthBuffer, IList<RewardPickup> rewardPickups, IList<EnemyProjectile> playerProjectiles, int renderWidth, int renderHeight, out RenderWorldCommand command)
        {
            command = default;
            spriteDebugCount = 0;
            if (player == null || mapManager == null || textureManager == null)
            {
                return false;
            }

            int[,] map = mapManager.Map;
            int[,] textureIds = mapManager.TextureIds;
            if (map == null || textureIds == null)
            {
                return false;
            }

            int mapWidth = map.GetLength(0);
            int mapHeight = map.GetLength(1);
            if (mapWidth <= 0 || mapHeight <= 0)
            {
                return false;
            }

            GetGpuWorldTargetSize(renderWidth, renderHeight, out int worldTargetWidth, out int worldTargetHeight);
            BuildWallColumns(player, map, textureIds, mapWidth, mapHeight, worldTargetWidth, worldTargetHeight);
            BuildSmoothedDepthBuffer();
            CopyDepthBufferForCpuOverlays(depthBuffer, renderWidth, worldTargetWidth);
            int[] wallAtlasPixels = textureManager.GetWallTextureAtlasPixels(out int wallAtlasWidth, out int wallAtlasHeight);
            int[] doorPixels = textureManager.GetDoorOpenTexturePixels(out int doorWidth, out int doorHeight);
            BuildSpriteData(
                player,
                rewardPickups,
                playerProjectiles,
                worldTargetWidth,
                worldTargetHeight,
                out SpritePassBuildResult enemySpritePass,
                out SpritePassBuildResult miscSpritePass,
                out int beamCount);

            command = new RenderWorldCommand
            {
                Camera = new WorldCameraData
                {
                    PositionX = player.Position.X,
                    PositionY = player.Position.Y,
                    DirectionX = player.Direction.X,
                    DirectionY = player.Direction.Y,
                    PlaneX = player.Plane.X,
                    PlaneY = player.Plane.Y
                },
                PlayerEyeZ = player.FloorZ + 0.5f,
                TargetWidth = worldTargetWidth,
                TargetHeight = worldTargetHeight,
                ColumnCount = worldTargetWidth,
                WallColumnGeometry = wallColumnGeometryBuffer,
                WallColumnMaterial = wallColumnMaterialBuffer,
                WallColumnDoorProgress = wallColumnDoorProgressBuffer,
                DepthBufferLength = depthBufferData?.Length ?? 0,
                DepthBuffer = depthBufferData,
                TextureSize = GameConfig.TextureSize,
                WallTextureCount = textureManager.WallTextureCount,
                WallTextureAtlasPixels = wallAtlasPixels,
                WallTextureAtlasWidth = wallAtlasWidth,
                WallTextureAtlasHeight = wallAtlasHeight,
                DoorOpenTexturePixels = doorPixels,
                DoorOpenTextureWidth = doorWidth,
                DoorOpenTextureHeight = doorHeight,
                EnemySpritePass = new RenderWorldSpritePassCommand
                {
                    TargetWidth = worldTargetWidth,
                    TargetHeight = worldTargetHeight,
                    DepthBufferLength = gpuDepthBufferData?.Length ?? 0,
                    DepthBuffer = gpuDepthBufferData,   // Â±2 min-filter ?ìš©ë³?
                    FogDensity = 0.15f,
                    SpriteAtlasPixels = enemySpritePass.SpriteCount > 0 ? enemySpritePass.AtlasPixels : null,
                    SpriteAtlasWidth = enemySpritePass.AtlasWidth,
                    SpriteAtlasHeight = enemySpritePass.AtlasHeight,
                    UploadFullSpriteAtlas = enemySpritePass.UploadFullAtlas,
                    DirtySpriteAtlasRects = enemySpritePass.DirtyRects,
                    DirtySpriteAtlasRectCount = enemySpritePass.DirtyRectCount,
                    Sprites = enemySpritePass.Sprites,
                    SpriteCount = enemySpritePass.SpriteCount
                },
                MiscSpritePass = new RenderWorldSpritePassCommand
                {
                    TargetWidth = worldTargetWidth,
                    TargetHeight = worldTargetHeight,
                    DepthBufferLength = gpuDepthBufferData?.Length ?? 0,
                    DepthBuffer = gpuDepthBufferData,   // Â±2 min-filter ?ìš©ë³?
                    FogDensity = 0.15f,
                    SpriteAtlasPixels = miscSpritePass.SpriteCount > 0 ? miscSpritePass.AtlasPixels : null,
                    SpriteAtlasWidth = miscSpritePass.AtlasWidth,
                    SpriteAtlasHeight = miscSpritePass.AtlasHeight,
                    UploadFullSpriteAtlas = miscSpritePass.UploadFullAtlas,
                    DirtySpriteAtlasRects = miscSpritePass.DirtyRects,
                    DirtySpriteAtlasRectCount = miscSpritePass.DirtyRectCount,
                    Sprites = miscSpritePass.Sprites,
                    SpriteCount = miscSpritePass.SpriteCount
                },
                DoorTileType = GameConfig.DoorTileType,
                FloorTextureIndex = 3,
                UseTexturedFloor = GameConfig.GpuWorldUseTexturedFloor,
                CeilingTextureIndex = 0,
                UseTexturedCeiling = GameConfig.GpuWorldUseTexturedCeiling,
                CeilingBlend = 0.22f,
                NearPlane = GameConfig.NearPlane,
                NearPlaneSoftness = GameConfig.NearPlaneSoftness,
                FloorBlend = 0.22f,
                FogDensity = 0.15f,
                FloorColor = GameConfig.FloorColor,
                CeilingColor = GameConfig.CeilingColor,
                Beams = beamCount > 0 ? beamBuffer : null,
                BeamCount = beamCount
            };
            return true;
        }

        /// <summary>?´ë²ˆ ?„ë ˆ?„ì— ê¸°ë¡???¤í”„?¼ì´???”ë²„ê·???ª© ?˜ë? ë°˜í™˜?œë‹¤.</summary>
        public int DebugSpriteCount => spriteDebugCount;

        /// <summary>?´ë²ˆ ?„ë ˆ?„ì˜ ?¤í”„?¼ì´???”ë²„ê·??¬ì˜ ?•ë³´ ë°°ì—´??ë°˜í™˜?œë‹¤. DebugSpriteCountë§Œí¼ë§?? íš¨?˜ë‹¤.</summary>
        public SpriteDebugProjection[] DebugSprites => spriteDebugBuffer;

        /// <summary>
        /// ë°?ì¸??„í™˜ ???”ë“œ ?Œë” ?„ì‹œ ìºì‹œë¥?ë¹„ìš´??
        /// ???„í??¼ìŠ¤/?¸ìŠ¤?´ìŠ¤ ë²„í¼ê°€ ?´ì „ ë°?ìµœë?ì¹˜ë¡œ ?¨ëŠ” ê²ƒì„ ë§‰ê¸° ?„í•œ ë¦¬ì…‹?´ë‹¤.
        /// </summary>
        public void ResetTransientCaches()
        {
            enemySpriteEntries.Clear();
            miscSpriteEntries.Clear();
            beamEntries.Clear();
            _segmentBuffer.Clear();

            enemySpriteBuffer = null;
            miscSpriteBuffer = null;
            beamBuffer = null;
            spriteDebugBuffer = null;
            spriteDebugCount = 0;

            ResetSpriteAtlasCache(enemySpriteAtlasCache);
            ResetSpriteAtlasCache(miscSpriteAtlasCache);
        }

        /// <summary>
        /// DDA ?ˆì´ìºìŠ¤?…ìœ¼ë¡?ê°??”ë©´ ?´ì˜ ë²?ì§€?¤ë©”?¸ë¦¬, ?¬ì§ˆ, ë¬?ê°œë°© ì§„í–‰?? ê¹Šì´ë¥?ê³„ì‚°?˜ì—¬
        /// wallColumnGeometryBuffer, wallColumnMaterialBuffer, wallColumnDoorProgressBuffer, depthBufferData??ê¸°ë¡?œë‹¤.
        /// </summary>
        /// <param name="player">ì¹´ë©”???„ì¹˜Â·ë°©í–¥Â·?¬ì˜ ?‰ë©´???œê³µ?˜ëŠ” ?Œë ˆ?´ì–´ ?íƒœ.</param>
        /// <param name="map">ë§??€???€??ë°°ì—´.</param>
        /// <param name="textureIds">ë§??€?¼ë³„ ?ìŠ¤ì²?ID ë°°ì—´.</param>
        /// <param name="mapWidth">ë§?ê°€ë¡??¬ê¸°(?€??.</param>
        /// <param name="mapHeight">ë§??¸ë¡œ ?¬ê¸°(?€??.</param>
        /// <param name="renderWidth">?Œë” ?€???ˆë¹„(????.</param>
        /// <param name="renderHeight">?Œë” ?€???’ì´(?½ì?).</param>
        private void BuildWallColumns(Player player, int[,] map, int[,] textureIds, int mapWidth, int mapHeight, int renderWidth, int renderHeight)
        {
            EnsureWallColumnBuffers(renderWidth);
            int wallTextureCount = Math.Max(1, textureManager.WallTextureCount);

            for (int x = 0; x < renderWidth; x++)
            {
                double cameraX = (2.0 * (x + 0.5) / renderWidth) - 1.0;
                double rayDirX = player.Direction.X + player.Plane.X * cameraX;
                double rayDirY = player.Direction.Y + player.Plane.Y * cameraX;

                int mapX = (int)player.Position.X;
                int mapY = (int)player.Position.Y;

                double deltaDistX = rayDirX == 0 ? 1e30 : Math.Abs(1.0 / rayDirX);
                double deltaDistY = rayDirY == 0 ? 1e30 : Math.Abs(1.0 / rayDirY);

                int stepX;
                int stepY;
                double sideDistX;
                double sideDistY;

                if (rayDirX < 0)
                {
                    stepX = -1;
                    sideDistX = (player.Position.X - mapX) * deltaDistX;
                }
                else
                {
                    stepX = 1;
                    sideDistX = (mapX + 1.0 - player.Position.X) * deltaDistX;
                }

                if (rayDirY < 0)
                {
                    stepY = -1;
                    sideDistY = (player.Position.Y - mapY) * deltaDistY;
                }
                else
                {
                    stepY = 1;
                    sideDistY = (mapY + 1.0 - player.Position.Y) * deltaDistY;
                }

                // ê³„ë‹¨ ë©?riser) ê°ì???ë³€??
                float eyeZpre = player.FloorZ + 0.5f;
                float prevTileFloor = player.FloorZ;
                bool stepFaceFound = false;
                float stepFaceDist = 0f;
                float stepFaceTop = 0f;
                float stepFaceBottom = 0f;
                float stepFaceUpperFloor = 0f; // ê³„ë‹¨ ???€?¼ì˜ ë°”ë‹¥ ?’ì´ (?˜í‰ë©??Œë”???¬ìš©)

                bool hit = false;
                int side = 0;
                while (!hit)
                {
                    if (sideDistX < sideDistY)
                    {
                        sideDistX += deltaDistX;
                        mapX += stepX;
                        side = 0;
                    }
                    else
                    {
                        sideDistY += deltaDistY;
                        mapY += stepY;
                        side = 1;
                    }

                    if (mapX < 0 || mapY < 0 || mapX >= mapWidth || mapY >= mapHeight)
                    {
                        break;
                    }

                    if (CollisionSystem.IsSolidType(map[mapX, mapY]))
                    {
                        hit = true;
                        break;
                    }

                    // ë°”ë‹¥ ?’ì´ ?ìŠ¹ êµ¬ê°„ = ê³„ë‹¨ ë©?riser) ê°ì?
                    if (!stepFaceFound)
                    {
                        float tileFloor = mapManager.GetFloorHeight(mapX, mapY);
                        if (tileFloor > prevTileFloor + 0.01f)
                        {
                            double sd = side == 0
                                ? (mapX - player.Position.X + (1 - stepX) / 2.0) / rayDirX
                                : (mapY - player.Position.Y + (1 - stepY) / 2.0) / rayDirY;
                            if (sd > 0.0001)
                            {
                                double sScale = renderHeight / GetRenderDistance(sd);
                                float fTop = (float)(renderHeight * 0.5 + (eyeZpre - tileFloor) * sScale);
                                float fBot = (float)(renderHeight * 0.5 + (eyeZpre - prevTileFloor) * sScale);
                                if (fBot > fTop)
                                {
                                    stepFaceDist = (float)sd;
                                    stepFaceTop = fTop;
                                    stepFaceBottom = fBot;
                                    stepFaceUpperFloor = tileFloor; // ?°ì´?”ê? ?˜í‰ë©?ê³„ì‚°???¬ìš©
                                    stepFaceFound = true;
                                }
                            }
                        }
                        prevTileFloor = tileFloor;
                    }
                }

                int geometryIndex = x * 4;
                int materialIndex = x * 4;
                int stepIndex = renderWidth * 4 + x * 4; // Row 1: ê³„ë‹¨ ë©??°ì´??
                if (!hit)
                {
                    wallColumnGeometryBuffer[geometryIndex + 0] = -1f;
                    wallColumnGeometryBuffer[geometryIndex + 1] = 0f;
                    wallColumnGeometryBuffer[geometryIndex + 2] = 0f;
                    wallColumnGeometryBuffer[geometryIndex + 3] = 0f;
                    wallColumnGeometryBuffer[stepIndex + 0] = 0f;
                    wallColumnGeometryBuffer[stepIndex + 1] = 0f;
                    wallColumnGeometryBuffer[stepIndex + 2] = 0f;
                    wallColumnGeometryBuffer[stepIndex + 3] = 0f;
                    wallColumnMaterialBuffer[materialIndex + 0] = 0;
                    wallColumnMaterialBuffer[materialIndex + 1] = 0;
                    wallColumnMaterialBuffer[materialIndex + 2] = 0;
                    wallColumnMaterialBuffer[materialIndex + 3] = 0;
                    wallColumnDoorProgressBuffer[x] = 0f;
                    depthBufferData[x] = float.MaxValue;
                    continue;
                }

                double perpWallDist = side == 0
                    ? (mapX - player.Position.X + (1 - stepX) / 2.0) / rayDirX
                    : (mapY - player.Position.Y + (1 - stepY) / 2.0) / rayDirY;

                if (perpWallDist < 0.0001)
                {
                    perpWallDist = 0.0001;
                }

                double renderDist = GetRenderDistance(perpWallDist);
                double scale = renderHeight / renderDist;

                // ?’ì´ ê¸°ë°˜ drawStart/drawEnd ê³„ì‚° (Doom ?¤í????¹í„° ?’ì´)
                float eyeZ = player.FloorZ + 0.5f;
                float hitFloor = mapManager.GetFloorHeight(mapX, mapY);
                float hitCeil = mapManager.GetCeilHeight(mapX, mapY);
                int fullDrawStart = (int)(renderHeight * 0.5 - (hitCeil - eyeZ) * scale);
                int drawStart = fullDrawStart;
                int drawEnd = (int)(renderHeight * 0.5 + (eyeZ - hitFloor) * scale);

                // ?˜ìœ„ ?¸í™˜: ê¸°ë³¸ ?’ì´(floor=0, ceil=1)????ê¸°ì¡´ê³??™ì¼??ê²°ê³¼
                int lineHeight = drawEnd - fullDrawStart;

                int tileType = map[mapX, mapY];
                float doorProgress = 0f;
                if (tileType == GameConfig.DoorTileType)
                {
                    doorProgress = mapManager.GetDoorOpenProgress(mapX, mapY);
                    if (doorProgress > 0f)
                    {
                        int visibleHeight = (int)Math.Round(lineHeight * Math.Max(0f, 1f - doorProgress));
                        if (visibleHeight <= 0)
                        {
                            wallColumnGeometryBuffer[geometryIndex + 0] = -1f;
                            wallColumnGeometryBuffer[geometryIndex + 1] = 0f;
                            wallColumnGeometryBuffer[geometryIndex + 2] = 0f;
                            wallColumnGeometryBuffer[geometryIndex + 3] = 0f;
                            wallColumnMaterialBuffer[materialIndex + 0] = 0;
                            wallColumnMaterialBuffer[materialIndex + 1] = 0;
                            wallColumnMaterialBuffer[materialIndex + 2] = 0;
                            wallColumnMaterialBuffer[materialIndex + 3] = 0;
                            wallColumnDoorProgressBuffer[x] = 0f;
                            depthBufferData[x] = float.MaxValue;
                            continue;
                        }

                        drawEnd = fullDrawStart + visibleHeight - 1;
                    }
                }

                double wallX = side == 0
                    ? player.Position.Y + perpWallDist * rayDirY
                    : player.Position.X + perpWallDist * rayDirX;
                wallX -= Math.Floor(wallX);

                int texX = (int)(wallX * GameConfig.TextureSize);
                if (side == 0 && rayDirX > 0) texX = GameConfig.TextureSize - texX - 1;
                if (side == 1 && rayDirY < 0) texX = GameConfig.TextureSize - texX - 1;
                if (texX < 0) texX = 0;
                if (texX >= GameConfig.TextureSize) texX = GameConfig.TextureSize - 1;

                int textureId = textureIds[mapX, mapY] % wallTextureCount;
                if (textureId < 0)
                {
                    textureId += wallTextureCount;
                }

                wallColumnGeometryBuffer[geometryIndex + 0] = (float)perpWallDist;
                wallColumnGeometryBuffer[geometryIndex + 1] = fullDrawStart;
                wallColumnGeometryBuffer[geometryIndex + 2] = drawStart;
                wallColumnGeometryBuffer[geometryIndex + 3] = drawEnd;

                // Row 1: ê³„ë‹¨ ë©?riser) ?°ì´??(stepDepth=0?´ë©´ ê³„ë‹¨ ?†ìŒ)
                if (stepFaceFound)
                {
                    wallColumnGeometryBuffer[stepIndex + 0] = stepFaceDist;
                    wallColumnGeometryBuffer[stepIndex + 1] = stepFaceTop;
                    wallColumnGeometryBuffer[stepIndex + 2] = stepFaceBottom;
                    wallColumnGeometryBuffer[stepIndex + 3] = stepFaceUpperFloor;
                }
                else
                {
                    wallColumnGeometryBuffer[stepIndex + 0] = 0f;
                    wallColumnGeometryBuffer[stepIndex + 1] = 0f;
                    wallColumnGeometryBuffer[stepIndex + 2] = 0f;
                    wallColumnGeometryBuffer[stepIndex + 3] = 0f;
                }

                wallColumnMaterialBuffer[materialIndex + 0] = textureId;
                wallColumnMaterialBuffer[materialIndex + 1] = texX;
                wallColumnMaterialBuffer[materialIndex + 2] = side;
                wallColumnMaterialBuffer[materialIndex + 3] = tileType;
                wallColumnDoorProgressBuffer[x] = doorProgress;
                depthBufferData[x] = (float)perpWallDist;
            }
        }

        /// <summary>
        /// ?Œë ˆ?´ì–´ ì¹´ë©”??ê¸°ì??¼ë¡œ ?? ?¬ì‚¬ì²? ?½ì—…, ?ˆì´?€ ë¹”ì„ ?”ë©´???¬ì˜?˜ê³ 
        /// ?¤í”„?¼ì´???„í??¼ìŠ¤ ë°?ë¹?ë²„í¼ë¥?ì¡°ë¦½?œë‹¤.
        /// </summary>
        /// <param name="player">ì¹´ë©”???„ì¹˜Â·ë°©í–¥Â·?¬ì˜ ?‰ë©´???œê³µ?˜ëŠ” ?Œë ˆ?´ì–´ ?íƒœ.</param>
        /// <param name="rewardPickups">?”ë©´???¬ì˜??ë³´ìƒ ?½ì—… ëª©ë¡.</param>
        /// <param name="playerProjectiles">?”ë©´???¬ì˜???Œë ˆ?´ì–´ ?¬ì‚¬ì²?ëª©ë¡.</param>
        /// <param name="renderWidth">?Œë” ?€???ˆë¹„(?½ì?).</param>
        /// <param name="renderHeight">?Œë” ?€???’ì´(?½ì?).</param>
        /// <param name="enemySpritePass">???¤í”„?¼ì´???¨ìŠ¤ ë¹Œë“œ ê²°ê³¼(?¸ìŠ¤?´ìŠ¤ ë°°ì—´ + ?„í??¼ìŠ¤ ?•ë³´).</param>
        /// <param name="miscSpritePass">ê¸°í? ?¤í”„?¼ì´???¬ì‚¬ì²´Â·í”½?? ?¨ìŠ¤ ë¹Œë“œ ê²°ê³¼.</param>
        /// <param name="beamCount">ë¹Œë“œ???ˆì´?€ ë¹??¸ìŠ¤?´ìŠ¤ ??</param>
        private void BuildSpriteData(
            Player player,
            IList<RewardPickup> rewardPickups,
            IList<EnemyProjectile> playerProjectiles,
            int renderWidth,
            int renderHeight,
            out SpritePassBuildResult enemySpritePass,
            out SpritePassBuildResult miscSpritePass,
            out int beamCount)
        {
            enemySpriteEntries.Clear();
            miscSpriteEntries.Clear();
            beamEntries.Clear();
            beamCount = 0;
            BeginSpriteAtlasFrame(enemySpriteAtlasCache);
            BeginSpriteAtlasFrame(miscSpriteAtlasCache);
            if (player == null || renderWidth <= 0 || renderHeight <= 0)
            {
                ReleaseUnusedSpriteAtlasSlots(enemySpriteAtlasCache);
                ReleaseUnusedSpriteAtlasSlots(miscSpriteAtlasCache);
                enemySpritePass = default;
                miscSpritePass = default;
                return;
            }

            float playerX = player.Position.X;
            float playerY = player.Position.Y;
            float dirX = player.Direction.X;
            float dirY = player.Direction.Y;
            float planeX = player.Plane.X;
            float planeY = player.Plane.Y;
            double invDet = 1.0 / ((planeX * dirY) - (dirX * planeY));

            Enemy[] enemies = enemyManager?.Enemies;
            if (enemies != null)
            {
                for (int i = 0; i < enemies.Length; i++)
                {
                    Enemy enemy = enemies[i];
                    if (enemy == null || !enemy.IsRenderable)
                    {
                        continue;
                    }

                    Color[] spritePixels = GetEnemySpritePixels(enemy);
                    if (spritePixels == null)
                    {
                        continue;
                    }

                    float dx = enemy.X - playerX;
                    float dy = enemy.Y - playerY;
                    float hitReactRatio = 0f;
                    if (enemy.HitReactTimer > 0f && GameConfig.EnemyHitReactDuration > 0f)
                    {
                        hitReactRatio = Math.Min(1f, enemy.HitReactTimer / GameConfig.EnemyHitReactDuration);
                    }

                    float enemyRenderScale = enemy.Scale * (1f + (GameConfig.EnemyHitReactScalePulse * hitReactRatio));
                    if (!IsPotentiallyVisible(dx, dy, enemyRenderScale, renderWidth, renderHeight, dirX, dirY, planeX, planeY, invDet))
                    {
                        continue;
                    }

                    float enemyDepth = GetProjectedDepth(dx, dy, planeX, planeY, invDet);
                    float enemyScreenX = GetProjectedScreenX(dx, dy, targetWidth: renderWidth, dirX, dirY, planeX, planeY, invDet);
                    float enemyProjectedHeight = Math.Max(6f, SnapSpriteScale(GetProjectedHeight(renderHeight, enemyDepth, enemyRenderScale)));
                    float hitReactLift = enemyProjectedHeight * 0.025f * hitReactRatio;
                    float enemyProjectedCenterY = SnapSpriteCenterY(GetProjectedCenterY(renderHeight, enemyDepth, enemyRenderScale) - hitReactLift);
                    float enemyProjectedCenterX = SnapSpriteCenterX(enemyScreenX);
                    int enemySpriteHeight = Math.Abs((int)enemyProjectedHeight);
                    if (enemySpriteHeight <= 0)
                    {
                        continue;
                    }

                    int enemySpriteWidth = enemySpriteHeight;
                    int enemyDrawStartX = (int)(enemyProjectedCenterX - (enemySpriteWidth * 0.5f));
                    int enemyDrawEndX = enemyDrawStartX + enemySpriteWidth - 1;
                    if (!IsSpriteVisibleAgainstDepth(enemyDepth, enemyDrawStartX, enemyDrawEndX))
                    {
                        continue;
                    }

                    ComputeVisibleSegments(enemyDepth, enemyDrawStartX, enemyDrawEndX, _segmentBuffer);
                    int totalEnemyCols = enemyDrawEndX - enemyDrawStartX + 1;
                    bool enemyFirstSegment = true;
                    foreach ((int segStart, int segEnd) in _segmentBuffer)
                    {
                        int segWidth = segEnd - segStart + 1;
                        float tLeft  = (float)(segStart - enemyDrawStartX) / totalEnemyCols;
                        float tRight = (float)(segEnd - enemyDrawStartX + 1) / totalEnemyCols;
                        enemySpriteEntries.Add(new SpriteBuildEntry
                        {
                            SortKey = -enemyDepth,
                            Owner = enemy,
                            SpritePixels = spritePixels,
                            TintHitFlash = enemy.HitFlash > 0f,
                            UVCropLeft = tLeft,
                            UVCropRight = tRight,
                            Instance = new WorldSpriteInstance
                            {
                                Kind = WorldSpriteKind.Enemy,
                                PositionX = segStart + segWidth * 0.5f,
                                PositionY = enemyProjectedCenterY,
                                Scale = enemyProjectedHeight,
                                WidthScale = (float)segWidth / enemyProjectedHeight,
                                VerticalOffsetFactor = enemyDepth,
                                DepthBias = 0.01f,
                                RenderMode = 2f,
                                TintArgb = Color.White.ToArgb(),
                                ShowHealthBar = enemyFirstSegment && enemy.Alive,
                                ShowTelegraph = enemyFirstSegment && (enemy.IsBoss || enemy.IsMiniBoss) && enemy.TelegraphTimer > 0f,
                                MinWallDepth = float.MaxValue,
                            }
                        });
                        enemyFirstSegment = false;
                    }
                }
            }

            IList<EnemyProjectile> projectiles = enemyManager?.EnemyProjectiles;
            if (projectiles != null)
            {
                for (int i = 0; i < projectiles.Count; i++)
                {
                    EnemyProjectile projectile = projectiles[i];
                    if (projectile == null || !projectile.Active)
                    {
                        continue;
                    }

                    if (projectile.Kind == EnemyProjectileKind.LaserBeam)
                    {
                        AddLaserBeamEntries(projectile, playerX, playerY, dirX, dirY, planeX, planeY, invDet, renderWidth, renderHeight);
                        continue;
                    }

                    Color[] spritePixels = GetProjectileSpritePixels(projectile);
                    if (spritePixels == null)
                    {
                        continue;
                    }

                    float dx = projectile.X - playerX;
                    float dy = projectile.Y - playerY;
                    bool isAcidPool = projectile.Kind == EnemyProjectileKind.AcidGlob
                        && Math.Abs(projectile.VelocityX) < 0.001f
                        && Math.Abs(projectile.VelocityY) < 0.001f;
                    float projectileScale = Math.Max(0.18f, projectile.Radius * 0.8f);
                    if (!IsPotentiallyVisible(dx, dy, projectileScale, renderWidth, renderHeight, dirX, dirY, planeX, planeY, invDet))
                    {
                        continue;
                    }

                    float projectileDepth = GetProjectedDepth(dx, dy, planeX, planeY, invDet);
                    float projectileScreenX = GetProjectedScreenX(dx, dy, targetWidth: renderWidth, dirX, dirY, planeX, planeY, invDet);
                    float projectileProjectedHeight = SnapSpriteScale(GetProjectedHeight(renderHeight, projectileDepth, projectileScale));
                    float projectileProjectedCenterY;
                    if (isAcidPool)
                    {
                        float safeDepth = Math.Max(0.01f, projectileDepth);
                        projectileProjectedCenterY = SnapSpriteCenterY(renderHeight * 0.5f + (renderHeight * 0.34f) / safeDepth);
                    }
                    else
                    {
                        projectileProjectedCenterY = SnapSpriteCenterY(GetProjectedCenterY(renderHeight, projectileDepth, projectileScale));
                    }
                    float projectileProjectedCenterX = SnapSpriteCenterX(projectileScreenX);
                    int projectileSpriteHeight = Math.Abs((int)projectileProjectedHeight);
                    if (projectileSpriteHeight <= 4)
                    {
                        continue;
                    }

                    int projectileSpriteWidth = isAcidPool ? (int)(projectileSpriteHeight * 1.8f) : projectileSpriteHeight;
                    int projectileDrawStartX = (int)(projectileProjectedCenterX - (projectileSpriteWidth * 0.5f));
                    int projectileDrawEndX = projectileDrawStartX + projectileSpriteWidth - 1;
                    if (!IsSpriteVisibleAgainstDepth(projectileDepth, projectileDrawStartX, projectileDrawEndX))
                    {
                        continue;
                    }

                    ComputeVisibleSegments(projectileDepth, projectileDrawStartX, projectileDrawEndX, _segmentBuffer);
                    int totalProjectileCols = projectileDrawEndX - projectileDrawStartX + 1;
                    foreach ((int segStart, int segEnd) in _segmentBuffer)
                    {
                        int segWidth = segEnd - segStart + 1;
                        float tLeft  = (float)(segStart - projectileDrawStartX) / totalProjectileCols;
                        float tRight = (float)(segEnd - projectileDrawStartX + 1) / totalProjectileCols;
                        miscSpriteEntries.Add(new SpriteBuildEntry
                        {
                            SortKey = -projectileDepth,
                            Owner = projectile,
                            SpritePixels = spritePixels,
                            UVCropLeft = tLeft,
                            UVCropRight = tRight,
                            Instance = new WorldSpriteInstance
                            {
                                Kind = WorldSpriteKind.Projectile,
                                PositionX = segStart + segWidth * 0.5f,
                                PositionY = projectileProjectedCenterY,
                                Scale = projectileProjectedHeight,
                                WidthScale = (float)segWidth / projectileProjectedHeight,
                                VerticalOffsetFactor = projectileDepth,
                                DepthBias = 0.12f,
                                TintArgb = Color.White.ToArgb(),
                                MinWallDepth = float.MaxValue,
                            }
                        });
                    }
                }
            }

            if (playerProjectiles != null)
            {
                for (int i = 0; i < playerProjectiles.Count; i++)
                {
                    EnemyProjectile projectile = playerProjectiles[i];
                    if (projectile == null || !projectile.Active)
                    {
                        continue;
                    }

                    Color[] spritePixels = GetProjectileSpritePixels(projectile);
                    if (spritePixels == null)
                    {
                        continue;
                    }

                    float dx = projectile.X - playerX;
                    float dy = projectile.Y - playerY;
                    float projectileScale = Math.Max(0.18f, projectile.Radius * 0.8f);
                    if (!IsPotentiallyVisible(dx, dy, projectileScale, renderWidth, renderHeight, dirX, dirY, planeX, planeY, invDet))
                    {
                        continue;
                    }

                    float projectileDepth = GetProjectedDepth(dx, dy, planeX, planeY, invDet);
                    float projectileScreenX = GetProjectedScreenX(dx, dy, targetWidth: renderWidth, dirX, dirY, planeX, planeY, invDet);
                    float projectileProjectedHeight = SnapSpriteScale(GetProjectedHeight(renderHeight, projectileDepth, projectileScale));
                    float projectileProjectedCenterY = SnapSpriteCenterY(GetProjectedCenterY(renderHeight, projectileDepth, projectileScale));
                    float projectileProjectedCenterX = SnapSpriteCenterX(projectileScreenX);
                    int projectileSpriteHeight = Math.Abs((int)projectileProjectedHeight);
                    if (projectileSpriteHeight <= 4)
                    {
                        continue;
                    }

                    int projectileSpriteWidth = projectileSpriteHeight;
                    int projectileDrawStartX = (int)(projectileProjectedCenterX - (projectileSpriteWidth * 0.5f));
                    int projectileDrawEndX = projectileDrawStartX + projectileSpriteWidth - 1;
                    if (!IsSpriteVisibleAgainstDepth(projectileDepth, projectileDrawStartX, projectileDrawEndX))
                    {
                        continue;
                    }

                    ComputeVisibleSegments(projectileDepth, projectileDrawStartX, projectileDrawEndX, _segmentBuffer);
                    int totalProjectileCols = projectileDrawEndX - projectileDrawStartX + 1;
                    foreach ((int segStart, int segEnd) in _segmentBuffer)
                    {
                        int segWidth = segEnd - segStart + 1;
                        float tLeft = (float)(segStart - projectileDrawStartX) / totalProjectileCols;
                        float tRight = (float)(segEnd - projectileDrawStartX + 1) / totalProjectileCols;
                        miscSpriteEntries.Add(new SpriteBuildEntry
                        {
                            SortKey = -projectileDepth,
                            Owner = projectile,
                            SpritePixels = spritePixels,
                            UVCropLeft = tLeft,
                            UVCropRight = tRight,
                            Instance = new WorldSpriteInstance
                            {
                                Kind = WorldSpriteKind.Projectile,
                                PositionX = segStart + segWidth * 0.5f,
                                PositionY = projectileProjectedCenterY,
                                Scale = projectileProjectedHeight,
                                WidthScale = (float)segWidth / projectileProjectedHeight,
                                VerticalOffsetFactor = projectileDepth,
                                DepthBias = 0.12f,
                                TintArgb = Color.White.ToArgb(),
                                MinWallDepth = float.MaxValue,
                            }
                        });
                    }
                }
            }

            if (rewardPickups != null)
            {
                for (int i = 0; i < rewardPickups.Count; i++)
                {
                    RewardPickup pickup = rewardPickups[i];
                    if (pickup == null || !pickup.Active)
                    {
                        continue;
                    }

                    Color[] spritePixels = textureManager.GetPickupSprite(pickup.Kind);
                    if (spritePixels == null)
                    {
                        continue;
                    }

                    float dx = pickup.X - playerX;
                    float dy = pickup.Y - playerY;
                    if (!IsPotentiallyVisible(dx, dy, 0.42f, renderWidth, renderHeight, dirX, dirY, planeX, planeY, invDet))
                    {
                        continue;
                    }

                    float pickupDepth = GetProjectedDepth(dx, dy, planeX, planeY, invDet);
                    float pickupScreenX = GetProjectedScreenX(dx, dy, targetWidth: renderWidth, dirX, dirY, planeX, planeY, invDet);
                    float pickupProjectedHeight = SnapSpriteScale(GetProjectedHeight(renderHeight, pickupDepth, 0.42f));
                    float pickupProjectedCenterY = SnapSpriteCenterY(GetProjectedPickupCenterY(renderHeight, pickupDepth, pickup.PulseTimer, 0.42f));
                    float pickupProjectedCenterX = SnapSpriteCenterX(pickupScreenX);
                    int pickupSpriteHeight = Math.Abs((int)pickupProjectedHeight);
                    if (pickupSpriteHeight <= 8)
                    {
                        continue;
                    }

                    int pickupSpriteWidth = Math.Max(12, (int)(pickupSpriteHeight * 0.82f));
                    int pickupDrawStartX = (int)(pickupProjectedCenterX - (pickupSpriteWidth * 0.5f));
                    int pickupDrawEndX = pickupDrawStartX + pickupSpriteWidth - 1;
                    if (!IsSpriteVisibleAgainstDepth(pickupDepth, pickupDrawStartX, pickupDrawEndX))
                    {
                        continue;
                    }

                    ComputeVisibleSegments(pickupDepth, pickupDrawStartX, pickupDrawEndX, _segmentBuffer);
                    int totalPickupCols = pickupDrawEndX - pickupDrawStartX + 1;
                    foreach ((int segStart, int segEnd) in _segmentBuffer)
                    {
                        int segWidth = segEnd - segStart + 1;
                        float tLeft  = (float)(segStart - pickupDrawStartX) / totalPickupCols;
                        float tRight = (float)(segEnd - pickupDrawStartX + 1) / totalPickupCols;
                        miscSpriteEntries.Add(new SpriteBuildEntry
                        {
                            SortKey = -pickupDepth,
                            Owner = pickup,
                            SpritePixels = spritePixels,
                            UVCropLeft = tLeft,
                            UVCropRight = tRight,
                            Instance = new WorldSpriteInstance
                            {
                                Kind = WorldSpriteKind.Pickup,
                                PositionX = segStart + segWidth * 0.5f,
                                PositionY = pickupProjectedCenterY,
                                Scale = pickupProjectedHeight,
                                WidthScale = (float)segWidth / pickupProjectedHeight,
                                VerticalOffsetFactor = pickupDepth,
                                DepthBias = 0.2f,
                                TintArgb = Color.White.ToArgb(),
                                MinWallDepth = float.MaxValue,
                            }
                        });
                    }
                }
            }

            beamCount = BuildBeamBuffer();

            EnsureDebugSpriteBuffer(enemySpriteEntries.Count + miscSpriteEntries.Count);
            spriteDebugCount = 0;
            enemySpritePass = BuildSpritePass(
                enemySpriteEntries,
                enemySpriteAtlasCache,
                ref enemySpriteBuffer,
                true);
            miscSpritePass = BuildSpritePass(
                miscSpriteEntries,
                miscSpriteAtlasCache,
                ref miscSpriteBuffer,
                false);
        }

        /// <summary>
        /// ?¤í”„?¼ì´??ë¹Œë“œ ??ª© ëª©ë¡???„í??¼ìŠ¤???•ë ¬Â·?…ë¡œ?œí•˜ê³?
        /// WorldSpriteInstance ë°°ì—´??ì±„ì›Œ SpritePassBuildResultë¡?ë°˜í™˜?œë‹¤.
        /// </summary>
        /// <param name="entries">?´ë²ˆ ?¨ìŠ¤?ì„œ ?Œë”ë§í•  ?¤í”„?¼ì´??ë¹Œë“œ ??ª© ëª©ë¡.</param>
        /// <param name="atlasCache">?¬ë¡¯ ? ë‹¹ê³??½ì? ?”í‹° ?íƒœë¥?ê´€ë¦¬í•˜???„í??¼ìŠ¤ ìºì‹œ.</param>
        /// <param name="spriteBuffer">?¸ìŠ¤?´ìŠ¤ë¥?ê¸°ë¡??ë²„í¼. ?¬ê¸°ê°€ ë¶€ì¡±í•˜ë©??¬í• ?¹ëœ??</param>
        /// <param name="forceRefreshAllCells">true?´ë©´ ë³€ê²??¬ë??€ ê´€ê³„ì—†??ëª¨ë“  ?€???„í??¼ìŠ¤???¬ë³µ?¬í•œ??</param>
        /// <returns>?¸ìŠ¤?´ìŠ¤ ë°°ì—´, ?„í??¼ìŠ¤ ?½ì?, ?”í‹° ?¬ê°???•ë³´ë¥??´ì? ë¹Œë“œ ê²°ê³¼.</returns>
        private SpritePassBuildResult BuildSpritePass(
            List<SpriteBuildEntry> entries,
            SpriteAtlasCache atlasCache,
            ref WorldSpriteInstance[] spriteBuffer,
            bool forceRefreshAllCells)
        {
            if (entries.Count == 0)
            {
                atlasCache.UploadFullAtlas = false;
                atlasCache.DirtyRectCount = 0;
                ReleaseUnusedSpriteAtlasSlots(atlasCache);
                TrimUnusedSpriteAtlasTail(atlasCache);
                return default;
            }

            atlasCache.ActiveSlotSpan = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                SpriteBuildEntry entry = entries[i];
                entry.AtlasSlot = GetOrCreateSpriteAtlasSlot(atlasCache, entry.Owner);
                atlasCache.PreviousUsed[entry.AtlasSlot] = true;
                atlasCache.ActiveSlotSpan = Math.Max(atlasCache.ActiveSlotSpan, entry.AtlasSlot + 1);
                entries[i] = entry;
            }

            ReleaseUnusedSpriteAtlasSlots(atlasCache);
            TrimUnusedSpriteAtlasTail(atlasCache);

            entries.Sort((a, b) =>
            {
                double diff = a.SortKey - b.SortKey;
                if (Math.Abs(diff) > 0.001)
                {
                    return diff < 0 ? -1 : 1;
                }

                return a.AtlasSlot.CompareTo(b.AtlasSlot);
            });

            EnsureSpriteBuffers(ref spriteBuffer, entries.Count);
            GetSpriteAtlasDimensions(atlasCache.PreviousSpriteCount, out int atlasWidth, out int atlasHeight);
            bool atlasLayoutChanged = atlasWidth != atlasCache.PreviousAtlasWidth || atlasHeight != atlasCache.PreviousAtlasHeight;
            atlasCache.UploadFullAtlas = atlasLayoutChanged;
            atlasCache.DirtyRectCount = 0;
            EnsureSpriteAtlasPixelCapacity(atlasCache, atlasWidth, atlasHeight);
            if (atlasLayoutChanged)
            {
                Array.Clear(atlasCache.Pixels, 0, atlasWidth * atlasHeight);
                atlasCache.PreviousAtlasWidth = atlasWidth;
                atlasCache.PreviousAtlasHeight = atlasHeight;
            }

            int columns = Math.Max(1, atlasWidth / SpriteAtlasCellStride);
            for (int i = 0; i < entries.Count; i++)
            {
                SpriteBuildEntry entry = entries[i];
                int cellX = entry.AtlasSlot % columns;
                int cellY = entry.AtlasSlot / columns;
                int atlasCellX = cellX * SpriteAtlasCellStride;
                int atlasCellY = cellY * SpriteAtlasCellStride;
                int atlasPixelX = atlasCellX + SpriteAtlasPadding;
                int atlasPixelY = atlasCellY + SpriteAtlasPadding;

                if (forceRefreshAllCells ||
                    atlasLayoutChanged ||
                    atlasCache.PreviousSpriteSources[entry.AtlasSlot] != entry.SpritePixels ||
                    atlasCache.PreviousSpriteTintFlags[entry.AtlasSlot] != entry.TintHitFlash)
                {
                    CopySpriteToAtlas(atlasCache.Pixels, entry.SpritePixels, atlasCellX, atlasCellY, atlasWidth, entry.TintHitFlash);
                    if (!atlasCache.UploadFullAtlas)
                    {
                        AddDirtySpriteAtlasRect(atlasCache, atlasCellX, atlasCellY, SpriteAtlasCellStride, SpriteAtlasCellStride);
                    }

                    atlasCache.PreviousSpriteSources[entry.AtlasSlot] = entry.SpritePixels;
                    atlasCache.PreviousSpriteTintFlags[entry.AtlasSlot] = entry.TintHitFlash;
                }

                WorldSpriteInstance instance = entry.Instance;
                instance.AtlasIndex = entry.AtlasSlot;
                float cellU0 = atlasPixelX;
                float cellU1 = atlasPixelX + SpriteCellSize - 1;
                if (entry.UVCropRight > 0f)
                {
                    float cellUWidth = cellU1 - cellU0;
                    instance.U0 = cellU0 + entry.UVCropLeft * cellUWidth;
                    instance.U1 = cellU0 + entry.UVCropRight * cellUWidth;
                }
                else
                {
                    instance.U0 = cellU0;
                    instance.U1 = cellU1;
                }

                instance.V0 = atlasPixelY;
                instance.V1 = atlasPixelY + SpriteCellSize - 1;
                spriteBuffer[i] = instance;
                AddSpriteDebugProjection(instance, entry.AtlasSlot);
            }
            return new SpritePassBuildResult
            {
                Sprites = spriteBuffer,
                SpriteCount = entries.Count,
                AtlasPixels = atlasCache.Pixels,
                AtlasWidth = atlasWidth,
                AtlasHeight = atlasHeight,
                UploadFullAtlas = atlasCache.UploadFullAtlas,
                DirtyRects = atlasCache.DirtyRects,
                DirtyRectCount = atlasCache.DirtyRectCount
            };
        }

        /// <summary>
        /// beamEntriesë¥?ê¹Šì´ ?¤ë¦„ì°¨ìˆœ?¼ë¡œ ?•ë ¬????beamBuffer??WorldBeamInstanceë¥?ê¸°ë¡?œë‹¤.
        /// </summary>
        /// <returns>ë¹Œë“œ???ˆì´?€ ë¹??¸ìŠ¤?´ìŠ¤ ?? beamEntriesê°€ ë¹„ì–´ ?ˆìœ¼ë©?0.</returns>
        private int BuildBeamBuffer()
        {
            if (beamEntries.Count <= 0)
            {
                return 0;
            }

            if (beamBuffer == null || beamBuffer.Length < beamEntries.Count)
            {
                beamBuffer = new WorldBeamInstance[Math.Max(beamEntries.Count, beamBuffer == null ? 8 : beamBuffer.Length * 2)];
            }

            beamEntries.Sort((a, b) => a.SortKey.CompareTo(b.SortKey));
            for (int i = 0; i < beamEntries.Count; i++)
            {
                beamBuffer[i] = beamEntries[i].Instance;
            }

            return beamEntries.Count;
        }

        /// <summary>
        /// ?„í??¼ìŠ¤ ìºì‹œ??DirtyRects ë°°ì—´??ë³€ê²½ëœ ?€ ?ì—­??ì¶”ê??œë‹¤.
        /// ë°°ì—´ ?©ëŸ‰??ë¶€ì¡±í•˜ë©???ë°°ë¡œ ?•ì¥?œë‹¤.
        /// </summary>
        /// <param name="atlasCache">?”í‹° ?¬ê°?•ì„ ì¶”ê????„í??¼ìŠ¤ ìºì‹œ.</param>
        /// <param name="x">?”í‹° ?ì—­???¼ìª½ X ì¢Œí‘œ(?„í??¼ìŠ¤ ?½ì? ê¸°ì?).</param>
        /// <param name="y">?”í‹° ?ì—­???„ìª½ Y ì¢Œí‘œ(?„í??¼ìŠ¤ ?½ì? ê¸°ì?).</param>
        /// <param name="width">?”í‹° ?ì—­???ˆë¹„(?½ì?).</param>
        /// <param name="height">?”í‹° ?ì—­???’ì´(?½ì?).</param>
        private void AddDirtySpriteAtlasRect(SpriteAtlasCache atlasCache, int x, int y, int width, int height)
        {
            int requiredLength = (atlasCache.DirtyRectCount + 1) * 4;
            if (atlasCache.DirtyRects == null || atlasCache.DirtyRects.Length < requiredLength)
            {
                int newLength = atlasCache.DirtyRects == null ? 16 : atlasCache.DirtyRects.Length * 2;
                while (newLength < requiredLength)
                {
                    newLength *= 2;
                }

                Array.Resize(ref atlasCache.DirtyRects, newLength);
            }

            int baseIndex = atlasCache.DirtyRectCount * 4;
            atlasCache.DirtyRects[baseIndex + 0] = x;
            atlasCache.DirtyRects[baseIndex + 1] = y;
            atlasCache.DirtyRects[baseIndex + 2] = width;
            atlasCache.DirtyRects[baseIndex + 3] = height;
            atlasCache.DirtyRectCount++;
        }

        /// <summary>
        /// ?¤í”„?¼ì´???˜ì— ?°ë¼ ?„í??¼ìŠ¤???´Â·í–‰ ?˜ë? ê²°ì •?˜ê³  ?½ì? ?¬ê¸°ë¥?ë°˜í™˜?œë‹¤.
        /// ê°€?¥í•œ ???•ì‚¬ê°í˜•??ê°€ê¹Œìš´ ?ˆì´?„ì›ƒ???¬ìš©?œë‹¤.
        /// </summary>
        /// <param name="spriteCount">?„í??¼ìŠ¤??ë°°ì¹˜???¤í”„?¼ì´????</param>
        /// <param name="width">ê³„ì‚°???„í??¼ìŠ¤ ?ˆë¹„(?½ì?). spriteCountê°€ 0 ?´í•˜?´ë©´ 0.</param>
        /// <param name="height">ê³„ì‚°???„í??¼ìŠ¤ ?’ì´(?½ì?). spriteCountê°€ 0 ?´í•˜?´ë©´ 0.</param>
        private void GetSpriteAtlasDimensions(int spriteCount, out int width, out int height)
        {
            if (spriteCount <= 0)
            {
                width = 0;
                height = 0;
                return;
            }

            int columns = (int)Math.Ceiling(Math.Sqrt(spriteCount));
            int rows = (spriteCount + columns - 1) / columns;
            width = columns * SpriteAtlasCellStride;
            height = rows * SpriteAtlasCellStride;
        }

        /// <summary>
        /// ?ì˜ ?„ì¬ ? ë‹ˆë©”ì´???„ë ˆ???½ì? ë°°ì—´??ë°˜í™˜?œë‹¤.
        /// ? ë‹ˆë©”ì´???„ë ˆ?„ì´ ?†ìœ¼ë©??•ì  ?¤í”„?¼ì´?¸ë? ?¬ìš©?˜ê³ , ê·¸ê²ƒ???†ìœ¼ë©?null??ë°˜í™˜?œë‹¤.
        /// </summary>
        /// <param name="enemy">?½ì???ê°€?¸ì˜¬ ???¸ìŠ¤?´ìŠ¤.</param>
        /// <returns>?„ì¬ ?„ë ˆ?„ì˜ ?½ì? ë°°ì—´(Color[]). ? íš¨???¤í”„?¼ì´?¸ê? ?†ìœ¼ë©?null.</returns>
        private Color[] GetEnemySpritePixels(Enemy enemy)
        {
            Color[] sprite = textureManager.GetAnimatedEnemySprite(enemy) ?? enemy.Sprite;
            if (sprite == null)
            {
                return null;
            }

            return sprite;
        }

        /// <summary>
        /// ?¬ì‚¬ì²?ì¢…ë¥˜???°ë¼ ë¯¸ë¦¬ ?ì„±???í˜• ?¤í”„?¼ì´???½ì? ë°°ì—´??ë°˜í™˜?œë‹¤.
        /// LaserBeam?€ ë³„ë„ ê²½ë¡œ?ì„œ ì²˜ë¦¬?˜ë?ë¡????¨ìˆ˜?ì„œ??null??ë°˜í™˜?œë‹¤.
        /// </summary>
        /// <param name="projectile">?½ì???ê°€?¸ì˜¬ ?¬ì‚¬ì²??¸ìŠ¤?´ìŠ¤.</param>
        /// <returns>?¬ì‚¬ì²??¤í”„?¼ì´???½ì? ë°°ì—´. ì§€?í•˜ì§€ ?ŠëŠ” ì¢…ë¥˜?´ë©´ null.</returns>
        private Color[] GetProjectileSpritePixels(EnemyProjectile projectile)
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
        /// acidGlobSprite, acidPoolSprite, enemyShotSprite, bossRocketSpriteê°€
        /// ?„ì§ ?ì„±?˜ì? ?Šì•˜?¼ë©´ BuildCircularSpriteë¡?ì´ˆê¸°?”í•œ??
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
        /// ?ˆì´?€ ë¹??¬ì‚¬ì²´ë? ì½”ì–´ ?ˆì´?´ì? ê¸€ë¡œìš° ?ˆì´????ê°œì˜ BeamBuildEntryë¡?ë¶„í•´?˜ì—¬ beamEntries??ì¶”ê??œë‹¤.
        /// ë¹?ê¸¸ì´ê°€ ?ˆë¬´ ì§§ìœ¼ë©?ì¶”ê??˜ì? ?ŠëŠ”??
        /// </summary>
        /// <param name="projectile">?ˆì´?€ ë¹??¬ì‚¬ì²??¸ìŠ¤?´ìŠ¤.</param>
        /// <param name="playerX">?Œë ˆ?´ì–´ ?”ë“œ X ì¢Œí‘œ.</param>
        /// <param name="playerY">?Œë ˆ?´ì–´ ?”ë“œ Y ì¢Œí‘œ.</param>
        /// <param name="dirX">ì¹´ë©”??ë°©í–¥ ë²¡í„° X ?±ë¶„.</param>
        /// <param name="dirY">ì¹´ë©”??ë°©í–¥ ë²¡í„° Y ?±ë¶„.</param>
        /// <param name="planeX">?¬ì˜ ?‰ë©´ ë²¡í„° X ?±ë¶„.</param>
        /// <param name="planeY">?¬ì˜ ?‰ë©´ ë²¡í„° Y ?±ë¶„.</param>
        /// <param name="invDet">?¬ì˜ ?‰ë ¬ ??–‰?¬ì˜ ?‰ë ¬????ˆ˜.</param>
        /// <param name="renderWidth">?Œë” ?€???ˆë¹„(?½ì?).</param>
        /// <param name="renderHeight">?Œë” ?€???’ì´(?½ì?).</param>
        private void AddLaserBeamEntries(
            EnemyProjectile projectile,
            float playerX,
            float playerY,
            float dirX,
            float dirY,
            float planeX,
            float planeY,
            double invDet,
            int renderWidth,
            int renderHeight)
        {
            float dx = projectile.EndX - projectile.X;
            float dy = projectile.EndY - projectile.Y;
            float length = (float)Math.Sqrt((dx * dx) + (dy * dy));
            if (length <= 0.05f)
            {
                return;
            }

            Color coreTint = projectile.WarmupTimer > 0f || projectile.Damage <= 0f
                ? Color.FromArgb(255, 255, 232, 182)
                : Color.FromArgb(255, 255, 132, 96);
            AddLaserBeamEntry(projectile.X, projectile.Y, projectile.EndX, projectile.EndY,
                projectile, playerX, playerY, dirX, dirY, planeX, planeY, invDet, renderWidth, renderHeight, 1f, 0.03f, coreTint);

            Color glowTint = projectile.WarmupTimer > 0f || projectile.Damage <= 0f
                ? Color.FromArgb(120, 255, 235, 170)
                : Color.FromArgb(105, 255, 220, 120);
            AddLaserBeamEntry(projectile.X, projectile.Y, projectile.EndX, projectile.EndY,
                projectile, playerX, playerY, dirX, dirY, planeX, planeY, invDet, renderWidth, renderHeight, 1.65f, 0.015f, glowTint);
        }

        /// <summary>
        /// ?ˆì´?€ ë¹”ì˜ ?”ë“œ ì¢Œí‘œ êµ¬ê°„???”ë©´ ì¢Œí‘œë¡??¬ì˜?˜ê³ , ?˜ë‚˜??WorldBeamInstanceë¥?beamEntries??ì¶”ê??œë‹¤.
        /// ?”ë©´ ë°–ìœ¼ë¡??„ì „??ë²—ì–´??ê²½ìš° ì¶”ê??˜ì? ?ŠëŠ”??
        /// </summary>
        /// <param name="startWorldX">ë¹??œì‘???”ë“œ X ì¢Œí‘œ.</param>
        /// <param name="startWorldY">ë¹??œì‘???”ë“œ Y ì¢Œí‘œ.</param>
        /// <param name="endWorldX">ë¹??ì  ?”ë“œ X ì¢Œí‘œ.</param>
        /// <param name="endWorldY">ë¹??ì  ?”ë“œ Y ì¢Œí‘œ.</param>
        /// <param name="projectile">?Œìœ  ?¬ì‚¬ì²??ê»˜Â·?œì—… ?íƒœ ì°¸ì¡°??.</param>
        /// <param name="playerX">?Œë ˆ?´ì–´ ?”ë“œ X ì¢Œí‘œ.</param>
        /// <param name="playerY">?Œë ˆ?´ì–´ ?”ë“œ Y ì¢Œí‘œ.</param>
        /// <param name="dirX">ì¹´ë©”??ë°©í–¥ ë²¡í„° X ?±ë¶„.</param>
        /// <param name="dirY">ì¹´ë©”??ë°©í–¥ ë²¡í„° Y ?±ë¶„.</param>
        /// <param name="planeX">?¬ì˜ ?‰ë©´ ë²¡í„° X ?±ë¶„.</param>
        /// <param name="planeY">?¬ì˜ ?‰ë©´ ë²¡í„° Y ?±ë¶„.</param>
        /// <param name="invDet">?¬ì˜ ?‰ë ¬ ??–‰?¬ì˜ ?‰ë ¬????ˆ˜.</param>
        /// <param name="renderWidth">?Œë” ?€???ˆë¹„(?½ì?).</param>
        /// <param name="renderHeight">?Œë” ?€???’ì´(?½ì?).</param>
        /// <param name="thicknessMultiplier">ê¸°ë³¸ ?ê»˜??ê³±í•  ë°°ìœ¨. ê¸€ë¡œìš° ?ˆì´?´ëŠ” 1ë³´ë‹¤ ?¬ê²Œ ?¤ì •?œë‹¤.</param>
        /// <param name="depthBias">ê¹Šì´ ?ì • ë°”ì´?´ìŠ¤. ?‘ì„?˜ë¡ ë¹”ì´ ?ìœ¼ë¡??˜ì˜¨??</param>
        /// <param name="tintOverride">ë¹??‰ì¡° ?¬ì •?? null?´ë©´ ?°ìƒ‰???¬ìš©?œë‹¤.</param>
        private void AddLaserBeamEntry(
            float startWorldX,
            float startWorldY,
            float endWorldX,
            float endWorldY,
            EnemyProjectile projectile,
            float playerX,
            float playerY,
            float dirX,
            float dirY,
            float planeX,
            float planeY,
            double invDet,
            int renderWidth,
            int renderHeight,
            float thicknessMultiplier,
            float depthBias,
            Color? tintOverride)
        {
            float startDx = startWorldX - playerX;
            float startDy = startWorldY - playerY;
            float endDx = endWorldX - playerX;
            float endDy = endWorldY - playerY;
            float startDepth = GetProjectedDepth(startDx, startDy, planeX, planeY, invDet);
            float endDepth = GetProjectedDepth(endDx, endDy, planeX, planeY, invDet);
            if (startDepth <= 0.08f && endDepth <= 0.08f)
            {
                return;
            }

            float avgDepth = Math.Max(0.08f, (startDepth + endDepth) * 0.5f);
            float startScreenX = GetProjectedScreenX(startDx, startDy, renderWidth, dirX, dirY, planeX, planeY, invDet);
            float endScreenX = GetProjectedScreenX(endDx, endDy, renderWidth, dirX, dirY, planeX, planeY, invDet);
            float thicknessScale = Math.Max(0.12f, projectile.Radius * ((projectile.WarmupTimer > 0f || projectile.Damage <= 0f) ? 0.34f : 0.42f)) * thicknessMultiplier;
            float startEffectiveDepth = Math.Max(0.08f, startDepth <= 0.08f ? avgDepth : startDepth);
            float endEffectiveDepth = Math.Max(0.08f, endDepth <= 0.08f ? avgDepth : endDepth);
            float startScreenY = renderHeight * 0.5f + (renderHeight * 0.12f) / startEffectiveDepth;
            float endScreenY = renderHeight * 0.5f + (renderHeight * 0.12f) / endEffectiveDepth;
            const float beamViewportMargin = 32f;
            startScreenX = Math.Max(-beamViewportMargin, Math.Min(renderWidth + beamViewportMargin, startScreenX));
            endScreenX = Math.Max(-beamViewportMargin, Math.Min(renderWidth + beamViewportMargin, endScreenX));
            startScreenY = Math.Max(-beamViewportMargin, Math.Min(renderHeight + beamViewportMargin, startScreenY));
            endScreenY = Math.Max(-beamViewportMargin, Math.Min(renderHeight + beamViewportMargin, endScreenY));
            float segmentDx = endScreenX - startScreenX;
            float segmentDy = endScreenY - startScreenY;
            float segmentScreenLength = (float)Math.Sqrt((segmentDx * segmentDx) + (segmentDy * segmentDy));
            if (segmentScreenLength <= 1f)
            {
                return;
            }

            float spriteHeight = Math.Max(3f, GetProjectedHeight(renderHeight, avgDepth, thicknessScale));
            float maxBeamThickness = renderHeight * 0.05f;
            if (spriteHeight > maxBeamThickness)
            {
                spriteHeight = maxBeamThickness;
            }
            float widthScale = Math.Max(1f, segmentScreenLength / Math.Max(1f, spriteHeight));
            float centerScreenX = (startScreenX + endScreenX) * 0.5f;
            float centerScreenY = (startScreenY + endScreenY) * 0.5f;
            if (centerScreenX < -64f || centerScreenX > renderWidth + 64f || centerScreenY < -64f || centerScreenY > renderHeight + 64f)
            {
                return;
            }

            beamEntries.Add(new BeamBuildEntry
            {
                SortKey = -avgDepth,
                Instance = new WorldBeamInstance
                {
                    PositionX = centerScreenX,
                    PositionY = centerScreenY,
                    Scale = spriteHeight,
                    WidthScale = widthScale,
                    SourceWidth = renderWidth,
                    SourceHeight = renderHeight,
                    Depth = avgDepth,
                    DepthBias = depthBias,
                    RotationDegrees = (float)(Math.Atan2(segmentDy, segmentDx) * (180.0 / Math.PI)),
                    TintArgb = (tintOverride ?? Color.White).ToArgb()
                }
            });
        }

        /// <summary>
        /// ?ˆì°¨?ìœ¼ë¡??í˜• ?¤í”„?¼ì´???½ì? ë°°ì—´???ì„±?œë‹¤.
        /// ì¤‘ì‹¬ë¶€??ê°€?¥ìë¦¬ê¹Œì§€ innerColor?ì„œ outerColorë¡?? í˜• ë³´ê°„?˜ë©°, ë°”ê¹¥ìª½ì? ?¬ëª…?˜ë‹¤.
        /// </summary>
        /// <param name="outerColor">?¤í”„?¼ì´???¸ê³½ ?‰ìƒ.</param>
        /// <param name="innerColor">?¤í”„?¼ì´??ì¤‘ì‹¬ ?‰ìƒ.</param>
        /// <param name="outerRadius">?¸ê³½ ë°˜ì?ë¦??•ê·œ?? 0~1 ë²”ìœ„). ??ê°?ë°”ê¹¥?€ ?¬ëª…?˜ë‹¤.</param>
        /// <param name="innerRadius">?´ë? ?¨ìƒ‰ ë°˜ì?ë¦??•ê·œ??. ??ê°??ˆìª½?€ innerColorë¡?ì±„ì›Œì§„ë‹¤.</param>
        /// <returns>GameConfig.TextureSize Ã— TextureSize ?¬ê¸°??Color[] ?½ì? ë°°ì—´.</returns>
        private Color[] BuildCircularSprite(Color outerColor, Color innerColor, float outerRadius, float innerRadius)
        {
            var pixels = new Color[SpriteCellSize * SpriteCellSize];
            float center = (SpriteCellSize - 1) * 0.5f;
            float invRadius = 1f / center;

            for (int y = 0; y < SpriteCellSize; y++)
            {
                for (int x = 0; x < SpriteCellSize; x++)
                {
                    float nx = (x - center) * invRadius;
                    float ny = (y - center) * invRadius;
                    float dist = (float)Math.Sqrt((nx * nx) + (ny * ny));
                    int index = (y * SpriteCellSize) + x;

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
        /// ??Color ê°’ì„ ? í˜• ë³´ê°„?œë‹¤. t=0?´ë©´ a, t=1?´ë©´ bë¥?ë°˜í™˜?œë‹¤.
        /// </summary>
        /// <param name="a">?œì‘ ?‰ìƒ.</param>
        /// <param name="b">???‰ìƒ.</param>
        /// <param name="t">ë³´ê°„ ë¹„ìœ¨(0~1). ë²”ìœ„ë¥?ë²—ì–´?˜ë©´ ?´ë¨?‘ëœ??</param>
        /// <returns>ë³´ê°„??Color.</returns>
        private static Color LerpColor(Color a, Color b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            int aa = (int)(a.A + ((b.A - a.A) * t));
            int rr = (int)(a.R + ((b.R - a.R) * t));
            int gg = (int)(a.G + ((b.G - a.G) * t));
            int bb = (int)(a.B + ((b.B - a.B) * t));
            return Color.FromArgb(aa, rr, gg, bb);
        }

        /// <summary>
        /// ?¤í”„?¼ì´???½ì? ë°°ì—´???„í??¼ìŠ¤ ë²„í¼??ì§€???€??ë³µì‚¬?œë‹¤.
        /// ?¨ë”© ?½ì??ëŠ” ê°€?¥ìë¦??‰ì„ ë³µì œ?˜ì—¬ ?•ë?/?Œì „ ???´ì›ƒ ?€???ì´ì§€ ?Šë„ë¡??œë‹¤.
        /// tintHitFlashê°€ true?´ë©´ ë¶ˆíˆ¬ëª??½ì???R ì±„ë„??255ë¡??¬ë ¤ ?¼ê²© ?¨ê³¼ë¥??œí˜„?œë‹¤.
        /// </summary>
        /// <param name="atlasPixels">?€???„í??¼ìŠ¤ ARGB ?½ì? ë°°ì—´.</param>
        /// <param name="spritePixels">ë³µì‚¬???ŒìŠ¤ ?¤í”„?¼ì´??Color[] ?½ì? ë°°ì—´.</param>
        /// <param name="atlasCellX">?„í??¼ìŠ¤ ???€???¼ìª½ X ì¢Œí‘œ(?¨ë”© ?¬í•¨).</param>
        /// <param name="atlasCellY">?„í??¼ìŠ¤ ???€???„ìª½ Y ì¢Œí‘œ(?¨ë”© ?¬í•¨).</param>
        /// <param name="atlasWidth">?„í??¼ìŠ¤ ?´ë?ì§€???ˆë¹„(?½ì?).</param>
        /// <param name="tintHitFlash">true?´ë©´ ë¶ˆíˆ¬ëª??½ì????¼ê²© ë¹¨ê°• ?‰ì¡°ë¥??ìš©?œë‹¤.</param>
        private void CopySpriteToAtlas(int[] atlasPixels, Color[] spritePixels, int atlasCellX, int atlasCellY, int atlasWidth, bool tintHitFlash)
        {
            if (spritePixels == null)
            {
                return;
            }

            int atlasX = atlasCellX + SpriteAtlasPadding;
            int atlasY = atlasCellY + SpriteAtlasPadding;
            for (int y = 0; y < SpriteCellSize; y++)
            {
                int sourceRow = y * SpriteCellSize;
                int atlasRow = (atlasY + y) * atlasWidth;
                for (int x = 0; x < SpriteCellSize; x++)
                {
                    Color color = spritePixels[sourceRow + x];
                    if (tintHitFlash && color.A > 0)
                    {
                        color = Color.FromArgb(color.A, 255, Math.Min(255, color.G + 40), Math.Min(255, color.B + 40));
                    }

                    atlasPixels[atlasRow + atlasX + x] = color.ToArgb();
                }
            }

            // ?€ ë°”ê¹¥ 1?½ì???ê°€?¥ìë¦??‰ì„ ë³µì œ???ë©´ ?•ë?/?Œì „ ì¤‘ì—??
            // ?´ì›ƒ atlas ?¬ë¡¯ ?‰ì´ ?ì´ì§€ ?Šì•„ ?¤í”„?¼ì´??ê¹¨ì§??ì¤„ì–´? ë‹¤.
            for (int y = 0; y < SpriteCellSize; y++)
            {
                int row = atlasY + y;
                int baseIndex = row * atlasWidth;
                atlasPixels[baseIndex + atlasCellX] = atlasPixels[baseIndex + atlasX];
                atlasPixels[baseIndex + atlasX + SpriteCellSize] = atlasPixels[baseIndex + atlasX + SpriteCellSize - 1];
            }

            int topRow = atlasY * atlasWidth;
            int bottomRow = (atlasY + SpriteCellSize - 1) * atlasWidth;
            int paddingTopRow = atlasCellY * atlasWidth;
            int paddingBottomRow = (atlasY + SpriteCellSize) * atlasWidth;
            for (int x = 0; x < SpriteCellSize; x++)
            {
                atlasPixels[paddingTopRow + atlasX + x] = atlasPixels[topRow + atlasX + x];
                atlasPixels[paddingBottomRow + atlasX + x] = atlasPixels[bottomRow + atlasX + x];
            }

            atlasPixels[(atlasCellY * atlasWidth) + atlasCellX] = atlasPixels[(atlasY * atlasWidth) + atlasX];
            atlasPixels[(atlasCellY * atlasWidth) + atlasX + SpriteCellSize] = atlasPixels[(atlasY * atlasWidth) + atlasX + SpriteCellSize - 1];
            atlasPixels[((atlasY + SpriteCellSize) * atlasWidth) + atlasCellX] = atlasPixels[((atlasY + SpriteCellSize - 1) * atlasWidth) + atlasX];
            atlasPixels[((atlasY + SpriteCellSize) * atlasWidth) + atlasX + SpriteCellSize] = atlasPixels[((atlasY + SpriteCellSize - 1) * atlasWidth) + atlasX + SpriteCellSize - 1];
        }

        /// <summary>
        /// ?¤ë¸Œ?íŠ¸ê°€ ?”ë©´???¬ì˜??ê°€?¥ì„±???ˆëŠ”ì§€ ë¹ ë¥´ê²??ë³„?œë‹¤.
        /// ì¹´ë©”???¤ì— ?ˆê±°???ˆë¬´ ?‘ê±°???”ë©´ ë°–ìœ¼ë¡??„ì „??ë²—ì–´??ê²½ìš° falseë¥?ë°˜í™˜?œë‹¤.
        /// </summary>
        /// <param name="dx">?¤ë¸Œ?íŠ¸ - ?Œë ˆ?´ì–´???”ë“œ X ì°¨ì´.</param>
        /// <param name="dy">?¤ë¸Œ?íŠ¸ - ?Œë ˆ?´ì–´???”ë“œ Y ì°¨ì´.</param>
        /// <param name="scale">?¤ë¸Œ?íŠ¸???”ë“œ ê³µê°„ ?¬ê¸°(?¤ì???.</param>
        /// <param name="renderWidth">?Œë” ?€???ˆë¹„(?½ì?).</param>
        /// <param name="renderHeight">?Œë” ?€???’ì´(?½ì?).</param>
        /// <param name="dirX">ì¹´ë©”??ë°©í–¥ ë²¡í„° X ?±ë¶„.</param>
        /// <param name="dirY">ì¹´ë©”??ë°©í–¥ ë²¡í„° Y ?±ë¶„.</param>
        /// <param name="planeX">?¬ì˜ ?‰ë©´ ë²¡í„° X ?±ë¶„.</param>
        /// <param name="planeY">?¬ì˜ ?‰ë©´ ë²¡í„° Y ?±ë¶„.</param>
        /// <param name="invDet">?¬ì˜ ?‰ë ¬ ??–‰?¬ì˜ ?‰ë ¬????ˆ˜.</param>
        /// <returns>?”ë©´???¼ë??¼ë„ ?¬ì˜??ê°€?¥ì„±???ˆìœ¼ë©?true, ?•ì‹¤??ë³´ì´ì§€ ?Šìœ¼ë©?false.</returns>
        private bool IsPotentiallyVisible(float dx, float dy, float scale, int renderWidth, int renderHeight,
            float dirX, float dirY, float planeX, float planeY, double invDet)
        {
            double transformX = invDet * ((dirY * dx) - (dirX * dy));
            double transformY = invDet * ((-planeY * dx) + (planeX * dy));
            if (transformY <= 0.05)
            {
                return false;
            }

            double spriteScreenX = (renderWidth * 0.5) * (1.0 + (transformX / transformY));
            double spriteHeight = Math.Abs((renderHeight / transformY) * scale);
            if (spriteHeight <= 4.0)
            {
                return false;
            }

            double spriteWidth = spriteHeight;
            double drawStartX = spriteScreenX - (spriteWidth * 0.5);
            double drawEndX = spriteScreenX + (spriteWidth * 0.5);
            return drawEndX >= -16.0 && drawStartX <= renderWidth + 16.0;
        }

        /// <summary>
        /// ?¤ë¸Œ?íŠ¸???”ë“œ ?ë? ì¢Œí‘œë¥?ì¹´ë©”??ì¢Œí‘œê³„ì˜ ê¹Šì´(transformY)ë¡?ë³€?˜í•œ??
        /// ë°˜í™˜ê°’ì´ 0ë³´ë‹¤ ?¬ë©´ ì¹´ë©”???ì— ?ˆëŠ” ê²ƒì´??
        /// </summary>
        /// <param name="dx">?¤ë¸Œ?íŠ¸ - ?Œë ˆ?´ì–´???”ë“œ X ì°¨ì´.</param>
        /// <param name="dy">?¤ë¸Œ?íŠ¸ - ?Œë ˆ?´ì–´???”ë“œ Y ì°¨ì´.</param>
        /// <param name="planeX">?¬ì˜ ?‰ë©´ ë²¡í„° X ?±ë¶„.</param>
        /// <param name="planeY">?¬ì˜ ?‰ë©´ ë²¡í„° Y ?±ë¶„.</param>
        /// <param name="invDet">?¬ì˜ ?‰ë ¬ ??–‰?¬ì˜ ?‰ë ¬????ˆ˜.</param>
        /// <returns>ì¹´ë©”??ì¢Œí‘œê³„ì—???¤ë¸Œ?íŠ¸ê¹Œì????¬ì˜ ê¹Šì´(?‘ìˆ˜ = ì¹´ë©”????.</returns>
        private static float GetProjectedDepth(float dx, float dy, float planeX, float planeY, double invDet)
        {
            return (float)(invDet * ((-planeY * dx) + (planeX * dy)));
        }

        /// <summary>
        /// ?¤ë¸Œ?íŠ¸???”ë“œ ?ë? ì¢Œí‘œë¥??”ë©´ X ì¢Œí‘œë¡?ë³€?˜í•œ??
        /// </summary>
        /// <param name="dx">?¤ë¸Œ?íŠ¸ - ?Œë ˆ?´ì–´???”ë“œ X ì°¨ì´.</param>
        /// <param name="dy">?¤ë¸Œ?íŠ¸ - ?Œë ˆ?´ì–´???”ë“œ Y ì°¨ì´.</param>
        /// <param name="targetWidth">?Œë” ?€???ˆë¹„(?½ì?).</param>
        /// <param name="dirX">ì¹´ë©”??ë°©í–¥ ë²¡í„° X ?±ë¶„.</param>
        /// <param name="dirY">ì¹´ë©”??ë°©í–¥ ë²¡í„° Y ?±ë¶„.</param>
        /// <param name="planeX">?¬ì˜ ?‰ë©´ ë²¡í„° X ?±ë¶„.</param>
        /// <param name="planeY">?¬ì˜ ?‰ë©´ ë²¡í„° Y ?±ë¶„.</param>
        /// <param name="invDet">?¬ì˜ ?‰ë ¬ ??–‰?¬ì˜ ?‰ë ¬????ˆ˜.</param>
        /// <returns>?”ë©´ X ì¢Œí‘œ(?½ì?). ?”ë©´ ì¤‘ì•™??targetWidth * 0.5???´ë‹¹?œë‹¤.</returns>
        private static float GetProjectedScreenX(float dx, float dy, int targetWidth, float dirX, float dirY, float planeX, float planeY, double invDet)
        {
            double transformX = invDet * ((dirY * dx) - (dirX * dy));
            double transformY = invDet * ((-planeY * dx) + (planeX * dy));
            return (float)((targetWidth * 0.5) * (1.0 + (transformX / transformY)));
        }

        /// <summary>
        /// ?¤ë¸Œ?íŠ¸???¬ì˜ ê¹Šì´?€ ?¤ì??¼ì„ ê¸°ë°˜?¼ë¡œ ?”ë©´??ê·¸ë¦´ ?’ì´(?½ì?)ë¥?ê³„ì‚°?œë‹¤.
        /// </summary>
        /// <param name="targetHeight">?Œë” ?€???’ì´(?½ì?).</param>
        /// <param name="depth">?¤ë¸Œ?íŠ¸???¬ì˜ ê¹Šì´.</param>
        /// <param name="scale">?¤ë¸Œ?íŠ¸???”ë“œ ê³µê°„ ?¬ê¸°(?¤ì???.</param>
        /// <returns>?”ë©´??ê·¸ë¦´ ?¤í”„?¼ì´???’ì´(?½ì?). ??ƒ ?‘ìˆ˜.</returns>
        private static float GetProjectedHeight(int targetHeight, float depth, float scale)
        {
            return Math.Abs((targetHeight / Math.Max(0.0001f, depth)) * scale);
        }

        /// <summary>
        /// ?¤ë¸Œ?íŠ¸???¬ì˜ ê¹Šì´?€ ?¤ì??¼ì„ ê¸°ë°˜?¼ë¡œ ?”ë©´ Y ì¤‘ì‹¬ ì¢Œí‘œë¥?ê³„ì‚°?œë‹¤.
        /// ?„ì¬???”ë©´ ?˜ì§ ì¤‘ì•™(targetHeight * 0.5)??ê¸°ì??¼ë¡œ ?œë‹¤.
        /// </summary>
        /// <param name="targetHeight">?Œë” ?€???’ì´(?½ì?).</param>
        /// <param name="depth">?¤ë¸Œ?íŠ¸???¬ì˜ ê¹Šì´.</param>
        /// <param name="scale">?¤ë¸Œ?íŠ¸???”ë“œ ê³µê°„ ?¬ê¸°(?¤ì???.</param>
        /// <returns>?¤í”„?¼ì´?¸ì˜ ?”ë©´ Y ì¤‘ì‹¬ ì¢Œí‘œ(?½ì?).</returns>
        private static float GetProjectedCenterY(int targetHeight, float depth, float scale)
        {
            float spriteHeight = GetProjectedHeight(targetHeight, depth, scale);
            return (targetHeight * 0.5f) + (spriteHeight * 0f);
        }

        /// <summary>
        /// ?¤í”„?¼ì´?¸ì˜ ?”ë©´ X ë²”ìœ„?ì„œ gpuDepthBufferDataë¥?ê²€?¬í•˜??
        /// ?ì–´???˜ë‚˜???´ì—???¤í”„?¼ì´?¸ê? ë²??ì— ?ˆëŠ”ì§€(depth &lt; ë²?ê¹Šì´) ?ì •?œë‹¤.
        /// </summary>
        /// <param name="depth">?¤í”„?¼ì´?¸ì˜ ?¬ì˜ ê¹Šì´.</param>
        /// <param name="drawStartX">?¤í”„?¼ì´???Œë” ?ì—­???¼ìª½ X ì¢Œí‘œ.</param>
        /// <param name="drawEndX">?¤í”„?¼ì´???Œë” ?ì—­???¤ë¥¸ìª?X ì¢Œí‘œ.</param>
        /// <returns>ê°€???´ì´ ?˜ë‚˜ ?´ìƒ?´ë©´ true, ?„ì „??ì°¨í?˜ë©´ false.</returns>
        private bool IsSpriteVisibleAgainstDepth(float depth, int drawStartX, int drawEndX)
        {
            // gpuDepthBufferData(Â±3 min-filter ?ìš©ë³?ë¥??¬ìš©?œë‹¤.
            // CPU RenderEnemies??zBuffer??ê°™ì? ë²„í¼?ì„œ ë³µì‚¬?˜ë?ë¡?
            // GPU ?œì¶œ ?ì •ê³?CPU enemyVisible ?ì •???™ì¼??depth ê¸°ì??¼ë¡œ ?¼ì¹˜?œë‹¤.
            if (gpuDepthBufferData == null || gpuDepthBufferData.Length == 0 || drawEndX < drawStartX)
            {
                return false;
            }

            int start = Math.Max(0, drawStartX);
            int end = Math.Min(gpuDepthBufferData.Length - 1, drawEndX);
            for (int x = start; x <= end; x++)
            {
                if (depth < gpuDepthBufferData[x])
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// ?½ì—… ?¤ë¸Œ?íŠ¸???”ë©´ Y ì¤‘ì‹¬ ì¢Œí‘œë¥?ê³„ì‚°?œë‹¤.
        /// ë°”ë‹¥ ê·¼ì²˜???„ì¹˜?˜ë©° PulseTimer???°ë¼ ?¬ì¸ ê³¡ì„ ?¼ë¡œ ë¶€??bob) ?¨ê³¼ë¥??ìš©?œë‹¤.
        /// </summary>
        /// <param name="targetHeight">?Œë” ?€???’ì´(?½ì?).</param>
        /// <param name="depth">?½ì—…???¬ì˜ ê¹Šì´.</param>
        /// <param name="pulseTimer">ë¶€??? ë‹ˆë©”ì´???€?´ë¨¸(ì´?.</param>
        /// <param name="scale">?½ì—…???”ë“œ ê³µê°„ ?¬ê¸°(?¤ì???.</param>
        /// <returns>?½ì—… ?¤í”„?¼ì´?¸ì˜ ?”ë©´ Y ì¤‘ì‹¬ ì¢Œí‘œ(?½ì?).</returns>
        private static float GetProjectedPickupCenterY(int targetHeight, float depth, float pulseTimer, float scale)
        {
            float bob = (float)Math.Sin(pulseTimer * 3.6f) * 0.08f;
            float spriteHeight = GetProjectedHeight(targetHeight, depth, scale);
            float groundY = targetHeight / 2f + ((targetHeight * 0.34f) / Math.Max(0.0001f, depth)) - (bob * targetHeight * 0.2f);
            return groundY - (spriteHeight * 0.5f);
        }

        /// <summary>
        /// ?¤í”„?¼ì´???”ë©´ X ì¤‘ì‹¬ ì¢Œí‘œë¥??½ì? ê²½ê³„???•ë ¬?œë‹¤.
        /// CPU RenderEnemies?€??ì¢Œí‘œ ?¼ì¹˜ë¥??„í•´ floor + 0.5 ë°©ì‹???¬ìš©?œë‹¤.
        /// </summary>
        /// <param name="value">?•ë ¬???”ë©´ X ì¢Œí‘œ(?½ì?, float).</param>
        /// <returns>?½ì? ê²½ê³„???•ë ¬??X ì¢Œí‘œ.</returns>
        private static float SnapSpriteCenterX(float value)
        {
            return (float)Math.Floor(value) + 0.5f;
        }

        /// <summary>
        /// ?¤í”„?¼ì´???”ë©´ Y ì¤‘ì‹¬ ì¢Œí‘œë¥?ë°˜ì˜¬ë¦¼í•˜??CPU RenderEnemies???•ìˆ˜ ?˜ëˆ—??ê²°ê³¼?€ ?¼ì¹˜?œí‚¨??
        /// Math.Floor + 0.5 ë°©ì‹?€ ?¤ì??¼ë‹¤????1?½ì? ?¤í”„?‹ì„ ë°œìƒ?œí‚¤ë¯€ë¡?Math.Roundë¥??¬ìš©?œë‹¤.
        /// </summary>
        /// <param name="value">?•ë ¬???”ë©´ Y ì¢Œí‘œ(?½ì?, float).</param>
        /// <returns>ë°˜ì˜¬ë¦¼ëœ Y ì¢Œí‘œ(float).</returns>
        private static float SnapSpriteCenterY(float value)
        {
            // CPU RenderEnemies: drawStartY = -spriteHeight/2 + frameH/2 ???¤í”„?¼ì´??ì¤‘ì‹¬ = frameH/2 (?•ìˆ˜ ?˜ëˆ—??
            // Math.Round???•ìˆ˜ ë°˜ì˜¬ë¦¼ìœ¼ë¡?CPU???•ìˆ˜ ?˜ëˆ—??ê²°ê³¼?€ ?¼ì¹˜?œë‹¤.
            // ?´ì „??Math.Floor(value)+0.5f???¤ì??¼ë‹¤????1?½ì? ?¤í”„?‹ì„ ë°œìƒ?œì¼°??
            return (float)Math.Round(value);
        }

        /// <summary>
        /// ?¤í”„?¼ì´???¬ì˜ ?’ì´ë¥?ë°˜ì˜¬ë¦¼í•˜??CPU RenderEnemies???•ìˆ˜ ê²°ê³¼?€ ?¼ì¹˜?œí‚¨??
        /// ìµœì†Ÿê°’ì? 1?½ì??´ë‹¤.
        /// </summary>
        /// <param name="value">ë°˜ì˜¬ë¦¼í•  ?¬ì˜ ?’ì´(?½ì?, float).</param>
        /// <returns>ë°˜ì˜¬ë¦¼ëœ ?¬ì˜ ?’ì´. ìµœì†Ÿê°?1.</returns>
        private static float SnapSpriteScale(float value)
        {
            return Math.Max(1f, (float)Math.Round(value));
        }

        /// <summary>
        /// ?Œë” ?´ìƒ?„ê? GPU ?”ë“œ ìµœë? ?´ìƒ?„ë? ì´ˆê³¼?˜ëŠ” ê²½ìš° ë¹„ìœ¨??? ì??˜ë©´???¤ì??¼ë‹¤?´í•œ??
        /// ?€???´ìƒ?„ëŠ” ì§ìˆ˜ë¡??´ë¦¼?œë‹¤.
        /// </summary>
        /// <param name="renderWidth">?…ë ¥ ?Œë” ?ˆë¹„(?½ì?).</param>
        /// <param name="renderHeight">?…ë ¥ ?Œë” ?’ì´(?½ì?).</param>
        /// <param name="targetWidth">GPU ?”ë“œ ?Œë” ?€???ˆë¹„(?½ì?). ìµœì†Ÿê°?2.</param>
        /// <param name="targetHeight">GPU ?”ë“œ ?Œë” ?€???’ì´(?½ì?). ìµœì†Ÿê°?2.</param>
        private void GetGpuWorldTargetSize(int renderWidth, int renderHeight, out int targetWidth, out int targetHeight)
        {
            if (renderWidth <= 0 || renderHeight <= 0)
            {
                targetWidth = 2;
                targetHeight = 2;
                return;
            }

            double widthScale = renderWidth > GameConfig.GpuWorldMaxRenderWidth
                ? GameConfig.GpuWorldMaxRenderWidth / (double)renderWidth
                : 1.0;
            double heightScale = renderHeight > GameConfig.GpuWorldMaxRenderHeight
                ? GameConfig.GpuWorldMaxRenderHeight / (double)renderHeight
                : 1.0;
            double scale = Math.Min(1.0, Math.Min(widthScale, heightScale));

            targetWidth = Math.Max(2, (int)Math.Round(renderWidth * scale));
            targetHeight = Math.Max(2, (int)Math.Round(renderHeight * scale));
            if ((targetWidth & 1) != 0)
            {
                targetWidth--;
            }

            if ((targetHeight & 1) != 0)
            {
                targetHeight--;
            }

            if (targetWidth <= 0) targetWidth = 2;
            if (targetHeight <= 0) targetHeight = 2;
        }

        /// <summary>
        /// GPU ê²½ë¡œ?ì„œ ê³„ì‚°??gpuDepthBufferData(Â±3 min-filter ?ìš©ë³?ë¥?
        /// CPU ?¤ë²„?ˆì´??depthBuffer??ë³µì‚¬?œë‹¤.
        /// ?´ìƒ?„ê? ?¤ë? ?ŒëŠ” min-filter ë¦¬ìƒ˜?Œë§?¼ë¡œ ì¶•ì†Œ?œë‹¤.
        /// </summary>
        /// <param name="depthBuffer">CPU ?¤ë²„?ˆì´ê°€ ì°¸ì¡°??ê¹Šì´ ë²„í¼(double[]). ???¨ìˆ˜?ì„œ ê°±ì‹ ?œë‹¤.</param>
        /// <param name="renderWidth">CPU ?„ë ˆ?„ë²„???ˆë¹„(?½ì?).</param>
        /// <param name="worldTargetWidth">GPU ?”ë“œ ?Œë” ?€???ˆë¹„(?½ì?).</param>
        private void CopyDepthBufferForCpuOverlays(double[] depthBuffer, int renderWidth, int worldTargetWidth)
        {
            // gpuDepthBufferData??BuildSmoothedDepthBuffer(Radius=3)ë¡?Â±3 min-filter??ë²„í¼??
            // GPU ?¤í”„?¼ì´???°ì´?”ë„ ?™ì¼ ë²„í¼ë¥?ì°¸ì¡°?˜ë?ë¡? CPU ?¤ë²„?ˆì´(ì²´ë ¥ ë°? ?ˆì´?€ ë¹?ê°€
            // ??ë²„í¼ë¥?ê¸°ì??¼ë¡œ ì°¨í ?ì •?˜ë©´ GPU/CPU ê°??ì • ë¶ˆì¼ì¹˜ê? ?œê±°?œë‹¤.
            if (depthBuffer == null || gpuDepthBufferData == null || renderWidth <= 0 || worldTargetWidth <= 0)
            {
                return;
            }

            int copyLength = Math.Min(depthBuffer.Length, renderWidth);

            if (renderWidth == worldTargetWidth)
            {
                // ?´ìƒ???¼ì¹˜: 1:1 ì§ì ‘ ë³µì‚¬
                for (int x = 0; x < copyLength; x++)
                {
                    depthBuffer[x] = gpuDepthBufferData[x];
                }
                return;
            }

            // ?´ìƒ??ë¶ˆì¼ì¹???min-filter ë¦¬ìƒ˜?Œë§.
            // gpuDepthBufferData???´ë? Â±3 ?¤ë¬´?©ëœ ?íƒœ?´ë?ë¡?ì¶”ê? min-filter??
            // ?¤ì??¼ë‹¤??ë§¤í•‘ ?¤ì°¨ë§?ë³´ì •?œë‹¤.
            float scale = (float)worldTargetWidth / renderWidth;
            for (int x = 0; x < copyLength; x++)
            {
                int srcStart = (int)(x * scale);
                int srcEnd = (int)((x + 1) * scale);
                if (srcStart < 0) srcStart = 0;
                if (srcEnd >= worldTargetWidth) srcEnd = worldTargetWidth - 1;

                double minDepth = gpuDepthBufferData[srcStart];
                for (int s = srcStart + 1; s <= srcEnd; s++)
                {
                    if (gpuDepthBufferData[s] < minDepth)
                    {
                        minDepth = gpuDepthBufferData[s];
                    }
                }
                depthBuffer[x] = minDepth;
            }
        }

        /// <summary>
        /// ë²½ê¹Œì§€???˜ì§ ?¬ì˜ ê±°ë¦¬ë¥??Œë”ë§?ê±°ë¦¬ë¡?ë³€?˜í•œ??
        /// NearPlaneë³´ë‹¤ ê°€ê¹Œìš´ ê±°ë¦¬?ëŠ” ?Œí”„???´ë¦¬?‘ì„ ?ìš©?œë‹¤.
        /// </summary>
        /// <param name="wallDistance">?ˆì´ìºìŠ¤?¸ë¡œ ê³„ì‚°??ë²½ê¹Œì§€???˜ì§ ?¬ì˜ ê±°ë¦¬.</param>
        /// <returns>?Œí”„???´ë¦¬?‘ì´ ?ìš©???Œë”ë§?ê±°ë¦¬. ìµœì†Ÿê°’ì? 0.0001.</returns>
        private static double GetRenderDistance(double wallDistance)
        {
            if (wallDistance <= 0.0001)
            {
                return 0.0001;
            }

            if (wallDistance >= GameConfig.NearPlane)
            {
                return wallDistance;
            }

            double delta = GameConfig.NearPlane - wallDistance;
            double blend = Math.Sqrt(delta * delta + GameConfig.NearPlaneSoftness * GameConfig.NearPlaneSoftness);
            return GameConfig.NearPlane - 0.5 * (delta + GameConfig.NearPlaneSoftness - blend);
        }

        /// <summary>
        /// ?¤í”„?¼ì´?¸ê? ?ìœ ?˜ëŠ” ?”ë©´ ??[drawStartX, drawEndX]ë¥?gpuDepthBufferDataë¡??¤ìº”??
        /// ?°ì†??ê°€??visible) êµ¬ê°„ ëª©ë¡??segments??ì±„ìš´??
        /// ??X??spriteDepth &lt; gpuDepthBufferData[X] ????ê°€?œë¡œ ?ì •?œë‹¤(open-space ?¬í•¨).
        /// </summary>
        /// <param name="spriteDepth">?¤í”„?¼ì´?¸ì˜ ?¬ì˜ ê¹Šì´.</param>
        /// <param name="drawStartX">?¤í”„?¼ì´???¬ì˜ ?œì‘ ??</param>
        /// <param name="drawEndX">?¤í”„?¼ì´???¬ì˜ ?????¬í•¨).</param>
        /// <param name="segments">ê²°ê³¼ë¥?ì±„ìš¸ ë¦¬ìŠ¤?? ?¸ì¶œ ?„ì— Clear?œë‹¤.</param>
        private void ComputeVisibleSegments(float spriteDepth, int drawStartX, int drawEndX, List<(int start, int end)> segments)
        {
            segments.Clear();
            if (gpuDepthBufferData == null || gpuDepthBufferData.Length == 0)
            {
                return;
            }

            int bufLen = gpuDepthBufferData.Length;
            int xStart = Math.Max(0, drawStartX);
            int xEnd   = Math.Min(bufLen - 1, drawEndX);

            int runStart = -1;
            for (int x = xStart; x <= xEnd; x++)
            {
                bool visible = spriteDepth < gpuDepthBufferData[x];
                if (visible)
                {
                    if (runStart < 0)
                    {
                        runStart = x;
                    }
                }
                else if (runStart >= 0)
                {
                    segments.Add((runStart, x - 1));
                    runStart = -1;
                }
            }

            if (runStart >= 0)
            {
                segments.Add((runStart, xEnd));
            }
        }

        /// <summary>
        /// depthBufferData??Â±Radius(3) ì¹?min-filterë¥??ìš©?˜ì—¬ gpuDepthBufferData???€?¥í•œ??
        /// GPU ?°ì´?”ê? ?¨ì¼ ë£©ì—…?¼ë¡œ ??ë²„í¼ë¥?ì°¸ì¡°?˜ë©´, ë²?ê²½ê³„ ê·¼ì²˜?ì„œ???¤í”„?¼ì´?¸ê? ?¬ë°”ë¥´ê²Œ ì°¨í?œë‹¤.
        /// ?´ë¦° ê³µê°„(float.MaxValue) ?´ì? ?¸ì ‘ ë²½ì˜ ê¹Šì´ë¥??„íŒŒë°›ì? ?ŠëŠ”??
        /// </summary>
        private void BuildSmoothedDepthBuffer()
        {
            if (depthBufferData == null)
            {
                return;
            }

            int n = depthBufferData.Length;
            if (gpuDepthBufferData == null || gpuDepthBufferData.Length != n)
            {
                gpuDepthBufferData = new float[n];
            }

            const int Radius = 3;
            for (int x = 0; x < n; x++)
            {
                // open-space ??ë²??†ìŒ)?€ ?¸ì ‘ ë²?depthë¥??„íŒŒë°›ì? ?ŠëŠ”??
                // min-filterë¥??ìš©?˜ë©´ float.MaxValueê°€ ?¸ì ‘ ë²?depthë¡??€ì²´ë˜??
                // ?°ì´?”ê? ?´ë‹¹ ?´ì„ ë²½ìœ¼ë¡??˜ëª» ?ì •?˜ê³  ?¤í”„?¼ì´?¸ë? discard?œë‹¤.
                if (depthBufferData[x] >= float.MaxValue)
                {
                    gpuDepthBufferData[x] = float.MaxValue;
                    continue;
                }

                float minD = depthBufferData[x];
                for (int k = 1; k <= Radius; k++)
                {
                    if (x - k >= 0 && depthBufferData[x - k] < minD)
                    {
                        minD = depthBufferData[x - k];
                    }

                    if (x + k < n && depthBufferData[x + k] < minD)
                    {
                        minD = depthBufferData[x + k];
                    }
                }

                gpuDepthBufferData[x] = minD;
            }
        }

        /// <summary>
        /// ë²???ë²„í¼(depthBufferData, wallColumnGeometryBuffer, wallColumnMaterialBuffer,
        /// wallColumnDoorProgressBuffer)ê°€ columnCount??ë§ê²Œ ? ë‹¹?˜ì—ˆ?”ì? ?•ì¸?˜ê³  ?„ìš”?˜ë©´ ?¬í• ?¹í•œ??
        /// </summary>
        /// <param name="columnCount">?Œë” ?€??????= ?Œë” ?ˆë¹„).</param>
        private void EnsureWallColumnBuffers(int columnCount)
        {
            if (depthBufferData == null || depthBufferData.Length != columnCount)
            {
                depthBufferData = new float[columnCount];
            }

            int packedLength = columnCount * 8; // Row 0: ë²?ì§€?¤ë©”?¸ë¦¬, Row 1: ê³„ë‹¨ ë©?riser) ì§€?¤ë©”?¸ë¦¬
            if (wallColumnGeometryBuffer == null || wallColumnGeometryBuffer.Length != packedLength)
            {
                wallColumnGeometryBuffer = new float[packedLength];
            }

            if (wallColumnMaterialBuffer == null || wallColumnMaterialBuffer.Length != packedLength)
            {
                wallColumnMaterialBuffer = new int[packedLength];
            }

            if (wallColumnDoorProgressBuffer == null || wallColumnDoorProgressBuffer.Length != columnCount)
            {
                wallColumnDoorProgressBuffer = new float[columnCount];
            }
        }

        /// <summary>
        /// ?¤í”„?¼ì´???¸ìŠ¤?´ìŠ¤ ë²„í¼ê°€ spriteCount ?´ìƒ???¬ê¸°ë¥?ê°€ì§€ê³??ˆëŠ”ì§€ ?•ì¸?˜ê³  ?„ìš”?˜ë©´ ?¬í• ?¹í•œ??
        /// </summary>
        /// <param name="spriteBuffer">?•ì¸??ë²„í¼ ì°¸ì¡°. ?¬ê¸°ê°€ ë¶€ì¡±í•˜ë©??ˆë¡œ ? ë‹¹?œë‹¤.</param>
        /// <param name="spriteCount">?„ìš”??ìµœì†Œ ?¸ìŠ¤?´ìŠ¤ ??</param>
        private void EnsureSpriteBuffers(ref WorldSpriteInstance[] spriteBuffer, int spriteCount)
        {
            if (spriteBuffer == null || spriteBuffer.Length < spriteCount)
            {
                spriteBuffer = new WorldSpriteInstance[spriteCount];
            }
        }

        /// <summary>
        /// ?¤í”„?¼ì´???”ë²„ê·??¬ì˜ ë²„í¼ê°€ spriteCount ?´ìƒ???¬ê¸°ë¥?ê°€ì§€ê³??ˆëŠ”ì§€ ?•ì¸?˜ê³  ?„ìš”?˜ë©´ ?¬í• ?¹í•œ??
        /// </summary>
        /// <param name="spriteCount">?„ìš”??ìµœì†Œ ??ª© ??</param>
        private void EnsureDebugSpriteBuffer(int spriteCount)
        {
            if (spriteDebugBuffer == null || spriteDebugBuffer.Length < spriteCount)
            {
                spriteDebugBuffer = new SpriteDebugProjection[spriteCount];
            }
        }

        /// <summary>
        /// ???„ë ˆ???œì‘ ???„í??¼ìŠ¤ ìºì‹œ??PreviousUsed ë°°ì—´??ì´ˆê¸°?”í•œ??
        /// ReleaseUnusedSpriteAtlasSlots?ì„œ ???„ë ˆ?„ì— ?¬ìš©?˜ì? ?Šì? ?¬ë¡¯???´ì œ????ê¸°ì????œë‹¤.
        /// </summary>
        /// <param name="atlasCache">ì´ˆê¸°?”í•  ?„í??¼ìŠ¤ ìºì‹œ.</param>
        private void BeginSpriteAtlasFrame(SpriteAtlasCache atlasCache)
        {
            if (atlasCache.PreviousUsed != null && atlasCache.PreviousSpriteCount > 0)
            {
                Array.Clear(atlasCache.PreviousUsed, 0, atlasCache.PreviousSpriteCount);
            }
        }

        /// <summary>
        /// ?´ë²ˆ ?„ë ˆ?„ì— ?¬ìš©?˜ì? ?Šì? ?„í??¼ìŠ¤ ?¬ë¡¯???Œìœ ??ì°¸ì¡°ë¥??´ì œ?˜ê³  free slot?¼ë¡œ ë°˜í™˜?œë‹¤.
        /// </summary>
        /// <param name="atlasCache">?¬ë¡¯???´ì œ???„í??¼ìŠ¤ ìºì‹œ.</param>
        private void ReleaseUnusedSpriteAtlasSlots(SpriteAtlasCache atlasCache)
        {
            for (int i = 0; i < atlasCache.PreviousSpriteCount; i++)
            {
                if (atlasCache.PreviousOwners[i] != null && !atlasCache.PreviousUsed[i])
                {
                    atlasCache.Slots.Remove(atlasCache.PreviousOwners[i]);
                    atlasCache.PreviousOwners[i] = null;
                    atlasCache.PreviousSpriteSources[i] = null;
                    atlasCache.PreviousSpriteTintFlags[i] = false;
                    atlasCache.FreeSlots.Push(i);
                }
            }
        }

        /// <summary>
        /// ?¬ë¡¯ ë°°ì—´???¤ìª½???¨ì•„ ?ˆëŠ” ë¹??ì—­???˜ë¼???¤ìŒ ?„ë ˆ???„í??¼ìŠ¤ ?¬ê¸°ê°€ ì¤„ì–´?????ˆê²Œ ?œë‹¤.
        /// </summary>
        private void TrimUnusedSpriteAtlasTail(SpriteAtlasCache atlasCache)
        {
            if (atlasCache.PreviousOwners == null || atlasCache.PreviousSpriteCount <= 0)
            {
                atlasCache.PreviousSpriteCount = 0;
                return;
            }

            int trimmedCount = atlasCache.PreviousSpriteCount;
            while (trimmedCount > 0 && atlasCache.PreviousOwners[trimmedCount - 1] == null)
            {
                trimmedCount--;
            }

            atlasCache.PreviousSpriteCount = trimmedCount;
        }

        /// <summary>
        /// owner ?¤ë¸Œ?íŠ¸???€???„í??¼ìŠ¤ ?¬ë¡¯??ë°˜í™˜?˜ê±°???ˆë¡œ ? ë‹¹?œë‹¤.
        /// ê°™ì? ownerê°€ ?´ë? ?¬ë¡¯??ê°€ì§€ê³??ˆìœ¼ë©?ê¸°ì¡´ ?¬ë¡¯??ë°˜í™˜?œë‹¤.
        /// ë¹??¬ë¡¯???ˆìœ¼ë©??¬ì‚¬?©í•˜ê³? ?†ìœ¼ë©??„ì—­ ì¹´ìš´?°ë? ì¦ê??œì¼œ ???¬ë¡¯??? ë‹¹?œë‹¤.
        /// </summary>
        /// <param name="atlasCache">?¬ë¡¯??ê´€ë¦¬í•˜???„í??¼ìŠ¤ ìºì‹œ.</param>
        /// <param name="owner">?¬ë¡¯ ?Œìœ ???? ?¬ì‚¬ì²? ?½ì—… ?±ì˜ ?¤ë¸Œ?íŠ¸ ì°¸ì¡°).</param>
        /// <returns>? ë‹¹???„í??¼ìŠ¤ ?¬ë¡¯ ?¸ë±??</returns>
        private int GetOrCreateSpriteAtlasSlot(SpriteAtlasCache atlasCache, object owner)
        {
            if (owner == null)
            {
                return 0;
            }

            if (atlasCache.Slots.TryGetValue(owner, out int existingSlot))
            {
                return existingSlot;
            }

            int slot;
            if (atlasCache.FreeSlots.Count > 0)
            {
                slot = atlasCache.FreeSlots.Pop();
            }
            else
            {
                slot = atlasCache.NextSlot++;
            }

            EnsureSpriteAtlasSlotCapacity(atlasCache, slot + 1);
            if (slot >= atlasCache.PreviousSpriteCount)
            {
                atlasCache.PreviousSpriteCount = slot + 1;
            }

            atlasCache.PreviousOwners[slot] = owner;
            atlasCache.Slots[owner] = slot;
            return slot;
        }

        /// <summary>
        /// ?„í??¼ìŠ¤ ìºì‹œ???¬ë¡¯ ê´€??ë°°ì—´??requiredSlots ?´ìƒ???¬ê¸°ë¥?ê°€ì§€?„ë¡ ?•ì¥?œë‹¤.
        /// ë¶€ì¡±í•˜ë©???ë°°ì”© ?•ì¥?œë‹¤.
        /// </summary>
        /// <param name="atlasCache">?•ì¥???„í??¼ìŠ¤ ìºì‹œ.</param>
        /// <param name="requiredSlots">?„ìš”??ìµœì†Œ ?¬ë¡¯ ??</param>
        private void EnsureSpriteAtlasSlotCapacity(SpriteAtlasCache atlasCache, int requiredSlots)
        {
            if (atlasCache.PreviousOwners == null || atlasCache.PreviousOwners.Length < requiredSlots)
            {
                int newLength = atlasCache.PreviousOwners == null ? 16 : atlasCache.PreviousOwners.Length * 2;
                while (newLength < requiredSlots)
                {
                    newLength *= 2;
                }

                Array.Resize(ref atlasCache.PreviousOwners, newLength);
                Array.Resize(ref atlasCache.PreviousUsed, newLength);
                Array.Resize(ref atlasCache.PreviousSpriteSources, newLength);
                Array.Resize(ref atlasCache.PreviousSpriteTintFlags, newLength);
            }
        }

        /// <summary>
        /// ?˜ë‚˜???¤í”„?¼ì´?¸ë? ?„í??¼ìŠ¤??ë°°ì¹˜?˜ê³  GPU???œì¶œ?˜ê¸° ?„í•œ ì¤‘ê°„ ë¹Œë“œ ??ª©.
        /// ?•ë ¬ ?? ?Œìœ ??ì°¸ì¡°, ?„í??¼ìŠ¤ ?¬ë¡¯, ?½ì? ?ŒìŠ¤, ?¼ê²© ?‰ì¡° ?Œë˜ê·¸ë? ?¬í•¨?œë‹¤.
        /// </summary>
        private struct SpriteBuildEntry
        {
            /// <summary>?ê·¼ ?•ë ¬ ?? ?Œìˆ˜ ê¹Šì´ë¥??¬ìš©?˜ì—¬ ê°€ê¹Œìš´ ?¤í”„?¼ì´?¸ê? ?¤ì— ?„ì¹˜?œë‹¤.</summary>
            public double SortKey;

            /// <summary>?¬ë¡¯ ? ë‹¹ ë°??´ì œ???¬ìš©?˜ëŠ” ?Œìœ ???¤ë¸Œ?íŠ¸ ì°¸ì¡°(?? ?¬ì‚¬ì²? ?½ì—… ??.</summary>
            public object Owner;

            /// <summary>????ª©??? ë‹¹???„í??¼ìŠ¤ ?¬ë¡¯ ?¸ë±??</summary>
            public int AtlasSlot;

            /// <summary>?„í??¼ìŠ¤??ë³µì‚¬???¤í”„?¼ì´???½ì? ë°°ì—´(Color[]).</summary>
            public Color[] SpritePixels;

            /// <summary>true?´ë©´ ?„í??¼ìŠ¤ ë³µì‚¬ ???¼ê²© ë¹¨ê°• ?‰ì¡°ë¥??ìš©?œë‹¤.</summary>
            public bool TintHitFlash;

            /// <summary>
            /// ?ìŠ¤ì²?ê°€ë¡??¬ë¡­ ?œì‘ ë¹„ìœ¨ (0..1). UVCropRightê°€ 0?´ë©´ ?¬ë¡­ ?†ìŒ(?„ì²´).
            /// BuildSpritePass?ì„œ U0???ìš©?œë‹¤.
            /// </summary>
            public float UVCropLeft;

            /// <summary>
            /// ?ìŠ¤ì²?ê°€ë¡??¬ë¡­ ??ë¹„ìœ¨ (0..1). 0?´ë©´ ?¬ë¡­ ?†ìŒ(?„ì²´ = 1ë¡?ì²˜ë¦¬).
            /// BuildSpritePass?ì„œ U1???ìš©?œë‹¤.
            /// </summary>
            public float UVCropRight;

            /// <summary>GPU???œì¶œ??WorldSpriteInstance ?°ì´??</summary>
            public WorldSpriteInstance Instance;
        }

        /// <summary>
        /// ?˜ë‚˜???ˆì´?€ ë¹??¸ê·¸ë¨¼íŠ¸ë¥?GPU???œì¶œ?˜ê¸° ?„í•œ ì¤‘ê°„ ë¹Œë“œ ??ª©.
        /// ?•ë ¬ ?¤ì? WorldBeamInstanceë¥??¬í•¨?œë‹¤.
        /// </summary>
        private struct BeamBuildEntry
        {
            /// <summary>ê¹Šì´ ?•ë ¬ ?? ?Œìˆ˜ ê¹Šì´ë¥??¬ìš©?˜ì—¬ ê°€ê¹Œìš´ ë¹”ì´ ?¤ì— ?„ì¹˜?œë‹¤.</summary>
            public double SortKey;

            /// <summary>GPU???œì¶œ??WorldBeamInstance ?°ì´??</summary>
            public WorldBeamInstance Instance;
        }

        /// <summary>
        /// ?„í??¼ìŠ¤ ?¬ë¡¯ ? ë‹¹ê³??½ì? ?”í‹° ?íƒœë¥?ì¶”ì ?˜ëŠ” ìºì‹œ.
        /// ë§??„ë ˆ???¬ìš©???¬ë¡¯ê³?ë³€ê²½ëœ ?½ì?ë§?GPU???…ë¡œ?œí•˜???€??­???ˆê°?œë‹¤.
        /// </summary>
        private sealed class SpriteAtlasCache
        {
            /// <summary>?„í??¼ìŠ¤ ?„ì²´ ARGB ?½ì? ë°°ì—´.</summary>
            public int[] Pixels;

            /// <summary>ë³€ê²½ëœ ?€ ?ì—­??ê¸°ë¡?˜ëŠ” ?•ìˆ˜ ë°°ì—´(x, y, w, h ?œìœ¼ë¡?4ê°œì”©).</summary>
            public int[] DirtyRects;

            /// <summary>DirtyRects??ê¸°ë¡???”í‹° ?¬ê°?•ì˜ ??</summary>
            public int DirtyRectCount;

            /// <summary>?„í??¼ìŠ¤ ?„ì²´ ?¬ì—…ë¡œë“œê°€ ?„ìš”?˜ë©´ true. ?ˆì´?„ì›ƒ??ë³€ê²½ëœ ê²½ìš° ?¤ì •?œë‹¤.</summary>
            public bool UploadFullAtlas;

            /// <summary>?´ë²ˆ ?„ë ˆ?„ì—???¬ìš©???¬ë¡¯??ìµœë? ?¸ë±??+ 1. ?„í??¼ìŠ¤ ?¬ê¸° ê³„ì‚°???¬ìš©?œë‹¤.</summary>
            public int ActiveSlotSpan;

            /// <summary>ê°??¬ë¡¯???Œìœ ???¤ë¸Œ?íŠ¸ ì°¸ì¡°. null?´ë©´ ?´ë‹¹ ?¬ë¡¯??ë¹„ì–´ ?ˆìŒ???˜í??¸ë‹¤.</summary>
            public object[] PreviousOwners;

            /// <summary>?´ë²ˆ ?„ë ˆ?„ì— ê°??¬ë¡¯???¬ìš©?˜ì—ˆ?”ì? ?¬ë?. BeginSpriteAtlasFrame?ì„œ ì´ˆê¸°?”ëœ??</summary>
            public bool[] PreviousUsed;

            /// <summary>ê°??¬ë¡¯???´ì „ ?„ë ˆ???¤í”„?¼ì´???½ì? ?ŒìŠ¤. ë³€ê²?ê°ì????¬ìš©?œë‹¤.</summary>
            public Color[][] PreviousSpriteSources;

            /// <summary>ê°??¬ë¡¯???´ì „ ?„ë ˆ???¼ê²© ?‰ì¡° ?Œë˜ê·? ë³€ê²?ê°ì????¬ìš©?œë‹¤.</summary>
            public bool[] PreviousSpriteTintFlags;

            /// <summary>? íš¨???¬ë¡¯ ???¬ë¡¯ ?¸ë±?¤ì˜ ?í•œ). ?„í??¼ìŠ¤ ì¹˜ìˆ˜ ê³„ì‚°???¬ìš©?œë‹¤.</summary>
            public int PreviousSpriteCount;

            /// <summary>ë§ˆì?ë§??„í??¼ìŠ¤ ë¹Œë“œ ?œì˜ ?ˆë¹„(?½ì?). ?ˆì´?„ì›ƒ ë³€ê²?ê°ì????¬ìš©?œë‹¤.</summary>
            public int PreviousAtlasWidth;

            /// <summary>ë§ˆì?ë§??„í??¼ìŠ¤ ë¹Œë“œ ?œì˜ ?’ì´(?½ì?). ?ˆì´?„ì›ƒ ë³€ê²?ê°ì????¬ìš©?œë‹¤.</summary>
            public int PreviousAtlasHeight;

            /// <summary>?Œìœ ???¤ë¸Œ?íŠ¸ ì°¸ì¡°ë¥??¬ë¡¯ ?¸ë±?¤ë¡œ ë§¤í•‘?œë‹¤.</summary>
            public readonly Dictionary<object, int> Slots;

            /// <summary>?´ì œ???¬ë¡¯ ?¸ë±?¤ë? ë³´ê??˜ëŠ” ?„ì—­ free-list ?¤íƒ?´ë‹¤.</summary>
            public readonly Stack<int> FreeSlots;

            /// <summary>???¬ë¡¯ ? ë‹¹ ???¬ìš©???¤ìŒ ?„ì—­ ?¬ë¡¯ ?¸ë±?¤ë‹¤.</summary>
            public int NextSlot;

            /// <summary>
            /// SpriteAtlasCacheë¥?ì´ˆê¸°?”í•œ??
            /// ?¬ë¡¯ ë§µê³¼ free-listë¥?ì´ˆê¸°?”í•œ??
            /// </summary>
            public SpriteAtlasCache()
            {
                Slots = new Dictionary<object, int>(ReferenceComparer.Instance);
                FreeSlots = new Stack<int>();
            }
        }

        /// <summary>
        /// ì°¸ì¡° ?™ì¼??ReferenceEquals)ë§?ë¹„êµ?˜ëŠ” IEqualityComparer êµ¬í˜„.
        /// SpriteAtlasCache???¬ë¡¯ ë§??•ì…”?ˆë¦¬?ì„œ ?¤ë¸Œ?íŠ¸ ì°¸ì¡°ë¥??¤ë¡œ ?¬ìš©?????„ìš”?˜ë‹¤.
        /// </summary>
        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            /// <summary>?±ê????¸ìŠ¤?´ìŠ¤.</summary>
            public static readonly ReferenceComparer Instance = new();

            /// <summary>
            /// ???¤ë¸Œ?íŠ¸ ì°¸ì¡°ê°€ ?™ì¼?œì?(ê°™ì? ë©”ëª¨ë¦?ì£¼ì†Œ) ?•ì¸?œë‹¤.
            /// </summary>
            /// <param name="x">ë¹„êµ??ì²?ë²ˆì§¸ ?¤ë¸Œ?íŠ¸.</param>
            /// <param name="y">ë¹„êµ????ë²ˆì§¸ ?¤ë¸Œ?íŠ¸.</param>
            /// <returns>??ì°¸ì¡°ê°€ ?™ì¼?˜ë©´ true.</returns>
            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            /// <summary>
            /// RuntimeHelpers.GetHashCodeë¥??¬ìš©?˜ì—¬ ì°¸ì¡° ê¸°ë°˜ ?´ì‹œë¥?ë°˜í™˜?œë‹¤.
            /// </summary>
            /// <param name="obj">?´ì‹œ ì½”ë“œë¥?ê³„ì‚°???¤ë¸Œ?íŠ¸.</param>
            /// <returns>ì°¸ì¡° ê¸°ë°˜ ?´ì‹œ ì½”ë“œ.</returns>
            public int GetHashCode(object obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }

        /// <summary>
        /// ?„í??¼ìŠ¤ ìºì‹œ??Pixels ë°°ì—´??atlasWidth Ã— atlasHeight ?´ìƒ???¬ê¸°ë¥?ê°€ì§€?„ë¡ ?•ì¥?œë‹¤.
        /// ?¬ê¸°ê°€ ë¶€ì¡±í•˜ë©??ˆë¡œ ? ë‹¹?˜ê³  UploadFullAtlasë¥?trueë¡??¤ì •?œë‹¤.
        /// </summary>
        /// <param name="atlasCache">?½ì? ë°°ì—´???•ì¥???„í??¼ìŠ¤ ìºì‹œ.</param>
        /// <param name="atlasWidth">?„ìš”???„í??¼ìŠ¤ ?ˆë¹„(?½ì?).</param>
        /// <param name="atlasHeight">?„ìš”???„í??¼ìŠ¤ ?’ì´(?½ì?).</param>
        private void EnsureSpriteAtlasPixelCapacity(SpriteAtlasCache atlasCache, int atlasWidth, int atlasHeight)
        {
            int atlasPixelCount = atlasWidth * atlasHeight;
            if (atlasCache.Pixels == null || atlasCache.Pixels.Length < atlasPixelCount)
            {
                atlasCache.Pixels = new int[atlasPixelCount];
                atlasCache.PreviousAtlasWidth = atlasWidth;
                atlasCache.PreviousAtlasHeight = atlasHeight;
                atlasCache.UploadFullAtlas = true;
            }
        }

        /// <summary>
        /// ë°??„í™˜ ???„í??¼ìŠ¤ ìºì‹œ ?„ì²´ë¥?ë¹„ì›Œ ??ë²„í¼ë¥??´ì œ?œë‹¤.
        /// </summary>
        private static void ResetSpriteAtlasCache(SpriteAtlasCache atlasCache)
        {
            if (atlasCache == null)
            {
                return;
            }

            atlasCache.Pixels = null;
            atlasCache.DirtyRects = null;
            atlasCache.DirtyRectCount = 0;
            atlasCache.UploadFullAtlas = false;
            atlasCache.ActiveSlotSpan = 0;
            atlasCache.PreviousOwners = null;
            atlasCache.PreviousUsed = null;
            atlasCache.PreviousSpriteSources = null;
            atlasCache.PreviousSpriteTintFlags = null;
            atlasCache.PreviousSpriteCount = 0;
            atlasCache.PreviousAtlasWidth = 0;
            atlasCache.PreviousAtlasHeight = 0;
            atlasCache.Slots.Clear();
            atlasCache.FreeSlots.Clear();
            atlasCache.NextSlot = 0;
        }

        /// <summary>
        /// ?¤í”„?¼ì´???”ë²„ê·??¬ì˜ ?•ë³´ë¥?spriteDebugBuffer??ê¸°ë¡?˜ê³  spriteDebugCountë¥?ì¦ê??œí‚¨??
        /// </summary>
        /// <param name="instance">?¬ì˜ ?•ë³´ë¥?ê°€?¸ì˜¬ WorldSpriteInstance.</param>
        /// <param name="atlasSlot">???¤í”„?¼ì´?¸ì— ? ë‹¹???„í??¼ìŠ¤ ?¬ë¡¯ ?¸ë±??</param>
        private void AddSpriteDebugProjection(WorldSpriteInstance instance, int atlasSlot)
        {
            int index = spriteDebugCount++;
            spriteDebugBuffer[index] = new SpriteDebugProjection
            {
                Kind = instance.Kind,
                X = instance.PositionX,
                Y = instance.PositionY,
                Width = instance.Scale * Math.Max(0.01f, instance.WidthScale),
                Height = instance.Scale,
                Depth = instance.VerticalOffsetFactor,
                AtlasSlot = atlasSlot
            };
        }
    }
}
