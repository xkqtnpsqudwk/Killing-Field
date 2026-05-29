using System;
using Microsoft.Data.Sqlite;
using System.IO;

namespace My2DEngine.Game.Core
{
    internal sealed class SqliteProgressionRepository
    {
        private const string DbFileName = @"Saves\roguelike_progression.db";

        private readonly string dbPath;

        public SqliteProgressionRepository()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            dbPath = Path.Combine(baseDir, DbFileName);
        }

        public PermanentProgressionData Load()
        {
            try
            {
                EnsureDatabase();
                return ReadRow();
            }
            catch
            {
                return PermanentProgressionData.CreateDefault();
            }
        }

        public void Save(PermanentProgressionData data)
        {
            if (data == null) return;

            try
            {
                EnsureDatabase();

                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT OR REPLACE INTO permanent_progression
                            (id, unspent_points, health_points, move_speed_points,
                             sense_value, luck_value, pistol_damage_points,
                             endless_mode_unlocked, has_seen_controls, updated_at)
                        VALUES
                            (1, @up, @hp, @ms, @sv, @lv, @pd, @em, @hsc, @ua)";

                    cmd.Parameters.AddWithValue("@up", data.UnspentPoints);
                    cmd.Parameters.AddWithValue("@hp", data.HealthPoints);
                    cmd.Parameters.AddWithValue("@ms", data.MoveSpeedPoints);
                    cmd.Parameters.AddWithValue("@sv", data.SenseValue);
                    cmd.Parameters.AddWithValue("@lv", data.LuckValue);
                    cmd.Parameters.AddWithValue("@pd", data.PistolDamagePoints);
                    cmd.Parameters.AddWithValue("@em", data.EndlessModeUnlocked ? 1 : 0);
                    cmd.Parameters.AddWithValue("@hsc", data.HasSeenControls ? 1 : 0);
                    cmd.Parameters.AddWithValue("@ua", DateTime.UtcNow.ToString("o"));

                    cmd.ExecuteNonQuery();
                }
            }
            catch
            {
            }
        }

        private void EnsureDatabase()
        {
            string dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            using (var conn = OpenConnection())
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS permanent_progression (
                            id                    INTEGER PRIMARY KEY CHECK(id = 1),
                            unspent_points        INTEGER NOT NULL DEFAULT 0,
                            health_points         INTEGER NOT NULL DEFAULT 0,
                            move_speed_points     INTEGER NOT NULL DEFAULT 0,
                            sense_value           REAL    NOT NULL DEFAULT 0.0,
                            luck_value            REAL    NOT NULL DEFAULT 0.0,
                            pistol_damage_points  INTEGER NOT NULL DEFAULT 0,
                            endless_mode_unlocked INTEGER NOT NULL DEFAULT 0,
                            has_seen_controls     INTEGER NOT NULL DEFAULT 0,
                            updated_at            TEXT    NOT NULL DEFAULT ''
                        )";
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS run_save (
                            id                  INTEGER PRIMARY KEY CHECK(id = 1),
                            floor               INTEGER NOT NULL,
                            player_health       REAL    NOT NULL,
                            bonus_max_health    REAL    NOT NULL DEFAULT 0,
                            bonus_move_speed    REAL    NOT NULL DEFAULT 0,
                            bonus_damage        REAL    NOT NULL DEFAULT 0,
                            bonus_ammo_drop     REAL    NOT NULL DEFAULT 0,
                            bonus_dash_cooldown REAL    NOT NULL DEFAULT 0,
                            bonus_coin_drop     REAL    NOT NULL DEFAULT 0,
                            bonus_life_steal    REAL    NOT NULL DEFAULT 0,
                            bonus_damage_reduction REAL NOT NULL DEFAULT 0,
                            bonus_shop_discount REAL NOT NULL DEFAULT 0,
                            bonus_kill_heal     REAL    NOT NULL DEFAULT 0,
                            bonus_kill_dash_refund REAL NOT NULL DEFAULT 0,
                            bonus_crit_chance   REAL    NOT NULL DEFAULT 0,
                            bonus_card_choice_bonus REAL NOT NULL DEFAULT 0,
                            bonus_shield_regen_rate REAL NOT NULL DEFAULT 0,
                            bonus_shield_regen_delay REAL NOT NULL DEFAULT 0,
                            bonus_shielded_damage REAL NOT NULL DEFAULT 0,
                            bonus_dash_strike_damage REAL NOT NULL DEFAULT 0,
                            player_shield       REAL    NOT NULL DEFAULT 100,
                            shield_regen_delay_timer REAL NOT NULL DEFAULT 0,
                            card_choice_bonus_offered INTEGER NOT NULL DEFAULT 0,
                            combat_floor_clears INTEGER NOT NULL DEFAULT 0,
                            boss_clear_growth   INTEGER NOT NULL DEFAULT 0,
                            owned_weapons_mask  INTEGER NOT NULL DEFAULT 1,
                            coin_count          INTEGER NOT NULL DEFAULT 0,
                            weapon_card_pool_count INTEGER NOT NULL DEFAULT 0,
                            rest_room_cooldown INTEGER NOT NULL DEFAULT 0,
                            current_weapon_type INTEGER NOT NULL DEFAULT 0,
                            weapon_ammo_state   TEXT    NOT NULL DEFAULT '',
                            weapon_upgrade_state TEXT   NOT NULL DEFAULT '',
                            run_stat_grade_state TEXT   NOT NULL DEFAULT '',
                            run_stat_pickup_state TEXT  NOT NULL DEFAULT '',
                            saved_at            TEXT    NOT NULL DEFAULT ''
                        )";
                    cmd.ExecuteNonQuery();
                }

                EnsureColumnExists(conn, "run_save", "combat_floor_clears", "INTEGER NOT NULL DEFAULT 0");
                EnsureColumnExists(conn, "run_save", "bonus_coin_drop", "REAL NOT NULL DEFAULT 0");
                EnsureColumnExists(conn, "run_save", "bonus_life_steal", "REAL NOT NULL DEFAULT 0");
                EnsureColumnExists(conn, "run_save", "bonus_damage_reduction", "REAL NOT NULL DEFAULT 0");
                EnsureColumnExists(conn, "run_save", "bonus_shop_discount", "REAL NOT NULL DEFAULT 0");
                EnsureColumnExists(conn, "run_save", "bonus_kill_heal", "REAL NOT NULL DEFAULT 0");
                EnsureColumnExists(conn, "run_save", "bonus_kill_dash_refund", "REAL NOT NULL DEFAULT 0");
                EnsureColumnExists(conn, "run_save", "bonus_crit_chance", "REAL NOT NULL DEFAULT 0");
                EnsureColumnExists(conn, "run_save", "bonus_card_choice_bonus", "REAL NOT NULL DEFAULT 0");
                EnsureColumnExists(conn, "run_save", "bonus_shield_regen_rate", "REAL NOT NULL DEFAULT 0");
                EnsureColumnExists(conn, "run_save", "bonus_shield_regen_delay", "REAL NOT NULL DEFAULT 0");
                EnsureColumnExists(conn, "run_save", "bonus_shielded_damage", "REAL NOT NULL DEFAULT 0");
                EnsureColumnExists(conn, "run_save", "bonus_dash_strike_damage", "REAL NOT NULL DEFAULT 0");
                EnsureColumnExists(conn, "run_save", "bonus_low_health_rage", "REAL NOT NULL DEFAULT 0");
                EnsureColumnExists(conn, "run_save", "bonus_kill_chain", "REAL NOT NULL DEFAULT 0");
                EnsureColumnExists(conn, "run_save", "bonus_explosive_specialist", "REAL NOT NULL DEFAULT 0");
                EnsureColumnExists(conn, "run_save", "bonus_low_ammo_rage", "REAL NOT NULL DEFAULT 0");
                EnsureColumnExists(conn, "run_save", "bonus_rapid_fire_chain", "REAL NOT NULL DEFAULT 0");
                EnsureColumnExists(conn, "run_save", "player_shield", "REAL NOT NULL DEFAULT 100");
                EnsureColumnExists(conn, "run_save", "shield_regen_delay_timer", "REAL NOT NULL DEFAULT 0");
                EnsureColumnExists(conn, "run_save", "card_choice_bonus_offered", "INTEGER NOT NULL DEFAULT 0");
                EnsureColumnExists(conn, "run_save", "coin_count", "INTEGER NOT NULL DEFAULT 0");
                EnsureColumnExists(conn, "run_save", "weapon_card_pool_count", "INTEGER NOT NULL DEFAULT 0");
                EnsureColumnExists(conn, "run_save", "rest_room_cooldown", "INTEGER NOT NULL DEFAULT 0");
                EnsureColumnExists(conn, "run_save", "current_weapon_type", "INTEGER NOT NULL DEFAULT 0");
                EnsureColumnExists(conn, "run_save", "weapon_ammo_state", "TEXT NOT NULL DEFAULT ''");
                EnsureColumnExists(conn, "run_save", "weapon_upgrade_state", "TEXT NOT NULL DEFAULT ''");
                EnsureColumnExists(conn, "run_save", "run_stat_grade_state", "TEXT NOT NULL DEFAULT ''");
                EnsureColumnExists(conn, "run_save", "run_stat_pickup_state", "TEXT NOT NULL DEFAULT ''");
                EnsureColumnExists(conn, "permanent_progression", "has_seen_controls", "INTEGER NOT NULL DEFAULT 0");
                EnsureColumnExists(conn, "run_records", "is_victory", "INTEGER NOT NULL DEFAULT 0");

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS run_records (
                            id               INTEGER PRIMARY KEY AUTOINCREMENT,
                            floor_reached    INTEGER NOT NULL DEFAULT 0,
                            enemies_killed   INTEGER NOT NULL DEFAULT 0,
                            bosses_killed    INTEGER NOT NULL DEFAULT 0,
                            duration_seconds INTEGER NOT NULL DEFAULT 0,
                            ended_at         TEXT    NOT NULL DEFAULT '',
                            is_victory       INTEGER NOT NULL DEFAULT 0
                        )";
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS game_settings (
                            id                      INTEGER PRIMARY KEY CHECK(id = 1),
                            fov_degrees             REAL    NOT NULL DEFAULT 80.0,
                            mouse_sensitivity       REAL    NOT NULL DEFAULT 0.003,
                            bgm_volume              INTEGER NOT NULL DEFAULT 100,
                            sfx_volume              INTEGER NOT NULL DEFAULT 100,
                            window_size_preset_idx  INTEGER NOT NULL DEFAULT 1,
                            updated_at              TEXT    NOT NULL DEFAULT ''
                        )";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public bool HasRunSave()
        {
            try
            {
                EnsureDatabase();
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM run_save WHERE id = 1";
                    return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
                }
            }
            catch
            {
                return false;
            }
        }

        public RunSaveData LoadRunSave()
        {
            try
            {
                EnsureDatabase();
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT floor, player_health,
                               bonus_max_health, bonus_move_speed, bonus_damage,
                               bonus_ammo_drop, bonus_dash_cooldown, bonus_coin_drop, bonus_life_steal,
                               bonus_damage_reduction, bonus_shop_discount, bonus_kill_heal,
                               bonus_kill_dash_refund, bonus_crit_chance, bonus_card_choice_bonus,
                               bonus_shield_regen_rate, bonus_shield_regen_delay,
                               bonus_shielded_damage, bonus_dash_strike_damage,
                               player_shield, shield_regen_delay_timer, card_choice_bonus_offered,
                               combat_floor_clears, boss_clear_growth, owned_weapons_mask, coin_count,
                               weapon_card_pool_count, rest_room_cooldown, current_weapon_type,
                               weapon_ammo_state, weapon_upgrade_state,
                               run_stat_grade_state, run_stat_pickup_state,
                               bonus_low_health_rage, bonus_kill_chain,
                               bonus_explosive_specialist, bonus_low_ammo_rage, bonus_rapid_fire_chain
                        FROM run_save WHERE id = 1";

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read()) return null;

                        return new RunSaveData
                        {
                            Floor               = reader.GetInt32(0),
                            PlayerHealth        = (float)reader.GetDouble(1),
                            BonusMaxHealth      = (float)reader.GetDouble(2),
                            BonusMoveSpeed      = (float)reader.GetDouble(3),
                            BonusDamage         = (float)reader.GetDouble(4),
                            BonusAmmoDropChance = (float)reader.GetDouble(5),
                            BonusDashCooldown   = (float)reader.GetDouble(6),
                            BonusCoinDropChance = (float)reader.GetDouble(7),
                            BonusLifeSteal      = (float)reader.GetDouble(8),
                            BonusDamageReduction = (float)reader.GetDouble(9),
                            BonusShopDiscount = (float)reader.GetDouble(10),
                            BonusKillHeal = (float)reader.GetDouble(11),
                            BonusKillDashCooldownRefund = (float)reader.GetDouble(12),
                            BonusCriticalChance = (float)reader.GetDouble(13),
                            BonusCardChoiceBonus = (float)reader.GetDouble(14),
                            BonusShieldRegenRate = (float)reader.GetDouble(15),
                            BonusShieldRegenDelayReduction = (float)reader.GetDouble(16),
                            BonusShieldedDamage = (float)reader.GetDouble(17),
                            BonusDashStrikeDamage = (float)reader.GetDouble(18),
                            PlayerShield = (float)reader.GetDouble(19),
                            ShieldRegenDelayTimer = (float)reader.GetDouble(20),
                            CardChoiceBonusOffered = reader.GetInt32(21) != 0,
                            ClearedCombatFloorCount = reader.GetInt32(22),
                            BossClearGrowthCount = reader.GetInt32(23),
                            OwnedWeaponsMask    = reader.GetInt32(24),
                            CoinCount           = reader.GetInt32(25),
                            WeaponCardPoolCount = reader.GetInt32(26),
                            RestRoomOpportunityCooldownActive = reader.GetInt32(27) != 0,
                            CurrentWeaponType   = reader.GetInt32(28),
                            WeaponAmmoState     = reader.GetString(29),
                            WeaponUpgradeState  = reader.GetString(30),
                            RunStatGradeState   = reader.GetString(31),
                            RunStatPickupState  = reader.GetString(32),
                            BonusLowHealthRage  = (float)reader.GetDouble(33),
                            BonusKillChain      = (float)reader.GetDouble(34),
                            BonusExplosiveSpecialist = (float)reader.GetDouble(35),
                            BonusLowAmmoRage    = (float)reader.GetDouble(36),
                            BonusRapidFireChain = (float)reader.GetDouble(37),
                        };
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        public void SaveRunSave(RunSaveData data)
        {
            if (data == null) return;
            try
            {
                EnsureDatabase();
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT OR REPLACE INTO run_save
                            (id, floor, player_health,
                             bonus_max_health, bonus_move_speed, bonus_damage,
                             bonus_ammo_drop, bonus_dash_cooldown, bonus_coin_drop, bonus_life_steal,
                             bonus_damage_reduction, bonus_shop_discount, bonus_kill_heal,
                             bonus_kill_dash_refund, bonus_crit_chance, bonus_card_choice_bonus,
                             bonus_shield_regen_rate, bonus_shield_regen_delay,
                             bonus_shielded_damage, bonus_dash_strike_damage,
                             player_shield, shield_regen_delay_timer, card_choice_bonus_offered,
                             combat_floor_clears, boss_clear_growth, owned_weapons_mask, coin_count,
                             weapon_card_pool_count, rest_room_cooldown, current_weapon_type,
                             weapon_ammo_state, weapon_upgrade_state,
                             run_stat_grade_state, run_stat_pickup_state,
                             bonus_low_health_rage, bonus_kill_chain,
                             bonus_explosive_specialist, bonus_low_ammo_rage, bonus_rapid_fire_chain,
                             saved_at)
                        VALUES
                            (1, @fl, @ph, @bh, @bm, @bd, @ba, @bdc, @bcd, @bls,
                             @bdr, @bsd, @bkh, @bkdr, @bcc, @bcb, @bsrr, @bsrd, @bsdm, @bdsd, @ps, @srdt, @bcbo,
                             @cf, @bg, @ow, @cc,
                             @wcp, @rrc, @cwt, @was, @wus, @rgs, @rps,
                             @blhr, @bkc, @bes, @blar, @brfc,
                             @sa)";

                    cmd.Parameters.AddWithValue("@fl", data.Floor);
                    cmd.Parameters.AddWithValue("@ph", data.PlayerHealth);
                    cmd.Parameters.AddWithValue("@bh", data.BonusMaxHealth);
                    cmd.Parameters.AddWithValue("@bm", data.BonusMoveSpeed);
                    cmd.Parameters.AddWithValue("@bd", data.BonusDamage);
                    cmd.Parameters.AddWithValue("@ba", data.BonusAmmoDropChance);
                    cmd.Parameters.AddWithValue("@bdc", data.BonusDashCooldown);
                    cmd.Parameters.AddWithValue("@bcd", data.BonusCoinDropChance);
                    cmd.Parameters.AddWithValue("@bls", data.BonusLifeSteal);
                    cmd.Parameters.AddWithValue("@bdr", data.BonusDamageReduction);
                    cmd.Parameters.AddWithValue("@bsd", data.BonusShopDiscount);
                    cmd.Parameters.AddWithValue("@bkh", data.BonusKillHeal);
                    cmd.Parameters.AddWithValue("@bkdr", data.BonusKillDashCooldownRefund);
                    cmd.Parameters.AddWithValue("@bcc", data.BonusCriticalChance);
                    cmd.Parameters.AddWithValue("@bcb", data.BonusCardChoiceBonus);
                    cmd.Parameters.AddWithValue("@bsrr", data.BonusShieldRegenRate);
                    cmd.Parameters.AddWithValue("@bsrd", data.BonusShieldRegenDelayReduction);
                    cmd.Parameters.AddWithValue("@bsdm", data.BonusShieldedDamage);
                    cmd.Parameters.AddWithValue("@bdsd", data.BonusDashStrikeDamage);
                    cmd.Parameters.AddWithValue("@ps", data.PlayerShield);
                    cmd.Parameters.AddWithValue("@srdt", data.ShieldRegenDelayTimer);
                    cmd.Parameters.AddWithValue("@bcbo", data.CardChoiceBonusOffered ? 1 : 0);
                    cmd.Parameters.AddWithValue("@cf", data.ClearedCombatFloorCount);
                    cmd.Parameters.AddWithValue("@bg", data.BossClearGrowthCount);
                    cmd.Parameters.AddWithValue("@ow", data.OwnedWeaponsMask);
                    cmd.Parameters.AddWithValue("@cc", data.CoinCount);
                    cmd.Parameters.AddWithValue("@wcp", data.WeaponCardPoolCount);
                    cmd.Parameters.AddWithValue("@rrc", data.RestRoomOpportunityCooldownActive ? 1 : 0);
                    cmd.Parameters.AddWithValue("@cwt", data.CurrentWeaponType);
                    cmd.Parameters.AddWithValue("@was", data.WeaponAmmoState ?? string.Empty);
                    cmd.Parameters.AddWithValue("@wus", data.WeaponUpgradeState ?? string.Empty);
                    cmd.Parameters.AddWithValue("@rgs", data.RunStatGradeState ?? string.Empty);
                    cmd.Parameters.AddWithValue("@rps", data.RunStatPickupState ?? string.Empty);
                    cmd.Parameters.AddWithValue("@blhr", data.BonusLowHealthRage);
                    cmd.Parameters.AddWithValue("@bkc", data.BonusKillChain);
                    cmd.Parameters.AddWithValue("@bes", data.BonusExplosiveSpecialist);
                    cmd.Parameters.AddWithValue("@blar", data.BonusLowAmmoRage);
                    cmd.Parameters.AddWithValue("@brfc", data.BonusRapidFireChain);
                    cmd.Parameters.AddWithValue("@sa", DateTime.UtcNow.ToString("o"));

                    cmd.ExecuteNonQuery();
                }
            }
            catch
            {
            }
        }

        public void DeleteRunSave()
        {
            try
            {
                EnsureDatabase();
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM run_save WHERE id = 1";
                    cmd.ExecuteNonQuery();
                }
            }
            catch
            {
            }
        }

        public void SaveRunRecord(RunRecord record)
        {
            if (record == null) return;
            try
            {
                EnsureDatabase();
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO run_records
                            (floor_reached, enemies_killed, bosses_killed, duration_seconds, ended_at, is_victory)
                        VALUES
                            (@fr, @ek, @bk, @ds, @ea, @iv)";

                    cmd.Parameters.AddWithValue("@fr", record.FloorReached);
                    cmd.Parameters.AddWithValue("@ek", record.EnemiesKilled);
                    cmd.Parameters.AddWithValue("@bk", record.BossesKilled);
                    cmd.Parameters.AddWithValue("@ds", record.DurationSeconds);
                    cmd.Parameters.AddWithValue("@ea", record.EndedAt ?? DateTime.UtcNow.ToString("o"));
                    cmd.Parameters.AddWithValue("@iv", record.IsVictory ? 1 : 0);

                    cmd.ExecuteNonQuery();
                }
            }
            catch
            {
            }
        }

        public RunRecord[] LoadRunRecords(int limit)
        {
            try
            {
                EnsureDatabase();
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT id, floor_reached, enemies_killed, bosses_killed, duration_seconds, ended_at, is_victory
                        FROM run_records
                        ORDER BY id DESC
                        LIMIT @lim";
                    cmd.Parameters.AddWithValue("@lim", limit);

                    var list = new System.Collections.Generic.List<RunRecord>();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new RunRecord
                            {
                                Id             = reader.GetInt32(0),
                                FloorReached   = reader.GetInt32(1),
                                EnemiesKilled  = reader.GetInt32(2),
                                BossesKilled   = reader.GetInt32(3),
                                DurationSeconds = reader.GetInt32(4),
                                EndedAt        = reader.GetString(5),
                                IsVictory      = reader.GetInt32(6) != 0,
                            });
                        }
                    }
                    return list.ToArray();
                }
            }
            catch
            {
                return new RunRecord[0];
            }
        }

        public GameSettings LoadSettings()
        {
            try
            {
                EnsureDatabase();
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT fov_degrees, mouse_sensitivity, bgm_volume,
                               sfx_volume, window_size_preset_idx
                        FROM game_settings WHERE id = 1";

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read()) return new GameSettings();

                        var s = new GameSettings
                        {
                            FovDegrees            = (float)reader.GetDouble(0),
                            MouseSensitivity      = (float)reader.GetDouble(1),
                            BgmVolume             = reader.GetInt32(2),
                            SfxVolume             = reader.GetInt32(3),
                            WindowSizePresetIndex = reader.GetInt32(4),
                        };
                        s.Sanitize();
                        return s;
                    }
                }
            }
            catch
            {
                return new GameSettings();
            }
        }

        public void SaveSettings(GameSettings settings)
        {
            if (settings == null) return;
            try
            {
                EnsureDatabase();
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT OR REPLACE INTO game_settings
                            (id, fov_degrees, mouse_sensitivity, bgm_volume,
                             sfx_volume, window_size_preset_idx, updated_at)
                        VALUES
                            (1, @fov, @sens, @bgm, @sfx, @win, @ua)";

                    cmd.Parameters.AddWithValue("@fov",  settings.FovDegrees);
                    cmd.Parameters.AddWithValue("@sens", settings.MouseSensitivity);
                    cmd.Parameters.AddWithValue("@bgm",  settings.BgmVolume);
                    cmd.Parameters.AddWithValue("@sfx",  settings.SfxVolume);
                    cmd.Parameters.AddWithValue("@win",  settings.WindowSizePresetIndex);
                    cmd.Parameters.AddWithValue("@ua",   DateTime.UtcNow.ToString("o"));

                    cmd.ExecuteNonQuery();
                }
            }
            catch
            {
            }
        }

        private bool HasRow()
        {
            using (var conn = OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM permanent_progression WHERE id = 1";
                return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
            }
        }

        private PermanentProgressionData ReadRow()
        {
            using (var conn = OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT unspent_points, health_points, move_speed_points,
                           sense_value, luck_value, pistol_damage_points,
                           endless_mode_unlocked, has_seen_controls
                    FROM permanent_progression
                    WHERE id = 1";

                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                        return PermanentProgressionData.CreateDefault();

                    return new PermanentProgressionData
                    {
                        UnspentPoints       = reader.GetInt32(0),
                        HealthPoints        = reader.GetInt32(1),
                        MoveSpeedPoints     = reader.GetInt32(2),
                        SenseValue          = (float)reader.GetDouble(3),
                        LuckValue           = (float)reader.GetDouble(4),
                        PistolDamagePoints  = reader.GetInt32(5),
                        EndlessModeUnlocked = reader.GetInt32(6) != 0,
                        HasSeenControls     = reader.GetInt32(7) != 0,
                    };
                }
            }
        }

        private static void EnsureColumnExists(SqliteConnection conn, string tableName, string columnName, string columnDefinition)
        {
            if (conn == null || string.IsNullOrWhiteSpace(tableName) || string.IsNullOrWhiteSpace(columnName))
            {
                return;
            }

            if (ColumnExists(conn, tableName, columnName))
            {
                return;
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition}";
                cmd.ExecuteNonQuery();
            }
        }

        private static bool ColumnExists(SqliteConnection conn, string tableName, string columnName)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"PRAGMA table_info({tableName})";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private SqliteConnection OpenConnection()
        {
            var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();
            return conn;
        }
    }
}
