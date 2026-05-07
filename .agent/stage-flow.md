# stage-flow

층 전환, 방 활성화, 분기, 휴식 룸, 보스 흐름을 수정할 때 보는 가이드.

## 핵심 파일

- `Game/Core/GameLogic.cs`
  - `TransitionToNextFloor()`
- `Game/Core/GameLogic.StageFlow.cs`
  - 문 상호작용, 층 출구, 상태 메시지
- `Game/Core/GameLogic.StageFlow.Progression.cs`
  - 방 활성화, 클리어, 보상/카드 상점
- `Game/Core/GameLogic.Branch.cs`
  - 일반 전투 층 클리어 후 분기 선택 UI
- `Game/Map/RoomTemplate.cs`
  - 층별 템플릿 선택과 동적 적 배치
- `Game/Map/MapManager.RoomTemplate.cs`
  - 선택된 템플릿을 실제 201x201 맵으로 로드
- `Game/Map/MapManager.Generation.Decor.cs`
  - 방 구조물/통로 보정

## 현재 흐름 요약

1. `TransitionToNextFloor()`가 층 번호를 올린다.
2. `RoomTemplateLibrary.SelectForFloor()`가 보스/휴식/정예/일반 방 템플릿을 고른다.
3. `MapManager.LoadRoomFromTemplate()`가
   - 시작실 `Id = 0`
   - 메인 방 `Id = 1`
   - 입구 문
   - 출구 문(`TargetRoomId = -1`)
   를 포함한 맵을 만든다.
4. 플레이어가 방 안으로 들어오면 `UpdateStageProgression()`이 방을 활성화한다.
5. 전투 방이면 적 스폰, 휴식 방이면 Luck 기반 고등급 카드 상점 선택지 3개 생성.
   - 열쇠 방의 목표 적은 일반 적과 같은 크기/체력바 색으로 시작하고, 충분히 피해를 입거나 주변 적이 모두 정리된 뒤에만 표적으로 드러난다.
6. 방 클리어 후
   - 일반 방: 카드 선택 → 분기 선택
   - 다음 층이 보스면: 카드 선택 후 `E`로 직행
   - 보스 방: 카드/영구 포인트 후 `E`로 다음 층
   - 위험도 `높음` 이상 전투 방: 카드 등급 +1 보정
   - 위험도 `극한` 전투 방: 현재 Luck으로 가능한 최고 등급보다 한 단계 높은 등급을 최소 보장

## 수정 원칙

1. **방 타입 추가는 템플릿, 활성화, 안내문, 보상 처리까지 한 묶음**
- `RoomTemplate`
- `MapManager.LoadRoomFromTemplate()`
- `ActivateStageRoom()`
- `UpdateInteractPrompt()`
- `UpdateStageProgression()`

2. **문 연결은 `StageDoorConnection`이 기준**
- 일반 연결 문은 `TargetRoomId = room id`
- 층 이동 출구 문은 `TargetRoomId = -1`

3. **방 상태는 `StageRoomBlueprint`와 `StageRoomState`를 분리해 유지**
- 정적 설계 데이터는 `Blueprint`
- 런타임 진행 상태는 `State`

4. **휴식 방은 적 스폰이 아니라 카드 상점 픽업 선택지 생성**
- `SpawnRestRoomChoices()`
- `CompleteRestRoomChoice()`
- `TrySkipActiveRestRoom()`

5. **층 전환 시 캐시와 상태를 전부 초기화**
- `RebuildStageRoomLookup()`
- `ResetStageState()`
- `renderer.ResetTransientCaches()`
- `cachedRenderer?.OnLevelTransition()`

## 자주 같이 수정해야 하는 곳

### 층/방 선택 규칙
- `Game/Map/RoomTemplate.cs`
  - `SelectForFloor()`
  - `GenerateRandomSpawns()`
  - 휴식/보스 템플릿 빌더

### 방 구조물과 이동 가능 경로
- `Game/Map/MapManager.Generation.Decor.cs`
  - `ApplyRoomLayouts()`
  - `BuildGeneralRoomStructures()`
  - `EnsureRoomTraversal()`

### 방 진입 시 전투 시작 조건
- `Game/Core/GameLogic.StageFlow.Progression.cs`
  - `IsInsideRoundStartTrigger()`
  - `ActivateStageRoom()`

### 클리어 후 보상/분기
- `Game/Core/GameLogic.StageFlow.Progression.cs`
  - `SpawnRewardPickupForRoom()`
  - `RoomTemplateLibrary.GetCardRewardGradeBoost()`
  - `RoomTemplateLibrary.IsExtremeRewardRoom()`
- `Game/Core/GameLogic.CardReward.cs`
  - `ConfirmCardSelection()`
- `Game/Core/GameLogic.Branch.cs`
  - `ShowBranchSelection()`

### 층 난이도 성장
- `Game/Core/GameLogic.cs`
  - `CommitCurrentFloorEnemyGrowthIfEligible()`
  - `RegisterBossClearEnemyGrowth()`
  - `ApplyEnemyDifficultyScaling()`
- `Game/Config/GameConfig.cs`
  - 층/보스 성장 배율

## 수정 절차

1. 새 룸 타입 또는 새 층 규칙을 `RoomTemplateLibrary`에서 정의한다.
2. 실제 맵 배치를 `MapManager.LoadRoomFromTemplate()`에 반영한다.
3. 진입/클리어/보상/출구 처리를 `StageFlow*.cs`에서 맞춘다.
4. 분기 UI나 안내 문구가 바뀌면 `Branch.cs`, `UpdateInteractPrompt()`도 수정한다.
5. 휴식/보스/정예별 메시지와 BGM 카테고리까지 검토한다.

## 체크리스트

- [ ] 새 방 타입이 템플릿 선택, 맵 로드, 활성화 로직에 모두 반영됐다
- [ ] 문 연결과 `TargetRoomId` 규칙이 일관된다
- [ ] 일반/보스/휴식 방 클리어 후 후속 흐름이 맞다
- [ ] 층 전환 시 방 상태, 픽업, 캐시가 깨끗하게 초기화된다
- [ ] 분기 UI와 상호작용 안내문이 실제 흐름과 맞다
