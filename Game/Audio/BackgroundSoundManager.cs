using System;
using System.Collections.Generic;
using My2DEngine.Game.Config;

namespace My2DEngine.Game.Audio
{
    /// <summary>
    /// 방(room) 상태에 따라 BGM을 전환하고 반복 재생하는 배경음 전용 매니저다.
    /// 두 개의 Windows Media Player COM 슬롯을 교차 사용하여
    /// 트랙 전환 시 크로스페이드(crossfade) 효과를 선형 보간으로 구현한다.
    /// <see cref="IDisposable"/>을 구현하므로 사용 후 반드시 <see cref="Dispose"/>를 호출해야 한다.
    /// </summary>
    internal sealed class BackgroundSoundManager : IDisposable
    {
        /// <summary>
        /// 단일 BGM 재생 슬롯을 나타내는 내부 데이터 클래스다.
        /// COM 플레이어 인스턴스, 현재 트랙 경로, 카테고리, 볼륨을 함께 보관한다.
        /// </summary>
        private sealed class BackgroundMusicPlayer
        {
            /// <summary>Windows Media Player COM 객체 (dynamic으로 바인딩)</summary>
            public dynamic Player;
            /// <summary>현재 플레이어에 로드된 오디오 파일의 절대 경로</summary>
            public string TrackPath;
            /// <summary>현재 재생 중인 BGM 카테고리. 재생 중이 아니면 null.</summary>
            public BackgroundMusicCategory? Category;
            /// <summary>현재 설정된 볼륨 값 (0 ~ <see cref="BackgroundMusicMaxVolume"/>)</summary>
            public int CurrentVolume;
        }

        /// <summary>BGM 재생 시 적용되는 최대 볼륨 값 (0~100 스케일)</summary>
        private const int BackgroundMusicMaxVolume = 75;

        /// <summary>트랙 전환 크로스페이드 총 소요 시간 (초)</summary>
        private const float BackgroundMusicFadeDuration = 0.6f;

        /// <summary>BGM 재생 상태를 폴링(polling)하는 간격 (초)</summary>
        private const float BackgroundMusicStatusPollInterval = 0.35f;

        /// <summary>WMP playState: 재생이 정지된 상태 코드</summary>
        private const int WmpPlayStateStopped = 1;

        /// <summary>WMP playState: 현재 재생 중인 상태 코드</summary>
        private const int WmpPlayStatePlaying = 3;

        /// <summary>WMP playState: 미디어 재생이 끝난 상태 코드</summary>
        private const int WmpPlayStateMediaEnded = 8;

        /// <summary>카테고리별 재생 목록(트랙 절대 경로 배열)을 저장하는 딕셔너리</summary>
        private readonly Dictionary<BackgroundMusicCategory, string[]> backgroundTrackPaths;

        /// <summary>각 카테고리에서 다음에 재생할 트랙 인덱스를 추적하는 딕셔너리</summary>
        private readonly Dictionary<BackgroundMusicCategory, int> nextBackgroundTrackIndices;

        /// <summary>멀티스레드 환경에서 플레이어 상태 접근을 직렬화하는 동기화 객체</summary>
        private readonly object soundLock;

        /// <summary>크로스페이드용 BGM 슬롯 배열. 인덱스 0과 1을 교대로 사용한다.</summary>
        private readonly BackgroundMusicPlayer[] backgroundPlayers;

        /// <summary>현재 활성(fade-in 중이거나 재생 중)인 BGM 플레이어 슬롯</summary>
        private BackgroundMusicPlayer currentBackgroundPlayer;

        /// <summary>전환 중 fade-out되고 있는 이전 BGM 플레이어 슬롯</summary>
        private BackgroundMusicPlayer fadingOutBackgroundPlayer;

        /// <summary>현재 진행 중인 크로스페이드 경과 시간 (초)</summary>
        private float backgroundTransitionTimer;

        /// <summary>fade-out을 시작할 때의 이전 플레이어 볼륨 (선형 보간 기준값)</summary>
        private int backgroundFadeOutStartVolume;

        /// <summary>크로스페이드 전환이 진행 중이면 true, 완료되었거나 비활성이면 false</summary>
        private bool backgroundTransitionActive;

        /// <summary>BGM 재생 상태 폴링까지 남은 누적 시간 (초)</summary>
        private float backgroundStatusPollTimer;

