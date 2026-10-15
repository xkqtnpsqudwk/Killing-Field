using System;
using System.Collections.Generic;
using My2DEngine.Game.Config;

namespace My2DEngine.Game.Core
{
    /// <summary>
    /// 규칙 카드와 악마의 계약(<see cref="RunPerk"/>). 런에 한 번씩만 얻고, 숫자 대신 규칙을 바꾼다.
    /// 카드 생성, 적용, 전투·이동 규칙 연결을 여기 모았다. 수치는 <see cref="PerkConfig"/>.
    /// </summary>
    public partial class GameLogic
    {
        /// <summary>얻은 특성 비트 묶음(RunPerk 값 = 비트 위치). 런 저장에 그대로 남는다.</summary>
        private int runPerkMask;

        /// <summary>usedStatOfferKeys에 특성 카드를 넣을 때 스탯 키와 겹치지 않게 더하는 값.</summary>
        private const int PerkOfferKeyBase = 1 << 12;

        private bool HasPerk(RunPerk perk)
        {
            return (runPerkMask & RunPerkCatalog.Bit(perk)) != 0;
        }

        /// <summary>얻은 특성 목록(정의 순서).</summary>
        public RunPerk[] GetOwnedPerks()
        {
            var list = new List<RunPerk>();
            for (int i = 0; i < RunPerkCatalog.Count; i++)
            {
                if (HasPerk((RunPerk)i))
                {
                    list.Add((RunPerk)i);
                }
            }

            return list.ToArray();
        }

        // ── 카드 만들기 ──────────────────────────────────────────

        /// <summary>
        /// Purple 이상 스탯 카드 한 칸을 일정 확률로 규칙 카드로 바꾼다. 아직 얻지 않았고 이번 보상에 없는 것만.
        /// </summary>
        private RewardCardOffer TryCreateRuleOffer(CardGrade grade, ISet<int> usedOfferKeys)
        {
            if ((int)grade < PerkConfig.RuleMinGrade || rewardRandom.NextDouble() >= PerkConfig.RuleOfferChance)
            {
                return null;
            }

            RunPerk? perk = PickAvailablePerk(RunPerkKind.Rule, usedOfferKeys);
            return perk.HasValue ? CreatePerkOffer(perk.Value, grade) : null;
        }

        /// <summary>일반 보상에서 계약 카드가 끼어들 차례면 한 장 만든다(없으면 null).</summary>
        private RewardCardOffer TryCreateContractOffer(ISet<int> usedOfferKeys)
        {
            if (currentFloor < PerkConfig.ContractMinFloor || rewardRandom.NextDouble() >= PerkConfig.ContractOfferChance)
            {
                return null;
            }

            RunPerk? perk = PickAvailablePerk(RunPerkKind.Contract, usedOfferKeys);
            return perk.HasValue ? CreatePerkOffer(perk.Value, CardGrade.Red) : null;
        }

