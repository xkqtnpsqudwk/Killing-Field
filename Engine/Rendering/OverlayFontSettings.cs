using System;
using System.Collections.Generic;
using System.IO;

namespace My2DEngine.Engine.Rendering
{
    /// <summary>
    /// 오버레이 텍스트 글꼴 설정. 렌더 백엔드를 만들기 전에 한 번 정한다.
    /// 글꼴 파일을 지정하면 시스템에 설치하지 않고 그 파일로 글자를 그린다.
    /// 픽셀 격자 크기를 주면 글자 크기를 그 배수로 맞추고 안티앨리어싱 없이 그려 픽셀 글꼴이 깨지지 않게 한다.
    /// </summary>
    public static class OverlayFontSettings
    {
        private static readonly List<string> fontFiles = new List<string>();

        /// <summary>글꼴 패밀리 이름. 파일을 못 읽으면 이 이름의 시스템 글꼴을 찾고, 그것도 없으면 <see cref="FallbackFamilyName"/>을 쓴다.</summary>
        public static string FamilyName { get; private set; } = FallbackFamilyName;

        /// <summary>글꼴을 못 찾을 때 쓰는 시스템 글꼴.</summary>
        public const string FallbackFamilyName = "Malgun Gothic";

        /// <summary>픽셀 글꼴의 기준 픽셀 크기. 0이면 크기를 맞추지 않고 안티앨리어싱으로 그린다.</summary>
        public static int PixelGridSize { get; private set; }

        /// <summary>함께 불러올 글꼴 파일 경로(보통·굵게).</summary>
        public static IReadOnlyList<string> FontFiles => fontFiles;

        /// <summary>
        /// 픽셀 글꼴을 쓴다. 존재하는 파일만 등록한다.
        /// </summary>
        /// <param name="familyName">글꼴 패밀리 이름(예: Galmuri11).</param>
        /// <param name="pixelGridSize">글꼴이 설계된 픽셀 크기(예: 12). 글자 크기를 이 배수로 맞춘다.</param>
        /// <param name="files">글꼴 파일 경로들.</param>
        public static void UsePixelFont(string familyName, int pixelGridSize, params string[] files)
        {
            if (string.IsNullOrWhiteSpace(familyName))
                throw new ArgumentException("Font family name is required.", nameof(familyName));

            FamilyName = familyName;
            PixelGridSize = System.Math.Max(0, pixelGridSize);
            fontFiles.Clear();
            if (files == null)
                return;

            foreach (string file in files)
            {
                if (!string.IsNullOrWhiteSpace(file) && File.Exists(file))
                    fontFiles.Add(Path.GetFullPath(file));
            }
        }
    }
}
