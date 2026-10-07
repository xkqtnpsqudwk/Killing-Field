namespace My2DEngine.Game.Core
{
    /// <summary>
    /// 카드·무기 이름과 설명 문장. 획득 메시지(GameLogic)와 카드 화면이 함께 쓴다.
    /// </summary>
    public static class CardText
    {
        public static string StatName(StatType stat)
        {
            return StatCardCatalog.Get(stat).Name;
        }

        public static string WeaponName(WeaponType type)
        {
            return WeaponPresentation.GetDisplayName(type);
        }

        /// <summary>카드에 적힌 이번 획득량(예: "+15%").</summary>
        public static string StatValue(StatType stat, float bonusValue)
        {
            return StatCardCatalog.Get(stat).FormatOfferValue(bonusValue);
        }

        /// <summary>지금까지 모은 누적량.</summary>
        public static string StatTotal(StatType stat, float totalBonus)
        {
            return StatCardCatalog.Get(stat).FormatTotalValue(totalBonus);
        }

        /// <summary>조건부 카드의 발동 조건. 상시 스탯이면 null.</summary>
        public static string StatCondition(StatType stat)
        {
            return StatCardCatalog.Get(stat).Condition;
        }

        public static string CategoryName(WeaponUpgradeCategory cat)
        {
            switch (cat)
            {
                case WeaponUpgradeCategory.Damage:      return "피해";
                case WeaponUpgradeCategory.Range:       return "사거리";
                case WeaponUpgradeCategory.Pellets:     return "탄환 수";
                case WeaponUpgradeCategory.FireRate:    return "발사 속도";
                case WeaponUpgradeCategory.Spread:      return "탄 퍼짐";
                case WeaponUpgradeCategory.Splash:      return "폭발 범위";
                case WeaponUpgradeCategory.AmmoDropBonus: return "탄 드랍";
                case WeaponUpgradeCategory.Special:     return "특수기";
                default:                                return "???";
            }
        }

        public static string WeaponUpgradeDesc(WeaponType type, WeaponUpgradeCategory cat, CardGrade grade)
        {
            if (cat == WeaponUpgradeCategory.Special)
            {
                switch (type)
                {
                    case WeaponType.BearKiller:   return "갈고리 - 적 끌어당기기 + 기절";
                    case WeaponType.HChainGun:    return "버스트 - 전탄 무재장전 연사";
                    case WeaponType.AutoCannon:   return "집속 포격 - 넓은 범위 고폭탄";
                    case WeaponType.DuelBerettas: return "피버 모드 - 전방 자동 제압";
                    default:                      return "특수기";
                }
            }

            bool isBlue = grade == CardGrade.Blue;
            switch (cat)
            {
                case WeaponUpgradeCategory.Damage:      return isBlue ? "+30% 피해" : "+60% 피해";
                case WeaponUpgradeCategory.Range:       return isBlue ? "+30% 사거리" : "+60% 사거리";
                case WeaponUpgradeCategory.Pellets:     return isBlue ? "+2 탄환" : "+4 탄환";
                case WeaponUpgradeCategory.FireRate:    return isBlue ? "-20% 쿨타임" : "-35% 쿨타임";
                case WeaponUpgradeCategory.Spread:      return isBlue ? "-30% 퍼짐" : "-55% 퍼짐";
                case WeaponUpgradeCategory.Splash:      return isBlue ? "+30% 폭발" : "+55% 폭발";
                case WeaponUpgradeCategory.AmmoDropBonus: return isBlue ? "+25% 드랍확률" : "+45% 드랍확률";
                default:                                return "???";
            }
        }
    }
}
