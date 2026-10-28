using ALPHANET.Characters;
using ALPHANET.States;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class ForeseenOutcome : ModCardTemplate, ICriticalIndicatorCard
{
    public CriticalIndicatorType GetCriticalIndicator() => CriticalIndicatorType.Enter;

    public ForeseenOutcome() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy, true)
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
        new DamageVar(10m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<CriticalState>(),
        HoverTipFactory.FromCard<TokenFuture>(upgrade: IsUpgraded)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        var player = Owner!;
        var creature = player.Creature;

        // 1. 造成 10 点伤害
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        // 2. 进入临界状态
        await CriticalState.Enter(choiceContext, creature, creature, this, false);

        // 3. 将一张“未来”添加到手牌（若本牌升级，则生成“未来+”）
        CardModel futureCard = CombatState!.CreateCard<TokenFuture>(player);
        if (IsUpgraded)
        {
            CardCmd.Upgrade(futureCard);
        }

        await CardPileCmd.AddGeneratedCardToCombat(futureCard, PileType.Hand, player);
    }

    protected override void OnUpgrade()
    {
        // 伤害保持 10 点不变，生成的卡牌升级逻辑由 OnPlay 内 IsUpgraded 与 descriptionUpgraded 处理
    }
}