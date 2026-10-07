using System;
using System.Windows.Forms;
using My2DEngine.Engine.Input;
using My2DEngine.Game.Config;

namespace My2DEngine.Game.Core
{
    /// <summary>
    /// GameLogic의 Red 카드 특수기 partial.
    /// F 키 입력으로 현재 무기의 Red 특수기를 발동한다.
    /// </summary>
    public partial class GameLogic
    {
        private bool specialKeyHeld;

        /// <summary>Dual 92s 피버 모드 잔여 시간(초).</summary>
        private float plazmaLaserTimer;

        /// <summary>Shotgun 갈고리 끌어당기기 잔여 이동 단계 수.</summary>
        private int hookPullSteps;
        private Enemy hookTarget;

        private const float PlazmaLaserDuration = 3.5f;
        private const float PlazmaLaserTickInterval = 0.12f;
        private float plazmaLaserTick;
        private const float PlazmaLaserRadius = 18f;
        private const float PlazmaLaserDamagePerTick = 18f;

        private const float HookStunDuration = 2.5f;
        private const float HookPullSpeed = 0.22f; // 타일/프레임

        /// <summary>F 키 입력을 처리하여 현재 무기 Red 특수기를 발동한다.</summary>
        private void HandleSpecialInput()
        {
            bool held = Input.GetKey(Keys.F);
            if (IsSelectionUiActive)
            {
                specialKeyHeld = held;
                return;
            }

            if (held && !specialKeyHeld)
            {
                TryActivateSpecial();
            }

            specialKeyHeld = held;
        }

        private void TryActivateSpecial()
        {
            if (player.IsDead || !weapon.CanUseSpecial()) return;

            switch (weapon.CurrentType)
            {
                case WeaponType.BearKiller:    ActivateShotgunHook();      break;
                case WeaponType.HChainGun:        ActivateLmgBurst();         break;
                case WeaponType.AutoCannon: ActivateAutoCannonBombardment(); break;
                case WeaponType.DuelBerettas:  ActivatePlazmaLaser();      break;
            }
        }

        // ── Shotgun 갈고리 ──────────────────────────────────────────────

        private void ActivateShotgunHook()
        {
            Enemy target = FindNearestAliveEnemy(10f);
            if (target == null)
            {
                stageStatus.Show("갈고리 - 대상 없음", 1.5f);
                return;
            }

            hookTarget = target;
            hookPullSteps = 20; // 약 20 프레임 동안 끌어당김

            target.StunTimer = HookStunDuration;
            EmitEnemyAlertSound(player.Position.X, player.Position.Y, 8.5f, player.Direction.X, player.Direction.Y);
            weapon.StartSpecialCooldown(10f);
            stageStatus.Show("갈고리 - 끌어당김!", 2f);
        }

        private void UpdateHookPull()
        {
            if (hookPullSteps <= 0 || hookTarget == null || !hookTarget.Alive)
            {
                hookPullSteps = 0;
                hookTarget = null;
                return;
            }

            // 적을 플레이어 방향으로 이동
            float dx = player.Position.X - hookTarget.X;
            float dy = player.Position.Y - hookTarget.Y;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);

            if (dist < 1.5f)
            {
                hookPullSteps = 0;
                hookTarget = null;
                return;
            }

            float nx = dx / dist;
            float ny = dy / dist;
            hookTarget.X += nx * HookPullSpeed;
            hookTarget.Y += ny * HookPullSpeed;
            hookPullSteps--;
        }

        // ── LMG 버스트 모드 ─────────────────────────────────────────────

        private void ActivateLmgBurst()
        {
            if (weapon.CurrentAmmo <= 0)
            {
                stageStatus.Show("LMG 버스트 - 탄약 없음", 1.6f);
                return;
            }

            weapon.StartLmgBurst();
            StartLmgFireLoop();
            stageStatus.Show("LMG 버스트 모드 - 전탄 연사!", 2.5f);
        }

        // ── Auto Cannon 집속 포격 ──────────────────────────────────────

