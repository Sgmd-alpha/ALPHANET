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
    // 1. 仿照官方 Thrash，使用带有 AssertMutable() 的属性来追踪额外伤害
    private decimal _extraDamage;
    private decimal ExtraDamage
    {
        get => _extraDamage;
        set
        {
            AssertMutable(); // 通知底层引擎：卡牌状态已发生变化！
            _extraDamage = value;
        }
    }

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

    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card != this) return;
        await CardCmd.AutoPlay(choiceContext, this, null);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState!)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        GrowDamageFromStrength();
    }

    public override Task AfterFlush(PlayerChoiceContext choiceContext, Player player, IReadOnlyCollection<CardModel> flushedCards, IReadOnlyCollection<CardModel> retainedCards)
    {
        if (retainedCards.Contains(this))
        {
            GrowDamageFromStrength();
        }
        return Task.CompletedTask;
    }

    private void GrowDamageFromStrength()
    {
        if (Owner?.Creature == null) return;

        var strength = Owner.Creature.GetPower<StrengthPower>();

        if (strength != null && strength.Amount > 0)
        {
            // 2. 像 Thrash 一样，直接给 BaseValue 加上伤害，并同步给 ExtraDamage 记录
            DynamicVars.Damage.BaseValue += strength.Amount;
            ExtraDamage += strength.Amount;
        }
    }

    // 3. 必须重写 AfterCloned，防止卡牌在手牌、弃牌堆间移动或被复制时，丢失原本的额外伤害
    protected override void AfterCloned()
    {
        base.AfterCloned();
        if (ExtraDamage > 0)
        {
            DynamicVars.Damage.BaseValue += ExtraDamage;
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m); // 7 -> 10
    }

    protected override void AfterDowngraded()
    {
        base.AfterDowngraded();
        DynamicVars.Damage.BaseValue += ExtraDamage;
    }
}