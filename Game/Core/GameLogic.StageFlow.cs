using System;
using System.Drawing;
using My2DEngine.Game.Config;
using My2DEngine.Game.Map;

namespace My2DEngine.Game.Core
{
    /// <summary>
    /// GameLogic의 스테이지 흐름 제어 partial.
    /// 문 상호작용, 스테이지 상태 메시지·타이머 갱신, 보스 인트로 타이머 관리,
    /// 스테이지 전체 상태 초기화를 담당한다.
    /// </summary>
    public partial class GameLogic
    {
        /// <summary>
        /// 플레이어 근처에 상호작용 가능한 문이 있으면 해당 문의 열림 애니메이션을 시작한다.
        /// 실제 문 탐색은 <see cref="TryGetInteractableDoor"/>가 수행하며,
        /// 성공 시 문 열림 사운드와 상태 메시지를 표시한다.
        /// </summary>
        private void TryOpenNearbyDoor()
        {
            if (!TryGetInteractableDoor(out Point door, out _))
            {
                return;
            }

            if (mapManager.BeginDoorOpening(door))
            {
                audio.PlayEffect(AudioConfig.DoorSoundAlias, true);
                EmitEnemyAlertSound(door.X + 0.5f, door.Y + 0.5f, 7.5f);
                SetStageStatus("연결 문 개방", 1.8f);
            }
        }

        /// <summary>
        /// 클리어된 방의 연결 문 중 플레이어와 가장 가까운 상호작용 가능한 문을 찾는다.
        /// 문이 닫혀 있고 목표 방이 존재하며 거리가 <see cref="WorldConfig.DoorInteractDistance"/> 이내일 때만 유효하다.
        /// </summary>
        /// <param name="interactDoor">찾은 문의 타일 좌표. 없으면 <see cref="Point.Empty"/>.</param>
        /// <param name="nextRoom">문 너머의 목표 스테이지 방. 없으면 null.</param>
        /// <returns>상호작용 가능한 문을 찾았으면 true, 없으면 false.</returns>
        private bool TryGetInteractableDoor(out Point interactDoor, out StageRoom nextRoom)
        {
            interactDoor = Point.Empty;
            nextRoom = null;
            StageRoom[] rooms = mapManager.StageRooms;
            if (rooms == null || rooms.Length == 0)
            {
                return false;
            }

            float bestDistSq = WorldConfig.DoorInteractDistance * WorldConfig.DoorInteractDistance;
            for (int i = 0; i < rooms.Length; i++)
            {
                StageRoom room = rooms[i];
                if (room == null || !room.State.Cleared)
                {
                    continue;
                }

                StageDoorConnection[] connections = room.Connections;
                if (connections == null)
                {
                    continue;
                }

                for (int j = 0; j < connections.Length; j++)
                {
                    StageDoorConnection connection = connections[j];
                    if (connection == null || !mapManager.IsDoorClosed(connection.Door))
                    {
                        continue;
                    }

                    StageRoom targetRoom = GetStageRoom(connection.TargetRoomId);
                    if (targetRoom == null)
                    {
                        continue;
                    }

                    float doorX = connection.Door.X + 0.5f;
                    float doorY = connection.Door.Y + 0.5f;
                    float dx = doorX - player.Position.X;
                    float dy = doorY - player.Position.Y;
                    float distSq = (dx * dx) + (dy * dy);
                    if (distSq > bestDistSq)
                    {
                        continue;
                    }

                    bestDistSq = distSq;
                    interactDoor = connection.Door;
                    nextRoom = targetRoom;
                }
            }

            return nextRoom != null;
        }

        /// <summary>
        /// 현재 상호작용 가능한 문이 있는지 확인하여 화면 안내 문구를 갱신한다.
        /// 로그라이크 모드의 층 출구 문이 우선 표시된다.
        /// </summary>
        private void UpdateInteractPrompt()
        {
            // 카드 연출/선택 또는 분기 선택 UI가 활성화된 동안에는 안내 문구를 숨긴다.
            if (IsStageFlowPaused)
            {
                interactPromptText = null;
                return;
            }

            if (endingSequenceCompleted && currentFloor == FinalRoguelikeFloor && TryGetFloorExitDoor(out _))
            {
                interactPromptText = "E : 무한 모드로";
                return;
            }

            if (currentFloor > 0 && TryGetFloorExitDoor(out _))
            {
                interactPromptText = "E : 다음 층으로";
                return;
            }

            StageRoom currentRoom = FindCurrentStageRoom();
            if (currentRoom != null && !currentRoom.State.Cleared && !currentRoom.State.Activated)
            {
                interactPromptText = BuildRoomEntryPrompt(currentRoom);
                return;
            }

            if (currentRoom != null && currentRoom.State.Activated && !currentRoom.State.Cleared && !currentRoom.IsRestRoom)
            {
                interactPromptText = BuildActiveRoomObjectivePrompt(currentRoom);
                return;
            }

            if (currentRoom != null && currentRoom.IsRestRoom && currentRoom.State.Activated && !currentRoom.State.Cleared)
            {
                interactPromptText = HasAffordableRestChoice(currentRoom.Id)
                    ? $"카드 상점 - 구매 또는 E로 나가기 (보유 코인 {player.CoinCount})"
                    : $"카드 상점 - E로 나가기 (보유 코인 {player.CoinCount})";
                return;
            }

            interactPromptText = TryGetInteractableDoor(out _, out _)
                ? "E : 연결된 문 열기"
                : null;
        }