        /// <summary>이 인스턴스가 이미 Dispose되었는지 나타내는 플래그</summary>
        private bool disposed;

        /// <summary>
        /// 사용자가 설정한 BGM 볼륨 비율 (0~100).
        /// 실제 재생 볼륨은 <see cref="BackgroundMusicMaxVolume"/>에 이 비율을 곱하여 결정된다.
        /// 기본값은 100(최대)이다.
        /// </summary>
        private int bgmVolume;

        public BackgroundSoundManager()
        {
            backgroundTrackPaths = new Dictionary<BackgroundMusicCategory, string[]>();
            nextBackgroundTrackIndices = new Dictionary<BackgroundMusicCategory, int>();
            soundLock = new object();
            backgroundPlayers = new BackgroundMusicPlayer[2];
            bgmVolume = 100;
        }

        /// <summary>
        /// 현재 설정된 BGM 볼륨 비율을 반환한다.
        /// </summary>
        /// <returns>BGM 볼륨 비율 (0~100).</returns>
        public int GetBgmVolume()
        {
            lock (soundLock)
            {
                return bgmVolume;
            }
        }

        /// <summary>
        /// BGM 볼륨을 설정한다. 크로스페이드 중이 아니라면 현재 재생 트랙에도 즉시 반영된다.
        /// </summary>
        /// <param name="volume">
        /// 설정할 볼륨 비율 (0~100).
        /// 0이면 무음, 100이면 <see cref="BackgroundMusicMaxVolume"/>에 해당하는 최대 볼륨이다.
        /// 범위를 벗어난 값은 자동으로 클램프된다.
        /// </param>
        public void SetBgmVolume(int volume)
        {
            lock (soundLock)
            {
                if (volume < 0) volume = 0;
                if (volume > 100) volume = 100;
                bgmVolume = volume;

                // 크로스페이드가 진행 중이 아닐 때만 즉시 반영한다.
                // 전환 중에는 AdvanceBackgroundTransition이 매 프레임 올바른 볼륨을 재계산하므로
                // 다음 프레임에 자동으로 새 설정이 반영된다.
                if (!backgroundTransitionActive && currentBackgroundPlayer != null)
                {
                    SetBackgroundPlayerVolume(currentBackgroundPlayer, GetEffectiveBgmMaxVolume());
                }
            }
        }

        /// <summary>
        /// <see cref="AudioConfig"/>에 정의된 BGM 파일 목록을 카테고리별로 등록하고
        /// 실제 파일 경로를 미리 해석하여 재생 목록을 초기화한다.
        /// 게임 시작 시 한 번 호출하면 이후 런타임 중에는 파일 시스템 접근이 발생하지 않는다.
        /// </summary>
        public void LoadAllSounds()
        {
            RegisterBackgroundPlaylist(BackgroundMusicCategory.Normal, AudioConfig.NormalBackgroundTracks);
            RegisterBackgroundPlaylist(BackgroundMusicCategory.MiniBoss, AudioConfig.MiniBossBackgroundTracks);
            RegisterBackgroundPlaylist(BackgroundMusicCategory.Boss, AudioConfig.BossBackgroundTracks);
        }

        /// <summary>
        /// 매 프레임 호출하여 현재 방 상태에 맞는 BGM을 재생하고
        /// 크로스페이드 전환 및 트랙 순환을 처리한다.
        /// 카테고리가 변경되거나 현재 트랙이 끝날 때 자동으로 다음 트랙으로 전환한다.
        /// </summary>
        /// <param name="category">현재 방 상태에 해당하는 BGM 카테고리</param>
        /// <param name="dt">직전 프레임과의 시간 간격 (초 단위 델타 타임)</param>
        public void UpdateBackgroundMusic(BackgroundMusicCategory category, float dt)
        {
            lock (soundLock)
            {
                EnsureBackgroundPlayers();

                string[] playlist;
                if (!backgroundTrackPaths.TryGetValue(category, out playlist) || playlist == null || playlist.Length == 0)
                {
                    return;
                }

                if (currentBackgroundPlayer == null || string.IsNullOrWhiteSpace(currentBackgroundPlayer.TrackPath))
                {
                    StartBackgroundTransition(category);
                    backgroundStatusPollTimer = BackgroundMusicStatusPollInterval;
                }
                else if (currentBackgroundPlayer.Category != category)
                {
                    StartBackgroundTransition(category);
                    backgroundStatusPollTimer = BackgroundMusicStatusPollInterval;
                }
                else if (!backgroundTransitionActive)
                {
                    backgroundStatusPollTimer -= Math.Max(0f, dt);
                    if (backgroundStatusPollTimer <= 0f)
                    {
                        backgroundStatusPollTimer = BackgroundMusicStatusPollInterval;
                        if (ShouldAdvanceToNextTrack(currentBackgroundPlayer))
                        {
                            StartBackgroundTransition(category);
                        }
                    }
                }

                AdvanceBackgroundTransition(dt);
            }
        }

