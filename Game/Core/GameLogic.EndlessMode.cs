using System;
using System.Drawing;
using My2DEngine.Engine.Rendering;
using My2DEngine.Game.Config;

namespace My2DEngine.Game.Core
{
    /// <summary>
    /// GameLogic의 666층 엔딩 연출과 무한 모드 해금/진입 상태를 담당하는 partial.
    /// </summary>
    public partial class GameLogic
    {
        private const int FinalRoguelikeFloor = 666;
        private const float EndingSequenceDuration = 4.8f;

        private bool endingSequenceActive;
        private bool endingSequenceCompleted;
        private bool endingUnlockedNow;
        private float endingSequenceTimer;
        private bool endlessModeActive;

        private bool IsFinalFloorBossClear()
        {
            return currentFloor == FinalRoguelikeFloor;
        }

        private void BeginFinalEndingSequence()
        {
            endingUnlockedNow = !IsEndlessModeUnlocked();
            UnlockEndlessMode();
            endingSequenceActive = true;
            endingSequenceCompleted = false;
            endingSequenceTimer = EndingSequenceDuration;

            weapon.PendingShot = false;
            weapon.FireButtonHeld = false;
            StopLoopingWeaponEffects();
            interactPromptText = null;
            SetStageStatus("666층 돌파 - 엔딩 연출", EndingSequenceDuration);
        }

        private void UpdateEndingSequence(float dt)
        {
            if (!endingSequenceActive)
            {
                return;
            }

            endingSequenceTimer -= dt;
            if (endingSequenceTimer > 0f)
            {
                return;
            }

            endingSequenceTimer = 0f;
            endingSequenceActive = false;
            endingSequenceCompleted = true;
            SetStageStatus("엔딩 완료 - [E] 무한 모드 진입", 4f);
        }

        private void ResetEndingSequenceState()
        {
            endingSequenceActive = false;
            endingSequenceCompleted = false;
            endingUnlockedNow = false;
            endingSequenceTimer = 0f;
        }

        private void ResetRunEndlessState()
        {
            ResetEndingSequenceState();
            endlessModeActive = false;
        }

        /// <summary>
        /// 무한 모드는 사실상 로그라이크 후반 이후를 가정하므로,
        /// 시작 시점에 기본 보스 누적 성장치를 미리 적용한다.
        /// </summary>
        private static int GetEndlessStartingBossClearCount()
        {
            return Math.Max(0, FinalRoguelikeFloor / 20);
        }

        /// <summary>
        /// 무한 모드는 해금 상태만 저장하므로 직전 런의 실제 휴식 층 분포를 복원할 수 없다.
        /// 시작 시점 전투 층 누적치는 최종 도달 층을 기준으로 근사한다.
        /// </summary>
        private static int GetEndlessStartingClearedCombatFloorCount()
        {
            return Math.Max(0, FinalRoguelikeFloor);
        }

        private void UnlockEndlessMode()
        {
            if (permanentProgression == null)
            {
                permanentProgression = PermanentProgressionData.CreateDefault();
            }

            if (permanentProgression.EndlessModeUnlocked)
            {
                return;
            }

            permanentProgression.EndlessModeUnlocked = true;
            SavePermanentProgression();
        }

        public bool IsEndlessModeUnlocked()
        {
            return permanentProgression != null && permanentProgression.EndlessModeUnlocked;
        }

        public void StartEndlessRun()
        {
            if (!IsEndlessModeUnlocked())
            {
                StartRoguelikeRun();
                return;
            }

            BeginRunRandom();
            bgmChannel.StopBackgroundMusic();
            deathMusicStopped = false;
            runEnemiesKilled = 0;
            runBossesKilled = 0;
            runStartTime = DateTime.UtcNow;
            runEnded = false;
            runEndedAtUtc = default;
            runResultRecorded = false;
            ResetCardRunState();
            ResetPlayerState();
            ResetRunEndlessState();
            currentFloor = FinalRoguelikeFloor;
            restRoomOpportunityCooldownActive = false;
            SetClearedCombatFloorCount(GetEndlessStartingClearedCombatFloorCount());
            SetBossClearEnemyGrowthCount(GetEndlessStartingBossClearCount());
            ApplyCombinedProgressionStats(refillHealth: true, healMaxHealthDelta: false);
            TransitionToNextFloor(commitCurrentFloorGrowth: false);
        }

        private void DrawEndingSequenceUI(Renderer r)
        {
            if (!endingSequenceActive)
            {
                return;
            }

            float fw = GameConfig.GpuWorldMaxRenderWidth;
            float fh = GameConfig.GpuWorldMaxRenderHeight;
            float progress = 1f - (endingSequenceTimer / EndingSequenceDuration);
            if (progress < 0f) progress = 0f;
            if (progress > 1f) progress = 1f;

            int backdropAlpha = (int)(160f + progress * 55f);
            int bandAlpha = (int)(110f + progress * 80f);
            r.DrawRectangle(0f, 0f, fw, fh, Color.FromArgb(backdropAlpha, 0, 0, 0));
            r.DrawRectangle(0f, fh * 0.24f, fw, fh * 0.30f, Color.FromArgb(bandAlpha, 20, 8, 8));

            r.DrawTextCenteredShadow("666층 돌파", fw * 0.5f, fh * 0.34f,
                Color.FromArgb(255, 255, 226, 150), 24f);
            r.DrawTextCenteredShadow("최종 보스를 격파했습니다", fw * 0.5f, fh * 0.405f,
                Color.FromArgb(235, 245, 240, 230), 12f);

            string unlockText = endingUnlockedNow ? "무한 모드 해금" : "무한 모드 유지";
            Color unlockColor = endingUnlockedNow
                ? Color.FromArgb(255, 125, 220, 150)
                : Color.FromArgb(255, 175, 205, 255);
            r.DrawTextCenteredShadow(unlockText, fw * 0.5f, fh * 0.47f, unlockColor, 16f);
            r.DrawTextCenteredShadow("잠시 후 끝없는 층으로 진입할 수 있습니다", fw * 0.5f, fh * 0.53f,
                Color.FromArgb(225, 225, 225, 225), 10f);
        }
    }
}
