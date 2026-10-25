using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Powers;

[RegisterPower]
public sealed class PriceyIsRightPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );

    private static int GetActualCost(CardModel card)
    {
        if (card.EnergyCost == null) return -1;
        return Math.Max(0, card.EnergyCost.GetWithModifiers(CostModifiers.All));
    }

    public override async Task AfterAutoPostPlayPhaseEntered(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player)
        {
            return;
        }

        CardPile hand = PileType.Hand.GetPile(player);

        for (int i = 0; i < (int)Amount; i++)
        {
            // 1. 筛选手牌中可打出、且当前耗能高于 1 (>= 2) 的牌
            var validCards = hand.Cards
                .Where(c => c != null 
                         && (c.Keywords == null || !c.Keywords.Contains(CardKeyword.Unplayable))
                         && GetActualCost(c) > 1)
                .ToList();

            if (validCards.Count == 0)
            {
                break;
            }

            // 2. 找出最高耗能，并在并列最高耗能的牌中随机抽取一张
            int maxCost = validCards.Max(GetActualCost);
            List<CardModel> highestCostCards = validCards
                .Where(c => GetActualCost(c) == maxCost)
                .ToList();

            CardModel? cardToPlay = player.RunState.Rng.Shuffle.NextItem(highestCostCards);
            if (cardToPlay != null)
            {
                Flash();
                await CardCmd.AutoPlay(choiceContext, cardToPlay, null);
            }
        }
    }
}