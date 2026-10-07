using System;
using System.Drawing;
using System.Windows.Forms;
using My2DEngine.Engine.Input;
using My2DEngine.Engine.Rendering;
using My2DEngine.Game.Config;
using My2DEngine.Game.Rendering.Ui;

namespace My2DEngine.Game.Core
{
    /// <summary>
    /// GameLogic의 영구 스탯 저장/불러오기, 배분 UI, 런 적용을 담당하는 partial.
    /// </summary>
    public partial class GameLogic
    {
        /// <summary>영구 성장 데이터를 SQLite 파일에 저장하고 불러오는 저장소.</summary>
        private readonly SqliteProgressionRepository progressionRepository = new SqliteProgressionRepository();

        /// <summary>현재 프로필의 영구 성장 상태. null이면 기본값으로 폴백한다.</summary>
        private PermanentProgressionData permanentProgression;

        /// <summary>이번 런에서 처치한 일반/정예/보스 포함 총 적 수.</summary>
        private int runEnemiesKilled;

        /// <summary>이번 런에서 처치한 정예 또는 보스 수.</summary>
        private int runBossesKilled;

        /// <summary>이번 런 시작 시각. 사망/요약 화면의 플레이 시간 계산에 사용한다.</summary>
        private DateTime runStartTime;

        /// <summary>런 종료 처리를 이미 수행했는지 여부.</summary>
        private bool runEnded;

        /// <summary>런 종료 시각(UTC). 사망 화면에서 시간이 계속 늘어나지 않도록 고정한다.</summary>
        private DateTime runEndedAtUtc;

        /// <summary>현재 런 결과를 저장소에 이미 기록했는지 여부.</summary>
        private bool runResultRecorded;

        /// <summary>영구 스탯 배분 UI가 열려 있는지 여부.</summary>
        private bool permanentStatsUiActive;

        /// <summary>I 키 토글을 한 번만 처리하기 위한 이전 프레임 입력 상태.</summary>
        private bool permanentStatsToggleHeld;

        /// <summary>영구 스탯 카드 1번 키의 엣지 트리거 상태.</summary>
        private bool permanentStatKey1Held;

        /// <summary>영구 스탯 카드 2번 키의 엣지 트리거 상태.</summary>
        private bool permanentStatKey2Held;

        /// <summary>영구 스탯 카드 3번 키의 엣지 트리거 상태.</summary>
        private bool permanentStatKey3Held;

        /// <summary>영구 스탯 카드 4번 키의 엣지 트리거 상태.</summary>
        private bool permanentStatKey4Held;

        /// <summary>영구 스탯 카드 5번 키의 엣지 트리거 상태.</summary>
        private bool permanentStatKey5Held;

        /// <summary>마우스 클릭으로 영구 스탯을 선택할 때 처리할 게임 좌표 X (-1이면 미처리).</summary>
        private float pendingPermanentStatClickX = -1f;

        /// <summary>마우스 클릭으로 영구 스탯을 선택할 때 처리할 게임 좌표 Y.</summary>
        private float pendingPermanentStatClickY = -1f;

        private void LoadPermanentProgression()
        {
            permanentProgression = progressionRepository.Load();
            permanentProgression.Sanitize();
        }

        private void SavePermanentProgression()
        {
            if (permanentProgression == null) return;
            permanentProgression.Sanitize();
            progressionRepository.Save(permanentProgression);
        }

        private float GetRunStatBonus(StatType stat)
        {
            return ClampRunStatBonusTotal(stat, runStatBonusTotals[(int)stat]);
        }

