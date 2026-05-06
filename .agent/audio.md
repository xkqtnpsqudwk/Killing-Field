# audio

효과음, 배경음, 루프 사운드를 수정할 때 보는 가이드.

## 핵심 파일

- `Game/Audio/GameAudioService.cs`
  - BGM/SFX 채널 수명 관리 파사드
- `Game/Audio/EffectSoundManager.cs`
  - 효과음 재생, alias 등록, 루프 재생
- `Game/Audio/BackgroundSoundManager.cs`
  - 카테고리별 BGM 재생과 크로스페이드
- `Game/Audio/AudioChannels.cs`
  - 게임 코드가 의존하는 인터페이스
- `Game/Audio/SoundAudioCommon.cs`
  - 사운드 파일 경로 탐색 공용 유틸
- `Game/Config/GameConfig.cs`
  - 사운드 alias와 상대 경로 정의
- `Game/Core/GameLogic.cs`
  - 실제 재생/정지 호출 지점
- `Game/Core/GameLogic.StageFlow.Context.cs`
  - BGM 카테고리 전환

## 핵심 규칙

1. **새 효과음은 `GameConfig` alias + path부터 추가**
- 코드에서 문자열 리터럴을 직접 쓰지 않는다.

2. **효과음은 `EffectSoundManager.LoadAllSounds()`에서 preload**
- alias만 추가하고 preload를 안 하면 런타임에서 재생되지 않는다.

3. **배경음은 카테고리 단위로만 전환**
- `BackgroundMusicCategory.Normal`
- `BackgroundMusicCategory.MiniBoss`
- `BackgroundMusicCategory.Boss`

4. **루프 사운드는 시작 조건과 종료 조건을 둘 다 잡아야 한다**
- 무기 해제
- 오버레이 오픈
- 사망
- 층 전환
- 메뉴 복귀
시 정지 경로가 빠지면 소리가 남는다.

5. **게임 로직에서는 채널 직접 접근보다 헬퍼 사용**
- `PlayEffectSound()`
- `StopEffectSound()`
- `PlayWeaponEffectSound()`
- `StopWeaponEffectSound()`

## 현재 구조 요약

- BGM:
  - `BackgroundSoundManager`
  - 2개 슬롯을 교차 사용해 크로스페이드
- SFX:
  - `EffectSoundManager`
  - 풀 크기 8 슬롯
  - alias 기반 재생

## 자주 같이 수정해야 하는 곳

### 새 무기 발사음
- `Game/Config/GameConfig.cs`
  - alias + path
- `Game/Audio/EffectSoundManager.cs`
  - `LoadAllSounds()`
- `Game/Entities/Weapon.cs`
  - `GetFireSoundAlias()`
- `Game/Core/GameLogic.Combat.cs`
  - 실제 발사 시점 재생

### 문 / 상호작용 / 환경음
- `Game/Config/GameConfig.cs`
- `Game/Audio/EffectSoundManager.cs`
- `Game/Core/GameLogic.StageFlow.cs`

### 보스/정예 BGM 전환
- `Game/Core/GameLogic.StageFlow.Context.cs`
  - `GetBackgroundMusicCategory()`
- `Game/Audio/BackgroundSoundManager.cs`
  - 플레이리스트 등록
- `Game/Config/GameConfig.cs`
  - 트랙 목록

### 적 전용 사운드
- `Game/Systems/EnemyManager.cs`
  - `PlayEnemySound()`
  - `ResolveEnemySoundAlias()`
- 필요 시 `EnemyAssetProfiles`의 cue/profile까지 확인

## 수정 절차

1. `GameConfig`에 alias와 경로를 추가한다.
2. 해당 매니저의 `LoadAllSounds()`에 preload를 추가한다.
3. 실제 재생 지점을 `GameLogic` 또는 `EnemyManager`에 연결한다.
4. 루프 사운드면 시작/정지 조건을 함께 넣는다.
5. 볼륨 슬라이더나 설정 저장이 필요한지 확인한다.

## 체크리스트

- [ ] 새 사운드가 `GameConfig`와 preload 양쪽에 등록됐다
- [ ] 실제 재생 지점이 alias를 사용한다
- [ ] 루프 사운드의 정지 조건이 모두 있다
- [ ] 보스/정예/일반 BGM 카테고리가 올바르게 전환된다
- [ ] 메뉴 복귀, 사망, 층 전환 후 사운드가 남지 않는다
