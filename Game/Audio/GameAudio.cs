using System;

namespace My2DEngine.Game.Audio
{
    /// <summary>
    /// 게임이 쓰는 소리 장치 두 개(효과음, 배경 음악)와 배경 음악 상태를 한곳에 묶는다.
    /// GameLogic은 무엇을 틀지만 정하고, 장치를 만들고 끄는 일과 사망 시 음악을 한 번만 끄는 규칙은 여기서 맡는다.
    /// </summary>
    internal sealed class GameAudio : IDisposable
    {
        private readonly EffectSoundManager effects;
        private readonly BackgroundSoundManager music;

        /// <summary>이번 사망에서 음악을 이미 껐는지. 다시 살아나거나 새 런을 시작하면 풀린다.</summary>
        private bool musicStoppedForDeath;

        public GameAudio()
        {
            effects = new EffectSoundManager();
            music = new BackgroundSoundManager();
            effects.LoadAllSounds();
            music.LoadAllSounds();
        }

        /// <summary>적 소리처럼 다른 시스템이 직접 쓰는 효과음 장치.</summary>
        public EffectSoundManager Effects => effects;

        public int BgmVolume
        {
            get => music.GetBgmVolume();
            set => music.SetBgmVolume(value);
        }

        public int SfxVolume
        {
            get => effects.GetSfxVolume();
            set => effects.SetSfxVolume(value);
        }

        /// <summary>효과음을 튼다. 빈 별칭은 무시된다.</summary>
        /// <param name="stopFirst">같은 별칭이 재생 중이면 먼저 멈춘다.</param>
        /// <param name="loop">반복 재생.</param>
        public void PlayEffect(string alias, bool stopFirst, bool loop = false)
        {
            effects.PlaySound(alias, stopFirst, loop);
        }

        /// <summary>효과음을 멈춘다. 빈 별칭은 무시한다.</summary>
        public void StopEffect(string alias)
        {
            if (!string.IsNullOrWhiteSpace(alias))
            {
                effects.StopSound(alias);
            }
        }

        /// <summary>현재 상황에 맞는 배경 음악으로 바꾸거나 이어서 재생한다(교차 페이드는 dt로 진행).</summary>
        public void UpdateMusic(BackgroundMusicCategory category, float dt)
        {
            music.UpdateBackgroundMusic(category, dt);
        }

        /// <summary>음악을 멈추고 사망 상태도 풀어 준다. 새 런 시작이나 메뉴 복귀 때 쓴다.</summary>
        public void StopMusic()
        {
            music.StopBackgroundMusic();
            musicStoppedForDeath = false;
        }

        /// <summary>음악만 멈춘다(승리·엔딩 동안 매 프레임 부른다).</summary>
        public void SilenceMusic()
        {
            music.StopBackgroundMusic();
        }

        /// <summary>사망하면 음악을 한 번만 끈다.</summary>
        public void StopMusicForDeath()
        {
            if (musicStoppedForDeath)
            {
                return;
            }

            music.StopBackgroundMusic();
            musicStoppedForDeath = true;
        }

        /// <summary>플레이어 상태를 되돌릴 때 사망 음악 상태도 되돌린다.</summary>
        public void ResetDeathMusic()
        {
            musicStoppedForDeath = false;
        }

        public void Dispose()
        {
            music.Dispose();
            effects.Dispose();
        }
    }
}
