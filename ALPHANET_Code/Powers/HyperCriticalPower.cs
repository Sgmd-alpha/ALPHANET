using ALPHANET.States;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
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
/// <summary>
/// 超级临界（组件 2）：临界状态下额外具有 Amount 点力量和敏捷
/// </summary>
[RegisterPower]
public sealed class HyperCriticalPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private decimal _grantedBonus = 0m;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );

    // 【核心修改 1】：改为动态获取，为图鉴假人兜底默认值
    protected override IEnumerable<DynamicVar> CanonicalVars
    {
        get
        {
            // Owner == null 说明是在图鉴看牌；Amount <= 0 说明还没正式生效
            decimal displayEntry = (Owner == null) ? 1m : (Owner.GetPower<HyperCriticalEnterPower>()?.Amount ?? 0m);
            decimal displayAmount = (Owner == null || Amount <= 0m) ? 1m : Amount;

            return
            [
                new IntVar("EntryAmount", displayEntry),
                new IntVar("Amount", displayAmount) // 强制声明 Amount，防止底层报乱码
            ];
        }
    }

    // 【核心修改 2】：统一的文本变量同步方法
    private void SyncVarsForDescription()
    {
        if (Owner == null) return;

        int entryAmount = Owner.GetPower<HyperCriticalEnterPower>()?.Amount ?? 0;
        DynamicVars["EntryAmount"].BaseValue = entryAmount;
        DynamicVars["Amount"].BaseValue = Amount; // 同步当前真实层数
    }

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
        SyncVarsForDescription(); // 附加时同步文本
    }

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power.Owner != Owner) return;

        if (power is HyperCriticalEnterPower)
        {
            SyncVarsForDescription(); // 组件1层数变化，同步文本
            return;
        }

        if (power is CriticalState || power == this)
        {
            await SyncCriticalBonusAsync();
            
            if (power == this)
            {
                SyncVarsForDescription(); // 自身层数变化，同步文本
            }
        }
    }

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