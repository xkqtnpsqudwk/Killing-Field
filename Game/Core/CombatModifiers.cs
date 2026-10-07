using System;
using My2DEngine.Game.Config;

namespace My2DEngine.Game.Core
{
    /// <summary>
    /// 스탯 카드로 붙는 전투 보정: 조건부 피해 증가, 치명타, 받는 피해 감소, 흡혈 비율.
    /// 대시 일격·연속 처치 창과 연사 누적 같은 짧은 상태도 여기서 센다.
    /// 카드 누적량은 생성자로 받은 조회 함수로 읽는다(GameLogic.GetRunStatBonus).
    /// </summary>
    internal sealed class CombatModifiers
    {
        private readonly Func<StatType, float> bonus;

        public CombatModifiers(Func<StatType, float> bonus)
        {
            this.bonus = bonus;
        }

        /// <summary>대시 직후 피해 증가 카드가 유효한 남은 시간(초).</summary>
        internal float DashStrikeTimer { get; set; }

        /// <summary>처치 직후 피해 증가 카드가 유효한 남은 시간(초).</summary>
        internal float KillChainTimer { get; set; }

        /// <summary>연사 가속 카드의 연속 명중 수.</summary>
        internal int RapidFireStreak { get; set; }

        /// <summary>연속 명중이 끊기기까지 남은 시간. 0이 되면 누적이 0으로 돌아간다.</summary>
        internal float RapidFireDecayTimer { get; set; }

        /// <summary>대시하면 대시 일격 카드가 있을 때만 창을 연다.</summary>
        public void OnDash()
        {
            DashStrikeTimer = bonus(StatType.DashStrikeDamage) > 0f
                ? RewardConfig.DashStrikeDamageWindow
                : 0f;
        }

        /// <summary>적을 맞히면 연사 누적을 올리고, 죽였으면 연속 처치 창을 연다(해당 카드가 있을 때만).</summary>
        public void OnEnemyHit(bool killed)
        {
            if (bonus(StatType.RapidFireChain) > 0f)
            {
                RapidFireStreak++;
                RapidFireDecayTimer = RewardConfig.RapidFireChainStreakDecayTime;
            }

            if (killed && bonus(StatType.KillChain) > 0f)
            {
                KillChainTimer = RewardConfig.KillChainWindow;
            }
        }

        public void Update(float dt)
        {
            float step = Math.Max(0f, dt);
            DashStrikeTimer = DashStrikeTimer <= 0f ? 0f : Math.Max(0f, DashStrikeTimer - step);

            if (KillChainTimer > 0f)
            {
                KillChainTimer = Math.Max(0f, KillChainTimer - step);
            }

            if (RapidFireDecayTimer <= 0f)
            {
                RapidFireStreak = 0;
                return;
            }

            RapidFireDecayTimer = Math.Max(0f, RapidFireDecayTimer - step);
            if (RapidFireDecayTimer <= 0f)
            {
                RapidFireStreak = 0;
            }
        }

        public void Reset()
        {
            DashStrikeTimer = 0f;
            KillChainTimer = 0f;
            RapidFireStreak = 0;
            RapidFireDecayTimer = 0f;
        }

        /// <summary>
        /// 조건부 피해 카드를 적용한 피해. 조건이 맞는 카드의 보너스를 모두 더한 뒤 원 피해에 한 번만 곱한다.
        /// 카드마다 상한은 RewardConfig에 있다.
        /// </summary>
        public float ApplyOutgoing(float damage, Player player, Weapon weapon)
        {
            if (damage <= 0f)
            {
                return 0f;
            }

            float multiplier = 1f;

            if (player != null && player.Shield > 0f)
            {
                multiplier += Capped(StatType.ShieldedDamage, RewardConfig.ShieldedDamageBonusCap);
            }

            if (DashStrikeTimer > 0f)
            {
                multiplier += Capped(StatType.DashStrikeDamage, RewardConfig.DashStrikeDamageBonusCap);
            }

            if (player != null && player.MaxHealth > 0f &&
                player.Health / player.MaxHealth < RewardConfig.LowHealthRageThreshold)
            {
                multiplier += Capped(StatType.LowHealthRage, RewardConfig.LowHealthRageBonusCap);
            }

            if (KillChainTimer > 0f)
            {
                multiplier += Capped(StatType.KillChain, RewardConfig.KillChainBonusCap);
            }

            if (weapon != null && weapon.CurrentType == WeaponType.AutoCannon)
            {
                multiplier += Capped(StatType.ExplosiveSpecialist, RewardConfig.ExplosiveSpecialistBonusCap);
            }

            if (weapon != null)
            {
                int maxAmmo = weapon.GetMaxAmmo(weapon.CurrentType);
                if (maxAmmo > 0 && weapon.CurrentAmmo <= (int)(maxAmmo * RewardConfig.LowAmmoRageThreshold))
                {
                    multiplier += Capped(StatType.LowAmmoRage, RewardConfig.LowAmmoRageBonusCap);
                }
            }

            if (RapidFireStreak >= RewardConfig.RapidFireChainMinStreak)
            {
                multiplier += Capped(StatType.RapidFireChain, RewardConfig.RapidFireChainBonusCap);
            }

            return Math.Max(0f, damage * multiplier);
        }

        /// <summary>치명타 확률만큼 피해를 2배로. 확률이 0이면 난수를 쓰지 않는다.</summary>
        public float ApplyCritical(float damage, Random random)
        {
            float criticalChance = Math.Max(0f, Math.Min(1f, bonus(StatType.CriticalChance)));
            if (criticalChance <= 0f || random.NextDouble() >= criticalChance)
            {
                return damage;
            }

            return damage * 2f;
        }

        /// <summary>받는 피해에 피해 감소 카드(최대 65%)를 적용한다.</summary>
        public float ApplyIncoming(float damage)
        {
            if (damage <= 0f)
            {
                return 0f;
            }

            float reduction = Math.Max(0f, Math.Min(0.65f, bonus(StatType.DamageReduction)));
            return Math.Max(0f, damage * (1f - reduction));
        }

        /// <summary>흡혈 비율. 독 안개 방에서는 환경 위험이 무력화되지 않게 절반으로 줄인다.</summary>
        public float LifeStealRatio(bool toxicMist)
        {
            float ratio = Math.Max(0f, Math.Min(1f, bonus(StatType.LifeSteal)));
            if (toxicMist)
            {
                ratio *= 0.5f;
            }

            return Math.Max(0f, Math.Min(1f, ratio));
        }

        private float Capped(StatType stat, float cap)
        {
            return Math.Max(0f, Math.Min(cap, bonus(stat)));
        }
    }
}
