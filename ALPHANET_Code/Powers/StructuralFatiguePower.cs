using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Powers;

[RegisterPower]
public sealed class StructuralFatiguePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    // 记录本回合内攻击过玩家的敌人集合（HashSet 天然去重，多段攻击也只记录 1 次）
    private readonly HashSet<Creature> _attackersThisTurn = new();

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );

    // 1. 监听受击事件：无论是否破甲（即便被格挡完全挡住），只要是敌人发起的攻击，就将其记入本回合攻击者集合
    public override Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target == Owner &&
            dealer != null &&
            dealer != Owner &&
            dealer.Side != Owner.Side &&
            props.HasFlag(ValueProp.Move))
        {
            // 若该敌人是本回合首次攻击你，闪烁一下图标给玩家视觉反馈
            if (_attackersThisTurn.Add(dealer))
            {
                Flash();
            }
        }

        return Task.CompletedTask;
    }

    // 2. 在敌方回合结束时，令所有本回合攻击过你的存活敌人失去 Amount 点力量
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        await base.AfterSideTurnEnd(choiceContext, side, participants);

        // 当敌方回合结束（或者任何发生过敌人攻击的回合结束）时结算
        if (_attackersThisTurn.Count == 0)
            return;

        // 仅在敌方回合结束时统一扣除（若存在友方回合反击/特殊触发，也会一并清算）
        if (side != Owner.Side)
        {
            List<Creature> attackers = _attackersThisTurn.Where(e => e != null && e.IsAlive).ToList();
            _attackersThisTurn.Clear();

            if (attackers.Count > 0)
            {
                Flash();
                foreach (Creature enemy in attackers)
                {
                    await PowerCmd.Apply<StrengthPower>(choiceContext, enemy, -Amount, Owner, null);
                }
            }
        }
    }
}