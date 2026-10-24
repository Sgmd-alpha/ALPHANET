using ALPHANET.Keywords;
using ALPHANET.Tags;
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
public sealed class TokenMemories : ModCardTemplate
{
    public TokenMemories() : base(2, CardType.Skill, CardRarity.Token, TargetType.Self, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    // 奇迹、保留、消耗（MiracleSystem 会自动接管透支扣血与红/金框高亮）
    public override IEnumerable<CardKeyword> CanonicalKeywords => 
    [ 
        ALPHANETKeywords.Miracle, 
        CardKeyword.Retain,
        CardKeyword.Exhaust 
    ];

    protected override HashSet<CardTag> CanonicalTags => 
    [
        ALPHANETTags.Miracle
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("CardSelectCount", 2)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;
        var discardPile = PileType.Discard.GetPile(player);
        
        if (discardPile.Cards.Count == 0)
        {
            return; 
        }

        int selectCount = DynamicVars.GetIntOrDefault("CardSelectCount", 2);
        int maxSelectable = Math.Min(selectCount, CardPile.MaxCardsInHand - PileType.Hand.GetPile(player).Cards.Count);

        if (maxSelectable > 0)
        {
            var selectedCards = (await CardSelectCmd.FromCombatPile(
                choiceContext, 
                discardPile, 
                player, 
                new CardSelectorPrefs(SelectionScreenPrompt, 0, maxSelectable)
            )).ToList();

            if (selectedCards.Count > 0)
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
        EnergyCost.UpgradeBy(-1); // 2 -> 1
    }
}