using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.InspectScreens;

namespace ALPHANET.Patches;

[HarmonyPatch(typeof(NInspectRelicScreen))]
public static class DynamicRelicInspectPatch
{
    // 自动拦截 NInspectRelicScreen 中用于更新界面数据的方法
    [HarmonyTargetMethods]
    public static IEnumerable<MethodBase> TargetMethods()
    {
        return typeof(NInspectRelicScreen)
            // 【修复】：加上 BindingFlags.DeclaredOnly，严禁越界抓取 Godot 引擎底层的 Show 方法！
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && 
                       (m.Name is "Show" or "Setup" or "Open" or "Refresh" || 
                        m.GetParameters().Any(p => p.ParameterType.Name.Contains("Relic"))));
    }

    [HarmonyPostfix]
    public static void ToggleScrollForModRelics(
        NInspectRelicScreen __instance,
        RichTextLabel? ____description,
        RichTextLabel? ____flavor,
        object[] __args)
    {
        if (____description == null && ____flavor == null) return;

        // 识别当前界面展示的遗物是否属于你的模组程序集
        bool isMyModRelic = IsAlphanetRelic(__instance, __args);

        if (isMyModRelic)
        {
            // === 你的模组遗物：开启滚动与滚轮拦截 ===
            if (____description != null)
            {
                ____description.ScrollActive = true;
                ____description.FitContent = false;
                ____description.ClipContents = true;
                ____description.MouseFilter = Control.MouseFilterEnum.Stop;
                ____description.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            }

            if (____flavor != null)
            {
                ____flavor.ScrollActive = true;
                ____flavor.FitContent = false;
                ____flavor.ClipContents = true;
                ____flavor.MouseFilter = Control.MouseFilterEnum.Stop;
                ____flavor.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
                ____flavor.CustomMinimumSize = new Vector2(____flavor.CustomMinimumSize.X, 140f);
            }
        }
        else
        {
            // === 原版遗物：恢复官方默认排版（自适应高度，不拦截鼠标） ===
            if (____description != null)
            {
                ____description.ScrollActive = false;
                ____description.FitContent = true;
                ____description.MouseFilter = Control.MouseFilterEnum.Ignore;
                // ____description.CustomMinimumSize 官方通常为默认的 0 或极小值，FitContent 会自动接管
            }

            if (____flavor != null)
            {
                ____flavor.ScrollActive = false;
                ____flavor.FitContent = true;
                ____flavor.MouseFilter = Control.MouseFilterEnum.Ignore;
                ____flavor.CustomMinimumSize = new Vector2(____flavor.CustomMinimumSize.X, 0f);
            }
        }
    }

    /// <summary>
    /// 动态探测当前展示的遗物实例是否属于我们的模组
    /// </summary>
    private static bool IsAlphanetRelic(NInspectRelicScreen screen, object[] args)
    {
        var myAssembly = typeof(DynamicRelicInspectPatch).Assembly;

        // 1. 检查方法传入的参数（如果拦截的是 Show(RelicModel relic) 这种方法）
        foreach (var arg in args)
        {
            if (arg != null && arg.GetType().Name.Contains("Relic"))
            {
                if (arg.GetType().Assembly == myAssembly) return true;
            }
        }

        // 2. 兜底检查界面实例内部保存的当前遗物字段（比如 _relic, _currentRelic 等）
        foreach (var field in typeof(NInspectRelicScreen).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            var val = field.GetValue(screen);
            if (val != null && val.GetType().Name.Contains("Relic"))
            {
                if (val.GetType().Assembly == myAssembly) return true;
            }
        }

        return false;
    }
}