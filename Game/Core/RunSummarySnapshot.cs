namespace My2DEngine.Game.Core
{
    /// <summary>
    /// 현재 런 종료 화면과 기록 저장에 표시할 최소 런 요약 정보.
    /// </summary>
    public readonly struct RunSummarySnapshot
    {
        public RunSummarySnapshot(int floorReached, int enemiesKilled, int bossesKilled, int durationSeconds)
        {
            FloorReached = floorReached;
            EnemiesKilled = enemiesKilled;
            BossesKilled = bossesKilled;
            DurationSeconds = durationSeconds;
        }

        public int FloorReached { get; }

        public int EnemiesKilled { get; }

        public int BossesKilled { get; }

        public int DurationSeconds { get; }
    }
}
