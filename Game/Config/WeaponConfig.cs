namespace My2DEngine.Game.Config
{
    /// <summary>
    /// 무기 5종의 피해·쿨다운·사거리·탄약과 무기 오버레이 애니메이션 값.
    /// 값을 바꾸면 게임 밸런스에 바로 영향을 주므로 의도적으로 수정할 때만 바꾼다.
    /// </summary>
    public static class WeaponConfig
    {
        /// <summary>발사 애니메이션 총 지속 시간(초).</summary>
        public const float WeaponAnimDuration = 0.3f;

        /// <summary>무기 시트에서 루프하는 idle 프레임 수.</summary>
        public const int WeaponIdleFrameCount = 3;

        /// <summary>무기 시트에서 발사 시퀀스 프레임 수.</summary>
        public const int WeaponFireSequenceFrameCount = 5;

        /// <summary>무기 시트 전체 프레임 수. idle + fire 순서로 저장된다.</summary>
        public const int WeaponOverlayFrameCount = WeaponIdleFrameCount + WeaponFireSequenceFrameCount;

        /// <summary>발사 중이 아닐 때 idle 프레임 한 칸당 유지 시간(초).</summary>
        public const float WeaponIdleAnimFrameDuration = 0.18f;

        /// <summary>.44 AMP 최대 보유 탄약 수 (런 내 기준).</summary>
        public const int PistolMaxAmmo = 60;

        /// <summary>.44 AMP 발사 쿨다운(초). 무겁고 강한 사이드암에 맞춰 느리게 잡는다.</summary>
        public const float PistolCooldown = 0.30f;

        /// <summary>.44 AMP 기본 피해량.</summary>
        public const float PistolDamage = 48f;

        /// <summary>.44 AMP 최대 사거리(타일).</summary>
        public const float PistolRange = 32f;

        /// <summary>.44 AMP 탄 퍼짐 반지름(타일). 정밀한 단발 사이드암이라 좁다.</summary>
        public const float PistolSpread = 0.12f;

        /// <summary>BearKiller 최대 보유 탄약 수 (재장전 없음). 더블배럴 화력에 맞춰 여유 탄 수를 줄였다.</summary>
        public const int ShotGunMaxAmmo = 24;

        /// <summary>BearKiller 발사 쿨다운(초). 무거운 펌프/브레이크 오픈 템포를 반영해 느리게 잡는다.</summary>
        public const float ShotGunCooldown = 1.02f;

        /// <summary>BearKiller 탄환 1발당 피해량. 단일 펠릿 화력은 낮추고 총 펠릿 수로 위력을 만든다.</summary>
        public const float ShotGunDamagePerPellet = 12f;

        /// <summary>BearKiller 1번 사격 시 발사되는 탄환 수. SSG 계열에 가깝게 크게 늘린다.</summary>
        public const int ShotGunPelletCount = 16;

        /// <summary>BearKiller 최대 사거리(타일). 강한 근중거리 화기라 기본 샷건보다 짧다.</summary>
        public const float ShotGunRange = 13f;

        /// <summary>BearKiller 탄 퍼짐 반지름(타일). 광범위 산탄으로 극근접 제압에 맞춘다.</summary>
        public const float ShotGunSpread = 1.7f;

        /// <summary>BearKiller가 최대 피해를 유지하는 거리(타일).</summary>
        public const float ShotGunFullDamageRange = 3.6f;

        /// <summary>BearKiller 최대 사거리에서 적용되는 최소 피해 배율.</summary>
        public const float ShotGunMinDamageMultiplier = 0.30f;

        /// <summary>BearKiller 발사 시 반동 흔들림 세기 배율.</summary>
        public const float ShotGunRecoilPowerMultiplier = 1.65f;

        /// <summary>BearKiller 발사 시 반동 흔들림 지속시간 배율.</summary>
        public const float ShotGunRecoilDurationMultiplier = 1.55f;

        /// <summary>Heavy Chaingun 최대 보유 탄약 수 (재장전 없음).</summary>
        public const int LMGMaxAmmo = 200;

        /// <summary>Heavy Chaingun 발사 쿨다운(초). 낮을수록 연사가 빠르다.</summary>
        public const float LMGCooldown = 0.09f;

        /// <summary>Heavy Chaingun 기본 피해량.</summary>
        public const float LMGDamage = 16f;

        /// <summary>Heavy Chaingun 최대 사거리(타일).</summary>
        public const float LMGRange = 25f;

        /// <summary>Heavy Chaingun 탄 퍼짐 반지름(타일).</summary>
        public const float LMGSpread = 0.55f;

        /// <summary>Heavy Chaingun 준비 지연 시간(초). 원본 팩이 즉시 연사형이라 0으로 유지한다.</summary>
        public const float LMGWindUpDelay = 0f;

        /// <summary>Auto Cannon 최대 보유 탄약 수 (재장전 없음).</summary>
        public const int RocketMaxAmmo = 18;

        /// <summary>Auto Cannon 발사 쿨다운(초). 플레이어 로켓 투사체를 발사한다.</summary>
        public const float RocketCooldown = 0.62f;

        /// <summary>Auto Cannon 기본 피해량.</summary>
        public const float RocketDamage = 78f;

        /// <summary>Auto Cannon 폭발 범위 반지름(타일).</summary>
        public const float RocketSplashRadius = 2.65f;

        /// <summary>Auto Cannon 최대 사거리(타일).</summary>
        public const float RocketRange = 28f;

        /// <summary>Auto Cannon 탄 퍼짐 반지름(타일).</summary>
        public const float RocketSpread = 0.45f;

        /// <summary>플레이어 로켓 비행 속도(타일/초). 낮을수록 천천히 날아가는 느낌이 강해진다.</summary>
        public const float RocketProjectileSpeed = 6.2f;

        /// <summary>플레이어 로켓 기본 충돌 반지름(타일).</summary>
        public const float RocketProjectileRadius = 0.16f;

        /// <summary>플레이어 로켓 최대 비행 시간(초). 벽에 맞지 않아도 이 시간이 지나면 폭발한다.</summary>
        public const float RocketProjectileLifetime = 4.8f;

        /// <summary>로켓 폭발 스프라이트가 화면에 남아 있는 시간(초).</summary>
        public const float RocketExplosionVisualDuration = 0.12f;

        /// <summary>Dual 92s 최대 보유 탄약 수 (재장전 없음).</summary>
        public const int PlazmaGunMaxAmmo = 60;

        /// <summary>Dual 92s 발사 쿨다운(초).</summary>
        public const float PlazmaGunCooldown = 0.12f;

        /// <summary>Dual 92s 기본 피해량. 레거시 슬롯명을 유지해 MinDamage 상수를 재사용한다.</summary>
        public const float PlazmaGunMinDamage = 18f;

        /// <summary>Dual 92s 최대 사거리(타일).</summary>
        public const float PlazmaGunRange = 24f;

        /// <summary>Dual 92s 탄 퍼짐 반지름(타일).</summary>
        public const float PlazmaGunSpread = 0.34f;
    }
}
