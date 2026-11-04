using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Timeline;

namespace ALPHANET.Patches;

[HarmonyPatch(typeof(NEpochInspectScreen))]
public static class EpochInspectScreenPatch
{
    // 记录已经绑定过拖拽事件的文本框，避免重复绑定
    private static readonly HashSet<ulong> DragBoundControlIds = new();
    private static bool _isDragging = false;

    // 如果你想仅针对特定的几个时间线生效，可以把它们的类名填在这里；
    // 留空则自动对你模组（ALPHANET）注册的所有时间线生效！
    private static readonly HashSet<string> TargetEpochClassNames = new()
    {
        // "YourCustomEpochClassName"
    };

    // 自动拦截 NEpochInspectScreen 中所有接收时间线数据的方法（如 Open, Setup, SetEpoch 等）
    [HarmonyTargetMethods]
    public static IEnumerable<MethodBase> TargetMethods()
    {
        return typeof(NEpochInspectScreen)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => !m.IsSpecialName && m.GetParameters().Length > 0 &&
                        m.GetParameters().Any(p => p.ParameterType.Name.Contains("Epoch")));
    }

    // 用来备份原版文本框的初始位置和尺寸，防止切换到原版时间线时错位
    private static Vector2? _originalPos;
    private static Vector2? _originalSize;

    [HarmonyPostfix]
    public static void FormatModEpochText(
        NEpochInspectScreen __instance, 
        Control? ____fancyText, 
        object[] __args)
    {
        if (____fancyText == null) return;

        RichTextLabel? rtl = ____fancyText as RichTextLabel 
                          ?? ____fancyText.FindChildren("*", "RichTextLabel", true, false)
                                          .Cast<RichTextLabel>()
                                          .FirstOrDefault();

        if (rtl == null) return;

        // 首次运行时，记录原版默认的坐标与尺寸
        _originalPos ??= ____fancyText.Position;
        _originalSize ??= ____fancyText.Size;

        bool isMyModEpoch = IsAlphanetEpoch(__instance, __args);

        if (isMyModEpoch)
        {
            // 1. 开启滚动条与边界裁剪，关闭高度自适应
            rtl.ScrollActive = true;
            rtl.FitContent = false;
            rtl.ClipContents = true;
            rtl.SelectionEnabled = false; // 关闭默认划词选中，防止与拖拽冲突

            // 2. 【核心修复】：激活鼠标滚轮支持与左键拖拽阅读！
            rtl.MouseFilter = Control.MouseFilterEnum.Stop;
            ____fancyText.MouseFilter = Control.MouseFilterEnum.Pass;

            // 绑定鼠标左键按住拖拽事件（每个节点实例仅绑定一次）
            ulong id = rtl.GetInstanceId();
            if (DragBoundControlIds.Add(id))
            {
                rtl.GuiInput += (@event) => OnRichTextGuiInput(rtl, @event);
            }

            // 3. 【核心修复】：调整文本框大小与位置，避免压住底部的“关闭”按钮
            // 默认控制在下半部分红框内（高度 250 像素刚好在关闭按钮上方停住）
            float targetHeight = 250f; 
            float offsetY = 0f;

            // 如果你希望像截图上方大红框那样，当没有插图时把文本框往上提：
            bool expandIntoPortraitArea = false; 
            if (expandIntoPortraitArea)
            {
                offsetY = -260f;      // 向上平移覆盖插图区
                targetHeight = 510f;  // 高度同时涵盖上下两个红框
            }

            Vector2 basePos = _originalPos.Value;
            Vector2 baseSize = _originalSize.Value;

            ____fancyText.Position = new Vector2(basePos.X, basePos.Y + offsetY);
            ____fancyText.CustomMinimumSize = new Vector2(baseSize.X, targetHeight);
            ____fancyText.Size = new Vector2(baseSize.X, targetHeight);

            if (rtl != ____fancyText)
            {
                rtl.CustomMinimumSize = new Vector2(baseSize.X, targetHeight);
                rtl.Size = new Vector2(baseSize.X, targetHeight);
            }

            // 4. 剥离原版居中标签，强制左对齐
            if (!string.IsNullOrEmpty(rtl.Text) && !rtl.Text.StartsWith("[left]"))
            {
                string clean = rtl.Text.Replace("[center]", "").Replace("[/center]", "");
                rtl.Text = $"[left]{clean}[/left]";
            }
        }
        else
        {
            // 切换回原版角色时间线时，完美还原坐标、尺寸、鼠标穿透与居中对齐
            if (_originalPos.HasValue && _originalSize.HasValue)
            {
                ____fancyText.Position = _originalPos.Value;
                ____fancyText.CustomMinimumSize = Vector2.Zero;
                ____fancyText.Size = _originalSize.Value;
            }

            rtl.FitContent = true;
            rtl.MouseFilter = Control.MouseFilterEnum.Ignore;
            rtl.SelectionEnabled = true;

            if (!string.IsNullOrEmpty(rtl.Text) && rtl.Text.StartsWith("[left]"))
            {
                string clean = rtl.Text.Replace("[left]", "").Replace("[/left]", "");
                rtl.Text = $"[center]{clean}[/center]";
            }
        }
    }

    /// <summary>
    /// 处理鼠标左键按住上下拖拽文本（抓手模式）
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
    /// 通过方法参数和实例字段，判断当前展示的时间线是否属于 ALPHANET 模组
    /// </summary>
    private static bool IsAlphanetEpoch(NEpochInspectScreen screen, object[] args)
    {
        var myAssembly = typeof(EpochInspectScreenPatch).Assembly;

        // 1. 优先检查调用方法时传入的参数（如 EpochModel 或 NEpochSlot）
        foreach (var arg in args)
        {
            if (CheckObjectBelongsToMod(arg, myAssembly))
                return true;
        }

        // 2. 兜底检查界面实例内部保存的当前状态字段
        foreach (var field in typeof(NEpochInspectScreen).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (CheckObjectBelongsToMod(field.GetValue(screen), myAssembly))
                return true;
        }

        return false;
    }

    private static bool CheckObjectBelongsToMod(object? obj, Assembly modAssembly)
    {
        if (obj == null) return false;
        Type type = obj.GetType();

        // 情况 A：对象本身就是你模组里的 EpochModel 类
        if (type.Assembly == modAssembly)
        {
            return TargetEpochClassNames.Count == 0 || TargetEpochClassNames.Contains(type.Name);
        }

        // 情况 B：对象是官方的 UI 容器（如 NEpochSlot），但里面包裹了你模组的 EpochModel
        if (type.Name.Contains("Epoch") || type.Name.Contains("Slot"))
        {
            foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                try
                {
                    var val = prop.GetValue(obj);
                    if (val != null && val.GetType().Assembly == modAssembly)
                    {
                        return TargetEpochClassNames.Count == 0 || TargetEpochClassNames.Contains(val.GetType().Name);
                    }
                }
                catch { }
            }
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                try
                {
                    var val = field.GetValue(obj);
                    if (val != null && val.GetType().Assembly == modAssembly)
                    {
                        return TargetEpochClassNames.Count == 0 || TargetEpochClassNames.Contains(val.GetType().Name);
                    }
                }
                catch { }
            }
        }

        return false;
    }
}