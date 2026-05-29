using System;
using My2DEngine.Game.Config;

namespace My2DEngine.Game
{
    /// <summary>
    /// 플레이어 무기의 상태를 관리하는 객체.
    /// 5종 무기(AMPistol/BearKiller/HChainGun/AutoCannon/DuelBerettas)를 하나의 인스턴스로 관리하며,
    /// 무기 전환 시 각 무기의 탄약은 독립적으로 보존된다.
    ///
    /// 발사 방식:
    ///  - AMPistol / BearKiller / AutoCannon : 단발
    ///  - HChainGun / DuelBerettas           : 홀드 연사
    /// </summary>
    public class Weapon
    {
        // ─── 무기 종류 ────────────────────────────────────────────────
        /// <summary>현재 장착된 무기 종류.</summary>
        public WeaponType CurrentType { get; private set; }

        // ─── 탄약 ─────────────────────────────────────────────────────
        /// <summary>무기별 잔여 탄약 배열. (int)WeaponType 인덱스로 접근한다.</summary>
        private readonly int[] ammoPerWeapon;

        /// <summary>현재 무기의 잔여 탄약 수.</summary>
        public int CurrentAmmo => ammoPerWeapon[(int)CurrentType];

        /// <summary>현재 무기의 탄창 최대 용량.</summary>
        public int MagazineSize { get; private set; }

        // ─── 발사 제어 플래그 ─────────────────────────────────────────
        /// <summary>단발 발사 요청 플래그. HandlePendingShot()이 처리 후 false로 초기화.</summary>
        public bool PendingShot { get; set; }

        /// <summary>홀드 연사형 무기(HChainGun, DuelBerettas)가 버튼이 눌린 동안 true를 유지한다.</summary>
        public bool FireButtonHeld { get; set; }

        // ─── 타이머 ───────────────────────────────────────────────────
        /// <summary>발사 쿨다운 잔여 시간(초).</summary>
        public float ShotCooldown { get; private set; }

        /// <summary>총구 화염 표시 잔여 시간(초).</summary>
        public float MuzzleFlashTimer { get; private set; }

        /// <summary>발사 애니메이션 잔여 시간(초).</summary>
        public float WeaponAnimTimer { get; private set; }

        // ─── 피해 · 사거리 ────────────────────────────────────────────
        /// <summary>기본 피해량. ShotGun은 탄환 1발 기준이다.</summary>
        public float Damage { get; private set; }

        /// <summary>현재 피해량. 스탯 카드 배율 적용 포함.</summary>
        public float CurrentDamage => Damage * StatDamageMult;

        /// <summary>최대 사격 유효 거리(타일).</summary>
        public float Range { get; private set; }

        /// <summary>탄 퍼짐 반지름(타일).</summary>
        public float SpreadRadius { get; private set; }

        // ─── 무기별 특수 능력치 ───────────────────────────────────────
        /// <summary>ShotGun 전용. 1회 사격 시 발사 탄환 수.</summary>
        public int PelletCount { get; private set; }

        /// <summary>AutoCannon 전용. 폭발 범위 반지름(타일).</summary>
        public float SplashRadius { get; private set; }

        // ─── 스탯 카드 전역 배율 ──────────────────────────────────────
        /// <summary>스탯 카드 '데미지' 보너스 배율 (1.0 = 없음). 모든 무기에 공통 적용.</summary>
        public float StatDamageMult { get; private set; } = 1f;

        /// <summary>영구 스탯이 제공하는 Pistol 전용 추가 피해 배율 (1.0 = 없음).</summary>
        public float PermanentPistolDamageMult { get; private set; } = 1f;

        // ─── 무기 업그레이드 등급 [weaponIndex, categoryIndex] ────────
        // 0 = 없음, 2 = Blue, 3 = Purple, 4 = Red(특수기 전용)
        private readonly int[,] weaponUpgradeLevels; // [5 무기, 8 카테고리]

        // ─── 내부 상태 ────────────────────────────────────────────────
        private float currentShotCooldownDuration;
        private float idleAnimationTimer;

        // ─── 생성자 ───────────────────────────────────────────────────
        public Weapon()
        {
            ammoPerWeapon = new int[5];
            weaponUpgradeLevels = new int[5, 8]; // [WeaponType, WeaponUpgradeCategory]
            Reset();
        }

