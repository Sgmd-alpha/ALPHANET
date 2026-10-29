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

    
    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        var player = Owner!;
        CardPile exhaustPile = PileType.Exhaust.GetPile(player);

        int handCount = PileType.Hand.GetPile(player).Cards.Count;

        // 为生成混沌预留一个手牌位置。
        // 只有混沌加入手牌后仍有空间，才允许选取消耗堆中的牌。
        bool canSelectCard =
            exhaustPile.Cards.Count > 0
            && handCount + 1 < CardPile.MaxCardsInHand;

        CardModel? selectedCard = null;

        if (canSelectCard)
        {
            try
            {
                var selectedCards = await CardSelectCmd.FromCombatPile(
                    choiceContext,
                    exhaustPile,
                    player,
                    new CardSelectorPrefs(SelectionScreenPrompt, 1)
                );

                selectedCard = selectedCards.FirstOrDefault();
            }
            catch (OperationCanceledException)
            {
                // 选牌被取消时，不继续执行这张牌的剩余效果。
                // 此时尚未生成混沌，避免只执行一半效果。
                return;
            }
        }

        // 1. 生成一张混沌并加入手牌。
        CardModel chaosCard = CombatState!.CreateCard<StatusChaos>(player);

        await CardPileCmd.AddGeneratedCardsToCombat(
            [chaosCard],
            PileType.Hand,
            player
        );

        // 2. 如果成功选中了消耗堆中的牌，将其加入手牌并降低费用。
        if (selectedCard != null)
        {
            await CardPileCmd.Add(selectedCard, PileType.Hand);

            selectedCard.EnergyCost.AddUntilPlayed(-1);
        }
    }


    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1); // 1 -> 0
    }
}