using System;
using System.Collections.Generic;
using System.Runtime;
using My2DEngine.Engine.Core;
using My2DEngine.Engine.Rendering;
using My2DEngine.Engine.Rendering.Abstractions;
using My2DEngine.Game;
using My2DEngine.Game.Audio;
using My2DEngine.Game.Config;
using My2DEngine.Game.Map;
using My2DEngine.Game.Rendering;
using My2DEngine.Game.Systems;

namespace My2DEngine.Game.Core
{
    /// <summary>
    /// 게임 전체 상태의 최상위 조정자(Coordinator) 클래스.
    /// 입력 해석, 스테이지 진행, 전투 시스템 호출, 렌더러/오디오 상태 갱신을 하나의 흐름으로 연결하되,
    /// 실제 세부 구현은 <see cref="EnemyManager"/>, <see cref="MapManager"/>, <see cref="RaycastRenderer"/> 같은
    /// 하위 시스템에 위임한다. 파일이 크기 때문에 여러 partial 파일로 나뉘어 있다.
    /// </summary>
    public partial class GameLogic : IDisposable
    {
        /// <summary>
        /// 게임 난이도 사전 설정 열거형.
        /// Easy/Normal/Hard에 따라 적의 체력·속도·공격력 배율이 달라진다.
        /// </summary>
        public enum DifficultyPreset
        {
            /// <summary>쉬운 난이도. 적 능력치가 기본값보다 낮게 설정된다.</summary>
            Easy,
            /// <summary>보통 난이도. 적 능력치가 기본값(배율 1.0)으로 설정된다.</summary>
            Normal,
            /// <summary>어려운 난이도. 적 능력치가 기본값보다 높게 설정된다.</summary>
            Hard
        }

        /// <summary>맵 타일·문·스테이지 방 정보를 관리하는 맵 매니저.</summary>
        private readonly MapManager mapManager;

        /// <summary>벽·스프라이트 텍스처 로딩 및 접근을 담당하는 텍스처 매니저.</summary>
        private readonly TextureManager textureManager;

        /// <summary>총성·문 소리 등 짧은 효과음을 재생하는 SFX 채널.</summary>
        private readonly EffectSoundManager sfxChannel;

        /// <summary>배경 음악(BGM) 재생·전환을 담당하는 BGM 채널.</summary>
        private readonly BackgroundSoundManager bgmChannel;

        /// <summary>적 생성·업데이트·조회를 담당하는 적 매니저.</summary>
        private readonly EnemyManager enemyManager;

        /// <summary>레이캐스팅 기반 3D 뷰 렌더러.</summary>
        private readonly RaycastRenderer renderer;

        /// <summary>
        /// Render() 호출 시 캐싱되는 렌더 백엔드 퍼사드.
        /// 층 전환 시 OnLevelTransition()을 호출해 백엔드 VRAM 캐시를 비울 때 사용한다.
        /// </summary>
        private Renderer cachedRenderer;

        /// <summary>플레이어-벽 충돌 판정 시스템. 맵이 교체될 때마다 재생성된다.</summary>
        private CollisionSystem collision;

        /// <summary>플레이어 위치·방향·체력·이동 상태를 보관하는 플레이어 객체.</summary>
        private readonly Player player;

        /// <summary>현재 장착된 무기 상태(탄약·재장전·발사 타이머)를 보관하는 무기 객체.</summary>
        private readonly Weapon weapon;

        /// <summary>맵 위에 배치된 보상 아이템 목록. 방 클리어 시 추가되고 획득 시 제거된다.</summary>
        private readonly List<RewardPickup> rewardPickups;

        /// <summary>플레이어가 발사한 로켓 투사체와 폭발 시각 효과 목록.</summary>
        private readonly List<EnemyProjectile> playerProjectiles;

        /// <summary>보상 희귀도 랜덤 결정에 사용되는 난수 생성기.</summary>
        private readonly System.Random rewardRandom;

        /// <summary>방 ID → StageRoom 빠른 조회를 위한 딕셔너리. RebuildStageRoomLookup()으로 갱신된다.</summary>
        private readonly Dictionary<int, StageRoom> stageRoomLookup;

        /// <summary>화면 상단에 표시할 스테이지 상태 메시지 문자열. null이면 표시하지 않는다.</summary>
        private string stageStatusMessage;

