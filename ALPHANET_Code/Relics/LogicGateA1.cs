using ALPHANET.Characters;
using ALPHANET.States;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Relics;

[RegisterRelic(typeof(ALPHANETRelicPool))]
public sealed class LogicGateA1 : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png"
    );

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<CriticalState>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 遗物给予的固定格挡使用 ValueProp.Unpowered（不受敏捷/脆弱影响）
        new BlockVar(6m, ValueProp.Unpowered)
    ];

    // 供 CriticalState.AfterRemoved 在退出临界状态时直接调用
    public async Task TriggerOnExitCritical(Creature creature)
    {
        Flash();
        await CreatureCmd.GainBlock(creature, DynamicVars.Block.BaseValue, ValueProp.Unpowered, null);
    }
}