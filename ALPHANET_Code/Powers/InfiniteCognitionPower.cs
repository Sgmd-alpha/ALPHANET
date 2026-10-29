using ALPHANET.States;
using ALPHANET.Systems;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Powers;

[RegisterPower]
public sealed class InfiniteCognitionPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    // 记录本回合是否已经触发过“退出临界状态”
    private bool _triggeredThisTurn = false;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<CriticalState>()
    ];

    // 每回合开始时重置“每回合一次”的触发锁
    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == Owner.Side)
        {
            _triggeredThisTurn = false;
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 供 CriticalState.AfterRemoved 在退出临界状态时直接调用
    /// </summary>
    public async Task OnExitCritical(Creature creature)
    {
        if (creature != Owner || !Owner.IsAlive || _triggeredThisTurn || Amount <= 0m)
            return;

        _triggeredThisTurn = true;
        Flash();

        var ctx = new ThrowingPlayerChoiceContext();
        await PowerCmd.Apply<InfiniteCognitionEchoPower>(ctx, Owner, Amount, Owner, null);
    }
}

/// <summary>
/// 退出临界状态后激活的“下 N 张牌额外打出一次”状态（与回响形态/双发叠加逻辑一致）
/// </summary>
[RegisterPower]
public sealed class InfiniteCognitionEchoPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    // 防回溯锁：记录这个状态刚被创建时，场上正在打出的卡牌是什么。
    // 如果当前正在结算的就是这张牌，直接免疫，绝不让它双发！
    private CardModel? _creatorCard = null;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/InfiniteCognitionPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/InfiniteCognitionPower.png"
    );

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        // 记录是谁（或者当前正在打哪张牌时）触发了本状态的创建
        // 通过CombatManager或当前正在结算的出牌队列锁定罪魁祸首
        if (CombatManager.Instance?.IsInProgress == true)
        {
            // 尝试获取当前正在触发结算的出牌
            var currentPlay = CombatManager.Instance.History.CardPlaysStarted.LastOrDefault(e => e.Actor == Owner && e.HappenedThisTurn(CombatState));
            _creatorCard = currentPlay?.CardPlay?.Card;
        }

        CardCriticalBadgeSystem.RefreshAllVisibleCardBadges();
        return Task.CompletedTask;
    }

    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        CardCriticalBadgeSystem.RefreshAllVisibleCardBadges();
        return Task.CompletedTask;
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        CardCriticalBadgeSystem.RefreshAllVisibleCardBadges();
        return Task.CompletedTask;
    }

    // =========================================================================
    // 核心：每张受影响的牌固定额外打出 1 次，但绝对排除那张“导致退出的罪魁祸首卡牌”！
    // =========================================================================
    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
    {
        if (card.Owner?.Creature != Owner || Amount <= 0m)
            return playCount;

        // 如果这张牌就是触发退出临界状态的那张牌，跳过双发！
        if (_creatorCard != null && card == _creatorCard)
        {
            return playCount;
        }

        return playCount + 1;
    }

    public override Task AfterModifyingCardPlayCount(CardModel card)
    {
        // 如果是触发卡，不闪烁也不算
        if (_creatorCard != null && card == _creatorCard)
            return Task.CompletedTask;

        Flash();
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay?.Card == null || cardPlay.Card.Owner?.Creature != Owner)
            return;

        // 如果是那张导致退出的罪魁祸首卡牌，它在打完后我们要顺便把 _creatorCard 清空，
        // 这样它的下一次出牌或者真正意义上的“下一张牌”就能正常享受回响了。
        if (_creatorCard != null && cardPlay.Card == _creatorCard)
        {
            if (cardPlay.IsLastInSeries)
            {
                _creatorCard = null;
            }
            return;
        }

        // 当正常的“下一张牌”完成了它的整串连击时，扣除 1 层回响！
        if (cardPlay.IsLastInSeries)
        {
            if (Amount > 1m)
            {
                await PowerCmd.Decrement(this);
            }
            else
            {
                await PowerCmd.Remove(this);
            }
            CardCriticalBadgeSystem.RefreshAllVisibleCardBadges();
        }
    }

    public bool ShouldShowInfinityBadgeOn(CardModel card)
    {
        if (!card.IsMutable || card.Owner?.Creature != Owner || card.Pile?.Type != PileType.Hand)
            return false;

        // 如果当前这张手牌恰好就是那张正在退出的触发卡，先不给它亮 ∞ 徽章
        if (_creatorCard != null && card == _creatorCard)
            return false;

        return Amount > 0m;
    }
}