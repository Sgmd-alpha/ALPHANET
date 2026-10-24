using ALPHANET.Characters;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class BlindSwap : ModCardTemplate
{
    public BlindSwap() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 抽牌数量：2 (升级后 3)
        new CardsVar(2)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromCard<StatusChaos>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;

        // 1. 先丢弃手牌中的 1 张牌
        var selectedToDiscard = await CardSelectCmd.FromHandForDiscard(
            context: choiceContext,
            player: player,
            prefs: new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, 1),
            filter: null,
            source: this
        );
        await CardCmd.Discard(choiceContext, selectedToDiscard);

        // 2. 再抽 2 (升级后 3) 张牌
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, player);

        // 3. 最后将一张“混沌”加入手牌
        CardModel chaosCard = CombatState!.CreateCard<StatusChaos>(player);
        await CardPileCmd.AddGeneratedCardsToCombat([ chaosCard ], PileType.Hand, player);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m); // 2 -> 3
    }
}