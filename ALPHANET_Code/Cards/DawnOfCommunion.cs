using ALPHANET.Characters;
using ALPHANET.Keywords;
using ALPHANET.Powers;
using ALPHANET.States;
using ALPHANET.Tags;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class DawnOfCommunion : ModCardTemplate, ICriticalIndicatorCard
{
    public CriticalIndicatorType GetCriticalIndicator() => CriticalIndicatorType.Enter;

    // 稀有度设为 CardRarity.Ancient，DustyTome 会自动从角色卡池中检索并选取它
    public DawnOfCommunion() : base(2, CardType.Power, CardRarity.Ancient, TargetType.Self, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Retain,
        ALPHANETKeywords.Miracle
    ];

    protected override HashSet<CardTag> CanonicalTags =>
    [
        ALPHANETTags.Miracle
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(1),
        new EnergyVar(1)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<CriticalState>(),
        HoverTipFactory.FromPower<DawnOfCommunionPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var creature = Owner!.Creature;
        await CreatureCmd.TriggerAnim(creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
        await PowerCmd.Apply<DawnOfCommunionPower>(choiceContext, creature, 1m, creature, this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1); // 2 -> 1
    }
}