using System;
using My2DEngine.Game.Config;
using My2DEngine.Game.Rendering.Ui.Screens;

namespace My2DEngine.Game.Core
{
    /// <summary>
    /// 카드 계열 시너지와 카드 고르기 화면의 다시 뽑기·건너뛰기.
    /// 시너지 단계는 저장하지 않고 스탯 카드 획득 횟수에서 매번 계산한다.
    /// </summary>
    public partial class GameLogic
    {
        /// <summary>이번 카드 보상에서 다시 뽑은 횟수. 비용이 점점 오른다.</summary>
        private int cardRerollCount;

        /// <summary>대시 계열 시너지: 대시가 끝난 뒤에도 피해를 받지 않는 남은 시간.</summary>
        private float dashGraceTimer;

        /// <summary>계열에 속한 스탯 카드를 런에서 모은 장 수.</summary>
        private int GetFamilyCardCount(StatFamily family)
        {
            int count = 0;
            for (int i = 0; i < runStatPickupCount.Length; i++)
            {
                if (StatCardCatalog.FamilyOf((StatType)i) == family)
                {
                    count += Math.Max(0, runStatPickupCount[i]);
                }
            }

            return count;
        }

        /// <summary>계열 시너지 단계(0, 1, 2).</summary>
        private int GetSynergyTier(StatFamily family)
        {
            return family == StatFamily.None ? 0 : StatSynergyCatalog.TierFor(GetFamilyCardCount(family));
        }

        /// <summary>계열별 카드 수(StatFamily 순서). 카드 화면과 스탯 화면에 넘긴다.</summary>
        private int[] BuildFamilyCounts()
        {
            int[] counts = new int[StatSynergyCatalog.All.Length + 1];
            foreach (StatFamily family in StatSynergyCatalog.All)
            {
                counts[(int)family] = GetFamilyCardCount(family);
            }

            return counts;
        }

        /// <summary>카드를 고르기 전후로 계열 단계가 올랐으면 알린다.</summary>
        private void AnnounceSynergyTierUp(StatFamily family, int tierBefore)
        {
            int tier = GetSynergyTier(family);
            if (family == StatFamily.None || tier <= tierBefore)
            {
                return;
            }

            StatFamilyDefinition definition = StatSynergyCatalog.Get(family);
            string bonus = tier >= 2 ? definition.Tier2 : definition.Tier1;
            stageStatus.Show($"시너지 {definition.Name} {(tier >= 2 ? "II" : "I")}: {bonus}", 4f);
        }

        // ── 보너스 ──────────────────────────────────────────────

        private float SynergyFireRateBonus(float bonus)
        {
            if (GetSynergyTier(StatFamily.Firepower) < 1)
            {
                return bonus;
            }

            return 1f - ((1f - bonus) * (1f - SynergyConfig.FirepowerFireRateBonus));
        }

        private float SynergyDamageMult()
        {
            return GetSynergyTier(StatFamily.Firepower) >= 2 ? SynergyConfig.FirepowerDamageMult : 1f;
        }

        private float SynergyDashCooldownBonus()
        {
            return GetSynergyTier(StatFamily.Dash) >= 2 ? SynergyConfig.DashCooldownBonus : 0f;
        }

        private float SynergyMaxShieldMult()
        {
            return GetSynergyTier(StatFamily.Shield) >= 1 ? SynergyConfig.ShieldMaxMult : 1f;
        }

        private float SynergyShieldDelayMult()
        {
            return GetSynergyTier(StatFamily.Shield) >= 2 ? SynergyConfig.ShieldDelayMult : 1f;
        }

        private float SynergyMaxHealthMult()
        {
            return GetSynergyTier(StatFamily.Survival) >= 1 ? SynergyConfig.SurvivalMaxHealthMult : 1f;
        }

        /// <summary>생존 II: 체력이 낮을 때 받는 피해를 줄인다.</summary>
        private float ApplySynergyIncoming(float damage)
        {
            if (GetSynergyTier(StatFamily.Survival) >= 2 && player.MaxHealth > 0f &&
                player.Health / player.MaxHealth < SynergyConfig.SurvivalLowHealthThreshold)
            {
                return damage * SynergyConfig.SurvivalLowHealthIncomingMult;
            }

            return damage;
        }

        /// <summary>처치 I·II: 처치할 때 보호막·체력을 조금 채운다.</summary>
        private void ApplySynergyOnKill()
        {
            int tier = GetSynergyTier(StatFamily.Slayer);
            if (tier >= 1)
            {
                player.AddShield(SynergyConfig.SlayerKillShield);
            }

            if (tier >= 2)
            {
                player.Health = Math.Min(player.MaxHealth, player.Health + SynergyConfig.SlayerKillHealth);
            }
        }

        /// <summary>대시 I: 대시가 끝난 뒤에도 잠깐 피해를 받지 않는다.</summary>
        private void StartSynergyDashGrace()
        {
            dashGraceTimer = GetSynergyTier(StatFamily.Dash) >= 1
                ? PlayerConfig.DashDuration + SynergyConfig.DashGraceSeconds
                : 0f;
        }

        private void UpdateSynergy(float dt)
        {
            dashGraceTimer = Math.Max(0f, dashGraceTimer - Math.Max(0f, dt));
        }

        // ── 다시 뽑기·건너뛰기 ─────────────────────────────────

        /// <summary>지금 다시 뽑기 비용.</summary>
        private int GetCardRerollCost()
        {
            return SynergyConfig.RerollBaseCost + cardRerollCount * SynergyConfig.RerollCostStep;
        }

        /// <summary>보스 보상(무기 카드)은 다시 뽑거나 건너뛸 수 없다.</summary>
        private bool CardRewardAllowsRerollAndSkip => cardRewardActive && !cardRewardWasBossRoom;

        /// <summary>코인을 내고 카드를 새로 뽑는다. 지금 카드는 모두 불타고 새 카드가 떨어진다.</summary>
        private bool TryRerollCards()
        {
            if (!CardRewardAllowsRerollAndSkip || !player.TrySpendCoins(GetCardRerollCost()))
            {
                return false;
            }

            cardRerollCount++;
            CardRewardScreen.BeginBurnAll(BuildCardRewardView());
            GenerateCardOffers();
            cardMotion.Hold(cardMotion.BurnRemaining * 0.8f);
            stageStatus.Show($"카드를 다시 뽑았다 (다음 비용 {GetCardRerollCost()} 코인)", 2.5f);
            return true;
        }

        /// <summary>카드를 받지 않고 코인을 받는다. 카드는 모두 불타고 다음 단계로 넘어간다.</summary>
        private bool TrySkipCards()
        {
            if (!CardRewardAllowsRerollAndSkip)
            {
                return false;
            }

            CardRewardScreen.BeginBurnAll(BuildCardRewardView());
            player.AddCoins(SynergyConfig.SkipCoinReward);
            stageStatus.Show($"카드를 태우고 코인 {SynergyConfig.SkipCoinReward}개를 얻었다", 2.5f);
            FinishCardReward();
            return true;
        }
    }
}
