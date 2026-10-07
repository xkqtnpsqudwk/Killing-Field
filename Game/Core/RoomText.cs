using System;
using My2DEngine.Game.Map;

namespace My2DEngine.Game.Core
{
    /// <summary>방 종류·목표·위험 요소에 따른 안내 문구.</summary>
    internal static class RoomText
    {
        /// <summary>방에 들어서기 전 상호작용 안내(예: "중앙 진입 시 생존전 시작 / 독성 안개").</summary>
        public static string EntryPrompt(StageRoom room)
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

        /// <summary>전투 중 목표 안내(예: "목표: 적 제거 (3 남음)").</summary>
        /// <param name="aliveEnemies">살아 있는 적 수(전멸 목표에서만 쓴다).</param>
        public static string ObjectivePrompt(StageRoom room, int aliveEnemies)
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
                    prompt = "목표: 적 제거 (" + aliveEnemies + " 남음)";
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

        /// <summary>전투 시작 때 상단 메시지(예: "생존전 시작 - 18초 버티기 / 보급 부족").</summary>
        public static string StartMessage(StageRoom room)
        {
            string text;
            switch (room.ObjectiveKind)
            {
                case RoomObjectiveKind.Survive:
                    text = "생존전 시작 - " + Math.Ceiling(Math.Max(1f, room.ObjectiveDuration)) + "초 버티기";
                    break;
                case RoomObjectiveKind.KeyTarget:
                    text = "열쇠 방 시작 - 은닉 표적 추적";
                    break;
                default:
                    text = room.IsBossRoom
                        ? "보스전 시작"
                        : room.IsMiniBossRoom
                            ? "정예전 시작"
                            : "라운드 시작 - 적 제거";
                    break;
            }

            if (room.HazardKind == RoomHazardKind.ToxicMist)
            {
                text += " / 독성 안개";
            }
            else if (room.HazardKind == RoomHazardKind.SupplyShortage)
            {
                text += " / 보급 부족";
            }

            return text;
        }
    }
}
