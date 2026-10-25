using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Powers;

[RegisterPower]
public sealed class InstantAffinityPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    // 记录本回合已经触发过升级与赋予保留的卡牌张数
    private int _triggeredThisTurn = 0;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );

    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == Owner.Side)
        {
            _triggeredThisTurn = 0;
        }
        return Task.CompletedTask;
    }

    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card.Owner != Owner.Player) return Task.CompletedTask;

        // まだ残余回数があるかチェック
        if (_triggeredThisTurn >= Amount) return Task.CompletedTask;

        // 判定条件：攻击或技能牌，且当前不具有“保留”关键词
        bool isAttackOrSkill = (card.Type == CardType.Attack || card.Type == CardType.Skill);
        bool hasRetain = card.Keywords != null && card.Keywords.Contains(CardKeyword.Retain);

        if (isAttackOrSkill && !hasRetain)
        {
            _triggeredThisTurn++;
            Flash();

            // 1. 获得保留
            CardCmd.ApplyKeyword(card, CardKeyword.Retain);

            // 2. 在本场战斗中升级
            if (card.IsUpgradable)
            {
                CardCmd.Upgrade(card);
            }
        }

        return Task.CompletedTask;
    }
}

