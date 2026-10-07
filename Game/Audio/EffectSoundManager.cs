using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;
using NAudio.Vorbis;
using My2DEngine.Game.Config;

namespace My2DEngine.Game.Audio
{
    /// <summary>
    /// 총소리, 문 열림 등 짧은 효과음을 NAudio WaveOutEvent 기반으로 재생하는 매니저다.
    /// </summary>
    internal sealed class EffectSoundManager : IDisposable
    {
        private sealed class EffectPlayerSlot
        {
            public WaveOutEvent Output;
            public IDisposable CurrentReader;
            public string Alias;
        }

        private const int PoolSize = 8;

        // alias → 파일 경로 (WAV / OGG)
        private readonly Dictionary<string, string> soundPaths;

        private readonly object soundLock = new object();
        private readonly EffectPlayerSlot[] slots = new EffectPlayerSlot[PoolSize];
        private int nextSlot;
        private int sfxVolume = 100;
        private bool disposed;

        public EffectSoundManager()
        {
            soundPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public int GetSfxVolume()
        {
            lock (soundLock)
            {
                return sfxVolume;
            }
        }

        public void SetSfxVolume(int volume)
        {
            lock (soundLock)
            {
                sfxVolume = Math.Max(0, Math.Min(100, volume));
            }
        }

        /// <summary>
        /// AudioConfig에 정의된 기본 효과음을 모두 등록한다.
        /// </summary>
        public void LoadAllSounds()
        {
            PreloadSoundEffect(AudioConfig.PistolFireSoundPath, AudioConfig.PistolFireSoundAlias);
            PreloadSoundEffect(AudioConfig.ShotGunFireSoundPath, AudioConfig.ShotGunFireSoundAlias);
            PreloadSoundEffect(AudioConfig.LMGFireSoundPath, AudioConfig.LMGFireSoundAlias);
            PreloadSoundEffect(AudioConfig.LMGWindUpSoundPath, AudioConfig.LMGWindUpSoundAlias);
            PreloadSoundEffect(AudioConfig.LMGWindDownSoundPath, AudioConfig.LMGWindDownSoundAlias);
            PreloadSoundEffect(AudioConfig.RocketFireSoundPath, AudioConfig.RocketFireSoundAlias);
            PreloadSoundEffect(AudioConfig.RocketBoomSoundPath, AudioConfig.RocketBoomSoundAlias);
            PreloadSoundEffect(AudioConfig.RocketFlySoundPath, AudioConfig.RocketFlySoundAlias);
            PreloadSoundEffect(AudioConfig.PlazmaGunFireSoundPath, AudioConfig.PlazmaGunFireSoundAlias);
            PreloadSoundEffect(AudioConfig.DoorSoundPath, AudioConfig.DoorSoundAlias);
        }

        private void PreloadSoundEffect(string relativePath, string alias)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                return;
            }

            lock (soundLock)
            {
                if (soundPaths.ContainsKey(alias))
                {
                    return;
                }

                string path = ResolveSoundPath(relativePath);
                if (path == null)
                {
                    return;
                }

                soundPaths[alias] = path;
            }
        }

        public bool HasSound(string alias)
        {
            lock (soundLock)
            {
                return !string.IsNullOrWhiteSpace(alias) &&
                    soundPaths.ContainsKey(alias);
            }
        }

        public void PlaySound(string alias, bool stopFirst = false, bool loop = false)
            => PlaySoundCore(alias, stopFirst, loop);

