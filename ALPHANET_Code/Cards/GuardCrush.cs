using ALPHANET.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class GuardCrush : ModCardTemplate
{
    public GuardCrush() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) 
    { 
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [

        new DamageVar(8m, ValueProp.Move), 
        new DamageVar("HeavyDamage", 24m, ValueProp.Move),

        new CalculationBaseVar(8m),
        new ExtraDamageVar(16m),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier((CardModel card, Creature? target) => 
        {
            return (target != null && target.Block > 0) ? 1 : 0;
        })
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        
        await DamageCmd.Attack(DynamicVars.CalculatedDamage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_heavy_blunt", null, "blunt_attack.mp3")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        // 升级 UI 组
        DynamicVars.Damage.UpgradeValueBy(2m);             // 8 -> 10
        DynamicVars["HeavyDamage"].UpgradeValueBy(6m);     // 24 -> 30
        
        // 升级后台组
        DynamicVars.CalculationBase.UpgradeValueBy(2m);    // 8 -> 10
        DynamicVars.ExtraDamage.UpgradeValueBy(4m);        // 16 -> 20
    }
}