using ALPHANET.Relics;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

namespace ALPHANET.Systems;

[HarmonyPatch]
public static class TouchOfOrobasPatch
{
    [HarmonyPatch(typeof(TouchOfOrobas), "RefinementUpgrades", MethodType.Getter)]
    [HarmonyPostfix]
    public static void AddMiracleShardUpgrade(ref Dictionary<ModelId, RelicModel> __result)
    {
        var starterId = ModelDb.Relic<MiracleResidue>().Id;
        var upgradedRelic = ModelDb.Relic<MiracleShard>();

        __result[starterId] = upgradedRelic;
    }
}