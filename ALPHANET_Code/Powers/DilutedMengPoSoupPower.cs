using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Powers;

[RegisterPower]
public sealed class DilutedMengPoSoupPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    // 隐藏能力，玩家看不见，完全由系统后台调度额外回合
    protected override bool IsVisibleInternal => false;

    public override bool ShouldTakeExtraTurn(Player player)
    {
        if (Amount > 0)
        {
            // 确保只有被丢药水的目标玩家（Owner）才能获得这个额外回合
            return player == Owner.Player;
        }
        return false;
    }

    public override async Task AfterTakingExtraTurn(Player player)
    {
        if (player == Owner.Player)
        {
            await PowerCmd.Decrement(this);
        }
    }
}