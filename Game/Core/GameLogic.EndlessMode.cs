using System;

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
    }
}
