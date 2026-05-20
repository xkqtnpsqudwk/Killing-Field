using System;
using System.Drawing;
using System.Windows.Forms;
using My2DEngine.Engine.Rendering;

namespace My2DEngine
{
    public partial class Form1
    {
        /// <summary>GameState 값과 같은 인덱스로 접근하는 상태 핸들러 테이블.</summary>
        private FormStateHandler[] stateHandlers;
        /// <summary>현재 화면 상태의 입력, 업데이트, 렌더링 처리를 위임받는 핸들러.</summary>
        private FormStateHandler currentStateHandler;

        /// <summary>
        /// 상태별 입력/업데이트/렌더 책임을 담당하는 핸들러 집합을 초기화한다.
        /// </summary>
        private void InitializeStateMachine()
        {
            stateHandlers = new FormStateHandler[Enum.GetValues(typeof(GameState)).Length];
            stateHandlers[(int)GameState.Menu] = new MenuStateHandler(this);
            stateHandlers[(int)GameState.ModeSelect] = new ModeSelectStateHandler(this);
            stateHandlers[(int)GameState.Settings] = new SettingsStateHandler(this);
            stateHandlers[(int)GameState.Playing] = new PlayingStateHandler(this);
            stateHandlers[(int)GameState.Pause] = new PauseStateHandler(this);
            stateHandlers[(int)GameState.Records] = new RecordsStateHandler(this);
            currentStateHandler = stateHandlers[(int)gameState];
        }

        /// <summary>
        /// 현재 상태를 다른 화면 상태로 전환한다.
        /// 상태별 진입/퇴장 처리는 반드시 이 메서드를 거쳐 수행해 마우스 캡처와 임시 UI 상태가 한곳에서 정리되게 한다.
        /// </summary>
        /// <param name="state">전환할 대상 상태.</param>
        private void TransitionToState(GameState state)
        {
            FormStateHandler nextState = stateHandlers[(int)state];
            if (currentStateHandler == nextState)
            {
                return;
            }

            currentStateHandler?.OnExit();
            gameState = state;
            currentStateHandler = nextState;
            currentStateHandler.OnEnter();
        }

        /// <summary>
        /// Form1 화면 상태의 공통 생명주기.
        /// WinForms 이벤트는 Form1.Input에서 한 번만 받고, 여기서 현재 상태별 처리로 분기한다.
        /// </summary>
        private abstract class FormStateHandler
        {
            protected FormStateHandler(Form1 owner)
            {
                Owner = owner;
            }

            protected Form1 Owner { get; }

            public virtual void OnEnter()
            {
            }

            public virtual void OnExit()
            {
            }

            public virtual void OnActivated()
            {
            }

            public virtual void Update()
            {
            }

            public abstract void Render(Renderer renderer);

            public virtual void HandleEscape()
            {
            }

            public virtual void HandleLeftClick(Point location)
            {
            }

            public virtual void HandleLeftMouseUp()
            {
            }

            public virtual void HandleMouseMove(MouseEventArgs e)
            {
            }
        }

        /// <summary>
        /// 메뉴/설정/기록처럼 OS 마우스 커서가 필요한 상태의 기본 핸들러.
        /// </summary>
        private abstract class FreeMouseStateHandler(Form1 owner) : FormStateHandler(owner)
        {
            public override void OnEnter()
            {
                Owner.ReleaseMouse();
            }
        }

        /// <summary>
        /// 메인 메뉴 상태. 메뉴 위에 영구 스탯 UI가 열리면 게임 내부 좌표계로 클릭을 전달한다.
        /// </summary>
        private sealed class MenuStateHandler(Form1 owner) : FreeMouseStateHandler(owner)
        {
            public override void Update()
            {
                Owner.HandleMenuWorldInput();
            }

            public override void Render(Renderer renderer)
            {
                Owner.DrawMenu(renderer, Owner.ClientSize.Width, Owner.ClientSize.Height);
                if (!Owner.HasPermanentStatsOverlay)
                {
                    return;
                }

                renderer.SetPresentationScale(
                    Owner.ClientSize.Width / (float)GameRenderWidth,
                    Owner.ClientSize.Height / (float)GameRenderHeight);
                try
                {
                    Owner.RenderPermanentStatsOverlay(renderer);
                }
                finally
                {
                    renderer.SetPresentationScale(1f, 1f);
                }
            }

            public override void HandleLeftClick(Point location)
            {
                if (Owner.HasPermanentStatsOverlay)
                {
                    Owner.NotifyWorldUiMouseClick(location);
                    return;
                }

                Owner.HandleMenuClick(location);
            }
        }

        /// <summary>일반/무한/이어하기 시작 경로를 고르는 모드 선택 상태.</summary>
        private sealed class ModeSelectStateHandler(Form1 owner) : FreeMouseStateHandler(owner)
        {
            public override void Render(Renderer renderer)
            {
                Owner.DrawModeSelect(renderer, Owner.ClientSize.Width, Owner.ClientSize.Height);
            }

            public override void HandleEscape()
            {
                Owner.EnterMenuState(GameState.Menu);
            }

            public override void HandleLeftClick(Point location)
            {
                Owner.HandleModeClick(location);
            }
        }

