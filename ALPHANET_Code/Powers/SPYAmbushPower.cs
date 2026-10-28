using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Powers;

[RegisterPower]
public sealed class SPYAmbushPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter; // 可叠加

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/SPYAmbushPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/SPYAmbushPower.png"
    );

    // 在玩家回合结束时：检查本回合是否打出过攻击牌
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        await base.AfterSideTurnEnd(choiceContext, side, participants);

        if (Owner?.Player != null && side == Owner.Side)
        {
            // 统计本回合由该玩家打出的攻击牌数量
            int attackCount = CombatManager.Instance.History.CardPlaysStarted.Count(e =>
                e.HappenedThisTurn(CombatState) &&
                e.CardPlay.Player == Owner.Player &&
                e.CardPlay.Card.Type == CardType.Attack);

            // 如果本回合没有打出过攻击牌
            if (attackCount == 0)
            {
                Flash();
                // 下回合赋予或增加展示增伤能力（每张暗度陈仓提供 25% 或升级后的 50%）
                int percentPerStack = 25; // 基础数值，可根据卡牌升级调整或统一通过 Amount 换算
                int bonusPercent = Amount * percentPerStack;

                // 给予或刷新独立的展示增伤Buff
                var displayBuff = Owner.GetPower<SPYAmbushBuffPower>();
                if (displayBuff != null)
                {
                    await PowerCmd.ModifyAmount(choiceContext, displayBuff, bonusPercent, Owner, null);
                }
                else
                {
                    await PowerCmd.Apply<SPYAmbushBuffPower>(choiceContext, Owner, bonusPercent, Owner, null);
                }
            }
        }
    }
}