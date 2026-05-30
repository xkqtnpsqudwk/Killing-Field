using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using My2DEngine.Engine.Core;
using My2DEngine.Engine.Hosting;
using My2DEngine.Engine.Input;
using My2DEngine.Engine.Rendering;

namespace My2DEngine
{
    /// <summary>
    /// 게임의 메인 폼 클래스. WinForms 수명주기 관리, 메뉴/UI 상태 전환,
    /// 게임 루프 타이머 구동을 담당한다.
    /// 실제 게임 로직은 <see cref="GameLogic"/>, 렌더 백엔드 선택 및 복구는 <see cref="GameHost"/>가 맡는다.
    /// </summary>
    public partial class Form1 : Form
    {
        /// <summary>
        /// 게임의 현재 화면 상태를 나타내는 열거형.
        /// 각 상태에 따라 렌더링할 UI와 처리할 입력이 결정된다.
        /// </summary>
        private enum GameState
        {
            /// <summary>메인 메뉴 화면. 게임 시작, 설정, 종료 버튼이 표시된다.</summary>
            Menu,
            /// <summary>모드 선택 화면. 일반 모드 또는 무한 모드 중 하나를 선택한다.</summary>
            ModeSelect,
            /// <summary>설정 화면. 시야각, 마우스 감도, 창 크기를 조정할 수 있다.</summary>
            Settings,
            /// <summary>게임 플레이 중 상태. 게임 월드가 렌더링되고 마우스가 캡처된다.</summary>
            Playing,
            /// <summary>일시정지 상태. 게임 월드 위에 정지 오버레이가 표시된다.</summary>
            Pause,
            /// <summary>기록 화면. 과거 런 기록 목록을 보여준다.</summary>
            Records
        }

        /// <summary>시야각(FOV) 슬라이더의 최솟값 (도 단위).</summary>
        private const float FovMin = 60f;

        /// <summary>시야각(FOV) 슬라이더의 최댓값 (도 단위).</summary>
        private const float FovMax = 90f;

        /// <summary>마우스 감도 슬라이더의 최솟값.</summary>
        private const float SensMin = 0.001f;

        /// <summary>마우스 감도 슬라이더의 최댓값.</summary>
        private const float SensMax = 0.01f;

        /// <summary>
        /// 월드 내부 렌더 해상도의 너비 (픽셀).
        /// 창 크기와 관계없이 고정되며, 창이 클수록 업스케일되어 출력된다.
        /// </summary>
        private const int GameRenderWidth = 640;

        /// <summary>
        /// 월드 내부 렌더 해상도의 높이 (픽셀).
        /// 창 크기와 관계없이 고정되며, 창이 클수록 업스케일되어 출력된다.
        /// </summary>
        private const int GameRenderHeight = 360;

        /// <summary>
        /// 설정 화면에서 선택할 수 있는 16:9 창 크기 프리셋 목록.
        /// 인덱스 1(1280x720)이 기본값이다.
        /// </summary>
        private static readonly Size[] WindowSizePresets =
        [
            new Size(960, 540),
            new Size(1280, 720),
            new Size(1600, 900),
            new Size(1920, 1080)
        ];

        /// <summary>
        /// 게임 루프를 구동하는 Application.Idle 핸들러가 등록되어 있는지 여부.
        /// WinForms 타이머(해상도 ~15.6ms, 지터) 대신 Idle 루프로 프레임을 구동해
        /// VSync(Present) 페이싱에 맞춰 모니터 주사율로 매끄럽게 돌린다.
        /// </summary>
        private bool gameLoopRunning;

        /// <summary>Win32 메시지 큐를 들여다볼 때 사용하는 메시지 구조체.</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMessage
        {
            public IntPtr Handle;
            public uint Message;
            public IntPtr WParam;
            public IntPtr LParam;
            public uint Time;
            public Point Point;
        }

