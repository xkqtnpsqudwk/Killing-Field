# add-enemy

새로운 적 아키타입을 My2DEngine에 추가하는 체크리스트.

## 필수 절차 (순서대로)

### 1. EnemyType 열거형 추가
- 파일: `Game/Entities/Enemy.cs` (또는 EnemyType 정의 위치)
- 기존 값 뒤에 새 타입 추가

### 2. EnemyCatalog에 아키타입 등록
- 파일: `Game/Entities/EnemyCatalog.cs`
- HP, 속도, 사거리, EnemyBehaviorPattern, EnemyRank 설정
- 데이터-코드 분리 원칙 준수 — 수치는 반드시 여기서만

### 3. GameConfig에 수치 상수 추가 (필요 시)
- 파일: `Game/Config/GameConfig.cs`
- 매직 넘버 금지 — 모든 밸런스 값은 GameConfig 경유

### 4. PendingAction 추가 (특수 공격이 있는 경우)
- 파일: Enemy/EnemyManager 관련 파일
- 기존 패턴 참조: `BeastPounce`, `TankerRocket`, `RunnerRiftSummon`, `ArtilleryLaser` 등

### 5. AI 로직 구현
- EnemyAiState 상태머신: `Patrol`, `Investigate`, `Search`, `Combat`, `AttackWindup`, `AttackRecover`, `Stunned`
- EnemyBehaviorPattern 전략 선택 또는 신규 추가
- 공격 로직은 EnemyManager에서 처리

### 6. 오디오 연결 (필요 시)
- `EffectSoundManager`에 사운드 등록
- SoundStimulus로 적 인식 트리거 설정 가능

### 7. 스폰 설정
- `StageFlow`에서 EnemyType별 스폰 조건/빈도 조정
- 난이도 스케일링: 한 전투 층 클리어시 HP +0.4%, 데미지 +0.3%, 속도 +0.5%
-                  보스 클리어시 HP +4%, 데미지 +3%, 속도 +1.5%
-                  휴식 스테이지는 스케일링에 포함하지 않게 자동 적용됨

## 체크리스트

- [ ] EnemyType 열거형에 추가됨
- [ ] EnemyCatalog에 아키타입 등록됨
- [ ] 모든 수치값이 GameConfig에 있음
- [ ] 특수 공격은 PendingAction 추가됨
- [ ] EnemyManager에서 PendingAction 처리됨
- [ ] 스폰 로직에 포함됨
