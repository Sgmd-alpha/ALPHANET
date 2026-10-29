using ALPHANET.Cards;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

namespace ALPHANET.Patches;

[HarmonyPatch]
public static class DustyTomeExcludePatch
{
    // 拦截 DustyTome 的 SetupForPlayer 方法
    [HarmonyPatch(typeof(DustyTome), nameof(DustyTome.SetupForPlayer))]
    [HarmonyPostfix]
    public static void ExcludeIdealDecreeFromDustyTome(DustyTome __instance, Player player)
    {
        // 如果 AncientCard 刚好抽到了我们的 DecreeNonexistenceIdeal，重新Roll一次或直接赋值为其他卡
        if (__instance.AncientCard != null)
        {
            var selectedCard = ModelDb.GetById<CardModel>(__instance.AncientCard);
            if (selectedCard is DecreeNonexistenceIdeal)
            {
                // 重新获取合法且排除此卡的先古卡池
                var validItems = player.Character.CardPool
                    .GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
                    .Where(c => c.Rarity == CardRarity.Ancient && c is not DecreeNonexistenceIdeal)
                    .ToList();

                if (validItems.Count > 0)
                {
                    var replacement = player.PlayerRng.Rewards.NextItem(validItems);
                    if (replacement != null)
                    {
                        __instance.AncientCard = replacement.Id;
                    }
                }
            }
        }
    }
}