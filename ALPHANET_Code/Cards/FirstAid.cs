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
public sealed class FirstAid : ModCardTemplate, ICriticalIndicatorCard
{
    public CriticalIndicatorType GetCriticalIndicator() => CriticalIndicatorType.Exit;
    public override bool GainsBlock => true;

    public FirstAid() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self) 
    { 
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    // 基础带有“消耗”
    public override IEnumerable<CardKeyword> CanonicalKeywords => 
    [ 
        CardKeyword.Exhaust 
    ];
    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        new BlockVar(4m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<CriticalState>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;
        await CreatureCmd.TriggerAnim(player.Creature, "Defend", player.Character.CastAnimDelay);
        // 1. 退出临界状态
        var criticalState = player.Creature.GetPower<CriticalState>();
        await PowerCmd.Remove(criticalState);
        await CardPileCmd.Draw(choiceContext, 1, Owner);

        // 2. 获得格挡
        await CreatureCmd.GainBlock(player.Creature, DynamicVars.Block, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
        AddKeyword(CardKeyword.Retain);
    }
}