# My2DEngine — CLAUDE.md

AI 어시스턴트를 위한 전체 코드베이스 기술 참조 문서.

---

## 1. 프로젝트 개요

- **솔루션:** `My2DEngine.sln`
- **언어/프레임워크:** C# (.NET 8.0 Windows)
- **렌더링:** DirectX 11 (D3D11)
- **오디오:** NAudio + NAudio.Vorbis (Ogg)
- **입력:** Windows Forms 키보드/마우스
- **게임 장르:** Doom 스타일 레이캐스팅 1인칭 로그라이크

---

## 2. 프로젝트 구조

```
/Engine          - 엔진 코어 라이브러리 (렌더링, 입력, 수학, 호스팅)
/Game            - 게임 로직 및 시스템
  /Core          - GameLogic 코디네이터, 게임 루프
  /Systems       - 전투·충돌 시스템
  /Map           - 맵/레벨 생성·스테이지 흐름
  /Rendering     - 레이캐스트 렌더러·UI
  /Audio         - 사운드·음악 관리
  /Config        - 밸런스·설정 상수 (GameConfig)
  /Entities      - 플레이어, 적, 무기, 보상 등 엔티티
/               - 앱 진입점 (Form1.cs, Program.cs, My2DEngine.csproj)
/My2DEngine.SmokeTests - 스모크 테스트
/packages        - NuGet 의존성
```

**주요 네임스페이스**

| 네임스페이스 | 역할 |
|---|---|
| `My2DEngine.Engine.*` | 엔진 코어 |
| `My2DEngine.Game.Core` | GameLogic 코디네이터 |
| `My2DEngine.Game.Systems` | 전투·충돌 |
| `My2DEngine.Game.Map` | 맵·스테이지 |
| `My2DEngine.Game.Rendering` | 레이캐스트·HUD |
| `My2DEngine.Game.Audio` | BGM·SFX |
| `My2DEngine.Game.Config` | 밸런스 상수 |
| `My2DEngine.Game.Entities` | 엔티티 |

## 2.1 에이전트 작업 문서 인덱스

실제 코드 수정 전, 작업 영역에 맞는 아래 문서를 먼저 확인한다.

| 문서 | 용도 |
|---|---|
| [`add-enemy.md`](add-enemy.md) | 새 적 아키타입 추가 절차, `EnemyCatalog`/`EnemyManager`/스폰 연결 체크 |
| [`add-weapon.md`](add-weapon.md) | 새 무기 추가 절차, `WeaponType`/탄약 배열/HUD/사운드 연결 체크 |
| [`balance-tune.md`](balance-tune.md) | 밸런스 상수 조정 시 건드릴 위치와 위험 포인트 확인 |
| [`player.md`](player.md) | 플레이어 이동, 대시, FOV, 스태미나, 영구 스탯 반영 수정 가이드 |
| [`physics.md`](physics.md) | 충돌, 문, 고체 타일, 바닥 높이, line-of-sight 수정 가이드 |
| [`rendering.md`](rendering.md) | HUD, GPU 월드 렌더, 스프라이트, 에셋 로딩 수정 가이드 |
| [`stage-flow.md`](stage-flow.md) | 층 전환, 방 활성화, 분기, 휴식 룸, 보스 흐름 수정 가이드 |
| [`progression-save.md`](progression-save.md) | 카드 보상, 영구 스탯, 런 저장/이어하기, 설정 저장 수정 가이드 |
| [`audio.md`](audio.md) | 효과음 alias, preload, BGM 카테고리, 루프 사운드 수정 가이드 |
| [`ui-input.md`](ui-input.md) | `Form1` 상태 전환, 메뉴/설정 UI, 마우스 캡처, 입력 라우팅 수정 가이드 |
| [`game-logic.md`](game-logic.md) | `GameLogic` partial 구조와 상태 초기화/업데이트 순서 수정 가이드 |
| [`game-like-roadmap.md`](game-like-roadmap.md) | 게임성을 높이기 위한 우선순위 로드맵 |

### 빠른 선택 기준

- 플레이어 체감 변경: `player.md`, `physics.md`, `balance-tune.md`
- 월드/HUD/에셋 변경: `rendering.md`
- 층 구조/전투 흐름 변경: `stage-flow.md`, `game-logic.md`
- 성장/세이브/카드 변경: `progression-save.md`
- 입력/메뉴/설정 화면 변경: `ui-input.md`
- 사운드 변경: `audio.md`

