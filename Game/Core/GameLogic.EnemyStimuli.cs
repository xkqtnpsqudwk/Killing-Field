namespace My2DEngine.Game.Core
{
    /// <summary>
    /// 플레이어 행동을 적 청각 자극으로 변환하는 partial 클래스다.
    /// </summary>
    public partial class GameLogic
    {
        private void EmitEnemyAlertSound(float x, float y, float radius, float dirX = 0f, float dirY = 0f)
        {
            enemyManager?.EmitPlayerSound(x, y, radius, dirX, dirY);
        }

        private void EmitWeaponFireNoise()
        {
            EmitEnemyAlertSound(
                player.Position.X,
                player.Position.Y,
                GetWeaponNoiseRadius(weapon.CurrentType),
                player.Direction.X,
                player.Direction.Y);
        }

        private static float GetWeaponNoiseRadius(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.BearKiller:
                    return 13.5f;
                case WeaponType.HChainGun:
                    return 11.25f;
                case WeaponType.AutoCannon:
                    return 15f;
                case WeaponType.DuelBerettas:
                    return 8.75f;
                case WeaponType.AMPistol:
                default:
                    return 9.5f;
            }
        }
    }
}