        /// <summary>
        /// 모든 BGM 재생을 즉시 중단하고 관련 상태를 초기화한다.
        /// 크로스페이드가 진행 중이었더라도 즉시 종료된다.
        /// COM 플레이어 객체 자체는 유지되므로 이후 재사용이 가능하다.
        /// </summary>
        public void StopBackgroundMusic()
        {
            lock (soundLock)
            {
                for (int i = 0; i < backgroundPlayers.Length; i++)
                {
                    StopBackgroundPlayer(backgroundPlayers[i]);
                }

                currentBackgroundPlayer = null;
                fadingOutBackgroundPlayer = null;
                backgroundTransitionTimer = 0f;
                backgroundFadeOutStartVolume = 0;
                backgroundTransitionActive = false;
                backgroundStatusPollTimer = 0f;
            }
        }

        /// <summary>
        /// BGM 재생을 중단하고 모든 COM 플레이어 객체를 해제한다.
        /// <see cref="IDisposable"/> 구현. 이후 이 인스턴스를 사용해서는 안 된다.
        /// 중복 호출은 안전하게 무시된다.
        /// </summary>
        public void Dispose()
        {
            lock (soundLock)
            {
                if (disposed)
                {
                    return;
                }

                StopBackgroundMusic();
                for (int i = 0; i < backgroundPlayers.Length; i++)
                {
                    ReleaseBackgroundPlayer(backgroundPlayers[i]);
                    backgroundPlayers[i] = null;
                }

                disposed = true;
            }
        }

        /// <summary>
        /// <see cref="bgmVolume"/> 비율을 반영한 실제 BGM 최대 볼륨을 계산하여 반환한다.
        /// 반환값은 0 ~ <see cref="BackgroundMusicMaxVolume"/> 범위다.
        /// </summary>
        private int GetEffectiveBgmMaxVolume()
        {
            return (int)(BackgroundMusicMaxVolume * bgmVolume / 100.0);
        }

        /// <summary>
        /// 지정한 카테고리의 BGM 재생 목록을 등록한다.
        /// 각 파일 이름을 실제 절대 경로로 변환하고, 변환에 성공한 파일만 목록에 포함한다.
        /// 변환에 실패한 파일은 로거에 기록되고 건너뛴다.
        /// 유효한 파일이 하나도 없으면 해당 카테고리는 등록되지 않는다.
        /// </summary>
        /// <param name="category">등록할 BGM 카테고리</param>
        /// <param name="fileNames">AudioConfig에서 제공되는 파일 이름(논리 이름) 배열</param>
        private void RegisterBackgroundPlaylist(BackgroundMusicCategory category, string[] fileNames)
        {
            if (fileNames == null || fileNames.Length == 0)
            {
                return;
            }

            var resolvedFiles = new List<string>(fileNames.Length);
            for (int i = 0; i < fileNames.Length; i++)
            {
                string path = ResolveSoundPath(fileNames[i]);
                if (path == null)
                {
                    SoundAudioCommon.LogFailure("Could not resolve background track: " + fileNames[i]);
                    continue;
                }

                resolvedFiles.Add(path);
            }

            if (resolvedFiles.Count == 0)
            {
                return;
            }

            backgroundTrackPaths[category] = resolvedFiles.ToArray();
            nextBackgroundTrackIndices[category] = 0;
        }

        /// <summary>
        /// 크로스페이드를 위한 두 개의 BGM 플레이어 슬롯이 모두 초기화되어 있는지 확인하고,
        /// null인 슬롯을 새 COM 플레이어로 채운다.
        /// COM 생성 및 해제를 최소화하기 위해 슬롯 수를 고정 크기로 유지한다.
        /// </summary>
        private void EnsureBackgroundPlayers()
        {
            for (int i = 0; i < backgroundPlayers.Length; i++)
            {
                if (backgroundPlayers[i] != null)
                {
                    continue;
                }

                backgroundPlayers[i] = CreateBackgroundPlayer();
            }
        }