        private void ApplyCombinedProgressionStats(bool refillHealth, bool healMaxHealthDelta)
        {
            PermanentProgressionData data = permanentProgression ?? PermanentProgressionData.CreateDefault();
            data.Sanitize();

            // 영구 성장과 런 카드 보너스를 한곳에서 다시 합성한다.
            // 카드 선택, 런 시작, 저장 데이터 로드 후 모두 이 경로를 거쳐 파생 스탯을 동기화한다.
            float previousMaxHealth = player.MaxHealth;
            float previousHealth = player.Health;
            float newMaxHealth = PlayerConfig.PlayerHealthMax * (1f + data.GetHealthBonus() + GetRunStatBonus(StatType.MaxHealth));

            player.MaxHealth = newMaxHealth;
            if (refillHealth)
            {
                player.Health = newMaxHealth;
            }
            else if (healMaxHealthDelta && newMaxHealth > previousMaxHealth)
            {
                player.Health = Math.Min(previousHealth + (newMaxHealth - previousMaxHealth), newMaxHealth);
            }
            else
            {
                player.Health = Math.Min(previousHealth, newMaxHealth);
            }

            player.MoveSpeed = PlayerConfig.MoveSpeed * (1f + data.GetMoveSpeedBonus() + GetRunStatBonus(StatType.MoveSpeed));
            float dashCooldownReduction = GetRunStatBonus(StatType.DashCooldown);
            if (data.MoveSpeedPoints >= PlayerConfig.MoveSpeedDashSynergyThreshold)
                dashCooldownReduction = Math.Min(1f - (PlayerConfig.DashCooldownMinDuration / PlayerConfig.DashCooldownDuration),
                    dashCooldownReduction + PlayerConfig.MoveSpeedDashSynergyBonus);
            player.SetDashCooldownMult(1f - dashCooldownReduction);
            player.ConfigureShield(
                PlayerConfig.PlayerShieldMax,
                GetEffectiveShieldRegenRate(),
                GetEffectiveShieldRegenDelayDuration());

            weapon.SetStatDamageMult(1f + GetRunStatBonus(StatType.Damage));
            weapon.SetPermanentPistolDamageMult(1f + data.GetPistolDamageBonus());

            senseLevel = data.GetSenseTier();
        }

        private float GetEffectiveShieldRegenRate()
        {
            float bonus = Math.Max(0f, GetRunStatBonus(StatType.ShieldRegenRate));
            // 보호막 회복 속도 카드는 기본 회복량에 더해지고, 전역 상한에서 멈춘다.
            return Math.Min(PlayerConfig.PlayerShieldMaxRegenRate, PlayerConfig.PlayerShieldBaseRegenRate + bonus);
        }

        private float GetEffectiveShieldRegenDelayDuration()
        {
            // 지연 감소는 비율 카드지만 최소 2초 지연은 반드시 남겨 전투 중 즉시 회복을 막는다.
            float reduction = Math.Max(0f, Math.Min(
                1f - (PlayerConfig.PlayerShieldMinRegenDelay / PlayerConfig.PlayerShieldBaseRegenDelay),
                GetRunStatBonus(StatType.ShieldRegenDelayReduction)));
            return Math.Max(PlayerConfig.PlayerShieldMinRegenDelay, PlayerConfig.PlayerShieldBaseRegenDelay * (1f - reduction));
        }

        private void HandlePermanentStatsInput()
        {
            bool toggleHeld = Input.GetKey(Keys.I);
            if (endingSequenceActive)
            {
                permanentStatsToggleHeld = toggleHeld;
                return;
            }

            if (toggleHeld &&
                !permanentStatsToggleHeld &&
                !CardRewardRevealPending &&
                !cardRewardActive &&
                !branchSelectionActive &&
                !playerStatsOverlayActive)
            {
                permanentStatsUiActive = !permanentStatsUiActive;
                if (permanentStatsUiActive)
                {
                    OnPermanentStatsUiOpened();
                }
            }

            permanentStatsToggleHeld = toggleHeld;

            if (!permanentStatsUiActive)
            {
                permanentStatKey1Held = false;
                permanentStatKey2Held = false;
                permanentStatKey3Held = false;
                permanentStatKey4Held = false;
                permanentStatKey5Held = false;
                pendingPermanentStatClickX = -1f;
                pendingPermanentStatClickY = -1f;
                return;
            }

            HandlePermanentStatsMouseClick();

            bool key1 = Input.GetKey(Keys.D1);
            bool key2 = Input.GetKey(Keys.D2);
            bool key3 = Input.GetKey(Keys.D3);
            bool key4 = Input.GetKey(Keys.D4);
            bool key5 = Input.GetKey(Keys.D5);

            if (key1 && !permanentStatKey1Held)
            {
                AllocatePermanentStat(PermanentStatSlot.Health);
            }
            else if (key2 && !permanentStatKey2Held)
            {
                AllocatePermanentStat(PermanentStatSlot.MoveSpeed);
            }
            else if (key3 && !permanentStatKey3Held)
            {
                AllocatePermanentStat(PermanentStatSlot.Sense);
            }
            else if (key4 && !permanentStatKey4Held)
            {
                AllocatePermanentStat(PermanentStatSlot.PistolDamage);
            }
            else if (key5 && !permanentStatKey5Held)
            {
                AllocatePermanentStat(PermanentStatSlot.Luck);
            }

            permanentStatKey1Held = key1;
            permanentStatKey2Held = key2;
            permanentStatKey3Held = key3;
            permanentStatKey4Held = key4;
            permanentStatKey5Held = key5;

            interactKeyHeld = Input.GetKey(Keys.E);
            specialKeyHeld = Input.GetKey(Keys.F);
            dashKeyHeld = Input.GetKey(Keys.ControlKey) || Input.GetKey(Keys.LControlKey) || Input.GetKey(Keys.RControlKey);
        }