        /// <summary>stageStatusMessage가 표시되는 남은 시간(초).</summary>
        private float stageStatusTimer;

        /// <summary>현재 전투 중인(활성화된) 스테이지 방의 인덱스. 활성 방이 없으면 -1.</summary>
        private int activeStageRoomIndex;

        /// <summary>모든 보스 방을 클리어했을 때 true가 되어 게임 승리 상태를 나타낸다.</summary>
        private bool victory;

        /// <summary>보스 방 진입 시 연출 타이머. 0 이하가 되면 연출이 끝난 것으로 간주한다.</summary>
        private float bossIntroTimer;

        /// <summary>이전 프레임에 상호작용 키(E)가 눌려 있었는지 여부. 엣지 트리거 처리에 사용.</summary>
        private bool interactKeyHeld;

        /// <summary>이전 프레임에 스팀팩 키(Q)가 눌려 있었는지 여부. 엣지 트리거 처리에 사용.</summary>
        private bool stimKeyHeld;

        /// <summary>이전 프레임에 대시 키(Ctrl)가 눌려 있었는지 여부. 엣지 트리거 처리에 사용.</summary>
        private bool dashKeyHeld;

        /// <summary>화면에 표시할 상호작용 안내 문구. 상호작용 가능한 문이 없으면 null.</summary>
        private string interactPromptText;

        /// <summary>현재 적용된 난이도 사전 설정 값.</summary>
        private DifficultyPreset difficultyPreset;

        /// <summary>피격 시 화면을 붉게 물들이는 플래시 효과의 남은 강도(0~1.2). 시간이 지나면 감소한다.</summary>
        private float playerDamageFlashTimer;

        /// <summary>피격 플래시의 수평 방향 성분(월드 공간). 피격 방향을 나타낸다.</summary>
        private float playerDamageFlashDirX;

        /// <summary>피격 플래시의 수직 방향 성분(월드 공간). 피격 방향을 나타낸다.</summary>
        private float playerDamageFlashDirY;

        /// <summary>피격 카메라 흔들림 효과의 남은 시간(초).</summary>
        private float playerDamageShakeTimer;

        /// <summary>피격 카메라 흔들림 효과의 현재 강도(0~1).</summary>
        private float playerDamageShakePower;

        /// <summary>발사 반동 카메라 흔들림 효과의 남은 시간(초).</summary>
        private float playerRecoilShakeTimer;

        /// <summary>발사 반동 카메라 흔들림 효과의 현재 강도(0~1).</summary>
        private float playerRecoilShakePower;

        /// <summary>사망 연출(화면 기울기) 진행도(0~1). 1.25초 동안 선형으로 증가한다.</summary>
        private float deathPresentationProgress;

        /// <summary>사망 시 화면이 기울어지는 방향. +1이면 오른쪽, -1이면 왼쪽으로 쓰러진다.</summary>
        private float deathRollDirection;

        /// <summary>현재 프레임에서 플레이어가 머물고 있는 스테이지 방의 캐시. 방이 바뀌면 갱신된다.</summary>
        private StageRoom currentStageRoomCache;

        /// <summary>사망 후 BGM 정지 처리를 한 번만 수행하기 위한 플래그.</summary>
        private bool deathMusicStopped;

        /// <summary>현재 LMG가 연사 준비 상태인지 여부. 필요 시 준비 지연을 거칠 수 있으나 Heavy Chaingun은 즉시 진입한다.</summary>
        private bool lmgSpinActive;

        /// <summary>LMG가 실제 발사 상태에 진입하기 전까지 남은 준비 시간(초). Heavy Chaingun 세팅에서는 0이다.</summary>
        private float lmgSpinUpTimer;

        /// <summary>현재 LMG 연사 루프 사운드가 재생 중인지 여부.</summary>
        private bool lmgFireLoopActive;

        /// <summary>현재 플레이어 로켓 비행 루프 사운드가 재생 중인지 여부.</summary>
        private bool rocketFlyLoopActive;

        /// <summary>Dispose가 이미 호출되었는지 여부. 중복 해제를 방지한다.</summary>
        private bool disposed;

        /// <summary>
        /// 현재 로그라이크 런의 층 번호(1~666+). 0이면 로그라이크 모드가 비활성 상태다.
        /// <see cref="StartRoguelikeRun"/>으로 시작되고 <see cref="TransitionToNextFloor"/>로 증가한다.
        /// </summary>
        private int currentFloor;

