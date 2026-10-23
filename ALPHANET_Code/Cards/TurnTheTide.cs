using ALPHANET.Characters;
using ALPHANET.States;
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
public sealed class TurnTheTide : ModCardTemplate, ICriticalIndicatorCard
{
    public CriticalIndicatorType GetCriticalIndicator() => CriticalIndicatorType.Exit;

    public TurnTheTide() : base(0, CardType.Attack, CardRarity.Ancient, TargetType.AllEnemies)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => [ CardKeyword.Retain ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<CriticalState>()
    ];

    // 处于临界状态时发金光
    protected override bool ShouldGlowGoldInternal => Owner?.Creature?.HasPower<CriticalState>() ?? false;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;

        // 1. 获取临界状态
        var criticalState = player.Creature.GetPower<CriticalState>();

        // 2. 造成 8(14) 点伤害，若处于临界状态则再造成一次 8(14) 点伤害
        int attackTimes = criticalState != null ? 2 : 1;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(attackTimes)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState!)
            .Execute(choiceContext);

        // 3. 退出临界状态
        await PowerCmd.Remove(criticalState);
    }

    protected override void OnUpgrade()
    {
        // 8 -> 14 (+6)
        DynamicVars.Damage.UpgradeValueBy(6m);
    }
}