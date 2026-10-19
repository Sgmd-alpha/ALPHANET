using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(TokenCardPool))]
public sealed class TokenFuture : ModCardTemplate
{
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => 
    [ 
        CardKeyword.Ethereal, 
        CardKeyword.Exhaust 
    ];

    // 继续保持优良传统：不用 Magic，用清晰的显式变量
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("CardSelectCount", 2)
    ];

    // 简化构造函数，直接填入参数，代码更清爽
    public TokenFuture() : base(2, CardType.Skill, CardRarity.Token, TargetType.Self, true)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var drawPile = PileType.Draw.GetPile(Owner);
        
        if (drawPile.Cards.Count == 0)
        {
            return; 
        }

        int selectCount = DynamicVars.GetIntOrDefault("CardSelectCount", 2);
        int maxSelectable = Math.Min(selectCount, CardPile.MaxCardsInHand - PileType.Hand.GetPile(Owner).Cards.Count);

        if (maxSelectable > 0)
        {
            var selectedCards = await CardSelectCmd.FromCombatPile(
                choiceContext, 
                drawPile, 
                Owner, 
                new CardSelectorPrefs(SelectionScreenPrompt, 0, maxSelectable)
            );

            if (selectedCards != null && selectedCards.Any())
            {
                await CardPileCmd.Add(selectedCards, PileType.Hand);

                foreach (var card in selectedCards)
                {
                    card.EnergyCost.AddUntilPlayed(-1);
                }
            }
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}