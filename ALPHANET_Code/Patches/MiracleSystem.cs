using ALPHANET.Tags;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ALPHANET.Systems
{
    [HarmonyPatch]
    public static class MiracleSystem
    {
        public static Dictionary<CardModel, int> DeficitTracker = new Dictionary<CardModel, int>();

        // =========================================================
        // 核心修正：只有在“仅仅因为没能量才不能打”时，奇迹才接管
        // =========================================================
        [HarmonyPatch(typeof(CardModel), nameof(CardModel.CanPlay), 
            new Type[] { typeof(UnplayableReason), typeof(AbstractModel) }, 
            new ArgumentType[] { ArgumentType.Out, ArgumentType.Out })]
        [HarmonyPostfix]
        public static void RepulsionPlayableWithArgs(CardModel __instance, ref bool __result, ref UnplayableReason reason)
        {
            if (__instance?.Tags != null && __instance.Tags.Contains(ALPHANETTags.Miracle))
            {
                // 只有当卡牌不能打出的原因【仅仅】是能量不足时，才强制变更为可打出！
                // 这样就完美保护了像《诡术》这种因为重写了 IsPlayable 而抛出 UnplayableReason.CardConditions 的牌。
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

                    // 【核心修复】：使用 Harmony 的 Traverse 暴力穿透 protected 限制读取属性！
                    // 这会准确调用对应卡牌（如《诡术》）自身重写后的 IsPlayable 逻辑。
                    bool internalPlayable = Traverse.Create(__instance).Property("IsPlayable").GetValue<bool>(); 

                    if (actualCost > currentEnergy && internalPlayable)
                    {
                        __result = true;
                    }
                }
            }
        }

        // =========================================================
        // 拦截真正扣费的瞬间
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
                    if (amount > currentEnergy)
                    {
                        DeficitTracker[__instance] = amount - currentEnergy;
                        amount = currentEnergy;
                    }
                    else
                    {
                        DeficitTracker[__instance] = 0;
                    }
                }
            }
        }

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

            // 【核心修复】：同样使用 Traverse 无视访问权限获取 IsPlayable
            bool internalPlayable = Traverse.Create(__instance).Property("IsPlayable").GetValue<bool>();

            // 只有当牌在机制上允许打出，且只是因为缺费时，红光才亮起
            if (actualCost > currentEnergy && internalPlayable) __result = true;
        }

        public static async Task PayMiracleCostIfNeeded(CardModel card, PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            if (card == null) return;

            int deficit = 0;
            if (DeficitTracker.TryGetValue(card, out int recordedDeficit))
            {
                deficit = recordedDeficit;
                DeficitTracker.Remove(card); 
            }

            if (deficit > 0)
            {
                int hpLoss = deficit * 3;
                await CreatureCmd.Damage(
                    choiceContext,
                    card.Owner.Creature,
                    hpLoss,
                    ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move,
                    card,
                    cardPlay
                );
            }
        }
    }
}