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
        CardKeyword.Ethereal,
        ALPHANETKeywords.Miracle
    ];

    protected override HashSet<CardTag> CanonicalTags =>
    [
        ALPHANETTags.Miracle
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(2m, ValueProp.Unpowered)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;
        var creature = player.Creature;

        // 复制品 A：在附魔触发之前生成
        CardModel card = CreateClone();
        CardCmd.PreviewCardPileAdd(
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Discard, player),
            1f
        );

        // 给自己叠加能力
        await PowerCmd.Apply<BrkThroughPower>(
            choiceContext,
            creature,
            DynamicVars.Block.BaseValue,
            creature,
            this
        );
    }

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        // 确保是这张牌自己的出牌事件
        if (cardPlay.Card != this)
            return;

        // 复制品 B：在附魔触发之后生成
        CardModel card = CreateClone();
        CardCmd.PreviewCardPileAdd(
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Discard, Owner!),
            1f
        );
    }

    protected override void OnUpgrade()
    {
        // 升级后，每回合给的格挡变成 3
        DynamicVars.Block.UpgradeValueBy(1m);
    }
}