        /// <summary>로그라이크 룸 템플릿 무작위 선택에 사용하는 전용 난수 생성기.</summary>
        private readonly System.Random templateRandom;

        /// <summary>현재 런에서 누적된 보스 클리어 수. 이후 층 적 성장 배율 계산에 사용된다.</summary>
        private int bossClearGrowthCount;

        /// <summary>현재 런에서 누적된 전투 층 클리어 수다. 휴식 층은 포함하지 않는다.</summary>
        private int clearedCombatFloorCount;

        /// <summary>
        /// 기본 생성자. 모든 하위 시스템을 만들고 DX11 기준선 렌더 경로를 초기화한다.
        /// </summary>
        public GameLogic()
        {
            mapManager = new MapManager();

            textureManager = new TextureManager();
            textureManager.LoadAllTextures();

            sfxChannel = new EffectSoundManager();
            bgmChannel = new BackgroundSoundManager();
            sfxChannel.LoadAllSounds();
            bgmChannel.LoadAllSounds();

            enemyManager = new EnemyManager(textureManager, sfxChannel);
            enemyManager.EnemyDeathCallback = OnEnemyKilled;
            renderer = new RaycastRenderer(mapManager, textureManager, enemyManager);

            player = new Player(3.5f, 3.5f);
            weapon = new Weapon();
            rewardPickups = new List<RewardPickup>();
            playerProjectiles = new List<EnemyProjectile>();
            rewardRandom = new System.Random();
            templateRandom = new System.Random();
            stageRoomLookup = new Dictionary<int, StageRoom>();
            ResetCardRunState();
            ResetRunEndlessState();

            SetDifficultyPreset(DifficultyPreset.Normal);
            ApplyMapSpawnSettings();
            collision = new CollisionSystem(mapManager.Map, mapManager.FloorHeights);
            enemyManager.BuildFromMap(mapManager.Map, player.Position, collision);
            RebuildStageRoomLookup();
            ResetStageState();
            LoadPermanentProgression();
            ApplyCombinedProgressionStats(refillHealth: true, healMaxHealthDelta: false);
        }

        /// <summary>
        /// 난이도 사전 설정을 적용한다.
        /// Easy는 적 능력치를 하향하고, Hard는 상향하며, Normal은 기본값(1.0)을 유지한다.
        /// </summary>
        /// <param name="preset">적용할 난이도 사전 설정.</param>
        public void SetDifficultyPreset(DifficultyPreset preset)
        {
            difficultyPreset = preset;
            ApplyEnemyDifficultyScaling();
        }

        /// <summary>
        /// 로그라이크 런을 새로 시작한다.
        /// 플레이어·무기 상태를 초기화하고 1층 룸 템플릿을 로드한다.
        /// </summary>
        public void StartRoguelikeRun()
        {
            bgmChannel.StopBackgroundMusic();
            deathMusicStopped = false;
            currentFloor = 0;
            runEnemiesKilled = 0;
            runBossesKilled = 0;
            runStartTime = DateTime.UtcNow;
            ResetEnemyGrowthProgression();
            ResetCardRunState();
            ResetPlayerState();
            ResetRunEndlessState();
            ApplyCombinedProgressionStats(refillHealth: true, healMaxHealthDelta: false);
            TransitionToNextFloor(commitCurrentFloorGrowth: false);
        }

