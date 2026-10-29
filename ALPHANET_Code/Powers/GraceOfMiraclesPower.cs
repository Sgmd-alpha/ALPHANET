using ALPHANET.Keywords;
using ALPHANET.Systems;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Powers;

[RegisterPower]
public sealed class GraceOfMiraclesPower : ModPowerTemplate
{
    private const string SubBlockKey = "SubBlock";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    // 独立记录“之后每次触发”的格挡总额（支持多张普通/升级版混打叠加）
    public decimal SubsequentBlock { get; set; } = 0m;

    private bool _triggeredThisTurn = false;
    private bool _isSubscribed = false;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(SubBlockKey, 2m)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(ALPHANETKeywords.Miracle)
    ];

    public void UpdateSubBlockVar()
    {
        if (DynamicVars.TryGetValue(SubBlockKey, out var v))
        {
            v.BaseValue = SubsequentBlock;
        }
    }

    // =========================================================================
    // 1. 订阅与注销 MiracleSystem.OnMiracleTriggered 事件（严防跨战斗内存泄漏）
    // =========================================================================
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        if (!_isSubscribed)
        {
            MiracleSystem.OnMiracleTriggered += OnMiracleTriggeredHandler;
            _isSubscribed = true;
        }
        return Task.CompletedTask;
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        Unsubscribe();
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        Unsubscribe();
        return Task.CompletedTask;
    }

    private void Unsubscribe()
    {
        if (_isSubscribed)
        {
            MiracleSystem.OnMiracleTriggered -= OnMiracleTriggeredHandler;
            _isSubscribed = false;
        }
    }

    // 每回合开始时重置“每回合首次触发”标记
    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (Owner != null && side == Owner.Side)
        {
            _triggeredThisTurn = false;
        }
        return Task.CompletedTask;
    }

    // =========================================================================
    // 2. 核心触发：当你触发奇迹效果时，使所有存活玩家获得格挡
    // =========================================================================
    private async Task OnMiracleTriggeredHandler(PlayerChoiceContext choiceContext, CardModel card, CardPlay cardPlay, int hpLost)
    {
        if (Owner == null || !Owner.IsAlive || Owner.CombatState == null)
        {
            Unsubscribe();
            return;
        }

        // 必须是本能力的拥有者触发的奇迹
        if (card?.Owner?.Creature != Owner)
            return;

        decimal blockToGain = !_triggeredThisTurn ? Amount : SubsequentBlock;
        _triggeredThisTurn = true;

        if (blockToGain <= 0m)
            return;

        Flash();
        var ctx = choiceContext ?? new ThrowingPlayerChoiceContext();

        // 获取所有存活的玩家（参考 Rally，并加上 Prepend(Owner).Distinct() 确保包含自身且不重复）
        IEnumerable<Creature> allPlayers = Owner.CombatState
            .GetTeammatesOf(Owner)
            .Prepend(Owner)
            .Where(c => c != null && c.IsAlive && c.IsPlayer)
            .Distinct();

        foreach (Creature ally in allPlayers)
        {
            await CreatureCmd.GainBlock(ally, blockToGain, ValueProp.Unpowered, null);
        }
    }
}