---

## 3. 아키텍처 개요

### 렌더링 파이프라인

```
RaycastRenderer
  └─ D3D11RenderBackend
       ├─ D3D11WorldRaycastPresenter  (벽·바닥 레이캐스팅)
       ├─ D3D11WorldSpritePresenter   (스프라이트)
       ├─ D3D11WorldBeamPresenter     (레이저 빔 효과)
       └─ D3D11OverlayPresenter       (HUD·UI)
WorldRenderDataBuilder  → GPU 커맨드 구성
TextureManager          → 텍스처 로딩·관리
Camera                  → FOV·투영
```

### 프레임당 업데이트 순서

1. 타이머 업데이트 (메시지, 데미지 플래시, 쿨다운, 문 애니메이션)
2. 적 AI 업데이트 → 데미지 콜백 발생
3. 입력 처리 (이동, 무기, 특수 행동)
4. 물리 (이동, 충돌, 아이템 수집)
5. 렌더링 (레이캐스팅 + HUD 합성)

### 시스템 의존 관계

```
GameLogic (코디네이터)
  ├─ Player
  ├─ Weapon
  ├─ EnemyManager
  │  ├─ Enemy[]
  │  ├─ EnemyProjectile[]
  │  └─ AudioService
  ├─ MapManager
  │  ├─ CollisionSystem
  │  └─ StageRoom[]
  ├─ RaycastRenderer
  │  ├─ Camera
  │  ├─ WorldRenderDataBuilder
  │  └─ TextureManager
  └─ GameAudioService
       ├─ BackgroundSoundManager (BGM)
       └─ EffectSoundManager (SFX)
```

---

## 4. 주요 클래스 목록

| 클래스 | 파일 위치 | 역할 |
|---|---|---|
| `GameLogic` | Game/Core/ | 마스터 코디네이터. 입력·루프·스테이지 진행·엔티티 업데이트 전체 관장 |
| `Player` | Game/Entities/ | 플레이어 상태: 위치, 방향, 체력, 스태미나, 이동, FOV, 대시 |
| `Weapon` | Game/Entities/ | 무기 5종 관리: 탄약, 데미지, 업그레이드, 발사 메커니즘 |
| `Enemy` | Game/Entities/ | 개별 적 인스턴스: 위치, 체력, 상태머신, AI, 전투 타이머 |
| `EnemyManager` | Game/Entities/ | 적 생명주기: 스폰, AI 업데이트, 발사체, 사망 콜백 |
| `MapManager` | Game/Map/ | 맵 데이터, 문, 스테이지 룸, 플레이어 스폰 포인트 |
| `CollisionSystem` | Game/Systems/ | 플레이어-벽 충돌 감지·반응 |
| `RaycastRenderer` | Game/Rendering/ | 레이캐스팅 3D 렌더러 + HUD |
| `StageFlow` | Game/Map/ | 스테이지 생성, 룸 템플릿, 난이도 스케일링 |
| `GameConfig` | Game/Config/ | 정적 밸런스 상수 (모든 수치값의 진실의 원천) |
| `EnemyCatalog` | Game/Entities/ | 적 아키타입 정적 카탈로그 |
| `GameAudioService` | Game/Audio/ | 오디오 시스템 퍼사드 |
| `RewardPickup` | Game/Entities/ | 드롭 아이템 (탄약, 코인, 스탯 카드) |
| `EnemyProjectile` | Game/Entities/ | 적·플레이어 발사체 |
| `PermanentProgressionData` | Game/ | 런 간 영구 스탯 데이터 |

> `GameLogic`은 **partial class**로 여러 파일에 나뉘어 있음. 각 파일은 책임 영역별로 분리.

---

## 5. 무기 시스템 (5종)

### WeaponType 열거형
```csharp
AMPistol, BearKiller, HChainGun, AutoCannon, DuelBerettas
```

### 무기 상세 스탯

| 무기 | 데미지 | 쿨다운 | 사거리 | 특성 |
|---|---|---|---|---|
| **AMPistol** (.44 AMP) | 48 | 0.3s | 32 tiles | 단발, 스프레드 0.12 |
| **BearKiller** (샷건) | 12×16발 | 1.02s | 13 tiles | 사거리 감쇠 30~100%, 스프레드 1.7 |
| **HChainGun** (체인건) | 16 | 0.09s (11rps) | 25 tiles | 홀드-파이어 연사 |
| **AutoCannon** (로켓 슬롯) | 78 | 0.62s | 28 tiles | 플레이어 로켓 투사체 6.2t/s, 스플래시 2.65 tiles |
| **DuelBerettas** (쌍권총) | 18 | 0.12s | 24 tiles | 홀드 연사 |