        private void OnPermanentStatsUiOpened()
        {
            weapon.PendingShot = false;
            weapon.FireButtonHeld = false;
            StopLoopingWeaponEffects();
            interactPromptText = null;
            pendingPermanentStatClickX = -1f;
            pendingPermanentStatClickY = -1f;
        }

        private void ResetPermanentStatsUiState()
        {
            permanentStatsUiActive = false;
            permanentStatsToggleHeld = false;
            permanentStatKey1Held = false;
            permanentStatKey2Held = false;
            permanentStatKey3Held = false;
            permanentStatKey4Held = false;
            permanentStatKey5Held = false;
            pendingPermanentStatClickX = -1f;
            pendingPermanentStatClickY = -1f;
        }

        private void GrantBossPermanentPoint()
        {
            if (permanentProgression == null)
            {
                permanentProgression = PermanentProgressionData.CreateDefault();
            }

            permanentProgression.UnspentPoints++;
            permanentProgression.Sanitize();
            SavePermanentProgression();
        }

        private void AllocatePermanentStat(PermanentStatSlot slot)
        {
            if (permanentProgression == null)
            {
                permanentProgression = PermanentProgressionData.CreateDefault();
            }

            bool success;
            bool healDelta = false;
            string successMessage;
            string failureMessage;

            switch (slot)
            {
                case PermanentStatSlot.Health:
                    success = permanentProgression.TrySpendHealthPoint();
                    healDelta = true;
                    successMessage = "영구 스탯 - 체력 +1";
                    failureMessage = "영구 스탯 포인트가 부족합니다";
                    break;

                case PermanentStatSlot.MoveSpeed:
                    success = permanentProgression.TrySpendMoveSpeedPoint();
                    successMessage = "영구 스탯 - 속도 +1";
                    failureMessage = "영구 스탯 포인트가 부족합니다";
                    break;

                case PermanentStatSlot.Sense:
                    success = permanentProgression.TrySpendSensePoint();
                    successMessage = $"영구 스탯 - 감각 Lv {permanentProgression.GetSenseTier()} ({permanentProgression.SenseValue:0.0})";
                    failureMessage = permanentProgression.UnspentPoints <= 0
                        ? "영구 스탯 포인트가 부족합니다"
                        : "감각은 최대 레벨입니다";
                    break;

                case PermanentStatSlot.PistolDamage:
                    success = permanentProgression.TrySpendPistolDamagePoint();
                    successMessage = "영구 스탯 - " + WeaponPresentation.GetDisplayName(WeaponType.AMPistol) + " 데미지 +1";
                    failureMessage = "영구 스탯 포인트가 부족합니다";
                    break;

                case PermanentStatSlot.Luck:
                    success = permanentProgression.TrySpendLuckPoint();
                    successMessage = $"영구 스탯 - 행운 Lv {permanentProgression.GetLuckLevel()} (+{permanentProgression.GetLuckLevel() * 10}%)";
                    failureMessage = permanentProgression.UnspentPoints <= 0
                        ? "영구 스탯 포인트가 부족합니다"
                        : "행운은 최대 레벨입니다";
                    break;

                default:
                    return;
            }

            if (!success)
            {
                SetStageStatus(failureMessage, 1.8f);
                return;
            }

            permanentProgression.Sanitize();
            SavePermanentProgression();
            ApplyCombinedProgressionStats(refillHealth: false, healMaxHealthDelta: healDelta);
            SetStageStatus(successMessage, 2.2f);
        }

