using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using My2DEngine.Engine.Input;
using My2DEngine.Engine.Rendering;
using My2DEngine.Game;
using My2DEngine.Game.Config;

namespace My2DEngine.Game.Core
{
    /// <summary>
    /// GameLogic의 카드 보상 시스템 partial.
    /// 룸 클리어 후 카드 3장을 제시하고, 1장 선택에 따라 플레이어/무기 스탯을 즉시 반영한다.
    /// </summary>
    public partial class GameLogic
    {
        /// <summary>룸 클리어 후 카드가 실제로 나타나기까지 대기하는 시간(초).</summary>
        private const float CardRewardRevealDelaySeconds = 2f;
        private const int RunStatCount = 6;

        private static readonly StatType[] RewardStatTypes =
        {
            StatType.MaxHealth,
            StatType.MoveSpeed,
            StatType.DashCooldown,
            StatType.AmmoDropChance,
            StatType.Damage,
            StatType.CoinDropChance
        };

        // ── 카드 보상 상태 ──────────────────────────────────────────────

        /// <summary>카드 선택 UI가 현재 활성화되어 있는지 여부.</summary>
        private bool cardRewardActive;

        /// <summary>룸 클리어 후 카드 보상 UI를 실제로 띄우기까지 남은 대기 시간(초).</summary>
        private float cardRewardRevealTimer;

        /// <summary>카드 UI를 연 방이 보스 방이었는지 여부. 완료 후 분기 처리에 사용.</summary>
        private bool cardRewardWasBossRoom;

        /// <summary>카드 UI를 연 후 다음 층이 보스인지 여부. 완료 후 분기/E키 처리에 사용.</summary>
        private bool cardRewardNextFloorIsBoss;

        /// <summary>현재 제시 중인 카드 3장. null이면 해당 슬롯 비어 있음.</summary>
        private readonly RewardCardOffer[] currentCardOffers = new RewardCardOffer[3];

        /// <summary>마우스 클릭으로 카드 선택 시 처리할 게임 좌표 X (-1이면 미처리).</summary>
        private float pendingCardClickX = -1f;
        /// <summary>마우스 클릭으로 카드 선택 시 처리할 게임 좌표 Y.</summary>
        private float pendingCardClickY = -1f;

        /// <summary>
        /// 현재 런에서 각 StatType에 보유 중인 카드 등급.
        /// -1 = 미보유, 0 = White, 1 = Green, 2 = Blue, 3 = Purple, 4 = Red.
        /// </summary>
        private readonly int[] runStatGrade = { -1, -1, -1, -1, -1, -1 }; // indexed by (int)StatType

        /// <summary>현재 런에서 스탯 카드로 누적된 총 보너스 값.</summary>
        private readonly float[] runStatBonusTotals = new float[RunStatCount];

        /// <summary>현재 런에서 각 스탯 카드를 획득한 횟수.</summary>
        private readonly int[] runStatPickupCount = new int[RunStatCount];

        /// <summary>카드 풀에 남은 무기 카드 수. 보스 처치 시 +1, 카드 선택 시 -1.</summary>
        private int weaponCardPoolCount;

        /// <summary>
        /// 현재 런에서 해금된 무기. 인덱스는 (int)WeaponType.
        /// 피스톨(0)은 항상 true, 나머지는 무기 카드 획득 시 true로 전환된다.
        /// </summary>
        private readonly bool[] ownedWeapons = new bool[5];

        // ── 카드 보상 오픈 ──────────────────────────────────────────────

        /// <summary>
        /// 카드 보상 연출을 시작하고, 잠깐의 대기 후 3장의 카드를 화면에 띄운다.
        /// </summary>
        /// <param name="wasBossRoom">방금 클리어한 방이 보스 방이었는지.</param>
        /// <param name="nextFloorIsBoss">다음 층이 보스 층(% 20 == 0)인지.</param>
        private void ShowCardReward(bool wasBossRoom, bool nextFloorIsBoss)
        {
            cardRewardWasBossRoom = wasBossRoom;
            cardRewardNextFloorIsBoss = nextFloorIsBoss;
            currentCardOffers[0] = null;
            currentCardOffers[1] = null;
            currentCardOffers[2] = null;
            pendingCardClickX = -1f;
            pendingCardClickY = -1f;
            cardRewardRevealTimer = CardRewardRevealDelaySeconds;
            cardRewardActive = false;
        }

