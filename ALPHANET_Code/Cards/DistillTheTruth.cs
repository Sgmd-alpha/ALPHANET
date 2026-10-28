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
public sealed class DistillTheTruth : ModCardTemplate
{
    public DistillTheTruth() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(3)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;
        var handPile = PileType.Hand.GetPile(player);

        // 1. 抽 3 (升级后 4) 张牌
        int drawCount = DynamicVars.Cards.IntValue;
        if (drawCount > 0)
        {
            await CardPileCmd.Draw(choiceContext, drawCount, player);
        }

        // 2. 如果手牌中有牌，要求玩家选择 1 张牌丢弃
        if (handPile.Cards.Count > 0)
        {
            var discardPrefs = new CardSelectorPrefs(SelectionScreenPrompt, 1);
            CardModel? cardToDiscard = (await CardSelectCmd.FromHand(
                context: choiceContext,
                player: player,
                prefs: discardPrefs,
                filter: null,
                source: this
            )).FirstOrDefault();

            if (cardToDiscard != null)
            {
                await CardCmd.Discard(choiceContext, cardToDiscard);
            }
        }

        // 3. 随后消耗所有剩余的手牌（建立快照遍历，防止遍历时集合变动报错）
        List<CardModel> remainingHandCards = handPile.Cards.Where(c => c != null).ToList();
        foreach (CardModel card in remainingHandCards)
        {
            await CardCmd.Exhaust(choiceContext, card);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m); // 3 -> 4
    }
}