        /// <summary>메시지를 제거하지 않고 큐에 대기 중인 메시지가 있는지 확인한다(PM_NOREMOVE).</summary>
        [DllImport("user32.dll")]
        private static extern bool PeekMessage(out NativeMessage message, IntPtr hWnd, uint filterMin, uint filterMax, uint flags);

        /// <summary>렌더 백엔드 생성 및 복구를 담당하는 게임 호스트.</summary>
        private readonly GameHost gameHost = new();

        /// <summary>현재 게임 화면 상태. 렌더링 대상과 입력 처리 방식을 결정한다.</summary>
        private GameState gameState = GameState.Menu;

        /// <summary>
        /// 설정 화면이 일시정지 중에 열렸는지 여부.
        /// true이면 설정 종료 시 일시정지 화면으로, false이면 메인 메뉴로 돌아간다.
        /// </summary>
        private bool settingsFromPause;

        /// <summary>메인 메뉴의 "기록" 버튼 영역.</summary>
        private Rectangle recordsButtonRect;
        /// <summary>기록 화면의 "뒤로" 버튼 영역.</summary>
        private Rectangle recordsBackRect;
        /// <summary>기록 화면의 정렬 기준 전환 버튼 영역.</summary>
        private Rectangle recordsSortRect;
        /// <summary>기록 화면 정렬 기준. 0=최신, 1=도달 층, 2=적 처치, 3=플레이 시간.</summary>
        private int recordsSortMode;
        /// <summary>메인 메뉴의 "이어하기" 버튼 영역 (저장된 런이 있을 때만 표시).</summary>
        private Rectangle continueButtonRect;
        /// <summary>메인 메뉴의 "게임 시작" 버튼 영역.</summary>
        private Rectangle startButtonRect;
        /// <summary>메인 메뉴의 "설정" 버튼 영역.</summary>
        private Rectangle settingsButtonRect;
        /// <summary>메인 메뉴의 "게임 종료" 버튼 영역.</summary>
        private Rectangle exitButtonRect;
        /// <summary>모드 선택 화면의 "일반 모드" 버튼 영역.</summary>
        private Rectangle modeNormalRect;
        /// <summary>모드 선택 화면의 "무한 모드" 버튼 영역.</summary>
        private Rectangle modeEndlessRect;
        /// <summary>모드 선택 화면의 "뒤로" 버튼 영역.</summary>
        private Rectangle modeBackRect;
        /// <summary>설정 화면의 시야각(FOV) 슬라이더 영역.</summary>
        private Rectangle fovSliderRect;
        /// <summary>설정 화면의 마우스 감도 슬라이더 영역.</summary>
        private Rectangle sensSliderRect;
        /// <summary>설정 화면의 BGM 볼륨 슬라이더 영역.</summary>
        private Rectangle bgmSliderRect;
        /// <summary>설정 화면의 효과음 볼륨 슬라이더 영역.</summary>
        private Rectangle sfxSliderRect;
        /// <summary>설정 화면의 창 크기 "이전" 버튼 영역.</summary>
        private Rectangle resolutionPrevRect;
        /// <summary>설정 화면의 창 크기 "다음" 버튼 영역.</summary>
        private Rectangle resolutionNextRect;
        /// <summary>설정 화면의 "뒤로" 버튼 영역.</summary>
        private Rectangle backButtonRect;
        /// <summary>일시정지 화면의 "게임 계속" 버튼 영역.</summary>
        private Rectangle pauseResumeRect;
        /// <summary>일시정지 화면의 "설정" 버튼 영역.</summary>
        private Rectangle pauseSettingsRect;
        /// <summary>일시정지 화면의 "메인메뉴" 버튼 영역.</summary>
        private Rectangle pauseMenuRect;
        /// <summary>사망 화면의 "재시작" 버튼 영역.</summary>
        private Rectangle deathRestartRect;
        /// <summary>사망 화면의 "메인메뉴" 버튼 영역.</summary>
        private Rectangle deathMenuRect;

