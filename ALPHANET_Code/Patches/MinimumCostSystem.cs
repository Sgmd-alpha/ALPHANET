using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace ALPHANET.Systems;

/// <summary>
/// 声明卡牌存在不可被突破的最低能量消耗下限
/// </summary>
public interface IMinimumEnergyCostCard
{
    int MinimumEnergyCost { get; }
}

[HarmonyPatch]
public static class MinimumCostSystem
{
    private static readonly Type? EnergyCostType = typeof(CardModel)
        .GetProperty(nameof(CardModel.EnergyCost), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        ?.PropertyType;

    private static readonly FieldInfo? OwnerCardField = FindOwnerCardField(EnergyCostType);

    private static FieldInfo? FindOwnerCardField(Type? type)
    {
        for (Type? current = type; current != null; current = current.BaseType)
        {
            foreach (FieldInfo field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (typeof(CardModel).IsAssignableFrom(field.FieldType))
                {
                    return field;
                }
            }
        }
        return null;
    }

    // 自动匹配 CardEnergyCost 中所有返回 int 的 GetWithModifiers / GetAmountToSpend 重载
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        if (EnergyCostType == null)
            yield break;

        foreach (MethodInfo method in EnergyCostType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (method.ReturnType == typeof(int) &&
                method.Name is "GetWithModifiers" or "GetAmountToSpend")
            {
                yield return method;
            }
        }
    }

    // 管线最终出口拦截：任何减费或免费手段结算完后，若低于下限则强制拉回下限
    [HarmonyPostfix]
    public static void ClampMinimumEnergyCost(object __instance, ref int __result)
    {
        if (OwnerCardField == null)
            return;

        if (OwnerCardField.GetValue(__instance) is IMinimumEnergyCostCard minCostCard)
        {
            int minCost = minCostCard.MinimumEnergyCost;
            if (__result < minCost)
            {
                __result = minCost;
            }
        }
    }
}