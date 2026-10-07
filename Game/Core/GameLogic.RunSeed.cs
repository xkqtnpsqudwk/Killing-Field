using System;

namespace My2DEngine.Game.Core
{
    /// <summary>
    /// GameLogic의 런 시드를 관리하는 partial.
    /// 보상·방 템플릿·적 난수를 하나의 시드에서 파생해, 같은 시드면 같은 방·카드·스폰이 나오게 한다.
    /// </summary>
    public partial class GameLogic
    {
        private int runSeed;

        /// <summary>현재 런의 시드. F3 디버그 HUD에 표시된다.</summary>
        public int RunSeed => runSeed;

        /// <summary>
        /// 고정 시드. 값이 있으면 모든 새 런이 이 시드로 시작한다(`--seed N` 실행 인자).
        /// null이면 런마다 새 시드를 뽑는다.
        /// </summary>
        public int? FixedRunSeed { get; set; }

        /// <summary>런을 시작할 때 고정 시드 또는 새 시드로 모든 난수 생성기를 다시 만든다.</summary>
        private void BeginRunRandom()
        {
            ReseedRandom(FixedRunSeed ?? CreateRandomSeed());
        }

        /// <summary>시드 하나에서 보상·템플릿·적 난수 생성기를 서로 다른 값으로 파생해 만든다.</summary>
        private void ReseedRandom(int seed)
        {
            runSeed = seed;
            rewardRandom = new Random(DeriveSeed(seed, 1));
            templateRandom = new Random(DeriveSeed(seed, 2));
            enemyManager.Reseed(DeriveSeed(seed, 3));
        }

        private static int CreateRandomSeed()
        {
            return Random.Shared.Next(1, int.MaxValue);
        }

        /// <summary>같은 시드에서 용도별로 겹치지 않는 하위 시드를 만든다.</summary>
        private static int DeriveSeed(int seed, int stream)
        {
            unchecked
            {
                return (seed * 397) ^ (stream * 0x2545F491);
            }
        }
    }
}