        /// <summary>
        /// 새 Windows Media Player COM 인스턴스를 생성하고
        /// BGM 재생에 맞게 초기 설정(autoStart 비활성, 볼륨 0, 루프 비활성)을 적용한다.
        /// COM 타입을 가져올 수 없거나 인스턴스 생성에 실패하면 null을 반환한다.
        /// </summary>
        /// <returns>초기화된 <see cref="BackgroundMusicPlayer"/>. 실패하면 null.</returns>
        private BackgroundMusicPlayer CreateBackgroundPlayer()
        {
            try
            {
                Type playerType = SoundAudioCommon.GetWindowsMediaPlayerType("background music");
                if (playerType == null)
                {
                    return null;
                }

                dynamic player = Activator.CreateInstance(playerType);
                player.settings.autoStart = false;
                player.settings.volume = 0;
                player.settings.setMode("loop", false);
                return new BackgroundMusicPlayer
                {
                    Player = player,
                    CurrentVolume = 0
                };
            }
            catch (Exception ex)
            {
                SoundAudioCommon.LogFailure("Failed to create background music player.", ex);
                return null;
            }
        }

        /// <summary>
        /// 지정한 카테고리의 다음 트랙으로 크로스페이드 전환을 시작한다.
        /// 현재 비활성 슬롯에 새 트랙을 로드하고 재생을 시작한 뒤,
        /// 현재 슬롯을 fade-out 대상으로 전환한다.
        /// </summary>
        /// <param name="category">전환할 BGM 카테고리</param>
        private void StartBackgroundTransition(BackgroundMusicCategory category)
        {
            string nextTrackPath = GetNextBackgroundTrackPath(category);
            if (string.IsNullOrWhiteSpace(nextTrackPath))
            {
                return;
            }

            BackgroundMusicPlayer nextPlayer = GetInactiveBackgroundPlayer();
            if (nextPlayer == null)
            {
                return;
            }

            if (!PrepareBackgroundPlayer(nextPlayer, nextTrackPath, category))
            {
                return;
            }

            fadingOutBackgroundPlayer = currentBackgroundPlayer;
            backgroundFadeOutStartVolume = fadingOutBackgroundPlayer == null ? 0 : fadingOutBackgroundPlayer.CurrentVolume;
            currentBackgroundPlayer = nextPlayer;
            backgroundTransitionTimer = 0f;
            backgroundTransitionActive = true;
        }

        /// <summary>
        /// 지정한 플레이어 슬롯에 새 트랙 경로를 설정하고 재생을 시작한다.
        /// 이전 재생을 중단하고 새 URL을 할당한 뒤 볼륨을 0으로 초기화하여 fade-in 준비를 마친다.
        /// </summary>
        /// <param name="player">설정할 플레이어 슬롯</param>
        /// <param name="trackPath">재생할 오디오 파일의 절대 경로</param>
        /// <param name="category">이 슬롯에 연결할 BGM 카테고리</param>
        /// <returns>설정 및 재생 시작에 성공하면 true, 실패하면 false.</returns>
        private bool PrepareBackgroundPlayer(BackgroundMusicPlayer player, string trackPath, BackgroundMusicCategory category)
        {
            if (player == null || player.Player == null)
            {
                return false;
            }

            try
            {
                player.Player.controls.stop();
                player.Player.URL = trackPath;
                player.Player.settings.setMode("loop", false);
                player.Player.settings.volume = 0;
                player.Player.controls.play();
                player.TrackPath = trackPath;
                player.Category = category;
                player.CurrentVolume = 0;
                return true;
            }
            catch (Exception ex)
            {
                SoundAudioCommon.LogFailure("Failed to prepare background track '" + trackPath + "'.", ex);
                return false;
            }
        }

