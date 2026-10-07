namespace My2DEngine.Game.Config
{
    /// <summary>
    /// 효과음 alias와 경로, 배경 음악 트랙 목록.
    /// 값을 바꾸면 게임 밸런스에 바로 영향을 주므로 의도적으로 수정할 때만 바꾼다.
    /// </summary>
    public static class AudioConfig
    {
        /// <summary>.44 AMP 발사 사운드 별칭.</summary>
        public const string PistolFireSoundAlias = "kf_pistol_fire";

        /// <summary>.44 AMP 발사 사운드 상대 경로.</summary>
        public const string PistolFireSoundPath = @"Gun\Pistol\Fire.wav";

        /// <summary>BearKiller 발사 사운드 별칭.</summary>
        public const string ShotGunFireSoundAlias = "kf_shotgun_fire";

        /// <summary>BearKiller 발사 사운드 상대 경로.</summary>
        public const string ShotGunFireSoundPath = @"Gun\ShotGun\Fire.wav";

        /// <summary>Heavy Chaingun 발사 사운드 별칭.</summary>
        public const string LMGFireSoundAlias = "kf_lmg_fire";

        /// <summary>Heavy Chaingun 발사 사운드 상대 경로.</summary>
        public const string LMGFireSoundPath = @"Gun\LMG\Fire.wav";

        /// <summary>LMG 회전 시작(wind up) 사운드 별칭. 현재 Heavy Chaingun 팩에는 대응 효과음이 없어 레거시 호환용으로만 남긴다.</summary>
        public const string LMGWindUpSoundAlias = "kf_lmg_windup";

        /// <summary>LMG 회전 시작(wind up) 사운드 상대 경로. 현재는 레거시 호환용이다.</summary>
        public const string LMGWindUpSoundPath = @"Gun\LMG\WindUP.wav";

        /// <summary>LMG 회전 종료(wind down) 사운드 별칭. 현재 Heavy Chaingun 팩에는 대응 효과음이 없어 레거시 호환용으로만 남긴다.</summary>
        public const string LMGWindDownSoundAlias = "kf_lmg_winddown";

        /// <summary>LMG 회전 종료(wind down) 사운드 상대 경로. 현재는 레거시 호환용이다.</summary>
        public const string LMGWindDownSoundPath = @"Gun\LMG\WindDown.wav";

        /// <summary>Auto Cannon 발사 사운드 별칭.</summary>
        public const string RocketFireSoundAlias = "kf_rocket_fire";

        /// <summary>Auto Cannon 발사 사운드 상대 경로.</summary>
        public const string RocketFireSoundPath = @"Gun\RocketLauncher\Fire.wav";

        /// <summary>Auto Cannon 레거시 폭발 사운드 별칭. 현재는 별도 착탄음을 사용하지 않는다.</summary>
        public const string RocketBoomSoundAlias = "kf_rocket_boom";

        /// <summary>Auto Cannon 레거시 폭발 사운드 상대 경로.</summary>
        public const string RocketBoomSoundPath = @"Gun\RocketLauncher\Boom.wav";

        /// <summary>레거시 추적탄 비행 사운드 별칭.</summary>
        public const string RocketFlySoundAlias = "kf_rocket_fly";

        /// <summary>레거시 추적탄 비행 사운드 상대 경로.</summary>
        public const string RocketFlySoundPath = @"Gun\RocketLauncher\Fly.wav";

        /// <summary>Dual 92s 발사 사운드 별칭.</summary>
        public const string PlazmaGunFireSoundAlias = "kf_plazma_fire";

        /// <summary>Dual 92s 발사 사운드 상대 경로.</summary>
        public const string PlazmaGunFireSoundPath = @"Gun\PlazmaGun\Fire.wav";

        /// <summary>문 개방 사운드 별칭.</summary>
        public const string DoorSoundAlias = "kf_door";

        /// <summary>문 개방 사운드 상대 경로.</summary>
        public const string DoorSoundPath = @"effect\Door.wav";

        /// <summary>일반 전투 구간에서 무작위로 선택되는 배경 음악 트랙 파일명 목록.</summary>
        public static readonly string[] NormalBackgroundTracks = new[]
        {
            @"BackGroundMusic\Normal.mp3",
            @"BackGroundMusic\Normal2.mp3"
        };

        /// <summary>미니보스 방에서 재생되는 배경 음악 트랙 파일명 목록.</summary>
        public static readonly string[] MiniBossBackgroundTracks = new[]
        {
            @"BackGroundMusic\MiniBoss.mp3",
            @"BackGroundMusic\MiniBoss2.mp3"
        };

        /// <summary>보스 방에서 재생되는 배경 음악 트랙 파일명 목록.</summary>
        public static readonly string[] BossBackgroundTracks = new[]
        {
            @"BackGroundMusic\Boss.mp3",
            @"BackGroundMusic\Boss2.mp3"
        };
    }
}
