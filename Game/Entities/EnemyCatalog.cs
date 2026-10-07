using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using My2DEngine.Game.Config;

namespace My2DEngine.Game
{
    /// <summary>
    /// 적 하나를 구성하는 런타임 기준 아키타입 데이터다.
    /// 스탯, 표시 이름, 스프라이트, AI, 사운드를 한곳에서 관리한다.
    /// </summary>
    public sealed class EnemyArchetype
    {
        private readonly EnemyBehaviorPattern[] behaviorPatternPool;
        private readonly string[] spriteVariantKeyPool;

        public EnemyArchetype(
            string assetId,
            string displayName,
            string roomLabel,
            string spriteVariantKey,
            EnemyDefinition definition,
            EnemyRank rank = EnemyRank.Normal,
            EnemyAiProfile aiProfile = null,
            EnemySoundProfile soundProfile = null,
            EnemyBehaviorPattern[] behaviorPatternPool = null,
            string[] spriteVariantKeyPool = null)
        {
            AssetId = assetId ?? string.Empty;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? roomLabel : displayName;
            RoomLabel = string.IsNullOrWhiteSpace(roomLabel) ? DisplayName : roomLabel;
            SpriteVariantKey = string.IsNullOrWhiteSpace(spriteVariantKey) ? assetId : spriteVariantKey;
            Definition = definition?.Clone() ?? new EnemyDefinition();
            Rank = rank;
            AiProfile = aiProfile ?? new EnemyAiProfile(EnemyBehaviorPattern.Default);
            SoundProfile = soundProfile ?? EnemySoundProfile.Silent;

            if (behaviorPatternPool == null || behaviorPatternPool.Length == 0)
            {
                this.behaviorPatternPool = Array.Empty<EnemyBehaviorPattern>();
            }
            else
            {
                this.behaviorPatternPool = new EnemyBehaviorPattern[behaviorPatternPool.Length];
                Array.Copy(behaviorPatternPool, this.behaviorPatternPool, behaviorPatternPool.Length);
            }

            if (spriteVariantKeyPool == null || spriteVariantKeyPool.Length == 0)
            {
                this.spriteVariantKeyPool = Array.Empty<string>();
            }
            else
            {
                this.spriteVariantKeyPool = new string[spriteVariantKeyPool.Length];
                Array.Copy(spriteVariantKeyPool, this.spriteVariantKeyPool, spriteVariantKeyPool.Length);
            }
        }

        public string AssetId { get; }
        public string DisplayName { get; }
        public string RoomLabel { get; }
        public string SpriteVariantKey { get; }
        public EnemyDefinition Definition { get; }
        public EnemyRank Rank { get; }
        public EnemyAiProfile AiProfile { get; }
        public EnemySoundProfile SoundProfile { get; }
        public EnemyBehaviorPattern DefaultBehaviorPattern => AiProfile.MovementPattern;
        public EnemyBehaviorPattern[] BehaviorPatternPool => behaviorPatternPool;
        public string[] SpriteVariantKeyPool => spriteVariantKeyPool;

        public EnemyDefinition CreateDefinition() => Definition.Clone();

        public EnemyAiProfile CreateAiProfile(EnemyBehaviorPattern movementPattern)
            => AiProfile.WithMovementPattern(movementPattern);
    }

    /// <summary>
    /// 게임 내 모든 적/보스 에셋 카탈로그다.
    /// 정의는 <c>Game/Data/enemies.json</c>에 있고, 처음 사용할 때 한 번 읽는다.
    /// 빌드 없이 적 수치를 고칠 수 있으며, 파일이 없거나 잘못되면 어느 적의 어느 값인지 담은 예외를 던진다.
    /// </summary>
    public static class EnemyCatalog
    {
        private const string DataFileName = "enemies.json";

        /// <summary>JSON에서 읽은 카탈로그 한 벌. 읽기가 끝나야 전역에 걸린다.</summary>
        private sealed class CatalogData
        {
            internal readonly List<EnemyArchetype> Archetypes = new List<EnemyArchetype>();
            internal readonly Dictionary<string, EnemyArchetype> ByAssetId =
                new Dictionary<string, EnemyArchetype>(StringComparer.OrdinalIgnoreCase);
            internal readonly Dictionary<EnemyType, EnemyArchetype> DefaultByType =
                new Dictionary<EnemyType, EnemyArchetype>();
        }

        private static readonly CatalogData s_data = LoadFromDataFile();

