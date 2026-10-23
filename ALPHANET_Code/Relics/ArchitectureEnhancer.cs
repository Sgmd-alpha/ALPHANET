using ALPHANET.Characters;
using ALPHANET.States;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Relics;

[RegisterRelic(typeof(ALPHANETRelicPool))]
public sealed class ArchitectureEnhancer : ModRelicTemplate
{
    // 战斗内触发限制开关
    private bool _usedThisCombat = false;

    public override RelicRarity Rarity => RelicRarity.Rare;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png"
    );

    // 悬浮提示：解释“临界状态”和“无实体”
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new List<IHoverTip>
    {
        HoverTipFactory.FromPower<CriticalState>(),
        HoverTipFactory.FromPower<IntangiblePower>()
    };

    // ==========================================
    // 机制：战斗开始时重置开关
    // ==========================================
    public override Task BeforeCombatStart()
    {
        _usedThisCombat = false;
        return Task.CompletedTask;
    }

    // ==========================================
    // 遗物专属效果：由临界状态的移除逻辑主动调用
    // ==========================================
    public async Task TriggerIntangible(Creature creature)
    {
        // 如果本场战斗已经触发过，则直接跳过
        if (_usedThisCombat)
        {
            return;
        }

        // 标记为已使用并闪烁遗物
        _usedThisCombat = true;
        Flash();
        
        await PowerCmd.Apply<IntangiblePower>(null!, creature, 1m, creature, null);
        await PowerCmd.Apply<BufferPower>(null!, creature, 1m, creature, null);
    }
}