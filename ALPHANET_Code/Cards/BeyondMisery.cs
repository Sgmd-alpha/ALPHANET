using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class BeyondMisery : ModCardTemplate
{
    //  private const string BaseHpLossKey = "BaseHpLoss";
    private const string TotalHpLossKey = "TotalHpLoss";

    public BeyondMisery() : base(1, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];

    // 悬停提示改为：虚弱、易伤、消耗（升级后如果自带保留，引擎会自动处理或可按需追加）
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<WeakPower>(),
        HoverTipFactory.FromPower<VulnerablePower>(),
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 1. 基础生命流失：10点
        new CalculationBaseVar(10m),
        // 2. 每层虚弱/易伤提供的额外倍率：1点
        new CalculationExtraVar(1m),
        // 3. 动态计算的总生命流失（当鼠标指向敌人，或场上只有一个敌人时，自动实时计算 10 + 虚弱 + 易伤）
        new CalculatedVar(TotalHpLossKey).WithMultiplier((card, target) =>
        {
            // 如果还没有拖动指向具体敌人，但场上只剩 1 个存活敌人，也可以默认预览该敌人的数值；
            // 如果希望只有拖动指向时才变数字，保留 target 判空即可
            Creature? effectiveTarget = target;
            if (effectiveTarget == null && card.CombatState != null)
            {
                var enemies = card.CombatState.HittableEnemies;
                if (enemies.Count == 1)
                {
                    effectiveTarget = enemies[0];
                }
            }

            if (effectiveTarget == null) return 0m;

            int weakAmount = effectiveTarget.GetPowerAmount<WeakPower>();
            int vulnAmount = effectiveTarget.GetPowerAmount<VulnerablePower>();
            return weakAmount + vulnAmount;
        })
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        Creature target = cardPlay.Target;

        // 1. 计算总生命流失：基础 10 点 + 目标当前虚弱与易伤层数总和
        decimal totalHpLoss = ((CalculatedVar)DynamicVars[TotalHpLossKey]).Calculate(target);

        await CreatureCmd.Damage(
            choiceContext,
            target,
            totalHpLoss,
            ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move,
            this,
            cardPlay
        );

        // 如果敌人还没死，继续将其身上的负面状态翻倍
        if (target.IsAlive)
        {
            // 2. 提取敌人身上的所有负面效果，并合并临时状态（官方《苦难》的逻辑）
            var debuffAmounts = target.Powers
                .Where(p => p.TypeForCurrentAmount == PowerType.Debuff)
                .ToDictionary(p => (PowerModel)p.ClonePreservingMutability(), p => p.Amount);

            // 处理类似“临时力量下降”这种包裹型状态，将其合并至内在状态的数值中
            foreach (var item in debuffAmounts.ToList())
            {
                if (item.Key is ITemporaryPower temporaryPower)
                {
                    var targetPair = debuffAmounts.FirstOrDefault(p => p.Key.Id == temporaryPower.InternallyAppliedPower.Id);
                    if (targetPair.Key != null)
                    {
                        debuffAmounts[targetPair.Key] += item.Value;
                    }
                }
            }

            // 3. 将统计好的负面效果“原封不动”地再次施加给该敌人，实现完美翻倍
            foreach (var item in debuffAmounts)
            {
                if (item.Value != 0)
                {
                    PowerModel? existingPower = PowerCmd.FindExistingInstanceForStacking(item.Key, target, item.Key.Applier);
                    if (existingPower != null)
                    {
                        await PowerCmd.ModifyAmount(choiceContext, existingPower, item.Value, item.Key.Applier, this);
                    }
                    else
                    {
                        PowerModel powerToApply = (PowerModel)item.Key.ClonePreservingMutability();
                        await PowerCmd.Apply(choiceContext, powerToApply, target, item.Value, item.Key.Applier, this);
                    }
                }
            }
        }
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}