using ALPHANET.Keywords;
using ALPHANET.Powers;
using ALPHANET.Systems;
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
    // 状态牌通常不可升级，重写此属性将其锁定
    public override int MaxUpgradeLevel => 0;

    public StatusChaos() : base(1, CardType.Status, CardRarity.Status, TargetType.None) 
    { 
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    // 关键字配置
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

    // 悬浮提示：显示关联的失神状态
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<AbsentMindedNextTurnPower>(1),
        HoverTipFactory.FromPower<AbsentMindedPower>(1)
    ];

    // 核心开关：告知引擎这张牌需要在手牌中监听回合结束事件
    public override bool HasTurnEndInHandEffect => true;

    // ==========================================
    // 触发条件：回合结束时仍在手牌中
    // ==========================================
    protected override async Task OnTurnEndInHand(PlayerChoiceContext choiceContext)
    {
        var player = Owner!;

        // 挂载 1 层“下回合失神”倒计时指示器
        await PowerCmd.Apply<AbsentMindedNextTurnPower>(choiceContext, player.Creature, 1, player.Creature, this);
    }

    // ==========================================
    // 触发条件：打出
    // ==========================================
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await MiracleSystem.PayMiracleCostIfNeeded(this, choiceContext, cardPlay);
        var player = Owner!;
        await PowerCmd.Apply<ChaosCostModifierPower>(choiceContext, player.Creature, 1, player.Creature, this);
    }
}