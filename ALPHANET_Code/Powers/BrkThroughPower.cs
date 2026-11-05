using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Powers;

[RegisterPower]
public sealed class BrkThroughPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    
    // 显示在右下角的数字，代表每回合给多少格挡
    public override PowerStackType StackType => PowerStackType.Counter; 

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );

    // 【修复1】恢复 CanonicalVars，并让图鉴和局内的文本动态读取层数
    protected override IEnumerable<DynamicVar> CanonicalVars
    {
        get
        {
            // 如果在主菜单看图鉴，默认显示 2；如果在实战中，文本跟随右下角的真实层数
            decimal displayBlock = (Owner == null || Amount <= 0m) ? 2m : Amount;
            return [ new BlockVar(displayBlock, ValueProp.Unpowered) ];
        }
    }

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        await base.AfterApplied(applier, cardSource);
        DynamicVars.Block.BaseValue = Amount;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner?.Player)
        {
            Flash();
            await CreatureCmd.GainBlock(player.Creature, DynamicVars.Block, null);
        }
    }
}