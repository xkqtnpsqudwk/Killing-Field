using System.Drawing;

namespace My2DEngine.Game
{
    /// <summary>
    /// 보상 카드에 적용되는 스탯 종류.
    /// </summary>
    public enum StatType
    {
        MaxHealth     = 0,
        MoveSpeed     = 1,
        DashCooldown  = 2,
        AmmoDropChance = 3,
        Damage        = 4,
        CoinDropChance = 5
    }

    /// <summary>
    /// 카드 등급. 누적 보너스와 색상이 등급마다 다르다.
    /// </summary>
    public enum CardGrade { White = 0, Green = 1, Blue = 2, Purple = 3, Red = 4 }

    /// <summary>
    /// 카드 등급 관련 정적 헬퍼.
    /// </summary>
    public static class CardGradeHelper
    {
        /// <summary>등급별 보너스 값 (1% ~ 5%). 스탯 카드 효과에 사용된다.</summary>
        public static float GetBonusValue(CardGrade grade)
        {
            switch (grade)
            {
                case CardGrade.White:  return 0.01f;
                case CardGrade.Green:  return 0.02f;
                case CardGrade.Blue:   return 0.03f;
                case CardGrade.Purple: return 0.04f;
                case CardGrade.Red:    return 0.05f;
                default: return 0f;
            }
        }

        /// <summary>등급 한글 이름.</summary>
        public static string GetGradeName(CardGrade grade)
        {
            switch (grade)
            {
                case CardGrade.White:  return "White";
                case CardGrade.Green:  return "Green";
                case CardGrade.Blue:   return "Blue";
                case CardGrade.Purple: return "Purple";
                case CardGrade.Red:    return "Red";
                default: return "???";
            }
        }

        /// <summary>등급 색상 (UI 표시용).</summary>
        public static Color GetGradeColor(CardGrade grade)
        {
            switch (grade)
            {
                case CardGrade.White:  return Color.FromArgb(255, 230, 230, 230);
                case CardGrade.Green:  return Color.FromArgb(255, 120, 215, 120);
                case CardGrade.Blue:   return Color.FromArgb(255, 100, 185, 255);
                case CardGrade.Purple: return Color.FromArgb(255, 185, 120, 255);
                case CardGrade.Red:    return Color.FromArgb(255, 255, 100, 80);
                default: return Color.White;
            }
        }
    }

    /// <summary>
    /// 플레이어에게 제시되는 단일 보상 카드 데이터.
    /// IsWeaponCard에 따라 스탯 카드 or 무기 업그레이드 카드로 해석한다.
    /// </summary>
    public sealed class RewardCardOffer
    {
        /// <summary>무기 업그레이드 카드이면 true, 스탯 카드이면 false.</summary>
        public bool IsWeaponCard { get; set; }

        // ── 스탯 카드 전용 ─────────────────────────────────
        /// <summary>업그레이드할 스탯 종류.</summary>
        public StatType StatType { get; set; }
        /// <summary>선택 시 이 카드가 나타내는 등급 (이미 해당 등급 이상이면 업그레이드).</summary>
        public CardGrade Grade { get; set; }
        /// <summary>캡 적용 후 실제로 부여되는 스탯 증가량.</summary>
        public float StatBonusValue { get; set; }

        // ── 무기 카드 전용 ─────────────────────────────────
        /// <summary>업그레이드할 무기 종류.</summary>
        public WeaponType WeaponType { get; set; }
        /// <summary>업그레이드할 카테고리.</summary>
        public WeaponUpgradeCategory WeaponCategory { get; set; }
        /// <summary>부여할 업그레이드 등급 (Blue, Purple, Red).</summary>
        public CardGrade WeaponGrade { get; set; }
    }
}
