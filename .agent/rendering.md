# rendering

렌더링 파이프라인과 에셋 연결을 수정할 때 보는 가이드.

## 핵심 파일

- `Game/Rendering/RaycastRenderer.cs`
  - 월드 렌더 + HUD + 오버레이의 최종 조립 지점
- `Game/Rendering/RaycastRenderer.World.cs`
  - GPU 월드 렌더 호출과 월드 오버레이 큐 처리
- `Game/Rendering/RaycastRenderer.Hud.cs`
  - 체력/스태미나/탄약/미니맵/피격/무기 오버레이
- `Game/Rendering/RaycastRenderer.Sprites.cs`
  - 적, 투사체, 픽업의 화면 투영과 월드 라벨
- `Game/Rendering/TextureManager*.cs`
  - 이미지 로드, 경로 탐색, 절차적 폴백, 스프라이트 시트 처리
- `Game/Rendering/WorldData/WorldRenderDataBuilder.cs`
  - GPU에 넘길 `RenderWorldCommand`를 생성
- `Engine/Rendering/Renderer.cs`
  - 게임 코드가 호출하는 공용 드로우 퍼사드
- `Engine/Hosting/GameHost.cs`
  - D3D11 백엔드 생성/복구/리사이즈 관리

## 파이프라인 요약

`Form1.RenderFrame()`
→ `GameLogic.Render()`
→ `RaycastRenderer.Render()`
→ `TryRenderWorld()`
→ `WorldRenderDataBuilder.TryBuild()`
→ `Renderer.TryDrawWorld()`
→ D3D11 presenter
→ HUD / 선택 오버레이 합성

## 수정 원칙

1. **HUD 변경은 `RaycastRenderer.Hud.cs`**
- 체력바, 탄약, 크로스헤어, 상호작용 안내를 건드릴 때는 여기부터 본다.

2. **월드 공간 오브젝트 변경은 `Sprites.cs` 또는 `WorldRenderDataBuilder.cs`**
- 화면 고정 UI인지
- 월드 깊이 영향을 받는 오브젝트인지
를 먼저 구분한다.

3. **새 렌더 데이터 필드를 추가하면 엔드투엔드로 연결**
- `RenderCommands.cs`
- `WorldRenderDataBuilder.cs`
- D3D11 presenter
- 필요 시 `RaycastRenderer`

4. **에셋 경로 규칙은 `TextureManager`가 결정**
- 이미지 추가만 하고 로더를 안 건드리면 런타임에서 못 찾을 수 있다.
- 비표준 시트는 `SpriteSheetCatalog.cs`도 같이 수정한다.

5. **층 전환 직후 캐시를 비워야 하는 변경인지 확인**
- 스프라이트 아틀라스/임시 버퍼가 커지는 수정이면
  `renderer.ResetTransientCaches()`
  `cachedRenderer?.OnLevelTransition()`
  흐름을 유지해야 한다.

## 자주 같이 수정해야 하는 곳

### 새 무기/픽업/적 스프라이트 추가
- `Game/Rendering/TextureManager.Loading.cs`
- `Game/Rendering/TextureManager.cs`
- `Game/Rendering/SpriteSheetCatalog.cs`
- `Game/Rendering/RaycastRenderer.Hud.cs` 또는 `Sprites.cs`

### 월드 렌더 표현 변경
- `Game/Rendering/WorldData/WorldRenderDataBuilder.cs`
- `Engine/Rendering/Abstractions/RenderCommands.cs`
- `Engine/Rendering/Backends/D3D11/World/*`

### 화면 해상도 / 합성 / 흔들림
- `Form1.cs`
  - `GameRenderWidth = 640`
  - `GameRenderHeight = 360`
  - `DrawScaledGameWorld()`
- `Game/Rendering/RaycastRenderer.cs`
  - `Render()`
  - death rotation / scale / shake

### 월드 위 체력바 / 텔레그래프 / 라벨
- `Game/Rendering/RaycastRenderer.Sprites.cs`
- `Game/Rendering/RaycastRenderer.World.cs`
  - `QueueWorldOverlayRect()`
  - `FlushWorldOverlayQueue()`

## 새 렌더 기능 추가 절차

1. CPU 쪽에서 어떤 상태가 필요한지 결정한다.
2. HUD인지 월드 오브젝트인지 먼저 분류한다.
3. 월드 오브젝트면 `WorldRenderDataBuilder`와 `RenderWorldCommand`를 먼저 설계한다.
4. 에셋이 필요하면 `TextureManager` 경로/폴백까지 연결한다.
5. `Form1`의 내부 렌더 해상도 스케일을 깨지 않는지 확인한다.

## 체크리스트

- [ ] 수정한 요소가 HUD인지 월드인지 분리돼 있다
- [ ] 새 이미지/시트가 `TextureManager`에서 실제로 로드된다
- [ ] GPU 경로에 필요한 데이터가 `RenderCommands`까지 연결됐다
- [ ] 층 전환 시 캐시/아틀라스 잔재가 남지 않는다
- [ ] 640x360 내부 좌표계와 창 업스케일이 모두 맞는다
