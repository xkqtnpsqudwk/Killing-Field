using System.Drawing;

namespace My2DEngine.Game.Config
{
    /// <summary>
    /// 게임 전반에서 공유하는 고정 밸런스 값과 연출 상수를 한곳에 모아둔 정적 클래스다.
    /// 값을 바꾸면 전체 게임 밸런스에 영향을 미치므로 의도적으로 수정할 때만 변경한다.
    /// </summary>
    public static class GameConfig
    {
        /// <summary>벽 텍스처 한 장의 크기(픽셀). 정사각형으로 가정한다.</summary>
        public const int TextureSize = 64;

        /// <summary>문(door) 타일 타입 번호. 맵 배열에서 이 값이면 문으로 처리된다.</summary>
        public const int DoorTileType = 9;

        /// <summary>문 텍스처 ID. TextureIds 배열에서 이 값을 가진 타일에 문 텍스처를 그린다.</summary>
        public const int DoorTextureId = 5;

        /// <summary>문 한 짝이 완전히 열리는 데 걸리는 시간(초).</summary>
        public const float DoorOpenDuration = 0.85f;

        /// <summary>플레이어가 문과 상호작용할 수 있는 최대 거리(타일).</summary>
        public const float DoorInteractDistance = 1.5f;

        /// <summary>전리품 아이템 수거 판정 거리(타일). 이 거리 이내로 접근하면 자동 획득된다.</summary>
        public const float PickupRadius = 0.7f;

        /// <summary>보스 등장 연출(인트로) 지속 시간(초). 이 시간 동안 화면에 보스 이름이 표시된다.</summary>
        public const float BossIntroDuration = 2.4f;

        /// <summary>전투 층(휴식 제외) 1회 클리어당 이후 적 최대 체력에 더해지는 성장 배율이다.</summary>
        public const float EnemyHealthGrowthPerClearedCombatFloor = 0.004f;

        /// <summary>전투 층(휴식 제외) 1회 클리어당 이후 적 공격력에 더해지는 성장 배율이다.</summary>
        public const float EnemyDamageGrowthPerClearedCombatFloor = 0.003f;

        /// <summary>전투 층(휴식 제외) 1회 클리어당 이후 적 이동 속도에 더해지는 성장 배율이다.</summary>
        public const float EnemyMoveSpeedGrowthPerClearedCombatFloor = 0.005f;

        /// <summary>보스 1회 클리어당 이후 적 최대 체력에 더해지는 성장 배율이다.</summary>
        public const float EnemyHealthGrowthPerBossClear = 0.04f;

        /// <summary>보스 1회 클리어당 이후 적 공격력에 더해지는 성장 배율이다.</summary>
        public const float EnemyDamageGrowthPerBossClear = 0.03f;

        /// <summary>보스 1회 클리어당 이후 적 이동 속도에 더해지는 성장 배율이다.</summary>
        public const float EnemyMoveSpeedGrowthPerBossClear = 0.015f;

        /// <summary>적 피격 플래시가 유지되는 시간(초).</summary>
        public const float EnemyHitFlashDuration = 0.18f;

        /// <summary>적이 피격 직후 움찔거리는 반응이 유지되는 시간(초).</summary>
        public const float EnemyHitReactDuration = 0.16f;

        /// <summary>일반 적이 피격 반응 중 이동 속도에 곱하는 배율.</summary>
        public const float EnemyHitReactMoveMultiplier = 0.42f;

        /// <summary>보스급 적이 피격 반응 중 이동 속도에 곱하는 배율.</summary>
        public const float BossHitReactMoveMultiplier = 0.72f;

        /// <summary>적 피격 반응 중 렌더 스케일에 더하는 최대 펄스 배율.</summary>
        public const float EnemyHitReactScalePulse = 0.08f;

        /// <summary>생존 방의 기본 생존 목표 시간(초).</summary>
        public const float SurvivalRoomBaseDuration = 18f;

        /// <summary>생존 방 목표 시간이 층마다 늘어나는 양(초).</summary>
        public const float SurvivalRoomDurationPerFloor = 0.08f;

        /// <summary>정예 생존 방에 추가되는 목표 시간(초).</summary>
        public const float SurvivalRoomEliteExtraDuration = 4f;

        /// <summary>생존 방 목표 시간 상한(초).</summary>
        public const float SurvivalRoomMaxDuration = 32f;

        /// <summary>생존 방 증원 생성 간격(초).</summary>
        public const float SurvivalRoomReinforcementInterval = 3.4f;