        /// <summary>FOV 슬라이더를 드래그 중인지 여부.</summary>
        private bool draggingFov;
        /// <summary>마우스 감도 슬라이더를 드래그 중인지 여부.</summary>
        private bool draggingSensitivity;
        /// <summary>BGM 볼륨 슬라이더를 드래그 중인지 여부.</summary>
        private bool draggingBgm;
        /// <summary>효과음 볼륨 슬라이더를 드래그 중인지 여부.</summary>
        private bool draggingSfx;
        /// <summary>게임 플레이 중 마우스 커서가 캡처(숨김+중앙 고정) 상태인지 여부.</summary>
        private bool mouseCaptured;
        /// <summary>마우스를 중앙으로 강제 이동 직후 발생하는 이동 이벤트를 무시하기 위한 플래그.</summary>
        private bool suppressMouseMove;
        /// <summary>현재 Invalidate 요청이 대기 중인지 여부. 중복 요청을 방지한다.</summary>
        private bool paintPending;
        /// <summary>메인 메뉴에 표시할 로고 이미지. 파일이 없으면 null.</summary>
        private Image menuLogo;
        /// <summary>메인 메뉴 배경 일러스트. 파일이 없으면 null이며, 이 경우 절차적 배경으로 대체된다.</summary>
        private Image menuBackground;
        /// <summary>버튼 9-slice 프레임 텍스처(기본). 없으면 단색 버튼으로 대체된다.</summary>
        private Image uiButtonFrame;
        /// <summary>버튼 9-slice 프레임 텍스처(호버). 없으면 기본 프레임/단색으로 대체된다.</summary>
        private Image uiButtonFrameHover;
        /// <summary>패널 9-slice 프레임 텍스처. 없으면 단색 패널로 대체된다.</summary>
        private Image uiPanelFrame;
        /// <summary>마지막으로 기록된 마우스 커서 위치 (클라이언트 좌표). 버튼 hover 판정에 사용.</summary>
        private Point lastMousePosition;
        /// <summary>UI 레이아웃이 마지막으로 계산된 클라이언트 너비. 크기 변화 감지에 사용.</summary>
        private int cachedLayoutWidth = -1;
        /// <summary>UI 레이아웃이 마지막으로 계산된 클라이언트 높이. 크기 변화 감지에 사용.</summary>
        private int cachedLayoutHeight = -1;
        /// <summary>폼이 닫히는 중일 때 true가 된다. 게임 루프와 렌더링을 차단한다.</summary>
        private bool shuttingDown;
        /// <summary>F3 키로 토글되는 디버그 HUD 표시 여부.</summary>
        private bool showDebugHud;
        /// <summary>지수 이동 평균으로 부드럽게 처리된 초당 프레임 수.</summary>
        private float smoothedFps;
        /// <summary>지수 이동 평균으로 부드럽게 처리된 프레임당 소요 시간 (밀리초).</summary>
        private float smoothedFrameMs;
        /// <summary>마지막 프레임에 사용된 게임 월드 렌더 해상도. 디버그 HUD에 표시된다.</summary>
        private Size lastWorldRenderSize;
        /// <summary>현재 선택된 창 크기 프리셋의 인덱스 (<see cref="WindowSizePresets"/> 기준).</summary>
        private int currentWindowSizePresetIndex = 1;
        /// <summary>설정 화면에서 편집 중인 임시 설정 값.</summary>
        private WorldSettingsSnapshot pendingSettings;
        /// <summary>설정 화면 편집 세션이 열려 있는지 여부.</summary>
        private bool settingsEditActive;
        /// <summary>설정 값이 메모리에서 변경되었지만 아직 영속 저장되지 않았는지 여부.</summary>
        private bool settingsDirty;

