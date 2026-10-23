using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class ConditionedReflex : ModCardTemplate
{
    public ConditionedReflex() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => 
    [
        CardKeyword.Retain
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        new DamageVar(7m, ValueProp.Move)
    ];

    // ==========================================
    // 核心机制 1：抽到时自动打出（修正为正确的官方生命周期钩子）
    // ==========================================
    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        // 过滤掉其他牌被抽到的情况，只有自己被抽到时才执行
        if (card != this)
        {
            return;
        }

        // 调用底层的 AutoPlay 指令。全体攻击不需单体 Target，传 null 即可
        await CardCmd.AutoPlay(choiceContext, this, null);
    }

    // ==========================================
    // 核心机制 2：打出效果与打出后成长
    // ==========================================
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 1. 对所有敌人造成伤害
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState!)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        // 2. 打出后结算成长
        GrowDamageFromStrength();
    }

    // ==========================================
    // 核心机制 3：保留后成长
    // ==========================================
    public override Task AfterFlush(PlayerChoiceContext choiceContext, Player player, IReadOnlyCollection<CardModel> flushedCards, IReadOnlyCollection<CardModel> retainedCards)
    {
        // 判定自己是否在本回合被成功保留
        if (retainedCards.Contains(this))
        {
            GrowDamageFromStrength();
        }
        return Task.CompletedTask;
    }

    // ==========================================
    // 辅助方法：统一的成长算法
    // ==========================================
    private void GrowDamageFromStrength()
    {
        if (Owner?.Creature == null) return;

        var strength = Owner.Creature.GetPower<StrengthPower>();

        // 只有当力量大于 0 时才增加伤害
        if (strength != null && strength.Amount > 0)
        {
            DynamicVars.Damage.BaseValue += strength.Amount;
        }
    }

    // ==========================================
    // 升级逻辑
    // ==========================================
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m); // 7 -> 10
    }
}