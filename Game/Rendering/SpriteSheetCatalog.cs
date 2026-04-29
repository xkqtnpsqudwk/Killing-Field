using System.Collections.Generic;

namespace My2DEngine.Game.Rendering
{
    /// <summary>
    /// 파일명 기준으로 스프라이트 시트의 절단 방법을 명시적으로 정의한다.
    /// 자동 감지보다 이 카탈로그를 우선 적용한다.
    /// </summary>
    internal static class SpriteSheetCatalog
    {
        /// <summary>
        /// 스프라이트 시트 한 파일의 절단 스펙.
        /// </summary>
        internal readonly struct SheetSpec
        {
            /// <summary>셀 한 변의 픽셀 크기 (정방형 기준).</summary>
            public readonly int CellSize;

            /// <summary>행마다 어떤 애니메이션인지 순서대로 지정. Death는 여러 행에 걸쳐 중복 선언 가능.</summary>
            public readonly BossAnimationKind[] RowKinds;

            /// <summary>각 행에서 추출할 유효 프레임 수 (빈 열 패딩은 제외).</summary>
            public readonly int[] FramesPerRow;

            /// <summary>각 행 애니메이션의 FPS.</summary>
            public readonly float[] RowFps;

            public SheetSpec(
                int cellSize,
                BossAnimationKind[] rowKinds,
                int[] framesPerRow,
                float[] rowFps)
            {
                CellSize = cellSize;
                RowKinds = rowKinds;
                FramesPerRow = framesPerRow;
                RowFps = rowFps;
            }
        }

        // 파일 이름(확장자 제외, 대소문자 무시) → 스펙
        private static readonly Dictionary<string, SheetSpec> Specs =
            new Dictionary<string, SheetSpec>(System.StringComparer.OrdinalIgnoreCase)
        {
            // ── 일반 적 (1536×1024, 256px 셀, 6열×4행) ─────────────────────────

            ["beam_revenant_sheet_doom"] = new SheetSpec(
                cellSize: 256,
                rowKinds:     new[] { BossAnimationKind.Idle, BossAnimationKind.Move, BossAnimationKind.Attack, BossAnimationKind.Death },
                framesPerRow: new[] { 6, 5, 5, 6 },
                rowFps:       new[] { 7f, 8f, 10f, 4f }),

            ["blind_pinky_sheet_doom"] = new SheetSpec(
                cellSize: 256,
                rowKinds:     new[] { BossAnimationKind.Idle, BossAnimationKind.Move, BossAnimationKind.Attack, BossAnimationKind.Death },
                framesPerRow: new[] { 6, 6, 5, 6 },
                rowFps:       new[] { 6f, 8f, 10f, 4f }),

            ["blood_ghost_sheet_doom"] = new SheetSpec(
                cellSize: 256,
                rowKinds:     new[] { BossAnimationKind.Idle, BossAnimationKind.Move, BossAnimationKind.Attack, BossAnimationKind.Death },
                framesPerRow: new[] { 6, 6, 5, 6 },
                rowFps:       new[] { 6f, 7f, 10f, 4f }),

            ["hellion_sheet_doom"] = new SheetSpec(
                cellSize: 256,
                rowKinds:     new[] { BossAnimationKind.Idle, BossAnimationKind.Move, BossAnimationKind.Attack, BossAnimationKind.Death },
                framesPerRow: new[] { 6, 6, 5, 6 },
                rowFps:       new[] { 6f, 8f, 10f, 4f }),

            ["slime_imp_sheet_doom"] = new SheetSpec(
                cellSize: 256,
                rowKinds:     new[] { BossAnimationKind.Idle, BossAnimationKind.Move, BossAnimationKind.Attack, BossAnimationKind.Death },
                framesPerRow: new[] { 6, 6, 4, 5 },
                rowFps:       new[] { 6f, 7f, 10f, 4f }),

            ["zombie_scientist_sheet_doom"] = new SheetSpec(
                cellSize: 256,
                rowKinds:     new[] { BossAnimationKind.Idle, BossAnimationKind.Move, BossAnimationKind.Attack, BossAnimationKind.Death },
                framesPerRow: new[] { 6, 6, 4, 5 },
                rowFps:       new[] { 6f, 7f, 10f, 4f }),

            // ── 보스 (대부분 1536×1024, agaures만 1024×1536) ─────────────────

            // agaures: 4열×6행 (세로 방향), 죽음 애니메이션이 두 행에 걸침
            ["agaures_sheet_doom"] = new SheetSpec(
                cellSize: 256,
                rowKinds:     new[] { BossAnimationKind.Idle, BossAnimationKind.Move, BossAnimationKind.Attack, BossAnimationKind.Special, BossAnimationKind.Death, BossAnimationKind.Death },
                framesPerRow: new[] { 4, 4, 3, 4, 4, 4 },
                rowFps:       new[] { 5f, 6f, 8f, 8f, 4f, 4f }),

            ["arachnocortex_sheet_doom"] = new SheetSpec(
                cellSize: 256,
                rowKinds:     new[] { BossAnimationKind.Idle, BossAnimationKind.Move, BossAnimationKind.Attack, BossAnimationKind.Death },
                framesPerRow: new[] { 3, 6, 6, 6 },
                rowFps:       new[] { 5f, 7f, 10f, 4f }),

            ["azazel_sheet_doom"] = new SheetSpec(
                cellSize: 256,
                rowKinds:     new[] { BossAnimationKind.Idle, BossAnimationKind.Move, BossAnimationKind.Attack, BossAnimationKind.Death },
                framesPerRow: new[] { 6, 6, 5, 6 },
                rowFps:       new[] { 5f, 6f, 9f, 4f }),

            ["behemoth_sheet_doom"] = new SheetSpec(
                cellSize: 256,
                rowKinds:     new[] { BossAnimationKind.Idle, BossAnimationKind.Move, BossAnimationKind.Attack, BossAnimationKind.Death },
                framesPerRow: new[] { 5, 5, 4, 6 },
                rowFps:       new[] { 5f, 6f, 9f, 4f }),
        };

        public static bool TryGet(string fileStem, out SheetSpec spec)
            => Specs.TryGetValue(fileStem, out spec);
    }
}
