using ALPHANET.Characters;
using ALPHANET.States;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Relics;

[RegisterRelic(typeof(ALPHANETRelicPool))]
public sealed class SilkPouch : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png"
    );

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new List<IHoverTip>
    {
        HoverTipFactory.FromPower<CriticalState>(),
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<DexterityPower>()
    };

    // ==========================================
    // 监听状态施加 (获得临时属性)
    // 请补全正确的钩子，如 OnPowerApplied 或 AfterPowerApplied
    // ==========================================
    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        // 确保发生变化的状态是【临界状态】，且该状态是挂在玩家自己身上的
        if (power is CriticalState && power.Owner == Owner.Creature)
        {
            if (amount > 0m) 
            {
                // 层数增加（进入临界状态）：获得临时属性
                Flash();
                await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, 2m, applier, null);
                await PowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature, 2m, applier, null);
            }
            else if (amount < 0m) 
            {
                // 层数减少（退出临界状态）：扣除临时属性
                Flash();
                await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, -2m, applier, null);
                await PowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature, -2m, applier, null);
            }
        }
    }
}