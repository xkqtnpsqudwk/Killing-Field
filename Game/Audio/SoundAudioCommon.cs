using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace My2DEngine.Game.Audio
{
    /// <summary>
    /// 현재 방 상태에 따라 배경음(BGM)을 선택하기 위한 카테고리 열거형이다.
    /// Normal은 일반 전투, MiniBoss는 미니 보스 방, Boss는 최종 보스 방에 해당한다.
    /// </summary>
    public enum BackgroundMusicCategory
    {
        /// <summary>일반 전투 구역에서 재생되는 배경음 카테고리</summary>
        Normal,
        /// <summary>미니 보스 방에서 재생되는 배경음 카테고리</summary>
        MiniBoss,
        /// <summary>최종 보스 방에서 재생되는 배경음 카테고리</summary>
        Boss
    }

    /// <summary>
    /// 오디오 파일 경로 해석, COM 플레이어 생성 및 해제 등
    /// <see cref="BackgroundSoundManager"/>와 <see cref="EffectSoundManager"/>가 공통으로 사용하는
    /// 정적 헬퍼 메서드를 모아둔 내부 유틸리티 클래스다.
    /// </summary>
    internal static class SoundAudioCommon
    {
        /// <summary>
        /// 폴더명 + 파일명 조합을 키로 사용하는 에셋 경로 캐시.
        /// 런타임 중 중복 파일 시스템 탐색을 방지한다.
        /// </summary>
        private static readonly Dictionary<string, string> resolvedAssetPathCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// WMPlayer.OCX COM 타입을 한 번만 조회하여 저장해 두는 정적 필드.
        /// null이면 COM 타입을 사용할 수 없는 환경임을 의미한다.
        /// </summary>
        private static Type windowsMediaPlayerType;

        /// <summary>
        /// WMPlayer.OCX COM 타입 조회를 이미 시도했는지 나타내는 플래그.
        /// true이면 재시도 없이 캐시된 결과를 반환한다.
        /// </summary>
        private static bool attemptedWindowsMediaPlayerTypeResolve;

        /// <summary>
        /// 오디오 관련 실패 메시지를 디버그 출력 창에 기록한다.
        /// </summary>
        /// <param name="message">실패에 대한 설명 메시지.</param>
        /// <param name="ex">관련 예외 객체. 없으면 null.</param>
        public static void LogFailure(string message, Exception ex = null)
        {
            if (ex == null)
            {
                Debug.WriteLine("[SoundManager] " + message);
                return;
            }

            Debug.WriteLine("[SoundManager] " + message + " " + ex);
        }

        /// <summary>
        /// COM 객체를 안전하게 해제한다.
        /// <see cref="Marshal.IsComObject"/>로 COM 여부를 확인한 뒤
        /// <see cref="Marshal.FinalReleaseComObject"/>를 호출하여 참조 카운트를 완전히 제거한다.
        /// </summary>
        /// <param name="instance">해제할 COM 객체. null이면 아무 작업도 하지 않는다.</param>
        /// <param name="context">로그 메시지에 포함될 컨텍스트 설명 문자열 (예: "background music player")</param>
        public static void ReleaseComObject(object instance, string context)
        {
            if (instance == null)
            {
                return;
            }

            try
            {
                if (Marshal.IsComObject(instance))
                {
                    Marshal.FinalReleaseComObject(instance);
                }
            }
            catch (Exception ex)
            {
                LogFailure("Failed to release COM object: " + context, ex);
            }
        }

        /// <summary>
        /// WMPlayer.OCX COM ProgID로부터 Windows Media Player의 <see cref="Type"/>을 가져온다.
        /// 첫 호출 시 ProgID 조회를 시도하고 결과를 정적 캐시에 저장한다.
        /// 이후 호출에서는 캐시된 타입을 즉시 반환하며 재조회하지 않는다.
        /// COM 환경이 지원되지 않으면 null을 반환하고 로거에 실패를 기록한다.
        /// </summary>
        /// <param name="context">로그 메시지에 포함될 용도 설명 (예: "background music", "effect playback")</param>
        /// <returns>WMPlayer.OCX COM 타입. 사용 불가 환경이면 null.</returns>
        public static Type GetWindowsMediaPlayerType(string context)
        {
            if (attemptedWindowsMediaPlayerTypeResolve)
            {
                if (windowsMediaPlayerType == null)
                {
                    LogFailure("WMPlayer.OCX COM type is unavailable" + (string.IsNullOrWhiteSpace(context) ? "." : " for " + context + "."));
                }

                return windowsMediaPlayerType;
            }

            attemptedWindowsMediaPlayerTypeResolve = true;
            try
            {
                windowsMediaPlayerType = Type.GetTypeFromProgID("WMPlayer.OCX");
            }
            catch (Exception ex)
            {
                LogFailure("Failed to resolve WMPlayer.OCX COM type" + (string.IsNullOrWhiteSpace(context) ? "." : " for " + context + "."), ex);
                windowsMediaPlayerType = null;
            }

            if (windowsMediaPlayerType == null)
            {
                LogFailure("WMPlayer.OCX COM type is unavailable" + (string.IsNullOrWhiteSpace(context) ? "." : " for " + context + "."));
            }

            return windowsMediaPlayerType;
        }

        /// <summary>
        /// <c>Images</c> 폴더 기준으로 상대 경로(예: <c>Gun\Pistol\Fire.wav</c>)를
        /// 절대 경로로 변환하여 반환한다.
        /// 파일을 찾지 못하거나 경로가 비어 있으면 null을 반환하고 디버그 로그에 실패를 기록한다.
        /// </summary>
        /// <param name="relativePath">Images 폴더 하위 상대 경로 (예: <c>Gun\ShotGun\Fire.wav</c>)</param>
        /// <returns>파일의 절대 경로. 찾지 못하면 null.</returns>
        public static string ResolveSoundPath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return null;
            string path = ResolveAssetPath("Sound", relativePath);
            if (path == null) LogFailure("Sound file not found: " + relativePath);
            return path;
        }

        /// <summary>
        /// 지정한 폴더 이름과 파일 이름으로부터 에셋의 실제 절대 경로를 찾아 반환한다.
        /// 최초 호출 시 복수의 후보 경로를 순서대로 탐색하며, 결과를 내부 캐시에 저장하여
        /// 이후 동일 요청에서는 파일 시스템 접근 없이 즉시 반환한다.
        /// 탐색 순서: 실행 디렉터리 직하 → 2단계 상위 → 3단계 상위 경로이며,
        /// 직접 경로에서 찾지 못하면 해당 폴더를 재귀 탐색한다.
        /// </summary>
        /// <param name="folderName">에셋 루트 하위의 폴더 이름 (예: "Sound", "Images")</param>
        /// <param name="fileName">찾을 파일의 이름 (예: "Fire.wav")</param>
        /// <returns>파일의 절대 경로 문자열. 찾지 못하면 null.</returns>
        public static string ResolveAssetPath(string folderName, string fileName)
        {
            if (string.IsNullOrWhiteSpace(folderName) || string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            string cacheKey = folderName + "|" + fileName;
            if (resolvedAssetPathCache.TryGetValue(cacheKey, out string cachedPath))
            {
                return cachedPath;
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates = new string[]
            {
                Path.Combine(baseDir, "Game", folderName, fileName),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "Game", folderName, fileName)),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Game", folderName, fileName))
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                string candidate = candidates[i];
                if (File.Exists(candidate))
                {
                    resolvedAssetPathCache[cacheKey] = candidate;
                    return candidate;
                }
            }

            resolvedAssetPathCache[cacheKey] = null;
            return null;
        }

    }
}
