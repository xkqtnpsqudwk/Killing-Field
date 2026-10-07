using System;
using System.Drawing;
using My2DEngine.Game.Config;
using My2DEngine.Game.Map;

namespace My2DEngine.Game.Core
{
    /// <summary>
    /// GameLogic의 스테이지 진행(Progression) partial.
    /// 방 활성화, 클리어 판정, 보상 아이템 생성·획득, 승리 조건 판정을 담당한다.
    /// 한 번에 하나의 활성 방(activeStageRoomIndex)만 진행시키는 것을 전제로 한다.
    /// </summary>
    public partial class GameLogic
    {
        /// <summary>
        /// 매 프레임 스테이지 진행 상태를 업데이트한다.
        /// <para>처리 흐름:</para>
        /// <list type="number">
        ///   <item>현재 활성 방이 유효한지 확인하고, 무효하면 인덱스를 -1로 초기화한다.</item>
        ///   <item>활성 방이 없으면 플레이어 위치가 어느 미활성 방 안에 있는지 확인하여 해당 방을 활성화한다.</item>
        ///   <item>활성 방의 살아있는 적이 모두 제거되면 방을 클리어 처리하고 보상을 생성한다.</item>
        ///   <item>보스 방 클리어 시 모든 보스 방이 클리어됐는지 확인하여 승리 여부를 결정한다.</item>
        /// </list>
        /// </summary>
        private void UpdateStageProgression(float dt)
        {
            if (!mapManager.HasStageFlow || player.IsDead)
            {
                return;
            }

            if (CardRewardRevealPending || cardRewardActive || branchSelectionActive || permanentStatsUiActive || endingSequenceActive)
            {
                return;
            }

            StageRoom[] rooms = mapManager.StageRooms;
            if (rooms == null || rooms.Length == 0)
            {
                return;
            }

            StageRoom activeRoom = null;
            if (activeStageRoomIndex >= 0 && activeStageRoomIndex < rooms.Length)
            {
                activeRoom = rooms[activeStageRoomIndex];
                if (activeRoom == null || !activeRoom.State.Activated || activeRoom.State.Cleared)
                {
                    activeRoom = null;
                    activeStageRoomIndex = -1;
                }
            }

            if (activeRoom == null)
            {
                for (int i = 0; i < rooms.Length; i++)
                {
                    StageRoom room = rooms[i];
                    if (room == null)
                    {
                        continue;
                    }

                    StageRoomState roomState = room.State;
                    if (roomState.Activated || roomState.Cleared)
                    {
                        continue;
                    }

                    if (IsInsideRoundStartTrigger(room))
                    {
                        ActivateStageRoom(i);
                        break;
                    }
                }
            }

            if (activeStageRoomIndex < 0 || activeStageRoomIndex >= rooms.Length)
            {
                return;
            }

            activeRoom = rooms[activeStageRoomIndex];
            StageRoomState activeState = activeRoom.State;
            if (!activeState.Activated || activeState.Cleared)
            {
                return;
            }

            if (activeRoom.IsRestRoom)
            {
                return;
            }

            UpdateActiveRoomHazard(activeRoom, activeState, dt);
            if (player.IsDead)
            {
                return;
            }

            UpdateSurvivalRoomReinforcements(activeRoom, activeState, dt);

            if (!IsCombatObjectiveComplete(activeRoom, activeState, dt))
            {
                return;
            }

            CompleteCombatRoom(activeRoom, activeState, clearRemainingEnemies: activeRoom.ObjectiveKind != RoomObjectiveKind.EliminateAll);
        }

        private bool IsCombatObjectiveComplete(StageRoom activeRoom, StageRoomState activeState, float dt)
        {
            switch (activeRoom.ObjectiveKind)
            {
                case RoomObjectiveKind.Survive:
                    // 생존 방은 적을 모두 잡는 것이 목표가 아니다.
                    // 타이머가 끝나면 남은 적을 정리하고 방을 클리어한다.
                    activeState.ObjectiveTimer = Math.Max(0f, activeState.ObjectiveTimer - Math.Max(0f, dt));
                    return activeState.ObjectiveTimer <= 0f;
                case RoomObjectiveKind.KeyTarget:
                    // 열쇠 방은 일반 적 수가 아니라 ObjectiveTarget 플래그가 붙은 표적만 본다.
                    return enemyManager.CountAliveObjectiveTargets() <= 0;
                default:
                    return enemyManager.CountAliveEnemies() <= 0;
            }
        }

