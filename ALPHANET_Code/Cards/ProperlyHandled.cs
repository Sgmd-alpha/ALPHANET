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
public sealed class ProperlyHandled : ModCardTemplate, ICriticalIndicatorCard
{
    public CriticalIndicatorType GetCriticalIndicator() => CriticalIndicatorType.Exit;
    // ==========================================
    // 基础设置：1费，攻击牌，罕见，单体目标
    // ==========================================
    public ProperlyHandled() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    // 悬浮窗提示“保留”词条
    public override IEnumerable<CardKeyword> CanonicalKeywords => 
    [
        CardKeyword.Retain
    ];

    // 悬浮窗提示“临界状态”解释
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<CriticalState>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        // 基础伤害 (8点，受力量加成)
        new DamageVar(8m, ValueProp.Move),
        // 抽牌数量 (1张)
        new CardsVar(1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        var player = Owner!;

        // 1. 造成伤害
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);

        // 2. 抽牌
        if (DynamicVars.Cards.BaseValue > 0)
        {
            await CardPileCmd.Draw(choiceContext, (int)DynamicVars.Cards.BaseValue, player);
        }

        // 3. 退出临界状态
        var critPower = player.Creature.GetPower<CriticalState>();
        if (critPower != null)
        {
            await PowerCmd.Remove(critPower); 
        }
    }

    // ==========================================
    // 升级逻辑
    // ==========================================
    protected override void OnUpgrade()
    {
        // 伤害 +3 (8 -> 11)
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}