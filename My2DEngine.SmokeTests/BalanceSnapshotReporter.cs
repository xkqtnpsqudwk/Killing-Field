using System;
using System.Collections.Generic;
using System.IO;
using My2DEngine.Game;
using My2DEngine.Game.Config;
using My2DEngine.Game.Core;
using My2DEngine.Game.Map;

namespace My2DEngine.SmokeTests
{
    /// <summary>
    /// 현재 밸런스 기준선을 사람이 빠르게 확인할 수 있도록 요약 리포트를 출력한다.
    /// 게임 수치 조정 없이 분포만 샘플링하는 도구이며, 스모크 테스트와 같은 프로젝트에서 실행한다.
    /// </summary>
    internal static class BalanceSnapshotReporter
    {
        private const int RoomSamplesPerBand = 1200;

        internal static void Write(TextWriter writer)
        {
            if (writer == null)
            {
                return;
            }

            writer.WriteLine("Balance baseline snapshot");
            writer.WriteLine("=========================");
            writer.WriteLine("This report samples current generation rules only. It does not change gameplay values.");
            writer.WriteLine();

            WriteCoreValues(writer);
            WriteRoomBand(writer, "Early floors 1-19", 1, 19, bossClearGrowthCount: 0, seed: 1729);
            WriteRoomBand(writer, "Mid floors 21-39", 21, 39, bossClearGrowthCount: 1, seed: 2718);
            WriteRoomBand(writer, "Late floors 41-59", 41, 59, bossClearGrowthCount: 2, seed: 3141);
            WriteShopAndCardValues(writer);
        }

        private static void WriteCoreValues(TextWriter writer)
        {
            writer.WriteLine("Core combat values");
            writer.WriteLine("- Player HP: " + Format(GameConfig.PlayerHealthMax));
            writer.WriteLine("- Player shield: " + Format(GameConfig.PlayerShieldMax) +
                ", regen " + Format(GameConfig.PlayerShieldBaseRegenRate) + "/s after " +
                Format(GameConfig.PlayerShieldBaseRegenDelay) + "s");
            writer.WriteLine("- Enemy growth per combat floor: HP +" +
                FormatPercent(GameConfig.EnemyHealthGrowthPerClearedCombatFloor) +
                ", damage +" + FormatPercent(GameConfig.EnemyDamageGrowthPerClearedCombatFloor) +
                ", speed +" + FormatPercent(GameConfig.EnemyMoveSpeedGrowthPerClearedCombatFloor));
            writer.WriteLine("- Enemy growth per boss clear: HP +" +
                FormatPercent(GameConfig.EnemyHealthGrowthPerBossClear) +
                ", damage +" + FormatPercent(GameConfig.EnemyDamageGrowthPerBossClear) +
                ", speed +" + FormatPercent(GameConfig.EnemyMoveSpeedGrowthPerBossClear));
            writer.WriteLine("- Survival room: " + Format(GameConfig.SurvivalRoomBaseDuration) +
                "s base, +" + Format(GameConfig.SurvivalRoomDurationPerFloor) +
                "s/floor, cap " + Format(GameConfig.SurvivalRoomMaxDuration) + "s");
            writer.WriteLine("- Key target: HP x" + Format(GameConfig.KeyTargetHealthMultiplier) +
                ", reveal below " + FormatPercent(GameConfig.KeyTargetRevealHealthRatio));
            writer.WriteLine("- Synergy cards: shielded damage cap +" +
                FormatPercent(GameConfig.ShieldedDamageBonusCap) +
                ", dash-strike damage cap +" + FormatPercent(GameConfig.DashStrikeDamageBonusCap) +
                " for " + Format(GameConfig.DashStrikeDamageWindow) + "s after dash");
            writer.WriteLine("- Toxic mist: " + Format(GameConfig.ToxicMistDamage) +
                " damage every " + Format(GameConfig.ToxicMistDamageInterval) + "s");
            writer.WriteLine();
        }

        private static void WriteRoomBand(
            TextWriter writer,
            string title,
            int minFloor,
            int maxFloor,
            int bossClearGrowthCount,
            int seed)
        {
            var rng = new Random(seed);
            var roomTypes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var objectives = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var hazards = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var risks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var enemies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            int floorSpan = Math.Max(1, maxFloor - minFloor + 1);
            for (int i = 0; i < RoomSamplesPerBand; i++)
            {
                int floor = minFloor + i % floorSpan;
                RoomTemplate template = RoomTemplateLibrary.SelectForFloor(
                    floor,
                    rng,
                    bossClearGrowthCount: bossClearGrowthCount,
                    allowRestRoom: true);

                Increment(roomTypes, GetRoomTypeLabel(template));
                Increment(objectives, template.ObjectiveKind.ToString());
                Increment(hazards, template.HazardKind.ToString());
                Increment(risks, GetRiskLabel(template));
                AddEnemyAssets(enemies, template.Spawns);
            }

            writer.WriteLine(title);
            writer.WriteLine("- Room types: " + FormatCounts(roomTypes, RoomSamplesPerBand));
            writer.WriteLine("- Objectives: " + FormatCounts(objectives, RoomSamplesPerBand));
            writer.WriteLine("- Hazards: " + FormatCounts(hazards, RoomSamplesPerBand));
            writer.WriteLine("- Risk: " + FormatCounts(risks, RoomSamplesPerBand));
            writer.WriteLine("- Top enemies: " + FormatTopCounts(enemies, Math.Min(8, enemies.Count)));
            writer.WriteLine();
        }

