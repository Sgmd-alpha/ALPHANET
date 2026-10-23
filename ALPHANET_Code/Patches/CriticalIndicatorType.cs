namespace ALPHANET.Cards;

public enum CriticalIndicatorType
{
    None,           // 无临界状态变动
    Enter,          // 🟣 紫色圆圈：打出后会【进入】临界状态
    Exit,           // ⚪ 白色圆圈：打出后会【退出】临界状态
    EnterThenExit   // 🟣➔⚪ 紫白双圈：先进入临界状态，随后退出（如单卡进出）
}

/// <summary>
/// 让涉及临界状态的卡牌实现此接口（或写在你的卡牌基类里）
/// </summary>
public interface ICriticalIndicatorCard
{
    // 支持动态计算！比如某张牌“如果生命低于50%才进入临界”，可以在属性里根据当前血量实时返回 Enter 或 None！
    CriticalIndicatorType GetCriticalIndicator();

    // 默认EnterThenExit，如果某张卡有不同的预览效果，可以自行重写此方法。
    CriticalIndicatorType GetPreviewCriticalIndicator()
    {
        return GetCriticalIndicator();
    }
}