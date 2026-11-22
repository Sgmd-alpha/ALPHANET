using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
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
public sealed class KarmicCataclysm : ModCardTemplate
{
    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;
    private const decimal BaseDamageAmount = 12m;
    private const decimal StatusBonus = 2m;
    private const decimal CurseBonus = 6m;

    public KarmicCataclysm() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CalculationBaseVar(BaseDamageAmount),
        new CalculationExtraVar(1m),
        new CalculatedDamageVar(ValueProp.Move)
            .WithMultiplier((card, target) => CalculateKarmaBonus())
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Retain)
    ];

    /// <summary>
    /// 统计本场战斗中所有人“抽到 + 生成/被塞入”的状态牌与诅咒牌带来的总增伤
    /// 状态牌：每张 +2；诅咒牌：每张 +6
    /// </summary>
    public static decimal CalculateKarmaBonus()
    {
        if (!CombatManager.Instance.IsInProgress)
            return 0m;

        var history = CombatManager.Instance.History;
        if (history == null)
            return 0m;

        decimal bonus = 0m;

        foreach (var entry in history.Entries.OfType<CardDrawnEntry>())
        {
            var card = entry.Card;
            if (card == null) continue;

            if (card.Type == CardType.Status)
                bonus += StatusBonus;
            else if (card.Type == CardType.Curse)
                bonus += CurseBonus;
        }

        foreach (var entry in history.Entries.OfType<CardGeneratedEntry>())
        {
            var card = entry.Card;
            if (card == null) continue;

            if (card.Type == CardType.Status)
                bonus += StatusBonus;
            else if (card.Type == CardType.Curse)
                bonus += CurseBonus;
        }

        return bonus;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        await DamageCmd.Attack(DynamicVars.CalculatedDamage.Calculate(cardPlay.Target))
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_heavy_blunt", null, "heavy_attack.mp3")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}