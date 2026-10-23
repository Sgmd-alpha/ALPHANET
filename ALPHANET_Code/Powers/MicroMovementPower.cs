using ALPHANET.Cards; 
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace ALPHANET.Powers;

[RegisterPower]
public sealed class MicroTwitchPower : TemporaryStrengthPower
{
    public override AbstractModel OriginModel => ModelDb.Card<MicroTwitch>();
    protected override bool IsPositive => false;
}