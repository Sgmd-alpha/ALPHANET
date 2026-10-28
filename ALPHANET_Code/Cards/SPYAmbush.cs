using ALPHANET.Characters;
using ALPHANET.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class SPYAmbush : ModCardTemplate
{
    public SPYAmbush() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<SPYAmbushPower>(25m) // 基础增伤 25%
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<SPYAmbushPower>(),
        HoverTipFactory.FromPower<SPYAmbushBuffPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;
        var creature = player.Creature;
        decimal basePercent = IsUpgraded ? 50m : 25m;

        // 给予可叠加的核心能力
        await PowerCmd.Apply<SPYAmbushPower>(choiceContext, creature, 1m, creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["SPYAmbushPower"].UpgradeValueBy(25m); // 25 -> 50
    }
}