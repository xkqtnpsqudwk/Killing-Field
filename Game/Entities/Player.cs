using My2DEngine.Engine.Math;
using My2DEngine.Game.Config;

namespace My2DEngine.Game
{
    /// <summary>
    /// 플레이어의 이동·체력·스태미나·시야 같은 런타임 상태를 담는 엔티티다.
    /// GameLogic이 입력을 처리하고 이 클래스의 메서드를 호출해 상태를 갱신하며,
    /// 렌더러는 Position·Direction·Plane을 읽어 레이캐스팅을 수행한다.
    /// </summary>
    public class Player
    {
        /// <summary>플레이어의 현재 월드 좌표. 충돌 이후 실제 이동이 반영된 위치다.</summary>
        public Vector2 Position { get; set; }

        /// <summary>플레이어가 바라보는 방향 단위 벡터. Rotate() 호출마다 갱신된다.</summary>
        public Vector2 Direction { get; set; }

        /// <summary>
        /// 레이캐스팅 시야면(camera plane) 벡터.
        /// FOV가 바뀌거나 Rotate()가 호출될 때마다 Direction과 함께 갱신된다.
        /// </summary>
        public Vector2 Plane { get; set; }

        /// <summary>현재 체력. 0 이하가 되면 IsDead가 true로 바뀐다.</summary>
        public float Health { get; set; }

        /// <summary>최대 체력. GameConfig.PlayerHealthMax로 초기화된다. 카드 보너스로 증가할 수 있다.</summary>
        public float MaxHealth { get; set; }

        /// <summary>현재 보호막. 피해를 먼저 흡수하고, 일정 시간 피해가 없으면 회복된다.</summary>
        public float Shield { get; private set; }

        /// <summary>최대 보호막. 기본값은 GameConfig.PlayerShieldMax다.</summary>
        public float MaxShield { get; private set; }

        /// <summary>보호막 초당 회복량.</summary>
        public float ShieldRegenRate { get; private set; }

        /// <summary>피해를 입은 뒤 보호막 회복이 시작되기까지의 대기 시간(초).</summary>
        public float ShieldRegenDelayDuration { get; private set; }

        /// <summary>현재 남은 보호막 회복 지연 타이머(초).</summary>
        public float ShieldRegenDelayTimer { get; set; }

        /// <summary>플레이어 사망 여부. true이면 입력·이동·발사가 무시된다.</summary>
        public bool IsDead { get; set; }

        /// <summary>현재 스태미나. 달리기/대시 시 소모되고 멈추면 자동 회복된다.</summary>
        public float Stamina { get; set; }

        /// <summary>최대 스태미나. GameConfig.StaminaMax로 초기화된다.</summary>
        public float MaxStamina { get; private set; }

        /// <summary>스태미나 회복 지연 타이머(초). 0이 돼야 회복이 시작된다.</summary>
        public float StaminaRecoverTimer { get; set; }

        /// <summary>대시 재사용 대기 타이머(초). 0이 돼야 다시 대시할 수 있다.</summary>
        public float DashCooldownTimer { get; set; }

        /// <summary>대시 지속 타이머(초). 0보다 크면 IsDashing이 true가 된다.</summary>
        public float DashTimer { get; set; }

        /// <summary>현재 대시 방향 X 성분(정규화).</summary>
        public float DashDirX { get; set; }

        /// <summary>현재 대시 방향 Y 성분(정규화).</summary>
        public float DashDirY { get; set; }

        /// <summary>DashTimer > 0이면 대시 중임을 나타낸다.</summary>
        public bool IsDashing => DashTimer > 0f;

        /// <summary>기본 이동 속도(타일/초). GameConfig.MoveSpeed로 초기화된다.</summary>
        public float MoveSpeed { get; set; }

        /// <summary>마우스 수평 이동 1픽셀당 회전 각도(라디안). 설정에서 조절 가능하다.</summary>
        public float MouseSensitivity { get; set; }

        /// <summary>플레이어 충돌 원 반지름(타일 단위). 벽·적과의 충돌 판정에 사용된다.</summary>
        public float Radius { get; private set; }

        /// <summary>플레이어가 현재 서 있는 타일의 바닥 높이(0.0~1.0). 계단·단차 이동 시 갱신된다.</summary>
        public float FloorZ { get; set; }

        /// <summary>현재 보유 중인 코인 수. 휴식 상점에서 소비한다.</summary>
        public int CoinCount { get; private set; }

