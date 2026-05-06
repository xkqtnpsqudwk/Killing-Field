# physics

충돌, 문, 타일 고체 판정, 바닥 높이를 수정할 때 보는 가이드.

## 핵심 파일

- `Game/Systems/CollisionSystem.cs`
  - 플레이어/적 이동과 시야(Line of Sight) 판정의 중심
- `Game/Map/MapManager.cs`
  - 맵 타일, 텍스처, 바닥/천장 높이 보관
- `Game/Map/MapManager.Doors.cs`
  - 문 열림 진행도와 타일 전환
- `Game/Core/GameLogic.Update.cs`
  - 플레이어 이동과 대시 입력 연결
- `Game/Config/GameConfig.cs`
  - 충돌/문 관련 상수

## 핵심 규칙

1. **맵이 바뀌면 `CollisionSystem`도 새로 만들어야 한다**
- `CollisionSystem`은 `map`과 `floorHeights` 참조를 생성 시점에 잡는다.
- 맵/바닥 높이를 갈아끼운 뒤 기존 인스턴스를 재사용하면 판정이 틀어진다.

2. **큰 이동량은 금지, 서브스텝 유지**
- `CollisionStepSize = 0.025f`
- `TryMovePlayer()` / `TryMoveEnemy()`는 큰 이동을 여러 단계로 나눠 처리한다.

3. **문은 열리기 전까지 고체 타일**
- 문 타일 타입은 `GameConfig.DoorTileType = 9`
- 완전히 열리기 전에는 `CollisionSystem.IsSolidType()`에서 고체로 본다.

4. **플레이어 높이 정보는 이동 후 `FloorZ` 갱신이 필수**
- 계단/단차를 다루는 수정이면 `GetFloorHeightAt()`와 `CanStep()`까지 같이 본다.

5. **새 고체 타일 타입을 만들면 충돌과 렌더를 같이 고쳐야 한다**
- `CollisionSystem.IsSolidType()`
- `MapManager.BuildDefaultTextureIds()`
- 월드 렌더 텍스처 선택 경로

## 자주 같이 수정해야 하는 곳

### 벽 충돌 / 미끄러짐 / 플레이어 반지름
- `Game/Systems/CollisionSystem.cs`
  - `IsWallRadius()`
  - `TryMovePlayer()`
- `Game/Config/GameConfig.cs`
  - `PlayerRadius`
  - `CollisionStepSize`

### 적-플레이어 간 최소 거리
- `Game/Systems/CollisionSystem.cs`
  - `IsBlockedForEnemy()`

### 시야 판정
- `Game/Systems/CollisionSystem.cs`
  - `HasLineOfSight()`
- 폭발, AI 인지, 원거리 공격 로직도 함께 확인

### 문 동작
- `Game/Map/MapManager.Doors.cs`
  - `BeginDoorOpening()`
  - `UpdateDoorAnimations()`
  - `OpenDoor()`
  - `CloseDoor()`
- `Game/Core/GameLogic.StageFlow.cs`
  - 상호작용 거리와 문 열기 입력 처리

### 바닥 높이 / 단차
- `Game/Map/MapManager.cs`
  - `floorHeights`
  - `ceilHeights`
- `Game/Systems/CollisionSystem.cs`
  - `CanStep()`
  - `GetFloorHeightAt()`
- `Game/Config/GameConfig.cs`
  - `MaxStepHeight`

## 수정 절차

1. 타일 의미를 먼저 정한다.
2. 그 타일이 고체인지 아닌지를 `IsSolidType()`에 반영한다.
3. 문/단차/특수 타일이면 `MapManager`의 데이터 보관 방식까지 같이 바꾼다.
4. 맵 생성 후 `CollisionSystem` 재생성 경로가 유지되는지 확인한다.
5. 빠른 이동(대시, 적 돌진)에서 벽 관통이 생기지 않는지 본다.

## 주의사항

- `CollisionStepSize`를 키우면 제일 먼저 벽 관통이 난다.
- 문은 진행도만 올린다고 열린 것이 아니다.
  - `progress >= 1f`가 되어 `OpenDoor()`가 타일을 `0`으로 바꿔야 실제 통과 가능하다.
- `MapManager.SetTile()`은 문이 아닌 타일로 바뀌면 `doorProgress`를 지운다.

## 체크리스트

- [ ] 맵 변경 뒤 `CollisionSystem`이 재생성된다
- [ ] 새 타일 타입의 고체 여부가 `IsSolidType()`에 반영됐다
- [ ] 문/단차 변경이 `MapManager`와 `GameLogic` 둘 다에 반영됐다
- [ ] 플레이어/적 빠른 이동에서도 벽 관통이 없다
- [ ] `FloorZ`와 line-of-sight 판정이 여전히 맞다
