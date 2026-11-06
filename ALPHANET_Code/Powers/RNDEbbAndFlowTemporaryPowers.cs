using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Combat.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Powers;

// 临时力量：在拥有者的回合结束时自动消失
[RegisterPower]
public sealed class RNDEbbAndFlowTemporaryStrengthPower
    : ModTemporaryAppliedPowerTemplate<
        RNDEbbAndFlowPower,
        StrengthPower>
{
    private const string SourceAmountKey = "SourceAmount";

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> AdditionalCanonicalVars
    {
        get
        {
            // 图鉴及预览状态下的默认值
            return [new DynamicVar(SourceAmountKey, 1m)];
        }
    }
    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            Creature? owner = IsMutable ? Owner : null;

            return
            [
                RNDEbbAndFlowPower.CreateSourceHoverTip(owner)
            ];
        }
    }

    // 供主能力调用，实时更新描述中的主能力层数
    internal void UpdateSourceAmount(decimal sourceAmount)
    {
        DynamicVars[SourceAmountKey].BaseValue = sourceAmount;
    }

    public override async Task AfterApplied(
        Creature? applier,
        CardModel? cardSource)
    {
        await base.AfterApplied(applier, cardSource);

        // 临时能力实际应用后，从拥有者身上读取主能力层数
        if (Owner != null)
        {
            var mainPower = Owner.GetPower<RNDEbbAndFlowPower>();

            UpdateSourceAmount(
                mainPower == null ? 0m : mainPower.Amount
            );
        }
    }
}

[RegisterPower]
public sealed class RNDEbbAndFlowTemporaryDexterityPower
    : ModTemporaryAppliedPowerTemplate<
        RNDEbbAndFlowPower,
        DexterityPower>
{
    private const string SourceAmountKey = "SourceAmount";

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> AdditionalCanonicalVars
    {
        get
        {
            return [new DynamicVar(SourceAmountKey, 1m)];
        }
    }

    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            Creature? owner = IsMutable ? Owner : null;

            return
            [
                RNDEbbAndFlowPower.CreateSourceHoverTip(owner)
            ];
        }
    }

    internal void UpdateSourceAmount(decimal sourceAmount)
    {
        DynamicVars[SourceAmountKey].BaseValue = sourceAmount;
    }

    public override async Task AfterApplied(
        Creature? applier,
        CardModel? cardSource)
    {
        await base.AfterApplied(applier, cardSource);

        if (Owner != null)
        {
            var mainPower = Owner.GetPower<RNDEbbAndFlowPower>();

            UpdateSourceAmount(
                mainPower == null ? 0m : mainPower.Amount
            );
        }
    }
}