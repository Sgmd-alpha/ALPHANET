using ALPHANET.Cards;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

namespace ALPHANET.Patches;

[HarmonyPatch(typeof(DustyTome), nameof(DustyTome.SetupForPlayer))]
public static class DustyTomePatch
{
    [HarmonyPostfix]
    public static void ForceDawnOfCommunion(DustyTome __instance)
    {
        // 直接获取指定先古卡
        var targetCard = ModelDb.Card<DawnOfCommunion>();

        // 强制指定尘封魔典的卡牌
        __instance.AncientCard = targetCard.Id;

        // GD.Print(
        //     $"[ALPHANET] DustyTome forced card: {targetCard.Id}"
        // );
    }
}