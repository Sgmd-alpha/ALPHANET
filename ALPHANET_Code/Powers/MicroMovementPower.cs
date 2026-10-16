using ALPHANET.Cards; 
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace ALPHANET.Powers;

[RegisterPower]
public sealed class MicroMovementPower : TemporaryStrengthPower
{
    public override AbstractModel OriginModel => ModelDb.Card<MicroMovement>();
    protected override bool IsPositive => false;
}