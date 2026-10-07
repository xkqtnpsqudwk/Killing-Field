using System;
using System.Windows.Forms;
using My2DEngine.Engine.Core;
using My2DEngine.Engine.Input;
using My2DEngine.Engine.Math;
using My2DEngine.Game;
using My2DEngine.Game.Config;
using My2DEngine.Game.Map;

namespace My2DEngine.Game.Core
{
    /// <summary>
    /// GameLogic의 프레임 단위 업데이트 처리 partial.
    /// 입력 수집, 플레이어 이동, 적 업데이트, 상호작용, 스테이지 진행을
    /// 한 프레임 안에서 올바른 순서로 실행한다.
    /// </summary>
    public partial class GameLogic
    {
        /// <summary>
        /// 매 프레임 호출되는 게임 루프의 핵심 메서드.
        /// <para>처리 순서:</para>
        /// <list type="number">
        ///   <item>시스템 타이머 갱신(메시지·보스 인트로·플래시·사망 연출·문 애니메이션·무기·대시·픽업)</item>
        ///   <item>적 AI 업데이트 및 플레이어 피격 콜백 연결</item>
        ///   <item>플레이어 사망 상태일 때 조기 반환(BGM 정지 포함)</item>
        ///   <item>재장전·이동·상호작용·발사 입력 처리</item>
        ///   <item>보상 획득·스테이지 진행·BGM 갱신·상호작용 안내문 갱신</item>
        /// </list>
        /// </summary>
        public void Update()
        {
            float dt = Time.DeltaTime;
            if (dt > 0.05f)
            {
                dt = 0.05f;
            }

            bool playerStatsOverlayWasActive = playerStatsOverlayActive;
            HandlePlayerStatsOverlayInput();

            bool permanentUiWasActive = permanentStatsUiActive;
            HandlePermanentStatsInput();
            if (playerStatsOverlayWasActive || playerStatsOverlayActive || permanentUiWasActive || permanentStatsUiActive)
            {
                interactPromptText = null;
                weapon.PendingShot = false;
                weapon.FireButtonHeld = false;
                return;
            }

            UpdateStageMessage(dt);
            UpdateBossIntroTimer(dt);
            UpdatePlayerDamageFlash(dt);
            UpdateCombatFeedback(dt);
            UpdateDeathPresentation(dt);
            UpdateEndingSequence(dt);

            if (endingSequenceActive)
            {
                interactPromptText = null;
                weapon.PendingShot = false;
                weapon.FireButtonHeld = false;
                return;
            }

            mapManager.UpdateDoorAnimations(dt);
            weapon.UpdateTimers(dt);
            player.UpdateDashCooldown(dt);
            player.UpdateDash(dt);
            player.UpdateShield(dt);
            UpdatePickupTimers(dt);

            if (!victory)
            {
                enemyManager.Update(dt, player.Position, player.IsDead, collision,
                    (damage, sourceX, sourceY) => OnPlayerDamaged(damage, sourceX, sourceY));
            }

            UpdatePlayerProjectiles(dt);

            if (player.IsDead)
            {
                MarkRunEndedIfNeeded();
                weapon.PendingShot = false;
                weapon.FireButtonHeld = false;
                StopLoopingWeaponEffects();
                playerProjectiles.Clear();
                if (!deathMusicStopped)
                {
                    bgmChannel.StopBackgroundMusic();
                    deathMusicStopped = true;
                }
                interactPromptText = null;
                return;
            }

            UpdatePendingCardRewardReveal(dt);

            HandleCardInput();
            HandleBranchInput();
            HandleWeaponSwitchInput();
            HandleSpecialInput();
            HandlePlayerMovement(dt);
            HandleInteractInput();
            HandleAutomaticFire(dt);
            HandlePendingShot();
            UpdateSpecialAbilities(dt);
            CollectRewardPickups();
            UpdateStageProgression(dt);
            UpdateBackgroundMusicState(dt);
            UpdateInteractPrompt();
        }

