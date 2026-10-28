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
public sealed class PackRatPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    // 记录本回合由《惜物成癖》临时赋予“保留”的能力牌，以便在下回合开始时移除临时词条
    private readonly HashSet<CardModel> _temporarilyRetainedCards = new();

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );

    public override Task AfterAutoPostPlayPhaseEntered(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player)
        {
            return Task.CompletedTask;
        }

        CardPile hand = PileType.Hand.GetPile(player);
        List<CardModel> powerCards = hand.Cards
            .Where(c => c != null && c.Type == CardType.Power)
            .ToList();

        if (powerCards.Count == 0)
        {
            return Task.CompletedTask;
        }

        Flash();
        int costReduction = Amount;

        foreach (CardModel card in powerCards)
        {
            // 1. 若该能力牌不具有虚无且尚未拥有保留，赋予保留以确保回合结束不被弃置
            bool hasEthereal = card.Keywords != null && card.Keywords.Contains(CardKeyword.Ethereal);
            bool hasRetain = card.Keywords != null && card.Keywords.Contains(CardKeyword.Retain);

            if (!hasEthereal && !hasRetain)
            {
                CardCmd.ApplyKeyword(card, CardKeyword.Retain);
                _temporarilyRetainedCards.Add(card);
            }

            // 2. 使它们的耗能 -1（若叠了多层《惜物成癖》，则减少对应层数的耗能）
            card.EnergyCost?.AddThisCombat(-costReduction);
        }

        return Task.CompletedTask;
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner.Player && _temporarilyRetainedCards.Count > 0)
        {
            foreach (CardModel card in _temporarilyRetainedCards)
            {
                if (card != null)
                {
                    CardCmd.RemoveKeyword(card, CardKeyword.Retain);
                }
            }
            _temporarilyRetainedCards.Clear();
        }
        return Task.CompletedTask;
    }
}