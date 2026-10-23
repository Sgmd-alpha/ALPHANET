using System.Reflection;
using ALPHANET.Cards;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;

namespace ALPHANET.Systems;

[HarmonyPatch]
public static class CorneredCostSystem
{
    private static bool _isRefreshing = false;

    public static void RefreshPlayerCorneredCards(Player? player)
    {
        if (player == null || _isRefreshing) return;

        try
        {
            _isRefreshing = true;

            PileType[] combatPiles = [PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust];
            foreach (var pileType in combatPiles)
            {
                var pile = pileType.GetPile(player);
                if (pile?.Cards == null) continue;

                foreach (var card in pile.Cards)
                {
                    if (card is Cornered cornered)
                    {
                        cornered.SyncCorneredCost();
                    }
                }
            }
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    public static void RefreshAllPlayersInCombat(ICombatState? combatState)
    {
        if (combatState?.Players == null) return;
        foreach (var player in combatState.Players)
        {
            RefreshPlayerCorneredCards(player);
        }
    }

    // 目标方法：锁定 Creature 类里所有可能改血量的方法（排除属性读取 get_）
    [HarmonyTargetMethods]
    public static IEnumerable<MethodBase> TargetHpChangeMethods()
    {
        foreach (var method in AccessTools.GetDeclaredMethods(typeof(Creature)))
        {
            if (method.Name.StartsWith("get_") || method.ReturnType == typeof(bool)) 
                continue;

            if (method.Name.Contains("Hp") || method.Name.Contains("Heal") || method.Name.Contains("Damage"))
            {
                if (!method.IsAbstract && !method.IsGenericMethod)
                {
                    yield return method;
                }
            }
        }
    }

    // 利用 Harmony 的 __instance 参数直接拿到当前生物对象！
    [HarmonyPostfix]
    public static void OnCreatureMethodPostfix(Creature __instance)
    {
        if (__instance == null || _isRefreshing) return;

        // 通过生物自带的 CombatState 获取战斗状态并刷新全场玩家
        var combatState = __instance.CombatState;
        if (combatState != null)
        {
            RefreshAllPlayersInCombat(combatState);
        }
    }
}