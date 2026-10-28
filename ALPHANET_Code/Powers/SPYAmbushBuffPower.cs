using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Powers;

[RegisterPower]
public sealed class SPYAmbushBuffPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter; // 不可叠加，通过 ModifyAmount 设定总数值

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/SPYAmbushBuffPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/SPYAmbushBuffPower.png"
    );

    // 独立乘区增伤：若本回合尚未打出攻击牌，则提供独立乘区加成
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (!props.IsPoweredAttack()) return 1m;
        if (cardSource == null || cardSource.Owner.Creature != Owner) return 1m;

        // 检查本回合是否已经打出过攻击牌（如果已经打出过，本回合临时清除增伤效果）
        int attackCountThisTurn = CombatManager.Instance.History.CardPlaysStarted.Count(e =>
            e.HappenedThisTurn(CombatState) &&
            e.CardPlay.Player == Owner.Player &&
            e.CardPlay.Card.Type == CardType.Attack);

        // 如果本回合已经出了攻击牌，当前攻击不享受暗度陈仓加成
        if (attackCountThisTurn > 0)
        {
            return 1m;
        }

        // 独立乘区：Amount 代表当前总百分比（如 25 或 50），转换为乘数 (1 + Amount / 100)
        return 1m + Amount / 100m;
    }

    // 每一张新卡牌出牌开始时检查：若刚刚打出了攻击牌，可选择让增伤在本回合失效（通过动态计算或在回合结束清理）
    // 此处由于我们在 ModifyDamageMultiplicative 实时检查了 attackCountThisTurn > 0 则返回 1m，因此完美实现了“打出攻击牌本回合临时清除”的效果。
}