        /// <summary>
        /// 현재 난이도 프리셋, 전투 층 클리어 수, 보스 클리어 수를 합산해 이후에 스폰되는 적 배율을 갱신한다.
        /// </summary>
        private void ApplyEnemyDifficultyScaling()
        {
            float baseHealthMultiplier;
            float baseDamageMultiplier;
            float baseMoveSpeedMultiplier;

            switch (difficultyPreset)
            {
                case DifficultyPreset.Easy:
                    baseHealthMultiplier = 0.85f;
                    baseDamageMultiplier = 0.85f;
                    baseMoveSpeedMultiplier = 0.92f;
                    break;
                case DifficultyPreset.Hard:
                    baseHealthMultiplier = 1.25f;
                    baseDamageMultiplier = 1.2f;
                    baseMoveSpeedMultiplier = 1.12f;
                    break;
                default:
                    baseHealthMultiplier = 1f;
                    baseDamageMultiplier = 1f;
                    baseMoveSpeedMultiplier = 1f;
                    break;
            }

            float floorHealthMultiplier = 1f + clearedCombatFloorCount * GameConfig.EnemyHealthGrowthPerClearedCombatFloor;
            float floorDamageMultiplier = 1f + clearedCombatFloorCount * GameConfig.EnemyDamageGrowthPerClearedCombatFloor;
            float floorMoveSpeedMultiplier = 1f + clearedCombatFloorCount * GameConfig.EnemyMoveSpeedGrowthPerClearedCombatFloor;

            float bossHealthMultiplier = 1f + bossClearGrowthCount * GameConfig.EnemyHealthGrowthPerBossClear;
            float bossDamageMultiplier = 1f + bossClearGrowthCount * GameConfig.EnemyDamageGrowthPerBossClear;
            float bossMoveSpeedMultiplier = 1f + bossClearGrowthCount * GameConfig.EnemyMoveSpeedGrowthPerBossClear;

            enemyManager.ConfigureDifficulty(
                baseHealthMultiplier * floorHealthMultiplier * bossHealthMultiplier,
                baseDamageMultiplier * floorDamageMultiplier * bossDamageMultiplier,
                baseMoveSpeedMultiplier * floorMoveSpeedMultiplier * bossMoveSpeedMultiplier);
        }

        /// <summary>새 런 시작 시 전투 층/보스 누적 성장치를 초기화한다.</summary>
        private void ResetEnemyGrowthProgression()
        {
            bossClearGrowthCount = 0;
            clearedCombatFloorCount = 0;
            ApplyEnemyDifficultyScaling();
        }

        /// <summary>지정한 값으로 보스 클리어 누적 성장치를 설정한다.</summary>
        private void SetBossClearEnemyGrowthCount(int count)
        {
            bossClearGrowthCount = Math.Max(0, count);
            ApplyEnemyDifficultyScaling();
        }

        /// <summary>보스를 하나 더 처치했을 때 이후 층 적 성장치를 1단계 올린다.</summary>
        private void RegisterBossClearEnemyGrowth()
        {
            bossClearGrowthCount++;
            ApplyEnemyDifficultyScaling();
        }

        /// <summary>지정한 값으로 전투 층 클리어 누적 성장치를 설정한다.</summary>
        private void SetClearedCombatFloorCount(int count)
        {
            clearedCombatFloorCount = Math.Max(0, count);
            ApplyEnemyDifficultyScaling();
        }

        /// <summary>
        /// 현재 층의 실제 전투 방이 클리어된 상태라면 전투 층 누적 성장치를 1단계 올린다.
        /// 휴식 층은 스케일링 대상에서 제외한다.
        /// </summary>
        private void CommitCurrentFloorEnemyGrowthIfEligible()
        {
            StageRoom encounterRoom = GetCurrentEncounterRoom();
            if (encounterRoom == null || !encounterRoom.State.Cleared || encounterRoom.IsRestRoom)
            {
                return;
            }

            clearedCombatFloorCount++;
        }

        /// <summary>현재 로그라이크 층의 메인 방(시작실 제외)을 반환한다.</summary>
        private StageRoom GetCurrentEncounterRoom()
        {
            StageRoom[] rooms = mapManager.StageRooms;
            if (rooms == null)
            {
                return null;
            }

            for (int i = 0; i < rooms.Length; i++)
            {
                StageRoom room = rooms[i];
                if (room != null && room.Id != 0)
                {
                    return room;
                }
            }

            return null;
        }

