using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Powers;

[RegisterPower]
[HarmonyPatch]
public sealed class GoldByGoldPower : ModPowerTemplate
{
    private const int DefaultGoldAmount = 15;

    // 记录当前战斗中因该状态死亡、待结算进战利品的额外金币总数
    private static readonly Dictionary<Player, int> PendingGoldRewards = new();

    public override PowerType Type => PowerType.None;
    public override PowerStackType StackType => PowerStackType.Counter; // 右下角显示 Amount（即剩余回合数）

    private bool _hasTriggered = false;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );

    // 【修复】：将 CanonicalVars 改为动态属性，为图鉴假人提供默认的 Amount
    protected override IEnumerable<DynamicVar> CanonicalVars
    {
        get
        {
            // Owner == null 说明是在图鉴看牌；Amount <= 0 说明还没叠加上去。此时默认显示 3 回合。
            decimal displayTurns = (Owner == null || Amount <= 0m) ? 3m : Amount;
            return
            [
                new DynamicVar("Amount", displayTurns),
                new GoldVar(DefaultGoldAmount)
            ];
        }
    }

    public void SetInitialGold(int gold)
    {
        DynamicVars.Gold.BaseValue = gold;
    }

    // 【修复】：在状态刚挂到敌人身上时，同步真实的 Amount 到文本变量中
    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        await base.AfterApplied(applier, cardSource);
        DynamicVars["Amount"].BaseValue = Amount;
    }

    public static IHoverTip CreateHoverTip(Creature? owner, decimal cardGoldValue = DefaultGoldAmount)
    {
        // 1. 获取主能力的图鉴模型，提供图标和基础本地化文本
        var sourceModel = ModelDb.Power<GoldByGoldPower>();

        // 2. 尝试获取敌人身上实际存在的能力实例
        var mainPower = owner?.GetPower<GoldByGoldPower>();

        // 3. 决定显示的数值（身上没有则显示默认 3 回合，金币显示卡牌上的当前值）
        decimal displayTurns = (mainPower == null || mainPower.Amount <= 0m) 
            ? 3m 
            : mainPower.Amount;

        decimal displayGold = mainPower == null 
            ? cardGoldValue 
            : mainPower.DynamicVars["Gold"].BaseValue;

        // 4. 手动注入变量并渲染文本
        LocString description = sourceModel.Description;
        description.Add("Amount", displayTurns);
        description.Add("Gold", displayGold);

        return new HoverTip(
            sourceModel,
            description.GetFormattedText(),
            isSmart: false
        );
    }

    // 当再次对该敌人打出《金金计较》时：累加悬停显示的金币数，并将右下角的剩余回合数刷新回 3 回合
    public async Task StackGoldAndRefreshTurns(
        PlayerChoiceContext choiceContext,
        int additionalGold,
        decimal targetTurns,
        Creature applier,
        CardModel cardSource)
    {
        Flash();
        DynamicVars.Gold.BaseValue += additionalGold;

        decimal diff = targetTurns - Amount;
        if (diff > 0m)
        {
            await PowerCmd.ModifyAmount(choiceContext, this, diff, applier, cardSource);
        }
        
        // 【修复3】：层数刷新后，同步更新文本里的回合数
        DynamicVars["Amount"].BaseValue = Amount; 
    }

    // 敌方回合结束时，右下角剩余回合数 -1（减到 0 时 PowerCmd.Decrement 会自动移除该状态）
    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        await base.AfterSideTurnEnd(choiceContext, side, participants);

        if (side == Owner.Side)
        {
            await PowerCmd.Decrement(this);
            // 【核心修复4】：回合递减后，同步更新文本里剩余的回合数
            DynamicVars["Amount"].BaseValue = Amount; 
        }
    }

    // 敌人死亡时，将当前累计的 {Gold} 加入战利品池
    public override Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature target,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (!wasRemovalPrevented && target == Owner && !_hasTriggered)
        {
            _hasTriggered = true;
            Flash();

            Player? player = Applier?.Player ?? CombatState?.Players.FirstOrDefault();
            if (player != null)
            {
                int totalGold = DynamicVars.Gold.IntValue;
                PendingGoldRewards.TryGetValue(player, out int current);
                PendingGoldRewards[player] = current + totalGold;
            }
        }

        return Task.CompletedTask;
    }

    [HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCombatStart))]
    [HarmonyPostfix]
    public static void ClearPendingGoldOnCombatStart()
    {
        PendingGoldRewards.Clear();
    }

    [HarmonyPatch(typeof(Hook), nameof(Hook.ModifyRewards))]
    [HarmonyPostfix]
    public static void InjectGoldRewards(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        if (PendingGoldRewards.TryGetValue(player, out int totalBonusGold) && totalBonusGold > 0)
        {
            rewards.Add(new GoldReward(totalBonusGold, player));
            PendingGoldRewards.Remove(player);
        }
    }
}