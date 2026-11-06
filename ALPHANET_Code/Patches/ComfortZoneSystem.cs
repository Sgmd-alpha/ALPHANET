using ALPHANET.Cards;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;

namespace ALPHANET.Systems;

[HarmonyPatch]
public static class ComfortZoneSystem
{
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardPlayed))]
    [HarmonyPostfix]
    public static void OnAnyCardPlayed(CardPlay cardPlay)
    {
        var playedCard = cardPlay?.Card;
        if (playedCard == null || playedCard.Type != CardType.Attack)
            return;

        // 【核心修复】只有当打出的攻击牌是【非基础牌】时，才触发全牌堆重置！
        if (!ComfortZone.IsBasicCard(playedCard))
        {
            ComfortZone.ResetOtherComfortZonesInPiles(playedCard);
        }
    }
}