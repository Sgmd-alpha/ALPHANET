using ALPHANET.Characters;
using ALPHANET.Powers;
using ALPHANET.States;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class TCSPandemonium : ModCardTemplate
{

    public TCSPandemonium() : base(5, CardType.Power, CardRarity.Rare, TargetType.Self, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(1),
        new PowerVar<TCSPandemoniumPower>(1m)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<CriticalState>(),
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
        HoverTipFactory.FromKeyword(CardKeyword.Unplayable)
    ];

    /// <summary>
    /// 统计当前玩家在【抽牌堆 + 弃牌堆 + 手牌】中的诅咒牌总数
    /// </summary>
    public static int CountCursesInActivePiles(Player? player)
    {
        if (player == null) return 0;

        if (CombatManager.Instance.IsInProgress && player.Creature?.CombatState != null)
        {
            int count = 0;
            count += PileType.Draw.GetPile(player).Cards.Count(c => c != null && c.Type == CardType.Curse);
            count += PileType.Discard.GetPile(player).Cards.Count(c => c != null && c.Type == CardType.Curse);
            count += PileType.Hand.GetPile(player).Cards.Count(c => c != null && c.Type == CardType.Curse);
            return count;
        }

        return 0;
    }

    // =========================================================================
    // 核心修复：直接重写官方原生的战斗内改费钩子 TryModifyEnergyCostInCombat！
    // 1. 图鉴界面不会调用此方法，且加了 card.IsMutable 保护，绝不会报 CanonicalModelException！
    // 2. 战斗中无论是复制、生成还是消耗诅咒牌，引擎每次读取费用都会实时计算并自动显示绿字！
    // =========================================================================
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;

        // 仅对这张牌自身生效，且必须是非图鉴的可变实例 (IsMutable)
        if (card != this || !IsMutable || Owner == null)
            return false;

        int curseCount = CountCursesInActivePiles(Owner);
        if (curseCount <= 0)
            return false;

        modifiedCost = Math.Max(0m, originalCost - curseCount);
        return true;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var creature = Owner!.Creature;
        decimal amount = DynamicVars["TCSPandemoniumPower"].BaseValue;

        await PowerCmd.Apply<TCSPandemoniumPower>(
            choiceContext,
            creature,
            amount,
            creature,
            this
        );
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}