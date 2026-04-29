using System;

namespace My2DEngine.Game
{
    /// <summary>
    /// 전리품 희귀도. 효과 크기와 UI 테두리 색 표현 둘 다에 사용된다.
    /// 숫자가 클수록 희귀하고 효과가 강하다.
    /// </summary>
    public enum RewardPickupRarity
    {
        /// <summary>희귀도 없음(내부 기본값).</summary>
        None = 0,
        /// <summary>일반(흰색 테두리).</summary>
        Common = 1,
        /// <summary>희귀(파란색 테두리). 효과가 Common보다 강하다.</summary>
        Rare = 2,
        /// <summary>에픽(보라색 테두리). 가장 강한 효과.</summary>
        Epic = 3,
    }

    /// <summary>
    /// 전리품 종류. 획득 시 처리하는 효과가 다르다.
    /// </summary>
    public enum RewardPickupKind
    {
        /// <summary>응급 치료제. 획득하면 체력이 일정량 회복된다.</summary>
        HealthPack = 0,
        /// <summary>스팀팩. 획득하면 StimPackCount가 증가한다.</summary>
        StimPack = 1,
        /// <summary>공용 탄약 보급. 획득 시 현재 들고 있는 무기의 잔탄을 즉시 보충한다.</summary>
        AmmoPack = 2,
        /// <summary>휴식 상점과 런 경제에 사용하는 코인. 획득 시 보유 코인이 증가한다.</summary>
        Coin = 3,
    }

    /// <summary>
    /// 월드에 떨어져 플레이어가 걸어서 획득할 수 있는 전리품 인스턴스다.
    /// Active가 false가 되면 GameLogic에서 리스트에서 제거하거나 비활성 처리된다.
    /// </summary>
    public class RewardPickup
    {
        /// <summary>이 전리품이 드롭된 방 ID. 방 클리어 이전에 드롭됐는지 추적하는 데 사용된다.</summary>
        public int RoomId { get; set; }

        /// <summary>전리품 종류(탄약/치료제/스팀팩).</summary>
        public RewardPickupKind Kind { get; set; }

        /// <summary>전리품 희귀도. 효과 크기와 UI 색상 결정에 사용된다.</summary>
        public RewardPickupRarity Rarity { get; set; }

        /// <summary>희귀도에 따른 효과 배율. 체력 회복량이나 탄약 지급량 보정에 사용된다.</summary>
        public float EffectMultiplier { get; set; } = 1f;

        /// <summary>전리품 월드 X 좌표.</summary>
        public float X { get; set; }

        /// <summary>전리품 월드 Y 좌표.</summary>
        public float Y { get; set; }

        /// <summary>true이면 아직 획득되지 않아 렌더링·충돌 대상이다. false이면 이미 획득됐다.</summary>
        public bool Active { get; set; } = true;

        /// <summary>휴식 룸에서 제공되는 3지선다 선택 픽업인지 여부.</summary>
        public bool IsRestChoice { get; set; }

        /// <summary>휴식 룸 상점에서 구매할 때 필요한 코인 비용.</summary>
        public int CoinCost { get; set; }

        /// <summary>이 픽업이 제공하는 개수. 코인 픽업 수량 등에 사용된다.</summary>
        public int Amount { get; set; } = 1;

        /// <summary>
        /// 반짝임(펄스) 애니메이션에 사용하는 누적 시간(초).
        /// sin(PulseTimer)로 주기적 크기 변화를 만든다. 1000초마다 리셋해 float 정밀도를 유지한다.
        /// </summary>
        public float PulseTimer { get; set; }

        /// <summary>
        /// 매 프레임 호출해 PulseTimer를 갱신한다.
        /// 1000초를 넘으면 0으로 리셋해 float 오버플로를 방지한다.
        /// </summary>
        /// <param name="dt">이번 프레임 DeltaTime(초)</param>
        public void Update(float dt)
        {
            PulseTimer += dt;
            if (PulseTimer > 1000f)
            {
                PulseTimer = 0f;
            }
        }

        /// <summary>
        /// 희귀도를 사람이 읽기 쉬운 문자열로 반환한다.
        /// UI 레이블에 표시하기 위해 사용된다.
        /// </summary>
        /// <returns>"Common", "Rare", "Epic" 중 하나. None이면 빈 문자열.</returns>
        public string GetRarityLabel()
        {
            switch (Rarity)
            {
                case RewardPickupRarity.Common:
                    return "Common";
                case RewardPickupRarity.Rare:
                    return "Rare";
                case RewardPickupRarity.Epic:
                    return "Epic";
                default:
                    return string.Empty;
            }
        }
    }
}