        private void HandlePermanentStatsMouseClick()
        {
            if (pendingPermanentStatClickX < 0f)
            {
                return;
            }

            float clickX = pendingPermanentStatClickX;
            float clickY = pendingPermanentStatClickY;
            pendingPermanentStatClickX = -1f;
            pendingPermanentStatClickY = -1f;

            float fw = RenderConfig.GpuWorldMaxRenderWidth;
            float fh = RenderConfig.GpuWorldMaxRenderHeight;
            float panelW = 470f;
            float panelH = 338f;
            float panelX = (fw - panelW) * 0.5f;
            float panelY = (fh - panelH) * 0.5f;
            float panelCenterX = panelX + panelW * 0.5f;
            float cardW = 205f;
            float cardH = 60f;
            float gapX = 18f;
            float gapY = 10f;
            float topY = panelY + 84f;
            float leftX = panelCenterX - cardW - gapX * 0.5f;
            float rightX = panelCenterX + gapX * 0.5f;
            float secondRowY = topY + cardH + gapY;
            float thirdRowY = topY + (cardH + gapY) * 2f;

            if (IsPointInsideRect(clickX, clickY, leftX, topY, cardW, cardH))
            {
                AllocatePermanentStat(PermanentStatSlot.Health);
            }
            else if (IsPointInsideRect(clickX, clickY, rightX, topY, cardW, cardH))
            {
                AllocatePermanentStat(PermanentStatSlot.MoveSpeed);
            }
            else if (IsPointInsideRect(clickX, clickY, leftX, secondRowY, cardW, cardH))
            {
                AllocatePermanentStat(PermanentStatSlot.Sense);
            }
            else if (IsPointInsideRect(clickX, clickY, rightX, secondRowY, cardW, cardH))
            {
                AllocatePermanentStat(PermanentStatSlot.PistolDamage);
            }
            else if (IsPointInsideRect(clickX, clickY, panelCenterX - cardW * 0.5f, thirdRowY, cardW, cardH))
            {
                AllocatePermanentStat(PermanentStatSlot.Luck);
            }
        }

        private static bool IsPointInsideRect(float x, float y, float left, float top, float width, float height)
        {
            return x >= left && x < left + width && y >= top && y < top + height;
        }

        private void DrawPermanentStatsUI(Renderer r)
        {
            if (!permanentStatsUiActive)
            {
                return;
            }

            PermanentProgressionData data = permanentProgression ?? PermanentProgressionData.CreateDefault();
            float fw = RenderConfig.GpuWorldMaxRenderWidth;
            float fh = RenderConfig.GpuWorldMaxRenderHeight;

            r.DrawRectangle(0f, 0f, fw, fh, Color.FromArgb(190, 0, 0, 0));

            float panelW = 470f;
            float panelH = 338f;
            float panelX = (fw - panelW) * 0.5f;
            float panelY = (fh - panelH) * 0.5f;
            float panelCenterX = panelX + panelW * 0.5f;

            DrawUiOverlayPanel(r, panelX, panelY, panelW, panelH,
                Color.FromArgb(228, 18, 24, 34), Color.FromArgb(200, 255, 210, 115));

            DrawOverlayHeader(r, "영구 스탯", panelCenterX, panelY + 18f);
            r.DrawTextCenteredShadow("보스 처치 시 포인트 +1", panelCenterX, panelY + 40f,
                Color.FromArgb(210, 190, 190, 190), 6f);
            r.DrawTextCenteredShadow("미사용 포인트: " + data.UnspentPoints, panelCenterX, panelY + 54f,
                data.UnspentPoints > 0 ? Color.FromArgb(255, 120, 220, 140) : Color.FromArgb(210, 175, 175, 175), 6f);
            r.DrawTextCenteredShadow("무한 모드: " + (data.EndlessModeUnlocked ? "해금됨" : "잠김"),
                panelCenterX, panelY + 68f,
                data.EndlessModeUnlocked ? Color.FromArgb(230, 145, 220, 160) : Color.FromArgb(180, 160, 160, 160), 6f);

            float cardW = 205f;
            float cardH = 60f;
            float gapX = 18f;
            float gapY = 10f;
            float topY = panelY + 84f;
            float leftX = panelCenterX - cardW - gapX * 0.5f;
            float rightX = panelCenterX + gapX * 0.5f;
            float thirdRowY = topY + (cardH + gapY) * 2f;

            DrawPermanentStatCard(r, leftX, topY, cardW, cardH, "[1] 체력",
                "Lv " + data.HealthPoints,
                $"+{data.GetHealthBonus() * 100f:0}% 최대 체력",
                Color.FromArgb(255, 210, 90, 90));

            DrawPermanentStatCard(r, rightX, topY, cardW, cardH, "[2] 속도",
                "Lv " + data.MoveSpeedPoints,
                $"+{data.GetMoveSpeedBonus() * 100f:0}% 이동 속도",
                Color.FromArgb(255, 90, 190, 240));

            int senseNext = data.GetSensePointsToNextLevel();
            string senseProgress = senseNext > 0 ? $"다음 Lv까지 -{senseNext}pt" : "최대 레벨";
            DrawPermanentStatCard(r, leftX, topY + cardH + gapY, cardW, cardH, "[3] 감각",
                $"Lv {data.GetSenseTier()} / 5  ({data.SenseValue:0.0} / {PlayerConfig.PermanentSenseMax:0.0})",
                $"{GetSenseDescription(data.GetSenseTier())}  {senseProgress}",
                Color.FromArgb(255, 195, 150, 255));

            DrawPermanentStatCard(r, rightX, topY + cardH + gapY, cardW, cardH, "[4] " + WeaponPresentation.GetDisplayName(WeaponType.AMPistol),
                "Lv " + data.PistolDamagePoints,
                $"+{data.GetPistolDamageBonus() * 100f:0}% 피해",
                Color.FromArgb(255, 255, 170, 95));

            int luckNext = data.GetLuckPointsToNextLevel();
            string luckProgress = luckNext > 0 ? $"다음 Lv까지 -{luckNext}pt" : "최대 레벨";
            DrawPermanentStatCard(r, panelCenterX - cardW * 0.5f, thirdRowY, cardW, cardH, "[5] 행운",
                $"Lv {data.GetLuckLevel()} / 10  ({data.LuckValue:0.0} / {PlayerConfig.PermanentLuckMax:0.0})",
                $"고급 카드 확률 +{data.GetLuckLevel() * 10}%  {luckProgress}",
                Color.FromArgb(255, 245, 210, 110));

            string footer = data.UnspentPoints > 0
                ? "1~5로 배분, I로 닫기"
                : "배분 가능한 포인트가 없습니다. I로 닫기";
            r.DrawTextCenteredShadow(footer, panelCenterX, panelY + panelH - 18f,
                Color.FromArgb(220, 220, 220, 220), 6f);
        }

