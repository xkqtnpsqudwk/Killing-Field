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
        private const float NormalEnemyAmmoDropChance = 0.28f;
        private const float MiniBossAmmoDropChance = 0.65f;
        private const float BossAmmoDropChance = 0.9f;
        private const float NormalEnemyCoinDropChance = 0.10f;
        private const float MiniBossCoinDropChance = 0.25f;
        private const float BossCoinDropChance = 0.50f;

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
        private void UpdateStageProgression()
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

            if (enemyManager.CountAliveEnemies() > 0)
            {
                return;
            }

            activeState.Cleared = true;
            activeState.Activated = false;
            activeState.RewardGranted = true;
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
                    if (activeRoom.IsMiniBossRoom)
                    {
                        // 정예 처치 → 스탯 카드만 제공
                        SetStageStatus("정예 처치!", 2.4f);
                    }
                    else
                    {
                        SetStageStatus("방 클리어!", 2.2f);
                    }

                    ShowCardReward(wasBossRoom: false, nextFloorIsBoss: nextIsBoss);
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
            SetStageStatus("방 클리어 - 연결된 문을 열 수 있습니다", 3.5f);
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

            TrySpawnAmmoDrop(enemy);
            TrySpawnCoinDrop(enemy);
        }

        private void TrySpawnAmmoDrop(Enemy enemy)
        {
            if (enemy == null || !HasAnyOwnedWeaponNeedingAmmo())
            {
                return;
            }

            WeaponType currentWeaponType = weapon.CurrentType;
            float baseChance = enemy.IsBoss
                ? BossAmmoDropChance
                : enemy.IsMiniBoss
                    ? MiniBossAmmoDropChance
                    : NormalEnemyAmmoDropChance;

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
        }

        private void TrySpawnCoinDrop(Enemy enemy)
        {
            if (enemy == null || currentFloor <= 0)
            {
                return;
            }

            bool isNormalEnemy = !enemy.IsBoss && !enemy.IsMiniBoss;
            float baseChance = enemy.IsBoss
                ? BossCoinDropChance
                : enemy.IsMiniBoss
                    ? MiniBossCoinDropChance
                    : NormalEnemyCoinDropChance;

            float runBonus = isNormalEnemy ? GetRunStatBonus(StatType.CoinDropChance) : 0f;
            float chance = Math.Min(1f, baseChance + runBonus);
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
            activeStageRoomIndex = roomIndex;

            if (room.IsRestRoom)
            {
                SpawnRestRoomChoices(room);
                SetStageStatus("휴식 상점 - 코인으로 아이템 구매", 3.6f);
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

            if (room.IsBossRoom)
            {
                bossIntroTimer = GameConfig.BossIntroDuration;
                SetStageStatus("보스전 시작", 3.5f);
            }
            else if (room.IsMiniBossRoom)
            {
                SetStageStatus("정예전 시작", 3f);
            }
            else
            {
                SetStageStatus("라운드 시작 - 적 제거", 2.8f);
            }
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
        /// 휴식 룸 진입 시 체력팩, 스팀팩, 탄약 3개 선택지를 배치한다.
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
            RewardPickupKind[] choices =
            {
                RewardPickupKind.HealthPack,
                RewardPickupKind.StimPack,
                RewardPickupKind.AmmoPack
            };

            for (int i = 0; i < choices.Length; i++)
            {
                rewardPickups.Add(new RewardPickup
                {
                    RoomId = room.Id,
                    Kind = choices[i],
                    Rarity = RewardPickupRarity.None,
                    EffectMultiplier = 1f,
                    X = spawnPoints[i].X,
                    Y = spawnPoints[i].Y,
                    Active = true,
                    PulseTimer = 0f,
                    IsRestChoice = true,
                    CoinCost = GetRestShopCost(choices[i])
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
        /// 방 ID를 기반으로 이 방에서 획득할 보상의 종류를 결정한다.
        /// 방 ID를 3으로 나눈 나머지에 따라 체력팩·스팀팩·탄약 순으로 순환한다.
        /// </summary>
        /// <param name="room">보상 종류를 결정할 스테이지 방.</param>
        /// <returns>결정된 보상 픽업 종류.</returns>
        private RewardPickupKind DetermineRewardKind(StageRoom room)
        {
            int rewardIndex = (room.Id - 1) % 3;
            switch (rewardIndex)
            {
                case 0:
                    return RewardPickupKind.HealthPack;
                case 1:
                    return RewardPickupKind.StimPack;
                default:
                    return RewardPickupKind.AmmoPack;
            }
        }

        /// <summary>
        /// 방 타입과 보상 종류를 기반으로 보상의 희귀도를 무작위로 결정한다.
        /// 스팀팩은 항상 <see cref="RewardPickupRarity.None"/>을 반환한다.
        /// 보스 방일수록 Epic·Rare 확률이 높고, 일반 방일수록 낮다.
        /// </summary>
        /// <param name="room">희귀도 판정에 사용할 스테이지 방(보스 여부 확인).</param>
        /// <param name="kind">보상 종류. StimPack이면 희귀도 판정을 건너뛴다.</param>
        /// <returns>결정된 희귀도.</returns>
        private RewardPickupRarity DetermineRewardRarity(StageRoom room, RewardPickupKind kind)
        {
            if (kind == RewardPickupKind.StimPack || kind == RewardPickupKind.Coin)
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
        /// 스팀팩이거나 희귀도가 None이면 기본 배율 1.0을 반환한다.
        /// </summary>
        /// <param name="kind">보상 종류.</param>
        /// <param name="rarity">보상 희귀도.</param>
        /// <returns>
        /// 효과 배율. 예를 들어 Rare 체력팩은 1.45배, Epic 탄약은 2.2배의 효과를 가진다.
        /// </returns>
        private float GetRewardEffectMultiplier(RewardPickupKind kind, RewardPickupRarity rarity)
        {
            if (kind == RewardPickupKind.StimPack || kind == RewardPickupKind.Coin || rarity == RewardPickupRarity.None)
            {
                return 1f;
            }

            switch (rarity)
            {
                case RewardPickupRarity.Rare:
                    return kind == RewardPickupKind.HealthPack ? 1.45f : 1.8f;
                case RewardPickupRarity.Epic:
                    return kind == RewardPickupKind.HealthPack ? 1.9f : 2.2f;
                default:
                    return kind == RewardPickupKind.HealthPack ? 1.0f : 1.5f;
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
                    CompleteRestRoomChoice(pickup.RoomId, purchased: true);
                    break;
                }

                pickup.Active = false;
                rewardPickups.RemoveAt(i);
            }
        }

        /// <summary>
        /// 휴식 룸에서 하나의 선택지를 획득했을 때 나머지 선택지를 제거하고 분기 선택으로 진행한다.
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

            SetStageStatus(purchased ? "휴식 구매 완료 - 다음 룸 선택" : "휴식 종료 - 다음 룸 선택", 3f);
            ShowBranchSelection();
        }

        /// <summary>
        /// 보상 픽업 종류에 따라 플레이어 또는 무기에 효과를 적용한다.
        /// <list type="bullet">
        ///   <item>HealthPack: 최대 체력 한도 내에서 체력을 회복한다.</item>
        ///   <item>StimPack: 스팀팩 보유 수를 1 증가시킨다.</item>
        ///   <item>AmmoPack: 현재 들고 있는 무기의 탄약을 즉시 보충한다.</item>
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
                case RewardPickupKind.HealthPack:
                    if (pickup.IsRestChoice && player.Health >= player.MaxHealth - 0.01f)
                    {
                        ShowRestShopBlockMessage("체력이 가득 찼습니다");
                        return false;
                    }

                    if (!TrySpendRestShopCost(pickup))
                    {
                        return false;
                    }

                    player.Health = Math.Min(player.MaxHealth, player.Health + GameConfig.HealthPickupAmount * effectMultiplier);
                    SetStageStatus(
                        pickup.IsRestChoice
                            ? BuildRestShopPurchaseMessage(pickup, "회복 키트 구매")
                            : BuildRewardPickupMessage(pickup, "의료 보급 확보"),
                        2.4f);
                    return true;
                case RewardPickupKind.StimPack:
                    if (!TrySpendRestShopCost(pickup))
                    {
                        return false;
                    }

                    player.AddStimPack(1);
                    SetStageStatus(
                        pickup.IsRestChoice
                            ? BuildRestShopPurchaseMessage(pickup, "스팀팩 구매")
                            : "스팀팩 획득",
                        2.4f);
                    return true;
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
                    string weaponLabel = WeaponPresentation.GetDisplayName(currentWeaponType);
                    SetStageStatus(
                        pickup.IsRestChoice
                            ? BuildRestShopPurchaseMessage(pickup, $"{weaponLabel} 탄약 +{addedAmmo}")
                            : BuildRewardPickupMessage(pickup, $"{weaponLabel} 탄약 +{addedAmmo}"),
                        2.2f);
                    return true;
                case RewardPickupKind.Coin:
                    int coinAmount = Math.Max(1, pickup.Amount);
                    player.AddCoins(coinAmount);
                    SetStageStatus($"코인 +{coinAmount}", 1.8f);
                    return true;
                default:
                    return false;
            }
        }

        private int GetRestShopCost(RewardPickupKind kind)
        {
            int baseCost;
            switch (kind)
            {
                case RewardPickupKind.HealthPack:
                    baseCost = GameConfig.RestShopHealthPackCost;
                    break;
                case RewardPickupKind.StimPack:
                    baseCost = GameConfig.RestShopStimPackCost;
                    break;
                case RewardPickupKind.AmmoPack:
                    baseCost = GameConfig.RestShopAmmoPackCost;
                    break;
                default:
                    return 0;
            }

            return baseCost + Math.Max(0, bossClearGrowthCount) * GameConfig.RestShopCostIncreasePerBossClear;
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

        private string BuildRestShopPurchaseMessage(RewardPickup pickup, string itemText)
        {
            int cost = pickup?.CoinCost ?? 0;
            return cost > 0
                ? $"{itemText} (-{cost} 코인)"
                : itemText;
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
