using ALPHANET.Characters;
using ALPHANET.Powers;
using ALPHANET.States;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;


namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class Potential : ModCardTemplate
{
    public Potential() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self) 
    { 
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => 
    [ 
        CardKeyword.Exhaust 
    ];

    // 完美复刻官方写法：使用官方的力量变量，欺骗 UI 渲染出力量图标
    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        new PowerVar<StrengthPower>(2m)
    ];

    // 悬浮提示
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<CriticalState>(),
        HoverTipFactory.FromPower<StrengthPower>() // 显式展示官方力量提示
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;

        await CreatureCmd.TriggerAnim(player.Creature, "Buff", player.Character.CastAnimDelay);

        // 1. 进入临界状态
        await CriticalState.Enter(choiceContext, player.Creature, player.Creature, this, false);
        await CardPileCmd.Draw(choiceContext, 1, Owner);

        // 2. 完美复刻官方打法：挂载我们继承自临时力量的 PotentialPower 子类
        // 注意由于我们使用了 PowerVar<StrengthPower>，系统自动生成的变量名就是 "Strength"
        await PowerCmd.Apply<PotentialPower>(
            choiceContext, 
            player.Creature, 
            DynamicVars.Strength.BaseValue,
            player.Creature, 
            this
        );
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Strength.UpgradeValueBy(1m);
        AddKeyword(CardKeyword.Retain);
    }
}