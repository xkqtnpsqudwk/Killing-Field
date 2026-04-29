using My2DEngine.Game;

namespace My2DEngine.Game.Config
{
    /// <summary>
    /// 일반 적·미니보스·보스를 구분하는 상위 등급 분류다.
    /// 등급에 따라 AI 행동, 스폰 규칙, 사망 시 보상이 달라진다.
    /// </summary>
    public enum EnemyRank
    {
        /// <summary>일반 잡몹 등급.</summary>
        Normal,
        /// <summary>중간 보스 등급. 고유 이름과 강화된 스탯을 가진다.</summary>
        MiniBoss,
        /// <summary>최종 보스 등급. 특수 패턴과 궁극기를 사용한다.</summary>
        Boss
    }

    /// <summary>
    /// AI가 어떤 방식으로 접근·회피·특수기를 조합하는지 결정하는 행동 패턴 분류다.
    /// EnemyManager.UpdateEnemyEngagedMovement에서 이 값으로 이동 전략을 분기한다.
    /// </summary>
    public enum EnemyBehaviorPattern
    {
        /// <summary>기본 직진 추적 패턴.</summary>
        Default,
        /// <summary>느리지만 강한 직진 돌진형.</summary>
        Juggernaut,
        /// <summary>빠른 속도로 돌격하는 패턴.</summary>
        Rushdown,
        /// <summary>횡이동하며 거리를 유지하는 패턴.</summary>
        Strafe,
        /// <summary>원거리에서 공격하며 근거리를 회피하는 패턴.</summary>
        Kite,
        /// <summary>타이밍에 맞춰 빠르게 돌진하는 패턴.</summary>
        Pouncer,
        /// <summary>근거리에서 빠르게 접근·이탈하는 패턴.</summary>
        Skirmisher,
        /// <summary>보스 탱커 패턴: 느리고 단단하며 로켓을 발사한다.</summary>
        BossTanker,
        /// <summary>보스 야수 패턴: 빠르게 달려들며 궁극기 돌진을 사용한다.</summary>
        BossBeast,
        /// <summary>보스 도주 패턴: 빠른 이동과 분신 소환을 사용한다.</summary>
        BossRunner,
        /// <summary>보스 포병 패턴: 원거리에서 레이저 빔을 회전시키며 공격한다.</summary>
        BossArtillery,

        // ── 일반 적 신규 패턴 ──────────────────────────────────────────────
        /// <summary>ZombieScientistPack: 사거리 유지 + 단발 권총 원거리 공격.</summary>
        ZombieGunner,
        /// <summary>BlindPinky: 청각 탐지만으로 직선 돌진, 장애물 충돌 후 재탐지.</summary>
        BlindCharger,
        /// <summary>Blood Ghost: 고속 직진 통과형 근접, 피격 시 반투명 + 이속 증가.</summary>
        BloodPhantom,
        /// <summary>BeamRevenant: 빔 워밍업 후 지속 조사, 안전 거리 유지 + 횡이동.</summary>
        BeamSniper,
        /// <summary>SlimeImp: 중거리 포물선 투척 + 슬라임 웅덩이, 압박 시 이탈.</summary>
        SlimeLobber,
        /// <summary>Hellion: 초고속 공전 후 순간 돌진, 3~5마리 무리 동기화.</summary>
        HellionSwarm,

        // ── 보스 신규 패턴 ─────────────────────────────────────────────────
        /// <summary>Azazel: 공중 화염 돌진 + Phase2 화염 자국 + 포탄 연속 투하.</summary>
        BossFlameLord,
        /// <summary>Behemoth: 극저속 압박 + 지면 충격파 + Phase2 광폭화 돌진.</summary>
        BossBehemoth,
        /// <summary>Arachnocortex: 플라즈마 부채꼴 + 레일건 차지 + Phase2 미니언 소환.</summary>
        BossCortex,
        /// <summary>Agaures: 정지 없는 화염 공전 + 돌진 이탈 + 순간이동 화염 기둥 궁극.</summary>
        BossRiftBlitz,
        /// <summary>Abaddon: 어둠 검기 + 어둠 구체 + Phase2 어둠 오라 + 암흑 폭발 궁극.</summary>
        BossDarkLord,
        /// <summary>Afrit: 공중 화염 포탄 + 화염 폭격 + Phase2 분열 포탄 + 화염 폭풍 궁극.</summary>
        BossAfritBomber,
        /// <summary>AgathoDemon: 독 투사체 + 독 안개 + 허물벗기 궁극(투명화+회복).</summary>
        BossAgathoDemon,
        /// <summary>Annihilator: 4방향 십자 포격 + Phase2 8방향 확대 + 전멸 포격 궁극.</summary>
        BossAnnihilator,
        /// <summary>Arachnobaron: 플라즈마 2연발 + 거미줄 슬로우 + Phase2 3연발 궁극.</summary>
        BossArackBaron,
        /// <summary>Arachnophyte: 발톱 연타 + 독침 + Phase2 거미 새끼 소환.</summary>
        BossArachnoFang,
        /// <summary>AracnorbQueen: 알 투척 소환 + 독 포격 + 알 폭탄 전체 산란 궁극.</summary>
        BossSpiderQueen
    }

}
