# Killing Field

C# / WinForms 기반의 **레이캐스팅 1인칭 슈터**(Doom류). 렌더링은 자체 엔진의 **Direct3D 11 백엔드**로 처리하며, 층(스테이지)을 진행하면서 적·미니보스·보스를 처치하고 문을 열어 다음 구역으로 나아간다.

> 학습/개인 프로젝트입니다.

## 주요 기능

- **레이캐스트 월드 렌더링** — Direct3D 11 백엔드(Vortice). 벽/문/바닥/천장 텍스처, 스프라이트 원근 정렬.
- **전투** — 무기 5종 전환, 투사체, 히트/킬 마커, 피격 방향 오버레이, 피격·반동 화면 흔들림.
- **적 시스템** — 일반/미니보스/보스, 방 레이아웃과 연계된 행동 패턴, 보스 HP HUD.
- **HUD** — 체력·보호막·스태미나 게이지(이미지 틀), 탄약/코인 패널, **미니맵(장식 프레임 안에 렌더, 끊김 없는 스크롤)**.
- **진행/세이브** — 스테이지 분기, 카드 보상, 영구 스탯, SQLite 기반 저장.
- **오디오** — NAudio(+Vorbis) 기반 BGM/효과음(일반/미니보스/보스 트랙).
- **게임 루프** — `Application.Idle` 루프 + VSync(Present) 페이싱으로 모니터 주사율에 맞춘 매끄러운 프레임. 시간은 `Stopwatch` 기반 `DeltaTime`이라 프레임레이트 독립적.

## 기술 스택

- **.NET 8** (`net8.0-windows`), **WinForms**
- **Direct3D 11** — `Vortice.Direct3D11 / Direct2D1 / DXGI / D3DCompiler` 등
- **오디오** — `NAudio`, `NAudio.Vorbis`, `NVorbis`
- **저장** — `Microsoft.Data.Sqlite`
- **빌드 환경** — .NET 10 SDK / Visual Studio 18 (또는 `dotnet` CLI)

## 프로젝트 구조

```
Killing-Field/
├─ My2DEngine.csproj      # 실행 진입점(WinExe). Program.cs + Form1.* 부분 클래스
├─ My2DEngine.slnx        # 솔루션(신형 XML 포맷)
├─ Engine/                # 엔진: D3D11 렌더 백엔드, Core(Time 등), 입력, 호스팅
├─ Game/                  # 게임: 로직(GameLogic), 렌더러(RaycastRenderer), 맵, 적, 시스템
│  ├─ Images/             # 월드/스프라이트/UI 텍스처
│  └─ Sound/              # BGM·효과음
├─ My2DEngine.SmokeTests/ # 에셋·불변식 스모크 테스트(콘솔)
└─ Tools/                 # 스프라이트 생성 등 개발용 도구(빌드 제외)
```

엔진(`Engine`)과 게임(`Game`)은 별도 클래스 라이브러리이고, 루트 앱이 이를 `ProjectReference`로 묶는다.

## 빌드 & 실행

```sh
# CLI
dotnet build My2DEngine.slnx
dotnet run --project My2DEngine.csproj

# 또는 Visual Studio에서 My2DEngine.slnx 열고 F5
```

## 조작

| 입력 | 동작 |
|---|---|
| `W` `A` `S` `D` | 이동 |
| 마우스 | 시야 회전 |
| `Shift` | 달리기 |
| `Ctrl` | 대시 |
| 좌클릭(LMB) | 발사 |
| `1` ~ `5` | 무기 전환 |
| `E` | 연결된 문 열기 |
| `F3` | 디버그 HUD 토글 |
| `Esc` | 메뉴/일시정지 |

## 라이선스

별도 명시가 없는 한 개인 학습용 프로젝트입니다. 포함된 이미지·사운드 에셋의 재사용 권리는 각 원저작자에게 있습니다.
