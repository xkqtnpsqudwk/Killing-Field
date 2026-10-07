namespace My2DEngine.Game
{
    // 적 타입
    // 코드 전반에서 쓰는 적 기본 유형 이름이다.
    public enum EnemyType
    {
        Gunner = 0,
        Elite = 1,
        RuinedGunner = 2,
        BloodGhost = 3,
        BeamRevenant = 4,
        Hellion = 5,
    }

    // 적 종류별 기본 설정값
    // 적 기본 스탯의 출발값을 담는 정의 데이터다.
    public class EnemyDefinition
    {
        public EnemyType Type { get; set; }
        public float Scale { get; set; }
        public float MaxHealth { get; set; }
        public float MoveSpeed { get; set; }
        public float AttackRange { get; set; }
        public float AttackDamage { get; set; }
        public float AttackCooldownDuration { get; set; }
        public float Radius { get; set; }

        public EnemyDefinition Clone()
        {
            return new EnemyDefinition
            {
                Type = Type,
                Scale = Scale,
                MaxHealth = MaxHealth,
                MoveSpeed = MoveSpeed,
                AttackRange = AttackRange,
                AttackDamage = AttackDamage,
                AttackCooldownDuration = AttackCooldownDuration,
                Radius = Radius
            };
        }
    }
}