        private static void WriteShopAndCardValues(TextWriter writer)
        {
            var world = new GameLogic();
            writer.WriteLine("Cards and shop");
            writer.WriteLine("- Card reward choices: base " +
                world.GetCardRewardOfferSlotCountSmokeSnapshot(0f) +
                ", with CardChoiceBonus " + world.GetCardRewardOfferSlotCountSmokeSnapshot(1f));
            writer.WriteLine("- Rest shop cost Green/Blue/Purple/Red: " +
                world.GetRestShopCardCostSmokeSnapshot(CardGrade.Green) + "/" +
                world.GetRestShopCardCostSmokeSnapshot(CardGrade.Blue) + "/" +
                world.GetRestShopCardCostSmokeSnapshot(CardGrade.Purple) + "/" +
                world.GetRestShopCardCostSmokeSnapshot(CardGrade.Red));
            writer.WriteLine("- Rest shop cost at 50% discount Green/Blue/Purple/Red: " +
                world.GetRestShopCardCostSmokeSnapshot(CardGrade.Green, 0.50f) + "/" +
                world.GetRestShopCardCostSmokeSnapshot(CardGrade.Blue, 0.50f) + "/" +
                world.GetRestShopCardCostSmokeSnapshot(CardGrade.Purple, 0.50f) + "/" +
                world.GetRestShopCardCostSmokeSnapshot(CardGrade.Red, 0.50f));
            writer.WriteLine("- Rest shop offers at Luck 0: " + FormatOffers(world.CreateRestShopCardOfferSmokeSnapshot(0f)));
            writer.WriteLine("- Rest shop offers at Luck 5: " + FormatOffers(world.CreateRestShopCardOfferSmokeSnapshot(5f)));
            writer.WriteLine("- Rest shop offers at max Luck: " + FormatOffers(world.CreateRestShopCardOfferSmokeSnapshot(GameConfig.PermanentLuckMax)));
            writer.WriteLine();
        }

        private static string GetRoomTypeLabel(RoomTemplate template)
        {
            if (template == null)
            {
                return "None";
            }

            if (template.IsBossRoom)
            {
                return "Boss";
            }

            if (template.IsRestRoom)
            {
                return "Shop";
            }

            return template.IsMiniBossRoom ? "Elite" : "Normal";
        }

        private static string GetRiskLabel(RoomTemplate template)
        {
            int score = RoomTemplateLibrary.GetRoomRiskScore(template);
            if (score >= RoomTemplateLibrary.ExtremeRoomRiskScore)
            {
                return "Extreme";
            }

            if (score >= RoomTemplateLibrary.HighRoomRiskScore)
            {
                return "High";
            }

            return "Standard";
        }

        private static void AddEnemyAssets(Dictionary<string, int> counts, StageSpawnPoint[] spawns)
        {
            if (counts == null || spawns == null)
            {
                return;
            }

            for (int i = 0; i < spawns.Length; i++)
            {
                string assetId = spawns[i]?.EnemyAssetId;
                if (!string.IsNullOrWhiteSpace(assetId))
                {
                    Increment(counts, assetId);
                }
            }
        }

        private static string FormatOffers(RewardCardOffer[] offers)
        {
            if (offers == null || offers.Length == 0)
            {
                return "none";
            }

            string result = string.Empty;
            for (int i = 0; i < offers.Length; i++)
            {
                RewardCardOffer offer = offers[i];
                if (offer == null)
                {
                    continue;
                }

                if (result.Length > 0)
                {
                    result += ", ";
                }

                result += offer.Grade + " " + offer.StatType;
            }

            return result.Length == 0 ? "none" : result;
        }

        private static string FormatCounts(Dictionary<string, int> counts, int total)
        {
            if (counts == null || counts.Count == 0 || total <= 0)
            {
                return "none";
            }

            List<KeyValuePair<string, int>> entries = SortCounts(counts);
            string result = string.Empty;
            for (int i = 0; i < entries.Count; i++)
            {
                if (result.Length > 0)
                {
                    result += ", ";
                }

                result += entries[i].Key + " " + entries[i].Value +
                    " (" + FormatPercent(entries[i].Value / (float)total) + ")";
            }

            return result;
        }

        private static string FormatTopCounts(Dictionary<string, int> counts, int maxEntries)
        {
            if (counts == null || counts.Count == 0 || maxEntries <= 0)
            {
                return "none";
            }

            List<KeyValuePair<string, int>> entries = SortCounts(counts);
            string result = string.Empty;
            int count = Math.Min(maxEntries, entries.Count);
            for (int i = 0; i < count; i++)
            {
                if (result.Length > 0)
                {
                    result += ", ";
                }

                result += entries[i].Key + " " + entries[i].Value;
            }

            return result;
        }

        private static List<KeyValuePair<string, int>> SortCounts(Dictionary<string, int> counts)
        {
            var entries = new List<KeyValuePair<string, int>>(counts);
            entries.Sort((a, b) =>
            {
                int compare = b.Value.CompareTo(a.Value);
                return compare != 0 ? compare : string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);
            });
            return entries;
        }

        private static void Increment(Dictionary<string, int> counts, string key)
        {
            if (counts == null || string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (!counts.ContainsKey(key))
            {
                counts[key] = 0;
            }

            counts[key]++;
        }

        private static string Format(float value)
        {
            return value.ToString("0.##");
        }

        private static string FormatPercent(float value)
        {
            return (value * 100f).ToString("0.#") + "%";
        }
    }
}