        private RunPerk? PickAvailablePerk(RunPerkKind kind, ISet<int> usedOfferKeys)
        {
            var candidates = new List<RunPerk>();
            for (int i = 0; i < RunPerkCatalog.Count; i++)
            {
                RunPerk perk = (RunPerk)i;
                if (RunPerkCatalog.Get(perk).Kind != kind || HasPerk(perk))
                {
                    continue;
                }

                if (usedOfferKeys != null && usedOfferKeys.Contains(PerkOfferKeyBase + i))
                {
                    continue;
                }

                candidates.Add(perk);
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            return candidates[templateRandom.Next(candidates.Count)];
        }

        private static RewardCardOffer CreatePerkOffer(RunPerk perk, CardGrade grade)
        {
            return new RewardCardOffer
            {
                IsPerkCard = true,
                Perk = perk,
                Grade = grade
            };
        }

        // ── 적용 ────────────────────────────────────────────────

        private void ApplyPerkCard(RewardCardOffer offer)
        {
            RunPerkDefinition definition = RunPerkCatalog.Get(offer.Perk);
            runPerkMask |= RunPerkCatalog.Bit(offer.Perk);
            ApplyCombinedProgressionStats(refillHealth: false, healMaxHealthDelta: false);
            string kind = definition.Kind == RunPerkKind.Contract ? "악마의 계약" : "규칙 카드";
            stageStatus.Show($"{kind}: {definition.Name}", 3.5f);
        }

        /// <summary>최대 체력 배율(피의 계약).</summary>
        private float PerkMaxHealthMult()
        {
            return HasPerk(RunPerk.BloodPact) ? PerkConfig.BloodPactMaxHealthMult : 1f;
        }

        /// <summary>이동 속도 배율(광기의 계약).</summary>
        private float PerkMoveSpeedMult()
        {
            return HasPerk(RunPerk.Frenzy) ? PerkConfig.FrenzyMoveSpeedMult : 1f;
        }

        /// <summary>카드 발사 속도에 광기의 계약을 겹친다. 쿨타임 감소끼리는 곱으로 합친다.</summary>
        private float PerkFireRateBonus(float cardBonus)
        {
            if (!HasPerk(RunPerk.Frenzy))
            {
                return cardBonus;
            }

            return 1f - ((1f - cardBonus) * (1f - PerkConfig.FrenzyFireRateBonus));
        }

        /// <summary>유리 대포는 보호막을 없앤다.</summary>
        private bool PerkRemovesShield()
        {
            return HasPerk(RunPerk.GlassCannon);
        }

        /// <summary>탐욕의 계약을 반영한 상점 가격.</summary>
        private int ApplyPerkShopPrice(int cost)
        {
            return HasPerk(RunPerk.Greed) ? Math.Max(1, (int)Math.Ceiling(cost * PerkConfig.GreedShopPriceMult)) : cost;
        }

        /// <summary>탐욕의 계약을 반영한 일반 적 코인 드랍 확률.</summary>
        private float ApplyPerkCoinChance(float chance, bool isNormalEnemy)
        {
            return HasPerk(RunPerk.Greed) && isNormalEnemy ? Math.Min(1f, chance * PerkConfig.GreedCoinChanceMult) : chance;
        }

        /// <summary>탐욕의 계약을 반영한 코인 개수.</summary>
        private int ApplyPerkCoinAmount(int amount)
        {
            return HasPerk(RunPerk.Greed) ? amount * PerkConfig.GreedCoinAmountMult : amount;
        }

        // ── 전투·이동 규칙 ──────────────────────────────────────

        /// <summary>관통탄: 맞은 적 뒤에서 가장 가까운 적에게 줄어든 피해를 한 번 더 준다.</summary>
        private void ApplyPierce(Enemy firstTarget, float spreadRadius, float damage)
        {
            if (firstTarget == null || !HasPerk(RunPerk.Piercing))
            {
                return;
            }

            Enemy second = FindBestTargetWithSpread(spreadRadius, weapon.Range, firstTarget, out _);
            DamageEnemy(second, damage * PerkConfig.PierceDamageRatio);
        }

        /// <summary>시체 폭발: 쓰러진 적 자리에서 그 적 최대 체력에 비례한 폭발.</summary>
        private void TryCorpseBlast(Enemy enemy)
        {
            if (!HasPerk(RunPerk.CorpseBlast) || enemy == null)
            {
                return;
            }

            float damage = enemy.MaxHealth * PerkConfig.CorpseBlastHealthRatio;
            ApplyExplosionDamage(enemy.X, enemy.Y, damage, PerkConfig.CorpseBlastRadius);
            SpawnBlastVisual(enemy.X, enemy.Y, PerkConfig.CorpseBlastRadius);
        }

        /// <summary>대시 충격파: 대시를 시작한 자리 주변 적에게 피해와 기절.</summary>
        private void TryDashShock()
        {
            if (!HasPerk(RunPerk.DashShock))
            {
                return;
            }

            float x = player.Position.X;
            float y = player.Position.Y;
            StunEnemiesAround(x, y, PerkConfig.DashShockRadius, PerkConfig.DashShockStun);
            ApplyExplosionDamage(x, y, weapon.CurrentDamage * PerkConfig.DashShockDamageMult, PerkConfig.DashShockRadius);
            SpawnBlastVisual(x, y, PerkConfig.DashShockRadius);
        }

        /// <summary>보호막 파열: 보호막이 막 깨졌으면 주변에 폭발과 기절.</summary>
        private void TryShieldBurst(float shieldBefore)
        {
            if (!HasPerk(RunPerk.ShieldBurst) || shieldBefore <= 0f || player.Shield > 0f)
            {
                return;
            }

            float x = player.Position.X;
            float y = player.Position.Y;
            StunEnemiesAround(x, y, PerkConfig.ShieldBurstRadius, PerkConfig.ShieldBurstStun);
            ApplyExplosionDamage(x, y, player.MaxShield * PerkConfig.ShieldBurstShieldRatio, PerkConfig.ShieldBurstRadius);
            SpawnBlastVisual(x, y, PerkConfig.ShieldBurstRadius);
        }

        private void StunEnemiesAround(float x, float y, float radius, float seconds)
        {
            Enemy[] enemies = enemyManager.Enemies;
            if (enemies == null)
            {
                return;
            }

            for (int i = 0; i < enemies.Length; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy == null || !enemy.Alive)
                {
                    continue;
                }

                float dx = enemy.X - x;
                float dy = enemy.Y - y;
                float reach = radius + enemy.Radius;
                if ((dx * dx) + (dy * dy) <= reach * reach)
                {
                    enemy.StunTimer = Math.Max(enemy.StunTimer, seconds);
                }
            }
        }

        /// <summary>
        /// 학살의 계약: 전투 중 처치 창이 끊기면 체력이 깎인다(체력 1 아래로는 깎지 않는다).
        /// 살아 있는 적이 없으면 창을 가득 채워 두어 방 사이에서는 깎이지 않는다.
        /// </summary>
        private void UpdatePerks(float dt)
        {
            if (!HasPerk(RunPerk.Slaughter) || player == null || player.IsDead)
            {
                return;
            }

            if (enemyManager.CountAliveEnemies() <= 0)
            {
                modifiers.SlaughterTimer = PerkConfig.SlaughterWindow;
                return;
            }

            if (modifiers.SlaughterTimer > 0f || player.Health <= 1f)
            {
                return;
            }

            float drain = player.MaxHealth * PerkConfig.SlaughterDrainPerSecond * Math.Max(0f, dt);
            player.Health = Math.Max(1f, player.Health - drain);
        }

        /// <summary>폭발 모양만 보여 준다(피해 없음). 로켓 폭발 그림을 쓴다.</summary>
        private void SpawnBlastVisual(float x, float y, float radius)
        {
            playerProjectiles.Add(new EnemyProjectile
            {
                Kind = EnemyProjectileKind.PlayerRocketExplosion,
                X = x,
                Y = y,
                Radius = Math.Min(Math.Max(0.42f, radius * 0.42f), 1.35f),
                Damage = 0f,
                Lifetime = WeaponConfig.RocketExplosionVisualDuration,
                Active = true
            });
            audio.PlayEffect(AudioConfig.RocketBoomSoundAlias, false);
        }
    }
}
