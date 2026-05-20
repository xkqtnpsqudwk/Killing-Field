namespace My2DEngine
{
    public partial class Form1
    {
        private enum SessionLaunchMode
        {
            Normal,
            Continue,
            Endless
        }

        /// <summary>
        /// 모드 선택 화면으로 전환한다.
        /// </summary>
        private void OpenModeSelect()
        {
            EnterMenuState(GameState.ModeSelect);
        }

        /// <summary>
        /// 일반 모드 로그라이크 런을 시작한다.
        /// </summary>
        private void StartNormalGame()
        {
            StartSession(SessionLaunchMode.Normal);
        }

        private void ContinueGame()
        {
            StartSession(SessionLaunchMode.Continue);
        }

        /// <summary>
        /// 해금된 무한 모드를 시작한다.
        /// </summary>
        private void StartEndlessGame()
        {
            StartSession(SessionLaunchMode.Endless);
        }

        /// <summary>
        /// 선택한 세션 시작 모드에 따라 런을 준비하고 플레이 상태로 전환한다.
        /// </summary>
        /// <param name="launchMode">세션 시작 모드.</param>
        private void StartSession(SessionLaunchMode launchMode)
        {
            switch (launchMode)
            {
                case SessionLaunchMode.Normal:
                    StartNormalRunSession();
                    break;
                case SessionLaunchMode.Continue:
                    ContinueSavedRunSession();
                    break;
                case SessionLaunchMode.Endless:
                    StartEndlessRunSession();
                    break;
            }

            EnterPlayingState();
        }

        /// <summary>
        /// 게임을 일시정지 상태로 전환한다.
        /// </summary>
        private void EnterPause()
        {
            EnterMenuState(GameState.Pause);
        }

        /// <summary>
        /// 게임을 Playing 상태로 복귀시킨다.
        /// </summary>
        private void ResumeGame()
        {
            EnterPlayingState();
        }

        /// <summary>
        /// 메인 메뉴에서 설정 화면을 연다.
        /// </summary>
        private void OpenSettingsFromMenu()
        {
            OpenSettings(fromPause: false);
        }

        /// <summary>
        /// 일시정지 중에 설정 화면을 연다.
        /// </summary>
        private void OpenSettingsFromPause()
        {
            OpenSettings(fromPause: true);
        }

        /// <summary>
        /// 설정 화면을 닫고 이전 상태로 돌아간다.
        /// </summary>
        private void ExitSettings()
        {
            CommitSettingsEditsIfNeeded();
            EnterMenuState(settingsFromPause ? GameState.Pause : GameState.Menu);
        }

        /// <summary>
        /// 설정 화면을 연다.
        /// </summary>
        /// <param name="fromPause">일시정지 중에 열렸는지 여부.</param>
        private void OpenSettings(bool fromPause)
        {
            settingsFromPause = fromPause;
            BeginSettingsEditSession();
            TransitionToState(GameState.Settings);
        }

        /// <summary>
        /// 메뉴성 화면 상태로 전환하고 마우스 캡처를 해제한다.
        /// </summary>
        /// <param name="state">전환할 비플레이 상태.</param>
        private void EnterMenuState(GameState state)
        {
            if (state != GameState.Settings)
            {
                settingsFromPause = false;
            }

            TransitionToState(state);
        }

        /// <summary>
        /// 플레이 상태로 전환한다.
        /// </summary>
        private void EnterPlayingState()
        {
            settingsFromPause = false;
            TransitionToState(GameState.Playing);
        }

        /// <summary>
        /// 메인 메뉴로 이동하고 메뉴 레이아웃 캐시를 무효화한다.
        /// </summary>
        private void GoToMenu()
        {
            CompleteActiveRunForMenuReturn();
            EnterMenuState(GameState.Menu);
            InvalidateUiLayoutCache();
        }

        private void RestartDeadRun()
        {
            CompleteDeadRunForRestart();
            StartSession(SessionLaunchMode.Normal);
            InvalidateUiLayoutCache();
        }

        /// <summary>
        /// 다음 페인트에서 UI 레이아웃을 다시 계산하도록 캐시를 무효화한다.
        /// </summary>
        private void InvalidateUiLayoutCache()
        {
            cachedLayoutWidth = -1;
            cachedLayoutHeight = -1;
        }
    }
}
