using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Relics;

namespace ALPHANET.Patches;

[HarmonyPatch(typeof(SeaGlass), nameof(SeaGlass.AfterObtained))]
public static class SeaGlassConsoleFixPatch
{
    [HarmonyPrefix]
    public static void FixNullCharacterId(SeaGlass __instance)
    {
        if (__instance.CharacterId == null)
        {
            if (__instance.Owner != null && __instance.Owner.Character != null)
            {
                __instance.CharacterId = __instance.Owner.Character.Id;
            }
        }
    }
}