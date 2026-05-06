# game-logic

`GameLogic` partial 구조를 건드릴 때 보는 상위 가이드.

## 핵심 역할

`GameLogic`은 게임 전체 상태의 최상위 코디네이터다.

- 플레이어/무기/적/맵/렌더러/오디오를 한 흐름으로 연결한다
- 실제 세부 계산은 하위 시스템에 위임한다
- 큰 클래스이므로 책임별 partial 파일로 나뉘어 있다

## partial 파일 책임 분리

- `GameLogic.cs`
  - 생성자, 공통 상태, 층 전환, 렌더/오디오 헬퍼
- `GameLogic.Update.cs`
  - 프레임 업데이트 순서, 이동, 상호작용, 자동 발사
- `GameLogic.Combat.cs`
  - 발사 확정, 피격 판정, 폭발
- `GameLogic.Projectiles.cs`
  - 플레이어 로켓과 폭발 이펙트
- `GameLogic.Special.cs`
  - Red 특수기
- `GameLogic.StageFlow*.cs`
  - 방 진행, 문, 보상, 층 출구, BGM 문맥
- `GameLogic.CardReward.cs`
  - 카드 생성/선택/적용
- `GameLogic.Branch.cs`
  - 다음 룸 분기 선택
- `GameLogic.PersistentStats.cs`
  - 영구 스탯, 저장/이어하기, 기록
- `GameLogic.PlayerStatsOverlay.cs`
  - `P` 스탯 오버레이
- `GameLogic.EndlessMode.cs`
  - 666층 엔딩과 무한 모드

## 프레임 순서

1. 타이머/오버레이 상태 갱신
2. 문/무기/대시/스팀/픽업 타이머 갱신
3. 적 AI 업데이트
4. 플레이어 투사체 업데이트
5. UI 상태면 조기 반환
6. 입력 처리
7. 발사 확정 / 특수기 갱신
8. 픽업 수집 / 스테이지 진행 / BGM / 안내문 갱신

새 기능이 어느 단계에 들어가야 하는지 먼저 정해야 한다.

## 수정 원칙

1. **책임에 맞는 partial에 넣는다**
- 무조건 `GameLogic.cs`에 추가하지 않는다.

2. **입력 차단 조건을 일관되게 유지**
- `player.IsDead`
- `victory`
- `permanentStatsUiActive`
- `playerStatsOverlayActive`
- `cardRewardActive`
- `branchSelectionActive`
- `endingSequenceActive`

3. **새 상태를 추가하면 reset 경로를 반드시 만든다**
- 런 전체 초기화: `ResetPlayerState()`, `ResetCardRunState()`, `ResetRunEndlessState()`
- 층 단위 초기화: `ResetStageState()`, `weapon.ResetFloorSpecial()`

4. **맵 교체 시 하위 시스템 재구성이 필요하다**
- `ApplyMapSpawnSettings()`
- `collision = new CollisionSystem(...)`
- `enemyManager.BuildFromMap(...)`
- `RebuildStageRoomLookup()`
- `renderer.ResetTransientCaches()`
- `cachedRenderer?.OnLevelTransition()`

5. **렌더는 상태 전달만 하고, 계산은 렌더러로 넘긴다**
- `GameLogic.Render()`는 값을 모아 넘기는 지점으로 유지한다.

## 새 기능 추가 시 먼저 확인할 질문

- 이 기능은 프레임 순서상 언제 업데이트돼야 하나?
- UI가 열렸을 때 입력을 막아야 하나?
- 층 전환 시 리셋돼야 하나, 런 전체에서 유지돼야 하나?
- 저장/이어하기 대상인가?
- HUD/오버레이/사운드도 같이 바뀌어야 하나?

## 체크리스트

- [ ] 기능 책임에 맞는 partial 파일에 배치됐다
- [ ] 입력 차단 조건이 기존 규칙과 충돌하지 않는다
- [ ] 런 초기화/층 초기화 경로에 새 상태가 포함됐다
- [ ] 맵 전환이 필요한 기능이면 하위 시스템 재구성이 반영됐다
- [ ] 렌더/오디오/저장까지 필요한 연결이 빠지지 않았다
