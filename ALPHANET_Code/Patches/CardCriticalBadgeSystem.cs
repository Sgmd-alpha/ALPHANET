using System.Reflection;
using ALPHANET.Cards;
using ALPHANET.Powers;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace ALPHANET.Systems;

[HarmonyPatch]
public static class CardCriticalBadgeSystem
{
    private const string BadgeNodeName = "ALPHANET_CritBadge";
    private const string InfinityBadgeNodeName = "ALPHANET_InfinityBadge";

    // =========================================================
    // 1. 临界状态进出图标路径与参数
    // =========================================================
    private static readonly string EnterIconPath = $"{Entry.ResPath}/images/ui/crit_enter.png";
    private static readonly string ExitIconPath = $"{Entry.ResPath}/images/ui/crit_exit.png";
    private static readonly string EnterExitIconPath = $"{Entry.ResPath}/images/ui/crit_enter_exit.png";

    private static readonly Vector2 BadgePosition = new(-87f, -190f);
    private static readonly Vector2 SingleBadgeSize = new(26f, 26f);
    private static readonly Vector2 DoubleBadgeSize = new(52f, 26f);

    // =========================================================
    // 2. 【无限认知】∞ 额外打出标志路径与位置参数
    // =========================================================
    private static readonly string InfinityIconPath = $"{Entry.ResPath}/images/ui/infinity_badge.png";

    // 位置说明：
    // (-87f, -158f) 为左上角费用水晶与临界圆圈的正下方；
    // 如果你想放在卡面右上角或正上方，只需调整这里的 X 和 Y 即可！
    private static readonly Vector2 InfinityBadgePosition = new(-90f, -156f);

    // ∞ 符号
    private static readonly Vector2 InfinityBadgeSize = new(40f, 40f);

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
    /// 供 CriticalState 与 InfiniteCognitionEchoPower 调用，立刻刷新手牌中所有卡牌的角标！
    /// </summary>
    public static void RefreshAllVisibleCardBadges()
    {
        var combatRoom = MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom.Instance;
        if (combatRoom == null || !GodotObject.IsInstanceValid(combatRoom)) return;

        foreach (var nCard in FindAllCardsInNode(combatRoom))
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
                          ?? __instance;

        // 分别更新【临界状态角标】与【无限认知 ∞ 角标】
        UpdateCritBadge(containerNode, cardModel);
        UpdateInfinityBadge(containerNode, cardModel);
    }

    private static void UpdateCritBadge(Node containerNode, CardModel? cardModel)
    {
        var existingBadge = containerNode.GetNodeOrNull<TextureRect>(BadgeNodeName);

        if (cardModel is not ICriticalIndicatorCard indicatorCard)
        {
            if (existingBadge != null) existingBadge.Visible = false;
            return;
        }
        
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
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
            };

            containerNode.AddChild(existingBadge);
        }

        existingBadge.Visible = true;
        existingBadge.Position = BadgePosition;

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
                existingBadge.CustomMinimumSize = SingleBadgeSize;
                existingBadge.Size = SingleBadgeSize;
                break;
        }
    }

    private static void UpdateInfinityBadge(Node containerNode, CardModel? cardModel)
    {
        var existingInfBadge = containerNode.GetNodeOrNull<TextureRect>(InfinityBadgeNodeName);

        bool shouldShow = false;
        if (cardModel != null && cardModel.IsMutable)
        {
            var creature = cardModel.Owner?.Creature;
            var echoPower = creature?.GetPower<InfiniteCognitionEchoPower>();
            if (echoPower != null && echoPower.ShouldShowInfinityBadgeOn(cardModel))
            {
                shouldShow = true;
            }
        }

        if (!shouldShow)
        {
            if (existingInfBadge != null) existingInfBadge.Visible = false;
            return;
        }

        if (existingInfBadge == null)
        {
            existingInfBadge = new TextureRect
            {
                Name = InfinityBadgeNodeName,
                Position = InfinityBadgePosition,
                ZIndex = 0,
                ZAsRelative = true,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
            };

            containerNode.AddChild(existingInfBadge);
        }

        existingInfBadge.Visible = true;
        existingInfBadge.Position = InfinityBadgePosition;
        existingInfBadge.Texture = ResourceLoader.Load<Texture2D>(InfinityIconPath);
        existingInfBadge.CustomMinimumSize = InfinityBadgeSize;
        existingInfBadge.Size = InfinityBadgeSize;
    }
}