using ALPHANET.Characters;
using ALPHANET.Powers;
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
public sealed class MicroTwitch : ModCardTemplate
{
    public MicroTwitch() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) 
    { 
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    // 显式声明伤害、力量流失、虚弱层数的变量
    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        new DamageVar(6m, ValueProp.Move),
        ModCardVars.Int("StrengthLoss", 2),
        ModCardVars.Int("Weak", 1)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<WeakPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 沿用你上张牌学到的官方防空机制
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        
        var player = Owner!;
        var target = cardPlay.Target;

        // 1. 造成伤害（播放基础攻击特效）
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        // 2. 本回合失去力量 (挂载我们刚刚写的负面临时力量)
        int strLossAmt = DynamicVars.GetIntOrDefault("StrengthLoss", 2);
        await PowerCmd.Apply<MicroTwitchPower>(choiceContext, target, strLossAmt, player.Creature, this);

        // 3. 给予虚弱
        int weakAmt = DynamicVars.GetIntOrDefault("Weak", 1);
        await PowerCmd.Apply<WeakPower>(choiceContext, target, weakAmt, player.Creature, this);
    }

    protected override void OnUpgrade()
    {
        // 全面数值提升
        DynamicVars.Damage.UpgradeValueBy(1m);
        DynamicVars["StrengthLoss"].UpgradeValueBy(1m);
        DynamicVars["Weak"].UpgradeValueBy(1m);
    }
}