        // ─── 공개 메서드 ──────────────────────────────────────────────

        /// <summary>
        /// 무기 전체 상태를 초기화한다. 모든 무기의 탄약이 가득 채워지고 Pistol이 선택된다.
        /// </summary>
        public void Reset()
        {
            ResetRunUpgrades();

            for (int i = 0; i < ammoPerWeapon.Length; i++)
                ammoPerWeapon[i] = GetMaxAmmoForType((WeaponType)i);

            CurrentType = WeaponType.AMPistol;
            PendingShot = false;
            ShotCooldown = 0f;
            MuzzleFlashTimer = 0f;
            WeaponAnimTimer = 0f;
            idleAnimationTimer = 0f;
            FireButtonHeld = false;

            ApplyWeaponStats(WeaponType.AMPistol);
        }

        /// <summary>
        /// 런 중 얻은 무기 업그레이드와 스탯 카드 배율을 초기화한다.
        /// 런 시작 시(또는 Reset() 내부에서) 호출된다.
        /// </summary>
        public void ResetRunUpgrades()
        {
            for (int w = 0; w < 5; w++)
                for (int c = 0; c < 8; c++)
                    weaponUpgradeLevels[w, c] = 0;

            StatDamageMult = 1f;
            SpecialCooldownTimer = 0f;
            LmgBurstRemaining = 0;
            PlazmaLaserActive = false;
            SpecialUsedThisFloor = false;
        }

        /// <summary>
        /// 지정한 무기와 카테고리의 업그레이드 등급을 반환한다 (0=없음, Blue/Purple/Red는 <see cref="CardGrade"/> 값을 따른다).
        /// </summary>
        public int GetUpgradeLevel(WeaponType type, WeaponUpgradeCategory category)
        {
            return weaponUpgradeLevels[(int)type, (int)category];
        }

        /// <summary>지정 무기의 현재 잔탄을 반환한다.</summary>
        public int GetAmmo(WeaponType type)
        {
            return ammoPerWeapon[(int)type];
        }

        /// <summary>스모크 테스트 전용. 지정 무기의 잔탄을 직접 설정한다.</summary>
        internal void SetAmmoForSmoke(WeaponType type, int amount)
        {
            int max = GetMaxAmmoForType(type);
            ammoPerWeapon[(int)type] = Math.Max(0, Math.Min(max, amount));
        }

        /// <summary>
        /// 저장된 런 상태를 기준으로 무기 업그레이드, 탄약, 현재 장착 무기를 복원한다.
        /// 영구 스탯 배율은 별도로 다시 적용되므로 여기서는 런 전용 상태만 다룬다.
        /// </summary>
        public void RestoreSavedRunState(int[] ammoCounts, int[,] upgradeLevels, WeaponType currentType)
        {
            ResetRunUpgrades();

            if (upgradeLevels != null)
            {
                int weaponCount = Math.Min(weaponUpgradeLevels.GetLength(0), upgradeLevels.GetLength(0));
                int categoryCount = Math.Min(weaponUpgradeLevels.GetLength(1), upgradeLevels.GetLength(1));
                for (int w = 0; w < weaponCount; w++)
                {
                    for (int c = 0; c < categoryCount; c++)
                    {
                        int level = upgradeLevels[w, c];
                        if (level < 0)
                        {
                            level = 0;
                        }
                        else if (level > (int)CardGrade.Red)
                        {
                            level = (int)CardGrade.Red;
                        }

                        weaponUpgradeLevels[w, c] = level;
                    }
                }
            }

            if (ammoCounts != null)
            {
                int ammoCount = Math.Min(ammoPerWeapon.Length, ammoCounts.Length);
                for (int i = 0; i < ammoCount; i++)
                {
                    int clamped = Math.Max(0, Math.Min(GetMaxAmmoForType((WeaponType)i), ammoCounts[i]));
                    ammoPerWeapon[i] = clamped;
                }
            }

            PendingShot = false;
            FireButtonHeld = false;
            ShotCooldown = 0f;
            MuzzleFlashTimer = 0f;
            WeaponAnimTimer = 0f;
            SpecialCooldownTimer = 0f;
            LmgBurstRemaining = 0;
            PlazmaLaserActive = false;
            SpecialUsedThisFloor = false;

            int currentTypeIndex = (int)currentType;
            if (currentTypeIndex < 0 || currentTypeIndex >= ammoPerWeapon.Length)
            {
                currentType = WeaponType.AMPistol;
            }

            CurrentType = currentType;
            ApplyWeaponStats(CurrentType);
        }