        /// <summary>
        /// 다음 층으로 전환한다.
        /// <para>순서:</para>
        /// <list type="number">
        ///   <item>필요하면 직전 층의 전투 클리어 성장치를 커밋한다.</item>
        ///   <item>층 번호를 1 증가시킨다.</item>
        ///   <item>preselected가 제공되면 해당 템플릿을, 없으면 층에 맞는 템플릿을 선택하여 맵을 로드한다.</item>
        ///   <item>플레이어를 새 방 시작 위치로 이동시킨다.</item>
        ///   <item>충돌·적·방 상태를 모두 재설정한다.</item>
        /// </list>
        /// </summary>
        /// <param name="preselected">
        /// 분기 선택 UI에서 플레이어가 고른 템플릿. null이면 층 번호 기반으로 자동 선택한다.
        /// </param>
        private void TransitionToNextFloor(RoomTemplate preselected = null, bool commitCurrentFloorGrowth = true)
        {
            if (commitCurrentFloorGrowth)
            {
                CommitCurrentFloorEnemyGrowthIfEligible();
            }

            currentFloor++;
            endlessModeActive = currentFloor > FinalRoguelikeFloor;
            ApplyEnemyDifficultyScaling();

            // 층당 1회용 특수기(LMG/PlazmaGun) → 매 층 전환 시 리셋
            weapon.ResetFloorSpecial();

            RoomTemplate template = preselected ?? RoomTemplateLibrary.SelectForFloor(currentFloor, templateRandom, bossClearGrowthCount);
            mapManager.LoadRoomFromTemplate(template, out _, out _);

            ApplyMapSpawnSettings();
            collision = new CollisionSystem(mapManager.Map, mapManager.FloorHeights);
            enemyManager.BuildFromMap(mapManager.Map, player.Position, collision);
            RebuildStageRoomLookup();
            ResetStageState();
            renderer.ResetTransientCaches();
            cachedRenderer?.OnLevelTransition();
            CollectTransitionGarbage();

            string floorTag = currentFloor % 20 == 0 ? " [BOSS]" : string.Empty;
            if (endlessModeActive)
            {
                floorTag += " [ENDLESS]";
            }

            string entryHint = " - 시작실에서 문을 열고 진입";

            if (currentFloor == FinalRoguelikeFloor + 1)
            {
                SetStageStatus("무한 모드 개시 - 667층" + entryHint, 4.2f);
            }
            else
            {
                SetStageStatus($"{currentFloor}층{floorTag}{entryHint}", 3.4f);
            }
        }

        /// <summary>
        /// 층 전환 시 참조가 끊긴 LOH 버퍼를 즉시 회수해 작업 집합이 이전 전투 최대치로 남지 않게 한다.
        /// 방 단위 로그라이크라 전환 시 짧은 정지보다 메모리 회수 일관성이 더 중요하다.
        /// </summary>
        private static void CollectTransitionGarbage()
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        }

        /// <summary>
        /// 플레이어의 현재 시야각(Field of View) 값을 도(degree) 단위로 반환한다.
        /// </summary>
        /// <returns>현재 시야각(도).</returns>
        public float GetFovDegrees()
        {
            return player.FovDegrees;
        }

        /// <summary>
        /// 플레이어의 시야각(Field of View)을 도(degree) 단위로 설정한다.
        /// </summary>
        /// <param name="degrees">설정할 시야각(도).</param>
        public void SetFovDegrees(float degrees)
        {
            player.FovDegrees = degrees;
        }

        /// <summary>
        /// 플레이어의 마우스 감도 값을 반환한다.
        /// </summary>
        /// <returns>현재 마우스 감도(래디안/픽셀 단위 스케일 계수).</returns>
        public float GetMouseSensitivity()
        {
            return player.MouseSensitivity;
        }

        /// <summary>
        /// 플레이어의 마우스 감도를 설정한다. 최솟값은 0.0001로 제한된다.
        /// </summary>
        /// <param name="value">설정할 감도 값. 0.0001 미만이면 0.0001로 보정된다.</param>
        public void SetMouseSensitivity(float value)
        {
            if (value < 0.0001f)
            {
                value = 0.0001f;
            }

            player.MouseSensitivity = value;
        }

        /// <summary>
        /// 발사 버튼을 눌렀을 때 호출한다.
        /// - Pistol / ShotGun / RocketLauncher : PendingShot 세팅
        /// - LMG : FireButtonHeld = true (매 프레임 즉시 연사 처리)
        /// - PlazmaGun : FireButtonHeld = true (매 프레임 연사 처리)
        /// </summary>
        public void FireButtonDown()
        {
            if (victory || player.IsDead || permanentStatsUiActive || playerStatsOverlayActive || endingSequenceActive) return;

            switch (weapon.CurrentType)
            {
                case WeaponType.HChainGun:
                    weapon.FireButtonHeld = true;
                    StartLmgSpin();
                    break;
                case WeaponType.DuelBerettas:
                    weapon.FireButtonHeld = true;
                    weapon.PendingShot = true;
                    break;
                default:
                    weapon.PendingShot = true;
                    break;
            }
        }