        // ── 공개 API ─────────────────────────────────────────────────────────

        public static EnemyArchetype[] GetAllArchetypes()
        {
            return s_data.Archetypes.ToArray();
        }

        public static EnemyArchetype Resolve(string assetId, EnemyType fallbackType)
        {
            if (!string.IsNullOrWhiteSpace(assetId) &&
                s_data.ByAssetId.TryGetValue(assetId, out EnemyArchetype archetype))
            {
                return archetype;
            }

            return GetDefaultForType(fallbackType);
        }

        public static EnemyArchetype Get(string assetId)
        {
            if (string.IsNullOrWhiteSpace(assetId))
                throw new ArgumentException("Enemy asset id is required.", nameof(assetId));

            if (s_data.ByAssetId.TryGetValue(assetId, out EnemyArchetype archetype))
                return archetype;

            throw new ArgumentException("Unknown enemy asset id: " + assetId, nameof(assetId));
        }

        public static EnemyArchetype GetDefaultForType(EnemyType type)
        {
            if (s_data.DefaultByType.TryGetValue(type, out EnemyArchetype archetype))
                return archetype;

            return s_data.DefaultByType[EnemyType.Gunner];
        }

        /// <summary>
        /// JSON 문자열을 카탈로그로 읽어 보기만 하고 결과는 버린다. 아키타입 수를 돌려준다.
        /// 잘못된 데이터면 <see cref="InvalidDataException"/>을 던진다. 스모크 테스트가 검증 규칙을 확인할 때 쓴다.
        /// </summary>
        internal static int ValidateJson(string json)
        {
            return ParseOrThrow(json, "inline json").Archetypes.Count;
        }

        // ── JSON 로딩 ────────────────────────────────────────────────────────

        private static CatalogData LoadFromDataFile()
        {
            string path = ResolveDataPath();
            return ParseOrThrow(File.ReadAllText(path), path);
        }

        private static CatalogData ParseOrThrow(string json, string source)
        {
            CatalogData data;
            try
            {
                data = Load(json);
            }
            catch (Exception ex) when (ex is JsonException || ex is FormatException || ex is KeyNotFoundException || ex is InvalidOperationException)
            {
                throw new InvalidDataException("Failed to load enemy catalog from " + source + ": " + ex.Message, ex);
            }

            if (!data.DefaultByType.ContainsKey(EnemyType.Gunner))
                throw new InvalidDataException(source + ": an archetype with \"defaultForType\": \"Gunner\" is required.");

            return data;
        }