        /// <summary>지정 무기의 최대 장탄 수를 반환한다.</summary>
        public int GetMaxAmmo(WeaponType type)
        {
            return GetMaxAmmoForType(type);
        }

        /// <summary>지정 무기의 탄약이 최대치보다 부족한지 반환한다.</summary>
        public bool NeedsAmmo(WeaponType type)
        {
            return GetAmmo(type) < GetMaxAmmo(type);
        }

        /// <summary>지정 무기의 Red 특수기 카드가 해금됐는지 반환한다.</summary>
        public bool HasSpecialUpgrade(WeaponType type)
        {
            return GetUpgradeLevel(type, WeaponUpgradeCategory.Special) >= (int)CardGrade.Red;
        }

        /// <summary>
        /// 지정한 무기와 카테고리에 업그레이드 등급을 설정하고, 현재 무기이면 즉시 스탯을 재적용한다.
        /// </summary>
        public void SetUpgrade(WeaponType type, WeaponUpgradeCategory category, CardGrade grade)
        {
            weaponUpgradeLevels[(int)type, (int)category] = (int)grade;
            if (type == CurrentType)
            {
                ApplyWeaponStats(CurrentType);
            }
        }

        /// <summary>
        /// 스탯 카드 '데미지' 배율을 설정한다. 1.0 미만은 허용하지 않는다.
        /// </summary>
        public void SetStatDamageMult(float mult)
        {
            StatDamageMult = Math.Max(1f, mult);
        }

        /// <summary>
        /// 영구 스탯이 제공하는 Pistol 전용 피해 배율을 설정한다.
        /// 현재 무기가 Pistol이면 즉시 스탯을 다시 적용한다.
        /// </summary>
        public void SetPermanentPistolDamageMult(float mult)
        {
            PermanentPistolDamageMult = Math.Max(1f, mult);
            if (CurrentType == WeaponType.AMPistol)
            {
                ApplyWeaponStats(CurrentType);
            }
        }

        /// <summary>지정한 무기로 전환한다. 기존 발사 상태를 취소하며 각 무기의 탄약은 독립 보존된다.</summary>
        public void SwitchTo(WeaponType type)
        {
            if (type == CurrentType) return;

            // 진행 중인 상태 전부 취소
            FireButtonHeld = false;
            PendingShot = false;
            ShotCooldown = 0f;
            MuzzleFlashTimer = 0f;
            WeaponAnimTimer = 0f;

            CurrentType = type;
            ApplyWeaponStats(type);
        }

        // ─── Red 특수기 상태 ──────────────────────────────────────────
        /// <summary>현재 Red 특수기 쿨다운 잔여 시간(초). 0이면 사용 가능.</summary>
        public float SpecialCooldownTimer { get; private set; }

        /// <summary>LMG 버스트 모드 잔여 탄약 수 (0이면 비활성).</summary>
        public int LmgBurstRemaining { get; private set; }

        /// <summary>DuelBerettas 피버 모드가 현재 활성인지 여부.</summary>
        public bool PlazmaLaserActive { get; private set; }

        /// <summary>보스 전 시작 전 1회용 특수기가 이미 사용됐는지 여부 (HChainGun·DuelBerettas 전용).</summary>
        public bool SpecialUsedThisFloor { get; private set; }

        /// <summary>BearKiller 갈고리/AutoCannon 포격 전용 쿨타임 기본값(초).</summary>
        private const float SpecialCooldownBase = 12f;

