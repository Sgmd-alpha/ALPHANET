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
public sealed class TokenFuture : ModCardTemplate
{
    public TokenFuture() : base(2, CardType.Skill, CardRarity.Token, TargetType.Self, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    // ==========================================
    // 补全【奇迹】关键字与标签（MiracleSystem 会自动接管扣血与高亮）
    // ==========================================
    public override IEnumerable<CardKeyword> CanonicalKeywords => 
    [ 
        ALPHANETKeywords.Miracle, // 奇迹
        CardKeyword.Ethereal,     // 虚无
        CardKeyword.Exhaust       // 消耗
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
        var drawPile = PileType.Draw.GetPile(Owner!);
        
        if (drawPile.Cards.Count == 0)
        {
            return; 
        }

        int selectCount = DynamicVars.GetIntOrDefault("CardSelectCount", 2);
        int maxSelectable = Math.Min(selectCount, CardPile.MaxCardsInHand - PileType.Hand.GetPile(Owner!).Cards.Count);

        if (maxSelectable > 0)
        {
            var selectedCards = await CardSelectCmd.FromCombatPile(
                choiceContext, 
                drawPile, 
                Owner!, 
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
        EnergyCost.UpgradeBy(-1); // 2 -> 1
    }
}