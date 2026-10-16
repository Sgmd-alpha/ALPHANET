using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Relics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;


namespace ALPHANET.Powers;

[RegisterPower]
public sealed class AbsentMindedPower : ModPowerTemplate
{
    public static HashSet<PlayerCombatState> SlumberingStates = new();

    static AbsentMindedPower()
    {
        var harmony = new Harmony("alphanet.absentminded.patch");

        
        
        var paelsEyeType = typeof(PaelsEye);
        harmony.Patch(paelsEyeType.GetMethod(nameof(PaelsEye.AfterTakingExtraTurn)), prefix: new HarmonyMethod(typeof(AbsentMindedPower), nameof(SuppressPaelsEyeTask)));
        harmony.Patch(paelsEyeType.GetMethod(nameof(PaelsEye.BeforeSideTurnEndEarly)), prefix: new HarmonyMethod(typeof(AbsentMindedPower), nameof(SuppressPaelsEyeTask)));
        harmony.Patch(paelsEyeType.GetMethod(nameof(PaelsEye.ShouldTakeExtraTurn)), prefix: new HarmonyMethod(typeof(AbsentMindedPower), nameof(SuppressPaelsEyeBool)));

        var combatStateType = typeof(PlayerCombatState);
        foreach (var method in combatStateType.GetMethods().Where(m => m.Name == "GainEnergy"))
        {
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(AbsentMindedPower), nameof(SuppressEnergyGain)));
        }

        var energySetter = combatStateType.GetProperty("Energy")?.GetSetMethod();
        if (energySetter != null)
        {
            harmony.Patch(energySetter, prefix: new HarmonyMethod(typeof(AbsentMindedPower), nameof(SuppressEnergySetter)));
        }
    }

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/AbsentMindedPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/AbsentMindedPower.png"
    );

    private static bool SuppressPaelsEyeTask(PaelsEye __instance, ref Task __result)
    {
        if (__instance.Owner != null && __instance.Owner.Creature.Powers.Any(p => p is AbsentMindedPower))
        {
            __result = Task.CompletedTask; return false; 
        }
        return true;
    }

    private static bool SuppressPaelsEyeBool(PaelsEye __instance, ref bool __result)
    {
        if (__instance.Owner != null && __instance.Owner.Creature.Powers.Any(p => p is AbsentMindedPower))
        {
            __result = false; return false; 
        }
        return true;
    }

    private static bool SuppressEnergyGain(PlayerCombatState __instance)
    {
        if (SlumberingStates.Contains(__instance)) return false; 
        return true;
    }

    private static bool SuppressEnergySetter(PlayerCombatState __instance, int value)
    {
        if (SlumberingStates.Contains(__instance) && value > __instance.Energy) return false; 
        return true;
    }

    // =========================================================

    public override PowerType Type => PowerType.Debuff; 
    public override PowerStackType StackType => PowerStackType.None; 

    private bool _isWakingUp = false;

    public override bool ShouldDraw(Player player, bool fromHandDraw)
    {
        if (player == Owner.Player) { Flash(); return false; }
        return true;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner.Player && !_isWakingUp)
        {
            Flash();
            _isWakingUp = true;
            PlayerCmd.EndTurn(player, canBackOut: false);
        }
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (_isWakingUp && side != Owner.Side)
        {
            await PowerCmd.Remove(this);
        }
    }

    // 销毁时，解除玩家的能量黑名单状态
    public override Task AfterRemoved(Creature oldOwner)
    {
        if (oldOwner.Player != null && oldOwner.Player.PlayerCombatState != null)
        {
            SlumberingStates.Remove(oldOwner.Player.PlayerCombatState);
        }
        return Task.CompletedTask;
    }
}