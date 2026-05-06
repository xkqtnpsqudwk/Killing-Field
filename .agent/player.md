# player

플레이어 시스템을 수정할 때 보는 작업 가이드.

## 핵심 책임

- `Game/Entities/Player.cs`
  - 플레이어의 런타임 상태를 소유한다.
  - 위치, 방향, FOV, 체력, 스태미나, 대시, 스팀팩, 코인을 관리한다.
- `Game/Core/GameLogic.Update.cs`
  - 실제 입력을 읽고 이동/대시/스팀 사용을 처리한다.
- `Game/Systems/CollisionSystem.cs`
  - 실제 위치 갱신은 여기서만 수행한다.
- `Game/Core/GameLogic.PersistentStats.cs`
  - 영구 스탯 + 런 카드 보너스를 플레이어/무기에 합산 적용한다.
- `Game/Core/GameLogic.PlayerStatsOverlay.cs`
  - `P` 오버레이에 노출되는 플레이어 스탯 표시를 담당한다.
- `Game/Config/GameConfig.cs`
  - 플레이어 관련 기본 수치의 진실의 원천이다.

## 수정 원칙

1. **입력 해석은 `GameLogic`, 상태 보관은 `Player`**
- `Player`에 키 입력 분기를 넣지 말고 `GameLogic.Update.cs`에서 처리한다.

2. **실제 이동은 반드시 `collision.TryMovePlayer()` 경유**
- `player.Position`을 직접 누적 이동시키면 벽 관통, 계단 높이, 코너 슬라이딩이 깨진다.

3. **시야 변경은 `Rotate()` 또는 `FovDegrees` 프로퍼티로만**
- `Direction`만 바꾸면 안 된다.
- `Plane`은 `Direction`과 항상 동기화되어야 한다.

4. **대시/스태미나 수치는 `GameConfig`에서만 조정**
- `Player.TrySpendDash()`, `GetEffectiveSpeed()`, `UpdateStamina()`는 상수를 읽기만 한다.

5. **오버레이가 열리면 발사/루프 사운드/상호작용 상태도 함께 정리**
- `OnPlayerStatsOverlayOpened()`
- `OnPermanentStatsUiOpened()`
- `ResetStageState()`

## 자주 같이 수정해야 하는 곳

### 이동 속도 / 질주 / 탈진
- `Game/Config/GameConfig.cs`
  - `MoveSpeed`
  - `SprintMultiplier`
  - `ExhaustedSpeedMultiplier`
- `Game/Entities/Player.cs`
  - `GetEffectiveSpeed()`
- `Game/Core/GameLogic.Update.cs`
  - `HandlePlayerMovement()`

### 대시
- `Game/Config/GameConfig.cs`
  - `DashDistance`
  - `DashDuration`
  - `DashCooldownDuration`
  - `DashStaminaCost`
- `Game/Entities/Player.cs`
  - `CanDash()`
  - `TrySpendDash()`
  - `StartDash()`
- `Game/Core/GameLogic.Update.cs`
  - `TryPerformDash()`

### 체력 / 최대 체력 / 영구 스탯 반영
- `Game/Config/GameConfig.cs`
  - `PlayerHealthMax`
  - `PermanentHealthPerPoint`
- `Game/Core/GameLogic.PersistentStats.cs`
  - `ApplyCombinedProgressionStats()`
- `Game/Core/GameLogic.PlayerStatsOverlay.cs`
  - 체력 breakdown 문구

### FOV / 마우스 감도
- `Game/Entities/Player.cs`
  - `FovDegrees`
  - `UpdatePlaneFromFov()`
- `Game/Core/GameLogic.cs`
  - `GetFovDegrees()`, `SetFovDegrees()`
  - `GetMouseSensitivity()`, `SetMouseSensitivity()`
- `Form1.UI.cs`
  - 설정 화면 슬라이더 표시

### 스팀팩 / 코인
- `Game/Entities/Player.cs`
  - `AddStimPack()`, `UseStimPack()`
  - `AddCoins()`, `TrySpendCoins()`
- `Game/Core/GameLogic.Update.cs`
  - `HandleStimInput()`
- `Game/Core/GameLogic.StageFlow.Progression.cs`
  - 코인 드랍, 휴식 상점 구매 로직

## 수정 절차

1. 플레이어 수치를 바꿀 일이면 먼저 `GameConfig.cs`를 수정한다.
2. 입력 방식이 바뀌면 `GameLogic.Update.cs`와 `Form1.Input.cs`를 같이 본다.
3. 이동 방식이 바뀌면 `CollisionSystem.cs`와 `Player.FloorZ` 갱신 경로까지 확인한다.
4. 카드/영구 스탯이 영향을 주는 값이면 `ApplyCombinedProgressionStats()`를 같이 수정한다.
5. 화면에 보이는 값이 바뀌면 `PlayerStatsOverlay`와 HUD 문구도 맞춘다.

## 체크리스트

- [ ] 위치 변경이 `collision.TryMovePlayer()` 경유로만 일어난다
- [ ] `Direction` 변경 시 `Plane` 동기화가 유지된다
- [ ] 수치 상수는 `GameConfig`에만 있다
- [ ] 카드/영구 스탯 보너스가 `ApplyCombinedProgressionStats()`에 반영된다
- [ ] 오버레이/사망/층 전환 시 플레이어 상태 초기화가 맞게 동작한다
