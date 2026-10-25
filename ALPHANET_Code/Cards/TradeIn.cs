using ALPHANET.Characters;
using ALPHANET.States;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class TradeIn : ModCardTemplate, ICriticalIndicatorCard
{
    public TradeIn() : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self, true)
    {
    }
    
    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    // 固定退出临界状态 -> 显示白色圆圈
    public CriticalIndicatorType GetCriticalIndicator() => CriticalIndicatorType.Exit;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(13m, ValueProp.Move),
        new CardsVar(2)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<CriticalState>()
    ];

    // 处于临界状态时边框发金光提示
    protected override bool ShouldGlowGoldInternal => Owner?.Creature?.HasPower<CriticalState>() ?? false;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;
        var creature = player.Creature;

        // 1. 丢弃所有手牌（先 ToList 拷贝快照，避免遍历时修改集合报错）
        List<CardModel> handCards = PileType.Hand.GetPile(player).Cards.ToList();
        if (handCards.Count > 0)
        {
            await CardCmd.Discard(choiceContext, handCards);
        }

        // 2. 退出临界状态
        var criticalState = creature.GetPower<CriticalState>();
        if (criticalState != null)
        {
            await PowerCmd.Remove(criticalState);
        }

        // 3. 获得 13 (升级后 17) 点格挡
        await CreatureCmd.GainBlock(creature, DynamicVars.Block, cardPlay);

        // 4. 下回合开始时，额外抽 2 张牌
        await PowerCmd.Apply<DrawCardsNextTurnPower>(choiceContext, creature, DynamicVars.Cards.BaseValue, creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(4m); // 13 -> 17
    }
}