using ALPHANET.Characters;
using ALPHANET.Powers;
using MegaCrit.Sts2.Core.CardSelection;
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
public sealed class MasterPlan : ModCardTemplate
{
    public MasterPlan() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    // 显式声明该卡牌可获得格挡
    public override bool GainsBlock => true;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Retain,
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(6m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<NoDrawPower>(),
        HoverTipFactory.FromPower<MasterPlanPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;
        var creature = player.Creature;

        // 1. 获得 6 (升级后 8) 点格挡
        await CreatureCmd.GainBlock(creature, DynamicVars.Block, cardPlay);

        // 2. 选择 1 张手牌放到抽牌堆底端
        var handPile = PileType.Hand.GetPile(player);
        if (handPile.Cards.Count == 0)
            return;

        var prefs = new CardSelectorPrefs(SelectionScreenPrompt, 1);
        CardModel? selectedCard = (await CardSelectCmd.FromHand(
            context: choiceContext,
            player: player,
            prefs: prefs,
            filter: null,
            source: this
        )).FirstOrDefault();

        if (selectedCard != null)
        {
            // 将选中的手牌移入抽牌堆底端
            await CardPileCmd.Add(selectedCard, PileType.Draw, CardPilePosition.Bottom);

            // 挂载 MasterPlanPower 并将该卡牌实例登记为“牌底哨兵”
            await PowerCmd.Apply<MasterPlanPower>(choiceContext, creature, 1m, creature, this);
            creature.GetPower<MasterPlanPower>()?.TrackCard(selectedCard);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m); // 6 -> 8
        RemoveKeyword(CardKeyword.Exhaust);   // 升级后移除【消耗】
    }
}