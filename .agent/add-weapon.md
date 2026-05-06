# add-weapon

새로운 무기를 My2DEngine에 추가하는 체크리스트.

## 무기 시스템 구조 요약

- WeaponType 열거형: `AMPistol, BearKiller, HChainGun, AutoCannon, DuelBerettas` (현재 5종)
- 탄약: `ammoPerWeapon[5]` — 무기별 독립 탄약
- 업그레이드 매트릭스: `weaponUpgradeLevels[5, 8]` (무기 × WeaponUpgradeCategory)
- 인덱스 범위 **반드시 준수** (5×8)

## 필수 절차 (순서대로)

### 1. WeaponType 열거형 추가
- 파일: `Game/Entities/Weapon.cs`
- 기존 값 뒤에 추가; `ammoPerWeapon`·`weaponUpgradeLevels` 배열 크기도 함께 확장

### 2. GameConfig에 스탯 상수 추가
- 파일: `Game/Config/GameConfig.cs`
- 필수 항목: 데미지, 쿨다운(초), 사거리(tiles), 스프레드, 탄약 최대값
- 특수 기능(스플래시, 관통, 추적 등)도 여기서 정의

### 3. Weapon 클래스에 발사 로직 추가
- 파일: `Game/Entities/Weapon.cs`
- 발사 유형별 분기:
  - **단발/연사**: `ShotCooldown` 체크 후 즉시 적용
  - **홀드 연사** (체인건 스타일): 키 홀드 동안 반복 발사
  - **홀드 연사** (DuelBerettas 현재 스타일): 버튼 홀드 동안 `PendingShot` 반복 세팅
  - **발사체**: `GameLogic.Projectiles`의 플레이어 로켓 흐름처럼 `EnemyProjectile` 인스턴스 생성

참고: 현재 AutoCannon은 `GameLogic.Projectiles`의 플레이어 로켓 투사체 흐름을 사용한다. 즉발 범위 피해가 필요한 특수기는 별도 메서드에서 처리한다.

### 4. 탄약 소모·보충 연결
- `ammoPerWeapon[newWeaponIndex]` 초기값 설정
- RewardPickup 탄약 드롭에 신규 무기 포함 여부 결정

### 5. HUD 표시 연결
- `RaycastRenderer` / OverlayPresenter에서 무기 아이콘·탄약 표시 추가

### 6. 오디오 연결
- `EffectSoundManager`에 발사음 등록
- 연사 무기는 사운드 루프 별도 관리 (HChainGun 참조)

### 7. 업그레이드 카드 연결
- `weaponUpgradeLevels[newWeaponIndex, category]` 사용 시
  WeaponUpgradeCategory(`Damage, Range, Pellets, FireRate, Spread, Splash, AmmoDropBonus, Special`) 중 해당 항목 처리 로직 확인

## 체크리스트

- [ ] WeaponType 열거형에 추가됨
- [ ] 배열 크기(ammoPerWeapon, weaponUpgradeLevels) 확장됨
- [ ] 모든 수치값이 GameConfig에 있음
- [ ] 발사 로직 구현됨 (Weapon.cs)
- [ ] HUD에 탄약 표시됨
- [ ] 발사음 연결됨
- [ ] 업그레이드 매트릭스 인덱스 범위(×8) 준수됨
