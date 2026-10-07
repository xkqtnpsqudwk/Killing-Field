using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using My2DEngine.Game;
using My2DEngine.Game.Config;
using My2DEngine.Game.Core;
using My2DEngine.Game.Map;

namespace My2DEngine.SmokeTests
{
    /// <summary>
    /// 밸런스 스냅샷의 한 행. 같은 (구역, 항목, 키)끼리 두 스냅샷을 비교한다.
    /// </summary>
    internal readonly record struct BalanceSnapshotRow(string Section, string Metric, string Key, string Value)
    {
        internal string Id => Section + " / " + Metric + (Key.Length > 0 ? " / " + Key : string.Empty);
    }

    /// <summary>
    /// 현재 밸런스 기준선을 사람이 빠르게 확인할 수 있도록 요약 리포트를 출력한다.
    /// 게임 수치 조정 없이 분포만 샘플링하는 도구이며, 스모크 테스트와 같은 프로젝트에서 실행한다.
    /// 같은 내용을 CSV로 저장하고 두 CSV를 비교해 수치 변경 전후를 확인할 수 있다.
    /// </summary>
    internal static class BalanceSnapshotReporter
    {
        private const int RoomSamplesPerBand = 1200;

        /// <summary>상점 카드 표본을 뽑을 때 쓰는 고정 런 시드. 실행마다 같은 결과를 내 CSV 비교가 가능하다.</summary>
        private const int ShopSampleSeed = 4242;

        private const string CsvHeader = "section,metric,key,value";

        /// <summary>리포트를 텍스트로 출력하고, 같은 내용을 비교용 행 목록으로 돌려준다.</summary>
        internal static List<BalanceSnapshotRow> Write(TextWriter writer)
        {
            writer ??= TextWriter.Null;
            var rows = new List<BalanceSnapshotRow>();

            writer.WriteLine("Balance baseline snapshot");
            writer.WriteLine("=========================");
            writer.WriteLine("This report samples current generation rules only. It does not change gameplay values.");
            writer.WriteLine();

            WriteCoreValues(writer, rows);
            WriteRoomBand(writer, rows, "Early floors 1-19", 1, 19, bossClearGrowthCount: 0, seed: 1729);
            WriteRoomBand(writer, rows, "Mid floors 21-39", 21, 39, bossClearGrowthCount: 1, seed: 2718);
            WriteRoomBand(writer, rows, "Late floors 41-59", 41, 59, bossClearGrowthCount: 2, seed: 3141);
            WriteShopAndCardValues(writer, rows);
            return rows;
        }

        /// <summary>행 목록을 UTF-8 CSV로 저장한다.</summary>
        internal static void SaveCsv(string path, List<BalanceSnapshotRow> rows)
        {
            var builder = new StringBuilder();
            builder.AppendLine(CsvHeader);
            foreach (BalanceSnapshotRow row in rows)
            {
                builder.Append(EscapeCsv(row.Section)).Append(',')
                    .Append(EscapeCsv(row.Metric)).Append(',')
                    .Append(EscapeCsv(row.Key)).Append(',')
                    .Append(EscapeCsv(row.Value)).AppendLine();
            }

            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
        }

        /// <summary>
        /// 두 CSV를 비교해 바뀐 값, 새로 생긴 행, 사라진 행을 출력한다.
        /// </summary>
        /// <returns>차이가 있으면 true.</returns>
        internal static bool Compare(TextWriter writer, string beforePath, string afterPath)
        {
            Dictionary<string, BalanceSnapshotRow> before = LoadCsv(beforePath);
            Dictionary<string, BalanceSnapshotRow> after = LoadCsv(afterPath);
            int changed = 0;
            int added = 0;
            int removed = 0;

            writer.WriteLine("Balance snapshot compare");
            writer.WriteLine("- before: " + beforePath);
            writer.WriteLine("- after:  " + afterPath);
            writer.WriteLine();

            foreach (KeyValuePair<string, BalanceSnapshotRow> entry in before)
            {
                if (!after.TryGetValue(entry.Key, out BalanceSnapshotRow next))
                {
                    writer.WriteLine("- removed  " + entry.Key + " = " + entry.Value.Value);
                    removed++;
                }
                else if (next.Value != entry.Value.Value)
                {
                    writer.WriteLine("~ changed  " + entry.Key + ": " + entry.Value.Value + " -> " + next.Value + FormatDelta(entry.Value.Value, next.Value));
                    changed++;
                }
            }

            foreach (KeyValuePair<string, BalanceSnapshotRow> entry in after)
            {
                if (!before.ContainsKey(entry.Key))
                {
                    writer.WriteLine("+ added    " + entry.Key + " = " + entry.Value.Value);
                    added++;
                }
            }

            writer.WriteLine();
            writer.WriteLine("changed " + changed + ", added " + added + ", removed " + removed);
            return changed + added + removed > 0;
        }