        /// <summary>생존 방에서 유지하려는 일반 활성 적 수.</summary>
        public const int SurvivalRoomTargetAliveEnemies = 4;

        /// <summary>정예 생존 방에서 추가로 유지하려는 활성 적 수.</summary>
        public const int SurvivalRoomEliteTargetAliveBonus = 1;

        /// <summary>생존 방 증원 1회당 최대 생성 수.</summary>
        public const int SurvivalRoomReinforcementCount = 2;

        /// <summary>열쇠 방 표적 적의 체력 배율.</summary>
        public const float KeyTargetHealthMultiplier = 1.65f;

        /// <summary>열쇠 방 표적 적의 스케일 배율.</summary>
        public const float KeyTargetScaleMultiplier = 1.12f;

        /// <summary>독성 안개 위험 방의 피해 간격(초).</summary>
        public const float ToxicMistDamageInterval = 1.15f;

        /// <summary>독성 안개가 한 번에 주는 피해량.</summary>
        public const float ToxicMistDamage = 4f;

        /// <summary>플레이어 기본 이동 속도(타일/초).</summary>
        public const float MoveSpeed = 2.5f;

        /// <summary>달리기(스프린트) 시 이동 속도에 곱하는 배율.</summary>
        public const float SprintMultiplier = 1.5f;

        /// <summary>스태미나가 완전히 고갈됐을 때 이동 속도에 곱하는 배율(반속).</summary>
        public const float ExhaustedSpeedMultiplier = 0.5f;

        /// <summary>마우스 수평 이동 1픽셀당 회전 각도(라디안).</summary>
        public const float MouseSensitivity = 0.003f;

        /// <summary>플레이어 충돌 원 반지름(타일). 작을수록 좁은 공간을 통과하기 쉽다.</summary>
        public const float PlayerRadius = 0.1f;

        /// <summary>
        /// 충돌 검사 1회 최대 이동 거리(타일).
        /// 큰 DeltaTime에서 벽 통과를 방지하기 위해 이동을 여러 단계로 분할한다.
        /// </summary>
        public const float CollisionStepSize = 0.025f;

        /// <summary>스태미나 최대값.</summary>
        public const float StaminaMax = 100f;

        /// <summary>달리는 동안 초당 소모되는 스태미나량.</summary>
        public const float StaminaDrainPerSec = 15f;

        /// <summary>스태미나가 회복될 때 초당 회복량.</summary>
        public const float StaminaRecoverPerSec = 15f;

        /// <summary>스태미나 소모를 멈춘 뒤 회복이 시작되기까지의 대기 시간(초).</summary>
        public const float StaminaRecoverDelay = 1.35f;

        /// <summary>대시 1회당 소모되는 스태미나량.</summary>
        public const float DashStaminaCost = 20f;

        /// <summary>대시 후 다시 대시할 수 있게 되기까지의 쿨다운 시간(초).</summary>
        public const float DashCooldownDuration = 5.0f;

        /// <summary>카드 보너스 적용 후 대시 쿨다운이 더 이상 줄어들지 않는 최소 시간(초).</summary>
        public const float DashCooldownMinDuration = 0.5f;

        /// <summary>대시로 이동하는 총 거리(타일). DashDuration 동안 이 거리를 이동한다.</summary>
        public const float DashDistance = 2.0f;

        /// <summary>대시가 지속되는 시간(초). 짧을수록 순간적인 돌진 느낌이 강해진다.</summary>
        public const float DashDuration = 0.20f;

        /// <summary>체력 치료제(HealthPack)를 획득했을 때 회복하는 체력량.</summary>
        public const float HealthPickupAmount = 32f;

        /// <summary>플레이어 최대 체력.</summary>
        public const float PlayerHealthMax = 100f;

        /// <summary>영구 스탯 체력 1포인트당 최대 체력 증가율.</summary>
        public const float PermanentHealthPerPoint = 0.10f;

        /// <summary>영구 스탯 속도 1포인트당 이동 속도 증가율.</summary>
        public const float PermanentMoveSpeedPerPoint = 0.05f;

        /// <summary>영구 스탯 Pistol 데미지 1포인트당 피해 증가율.</summary>
        public const float PermanentPistolDamagePerPoint = 0.10f;

        /// <summary>영구 스탯 감각 1포인트당 증가량.</summary>
        public const float PermanentSensePerPoint = 0.10f;

        /// <summary>영구 스탯 감각 최대값 (5레벨 달성 누적값).</summary>
        public const float PermanentSenseMax = 7.5f;

        /// <summary>영구 스탯 행운 1포인트당 증가량.</summary>
        public const float PermanentLuckPerPoint = 0.10f;