        /// <summary>
        /// Form1을 초기화한다.
        /// 디자이너 컴포넌트 초기화, 메뉴 로고 로드, 창 설정, 페인트 모드 구성,
        /// 게임 루프 타이머 시작, 입력 이벤트 핸들러 등록을 순서대로 수행한다.
        /// </summary>
        public Form1()
        {
            InitializeComponent();
            LoadMenuLogo();
            LoadMenuBackground();
            LoadUiFrames();
            world.SetUiPanelFrame(uiPanelFrame);
            LoadAndApplySettings();
            LoadAppIcon();
            InitializeStateMachine();

            KeyPreview = true;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            ApplyWindowSizePreset(currentWindowSizePresetIndex);

            ConfigurePaintingMode();

            Application.Idle += OnApplicationIdle;
            gameLoopRunning = true;

            KeyDown += OnGameKeyDown;
            KeyUp += OnGameKeyUp;
            MouseDown += OnGameMouseDown;
            MouseUp += OnGameMouseUp;
            MouseMove += OnMouseMove;

            Activated += (s, e) =>
            {
                currentStateHandler.OnActivated();
            };
            Deactivate += (s, e) =>
            {
                Input.Reset();
                ReleaseMouse();
            };
            FormClosing += (s, e) =>
            {
                CommitSettingsEditsIfNeeded();
                shuttingDown = true;
                paintPending = false;
                if (gameLoopRunning)
                {
                    Application.Idle -= OnApplicationIdle;
                    gameLoopRunning = false;
                }
            };
            FormClosed += (s, e) =>
            {
                Input.Reset();
                ReleaseMouse();
                gameHost.Dispose();
                DisposeWorld();
                menuLogo?.Dispose();
                menuLogo = null;
                menuBackground?.Dispose();
                menuBackground = null;
                uiButtonFrame?.Dispose();
                uiButtonFrame = null;
                uiButtonFrameHover?.Dispose();
                uiButtonFrameHover = null;
                uiPanelFrame?.Dispose();
                uiPanelFrame = null;
            };
        }

        /// <summary>
        /// WinForms 페인팅을 D3D11 전용 모드로 고정한다.
        /// Opaque 스타일을 사용하여 WinForms의 기본 배경 페인트를 억제한다.
        /// </summary>
        private void ConfigurePaintingMode()
        {
            DoubleBuffered = false;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, false);
            SetStyle(ControlStyles.Opaque, true);
            UpdateStyles();
        }

        /// <summary>
        /// 다음 프레임 페인트를 예약한다.
        /// force가 true이면 이미 대기 중인 요청을 무시하고 즉시 다시 그리도록 강제한다.
        /// </summary>
        /// <param name="force">이미 대기 중인 페인트 요청을 무시할지 여부.</param>
        private void RequestPaint(bool force = false)
        {
            if (force)
            {
                paintPending = false;
            }

            if (paintPending)
            {
                return;
            }

            paintPending = true;
            Invalidate();
        }

        /// <summary>
        /// Application.Idle 핸들러. 메시지 큐가 비어 있는 동안 게임 틱을 연속 실행한다.
        /// VSync(Present(1))가 프레임을 모니터 주사율로 페이싱하므로 CPU를 무한 점유하지 않고,
        /// 입력 등 새 메시지가 들어오면 루프를 빠져나가 메시지 펌프에 양보한다.
        /// </summary>
        /// <param name="sender">이벤트 발생원 (사용되지 않음).</param>
        /// <param name="e">이벤트 인수 (사용되지 않음).</param>
        private void OnApplicationIdle(object sender, EventArgs e)
        {
            while (IsApplicationIdle())
            {
                if (shuttingDown || IsDisposed || Disposing)
                {
                    return;
                }

                Tick();

                // 최소화 상태에서는 Present가 VSync로 페이싱되지 않아(즉시 반환) 루프가 CPU를 폭주시킨다.
                // 잠깐 쉬어 점유를 막는다(렌더는 RenderNow에서 이미 건너뛴다).
                if (WindowState == FormWindowState.Minimized)
                {
                    System.Threading.Thread.Sleep(8);
                }
            }
        }

        /// <summary>큐에 대기 중인 Win32 메시지가 없으면 true(=아이들 상태)를 반환한다.</summary>
        private static bool IsApplicationIdle()
        {
            return !PeekMessage(out _, IntPtr.Zero, 0, 0, 0);
        }

