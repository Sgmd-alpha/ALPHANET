using ALPHANET.States;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Powers;

/// <summary>
/// 超级临界（组件 1）：每次进入临界状态时获得 1 点永久力量
/// </summary>
[RegisterPower]
public sealed class HyperCriticalEnterPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;
    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/HyperCriticalPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/HyperCriticalPower.png"
    );

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)

    {
        // 每当向自身施加 CriticalState（无论是从无到有进入，还是在临界状态下重复触发进入），均获得 Amount 点永久力量
        if (power is CriticalState && power.Owner == Owner && amount > 0m)
        {
            Flash();
            var ctx = new ThrowingPlayerChoiceContext();
            await PowerCmd.Apply<StrengthPower>(ctx, Owner, Amount, Owner, null);
        }
    }
}

/// <summary>
/// 超级临界（组件 2）：临界状态下额外具有 Amount 点力量和敏捷（有借有还账本，支持多层叠加与中途打出）
/// </summary>
[RegisterPower]
public sealed class HyperCriticalPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    // 记录当前已经实际借给玩家的临时力量/敏捷层数，做到精准“多退少补”
    private decimal _grantedBonus = 0m;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );

    private async Task SyncCriticalBonusAsync()
    {
        if (Owner == null || !Owner.IsAlive) return;

        bool inCritical = Owner.HasPower<CriticalState>();
        decimal targetBonus = inCritical ? Amount : 0m;
        decimal delta = targetBonus - _grantedBonus;

        if (delta != 0m)
        {
            _grantedBonus = targetBonus;
            Flash();
            var ctx = new ThrowingPlayerChoiceContext();
            await PowerCmd.Apply<StrengthPower>(ctx, Owner, delta, Owner, null);
            await PowerCmd.Apply<DexterityPower>(ctx, Owner, delta, Owner, null);
        }
    }

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        await SyncCriticalBonusAsync();
    }

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power.Owner == Owner && (power is CriticalState || power == this))
        {
            await SyncCriticalBonusAsync();
        }
    }

    // 当退出临界状态时，CriticalState 被移除，触发手牌/状态栏同步归还力量与敏捷
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await SyncCriticalBonusAsync();
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        if (_grantedBonus != 0m && oldOwner != null && oldOwner.IsAlive)
        {
            decimal toRemove = _grantedBonus;
            _grantedBonus = 0m;
            var ctx = new ThrowingPlayerChoiceContext();
            await PowerCmd.Apply<StrengthPower>(ctx, oldOwner, -toRemove, oldOwner, null);
            await PowerCmd.Apply<DexterityPower>(ctx, oldOwner, -toRemove, oldOwner, null);
        }
    }
}