        /// <summary>
        /// 크로스페이드 전환을 매 프레임 진행시킨다.
        /// 경과 시간을 기준으로 새 트랙의 볼륨을 선형으로 올리고,
        /// 이전 트랙의 볼륨을 선형으로 낮춘다.
        /// 전환이 완료되면 이전 플레이어를 중단하고 전환 상태를 초기화한다.
        /// </summary>
        /// <param name="dt">직전 프레임과의 시간 간격 (초)</param>
        private void AdvanceBackgroundTransition(float dt)
        {
            if (currentBackgroundPlayer == null)
            {
                return;
            }

            if (!backgroundTransitionActive)
            {
                // 전환이 없을 때는 사용자 볼륨이 반영된 최대치로 유지한다.
                SetBackgroundPlayerVolume(currentBackgroundPlayer, GetEffectiveBgmMaxVolume());
                return;
            }

            backgroundTransitionTimer += Math.Max(0f, dt);
            float duration = BackgroundMusicFadeDuration <= 0f ? 0.001f : BackgroundMusicFadeDuration;
            float progress = backgroundTransitionTimer / duration;
            if (progress > 1f)
            {
                progress = 1f;
            }

            // 새 트랙 fade-in: 0 → 사용자 볼륨 반영 최대치까지 선형 증가
            SetBackgroundPlayerVolume(currentBackgroundPlayer, (int)Math.Round(GetEffectiveBgmMaxVolume() * progress));

            if (fadingOutBackgroundPlayer != null)
            {
                // 이전 트랙 fade-out: fade 시작 시점 볼륨 → 0까지 선형 감소
                int fadeOutVolume = (int)Math.Round(backgroundFadeOutStartVolume * (1f - progress));
                SetBackgroundPlayerVolume(fadingOutBackgroundPlayer, fadeOutVolume);
            }

            if (progress < 1f)
            {
                return;
            }

            // 크로스페이드 완료: 이전 트랙 정지 및 상태 초기화
            StopBackgroundPlayer(fadingOutBackgroundPlayer);
            fadingOutBackgroundPlayer = null;
            backgroundTransitionActive = false;
            backgroundTransitionTimer = 0f;
            backgroundFadeOutStartVolume = 0;
            SetBackgroundPlayerVolume(currentBackgroundPlayer, GetEffectiveBgmMaxVolume());
        }

        /// <summary>
        /// 현재 재생 중인 트랙이 종료되었거나 fade 시작을 위해 다음 트랙으로 넘어가야 하는지 판단한다.
        /// 재생이 완전히 끝난 경우뿐 아니라 남은 재생 시간이 fade 길이보다 짧아진 경우에도
        /// true를 반환하여 끊김 없는 연속 재생이 되도록 한다.
        /// </summary>
        /// <param name="player">상태를 확인할 BGM 플레이어 슬롯</param>
        /// <returns>다음 트랙으로 전환을 시작해야 하면 true, 계속 재생 중이면 false.</returns>
        private bool ShouldAdvanceToNextTrack(BackgroundMusicPlayer player)
        {
            if (player == null || player.Player == null)
            {
                return true;
            }

            try
            {
                int state = (int)player.Player.playState;
                if (state == WmpPlayStateMediaEnded || state == WmpPlayStateStopped)
                {
                    return true;
                }

                if (state != WmpPlayStatePlaying)
                {
                    return false;
                }

                dynamic media = player.Player.currentMedia;
                if (media == null)
                {
                    return false;
                }

                double duration = media.duration;
                double position = player.Player.controls.currentPosition;
                double remaining = duration - position;
                double advanceThreshold = Math.Max(BackgroundMusicFadeDuration, BackgroundMusicStatusPollInterval + 0.05f);
                return duration > 0.1 && remaining <= advanceThreshold;
            }
            catch (Exception ex)
            {
                SoundAudioCommon.LogFailure("Failed to inspect background player state.", ex);
                return false;
            }
        }

        /// <summary>
        /// 지정한 카테고리의 재생 목록에서 다음 트랙 경로를 가져온다.
        /// 목록을 순환(round-robin)하므로 마지막 트랙 이후 첫 번째 트랙으로 돌아간다.
        /// </summary>
        /// <param name="category">트랙을 가져올 BGM 카테고리</param>
        /// <returns>다음 재생할 트랙의 절대 경로. 목록이 없거나 비어 있으면 null.</returns>
        private string GetNextBackgroundTrackPath(BackgroundMusicCategory category)
        {
            string[] playlist;
            if (!backgroundTrackPaths.TryGetValue(category, out playlist) || playlist == null || playlist.Length == 0)
            {
                return null;
            }

            int nextIndex;
            if (!nextBackgroundTrackIndices.TryGetValue(category, out nextIndex))
            {
                nextIndex = 0;
            }

            if (nextIndex < 0 || nextIndex >= playlist.Length)
            {
                nextIndex = 0;
            }

            nextBackgroundTrackIndices[category] = (nextIndex + 1) % playlist.Length;
            return playlist[nextIndex];
        }

