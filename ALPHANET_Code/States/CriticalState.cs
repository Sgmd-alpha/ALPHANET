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

    // 绝对免疫开关 (仅限初始遗物触发)
    public bool RelicImmunityActive = false;
    
    // 寿命锁 1：玩家是否已经体验过一个完整的出牌阶段？
    public bool HasEnjoyedPlayableTurn = false;
    // 寿命锁 2：敌人的回合是否已经发生过？
    private bool _hasEnemyTurnStarted = false;
    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );

    // ==========================================
    // 入口与判定
    // ==========================================
    public static async Task Enter(PlayerChoiceContext choiceContext, Creature creature, Creature? applier = null, CardModel? cardSource = null, bool fromStarterRelic = false)
    {
        bool wasAlreadyInCritical = creature.HasPower<CriticalState>();

        // 机制触发：如果已经处于临界状态，扣除 1 点最大生命值
        if (wasAlreadyInCritical)
        {
            await CreatureCmd.LoseMaxHp(choiceContext, creature, 1m, false);
        }

        // 施加状态本身
        await PowerCmd.Apply<CriticalState>(choiceContext, creature, 1m, applier, cardSource);

        var power = creature.GetPower<CriticalState>();
        if (power != null)
        {
            if (fromStarterRelic) 
            {
                power.RelicImmunityActive = true;
            }

            // 寿命核验：只有首次进入时初始化。如果是玩家出牌阶段主动打牌进入，直接扣上寿命锁。
            if (!wasAlreadyInCritical)
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

    // ==========================================
    // 免疫网：拦截一切导致暴毙的伤害与流失
    // ==========================================
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        // 只有遗物触发，且当前绝对不是玩家的“手动出牌阶段”时，免疫才生效
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
    
    // ==========================================
    // 记录敌人回合
    // ==========================================
    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == CombatSide.Enemy)
        {
            // 记录敌人已经进行过回合
            _hasEnemyTurnStarted = true;
        }
        return Task.CompletedTask;
    }

    // ==========================================
    // 倒计时处决与无敌剥夺：卡在出牌阶段开始的一瞬间
    // ==========================================
    public override async Task AfterPlayerTurnStartLate(PlayerChoiceContext choiceContext, Player player)
    {
        // 确保是自己的回合
        if (player != Owner.Player) return;

        // 此时玩家真正获得了出牌控制权，自动打牌阶段已全部结束，绝对免疫罩子碎裂
        RelicImmunityActive = false; 

        // 双锁验证：敌人动过没？我享受过出牌回合没？
        if (_hasEnemyTurnStarted && HasEnjoyedPlayableTurn)
        {
            // 条件均满足，在玩家即将出牌前宣判死刑
            Flash();
            await CreatureCmd.Kill(Owner);
        }
        else
        {
            // 如果这是我触发临界后的第一个出牌回合，放我一马，并把寿命锁扣上
            HasEnjoyedPlayableTurn = true;
        }
    }

    // ==========================================
    // 击杀解除
    // ==========================================
    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature target, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (target.Side == CombatSide.Enemy && target.Powers.All(p => p.ShouldOwnerDeathTriggerFatal()))
        {
            Flash();
            await PowerCmd.Remove<CriticalState>(Owner);
        }
    }
}