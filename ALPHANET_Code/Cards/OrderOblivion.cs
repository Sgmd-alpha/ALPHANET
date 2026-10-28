using ALPHANET.Characters;
using ALPHANET.Keywords;
using ALPHANET.States;
using ALPHANET.Tags;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class OrderOblivion : ModCardTemplate, ICriticalIndicatorCard
{
    public CriticalIndicatorType GetCriticalIndicator() => CriticalIndicatorType.Enter;

    // 目标类型对齐 Volley 使用 TargetType.RandomEnemy
    public OrderOblivion() : base(5, CardType.Attack, CardRarity.Rare, TargetType.RandomEnemy, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
        ALPHANETKeywords.Miracle
    ];

    protected override HashSet<CardTag> CanonicalTags =>
    [
        ALPHANETTags.Miracle
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(11m, ValueProp.Move),
        new DynamicVar("Hits", 6m)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<CriticalState>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;
        var creature = player.Creature;

        // 1. 进入临界状态
        await CriticalState.Enter(choiceContext, creature, creature, this, false);

        // 2. 原生引擎多段随机打击（完美防鞭尸、防无效目标）
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(DynamicVars["Hits"].IntValue)
            .FromCard(this, cardPlay)
            .TargetingRandomOpponents(CombatState!)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(1m); // 11 -> 12
        DynamicVars["Hits"].UpgradeValueBy(1m); // 6 -> 7
    }
}