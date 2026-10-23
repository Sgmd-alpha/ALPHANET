using ALPHANET.Characters;
using ALPHANET.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class Accumulate : ModCardTemplate
{
    public Accumulate() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 注册一个数值，每次触发要抽几张牌
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(1)
    ];

    // 仿照官方做法，给这张牌加上“保留”的悬停提示说明，方便玩家阅读，ExtraHoverTips 改为 AdditionalHoverTips
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => 
    [
        HoverTipFactory.FromKeyword(CardKeyword.Retain)
    ];

    protected override HashSet<CardTag> CanonicalTags => new();

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 播放能力牌强化动画
        await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
        
        // 给玩家挂上“积少成多”的持续状态
        await PowerCmd.Apply<AccumulatePower>(
            choiceContext, 
            Owner.Creature, 
            DynamicVars.Cards.BaseValue, 
            Owner.Creature, 
            this);
    }

    protected override void OnUpgrade()
    {
        // 升级：耗能 -1
        EnergyCost.UpgradeBy(-1);
    }
}