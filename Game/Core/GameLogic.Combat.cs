using System;
using My2DEngine.Engine.Math;
using My2DEngine.Game.Config;
using My2DEngine.Game.Systems;

namespace My2DEngine.Game.Core
{
    /// <summary>
    /// GameLogic의 전투 처리 partial.
    /// 발사 확정, 적 피격 판정, 플레이어 피격 이벤트 처리를 담당한다.
    ///
    /// 무기별 발사 방식:
    ///   AMPistol       - 단발 / 단일 타겟
    ///   BearKiller     - 단발 / 다중 탄환(PelletCount개) / 넓은 퍼짐
    ///   HChainGun      - 연사 / 단일 타겟 (Update에서 PendingShot 반복 세팅)
    ///   AutoCannon     - 단발 / 플레이어 로켓 투사체 / 충돌 지점 범위 폭발
    ///   DuelBerettas   - 홀드 연사 / 단일 타겟
    /// </summary>
    public partial class GameLogic
    {
        /// <summary>
        /// 보류 중인 발사 요청을 실제 탄 소비·판정·반동·사운드·적 피격으로 확정한다.
        /// </summary>
        private void HandlePendingShot()
        {
            if (!weapon.PendingShot)
            {
                return;
            }

            weapon.PendingShot = false;

            if (player.IsDead || victory)
            {
                return;
            }

            if (!weapon.CanFire())
            {
                RegisterWeaponBlockedFeedback(force: true);
                return;
            }

            weapon.Fire();
            EmitWeaponFireNoise();
            if (weapon.CurrentType == WeaponType.HChainGun)
            {
                StartLmgFireLoop();
            }
            if (weapon.CurrentType == WeaponType.BearKiller)
            {
                feedback.OnRecoil(WeaponConfig.ShotGunRecoilPowerMultiplier, WeaponConfig.ShotGunRecoilDurationMultiplier);
            }
            else if (weapon.CurrentType == WeaponType.AutoCannon)
            {
                feedback.OnRecoil(1.2f, 1.08f);
            }
            else
            {
                feedback.OnRecoil();
            }
            if (weapon.CurrentType != WeaponType.HChainGun)
            {
                audio.PlayEffect(weapon.GetFireSoundAlias(), false);
            }
            if (weapon.CurrentAmmo == 0 && weapon.CurrentType == WeaponType.HChainGun)
            {
                StopLmgFireLoop();
            }

            // ── 무기별 피격 판정 ────────────────────────────────────────
            switch (weapon.CurrentType)
            {
                case WeaponType.BearKiller:
                    FireShotgunPellets();
                    break;
                case WeaponType.AutoCannon:
                    FireAutoCannonShell();
                    break;
                default:
                    FireSingleShot(weapon.CurrentDamage);
                    break;
            }
        }

        // ─── 발사 방식별 구현 ─────────────────────────────────────────────

        /// <summary>
        /// 단일 타겟 사격 (AMPistol / HChainGun / DuelBerettas 공용).
        /// 시야각 콘 안의 가장 가까운 적에게 damage를 적용한다.
        /// </summary>
        private void FireSingleShot(float damage)
        {
            Enemy target = FindBestTarget(weapon.SpreadRadius, weapon.Range);
            DamageEnemy(target, damage);
        }

        /// <summary>
        /// ShotGun 다중 탄환 발사.
        /// PelletCount개의 가상 탄환을 서로 다른 퍼짐 오프셋으로 발사하여
        /// 범위 내 적 최대 PelletCount명에게 각각 Damage를 적용한다.
        /// </summary>
        private void FireShotgunPellets()
        {
            Enemy[] enemies = enemyManager.Enemies;
            if (enemies == null) return;

            float baseSpread = weapon.SpreadRadius;
            int pelletsLeft = weapon.PelletCount;

            // 중심 방향부터 퍼지는 순서로 탄환마다 목표 탐색
            for (int pellet = 0; pellet < weapon.PelletCount && pelletsLeft > 0; pellet++)
            {
                // 각 탄환마다 약간씩 다른 퍼짐 오프셋 적용
                float offsetFactor = (pellet == 0) ? 0f : (pellet % 2 == 1 ? 1f : -1f) * (float)Math.Ceiling(pellet / 2.0);
                float pelletSpread = baseSpread * (0.3f + 0.7f * Math.Abs(offsetFactor) / (weapon.PelletCount * 0.5f));

                Enemy target = FindBestTargetWithSpread(pelletSpread, weapon.Range, null, out float hitDistance);
                if (target != null)
                {
                    float pelletDamage = weapon.CurrentDamage * GetShotGunDamageMultiplier(hitDistance);
                    DamageEnemy(target, pelletDamage);
                    pelletsLeft--;
                }
            }
        }

