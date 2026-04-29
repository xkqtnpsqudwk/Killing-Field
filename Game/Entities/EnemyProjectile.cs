namespace My2DEngine.Game
{
    /// <summary>
    /// 적이 발사하는 투사체의 종류를 구분한다.
    /// EnemyManager.Projectiles가 이 값으로 업데이트 로직을 분기한다.
    /// </summary>
    public enum EnemyProjectileKind
    {
        /// <summary>미니보스가 발사하는 산성 구슬. 벽/플레이어에 닿으면 바닥 장판으로 전환된다.</summary>
        AcidGlob,
        /// <summary>일반 총기형 적의 총알. 벽에 닿으면 즉시 제거된다.</summary>
        EnemyShot,
        /// <summary>BossTanker가 발사하는 로켓. 벽이나 수명 소진 시 폭발 범위 피해를 준다.</summary>
        BossRocket,
        /// <summary>플레이어가 발사한 로켓. 천천히 비행하다 벽/적과 충돌하면 폭발한다.</summary>
        PlayerRocket,
        /// <summary>플레이어 로켓의 짧은 폭발 시각 효과. 피해는 이미 적용된 상태다.</summary>
        PlayerRocketExplosion,
        /// <summary>BossArtillery의 레이저 빔. 위치 고정이며 BeamAngle이 회전해 플레이어를 추적한다.</summary>
        LaserBeam
    }

    /// <summary>
    /// 적이 발사한 투사체 하나의 런타임 상태를 담는다.
    /// EnemyManager.enemyProjectiles 리스트가 이 객체들을 보유하고 매 프레임 갱신한다.
    /// 종류(Kind)에 따라 이동·충돌·피해 방식이 달라진다.
    /// </summary>
    public sealed class EnemyProjectile
    {
        /// <summary>이 투사체의 종류. 업데이트·렌더 분기에 사용된다.</summary>
        public EnemyProjectileKind Kind { get; set; }

        /// <summary>현재 월드 X 좌표. 매 프레임 VelocityX * dt만큼 이동한다(LaserBeam 제외).</summary>
        public float X { get; set; }

        /// <summary>현재 월드 Y 좌표. 매 프레임 VelocityY * dt만큼 이동한다(LaserBeam 제외).</summary>
        public float Y { get; set; }

        /// <summary>이동 속도 X 성분(타일/초). LaserBeam은 0이다.</summary>
        public float VelocityX { get; set; }

        /// <summary>이동 속도 Y 성분(타일/초). LaserBeam은 0이다.</summary>
        public float VelocityY { get; set; }

        /// <summary>LaserBeam의 빔 끝점 월드 X 좌표. TraceBeamEnd()로 매 프레임 갱신된다.</summary>
        public float EndX { get; set; }

        /// <summary>LaserBeam의 빔 끝점 월드 Y 좌표.</summary>
        public float EndY { get; set; }

        /// <summary>LaserBeam 현재 각도(라디안). BeamRotateSpeed * dt만큼 매 프레임 회전한다.</summary>
        public float BeamAngle { get; set; }

        /// <summary>LaserBeam 회전 속도(라디안/초). 양수면 시계 방향.</summary>
        public float BeamRotateSpeed { get; set; }

        /// <summary>LaserBeam 최대 빔 길이(타일). 0이면 기본값(16)이 사용된다.</summary>
        public float BeamMaxDistance { get; set; }

        /// <summary>충돌 판정 원 반지름(타일). AcidGlob는 장판 전환 시 더 커진다.</summary>
        public float Radius { get; set; }

        /// <summary>BossRocket 폭발 반지름(타일). Radius보다 크면 폭발 범위가 더 넓다.</summary>
        public float ExplosionRadius { get; set; }

        /// <summary>플레이어에게 주는 피해량. Kind에 따라 직접 피해 또는 틱 피해로 사용된다.</summary>
        public float Damage { get; set; }

        /// <summary>
        /// 잔여 수명(초). 0 이하가 되면 비활성화된다.
        /// BossRocket은 수명 소진 시 폭발한다.
        /// </summary>
        public float Lifetime { get; set; }

        /// <summary>
        /// LaserBeam 예열 시간(초). 0보다 크면 피해를 주지 않고 예고만 표시된다.
        /// 0이 되면 본격적인 빔 회전과 피해가 시작된다.
        /// </summary>
        public float WarmupTimer { get; set; }

        /// <summary>틱 피해 간격(초). AcidGlob·LaserBeam에서 반복 피해 주기를 결정한다.</summary>
        public float TickInterval { get; set; }

        /// <summary>다음 틱 피해까지의 잔여 시간(초). 0이 되면 피해를 주고 다시 TickInterval로 초기화된다.</summary>
        public float TickTimer { get; set; }

        /// <summary>플레이어 추적 로켓이 따라갈 대상 적이다. null이면 현재 진행 방향을 유지한다.</summary>
        public Enemy TrackingTarget { get; set; }

        /// <summary>추적 회전 속도(라디안/초). 0이면 추적 보정 없이 직진한다.</summary>
        public float TurnRate { get; set; }

        /// <summary>true이면 활성 상태로 업데이트·렌더링된다. false이면 리스트에서 제거 대상이 된다.</summary>
        public bool Active { get; set; }
    }
}
