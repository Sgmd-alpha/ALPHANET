using System.Runtime.CompilerServices;
using ALPHANET.States;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Powers;

[RegisterPower]
[HarmonyPatch]
public sealed class TCSPandemoniumPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    // 记录每张诅咒牌最初的原生状态（用于区分“原本不可打出的诅咒”与“原本可打出的诅咒”，并在退出临界时还原）
    private class CurseOriginalRecord
    {
        public bool WasOriginallyUnplayable;
        public bool HadOriginalExhaust;
        public int OriginalCanonicalCost;
        public bool IsCurrentlyUnlockedByPandemonium;
    }

    private static readonly ConditionalWeakTable<CardModel, CurseOriginalRecord> _curseRecords = new();

    // 静态构造函数：确保无论主程序如何加载，本类的 Harmony 补丁 100% 被强制注册！
    // static TCSPandemoniumPower()
    // {
    //     try
    //     {
    //         var harmony = new Harmony("alphanet.tcspandemonium.system.patch");
    //         harmony.PatchAll();
    //     }
    //     catch (Exception ex)
    //     {
    //         Godot.GD.PrintErr($"[ALPHANET] Failed to patch TCSPandemoniumPower: {ex}");
    //     }
    // }

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<CriticalState>(),
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
        HoverTipFactory.FromKeyword(CardKeyword.Unplayable)
    ];

    /// <summary>
    /// 获取或初始化一张诅咒牌的原始属性记录
    /// </summary>
    private static CurseOriginalRecord GetOrInitRecord(CardModel card)
    {
        return _curseRecords.GetValue(card, c =>
        {
            bool hasUnplayable = (c.Keywords != null && c.Keywords.Contains(CardKeyword.Unplayable))
                              || (c.CanonicalKeywords != null && c.CanonicalKeywords.Contains(CardKeyword.Unplayable));
            bool hasExhaust = (c.Keywords != null && c.Keywords.Contains(CardKeyword.Exhaust))
                           || (c.CanonicalKeywords != null && c.CanonicalKeywords.Contains(CardKeyword.Exhaust));
            int canonicalCost = c.EnergyCost != null ? c.EnergyCost.Canonical : -1;

            return new CurseOriginalRecord
            {
                WasOriginallyUnplayable = hasUnplayable || canonicalCost < 0,
                HadOriginalExhaust = hasExhaust,
                OriginalCanonicalCost = canonicalCost,
                IsCurrentlyUnlockedByPandemonium = false
            };
        });
    }

    /// <summary>
    /// 判定指定卡牌的拥有者当前是否激活了【群魔乱舞 + 临界状态】
    /// </summary>
    public static bool IsPandemoniumActiveFor(CardModel? card)
    {
        if (card == null || !card.IsMutable)
            return false;

        var creature = card.Owner?.Creature;
        if (creature == null || !creature.IsAlive || creature.CombatState == null)
            return false;

        return creature.HasPower<TCSPandemoniumPower>() && creature.HasPower<CriticalState>();
    }

    /// <summary>
    /// 动态同步玩家手牌中所有诅咒牌的词条与费用状态：
    /// 处于【群魔乱舞 + 临界状态】时，直接拔除 Unplayable 词条、赋予 Exhaust 词条，并将原本不可打出诅咒牌的费用设为 0！
    /// </summary>
    public static void SyncHandCursesForPlayer(Player? player)
    {
        if (player?.Creature == null || player.Creature.CombatState == null)
            return;

        bool isActive = player.Creature.IsAlive
                     && player.Creature.HasPower<TCSPandemoniumPower>()
                     && player.Creature.HasPower<CriticalState>();

        var handCards = PileType.Hand.GetPile(player).Cards;
        foreach (var card in handCards)
        {
            if (card == null || !card.IsMutable || card.Type != CardType.Curse)
                continue;

            var record = GetOrInitRecord(card);

            if (isActive)
            {
                if (!record.IsCurrentlyUnlockedByPandemonium)
                {
                    record.IsCurrentlyUnlockedByPandemonium = true;

                    // 1. 如果原本不可打出，拔除 Unplayable 词条并将费用改为 0 费！
                    if (record.WasOriginallyUnplayable)
                    {
                        card.RemoveKeyword(CardKeyword.Unplayable);
                        card.EnergyCost?.SetThisCombat(0);
                    }

                    // 2. 赋予 Exhaust 词条，确保打出后由引擎原生直接送入消耗堆！
                    if (!record.HadOriginalExhaust)
                    {
                        card.AddKeyword(CardKeyword.Exhaust);
                    }
                }
            }
            else
            {
                if (record.IsCurrentlyUnlockedByPandemonium)
                {
                    record.IsCurrentlyUnlockedByPandemonium = false;

                    // 退出临界状态时，还原还没有打出的诅咒牌属性
                    if (record.WasOriginallyUnplayable)
                    {
                        card.AddKeyword(CardKeyword.Unplayable);
                        card.EnergyCost?.SetThisCombat(record.OriginalCanonicalCost);
                    }

                    if (!record.HadOriginalExhaust)
                    {
                        card.RemoveKeyword(CardKeyword.Exhaust);
                    }
                }
            }
        }
    }

    // =========================================================================
    // 生命周期钩子：在能力施加、能力变动、抽牌、换牌堆时实时同步手牌诅咒状态
    // =========================================================================
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        SyncHandCursesForPlayer(Owner?.Player);
        return Task.CompletedTask;
    }

    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        SyncHandCursesForPlayer(Owner?.Player);
        return Task.CompletedTask;
    }

    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        SyncHandCursesForPlayer(Owner?.Player);
        return Task.CompletedTask;
    }

    // 打出诅咒牌后：抽取 Amount 张牌（并兜底确保其进入消耗堆）
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var card = cardPlay?.Card;
        if (card == null || card.Owner?.Creature != Owner || card.Type != CardType.Curse)
            return;

        if (!Owner.HasPower<CriticalState>())
            return;

        Flash();
        var ctx = choiceContext ?? new ThrowingPlayerChoiceContext();

        // 确保这张诅咒牌带有 Exhaust 词条，使引擎在 OnPlayWrapper 结束时将其移入消耗堆
        if (card.Keywords != null && !card.Keywords.Contains(CardKeyword.Exhaust))
        {
            card.AddKeyword(CardKeyword.Exhaust);
        }

        int drawCount = Amount;
        if (drawCount > 0 && Owner.Player != null)
        {
            await CardPileCmd.Draw(ctx, drawCount, Owner.Player);
        }
    }

    // =========================================================================
    // 仿照 MiracleSystem 的全局 Harmony 补丁：彻底打通诅咒牌出牌与扣费管线！
    // =========================================================================

    // 1. 修正：使用官方真实的 Hook.AfterPowerAmountChanged 签名
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterPowerAmountChanged))]
    [HarmonyPostfix]
    public static void OnAnyPowerChanged(PowerModel power)
    {
        var creature = power?.Owner;
        if (creature?.Player != null)
        {
            SyncHandCursesForPlayer(creature.Player);
        }
    }

    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardChangedPiles))]
    [HarmonyPostfix]
    public static void OnAnyCardChangedPiles(CardModel? card)
    {
        if (card != null && card.IsMutable && card.Owner != null)
        {
            SyncHandCursesForPlayer(card.Owner);
        }
    }

    // 2. 强行解锁 CardModel.CanPlay (带参数重载) —— 仿照 MiracleSystem.RepulsionPlayableWithArgs
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.CanPlay),
        new Type[] { typeof(UnplayableReason), typeof(AbstractModel) },
        new ArgumentType[] { ArgumentType.Out, ArgumentType.Out })]
    [HarmonyPostfix]
    public static void CursePlayableWithArgs(CardModel __instance, ref bool __result, ref UnplayableReason reason, ref AbstractModel? preventer)
    {
        if (__instance == null || !__instance.IsMutable || __instance.Type != CardType.Curse)
            return;

        if (!IsPandemoniumActiveFor(__instance))
            return;

        // 实时确保手牌中的这张诅咒牌已被解锁
        SyncHandCursesForPlayer(__instance.Owner);

        var record = GetOrInitRecord(__instance);

        if (!__result)
        {
            // A. 如果是原本不可打出的诅咒牌（如 Regret），无条件免费放行！
            if (record.WasOriginallyUnplayable)
            {
                reason = UnplayableReason.None;
                preventer = null;
                __result = true;
            }
            // B. 如果是原本可打出的有费诅咒牌（如 1 费的 SporeMind），只要能量够就放行！
            else if (__instance.Owner?.PlayerCombatState != null)
            {
                int currentEnergy = __instance.Owner.PlayerCombatState.Energy;
                int actualCost = Math.Max(0, __instance.EnergyCost.GetWithModifiers(CostModifiers.All));
                if (currentEnergy >= actualCost)
                {
                    reason = UnplayableReason.None;
                    preventer = null;
                    __result = true;
                }
            }
        }
    }

    // 3. 强行解锁 CardModel.CanPlay (无参数重载) —— 仿照 MiracleSystem.RepulsionPlayableNoArgs
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.CanPlay), new Type[0])]
    [HarmonyPostfix]
    public static void CursePlayableNoArgs(CardModel __instance, ref bool __result)
    {
        if (__instance == null || !__instance.IsMutable || __instance.Type != CardType.Curse)
            return;

        if (!IsPandemoniumActiveFor(__instance))
            return;

        SyncHandCursesForPlayer(__instance.Owner);

        var record = GetOrInitRecord(__instance);

        if (!__result)
        {
            if (record.WasOriginallyUnplayable)
            {
                __result = true;
            }
            else if (__instance.Owner?.PlayerCombatState != null)
            {
                int currentEnergy = __instance.Owner.PlayerCombatState.Energy;
                int actualCost = Math.Max(0, __instance.EnergyCost.GetWithModifiers(CostModifiers.All));
                if (currentEnergy >= actualCost)
                {
                    __result = true;
                }
            }
        }
    }

    // 4. 拦截扣费瞬间：原本不可打出的诅咒牌强制 0 费，绝不扣能量也绝不传出 -1 导致异常！
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.SpendEnergy))]
    [HarmonyPrefix]
    public static void InterceptCurseSpendEnergy(CardModel __instance, ref int amount)
    {
        if (__instance != null && __instance.IsMutable && __instance.Type == CardType.Curse)
        {
            var record = GetOrInitRecord(__instance);
            if (record.WasOriginallyUnplayable || amount < 0)
            {
                amount = 0;
            }
        }
    }
}