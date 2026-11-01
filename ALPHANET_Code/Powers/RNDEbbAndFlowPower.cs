using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
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

    // 记账本，记录当前这一瞬间触发了多少次
    private int _pendingTriggers = 0;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(BlockGainKey, 3m)
    ];

    // =========================================================================
    // 步骤 1：监听层数变动。卡牌造成的记账，药水/遗物造成的立刻结算！
    // =========================================================================
    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (amount > 0m &&
            power.GetTypeForAmount(amount) == PowerType.Debuff &&
            power.Owner.IsEnemy &&
            applier == Owner &&
            !(power is ITemporaryPower))
        {
            _pendingTriggers++; 

            // 【核心修复】：如果来源不是卡牌（如药水、遗物），不需要等卡牌打完，直接原地爆发！
            if (cardSource == null)
            {
                await FlushPendingTriggers(choiceContext);
            }
        }
    }

    // =========================================================================
    // 步骤 2：只有卡牌触发的，才在这里一口气把积攒的次数全部兑现！
    // =========================================================================
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await FlushPendingTriggers(choiceContext);
    }

    // 兜底机制：如果是某些极其罕见的机制（回合结束时自动触发虚弱等），在此保底结算
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player)
        {
            await FlushPendingTriggers(choiceContext);
        }
    }

    // =========================================================================
    // 步骤 3：核心统一结算逻辑，将 N 次动画合并为 1 次巨大的动画
    // =========================================================================
    private async Task FlushPendingTriggers(PlayerChoiceContext choiceContext)
    {
        if (_pendingTriggers <= 0) return;

        int triggers = _pendingTriggers;
        _pendingTriggers = 0; // 结算前先清零，防止嵌套死循环

        Flash(); // 不管触发了几次，只闪烁 1 次能力图标！
        var ctx = choiceContext ?? new ThrowingPlayerChoiceContext();

        // 算出合并后的总层数
        decimal totalAmount = Amount * triggers;

        // 一次性获得巨大数值的临时力量和敏捷
        await PowerCmd.Apply<RNDEbbAndFlowTemporaryStrengthPower>(ctx, Owner, totalAmount, Owner, null);
        await PowerCmd.Apply<RNDEbbAndFlowTemporaryDexterityPower>(ctx, Owner, totalAmount, Owner, null);

        // 一次性叠加所有的格挡
        decimal baseBlock = DynamicVars[BlockGainKey].BaseValue;
        if (baseBlock > 0m)
        {
            await CreatureCmd.GainBlock(Owner, totalAmount * baseBlock, ValueProp.Unpowered, null);
        }
    }
}