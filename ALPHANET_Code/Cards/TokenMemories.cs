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
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    // 奇迹、保留、消耗
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

    // 选牌
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("CardSelectCount", 2)
    ];

    // 基础耗能 2
    public TokenMemories() : base(2, CardType.Skill, CardRarity.Token, TargetType.Self, true)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 获取弃牌堆
        var discardPile = PileType.Discard.GetPile(Owner);
        
        if (discardPile.Cards.Count == 0)
        {
            return; 
        }

        int selectCount = DynamicVars.GetIntOrDefault("CardSelectCount", 2);
        int maxSelectable = Math.Min(selectCount, CardPile.MaxCardsInHand - PileType.Hand.GetPile(Owner).Cards.Count);

        if (maxSelectable > 0)
        {
            var selectedCards = await CardSelectCmd.FromCombatPile(
                choiceContext, 
                discardPile, // 从弃牌堆打开选牌界面
                Owner, 
                new CardSelectorPrefs(SelectionScreenPrompt, 0, maxSelectable)
            );

            if (selectedCards != null && selectedCards.Any())
            {
                // 移入手牌
                await CardPileCmd.Add(selectedCards, PileType.Hand);

                foreach (var card in selectedCards)
                {
                    card.EnergyCost.AddThisTurn(-1);
                }
            }
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}