        private void DrawPermanentStatCard(Renderer r, float x, float y, float w, float h,
            string title, string levelText, string effectText, Color accent)
        {
            float centerX = x + w * 0.5f;

            DrawUiOverlayPanel(r, x, y, w, h, Color.FromArgb(205, 28, 36, 50), accent);

            r.DrawTextCenteredShadow(title, centerX, y + 14f, accent, 12f, true);
            r.DrawTextCenteredShadow(levelText, centerX, y + 33f,
                Color.FromArgb(235, 230, 230, 230), 6f);
            r.DrawTextCenteredShadow(effectText, centerX, y + 47f,
                Color.FromArgb(210, 185, 185, 185), 6f);
        }

        private string GetSenseDescription(int senseTier)
        {
            switch (senseTier)
            {
                case 0: return "분기 정보 비공개";
                case 1: return "적 종류 공개";
                case 2: return "적 수 공개";
                case 3: return "보상 종류 공개";
                case 4: return "보상 힌트 강화";
                default: return "모든 분기 정보 공개";
            }
        }

        /// <summary>플레이어가 현재 사망 상태인지 여부.</summary>
        public bool IsPlayerDead => player != null && player.IsDead;

        /// <summary>저장된 런 진행도가 있는지 여부.</summary>
        public bool HasRunSave() => progressionRepository.HasRunSave();

