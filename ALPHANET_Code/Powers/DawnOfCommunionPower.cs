using ALPHANET.States;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Powers;

[RegisterPower]
public sealed class DawnOfCommunionPower : ModPowerTemplate
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
        int count = (int)Amount;

        // 1. 抽 1 * Amount 张牌
        await CardPileCmd.Draw(choiceContext, count, player);

        // 2. 获得 1 * Amount 点能量
        await PlayerCmd.GainEnergy(count, player);

        // 3. 如果你不处于临界状态，则进入临界状态
        if (!Owner.HasPower<CriticalState>())
        {
            await CriticalState.Enter(choiceContext, Owner, Owner, null, false);
        }
    }
}