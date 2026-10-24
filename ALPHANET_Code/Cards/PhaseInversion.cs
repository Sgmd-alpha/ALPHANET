using ALPHANET.Characters;
using ALPHANET.States;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class PhaseInversion : ModCardTemplate, ICriticalIndicatorCard
{
    public PhaseInversion() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self, true)
    {
    }
    public CriticalIndicatorType GetCriticalIndicator()
    {
        if (Owner?.Creature == null || CombatState == null)
        {
            return CriticalIndicatorType.EnterThenExit;
        }

        // 战斗中：若已处于临界状态，打出则退出（白圈）；否则打出则进入（紫圈）
        return Owner.Creature.HasPower<CriticalState>()
            ? CriticalIndicatorType.Exit
            : CriticalIndicatorType.Enter;
    }
    public CriticalIndicatorType GetPreviewCriticalIndicator() => CriticalIndicatorType.EnterThenExit;
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Ethereal
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(1)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromCard(ModelDb.Card<StatusChaos>()),
        HoverTipFactory.FromPower<CriticalState>()
    ];
    // 处于临界状态时发金光提示
    protected override bool ShouldGlowGoldInternal => Owner?.Creature?.HasPower<CriticalState>() ?? false;

    // ==========================================
    // 打出效果
    // ==========================================
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;
        var creature = player.Creature;

        // 1. 在手牌中加入一张“混沌”
        CardModel chaosCard = CombatState!.CreateCard<StatusChaos>(player);
        await CardPileCmd.AddGeneratedCardsToCombat([ chaosCard ], PileType.Hand, player);

        // 2. 抽 1 张牌
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, player);

        // 3. 如果自身处于临界状态则退出，反之则进入临界状态
        var criticalState = creature.GetPower<CriticalState>();
        if (criticalState != null)
        {
            await PowerCmd.Remove(criticalState);
        }
        else
        {
            await CriticalState.Enter(choiceContext, creature, creature, this, false);
        }
    }

    // ==========================================
    // 升级逻辑：移除虚无
    // ==========================================
    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Ethereal);
    }
}