        private void GenerateCardOffers()
        {
            currentCardOffers[0] = null;
            currentCardOffers[1] = null;
            currentCardOffers[2] = null;

            if (cardRewardWasBossRoom)
            {
                GenerateBossWeaponOffers();
                return;
            }

            // 무기 카드 풀이 있으면 슬롯 2에 무기 카드, 슬롯 0~1에 스탯 카드
            bool hasWeaponCard = false;
            if (weaponCardPoolCount > 0)
            {
                if (GenerateWeaponOffer(out currentCardOffers[2]))
                {
                    hasWeaponCard = true;
                }
                else
                {
                    // 더 이상 제공할 무기 업그레이드가 없음 → 풀 소진 처리
                    weaponCardPoolCount = 0;
                    currentCardOffers[2] = null;
                }
            }

            int statSlots = hasWeaponCard ? 2 : 3;
            HashSet<int> usedStatOfferKeys = new HashSet<int>();

            for (int slot = 0; slot < statSlots; slot++)
            {
                RewardCardOffer offer = GenerateStatOffer(usedStatOfferKeys);
                currentCardOffers[slot] = offer;
                TrackStatOfferKey(offer, usedStatOfferKeys);
            }
        }

        private void GenerateBossWeaponOffers()
        {
            if (TryGenerateWeaponOffers(3, out RewardCardOffer[] offers))
            {
                int slot = 0;
                for (; slot < offers.Length; slot++)
                {
                    currentCardOffers[slot] = offers[slot];
                }

                if (slot >= currentCardOffers.Length)
                {
                    return;
                }

                HashSet<int> usedStatOfferKeys = new HashSet<int>();
                for (; slot < currentCardOffers.Length; slot++)
                {
                    RewardCardOffer offer = GenerateStatOffer(usedStatOfferKeys);
                    currentCardOffers[slot] = offer;
                    TrackStatOfferKey(offer, usedStatOfferKeys);
                }

                return;
            }

            if (weaponCardPoolCount > 0)
            {
                weaponCardPoolCount = 0;
            }

            HashSet<int> fallbackStatOfferKeys = new HashSet<int>();
            for (int slot = 0; slot < currentCardOffers.Length; slot++)
            {
                RewardCardOffer offer = GenerateStatOffer(fallbackStatOfferKeys);
                currentCardOffers[slot] = offer;
                TrackStatOfferKey(offer, fallbackStatOfferKeys);
            }
        }

        private RewardCardOffer GenerateStatOffer(ISet<int> usedStatOfferKeys)
        {
            List<StatType> candidates = BuildAvailableStatOfferPool();
            if (candidates.Count <= 0)
            {
                candidates.Add(StatType.Damage);
            }

            List<CardGrade> availableGrades = BuildAvailableStatOfferGrades(candidates, usedStatOfferKeys);
            CardGrade offerGrade = availableGrades.Count > 0
                ? RollStatOfferGrade(availableGrades)
                : RollStatOfferGrade();

            List<StatType> gradeCandidates = BuildAvailableStatOfferPoolForGrade(candidates, offerGrade, usedStatOfferKeys);
            if (gradeCandidates.Count <= 0)
            {
                gradeCandidates = candidates;
            }

            StatType picked = gradeCandidates[templateRandom.Next(gradeCandidates.Count)];
            float actualBonus = GetEffectiveStatOfferBonus(picked, offerGrade);

            return new RewardCardOffer
            {
                IsWeaponCard = false,
                StatType = picked,
                Grade = offerGrade,
                StatBonusValue = actualBonus
            };
        }

        private static void TrackStatOfferKey(RewardCardOffer offer, ISet<int> usedStatOfferKeys)
        {
            if (offer == null || offer.IsWeaponCard || usedStatOfferKeys == null)
            {
                return;
            }

            usedStatOfferKeys.Add(GetStatOfferKey(offer.StatType, offer.Grade));
        }

        private List<StatType> BuildAvailableStatOfferPool()
        {
            List<StatType> candidates = new List<StatType>(RewardStatTypes.Length);
            for (int i = 0; i < RewardStatTypes.Length; i++)
            {
                StatType stat = RewardStatTypes[i];
                if (IsStatOfferAvailable(stat))
                {
                    candidates.Add(stat);
                }
            }

            return candidates;
        }

        private static int GetStatOfferKey(StatType stat, CardGrade grade)
        {
            return (((int)stat) << 3) | (int)grade;
        }

        private static List<CardGrade> BuildAvailableStatOfferGrades(
            List<StatType> stats,
            ISet<int> usedStatOfferKeys)
        {
            List<CardGrade> grades = new List<CardGrade>(5);
            for (int g = 0; g <= (int)CardGrade.Red; g++)
            {
                CardGrade grade = (CardGrade)g;
                for (int i = 0; i < stats.Count; i++)
                {
                    int key = GetStatOfferKey(stats[i], grade);
                    if (usedStatOfferKeys == null || !usedStatOfferKeys.Contains(key))
                    {
                        grades.Add(grade);
                        break;
                    }
                }
            }

            return grades;
        }

