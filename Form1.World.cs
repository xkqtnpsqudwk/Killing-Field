using System;
using System.Drawing;
using My2DEngine.Engine.Rendering;
using My2DEngine.Game.Core;

namespace My2DEngine
{
    public partial class Form1
    {
        private readonly record struct WorldSettingsSnapshot(
            float FovDegrees,
            float MouseSensitivity,
            int BgmVolume,
            int SfxVolume,
            int WindowSizePresetIndex);

        private readonly record struct WorldDebugSnapshot(
            bool UsesGpuWorldRendering,
            string WorldStatus,
            string LaserStatus);

        private readonly record struct WorldRunRecordSnapshot(
            int FloorReached,
            int EnemiesKilled,
            int BossesKilled,
            int DurationSeconds,
            string EndedAt);

        private readonly record struct WorldRunSummarySnapshot(
            int FloorReached,
            int EnemiesKilled,
            int BossesKilled,
            int DurationSeconds);

        /// <summary>게임 월드 상태 업데이트와 렌더링을 담당하는 게임 로직 인스턴스.</summary>
        private readonly GameLogic world = new();

        private bool HasPermanentStatsOverlay => world.PermanentStatsUiActive;
        private bool HasMouseSelectableOverlay => world.MouseSelectableOverlayActive || HasDeathOverlay;
        private bool HasDeathOverlay => world.IsPlayerDead;
        private bool HasCardRewardOverlay => world.CardRewardActive;
        private bool HasBranchSelectionOverlay => world.BranchSelectionActive;
        private bool CanContinueSavedRun => world.HasRunSave();
        private bool CanStartEndlessRun => world.IsEndlessModeUnlocked();

        private void DisposeWorld()
        {
            world.Dispose();
        }

        private void UpdatePlayingWorld()
        {
            world.Update();
        }

        private void HandleMenuWorldInput()
        {
            world.HandleMenuPermanentStatsInput();
        }

        private void RenderWorld(Renderer renderer, int width, int height, bool showSpriteProjectionDebug)
        {
            world.SetShowSpriteProjectionDebug(showSpriteProjectionDebug);
            try
            {
                world.Render(renderer, width, height);
            }
            finally
            {
                world.SetShowSpriteProjectionDebug(false);
            }
        }

        private void RenderPermanentStatsOverlay(Renderer renderer)
        {
            world.RenderPermanentStatsOverlay(renderer);
        }

        private WorldDebugSnapshot CaptureWorldDebugSnapshot()
        {
            return new WorldDebugSnapshot(
                world.UsesGpuWorldRendering(),
                world.GetGpuWorldStatus() ?? "n/a",
                world.GetLaserRenderStatus() ?? "n/a");
        }

        private void StartNormalRunSession()
        {
            world.DeleteRunProgress();
            world.SetDifficultyPreset(GameLogic.DifficultyPreset.Normal);
            world.StartRoguelikeRun();
        }

        private void ContinueSavedRunSession()
        {
            world.ContinueRun();
        }

        private void StartEndlessRunSession()
        {
            world.DeleteRunProgress();
            world.SetDifficultyPreset(GameLogic.DifficultyPreset.Normal);
            world.StartEndlessRun();
        }

        private void CompleteActiveRunForMenuReturn()
        {
            if (gameState != GameState.Playing && gameState != GameState.Pause)
            {
                return;
            }

            if (world.IsPlayerDead)
            {
                world.RecordRunResult();
                world.DeleteRunProgress();
            }
            else
            {
                world.SaveRunProgress();
            }

            world.StopBackgroundMusic();
        }

        private void CompleteDeadRunForRestart()
        {
            if (!world.IsPlayerDead)
            {
                return;
            }

            world.RecordRunResult();
            world.DeleteRunProgress();
            world.StopBackgroundMusic();
        }

        private void SetWorldFovDegrees(float value)
        {
            world.SetFovDegrees(value);
        }

        private float GetWorldFovDegrees()
        {
            return world.GetFovDegrees();
        }

        private void SetWorldMouseSensitivity(float value)
        {
            world.SetMouseSensitivity(value);
        }

        private float GetWorldMouseSensitivity()
        {
            return world.GetMouseSensitivity();
        }

        private void SetWorldBgmVolume(int value)
        {
            world.SetBgmVolume(value);
        }

        private int GetWorldBgmVolume()
        {
            return world.GetBgmVolume();
        }

        private void SetWorldSfxVolume(int value)
        {
            world.SetSfxVolume(value);
        }

        private int GetWorldSfxVolume()
        {
            return world.GetSfxVolume();
        }

        private WorldSettingsSnapshot LoadWorldSettings()
        {
            GameSettings settings = world.LoadSettings();
            return new WorldSettingsSnapshot(
                settings.FovDegrees,
                settings.MouseSensitivity,
                settings.BgmVolume,
                settings.SfxVolume,
                settings.WindowSizePresetIndex);
        }

        private void SaveWorldSettings(WorldSettingsSnapshot settings)
        {
            world.SaveSettings(new GameSettings
            {
                FovDegrees = settings.FovDegrees,
                MouseSensitivity = settings.MouseSensitivity,
                BgmVolume = settings.BgmVolume,
                SfxVolume = settings.SfxVolume,
                WindowSizePresetIndex = settings.WindowSizePresetIndex
            });
        }

        private WorldRunRecordSnapshot[] LoadWorldRunRecords(int limit)
        {
            RunRecord[] records = world.LoadRunRecords(limit);
            var snapshots = new WorldRunRecordSnapshot[records.Length];
            for (int i = 0; i < records.Length; i++)
            {
                snapshots[i] = new WorldRunRecordSnapshot(
                    records[i].FloorReached,
                    records[i].EnemiesKilled,
                    records[i].BossesKilled,
                    records[i].DurationSeconds,
                    records[i].EndedAt);
            }

            return snapshots;
        }

        private WorldRunSummarySnapshot CaptureWorldRunSummarySnapshot()
        {
            RunSummarySnapshot summary = world.CreateRunSummarySnapshot();
            return new WorldRunSummarySnapshot(
                summary.FloorReached,
                summary.EnemiesKilled,
                summary.BossesKilled,
                summary.DurationSeconds);
        }

        private void BeginWorldPrimaryFire()
        {
            world.FireButtonDown();
        }

        private void EndWorldPrimaryFire()
        {
            world.FireButtonUp();
        }

        private void AddWorldMouseDelta(int deltaX)
        {
            world.AddMouseDelta(deltaX);
        }

        private bool TryHandleWorldOverlayClick(Point clientLocation)
        {
            if (HasDeathOverlay)
            {
                return HandleDeathClick(clientLocation);
            }

            if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
            {
                return false;
            }

            float gameX = clientLocation.X * GameRenderWidth / (float)ClientSize.Width;
            float gameY = clientLocation.Y * GameRenderHeight / (float)ClientSize.Height;

            if (HasPermanentStatsOverlay)
            {
                world.NotifyPermanentStatsMouseClick(gameX, gameY);
                return true;
            }

            if (HasCardRewardOverlay)
            {
                world.NotifyCardMouseClick(gameX, gameY);
                return true;
            }

            if (HasBranchSelectionOverlay)
            {
                world.NotifyBranchMouseClick(gameX, gameY);
                return true;
            }

            return false;
        }
    }
}
