using ALPHANET.Characters;
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
public sealed class EvasiveDash : ModCardTemplate, ICriticalIndicatorCard
{
    public CriticalIndicatorType GetCriticalIndicator() => CriticalIndicatorType.Exit;

    public EvasiveDash() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => 
    [
        CardKeyword.Retain,
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        ModCardVars.Int("DexLoss", 4),
        ModCardVars.Int("Intangible", 1)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => 
    [
        HoverTipFactory.FromPower<CriticalState>(),
        HoverTipFactory.FromPower<DexterityPower>(),
        HoverTipFactory.FromPower<IntangiblePower>()
    ];

    // 处于临界状态时发金光提示
    protected override bool ShouldGlowGoldInternal => Owner?.Creature?.HasPower<CriticalState>() ?? false;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var creature = Owner!.Creature;

        // 1. 退出临界状态
        var criticalState = creature.GetPower<CriticalState>();
        if (criticalState != null)
        {
            await PowerCmd.Remove(criticalState);
        }

        // 2. 失去 4 (升级后 2) 点敏捷
        decimal dexLoss = DynamicVars["DexLoss"].BaseValue;
        await PowerCmd.Apply<DexterityPower>(choiceContext, creature, -dexLoss, creature, this);

        // 3. 获得 1 层无实体
        decimal intangibleAmt = DynamicVars["Intangible"].BaseValue;
        await PowerCmd.Apply<IntangiblePower>(choiceContext, creature, intangibleAmt, creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["DexLoss"].UpgradeValueBy(-2m); // 4 -> 2
    }
}

