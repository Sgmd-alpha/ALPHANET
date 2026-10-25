using ALPHANET.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;


namespace ALPHANET.Powers;

[RegisterPower]

public sealed class SolidImpactPower : PowerModel
{
    public bool Triggered { get; set; } = false;
    public override PowerType Type => PowerType.None;
    public override PowerStackType StackType => PowerStackType.None;
    protected override bool IsVisibleInternal => false;
    // 拦截器核心钩子
    public override decimal ModifyHpLostAfterOstyLate(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (cardSource is SolidImpact && props.IsPoweredAttack())
        {
            if (amount < Amount)
            {
                Triggered = true; 
                return Amount;    
            }
        }
        return amount;
    }
}