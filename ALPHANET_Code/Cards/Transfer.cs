using ALPHANET.Characters;
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
public sealed class Transfer : ModCardTemplate
{
    public Transfer() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) 
    { 
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        new DamageVar(6m, ValueProp.Move),
        ModCardVars.Int("Draw", 1),
        ModCardVars.Int("Energy", 1)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<EnergyNextTurnPower>(),
        HoverTipFactory.FromPower<DrawCardsNextTurnPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        var player = Owner!;

        // 造成伤害，沿用官方的攻击特效
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        // 下回合抽牌
        int drawAmt = DynamicVars.GetIntOrDefault("Draw", 1);
        await PowerCmd.Apply<DrawCardsNextTurnPower>(choiceContext, player.Creature, drawAmt, player.Creature, this);

        // 下回合获得能量
        int energyAmt = DynamicVars.GetIntOrDefault("Energy", 1);
        await PowerCmd.Apply<EnergyNextTurnPower>(choiceContext, player.Creature, energyAmt, player.Creature, this);
    }

    protected override void OnUpgrade()
    {
        // 升级时伤害 +3（6 -> 9）
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}