        private static void WriteCoreValues(TextWriter writer, List<BalanceSnapshotRow> rows)
        {
            const string section = "Core";
            AddValue(rows, section, "PlayerHealthMax", GameConfig.PlayerHealthMax);
            AddValue(rows, section, "PlayerShieldMax", GameConfig.PlayerShieldMax);
            AddValue(rows, section, "PlayerShieldBaseRegenRate", GameConfig.PlayerShieldBaseRegenRate);
            AddValue(rows, section, "PlayerShieldBaseRegenDelay", GameConfig.PlayerShieldBaseRegenDelay);
            AddValue(rows, section, "EnemyHealthGrowthPerClearedCombatFloor", GameConfig.EnemyHealthGrowthPerClearedCombatFloor);
            AddValue(rows, section, "EnemyDamageGrowthPerClearedCombatFloor", GameConfig.EnemyDamageGrowthPerClearedCombatFloor);
            AddValue(rows, section, "EnemyMoveSpeedGrowthPerClearedCombatFloor", GameConfig.EnemyMoveSpeedGrowthPerClearedCombatFloor);
            AddValue(rows, section, "EnemyHealthGrowthPerBossClear", GameConfig.EnemyHealthGrowthPerBossClear);
            AddValue(rows, section, "EnemyDamageGrowthPerBossClear", GameConfig.EnemyDamageGrowthPerBossClear);
            AddValue(rows, section, "EnemyMoveSpeedGrowthPerBossClear", GameConfig.EnemyMoveSpeedGrowthPerBossClear);
            AddValue(rows, section, "SurvivalRoomBaseDuration", GameConfig.SurvivalRoomBaseDuration);
            AddValue(rows, section, "SurvivalRoomDurationPerFloor", GameConfig.SurvivalRoomDurationPerFloor);
            AddValue(rows, section, "SurvivalRoomMaxDuration", GameConfig.SurvivalRoomMaxDuration);
            AddValue(rows, section, "KeyTargetHealthMultiplier", GameConfig.KeyTargetHealthMultiplier);
            AddValue(rows, section, "KeyTargetRevealHealthRatio", GameConfig.KeyTargetRevealHealthRatio);
            AddValue(rows, section, "ShieldedDamageBonusCap", GameConfig.ShieldedDamageBonusCap);
            AddValue(rows, section, "DashStrikeDamageBonusCap", GameConfig.DashStrikeDamageBonusCap);
            AddValue(rows, section, "DashStrikeDamageWindow", GameConfig.DashStrikeDamageWindow);
            AddValue(rows, section, "ToxicMistDamage", GameConfig.ToxicMistDamage);
            AddValue(rows, section, "ToxicMistDamageInterval", GameConfig.ToxicMistDamageInterval);

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
            List<BalanceSnapshotRow> rows,
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

            AddCounts(rows, title, "RoomType", roomTypes);
            AddCounts(rows, title, "Objective", objectives);
            AddCounts(rows, title, "Hazard", hazards);
            AddCounts(rows, title, "Risk", risks);
            AddCounts(rows, title, "Enemy", enemies);

            writer.WriteLine(title);
            writer.WriteLine("- Room types: " + FormatCounts(roomTypes, RoomSamplesPerBand));
            writer.WriteLine("- Objectives: " + FormatCounts(objectives, RoomSamplesPerBand));
            writer.WriteLine("- Hazards: " + FormatCounts(hazards, RoomSamplesPerBand));
            writer.WriteLine("- Risk: " + FormatCounts(risks, RoomSamplesPerBand));
            writer.WriteLine("- Top enemies: " + FormatTopCounts(enemies, Math.Min(8, enemies.Count)));
            writer.WriteLine();
        }

