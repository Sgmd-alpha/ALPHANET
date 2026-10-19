using ALPHANET.Keywords;
using ALPHANET.Systems;
using ALPHANET.Tags;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;


namespace ALPHANET.Cards;
[RegisterCard(typeof(TokenCardPool))]
public sealed class TokenStasis : ModCardTemplate
{
    public TokenStasis() : base(1, CardType.Skill, CardRarity.Token, TargetType.Self, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );
    public override IEnumerable<CardKeyword> CanonicalKeywords => 
    [ 
        ALPHANETKeywords.Miracle,   // 奇迹
        CardKeyword.Retain,         // 保留
        CardKeyword.Exhaust         // 消耗
    ];
    protected override HashSet<CardTag> CanonicalTags => 
    [
        ALPHANETTags.Miracle
    ];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;
        await MiracleSystem.PayMiracleCostIfNeeded(this, choiceContext, cardPlay);
        List<CardModel> generatedCards =
        [
            player.Creature.CombatState!.CreateCard<StatusChaos>(player)
        ];
        await CardPileCmd.AddGeneratedCardsToCombat(generatedCards, PileType.Hand, player);

        CardPile exhaustPile = PileType.Exhaust.GetPile(player);
        if (exhaustPile.Cards.Count > 0)
        {
            var prefs = new CardSelectorPrefs(SelectionScreenPrompt, 1);
            
            var selectedCards = await CardSelectCmd.FromCombatPile(
                choiceContext, 
                exhaustPile, 
                player, 
                prefs
            );
            CardModel? selectedCard = selectedCards.FirstOrDefault();

            if (selectedCard != null)
            {
                await CardPileCmd.Add(selectedCard, PileType.Hand);

                selectedCard.EnergyCost.AddUntilPlayed(-1);
            }
        }
    }
    protected override void OnUpgrade()
    {
        // 耗能 -1
        EnergyCost.UpgradeBy(-1);
    }
}