        /// <summary>
        /// 발사 버튼을 뗐을 때 호출한다.
        /// - LMG : FireButtonHeld = false
        /// - PlazmaGun : FireButtonHeld = false
        /// </summary>
        public void FireButtonUp()
        {
            switch (weapon.CurrentType)
            {
                case WeaponType.HChainGun:
                    weapon.FireButtonHeld = false;
                    StopLmgFireLoop();
                    break;
                case WeaponType.DuelBerettas:
                    weapon.FireButtonHeld = false;
                    weapon.CancelCharge();
                    break;
            }
        }

        /// <summary>
        /// 지정 무기로 전환한다. 승리·사망 중에는 무시된다.
        /// </summary>
        public void SwitchWeapon(WeaponType type)
        {
            if (victory || player.IsDead || permanentStatsUiActive || playerStatsOverlayActive || endingSequenceActive) return;
            StopLoopingWeaponEffects();
            weapon.SwitchTo(type);
        }

        /// <summary>
        /// 현재 BGM 볼륨 비율을 반환한다.
        /// </summary>
        /// <returns>BGM 볼륨 (0~100).</returns>
        public int GetBgmVolume()
        {
            return bgmChannel.GetBgmVolume();
        }

        /// <summary>
        /// BGM 볼륨을 설정한다. 현재 재생 트랙에도 즉시 반영된다.
        /// </summary>
        /// <param name="volume">설정할 볼륨 비율 (0~100).</param>
        public void SetBgmVolume(int volume)
        {
            bgmChannel.SetBgmVolume(volume);
        }

        /// <summary>
        /// 현재 효과음 볼륨을 반환한다.
        /// </summary>
        /// <returns>효과음 볼륨 (0~100).</returns>
        public int GetSfxVolume()
        {
            return sfxChannel.GetSfxVolume();
        }

        /// <summary>
        /// 효과음 볼륨을 설정한다. 이후 재생되는 효과음부터 반영된다.
        /// </summary>
        /// <param name="volume">설정할 볼륨 값 (0~100).</param>
        public void SetSfxVolume(int volume)
        {
            sfxChannel.SetSfxVolume(volume);
        }

        /// <summary>
        /// 배경 음악을 즉시 정지하고 사망 음악 정지 플래그를 초기화한다.
        /// 맵 교체나 외부에서 명시적으로 BGM을 끌 때 호출한다.
        /// </summary>
        public void StopBackgroundMusic()
        {
            bgmChannel.StopBackgroundMusic();
            deathMusicStopped = false;
        }

        /// <summary>
        /// 지정한 별칭의 효과음을 재생한다.
        /// </summary>
        /// <param name="alias">재생할 효과음의 별칭(예: "fire", "reload", "door").</param>
        /// <param name="stopFirst">true이면 현재 재생 중인 효과음을 먼저 중단하고 재생한다.</param>
        private void PlayEffectSound(string alias, bool stopFirst, bool loop = false)
        {
            sfxChannel.PlaySound(alias, stopFirst, loop);
        }

        /// <summary>
        /// 지정한 별칭의 효과음 재생을 중단한다.
        /// </summary>
        /// <param name="alias">중단할 사운드 별칭.</param>
        private void StopEffectSound(string alias)
        {
            if (!string.IsNullOrWhiteSpace(alias))
            {
                sfxChannel.StopSound(alias);
            }
        }

        /// <summary>
        /// null이나 빈 별칭은 무시하고 무기 효과음을 재생한다.
        /// </summary>
        /// <param name="alias">재생할 사운드 별칭.</param>
        /// <param name="stopFirst">기존 동일 별칭 재생을 먼저 중단할지 여부.</param>
        /// <param name="loop">true이면 효과음을 반복 재생한다.</param>
        private void PlayWeaponEffectSound(string alias, bool stopFirst, bool loop = false)
        {
            if (!string.IsNullOrWhiteSpace(alias))
            {
                PlayEffectSound(alias, stopFirst, loop);
            }
        }

        /// <summary>
        /// null이나 빈 별칭은 무시하고 무기 효과음 재생을 중단한다.
        /// </summary>
        /// <param name="alias">중단할 사운드 별칭.</param>
        private void StopWeaponEffectSound(string alias)
        {
            StopEffectSound(alias);
        }

