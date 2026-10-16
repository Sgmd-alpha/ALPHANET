using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;


namespace ALPHANET.Powers;

[RegisterPower]
public sealed class AbsentMindedNextTurnPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff; 
    public override PowerStackType StackType => PowerStackType.None;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/AbsentMindedNextTurnPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/AbsentMindedNextTurnPower.png"
    ); 

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        // 触发条件 1：敌方回合结束（这是针对普通的受惩罚情况）
        bool isMonsterTurnEnded = (side != Owner.Side);
        
        // 触发条件 2【联动千年黄粱】：己方回合结束，且玩家身上有连续失神状态（说明马上要进入额外回合了！）
        bool isPlayerTurnEndedWithExtraTurn = (side == Owner.Side && Owner.HasPower<OutsidersSlumberPower>());

        if (isMonsterTurnEnded || isPlayerTurnEndedWithExtraTurn)
        {
            Flash();
            
            // 在挂载真正的失神之前，提前把玩家拉入能量黑名单！
            if (Owner.Player != null && Owner.Player.PlayerCombatState != null)
            {
                AbsentMindedPower.SlumberingStates.Add(Owner.Player.PlayerCombatState);
            }

            // 挂载真正的失神，随后销毁自己
            await PowerCmd.Apply<AbsentMindedPower>(null!, Owner, 1, Owner, null!);
            await PowerCmd.Remove(this);
        }
    }
}