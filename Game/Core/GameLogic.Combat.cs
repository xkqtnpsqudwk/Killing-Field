using System;
using My2DEngine.Engine.Math;
using My2DEngine.Game.Config;

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
        private const float HitMarkerDuration = 0.16f;
        private const float KillMarkerDuration = 0.34f;
        private const float WeaponStatusDuration = 0.72f;
        private const float WeaponStatusRepeatDelay = 0.24f;
        private const float PickupToastDuration = 1.35f;

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
                ApplyRecoil(GameConfig.ShotGunRecoilPowerMultiplier, GameConfig.ShotGunRecoilDurationMultiplier);
            }
            else if (weapon.CurrentType == WeaponType.AutoCannon)
            {
                ApplyRecoil(1.2f, 1.08f);
            }
            else
            {
                ApplyRecoil();
            }
            if (weapon.CurrentType != WeaponType.HChainGun)
            {
                PlayWeaponEffectSound(weapon.GetFireSoundAlias(), false);
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

            enemy.TakeDamage(damage);
            RegisterEnemyHitFeedback(!enemy.Alive);
        }

        /// <summary>명중이면 흰색 히트마커, 처치이면 강화 히트마커가 표시되도록 타이머를 갱신한다.</summary>
        private void RegisterEnemyHitFeedback(bool killed)
        {
            hitMarkerTimer = Math.Max(hitMarkerTimer, HitMarkerDuration);
            if (killed)
            {
                killMarkerTimer = KillMarkerDuration;
            }
        }

        private float GetHitMarkerAlpha()
        {
            return HitMarkerDuration <= 0f ? 0f : Math.Min(1f, hitMarkerTimer / HitMarkerDuration);
        }

        private float GetKillMarkerAlpha()
        {
            return KillMarkerDuration <= 0f ? 0f : Math.Min(1f, killMarkerTimer / KillMarkerDuration);
        }

        /// <summary>발사 입력이 탄약 또는 쿨다운 때문에 막혔을 때 HUD 상태 문구를 등록한다.</summary>
        private void RegisterWeaponBlockedFeedback(bool force = false)
        {
            string text = BuildWeaponBlockedText();
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (!force && weaponStatusRepeatGate > 0f && string.Equals(weaponStatusText, text, StringComparison.Ordinal))
            {
                return;
            }

            weaponStatusText = text;
            weaponStatusTimer = WeaponStatusDuration;
            weaponStatusRepeatGate = WeaponStatusRepeatDelay;
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

        private float GetWeaponStatusAlpha()
        {
            if (weaponStatusTimer <= 0f)
            {
                return 0f;
            }

            return Math.Min(1f, weaponStatusTimer / 0.18f);
        }

        /// <summary>보상 드롭/획득 토스트를 등록한다.</summary>
        private void RegisterPickupToast(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            pickupToastText = text;
            pickupToastTimer = PickupToastDuration;
        }

        private float GetPickupToastAlpha()
        {
            if (pickupToastTimer <= 0f)
            {
                return 0f;
            }

            return Math.Min(1f, pickupToastTimer / 0.24f);
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
            bestHitDistance = float.MaxValue;
            Enemy[] enemies = enemyManager.Enemies;
            if (enemies == null) return null;

            Enemy bestTarget = null;
            float bestDistance = float.MaxValue;

            foreach (Enemy enemy in enemies)
            {
                if (enemy == null || !enemy.Alive) continue;
                if (enemy == exclude) continue;

                float dx = enemy.X - player.Position.X;
                float dy = enemy.Y - player.Position.Y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);

                if (dist <= 0.001f || dist > range) continue;

                float forward = (dx * player.Direction.X + dy * player.Direction.Y) / dist;
                if (forward < 0.75f) continue;

                float lateral = Math.Abs(dx * player.Direction.Y - dy * player.Direction.X);
                float allowed = spreadRadius + dist * 0.03f;
                if (lateral > allowed) continue;

                if (!collision.HasLineOfSight(player.Position.X, player.Position.Y, enemy.X, enemy.Y, dist)) continue;

                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestTarget = enemy;
                }
            }

            bestHitDistance = bestDistance;
            return bestTarget;
        }

        // ─── 반동 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 샷건 탄환의 거리 감쇠 배율을 계산한다.
        /// 가까운 거리에서는 최대 피해를 유지하고, 최대 사거리 근처에서는 최소 배율까지 감소한다.
        /// </summary>
        private float GetShotGunDamageMultiplier(float distance)
        {
            if (distance <= GameConfig.ShotGunFullDamageRange)
            {
                return 1f;
            }

            float falloffSpan = Math.Max(0.001f, weapon.Range - GameConfig.ShotGunFullDamageRange);
            float t = (distance - GameConfig.ShotGunFullDamageRange) / falloffSpan;
            t = Math.Max(0f, Math.Min(1f, t));
            return 1f - ((1f - GameConfig.ShotGunMinDamageMultiplier) * t);
        }

        /// <summary>발사 반동 카메라 흔들림을 설정한다.</summary>
        private void ApplyRecoil(float powerMultiplier = 1.0f, float durationMultiplier = 1.0f)
        {
            playerRecoilShakeTimer = System.Math.Min(0.3f, System.Math.Max(playerRecoilShakeTimer, 0.18f * durationMultiplier));
            playerRecoilShakePower = System.Math.Min(1f, System.Math.Max(playerRecoilShakePower, 0.72f * powerMultiplier));
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

            player.TakeDamage(damage);

            float flashStrength = damage / 18f;
            if (flashStrength < 0.35f) flashStrength = 0.35f;
            if (flashStrength > 1.2f)  flashStrength = 1.2f;

            playerDamageFlashTimer = Math.Min(1.2f, Math.Max(playerDamageFlashTimer, flashStrength));
            playerDamageShakeTimer = Math.Min(0.5f, Math.Max(playerDamageShakeTimer, 0.18f + flashStrength * 0.18f));
            playerDamageShakePower = Math.Min(1f,   Math.Max(playerDamageShakePower, 0.35f + flashStrength * 0.45f));

            float dirX = sourceX - player.Position.X;
            float dirY = sourceY - player.Position.Y;
            float len  = (float)Math.Sqrt((dirX * dirX) + (dirY * dirY));
            if (len > 0.001f)
            {
                playerDamageFlashDirX = dirX / len;
                playerDamageFlashDirY = dirY / len;
            }
            else
            {
                playerDamageFlashDirX = -player.Direction.X;
                playerDamageFlashDirY = -player.Direction.Y;
            }

            if (player.IsDead && deathPresentationProgress <= 0f)
            {
                Vector2 right = new Vector2(-player.Direction.Y, player.Direction.X);
                float rightDot = (playerDamageFlashDirX * right.X) + (playerDamageFlashDirY * right.Y);
                deathRollDirection = (System.Math.Abs(rightDot) > 0.08f)
                    ? (rightDot >= 0f ? -1f : 1f)
                    : 1f;
            }
        }
    }
}
