using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;


namespace ALPHANET.Powers;

[RegisterPower]
public sealed class ChaosCostModifierPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;
    
    // 使用 Stack 使层数可以无限叠加（打1张挂1层，打2张挂2层...）
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );

    // ==========================================
    // 核心钩子：动态修改特定卡牌的战斗耗能
    // ==========================================
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;

        // 如果这张牌不是该角色的牌，或者这张牌根本就不是“混沌”，则放行不修改
        if (card.Owner.Creature != Owner || card is not Cards.StatusChaos)
        {
            return false;
        }

        // 修改耗能 = 原本耗能(1) + 当前状态的层数
        // 引擎会自动接管后续的 UI 渲染（红色高亮）和费用检查
        modifiedCost = originalCost + Amount;
        return true;
    }

    // ==========================================
    // 生命周期：回合结束时销毁
    // ==========================================
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        // 检查是不是己方回合结束
        if (participants.Contains(Owner))
        {
            await PowerCmd.Remove(this);
        }
    }
}