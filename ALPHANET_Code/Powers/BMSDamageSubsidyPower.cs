using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Powers;

[RegisterPower]
public sealed class BMSDamageSubsidyPower : ModPowerTemplate
{
    private const string HealPctKey = "HealPct";
    private const string AccumulatedKey = "Accumulated";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter; // Amount 代表格挡倍率（1倍、2倍...）

    // 恢复百分比（不可叠加，只取已打出卡牌中的最高值：25 或 50）
    public decimal HealPercentage { get; set; } = 25m;

    // 本回合内已累计失去的生命值
    public int CurrentTurnHpLoss { get; private set; } = 0;

    // 结转到下回合初用于生成格挡的累计基数
    public int PendingNextTurnBlockBase { get; private set; } = 0;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(HealPctKey, 25m),
        new DynamicVar(AccumulatedKey, 0m)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
    ];

    public void SyncDynamicVars()
    {
        if (DynamicVars.TryGetValue(HealPctKey, out var healVar))
        {
            healVar.BaseValue = HealPercentage;
        }
        if (DynamicVars.TryGetValue(AccumulatedKey, out var accVar))
        {
            // 若当前处于自己回合，显示本回合正在累计的值；若处于敌方回合，显示即将转化为下回合格挡的值
            int displayVal = CombatState?.CurrentSide == Owner?.Side ? CurrentTurnHpLoss : PendingNextTurnBlockBase;
            accVar.BaseValue = displayVal;
        }
    }

    // =========================================================================
    // 1. 监听玩家在“自己的回合”失去生命值，实时累计
    // =========================================================================
    public override Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner || result == null || result.UnblockedDamage <= 0)
            return Task.CompletedTask;

        // 严格判定：必须是在“你的回合”内失去的生命值才累计
        if (CombatState != null && CombatState.CurrentSide == Owner.Side)
        {
            CurrentTurnHpLoss += result.UnblockedDamage;
            Flash();
            SyncDynamicVars();
        }

        return Task.CompletedTask;
    }

    // =========================================================================
    // 2. 你的回合结束时：恢复累计值 25% / 50% 的生命（不可叠加），并锁存下回合格挡基数
    // =========================================================================
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != Owner.Side)
            return;

        PendingNextTurnBlockBase = CurrentTurnHpLoss;
        CurrentTurnHpLoss = 0;
        SyncDynamicVars();

        if (PendingNextTurnBlockBase > 0 && Owner.IsAlive)
        {
            // 采用传统四舍五入 (AwayFromZero)：
            // 例如失去 3 点生命：25% 为 0.75 -> 回 1 血；50% 为 1.5 -> 回 2 血！
            decimal rawHeal = PendingNextTurnBlockBase * (HealPercentage / 100m);
            int healAmount = Math.Max(1, (int)Math.Round(rawHeal, MidpointRounding.AwayFromZero));

            Flash();
            await CreatureCmd.Heal(Owner, healAmount);
        }
    }

    // =========================================================================
    // 3. 你的下回合开始时（在旧格挡清空后）：获得等同于累计值 Amount 倍的格挡
    // =========================================================================
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player?.Creature != Owner)
            return;

        if (PendingNextTurnBlockBase > 0 && Amount > 0m && Owner.IsAlive)
        {
            decimal totalBlock = PendingNextTurnBlockBase * Amount;
            PendingNextTurnBlockBase = 0;
            SyncDynamicVars();

            Flash();
            await CreatureCmd.GainBlock(Owner, totalBlock, ValueProp.Unpowered, null);
        }
        else
        {
            PendingNextTurnBlockBase = 0;
            SyncDynamicVars();
        }
    }
}