        /// <summary>
        /// 이동 입력(W/A/S/D, 화살표)을 읽어 플레이어를 이동시킨다.
        /// 이동 방향을 먼저 합성하여 정규화한 뒤 충돌 시스템에 한 번만 적용한다.
        /// 대시 중일 때는 일반 이동 대신 대시 방향으로만 이동한다.
        /// </summary>
        /// <param name="dt">이번 프레임의 경과 시간(초).</param>
        private void HandlePlayerMovement(float dt)
        {
            bool sprintHeld = Input.GetKey(Keys.ShiftKey) ||
                Input.GetKey(Keys.LShiftKey) ||
                Input.GetKey(Keys.RShiftKey);

            bool dashHeld = Input.GetKey(Keys.ControlKey) ||
                Input.GetKey(Keys.LControlKey) ||
                Input.GetKey(Keys.RControlKey);

            float speed = player.GetEffectiveSpeed(sprintHeld);
            float move = speed * dt;
            if (move > 0.15f)
            {
                move = 0.15f;
            }

            player.UpdateStamina(dt, sprintHeld && move > 0f);

            float moveX = 0f;
            float moveY = 0f;

            if (Input.GetKey(Keys.W) || Input.GetKey(Keys.Up))
            {
                moveX += player.Direction.X;
                moveY += player.Direction.Y;
            }
            if (Input.GetKey(Keys.S) || Input.GetKey(Keys.Down))
            {
                moveX -= player.Direction.X;
                moveY -= player.Direction.Y;
            }

            Vector2 right = new Vector2(-player.Direction.Y, player.Direction.X);
            if (Input.GetKey(Keys.A))
            {
                moveX -= right.X;
                moveY -= right.Y;
            }
            if (Input.GetKey(Keys.D))
            {
                moveX += right.X;
                moveY += right.Y;
            }

            if (dashHeld && !dashKeyHeld)
            {
                TryPerformDash(moveX, moveY);
            }

            dashKeyHeld = dashHeld;

            if (player.IsDashing)
            {
                float dashStep = (PlayerConfig.DashDistance / PlayerConfig.DashDuration) * dt;
                collision.TryMovePlayer(player, player.DashDirX * dashStep, player.DashDirY * dashStep);
                return;
            }

            float moveLenSq = (moveX * moveX) + (moveY * moveY);
            if (moveLenSq > 0.0001f)
            {
                float invLen = 1f / (float)Math.Sqrt(moveLenSq);
                collision.TryMovePlayer(player, moveX * invLen * move, moveY * invLen * move);
            }
        }

        /// <summary>
        /// 대시 쿨다운이 남아 있는지 확인하고, 가능하면 대시를 시작한다.
        /// 입력 방향이 없으면 플레이어의 현재 바라보는 방향으로 대시하여
        /// 정지 상태에서도 회피기로 일관되게 사용할 수 있게 한다.
        /// </summary>
        /// <param name="moveX">현재 프레임의 이동 입력 X 성분(정규화 전).</param>
        /// <param name="moveY">현재 프레임의 이동 입력 Y 성분(정규화 전).</param>
        private void TryPerformDash(float moveX, float moveY)
        {
            if (!player.TrySpendDash())
            {
                return;
            }

            float dashX = moveX;
            float dashY = moveY;
            float dashLenSq = (dashX * dashX) + (dashY * dashY);

            if (dashLenSq <= 0.0001f)
            {
                dashX = player.Direction.X;
                dashY = player.Direction.Y;
                dashLenSq = (dashX * dashX) + (dashY * dashY);
            }

            if (dashLenSq <= 0.0001f)
            {
                return;
            }

            float invLen = 1f / (float)Math.Sqrt(dashLenSq);
            player.StartDash(dashX * invLen, dashY * invLen);
            StartDashStrikeWindow();
            EmitEnemyAlertSound(player.Position.X, player.Position.Y, 6.5f, dashX * invLen, dashY * invLen);
            playerRecoilShakeTimer = Math.Min(0.22f, Math.Max(playerRecoilShakeTimer, 0.12f));
            playerRecoilShakePower = Math.Min(0.45f, Math.Max(playerRecoilShakePower, 0.2f));
        }

        /// <summary>
        /// 피격 플래시·카메라 흔들림과 발사 반동 흔들림 타이머를 프레임마다 감소시킨다.
        /// 타이머가 0 이하가 되면 해당 효과를 즉시 소거한다.
        /// </summary>
        /// <param name="dt">이번 프레임의 경과 시간(초).</param>
        private void UpdatePlayerDamageFlash(float dt)
        {
            if (playerDamageFlashTimer <= 0f)
            {
                playerDamageFlashTimer = 0f;
                playerDamageShakeTimer = 0f;
                playerDamageShakePower = 0f;
            }
            else
            {
                playerDamageFlashTimer = Math.Max(playerDamageFlashTimer - dt, 0f);
                playerDamageShakeTimer = Math.Max(playerDamageShakeTimer - dt, 0f);
                playerDamageShakePower = Math.Max(playerDamageShakePower - dt * 2.2f, 0f);
            }

            if (playerRecoilShakeTimer <= 0f)
            {
                playerRecoilShakeTimer = 0f;
                playerRecoilShakePower = 0f;
            }
            else
            {
                playerRecoilShakeTimer = Math.Max(playerRecoilShakeTimer - dt, 0f);
                playerRecoilShakePower = Math.Max(playerRecoilShakePower - dt * 4.6f, 0f);
            }
        }

        /// <summary>플레이어 공격 피드백 타이머를 프레임마다 감소시킨다.</summary>
        private void UpdateCombatFeedback(float dt)
        {
            hitMarkerTimer = Math.Max(0f, hitMarkerTimer - dt);
            killMarkerTimer = Math.Max(0f, killMarkerTimer - dt);
            weaponStatusTimer = Math.Max(0f, weaponStatusTimer - dt);
            weaponStatusRepeatGate = Math.Max(0f, weaponStatusRepeatGate - dt);
            if (weaponStatusTimer <= 0f)
            {
                weaponStatusText = null;
            }

            pickupToastTimer = Math.Max(0f, pickupToastTimer - dt);
            if (pickupToastTimer <= 0f)
            {
                pickupToastText = null;
            }

            UpdateDashStrikeWindow(dt);
            UpdateKillChainWindow(dt);
            UpdateRapidFireStreak(dt);

            if (controlsTutorialTimer > 0f)
                controlsTutorialTimer = Math.Max(0f, controlsTutorialTimer - dt);
        }

