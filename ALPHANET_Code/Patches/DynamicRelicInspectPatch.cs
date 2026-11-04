using System.Collections;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.InspectScreens; // 请确认实际命名空间

namespace ALPHANET.Patches;

[HarmonyPatch(typeof(NInspectRelicScreen))]
public static class DynamicRelicInspectPatch
{
    // 记录已经绑定过拖拽事件的文本框，避免重复绑定
    private static readonly HashSet<ulong> DragBoundControlIds = new();
    private static bool _isDragging = false;

    [HarmonyTargetMethods]
    public static IEnumerable<MethodBase> TargetMethods()
    {
        return typeof(NInspectRelicScreen)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && 
                       (m.Name is "Show" or "Setup" or "Open" or "Refresh" or "UpdateRelic" or "SetRelic" || 
                        m.GetParameters().Any(p => p.ParameterType.Name.Contains("Relic")) ||
                        m.GetParameters().Length == 0)); // 涵盖点击左右箭头时的无参刷新方法
    }

    [HarmonyPostfix]
    public static void ToggleScrollForModRelics(
        NInspectRelicScreen __instance,
        RichTextLabel? ____description,
        RichTextLabel? ____flavor,
        int ____index, // 直接注入私有字段 _index（当前查看的遗物在列表中的下标）
        object[] __args)
    {
        if (____description == null && ____flavor == null) return;

        bool isMyModRelic = IsAlphanetRelic(__instance, ____index, __args);

        if (isMyModRelic)
        {
            if (____description != null)
            {
                EnableScrollAndDrag(____description);
            }

            if (____flavor != null)
            {
                EnableScrollAndDrag(____flavor, minHeight: 140f);
            }
        }
        else
        {
            if (____description != null)
            {
                RestoreVanillaLayout(____description);
            }

            if (____flavor != null)
            {
                RestoreVanillaLayout(____flavor, resetMinHeight: true);
            }
        }
    }

    /// <summary>
    /// 开启滚动、滚轮拦截以及鼠标左键抓手拖拽滑动
    /// </summary>
    private static void EnableScrollAndDrag(RichTextLabel rtl, float? minHeight = null)
    {
        rtl.ScrollActive = true;
        rtl.FitContent = false;
        rtl.ClipContents = true;
        rtl.SelectionEnabled = false; // 关闭默认的左键划词选中，防止和拖拽冲突
        rtl.MouseFilter = Control.MouseFilterEnum.Stop;
        rtl.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

        if (minHeight.HasValue)
        {
            rtl.CustomMinimumSize = new Vector2(rtl.CustomMinimumSize.X, minHeight.Value);
        }

        // 绑定鼠标左键按住拖拽事件（每个节点实例仅绑定一次）
        ulong id = rtl.GetInstanceId();
        if (DragBoundControlIds.Add(id))
        {
            rtl.GuiInput += (@event) => OnRichTextGuiInput(rtl, @event);
        }
    }

    /// <summary>
    /// 恢复原版遗物的默认居中自适应布局
    /// </summary>
    private static void RestoreVanillaLayout(RichTextLabel rtl, bool resetMinHeight = false)
    {
        rtl.ScrollActive = false;
        rtl.FitContent = true;
        rtl.MouseFilter = Control.MouseFilterEnum.Ignore;

        if (resetMinHeight)
        {
            rtl.CustomMinimumSize = new Vector2(rtl.CustomMinimumSize.X, 0f);
        }
    }

    /// <summary>
    /// 处理鼠标左键按住上下拖拽文本（抓手模式：鼠标往上拉 = 看下面的文字）
    /// </summary>
    private static void OnRichTextGuiInput(RichTextLabel rtl, InputEvent @event)
    {
        if (!rtl.ScrollActive) return;

        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            _isDragging = mb.Pressed;
            rtl.AcceptEvent();
        }
        else if (@event is InputEventMouseMotion mm && _isDragging && (mm.ButtonMask & MouseButtonMask.Left) != 0)
        {
            VScrollBar? vScroll = rtl.GetVScrollBar();
            if (vScroll != null)
            {
                // 鼠标往上拖 (Relative.Y < 0) 时，滚动条数值增加（往下看）
                vScroll.Value -= mm.Relative.Y;
                rtl.AcceptEvent();
            }
        }
    }

    /// <summary>
    /// 精准识别当前展示的遗物是否属于 ALPHANET 模组（支持从 _relics 列表 + _index 中提取）
    /// </summary>
    private static bool IsAlphanetRelic(NInspectRelicScreen screen, int currentIndex, object[] args)
    {
        var myAssembly = typeof(DynamicRelicInspectPatch).Assembly;

        // 1. 检查方法传入的参数（兼容直接传 RelicModel 或传 IList<RelicModel> 的情况）
        if (args != null)
        {
            foreach (var arg in args)
            {
                if (CheckObjectOrListAt(arg, currentIndex, myAssembly))
                    return true;
            }
        }

        // 2. 检查 NInspectRelicScreen 内部的所有字段（重点抓取 _relics 列表并按 _index 取出当前遗物）
        foreach (var field in typeof(NInspectRelicScreen).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            var val = field.GetValue(screen);
            if (CheckObjectOrListAt(val, currentIndex, myAssembly))
                return true;
        }

        return false;
    }

    private static bool CheckObjectOrListAt(object? obj, int index, Assembly modAssembly)
    {
        if (obj == null) return false;
        Type type = obj.GetType();

        // 情况 A：字段或参数本身就是单个遗物模型
        if (type.Assembly == modAssembly) return true;

        // 情况 B：字段是遗物列表（如 List<RelicModel> 或 RelicModel[]），根据当前 _index 取出正在展示的那一个！
        if (obj is IList list && list.Count > 0)
        {
            int safeIndex = Math.Clamp(index, 0, list.Count - 1);
            object? currentItem = list[safeIndex];
            if (currentItem != null && IsItemFromMod(currentItem, modAssembly))
                return true;
        }
        else if (obj is IEnumerable enumerable && obj is not string && obj is not Node)
        {
            var items = enumerable.Cast<object>().ToList();
            if (items.Count > 0)
            {
                int safeIndex = Math.Clamp(index, 0, items.Count - 1);
                object? currentItem = items[safeIndex];
                if (currentItem != null && IsItemFromMod(currentItem, modAssembly))
                    return true;
            }
        }

        return false;
    }

    private static bool IsItemFromMod(object item, Assembly modAssembly)
    {
        Type itemType = item.GetType();
        if (itemType.Assembly == modAssembly) return true;

        // 情况 C：如果列表里存的是 UI 节点（如 NRelicHolder）或通过 RitsuLib 包装过的代理类，检查其内部属性/字段
        foreach (var prop in itemType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            try
            {
                var subVal = prop.GetValue(item);
                if (subVal != null && subVal.GetType().Assembly == modAssembly) return true;
            }
            catch { }
        }

        return false;
    }
}