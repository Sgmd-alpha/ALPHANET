using System.Runtime.CompilerServices;
using ALPHANET.Tags;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ALPHANET.Systems;

[HarmonyPatch]
public static class MiracleSystem
{
    private class MiraclePlayRecord
    {
        public int Deficit;
        public bool IsZeroCostMiracle;
    }

    // 记录每张正在打出的奇迹卡的透支差额与 0 费状态
    private static readonly ConditionalWeakTable<CardModel, MiraclePlayRecord> _playRecords = new();

    // =========================================================
    // 跨战斗安全隔离的“本场战斗奇迹触发次数”统计器（供《奇迹之雨》等直接读取）
    // =========================================================
    private class MiracleCombatTracker
    {
        public WeakReference<ICombatState>? CombatRef;
        public int TotalTriggersInCombat = 0;

        public int GetCount(ICombatState? currentCombat)
        {
            if (currentCombat == null || CombatRef == null) return 0;
            if (CombatRef.TryGetTarget(out var recorded) && ReferenceEquals(recorded, currentCombat))
                return TotalTriggersInCombat;
            return 0;
        }

        public void Increment(ICombatState? currentCombat)
        {
            if (currentCombat == null) return;
            if (CombatRef != null && CombatRef.TryGetTarget(out var recorded) && ReferenceEquals(recorded, currentCombat))
            {
                TotalTriggersInCombat++;
            }
            else
            {
                CombatRef = new WeakReference<ICombatState>(currentCombat);
                TotalTriggersInCombat = 1;
            }
        }
    }

    private static readonly ConditionalWeakTable<Creature, MiracleCombatTracker> _combatTrackers = new();

    /// <summary>
    /// 获取指定生物在本场战斗中触发“奇迹”效果的总次数（供《奇迹之雨》等调用）
    /// </summary>
    public static int GetMiracleTriggerCount(Creature? creature)
    {
        if (creature == null || creature.CombatState == null) return 0;
        return _combatTrackers.GetOrCreateValue(creature).GetCount(creature.CombatState);
    }

    /// <summary>
    /// 当玩家触发奇迹效果时广播的异步事件（供《天赐良机》、《奇迹助力》等能力订阅）
    /// 参数：(choiceContext, card, cardPlay, hpLost)
    /// </summary>
    public static event Func<PlayerChoiceContext, CardModel, CardPlay, int, Task>? OnMiracleTriggered;

