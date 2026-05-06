# progression-save

카드 보상, 영구 스탯, 저장/이어하기를 수정할 때 보는 가이드.

## 핵심 파일

- `Game/Core/CardSystem.cs`
  - `StatType`, `CardGrade`, `RewardCardOffer`
- `Game/Core/GameLogic.CardReward.cs`
  - 런 중 카드 생성/선택/적용
- `Game/Core/GameLogic.PersistentStats.cs`
  - 영구 스탯 UI, 저장/이어하기, 런 보너스 적용
- `Game/Core/PermanentProgressionData.cs`
  - 영구 스탯 데이터 모델과 레벨 계산
- `Game/Core/SqliteProgressionRepository.cs`
  - SQLite 테이블 생성, 로드/저장
- `Game/Core/RunSaveData.cs`
  - 런 세이브 직렬화 모델
- `Game/Core/GameSettings.cs`
  - FOV, 감도, 볼륨, 창 크기 저장 모델

## 핵심 규칙

1. **기본값 + 영구 스탯 + 런 카드 보너스의 합산 지점은 `ApplyCombinedProgressionStats()` 하나**
- 체력
- 이동 속도
- 대시 쿨다운
- 무기 데미지
를 다른 곳에서 따로 덮어쓰지 말 것.

2. **스탯 카드는 고정 길이 배열 기반**
- `RunStatCount = 6`
- `runStatGrade`
- `runStatBonusTotals`
- `runStatPickupCount`
- `RewardStatTypes`

3. **무기 해금 상태도 저장 데이터의 일부**
- `ownedWeapons[5]`
- `weaponCardPoolCount`
- `CurrentWeaponType`
- `WeaponAmmoState`
- `WeaponUpgradeState`

4. **영구 스탯과 런 세이브는 SQLite 테이블 구조까지 함께 봐야 한다**
- `permanent_progression`
- `run_save`
- `run_records`
- `game_settings`

5. **메뉴로 나갈 때 저장되는 흐름을 깨뜨리지 말 것**
- `Form1.GoToMenu()`
- 생존 중이면 `SaveRunProgress()`
- 사망했으면 `RecordRunResult()` + `DeleteRunProgress()`

## 새 스탯 카드 추가 절차

1. `Game/Core/CardSystem.cs`
   - `StatType`에 추가
2. `Game/Core/GameLogic.CardReward.cs`
   - `RunStatCount`
   - `RewardStatTypes`
   - 카드 생성 후보 풀
   - 캡/보너스 계산
   - 문자열/UI 표시
3. `Game/Core/GameLogic.PersistentStats.cs`
   - `ApplyCombinedProgressionStats()`
   - 저장/복원 배열 직렬화
4. `Game/Core/GameLogic.PlayerStatsOverlay.cs`
   - 오버레이 노출
5. 필요 시 `SqliteProgressionRepository`와 `RunSaveData` 저장 필드 확장

## 새 영구 스탯 추가 절차

1. `PermanentProgressionData.cs`
   - 필드
   - `Sanitize()`
   - 보너스 계산
   - 포인트 소모 메서드
2. `GameLogic.PersistentStats.cs`
   - UI 카드
   - 배분 입력
   - `ApplyCombinedProgressionStats()`
3. `SqliteProgressionRepository.cs`
   - 테이블 컬럼
   - 로드/저장 쿼리

## 런 세이브 필드 추가 절차

1. `RunSaveData.cs` 필드 추가
2. `GameLogic.PersistentStats.cs`
   - `SaveRunProgress()`
   - `ContinueRun()`
3. `SqliteProgressionRepository.cs`
   - `run_save` 테이블 생성 SQL
   - `EnsureColumnExists(...)`
   - `LoadRunSave()`
   - `SaveRunSave()`

## 현재 시스템에서 중요한 고정점

- 스탯 카드 등급 보너스는 `White +1%` ~ `Red +5%`
- 무기 카드는 런 중 무기 해금과 업그레이드를 동시에 담당한다
- 보스 처치 시
  - `weaponCardPoolCount++`
  - 영구 포인트 지급
- `ContinueRun()`은 저장된 층의 **직전 층 번호**를 세팅한 뒤
  `TransitionToNextFloor()`로 실제 맵을 다시 만든다

## 체크리스트

- [ ] 새 스탯/영구 스탯이 `ApplyCombinedProgressionStats()`까지 반영됐다
- [ ] 고정 길이 배열과 enum 인덱스가 모두 맞는다
- [ ] 런 세이브 로드/저장 SQL과 모델이 함께 갱신됐다
- [ ] 오버레이/UI 문자열도 같이 수정됐다
- [ ] 메뉴 복귀, 이어하기, 사망 후 삭제 흐름이 유지된다
