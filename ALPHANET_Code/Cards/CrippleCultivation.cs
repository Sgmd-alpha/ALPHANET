using ALPHANET.Characters;
using ALPHANET.Systems;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class CrippleCultivation : ModCardTemplate
{
    public CrippleCultivation() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy, true)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => 
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<ArtifactPower>(),
        HoverTipFactory.FromPower<PlatingPower>(),
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<WeakPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        new DamageVar(4m, ValueProp.Move),
        new DynamicVar("Weak", 1m),

        // 实时预测指向的目标力量会变成多少
        ModCardVars.Computed(
            "TargetNewStr",
            static ctx => ctx.Target != null ? CultivationTrackerSystem.PredictNewStrength(ctx.Target) : 0m,
            baseValue: 0m)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        var target = cardPlay.Target;

        // 1. 造成伤害
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        // 2. 清除人工制品
        if (target.HasPower<ArtifactPower>())
        {
            await PowerCmd.Remove<ArtifactPower>(target);
        }

        // 3. 使其格挡变为 0
        if (target.Block > 0)
        {
            await CreatureCmd.LoseBlock(choiceContext, target, target.Block, Owner.Creature);
        }

        // 4. 清除覆甲
        if (target.HasPower<PlatingPower>())
        {
            await PowerCmd.Remove<PlatingPower>(target);
        }

        // 5. 清除其在战斗中获得的力量
        await CultivationTrackerSystem.CrippleTargetStrength(target);

        // 6. 给予虚弱
        int weakAmount = DynamicVars["Weak"].IntValue;
        if (weakAmount > 0)
        {
            await PowerCmd.Apply<WeakPower>(choiceContext, target, weakAmount, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m); // 4 -> 6
        DynamicVars["Weak"].UpgradeValueBy(1m); // 1 -> 2
    }
}