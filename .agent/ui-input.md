# ui-input

WinForms 상태 전환, 메뉴 UI, 입력 라우팅을 수정할 때 보는 가이드.

## 핵심 파일

- `Form1.cs`
  - 메인 루프, 렌더 호출, 창 수명주기
- `Form1.Input.cs`
  - 키/마우스 이벤트 처리와 게임 상태별 입력 분기
- `Form1.UI.cs`
  - 메뉴/설정/일시정지/기록 화면 레이아웃과 클릭 처리
- `Engine/Input/Input.cs`
  - 프레임 폴링용 키 상태 저장소
- `Game/Core/GameSettings.cs`
  - 설정 저장 모델

## 현재 상태 머신

- `Menu`
- `ModeSelect`
- `Settings`
- `Playing`
- `Pause`
- `Records`

새 상태를 추가하면 이 enum만 바꾸면 끝이 아니다.

## 수정 원칙

1. **새 UI 상태를 추가하면 최소 5곳을 같이 본다**
- `Form1.GameState`
- `GameLoop()`
- `RenderFrame()`
- 클릭 핸들러 (`HandleMenuClick`, `HandlePauseClick` 등)
- `Escape` 처리 (`OnGameKeyDown`)

2. **마우스 캡처 규칙을 같이 유지**
- 플레이 중 기본은 캡처
- `world.MouseSelectableOverlayActive`면 해제
- 메뉴/설정/일시정지는 해제

3. **입력 저장은 `Input` 정적 저장소, 실제 해석은 상태별 핸들러**
- WinForms 이벤트 안에서 게임 로직을 너무 많이 처리하지 않는다.

4. **설정 변경은 즉시 반영 + 즉시 저장**
- `SetFovFromMouse()`
- `SetSensitivityFromMouse()`
- `SetBgmVolumeFromMouse()`
- `SetSfxVolumeFromMouse()`
- 전부 `PersistSettings()`까지 간다.

5. **월드 선택형 오버레이는 640x360 내부 좌표계로 변환**
- `NotifyWorldUiMouseClick()`가 클라이언트 좌표를 게임 좌표로 바꾼다.

## 자주 같이 수정해야 하는 곳

### 새 메뉴 버튼 추가
- `Form1.cs`
  - 버튼 사각형 필드
- `Form1.UI.cs`
  - 레이아웃 계산
  - 그리기
  - 클릭 처리

### 새 설정 항목 추가
- `Game/Core/GameSettings.cs`
- `Form1.UI.cs`
  - 슬라이더/버튼 UI
  - `PersistSettings()`
- `Game/Core/SqliteProgressionRepository.cs`
  - `game_settings` 테이블 로드/저장
- 실제 적용 대상 (`GameLogic` 또는 오디오/렌더 시스템)

### 새 인게임 오버레이 추가
- `GameLogic`에 상태/property 추가
- `Form1.Input.cs`
  - `world.MouseSelectableOverlayActive`와 연동 필요 여부 확인
- `GameLogic.Render()` 또는 메뉴 렌더 경로에 draw 호출 추가

## 현재 렌더 구조

- 내부 게임 렌더 해상도는 `640 x 360`
- `DrawScaledGameWorld()`가 창 크기로 업스케일
- 메뉴/설정/기록은 창 좌표계 기준
- 월드 선택 UI 클릭은 다시 640x360 좌표로 역변환

## 수정 절차

1. UI가 메뉴 좌표계인지 월드 좌표계인지 먼저 정한다.
2. 상태 전환 조건과 `Escape` 동작을 정의한다.
3. 마우스 캡처 해제/복귀 조건을 같이 넣는다.
4. 설정 값이면 `PersistSettings()`와 DB 저장까지 확인한다.
5. 창 크기 변경 후 `EnsureUiLayout()` 재계산이 맞는지 본다.

## 체크리스트

- [ ] 새 UI 상태가 입력, 렌더, Escape 처리에 모두 반영됐다
- [ ] 마우스 캡처/해제 흐름이 깨지지 않는다
- [ ] 월드 오버레이 클릭 좌표 변환이 맞다
- [ ] 설정 변경이 즉시 반영되고 저장도 된다
- [ ] 메뉴 레이아웃 캐시와 창 크기 변경 대응이 유지된다
