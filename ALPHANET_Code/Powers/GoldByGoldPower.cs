using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
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

    // 悬停描述中的 {Gold} 绑定到此变量，初始为 15
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new GoldVar(DefaultGoldAmount)
    ];

    public void SetInitialGold(int gold)
    {
        DynamicVars.Gold.BaseValue = gold;
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