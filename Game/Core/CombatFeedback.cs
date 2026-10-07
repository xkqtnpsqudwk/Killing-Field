using System;

namespace My2DEngine.Game.Core
{
    /// <summary>
    /// 전투 중 화면 피드백 상태: 피격 번쩍임·흔들림, 반동 흔들림, 명중·처치 표시, 무기 상태 문구, 획득 알림.
    /// 게임 규칙에는 영향을 주지 않고 렌더러로 넘기는 값만 갖는다. 시간이 지나면 저절로 사라진다.
    /// </summary>
    internal sealed class CombatFeedback
    {
        private const float HitMarkerDuration = 0.16f;
        private const float KillMarkerDuration = 0.34f;
        private const float WeaponStatusDuration = 0.72f;
        private const float WeaponStatusRepeatDelay = 0.24f;
        private const float PickupToastDuration = 1.35f;

        /// <summary>피격 번쩍임 강도(남은 시간 겸용).</summary>
        public float DamageFlash { get; private set; }

        /// <summary>마지막 피격 방향(플레이어 기준 단위 벡터).</summary>
        public float DamageDirX { get; private set; }
        public float DamageDirY { get; private set; }

        public float DamageShakeTimer { get; private set; }
        public float DamageShakePower { get; private set; }
        public float RecoilShakeTimer { get; private set; }
        public float RecoilShakePower { get; private set; }

        private float hitMarkerTimer;
        private float killMarkerTimer;
        private float weaponStatusTimer;
        private float weaponStatusRepeatGate;
        private float pickupToastTimer;

        /// <summary>발사가 막힌 이유("NO AMMO" 등). 없으면 null.</summary>
        public string WeaponStatusText { get; private set; }

        /// <summary>보상 드롭·획득 알림. 없으면 null.</summary>
        public string PickupToastText { get; private set; }

        public float HitMarkerAlpha => Math.Min(1f, hitMarkerTimer / HitMarkerDuration);
        public float KillMarkerAlpha => Math.Min(1f, killMarkerTimer / KillMarkerDuration);
        public float WeaponStatusAlpha => weaponStatusTimer <= 0f ? 0f : Math.Min(1f, weaponStatusTimer / 0.18f);
        public float PickupToastAlpha => pickupToastTimer <= 0f ? 0f : Math.Min(1f, pickupToastTimer / 0.24f);

        /// <summary>
        /// 플레이어가 맞았을 때. 세기는 감소 후 피해량으로 정하고(보호막 피격도 보이게), 방향은 공격 지점 쪽.
        /// 공격 지점이 플레이어와 겹치면 바라보는 방향의 반대쪽으로 한다.
        /// </summary>
        public void OnPlayerHit(float damage, float dirX, float dirY, float facingX, float facingY)
        {
            float strength = damage / 18f;
            if (strength < 0.35f) strength = 0.35f;
            if (strength > 1.2f) strength = 1.2f;

            DamageFlash = Math.Min(1.2f, Math.Max(DamageFlash, strength));
            DamageShakeTimer = Math.Min(0.5f, Math.Max(DamageShakeTimer, 0.18f + strength * 0.18f));
            DamageShakePower = Math.Min(1f, Math.Max(DamageShakePower, 0.35f + strength * 0.45f));

            float len = (float)Math.Sqrt((dirX * dirX) + (dirY * dirY));
            if (len > 0.001f)
            {
                DamageDirX = dirX / len;
                DamageDirY = dirY / len;
            }
            else
            {
                DamageDirX = -facingX;
                DamageDirY = -facingY;
            }
        }

        /// <summary>발사 반동 흔들림. 배율은 무기별로 다르다.</summary>
        public void OnRecoil(float powerMultiplier = 1.0f, float durationMultiplier = 1.0f)
        {
            RecoilShakeTimer = Math.Min(0.3f, Math.Max(RecoilShakeTimer, 0.18f * durationMultiplier));
            RecoilShakePower = Math.Min(1f, Math.Max(RecoilShakePower, 0.72f * powerMultiplier));
        }

        /// <summary>대시할 때 작은 흔들림.</summary>
        public void OnDash()
        {
            RecoilShakeTimer = Math.Min(0.22f, Math.Max(RecoilShakeTimer, 0.12f));
            RecoilShakePower = Math.Min(0.45f, Math.Max(RecoilShakePower, 0.2f));
        }

        /// <summary>적을 맞혔을 때 명중 표시, 죽였으면 처치 표시.</summary>
        public void OnEnemyHit(bool killed)
        {
            hitMarkerTimer = Math.Max(hitMarkerTimer, HitMarkerDuration);
            if (killed)
            {
                killMarkerTimer = KillMarkerDuration;
            }
        }

        /// <summary>
        /// 발사가 막힌 이유를 띄운다. 같은 문구는 잠깐 동안 다시 띄우지 않는다(force면 무시).
        /// </summary>
        public void ShowWeaponStatus(string text, bool force)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (!force && weaponStatusRepeatGate > 0f && string.Equals(WeaponStatusText, text, StringComparison.Ordinal))
            {
                return;
            }

            WeaponStatusText = text;
            weaponStatusTimer = WeaponStatusDuration;
            weaponStatusRepeatGate = WeaponStatusRepeatDelay;
        }

        public void ShowPickupToast(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            PickupToastText = text;
            pickupToastTimer = PickupToastDuration;
        }

        public void Update(float dt)
        {
            if (DamageFlash <= 0f)
            {
                DamageFlash = 0f;
                DamageShakeTimer = 0f;
                DamageShakePower = 0f;
            }
            else
            {
                DamageFlash = Math.Max(DamageFlash - dt, 0f);
                DamageShakeTimer = Math.Max(DamageShakeTimer - dt, 0f);
                DamageShakePower = Math.Max(DamageShakePower - dt * 2.2f, 0f);
            }

            if (RecoilShakeTimer <= 0f)
            {
                RecoilShakeTimer = 0f;
                RecoilShakePower = 0f;
            }
            else
            {
                RecoilShakeTimer = Math.Max(RecoilShakeTimer - dt, 0f);
                RecoilShakePower = Math.Max(RecoilShakePower - dt * 4.6f, 0f);
            }

            hitMarkerTimer = Math.Max(0f, hitMarkerTimer - dt);
            killMarkerTimer = Math.Max(0f, killMarkerTimer - dt);
            weaponStatusTimer = Math.Max(0f, weaponStatusTimer - dt);
            weaponStatusRepeatGate = Math.Max(0f, weaponStatusRepeatGate - dt);
            if (weaponStatusTimer <= 0f)
            {
                WeaponStatusText = null;
            }

            pickupToastTimer = Math.Max(0f, pickupToastTimer - dt);
            if (pickupToastTimer <= 0f)
            {
                PickupToastText = null;
            }
        }

        public void Reset()
        {
            DamageFlash = 0f;
            DamageDirX = 0f;
            DamageDirY = 0f;
            DamageShakeTimer = 0f;
            DamageShakePower = 0f;
            RecoilShakeTimer = 0f;
            RecoilShakePower = 0f;
            hitMarkerTimer = 0f;
            killMarkerTimer = 0f;
            WeaponStatusText = null;
            weaponStatusTimer = 0f;
            weaponStatusRepeatGate = 0f;
            PickupToastText = null;
            pickupToastTimer = 0f;
        }
    }
}