        /// <summary>매 프레임 무기 타이머와 특수기 쿨다운을 갱신한다.</summary>
        public void UpdateTimers(float dt)
        {
            if (ShotCooldown > 0f)
            {
                ShotCooldown -= dt;
                if (ShotCooldown < 0f) ShotCooldown = 0f;
            }

            if (MuzzleFlashTimer > 0f)
            {
                MuzzleFlashTimer -= dt;
                if (MuzzleFlashTimer < 0f) MuzzleFlashTimer = 0f;
            }

            if (WeaponAnimTimer > 0f)
            {
                WeaponAnimTimer -= dt;
                if (WeaponAnimTimer < 0f) WeaponAnimTimer = 0f;
            }

            idleAnimationTimer += Math.Max(0f, dt);
            float idleLoopDuration = GameConfig.WeaponIdleAnimFrameDuration * GameConfig.WeaponIdleFrameCount;
            if (idleLoopDuration > 0f && idleAnimationTimer >= idleLoopDuration)
            {
                idleAnimationTimer %= idleLoopDuration;
            }

            // Red 특수기 쿨다운 감소
            if (SpecialCooldownTimer > 0f)
            {
                SpecialCooldownTimer = Math.Max(0f, SpecialCooldownTimer - dt);
            }
        }

        /// <summary>
        /// 현재 무기에 Red 특수기가 있고 사용 가능한 상태인지 확인한다.
        /// </summary>
        public bool CanUseSpecial()
        {
            if (CurrentType == WeaponType.AMPistol) return false;
            if (!HasSpecialUpgrade(CurrentType)) return false;
            if (SpecialCooldownTimer > 0f) return false;

            // 1회용 특수기 (HChainGun, DuelBerettas)
            if (CurrentType == WeaponType.HChainGun || CurrentType == WeaponType.DuelBerettas)
                return !SpecialUsedThisFloor;

            return true;
        }

        /// <summary>Red 특수기 쿨다운을 시작한다 (쿨타임 무기용).</summary>
        public void StartSpecialCooldown(float duration = SpecialCooldownBase)
        {
            SpecialCooldownTimer = duration;
        }

        /// <summary>LMG 버스트 모드를 시작한다. 현재 탄약 전량을 버스트 잔여로 설정한다.</summary>
        public void StartLmgBurst()
        {
            LmgBurstRemaining = ammoPerWeapon[(int)WeaponType.HChainGun];
            SpecialUsedThisFloor = true;
        }

        /// <summary>LMG 버스트 모드에서 탄 1발을 소비한다. 0이 되면 버스트 모드 종료.</summary>
        public void ConsumeLmgBurstShot()
        {
            if (LmgBurstRemaining > 0)
            {
                LmgBurstRemaining--;
                ammoPerWeapon[(int)WeaponType.HChainGun] = Math.Max(0, ammoPerWeapon[(int)WeaponType.HChainGun] - 1);
            }
        }

        /// <summary>DuelBerettas 피버 모드를 활성화한다.</summary>
        public void StartPlazmaLaser()
        {
            PlazmaLaserActive = true;
            SpecialUsedThisFloor = true;
        }

        /// <summary>DuelBerettas 피버 모드를 비활성화한다.</summary>
        public void StopPlazmaLaser()
        {
            PlazmaLaserActive = false;
        }

        /// <summary>층 전환 시 1회용 특수기 플래그를 리셋한다.</summary>
        public void ResetFloorSpecial()
        {
            SpecialUsedThisFloor = false;
            LmgBurstRemaining = 0;
            PlazmaLaserActive = false;
        }

        /// <summary>카드 선택 시 전체 무기 탄약을 가득 충전한다.</summary>
        public void RefillAllOnCardSelect()
        {
            for (int i = 0; i < 5; i++)
            {
                WeaponType t = (WeaponType)i;
                int max = GetMaxAmmoForType(t);
                ammoPerWeapon[i] = max;
            }
        }

        /// <summary>
        /// 지정 무기에 탄약을 추가한다 (적 처치 드랍 등). 최대치를 초과하지 않는다.
        /// </summary>
        public int AddAmmo(WeaponType type, int amount)
        {
            if (amount <= 0)
            {
                return 0;
            }

            int idx = (int)type;
            int current = ammoPerWeapon[idx];
            int next = Math.Min(GetMaxAmmoForType(type), current + amount);
            ammoPerWeapon[idx] = next;
            return next - current;
        }

        /// <summary>현재 들고 있는 무기에 탄약을 추가하고 실제로 보충된 수량을 반환한다.</summary>
        public int AddAmmoToCurrentWeapon(int amount)
        {
            return AddAmmo(CurrentType, amount);
        }

