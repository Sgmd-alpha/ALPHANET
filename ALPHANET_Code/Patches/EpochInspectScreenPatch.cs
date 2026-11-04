using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Timeline;

namespace ALPHANET.Patches;

[HarmonyPatch(typeof(NEpochInspectScreen))]
public static class EpochInspectScreenPatch
{
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

    // ____fancyText：利用 Harmony 四个下划线语法，直接注入私有字段 _fancyText
    // __args：获取原方法传入的所有参数，用来精准判断当前打开的是不是你模组的时间线
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

            // 2. 【核心修复】：激活鼠标滚轮支持！
            // 原版默认是 MouseFilterEnum.Ignore，会导致滚轮事件直接穿透失效
            rtl.MouseFilter = Control.MouseFilterEnum.Stop;
            ____fancyText.MouseFilter = Control.MouseFilterEnum.Pass;

            // 3. 【核心修复】：调整文本框大小与位置，避免压住底部的“关闭”按钮
            // 默认控制在下半部分红框内（高度 250 像素刚好在关闭按钮上方停住）
            float targetHeight = 250f; 
            float offsetY = 0f;

            // 如果你希望像截图上方大红框那样，当没有插图（或你想强行占用上方空间）时把文本框往上提：
            // 只要把下面这行 if 条件改成 true，文本框就会往上移动 260 像素并拉长到 510 像素！
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

            if (!string.IsNullOrEmpty(rtl.Text) && rtl.Text.StartsWith("[left]"))
            {
                string clean = rtl.Text.Replace("[left]", "").Replace("[/left]", "");
                rtl.Text = $"[center]{clean}[/center]";
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