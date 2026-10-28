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

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        // 只响应自己玩家打出的牌
        if (cardPlay.Card.Owner.Creature != Owner)
            return;

        // 判定打出的牌实际耗能是否 >= 3
        // 使用官方示例中的 GetResolved()。
        if (cardPlay.Card.EnergyCost.GetResolved() < 3)
            return;

        Flash();

        decimal totalBlock = BaseBlockPerStack * Amount;
        decimal totalEnergy = BaseEnergyPerStack * Amount;

        // 1. 立即获得 10 * 层数格挡
        await CreatureCmd.GainBlock(Owner, totalBlock, ValueProp.Unpowered, cardPlay);
        // 2. 下回合获得 1 * 层数能量
        await PowerCmd.Apply<EnergyNextTurnPower>(new ThrowingPlayerChoiceContext(), Owner, totalEnergy, Owner, null);
    }
}
