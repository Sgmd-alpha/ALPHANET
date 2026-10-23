using ALPHANET.Characters;
using ALPHANET.Keywords;
using ALPHANET.States;
using ALPHANET.Tags;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class PsionicBladedance : ModCardTemplate, ICriticalIndicatorCard
{
    public CriticalIndicatorType GetCriticalIndicator() => CriticalIndicatorType.Enter;
    public PsionicBladedance() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );
    public override IEnumerable<CardKeyword> CanonicalKeywords => 
    [ 
        ALPHANETKeywords.Miracle,
    ];
    protected override HashSet<CardTag> CanonicalTags => 
    [
        ALPHANETTags.Miracle
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<CriticalState>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 1. 基础伤害：2点（升级后 3点）
        new CalculationBaseVar(2m),
        
        // 2. 每次触发增加的伤害：2点（升级后 3点）
        new ExtraDamageVar(2m),
        
        // 3. 攻击次数：2次
        new RepeatVar(2),

        // 4. 动态总伤害计算：Base + Extra * 本场战斗触发进入临界状态效果的次数
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier((CardModel card, Creature? _) => 
            CriticalState.GetEnterTriggerCount(card.Owner?.Creature))
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var playerCreature = Owner!.Creature;

        // 1. 先触发“进入临界状态”的效果（此时计数器会立刻 +1，若已在临界状态还会按你写的逻辑扣1点最大生命）
        await CriticalState.Enter(choiceContext, playerCreature, playerCreature, this, false);

        // 2. 对所有敌人造成 CalculatedDamage 点伤害 2 次（自动吃到刚才第 1 步增加的那次伤害！）
        await DamageCmd.Attack(DynamicVars.CalculatedDamage)
            .WithHitCount(DynamicVars.Repeat.IntValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState!)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        // 基础伤害 2 -> 3
        DynamicVars.CalculationBase.UpgradeValueBy(1m);
        // 每次触发增加的伤害 2 -> 3
        DynamicVars.ExtraDamage.UpgradeValueBy(1m);
    }
}