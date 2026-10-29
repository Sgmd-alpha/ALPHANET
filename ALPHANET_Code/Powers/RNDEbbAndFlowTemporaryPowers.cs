
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
    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );
}

// 临时敏捷：在拥有者的回合结束时自动消失
[RegisterPower]
public sealed class RNDEbbAndFlowTemporaryDexterityPower
    : ModTemporaryAppliedPowerTemplate<
        RNDEbbAndFlowPower,
        DexterityPower>
{
    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );
}
