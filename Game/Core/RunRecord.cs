namespace My2DEngine.Game.Core
{
    public sealed class RunRecord
    {
        public int Id { get; set; }
        public int FloorReached { get; set; }
        public int EnemiesKilled { get; set; }
        public int BossesKilled { get; set; }
        public int DurationSeconds { get; set; }
        public string EndedAt { get; set; }
    }
}
