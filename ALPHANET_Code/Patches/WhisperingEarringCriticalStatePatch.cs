using ALPHANET.States;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ALPHANET.Patches;

[HarmonyPatch]
public static class WhisperingEarringCriticalStatePatch
{
    // 拦截低语耳环的自动出牌阶段的结尾
    [HarmonyPatch(typeof(WhisperingEarring), nameof(WhisperingEarring.AfterAutoPrePlayPhaseEnteredLate))]
    [HarmonyPostfix]
    public static void RemoveCriticalStateAfterAutoPlay(
        PlayerChoiceContext choiceContext, 
        Player player, 
        ref Task __result)
    {
        // 因为目标方法是 async Task，我们必须等待原始任务完成，再执行我们的清理逻辑
        Task originalTask = __result;
        __result = WaitAndCleanupCriticalStateAsync(originalTask, player, choiceContext);
    }

    private static async Task WaitAndCleanupCriticalStateAsync(Task originalTask, Player player, PlayerChoiceContext choiceContext)
    {
        // 1. 等待低语耳环的 13 张自动出牌彻底执行完毕
        await originalTask;

        // 2. 检测该玩家此刻是否处于临界状态
        if (player.Creature != null && player.Creature.HasPower<CriticalState>())
        {
            var criticalPower = player.Creature.GetPower<CriticalState>();
            if (criticalPower != null)
            {
                // 3. 强制移除临界状态
                // 这样当正式进入玩家的控制阶段时，角色就是一个干干净净的非临界状态。
                var ctx = choiceContext ?? new ThrowingPlayerChoiceContext();
                await PowerCmd.Remove(criticalPower);
                
                // 如果你有 UI 刷新逻辑，可以在这里调用，比如：
                // ALPHANET.Systems.CardCriticalBadgeSystem.RefreshAllVisibleCardBadges();
            }
        }
    }
}