using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Powers;

[RegisterPower]
public sealed class PsychicBackflowPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private const decimal BaseBlockPerStack = 10m;
    private const decimal BaseEnergyPerStack = 1m;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner.Player) return;

        // 判定打出的牌耗能是否 >= 3（兼容 X 费牌以及临时变费后的实际耗能）
        int costAtPlay = cardPlay.Card.EnergyCost != null
            ? Math.Max(0, cardPlay.Card.EnergyCost.GetWithModifiers(CostModifiers.All))
            : 0;

        if (costAtPlay >= 3)
        {
            Flash();
            decimal totalBlock = BaseBlockPerStack * Amount;
            decimal totalEnergy = BaseEnergyPerStack * Amount;

            // 1. 获得 10 * 层数 格挡
            await CreatureCmd.GainBlock(Owner, totalBlock, ValueProp.Unpowered, cardPlay);

            // 2. 下回合获得 1 * 层数 能量
            await PowerCmd.Apply<EnergyNextTurnPower>(choiceContext, Owner, totalEnergy, Owner, null);
        }
    }
}

