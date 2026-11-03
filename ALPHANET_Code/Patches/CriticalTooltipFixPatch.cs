using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ALPHANET.Patches;

[HarmonyPatch]
public static class CriticalTooltipFixPatch
{
    // 记录最近一次活跃的玩家角色（覆盖选牌、商店、局内图鉴等非战斗界面）
    private static CharacterModel? _lastKnownCharacter;

    // 1. 拦截玩家/生物属性的读取，作为轻量级的全局角色状态探针
    [HarmonyPatch(typeof(Player), nameof(Player.Character), MethodType.Getter)]
    [HarmonyPostfix]
    public static void TrackPlayerCharacter(Player __instance, CharacterModel __result)
    {
        if (__result != null)
        {
            _lastKnownCharacter = __result;
        }
    }

    // 2. 拦截卡牌 Owner 的读取（当其他角色在手牌、奖励界面、牌组里看到卡牌时必触发）
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.Owner), MethodType.Getter)]
    [HarmonyPostfix]
    public static void TrackCardOwner(Player? __result)
    {
        if (__result?.Character != null)
        {
            _lastKnownCharacter = __result.Character;
        }
    }

    // 3. 在渲染任何本地化文本前，注入准确的 OtherCharFlag
    [HarmonyPatch(typeof(LocString), nameof(LocString.GetFormattedText))]
    [HarmonyPrefix]
    public static void InjectOtherCharFlag(LocString __instance)
    {
        try
        {
            decimal flagValue = IsOtherCharacterActive() ? 1m : 0m;
            __instance.Add(new DynamicVar("OtherCharFlag", flagValue));
        }
        catch
        {
            // 忽略异常，安全兜底
        }
    }

    private static bool IsOtherCharacterActive()
    {
        try
        {
            // 优先级 A：如果在战斗中，直接从 CombatManager 的参战生物里找玩家角色
            if (CombatManager.Instance != null && CombatManager.Instance.IsInProgress)
            {
                // 通过反射安全获取当前战斗中的所有玩家/生物，避免 API 属性名不一致报错
                var cmType = CombatManager.Instance.GetType();
                foreach (var prop in cmType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (typeof(IEnumerable).IsAssignableFrom(prop.PropertyType) && prop.PropertyType != typeof(string))
                    {
                        if (prop.GetValue(CombatManager.Instance) is IEnumerable list)
                        {
                            foreach (var item in list)
                            {
                                if (item is Player p && p.Character != null)
                                {
                                    _lastKnownCharacter = p.Character;
                                    return p.Character is not Characters.ALPHANETCharacter;
                                }
                            }
                        }
                    }
                }
            }

            // 优先级 B：使用探针刚刚捕获到的当前局内角色（选牌界面、手牌悬停、局内打开图鉴）
            if (_lastKnownCharacter != null)
            {
                return _lastKnownCharacter is not Characters.ALPHANETCharacter;
            }
        }
        catch
        {
        }

        // 优先级 C：刚启动游戏在主菜单直接看图鉴（没有任何对局记录），默认显示诺奥瑟的“死亡”
        return false;
    }
}