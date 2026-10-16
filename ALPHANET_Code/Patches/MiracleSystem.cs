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
        // 核心：账本！用于在扣除能量的瞬间，记录到底是缺了多少费
        public static Dictionary<CardModel, int> DeficitTracker = new Dictionary<CardModel, int>();

        [HarmonyPatch(typeof(CardModel), nameof(CardModel.CanPlay), 
            new Type[] { typeof(UnplayableReason), typeof(AbstractModel) }, 
            new ArgumentType[] { ArgumentType.Out, ArgumentType.Out })]
        [HarmonyPostfix]
        public static void ForcePlayableWithArgs(CardModel __instance, ref bool __result, ref UnplayableReason reason)
        {
            if (__instance?.Tags != null && __instance.Tags.Contains(ALPHANETTags.Miracle))
            {
                reason = UnplayableReason.None;
                __result = true;
            }
        }

        [HarmonyPatch(typeof(CardModel), nameof(CardModel.CanPlay), new Type[0])]
        [HarmonyPostfix]
        public static void ForcePlayableNoArgs(CardModel __instance, ref bool __result)
        {
            if (__instance?.Tags != null && __instance.Tags.Contains(ALPHANETTags.Miracle))
            {
                __result = true;
            }
        }

        // =========================================================
        // 机制重构：拦截真正扣费的瞬间！
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
                        // 1. 在能量还没被扣除前，算出真实的差额并记在账本上
                        DeficitTracker[__instance] = amount - currentEnergy;
                        
                        // 2. 强行降低本次扣除的能量上限，防止引擎把能量扣成负数
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

            if (actualCost > currentEnergy) __result = true;
        }

        public static async Task PayMiracleCostIfNeeded(CardModel card, PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            if (card == null) return;

            // 取出账本里记录的差额
            int deficit = 0;
            if (DeficitTracker.TryGetValue(card, out int recordedDeficit))
            {
                deficit = recordedDeficit;
                DeficitTracker.Remove(card); // 阅后即焚，保持内存干净
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