        /// <summary>영구 스탯 행운 최대값 (10레벨 달성 누적값).</summary>
        public const float PermanentLuckMax = 27.5f;

        /// <summary>탄창 최대 탄약 수.</summary>
        public const int MagazineSize = 9;

        /// <summary>발사 후 다시 발사할 수 있게 되기까지의 쿨다운 시간(초).</summary>
        public const float ShotCooldownDuration = 0.22f;

        /// <summary>기본 발사 피해량.</summary>
        public const float ShotDamage = 30f;

        /// <summary>히트 판정 최대 거리(타일). 이 거리 바깥의 적은 맞지 않는다.</summary>
        public const float ShotRange = 30f;

        /// <summary>탄 퍼짐 반지름(타일). 클수록 조준에서 더 멀리 벗어난 적도 맞는다.</summary>
        public const float ShotSpreadRadius = 0.28f;

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
        /// <summary>발사/보조 애니메이션 종료 후 사운드를 조금 더 유지하는 tail 시간(초).</summary>
        public const float WeaponSoundTailGrace = 0.14f;
        /// <summary>스팀팩 사용 시 이동 속도에 곱하는 배율.</summary>
        public const float StimSpeedMultiplier = 2.0f;

        /// <summary>스팀팩 효과 지속 시간(초).</summary>
        public const float StimBoostDuration = 6.0f;

        /// <summary>휴식 상점에서 회복 키트를 구매할 때 필요한 코인 수.</summary>
        public const int RestShopHealthPackCost = 1;

        /// <summary>휴식 상점에서 스팀팩을 구매할 때 필요한 코인 수.</summary>
        public const int RestShopStimPackCost = 1;

        /// <summary>휴식 상점에서 탄약 보급을 구매할 때 필요한 코인 수.</summary>
        public const int RestShopAmmoPackCost = 1;

        /// <summary>보스를 1회 클리어할 때마다 휴식 상점 가격에 더해지는 코인 수.</summary>
        public const int RestShopCostIncreasePerBossClear = 1;

        /// <summary>플레이어가 자동으로 올라설 수 있는 최대 바닥 단차(타일). Doom의 24/64 ≈ 0.375와 동일.</summary>
        public const float MaxStepHeight = 0.375f;

        /// <summary>레이캐스팅 근거리 클리핑 거리(타일). 카메라에 너무 가까운 벽을 처리한다.</summary>
        public const float NearPlane = 0.2f;

        /// <summary>근거리 클리핑 부드러움 계수. 클수록 클리핑 경계가 부드럽게 처리된다.</summary>
        public const float NearPlaneSoftness = 0.03f;

        /// <summary>기본 시야각(도 단위).</summary>
        public const float DefaultFovDegrees = 80f;

        /// <summary>설정에서 선택 가능한 최소 시야각(도 단위).</summary>
        public const float MinFovDegrees = 60f;

        /// <summary>설정에서 선택 가능한 최대 시야각(도 단위).</summary>
        public const float MaxFovDegrees = 90f;

        /// <summary>GPU 월드 렌더 타깃 최대 너비(픽셀). 이보다 크면 이 값으로 제한된다.</summary>
        public const int GpuWorldMaxRenderWidth = 640;

        /// <summary>GPU 월드 렌더 타깃 최대 높이(픽셀).</summary>
        public const int GpuWorldMaxRenderHeight = 360;

        /// <summary>true이면 바닥에 텍스처를 입힌다. false이면 단색 FloorColor로 표시된다.</summary>
        public const bool GpuWorldUseTexturedFloor = true;

        /// <summary>true이면 천장에 텍스처를 입힌다. false이면 단색 CeilingColor로 표시된다.</summary>
        public const bool GpuWorldUseTexturedCeiling = true;

        /// <summary>천장 색상(기본값: 검정). 텍스처와 블렌딩하거나 단색 폴백으로 사용된다.</summary>
        public static readonly Color CeilingColor = Color.Black;

        /// <summary>바닥 단색(기본값: 어두운 회색).</summary>
        public static readonly Color FloorColor = Color.FromArgb(60, 60, 60);

        /// <summary>.44 AMP 발사 사운드 별칭.</summary>
        public const string PistolFireSoundAlias = "kf_pistol_fire";
        /// <summary>.44 AMP 발사 사운드 상대 경로.</summary>
        public const string PistolFireSoundPath = @"Gun\Pistol\Fire.wav";

