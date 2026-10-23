using System.Reflection;
using ALPHANET.Cards;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace ALPHANET.Systems;

[HarmonyPatch]
public static class CardCriticalBadgeSystem
{
    private const string BadgeNodeName = "ALPHANET_CritBadge";

    // =========================================================
    // 在这里配置你的 PNG 图标路径与微调参数
    // =========================================================
    private static readonly string EnterIconPath = $"{Entry.ResPath}/images/ui/crit_enter.png";
    private static readonly string ExitIconPath = $"{Entry.ResPath}/images/ui/crit_exit.png";
    private static readonly string EnterExitIconPath = $"{Entry.ResPath}/images/ui/crit_enter_exit.png";

    // 图标位置：对应截图中“灵剑之舞”文字正右方的蓝框位置
    // X 越小越靠左，Y 越小越靠上
    private static readonly Vector2 BadgePosition = new(68f, -190f);

    // 图标缩放后的显示大小（像素）：无论原图多大，都会自动等比缩放到 26x26
    private static readonly Vector2 SingleBadgeSize = new(26f, 26f);
    private static readonly Vector2 DoubleBadgeSize = new(52f, 26f);

    [HarmonyTargetMethods]
    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var method in AccessTools.GetDeclaredMethods(typeof(NCard)))
        {
            if (method.Name == "UpdateVisuals" || method.Name == "Reload" || method.Name == "set_Model")
            {
                if (!method.IsAbstract && !method.IsGenericMethod)
                    yield return method;
            }
        }
    }

    /// <summary>
    /// 供 CriticalState 在进出临界状态时调用，立刻刷新手牌中动态卡牌（如《阴阳逆转》《潜能》）的圆圈颜色！
    /// </summary>
    public static void RefreshAllVisibleCardBadges()
    {
        var combatRoom = MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom.Instance;
        if (combatRoom == null || !GodotObject.IsInstanceValid(combatRoom)) return;

        // 遍历战斗房间树中的所有 NCard 节点，立刻更新一次它们的圆圈图标
        foreach (var nCard in FindAllCardsInNode((Node)combatRoom))
        {
            OnCardVisualsUpdated(nCard);
        }
    }

    private static IEnumerable<NCard> FindAllCardsInNode(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is NCard card)
                yield return card;

            foreach (var nested in FindAllCardsInNode(child))
                yield return nested;
        }
    }

    [HarmonyPostfix]
    public static void OnCardVisualsUpdated(NCard __instance)
    {
        if (__instance == null || !GodotObject.IsInstanceValid(__instance)) return;

        var modelProp = AccessTools.PropertyGetter(typeof(NCard), "Model") 
                     ?? AccessTools.PropertyGetter(typeof(NCard), "CardModel");
        var cardModel = modelProp?.Invoke(__instance, null) as CardModel;

        Node containerNode = __instance.GetNodeOrNull("Visuals") 
                          ?? __instance.GetNodeOrNull("CardContainer") 
                          ?? __instance.GetNodeOrNull("Front") 
                          ?? (Node)__instance;

        var existingBadge = containerNode.GetNodeOrNull<TextureRect>(BadgeNodeName);

        if (cardModel is not ICriticalIndicatorCard indicatorCard)
        {
            if (existingBadge != null) existingBadge.Visible = false;
            return;
        }
        
        // 规范模型（图鉴/预览）不可用于查询运行时 Owner，
        // 因此调用专门的预览方法；正常运行时则计算真实状态。
        var indicatorType = cardModel.IsMutable
            ? indicatorCard.GetCriticalIndicator()
            : indicatorCard.GetPreviewCriticalIndicator();

        if (indicatorType == CriticalIndicatorType.None)
        {
            if (existingBadge != null) existingBadge.Visible = false;
            return;
        }

        if (existingBadge == null)
        {
            existingBadge = new TextureRect
            {
                Name = BadgeNodeName,
                Position = BadgePosition,
                ZIndex = 0,
                ZAsRelative = true,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                // 关键设置：忽略原图分辨率，强制缩放到我们设定的 Size 大小，并保持长宽比居中
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
            };

            containerNode.AddChild(existingBadge);
        }

        existingBadge.Visible = true;
        existingBadge.Position = BadgePosition;

        // 根据状态切换对应的 PNG 贴图与尺寸
        switch (indicatorType)
        {
            case CriticalIndicatorType.Enter:
                existingBadge.Texture = ResourceLoader.Load<Texture2D>(EnterIconPath);
                existingBadge.CustomMinimumSize = SingleBadgeSize;
                existingBadge.Size = SingleBadgeSize;
                break;

            case CriticalIndicatorType.Exit:
                existingBadge.Texture = ResourceLoader.Load<Texture2D>(ExitIconPath);
                existingBadge.CustomMinimumSize = SingleBadgeSize;
                existingBadge.Size = SingleBadgeSize;
                break;

            case CriticalIndicatorType.EnterThenExit:
                existingBadge.Texture = ResourceLoader.Load<Texture2D>(EnterExitIconPath);
                existingBadge.CustomMinimumSize = DoubleBadgeSize;
                existingBadge.Size = DoubleBadgeSize;
                break;
        }
    }
}