        /// <summary>
        /// 사망 연출 진행도(<see cref="deathPresentationProgress"/>)를 매 프레임 갱신한다.
        /// 사망 상태가 아니면 즉시 0으로 초기화하고, 사망 상태면 1.25초에 걸쳐 0에서 1로 증가한다.
        /// </summary>
        /// <param name="dt">이번 프레임의 경과 시간(초).</param>
        private void UpdateDeathPresentation(float dt)
        {
            if (!player.IsDead)
            {
                deathPresentationProgress = 0f;
                deathRollDirection = 1f;
                return;
            }

            deathPresentationProgress += dt / 1.25f;
            if (deathPresentationProgress > 1f)
            {
                deathPresentationProgress = 1f;
            }
        }

        /// <summary>
        /// 숫자 키 1~5로 무기를 전환한다.
        /// 현재 장착 무기와 동일한 키를 누르면 무시한다.
        /// </summary>
        private void HandleWeaponSwitchInput()
        {
            // 카드/분기 선택 UI 중에는 1/2/3 키가 선택지 입력으로 사용되므로 무기 전환을 차단한다.
            if (cardRewardActive || branchSelectionActive)
            {
                return;
            }

            if (Input.GetKey(Keys.D1)) SwitchWeapon(WeaponType.AMPistol);
            else if (Input.GetKey(Keys.D2) && ownedWeapons[(int)WeaponType.BearKiller])        SwitchWeapon(WeaponType.BearKiller);
            else if (Input.GetKey(Keys.D3) && ownedWeapons[(int)WeaponType.HChainGun])            SwitchWeapon(WeaponType.HChainGun);
            else if (Input.GetKey(Keys.D4) && ownedWeapons[(int)WeaponType.AutoCannon]) SwitchWeapon(WeaponType.AutoCannon);
            else if (Input.GetKey(Keys.D5) && ownedWeapons[(int)WeaponType.DuelBerettas])      SwitchWeapon(WeaponType.DuelBerettas);
        }

        /// <summary>
        /// 홀드 연사형 무기 처리를 담당한다.
        /// Heavy Chaingun과 Dual 92s 슬롯 모두 버튼 홀드 시 즉시 연사를 시작한다.
        /// </summary>
        private void HandleAutomaticFire(float dt)
        {
            if (weapon.CurrentType != WeaponType.HChainGun &&
                weapon.CurrentType != WeaponType.DuelBerettas)
            {
                return;
            }

            bool burstActive = weapon.LmgBurstRemaining > 0;
            if (!weapon.FireButtonHeld && !burstActive) return;

            if (weapon.CurrentAmmo <= 0)
            {
                if (weapon.CurrentType == WeaponType.HChainGun)
                {
                    StopLmgFireLoop();
                }
                RegisterWeaponBlockedFeedback();
                return;
            }

            if (weapon.CurrentType == WeaponType.HChainGun && burstActive)
            {
                if (!lmgFireLoopActive)
                {
                    StartLmgFireLoop();
                }
            }
            else if (weapon.CurrentType == WeaponType.HChainGun)
            {
                StartLmgSpin();
                if (!lmgFireLoopActive && lmgSpinUpTimer > 0f)
                {
                    lmgSpinUpTimer = Math.Max(lmgSpinUpTimer - dt, 0f);
                    if (lmgSpinUpTimer > 0f)
                    {
                        return;
                    }
                }
            }

            if (weapon.CanFire())
            {
                weapon.PendingShot = true;
            }
        }

        /// <summary>
        /// E 키 입력을 감지하여 근처 문 열기를 시도한다.
        /// 엣지 트리거(키를 이번 프레임에 처음 눌렀을 때)에만 반응한다.
        /// </summary>
        private void HandleInteractInput()
        {
            bool interactHeld = Input.GetKey(Keys.E);
            if (interactHeld && !interactKeyHeld)
            {
                // 카드 연출/선택 및 분기 UI가 진행 중일 때는 E키 동작을 차단한다.
                if (CardRewardRevealPending || cardRewardActive || branchSelectionActive || endingSequenceActive)
                {
                    interactKeyHeld = interactHeld;
                    return;
                }

                if (TrySkipActiveRestRoom())
                {
                    interactKeyHeld = interactHeld;
                    return;
                }

                // 로그라이크 모드: 층 출구 문이 우선
                if (currentFloor > 0 && TryGetFloorExitDoor(out _))
                {
                    TransitionToNextFloor();
                }
                else
                {
                    TryOpenNearbyDoor();
                }
            }

            interactKeyHeld = interactHeld;
        }
    }
}