        /// <summary>BearKiller 발사 사운드 별칭.</summary>
        public const string ShotGunFireSoundAlias = "kf_shotgun_fire";
        /// <summary>BearKiller 발사 사운드 상대 경로.</summary>
        public const string ShotGunFireSoundPath = @"Gun\ShotGun\Fire.wav";

        /// <summary>Heavy Chaingun 발사 사운드 별칭.</summary>
        public const string LMGFireSoundAlias = "kf_lmg_fire";
        /// <summary>Heavy Chaingun 발사 사운드 상대 경로.</summary>
        public const string LMGFireSoundPath = @"Gun\LMG\Fire.wav";
        /// <summary>LMG 회전 시작(wind up) 사운드 별칭. 현재 Heavy Chaingun 팩에는 대응 효과음이 없어 레거시 호환용으로만 남긴다.</summary>
        public const string LMGWindUpSoundAlias = "kf_lmg_windup";
        /// <summary>LMG 회전 시작(wind up) 사운드 상대 경로. 현재는 레거시 호환용이다.</summary>
        public const string LMGWindUpSoundPath = @"Gun\LMG\WindUP.wav";
        /// <summary>LMG 회전 종료(wind down) 사운드 별칭. 현재 Heavy Chaingun 팩에는 대응 효과음이 없어 레거시 호환용으로만 남긴다.</summary>
        public const string LMGWindDownSoundAlias = "kf_lmg_winddown";
        /// <summary>LMG 회전 종료(wind down) 사운드 상대 경로. 현재는 레거시 호환용이다.</summary>
        public const string LMGWindDownSoundPath = @"Gun\LMG\WindDown.wav";

        /// <summary>Auto Cannon 발사 사운드 별칭.</summary>
        public const string RocketFireSoundAlias = "kf_rocket_fire";
        /// <summary>Auto Cannon 발사 사운드 상대 경로.</summary>
        public const string RocketFireSoundPath = @"Gun\RocketLauncher\Fire.wav";
        /// <summary>Auto Cannon 레거시 폭발 사운드 별칭. 현재는 별도 착탄음을 사용하지 않는다.</summary>
        public const string RocketBoomSoundAlias = "kf_rocket_boom";
        /// <summary>Auto Cannon 레거시 폭발 사운드 상대 경로.</summary>
        public const string RocketBoomSoundPath = @"Gun\RocketLauncher\Boom.wav";
        /// <summary>레거시 추적탄 비행 사운드 별칭.</summary>
        public const string RocketFlySoundAlias = "kf_rocket_fly";
        /// <summary>레거시 추적탄 비행 사운드 상대 경로.</summary>
        public const string RocketFlySoundPath = @"Gun\RocketLauncher\Fly.wav";

        /// <summary>Dual 92s 발사 사운드 별칭.</summary>
        public const string PlazmaGunFireSoundAlias = "kf_plazma_fire";
        /// <summary>Dual 92s 발사 사운드 상대 경로.</summary>
        public const string PlazmaGunFireSoundPath = @"Gun\PlazmaGun\Fire.wav";

        // ─────────────────────────── Pistol ───────────────────────────
        /// <summary>.44 AMP 탄창 크기(레거시 UI 호환용).</summary>
        public const int PistolMagazineSize = 12;
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

        // ─────────────────────────── ShotGun ──────────────────────────
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

        // ───────────────────────────── LMG ────────────────────────────
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

        // ─────────────────────────── RocketLauncher ───────────────────
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

        // ─────────────────────────── PlazmaGun ────────────────────────
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

        /// <summary>문 개방 사운드 별칭.</summary>
        public const string DoorSoundAlias = "kf_door";
        /// <summary>문 개방 사운드 상대 경로.</summary>
        public const string DoorSoundPath = @"effect\Door.wav";

        /// <summary>일반 전투 구간에서 무작위로 선택되는 배경 음악 트랙 파일명 목록.</summary>
        public static readonly string[] NormalBackgroundTracks = new[]
        {
            @"BackGroundMusic\Normal.wav",
            @"BackGroundMusic\Normal2.wav"
        };

        /// <summary>미니보스 방에서 재생되는 배경 음악 트랙 파일명 목록.</summary>
        public static readonly string[] MiniBossBackgroundTracks = new[]
        {
            @"BackGroundMusic\MiniBoss.wav",
            @"BackGroundMusic\MiniBoss2.wav"
        };

        /// <summary>보스 방에서 재생되는 배경 음악 트랙 파일명 목록.</summary>
        public static readonly string[] BossBackgroundTracks = new[]
        {
            @"BackGroundMusic\Boss.wav",
            @"BackGroundMusic\Boss2.wav"
        };
    }
}