        private static List<StatType> BuildAvailableStatOfferPoolForGrade(
            List<StatType> stats,
            CardGrade grade,
            ISet<int> usedStatOfferKeys)
        {
            List<StatType> filtered = new List<StatType>(stats.Count);
            for (int i = 0; i < stats.Count; i++)
            {
                int key = GetStatOfferKey(stats[i], grade);
                if (usedStatOfferKeys != null && usedStatOfferKeys.Contains(key))
                {
                    continue;
                }

                filtered.Add(stats[i]);
            }

            return filtered;
        }

        private bool IsStatOfferAvailable(StatType stat)
        {
            const float epsilon = 0.0001f;
            return GetRemainingStatBonusCap(stat) > epsilon;
        }

        private float GetEffectiveStatOfferBonus(StatType stat, CardGrade grade)
        {
            float baseBonus = GetBaseStatCardBonus(grade);
            float remaining = GetRemainingStatBonusCap(stat);
            return Math.Max(0f, Math.Min(baseBonus, remaining));
        }

        private float GetRemainingStatBonusCap(StatType stat)
        {
            float maxBonus = GetStatBonusCap(stat);
            if (float.IsPositiveInfinity(maxBonus))
            {
                return float.MaxValue;
            }

            return Math.Max(0f, maxBonus - runStatBonusTotals[(int)stat]);
        }

        private float GetStatBonusCap(StatType stat)
        {
            switch (stat)
            {
                case StatType.DashCooldown:
                    return 1f - (GameConfig.DashCooldownMinDuration / GameConfig.DashCooldownDuration);
                case StatType.AmmoDropChance:
                    return 1f - NormalEnemyAmmoDropChance;
                case StatType.CoinDropChance:
                    return 0.50f;
                default:
                    return float.PositiveInfinity;
            }
        }

        private static float GetBaseStatCardBonus(CardGrade grade)
        {
            return CardGradeHelper.GetBonusValue(grade);
        }

        // 운 레벨(0~10)별 등급 가중치 테이블. [luckLevel, (int)CardGrade]
        // 열: White=0, Green=1, Blue=2, Purple=3, Red=4
        private static readonly int[,] LuckGradeWeights = new int[11, 5]
        {
            // White  Green  Blue  Purple  Red
            {  100,    0,    0,     0,    0 }, // Lv.0
            {  100,    0,    0,     0,    0 }, // Lv.1
            {  100,    0,    0,     0,    0 }, // Lv.2
            {   75,   25,    0,     0,    0 }, // Lv.3
            {   55,   30,   15,     0,    0 }, // Lv.4
            {   45,   33,   20,     2,    0 }, // Lv.5
            {   30,   40,   25,     5,    0 }, // Lv.6
            {   16,   30,   43,    10,    1 }, // Lv.7
            {   15,   20,   32,    30,    3 }, // Lv.8
            {   10,   17,   25,    33,   15 }, // Lv.9
            {    5,   10,   20,    40,   25 }, // Lv.10
        };

        private CardGrade RollStatOfferGrade()
        {
            return RollStatOfferGrade(null);
        }

        private CardGrade RollStatOfferGrade(ICollection<CardGrade> allowedGrades)
        {
            PermanentProgressionData data = permanentProgression ?? PermanentProgressionData.CreateDefault();
            int luckLevel = Math.Max(0, Math.Min(10, data.GetLuckLevel()));

            // White(0)부터 Red(4)까지 Luck 테이블 전체 범위로 순수 확률 롤
            int total = 0;
            for (int g = 0; g <= (int)CardGrade.Red; g++)
            {
                CardGrade grade = (CardGrade)g;
                if (allowedGrades != null && !allowedGrades.Contains(grade))
                {
                    continue;
                }

                total += LuckGradeWeights[luckLevel, g];
            }

            if (total <= 0)
                return CardGrade.White;

            int roll = rewardRandom.Next(total);
            int cumulative = 0;
            for (int g = 0; g <= (int)CardGrade.Red; g++)
            {
                CardGrade grade = (CardGrade)g;
                if (allowedGrades != null && !allowedGrades.Contains(grade))
                {
                    continue;
                }

                cumulative += LuckGradeWeights[luckLevel, g];
                if (roll < cumulative)
                    return grade;
            }

            return CardGrade.Red;
        }

        private bool GenerateWeaponOffer(out RewardCardOffer offer)
        {
            offer = null;
            if (!TryGenerateWeaponOffers(1, out RewardCardOffer[] offers))
            {
                return false;
            }

            offer = offers[0];
            return true;
        }

