using ALPHANET.States;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;


namespace ALPHANET.Powers;

[RegisterPower]
public sealed class NarrowExtractionPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );

    // 【核心修复】：将需要显示在文本里的变量注册到 CanonicalVars 中
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("BlockAmount", 0m),
        new CardsVar(1)
    ];

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (!(amount <= 0m) && power is CriticalState && power.Owner == Owner)
        {
            Flash(); 

            // 直接从 DynamicVars 中读取累计的格挡值
            decimal blockAmount = DynamicVars["BlockAmount"].BaseValue;
            if (blockAmount > 0m)
            {
                await CreatureCmd.GainBlock(Owner, blockAmount, ValueProp.Unpowered, null);
            }
            
            if (Amount > 0)
            {
                await CardPileCmd.Draw(choiceContext, Amount, Owner.Player!);
            }
        }
    }
}