        /// <summary>현재 런 진행도를 DB에 저장한다. 플레이어가 살아 있고 런이 진행 중일 때만 저장.</summary>
        public void SaveRunProgress()
        {
            if (currentFloor <= 0 || player == null || player.IsDead) return;

            int weaponMask = 0;
            for (int i = 0; i < ownedWeapons.Length; i++)
                if (ownedWeapons[i]) weaponMask |= (1 << i);

            var data = new RunSaveData
            {
                Floor               = currentFloor,
                PlayerHealth        = player.Health,
                BonusMaxHealth      = GetRunStatBonus(StatType.MaxHealth),
                BonusMoveSpeed      = GetRunStatBonus(StatType.MoveSpeed),
                BonusDamage         = GetRunStatBonus(StatType.Damage),
                BonusAmmoDropChance = GetRunStatBonus(StatType.AmmoDropChance),
                BonusDashCooldown   = GetRunStatBonus(StatType.DashCooldown),
                BonusCoinDropChance = GetRunStatBonus(StatType.CoinDropChance),
                BonusLifeSteal      = GetRunStatBonus(StatType.LifeSteal),
                BonusDamageReduction = GetRunStatBonus(StatType.DamageReduction),
                BonusShopDiscount = GetRunStatBonus(StatType.ShopDiscount),
                BonusKillHeal = GetRunStatBonus(StatType.KillHeal),
                BonusKillDashCooldownRefund = GetRunStatBonus(StatType.KillDashCooldownRefund),
                BonusCriticalChance = GetRunStatBonus(StatType.CriticalChance),
                BonusCardChoiceBonus = GetRunStatBonus(StatType.CardChoiceBonus),
                BonusShieldRegenRate = GetRunStatBonus(StatType.ShieldRegenRate),
                BonusShieldRegenDelayReduction = GetRunStatBonus(StatType.ShieldRegenDelayReduction),
                BonusShieldedDamage = GetRunStatBonus(StatType.ShieldedDamage),
                BonusDashStrikeDamage = GetRunStatBonus(StatType.DashStrikeDamage),
                BonusLowHealthRage = GetRunStatBonus(StatType.LowHealthRage),
                BonusKillChain = GetRunStatBonus(StatType.KillChain),
                BonusExplosiveSpecialist = GetRunStatBonus(StatType.ExplosiveSpecialist),
                BonusLowAmmoRage = GetRunStatBonus(StatType.LowAmmoRage),
                BonusRapidFireChain = GetRunStatBonus(StatType.RapidFireChain),
                PlayerShield = player.Shield,
                ShieldRegenDelayTimer = player.ShieldRegenDelayTimer,
                CardChoiceBonusOffered = cardChoiceBonusOffered,
                ClearedCombatFloorCount = clearedCombatFloorCount,
                BossClearGrowthCount = bossClearGrowthCount,
                OwnedWeaponsMask    = weaponMask,
                CoinCount           = player.CoinCount,
                WeaponCardPoolCount = weaponCardPoolCount,
                RestRoomOpportunityCooldownActive = restRoomOpportunityCooldownActive,
                CurrentWeaponType   = (int)weapon.CurrentType,
                WeaponAmmoState     = SerializeWeaponAmmoState(),
                WeaponUpgradeState  = SerializeWeaponUpgradeState(),
                RunStatGradeState   = SerializeIntArray(runStatGrade),
                RunStatPickupState  = SerializeIntArray(runStatPickupCount),
            };
            progressionRepository.SaveRunSave(data);
        }

        /// <summary>저장된 런 진행도를 삭제한다 (사망 또는 새 게임 시작 시).</summary>
        public void DeleteRunProgress() => progressionRepository.DeleteRunSave();

        /// <summary>저장된 런 진행도를 불러와 게임을 재개한다.</summary>
        public void ContinueRun()
        {
            var save = progressionRepository.LoadRunSave();
            if (save == null) return;

            BeginRunRandom();

            bgmChannel.StopBackgroundMusic();
            deathMusicStopped = false;

            runEnemiesKilled = 0;
            runBossesKilled = 0;
            runStartTime = DateTime.UtcNow;
            runEnded = false;
            runEndedAtUtc = default;
            runResultRecorded = false;

            currentFloor = save.Floor - 1; // TransitionToNextFloor 에서 +1 됨
            restRoomOpportunityCooldownActive = save.RestRoomOpportunityCooldownActive;
            clearedCombatFloorCount = save.ClearedCombatFloorCount;
            bossClearGrowthCount = save.BossClearGrowthCount;

            ResetCardRunState();  // runStatBonusTotals 및 ownedWeapons 초기화
            ResetPlayerState();
            ResetRunEndlessState();

            // 카드 보너스 복원
            runStatBonusTotals[(int)StatType.MaxHealth]      = save.BonusMaxHealth;
            runStatBonusTotals[(int)StatType.MoveSpeed]      = save.BonusMoveSpeed;
            runStatBonusTotals[(int)StatType.Damage]         = save.BonusDamage;
            runStatBonusTotals[(int)StatType.AmmoDropChance] = save.BonusAmmoDropChance;
            runStatBonusTotals[(int)StatType.DashCooldown]   = save.BonusDashCooldown;
            runStatBonusTotals[(int)StatType.CoinDropChance] = save.BonusCoinDropChance;
            runStatBonusTotals[(int)StatType.LifeSteal]      = save.BonusLifeSteal;
            runStatBonusTotals[(int)StatType.DamageReduction] = save.BonusDamageReduction;
            runStatBonusTotals[(int)StatType.ShopDiscount] = save.BonusShopDiscount;
            runStatBonusTotals[(int)StatType.KillHeal] = save.BonusKillHeal;
            runStatBonusTotals[(int)StatType.KillDashCooldownRefund] = save.BonusKillDashCooldownRefund;
            runStatBonusTotals[(int)StatType.CriticalChance] = save.BonusCriticalChance;
            runStatBonusTotals[(int)StatType.CardChoiceBonus] = save.BonusCardChoiceBonus;
            runStatBonusTotals[(int)StatType.ShieldRegenRate] = save.BonusShieldRegenRate;
            runStatBonusTotals[(int)StatType.ShieldRegenDelayReduction] = save.BonusShieldRegenDelayReduction;
            runStatBonusTotals[(int)StatType.ShieldedDamage] = save.BonusShieldedDamage;
            runStatBonusTotals[(int)StatType.DashStrikeDamage] = save.BonusDashStrikeDamage;
            runStatBonusTotals[(int)StatType.LowHealthRage] = save.BonusLowHealthRage;
            runStatBonusTotals[(int)StatType.KillChain] = save.BonusKillChain;
            runStatBonusTotals[(int)StatType.ExplosiveSpecialist] = save.BonusExplosiveSpecialist;
            runStatBonusTotals[(int)StatType.LowAmmoRage] = save.BonusLowAmmoRage;
            runStatBonusTotals[(int)StatType.RapidFireChain] = save.BonusRapidFireChain;
            cardChoiceBonusOffered = save.CardChoiceBonusOffered || save.BonusCardChoiceBonus >= 1f;
            RestoreIntArray(save.RunStatGradeState, runStatGrade, -1);
            RestoreIntArray(save.RunStatPickupState, runStatPickupCount, 0);
            ClampRunStatBonusTotals();
            weaponCardPoolCount = Math.Max(0, save.WeaponCardPoolCount);

            // 무기 소유 복원
            for (int i = 0; i < ownedWeapons.Length; i++)
                ownedWeapons[i] = ((save.OwnedWeaponsMask >> i) & 1) != 0;
            ownedWeapons[0] = true; // 피스톨은 항상 소유

            WeaponType currentWeaponType = GetSavedCurrentWeaponType(save.CurrentWeaponType);
            int[] savedWeaponAmmo = ParseIntArrayOrNull(save.WeaponAmmoState, ownedWeapons.Length);
            int weaponUpgradeCategoryCount = Enum.GetValues(typeof(WeaponUpgradeCategory)).Length;
            int[,] savedWeaponUpgrades = ParseWeaponUpgradeState(save.WeaponUpgradeState, ownedWeapons.Length, weaponUpgradeCategoryCount);
            weapon.RestoreSavedRunState(savedWeaponAmmo, savedWeaponUpgrades, currentWeaponType);

            // 스탯 적용 (MaxHealth 포함) 후 저장된 HP로 덮어쓰기
            ApplyCombinedProgressionStats(refillHealth: true, healMaxHealthDelta: false);
            player.Health = Math.Min(save.PlayerHealth, player.MaxHealth);
            player.RestoreShieldState(save.PlayerShield, save.ShieldRegenDelayTimer);
            player.SetCoinCount(save.CoinCount);

            TransitionToNextFloor(commitCurrentFloorGrowth: false);
        }

