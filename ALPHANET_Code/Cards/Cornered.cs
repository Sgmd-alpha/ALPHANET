using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
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
    private bool _hasAppliedDiscount;

    public Cornered() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    public bool IsCorneredActive => 
        IsMutable && 
        Owner?.Creature != null && 
        Owner.Creature.CurrentHp <= DynamicVars.GetIntOrDefault("HpThreshold", 20);

    protected override bool ShouldGlowGoldInternal => IsCorneredActive;

    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        new DamageVar(8m, ValueProp.Move),
        new RepeatVar(2),
        ModCardVars.Int("HpThreshold", 20)
    ];

    // ==========================================
    // 核心：使用 AddThisCombat，让账本完全由我们自己掌控
    // ==========================================
    public void SyncCorneredCost()
    {
        if (!IsMutable || Owner?.Creature == null) return;

        bool shouldBeFree = IsCorneredActive;

        if (shouldBeFree && !_hasAppliedDiscount)
        {
            // 减费：1 变 0
            EnergyCost.AddThisCombat(-1);
            _hasAppliedDiscount = true;
        }
        else if (!shouldBeFree && _hasAppliedDiscount)
        {
            // 回血脱离绝境，把借的费用加回来：0 变 1
            EnergyCost.AddThisCombat(1);
            _hasAppliedDiscount = false;
        }
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _hasAppliedDiscount = false;
        SyncCorneredCost();
    }

    // ==========================================
    // 关键生命周期钩子：确保卡牌在手里时也能实时更新
    // ==========================================
    public override Task AfterCardEnteredCombat(CardModel card)
    {
        if (card == this) SyncCorneredCost();
        return Task.CompletedTask;
    }

    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card == this) SyncCorneredCost();
        return Task.CompletedTask;
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
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

        // 【极其重要】：删除了之前的 _hasAppliedDiscount = false;
        // 因为我们现在用的是 ThisCombat，引擎不会清理它，它必须保持 true 状态直到玩家回血！

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