        private void ActivateAutoCannonBombardment()
        {
            Enemy target = FindBestTarget(Math.Max(weapon.SpreadRadius, 0.6f), weapon.Range + 4f);
            if (target == null)
            {
                stageStatus.Show("집속 포격 - 대상 없음", 1.5f);
                return;
            }

            audio.PlayEffect(AudioConfig.RocketFireSoundAlias, true);
            ApplyExplosionDamage(target.X, target.Y, weapon.CurrentDamage * 1.55f, weapon.SplashRadius * 1.65f);
            EmitEnemyAlertSound(target.X, target.Y, 16.5f);
            weapon.StartSpecialCooldown(14f);
            stageStatus.Show("집속 포격 - 고폭탄 투하!", 2.2f);
        }

        // ── Dual 92s 피버 모드 ─────────────────────────────────────────

        private void ActivatePlazmaLaser()
        {
            if (weapon.CurrentAmmo <= 0)
            {
                stageStatus.Show("Dual 92s 피버 - 탄약 없음", 1.6f);
                return;
            }

            weapon.StartPlazmaLaser();
            plazmaLaserTimer = PlazmaLaserDuration;
            plazmaLaserTick = 0f;
            stageStatus.Show("Dual 92s 피버 - 전방 자동 제압!", 2.5f);
        }

        private void UpdatePlazmaLaser(float dt)
        {
            if (!weapon.PlazmaLaserActive) return;

            plazmaLaserTimer -= dt;
            if (plazmaLaserTimer <= 0f)
            {
                weapon.StopPlazmaLaser();
                plazmaLaserTimer = 0f;
                return;
            }

            plazmaLaserTick -= dt;
            if (plazmaLaserTick > 0f) return;

            plazmaLaserTick = PlazmaLaserTickInterval;

            float targetRange = Math.Max(weapon.Range, PlazmaLaserRadius);
            float targetSpread = weapon.SpreadRadius + 0.85f;
            Enemy primary = FindBestTargetWithSpread(targetSpread, targetRange, null, out _);
            if (primary == null)
            {
                return;
            }

            float primaryDamage = Math.Max(PlazmaLaserDamagePerTick, weapon.CurrentDamage * 0.9f);
            DamageEnemy(primary, primaryDamage);

            Enemy secondary = FindBestTargetWithSpread(targetSpread + 0.35f, targetRange, primary, out _);
            if (secondary != null)
            {
                DamageEnemy(secondary, primaryDamage * 0.75f);
            }

            audio.PlayEffect(AudioConfig.PlazmaGunFireSoundAlias, true);
            EmitEnemyAlertSound(player.Position.X, player.Position.Y, 10f, player.Direction.X, player.Direction.Y);
        }

        // ── 공통 헬퍼 ───────────────────────────────────────────────────

        private Enemy FindNearestAliveEnemy(float maxRange)
        {
            Enemy[] enemies = enemyManager.Enemies;
            if (enemies == null) return null;

            Enemy nearest = null;
            float bestDist = maxRange * maxRange;

            foreach (Enemy e in enemies)
            {
                if (e == null || !e.Alive) continue;

                float dx = e.X - player.Position.X;
                float dy = e.Y - player.Position.Y;
                float distSq = dx * dx + dy * dy;

                if (distSq < bestDist)
                {
                    bestDist = distSq;
                    nearest = e;
                }
            }

            return nearest;
        }

        /// <summary>GameLogic.CardReward에서 사용하는 정적 래퍼.</summary>
        internal static WeaponUpgradeCategory[] GetUpgradeCategoriesForWeaponStatic(WeaponType type)
        {
            return GetUpgradeCategoriesForWeapon(type);
        }

        /// <summary>
        /// 특수기 관련 상태를 매 프레임 업데이트한다. Update() 루프에서 호출한다.
        /// </summary>
        private void UpdateSpecialAbilities(float dt)
        {
            UpdateHookPull();
            UpdatePlazmaLaser(dt);
        }
    }
}