        /// <summary>영구 스탯 UI가 현재 열려 있는지 여부 (메뉴에서 오버레이 렌더링 여부 판단용).</summary>
        public bool PermanentStatsUiActive => permanentStatsUiActive;

        /// <summary>영구 스탯 UI가 열려 있을 때 마우스 클릭 좌표를 다음 프레임 입력 처리로 전달한다.</summary>
        public void NotifyPermanentStatsMouseClick(float gameX, float gameY)
        {
            pendingPermanentStatClickX = gameX;
            pendingPermanentStatClickY = gameY;
        }

        /// <summary>메인 메뉴에서 영구 스탯 입력(I 토글, 1~5 배분)을 처리한다.</summary>
        public void HandleMenuPermanentStatsInput()
        {
            HandlePermanentStatsInput();
        }

        /// <summary>영구 스탯 UI를 직접 그린다 (메뉴 오버레이용).</summary>
        public void RenderPermanentStatsOverlay(Renderer r)
        {
            DrawPermanentStatsUI(r);
        }

        private enum PermanentStatSlot
        {
            Health,
            MoveSpeed,
            Sense,
            PistolDamage,
            Luck
        }

        public void RecordRunResult()
        {
            if (currentFloor <= 0 || runResultRecorded) return;

            if (player != null && player.IsDead)
            {
                MarkRunEndedIfNeeded();
            }

            int seconds = GetCurrentRunDurationSeconds();
            var record = new RunRecord
            {
                FloorReached    = currentFloor,
                EnemiesKilled   = runEnemiesKilled,
                BossesKilled    = runBossesKilled,
                DurationSeconds = seconds,
                EndedAt         = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                IsVictory       = victory,
            };
            progressionRepository.SaveRunRecord(record);
            runResultRecorded = true;
        }

        public RunSummarySnapshot CreateRunSummarySnapshot()
        {
            if (player != null && player.IsDead)
            {
                MarkRunEndedIfNeeded();
            }

            return new RunSummarySnapshot(
                Math.Max(0, currentFloor),
                Math.Max(0, runEnemiesKilled),
                Math.Max(0, runBossesKilled),
                GetCurrentRunDurationSeconds());
        }