### 무기 상태 변수
```csharp
Weapon.CurrentType          // 현재 선택 무기
ammoPerWeapon[5]            // 무기별 독립 탄약
weaponUpgradeLevels[5, 8]   // 업그레이드 등급 매트릭스 (무기 × 카테고리)
ShotCooldown                // 재발사 타이머
ArmorPiercingActive         // 방어 관통 토글 (1.5× 데미지)
```

### WeaponUpgradeCategory 열거형
```csharp
Damage, Range, Pellets, FireRate, Spread, Splash, AmmoDropBonus, Special
```

### 카드 등급별 보너스
| 등급 | 보너스 |
|---|---|
| White | +1% |
| Green | +2% |
| Blue | +3% |
| Purple | +4% |
| Red | +5% |

---

## 6. 플레이어 시스템

### 핵심 프로퍼티
```csharp
Vector2 Position       // 월드 좌표
Vector2 Direction      // 바라보는 방향 (단위벡터)
Vector2 Plane          // 레이캐스팅 카메라 평면 (Direction과 수직)
float Health           // 현재 체력
float MaxHealth        // 체력 상한
float Shield           // 현재 보호막
float Stamina          // 현재 스태미나
bool IsDead            // 사망 플래그
float Radius = 0.1f    // 충돌 원 반지름
```

### 수치 상수 (GameConfig)
| 항목 | 값 |
|---|---|
| 기본 체력 | 100 |
| 기본 보호막 | 100 |
| 보호막 회복 | 피해 후 10s 지연, 기본 1/s |
| 스태미나 최대 | 100 |
| 스태미나 소모 (질주) | 15/s |
| 스태미나 회복 | 15/s (1.35s 딜레이) |
| 기본 이동속도 | 2.5 tiles/s |
| 질주 배율 | 1.5× |
| 탈진 배율 | 0.5× |
| 대시 거리 | 2.0 tiles |
| 대시 지속시간 | 0.20s |
| 대시 쿨다운 | 5.0s |
| 대시 스태미나 비용 | 20 |
| 기본 FOV | 80° (범위: 60°~90°) |

### 입력 키 매핑
| 키 | 동작 |
|---|---|
| WASD | 이동 |
| Shift | 질주 |
| Ctrl | 대시 |
| 마우스 | 시야 회전 (감도 0.003 rad/pixel) |
| 1~5 | 무기 선택 |
| E | 상호작용 (문 열기 등) |
| LMB | 발사 |

---

## 7. 적 시스템

### EnemyType / 기본 스탯
| 타입 | HP | 속도 | 사거리 | 특성 |
|---|---|---|---|---|
| Gunner | 60 | 1.05 | 7.6 tiles | 원거리 |
| Elite | 120 | 0.92 | 1.02 tiles | 근접 |
| RuinedGunner | 72 | 1.0 | 6.4 tiles | 약화 원거리 |
| BloodGhost, BeamRevenant 등 | — | — | — | 특수 아키타입 |

### EnemyRank
```csharp
Normal, MiniBoss, Boss
```

### EnemyAiState (상태머신)
```csharp
Patrol → Investigate/Search → Combat → AttackWindup → AttackRecover
Stunned
```

### EnemyBehaviorPattern (전략)
```csharp
Default, Kite, Strafe, Juggernaut, Rushdown, Pouncer, Skirmisher
```

### 보류 액션 (PendingAction Enum)
```csharp
MeleeAttack, RangedAttack,
MiniBossAcid, MiniBossCharge,
BeastPounce, BeastUltimateCharge,
TankerRocket, TankerRocketRain,
RunnerBurst, RunnerRiftSummon,
ArtilleryLaser, ArtilleryUltimateSweep
```

### 난이도 스케일링
- 전투 층 클리어당: 적 체력 +0.4%, 데미지 +0.3%, 속도 +0.5%
- 보스 클리어당: 적 체력 +4%, 데미지 +3%, 속도 +1.5%

---

## 8. 맵·스테이지 시스템

### 타일 타입
| 값 | 의미 |
|---|---|
| 0 | 빈 공간 |
| 1~8 | 벽 (텍스처 ID) |
| 9 | 문 |

