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

        /// <summary>카드/분기 패널 배경에 사용하는 9-slice 프레임 텍스처. null이면 단색으로 그린다.</summary>
        private Image uiPanelFrame;

        /// <summary>앱 레이어에서 로드한 패널 프레임 텍스처를 주입한다.</summary>
        public void SetUiPanelFrame(Image frame)
        {
            uiPanelFrame = frame;
        }

        /// <summary>
        /// 오버레이 UI(영구 스탯, 인게임 정보, 카드 카드 슬롯)의 공통 패널 배경을 그린다.
        /// 패널 프레임이 있으면 9-slice 금속 틀을, 없으면 단색 배경 + 1px 강조 테두리를 그린다.
        /// 강조 색 테두리는 두 경로 모두 위에 유지해 화면별 정체성(파랑/금색 등)을 보존한다.
        /// </summary>
        /// <param name="fill">프레임이 없을 때 사용할 단색 배경 색.</param>
        /// <param name="accent">패널 외곽 강조 테두리 색.</param>
        private void DrawUiOverlayPanel(Renderer r, float x, float y, float w, float h, Color fill, Color accent)
        {
            if (uiPanelFrame != null)
            {
                float db = Math.Min(w, h) * 0.16f;
                r.DrawImageNineSlice(uiPanelFrame, x, y, w, h, uiPanelFrame.Width * 0.16f, db);
                float t = Math.Max(2f, h * 0.008f);
                r.DrawRectangle(x, y, w, t, accent);
                r.DrawRectangle(x, y + h - t, w, t, accent);
                r.DrawRectangle(x, y, t, h, accent);
                r.DrawRectangle(x + w - t, y, t, h, accent);
            }
            else
            {
                r.DrawRectangle(x, y, w, h, fill);
                r.DrawRectangle(x, y, w, 1f, accent);
                r.DrawRectangle(x, y + h - 1f, w, 1f, accent);
                r.DrawRectangle(x, y, 1f, h, accent);
                r.DrawRectangle(x + w - 1f, y, 1f, h, accent);
            }
        }

        /// <summary>
        /// 카드/분기 패널 배경을 그린다. 패널 프레임이 있으면 9-slice 금속 틀을 쓰되
        /// 등급 색 테두리는 그 위에 유지해 카드 등급 정보가 사라지지 않게 한다.
        /// </summary>
        private void DrawCardPanelBackground(Renderer r, float x, float y, float w, float h, Color gradeColor)
        {
            if (uiPanelFrame != null)
            {
                float db = Math.Min(w, h) * 0.20f;
                r.DrawImageNineSlice(uiPanelFrame, x, y, w, h, uiPanelFrame.Width * 0.16f, db);
                float t = Math.Max(2f, h * 0.012f);
                r.DrawRectangle(x, y, w, t, gradeColor);
                r.DrawRectangle(x, y + h - t, w, t, gradeColor);
                r.DrawRectangle(x, y, t, h, gradeColor);
                r.DrawRectangle(x + w - t, y, t, h, gradeColor);
            }
            else
            {
                r.DrawRectangle(x, y, w, h, Color.FromArgb(215, 20, 28, 42));
                r.DrawRectangle(x, y, w, 1f, gradeColor);
                r.DrawRectangle(x, y + h - 1, w, 1f, gradeColor);
                r.DrawRectangle(x, y, 1f, h, gradeColor);
                r.DrawRectangle(x + w - 1, y, 1f, h, gradeColor);
            }
        }

        /// <summary>런 스탯 배열 크기. StatType enum 값과 1:1로 맞춰 인덱싱한다.</summary>
        private static readonly int RunStatCount = Enum.GetValues(typeof(StatType)).Length;

        /// <summary>기본 카드 보상 선택지 수.</summary>
        private const int BaseCardRewardOfferCount = 3;

        /// <summary>카드 선택지 증가 스탯까지 반영한 최대 선택지 수.</summary>
        private const int MaxCardRewardOfferCount = 4;

        /// <summary>카드 선택지 증가 카드가 등장할 수 있는 최소 층.</summary>
        private const int CardChoiceBonusMinFloor = 10;

        /// <summary>일반 스탯 카드 후보 풀. 정의는 <see cref="StatCardCatalog"/>에 있다.</summary>
        private static readonly StatType[] RewardStatTypes = StatCardCatalog.RewardPool;

        // ── 카드 보상 상태 ──────────────────────────────────────────────

        /// <summary>카드 선택 UI가 현재 활성화되어 있는지 여부.</summary>
        private bool cardRewardActive;

        /// <summary>룸 클리어 후 카드 보상 UI를 실제로 띄우기까지 남은 대기 시간(초).</summary>
        private float cardRewardRevealTimer;

        /// <summary>카드 UI를 연 방이 보스 방이었는지 여부. 완료 후 분기 처리에 사용.</summary>
        private bool cardRewardWasBossRoom;

        /// <summary>카드 UI를 연 후 다음 층이 보스인지 여부. 완료 후 분기/E키 처리에 사용.</summary>
        private bool cardRewardNextFloorIsBoss;

        /// <summary>이번 카드 보상에서 스탯 카드 등급을 올릴 단계 수.</summary>
        private int cardRewardGradeBoost;

        /// <summary>이번 카드 보상에서 보장할 최소 스탯 카드 등급. -1이면 보장 없음.</summary>
        private int cardRewardMinimumStatGrade = -1;

        /// <summary>현재 제시 중인 카드. 카드 선택지 증가 스탯이 있으면 4번째 슬롯까지 사용한다.</summary>
        private readonly RewardCardOffer[] currentCardOffers = new RewardCardOffer[MaxCardRewardOfferCount];

        /// <summary>마우스 클릭으로 카드 선택 시 처리할 게임 좌표 X (-1이면 미처리).</summary>
        private float pendingCardClickX = -1f;
        /// <summary>마우스 클릭으로 카드 선택 시 처리할 게임 좌표 Y.</summary>
        private float pendingCardClickY = -1f;

        /// <summary>
        /// 현재 런에서 각 StatType에 보유 중인 카드 등급.
        /// -1 = 미보유, 0 = White, 1 = Green, 2 = Blue, 3 = Purple, 4 = Red.
        /// </summary>
        private readonly int[] runStatGrade = CreateInitialRunStatGradeState(); // indexed by (int)StatType

        /// <summary>현재 런에서 스탯 카드로 누적된 총 보너스 값.</summary>
        private readonly float[] runStatBonusTotals = new float[RunStatCount];

        /// <summary>현재 런에서 각 스탯 카드를 획득한 횟수.</summary>
        private readonly int[] runStatPickupCount = new int[RunStatCount];

        /// <summary>카드 풀에 남은 무기 카드 수. 보스 처치 시 +1, 카드 선택 시 -1.</summary>
        private int weaponCardPoolCount;

        /// <summary>카드 선택지 증가 카드는 한 번 제시되면 다시 등장하지 않는다.</summary>
        private bool cardChoiceBonusOffered;

        /// <summary>
        /// 현재 런에서 해금된 무기. 인덱스는 (int)WeaponType.
        /// 피스톨(0)은 항상 true, 나머지는 무기 카드 획득 시 true로 전환된다.
        /// </summary>
        private readonly bool[] ownedWeapons = new bool[5];

        private static int[] CreateInitialRunStatGradeState()
        {
            int[] values = new int[RunStatCount];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = -1;
            }

            return values;
        }

        private void ClearCurrentCardOffers()
        {
            for (int i = 0; i < currentCardOffers.Length; i++)
            {
                currentCardOffers[i] = null;
            }
        }

        private int GetCardRewardOfferSlotCount()
        {
            int bonus = GetRunStatBonus(StatType.CardChoiceBonus) >= 1f ? 1 : 0;
            return Math.Max(BaseCardRewardOfferCount, Math.Min(MaxCardRewardOfferCount, BaseCardRewardOfferCount + bonus));
        }

        // ── 카드 보상 오픈 ──────────────────────────────────────────────

        /// <summary>
        /// 카드 보상 연출을 시작하고, 잠깐의 대기 후 3장의 카드를 화면에 띄운다.
        /// </summary>
        /// <param name="wasBossRoom">방금 클리어한 방이 보스 방이었는지.</param>
        /// <param name="nextFloorIsBoss">다음 층이 보스 층(% 20 == 0)인지.</param>
        private void ShowCardReward(bool wasBossRoom, bool nextFloorIsBoss, int gradeBoost = 0, int minimumStatGrade = -1)
        {
            cardRewardWasBossRoom = wasBossRoom;
            cardRewardNextFloorIsBoss = nextFloorIsBoss;
            cardRewardGradeBoost = Math.Max(0, gradeBoost);
            cardRewardMinimumStatGrade = minimumStatGrade < 0
                ? -1
                : Math.Max((int)CardGrade.White, Math.Min((int)CardGrade.Red, minimumStatGrade));
            ClearCurrentCardOffers();
            pendingCardClickX = -1f;
            pendingCardClickY = -1f;
            cardRewardRevealTimer = CardRewardRevealDelaySeconds;
            cardRewardActive = false;
        }

        private void GenerateCardOffers()
        {
            ClearCurrentCardOffers();
            int offerSlotCount = GetCardRewardOfferSlotCount();

            // 보스 보상은 무기 업그레이드가 우선이다.
            // 더 이상 줄 무기 카드가 없으면 같은 슬롯 수를 스탯 카드로 채운다.
            if (cardRewardWasBossRoom)
            {
                GenerateBossWeaponOffers(offerSlotCount);
                return;
            }

            // 무기 카드 풀이 있으면 마지막 슬롯에 무기 카드, 앞 슬롯에 스탯 카드
            bool hasWeaponCard = false;
            if (weaponCardPoolCount > 0)
            {
                int weaponSlot = offerSlotCount - 1;
                if (GenerateWeaponOffer(out currentCardOffers[weaponSlot]))
                {
                    hasWeaponCard = true;
                }
                else
                {
                    // 더 이상 제공할 무기 업그레이드가 없음 → 풀 소진 처리
                    weaponCardPoolCount = 0;
                    currentCardOffers[weaponSlot] = null;
                }
            }

            int statSlots = hasWeaponCard ? offerSlotCount - 1 : offerSlotCount;
            HashSet<int> usedStatOfferKeys = new HashSet<int>();

            for (int slot = 0; slot < statSlots; slot++)
            {
                RewardCardOffer offer = GenerateStatOffer(usedStatOfferKeys);
                currentCardOffers[slot] = offer;
                TrackStatOfferKey(offer, usedStatOfferKeys);
            }
        }

        private void GenerateBossWeaponOffers(int offerSlotCount)
        {
            if (TryGenerateWeaponOffers(offerSlotCount, out RewardCardOffer[] offers))
            {
                int slot = 0;
                for (; slot < offers.Length; slot++)
                {
                    currentCardOffers[slot] = offers[slot];
                }

                if (slot >= offerSlotCount)
                {
                    return;
                }

                HashSet<int> usedStatOfferKeys = new HashSet<int>();
                for (; slot < offerSlotCount; slot++)
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
            for (int slot = 0; slot < offerSlotCount; slot++)
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

            // 먼저 이번 보상 안에서 중복되지 않는 등급을 고르고,
            // 방 난이도 보너스/극한 보상 최소 등급을 그 뒤에 적용한다.
            List<CardGrade> availableGrades = BuildAvailableStatOfferGrades(candidates, usedStatOfferKeys);
            CardGrade offerGrade = availableGrades.Count > 0
                ? RollStatOfferGrade(availableGrades)
                : RollStatOfferGrade();
            offerGrade = ApplyCardRewardGradeBoost(offerGrade, cardRewardGradeBoost, availableGrades);
            offerGrade = ApplyCardRewardMinimumGrade(offerGrade, cardRewardMinimumStatGrade, availableGrades);

            List<StatType> gradeCandidates = BuildAvailableStatOfferPoolForGrade(candidates, offerGrade, usedStatOfferKeys);
            if (gradeCandidates.Count <= 0)
            {
                gradeCandidates = candidates;
            }

            StatType picked = gradeCandidates[templateRandom.Next(gradeCandidates.Count)];
            float actualBonus = GetEffectiveStatOfferBonus(picked, offerGrade);

            return CreateStatOffer(picked, offerGrade, actualBonus);
        }

        private RewardCardOffer[] GenerateRestShopCardOffers(int desiredCount)
        {
            if (desiredCount <= 0)
            {
                return Array.Empty<RewardCardOffer>();
            }

            // 상점은 현재 행운으로 자연 등장 가능한 최고 등급보다 한 단계 높은 카드만 판다.
            // usedStatOfferKeys로 같은 스탯+등급 카드가 한 상점에 중복 진열되는 것을 막는다.
            CardGrade offerGrade = GetRestShopCardGrade();
            var offers = new List<RewardCardOffer>(desiredCount);
            var usedStatOfferKeys = new HashSet<int>();
            for (int i = 0; i < desiredCount; i++)
            {
                RewardCardOffer offer = GenerateStatOfferForGrade(offerGrade, usedStatOfferKeys);
                if (offer == null)
                {
                    break;
                }

                offers.Add(offer);
                TrackStatOfferKey(offer, usedStatOfferKeys);
            }

            return offers.ToArray();
        }

        private RewardCardOffer GenerateStatOfferForGrade(CardGrade offerGrade, ISet<int> usedStatOfferKeys)
        {
            List<StatType> candidates = BuildAvailableStatOfferPool();
            if (candidates.Count <= 0)
            {
                return null;
            }

            List<StatType> gradeCandidates = BuildAvailableStatOfferPoolForGrade(candidates, offerGrade, usedStatOfferKeys);
            if (gradeCandidates.Count <= 0)
            {
                return null;
            }

            StatType picked = gradeCandidates[templateRandom.Next(gradeCandidates.Count)];
            float actualBonus = GetEffectiveStatOfferBonus(picked, offerGrade);
            return CreateStatOffer(picked, offerGrade, actualBonus);
        }

        private RewardCardOffer CreateStatOffer(StatType stat, CardGrade grade, float actualBonus)
        {
            RewardCardOffer offer = new RewardCardOffer
            {
                IsWeaponCard = false,
                StatType = stat,
                Grade = grade,
                StatBonusValue = actualBonus
            };

            RegisterStatOfferGenerated(offer);
            return offer;
        }

        private void RegisterStatOfferGenerated(RewardCardOffer offer)
        {
            if (offer != null && !offer.IsWeaponCard && offer.StatType == StatType.CardChoiceBonus)
            {
                cardChoiceBonusOffered = true;
            }
        }

        private CardGrade GetRestShopCardGrade()
        {
            return GetOneGradeAboveHighestLuckAvailableStatOfferGrade();
        }

        private CardGrade GetOneGradeAboveHighestLuckAvailableStatOfferGrade()
        {
            CardGrade highestNormallyAvailable = GetHighestLuckAvailableStatOfferGrade();
            if ((int)highestNormallyAvailable >= (int)CardGrade.Red)
            {
                return CardGrade.Red;
            }

            return (CardGrade)((int)highestNormallyAvailable + 1);
        }

        private static CardGrade ApplyCardRewardGradeBoost(CardGrade grade, int boost, ICollection<CardGrade> allowedGrades)
        {
            if (boost <= 0 || (int)grade >= (int)CardGrade.Red)
            {
                return grade;
            }

            // 허용 가능한 등급만 건너뛰며 올린다.
            // 후보 스탯이 이미 상한에 걸린 등급은 선택하지 않아 빈 보상을 피한다.
            CardGrade result = grade;
            for (int i = 0; i < boost && (int)result < (int)CardGrade.Red; i++)
            {
                CardGrade desired = (CardGrade)((int)result + 1);
                if (allowedGrades == null || allowedGrades.Contains(desired))
                {
                    result = desired;
                    continue;
                }

                for (int g = (int)desired + 1; g <= (int)CardGrade.Red; g++)
                {
                    CardGrade candidate = (CardGrade)g;
                    if (allowedGrades.Contains(candidate))
                    {
                        result = candidate;
                        break;
                    }
                }
            }

            return result;
        }

        private static CardGrade ApplyCardRewardMinimumGrade(CardGrade grade, int minimumGrade, ICollection<CardGrade> allowedGrades)
        {
            if (minimumGrade < 0 || (int)grade >= minimumGrade)
            {
                return grade;
            }

            CardGrade desired = (CardGrade)Math.Min((int)CardGrade.Red, minimumGrade);
            if (allowedGrades == null || allowedGrades.Contains(desired))
            {
                return desired;
            }

            for (int g = (int)desired + 1; g <= (int)CardGrade.Red; g++)
            {
                CardGrade candidate = (CardGrade)g;
                if (allowedGrades.Contains(candidate))
                {
                    return candidate;
                }
            }

            return grade;
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
            if (stat == StatType.CardChoiceBonus)
            {
                // 선택지 증가 카드는 런의 흐름을 크게 바꾸므로 낮은 층과 재등장을 모두 차단한다.
                if (currentFloor < CardChoiceBonusMinFloor || cardChoiceBonusOffered)
                {
                    return false;
                }

                if (GetRunStatBonus(StatType.CardChoiceBonus) >= 1f)
                {
                    return false;
                }
            }

            return GetRemainingStatBonusCap(stat) > epsilon;
        }

        private float GetEffectiveStatOfferBonus(StatType stat, CardGrade grade)
        {
            float baseBonus = GetBaseStatCardBonus(stat, grade);
            float remaining = GetRemainingStatBonusCap(stat);
            // 상한에 거의 도달한 스탯은 카드 등급보다 실제 증가량이 작아질 수 있다.
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
            return StatCardCatalog.Get(stat).Cap;
        }

        private static float GetBaseStatCardBonus(StatType stat, CardGrade grade)
        {
            return StatCardCatalog.Get(stat).GetBaseBonus(grade);
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

            // White(0)부터 Red(4)까지 Luck 테이블 전체 범위로 순수 확률 롤.
            // allowedGrades가 있으면 이미 중복/상한 때문에 제시할 수 없는 등급은 확률 합산에서 제외한다.
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

        private CardGrade GetHighestLuckAvailableStatOfferGrade()
        {
            PermanentProgressionData data = permanentProgression ?? PermanentProgressionData.CreateDefault();
            int luckLevel = Math.Max(0, Math.Min(10, data.GetLuckLevel()));
            for (int g = (int)CardGrade.Red; g >= (int)CardGrade.White; g--)
            {
                if (LuckGradeWeights[luckLevel, g] > 0)
                {
                    return (CardGrade)g;
                }
            }

            return CardGrade.White;
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

            // 무기 카드는 같은 보상 묶음 안에서 중복되지 않도록 뽑은 후보를 제거한다.
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

            GetCardRewardLayout(out int slotCount, out float cardW, out float cardH, out float gap, out float startX, out float cardY);

            for (int i = 0; i < slotCount; i++)
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

        /// <summary>카드 선택 대기 중 별도 상단 클리어 배너는 표시하지 않는다.</summary>
        private void DrawPendingCardRewardReveal(Renderer r)
        {
            return;
        }

        private void ConfirmCardSelection(int index)
        {
            RewardCardOffer offer = currentCardOffers[index];
            if (offer == null) return;

            ApplyCardOffer(offer);

            cardRewardActive = false;
            ClearCurrentCardOffers();
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
            float currentTotal = ClampRunStatBonusTotal(stat, runStatBonusTotals[statIndex]);
            float bonus = offer.StatBonusValue > 0f
                ? offer.StatBonusValue
                : GetEffectiveStatOfferBonus(stat, grade);
            float nextTotal = Math.Max(0f, currentTotal + bonus);
            float cappedTotal = GetStatBonusCap(stat);
            // 저장된 누적값과 신규 보너스를 모두 다시 클램프해 세이브/밸런스 변경 후에도 음수나 상한 초과가 남지 않게 한다.
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

        private static float ClampRunStatBonusTotal(StatType stat, float value)
        {
            return StatCardCatalog.Get(stat).Clamp(value);
        }

        private void GetCardRewardLayout(out int slotCount, out float cardW, out float cardH, out float gap, out float startX, out float cardY)
        {
            slotCount = GetCardRewardOfferSlotCount();
            cardW = slotCount > BaseCardRewardOfferCount ? 145f : 170f;
            cardH = 210f;
            gap = slotCount > BaseCardRewardOfferCount ? 12f : 15f;

            float fw = GameConfig.GpuWorldMaxRenderWidth;
            float fh = GameConfig.GpuWorldMaxRenderHeight;
            float totalW = slotCount * cardW + Math.Max(0, slotCount - 1) * gap;
            startX = (fw - totalW) * 0.5f;
            cardY = cardRewardWasBossRoom ? fh * 0.20f : fh * 0.16f;
        }

        private void ClampRunStatBonusTotals()
        {
            for (int i = 0; i < runStatBonusTotals.Length; i++)
            {
                runStatBonusTotals[i] = ClampRunStatBonusTotal((StatType)i, runStatBonusTotals[i]);
            }
        }

        // ── 카드 상태 리셋 ──────────────────────────────────────────────

        /// <summary>카드 UI 표시 상태만 초기화한다 (층 전환 시). 런 카드 등급은 유지.</summary>
        private void ResetCardDisplayState()
        {
            cardRewardActive = false;
            cardRewardRevealTimer = 0f;
            cardRewardWasBossRoom = false;
            cardRewardNextFloorIsBoss = false;
            cardRewardGradeBoost = 0;
            cardRewardMinimumStatGrade = -1;
            ClearCurrentCardOffers();
            pendingCardClickX = -1f;
            pendingCardClickY = -1f;
        }

        /// <summary>런 전체 카드 상태를 초기화한다 (새 런 시작 시).</summary>
        private void ResetCardRunState()
        {
            ResetCardDisplayState();
            weaponCardPoolCount = 0;
            cardChoiceBonusOffered = false;
            dashStrikeWindowTimer = 0f;
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
            return StatCardCatalog.Get(stat).Name;
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
            return StatCardCatalog.Get(stat).FormatOfferValue(bonusValue);
        }

        /// <summary>
        /// 조건부 발동 스탯 카드의 발동 조건 설명을 반환한다.
        /// 발동 조건이 없는 상시 스탯은 null을 반환한다.
        /// </summary>
        private static string GetStatConditionText(StatType stat)
        {
            return StatCardCatalog.Get(stat).Condition;
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

            GetCardRewardLayout(out int slotCount, out float cardW, out float cardH, out float gap, out float startX, out float cardY);

            for (int i = 0; i < slotCount; i++)
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

            // 배경 + 등급 색 테두리 (패널 프레임이 있으면 9-slice 금속 틀 사용)
            DrawCardPanelBackground(r, x, y, w, h, gradeColor);

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
            float nameFontSize = w < 160f ? 10f : 12f;
            float valueFontSize = w < 160f ? 13f : 15f;

            r.DrawTextCenteredShadow("스탯 카드", cx, y + 16f,
                Color.FromArgb(200, 170, 170, 170), 9f);
            r.DrawTextCenteredShadow(gradeName, cx, y + 40f, gradeColor, 13f);
            r.DrawRectangle(cx - w * 0.35f, y + 57f, w * 0.7f, 1f,
                Color.FromArgb(80, 200, 200, 200));
            r.DrawTextCenteredShadow(statName, cx, y + 80f,
                Color.FromArgb(255, 235, 225, 200), nameFontSize);
            r.DrawTextCenteredShadow(valueText, cx, y + 108f, gradeColor, valueFontSize);

            // 조건부 카드는 발동 조건을 명시해 단순 수치 카드와 구분한다.
            string conditionText = GetStatConditionText(offer.StatType);
            if (!string.IsNullOrEmpty(conditionText))
            {
                r.DrawTextCenteredShadow(conditionText, cx, y + 128f,
                    Color.FromArgb(235, 255, 200, 110), 8.2f);
            }

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
            return StatCardCatalog.Get(stat).FormatTotalValue(totalBonus);
        }

        /// <summary>
        /// 이번 런에서 획득한 스탯 카드 요약 목록을 반환한다.
        /// 각 항목은 "스탯이름 +누적값 (횟수)" 형태이며, 한 번이라도 획득한 스탯만 포함한다.
        /// 런 종료 요약 화면에서 사용한다.
        /// </summary>
        public string[] GetAcquiredStatCardSummary()
        {
            var list = new List<string>();
            for (int i = 0; i < RewardStatTypes.Length; i++)
            {
                StatType stat = RewardStatTypes[i];
                int count = runStatPickupCount[(int)stat];
                if (count <= 0)
                {
                    continue;
                }

                string totalText = GetStatBonusTotalText(stat, runStatBonusTotals[(int)stat]);
                list.Add(count > 1
                    ? $"{GetStatName(stat)} {totalText} (x{count})"
                    : $"{GetStatName(stat)} {totalText}");
            }

            return list.ToArray();
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
