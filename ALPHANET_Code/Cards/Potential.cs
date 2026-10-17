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

    // 基础带有“消耗”
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
        HoverTipFactory.FromPower<StrengthPower>() 
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;

        await CreatureCmd.TriggerAnim(player.Creature, "Buff", player.Character.CastAnimDelay);

        // 1. 无条件获得临时力量 (对应卡面文本的第一句)
        await PowerCmd.Apply<PotentialPower>(
            choiceContext, 
            player.Creature, 
            DynamicVars.Strength.BaseValue,
            player.Creature, 
            this
        );

        // 2. 检查玩家当前是否已经处于临界状态
        var criticalState = player.Creature.GetPower<CriticalState>();
        
        if (criticalState != null)
        {
            // 若处于临界状态，抽2张牌
            await CardPileCmd.Draw(choiceContext, 2, player);
        }
        else
        {
            // 否则，进入临界状态并抽1张牌
            await CriticalState.Enter(choiceContext, player.Creature, player.Creature, this, false);
            await CardPileCmd.Draw(choiceContext, 1, player);
        }
    }

    protected override void OnUpgrade()
    {
        // 升级时：临时力量增加 2 (2 -> 4)
        DynamicVars.Strength.UpgradeValueBy(2m);
        // 动态追加“保留”关键字
        AddKeyword(CardKeyword.Retain);
    }
}