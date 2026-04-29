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

        public static EnemyDefinition[] GetDefaultDefinitions()
        {
            return new EnemyDefinition[]
            {
                new EnemyDefinition 
                { 
                    Type = EnemyType.Gunner, 
                    Scale = 0.82f, 
                    MaxHealth = 60f, 
                    MoveSpeed = 1.05f, 
                    AttackRange = 7.6f, 
                    AttackDamage = 10f, 
                    AttackCooldownDuration = 0.68f, 
                    Radius = 0.22f
                },
                new EnemyDefinition 
                { 
                    Type = EnemyType.Elite, 
                    Scale = 1.18f, 
                    MaxHealth = 120f, 
                    MoveSpeed = 0.92f, 
                    AttackRange = 1.02f, 
                    AttackDamage = 18f, 
                    AttackCooldownDuration = 1.0f, 
                    Radius = 0.34f
                },
                new EnemyDefinition
                {
                    Type = EnemyType.RuinedGunner,
                    Scale = 0.82f,
                    MaxHealth = 72f,
                    MoveSpeed = 1.0f,
                    AttackRange = 6.4f,
                    AttackDamage = 8f,
                    AttackCooldownDuration = 1.25f,
                    Radius = 0.22f
                },
                new EnemyDefinition
                {
                    Type = EnemyType.BloodGhost,
                    Scale = 1.1f,
                    MaxHealth = 100f,
                    MoveSpeed = 1.3f,
                    AttackRange = 1.1f,
                    AttackDamage = 16f,
                    AttackCooldownDuration = 0.85f,
                    Radius = 0.30f
                },
                new EnemyDefinition
                {
                    Type = EnemyType.BeamRevenant,
                    Scale = 0.9f,
                    MaxHealth = 80f,
                    MoveSpeed = 0.95f,
                    AttackRange = 9f,
                    AttackDamage = 12f,
                    AttackCooldownDuration = 1.1f,
                    Radius = 0.24f
                },
                new EnemyDefinition
                {
                    Type = EnemyType.Hellion,
                    Scale = 0.78f,
                    MaxHealth = 55f,
                    MoveSpeed = 1.4f,
                    AttackRange = 5.5f,
                    AttackDamage = 7f,
                    AttackCooldownDuration = 1.0f,
                    Radius = 0.20f
                },
            };
        }
    }
}