        private string BuildRoomEntryPrompt(StageRoom room)
        {
            if (room.IsRestRoom)
            {
                return "중앙 진입 시 카드 상점";
            }

            string prompt;
            switch (room.ObjectiveKind)
            {
                case RoomObjectiveKind.Survive:
                    prompt = "중앙 진입 시 생존전 시작";
                    break;
                case RoomObjectiveKind.KeyTarget:
                    prompt = "중앙 진입 시 은닉 표적 방 시작";
                    break;
                default:
                    prompt = room.IsBossRoom
                        ? "중앙 진입 시 보스전 시작"
                        : room.IsMiniBossRoom
                            ? "중앙 진입 시 정예전 시작"
                            : "중앙 진입 시 라운드 시작";
                    break;
            }

            if (room.HazardKind == RoomHazardKind.ToxicMist)
            {
                prompt += " / 독성 안개";
            }
            else if (room.HazardKind == RoomHazardKind.SupplyShortage)
            {
                prompt += " / 보급 부족";
            }

            return prompt;
        }

        private string BuildActiveRoomObjectivePrompt(StageRoom room)
        {
            string prompt;
            switch (room.ObjectiveKind)
            {
                case RoomObjectiveKind.Survive:
                    prompt = "목표: " + Math.Ceiling(Math.Max(0f, room.State.ObjectiveTimer)) + "초 버티기";
                    break;
                case RoomObjectiveKind.KeyTarget:
                    prompt = "목표: 은닉 표적 추적";
                    break;
                default:
                    prompt = "목표: 적 제거 (" + enemyManager.CountAliveEnemies() + " 남음)";
                    break;
            }

            if (room.HazardKind == RoomHazardKind.ToxicMist)
            {
                prompt += " / 위험: 독성 안개";
            }
            else if (room.HazardKind == RoomHazardKind.SupplyShortage)
            {
                prompt += " / 보급 없음, 보상 +1";
            }

            return prompt;
        }

        private float GetToxicMistOverlayAlpha()
        {
            if (!IsActiveToxicMistRoom())
            {
                return 0f;
            }

            StageRoom room = mapManager.StageRooms[activeStageRoomIndex];
            float interval = Math.Max(0.001f, RoomConfig.ToxicMistDamageInterval);
            float tickPulse = 1f - Math.Max(0f, Math.Min(1f, room.State.HazardTickTimer / interval));
            return Math.Min(1f, 0.18f + tickPulse * 0.10f);
        }