        private void CompleteCombatRoom(StageRoom activeRoom, StageRoomState activeState, bool clearRemainingEnemies)
        {
            if (clearRemainingEnemies)
            {
                enemyManager.ClearActiveEncounter();
                playerProjectiles.Clear();
                StopLoopingWeaponEffects();
            }

            activeState.Cleared = true;
            activeState.Activated = false;
            activeState.RewardGranted = true;
            activeState.ObjectiveTimer = 0f;
            activeState.HazardTickTimer = 0f;
            activeState.ReinforcementTimer = 0f;
            activeStageRoomIndex = -1;

            // 로그라이크 모드: 카드 보상 후 출구 문으로 다음 층으로 이동한다.
            if (currentFloor > 0)
            {
                int nextFloor = currentFloor + 1;
                bool nextIsBoss = nextFloor % 20 == 0;

                if (activeRoom.IsBossRoom)
                {
                    // 보스 클리어 → 무기 카드 풀 +1, 영구 포인트 지급.
                    // 666층은 엔딩 연출 후 무한 모드 진입.
                    RegisterBossClearEnemyGrowth();
                    weaponCardPoolCount++;
                    GrantBossPermanentPoint();
                    if (IsFinalFloorBossClear())
                    {
                        BeginFinalEndingSequence();
                    }
                    else
                    {
                        SetStageStatus($"{currentFloor}층 보스 처치! 무기 카드 & 영구 포인트 획득! 다음 층 적 강화", 5.8f);
                        ShowCardReward(wasBossRoom: true, nextFloorIsBoss: nextIsBoss);
                    }
                }
                else
                {
                    int rewardGradeBoost = RoomTemplateLibrary.GetCardRewardGradeBoost(activeRoom);
                    int minimumRewardGrade = RoomTemplateLibrary.IsExtremeRewardRoom(activeRoom)
                        ? (int)GetOneGradeAboveHighestLuckAvailableStatOfferGrade()
                        : -1;
                    ShowCardReward(
                        wasBossRoom: false,
                        nextFloorIsBoss: nextIsBoss,
                        gradeBoost: rewardGradeBoost,
                        minimumStatGrade: minimumRewardGrade);
                }

                return;
            }

            // 기존 미로 모드
            if (activeRoom.IsBossRoom)
            {
                if (AreAllBossRoomsCleared())
                {
                    victory = true;
                    SetStageStatus("모든 보스 구역 확보 - 작전 완료", 6f);
                }
                else
                {
                    SetStageStatus("보스 격파 - 남은 구역을 수색하세요", 4f);
                }

                return;
            }

            SpawnRewardPickupForRoom(activeRoom);
        }

        private void UpdateActiveRoomHazard(StageRoom activeRoom, StageRoomState activeState, float dt)
        {
            if (activeRoom.HazardKind != RoomHazardKind.ToxicMist || player.IsDead)
            {
                return;
            }

            // 독 안개는 방 전체 효과이므로 특정 적/투사체 위치가 아니라 방 중앙을 피해 방향 기준으로 쓴다.
            activeState.HazardTickTimer -= Math.Max(0f, dt);
            if (activeState.HazardTickTimer > 0f)
            {
                return;
            }

            activeState.HazardTickTimer = GameConfig.ToxicMistDamageInterval;
            OnPlayerDamaged(GameConfig.ToxicMistDamage, activeRoom.Bounds.Left + activeRoom.Bounds.Width * 0.5f, activeRoom.Bounds.Top + activeRoom.Bounds.Height * 0.5f);
        }

        private void UpdateSurvivalRoomReinforcements(StageRoom activeRoom, StageRoomState activeState, float dt)
        {
            if (activeRoom.ObjectiveKind != RoomObjectiveKind.Survive)
            {
                return;
            }

            // 생존 방은 플레이어가 시간을 버티는 동안 일정 수의 적 압박을 유지한다.
            // 이미 목표 수 이상이면 타이머를 과도하게 누적하지 않도록 상한만 잡는다.
            int targetAlive = GameConfig.SurvivalRoomTargetAliveEnemies +
                (activeRoom.IsMiniBossRoom ? GameConfig.SurvivalRoomEliteTargetAliveBonus : 0);
            int alive = enemyManager.CountAliveEnemies();
            if (alive >= targetAlive)
            {
                activeState.ReinforcementTimer = Math.Min(activeState.ReinforcementTimer, GameConfig.SurvivalRoomReinforcementInterval);
                return;
            }

            activeState.ReinforcementTimer -= Math.Max(0f, dt);
            if (activeState.ReinforcementTimer > 0f)
            {
                return;
            }

            int spawnCount = Math.Min(GameConfig.SurvivalRoomReinforcementCount, targetAlive - alive);
            int spawned = enemyManager.SpawnStageReinforcements(activeRoom, collision, player.Position, spawnCount);
            activeState.ReinforcementTimer = spawned > 0
                ? GameConfig.SurvivalRoomReinforcementInterval
                : Math.Min(1.0f, GameConfig.SurvivalRoomReinforcementInterval * 0.35f);
        }

        /// <summary>
        /// 적이 사망했을 때 탄약 드랍을 판정한다.
        /// 소유한 무기 중 하나라도 탄약이 부족하면 공용 탄약 픽업을 생성한다.
        /// </summary>
        private void OnEnemyKilled(Enemy enemy)
        {
            if (enemy == null || enemy.IsIllusion || player.IsDead)
            {
                return;
            }

            runEnemiesKilled++;
            if (enemy.IsBoss || enemy.IsMiniBoss)
                runBossesKilled++;

            ApplyOnEnemyKilledRunStatBonuses();
            TrySpawnAmmoDrop(enemy);
            TrySpawnCoinDrop(enemy);
        }

        private void ApplyOnEnemyKilledRunStatBonuses()
        {
            if (player == null || player.IsDead)
            {
                return;
            }

            // 처치 회복은 최대 체력이 아니라 잃은 체력 기준이다.
            // 체력이 많이 깎인 상황일수록 가치가 커지지만 상한은 10%로 제한한다.
            float killHealRatio = Math.Max(0f, Math.Min(0.10f, GetRunStatBonus(StatType.KillHeal)));
            if (killHealRatio > 0f && player.Health < player.MaxHealth)
            {
                float missingHealth = Math.Max(0f, player.MaxHealth - player.Health);
                player.Health = Math.Min(player.MaxHealth, player.Health + missingHealth * killHealRatio);
            }

            float dashRefundRatio = Math.Max(0f, Math.Min(0.25f, GetRunStatBonus(StatType.KillDashCooldownRefund)));
            if (dashRefundRatio > 0f && player.DashCooldownTimer > 0f)
            {
                // 남은 쿨다운을 비율로 줄여 방금 대시한 직후일수록 환급량이 크다.
                player.DashCooldownTimer = Math.Max(0f, player.DashCooldownTimer * (1f - dashRefundRatio));
            }
        }