        /// <summary>
        /// 설정 상태. 메뉴에서 열리면 독립 화면으로, 일시정지에서 열리면 월드 위 오버레이로 렌더링한다.
        /// </summary>
        private sealed class SettingsStateHandler(Form1 owner) : FreeMouseStateHandler(owner)
        {
            public override void Render(Renderer renderer)
            {
                if (Owner.settingsFromPause)
                {
                    Owner.DrawScaledGameWorld(renderer);
                    Owner.DrawSettings(renderer, Owner.ClientSize.Width, Owner.ClientSize.Height, overlay: true);
                    return;
                }

                Owner.DrawSettings(renderer, Owner.ClientSize.Width, Owner.ClientSize.Height, overlay: false);
            }

            public override void HandleEscape()
            {
                Owner.ExitSettings();
            }

            public override void HandleLeftClick(Point location)
            {
                Owner.HandleSettingsClick(location);
            }

            public override void HandleMouseMove(MouseEventArgs e)
            {
                if (Owner.draggingFov)
                {
                    Owner.SetFovFromMouse(e.X);
                }

                if (Owner.draggingSensitivity)
                {
                    Owner.SetSensitivityFromMouse(e.X);
                }

                if (Owner.draggingBgm)
                {
                    Owner.SetBgmVolumeFromMouse(e.X);
                }

                if (Owner.draggingSfx)
                {
                    Owner.SetSfxVolumeFromMouse(e.X);
                }
            }
        }

        /// <summary>
        /// 실제 게임 플레이 상태.
        /// 카드/분기/사망 UI처럼 마우스 선택이 필요한 오버레이가 열리면 캡처를 풀고, 없으면 FPS 조작용 캡처를 유지한다.
        /// </summary>
        private sealed class PlayingStateHandler(Form1 owner) : FormStateHandler(owner)
        {
            public override void OnEnter()
            {
                if (Owner.HasMouseSelectableOverlay)
                {
                    Owner.ReleaseMouse();
                }
                else
                {
                    Owner.CaptureMouse();
                }
            }

            public override void OnActivated()
            {
                if (!Owner.HasMouseSelectableOverlay)
                {
                    Owner.CaptureMouse();
                }
            }

            public override void Update()
            {
                Owner.UpdatePlayingWorld();

                // 월드 오버레이는 WinForms 클라이언트 좌표로 클릭을 받기 때문에 OS 커서가 보여야 한다.
                if (Owner.HasMouseSelectableOverlay)
                {
                    if (Owner.mouseCaptured)
                    {
                        Owner.ReleaseMouse();
                    }

                    return;
                }

                if (!Owner.mouseCaptured)
                {
                    Owner.CaptureMouse();
                }
            }

            public override void Render(Renderer renderer)
            {
                Owner.DrawScaledGameWorld(renderer);
                if (Owner.HasDeathOverlay)
                {
                    Owner.DrawDeathOverlay(renderer, Owner.ClientSize.Width, Owner.ClientSize.Height);
                }
            }

            public override void HandleEscape()
            {
                if (Owner.HasDeathOverlay)
                {
                    Owner.GoToMenu();
                    return;
                }

                Owner.EnterPause();
            }

            public override void HandleLeftClick(Point location)
            {
                if (Owner.HasMouseSelectableOverlay)
                {
                    Owner.NotifyWorldUiMouseClick(location);
                    return;
                }

                if (!Owner.mouseCaptured)
                {
                    Owner.CaptureMouse();
                    return;
                }

                Owner.BeginWorldPrimaryFire();
            }

            public override void HandleLeftMouseUp()
            {
                if (Owner.mouseCaptured && !Owner.HasMouseSelectableOverlay)
                {
                    Owner.EndWorldPrimaryFire();
                }
            }

            public override void HandleMouseMove(MouseEventArgs e)
            {
                if (!Owner.mouseCaptured)
                {
                    return;
                }

                if (Owner.suppressMouseMove)
                {
                    Owner.suppressMouseMove = false;
                    return;
                }

                Point center = Owner.GetClientCenter();
                int dx = e.X - center.X;
                if (dx != 0)
                {
                    Owner.AddWorldMouseDelta(dx);
                }

                // 상대 마우스 입력을 직접 구현한다. 이동량을 적용한 뒤 매 프레임 커서를 중앙으로 되돌린다.
                Owner.CenterMouse();
            }
        }

        /// <summary>게임 월드 위에 일시정지 메뉴를 띄우는 상태.</summary>
        private sealed class PauseStateHandler(Form1 owner) : FreeMouseStateHandler(owner)
        {
            public override void Render(Renderer renderer)
            {
                Owner.DrawScaledGameWorld(renderer);
                Owner.DrawPauseOverlay(renderer, Owner.ClientSize.Width, Owner.ClientSize.Height);
            }

            public override void HandleEscape()
            {
                Owner.ResumeGame();
            }

            public override void HandleLeftClick(Point location)
            {
                Owner.HandlePauseClick(location);
            }
        }

        /// <summary>저장된 런 기록 목록을 보여주는 상태.</summary>
        private sealed class RecordsStateHandler(Form1 owner) : FreeMouseStateHandler(owner)
        {
            public override void Render(Renderer renderer)
            {
                Owner.DrawRecords(renderer, Owner.ClientSize.Width, Owner.ClientSize.Height);
            }

            public override void HandleEscape()
            {
                Owner.EnterMenuState(GameState.Menu);
            }

            public override void HandleLeftClick(Point location)
            {
                Owner.HandleRecordsClick(location);
            }
        }
    }
}
