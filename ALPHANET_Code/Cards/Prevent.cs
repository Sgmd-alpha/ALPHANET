using ALPHANET.Characters;
using ALPHANET.States;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ALPHANET.Cards;

[RegisterCard(typeof(ALPHANETCardPool))]
public sealed class Prevent : ModCardTemplate, ICriticalIndicatorCard
{
    public CriticalIndicatorType GetCriticalIndicator() => CriticalIndicatorType.Exit;
    // 不需要写 GainsBlock => true 否则 UI 会错误地在当前回合就显示格挡护盾预览
    public Prevent() : base(1, CardType.Skill, CardRarity.Common, TargetType.None) 
    { 
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    // 基础带有“保留”关键字
    public override IEnumerable<CardKeyword> CanonicalKeywords => 
    [ 
        CardKeyword.Retain 
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars => 
    [
        new BlockVar(12m, ValueProp.Move)
    ];

    // 悬浮提示
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<CriticalState>(),
        HoverTipFactory.FromPower<BlockNextTurnPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;

        await CreatureCmd.TriggerAnim(player.Creature, "Cast", player.Character.CastAnimDelay);

        var criticalState = player.Creature.GetPower<CriticalState>();
        await PowerCmd.Remove(criticalState);

        CardPile handPile = PileType.Hand.GetPile(player);
        var statusCards = handPile.Cards.Where(c => c.Type == CardType.Status).ToList();
        
        foreach (var card in statusCards)
        {
            await CardCmd.Exhaust(choiceContext, card);
        }
        decimal baseBlock = DynamicVars.Block.BaseValue;
        decimal modifiedBlock = Hook.ModifyBlock(
            player.Creature.CombatState!, 
            player.Creature, 
            baseBlock, 
            ValueProp.Move, 
            this, 
            cardPlay, 
            out _ 
        );
        modifiedBlock = Math.Max(modifiedBlock, 0m);
        await PowerCmd.Apply<BlockNextTurnPower>(choiceContext, player.Creature, modifiedBlock, player.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(4m);
    }
}