        /// <summary>
        /// 한 프레임을 처리한다. 시간 갱신, FPS 평활화, 게임 로직 업데이트 후 화면을 그린다.
        /// 폼이 종료 중이거나 해제된 상태이면 즉시 반환한다.
        /// </summary>
        private void Tick()
        {
            if (shuttingDown || IsDisposed || Disposing)
            {
                return;
            }

            Time.Update();
            float dt = Time.DeltaTime;
            if (dt > 0f)
            {
                float fps = 1f / dt;
                float frameMs = dt * 1000f;
                float blend = smoothedFps <= 0f ? 1f : 0.12f;
                smoothedFps += (fps - smoothedFps) * blend;
                smoothedFrameMs += (frameMs - smoothedFrameMs) * blend;
            }

            currentStateHandler.Update();

            RenderNow();
        }

        /// <summary>
        /// 현재 상태를 즉시 한 번 렌더링한다. 게임 루프 틱과 WinForms 페인트 양쪽에서 공유한다.
        /// 렌더러 생성이나 드로우 중 예외가 발생하면 <see cref="GameHost.ReportBackendFailure"/>로 보고한다.
        /// </summary>
        private void RenderNow()
        {
            if (shuttingDown || IsDisposed || Disposing)
            {
                return;
            }

            // 최소화/0크기 상태에서는 그릴 표면이 없으므로 렌더와 Present를 건너뛴다.
            if (WindowState == FormWindowState.Minimized || ClientSize.Width <= 0 || ClientSize.Height <= 0)
            {
                return;
            }

            paintPending = false;
            EnsureUiLayout(ClientSize.Width, ClientSize.Height);

            try
            {
                Renderer renderer = gameHost.CreateRenderer();
                RenderFrame(renderer);
            }
            catch (Exception ex)
            {
                gameHost.ReportBackendFailure(ex.Message);
            }
        }

        /// <summary>
        /// WinForms 페인트 이벤트 핸들러. 크기 변경·노출 등으로 OS가 다시 그리기를 요청할 때
        /// 현재 화면을 렌더링한다. 평상시 프레임 구동은 <see cref="OnApplicationIdle"/> 루프가 담당한다.
        /// </summary>
        /// <param name="e">페인트 이벤트 인수 (GDI 모드에서만 Graphics 객체가 유효하게 사용된다).</param>
        protected override void OnPaint(PaintEventArgs e)
        {
            RenderNow();
        }

        /// <summary>
        /// 현재 게임 상태에 따라 적절한 화면을 렌더러로 그린다.
        /// BeginFrame/EndFrame 호출로 프레임을 감싸며, 디버그 HUD가 켜져 있으면 마지막에 표시한다.
        /// </summary>
        /// <param name="renderer">이번 프레임을 그릴 렌더러 인스턴스.</param>
        private void RenderFrame(Renderer renderer)
        {
            renderer.BeginFrame(Color.Black);
            try
            {
                currentStateHandler.Render(renderer);

                if (showDebugHud)
                {
                    DrawDebugHud(renderer);
                }
            }
            finally
            {
                renderer.EndFrame();
            }
        }

        /// <summary>
        /// 게임 월드를 내부 렌더 해상도(640x360)로 그린 뒤 창 크기에 맞게 업스케일한다.
        /// 프레젠테이션 스케일을 설정하여 UI/오버레이가 창 좌표 기준으로 정렬되도록 한다.
        /// 렌더 완료 후 스케일은 항상 1:1로 초기화된다.
        /// </summary>
        /// <param name="renderer">게임 월드를 그릴 렌더러 인스턴스.</param>
        private void DrawScaledGameWorld(Renderer renderer)
        {
            lastWorldRenderSize = new Size(GameRenderWidth, GameRenderHeight);
            renderer.SetPresentationScale(
                ClientSize.Width / (float)GameRenderWidth,
                ClientSize.Height / (float)GameRenderHeight);
            try
            {
                RenderWorld(renderer, GameRenderWidth, GameRenderHeight, showDebugHud);
            }
            finally
            {
                renderer.SetPresentationScale(1f, 1f);
            }
        }

