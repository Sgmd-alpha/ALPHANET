using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace ALPHANET.Systems;

[HarmonyPatch]
public static class CultivationTrackerSystem
{
    private class StrengthLedger
    {
        public int GainedPermanentStrength = 0;
        public int PendingTempChangeIgnore = 0;
    }

    private static readonly ConditionalWeakTable<Creature, StrengthLedger> Ledgers = new();

    private static StrengthLedger GetLedger(Creature creature) => Ledgers.GetOrCreateValue(creature);

    // =========================================================
    // 1. 排除临时力量：标记所有由 TemporaryStrengthPower 引发的正向力量变动
    // =========================================================

    [HarmonyPatch(typeof(TemporaryStrengthPower), nameof(TemporaryStrengthPower.BeforeApplied))]
    [HarmonyPrefix]
    public static void OnTempStrBeforeApplied(TemporaryStrengthPower __instance, Creature target, decimal amount)
    {
        int delta = __instance.Sign * (int)amount;
        if (delta > 0 && target != null)
        {
            GetLedger(target).PendingTempChangeIgnore += delta;
        }
    }

    [HarmonyPatch(typeof(TemporaryStrengthPower), nameof(TemporaryStrengthPower.AfterPowerAmountChanged))]
    [HarmonyPrefix]
    public static void OnTempStrAmountChanged(TemporaryStrengthPower __instance, PowerModel power, decimal amount)
    {
        if (power == __instance && amount != __instance.Amount && __instance.Owner != null)
        {
            int delta = __instance.Sign * (int)amount;
            if (delta > 0)
            {
                GetLedger(__instance.Owner).PendingTempChangeIgnore += delta;
            }
        }
    }

    [HarmonyPatch(typeof(TemporaryStrengthPower), nameof(TemporaryStrengthPower.AfterSideTurnEnd))]
    [HarmonyPrefix]
    public static void OnTempStrTurnEnd(TemporaryStrengthPower __instance, IEnumerable<Creature> participants)
    {
        if (__instance.Owner != null && participants.Contains(__instance.Owner))
        {
            int revertDelta = -__instance.Sign * __instance.Amount;
            if (revertDelta > 0)
            {
                GetLedger(__instance.Owner).PendingTempChangeIgnore += revertDelta;
            }
        }
    }

    // =========================================================
    // 2. 记账：只记录实打实的永久正力量增长
    // =========================================================

    [HarmonyPatch(typeof(PowerModel), nameof(PowerModel.SetAmount))]
    [HarmonyPrefix]
    public static void OnPowerSetAmountPrefix(PowerModel __instance, int amount)
    {
        if (__instance is not StrengthPower) return;

        int currentAmount = __instance.Amount;
        int clampedNew = Math.Clamp(amount, -999999999, 999999999);
        int delta = clampedNew - currentAmount;

        // 减力量（delta <= 0）直接跳过，不改账本
        if (delta <= 0) return;

        Creature? owner = __instance.Owner;
        if (owner == null) return;

        var ledger = GetLedger(owner);

        // 抵消掉临时力量带来的正增长（比如尖啸在回合结束时恢复的 +6 力量）
        if (ledger.PendingTempChangeIgnore > 0)
        {
            int ignored = Math.Min(delta, ledger.PendingTempChangeIgnore);
            delta -= ignored;
            ledger.PendingTempChangeIgnore -= ignored;
        }

        // 剩下的就是真正的永久力量成长，记入账本
        if (delta > 0)
        {
            ledger.GainedPermanentStrength += delta;
        }
    }

    // =========================================================
    // 3. 【修复】：多条命 Boss 换命 / 死亡复活时，清空上一条命的计数。
    // =========================================================
    // 拦截 A：当多命 Boss 的一条命被打空（触发 Hook.AfterDeath）时，重置其账本
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterDeath))]
    [HarmonyPostfix]
    public static void OnCreatureAfterDeath(Creature creature)
    {
        if (creature != null)
        {
            var ledger = GetLedger(creature);
            ledger.GainedPermanentStrength = 0;
            ledger.PendingTempChangeIgnore = 0;
        }
    }

    // 拦截 B：当多命 Boss 转阶段通过底层直接拔除（RemoveInternal）身上的正数 StrengthPower 时，同步清零账本
    // （注：如果是玩家打出减力量卡使 +2 力量变成 0，我们可以区分，或者如果整团正力量被阶段切换强行抹除，则重置）
    private static bool _isCrippling = false;

    [HarmonyPatch(typeof(PowerModel), "RemoveInternal")]
    [HarmonyPrefix]
    public static void OnPowerRemoveInternalPrefix(PowerModel __instance)
    {
        // 如果不是《废其修为》自己调用的移除，且移除瞬间该 StrengthPower 依然是正数（说明是 Boss 转阶段/净化强行清空了自身所有状态！）
        if (!_isCrippling && __instance is StrengthPower && __instance.Owner != null && __instance.Amount > 0)
        {
            var ledger = GetLedger(__instance.Owner);
            ledger.GainedPermanentStrength = 0;
            ledger.PendingTempChangeIgnore = 0;
        }
    }

    // =========================================================
    // 4. 公开接口：供《废其修为》预测与结算调用
    // =========================================================

    public static int PredictNewStrength(Creature? target)
    {
        if (target == null) return 0;
        int currentStr = target.GetPower<StrengthPower>()?.Amount ?? 0;
        int gained = GetLedger(target).GainedPermanentStrength;
        return currentStr - gained;
    }

    public static async Task CrippleTargetStrength(Creature target)
    {
        if (target == null || !target.IsAlive) return;

        var ledger = GetLedger(target);
        int gained = ledger.GainedPermanentStrength;

        // 没长过永久力量，或者刚被上一张《废其修为》废过了，直接结束
        if (gained <= 0) return;

        // 立刻清空账本，防止第二张卡重复扣除
        ledger.GainedPermanentStrength = 0;

        var strengthPower = target.GetPower<StrengthPower>();
        int currentStr = strengthPower?.Amount ?? 0;
        int newStr = currentStr - gained;

        if (strengthPower != null)
        {
            if (newStr == 0)
            {
                try
                {
                    _isCrippling = true;
                    await PowerCmd.Remove(strengthPower);
                }
                finally
                {
                    _isCrippling = false;
                }
            }
            else
            {
                strengthPower.SetAmount(newStr, silent: false);
                AccessTools.Method(typeof(PowerModel), "Flash")?.Invoke(strengthPower, null);
            }
        }
        else if (newStr != 0)
        {
            var newPower = ModelDb.Power<StrengthPower>().ToMutable();
            newPower.ApplyInternal(target, newStr, silent: false);
            AccessTools.Method(typeof(PowerModel), "Flash")?.Invoke(newPower, null);
        }

        // 强制刷新怪物头顶的攻击意图伤害数值
        ICombatState? combatState = target.CombatState;
        if (CombatManager.Instance.IsInProgress && target.IsMonster && target.IsAlive && combatState != null)
        {
            NCreature? nCreature = NCombatRoom.Instance?.GetCreatureNode(target);
            if (nCreature != null)
            {
                try
                {
                    await nCreature.UpdateIntent(combatState.Allies);
                }
                catch (ObjectDisposedException ex)
                {
                    Log.Error(ex.ToString());
                }
            }
        }
    }
}