        private bool TryGenerateWeaponOffers(int desiredCount, out RewardCardOffer[] offers)
        {
            offers = null;
            if (desiredCount <= 0)
            {
                return false;
            }

            List<RewardCardOffer> candidates = BuildWeaponOfferCandidates();
            if (candidates.Count == 0)
            {
                return false;
            }

            int uniqueCount = Math.Min(desiredCount, candidates.Count);
            offers = new RewardCardOffer[uniqueCount];
            for (int i = 0; i < uniqueCount; i++)
            {
                int pickIndex = templateRandom.Next(candidates.Count);
                offers[i] = CloneWeaponOffer(candidates[pickIndex]);
                candidates.RemoveAt(pickIndex);
            }

            return true;
        }

        private List<RewardCardOffer> BuildWeaponOfferCandidates()
        {
            WeaponType[] upgradeable =
            {
                WeaponType.BearKiller, WeaponType.HChainGun,
                WeaponType.AutoCannon, WeaponType.DuelBerettas
            };

            List<RewardCardOffer> candidates = new List<RewardCardOffer>();

            foreach (WeaponType wt in upgradeable)
            {
                WeaponUpgradeCategory[] cats = GetUpgradeCategoriesForWeapon(wt);

                foreach (WeaponUpgradeCategory cat in cats)
                {
                    int level = weapon.GetUpgradeLevel(wt, cat);
                    if (level <= 0)
                    {
                        candidates.Add(new RewardCardOffer
                        {
                            IsWeaponCard = true,
                            WeaponType = wt,
                            WeaponCategory = cat,
                            WeaponGrade = CardGrade.Blue
                        });
                    }
                    else if (level == (int)CardGrade.Blue)
                    {
                        candidates.Add(new RewardCardOffer
                        {
                            IsWeaponCard = true,
                            WeaponType = wt,
                            WeaponCategory = cat,
                            WeaponGrade = CardGrade.Purple
                        });
                    }
                }

                if (!weapon.HasSpecialUpgrade(wt) && HasAnyPurpleUpgrade(wt))
                {
                    candidates.Add(new RewardCardOffer
                    {
                        IsWeaponCard = true,
                        WeaponType = wt,
                        WeaponCategory = WeaponUpgradeCategory.Special,
                        WeaponGrade = CardGrade.Red
                    });
                }
            }

            return candidates;
        }

        private static RewardCardOffer CloneWeaponOffer(RewardCardOffer source)
        {
            if (source == null)
            {
                return null;
            }

            return new RewardCardOffer
            {
                IsWeaponCard = true,
                WeaponType = source.WeaponType,
                WeaponCategory = source.WeaponCategory,
                WeaponGrade = source.WeaponGrade
            };
        }

        private bool HasAnyPurpleUpgrade(WeaponType type)
        {
            WeaponUpgradeCategory[] cats = GetUpgradeCategoriesForWeapon(type);
            for (int i = 0; i < cats.Length; i++)
            {
                if (weapon.GetUpgradeLevel(type, cats[i]) >= (int)CardGrade.Purple)
                {
                    return true;
                }
            }

            return false;
        }

        private static WeaponUpgradeCategory[] GetUpgradeCategoriesForWeapon(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.BearKiller:
                    return new[] { WeaponUpgradeCategory.Damage, WeaponUpgradeCategory.Range, WeaponUpgradeCategory.Pellets };
                case WeaponType.HChainGun:
                    return new[] { WeaponUpgradeCategory.Damage, WeaponUpgradeCategory.FireRate, WeaponUpgradeCategory.Spread };
                case WeaponType.AutoCannon:
                    return new[] { WeaponUpgradeCategory.Damage, WeaponUpgradeCategory.Splash, WeaponUpgradeCategory.AmmoDropBonus };
                case WeaponType.DuelBerettas:
                    return new[] { WeaponUpgradeCategory.Damage, WeaponUpgradeCategory.Range, WeaponUpgradeCategory.FireRate };
                default:
                    return Array.Empty<WeaponUpgradeCategory>();
            }
        }

        // ── 카드 입력 처리 ──────────────────────────────────────────────

        /// <summary>
        /// 룸 클리어 후 대기 시간이 끝나면 카드 제안 3장을 생성하고 카드 UI를 연다.
        /// </summary>
        private void UpdatePendingCardRewardReveal(float dt)
        {
            if (cardRewardRevealTimer <= 0f)
            {
                return;
            }

            cardRewardRevealTimer = Math.Max(0f, cardRewardRevealTimer - Math.Max(0f, dt));
            if (cardRewardRevealTimer > 0f)
            {
                return;
            }

            weapon.PendingShot = false;
            weapon.FireButtonHeld = false;
            weapon.PendingChargeFire = false;
            weapon.CancelCharge();
            StopLoopingWeaponEffects();
            interactPromptText = null;

            GenerateCardOffers();
            cardRewardActive = true;
        }