        /// <summary>
        /// 배경 페인트 이벤트 핸들러. D3D11 모드에서는 WinForms의 기본 배경 지우기를 억제하여
        /// 백버퍼가 덮어쓰기 전에 화면이 깜박이는 현상을 방지한다.
        /// </summary>
        /// <param name="e">페인트 이벤트 인수.</param>
        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }

        /// <summary>
        /// 메인 메뉴에 표시할 로고 이미지를 Game/Images/Logo.png 경로에서 로드한다.
        /// 파일이 존재하지 않으면 menuLogo는 null로 유지되고, 대신 텍스트 타이틀이 표시된다.
        /// </summary>
        private void LoadMenuLogo()
        {
            string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Game", "Images", "Logo.png");
            if (!File.Exists(logoPath))
            {
                return;
            }

            menuLogo = Image.FromFile(logoPath);
        }

        /// <summary>
        /// 메인 메뉴 배경 일러스트를 Game/Images/MenuBackground.png 경로에서 로드한다.
        /// 파일이 없으면 menuBackground는 null로 유지되고, 메뉴는 절차적 배경으로 그려진다.
        /// 16:9 가로 이미지를 권장하며, 화면을 비율 유지(cover)로 채운 뒤 가독성 스크림이 덧씌워진다.
        /// </summary>
        private void LoadMenuBackground()
        {
            string bgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Game", "Images", "MenuBackground.png");
            if (!File.Exists(bgPath))
            {
                return;
            }

            menuBackground = Image.FromFile(bgPath);
        }

        /// <summary>
        /// UI 9-slice 프레임 텍스처(버튼/패널)를 Game/Images/ui 폴더에서 로드한다.
        /// 각 파일이 없으면 해당 필드는 null로 유지되고, 그리기 시 단색으로 대체된다.
        /// </summary>
        private void LoadUiFrames()
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Game", "Images", "ui");
            uiButtonFrame = LoadOptionalImage(Path.Combine(dir, "ButtonFrame.png"));
            uiButtonFrameHover = LoadOptionalImage(Path.Combine(dir, "ButtonFrameHover.png"));
            uiPanelFrame = LoadOptionalImage(Path.Combine(dir, "PanelFrame.png"));
        }

        private static Image LoadOptionalImage(string path)
        {
            return File.Exists(path) ? Image.FromFile(path) : null;
        }

        private void LoadAppIcon()
        {
            string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Game", "Images", "killing Field", "icon.ico");
            if (!File.Exists(icoPath))
            {
                return;
            }

            Icon = new Icon(icoPath);
        }

        /// <summary>
        /// Win32 창 핸들이 생성된 직후 호출된다.
        /// GameHost에 핸들과 현재 클라이언트 크기를 전달하여 D3D11 스왑체인을 초기화한다.
        /// </summary>
        /// <param name="e">이벤트 인수.</param>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            gameHost.UpdateSurface(Handle, ClientSize.Width, ClientSize.Height);
        }

        /// <summary>
        /// 창 크기가 변경될 때 호출된다.
        /// GameHost에 새 클라이언트 크기를 전달하여 D3D11 스왑체인을 리사이즈한다.
        /// </summary>
        /// <param name="e">이벤트 인수.</param>
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (IsHandleCreated)
            {
                gameHost.UpdateSurface(Handle, ClientSize.Width, ClientSize.Height);
            }
        }

        /// <summary>
        /// 지정된 인덱스의 창 크기 프리셋을 적용한다.
        /// 인덱스가 범위를 벗어나면 가장 가까운 끝값으로 클램핑된다.
        /// </summary>
        /// <param name="presetIndex"><see cref="WindowSizePresets"/> 배열의 인덱스.</param>
        private void ApplyWindowSizePreset(int presetIndex)
        {
            if (presetIndex < 0)
            {
                presetIndex = 0;
            }
            else if (presetIndex >= WindowSizePresets.Length)
            {
                presetIndex = WindowSizePresets.Length - 1;
            }

            currentWindowSizePresetIndex = presetIndex;
            ClientSize = WindowSizePresets[currentWindowSizePresetIndex];
        }

        /// <summary>
        /// 현재 창 크기 프리셋 인덱스를 delta만큼 변경한다.
        /// 결과 인덱스가 범위를 벗어나면 변경하지 않는다.
        /// </summary>
        /// <param name="delta">인덱스 변화량. +1은 다음 크기, -1은 이전 크기.</param>
        private void ChangeWindowSizePreset(int delta)
        {
            int currentPresetIndex = settingsEditActive
                ? pendingSettings.WindowSizePresetIndex
                : currentWindowSizePresetIndex;
            int nextIndex = currentPresetIndex + delta;
            if (nextIndex < 0 || nextIndex >= WindowSizePresets.Length)
            {
                return;
            }

            if (settingsEditActive)
            {
                pendingSettings = pendingSettings with { WindowSizePresetIndex = nextIndex };
                MarkSettingsDirty();
                return;
            }

            ApplyWindowSizePreset(nextIndex);
            PersistSettings(CaptureAppliedSettings());
        }

        /// <summary>
        /// 현재 선택된 창 크기 프리셋을 "너비 x 높이" 형식의 문자열로 반환한다.
        /// 설정 화면의 해상도 표시 레이블에 사용된다.
        /// </summary>
        /// <returns>"1280 x 720" 형식의 창 크기 문자열.</returns>
        private string GetCurrentWindowSizeLabel()
        {
            int presetIndex = settingsEditActive
                ? pendingSettings.WindowSizePresetIndex
                : currentWindowSizePresetIndex;
            Size size = WindowSizePresets[presetIndex];
            return size.Width + " x " + size.Height;
        }

        private void LoadAndApplySettings()
        {
            WorldSettingsSnapshot settings = LoadWorldSettings();
            SetWorldFovDegrees(settings.FovDegrees);
            SetWorldMouseSensitivity(settings.MouseSensitivity);
            SetWorldBgmVolume(settings.BgmVolume);
            SetWorldSfxVolume(settings.SfxVolume);
            currentWindowSizePresetIndex = settings.WindowSizePresetIndex;
            pendingSettings = settings;
            settingsEditActive = false;
            settingsDirty = false;
        }

        private WorldSettingsSnapshot CaptureAppliedSettings()
        {
            return new WorldSettingsSnapshot(
                GetWorldFovDegrees(),
                GetWorldMouseSensitivity(),
                GetWorldBgmVolume(),
                GetWorldSfxVolume(),
                currentWindowSizePresetIndex);
        }

        private WorldSettingsSnapshot GetDisplayedSettings()
        {
            return settingsEditActive ? pendingSettings : CaptureAppliedSettings();
        }

        private void BeginSettingsEditSession()
        {
            pendingSettings = CaptureAppliedSettings();
            settingsEditActive = true;
            settingsDirty = false;
        }

        private void ApplySettings(WorldSettingsSnapshot settings)
        {
            SetWorldFovDegrees(settings.FovDegrees);
            SetWorldMouseSensitivity(settings.MouseSensitivity);
            SetWorldBgmVolume(settings.BgmVolume);
            SetWorldSfxVolume(settings.SfxVolume);
            ApplyWindowSizePreset(settings.WindowSizePresetIndex);
        }

        private void PersistSettings(WorldSettingsSnapshot settings)
        {
            SaveWorldSettings(settings);
            settingsDirty = false;
        }

        private void MarkSettingsDirty()
        {
            settingsDirty = true;
        }

        private void CommitSettingsEditsIfNeeded()
        {
            if (!settingsEditActive)
            {
                return;
            }

            if (settingsDirty)
            {
                ApplySettings(pendingSettings);
                PersistSettings(pendingSettings);
            }

            settingsEditActive = false;
            settingsDirty = false;
        }
    }
}
