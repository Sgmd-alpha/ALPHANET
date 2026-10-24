using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Powers;

[RegisterPower]
public sealed class SecondaryFusePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.None;
    public override PowerStackType StackType => PowerStackType.None;

    // false = 预热阶段（敌人本次攻击回合尚未结束）
    // true  = 激活阶段（从敌人本次攻击结束后开始，直至敌人下次攻击结束）
    private bool _isImmunityActive = false;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );

    // ==========================================
    // 免疫一切攻击与技能伤害
    // ==========================================
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (target == Owner && _isImmunityActive)
        {
            Flash();
            return 0m;
        }
        return 1m;
    }

    // ==========================================
    // 免疫一切直接生命流失（如果需要连生命流失也一并免疫）
    // ==========================================
    public override decimal ModifyHpLostAfterOsty(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner && _isImmunityActive)
        {
            Flash();
            return 0m;
        }
        return amount;
    }

    // ==========================================
    // 时间轴状态机：精准控制开启与结束时机
    // ==========================================
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        // 阶段一 -> 阶段二：与 AbsentMindedNextTurnPower 完全同步的激活时机！
        if (!_isImmunityActive)
        {
            bool isMonsterTurnEnded = (side != Owner.Side);
            bool isPlayerTurnEndedWithExtraTurn = (side == Owner.Side && Owner.HasPower<OutsidersSlumberPower>());

            if (isMonsterTurnEnded || isPlayerTurnEndedWithExtraTurn)
            {
                _isImmunityActive = true;
                Flash();
            }
            return;
        }

        // 阶段二 -> 阶段三：当免伤已激活，且敌人的下一次攻击回合（或黄粱额外回合）结束时，移除自身！
        if (_isImmunityActive)
        {
            bool isNextMonsterTurnEnded = (side != Owner.Side);
            bool isExtraTurnEnded = (side == Owner.Side && Owner.HasPower<OutsidersSlumberPower>());

            if (isNextMonsterTurnEnded || isExtraTurnEnded)
            {
                Flash();
                await PowerCmd.Remove(this);
            }
        }
    }
}