        /// <summary>
        /// 대시 쿨다운 배율. 1.0 = 기본, 값이 낮을수록 쿨다운이 짧아진다.
        /// 스탯 카드 '대시 쿨타임'으로 감소시킬 수 있다.
        /// </summary>
        public float DashCooldownMult { get; private set; } = 1f;

        /// <summary>FovDegrees 프로퍼티 백킹 필드. 직접 쓰지 않고 FovDegrees를 통해 접근한다.</summary>
        private float fovDegrees;

        /// <summary>
        /// 현재 시야각(도 단위). Min/Max 범위로 클램핑하고 바뀔 때마다 Plane을 즉시 재계산한다.
        /// FOV가 바뀌면 시야면(plane)도 즉시 다시 계산해야
        /// ray direction과 투영이 다음 프레임부터 일관되게 유지된다.
        /// </summary>
        public float FovDegrees
        {
            get => fovDegrees;
            set
            {
                fovDegrees = System.Math.Max(GameConfig.MinFovDegrees, System.Math.Min(GameConfig.MaxFovDegrees, value));
                UpdatePlaneFromFov();
            }
        }

        /// <summary>
        /// 주어진 월드 좌표에 플레이어를 생성하고 모든 상태를 초기값으로 설정한다.
        /// </summary>
        /// <param name="x">시작 월드 X 좌표</param>
        /// <param name="y">시작 월드 Y 좌표</param>
        public Player(float x, float y)
        {
            Position = new Vector2(x, y);
            Direction = new Vector2(1f, 0f);
            Plane = new Vector2(0f, 0f);

            Health = GameConfig.PlayerHealthMax;
            MaxHealth = GameConfig.PlayerHealthMax;
            MaxShield = GameConfig.PlayerShieldMax;
            Shield = MaxShield;
            ShieldRegenRate = GameConfig.PlayerShieldBaseRegenRate;
            ShieldRegenDelayDuration = GameConfig.PlayerShieldBaseRegenDelay;
            ShieldRegenDelayTimer = 0f;
            IsDead = false;

            Stamina = GameConfig.StaminaMax;
            MaxStamina = GameConfig.StaminaMax;
            StaminaRecoverTimer = 0f;
            DashCooldownTimer = 0f;
            DashTimer = 0f;
            DashDirX = 0f;
            DashDirY = 0f;

            MoveSpeed = GameConfig.MoveSpeed;
            MouseSensitivity = GameConfig.MouseSensitivity;
            Radius = GameConfig.PlayerRadius;
            CoinCount = 0;

            fovDegrees = GameConfig.DefaultFovDegrees;
            UpdatePlaneFromFov();
        }

        /// <summary>
        /// 플레이어를 최초 생성 상태로 되돌린다. 체력·스태미나·대시 상태를 모두 초기화한다.
        /// 스테이지 재시작이나 게임 오버 직후에 호출된다.
        /// </summary>
        public void Reset()
        {
            ResetRunBonuses();
            Health = MaxHealth;
            Shield = MaxShield;
            ShieldRegenDelayTimer = 0f;
            IsDead = false;
            Stamina = MaxStamina;
            StaminaRecoverTimer = 0f;
            DashCooldownTimer = 0f;
            DashTimer = 0f;
            DashDirX = 0f;
            DashDirY = 0f;
            CoinCount = 0;
        }

        /// <summary>
        /// 피해를 받아 체력을 감소시킨다. 체력이 0 이하가 되면 IsDead를 true로 바꾼다.
        /// </summary>
        /// <param name="damage">받는 피해량(양수). 0 이하이면 체력이 증가하지 않도록 주의한다.</param>
        public void TakeDamage(float damage)
        {
            Health -= damage;
            if (Health <= 0f)
            {
                Health = 0f;
                IsDead = true;
            }
        }

        /// <summary>
        /// 보호막으로 피해를 먼저 흡수하고, 남은 피해량을 반환한다.
        /// 실제 피해가 들어오면 보호막 회복 지연 타이머가 다시 시작된다.
        /// </summary>
        public float AbsorbShieldDamage(float damage)
        {
            if (damage <= 0f)
            {
                return 0f;
            }

            ShieldRegenDelayTimer = ShieldRegenDelayDuration;
            if (Shield <= 0f || MaxShield <= 0f)
            {
                Shield = 0f;
                return damage;
            }

            float absorbed = System.Math.Min(Shield, damage);
            Shield -= absorbed;
            if (Shield < 0f)
            {
                Shield = 0f;
            }

            return damage - absorbed;
        }