        /// <summary>발사 가능 여부를 반환한다.</summary>
        public bool CanFire()
        {
            return ShotCooldown <= 0f && CurrentAmmo > 0;
        }

        /// <summary>단발/연사 발사를 실행한다.</summary>
        public void Fire()
        {
            if (!CanFire()) return;

            MuzzleFlashTimer = 0.08f;
            WeaponAnimTimer = GameConfig.WeaponAnimDuration;
            ShotCooldown = currentShotCooldownDuration;

            int idx = (int)CurrentType;
            if (CurrentType == WeaponType.HChainGun && LmgBurstRemaining > 0)
            {
                ConsumeLmgBurstShot();
                return;
            }

            ammoPerWeapon[idx]--;
            if (ammoPerWeapon[idx] < 0) ammoPerWeapon[idx] = 0;
        }

        /// <summary>
        /// 현재 무기의 발사 사운드 별칭을 반환한다.
        /// </summary>
        public string GetFireSoundAlias()
        {
            switch (CurrentType)
            {
                case WeaponType.AMPistol:
                    return GameConfig.PistolFireSoundAlias;
                case WeaponType.BearKiller:
                    return GameConfig.ShotGunFireSoundAlias;
                case WeaponType.HChainGun:
                    return GameConfig.LMGFireSoundAlias;
                case WeaponType.AutoCannon:
                    return GameConfig.RocketFireSoundAlias;
                case WeaponType.DuelBerettas:
                    return GameConfig.PlazmaGunFireSoundAlias;
                default:
                    return null;
            }
        }

        /// <summary>
        /// 현재 발사 애니메이션 프레임 인덱스를 반환한다.
        /// 0~2=idle 루프, 3~7=발사 모션.
        /// </summary>
        public int GetCurrentWeaponFrameIndex()
        {
            if (WeaponAnimTimer <= 0f || GameConfig.WeaponAnimDuration <= 0f)
            {
                int idleFrame = (int)(idleAnimationTimer / GameConfig.WeaponIdleAnimFrameDuration)
                    % GameConfig.WeaponIdleFrameCount;
                return idleFrame;
            }

            float progress = 1f - (WeaponAnimTimer / GameConfig.WeaponAnimDuration);
            if (progress < 0f) progress = 0f;
            int fireFrame = Math.Min(
                (int)(progress * GameConfig.WeaponFireSequenceFrameCount),
                GameConfig.WeaponFireSequenceFrameCount - 1);

            return GameConfig.WeaponIdleFrameCount + fireFrame;
        }

        // ─── 비공개 헬퍼 ──────────────────────────────────────────────

