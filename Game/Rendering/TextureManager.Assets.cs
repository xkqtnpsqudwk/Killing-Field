using System;
using System.IO;

namespace My2DEngine.Game.Rendering
{
    /// <summary>
    /// 에셋 파일 경로 해석과 디렉터리 탐색을 담당하는 partial 클래스.
    /// 이미지 파일 또는 디렉터리의 절대 경로를 여러 후보 위치에서 탐색하고
    /// 내부 캐시를 통해 중복 탐색을 방지한다.
    /// </summary>
    public partial class TextureManager
    {
        /// <summary>
        /// 지정한 파일 이름으로 Images 폴더 내의 절대 경로를 반환한다.
        /// 내부적으로 <see cref="ResolveAssetPath"/>를 통해 경로를 탐색한다.
        /// </summary>
        /// <param name="fileName">찾을 이미지 파일 이름 (예: "wall.png")</param>
        /// <returns>이미지 파일의 절대 경로. 찾지 못하면 null.</returns>
        private string ResolveImagePath(string fileName)
        {
            return ResolveAssetPath("Images", fileName);
        }

        /// <summary>
        /// 지정한 디렉터리 세그먼트들로 Images 폴더 하위의 절대 디렉터리 경로를 반환한다.
        /// 내부적으로 <see cref="ResolveAssetDirectory"/>를 통해 탐색한다.
        /// </summary>
        /// <param name="directorySegments">
        /// 디렉터리 경로 세그먼트 배열. 순서대로 조합하여 하위 디렉터리를 탐색한다.
        /// (예: "Item" → Images/Item/, "Enemy", "Gunner" → Images/Enemy/Gunner/)
        /// </param>
        /// <returns>존재하는 디렉터리의 절대 경로. 찾지 못하면 null.</returns>
        private string ResolveImageDirectory(params string[] directorySegments)
        {
            return ResolveAssetDirectory("Images", directorySegments);
        }

        /// <summary>
        /// 지정한 폴더 이름과 파일 이름으로부터 에셋의 실제 절대 경로를 탐색하여 반환한다.
        /// 탐색 순서:
        /// 1. 실행 디렉터리/Game/{folderName}/{fileName}
        /// 2. 2단계 상위/Game/{folderName}/{fileName}
        /// 3. 3단계 상위/Game/{folderName}/{fileName}
        /// 4. 위 경로들의 {folderName} 폴더 내 재귀 탐색
        /// 결과는 캐시에 저장되어 이후 동일 요청 시 파일 시스템 접근 없이 즉시 반환된다.
        /// </summary>
        /// <param name="folderName">에셋 루트 하위의 폴더 이름 (예: "Images", "Sound")</param>
        /// <param name="fileName">찾을 파일 이름 (예: "wall.png")</param>
        /// <returns>파일의 절대 경로. 찾지 못하면 null (null도 캐시됨).</returns>
        private string ResolveAssetPath(string folderName, string fileName)
        {
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

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    resolvedAssetPathCache[cacheKey] = candidate;
                    return candidate;
                }
            }

            string[] rootCandidates = new string[]
            {
                Path.Combine(baseDir, "Game", folderName),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "Game", folderName)),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Game", folderName))
            };

            for (int i = 0; i < rootCandidates.Length; i++)
            {
                string root = rootCandidates[i];
                if (!Directory.Exists(root))
                {
                    continue;
                }

                string[] matches = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
                if (matches.Length > 0)
                {
                    resolvedAssetPathCache[cacheKey] = matches[0];
                    return matches[0];
                }
            }

            resolvedAssetPathCache[cacheKey] = null;
            return null;
        }

        /// <summary>
        /// 지정한 폴더 이름과 디렉터리 세그먼트들로부터 에셋 디렉터리의 실제 절대 경로를 탐색하여 반환한다.
        /// 탐색 순서:
        /// 1. 실행 디렉터리/Game/{folderName}/{segment1}/{segment2}/...
        /// 2. 2단계 상위 경로에서 동일 패턴
        /// 3. 3단계 상위 경로에서 동일 패턴
        /// 결과는 캐시에 저장되어 이후 동일 요청 시 파일 시스템 접근 없이 즉시 반환된다.
        /// </summary>
        /// <param name="folderName">에셋 루트 하위의 기준 폴더 이름 (예: "Images")</param>
        /// <param name="directorySegments">기준 폴더 하위의 경로 세그먼트 배열 (예: "Enemy", "Gunner", "Idle")</param>
        /// <returns>존재하는 디렉터리의 절대 경로. 찾지 못하면 null (null도 캐시됨).</returns>
        private string ResolveAssetDirectory(string folderName, params string[] directorySegments)
        {
            string cacheKey = folderName + "|" + string.Join("|", directorySegments);
            if (resolvedAssetDirectoryCache.TryGetValue(cacheKey, out string cachedDirectory))
            {
                return cachedDirectory;
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] roots = new string[]
            {
                Path.Combine(baseDir, "Game", folderName),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "Game", folderName)),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Game", folderName))
            };

            for (int i = 0; i < roots.Length; i++)
            {
                string current = roots[i];
                if (!Directory.Exists(current))
                {
                    continue;
                }

                for (int segmentIndex = 0; segmentIndex < directorySegments.Length; segmentIndex++)
                {
                    current = Path.Combine(current, directorySegments[segmentIndex]);
                }

                if (Directory.Exists(current))
                {
                    resolvedAssetDirectoryCache[cacheKey] = current;
                    return current;
                }
            }

            resolvedAssetDirectoryCache[cacheKey] = null;
            return null;
        }
    }
}