### 물리/충돌 상수
| 항목 | 값 |
|---|---|
| 플레이어 반지름 | 0.1 tiles |
| 충돌 스텝 크기 | 0.025 tiles |
| 최대 스텝 높이 | 0.375 tiles |
| 문 열리는 시간 | 0.85s |
| 문 상호작용 거리 | 1.5 tiles |

### 룸 레이아웃 변형 (RoomLayoutVariant)
```csharp
Open, CenterPillar, TwinPillars, CornerPillars,
SplitLanes, DiagonalPillars, InnerRing, CenterWall
```

### 난이도 프리셋
```csharp
Easy, Normal, Hard  // 적 배율 조정
```

---

## 9. 오디오 시스템

| 클래스 | 역할 |
|---|---|
| `GameAudioService` | 오디오 퍼사드 |
| `BackgroundSoundManager` | BGM 재생·트랙 관리 |
| `EffectSoundManager` | SFX·무기 사운드 |
| `AudioChannels` | IBackgroundAudioChannel / IEffectAudioChannel 추상화 |

- 무기별 발사음: Pistol, ShotGun, LMG, Rocket, PlazmaGun 레거시 alias
- LMG 등 연사 무기는 사운드 루프 별도 관리
- 사운드 자극(SoundStimulus)으로 적 인식 트리거 가능

---

## 10. 진행 시스템 (로그라이크)

### 런 내 보상
- 룸 클리어 후 무기 업그레이드 카드 또는 스탯 카드 선택
- **StatType:** `MaxHealth, MoveSpeed, DashCooldown, AmmoDropChance, Damage, CoinDropChance, LifeSteal, DamageReduction, ShopDiscount, KillHeal, KillDashCooldownRefund, CriticalChance, CardChoiceBonus, ShieldRegenRate, ShieldRegenDelayReduction`

### 영구 진행 (PermanentProgressionData)
- 런 클리어 후 언락 포인트로 영구 스탯 투자
- **영구 스탯:** `Health, MoveSpeed, PistolDamage, Sense, Luck`

## 11. 설계 패턴

| 패턴 | 적용 위치 |
|---|---|
| Coordinator | `GameLogic` — 모든 서브시스템 조율 |
| Partial Class | `GameLogic` 외 대형 클래스 — 책임별 파일 분리 |
| Facade | `GameAudioService` |
| Builder | `WorldRenderDataBuilder` |
| Factory | `RenderBackendFactory` |
| Object Pool | Enemy 배열 사전 할당·재사용 |
| State Machine | Enemy AI (`EnemyAiState`) |
| Data-Driven | `EnemyCatalog` 아키타입 정의 |
| Strategy | `EnemyBehaviorPattern` 교체 가능 AI 전략 |
| Callback | 적 데미지 시 `OnPlayerDamaged` 호출 |

---

## 12. NuGet 의존성

| 패키지 | 용도 |
|---|---|
| NAudio | 오디오 재생 |
| NAudio.Vorbis | Ogg Vorbis 지원 |
| SharpGen.Runtime | DirectX COM 인터롭 |
| System.Runtime.CompilerServices.Unsafe | 저수준 인터롭 |

---

## 13. 코드 수정 시 주의사항

1. **수치값 수정은 항상 `GameConfig`에서** — 매직 넘버를 코드에 직접 쓰지 말 것.
2. **`GameLogic`은 partial class** — 기능 추가 시 적절한 파일 선택 또는 새 partial 파일 생성.
3. **Enemy 추가 시 `EnemyCatalog`에 아키타입 등록 필요** — 코드와 데이터 분리 유지.
4. **무기 업그레이드는 `weaponUpgradeLevels[weapon, category]` 매트릭스** — 인덱스 범위(5×8) 준수.
5. **충돌은 서브스텝 방식** — `CollisionStepSize = 0.025f` 단위로 분할. 한 번에 큰 이동을 적용하면 벽 관통 발생.
6. **레이캐스팅 렌더러는 `Camera.Plane`이 `Direction`과 수직** — 방향 변경 시 반드시 Plane도 갱신.
7. **오디오 채널은 인터페이스 기반** — `IBackgroundAudioChannel` / `IEffectAudioChannel`로 접근.
8. **문(Door) 타일은 완전히 열리면 타일 타입 0으로 전환됨** — 문 상태 확인 시 progress 값 참조.
