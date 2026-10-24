using ALPHANET.Keywords;
using ALPHANET.Tags;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
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
        ALPHANETKeywords.Miracle, // 奇迹
        CardKeyword.Retain,       // 保留
        CardKeyword.Exhaust       // 消耗
    ];

    protected override HashSet<CardTag> CanonicalTags => 
    [
        ALPHANETTags.Miracle
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromCard<StatusChaos>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;

        // 1. 将一张“混沌”加入手牌
        CardModel chaosCard = CombatState!.CreateCard<StatusChaos>(player);
        await CardPileCmd.AddGeneratedCardsToCombat([ chaosCard ], PileType.Hand, player);

        // 2. 将消耗牌堆里的 1 张牌放入手牌，并使其在被打出前的耗能 -1
        CardPile exhaustPile = PileType.Exhaust.GetPile(player);
        bool hasHandSpace = PileType.Hand.GetPile(player).Cards.Count < CardPile.MaxCardsInHand;

        if (exhaustPile.Cards.Count > 0 && hasHandSpace)
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
        EnergyCost.UpgradeBy(-1); // 1 -> 0
    }
}