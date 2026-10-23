using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Powers;

[RegisterPower]
public sealed class OutsidersSlumberPower : ModPowerTemplate
{
    // 能量封锁黑名单
    public static HashSet<PlayerCombatState> SlumberingStates = new();

    static OutsidersSlumberPower()
    {
        var harmony = new Harmony("alphanet.outsidersslumber.patch");
        
        // 1. 拦截佩尔之眼
        var paelsEyeType = typeof(PaelsEye);
        harmony.Patch(paelsEyeType.GetMethod(nameof(PaelsEye.AfterTakingExtraTurn)), prefix: new HarmonyMethod(typeof(OutsidersSlumberPower), nameof(SuppressPaelsEyeTask)));
        harmony.Patch(paelsEyeType.GetMethod(nameof(PaelsEye.BeforeSideTurnEndEarly)), prefix: new HarmonyMethod(typeof(OutsidersSlumberPower), nameof(SuppressPaelsEyeTask)));
        harmony.Patch(paelsEyeType.GetMethod(nameof(PaelsEye.ShouldTakeExtraTurn)), prefix: new HarmonyMethod(typeof(OutsidersSlumberPower), nameof(SuppressPaelsEyeBool)));

        // 2. 焊死能量入口
        var combatStateType = typeof(PlayerCombatState);
        foreach (var method in combatStateType.GetMethods().Where(m => m.Name == "GainEnergy"))
        {
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(OutsidersSlumberPower), nameof(SuppressEnergyGain)));
        }

        var energySetter = combatStateType.GetProperty("Energy")?.GetSetMethod();
        if (energySetter != null)
        {
            harmony.Patch(energySetter, prefix: new HarmonyMethod(typeof(OutsidersSlumberPower), nameof(SuppressEnergySetter)));
        }
    }

    private static bool SuppressPaelsEyeTask(PaelsEye __instance, ref Task __result)
    {
        if (__instance.Owner != null && __instance.Owner.Creature.Powers.Any(p => p is OutsidersSlumberPower))
        {
            __result = Task.CompletedTask; return false; 
        }
        return true;
    }

    private static bool SuppressPaelsEyeBool(PaelsEye __instance, ref bool __result)
    {
        if (__instance.Owner != null && __instance.Owner.Creature.Powers.Any(p => p is OutsidersSlumberPower))
        {
            __result = false; return false; 
        }
        return true;
    }

    // 【报错修复】不再需要找 Player，直接查黑名单即可！
    private static bool SuppressEnergyGain(PlayerCombatState __instance)
    {
        if (SlumberingStates.Contains(__instance)) return false; 
        return true;
    }

    private static bool SuppressEnergySetter(PlayerCombatState __instance, int value)
    {
        if (SlumberingStates.Contains(__instance))
        {
            if (value > __instance.Energy) return false; 
        }
        return true;
    }

    // =========================================================

    public override PowerType Type => PowerType.Debuff; 
    public override PowerStackType StackType => PowerStackType.Counter;

    private bool _isWakingUp = false;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/OutsidersSlumberPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/OutsidersSlumberPower.png"
    );

    public override bool ShouldDraw(Player player, bool fromHandDraw)
    {
        if (player == Owner.Player) { Flash(); return false; }
        return true;
    }

    public override bool ShouldTakeExtraTurn(Player player)
    {
        return player == Owner.Player && !_isWakingUp;
    }

    public override bool ShouldClearBlock(Creature creature)
    {
        if (Owner == creature) return false;
        return true;
    }

    public override Task AfterPreventingBlockClear(AbstractModel preventer, Creature creature)
    {
        if (this == preventer) Flash();
        return Task.CompletedTask;
    }
    
    // ==========================================
    // 回合开始：完全静默，只需掉层
    // ==========================================
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner.Player && !_isWakingUp)
        {
            Flash();

            var intangible = player.Creature.Powers.FirstOrDefault(p => p is IntangiblePower);
            if (intangible != null)
            {
                await PowerCmd.Decrement(intangible);
            }

            if (Amount > 1)
            {
                await PowerCmd.Decrement(this);
            }
            else
            {
                _isWakingUp = true;
            }
            foreach (var debuff in player.Creature.Powers.Where(p => p.Type == PowerType.Debuff && p != this).ToList())
            {
                await debuff.AfterSideTurnEnd(choiceContext, CombatSide.Enemy, Owner.CombatState!.Allies);  //清理绑定在 Enemy 回合结束但逻辑上属于玩家回合周期的 TaintedPower 等
            }

            PlayerCmd.EndTurn(player, canBackOut: false);
        }
    }

    // ==========================================
    // 金蝉脱壳，完美避开佩尔之眼结算
    // ==========================================
    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (_isWakingUp && side != Owner.Side)
        {
            await PowerCmd.Remove(this);
        }
    }

    // ==========================================
    // 彻底醒来时，将玩家从能量静默黑名单中解除！
    // ==========================================
    public override Task AfterRemoved(Creature oldOwner)
    {
        if (oldOwner.Player != null && oldOwner.Player.PlayerCombatState != null)
        {
            SlumberingStates.Remove(oldOwner.Player.PlayerCombatState);
        }
        return Task.CompletedTask;
    }
}