        /// <summary>
        /// 플레이어를 주어진 각도만큼 수평 회전한다.
        /// Direction과 Plane 벡터를 2D 회전 행렬로 변환해 동시에 회전시킨다.
        /// Plane이 Direction과 항상 직각을 유지하므로 FOV가 보존된다.
        /// </summary>
        /// <param name="angle">회전 각도(라디안). 양수면 시계 반대 방향.</param>
        public void Rotate(float angle)
        {
            float cos = (float)System.Math.Cos(angle);
            float sin = (float)System.Math.Sin(angle);

            float oldDirX = Direction.X;
            Direction = new Vector2(
                Direction.X * cos - Direction.Y * sin,
                oldDirX * sin + Direction.Y * cos);

            float oldPlaneX = Plane.X;
            Plane = new Vector2(
                Plane.X * cos - Plane.Y * sin,
                oldPlaneX * sin + Plane.Y * cos);
        }

        public void UpdateStamina(float dt, bool isSprinting)
        {
            if (isSprinting)
            {
                // 달리는 동안에는 즉시 회복 지연을 갱신해 짧게 끊어 달려도 바로 차오르지 않게 한다.
                Stamina -= GameConfig.StaminaDrainPerSec * dt;
                if (Stamina < 0f) Stamina = 0f;
                StaminaRecoverTimer = GameConfig.StaminaRecoverDelay;
            }
            else
            {
                if (StaminaRecoverTimer > 0f)
                {
                    // 회복 지연이 끝난 뒤에만 실제 회복을 시작한다.
                    StaminaRecoverTimer -= dt;
                    if (StaminaRecoverTimer < 0f) StaminaRecoverTimer = 0f;
                }
                else
                {
                    Stamina += GameConfig.StaminaRecoverPerSec * dt;
                    if (Stamina > MaxStamina) Stamina = MaxStamina;
                }
            }
        }

        public void UpdateShield(float dt)
        {
            if (dt <= 0f || MaxShield <= 0f)
            {
                return;
            }

            if (Shield >= MaxShield)
            {
                // 완충 상태에서는 지연 타이머를 비워 다음 피격 후 새 지연만 남도록 한다.
                Shield = MaxShield;
                ShieldRegenDelayTimer = 0f;
                return;
            }

            if (ShieldRegenDelayTimer > 0f)
            {
                // 피해를 입은 뒤 일정 시간은 보호막이 회복되지 않는다.
                ShieldRegenDelayTimer -= dt;
                if (ShieldRegenDelayTimer < 0f)
                {
                    ShieldRegenDelayTimer = 0f;
                }
                return;
            }

            Shield += ShieldRegenRate * dt;
            if (Shield > MaxShield)
            {
                Shield = MaxShield;
            }
        }

        public void UpdateDashCooldown(float dt)
        {
            if (DashCooldownTimer <= 0f)
            {
                DashCooldownTimer = 0f;
                return;
            }

            DashCooldownTimer -= dt;
            if (DashCooldownTimer < 0f)
            {
                DashCooldownTimer = 0f;
            }
        }

        public void UpdateDash(float dt)
        {
            if (DashTimer <= 0f)
            {
                DashTimer = 0f;
                return;
            }

            DashTimer -= dt;
            if (DashTimer < 0f)
            {
                DashTimer = 0f;
            }
        }

        public void AddCoins(int count)
        {
            if (count <= 0)
            {
                return;
            }

            CoinCount += count;
        }

        public bool TrySpendCoins(int count)
        {
            if (count <= 0)
            {
                return true;
            }

            if (CoinCount < count)
            {
                return false;
            }

            CoinCount -= count;
            return true;
        }

        public void SetCoinCount(int count)
        {
            CoinCount = System.Math.Max(0, count);
        }

        public void ConfigureShield(float maxShield, float regenRate, float regenDelayDuration)
        {
            // 카드/영구 성장 재적용 중에도 현재 보호막은 새 최대치 안으로만 보정한다.
            // 최대치가 커졌다고 즉시 완충하지 않으므로 전투 중 스탯 적용이 과한 회복이 되지 않는다.
            MaxShield = System.Math.Max(0f, maxShield);
            Shield = System.Math.Max(0f, System.Math.Min(MaxShield, Shield));
            ShieldRegenRate = System.Math.Max(0f, regenRate);
            ShieldRegenDelayDuration = System.Math.Max(GameConfig.PlayerShieldMinRegenDelay, regenDelayDuration);
            ShieldRegenDelayTimer = System.Math.Max(0f, System.Math.Min(ShieldRegenDelayDuration, ShieldRegenDelayTimer));
        }

        public void RestoreShieldState(float shield, float regenDelayTimer)
        {
            Shield = System.Math.Max(0f, System.Math.Min(MaxShield, shield));
            ShieldRegenDelayTimer = System.Math.Max(0f, System.Math.Min(ShieldRegenDelayDuration, regenDelayTimer));
        }

