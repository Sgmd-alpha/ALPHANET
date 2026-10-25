using ALPHANET.Cards;
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
public sealed class TacticalReversalPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player) return;

        Flash();
        int count = Amount;
        var cards = new List<CardModel>(count);
        for (int i = 0; i < count; i++)
        {
            cards.Add(Owner.CombatState!.CreateCard<TokenParadox>(player));
        }

        await CardPileCmd.AddGeneratedCardsToCombat(cards, PileType.Hand, player);
        await PowerCmd.Remove(this);
    }
}

[RegisterPower]
public sealed class TacticalReversalUpgradedPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/TacticalReversalPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/TacticalReversalPower.png"
    );

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player) return;

        Flash();
        int count = Amount;
        var cards = new List<CardModel>(count);
        for (int i = 0; i < count; i++)
        {
            CardModel card = Owner.CombatState!.CreateCard<TokenParadox>(player);
            CardCmd.Upgrade(card);
            cards.Add(card);
        }

        await CardPileCmd.AddGeneratedCardsToCombat(cards, PileType.Hand, player);
        await PowerCmd.Remove(this);
    }
}

