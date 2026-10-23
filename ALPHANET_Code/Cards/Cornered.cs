using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class Cornered : ModCardTemplate
{
    // 记录这张牌当前是否已经应用了“绝境减费”，确保有借有还，绝不叠算或吞掉其他减费
    private bool _hasAppliedDiscount;

    public Cornered() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    // 判定当前是否满足绝境条件（血量 <= 20）
    public bool IsCorneredActive => 
        IsMutable && 
        Owner?.Creature != null && 
        Owner.Creature.CurrentHp <= DynamicVars.GetIntOrDefault("HpThreshold", 20);

    // 满足条件时发金光
    protected override bool ShouldGlowGoldInternal => IsCorneredActive;

    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        new DamageVar(8m, ValueProp.Move),
        new RepeatVar(2),
        ModCardVars.Int("HpThreshold", 20)
    ];

    // ==========================================
    // 核心：有借有还的费用同步状态机
    // ==========================================
    public void SyncCorneredCost()
    {
        if (!IsMutable || Owner?.Creature == null) return;

        bool shouldBeFree = IsCorneredActive;

        if (shouldBeFree && !_hasAppliedDiscount)
        {
            // 进入绝境（<= 20血）：费用 -1（1费变0费）
            EnergyCost.AddUntilPlayed(-1);
            _hasAppliedDiscount = true;
        }
        else if (!shouldBeFree && _hasAppliedDiscount)
        {
            // 脱离绝境（回血到 > 20血）：把之前减掉的 1 费加回来（0费变回1费）！
            EnergyCost.AddUntilPlayed(1);
            _hasAppliedDiscount = false;
        }
    }

    // 克隆卡牌时重置标记（或同步当前状态），防止战斗中复制卡牌时状态错乱
    protected override void AfterCloned()
    {
        base.AfterCloned();
        _hasAppliedDiscount = false;
    }

    // 卡牌进入战斗或被打出时自检
    public override Task AfterCardEnteredCombat(CardModel card)
    {
        SyncCorneredCost();
        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        SyncCorneredCost();
        return Task.CompletedTask;
    }

    // ==========================================
    // 打出效果
    // ==========================================
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        // 打出后，引擎会自动清理 UntilPlayed 的费用修饰，因此我们也将自身账本复位
        _hasAppliedDiscount = false;

        int hitCount = IsCorneredActive ? DynamicVars.Repeat.IntValue : 1;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(hitCount)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Repeat.UpgradeValueBy(1m);
    }
}