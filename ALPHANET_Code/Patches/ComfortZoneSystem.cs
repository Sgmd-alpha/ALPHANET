using ALPHANET.Cards;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;

namespace ALPHANET.Systems;

[HarmonyPatch]
public static class ComfortZoneSystem
{
    // 拦截全局的 Hook.AfterCardPlayed
    // 无论《习常惯行》藏在抽牌堆、弃牌堆还是手牌里，只要主人打出了其他攻击牌，立刻将它重置！
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardPlayed))]
    [HarmonyPostfix]
    public static void OnAnyCardPlayed(CardPlay cardPlay)
    {
        var playedCard = cardPlay?.Card;
        if (playedCard != null && playedCard.Type == CardType.Attack)
        {
            ComfortZone.ResetOtherComfortZonesInPiles(playedCard);
        }
    }
}