    // =========================================================
    // 1. 允许能量不足时打出奇迹卡
    // =========================================================
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.CanPlay),
        new Type[] { typeof(UnplayableReason), typeof(AbstractModel) },
        new ArgumentType[] { ArgumentType.Out, ArgumentType.Out })]
    [HarmonyPostfix]
    public static void RepulsionPlayableWithArgs(CardModel __instance, ref bool __result, ref UnplayableReason reason)
    {
        if (__instance?.Tags != null && __instance.Tags.Contains(ALPHANETTags.Miracle))
        {
            if (!__result && reason == UnplayableReason.EnergyCostTooHigh)
            {
                reason = UnplayableReason.None;
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(CardModel), nameof(CardModel.CanPlay), new Type[0])]
    [HarmonyPostfix]
    public static void RepulsionPlayableNoArgs(CardModel __instance, ref bool __result)
    {
        if (__instance?.Tags != null && __instance.Tags.Contains(ALPHANETTags.Miracle))
        {
            if (!__result && __instance.Owner?.PlayerCombatState != null)
            {
                int currentEnergy = __instance.Owner.PlayerCombatState.Energy;
                int actualCost = Math.Max(0, __instance.EnergyCost.GetWithModifiers(CostModifiers.All));

                bool internalPlayable = Traverse.Create(__instance).Property("IsPlayable").GetValue<bool>();

                if (actualCost > currentEnergy && internalPlayable)
                {
                    __result = true;
                }
            }
        }
    }

    // =========================================================
    // 2. 拦截扣费瞬间：记录能量差额（红光触发）或 0 费打出（金光触发）
    // =========================================================
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.SpendEnergy))]
    [HarmonyPrefix]
    public static void InterceptSpendEnergy(CardModel __instance, ref int amount)
    {
        if (__instance?.Tags != null && __instance.Tags.Contains(ALPHANETTags.Miracle))
        {
            if (__instance.Owner?.PlayerCombatState != null)
            {
                int currentEnergy = __instance.Owner.PlayerCombatState.Energy;
                var record = _playRecords.GetOrCreateValue(__instance);

                if (amount > currentEnergy)
                {
                    // 情况 A：能量不足，透支能量触发奇迹（红光）
                    record.Deficit = amount - currentEnergy;
                    record.IsZeroCostMiracle = false;
                    amount = currentEnergy;
                }
                else if (amount == 0)
                {
                    // 情况 B：当前耗能为 0 的奇迹卡，免费触发奇迹（金光）
                    record.Deficit = 0;
                    record.IsZeroCostMiracle = true;
                }
                else
                {
                    // 正常支付足额非 0 能量打出，未触发奇迹
                    record.Deficit = 0;
                    record.IsZeroCostMiracle = false;
                }
            }
        }
    }

    // =========================================================
    // 3. 奇迹卡金框与红框高亮逻辑
    // =========================================================
    [HarmonyPatch(typeof(CardModel), "get_ShouldGlowGold")]
    [HarmonyPostfix]
    public static void HandleMiracleGlowGold(CardModel __instance, ref bool __result)
    {
        if (__instance?.Tags == null || !__instance.Tags.Contains(ALPHANETTags.Miracle) || __result) return;
        if (__instance.EnergyCost == null) return;

        int actualCost = Math.Max(0, __instance.EnergyCost.GetWithModifiers(CostModifiers.All));
        if (actualCost == 0) __result = true;
    }

    [HarmonyPatch(typeof(CardModel), "get_ShouldGlowRed")]
    [HarmonyPostfix]
    public static void HandleMiracleGlowRed(CardModel __instance, ref bool __result)
    {
        if (__instance?.Tags == null || !__instance.Tags.Contains(ALPHANETTags.Miracle)) return;
        if (__instance.Owner?.PlayerCombatState == null) return;

        int currentEnergy = __instance.Owner.PlayerCombatState.Energy;
        int actualCost = Math.Max(0, __instance.EnergyCost.GetWithModifiers(CostModifiers.All));

        bool internalPlayable = Traverse.Create(__instance).Property("IsPlayable").GetValue<bool>();

        if (actualCost > currentEnergy && internalPlayable) __result = true;
    }

    // =========================================================
    // 4. 【核心架构升级】：拦截所有卡牌的 OnPlayWrapper，全自动在 OnPlay 前结算奇迹代价！
    // =========================================================
    [HarmonyPatch(typeof(CardModel), "OnPlayWrapper")]
    [HarmonyPostfix]
    public static void AutoPayMiracleOnPlayWrapper(
        CardModel __instance,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        ref Task __result)
    {
        // 如果不是奇迹牌，直接放行原 Task，零性能开销
        if (__instance?.Tags == null || !__instance.Tags.Contains(ALPHANETTags.Miracle))
            return;

        // 将原本的 OnPlayWrapper Task 包装一层：先结算奇迹扣血与事件，再执行卡牌自己的 OnPlay！
        Task originalTask = __result;
        __result = ExecuteMiracleThenPlayAsync(__instance, choiceContext, cardPlay, originalTask);
    }

    private static async Task ExecuteMiracleThenPlayAsync(
        CardModel card,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        Task originalOnPlayTask)
    {
        // 1. 在卡牌 OnPlay 真正开始前，先自动结算奇迹代价与触发计数
        await PayMiracleCostIfNeeded(card, choiceContext, cardPlay);

        // 2. 继续执行卡牌自身的 OnPlay 逻辑
        await originalOnPlayTask;
    }

    /// <summary>
    /// 结算奇迹代价（已由 OnPlayWrapper 自动调用；即使旧卡牌里还没删掉手动调用，因为有 _playRecords.Remove 保护，也绝不会重复扣血！）
    /// </summary>
    public static async Task PayMiracleCostIfNeeded(CardModel card, PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (card == null || card.Owner?.Creature == null) return;

        // 取出并立刻销毁本次出牌记录（防止任何情况下的重复结算）
        if (!_playRecords.TryGetValue(card, out var record))
            return;

        _playRecords.Remove(card);

        int deficit = record.Deficit;
        bool isZeroCost = record.IsZeroCostMiracle;

        // 如果是 0 费但并非通过 SpendEnergy 扣费（例如被其他效果直接打出且当前实际费用为 0），也兜底识别为 0 费奇迹
        if (deficit == 0 && !isZeroCost && card.EnergyCost != null)
        {
            int actualCost = Math.Max(0, card.EnergyCost.GetWithModifiers(CostModifiers.All));
            if (actualCost == 0 && cardPlay.IsFirstInSeries)
            {
                isZeroCost = true;
            }
        }

        // 既没有透支能量，也不是 0 费奇迹 -> 说明是正常交费打出，未触发奇迹效果
        if (deficit <= 0 && !isZeroCost)
            return;

        Creature playerCreature = card.Owner.Creature;
        int hpLoss = 0;

        // 1. 如果透支了能量（红光奇迹），计算并扣除生命值
        if (deficit > 0)
        {
            // 默认每点能量消耗 3 点生命；如果拥有稀有遗物《西缪回忆录 (SemusMemoirs)》，减轻为 2 点！
            int hpPerEnergy = HasSemusMemoirs(card.Owner) ? 2 : 3;
            hpLoss = deficit * hpPerEnergy;

            await CreatureCmd.Damage(
                choiceContext,
                playerCreature,
                hpLoss,
                ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move,
                card,
                cardPlay
            );
        }

        // 2. 记录“触发了一次奇迹效果”（无论是红光透支，还是 0 费金光，都算触发奇迹！）
        _combatTrackers.GetOrCreateValue(playerCreature).Increment(playerCreature.CombatState);

        // 3. 广播奇迹触发事件（供《天赐良机》《奇迹助力》等能力直接响应）
        if (OnMiracleTriggered != null)
        {
            foreach (var handler in OnMiracleTriggered.GetInvocationList().Cast<Func<PlayerChoiceContext, CardModel, CardPlay, int, Task>>())
            {
                await handler(choiceContext, card, cardPlay, hpLoss);
            }
        }
    }

    private static bool HasSemusMemoirs(MegaCrit.Sts2.Core.Entities.Players.Player? player)
    {
        if (player?.Relics == null) return false;
        // 通过类名匹配《西缪回忆录》，即便你还没创建 SemusMemoirs 类也能直接编译通过！
        var relic = player.Relics.FirstOrDefault(r => r.GetType().Name == "SemusMemoirs");
        if (relic != null)
        {
            AccessTools.Method(typeof(RelicModel), "Flash")?.Invoke(relic, null);
            return true;
        }
        return false;
    }
}