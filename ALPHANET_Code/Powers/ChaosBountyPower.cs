using ALPHANET.Cards;
using ALPHANET.Keywords;
using ALPHANET.Tags;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Random;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Powers;

[RegisterPower]
public sealed class ChaosBountyPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private bool _triggeredThisTurn = false;

    static ChaosBountyPower()
    {
        var harmony = new Harmony("alphanet.chaosbounty.transform.patch");

        // 精准匹配官方底层方法签名：
        // Transform(IEnumerable<CardTransformation> transformations, Rng? rng, CardPreviewStyle style)
        var transformMethod = AccessTools.Method(
            typeof(CardCmd),
            nameof(CardCmd.Transform),
            new Type[] { typeof(IEnumerable<CardTransformation>), typeof(Rng), typeof(CardPreviewStyle) }
        );

        if (transformMethod != null)
        {
            harmony.Patch(
                transformMethod,
                prefix: new HarmonyMethod(typeof(ChaosBountyPower), nameof(OnTransformPrefix)),
                postfix: new HarmonyMethod(typeof(ChaosBountyPower), nameof(OnTransformPostfix))
            );
        }
    }

    // 在 Transform 执行前，检查本次要变化的卡牌中是否包含《混沌》(StatusChaos)
    private static void OnTransformPrefix(IEnumerable<CardTransformation> transformations, out List<Creature> __state)
    {
        __state = new List<Creature>();
        if (transformations == null) return;

        foreach (var item in transformations)
        {
            if (item.Original is StatusChaos && item.Original.Owner?.Creature != null)
            {
                __state.Add(item.Original.Owner.Creature);
            }
        }
    }

    // 在 Transform 完成后，若本次变化了《混沌》，立刻触发玩家身上的《混沌馈赠》
    private static void OnTransformPostfix(List<Creature> __state, ref Task<IEnumerable<CardPileAddResult>> __result)
    {
        if (__state == null || __state.Count == 0) return;

        var originalTask = __result;
        __result = ExecuteTransformAndTriggerAsync(originalTask, __state);
    }

    private static async Task<IEnumerable<CardPileAddResult>> ExecuteTransformAndTriggerAsync(
        Task<IEnumerable<CardPileAddResult>> originalTask,
        List<Creature> ownersOfTransformedChaos)
    {
        var results = await originalTask;

        foreach (var creature in ownersOfTransformedChaos.Distinct())
        {
            var power = creature.GetPower<ChaosBountyPower>();
            if (power != null && !power._triggeredThisTurn)
            {
                await power.TriggerBounty(new ThrowingPlayerChoiceContext());
            }
        }

        return results;
    }

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );

    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == Owner.Side)
        {
            _triggeredThisTurn = false;
        }
        return Task.CompletedTask;
    }

    // 触发条件 1：当《混沌》被【消耗】时触发
    public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        if (card.Owner == Owner.Player && card is StatusChaos && !_triggeredThisTurn)
        {
            await TriggerBounty(choiceContext);
        }
    }

    // 统一触发入口（消耗或变化《混沌》均会调用此处）
    public async Task TriggerBounty(PlayerChoiceContext choiceContext)
    {
        if (_triggeredThisTurn || !Owner.IsAlive) return;

        _triggeredThisTurn = true;
        Flash();

        // 赋予 Amount 层“下一张奇迹牌免费打出”的状态
        await PowerCmd.Apply<FreeMiraclePower>(choiceContext, Owner, Amount, Owner, null);
    }
}

/// <summary>
/// 辅助状态：利用官方 TryModifyEnergyCostInCombatLate 钩子，使你打出的下一张带有【奇迹】的牌耗能直接变为 0
/// </summary>
[RegisterPower]
public sealed class FreeMiraclePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/ChaosBountyPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/ChaosBountyPower.png"
    );

    private static bool IsMiracleCard(CardModel? card)
    {
        if (card == null) return false;
        return (card.Tags != null && card.Tags.Contains(ALPHANETTags.Miracle))
            || (card.Keywords != null && card.Keywords.Contains(ALPHANETKeywords.Miracle));
    }

    private bool ShouldSkip(CardModel card)
    {
        if (card.Owner?.Creature != Owner || Amount <= 0m)
            return true;

        if (!IsMiracleCard(card))
            return true;

        return card.Pile?.Type switch
        {
            PileType.Hand or PileType.Play => false,
            _ => true
        };
    }

    // ==========================================
    // 核心：直接重写官方原生战斗内改费钩子！
    // ==========================================
    public override bool TryModifyEnergyCostInCombatLate(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (ShouldSkip(card))
        {
            return false;
        }

        modifiedCost = 0m;
        return true;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay?.Card != null &&
            cardPlay.Card.Owner?.Creature == Owner &&
            !cardPlay.IsAutoPlay &&
            cardPlay.IsLastInSeries &&
            IsMiracleCard(cardPlay.Card))
        {
            Flash();
            if (Amount > 1m)
            {
                await PowerCmd.Decrement(this);
            }
            else
            {
                await PowerCmd.Remove(this);
            }
        }
    }
}