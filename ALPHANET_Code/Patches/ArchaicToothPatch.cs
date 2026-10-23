using ALPHANET.Cards;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

namespace ALPHANET.Systems;

[HarmonyPatch]
public static class ArchaicToothPatch
{
    // 拦截 ArchaicTooth 的私有静态属性 TranscendenceUpgrades 的 Getter 方法
    [HarmonyPatch(typeof(ArchaicTooth), "TranscendenceUpgrades", MethodType.Getter)]
    [HarmonyPostfix]
    public static void AddCustomTranscendenceUpgrade(ref Dictionary<ModelId, CardModel> __result)
    {
        // 将【化险为夷 (Salvage)】 -> 【力挽狂澜 (TurnTheTide)】的映射关系塞入古老牙齿的字典中
        var starterId = ModelDb.Card<Salvage>().Id;
        var ancientCard = ModelDb.Card<TurnTheTide>();

        __result[starterId] = ancientCard;
    }
}