        private bool IsActiveToxicMistRoom()
        {
            if (activeStageRoomIndex < 0 ||
                mapManager.StageRooms == null ||
                activeStageRoomIndex >= mapManager.StageRooms.Length)
            {
                return false;
            }

            StageRoom room = mapManager.StageRooms[activeStageRoomIndex];
            if (room == null ||
                room.HazardKind != RoomHazardKind.ToxicMist ||
                !room.State.Activated ||
                room.State.Cleared)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 로그라이크 모드에서 클리어된 방의 층 출구 문(TargetRoomId == -1)이
        /// 플레이어 상호작용 거리 이내에 있으면 해당 문 좌표를 반환한다.
        /// </summary>
        /// <param name="exitDoor">찾은 출구 문의 타일 좌표. 없으면 <see cref="Point.Empty"/>.</param>
        /// <returns>출구 문을 찾았으면 true.</returns>
        private bool TryGetFloorExitDoor(out Point exitDoor)
        {
            exitDoor = Point.Empty;
            StageRoom[] rooms = mapManager.StageRooms;
            if (rooms == null)
            {
                return false;
            }

            float interactDistSq = WorldConfig.DoorInteractDistance * WorldConfig.DoorInteractDistance;

            for (int i = 0; i < rooms.Length; i++)
            {
                StageRoom room = rooms[i];
                if (room == null || !room.State.Cleared)
                {
                    continue;
                }

                StageDoorConnection[] connections = room.Connections;
                if (connections == null)
                {
                    continue;
                }

                for (int j = 0; j < connections.Length; j++)
                {
                    StageDoorConnection connection = connections[j];
                    if (connection == null || connection.TargetRoomId != -1)
                    {
                        continue; // -1만 층 출구 신호
                    }

                    if (!mapManager.IsDoorClosed(connection.Door))
                    {
                        continue;
                    }

                    float dx = connection.Door.X + 0.5f - player.Position.X;
                    float dy = connection.Door.Y + 0.5f - player.Position.Y;
                    if ((dx * dx) + (dy * dy) <= interactDistSq)
                    {
                        exitDoor = connection.Door;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 스테이지 상태 메시지 타이머를 감소시키고, 시간이 다 되면 메시지를 지운다.
        /// </summary>
        /// <param name="dt">이번 프레임의 경과 시간(초).</param>
        private void UpdateStageMessage(float dt)
        {
            if (stageStatusTimer <= 0f)
            {
                return;
            }

            stageStatusTimer -= dt;
            if (stageStatusTimer <= 0f)
            {
                stageStatusTimer = 0f;
                stageStatusMessage = null;
            }
        }

        /// <summary>스테이지 상태 메시지가 사라지기 전 마지막 구간에서 천천히 페이드아웃하는 시간(초).</summary>
        private const float StageStatusFadeOut = 0.7f;

        /// <summary>
        /// 스테이지 상태 메시지의 표시 강도(0~1)를 반환한다.
        /// 남은 시간이 페이드아웃 구간보다 많으면 1, 적으면 비례 감소한다.
        /// </summary>
        private float GetStageStatusAlpha()
        {
            if (stageStatusTimer <= 0f)
            {
                return 0f;
            }

            return Math.Min(1f, stageStatusTimer / StageStatusFadeOut);
        }

        /// <summary>
        /// 보스 방 진입 연출 타이머를 감소시킨다.
        /// 타이머가 0에 도달하면 보스 인트로 연출이 끝난 것으로 간주된다.
        /// </summary>
        /// <param name="dt">이번 프레임의 경과 시간(초).</param>
        private void UpdateBossIntroTimer(float dt)
        {
            if (bossIntroTimer <= 0f)
            {
                return;
            }

            bossIntroTimer -= dt;
            if (bossIntroTimer < 0f)
            {
                bossIntroTimer = 0f;
            }
        }

        /// <summary>
        /// 화면에 표시할 스테이지 상태 메시지와 지속 시간을 설정한다.
        /// 이전 메시지가 있어도 덮어쓴다.
        /// </summary>
        /// <param name="message">화면에 표시할 메시지 문자열.</param>
        /// <param name="duration">메시지를 표시할 시간(초).</param>
        private void SetStageStatus(string message, float duration)
        {
            stageStatusMessage = message;
            stageStatusTimer = duration;
        }

        /// <summary>
        /// 스테이지 전체 상태를 초기값으로 재설정한다.
        /// 승리 플래그, 활성 방 인덱스, 시각 효과, 보상 픽업, 상호작용 키 상태를 모두 초기화하고,
        /// 스테이지 흐름이 있는 맵에서는 모든 방의 진행 상태와 문을 닫힌 상태로 되돌린다.
        /// </summary>
        private void ResetStageState()
        {
            StopLoopingWeaponEffects();
            victory = false;
            activeStageRoomIndex = -1;
            stageStatusMessage = null;
            stageStatusTimer = 0f;
            bossIntroTimer = 0f;
            playerDamageFlashTimer = 0f;
            playerDamageFlashDirX = 0f;
            playerDamageFlashDirY = 0f;
            playerDamageShakeTimer = 0f;
            playerDamageShakePower = 0f;
            playerRecoilShakeTimer = 0f;
            playerRecoilShakePower = 0f;
            hitMarkerTimer = 0f;
            killMarkerTimer = 0f;
            weaponStatusText = null;
            weaponStatusTimer = 0f;
            weaponStatusRepeatGate = 0f;
            pickupToastText = null;
            pickupToastTimer = 0f;
            deathPresentationProgress = 0f;
            deathRollDirection = 1f;
            interactKeyHeld = false;
            specialKeyHeld = false;
            currentStageRoomCache = null;
            branchSelectionActive = false;
            branchOptionA = null;
            branchOptionB = null;
            branch1KeyHeld = false;
            branch2KeyHeld = false;
            ResetPermanentStatsUiState();
            ResetPlayerStatsOverlayState();
            ResetEndingSequenceState();
            hookPullSteps = 0;
            hookTarget = null;
            plazmaLaserTimer = 0f;
            plazmaLaserTick = 0f;
            weapon.StopPlazmaLaser();
            ResetCardDisplayState();
            rewardPickups.Clear();
            playerProjectiles.Clear();

            if (!mapManager.HasStageFlow || mapManager.StageRooms == null)
            {
                interactPromptText = null;
                return;
            }

            StageRoom[] rooms = mapManager.StageRooms;
            for (int i = 0; i < rooms.Length; i++)
            {
                StageRoom room = rooms[i];
                room.ResetProgress(room.Id == 0);

                StageDoorConnection[] connections = room.Connections;
                if (connections == null)
                {
                    continue;
                }

                for (int j = 0; j < connections.Length; j++)
                {
                    StageDoorConnection connection = connections[j];
                    if (connection != null)
                    {
                        mapManager.CloseDoor(connection.Door);
                    }
                }
            }

            interactPromptText = null;
            if (rooms.Length > 0)
            {
                SetStageStatus("시작실에서 준비한 뒤 연결 문을 여세요", 3f);
            }
        }
    }
}