        public float GetEffectiveSpeed(bool sprintHeld)
        {
            bool exhausted = Stamina <= 0f;
            bool canSprint = sprintHeld && !exhausted;

            float speed;
            if (exhausted)
            {
                speed = MoveSpeed * GameConfig.ExhaustedSpeedMultiplier;
            }
            else if (canSprint)
            {
                speed = MoveSpeed * GameConfig.SprintMultiplier;
            }
            else
            {
                speed = MoveSpeed;
            }

            return speed;
        }

        /// <summary>
        /// 현재 대시를 사용할 수 있는지 확인한다.
        /// 살아 있고 쿨다운이 끝났으며 스태미나가 충분해야 한다.
        /// </summary>
        /// <returns>대시 가능하면 true</returns>
        public bool CanDash()
        {
            return !IsDead &&
                DashCooldownTimer <= 0f &&
                Stamina >= GameConfig.DashStaminaCost;
        }

        /// <summary>
        /// 대시에 필요한 스태미나를 소비하고 쿨다운을 시작한다.
        /// CanDash()가 false이면 아무 작업 없이 false를 반환한다.
        /// </summary>
        /// <returns>스태미나 소비에 성공하면 true</returns>
        public bool TrySpendDash()
        {
            if (!CanDash())
            {
                return false;
            }

            Stamina -= GameConfig.DashStaminaCost;
            if (Stamina < 0f)
            {
                Stamina = 0f;
            }

            DashCooldownTimer = GameConfig.DashCooldownDuration * DashCooldownMult;
            StaminaRecoverTimer = GameConfig.StaminaRecoverDelay;
            return true;
        }

        /// <summary>
        /// 대시 쿨다운 배율을 설정한다. 0.3 이하로는 내려가지 않는다.
        /// </summary>
        /// <param name="mult">새 배율 (1.0 = 기본, 0.4 = 60% 감소).</param>
        public void SetDashCooldownMult(float mult)
        {
            float minMult = GameConfig.DashCooldownMinDuration / GameConfig.DashCooldownDuration;
            DashCooldownMult = System.Math.Max(minMult, mult);
        }

        /// <summary>
        /// 런 중 획득한 스탯 카드 보너스를 초기값으로 되돌린다.
        /// <see cref="StartRoguelikeRun"/> 시작 직전에 호출한다.
        /// </summary>
        public void ResetRunBonuses()
        {
            MaxHealth = GameConfig.PlayerHealthMax;
            MaxShield = GameConfig.PlayerShieldMax;
            Shield = System.Math.Min(Shield, MaxShield);
            ShieldRegenRate = GameConfig.PlayerShieldBaseRegenRate;
            ShieldRegenDelayDuration = GameConfig.PlayerShieldBaseRegenDelay;
            ShieldRegenDelayTimer = 0f;
            MoveSpeed = GameConfig.MoveSpeed;
            DashCooldownMult = 1f;
        }

        /// <summary>
        /// 대시 방향을 정규화하고 DashTimer를 시작한다.
        /// 대시는 방향을 정규화한 뒤 timer만 시작해 두고,
        /// 실제 이동은 update 루프에서 짧은 시간 동안 분할 적용한다.
        /// </summary>
        /// <param name="dirX">대시 방향 X 성분(정규화 불필요)</param>
        /// <param name="dirY">대시 방향 Y 성분(정규화 불필요)</param>
        public void StartDash(float dirX, float dirY)
        {
            float len = (float)System.Math.Sqrt((dirX * dirX) + (dirY * dirY));

            if (len <= 0.001f)
            {
                DashDirX = 0f;
                DashDirY = 0f;
                DashTimer = 0f;
                return;
            }

            DashDirX = dirX / len;
            DashDirY = dirY / len;
            DashTimer = GameConfig.DashDuration;
        }

        /// <summary>
        /// 현재 fovDegrees로부터 시야면(Plane) 벡터를 재계산한다.
        /// Plane 길이 = tan(FOV/2). Direction에 수직으로 배치된다.
        /// FOV 변경 또는 생성 직후 한 번 호출해 Plane을 동기화한다.
        /// </summary>
        private void UpdatePlaneFromFov()
        {
            float halfRad = fovDegrees * 0.5f * (float)System.Math.PI / 180f;
            float planeLen = (float)System.Math.Tan(halfRad);
            Plane = new Vector2(-Direction.Y * planeLen, Direction.X * planeLen);
        }
    }
}