        /// <summary>
        /// LMG 연사 준비 상태를 시작한다. 준비 지연이 0이면 즉시 발사 가능 상태가 된다.
        /// </summary>
        private void StartLmgSpin()
        {
            if (weapon.CurrentType != WeaponType.HChainGun ||
                lmgSpinActive ||
                weapon.CurrentAmmo <= 0 ||
                player.IsDead ||
                victory)
            {
                return;
            }

            StopWeaponEffectSound(GameConfig.LMGWindDownSoundAlias);
            lmgSpinUpTimer = GameConfig.LMGWindUpDelay;
            lmgSpinActive = true;
        }

        /// <summary>
        /// LMG 연사 루프를 시작한다.
        /// </summary>
        private void StartLmgFireLoop()
        {
            if (weapon.CurrentType != WeaponType.HChainGun ||
                lmgFireLoopActive ||
                weapon.CurrentAmmo <= 0 ||
                player.IsDead ||
                victory)
            {
                return;
            }

            StopWeaponEffectSound(GameConfig.LMGWindDownSoundAlias);
            StopWeaponEffectSound(GameConfig.LMGWindUpSoundAlias);
            PlayWeaponEffectSound(GameConfig.LMGFireSoundAlias, true, true);
            lmgSpinUpTimer = 0f;
            lmgSpinActive = true;
            lmgFireLoopActive = true;
        }

        /// <summary>
        /// LMG 연사 루프를 중단한다.
        /// </summary>
        private void StopLmgFireLoop()
        {
            if (lmgFireLoopActive)
            {
                StopWeaponEffectSound(GameConfig.LMGFireSoundAlias);
                lmgFireLoopActive = false;
            }

            if (lmgSpinActive)
            {
                StopWeaponEffectSound(GameConfig.LMGWindUpSoundAlias);
                lmgSpinActive = false;
            }

            lmgSpinUpTimer = 0f;

        }

        /// <summary>
        /// 현재 재생 중인 루프형 무기 효과음을 모두 정리한다.
        /// </summary>
        private void StopLoopingWeaponEffects()
        {
            StopWeaponEffectSound(GameConfig.LMGFireSoundAlias);
            StopWeaponEffectSound(GameConfig.LMGWindUpSoundAlias);
            StopWeaponEffectSound(GameConfig.LMGWindDownSoundAlias);
            StopWeaponEffectSound(GameConfig.RocketFlySoundAlias);
            lmgSpinActive = false;
            lmgSpinUpTimer = 0f;
            lmgFireLoopActive = false;
            rocketFlyLoopActive = false;
        }

        /// <summary>
        /// 마지막 렌더 프레임에서 GPU 월드 렌더링이 사용되었는지 여부를 반환한다.
        /// </summary>
        /// <returns>GPU 렌더링을 사용했으면 true, 소프트웨어 렌더링이면 false.</returns>
        public bool UsesGpuWorldRendering()
        {
            return renderer.LastFrameUsedGpuWorld;
        }

        /// <summary>
        /// GPU 월드 렌더링의 현재 상태 문자열을 반환한다(디버그/진단용).
        /// </summary>
        /// <returns>렌더러가 보고하는 GPU 월드 상태 문자열.</returns>
        public string GetGpuWorldStatus()
        {
            return renderer.LastGpuWorldStatus;
        }

        /// <summary>
        /// 레이저 렌더링의 현재 상태 문자열을 반환한다(디버그/진단용).
        /// </summary>
        /// <returns>렌더러가 보고하는 레이저 렌더 상태 문자열.</returns>
        public string GetLaserRenderStatus()
        {
            return renderer.LastLaserRenderStatus;
        }

        /// <summary>
        /// 스프라이트 투영 디버그 시각화 기능을 켜거나 끈다.
        /// </summary>
        /// <param name="enabled">true이면 디버그 오버레이를 활성화한다.</param>
        public void SetShowSpriteProjectionDebug(bool enabled)
        {
            renderer.SetShowSpriteProjectionDebug(enabled);
        }