        /// <summary>카드 보상 UI가 활성일 때 마우스 클릭 좌표(게임 해상도 기준)로 카드를 선택한다.</summary>
        private void HandleCardInput()
        {
            if (!cardRewardActive) return;
            if (pendingCardClickX < 0f) return;

            float clickX = pendingCardClickX;
            float clickY = pendingCardClickY;
            pendingCardClickX = -1f;
            pendingCardClickY = -1f;

            const float cardW = 170f;
            const float cardH = 210f;
            const float gap   = 15f;
            const float totalW = 3f * cardW + 2f * gap;
            float fw = GameConfig.GpuWorldMaxRenderWidth;
            float fh = GameConfig.GpuWorldMaxRenderHeight;
            float startX = (fw - totalW) * 0.5f;
            float cardY  = cardRewardWasBossRoom ? fh * 0.20f : fh * 0.16f;

            for (int i = 0; i < 3; i++)
            {
                if (currentCardOffers[i] == null) continue;
                float cx = startX + i * (cardW + gap);
                if (clickX >= cx && clickX < cx + cardW &&
                    clickY >= cardY && clickY < cardY + cardH)
                {
                    ConfirmCardSelection(i);
                    return;
                }
            }
        }

        /// <summary>
        /// Form1에서 마우스 클릭 이벤트를 게임 좌표로 변환해 전달한다.
        /// 카드 보상 UI가 활성인 경우에만 다음 프레임에 처리된다.
        /// </summary>
        public void NotifyCardMouseClick(float gameX, float gameY)
        {
            pendingCardClickX = gameX;
            pendingCardClickY = gameY;
        }

        /// <summary>카드 보상 UI가 현재 열려 있는지 여부 (Form1에서 마우스 캡처 해제 판단용).</summary>
        public bool CardRewardActive => cardRewardActive;

        /// <summary>룸 클리어 후 카드 UI가 아직 나타나지 않았지만 곧 표시될 대기 상태인지 여부.</summary>
        private bool CardRewardRevealPending => cardRewardRevealTimer > 0f;

        /// <summary>마우스 자유 입력이 필요한 오버레이 UI가 열려 있는지 여부.</summary>
        public bool MouseSelectableOverlayActive => cardRewardActive || branchSelectionActive || permanentStatsUiActive;

        /// <summary>카드 선택 대기 중 화면 상단 중앙에 스테이지 클리어 배너를 그린다.</summary>
        private void DrawPendingCardRewardReveal(Renderer r)
        {
            if (!CardRewardRevealPending || cardRewardActive)
            {
                return;
            }

            float fw = GameConfig.GpuWorldMaxRenderWidth;
            float fh = GameConfig.GpuWorldMaxRenderHeight;
            float panelW = 300f;
            float panelH = 56f;
            float panelX = (fw - panelW) * 0.5f;
            float panelY = fh * 0.17f;

            r.DrawRectangle(panelX, panelY, panelW, panelH, Color.FromArgb(170, 0, 0, 0));
            r.DrawRectangle(panelX, panelY, panelW, 1f, Color.FromArgb(210, 255, 210, 125));
            r.DrawRectangle(panelX, panelY + panelH - 1f, panelW, 1f, Color.FromArgb(210, 255, 210, 125));
            r.DrawTextCenteredShadow("스테이지 클리어", fw * 0.5f, panelY + panelH * 0.5f,
                Color.FromArgb(255, 255, 220, 130), 20f);
        }

        private void ConfirmCardSelection(int index)
        {
            RewardCardOffer offer = currentCardOffers[index];
            if (offer == null) return;

            ApplyCardOffer(offer);

            cardRewardActive = false;
            currentCardOffers[0] = null;
            currentCardOffers[1] = null;
            currentCardOffers[2] = null;
            pendingCardClickX = -1f;
            pendingCardClickY = -1f;

            // 카드 선택 후 분기 처리
            if (!cardRewardWasBossRoom && !cardRewardNextFloorIsBoss)
            {
                // 일반 룸 클리어 → 분기 선택
                ShowBranchSelection();
            }
            else if (!cardRewardWasBossRoom && cardRewardNextFloorIsBoss)
            {
                // 다음 층이 보스 → E키로 강제 진입
                SetStageStatus("[E] 다음 층으로 (보스)", 4f);
            }
            else
            {
                // 보스 클리어 후 → E키로 진행
                SetStageStatus("[E] 다음 층으로", 4f);
            }
        }

