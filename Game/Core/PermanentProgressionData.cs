using System;
using System.Runtime.Serialization;
using My2DEngine.Game.Config;

namespace My2DEngine.Game.Core
{
    /// <summary>
    /// 런 간 유지되는 영구 스탯 포인트와 배분 결과를 저장하는 데이터 계약 모델이다.
    /// JSON 파일 하나에 그대로 직렬화/역직렬화된다.
    /// </summary>
    [DataContract]
    public sealed class PermanentProgressionData
    {
        // ─── 레벨 임계값 (누적 값 기준, 포인트 1개 = 0.1) ────────────
        // 간격 패턴: 0.5, 1.0, 1.5, 2.0, ... (매 레벨업마다 0.5씩 증가)
        // 감각 5레벨 달성: 5+10+15+20+25 = 75포인트
        // 운 10레벨 달성: 5+10+...+50 = 275포인트
        private static readonly float[] SenseLevelThresholds =
            { 0.5f, 1.5f, 3.0f, 5.0f, 7.5f };

        private static readonly float[] LuckLevelThresholds =
            { 0.5f, 1.5f, 3.0f, 5.0f, 7.5f, 10.5f, 14.0f, 18.0f, 22.5f, 27.5f };

        [DataMember(Name = "unspentPoints")]
        public int UnspentPoints { get; set; }

        [DataMember(Name = "healthPoints")]
        public int HealthPoints { get; set; }

        [DataMember(Name = "moveSpeedPoints")]
        public int MoveSpeedPoints { get; set; }

        [DataMember(Name = "senseValue")]
        public float SenseValue { get; set; }

        [DataMember(Name = "luckValue")]
        public float LuckValue { get; set; }

        [DataMember(Name = "pistolDamagePoints")]
        public int PistolDamagePoints { get; set; }

        [DataMember(Name = "endlessModeUnlocked")]
        public bool EndlessModeUnlocked { get; set; }

        public static PermanentProgressionData CreateDefault()
        {
            return new PermanentProgressionData();
        }

        public void Sanitize()
        {
            if (UnspentPoints < 0) UnspentPoints = 0;
            if (HealthPoints < 0) HealthPoints = 0;
            if (MoveSpeedPoints < 0) MoveSpeedPoints = 0;
            if (PistolDamagePoints < 0) PistolDamagePoints = 0;

            SenseValue = ClampToStep(SenseValue, 0f, GameConfig.PermanentSenseMax);
            LuckValue = ClampToStep(LuckValue, 0f, GameConfig.PermanentLuckMax);
        }

        public float GetHealthBonus()
        {
            return HealthPoints * GameConfig.PermanentHealthPerPoint;
        }

        public float GetMoveSpeedBonus()
        {
            return MoveSpeedPoints * GameConfig.PermanentMoveSpeedPerPoint;
        }

        public float GetPistolDamageBonus()
        {
            return PistolDamagePoints * GameConfig.PermanentPistolDamagePerPoint;
        }

        public float GetSenseValue()
        {
            return ClampToStep(SenseValue, 0f, GameConfig.PermanentSenseMax);
        }

        /// <summary>현재 감각 레벨 (0~5). 임계값 배열로 계산.</summary>
        public int GetSenseTier()
        {
            float val = GetSenseValue();
            for (int i = SenseLevelThresholds.Length - 1; i >= 0; i--)
                if (val >= SenseLevelThresholds[i]) return i + 1;
            return 0;
        }

        /// <summary>감각 다음 레벨까지 필요한 추가 포인트 수. 최대 레벨이면 0 반환.</summary>
        public int GetSensePointsToNextLevel()
        {
            int tier = GetSenseTier();
            if (tier >= SenseLevelThresholds.Length) return 0;
            float needed = SenseLevelThresholds[tier] - GetSenseValue();
            return (int)Math.Ceiling(Math.Max(0.0, (double)needed / GameConfig.PermanentSensePerPoint));
        }

        /// <summary>현재 운 레벨 (0~10). 임계값 배열로 계산.</summary>
        public int GetLuckLevel()
        {
            float val = ClampToStep(LuckValue, 0f, GameConfig.PermanentLuckMax);
            for (int i = LuckLevelThresholds.Length - 1; i >= 0; i--)
                if (val >= LuckLevelThresholds[i]) return i + 1;
            return 0;
        }

        /// <summary>운 다음 레벨까지 필요한 추가 포인트 수. 최대 레벨이면 0 반환.</summary>
        public int GetLuckPointsToNextLevel()
        {
            int level = GetLuckLevel();
            if (level >= LuckLevelThresholds.Length) return 0;
            float val = ClampToStep(LuckValue, 0f, GameConfig.PermanentLuckMax);
            float needed = LuckLevelThresholds[level] - val;
            return (int)Math.Ceiling(Math.Max(0.0, (double)needed / GameConfig.PermanentLuckPerPoint));
        }

        /// <summary>운 레벨 기반 확률 값 (0.0~1.0). 레벨당 0.1씩 증가.</summary>
        public float GetLuckValue()
        {
            return GetLuckLevel() * 0.1f;
        }

        public bool TrySpendHealthPoint()
        {
            if (UnspentPoints <= 0)
            {
                return false;
            }

            UnspentPoints--;
            HealthPoints++;
            return true;
        }

        public bool TrySpendMoveSpeedPoint()
        {
            if (UnspentPoints <= 0)
            {
                return false;
            }

            UnspentPoints--;
            MoveSpeedPoints++;
            return true;
        }

        public bool TrySpendSensePoint()
        {
            if (UnspentPoints <= 0 || SenseValue >= GameConfig.PermanentSenseMax)
            {
                return false;
            }

            UnspentPoints--;
            SenseValue = ClampToStep(SenseValue + GameConfig.PermanentSensePerPoint, 0f, GameConfig.PermanentSenseMax);
            return true;
        }

        public bool TrySpendLuckPoint()
        {
            if (UnspentPoints <= 0 || LuckValue >= GameConfig.PermanentLuckMax)
            {
                return false;
            }

            UnspentPoints--;
            LuckValue = ClampToStep(LuckValue + GameConfig.PermanentLuckPerPoint, 0f, GameConfig.PermanentLuckMax);
            return true;
        }

        public bool TrySpendPistolDamagePoint()
        {
            if (UnspentPoints <= 0)
            {
                return false;
            }

            UnspentPoints--;
            PistolDamagePoints++;
            return true;
        }

        private static float ClampToStep(float value, float min, float max)
        {
            if (value < min)
            {
                value = min;
            }
            else if (value > max)
            {
                value = max;
            }

            return (float)Math.Round(value, 1, MidpointRounding.AwayFromZero);
        }
    }
}
