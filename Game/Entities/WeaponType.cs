namespace My2DEngine.Game
{
    /// <summary>
    /// 무기 종류 열거형. 1~5번 키에 각각 매핑된다.
    /// </summary>
    public enum WeaponType
    {
        AMPistol = 0,  // 1번 - 단발
        BearKiller = 1,  // 2번 - 단발 (다중 탄환)
        HChainGun = 2,  // 3번 - 연사
        AutoCannon = 3,  // 4번 - 단발 (범위 폭발)
        DuelBerettas = 4   // 5번 - 단발 차지샷
    }

    /// <summary>
    /// 무기 업그레이드 카드 카테고리.
    /// 무기별로 적용 가능한 카테고리가 다르다.
    /// </summary>
    public enum WeaponUpgradeCategory
    {
        /// <summary>모든 비 피스톨 무기 공통 - 기본 피해 증가.</summary>
        Damage = 0,
        /// <summary>BearKiller / DuelBerettas - 사거리 증가.</summary>
        Range = 1,
        /// <summary>BearKiller 전용 - 발사 탄환 수 증가.</summary>
        Pellets = 2,
        /// <summary>HChainGun / DuelBerettas - 발사 속도(차지 속도) 증가.</summary>
        FireRate = 3,
        /// <summary>HChainGun 전용 - 탄 퍼짐 감소.</summary>
        Spread = 4,
        /// <summary>AutoCannon 전용 - 폭발 범위 증가.</summary>
        Splash = 5,
        /// <summary>AutoCannon 전용 - 탄약 드랍 확률 증가.</summary>
        AmmoDropBonus = 6,
        /// <summary>무기별 Red 카드 전용 - 특수기 해금.</summary>
        Special = 7
    }

    /// <summary>
    /// 내부 WeaponType 값을 현재 적용 중인 외부 리소스 명칭으로 변환한다.
    /// 게임 로직 enum은 유지하고, HUD/카드/픽업 표기만 이 매핑을 사용한다.
    /// </summary>
    internal static class WeaponPresentation
    {
        public static string GetDisplayName(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.AMPistol:
                    return ".44 AMP";
                case WeaponType.BearKiller:
                    return "BearKiller";
                case WeaponType.HChainGun:
                    return "Heavy Chaingun";
                case WeaponType.AutoCannon:
                    return "Auto Cannon";
                case WeaponType.DuelBerettas:
                    return "Dual Berettas";
                default:
                    return "Weapon";
            }
        }

        public static string GetHudName(WeaponType type)
        {
            return GetDisplayName(type).ToUpperInvariant();
        }
    }
}