        private void ApplyCardOffer(RewardCardOffer offer)
        {
            if (offer.IsWeaponCard)
            {
                weapon.SetUpgrade(offer.WeaponType, offer.WeaponCategory, offer.WeaponGrade);
                weaponCardPoolCount = Math.Max(0, weaponCardPoolCount - 1);
                // 처음 카드를 받은 무기는 즉시 해금
                ownedWeapons[(int)offer.WeaponType] = true;
            }
            else
            {
                ApplyStatCard(offer);
            }

            if (offer.IsWeaponCard)
            {
                SetStageStatus(BuildWeaponCardMessage(offer), 3f);
            }
        }

        private void ApplyStatCard(RewardCardOffer offer)
        {
            if (offer == null)
            {
                return;
            }

            StatType stat = offer.StatType;
            CardGrade grade = offer.Grade;
            int statIndex = (int)stat;
            float currentTotal = runStatBonusTotals[statIndex];
            float bonus = offer.StatBonusValue > 0f
                ? offer.StatBonusValue
                : GetEffectiveStatOfferBonus(stat, grade);
            float nextTotal = currentTotal + bonus;
            float cappedTotal = GetStatBonusCap(stat);
            if (!float.IsPositiveInfinity(cappedTotal))
            {
                nextTotal = Math.Min(cappedTotal, nextTotal);
            }

            float actualBonus = Math.Max(0f, nextTotal - currentTotal);
            if (actualBonus <= 0f)
            {
                return;
            }

            runStatGrade[statIndex] = Math.Max(runStatGrade[statIndex], (int)grade);
            runStatBonusTotals[statIndex] = nextTotal;
            runStatPickupCount[statIndex]++;

            ApplyCombinedProgressionStats(refillHealth: false, healMaxHealthDelta: stat == StatType.MaxHealth);
            SetStageStatus(BuildStatCardMessage(stat, grade, actualBonus), 3f);
        }

        // ── 카드 상태 리셋 ──────────────────────────────────────────────

        /// <summary>카드 UI 표시 상태만 초기화한다 (층 전환 시). 런 카드 등급은 유지.</summary>
        private void ResetCardDisplayState()
        {
            cardRewardActive = false;
            cardRewardRevealTimer = 0f;
            cardRewardWasBossRoom = false;
            cardRewardNextFloorIsBoss = false;
            currentCardOffers[0] = null;
            currentCardOffers[1] = null;
            currentCardOffers[2] = null;
            pendingCardClickX = -1f;
            pendingCardClickY = -1f;
        }

        /// <summary>런 전체 카드 상태를 초기화한다 (새 런 시작 시).</summary>
        private void ResetCardRunState()
        {
            ResetCardDisplayState();
            weaponCardPoolCount = 0;
            for (int i = 0; i < runStatGrade.Length; i++)
            {
                runStatGrade[i] = -1;
                runStatBonusTotals[i] = 0f;
                runStatPickupCount[i] = 0;
            }
            // 피스톨만 보유 상태로 초기화
            ownedWeapons[0] = true;
            for (int i = 1; i < ownedWeapons.Length; i++)
                ownedWeapons[i] = false;
        }

        // ── 카드 설명 문자열 ────────────────────────────────────────────

        private string BuildStatCardMessage(StatType stat, CardGrade grade, float actualBonus)
        {
            string gradeName = CardGradeHelper.GetGradeName(grade);
            string statName = GetStatName(stat);
            return $"[{gradeName}] {statName} {GetStatCardValueText(stat, actualBonus)} 획득";
        }

        private string BuildWeaponCardMessage(RewardCardOffer offer)
        {
            string gradeName = CardGradeHelper.GetGradeName(offer.WeaponGrade);
            string weaponName = GetWeaponName(offer.WeaponType);
            string catName = GetCategoryName(offer.WeaponCategory);
            if (offer.WeaponCategory == WeaponUpgradeCategory.Special)
            {
                return $"[{gradeName}] {weaponName} 특수기 해금";
            }

            return $"[{gradeName}] {weaponName} {catName} 업그레이드";
        }

        private static string GetStatName(StatType stat)
        {
            switch (stat)
            {
                case StatType.MaxHealth:   return "최대 체력";
                case StatType.MoveSpeed:   return "이동 속도";
                case StatType.DashCooldown:return "대시 쿨타임";
                case StatType.AmmoDropChance: return "탄 드랍 확률";
                case StatType.Damage:      return "공격력";
                case StatType.CoinDropChance: return "코인 드랍 확률";
                default:                   return "???";
            }
        }

        private static string GetWeaponName(WeaponType type)
        {
            return WeaponPresentation.GetDisplayName(type);
        }

