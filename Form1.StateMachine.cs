using System;
using System.Drawing;
using System.Windows.Forms;
using My2DEngine.Engine.Rendering;

namespace My2DEngine
{
    public partial class Form1
    {
        private FormStateHandler[] stateHandlers;
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

        private abstract class FreeMouseStateHandler(Form1 owner) : FormStateHandler(owner)
        {
            public override void OnEnter()
            {
                Owner.ReleaseMouse();
            }
        }

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
            }

            public override void HandleEscape()
            {
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

                Owner.CenterMouse();
            }
        }

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
