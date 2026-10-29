using ALPHANET.States;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Powers;

[RegisterPower]
public sealed class FinalResonancePower : ModPowerTemplate
{
    private const string CritDmgKey = "CritDmg";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    // 独立维护临界状态下的伤害总额
    public decimal CritAmount { get; set; } = 0m;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(CritDmgKey, 25m) // 用于能力栏悬浮提示显示
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<CriticalState>()
    ];

    /// <summary>
    /// 同步刷新能力图标悬浮提示中的临界伤害数值
    /// </summary>
    public void UpdateCritVar()
    {
        if (DynamicVars.TryGetValue(CritDmgKey, out var v))
        {
            v.BaseValue = CritAmount;
        }
    }

    // =========================================================================
    // 严格在所有回合末事件（包括手牌消耗、回合末自动打出）全部彻底结束后结算
    // =========================================================================
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        // 只有玩家的回合结束时才触发
        if (side != Owner.Side || Amount <= 0m)
            return;

        // 实时检测此刻玩家是否处于临界状态
        bool isCrit = Owner.HasPower<CriticalState>();
        decimal damageToDeal = isCrit ? CritAmount : Amount;

        if (damageToDeal > 0m && CombatState != null)
        {
            Flash();
            var ctx = choiceContext ?? new ThrowingPlayerChoiceContext();
            await CreatureCmd.Damage(ctx, CombatState.HittableEnemies, damageToDeal, ValueProp.Unpowered, Owner);
        }
    }
}