        private static void WriteShopAndCardValues(TextWriter writer, List<BalanceSnapshotRow> rows)
        {
            const string section = "Cards and shop";
            var world = new GameLogic(ShopSampleSeed);
            int baseChoices = world.GetCardRewardOfferSlotCountSmokeSnapshot(0f);
            int bonusChoices = world.GetCardRewardOfferSlotCountSmokeSnapshot(1f);
            AddValue(rows, section, "CardRewardChoices", baseChoices, "Base");
            AddValue(rows, section, "CardRewardChoices", bonusChoices, "WithCardChoiceBonus");

            CardGrade[] shopGrades = { CardGrade.Green, CardGrade.Blue, CardGrade.Purple, CardGrade.Red };
            var costs = new int[shopGrades.Length];
            var discountedCosts = new int[shopGrades.Length];
            for (int i = 0; i < shopGrades.Length; i++)
            {
                costs[i] = world.GetRestShopCardCostSmokeSnapshot(shopGrades[i]);
                discountedCosts[i] = world.GetRestShopCardCostSmokeSnapshot(shopGrades[i], 0.50f);
                AddValue(rows, section, "RestShopCost", costs[i], shopGrades[i].ToString());
                AddValue(rows, section, "RestShopCostAt50Discount", discountedCosts[i], shopGrades[i].ToString());
            }

            RewardCardOffer[] luck0 = world.CreateRestShopCardOfferSmokeSnapshot(0f);
            RewardCardOffer[] luck5 = world.CreateRestShopCardOfferSmokeSnapshot(5f);
            RewardCardOffer[] luckMax = world.CreateRestShopCardOfferSmokeSnapshot(GameConfig.PermanentLuckMax);
            AddOffers(rows, section, "RestShopOffersLuck0", luck0);
            AddOffers(rows, section, "RestShopOffersLuck5", luck5);
            AddOffers(rows, section, "RestShopOffersLuckMax", luckMax);

            writer.WriteLine("Cards and shop");
            writer.WriteLine("- Card reward choices: base " + baseChoices + ", with CardChoiceBonus " + bonusChoices);
            writer.WriteLine("- Rest shop cost Green/Blue/Purple/Red: " + string.Join("/", costs));
            writer.WriteLine("- Rest shop cost at 50% discount Green/Blue/Purple/Red: " + string.Join("/", discountedCosts));
            writer.WriteLine("- Rest shop offers at Luck 0 (seed " + ShopSampleSeed + "): " + FormatOffers(luck0));
            writer.WriteLine("- Rest shop offers at Luck 5 (seed " + ShopSampleSeed + "): " + FormatOffers(luck5));
            writer.WriteLine("- Rest shop offers at max Luck (seed " + ShopSampleSeed + "): " + FormatOffers(luckMax));
            writer.WriteLine();
        }

        private static void AddValue(List<BalanceSnapshotRow> rows, string section, string metric, float value, string key = "")
        {
            rows.Add(new BalanceSnapshotRow(section, metric, key, value.ToString("0.####", CultureInfo.InvariantCulture)));
        }

        private static void AddCounts(List<BalanceSnapshotRow> rows, string section, string metric, Dictionary<string, int> counts)
        {
            foreach (KeyValuePair<string, int> entry in SortCounts(counts))
            {
                rows.Add(new BalanceSnapshotRow(section, metric, entry.Key, entry.Value.ToString(CultureInfo.InvariantCulture)));
            }
        }

        private static void AddOffers(List<BalanceSnapshotRow> rows, string section, string metric, RewardCardOffer[] offers)
        {
            int count = offers == null ? 0 : offers.Length;
            for (int i = 0; i < count; i++)
            {
                RewardCardOffer offer = offers[i];
                string value = offer == null ? "none" : offer.Grade + " " + offer.StatType;
                rows.Add(new BalanceSnapshotRow(section, metric, "Slot" + (i + 1), value));
            }
        }

        private static Dictionary<string, BalanceSnapshotRow> LoadCsv(string path)
        {
            var rows = new Dictionary<string, BalanceSnapshotRow>(StringComparer.Ordinal);
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length == 0 || (i == 0 && lines[i] == CsvHeader))
                {
                    continue;
                }

                List<string> fields = ParseCsvLine(lines[i]);
                if (fields.Count != 4)
                {
                    throw new InvalidDataException(path + ":" + (i + 1) + ": expected 4 columns, got " + fields.Count + ".");
                }

                var row = new BalanceSnapshotRow(fields[0], fields[1], fields[2], fields[3]);
                rows[row.Id] = row;
            }

            return rows;
        }

        private static string EscapeCsv(string value)
        {
            value ??= string.Empty;
            if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0)
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var current = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else if (c == '"')
                    {
                        quoted = false;
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                else if (c == '"')
                {
                    quoted = true;
                }
                else if (c == ',')
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            fields.Add(current.ToString());
            return fields;
        }

        /// <summary>두 값이 모두 숫자면 증감을 덧붙인다.</summary>
        private static string FormatDelta(string before, string after)
        {
            if (!float.TryParse(before, NumberStyles.Float, CultureInfo.InvariantCulture, out float a) ||
                !float.TryParse(after, NumberStyles.Float, CultureInfo.InvariantCulture, out float b))
            {
                return string.Empty;
            }

            float delta = b - a;
            string text = " (" + (delta >= 0f ? "+" : string.Empty) + delta.ToString("0.####", CultureInfo.InvariantCulture);
            if (a != 0f)
            {
                text += ", " + (delta / a * 100f).ToString("+0.#;-0.#;0", CultureInfo.InvariantCulture) + "%";
            }

            return text + ")";
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
