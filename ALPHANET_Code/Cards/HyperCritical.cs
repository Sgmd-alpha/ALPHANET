using ALPHANET.Characters;
using ALPHANET.Powers;
using ALPHANET.States;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class HyperCritical : ModCardTemplate
{
    public HyperCritical() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("EnterStr", 1),
        ModCardVars.Int("CritStat", 1)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<CriticalState>(),
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<DexterityPower>(),
        HoverTipFactory.FromPower<HyperCriticalPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var creature = Owner!.Creature;
        decimal enterStr = DynamicVars["EnterStr"].BaseValue;
        decimal critStat = DynamicVars["CritStat"].BaseValue;

        // 1. 挂载“每次进入临界状态时获得 1 点力量”
        await PowerCmd.Apply<HyperCriticalEnterPower>(choiceContext, creature, enterStr, creature, this);

        // 2. 挂载“临界状态下额外具有 1 (升级后 2) 点力量和敏捷”
        await PowerCmd.Apply<HyperCriticalPower>(choiceContext, creature, critStat, creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["CritStat"].UpgradeValueBy(1m); // 1 -> 2
    }
}

