using ALPHANET.Characters;
using ALPHANET.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class SecondaryFuse : ModCardTemplate
{
    public SecondaryFuse() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self, true)
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

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<AbsentMindedPower>(),
        HoverTipFactory.FromPower<SecondaryFusePower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var creature = Owner!.Creature;

        // 1. 施加“下回合失神”（与混沌赋予的下回合失神完美兼容合并，自动处理禁抽牌/禁能量/禁佩尔之眼）
        await PowerCmd.Apply<AbsentMindedNextTurnPower>(choiceContext, creature, 1m, creature, this);

        // 2. 施加“第二熔断”护盾（在敌人本次攻击结束后激活，持续至敌人下次攻击结束）
        await PowerCmd.Apply<SecondaryFusePower>(choiceContext, creature, 1m, creature, this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1); // 1 -> 0
    }
}