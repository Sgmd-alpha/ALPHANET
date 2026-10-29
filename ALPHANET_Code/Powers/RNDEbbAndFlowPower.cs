using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Powers;

[RegisterPower]
public sealed class RNDEbbAndFlowPower : ModPowerTemplate
{
    private const string BlockGainKey = "BlockGain";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter; // 允许多次打出叠加层数

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(BlockGainKey, 3m) // 基础提供 3 点格挡，当打出升级版卡牌时会动态更新此变量
    ];

    // =========================================================================
    // 核心机制：监听场上任何能力的层数变动，筛选出“玩家对敌人施加负面状态”的动作
    // =========================================================================
    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        // 1. 过滤条件：层数增加 (非 0) + 类型为 Debuff + 目标是敌人 + 施加者是本能力的拥有者 (玩家自身)
        //    并且排除 ITemporaryPower (防止临时力量衰减等机制引发连锁判定)
        if (amount != 0m &&
            power.GetTypeForAmount(amount) == PowerType.Debuff &&
            power.Owner.IsEnemy &&
            applier == Owner &&
            !(power is ITemporaryPower))
        {
            Flash();
            var ctx = choiceContext ?? new ThrowingPlayerChoiceContext();

            // 每 1 层此能力，提供 1 点临时力量和 1 点临时敏捷
            await PowerCmd.Apply<RNDEbbAndFlowTemporaryStrengthPower>(ctx, Owner, Amount, Owner, null);
            await PowerCmd.Apply<RNDEbbAndFlowTemporaryDexterityPower>(ctx, Owner, Amount, Owner, null);
            
            // 每 1 层此能力，提供对应基础数值的格挡
            decimal baseBlock = DynamicVars[BlockGainKey].BaseValue;
            if (baseBlock > 0m)
            {
                await CreatureCmd.GainBlock(Owner, Amount * baseBlock, ValueProp.Unpowered, null);
            }
        }
    }
}