        private void TrySpawnAmmoDrop(Enemy enemy)
        {
            if (enemy == null || IsSupplyShortageRoomDropBlocked(enemy) || !HasAnyOwnedWeaponNeedingAmmo())
            {
                return;
            }

            WeaponType currentWeaponType = weapon.CurrentType;
            float baseChance = enemy.IsBoss
                ? GameConfig.BossAmmoDropChance
                : enemy.IsMiniBoss
                    ? GameConfig.MiniBossAmmoDropChance
                    : GameConfig.NormalEnemyAmmoDropChance;

            // 스탯 카드 '탄 드랍 확률' 보너스 적용
            float runBonus = GetRunStatBonus(StatType.AmmoDropChance);

            // 현재 무기의 AmmoDropBonus 업그레이드 적용
            float weaponBonus = 0f;
            if (currentWeaponType != WeaponType.AMPistol)
            {
                int upgradeLevel = weapon.GetUpgradeLevel(currentWeaponType, WeaponUpgradeCategory.AmmoDropBonus);
                if (upgradeLevel >= (int)CardGrade.Purple)      weaponBonus = 0.45f;
                else if (upgradeLevel >= (int)CardGrade.Blue)   weaponBonus = 0.25f;
            }

            float chance = Math.Min(1f, baseChance + runBonus + weaponBonus);

            if (rewardRandom.NextDouble() > chance)
            {
                return;
            }

            StageRoom room = FindStageRoomAtPosition(enemy.X, enemy.Y) ?? FindCurrentStageRoom();
            rewardPickups.Add(new RewardPickup
            {
                RoomId = room?.Id ?? -1,
                Kind = RewardPickupKind.AmmoPack,
                Rarity = RewardPickupRarity.None,
                EffectMultiplier = 1f,
                X = enemy.X,
                Y = enemy.Y,
                Active = true,
                PulseTimer = 0f
            });
            RegisterPickupToast("AMMO DROPPED");
        }

        private void TrySpawnCoinDrop(Enemy enemy)
        {
            if (enemy == null || currentFloor <= 0 || IsSupplyShortageRoomDropBlocked(enemy))
            {
                return;
            }

            bool isNormalEnemy = !enemy.IsBoss && !enemy.IsMiniBoss;
            float baseChance = enemy.IsBoss
                ? GameConfig.BossCoinDropChance
                : enemy.IsMiniBoss
                    ? GameConfig.MiniBossCoinDropChance
                    : GameConfig.NormalEnemyCoinDropChance;

            float runBonus = isNormalEnemy ? GetRunStatBonus(StatType.CoinDropChance) : 0f;
            float luckBonus = isNormalEnemy && permanentProgression != null
                ? permanentProgression.GetLuckLevel() * (GameConfig.LuckCoinDropBonusMax / 10f)
                : 0f;
            float chance = Math.Min(1f, baseChance + runBonus + luckBonus);
            if (rewardRandom.NextDouble() > chance)
            {
                return;
            }

            int coinAmount = enemy.IsBoss ? 3 : enemy.IsMiniBoss ? 2 : 1;
            StageRoom room = FindStageRoomAtPosition(enemy.X, enemy.Y) ?? FindCurrentStageRoom();
            rewardPickups.Add(new RewardPickup
            {
                RoomId = room?.Id ?? -1,
                Kind = RewardPickupKind.Coin,
                Rarity = RewardPickupRarity.None,
                EffectMultiplier = 1f,
                X = enemy.X,
                Y = enemy.Y,
                Active = true,
                PulseTimer = 0f,
                Amount = coinAmount
            });
            RegisterPickupToast(coinAmount > 1 ? "COIN x" + coinAmount + " DROPPED" : "COIN DROPPED");
        }

        private bool IsSupplyShortageRoomDropBlocked(Enemy enemy)
        {
            StageRoom room = enemy == null
                ? FindCurrentStageRoom()
                : FindStageRoomAtPosition(enemy.X, enemy.Y) ?? FindCurrentStageRoom();
            // 보급 부족 방은 전투 중 드랍을 막지만, 방 클리어 보상 등 다른 보상 체계까지 막지는 않는다.
            return room != null && room.HazardKind == RoomHazardKind.SupplyShortage && room.State.Activated && !room.State.Cleared;
        }

