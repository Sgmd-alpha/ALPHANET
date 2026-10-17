using ALPHANET.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.States;

[RegisterPower]
public sealed class CriticalState : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single; 

    public bool RelicImmunityActive = false;
    public bool HasEnjoyedPlayableTurn = false;
    private bool _hasEnemyTurnStarted = false;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );

    public static async Task Enter(PlayerChoiceContext? choiceContext, Creature creature, Creature? applier = null, CardModel? cardSource = null, bool fromStarterRelic = false)
    {
        // 【核心修复】：如果外部传入了 null，自动兜底创建一个 ThrowingPlayerChoiceContext
        choiceContext ??= new ThrowingPlayerChoiceContext();

        bool wasAlreadyInCritical = creature.HasPower<CriticalState>();

        // 机制触发：如果已经处于临界状态，扣除 1 点最大生命值
        if (wasAlreadyInCritical)
        {
            await CreatureCmd.LoseMaxHp(choiceContext, creature, 1m, false);
        }

        // 施加状态本身（此时 choiceContext 绝对非空，绝处逢生抽牌再也不会报错！）
        await PowerCmd.Apply<CriticalState>(choiceContext, creature, 1m, applier, cardSource);

        var power = creature.GetPower<CriticalState>();
        if (power != null)
        {
            if (fromStarterRelic) 
            {
                power.RelicImmunityActive = true;
                // 被动名刀触发时重置寿命锁，确保能活过接下来的完整回合
                power.HasEnjoyedPlayableTurn = false;
                power._hasEnemyTurnStarted = false;
            }
            else if (!wasAlreadyInCritical)
            {
                power.HasEnjoyedPlayableTurn = MiracleResidue.IsPlayerMainPhase;
            }
        }
        
        // 联动架构增幅器
        if (creature.IsPlayer && creature.Player?.Relics != null)
        {
            var amplifier = creature.Player.Relics.OfType<ArchitectureAmplifier>().FirstOrDefault();
            if (amplifier != null)
            {
                await amplifier.TriggerIntangible(creature);
            }
        }
    }

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (target == Owner && RelicImmunityActive && !MiracleResidue.IsPlayerMainPhase)
        {
            Flash();
            return 0m; 
        }
        return 1m;
    }

    public override decimal ModifyHpLostAfterOsty(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (RelicImmunityActive && !MiracleResidue.IsPlayerMainPhase)
        {
            Flash();
            return 0m; 
        }
        return amount;
    }
    
    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == CombatSide.Enemy)
        {
            _hasEnemyTurnStarted = true;
        }
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStartLate(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player) return;

        RelicImmunityActive = false; 

        if (_hasEnemyTurnStarted && HasEnjoyedPlayableTurn)
        {
            Flash();

            // 【核心修复】：检查身上是否有未使用的《奇迹残渣》
            // 如果有，直接走免死续命逻辑并传入当前合法的 choiceContext，不在回合初调用 Kill 破坏联机状态机！
            var miracle = player.Relics?.OfType<MiracleResidue>().FirstOrDefault();
            if (miracle != null && miracle.CanPreventDeath)
            {
                // 将生命值扣到 1 点，并直接触发名刀续命
                if (Owner.CurrentHp > 1m)
                {
                    await CreatureCmd.Damage(choiceContext, Owner, Owner.CurrentHp - 1m, ValueProp.Unblockable | ValueProp.Unpowered, null, null);
                }
                await miracle.TriggerDeathPrevention(choiceContext, Owner);
                
                // 因为已经在玩家回合初续命，本回合就是他享受的最后一个出牌回合，扣上寿命锁但剥夺当回合免伤罩
                RelicImmunityActive = false;
                HasEnjoyedPlayableTurn = true;
                _hasEnemyTurnStarted = false;
            }
            else
            {
                // 没有名刀保护，真正执行处决
                await CreatureCmd.Kill(Owner);
            }
        }
        else
        {
            HasEnjoyedPlayableTurn = true;
        }
    }

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature target, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (target.Side == CombatSide.Enemy && target.Powers.All(p => p.ShouldOwnerDeathTriggerFatal()))
        {
            Flash();
            await PowerCmd.Remove<CriticalState>(Owner);
        }
    }
}