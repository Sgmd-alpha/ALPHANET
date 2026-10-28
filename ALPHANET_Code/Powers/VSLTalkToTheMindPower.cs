using ALPHANET.States;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Powers;

[RegisterPower]
public sealed class VSLTalkToTheMindPower : ModPowerTemplate
{
    private const string CritBlockKey = "CritBlock";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(CritBlockKey, 4m)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<CriticalState>()
    ];

    // 当层数变化或首次施加时，同步更新悬停描述里的翻倍数值
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        UpdateCritBlockVar();
        return Task.CompletedTask;
    }

    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power == this)
        {
            UpdateCritBlockVar();
        }
        return Task.CompletedTask;
    }

    private void UpdateCritBlockVar()
    {
        if (DynamicVars.TryGetValue(CritBlockKey, out var v))
        {
            v.BaseValue = Amount * 2m;
        }
    }

    // =========================================================================
    // 核心触发：监听全场伤害结算（无论在玩家回合还是敌方回合均会触发！）
    // =========================================================================
    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        // 1. 必须是玩家自身造成的伤害，且目标是敌人
        if (Owner == null || !Owner.IsAlive || dealer != Owner)
            return;

        if (target == null || target.Side == Owner.Side)
            return;

        // 2. 必须是攻击伤害 (IsPoweredAttack 会过滤掉失去生命、荆棘反伤、药水或能力直伤)
        if (!props.IsPoweredAttack())
            return;

        // 3. 计算本次命中获得的格挡：基础为 Amount (2)，处于临界状态时翻倍 (4)
        decimal blockToGain = Amount;
        if (Owner.HasPower<CriticalState>())
        {
            blockToGain *= 2m;
        }

        if (blockToGain > 0m)
        {
            Flash();
            await CreatureCmd.GainBlock(Owner, blockToGain, ValueProp.Unpowered, null);
        }
    }
}