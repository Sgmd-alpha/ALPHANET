using ALPHANET.Characters;
using ALPHANET.Keywords;
using ALPHANET.Systems;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class CascadeOfMiracles : ModCardTemplate
{
    private const string CalculatedHitsKey = "CalculatedHits";

    public CascadeOfMiracles() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Retain
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move),
        // 基础攻击 1 次
        new CalculationBaseVar(1m),
        // 每次满足条件，额外增加 1 次
        new CalculationExtraVar(1m),
        // 结算条件：当前战斗中触发奇迹的总次数
        new CalculatedVar(CalculatedHitsKey).WithMultiplier((CardModel card, Creature? _) => 
        {
            return MiracleSystem.GetMiracleTriggerCount(card.Owner?.Creature);
        })
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        // 因为卡牌描述提到了奇迹，所以加上奇迹的悬浮提示
        HoverTipFactory.FromKeyword(ALPHANETKeywords.Miracle)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        // 提取 CalculatedVar 计算出的总打击次数（基础 1 次 + 奇迹触发次数）
        int hitCount = (int)((CalculatedVar)DynamicVars[CalculatedHitsKey]).Calculate(cardPlay.Target);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(hitCount)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m); // 6 -> 9
    }
}