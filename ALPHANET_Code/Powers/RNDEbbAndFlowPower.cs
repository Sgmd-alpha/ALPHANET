using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Powers;

[RegisterPower]
public sealed class RNDEbbAndFlowPower : ModPowerTemplate
{
    private const string BlockGainKey = "BlockGain";

    // 记账本，记录当前这一瞬间触发了多少次
    private int _pendingTriggers = 0;
    private const string AccumulatedBlockGainKey = "AccumulatedBlockGain";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars
    {
        get
        {
            decimal displayAmount = (Owner == null || Amount <= 0m)
                ? 1m
                : Amount;

            return
            [
                new DynamicVar("Amount", displayAmount),

                // 预览默认值
                new DynamicVar(BlockGainKey, 3m * displayAmount),

                // 实际累计格挡，初始为 0
                new DynamicVar(AccumulatedBlockGainKey, 0m)
            ];
        }
    }

        internal static IHoverTip CreateSourceHoverTip(Creature? owner)
    {
        // 获取主能力的图鉴模型，用于提供来源提示的图标和标题
        var sourceModel = ModelDb.Power<RNDEbbAndFlowPower>();

        // 关键：从临时能力的实际拥有者身上获取主能力实例
        var mainPower = owner?.GetPower<RNDEbbAndFlowPower>();

        decimal displayAmount = mainPower == null
            ? 1m
            : mainPower.Amount;

        decimal displayBlock = mainPower == null
            ? 3m * displayAmount
            : mainPower.DynamicVars[BlockGainKey].BaseValue;

        // 使用主能力的普通 Description
        LocString description = sourceModel.Description;

        // 手动注入变量
        description.Add("Amount", displayAmount);
        description.Add(BlockGainKey, displayBlock);

        return new HoverTip(
            sourceModel,
            description.GetFormattedText(),
            isSmart: false
        );
    }

    // 同步主能力相关的动态描述变量
    private void RefreshDescriptionVars(decimal amount, CardModel? cardSource)
    {
        // 读取本次新增层数对应的格挡值
        decimal blockPerStack = cardSource == null
            ? 3m
            : cardSource.DynamicVars[BlockGainKey].BaseValue;

        // 只累计本次新增层数的贡献
        decimal addedBlock = amount * blockPerStack;

        DynamicVars[AccumulatedBlockGainKey].BaseValue += addedBlock;

        // 同步最终显示值
        DynamicVars[BlockGainKey].BaseValue =
            DynamicVars[AccumulatedBlockGainKey].BaseValue;

        // 同步已经存在的临时力量
        var strengthPower = Owner.GetPower<RNDEbbAndFlowTemporaryStrengthPower>();
        strengthPower?.UpdateSourceAmount(Amount);

        // 同步已经存在的临时敏捷
        var dexterityPower = Owner.GetPower<RNDEbbAndFlowTemporaryDexterityPower>();
        dexterityPower?.UpdateSourceAmount(Amount);
    }

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        await base.AfterApplied(applier, cardSource);
    }

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        await base.AfterPowerAmountChanged(
            choiceContext, power, amount, applier, cardSource);

        if (power == this)
        {
            RefreshDescriptionVars(amount, cardSource);
        }

        if (amount > 0m &&
        power.GetTypeForAmount(amount) == PowerType.Debuff &&
        power.Owner.IsEnemy &&
        applier == Owner &&
        (!(power is ITemporaryPower) || power is ShacklingPotionPower))
        {
            _pendingTriggers++;

            if (cardSource == null)
            {
                await FlushPendingTriggers(choiceContext);
            }
        }
    }
    // =========================================================================
    // 步骤 2：只有卡牌触发的，才在这里一口气把积攒的次数全部兑现！
    // =========================================================================
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await FlushPendingTriggers(choiceContext);
    }

    // 兜底机制：如果是某些极其罕见的机制（回合结束时自动触发虚弱等），在此保底结算
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player)
        {
            await FlushPendingTriggers(choiceContext);
        }
    }

    // =========================================================================
    // 步骤 3：核心统一结算逻辑，将 N 次动画合并为 1 次巨大的动画
    // =========================================================================
    private async Task FlushPendingTriggers(PlayerChoiceContext choiceContext)
    {
        if (_pendingTriggers <= 0) return;

        int triggers = _pendingTriggers;
        _pendingTriggers = 0; // 结算前先清零，防止嵌套死循环

        Flash(); // 不管触发了几次，只闪烁 1 次能力图标！
        var ctx = choiceContext ?? new ThrowingPlayerChoiceContext();

        // 算出合并后的总层数
        decimal totalAmount = Amount * triggers;

        // 一次性获得巨大数值的临时力量和敏捷
        await PowerCmd.Apply<RNDEbbAndFlowTemporaryStrengthPower>(ctx, Owner, totalAmount, Owner, null);
        await PowerCmd.Apply<RNDEbbAndFlowTemporaryDexterityPower>(ctx, Owner, totalAmount, Owner, null);

        // BlockGain 已经是所有能力层数贡献的格挡总和
        decimal totalBlock = DynamicVars[BlockGainKey].BaseValue * triggers;

        if (totalBlock > 0m)
        {
            await CreatureCmd.GainBlock(
                Owner,
                totalBlock,
                ValueProp.Unpowered,
                null
            );
        }
    }
}