        private static string GetCategoryName(WeaponUpgradeCategory cat)
        {
            switch (cat)
            {
                case WeaponUpgradeCategory.Damage:      return "피해";
                case WeaponUpgradeCategory.Range:       return "사거리";
                case WeaponUpgradeCategory.Pellets:     return "탄환 수";
                case WeaponUpgradeCategory.FireRate:    return "발사 속도";
                case WeaponUpgradeCategory.Spread:      return "탄 퍼짐";
                case WeaponUpgradeCategory.Splash:      return "폭발 범위";
                case WeaponUpgradeCategory.AmmoDropBonus: return "탄 드랍";
                case WeaponUpgradeCategory.Special:     return "특수기";
                default:                                return "???";
            }
        }

        private static string GetWeaponUpgradeDesc(WeaponType type, WeaponUpgradeCategory cat, CardGrade grade)
        {
            if (cat == WeaponUpgradeCategory.Special)
            {
                switch (type)
                {
                    case WeaponType.BearKiller:        return "갈고리 - 적 끌어당기기 + 기절";
                    case WeaponType.HChainGun:            return "버스트 - 전탄 무재장전 연사";
                    case WeaponType.AutoCannon: return "집속 포격 - 넓은 범위 고폭탄";
                    case WeaponType.DuelBerettas:      return "피버 모드 - 전방 자동 제압";
                    default:                        return "특수기";
                }
            }

            bool isBlue = grade == CardGrade.Blue;
            switch (cat)
            {
                case WeaponUpgradeCategory.Damage:      return isBlue ? "+30% 피해" : "+60% 피해";
                case WeaponUpgradeCategory.Range:       return isBlue ? "+30% 사거리" : "+60% 사거리";
                case WeaponUpgradeCategory.Pellets:     return isBlue ? "+2 탄환" : "+4 탄환";
                case WeaponUpgradeCategory.FireRate:    return isBlue ? "-20% 쿨타임" : "-35% 쿨타임";
                case WeaponUpgradeCategory.Spread:      return isBlue ? "-30% 퍼짐" : "-55% 퍼짐";
                case WeaponUpgradeCategory.Splash:      return isBlue ? "+30% 폭발" : "+55% 폭발";
                case WeaponUpgradeCategory.AmmoDropBonus: return isBlue ? "+25% 드랍확률" : "+45% 드랍확률";
                default:                                return "???";
            }
        }

        private static string GetStatCardValueText(StatType stat, float bonusValue)
        {
            int pct = (int)Math.Round(bonusValue * 100f);
            switch (stat)
            {
                case StatType.DashCooldown:
                    return $"-{pct}%";
                default:
                    return $"+{pct}%";
            }
        }

        // ── 카드 UI 렌더링 ──────────────────────────────────────────────

        /// <summary>카드 보상 선택 UI를 화면에 그린다.</summary>
        private void DrawCardRewardUI(Renderer r)
        {
            if (!cardRewardActive) return;

            float fw = GameConfig.GpuWorldMaxRenderWidth;
            float fh = GameConfig.GpuWorldMaxRenderHeight;

            // 반투명 어둠 처리
            r.DrawRectangle(0f, 0f, fw, fh, Color.FromArgb(185, 0, 0, 0));

            r.DrawTextCenteredShadow("[ 카드 선택 ]", fw * 0.5f, fh * 0.08f,
                Color.FromArgb(255, 255, 235, 150), 14f);

            if (cardRewardWasBossRoom)
            {
                r.DrawTextCenteredShadow("보스 보상: 영구 스탯 포인트 +1 획득", fw * 0.5f, fh * 0.12f,
                    Color.FromArgb(255, 140, 220, 155), 9.5f);
            }

            float cardW = 170f;
            float cardH = 210f;
            float gap = 15f;
            float totalW = 3f * cardW + 2f * gap;
            float startX = (fw - totalW) * 0.5f;
            float cardY = cardRewardWasBossRoom ? fh * 0.20f : fh * 0.16f;

            for (int i = 0; i < 3; i++)
            {
                RewardCardOffer offer = currentCardOffers[i];
                if (offer == null) continue;
                float cx = startX + i * (cardW + gap);
                DrawRewardCard(r, offer, cx, cardY, cardW, cardH);
            }
        }

