using ALPHANET.Characters;
using ALPHANET.Keywords;
using ALPHANET.Tags;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class MiracleStrike : ModCardTemplate
{
    public MiracleStrike() : base(3, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies, true)
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
        ALPHANETTags.Miracle,
        CardTag.Strike
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(12m, ValueProp.Move),
        new CardsVar(1)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Retain)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;
        decimal damage = DynamicVars.Damage.BaseValue;

        // 1. 对所有敌人造成 12 (升级后 14) 点伤害 2 次
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(2)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState!)
            .Execute(choiceContext);

        // 2. 抽 1 (升级后 2) 张牌并使其获得保留
        int drawCount = DynamicVars.Cards.IntValue;
        if (drawCount > 0)
        {
            IEnumerable<CardModel> drawnCards = await CardPileCmd.Draw(choiceContext, drawCount, player);
            foreach (CardModel card in drawnCards)
            {
                CardCmd.ApplySingleTurnRetain(card);
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m); // 12 -> 14
        DynamicVars.Cards.UpgradeValueBy(1m);  // 1 -> 2
    }
}