        private int GetCurrentRunDurationSeconds()
        {
            if (runStartTime == default)
            {
                return 0;
            }

            DateTime endTime = runEnded ? runEndedAtUtc : DateTime.UtcNow;
            double seconds = (endTime - runStartTime).TotalSeconds;
            if (seconds <= 0d)
            {
                return 0;
            }

            if (seconds >= int.MaxValue)
            {
                return int.MaxValue;
            }

            return (int)seconds;
        }

        private void MarkRunEndedIfNeeded()
        {
            if (runEnded)
            {
                return;
            }

            runEndedAtUtc = DateTime.UtcNow;
            runEnded = true;
        }

        public RunRecord[] LoadRunRecords(int limit = 20) => progressionRepository.LoadRunRecords(limit);

        public GameSettings LoadSettings() => progressionRepository.LoadSettings();

        public void SaveSettings(GameSettings settings) => progressionRepository.SaveSettings(settings);

        private string SerializeWeaponAmmoState()
        {
            string[] parts = new string[ownedWeapons.Length];
            for (int i = 0; i < ownedWeapons.Length; i++)
            {
                parts[i] = weapon.GetAmmo((WeaponType)i).ToString();
            }

            return string.Join(",", parts);
        }

        private string SerializeWeaponUpgradeState()
        {
            int weaponCount = ownedWeapons.Length;
            int categoryCount = Enum.GetValues(typeof(WeaponUpgradeCategory)).Length;
            string[] parts = new string[weaponCount * categoryCount];
            int index = 0;
            for (int weaponIndex = 0; weaponIndex < weaponCount; weaponIndex++)
            {
                for (int categoryIndex = 0; categoryIndex < categoryCount; categoryIndex++)
                {
                    parts[index++] = weapon.GetUpgradeLevel((WeaponType)weaponIndex, (WeaponUpgradeCategory)categoryIndex).ToString();
                }
            }

            return string.Join(",", parts);
        }

        private static string SerializeIntArray(int[] values)
        {
            if (values == null || values.Length == 0)
            {
                return string.Empty;
            }

            string[] parts = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                parts[i] = values[i].ToString();
            }

            return string.Join(",", parts);
        }

        private static void RestoreIntArray(string serialized, int[] destination, int defaultValue)
        {
            if (destination == null)
            {
                return;
            }

            for (int i = 0; i < destination.Length; i++)
            {
                destination[i] = defaultValue;
            }

            if (string.IsNullOrWhiteSpace(serialized))
            {
                return;
            }

            string[] parts = serialized.Split(',');
            int count = Math.Min(destination.Length, parts.Length);
            for (int i = 0; i < count; i++)
            {
                if (int.TryParse(parts[i], out int value))
                {
                    destination[i] = value;
                }
            }
        }

        private static int[] ParseIntArrayOrNull(string serialized, int expectedCount)
        {
            if (expectedCount <= 0 || string.IsNullOrWhiteSpace(serialized))
            {
                return null;
            }

            int[] values = new int[expectedCount];
            string[] parts = serialized.Split(',');
            int count = Math.Min(expectedCount, parts.Length);
            bool hasParsedValue = false;
            for (int i = 0; i < count; i++)
            {
                if (int.TryParse(parts[i], out int value))
                {
                    values[i] = value;
                    hasParsedValue = true;
                }
            }

            return hasParsedValue ? values : null;
        }

        private static int[,] ParseWeaponUpgradeState(string serialized, int weaponCount, int categoryCount)
        {
            int[] flat = ParseIntArrayOrNull(serialized, weaponCount * categoryCount);
            if (flat == null)
            {
                return null;
            }

            int[,] values = new int[weaponCount, categoryCount];
            int index = 0;
            for (int weaponIndex = 0; weaponIndex < weaponCount; weaponIndex++)
            {
                for (int categoryIndex = 0; categoryIndex < categoryCount; categoryIndex++)
                {
                    values[weaponIndex, categoryIndex] = flat[index++];
                }
            }

            return values;
        }

        private WeaponType GetSavedCurrentWeaponType(int currentWeaponType)
        {
            if (currentWeaponType < 0 || currentWeaponType >= ownedWeapons.Length)
            {
                return WeaponType.AMPistol;
            }

            if (!ownedWeapons[currentWeaponType])
            {
                return WeaponType.AMPistol;
            }

            return (WeaponType)currentWeaponType;
        }
    }
}
