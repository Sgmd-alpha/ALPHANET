using ALPHANET.Cards;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Random;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Powers;

[RegisterPower]
public sealed class HREChaosFormPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    static HREChaosFormPower()
    {
        var harmony = new Harmony("alphanet.hrechaosform.transform.patch");

        var transformMethod = AccessTools.Method(
            typeof(CardCmd),
            nameof(CardCmd.Transform),
            new Type[] { typeof(IEnumerable<CardTransformation>), typeof(Rng), typeof(CardPreviewStyle) }
        );

        if (transformMethod != null)
        {
            harmony.Patch(
                transformMethod,
                prefix: new HarmonyMethod(typeof(HREChaosFormPower), nameof(OnTransformPrefix)),
                postfix: new HarmonyMethod(typeof(HREChaosFormPower), nameof(OnTransformPostfix))
            );
        }
    }

    // 在 Transform 执行前，记录每一张即将被变化的《混沌》(StatusChaos) 所属的玩家生物（不去重，有几张算几次！）
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

    // 在 Transform 完成后，按变化的《混沌》张数逐次触发《混沌形态》
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

        // 注意：这里不调用 .Distinct()，因为每变化一张《混沌》都要触发一次！
        foreach (var creature in ownersOfTransformedChaos)
        {
            var power = creature.GetPower<HREChaosFormPower>();
            if (power != null)
            {
                await power.TriggerChaosForm(new ThrowingPlayerChoiceContext());
            }
        }

        return results;
    }

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromCard<StatusChaos>(),
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<DexterityPower>()
    ];

    // 触发条件 1：每当《混沌》被【消耗】时触发（包括主动消耗、被其他牌消耗、或回合末虚无消耗）
    public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        if (card.Owner == Owner.Player && card is StatusChaos)
        {
            await TriggerChaosForm(choiceContext);
        }
    }

    // 统一触发入口：获得 Amount 点力量和 Amount 点敏捷
    public async Task TriggerChaosForm(PlayerChoiceContext choiceContext)
    {
        if (Owner == null || !Owner.IsAlive || Amount <= 0m) return;

        Flash();
        var ctx = choiceContext ?? new ThrowingPlayerChoiceContext();

        await PowerCmd.Apply<StrengthPower>(ctx, Owner, Amount, Owner, null);
        await PowerCmd.Apply<DexterityPower>(ctx, Owner, Amount, Owner, null);
    }
}