        /// <summary>
        /// 현재 활성 슬롯이 아닌 반대 슬롯(비활성 슬롯)을 반환한다.
        /// 크로스페이드 시 새 트랙을 로드할 슬롯을 결정하는 데 사용한다.
        /// </summary>
        /// <returns>현재 활성 슬롯의 반대편 <see cref="BackgroundMusicPlayer"/>. 슬롯이 없으면 null.</returns>
        private BackgroundMusicPlayer GetInactiveBackgroundPlayer()
        {
            if (backgroundPlayers.Length == 0)
            {
                return null;
            }

            if (currentBackgroundPlayer == null)
            {
                return backgroundPlayers[0];
            }

            return ReferenceEquals(currentBackgroundPlayer, backgroundPlayers[0])
                ? backgroundPlayers[1]
                : backgroundPlayers[0];
        }

        /// <summary>
        /// 지정한 플레이어 슬롯의 볼륨을 설정한다.
        /// 값은 0 ~ <see cref="BackgroundMusicMaxVolume"/> 범위로 클램프된다.
        /// 볼륨이 변경되지 않았다면 COM 호출을 건너뛰어 불필요한 오버헤드를 줄인다.
        /// </summary>
        /// <param name="player">볼륨을 설정할 플레이어 슬롯</param>
        /// <param name="volume">설정할 볼륨 값 (0 ~ <see cref="BackgroundMusicMaxVolume"/>)</param>
        private void SetBackgroundPlayerVolume(BackgroundMusicPlayer player, int volume)
        {
            if (player == null || player.Player == null)
            {
                return;
            }

            if (volume < 0) volume = 0;
            if (volume > BackgroundMusicMaxVolume) volume = BackgroundMusicMaxVolume;

            try
            {
                if (player.CurrentVolume == volume)
                {
                    return;
                }

                player.Player.settings.volume = volume;
                player.CurrentVolume = volume;
            }
            catch (Exception ex)
            {
                SoundAudioCommon.LogFailure("Failed to update background music volume.", ex);
            }
        }

        /// <summary>
        /// 지정한 플레이어 슬롯의 재생을 중단하고 URL 및 메타데이터를 초기화한다.
        /// COM 객체 자체는 유지되므로 이후 다시 사용할 수 있다.
        /// </summary>
        /// <param name="player">중단할 플레이어 슬롯. null이면 아무 작업도 하지 않는다.</param>
        private void StopBackgroundPlayer(BackgroundMusicPlayer player)
        {
            if (player == null || player.Player == null)
            {
                return;
            }

            try
            {
                player.Player.controls.stop();
                player.Player.URL = string.Empty;
                player.CurrentVolume = 0;
                player.TrackPath = null;
                player.Category = null;
            }
            catch (Exception ex)
            {
                SoundAudioCommon.LogFailure("Failed to stop background music player.", ex);
            }
        }

        /// <summary>
        /// 지정한 플레이어 슬롯을 중단하고 COM 객체를 완전히 해제한다.
        /// <see cref="Dispose"/> 시 각 슬롯에 대해 호출된다.
        /// </summary>
        /// <param name="player">해제할 플레이어 슬롯. null이면 아무 작업도 하지 않는다.</param>
        private void ReleaseBackgroundPlayer(BackgroundMusicPlayer player)
        {
            if (player == null)
            {
                return;
            }

            StopBackgroundPlayer(player);
            SoundAudioCommon.ReleaseComObject((object)player.Player, "background music player");
            player.Player = null;
            player.TrackPath = null;
            player.Category = null;
            player.CurrentVolume = 0;
        }

        /// <summary>
        /// 파일 이름으로부터 Sound 폴더 내의 실제 절대 경로를 반환한다.
        /// 내부적으로 <see cref="SoundAudioCommon.ResolveAssetPath"/>를 위임 호출한다.
        /// </summary>
        /// <param name="fileName">찾을 사운드 파일 이름 (예: "bgm_normal_01.mp3")</param>
        /// <returns>파일의 절대 경로. 찾지 못하면 null.</returns>
        private string ResolveSoundPath(string fileName)
        {
            return SoundAudioCommon.ResolveAssetPath("Sound", fileName);
        }
    }
}