        /// <summary>실행 폴더 기준으로 enemies.json을 찾는다. 빌드 출력과 소스 트리 양쪽을 본다.</summary>
        private static string ResolveDataPath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates =
            {
                Path.Combine(baseDir, "Game", "Data", DataFileName),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "Game", "Data", DataFileName)),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Game", "Data", DataFileName))
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                if (File.Exists(candidates[i]))
                    return candidates[i];
            }

            throw new FileNotFoundException("Enemy catalog data file not found. Looked in: " + string.Join("; ", candidates));
        }

        private static CatalogData Load(string json)
        {
            var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
            using JsonDocument document = JsonDocument.Parse(json, options);
            JsonElement list = Required(document.RootElement, "archetypes", "root");
            var data = new CatalogData();
            foreach (JsonElement entry in list.EnumerateArray())
            {
                string id = Required(entry, "id", "archetype").GetString();
                string where = "archetype \"" + id + "\"";
                if (data.ByAssetId.ContainsKey(id))
                    throw new InvalidOperationException("duplicate id or alias \"" + id + "\".");

                JsonElement stats = Required(entry, "stats", where);
                var definition = new EnemyDefinition
                {
                    Type = ParseEnum<EnemyType>(Required(stats, "type", where), where),
                    Scale = Required(stats, "scale", where).GetSingle(),
                    MaxHealth = Required(stats, "maxHealth", where).GetSingle(),
                    MoveSpeed = Required(stats, "moveSpeed", where).GetSingle(),
                    AttackRange = Required(stats, "attackRange", where).GetSingle(),
                    AttackDamage = Required(stats, "attackDamage", where).GetSingle(),
                    AttackCooldownDuration = Required(stats, "attackCooldown", where).GetSingle(),
                    Radius = Required(stats, "radius", where).GetSingle()
                };

                var archetype = new EnemyArchetype(
                    id,
                    Optional(entry, "displayName"),
                    Optional(entry, "roomLabel"),
                    Optional(entry, "spriteKey"),
                    definition,
                    ParseEnum<EnemyRank>(Required(entry, "rank", where), where),
                    LoadAiProfile(Required(entry, "ai", where), where),
                    EnemySoundProfile.Silent,
                    LoadEnumArray<EnemyBehaviorPattern>(entry, "behaviorPatterns", where),
                    LoadStringArray(entry, "spriteVariants"));

                data.Archetypes.Add(archetype);
                Register(data.ByAssetId, archetype, LoadStringArray(entry, "aliases"));

                if (entry.TryGetProperty("defaultForType", out JsonElement defaultFor))
                {
                    EnemyType type = ParseEnum<EnemyType>(defaultFor, where);
                    if (data.DefaultByType.ContainsKey(type))
                        throw new InvalidOperationException(where + ": another archetype is already the default for " + type + ".");
                    data.DefaultByType[type] = archetype;
                }
            }

            return data;
        }

        /// <summary>
        /// 스타일로 기본값을 정한 AI 프로필을 만든 뒤, overrides에 적힌 값만 덮어쓴다.
        /// overrides의 키는 EnemyAiProfile의 float 속성 이름과 같아야 한다.
        /// </summary>
        private static EnemyAiProfile LoadAiProfile(JsonElement ai, string where)
        {
            var profile = new EnemyAiProfile(
                ParseEnum<EnemyBehaviorPattern>(Required(ai, "movementPattern", where), where),
                ParseEnum<EnemyWanderStyle>(Required(ai, "wanderStyle", where), where),
                ParseEnum<EnemyRangedAttackStyle>(Required(ai, "rangedAttackStyle", where), where),
                ParseEnum<EnemyBossCombatStyle>(Required(ai, "bossCombatStyle", where), where),
                ParseEnum<EnemyBossUltimateStyle>(Required(ai, "bossUltimateStyle", where), where));

            if (!ai.TryGetProperty("overrides", out JsonElement overrides))
                return profile;

            foreach (JsonProperty value in overrides.EnumerateObject())
            {
                PropertyInfo property = typeof(EnemyAiProfile).GetProperty(value.Name, BindingFlags.Public | BindingFlags.Instance);
                if (property == null || property.PropertyType != typeof(float) || !property.CanWrite)
                    throw new InvalidOperationException(where + ": unknown AI override \"" + value.Name + "\".");

                property.SetValue(profile, value.Value.GetSingle());
            }

            return profile;
        }

        private static JsonElement Required(JsonElement element, string name, string where)
        {
            if (!element.TryGetProperty(name, out JsonElement value))
                throw new KeyNotFoundException(where + ": missing \"" + name + "\".");
            return value;
        }

        private static string Optional(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out JsonElement value) ? value.GetString() : null;
        }

        private static T ParseEnum<T>(JsonElement value, string where) where T : struct, Enum
        {
            string text = value.GetString();
            if (!Enum.TryParse(text, ignoreCase: false, out T result) || !Enum.IsDefined(typeof(T), result))
                throw new FormatException(where + ": \"" + text + "\" is not a valid " + typeof(T).Name + ".");
            return result;
        }

        private static T[] LoadEnumArray<T>(JsonElement element, string name, string where) where T : struct, Enum
        {
            if (!element.TryGetProperty(name, out JsonElement array))
                return null;

            var result = new List<T>();
            foreach (JsonElement item in array.EnumerateArray())
                result.Add(ParseEnum<T>(item, where));
            return result.ToArray();
        }

        private static string[] LoadStringArray(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out JsonElement array))
                return null;

            var result = new List<string>();
            foreach (JsonElement item in array.EnumerateArray())
                result.Add(item.GetString());
            return result.ToArray();
        }

        private static void Register(Dictionary<string, EnemyArchetype> byAssetId, EnemyArchetype archetype, string[] aliases)
        {
            byAssetId[archetype.AssetId] = archetype;
            if (aliases == null)
                return;

            for (int i = 0; i < aliases.Length; i++)
            {
                string alias = aliases[i];
                if (string.IsNullOrWhiteSpace(alias))
                    continue;

                if (byAssetId.TryGetValue(alias, out EnemyArchetype existing) && !ReferenceEquals(existing, archetype))
                    throw new InvalidOperationException("alias \"" + alias + "\" is used by both " + existing.AssetId + " and " + archetype.AssetId + ".");

                byAssetId[alias] = archetype;
            }
        }
    }
}
