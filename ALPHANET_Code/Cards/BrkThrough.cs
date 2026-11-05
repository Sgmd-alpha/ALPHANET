using ALPHANET.Characters;
using ALPHANET.Keywords;
using ALPHANET.Powers;
using ALPHANET.Tags;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class BrkThrough : ModCardTemplate
{
    public BrkThrough() : base(0, CardType.Power, CardRarity.Uncommon, TargetType.Self, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        ALPHANETKeywords.Miracle
    ];

    protected override HashSet<CardTag> CanonicalTags =>
    [
        ALPHANETTags.Miracle
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(2m, ValueProp.Move)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;
        var creature = player.Creature;

        // 将2张此牌的复制品加入弃牌堆
        for (int i = 0; i < 2; i++)
        {
            CardModel card = CreateClone();
            CardCmd.PreviewCardPileAdd(await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Discard, Owner), 1.2f);
        }
        
        //给自己叠加能力
        await PowerCmd.Apply<BrkThroughPower>(choiceContext, creature, DynamicVars.Block.BaseValue, creature, this);
    }

    protected override void OnUpgrade()
    {
        // 升级后，每回合给的格挡变成 3
        DynamicVars.Block.UpgradeValueBy(1m);
    }
}