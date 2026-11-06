using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Powers;

[RegisterPower]
public sealed class SPYAmbushPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter; // 层数代表百分比（如 25、50）

    // 仿照 ArtOfWar 的双状态追踪
    private bool _anyAttacksPlayedThisTurn = false;
    private bool _isBuffActiveThisTurn = false; // 上一回合若未出攻击牌，本回合全程激活增伤

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/SPYAmbushPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/SPYAmbushPower.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars
    {
        get
        {
            decimal displayVal = (Owner == null || Amount <= 0m) ? 25m : Amount;
            return [ new DynamicVar("Amount", displayVal) ];
        }
    }

    // 1. 打出能力牌挂上时：判断本回合是否已经打出过攻击牌，若没有，立刻赋予 SPYAmbushBuffPower
    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        await base.AfterApplied(applier, cardSource);
        DynamicVars["Amount"].BaseValue = Amount;

        if (Owner?.Player != null && CombatState != null)
        {
            _anyAttacksPlayedThisTurn = CombatManager.Instance.History.CardPlaysStarted.Any(e =>
                e.HappenedThisTurn(CombatState) &&
                e.CardPlay.Player == Owner.Player &&
                e.CardPlay.Card.Type == CardType.Attack);

            if (!_anyAttacksPlayedThisTurn && Amount > 0m)
            {
                var ctx = new ThrowingPlayerChoiceContext();
                await SyncNextTurnBuffAsync(ctx);
            }
        }
    }

    // 当打出第二张《暗度陈仓》叠加层数时，同步刷新 SPYAmbushBuffPower 的数值
    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power == this)
        {
            DynamicVars["Amount"].BaseValue = Amount;
            if (!_anyAttacksPlayedThisTurn && Amount > 0m)
            {
                await SyncNextTurnBuffAsync(choiceContext);
            }
        }
    }

    // 2. 监听出牌：只要打出攻击牌，标记本回合已出过攻击牌（SPYAmbushBuffPower 也会自己监听并移除自己）
    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner != null && cardPlay.Card.Owner == Owner.Player && cardPlay.Card.Type == CardType.Attack)
        {
            _anyAttacksPlayedThisTurn = true;
        }
        return Task.CompletedTask;
    }

    // 3. 回合结束时（仿照 ArtOfWar.AfterSideTurnEnd）：结算上一回合是否未出攻击牌
    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner != null && participants.Contains(Owner))
        {
            // 如果本回合没有打过攻击牌（或者身上依然保有 SPYAmbushBuffPower），则下一回合激活实际增伤！
            _isBuffActiveThisTurn = !_anyAttacksPlayedThisTurn;
            _anyAttacksPlayedThisTurn = false;
        }
        return Task.CompletedTask;
    }

    // 4. 新回合开始时：重置新一轮的 SPYAmbushBuffPower 预告状态
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        await base.AfterPlayerTurnStart(choiceContext, player);

        if (player != Owner?.Player) return;

        // 如果上一回合忍住没出攻击牌，本回合初闪光提示增伤已生效！
        if (_isBuffActiveThisTurn)
        {
            Flash();
        }

        // 新回合刚开始，尚未打出攻击牌，为玩家挂上/刷新“下回合攻击伤害提升”的 Buff 状态
        _anyAttacksPlayedThisTurn = false;
        if (Amount > 0m)
        {
            await SyncNextTurnBuffAsync(choiceContext);
        }
    }

    // 5. 实际独立乘区增伤：只要上一回合未出攻击牌（_isBuffActiveThisTurn 为 true），本回合所有攻击享受加成
    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (!_isBuffActiveThisTurn) return 1m;
        if (!props.IsPoweredAttack()) return 1m;
        if (dealer != Owner) return 1m;

        return 1m + (Amount / 100m);
    }

    private async Task SyncNextTurnBuffAsync(PlayerChoiceContext choiceContext)
    {
        if (Owner == null) return;

        var existingBuff = Owner.GetPower<SPYAmbushBuffPower>();
        if (existingBuff == null)
        {
            await PowerCmd.Apply<SPYAmbushBuffPower>(choiceContext, Owner, Amount, Owner, null);
        }
        else
        {
            decimal diff = Amount - existingBuff.Amount;
            if (diff != 0m)
            {
                await PowerCmd.ModifyAmount(choiceContext, existingBuff, diff, Owner, null);
            }
        }
    }
}