        private void ApplyWeaponStats(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.AMPistol:
                    MagazineSize                = GameConfig.PistolMaxAmmo;
                    Damage                      = GameConfig.PistolDamage * PermanentPistolDamageMult;
                    Range                       = GameConfig.PistolRange;
                    SpreadRadius                = GameConfig.PistolSpread;
                    currentShotCooldownDuration = GameConfig.PistolCooldown;
                    PelletCount                 = 1;
                    SplashRadius                = 0f;
                    break;

                case WeaponType.BearKiller:
                    MagazineSize                = GameConfig.ShotGunMaxAmmo;
                    Damage                      = GameConfig.ShotGunDamagePerPellet
                        * (1f + GetUpgradeMult(type, WeaponUpgradeCategory.Damage));
                    Range                       = GameConfig.ShotGunRange
                        * (1f + GetUpgradeMult(type, WeaponUpgradeCategory.Range));
                    SpreadRadius                = GameConfig.ShotGunSpread;
                    currentShotCooldownDuration = GameConfig.ShotGunCooldown;
                    PelletCount                 = GameConfig.ShotGunPelletCount
                        + (int)GetUpgradeMult(type, WeaponUpgradeCategory.Pellets);
                    SplashRadius                = 0f;
                    break;

                case WeaponType.HChainGun:
                    MagazineSize                = GameConfig.LMGMaxAmmo;
                    Damage                      = GameConfig.LMGDamage
                        * (1f + GetUpgradeMult(type, WeaponUpgradeCategory.Damage));
                    Range                       = GameConfig.LMGRange;
                    SpreadRadius                = GameConfig.LMGSpread
                        * (1f - GetUpgradeMult(type, WeaponUpgradeCategory.Spread));
                    currentShotCooldownDuration = GameConfig.LMGCooldown
                        * (1f - GetUpgradeMult(type, WeaponUpgradeCategory.FireRate));
                    PelletCount                 = 1;
                    SplashRadius                = 0f;
                    break;

                case WeaponType.AutoCannon:
                    MagazineSize                = GameConfig.RocketMaxAmmo;
                    Damage                      = GameConfig.RocketDamage
                        * (1f + GetUpgradeMult(type, WeaponUpgradeCategory.Damage));
                    Range                       = GameConfig.RocketRange;
                    SpreadRadius                = GameConfig.RocketSpread;
                    currentShotCooldownDuration = GameConfig.RocketCooldown;
                    PelletCount                 = 1;
                    SplashRadius                = GameConfig.RocketSplashRadius
                        * (1f + GetUpgradeMult(type, WeaponUpgradeCategory.Splash));
                    break;

                case WeaponType.DuelBerettas:
                    MagazineSize                = GameConfig.PlazmaGunMaxAmmo;
                    Damage                      = GameConfig.PlazmaGunMinDamage
                        * (1f + GetUpgradeMult(type, WeaponUpgradeCategory.Damage));
                    Range                       = GameConfig.PlazmaGunRange
                        * (1f + GetUpgradeMult(type, WeaponUpgradeCategory.Range));
                    SpreadRadius                = GameConfig.PlazmaGunSpread;
                    currentShotCooldownDuration = GameConfig.PlazmaGunCooldown
                        * (1f - GetUpgradeMult(type, WeaponUpgradeCategory.FireRate));
                    PelletCount                 = 1;
                    SplashRadius                = 0f;
                    break;
            }

            // 탄약이 새 탄창 크기를 초과하면 클램프
            int typeIdx = (int)type;
            if (ammoPerWeapon[typeIdx] > MagazineSize)
                ammoPerWeapon[typeIdx] = MagazineSize;
        }

        /// <summary>
        /// 지정 무기·카테고리의 업그레이드 레벨에 따른 보너스 수치를 반환한다.
        /// Pellets의 경우 추가 탄환 개수(정수), 나머지는 비율(0.0 ~ 0.6)이다.
        /// 무기 카드 자체는 Blue/Purple 2단계 업그레이드만 사용하므로 Green 값은 나오지 않는다.
        /// </summary>
        private float GetUpgradeMult(WeaponType type, WeaponUpgradeCategory cat)
        {
            int level = weaponUpgradeLevels[(int)type, (int)cat];
            if (level <= 0) return 0f;
            bool isBlueTier = level == (int)CardGrade.Blue;

            switch (cat)
            {
                case WeaponUpgradeCategory.Damage:     return isBlueTier ? 0.30f : 0.60f;
                case WeaponUpgradeCategory.Range:      return isBlueTier ? 0.30f : 0.60f;
                case WeaponUpgradeCategory.Pellets:    return isBlueTier ? 2f    : 4f;    // 정수 개수
                case WeaponUpgradeCategory.FireRate:   return isBlueTier ? 0.20f : 0.35f; // 쿨타임 감소율
                case WeaponUpgradeCategory.Spread:     return isBlueTier ? 0.30f : 0.55f; // 퍼짐 감소율
                case WeaponUpgradeCategory.Splash:     return isBlueTier ? 0.30f : 0.55f;
                case WeaponUpgradeCategory.AmmoDropBonus: return isBlueTier ? 0.25f : 0.45f; // 드랍 확률 증가
                case WeaponUpgradeCategory.Special:    return 0f;
                default: return 0f;
            }
        }

        private static int GetMaxAmmoForType(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.AMPistol:         return GameConfig.PistolMaxAmmo;
                case WeaponType.BearKiller:        return GameConfig.ShotGunMaxAmmo;
                case WeaponType.HChainGun:            return GameConfig.LMGMaxAmmo;
                case WeaponType.AutoCannon: return GameConfig.RocketMaxAmmo;
                case WeaponType.DuelBerettas:      return GameConfig.PlazmaGunMaxAmmo;
                default:                        return GameConfig.PistolMaxAmmo;
            }
        }
    }
}