        /// <summary>
        /// 마우스 수평 이동량(픽셀 단위)을 받아 플레이어를 회전시킨다.
        /// 플레이어가 사망한 상태거나 이동량이 0이면 무시한다.
        /// </summary>
        /// <param name="dx">프레임 사이의 마우스 수평 이동 픽셀 수. 양수면 오른쪽, 음수면 왼쪽.</param>
        public void AddMouseDelta(int dx)
        {
            if (dx == 0 || player.IsDead || permanentStatsUiActive || playerStatsOverlayActive || endingSequenceActive)
            {
                return;
            }

            player.Rotate(dx * player.MouseSensitivity);
        }

        /// <summary>
        /// 플레이어·무기·적·시각 효과 상태를 초기값으로 되돌린다.
        /// 맵을 새로 생성할 때 호출하여 이전 게임의 잔여 상태를 제거한다.
        /// </summary>
        private void ResetPlayerState()
        {
            StopLoopingWeaponEffects();
            player.Reset();
            weapon.Reset();
            enemyManager.ResetAllEnemies();
            playerProjectiles.Clear();
            playerDamageFlashTimer = 0f;
            playerDamageFlashDirX = 0f;
            playerDamageFlashDirY = 0f;
            playerDamageShakeTimer = 0f;
            playerDamageShakePower = 0f;
            playerRecoilShakeTimer = 0f;
            playerRecoilShakePower = 0f;
            deathPresentationProgress = 0f;
            deathRollDirection = 1f;
            dashKeyHeld = false;
            deathMusicStopped = false;
        }

        /// <summary>
        /// 맵에서 정의한 플레이어 초기 위치·방향·시야각을 플레이어 객체에 적용한다.
        /// 맵 교체 후 반드시 호출해야 스폰 위치가 올바르게 설정된다.
        /// </summary>
        private void ApplyMapSpawnSettings()
        {
            player.Position = mapManager.PlayerStartPosition;
            player.Direction = mapManager.PlayerStartDirection;
            player.FovDegrees = mapManager.PlayerStartFov;
            currentStageRoomCache = null;
        }

        /// <summary>
        /// 현재 게임 상태를 렌더러에 전달하여 한 프레임을 화면에 그린다.
        /// GameLogic은 렌더 세부 구현을 알지 않고, 상태 값만 렌더러로 넘긴다.
        /// </summary>
        /// <param name="r">GDI+ 등 플랫폼 렌더 컨텍스트.</param>
        /// <param name="screenWidth">렌더 대상의 가로 픽셀 수.</param>
        /// <param name="screenHeight">렌더 대상의 세로 픽셀 수.</param>
        public void Render(Renderer r, int screenWidth, int screenHeight)
        {
            cachedRenderer = r;
            renderer.Render(r, screenWidth, screenHeight, player, weapon, rewardPickups, playerProjectiles, enemyManager.GetBossEnemy(),
                bossIntroTimer, stageStatusMessage, interactPromptText, victory,
                playerDamageFlashTimer, playerDamageFlashDirX, playerDamageFlashDirY,
                playerDamageShakeTimer, playerDamageShakePower,
                playerRecoilShakeTimer, playerRecoilShakePower,
                deathPresentationProgress, deathRollDirection);

            DrawPendingCardRewardReveal(r);
            DrawCardRewardUI(r);
            DrawBranchSelectionUI(r);
            DrawPermanentStatsUI(r);
            DrawPlayerStatsOverlay(r);
            DrawEndingSequenceUI(r);
        }

        /// <summary>
        /// 렌더러·사운드 매니저·텍스처 매니저 등 비관리 리소스를 해제한다.
        /// 중복 호출은 안전하게 무시된다.
        /// </summary>
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            renderer.Dispose();
            bgmChannel.Dispose();
            sfxChannel.Dispose();
            textureManager.Dispose();
            disposed = true;
        }

        /// <summary>
        /// <see cref="stageRoomLookup"/> 딕셔너리를 현재 맵의 스테이지 방 목록으로 재구성한다.
        /// 맵이 교체될 때마다 호출하여 ID 기반 빠른 조회가 항상 최신 상태를 반영하게 한다.
        /// </summary>
        private void RebuildStageRoomLookup()
        {
            stageRoomLookup.Clear();
            currentStageRoomCache = null;

            StageRoom[] rooms = mapManager.StageRooms;
            if (rooms == null)
            {
                return;
            }

            for (int i = 0; i < rooms.Length; i++)
            {
                StageRoom room = rooms[i];
                if (room != null)
                {
                    stageRoomLookup[room.Id] = room;
                }
            }
        }
    }
}