        /// <summary>
        /// Auto Cannon 로켓을 플레이어 전방으로 발사한다.
        /// 이동, 벽/적 충돌, 폭발 피해는 GameLogic.Projectiles에서 처리한다.
        /// </summary>
        private void FireAutoCannonShell()
        {
            SpawnPlayerRocket(weapon.CurrentDamage, weapon.SplashRadius);
            EmitEnemyAlertSound(player.Position.X, player.Position.Y, 14f, player.Direction.X, player.Direction.Y);
        }

        private void ApplyExplosionDamage(float centerX, float centerY, float damage, float radius)
        {
            Enemy[] enemies = enemyManager.Enemies;
            if (enemies == null)
            {
                return;
            }

            float effectiveRadius = Math.Max(0.6f, radius);
            for (int i = 0; i < enemies.Length; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy == null || !enemy.Alive)
                {
                    continue;
                }

                float dx = enemy.X - centerX;
                float dy = enemy.Y - centerY;
                float distance = (float)Math.Sqrt((dx * dx) + (dy * dy));
                float surfaceDistance = Math.Max(0f, distance - enemy.Radius);
                if (surfaceDistance > effectiveRadius)
                {
                    continue;
                }

                if (collision != null && distance > enemy.Radius &&
                    !collision.HasLineOfSight(centerX, centerY, enemy.X, enemy.Y, distance))
                {
                    // 폭발도 벽 뒤 적까지 관통하지 않도록 중심점과 적 사이 시야를 확인한다.
                    continue;
                }

                float t = effectiveRadius <= 0.001f ? 0f : surfaceDistance / effectiveRadius;
                float falloff = 1f - (0.55f * t);
                if (falloff < 0.45f)
                {
                    falloff = 0.45f;
                }

                DamageEnemy(enemy, damage * falloff);
            }
        }

        /// <summary>적에게 플레이어 피해를 적용하고 명중/처치 피드백을 기록한다.</summary>
        private void DamageEnemy(Enemy enemy, float damage)
        {
            if (enemy == null || damage <= 0f || !enemy.Alive)
            {
                return;
            }

            float modifiedDamage = modifiers.ApplyOutgoing(damage, player, weapon);
            float finalDamage = modifiers.ApplyCritical(modifiedDamage, rewardRandom);
            float healthBefore = Math.Max(0f, enemy.Health);
            enemy.TakeDamage(finalDamage);
            float healthAfter = Math.Max(0f, enemy.Health);
            ApplyPlayerLifeSteal(healthBefore - healthAfter);
            bool killed = !enemy.Alive;
            feedback.OnEnemyHit(killed);
            modifiers.OnEnemyHit(killed);
        }

        private void ApplyPlayerLifeSteal(float dealtDamage)
        {
            ApplyPlayerLifeSteal(dealtDamage, IsActiveToxicMistRoom());
        }

        private void ApplyPlayerLifeSteal(float dealtDamage, bool toxicMistPenaltyActive)
        {
            if (player == null || player.IsDead || dealtDamage <= 0f || player.Health >= player.MaxHealth)
            {
                return;
            }

            float lifeStealRatio = modifiers.LifeStealRatio(toxicMistPenaltyActive);
            if (lifeStealRatio <= 0f)
            {
                return;
            }

            float healAmount = dealtDamage * lifeStealRatio;
            if (healAmount <= 0f)
            {
                return;
            }

            player.Health = Math.Min(player.MaxHealth, Math.Max(0f, player.Health) + healAmount);
        }

