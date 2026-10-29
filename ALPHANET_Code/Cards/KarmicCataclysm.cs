using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
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
        new DamageVar(BaseDamageAmount, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Retain)
    ];

    /// <summary>
    /// 统计本场战斗中所有人“抽到 + 生成/被塞入”的状态牌与诅咒牌带来的总增伤
    /// 状态牌：每张 +2
    /// 诅咒牌：每张 +6
    /// </summary>
    public static decimal CalculateKarmaBonus()
    {
        if (!CombatManager.Instance.IsInProgress)
            return 0m;

        var history = CombatManager.Instance.History;
        if (history == null)
            return 0m;

        decimal bonus = 0m;

        // 1. 统计所有人抽到的卡牌（CardDrawnEntry）
        foreach (var entry in history.Entries.OfType<CardDrawnEntry>())
        {
            var card = entry.Card;
            if (card == null) continue;

            if (card.Type == CardType.Status)
            {
                bonus += StatusBonus;
            }
            else if (card.Type == CardType.Curse)
            {
                bonus += CurseBonus;
            }
        }

        // 2. 统计所有人中途生成/被敌人塞入牌堆的卡牌（CardGeneratedEntry）
        //    只统计新增进战斗的状态/诅咒牌，避免与开局已在卡组里的牌混淆
        foreach (var entry in history.Entries.OfType<CardGeneratedEntry>())
        {
            var card = entry.Card;
            if (card == null) continue;

            // 如果该条目是由生成(Generation)触发的
            if (card.Type == CardType.Status)
            {
                bonus += StatusBonus;
            }
            else if (card.Type == CardType.Curse)
            {
                bonus += CurseBonus;
            }
        }

        return bonus;
    }

    /// <summary>
    /// 在进入手牌、回合开始或手牌变动时刷新卡面伤害数值，显示绿字加成
    /// </summary>
    public override Task AfterCardEnteredCombat(CardModel card)
    {
        if (card == this)
        {
            RefreshDamage();
        }
        return Task.CompletedTask;
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        RefreshDamage();
        return Task.CompletedTask;
    }

    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        RefreshDamage();
        return Task.CompletedTask;
    }

    public void RefreshDamage()
    {
        if (!IsMutable) return;

        decimal currentDamage = BaseDamageAmount + CalculateKarmaBonus();
        DynamicVars.Damage.BaseValue = currentDamage;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        // 出牌瞬间做最终准确结算
        RefreshDamage();

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_heavy_blunt", null, "heavy_attack.mp3")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        // 升级获得【保留】
        AddKeyword(CardKeyword.Retain);
    }
}