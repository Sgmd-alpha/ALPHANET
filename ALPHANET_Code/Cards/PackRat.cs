using ALPHANET.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

//  这是一个已被移除的能力牌
//  "ALPHANET_CARD_PACK_RAT.title": "惜物成癖",
//  "ALPHANET_CARD_PACK_RAT.description": "你的回合结束时，[gold]保留[/gold]你[gold]手牌[/gold]中的所有能力牌，并使它们的耗能-1。",

public sealed class PackRat : ModCardTemplate
{
    public PackRat() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<PackRatPower>(1m)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Retain),
        HoverTipFactory.FromPower<PackRatPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var creature = Owner!.Creature;
        await PowerCmd.Apply<PackRatPower>(
            choiceContext,
            creature,
            DynamicVars["PackRatPower"].BaseValue,
            creature,
            this
        );
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate); // 升级后获得【固有】
    }
}