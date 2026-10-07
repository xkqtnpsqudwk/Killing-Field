using System;
using My2DEngine.Game.Config;
using My2DEngine.Game.Map;

namespace My2DEngine.Game.Core
{
    /// <summary>
    /// 보상 아이템의 양·희귀도·가격 표. 상태 없이 입력만 보고 계산한다.
    /// </summary>
    internal static class RewardRules
    {
        /// <summary>탄약 상자 하나가 주는 탄약 수. 희귀도 배율은 1 미만으로 내려가지 않는다.</summary>
        public static int AmmoPickupAmount(WeaponType type, float effectMultiplier)
        {
            float clampedMultiplier = Math.Max(1f, effectMultiplier);
            return Math.Max(1, (int)Math.Ceiling(AmmoPickupBaseAmount(type) * clampedMultiplier));
        }

        /// <summary>무기별 기본 탄약 지급량.</summary>
        public static int AmmoPickupBaseAmount(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.AMPistol: return 12;
                case WeaponType.BearKiller: return 2;
                case WeaponType.HChainGun: return 18;
                case WeaponType.AutoCannon: return 3;
                case WeaponType.DuelBerettas: return 10;
                default: return 0;
            }
        }

        /// <summary>전투 방 클리어 보상 종류. 회복 보상이 빠진 뒤로 탄약 보급만 남았다.</summary>
        public static RewardPickupKind RoomRewardKind(StageRoom room)
        {
            return RewardPickupKind.AmmoPack;
        }

        /// <summary>
        /// 방 클리어 보상의 희귀도를 굴린다. 코인과 카드는 희귀도가 없다.
        /// 보스 방일수록 Epic·Rare가 잘 나온다. 난수를 한 번 쓴다(코인·카드는 쓰지 않는다).
        /// </summary>
        public static RewardPickupRarity RollRarity(StageRoom room, RewardPickupKind kind, Random random)
        {
            if (kind == RewardPickupKind.Coin || kind == RewardPickupKind.Card)
            {
                return RewardPickupRarity.None;
            }

            int roll = random.Next(100);
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

        /// <summary>희귀도별 효과 배율. 코인·카드·희귀도 없음은 1.</summary>
        public static float EffectMultiplier(RewardPickupKind kind, RewardPickupRarity rarity)
        {
            if (kind == RewardPickupKind.Coin || kind == RewardPickupKind.Card || rarity == RewardPickupRarity.None)
            {
                return 1f;
            }

            switch (rarity)
            {
                case RewardPickupRarity.Rare: return 1.8f;
                case RewardPickupRarity.Epic: return 2.2f;
                default: return 1.5f;
            }
        }

        /// <summary>카드 상점 가격. 등급과 보스 처치 수만큼 오르고, 상점 할인 카드로 최대 50% 내린다.</summary>
        public static int RestShopCardCost(CardGrade grade, int bossClearCount, float shopDiscountBonus)
        {
            int gradeCost = Math.Max(0, (int)grade) * RewardConfig.RestShopCardCostPerGrade;
            int baseCost = RewardConfig.RestShopCardBaseCost +
                gradeCost +
                Math.Max(0, bossClearCount) * RewardConfig.RestShopCostIncreasePerBossClear;
            float discount = Math.Max(0f, Math.Min(0.50f, shopDiscountBonus));
            return Math.Max(1, (int)Math.Ceiling(baseCost * (1f - discount)));
        }
    }
}