        /// <summary>발사 입력이 탄약 또는 쿨다운 때문에 막혔을 때 HUD 상태 문구를 등록한다.</summary>
        private void RegisterWeaponBlockedFeedback(bool force = false)
        {
            feedback.ShowWeaponStatus(BuildWeaponBlockedText(), force);
        }

        private string BuildWeaponBlockedText()
        {
            if (weapon.CurrentAmmo <= 0)
            {
                return "NO AMMO";
            }

            if (weapon.ShotCooldown > 0.05f)
            {
                return "READY " + weapon.ShotCooldown.ToString("0.0") + "s";
            }

            return null;
        }

        // ─── 타겟 탐색 헬퍼 ───────────────────────────────────────────────

        /// <summary>전방 콘·시야 기준 가장 가까운 적을 탐색한다.</summary>
        private Enemy FindBestTarget(float spreadRadius, float range)
        {
            return FindBestTargetWithSpread(spreadRadius, range, null, out _);
        }

        /// <summary>
        /// 지정 퍼짐 반지름과 사거리로 타겟을 탐색한다.
        /// <paramref name="exclude"/>를 전달하면 해당 적은 결과에서 제외된다.
        /// </summary>
        private Enemy FindBestTargetWithSpread(float spreadRadius, float range, Enemy exclude, out float bestHitDistance)
        {
            return CombatTargeting.FindInCone(enemyManager.Enemies, collision,
                player.Position.X, player.Position.Y, player.Direction.X, player.Direction.Y,
                spreadRadius, range, exclude, out bestHitDistance);
        }

        // ─── 반동 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 샷건 탄환의 거리 감쇠 배율을 계산한다.
        /// 가까운 거리에서는 최대 피해를 유지하고, 최대 사거리 근처에서는 최소 배율까지 감소한다.
        /// </summary>
        private float GetShotGunDamageMultiplier(float distance)
        {
            if (distance <= WeaponConfig.ShotGunFullDamageRange)
            {
                return 1f;
            }

            float falloffSpan = Math.Max(0.001f, weapon.Range - WeaponConfig.ShotGunFullDamageRange);
            float t = (distance - WeaponConfig.ShotGunFullDamageRange) / falloffSpan;
            t = Math.Max(0f, Math.Min(1f, t));
            return 1f - ((1f - WeaponConfig.ShotGunMinDamageMultiplier) * t);
        }

        /// <summary>
        /// 플레이어가 피해를 받았을 때 호출되는 이벤트 핸들러.
        /// 체력을 감소시키고 피격 방향에 따른 화면 플래시·카메라 흔들림 효과를 설정한다.
        /// </summary>
        private void OnPlayerDamaged(float damage, float sourceX, float sourceY)
        {
            if (player.IsDashing)
            {
                return;
            }

            // 피해 감소를 먼저 적용하고, 남은 피해를 보호막이 흡수한 뒤 체력에 전달한다.
            // 피격 연출 강도는 실제 체력 피해가 아니라 감소 후 총 피해를 기준으로 잡아 보호막 피격도 읽히게 한다.
            float reducedDamage = modifiers.ApplyIncoming(damage);
            if (reducedDamage <= 0f)
            {
                return;
            }

            float healthDamage = player.AbsorbShieldDamage(reducedDamage);
            if (healthDamage > 0f)
            {
                player.TakeDamage(healthDamage);
            }

            feedback.OnPlayerHit(reducedDamage, sourceX - player.Position.X, sourceY - player.Position.Y,
                player.Direction.X, player.Direction.Y);

            if (player.IsDead && deathPresentationProgress <= 0f)
            {
                MarkRunEndedIfNeeded();
                Vector2 right = new Vector2(-player.Direction.Y, player.Direction.X);
                float rightDot = (feedback.DamageDirX * right.X) + (feedback.DamageDirY * right.Y);
                deathRollDirection = (System.Math.Abs(rightDot) > 0.08f)
                    ? (rightDot >= 0f ? -1f : 1f)
                    : 1f;
            }
        }
    }
}