        private void DrawRewardCard(Renderer r, RewardCardOffer offer,
            float x, float y, float w, float h)
        {
            Color gradeColor = offer.IsWeaponCard
                ? CardGradeHelper.GetGradeColor(offer.WeaponGrade)
                : CardGradeHelper.GetGradeColor(offer.Grade);

            // 배경
            r.DrawRectangle(x, y, w, h, Color.FromArgb(215, 20, 28, 42));

            // 테두리 4변
            r.DrawRectangle(x,         y,         w, 1f, gradeColor);
            r.DrawRectangle(x,         y + h - 1, w, 1f, gradeColor);
            r.DrawRectangle(x,         y,         1f, h,  gradeColor);
            r.DrawRectangle(x + w - 1, y,         1f, h,  gradeColor);

            float cx = x + w * 0.5f;

            if (offer.IsWeaponCard)
                DrawWeaponCardContent(r, offer, cx, y, w, h);
            else
                DrawStatCardContent(r, offer, cx, y, w, h);

            // 하단 구분선 + 클릭 힌트
            r.DrawRectangle(x + 6f, y + h - 30f, w - 12f, 1f,
                Color.FromArgb(100, 200, 200, 200));
            r.DrawTextCenteredShadow("클릭하여 선택", cx, y + h - 15f,
                Color.FromArgb(255, 255, 225, 100), 10f);
        }

        private void DrawStatCardContent(Renderer r, RewardCardOffer offer,
            float cx, float y, float w, float h)
        {
            Color gradeColor = CardGradeHelper.GetGradeColor(offer.Grade);
            string gradeName = CardGradeHelper.GetGradeName(offer.Grade);
            string statName = GetStatName(offer.StatType);
            string valueText = GetStatCardValueText(offer.StatType, offer.StatBonusValue);

            r.DrawTextCenteredShadow("스탯 카드", cx, y + 16f,
                Color.FromArgb(200, 170, 170, 170), 9f);
            r.DrawTextCenteredShadow(gradeName, cx, y + 40f, gradeColor, 13f);
            r.DrawRectangle(cx - w * 0.35f, y + 57f, w * 0.7f, 1f,
                Color.FromArgb(80, 200, 200, 200));
            r.DrawTextCenteredShadow(statName, cx, y + 80f,
                Color.FromArgb(255, 235, 225, 200), 12f);
            r.DrawTextCenteredShadow(valueText, cx, y + 108f, gradeColor, 15f);

            // 현재 보유 등급 표시
            int statIndex = (int)offer.StatType;
            int cur = runStatGrade[statIndex];
            int pickupCount = runStatPickupCount[statIndex];
            string owned = pickupCount <= 0
                ? "누적: 없음"
                : $"누적 {GetStatBonusTotalText(offer.StatType, runStatBonusTotals[statIndex])} / {pickupCount}회";
            Color ownedColor = cur < 0
                ? Color.FromArgb(170, 155, 155, 155)
                : CardGradeHelper.GetGradeColor((CardGrade)cur);
            r.DrawTextCenteredShadow(owned, cx, y + 148f, ownedColor, 9f);
        }

        private static string GetStatBonusTotalText(StatType stat, float totalBonus)
        {
            int pct = (int)Math.Round(totalBonus * 100f);
            switch (stat)
            {
                case StatType.DashCooldown:
                    return $"-{pct}%";
                default:
                    return $"+{pct}%";
            }
        }

        private void DrawWeaponCardContent(Renderer r, RewardCardOffer offer,
            float cx, float y, float w, float h)
        {
            Color gradeColor = CardGradeHelper.GetGradeColor(offer.WeaponGrade);
            string gradeName = CardGradeHelper.GetGradeName(offer.WeaponGrade);
            string weaponName = GetWeaponName(offer.WeaponType);
            string catName = GetCategoryName(offer.WeaponCategory);
            string effectDesc = GetWeaponUpgradeDesc(offer.WeaponType, offer.WeaponCategory, offer.WeaponGrade);

            bool isNewWeapon = !ownedWeapons[(int)offer.WeaponType];
            string headerText = isNewWeapon ? "신규 무기 해금" : "무기 업그레이드";
            Color headerColor = isNewWeapon
                ? Color.FromArgb(200, 255, 220, 100)
                : Color.FromArgb(200, 170, 170, 170);
            r.DrawTextCenteredShadow(headerText, cx, y + 16f, headerColor, 9f);
            r.DrawTextCenteredShadow(gradeName, cx, y + 40f, gradeColor, 13f);
            r.DrawRectangle(cx - w * 0.35f, y + 57f, w * 0.7f, 1f,
                Color.FromArgb(80, 200, 200, 200));
            r.DrawTextCenteredShadow(weaponName, cx, y + 80f,
                Color.FromArgb(255, 235, 225, 200), 12f);
            r.DrawTextCenteredShadow(catName, cx, y + 104f, gradeColor, 12f);
            r.DrawTextCenteredShadow(effectDesc, cx, y + 148f,
                Color.FromArgb(200, 210, 210, 210), 9f);
        }
    }
}