        private void PlaySoundCore(string alias, bool stopFirst, bool loop)
        {
            lock (soundLock)
            {
                if (disposed || string.IsNullOrWhiteSpace(alias))
                {
                    return;
                }

                WaveStream reader = TryCreateReader(alias);
                if (reader == null)
                {
                    return;
                }

                try
                {
                    if (stopFirst)
                    {
                        StopSlotsWithAlias(alias);
                    }

                    EffectPlayerSlot slot = GetFreeSlot();
                    StopAndClearSlot(slot);

                    slot.Output = slot.Output ?? new WaveOutEvent();
                    IWaveProvider provider = loop ? new LoopStream(reader) : reader;
                    slot.Output.Volume = sfxVolume / 100f;
                    slot.Output.Init(provider);
                    slot.Output.Play();

                    slot.CurrentReader = reader;
                    slot.Alias = alias;
                }
                catch (Exception ex)
                {
                    reader?.Dispose();
                    SoundAudioCommon.LogFailure("Failed to play '" + alias + "'", ex);
                }
            }
        }

        public void StopSound(string alias)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                return;
            }

            lock (soundLock)
            {
                StopSlotsWithAlias(alias);
            }
        }

        public void Dispose()
        {
            lock (soundLock)
            {
                if (disposed)
                {
                    return;
                }

                for (int i = 0; i < slots.Length; i++)
                {
                    DisposeSlot(slots[i]);
                }

                soundPaths.Clear();
                disposed = true;
            }
        }

        private WaveStream TryCreateReader(string alias)
        {
            try
            {
                if (soundPaths.TryGetValue(alias, out string path))
                {
                    return CreateReaderForPath(path);
                }
            }
            catch (Exception ex)
            {
                SoundAudioCommon.LogFailure("Cannot create reader for '" + alias + "'", ex);
            }

            return null;
        }

        private static WaveStream CreateReaderForPath(string path)
        {
            if (path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
            {
                return new VorbisWaveReader(path);
            }

            return new WaveFileReader(path);
        }

        private EffectPlayerSlot GetFreeSlot()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                int idx = (nextSlot + i) % PoolSize;
                EffectPlayerSlot slot = slots[idx];
                if (slot == null || slot.Output == null || slot.Output.PlaybackState != PlaybackState.Playing)
                {
                    nextSlot = (idx + 1) % PoolSize;
                    if (slots[idx] == null)
                    {
                        slots[idx] = new EffectPlayerSlot();
                    }

                    return slots[idx];
                }
            }

            EffectPlayerSlot fallback = slots[nextSlot] ?? (slots[nextSlot] = new EffectPlayerSlot());
            nextSlot = (nextSlot + 1) % PoolSize;
            return fallback;
        }

        private void StopSlotsWithAlias(string alias)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                EffectPlayerSlot slot = slots[i];
                if (slot != null && string.Equals(slot.Alias, alias, StringComparison.OrdinalIgnoreCase))
                {
                    StopAndClearSlot(slot);
                }
            }
        }

        private static void StopAndClearSlot(EffectPlayerSlot slot)
        {
            if (slot == null)
            {
                return;
            }

            try { slot.Output?.Stop(); } catch { }
            try { slot.Output?.Dispose(); } catch { }
            slot.Output = null;

            try { slot.CurrentReader?.Dispose(); } catch { }
            slot.CurrentReader = null;
            slot.Alias = null;
        }

        private static void DisposeSlot(EffectPlayerSlot slot)
        {
            if (slot == null)
            {
                return;
            }

            StopAndClearSlot(slot);
        }

        private string ResolveSoundPath(string relativePath)
            => SoundAudioCommon.ResolveSoundPath(relativePath);

        private sealed class LoopStream : WaveStream
        {
            private readonly WaveStream source;

            public LoopStream(WaveStream source)
            {
                this.source = source;
            }

            public override WaveFormat WaveFormat => source.WaveFormat;
            public override long Length => long.MaxValue;
            public override long Position
            {
                get => source.Position;
                set => source.Position = value;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int read = 0;
                while (read < count)
                {
                    int chunk = source.Read(buffer, offset + read, count - read);
                    if (chunk == 0)
                    {
                        source.Position = 0;
                    }
                    else
                    {
                        read += chunk;
                    }
                }

                return read;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    source.Dispose();
                }

                base.Dispose(disposing);
            }
        }
    }
}
