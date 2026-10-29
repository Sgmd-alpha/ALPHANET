using ALPHANET.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace ALPHANET.Powers;

[RegisterPower]
public sealed class MicroTwitchPower : TemporaryStrengthPower, IModPowerAssetOverrides
{
    public override AbstractModel OriginModel => ModelDb.Card<MicroTwitch>();
    protected override bool IsPositive => false;

    // 通过实现 IModPowerAssetOverrides 接口配置图标路径
    public PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );

    public string? CustomIconPath => $"{Entry.ResPath}/images/powers/{GetType().Name}.png";
    public string? CustomBigIconPath => $"{Entry.ResPath}/images/powers/{GetType().Name}.png";
}