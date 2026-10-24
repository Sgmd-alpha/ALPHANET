using ALPHANET.Keywords;
using ALPHANET.Powers;
using ALPHANET.Tags;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;
[RegisterCard(typeof(StatusCardPool))]
public sealed class StatusChaos : ModCardTemplate
{
    // 状态牌不可升级
    public override int MaxUpgradeLevel => 0;

    public StatusChaos() : base(1, CardType.Status, CardRarity.Status, TargetType.None, true) 
    { 
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => 
    [ 
        CardKeyword.Ethereal,
        ALPHANETKeywords.Miracle,
        CardKeyword.Exhaust
    ];

    protected override HashSet<CardTag> CanonicalTags => 
    [
        ALPHANETTags.Miracle
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<AbsentMindedNextTurnPower>(1),
        HoverTipFactory.FromPower<AbsentMindedPower>(1)
    ];

    // 告知引擎这张牌需要在手牌中监听回合结束事件
    public override bool HasTurnEndInHandEffect => true;

    // ==========================================
    // 触发条件：回合结束时仍在手牌中
    // ==========================================
    protected override async Task OnTurnEndInHand(PlayerChoiceContext choiceContext)
    {
        var creature = Owner!.Creature;

        // 挂载“下回合失神”倒计时指示器
        await PowerCmd.Apply<AbsentMindedNextTurnPower>(choiceContext, creature, 1m, creature, this);
    }

    // ==========================================
    // 触发条件：打出（奇迹扣血已由 MiracleSystem 自动接管）
    // ==========================================
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var creature = Owner!.Creature;

        // 打出后本回合所有混沌的耗能增加 1
        await PowerCmd.Apply<ChaosCostModifierPower>(choiceContext, creature, 1m, creature, this);
    }
}