        /// <summary>
        /// 현재 소유한 무기 중 탄약이 부족한 무기가 하나라도 있는지 확인한다.
        /// </summary>
        private bool HasAnyOwnedWeaponNeedingAmmo()
        {
            for (int i = 0; i < ownedWeapons.Length; i++)
            {
                if (!ownedWeapons[i])
                {
                    continue;
                }

                if (weapon.NeedsAmmo((WeaponType)i))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 현재 무기에 맞는 기본 탄약 지급량을 반환한다.
        /// </summary>
        private int GetAmmoPickupAmount(WeaponType type, float effectMultiplier)
        {
            float clampedMultiplier = Math.Max(1f, effectMultiplier);
            return Math.Max(1, (int)Math.Ceiling(GetAmmoPickupBaseAmount(type) * clampedMultiplier));
        }

        /// <summary>
        /// 무기 종류에 맞는 기본 탄약 지급량을 반환한다.
        /// </summary>
        private int GetAmmoPickupBaseAmount(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.AMPistol:
                    return 12;
                case WeaponType.BearKiller:
                    return 2;
                case WeaponType.HChainGun:
                    return 18;
                case WeaponType.AutoCannon:
                    return 3;
                case WeaponType.DuelBerettas:
                    return 10;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// 지정한 인덱스의 스테이지 방을 활성화하고 해당 방의 조우(encounter) 적을 소환한다.
        /// 소환된 적이 없으면(데이터 오류 또는 빈 방) 즉시 클리어 처리한다.
        /// 방 종류(보스·정예·일반)에 따라 보스 인트로 타이머와 상태 메시지를 다르게 설정한다.
        /// </summary>
        /// <param name="roomIndex">활성화할 방의 <see cref="MapManager.StageRooms"/> 배열 인덱스.</param>
        private void ActivateStageRoom(int roomIndex)
        {
            StageRoom room = mapManager.StageRooms[roomIndex];
            StageRoomState state = room.State;
            state.Activated = true;
            state.ObjectiveTimer = 0f;
            state.HazardTickTimer = 0f;
            activeStageRoomIndex = roomIndex;

            if (controlsTutorialTimer <= 0f && permanentProgression != null && !permanentProgression.HasSeenControls)
            {
                controlsTutorialTimer = 6f;
                permanentProgression.HasSeenControls = true;
                progressionRepository?.Save(permanentProgression);
            }

            if (room.IsRestRoom)
            {
                SpawnRestRoomChoices(room);
                SetStageStatus("카드 상점 - 코인으로 고등급 카드 구매", 3.6f);
                return;
            }

            int spawned = enemyManager.SpawnStageEncounter(room, collision, player.Position);

            if (spawned <= 0)
            {
                state.Cleared = true;
                state.Activated = false;
                state.RewardGranted = true;
                activeStageRoomIndex = -1;

                if (currentFloor > 0)
                {
                    // 로그라이크 모드: 스폰 없는 방도 출구로 진행
                    SetStageStatus("[E] 다음 층으로", 3f);
                }
                else if (room.IsBossRoom)
                {
                    if (AreAllBossRoomsCleared())
                    {
                        victory = true;
                        SetStageStatus("작전 완료", 4f);
                    }
                    else
                    {
                        SetStageStatus("보스 구역 확보 - 다른 보스가 남아 있습니다", 3.5f);
                    }
                }
                else
                {
                    SpawnRewardPickupForRoom(room);
                }

                return;
            }

            InitializeActiveRoomObjective(room, state);

            if (room.IsBossRoom)
            {
                bossIntroTimer = GameConfig.BossIntroDuration;
                SetStageStatus(BuildRoomStartMessage(room), 3.5f);
            }
            else if (room.IsMiniBossRoom)
            {
                SetStageStatus(BuildRoomStartMessage(room), 3f);
            }
            else
            {
                SetStageStatus(BuildRoomStartMessage(room), 2.8f);
            }
        }

        private void InitializeActiveRoomObjective(StageRoom room, StageRoomState state)
        {
            if (room.ObjectiveKind == RoomObjectiveKind.Survive)
            {
                // 생존 방은 목표 시간과 첫 증원 타이머를 방 활성화 시점에 고정한다.
                state.ObjectiveTimer = Math.Max(1f, room.ObjectiveDuration);
                state.ReinforcementTimer = Math.Min(1.2f, GameConfig.SurvivalRoomReinforcementInterval);
            }
            else if (room.ObjectiveKind == RoomObjectiveKind.KeyTarget && enemyManager.CountAliveObjectiveTargets() <= 0)
            {
                // 데이터 오류나 스폰 실패로 표적이 없으면 진행이 막히지 않도록 일반 처치 방으로 폴백한다.
                room.ObjectiveKind = RoomObjectiveKind.EliminateAll;
            }

            if (room.HazardKind == RoomHazardKind.ToxicMist)
            {
                state.HazardTickTimer = GameConfig.ToxicMistDamageInterval;
            }
            else
            {
                state.HazardTickTimer = 0f;
            }
        }

        private string BuildRoomStartMessage(StageRoom room)
        {
            string objectiveText;
            switch (room.ObjectiveKind)
            {
                case RoomObjectiveKind.Survive:
                    objectiveText = "생존전 시작 - " + Math.Ceiling(Math.Max(1f, room.ObjectiveDuration)) + "초 버티기";
                    break;
                case RoomObjectiveKind.KeyTarget:
                    objectiveText = "열쇠 방 시작 - 은닉 표적 추적";
                    break;
                default:
                    objectiveText = room.IsBossRoom
                        ? "보스전 시작"
                        : room.IsMiniBossRoom
                            ? "정예전 시작"
                            : "라운드 시작 - 적 제거";
                    break;
            }

            if (room.HazardKind == RoomHazardKind.ToxicMist)
            {
                objectiveText += " / 독성 안개";
            }
            else if (room.HazardKind == RoomHazardKind.SupplyShortage)
            {
                objectiveText += " / 보급 부족";
            }

            return objectiveText;
        }

        /// <summary>
        /// 지정한 방의 중앙 전투 시작 트리거 존 안에 플레이어가 들어왔는지 확인한다.
        /// 플레이어가 문을 열고 방 안으로 진입한 뒤 중앙 쪽까지 이동해야 전투가 시작된다.
        /// </summary>
        private bool IsInsideRoundStartTrigger(StageRoom room)
        {
            if (room == null || room.State.Cleared || room.State.Activated)
            {
                return false;
            }

            // 방 경계 안으로 진입하는 순간 라운드 시작
            return room.Contains(player.Position.X, player.Position.Y);
        }

        /// <summary>
        /// 지정한 방에 보상 픽업 아이템을 하나 생성한다.
        /// 이미 같은 방 ID의 활성 픽업이 존재하면 중복 생성을 방지한다.
        /// 보상 종류와 희귀도는 방 ID와 방 타입을 기반으로 결정된다.
        /// </summary>
        /// <param name="room">보상을 생성할 스테이지 방. null이면 아무 동작도 하지 않는다.</param>
        private void SpawnRewardPickupForRoom(StageRoom room)
        {
            if (room == null)
            {
                return;
            }

            for (int i = 0; i < rewardPickups.Count; i++)
            {
                RewardPickup existing = rewardPickups[i];
                if (existing != null && existing.Active && existing.RoomId == room.Id)
                {
                    return;
                }
            }

            PointF spawn = FindRewardSpawnPoint(room);
            RewardPickupKind kind = DetermineRewardKind(room);
            RewardPickupRarity rarity = DetermineRewardRarity(room, kind);
            rewardPickups.Add(new RewardPickup
            {
                RoomId = room.Id,
                Kind = kind,
                Rarity = rarity,
                EffectMultiplier = GetRewardEffectMultiplier(kind, rarity),
                X = spawn.X,
                Y = spawn.Y,
                Active = true,
                PulseTimer = 0f
            });
        }

        /// <summary>
        /// 휴식 룸 진입 시 Luck 기반 고등급 카드 상점 선택지 3개를 배치한다.
        /// 이미 같은 방의 선택지가 있으면 중복 생성하지 않는다.
        /// </summary>
        private void SpawnRestRoomChoices(StageRoom room)
        {
            if (room == null)
            {
                return;
            }

            for (int i = 0; i < rewardPickups.Count; i++)
            {
                RewardPickup existing = rewardPickups[i];
                if (existing != null && existing.Active && existing.RoomId == room.Id)
                {
                    return;
                }
            }

            PointF[] spawnPoints = FindRestChoiceSpawnPoints(room);
            RewardCardOffer[] offers = GenerateRestShopCardOffers(3);

            for (int i = 0; i < offers.Length && i < spawnPoints.Length; i++)
            {
                RewardCardOffer offer = offers[i];
                CardGrade grade = offer.IsWeaponCard ? offer.WeaponGrade : offer.Grade;
                rewardPickups.Add(new RewardPickup
                {
                    RoomId = room.Id,
                    Kind = RewardPickupKind.Card,
                    Rarity = RewardPickupRarity.None,
                    EffectMultiplier = 1f,
                    X = spawnPoints[i].X,
                    Y = spawnPoints[i].Y,
                    Active = true,
                    PulseTimer = 0f,
                    IsRestChoice = true,
                    CoinCost = GetRestShopCardCost(grade),
                    CardOffer = offer
                });
            }
        }

        /// <summary>
        /// 휴식 룸 3지선다 픽업이 놓일 좌/중/우 지점을 계산한다.
        /// 후보 위치가 막혀 있으면 근처의 이동 가능한 타일로 보정한다.
        /// </summary>
        private PointF[] FindRestChoiceSpawnPoints(StageRoom room)
        {
            Rectangle bounds = room.Bounds;
            int centerX = bounds.Left + bounds.Width / 2;
            int centerY = bounds.Top + bounds.Height / 2;
            int spacing = Math.Max(2, bounds.Width / 6);

            return new[]
            {
                FindNearestWalkableRewardPoint(centerX - spacing, centerY, bounds),
                FindNearestWalkableRewardPoint(centerX, centerY, bounds),
                FindNearestWalkableRewardPoint(centerX + spacing, centerY, bounds)
            };
        }

        /// <summary>
        /// 선호 타일 좌표 주변에서 가장 가까운 이동 가능한 보상 배치 지점을 찾는다.
        /// </summary>
        private PointF FindNearestWalkableRewardPoint(int preferredX, int preferredY, Rectangle bounds)
        {
            int[,] map = mapManager.Map;
            if (map == null)
            {
                return new PointF(preferredX + 0.5f, preferredY + 0.5f);
            }

            int maxRadius = Math.Max(bounds.Width, bounds.Height);
            for (int radius = 0; radius <= maxRadius; radius++)
            {
                for (int y = preferredY - radius; y <= preferredY + radius; y++)
                {
                    for (int x = preferredX - radius; x <= preferredX + radius; x++)
                    {
                        if (Math.Abs(x - preferredX) != radius && Math.Abs(y - preferredY) != radius)
                        {
                            continue;
                        }

                        if (IsWalkableRewardTile(x, y, bounds, map))
                        {
                            return new PointF(x + 0.5f, y + 0.5f);
                        }
                    }
                }
            }

            return FindRewardSpawnPoint(new StageRoom { Bounds = bounds });
        }

        /// <summary>
        /// 방의 중심에서 나선형으로 바깥쪽을 탐색하여 보상 아이템을 놓을 최적 위치를 찾는다.
        /// <list type="bullet">
        ///   <item>1단계: 주변 8타일이 모두 이동 가능한 "안전" 타일을 우선 탐색한다.</item>
        ///   <item>2단계: 안전 타일이 없으면 이동 가능한 타일 아무 곳에나 배치한다.</item>
        ///   <item>3단계: 이동 가능한 타일도 없으면 방 중심 좌표를 반환한다.</item>
        /// </list>
        /// </summary>
        /// <param name="room">보상을 생성할 스테이지 방.</param>
        /// <returns>보상 아이템의 월드 좌표(타일 중심, 즉 타일 좌표 + 0.5).</returns>
        private PointF FindRewardSpawnPoint(StageRoom room)
        {
            int[,] map = mapManager.Map;
            Rectangle bounds = room.Bounds;
            int centerX = bounds.Left + bounds.Width / 2;
            int centerY = bounds.Top + bounds.Height / 2;
            int maxRadius = Math.Max(bounds.Width, bounds.Height);

            for (int radius = 0; radius <= maxRadius; radius++)
            {
                for (int y = centerY - radius; y <= centerY + radius; y++)
                {
                    for (int x = centerX - radius; x <= centerX + radius; x++)
                    {
                        if (Math.Abs(x - centerX) != radius && Math.Abs(y - centerY) != radius)
                        {
                            continue;
                        }

                        if (IsSafeRewardTile(x, y, bounds, map))
                        {
                            return new PointF(x + 0.5f, y + 0.5f);
                        }
                    }
                }
            }

            for (int radius = 0; radius <= maxRadius; radius++)
            {
                for (int y = centerY - radius; y <= centerY + radius; y++)
                {
                    for (int x = centerX - radius; x <= centerX + radius; x++)
                    {
                        if (Math.Abs(x - centerX) != radius && Math.Abs(y - centerY) != radius)
                        {
                            continue;
                        }

                        if (IsWalkableRewardTile(x, y, bounds, map))
                        {
                            return new PointF(x + 0.5f, y + 0.5f);
                        }
                    }
                }
            }

            return new PointF(centerX + 0.5f, centerY + 0.5f);
        }

        /// <summary>
        /// 지정한 타일이 보상 배치에 "안전"한지 확인한다.
        /// 해당 타일 자체가 이동 가능하고, 상하좌우·대각선 8방향 타일도 모두 이동 가능해야 한다.
        /// </summary>
        /// <param name="x">검사할 타일의 X 좌표.</param>
        /// <param name="y">검사할 타일의 Y 좌표.</param>
        /// <param name="bounds">방의 경계 사각형. 벽 인접 여부 확인에 사용된다.</param>
        /// <param name="map">맵 타일 배열. 0이 빈 공간이다.</param>
        /// <returns>해당 타일과 주변 8타일이 모두 이동 가능하면 true.</returns>
        private bool IsSafeRewardTile(int x, int y, Rectangle bounds, int[,] map)
        {
            if (!IsWalkableRewardTile(x, y, bounds, map))
            {
                return false;
            }

            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if (offsetX == 0 && offsetY == 0)
                    {
                        continue;
                    }

                    int checkX = x + offsetX;
                    int checkY = y + offsetY;
                    if (!IsWalkableRewardTile(checkX, checkY, bounds, map))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// 지정한 타일이 보상 아이템을 놓을 수 있는 이동 가능한 타일인지 확인한다.
        /// 방 경계에서 2타일 이상 안쪽에 있어야 하며, 맵 범위 내의 빈 공간(값 0)이어야 한다.
        /// </summary>
        /// <param name="x">검사할 타일의 X 좌표.</param>
        /// <param name="y">검사할 타일의 Y 좌표.</param>
        /// <param name="bounds">방의 경계 사각형. 경계 근접 여부 확인에 사용된다.</param>
        /// <param name="map">맵 타일 배열. null이면 false를 반환한다.</param>
        /// <returns>이동 가능한 보상 배치 타일이면 true.</returns>
        private bool IsWalkableRewardTile(int x, int y, Rectangle bounds, int[,] map)
        {
            if (map == null)
            {
                return false;
            }

            if (x <= bounds.Left + 1 || x >= bounds.Right - 2 || y <= bounds.Top + 1 || y >= bounds.Bottom - 2)
            {
                return false;
            }

            if (x < 0 || y < 0 || x >= map.GetLength(0) || y >= map.GetLength(1))
            {
                return false;
            }

            return map[x, y] == 0;
        }

        /// <summary>
        /// 방 ID를 기반으로 전투 방 클리어 보상의 종류를 결정한다.
        /// 회복/스팀 보상은 제거되어 전투 방 보상은 탄약 보급만 생성한다.
        /// </summary>
        /// <param name="room">보상 종류를 결정할 스테이지 방.</param>
        /// <returns>결정된 보상 픽업 종류.</returns>
        private RewardPickupKind DetermineRewardKind(StageRoom room)
        {
            return RewardPickupKind.AmmoPack;
        }

        /// <summary>
        /// 방 타입과 보상 종류를 기반으로 보상의 희귀도를 무작위로 결정한다.
        /// 코인과 카드는 항상 <see cref="RewardPickupRarity.None"/>을 반환한다.
        /// 보스 방일수록 Epic·Rare 확률이 높고, 일반 방일수록 낮다.
        /// </summary>
        /// <param name="room">희귀도 판정에 사용할 스테이지 방(보스 여부 확인).</param>
        /// <param name="kind">보상 종류.</param>
        /// <returns>결정된 희귀도.</returns>
        private RewardPickupRarity DetermineRewardRarity(StageRoom room, RewardPickupKind kind)
        {
            if (kind == RewardPickupKind.Coin || kind == RewardPickupKind.Card)
            {
                return RewardPickupRarity.None;
            }

            int roll = rewardRandom.Next(100);
            if (room != null && room.IsBossRoom)
            {
                if (roll < 45) return RewardPickupRarity.Epic;
                if (roll < 100) return RewardPickupRarity.Rare;
            }
            else if (room != null && room.IsMiniBossRoom)
            {
                if (roll < 20) return RewardPickupRarity.Epic;
                if (roll < 65) return RewardPickupRarity.Rare;
            }
            else
            {
                if (roll < 10) return RewardPickupRarity.Epic;
                if (roll < 35) return RewardPickupRarity.Rare;
            }

            return RewardPickupRarity.Common;
        }

        /// <summary>
        /// 보상 종류와 희귀도를 기반으로 효과 배율을 계산한다.
        /// 코인, 카드이거나 희귀도가 None이면 기본 배율 1.0을 반환한다.
        /// </summary>
        /// <param name="kind">보상 종류.</param>
        /// <param name="rarity">보상 희귀도.</param>
        /// <returns>
        /// 효과 배율. 예를 들어 Rare 탄약은 1.8배, Epic 탄약은 2.2배의 효과를 가진다.
        /// </returns>
        private float GetRewardEffectMultiplier(RewardPickupKind kind, RewardPickupRarity rarity)
        {
            if (kind == RewardPickupKind.Coin || kind == RewardPickupKind.Card || rarity == RewardPickupRarity.None)
            {
                return 1f;
            }

            switch (rarity)
            {
                case RewardPickupRarity.Rare:
                    return 1.8f;
                case RewardPickupRarity.Epic:
                    return 2.2f;
                default:
                    return 1.5f;
            }
        }

        /// <summary>
        /// 보상 픽업 목록을 순회하여 각 픽업의 애니메이션 타이머를 갱신한다.
        /// 비활성화된 픽업은 목록에서 제거한다.
        /// </summary>
        /// <param name="dt">이번 프레임의 경과 시간(초).</param>
        private void UpdatePickupTimers(float dt)
        {
            for (int i = rewardPickups.Count - 1; i >= 0; i--)
            {
                RewardPickup pickup = rewardPickups[i];
                if (pickup == null || !pickup.Active)
                {
                    rewardPickups.RemoveAt(i);
                    continue;
                }

                pickup.Update(dt);
            }
        }

        /// <summary>
        /// 플레이어 위치에 있는 보상 픽업을 획득 처리한다.
        /// 픽업 반경(<see cref="GameConfig.PickupRadius"/>) 이내의 픽업을 즉시 적용하고 목록에서 제거한다.
        /// </summary>
        private void CollectRewardPickups()
        {
            if (rewardPickups.Count == 0)
            {
                return;
            }

            for (int i = rewardPickups.Count - 1; i >= 0; i--)
            {
                RewardPickup pickup = rewardPickups[i];
                if (pickup == null || !pickup.Active)
                {
                    rewardPickups.RemoveAt(i);
                    continue;
                }

                float dx = pickup.X - player.Position.X;
                float dy = pickup.Y - player.Position.Y;
                float pickupRadius = GameConfig.PickupRadius;
                if ((dx * dx) + (dy * dy) > pickupRadius * pickupRadius)
                {
                    continue;
                }

                if (!ApplyRewardPickup(pickup))
                {
                    continue;
                }

                if (pickup.IsRestChoice)
                {
                    int roomId = pickup.RoomId;
                    pickup.Active = false;
                    rewardPickups.RemoveAt(i);
                    if (!HasActiveRestChoice(roomId))
                    {
                        CompleteRestRoomChoice(roomId, purchased: true);
                    }
                    break;
                }

                pickup.Active = false;
                rewardPickups.RemoveAt(i);
            }
        }

        /// <summary>
        /// 휴식 룸 상점을 떠날 때 남은 선택지를 제거하고 분기 선택으로 진행한다.
        /// </summary>
        private void CompleteRestRoomChoice(int roomId, bool purchased = true)
        {
            for (int i = rewardPickups.Count - 1; i >= 0; i--)
            {
                RewardPickup pickup = rewardPickups[i];
                if (pickup != null && pickup.RoomId == roomId)
                {
                    rewardPickups.RemoveAt(i);
                }
            }

            StageRoom room = GetStageRoom(roomId);
            if (room != null)
            {
                room.State.Cleared = true;
                room.State.Activated = false;
                room.State.RewardGranted = true;
            }

            if (activeStageRoomIndex >= 0 &&
                activeStageRoomIndex < mapManager.StageRooms.Length &&
                mapManager.StageRooms[activeStageRoomIndex]?.Id == roomId)
            {
                activeStageRoomIndex = -1;
            }

            ShowBranchSelection();
        }

        /// <summary>
        /// 보상 픽업 종류에 따라 플레이어 또는 무기에 효과를 적용한다.
        /// <list type="bullet">
        ///   <item>AmmoPack: 현재 들고 있는 무기의 탄약을 즉시 보충한다.</item>
        ///   <item>Card: 휴식 상점 구매 카드 효과를 즉시 적용한다.</item>
        /// </list>
        /// </summary>
        /// <param name="pickup">획득한 보상 픽업 객체.</param>
        private bool ApplyRewardPickup(RewardPickup pickup)
        {
            if (pickup == null)
            {
                return false;
            }

            float effectMultiplier = Math.Max(1f, pickup.EffectMultiplier);
            switch (pickup.Kind)
            {
                case RewardPickupKind.AmmoPack:
                    WeaponType currentWeaponType = weapon.CurrentType;
                    int ammoAmount = GetAmmoPickupAmount(currentWeaponType, effectMultiplier);
                    int ammoCapacity = weapon.GetMaxAmmo(currentWeaponType) - weapon.GetAmmo(currentWeaponType);
                    int addedAmmo = Math.Max(0, Math.Min(ammoAmount, ammoCapacity));
                    if (addedAmmo <= 0)
                    {
                        if (pickup.IsRestChoice)
                        {
                            ShowRestShopBlockMessage("현재 무기 탄약이 가득 찼습니다");
                        }
                        return false;
                    }

                    if (!TrySpendRestShopCost(pickup))
                    {
                        return false;
                    }

                    weapon.AddAmmoToCurrentWeapon(ammoAmount);
                    RegisterPickupToast(pickup.IsRestChoice ? "AMMO BOUGHT" : "AMMO +" + addedAmmo);
                    return true;
                case RewardPickupKind.Card:
                    if (pickup.CardOffer == null)
                    {
                        return false;
                    }

                    if (!TrySpendRestShopCost(pickup))
                    {
                        return false;
                    }

                    ApplyCardOffer(pickup.CardOffer);
                    RegisterPickupToast("CARD BOUGHT");
                    return true;
                case RewardPickupKind.Coin:
                    int coinAmount = Math.Max(1, pickup.Amount);
                    player.AddCoins(coinAmount);
                    RegisterPickupToast("COIN +" + coinAmount);
                    return true;
                default:
                    return false;
            }
        }

        private int GetRestShopCardCost(CardGrade grade)
        {
            int gradeCost = Math.Max(0, (int)grade) * GameConfig.RestShopCardCostPerGrade;
            int baseCost = GameConfig.RestShopCardBaseCost +
                gradeCost +
                Math.Max(0, bossClearGrowthCount) * GameConfig.RestShopCostIncreasePerBossClear;
            float discount = Math.Max(0f, Math.Min(0.50f, GetRunStatBonus(StatType.ShopDiscount)));
            return Math.Max(1, (int)Math.Ceiling(baseCost * (1f - discount)));
        }

        private bool TrySpendRestShopCost(RewardPickup pickup)
        {
            if (pickup == null || !pickup.IsRestChoice)
            {
                return true;
            }

            int cost = Math.Max(0, pickup.CoinCost);
            if (player.TrySpendCoins(cost))
            {
                return true;
            }

            ShowRestShopBlockMessage($"코인이 부족합니다 ({cost} 필요)");
            return false;
        }

        private void ShowRestShopBlockMessage(string message)
        {
            if (!string.IsNullOrWhiteSpace(message) &&
                (!string.Equals(stageStatusMessage, message, StringComparison.Ordinal) || stageStatusTimer <= 0.15f))
            {
                SetStageStatus(message, 1.6f);
            }
        }

        private bool TrySkipActiveRestRoom()
        {
            if (activeStageRoomIndex < 0 || mapManager.StageRooms == null || activeStageRoomIndex >= mapManager.StageRooms.Length)
            {
                return false;
            }

            StageRoom room = mapManager.StageRooms[activeStageRoomIndex];
            if (room == null || !room.IsRestRoom || !room.State.Activated || room.State.Cleared)
            {
                return false;
            }

            CompleteRestRoomChoice(room.Id, purchased: false);
            return true;
        }

        private bool HasAffordableRestChoice(int roomId)
        {
            for (int i = 0; i < rewardPickups.Count; i++)
            {
                RewardPickup pickup = rewardPickups[i];
                if (pickup == null || !pickup.Active || !pickup.IsRestChoice || pickup.RoomId != roomId)
                {
                    continue;
                }

                if (player.CoinCount >= Math.Max(0, pickup.CoinCost))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasActiveRestChoice(int roomId)
        {
            for (int i = 0; i < rewardPickups.Count; i++)
            {
                RewardPickup pickup = rewardPickups[i];
                if (pickup != null && pickup.Active && pickup.IsRestChoice && pickup.RoomId == roomId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 보상 획득 메시지를 구성한다.
        /// 희귀도 라벨이 있으면 "라벨 기본메시지" 형식으로, 없으면 기본 메시지만 반환한다.
        /// </summary>
        /// <param name="pickup">희귀도 라벨을 읽을 보상 픽업 객체.</param>
        /// <param name="baseMessage">기본 메시지 문자열(예: "의료 보급 확보").</param>
        /// <returns>희귀도 라벨이 포함된 최종 메시지 문자열.</returns>
        private string BuildRewardPickupMessage(RewardPickup pickup, string baseMessage)
        {
            if (pickup == null || pickup.Rarity == RewardPickupRarity.None)
            {
                return baseMessage;
            }

            string label = pickup.GetRarityLabel();
            return string.IsNullOrWhiteSpace(label)
                ? baseMessage
                : label + " " + baseMessage;
        }

        /// <summary>
        /// 방 ID로 스테이지 방 객체를 빠르게 조회한다.
        /// 내부적으로 <see cref="stageRoomLookup"/> 딕셔너리를 사용하여 O(1) 탐색을 수행한다.
        /// </summary>
        /// <param name="roomId">조회할 방의 ID.</param>
        /// <returns>해당 ID의 <see cref="StageRoom"/>. 없으면 null.</returns>
        private StageRoom GetStageRoom(int roomId)
        {
            return stageRoomLookup.TryGetValue(roomId, out StageRoom room)
                ? room
                : null;
        }

        /// <summary>
        /// 지정 좌표를 포함하는 스테이지 방을 찾아 반환한다.
        /// </summary>
        private StageRoom FindStageRoomAtPosition(float x, float y)
        {
            StageRoom[] rooms = mapManager.StageRooms;
            if (rooms == null)
            {
                return null;
            }

            for (int i = 0; i < rooms.Length; i++)
            {
                StageRoom room = rooms[i];
                if (room != null && room.Contains(x, y))
                {
                    return room;
                }
            }

            return null;
        }

        /// <summary>
        /// 맵의 모든 보스 방이 클리어되었는지 확인한다.
        /// 보스 방이 하나도 없으면 false를 반환한다(맵 데이터 오류 방어).
        /// </summary>
        /// <returns>보스 방이 하나 이상 존재하고 모두 클리어됐으면 true, 그렇지 않으면 false.</returns>
        private bool AreAllBossRoomsCleared()
        {
            StageRoom[] rooms = mapManager.StageRooms;
            if (rooms == null)
            {
                return false;
            }

            bool hasBossRoom = false;
            for (int i = 0; i < rooms.Length; i++)
            {
                StageRoom room = rooms[i];
                if (room == null || !room.IsBossRoom)
                {
                    continue;
                }

                hasBossRoom = true;
                if (!room.State.Cleared)
                {
                    return false;
                }
            }

            return hasBossRoom;
        }
    }
}
